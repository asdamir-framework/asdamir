// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.ErrorHandling.Abstractions;
using Asdamir.Core.ErrorHandling.Extensions;
using Polly;
using Polly.Retry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Asdamir.Data.Configuration;

/// <summary>
/// Dynamic configuration provider that loads configuration from a custom source and periodically reloads it.
/// <list type="bullet">
/// <item><b>The first load is synchronous</b> — <see cref="Load"/> returns only after one load attempt, so
/// values read while the host is still being built (options binding, <c>builder.Configuration["…"]</c>) see
/// the database values instead of an empty set.</item>
/// <item><b>Last-known-good</b> — a load that throws never replaces the current data; the previous values
/// stay in effect and the failure is retried (Polly, 3 attempts with exponential back-off, then again on the
/// next refresh tick). A loader must therefore THROW on failure, not return an empty dictionary.</item>
/// <item>Keys are case-insensitive, like every other <c>IConfiguration</c> provider.</item>
/// </list>
/// If the very first load fails the host still starts (with no DB-backed values) and the refresh loop
/// retries immediately; the failure is kept in <see cref="LastLoadError"/> and logged as soon as a logger
/// is available.
/// </summary>
public sealed class DynamicConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly DynamicConfigurationSource _source;
    private readonly CancellationTokenSource _cts = new();
    private readonly AsyncRetryPolicy _retryPolicy;
    private ILogger _logger;
    private string _reportedDroppedKeys = string.Empty;
    private bool _pendingErrorUnreported;
    private Task? _loop;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicConfigurationProvider"/> class.
    /// </summary>
    /// <param name="source">The configuration source containing the loader and refresh interval.</param>
    /// <param name="logger">Optional logger for diagnostics. If null, NullLogger is used until
    /// <see cref="AttachLogger"/> supplies one.</param>
    public DynamicConfigurationProvider(
        DynamicConfigurationSource source,
        ILogger<DynamicConfigurationProvider>? logger = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _logger = logger ?? NullLogger<DynamicConfigurationProvider>.Instance;
        Data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        // Polly retry policy: 3 attempts with exponential backoff (refresh loop only — never the
        // synchronous first load, which must not hold host start-up for 14 seconds).
        _retryPolicy = Policy
            .Handle<Exception>(ex => ex is not OperationCanceledException)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        exception,
                        "Configuration load failed (Attempt {RetryCount}/3). Retrying in {RetryDelay}s",
                        retryCount,
                        timeSpan.TotalSeconds);
                });
    }

    /// <summary>
    /// The exception of the most recent failed load, or null when the most recent load succeeded. While it
    /// is non-null the provider is serving last-known-good (or, before any success, no) values.
    /// </summary>
    public Exception? LastLoadError { get; private set; }

    /// <summary>UTC time of the most recent successful load, or null if none has succeeded yet.</summary>
    public DateTime? LastSuccessfulLoadUtc { get; private set; }

    /// <summary>
    /// Supply the real logger once the host's logging exists (configuration is built before it). Reports a
    /// load failure and any dropped protected keys that happened while no logger was attached.
    /// </summary>
    public void AttachLogger(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (_pendingErrorUnreported && LastLoadError is { } error)
        {
            _pendingErrorUnreported = false;
            _logger.LogError(error,
                "DB-backed configuration could not be loaded; the host is running on last-known-good values ({KeyCount} keys)",
                Data.Count);
        }
        _reportedDroppedKeys = string.Empty;   // re-report through the real logger
        _reportedUndecryptableKeys = string.Empty;
        _reportedPlaintextSecretKeys = string.Empty;
        ReportDroppedProtectedKeys();
    }

    /// <summary>
    /// Performs the first load <b>synchronously</b> (one attempt), then starts the periodic refresh loop.
    /// A failed first load does not throw: the provider starts empty, remembers the failure in
    /// <see cref="LastLoadError"/> and the loop retries at once.
    /// </summary>
    public override void Load()
    {
        if (_loop is not null) return;   // Load() is called once per ConfigurationRoot build; guard a repeat

        var firstLoadSucceeded = false;
        try
        {
            // ConfigurationProvider.Load is a synchronous contract; Task.Run keeps the loader off any
            // captured context so this cannot deadlock.
            var dict = Task.Run(() => _source.Loader(_cts.Token)).GetAwaiter().GetResult(); // audit-lint:ignore AUD008 — IConfigurationProvider.Load is sync by contract; Task.Run avoids context capture
            firstLoadSucceeded = Apply(dict, raiseReload: false);
        }
        catch (Exception ex)
        {
            RecordFailure(ex, "Initial configuration load failed; starting without DB-backed values and retrying");
        }

        _logger.LogInformation(
            "DynamicConfigurationProvider starting with {RefreshInterval}s refresh interval",
            _source.RefreshInterval.TotalSeconds);
        _loop = Task.Run(() => LoopAsync(skipFirstLoad: firstLoadSucceeded));
    }

    /// <summary>Swap in a freshly loaded dictionary. Returns false (and changes nothing) for a null result.</summary>
    private bool Apply(Dictionary<string, string?>? dict, bool raiseReload)
    {
        if (dict == null)
        {
            // A null result is a loader bug, not "the table is empty" — keep what we have.
            _logger.LogWarning("Configuration loader returned null dictionary; keeping the current values");
            return false;
        }

        // Normalise to case-insensitive keys whatever comparer the loader used.
        var next = new Dictionary<string, string?>(dict.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in dict) next[kvp.Key] = kvp.Value;

        var current = Data;
        var changed = next.Count != current.Count;
        if (!changed)
        {
            foreach (var kvp in next)
            {
                if (!current.TryGetValue(kvp.Key, out var existing) || !string.Equals(existing, kvp.Value, StringComparison.Ordinal))
                {
                    changed = true;
                    break;
                }
            }
        }

        Data = next;
        LastLoadError = null;
        _pendingErrorUnreported = false;
        LastSuccessfulLoadUtc = DateTime.UtcNow;
        ReportDroppedProtectedKeys();

        if (changed)
        {
            _logger.LogInformation("Configuration reloaded: {TotalKeys} keys", next.Count);
            if (raiseReload) OnReload();
        }
        else
        {
            _logger.LogDebug("Configuration checked: No changes detected");
        }
        return true;
    }

    private void RecordFailure(Exception ex, string message)
    {
        LastLoadError = ex;
        _pendingErrorUnreported = ReferenceEquals(_logger, NullLogger<DynamicConfigurationProvider>.Instance);
        _logger.LogError(ex, "{Message} (keeping {KeyCount} last-known-good keys)", message, Data.Count);
    }

    private string _reportedUndecryptableKeys = string.Empty;
    private string _reportedPlaintextSecretKeys = string.Empty;

    private void ReportDroppedProtectedKeys()
    {
        ReportSecretKeys();

        var dropped = _source.LastDroppedProtectedKeys;
        if (dropped.Count == 0) return;
        var signature = string.Join(",", dropped);
        if (signature == _reportedDroppedKeys) return;
        if (ReferenceEquals(_logger, NullLogger<DynamicConfigurationProvider>.Instance)) return;   // report once a logger exists
        _reportedDroppedKeys = signature;
        _logger.LogWarning(
            "DB-backed configuration contains protected keys that were NOT applied (a database row may not override them): {Keys}",
            signature);
    }

    // Once per distinct set, and only through a real logger — the same discipline as the protected keys.
    private void ReportSecretKeys()
    {
        if (ReferenceEquals(_logger, NullLogger<DynamicConfigurationProvider>.Instance)) return;

        var undecryptable = string.Join(",", _source.LastUndecryptableKeys);
        if (undecryptable.Length > 0 && undecryptable != _reportedUndecryptableKeys)
        {
            _logger.LogError(
                "DB-backed configuration: encrypted values could NOT be decrypted and were not applied (wrong Security:EncryptionKey, or a damaged value). ErrorKey: {ErrorKey}, Keys: {Keys}",
                "CONFIG_SECRET_UNDECRYPTABLE", undecryptable);
        }
        _reportedUndecryptableKeys = undecryptable;

        var plaintext = string.Join(",", _source.LastPlaintextSecretKeys);
        if (plaintext.Length > 0 && plaintext != _reportedPlaintextSecretKeys)
        {
            _logger.LogWarning(
                "DB-backed configuration: values flagged encrypted are stored in PLAINTEXT (applied as stored; save them again to encrypt). Keys: {Keys}",
                plaintext);
        }
        _reportedPlaintextSecretKeys = plaintext;
    }

    private async Task LoopAsync(bool skipFirstLoad)
    {
        var skip = skipFirstLoad;
        while (!_cts.IsCancellationRequested)
        {
            if (skip)
            {
                skip = false;
            }
            else
            {
                try
                {
                    // The token is passed to Polly too, so a back-off sleep ends at once on Dispose.
                    await _retryPolicy.ExecuteAsync(async ct =>
                    {
                        var dict = await _source.Loader(ct);
                        Apply(dict, raiseReload: true);
                    }, _cts.Token);
                }
                catch (OperationCanceledException) when (_cts.IsCancellationRequested)
                {
                    _logger.LogInformation("Configuration refresh loop cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    RecordFailure(ex, "Configuration load failed after all retry attempts; will retry on the next refresh");
                }
            }

            try
            {
                await Task.Delay(_source.RefreshInterval, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("DynamicConfigurationProvider stopped");
    }

    /// <summary>
    /// Releases resources used by the provider and stops the refresh loop.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _logger.LogDebug("Disposing DynamicConfigurationProvider");
        _cts.Cancel();
        try { _loop?.Wait(TimeSpan.FromSeconds(5)); }
        catch (AggregateException) { /* the loop only ends by cancellation here */ }
        _cts.Dispose();
        _disposed = true;
    }
}
