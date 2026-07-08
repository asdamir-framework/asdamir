// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Data.Configuration;

/// <summary>
/// Reads feature flags and typed feature configuration, applying an optional per-tenant override on top
/// of the global value. Backed by the DB-loaded configuration (<c>AppConfigurations</c>).
/// </summary>
public interface IFeatureManager
{
    /// <summary>True when the feature is enabled — the tenant-scoped value if present, else the global one.</summary>
    /// <param name="featureName">The feature flag name.</param>
    /// <param name="tenantId">Optional tenant to resolve a per-tenant override; null uses the ambient tenant/global.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> IsEnabledAsync(string featureName, string? tenantId = null, CancellationToken ct = default);

    /// <summary>Binds a typed feature configuration value (tenant override, else global); default when absent.</summary>
    /// <typeparam name="T">The value type to bind to.</typeparam>
    /// <param name="key">The configuration key.</param>
    /// <param name="tenantId">Optional tenant to resolve a per-tenant override; null uses the ambient tenant/global.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<T?> GetConfigurationAsync<T>(string key, string? tenantId = null, CancellationToken ct = default);
}
