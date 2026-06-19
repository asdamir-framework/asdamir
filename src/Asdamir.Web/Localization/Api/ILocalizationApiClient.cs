// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Web.Localization.Api;

/// <summary>
/// REST API client for localization resources
/// </summary>
public interface ILocalizationApiClient
{
    /// <summary>
    /// Get current localization version for cache invalidation
    /// </summary>
    Task<long> GetVersionAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all localization resources for a culture
    /// </summary>
    /// <param name="culture">Culture code (e.g., "tr-TR", "ru-RU", "en-US")</param>
    /// <param name="tenantId">Optional tenant ID for tenant-specific overrides</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of localization key-value pairs</returns>
    Task<Dictionary<string, string>> GetAllAsync(
        string culture, 
        Guid? tenantId = null, 
        CancellationToken cancellationToken = default);
}
