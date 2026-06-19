// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Web.Localization.Abstractions;
using System.Collections.Concurrent;
using System.Net.Http.Json;

namespace Asdamir.Web.Localization.Client;

/// <summary>
/// HttpClient-based localization service implementation.
/// Framework NuGet package - calls API (NO database access).
/// Clean architecture: Blazor → API → Database
/// </summary>
public sealed class LocalizationHttpClient : ILocalizationService
{
    private readonly ILogger<LocalizationHttpClient> _logger;
    private readonly HttpClient _httpClient;
    private readonly IReadOnlyList<string> _configuredCultures;
    private readonly ConcurrentDictionary<string, Dictionary<string, string>> _cache = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public LocalizationHttpClient(
        ILogger<LocalizationHttpClient> logger,
        HttpClient httpClient)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        // Supported cultures must match database values exactly
        _configuredCultures = new List<string> { "tr-TR", "en-US", "ru-RU" };
    }

    public IReadOnlyList<string> SupportedCultures => _configuredCultures;

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

    /// <summary>
    /// Clear the in-memory cache to force reload from API
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
        _logger.LogInformation("Localization cache cleared");
    }

    public async Task<IDictionary<string, string>> GetResourcesAsync(string cultureName, CancellationToken cancellationToken = default)
    {
        // Return whatever was loaded (possibly empty). The upper layer (e.g. SimpleStringLocalizer)
        // is responsible for deciding whether an empty result should be cached. Returning a fake
        // single-entry dictionary here previously masked failures as "success" and caused a stale
        // empty cache to be pinned for the lifetime of the process.
        return await LoadCultureAsync(cultureName, cancellationToken);
    }

    public async Task<string?> GetAsync(string cultureName, string key, CancellationToken cancellationToken = default)
    {
        var resources = await LoadCultureAsync(cultureName, cancellationToken);
        return resources.TryGetValue(key, out var value) ? value : null;
    }

    public async Task<IDictionary<string, string>> GetResourcesByCategoryAsync(string cultureName, string category, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{cultureName}:{category}";
        
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (_cache.TryGetValue(cacheKey, out cached))
            {
                return cached;
            }

            _logger.LogInformation("Loading localization resources for culture {Culture} category {Category} from Gateway", cultureName, category);

            // Audit fix: escape URL path and query components. category flows from caller code,
            // cultureName is the validated full code, but escaping the request is the
            // defense-in-depth posture.
            var response = await _httpClient.GetAsync(
                $"gateway/localization/category/{Uri.EscapeDataString(category)}?culture={Uri.EscapeDataString(cultureName)}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to load localization resources for culture {Culture} category {Category}. Status: {StatusCode}",
                    cultureName, category, response.StatusCode);
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var resources = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(cancellationToken: cancellationToken)
                ?? new Dictionary<string, string>();

            var map = new Dictionary<string, string>(resources, StringComparer.OrdinalIgnoreCase);
            _cache[cacheKey] = map;

            _logger.LogInformation("Loaded {Count} localization resources for culture {Culture} category {Category}", map.Count, cultureName, category);

            return map;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load localization resources for culture {Culture} category {Category}", cultureName, category);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<Dictionary<string, string>> LoadCultureAsync(string cultureName, CancellationToken cancellationToken)
    {
        // Always use full culture code (tr-TR, en-US, ru-RU)
        if (!SupportedCultures.Contains(cultureName))
        {
            // Fallback to default
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

            var response = await _httpClient.GetAsync(
                $"gateway/localization/all?culture={Uri.EscapeDataString(cultureName)}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to load localization resources for culture {Culture} from API. Status: {StatusCode}",
                    cultureName, response.StatusCode);
                // Do NOT cache failures - return empty so the next call retries.
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var resources = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>(cancellationToken: cancellationToken)
                ?? new Dictionary<string, string>();

            if (resources.Count == 0)
            {
                _logger.LogWarning("API returned empty localization dictionary for culture {Culture} - not caching", cultureName);
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var map = new Dictionary<string, string>(resources, StringComparer.OrdinalIgnoreCase);
            _cache[cultureName] = map;

            _logger.LogInformation("Loaded {Count} localization resources for culture {Culture} from API", map.Count, cultureName);

            return map;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP request failed while loading localization for culture {Culture}", cultureName);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load localization resources for culture {Culture}", cultureName);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            _lock.Release();
        }
    }
}
