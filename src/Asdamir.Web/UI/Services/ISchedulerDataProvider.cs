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
/// Supplies the <c>Scheduler</c> component with the recurring jobs to display, decoupling the UI
/// from any specific backend (Hangfire monitor, AdminConsole API, …). The host app registers an
/// implementation; when none is registered the component shows an empty state rather than mock data.
/// </summary>
public interface ISchedulerDataProvider
{
    /// <summary>The configured recurring jobs (cron + last/next run), newest schedule first.</summary>
    Task<IReadOnlyList<ScheduledJobInfo>> GetRecurringJobsAsync(CancellationToken ct = default);

    /// <summary>Triggers <paramref name="jobId"/> to run now. Returns false if the job is unknown.</summary>
    Task<bool> TriggerAsync(string jobId, CancellationToken ct = default);
}

/// <summary>One recurring job as shown in the scheduler UI. <see cref="Cron"/> is a 5-field cron
/// expression the component evaluates (in-house) to preview the next run.</summary>
public sealed record ScheduledJobInfo(
    string Id,
    string Name,
    string Cron,
    DateTime? LastRunUtc,
    string? LastResult,
    string? Queue);
