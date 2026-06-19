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

public sealed class FeatureManager : IFeatureManager
{
    private readonly IConfiguration _cfg;
    private readonly ITenantContext _tenant;

    public FeatureManager(IConfiguration cfg, ITenantContext tenant)
    {
        _cfg = cfg; _tenant = tenant;
    }

    private string BuildKey(string baseKey, string? tenantId)
        => tenantId is null ? baseKey : $"Tenants:{tenantId}:{baseKey}";

    public Task<bool> IsEnabledAsync(string featureName, string? tenantId = null, CancellationToken ct = default)
    {
        var tid = tenantId ?? _tenant.TenantId ?? "default";
        var keyTenant = BuildKey($"Features:{featureName}", tid);
        var keyGlobal = $"Features:{featureName}";
        var val = _cfg[keyTenant] ?? _cfg[keyGlobal];
        return Task.FromResult(bool.TryParse(val, out var b) && b);
    }

    public Task<T?> GetConfigurationAsync<T>(string key, string? tenantId = null, CancellationToken ct = default)
    {
        var tid = tenantId ?? _tenant.TenantId ?? "default";
        var keyTenant = BuildKey(key, tid);
        var valTenant = _cfg.GetSection(keyTenant).Get<T>();
        if (valTenant is not null) return Task.FromResult((T?)valTenant);
        var valGlobal = _cfg.GetSection(key).Get<T>();
        return Task.FromResult((T?)valGlobal);
    }
}
