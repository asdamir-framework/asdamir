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

/// <summary>Read model describing a Hangfire recurring background job and its most recent execution, for the jobs dashboard.</summary>
public class RecurringJobDto
{
    /// <summary>Hangfire recurring-job identifier.</summary>
    public string Id { get; set; } = "";
    /// <summary>Display name of the job.</summary>
    public string Name { get; set; } = "";
    /// <summary>Cron expression (Hangfire syntax) driving the schedule.</summary>
    public string Cron { get; set; } = "";
    /// <summary>Hangfire queue the job runs on, e.g. "default".</summary>
    public string Queue { get; set; } = "";
    /// <summary>UTC time of the next scheduled run; null if the job is paused/removed.</summary>
    public DateTime? NextExecution { get; set; }
    /// <summary>UTC time of the most recent run; null if it has never run.</summary>
    public DateTime? LastExecution { get; set; }
    /// <summary>Hangfire job id of the most recent execution.</summary>
    public string? LastJobId { get; set; }
    /// <summary>State of the last execution, e.g. "Succeeded", "Failed", "Processing".</summary>
    public string? LastJobState { get; set; }
    /// <summary>Result or error detail from the last execution.</summary>
    public string? LastJobResult { get; set; }
    /// <summary>True when the recurring definition has been removed/deregistered.</summary>
    public bool Removed { get; set; }
    /// <summary>IANA/Windows time-zone id the cron schedule is evaluated in; null means UTC.</summary>
    public string? TimeZoneId { get; set; }
    /// <summary>Name of the target method invoked by the job.</summary>
    public string? Method { get; set; }
    /// <summary>Fully-qualified type declaring the invoked method.</summary>
    public string? Class { get; set; }
}
