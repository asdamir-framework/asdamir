// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Web.UI.Services;

/// <summary>
/// Global search service interface for enterprise-wide search functionality
/// </summary>
public interface IGlobalSearchService
{
    /// <summary>
    /// Search across all modules and entities
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of search results</returns>
    Task<List<GlobalSearchResult>> SearchAsync(string query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search with filters
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="filters">Search filters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of search results</returns>
    Task<List<GlobalSearchResult>> SearchAsync(string query, GlobalSearchFilters filters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get search suggestions for autocomplete
    /// </summary>
    /// <param name="query">Partial query</param>
    /// <param name="limit">Maximum number of suggestions</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of suggestions</returns>
    Task<List<string>> GetSuggestionsAsync(string query, int limit = 10, CancellationToken cancellationToken = default);
}

/// <summary>
/// Global search result model
/// </summary>
public class GlobalSearchResult
{
    /// <summary>
    /// Result ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Title/Name of the result
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Description or excerpt
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Module/Entity type (e.g., "Users", "Products", "Menu", "Localization")
    /// </summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>
    /// Icon for the module
    /// </summary>
    public string Icon { get; set; } = "📄";

    /// <summary>
    /// Navigation URL
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Match score/relevance (0-100)
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Last modified date
    /// </summary>
    public DateTime? LastModified { get; set; }
}

/// <summary>
/// Global search filters
/// </summary>
public class GlobalSearchFilters
{
    /// <summary>
    /// Filter by modules (e.g., "Users", "Products", "Menu")
    /// </summary>
    public List<string> Modules { get; set; } = new();

    /// <summary>
    /// Date range filter - from
    /// </summary>
    public DateTime? DateFrom { get; set; }

    /// <summary>
    /// Date range filter - to
    /// </summary>
    public DateTime? DateTo { get; set; }

    /// <summary>
    /// Maximum number of results
    /// </summary>
    public int MaxResults { get; set; } = 50;

    /// <summary>
    /// Include archived/deleted items
    /// </summary>
    public bool IncludeArchived { get; set; } = false;
}
