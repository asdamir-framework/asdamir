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
/// Persistence for background runs. Every method is TENANT-SCOPED — the implementation resolves the
/// ambient tenant and a run created under one tenant is never visible to another. Two implementations
/// ship: a Dapper store (SQL, guarded WHERE-scoped state transitions mirroring the proc convention)
/// and an in-memory store for <c>Persistence:UseInMemory</c>. In-memory-only persistence is FORBIDDEN
/// in production — a run must survive a process restart (restart-recovery reads it back).
/// </summary>
public interface IBackgroundRunStore
{
    /// <summary>
    /// Inserts a new run in <see cref="BackgroundRunState.Pending"/> for the current tenant and
    /// returns its id.
    /// </summary>
    Task<Guid> CreatePendingAsync(BackgroundRunRequest req, CancellationToken ct = default);

    /// <summary>
    /// Returns the id of an existing non-terminal (Pending/Running) run for the current tenant that
    /// matches (<paramref name="jobType"/> + <paramref name="dedupKey"/>), or <c>null</c> if none —
    /// the dedup lookup behind the default concurrency policy.
    /// </summary>
    Task<Guid?> FindActiveByDedupAsync(string jobType, string? dedupKey, CancellationToken ct = default);

    /// <summary>Returns the run's status for the current tenant, or <c>null</c> if absent.</summary>
    Task<BackgroundRunStatus?> GetAsync(Guid runId, CancellationToken ct = default);

    /// <summary>
    /// Returns the opaque payload for a run of the current tenant (kept out of
    /// <see cref="BackgroundRunStatus"/> because it can be large). Used by the runner to feed the
    /// handler; returns <c>null</c> if the run is absent or has no payload.
    /// </summary>
    Task<string?> GetPayloadAsync(Guid runId, CancellationToken ct = default);

    /// <summary>
    /// Guarded transition <c>Pending → Running</c> (records <c>StartedAtUtc</c> and the owner token).
    /// Returns <c>true</c> iff a row was actually transitioned (illegal transition = 0 rows).
    /// </summary>
    Task<bool> MarkRunningAsync(Guid runId, string ownerToken, CancellationToken ct = default);

    /// <summary>Guarded transition <c>Running → Completed</c> (records <c>ResultRef</c>, <c>CompletedAtUtc</c>).</summary>
    Task<bool> MarkCompletedAsync(Guid runId, string? resultRef, CancellationToken ct = default);

    /// <summary>Guarded transition <c>Running → Failed</c> (records the error, <c>CompletedAtUtc</c>).</summary>
    Task<bool> MarkFailedAsync(Guid runId, string errorSummary, CancellationToken ct = default);

    /// <summary>
    /// Persists the latest coalesced progress for a run (called by the throttled reporter). Not a
    /// state transition; a no-op if the run is already terminal.
    /// </summary>
    Task UpdateProgressAsync(Guid runId, int completed, int? total, CancellationToken ct = default);

    /// <summary>
    /// Restart-recovery: flips every run still <see cref="BackgroundRunState.Pending"/> or
    /// <see cref="BackgroundRunState.Running"/> (left behind by a dead process) to
    /// <see cref="BackgroundRunState.Interrupted"/>, across ALL tenants (startup, no request scope).
    /// Returns the number of runs recovered.
    /// </summary>
    Task<int> RecoverInterruptedAsync(CancellationToken ct = default);
}
