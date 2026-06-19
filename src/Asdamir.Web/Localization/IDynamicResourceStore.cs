// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Web.Localization;

/// <summary>
/// Interface for dynamic resource store that can load localization resources
/// </summary>
public interface IDynamicResourceStore
{
    /// <summary>
    /// Supported cultures
    /// </summary>
    IReadOnlyList<string> SupportedCultures { get; }

    /// <summary>
    /// Get all resources for a culture
    /// </summary>
    Task<IDictionary<string, string>> GetResourcesAsync(string cultureName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific resource value for a culture and key
    /// </summary>
    Task<string?> GetAsync(string cultureName, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upsert a localization key with values for multiple cultures
    /// </summary>
    Task UpsertAsync(string key, IDictionary<string, string> cultureToValue, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a localization key
    /// </summary>
    Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear cached resources to force reload
    /// </summary>
    void ClearCache();
}
