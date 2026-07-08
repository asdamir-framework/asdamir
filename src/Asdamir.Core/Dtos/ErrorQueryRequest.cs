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

/// <summary>Filter/paging criteria for querying the operator-facing error log (dbo.AppLog) on the Error Monitoring dashboard.</summary>
public class ErrorQueryRequest
{
    /// <summary>Inclusive lower bound (UTC) of the query window; null = unbounded.</summary>
    public DateTime? StartDate { get; set; }
    /// <summary>Inclusive upper bound (UTC) of the query window; null = unbounded.</summary>
    public DateTime? EndDate { get; set; }
    /// <summary>Filter by log level (e.g. "Error", "Warning", "Information"); null = all levels.</summary>
    public string? Level { get; set; }
    /// <summary>Filter by originating app/component (the Source tag); null = all sources.</summary>
    public string? Source { get; set; }
    /// <summary>Filter by resolution state: true = resolved, false = open; null = both.</summary>
    public bool? IsResolved { get; set; }
    /// <summary>Restrict to errors logged in the context of this operator/user id; null = all users.</summary>
    public int? UserId { get; set; }
    /// <summary>1-based page number of the result set.</summary>
    public int Page { get; set; } = 1;
    /// <summary>Maximum number of rows returned per page.</summary>
    public int PageSize { get; set; } = 50;
    /// <summary>Free-text term matched against the error message/details; null = no text filter.</summary>
    public string? SearchTerm { get; set; }
}

/// <summary>Paged result of an <see cref="ErrorQueryRequest"/> — the matching error rows plus paging metadata.</summary>
public class ErrorQueryResponse
{
    /// <summary>The error rows on the current page.</summary>
    public List<RecentErrorDto> Errors { get; set; } = new();
    /// <summary>Total number of rows matching the filter across all pages.</summary>
    public int TotalCount { get; set; }
    /// <summary>1-based page number this response represents.</summary>
    public int Page { get; set; }
    /// <summary>Page size used to produce this response.</summary>
    public int PageSize { get; set; }
}

/// <summary>Condensed error row shown in the dashboard's recent-errors list.</summary>
public class RecentErrorDto
{
    /// <summary>Primary key of the AppLog row.</summary>
    public int Id { get; set; }
    /// <summary>Severity level of the entry (e.g. "Error", "Warning").</summary>
    public string Level { get; set; } = "";
    /// <summary>Operator-facing error text (the full logged message, not the end-user's localized one).</summary>
    public string Message { get; set; } = "";
    /// <summary>Originating app/component that raised the error.</summary>
    public string Source { get; set; } = "";
    /// <summary>UTC time the error was logged.</summary>
    public DateTime Timestamp { get; set; }
    /// <summary>Whether an operator has marked this error resolved.</summary>
    public bool IsResolved { get; set; }
}
