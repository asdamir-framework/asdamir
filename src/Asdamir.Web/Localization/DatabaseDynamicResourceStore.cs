// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.Collections.Concurrent;
using System.Net.Http.Json;

namespace Asdamir.Web.Localization;

/// <summary>
/// API-backed dynamic resource store for localization
/// Reads localization data from gateway (Infrastructure service)
/// Clean architecture: Framework → gateway (no direct DB access)
/// </summary>
public sealed class DatabaseDynamicResourceStore : IDynamicResourceStore
{
    private readonly ILogger<DatabaseDynamicResourceStore> _logger;
    private readonly HttpClient _httpClient;
    private readonly IReadOnlyList<string> _configuredCultures;
    private readonly ConcurrentDictionary<string, Dictionary<string, string>> _cache = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public DatabaseDynamicResourceStore(
        ILogger<DatabaseDynamicResourceStore> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("gatewayNoAuth");

        // Supported cultures must match database values exactly
        _configuredCultures = new List<string> { "tr-TR", "en-US", "ru-RU" };
    }

    public IReadOnlyList<string> SupportedCultures => _configuredCultures;

    /// <summary>
    /// Clear the in-memory cache to force reload from database
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
        _logger.LogInformation("Localization cache cleared");
    }

    public async Task<IDictionary<string, string>> GetResourcesAsync(string cultureName, CancellationToken cancellationToken = default)
    {
        var dict = await LoadCultureAsync(cultureName, cancellationToken);
        // Fallback: Eğer hiç veri yoksa, circuit kopmasın diye en azından bir anahtar dön
        if (dict == null || dict.Count == 0)
        {
            _logger.LogWarning("Localization dictionary is empty for culture {Culture}. Returning fallback string.", cultureName);
            return new Dictionary<string, string> { { "App.Fallback", $"[No localization data for {cultureName}]" } };
        }
        return dict;
    }

    public async Task<string?> GetAsync(string cultureName, string key, CancellationToken cancellationToken = default)
    {
        var resources = await LoadCultureAsync(cultureName, cancellationToken);
        return resources.TryGetValue(key, out var value) ? value : null;
    }

    public Task UpsertAsync(string key, IDictionary<string, string> cultureToValue, CancellationToken cancellationToken = default)
    {
        // Not implemented - localization resources are managed via SQL scripts
        throw new NotImplementedException("Upsert functionality not implemented. Use SQL scripts to manage localization resources.");
    }

    public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        // Not implemented - localization resources are managed via SQL scripts
        throw new NotImplementedException("Delete functionality not implemented. Use SQL scripts to manage localization resources.");
    }

    private async Task<Dictionary<string, string>> LoadCultureAsync(string cultureName, CancellationToken cancellationToken)
    {
        // Always use full culture code (tr-TR, en-US, ru-RU)
        if (!SupportedCultures.Contains(cultureName))
        {
            // fallback to default
            cultureName = SupportedCultures.Contains("en-US") ? "en-US" : SupportedCultures.First();
        }

        if (_cache.TryGetValue(cultureName, out var cached))
        {
            return cached;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_cache.TryGetValue(cultureName, out cached))
            {
                return cached;
            }

            _logger.LogInformation("Loading localization resources for culture {Culture} from Gateway", cultureName);

            // Call Gateway endpoint with full culture code
            // Audit fix: escape culture in URL. Caller-supplied culture is normalised by
            // SupportedCultures.Contains above, so practically safe today; escaping closes
            // the door if SupportedCultures ever grows to include values with reserved chars.
            var response = await _httpClient.GetAsync($"gateway/localization/all?culture={Uri.EscapeDataString(cultureName)}", cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to load localization resources for culture {Culture} from API. Status: {StatusCode}",
                    cultureName, response.StatusCode);
                var emptyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                // Fallback anahtar ekle
                emptyMap["App.Fallback"] = $"[No localization data for {cultureName}]";
                _cache[cultureName] = emptyMap;
                return emptyMap;
            }

            var resources = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(cancellationToken: cancellationToken)
                ?? new Dictionary<string, string>();

            // Fallback: Eğer API'dan veri gelmezse, fallback anahtar ekle
            if (resources.Count == 0)
            {
                resources["App.Fallback"] = $"[No localization data for {cultureName}]";
            }

            var map = new Dictionary<string, string>(resources, StringComparer.OrdinalIgnoreCase);
            _cache[cultureName] = map;

            _logger.LogInformation("Loaded {Count} localization resources for culture {Culture} from API", map.Count, cultureName);

            return map;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP request failed while loading localization for culture {Culture}", cultureName);
            var emptyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            emptyMap["App.Fallback"] = $"[No localization data for {cultureName}]";
            _cache[cultureName] = emptyMap;
            return emptyMap;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load localization resources for culture {Culture}", cultureName);
            var emptyMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            emptyMap["App.Fallback"] = $"[No localization data for {cultureName}]";
            _cache[cultureName] = emptyMap;
            return emptyMap;
        }
        finally
        {
            _lock.Release();
        }
    }
}
