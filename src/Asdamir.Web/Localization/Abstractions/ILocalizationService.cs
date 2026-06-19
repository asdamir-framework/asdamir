// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Web.Localization.Abstractions;

/// <summary>
/// Localization service for Framework consumers.
/// ONLY HttpClient implementation - NO database access.
/// </summary>
public interface ILocalizationService
{
    /// <summary>
    /// Supported cultures
    /// </summary>
    IReadOnlyList<string> SupportedCultures { get; }

    /// <summary>
    /// Gets all localized resources for a culture.
    /// </summary>
    /// <param name="cultureName">Culture code (e.g., "en-US", "tr-TR")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of key-value pairs</returns>
    Task<IDictionary<string, string>> GetResourcesAsync(string cultureName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific localized resource value by key and culture.
    /// </summary>
    /// <param name="cultureName">Culture code (e.g., "en-US", "tr-TR")</param>
    /// <param name="key">Resource key (e.g., "welcome.message")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Localized value or null if not found</returns>
    Task<string?> GetAsync(string cultureName, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upsert a localization key with values for multiple cultures.
    /// NOTE: Not implemented in HttpClient mode - use SQL scripts.
    /// </summary>
    Task UpsertAsync(string key, IDictionary<string, string> cultureToValue, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a localization key.
    /// NOTE: Not implemented in HttpClient mode - use SQL scripts.
    /// </summary>
    Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear cached resources to force reload from API.
    /// </summary>
    void ClearCache();
}
