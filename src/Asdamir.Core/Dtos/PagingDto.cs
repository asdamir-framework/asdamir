// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.Dtos;

/// <summary>
/// Inbound paging/sort/filter request for a list endpoint: which page to fetch, how large, and how
/// to filter and order the results.
/// </summary>
/// <param name="Page">1-based index of the requested page.</param>
/// <param name="PageSize">Maximum number of items to return per page.</param>
/// <param name="Query">Optional free-text search term to filter rows.</param>
/// <param name="Sort">Optional field name to sort by.</param>
/// <param name="Dir">Optional sort direction ("asc" or "desc").</param>
public record PagedRequestDto(int Page = 1, int PageSize = 20, string? Query = null, string? Sort = null, string? Dir = null);

/// <summary>
/// A single page of results plus the paging metadata a client needs to render a pager.
/// </summary>
/// <typeparam name="T">Element type of the paged items.</typeparam>
/// <param name="Items">The rows belonging to the requested page.</param>
/// <param name="Total">Total matching rows across all pages (for pager math).</param>
/// <param name="Page">1-based index of the page these items came from.</param>
/// <param name="PageSize">Page size that was applied to produce this page.</param>
public record PagedResultDto<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);