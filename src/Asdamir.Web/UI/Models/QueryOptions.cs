// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Web.UI.Models;

/// <summary>
/// Query options for server-side data operations
/// </summary>
public class QueryOptions
{
    /// <summary>Starting index for data request (zero-based)</summary>
    public int StartIndex { get; set; }

    /// <summary>Number of items to retrieve</summary>
    public int Count { get; set; }

    /// <summary>Search query text</summary>
    public string? SearchQuery { get; set; }

    /// <summary>Search query text (legacy alias for SearchQuery)</summary>
    public string? Search
    {
        get => SearchQuery;
        set => SearchQuery = value;
    }

    /// <summary>Column to sort by</summary>
    public string? SortColumn { get; set; }

    /// <summary>Sort direction: "asc" or "desc"</summary>
    public string? SortDirection { get; set; }

    /// <summary>Sort direction as integer: 1 (asc) or -1 (desc)</summary>
    public int SortDir
    {
        get => SortDirection?.ToLower() == "desc" ? -1 : 1;
        set => SortDirection = value < 0 ? "desc" : "asc";
    }

    /// <summary>Column-specific filters (key: column name, value: filter value)</summary>
    public Dictionary<string, string>? Filters { get; set; }

    /// <summary>Column-specific filters (legacy alias for Filters)</summary>
    public Dictionary<string, string>? ColumnFilters
    {
        get => Filters;
        set => Filters = value;
    }

    /// <summary>Selected quick filter name</summary>
    public string? QuickFilter { get; set; }

    /// <summary>Page number (1-based, calculated from StartIndex and Count)</summary>
    public int PageNumber => (StartIndex / Count) + 1;

    /// <summary>Page size (same as Count)</summary>
    public int PageSize => Count;
}

/// <summary>
/// Query result for server-side data operations
/// </summary>
/// <typeparam name="TItem">The type of data item</typeparam>
public class QueryResult<TItem>
{
    /// <summary>Items for the current page/request</summary>
    public List<TItem> Items { get; set; } = new();

    /// <summary>Total count of items (across all pages)</summary>
    public int TotalCount { get; set; }

    /// <summary>Optional error message if query failed</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Indicates if the query was successful</summary>
    public bool IsSuccess => string.IsNullOrEmpty(ErrorMessage);
}
