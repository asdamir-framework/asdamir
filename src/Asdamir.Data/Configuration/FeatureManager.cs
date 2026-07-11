// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.MultiTenancy;
using Microsoft.Extensions.Configuration;

namespace Asdamir.Data.Configuration;

/// <summary>
/// <see cref="IFeatureManager"/> over <see cref="IConfiguration"/>: resolves a tenant-scoped key
/// (<c>Tenants:{id}:Features:{name}</c>) first, falling back to the global key.
/// </summary>
public sealed class FeatureManager : IFeatureManager
{
    private readonly IConfiguration _cfg;
    private readonly ITenantContext _tenant;

    /// <summary>Creates the feature manager over the app configuration and the ambient tenant context.</summary>
    public FeatureManager(IConfiguration cfg, ITenantContext tenant)
    {
        _cfg = cfg; _tenant = tenant;
    }

    private string BuildKey(string baseKey, string? tenantId)
        => tenantId is null ? baseKey : $"Tenants:{tenantId}:{baseKey}";

    /// <inheritdoc/>
    public Task<bool> IsEnabledAsync(string featureName, string? tenantId = null, CancellationToken ct = default)
    {
        var tid = tenantId ?? _tenant.TenantId ?? "default";
        var keyTenant = BuildKey($"Features:{featureName}", tid);
        var keyGlobal = $"Features:{featureName}";
        var val = _cfg[keyTenant] ?? _cfg[keyGlobal];
        return Task.FromResult(bool.TryParse(val, out var b) && b);
    }

    /// <inheritdoc/>
    public Task<T?> GetConfigurationAsync<T>(string key, string? tenantId = null, CancellationToken ct = default)
    {
        var tid = tenantId ?? _tenant.TenantId ?? "default";
        var keyTenant = BuildKey(key, tid);
        // Decide presence via Exists(), NOT "Get<T>() is not null": a VALUE type binds to default(T) (e.g. 0
        // for int, false for bool) when the tenant key is ABSENT, which would wrongly shadow the global
        // fallback and make it unreachable. Exists() is true only when the section has a value or children.
        var tenantSection = _cfg.GetSection(keyTenant);
        if (tenantSection.Exists()) return Task.FromResult(tenantSection.Get<T>());
        return Task.FromResult(_cfg.GetSection(key).Get<T>());
    }
}
