// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.Dtos;
using System.Net.Http.Json;

namespace Asdamir.Web.UI.Services;

/// <summary>
/// Global search service implementation
/// Enterprise-wide search across all modules
/// </summary>
public class GlobalSearchService : IGlobalSearchService
{
    private readonly ILogger<GlobalSearchService> _logger;
    private readonly HttpClient _httpClient;
    private readonly NavigationManager _navigationManager;

    public GlobalSearchService(
        ILogger<GlobalSearchService> logger,
        IHttpClientFactory httpClientFactory,
        NavigationManager navigationManager)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _navigationManager = navigationManager;
    }

    public async Task<List<GlobalSearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        return await SearchAsync(query, new GlobalSearchFilters(), cancellationToken);
    }

    public async Task<List<GlobalSearchResult>> SearchAsync(
        string query, 
        GlobalSearchFilters filters, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        {
            return new List<GlobalSearchResult>();
        }

        try
        {
            _logger.LogInformation("Global search query: {Query}", query);
            
            // Build absolute URL using NavigationManager base URI
            var baseUri = _navigationManager.BaseUri.TrimEnd('/');
            var url = $"{baseUri}/frameworkapi/search?query={Uri.EscapeDataString(query)}&maxResults={filters.MaxResults}";
            
            if (filters.Modules.Count > 0)
            {
                url += $"&modules={string.Join(",", filters.Modules)}";
            }

            var response = await _httpClient.GetFromJsonAsync<List<GlobalSearchResultDto>>(url, cancellationToken);
            
            if (response == null)
            {
                return new List<GlobalSearchResult>();
            }

            // Map DTO to domain model
            return response.Select(dto => new GlobalSearchResult
            {
                Id = dto.Id,
                Title = dto.Title,
                Description = dto.Description,
                Module = dto.Module,
                Icon = dto.Icon,
                Url = dto.Url,
                Score = dto.Score,
                LastModified = dto.LastModified
            }).ToList();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during global search for query: {Query}", query);
            return new List<GlobalSearchResult>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during global search for query: {Query}", query);
            return new List<GlobalSearchResult>();
        }
    }

    public Task<List<string>> GetSuggestionsAsync(
        string query, 
        int limit = 10, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        {
            return Task.FromResult(new List<string>());
        }

        try
        {
            // Build absolute URL using NavigationManager base URI
            var baseUri = _navigationManager.BaseUri.TrimEnd('/');
            var url = $"{baseUri}/frameworkapi/search/suggestions?query={Uri.EscapeDataString(query)}&limit={limit}";
            return _httpClient.GetFromJsonAsync<List<string>>(url, cancellationToken)
                .ContinueWith(t => t.Result ?? new List<string>(), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting search suggestions for: {Query}", query);
            return Task.FromResult(new List<string>());
        }
    }
}
