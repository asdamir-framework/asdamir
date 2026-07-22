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
/// A request to run a long-running operation in the background. The framework manages the run
/// LIFECYCLE; the operation's CONTENT is supplied by the app as a handler registered under
/// <see cref="JobType"/> (see <c>IBackgroundJobHandler</c>).
/// </summary>
/// <param name="JobType">
/// Stable key that selects the app-registered handler (e.g. <c>"reconciliation"</c>). Required.
/// </param>
/// <param name="Payload">
/// Opaque, app-defined input handed verbatim to the handler (typically JSON). May be <c>null</c>
/// for parameter-less jobs. The framework never inspects it.
/// </param>
/// <param name="DedupKey">
/// Optional business key that, together with <see cref="JobType"/>, identifies "the same run".
/// When <see cref="AllowConcurrent"/> is <c>false</c> (default), a second enqueue of the same
/// (JobType + DedupKey) that is still Pending/Running returns the EXISTING run id instead of
/// starting a duplicate.
/// </param>
/// <param name="AllowConcurrent">
/// When <c>true</c>, the dedup check is skipped and a fresh run always starts. Default <c>false</c>.
/// </param>
public sealed record BackgroundRunRequest(
    string JobType,
    string? Payload = null,
    string? DedupKey = null,
    bool AllowConcurrent = false);

/// <summary>
/// A point-in-time snapshot of a background run's lifecycle, returned by
/// <c>IBackgroundRunService.GetStatusAsync</c>. All timestamps are UTC.
/// </summary>
/// <param name="RunId">The run's unique id (as handed back by <c>EnqueueAsync</c>).</param>
/// <param name="JobType">The handler key this run targets.</param>
/// <param name="State">The current lifecycle <see cref="BackgroundRunState"/>.</param>
/// <param name="ProgressCompleted">Units completed so far, or <c>null</c> if the handler reports no progress.</param>
/// <param name="ProgressTotal">Total units expected, or <c>null</c> if unknown.</param>
/// <param name="ResultRef">
/// App-defined pointer to the result (e.g. a report id / blob key) set by the handler on success.
/// The framework stores/returns it opaquely; it never holds the result payload itself.
/// </param>
/// <param name="ErrorSummary">Truncated failure reason when <see cref="State"/> is Failed/Interrupted.</param>
/// <param name="CreatedAtUtc">When the run was enqueued.</param>
/// <param name="StartedAtUtc">When the runner began executing the handler, or <c>null</c> if not yet started.</param>
/// <param name="CompletedAtUtc">When the run reached a terminal state, or <c>null</c> if still active.</param>
public sealed record BackgroundRunStatus(
    Guid RunId,
    string JobType,
    BackgroundRunState State,
    int? ProgressCompleted,
    int? ProgressTotal,
    string? ResultRef,
    string? ErrorSummary,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc);
