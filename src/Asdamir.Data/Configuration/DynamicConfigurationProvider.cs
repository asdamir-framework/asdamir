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
/// Dynamic configuration provider that periodically reloads configuration from a custom source.
/// Supports retry policies for resilience and structured logging for diagnostics.
/// </summary>
public sealed class DynamicConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly DynamicConfigurationSource _source;
    private readonly CancellationTokenSource _cts = new();
    private readonly ILogger<DynamicConfigurationProvider> _logger;
    private readonly AsyncRetryPolicy _retryPolicy;
    private Task? _loop;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicConfigurationProvider"/> class.
    /// </summary>
    /// <param name="source">The configuration source containing the loader and refresh interval.</param>
    /// <param name="logger">Optional logger for diagnostics. If null, NullLogger is used.</param>
    public DynamicConfigurationProvider(
        DynamicConfigurationSource source,
        ILogger<DynamicConfigurationProvider>? logger = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _logger = logger ?? NullLogger<DynamicConfigurationProvider>.Instance;

        // Polly retry policy: 3 attempts with exponential backoff
        _retryPolicy = Policy
            .Handle<Exception>()
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
    /// Loads the initial configuration and starts the periodic refresh loop.
    /// </summary>
    public override void Load()
    {
        Data = new Dictionary<string, string?>();
        _logger.LogInformation(
            "DynamicConfigurationProvider starting with {RefreshInterval}s refresh interval",
            _source.RefreshInterval.TotalSeconds);
        _loop = Task.Run(LoopAsync);
    }

    private async Task LoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                await _retryPolicy.ExecuteAsync(async () =>
                {
                    var dict = await _source.Loader(_cts.Token);

                    if (dict == null)
                    {
                        _logger.LogWarning("Configuration loader returned null dictionary");
                        return;
                    }

                    var changedCount = 0;
                    foreach (var kvp in dict)
                    {
                        if (!Data.TryGetValue(kvp.Key, out var existing) || existing != kvp.Value)
                        {
                            changedCount++;
                        }
                    }

                    Data = dict;

                    if (changedCount > 0)
                    {
                        _logger.LogInformation(
                            "Configuration reloaded: {TotalKeys} keys, {ChangedKeys} changed",
                            dict.Count,
                            changedCount);
                        OnReload();
                    }
                    else
                    {
                        _logger.LogDebug("Configuration checked: No changes detected");
                    }
                });
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                _logger.LogInformation("Configuration refresh loop cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Configuration load failed after all retry attempts. Will retry in {RefreshInterval}s",
                    _source.RefreshInterval.TotalSeconds);
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
        _loop?.Wait(TimeSpan.FromSeconds(5));
        _cts.Dispose();
        _disposed = true;
    }
}
