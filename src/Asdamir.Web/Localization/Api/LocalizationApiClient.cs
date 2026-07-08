// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.Text.Json;

namespace Asdamir.Web.Localization.Api;

/// <summary>
/// HTTP client for localization REST API
/// </summary>
public sealed class LocalizationApiClient : ILocalizationApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LocalizationApiClient> _logger;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Initializes the client with the HTTP transport and logger.
    /// </summary>
    /// <param name="httpClient">The configured HttpClient pointed at the localization API.</param>
    /// <param name="logger">Logger for fetch/parse diagnostics.</param>
    public LocalizationApiClient(HttpClient httpClient, ILogger<LocalizationApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<long> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetStringAsync("localization/version", cancellationToken);
            
            // Try to parse as long directly, or extract from JSON
            if (long.TryParse(response, out var version))
                return version;
            
            // Try JSON format: { "version": 123 }
            using var doc = JsonDocument.Parse(response);
            if (doc.RootElement.TryGetProperty("version", out var versionProp))
                return versionProp.GetInt64();
            
            _logger.LogWarning("Could not parse version from response: {Response}", response);
            return 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get localization version");
            return 1; // Default version
        }
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, string>> GetAllAsync(
        string culture, 
        Guid? tenantId = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"localization/all?culture={Uri.EscapeDataString(culture)}";
            if (tenantId.HasValue)
                url += $"&tenantId={tenantId.Value}";

            _logger.LogDebug("Fetching localization resources: {Url}", url);

            using var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var resources = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);

            _logger.LogInformation("✅ Loaded {Count} localization resources for culture {Culture}", 
                resources?.Count ?? 0, culture);

            return resources ?? new Dictionary<string, string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load localization resources for culture {Culture}", culture);
            return new Dictionary<string, string>();
        }
    }
}
