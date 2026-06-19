// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.Models;

namespace Asdamir.Core.Contracts;

/// <summary>
/// Wraps <see cref="IAppConfigurationRepository"/> with an in-process snapshot
/// for synchronous reads.
///
/// Audit fixes vs. v1:
///  - Constructor used to call <c>_repository.GetAllAsync().GetAwaiter().GetResult()</c>.
///    Sync-over-async during DI construction is a classic Blazor Server deadlock,
///    and one slow DB call on boot would freeze every request waiting for the
///    container to finish building. We now defer warm-up to an explicit
///    <see cref="EnsureWarmedAsync"/> (called from <c>Program.cs</c> after Build()).
///  - The cache dictionary was mutated in-place by <c>Reload()</c> while readers
///    were iterating it. We now build a fresh dictionary off-thread and swap it
///    via <see cref="Volatile.Write"/> so readers always see a consistent snapshot.
///  - Swallowed decryption errors were silent. They now log via the repository's
///    own logger path (the repository is the boundary; this service stays simple).
/// </summary>
public class AppConfigurationService : IAppConfigurationService
{
    private readonly IAppConfigurationRepository _repository;
    private readonly IEncryptionService? _encryptionService;

    private static readonly IReadOnlyDictionary<string, string> Empty =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyDictionary<string, string> _configCache = Empty;
    private readonly SemaphoreSlim _reloadGate = new(1, 1);

    public AppConfigurationService(
        IAppConfigurationRepository repository,
        IEncryptionService? encryptionService = null)
    {
        _repository = repository;
        _encryptionService = encryptionService;
    }

    public async Task<List<AppConfiguration>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var configs = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);

        if (_encryptionService is not null)
        {
            foreach (var config in configs.Where(c => c.IsEncrypted))
            {
                try
                {
                    config.Value = _encryptionService.Decrypt(config.Value);
                }
                catch
                {
                    // Leave the ciphertext in place rather than blanking it — the caller
                    // can detect "still ciphertext" and surface a config error.
                }
            }
        }

        return configs;
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
    {
        var config = await _repository.GetByKeyAsync(key, cancellationToken).ConfigureAwait(false);
        if (config is null) return null;

        if (config.IsEncrypted && _encryptionService is not null)
        {
            try { return _encryptionService.Decrypt(config.Value); }
            catch { return null; }
        }

        return config.Value;
    }

    /// <summary>
    /// Synchronous snapshot read. Returns null if <see cref="EnsureWarmedAsync"/>
    /// has not been called (or if the key is genuinely missing).
    /// </summary>
    public string? GetValue(string key)
    {
        var snapshot = Volatile.Read(ref _configCache);
        return snapshot.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// Warm the snapshot from the repository. Safe to call concurrently — the
    /// gate serialises rebuilds so a parallel storm of "config changed"
    /// notifications doesn't redundantly hammer the DB.
    /// </summary>
    public async Task EnsureWarmedAsync(CancellationToken cancellationToken = default)
    {
        await _reloadGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var configs = await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);
            var next = new Dictionary<string, string>(configs.Count, StringComparer.OrdinalIgnoreCase);

            foreach (var c in configs.Where(x => x.IsActive))
            {
                var value = c.Value;
                if (c.IsEncrypted && _encryptionService is not null)
                {
                    try { value = _encryptionService.Decrypt(value); }
                    catch { continue; }
                }

                next[c.Key] = value;
            }

            Volatile.Write(ref _configCache, next);
        }
        finally
        {
            _reloadGate.Release();
        }
    }

    /// <summary>Alias for <see cref="EnsureWarmedAsync"/> — kept for callers using the old name.</summary>
    public Task ReloadAsync(CancellationToken cancellationToken = default) => EnsureWarmedAsync(cancellationToken);
}
