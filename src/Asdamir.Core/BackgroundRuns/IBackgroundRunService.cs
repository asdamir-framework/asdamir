// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.BackgroundRuns;

/// <summary>
/// The API-tier primitive for long-running operations: <b>trigger → run in the background →
/// query status/progress</b>. A controller calls <see cref="EnqueueAsync"/> to start a run and
/// returns its id immediately; callers poll <see cref="GetStatusAsync"/> for lifecycle + progress.
/// <para>
/// The framework manages the run LIFECYCLE and persistence; the job's CONTENT is app-defined via an
/// <see cref="IBackgroundJobHandler"/> registered under the request's <c>JobType</c>. This means an
/// existing consumer (e.g. a <c>RunAsync(...)</c> engine) is wrapped WITHOUT changing its signature —
/// the wrapping handler is the app's job. Runs are tenant-scoped: a run enqueued under one tenant is
/// invisible to another.
/// </para>
/// </summary>
public interface IBackgroundRunService
{
    /// <summary>
    /// Enqueues a background run and returns its id. With the default concurrency policy
    /// (<see cref="BackgroundRunRequest.AllowConcurrent"/> = <c>false</c>), if a run of the same
    /// (<c>JobType</c> + <c>DedupKey</c>) is already Pending/Running for this tenant, the EXISTING
    /// run id is returned and no duplicate is started.
    /// </summary>
    /// <param name="req">The run request (job type, payload, dedup key, concurrency flag).</param>
    /// <param name="ct">Cancellation for the enqueue call itself (not the run).</param>
    /// <returns>The new run id, or the id of the existing in-flight run when deduplicated.</returns>
    Task<Guid> EnqueueAsync(BackgroundRunRequest req, CancellationToken ct = default);

    /// <summary>
    /// Returns the current status/progress of a run, scoped to the caller's tenant, or
    /// <c>null</c> if no such run exists for this tenant.
    /// </summary>
    /// <param name="runId">The run id from <see cref="EnqueueAsync"/>.</param>
    /// <param name="ct">Cancellation for the query.</param>
    Task<BackgroundRunStatus?> GetStatusAsync(Guid runId, CancellationToken ct = default);
}
