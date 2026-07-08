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

/// <summary>Aggregated headline stats for the Error Monitoring dashboard's overview cards and trend chart.</summary>
public class ErrorDashboardOverviewResponse
{
    /// <summary>Total error count in the selected window.</summary>
    public int TotalErrors { get; set; }
    /// <summary>Count of critical/fatal-level errors in the window.</summary>
    public int CriticalErrors { get; set; }
    /// <summary>Count of warning-level entries in the window.</summary>
    public int WarningErrors { get; set; }
    /// <summary>Count of informational-level entries in the window.</summary>
    public int InfoErrors { get; set; }
    /// <summary>Number of entries operators have marked resolved.</summary>
    public int ResolvedErrors { get; set; }
    /// <summary>Number of entries still open (unresolved).</summary>
    public int UnresolvedErrors { get; set; }
    /// <summary>UTC timestamp of the most recent error in the window.</summary>
    public DateTime LastErrorTime { get; set; }
    /// <summary>Per-day, per-level counts driving the dashboard's trend chart.</summary>
    public List<ErrorTrendDto> Trends { get; set; } = new();
}

/// <summary>A single point in the error trend series — one day/level bucket.</summary>
public class ErrorTrendDto
{
    /// <summary>The day (UTC) this bucket aggregates.</summary>
    public DateTime Date { get; set; }
    /// <summary>Number of errors of <see cref="Level"/> on <see cref="Date"/>.</summary>
    public int Count { get; set; }
    /// <summary>Severity level this bucket counts (e.g. "Error", "Warning").</summary>
    public string Level { get; set; } = "";
}
