// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.BackgroundRuns;
using Asdamir.Core.MultiTenancy;

namespace Asdamir.Data.BackgroundRuns;

/// <summary>
/// Default <see cref="IBackgroundRunService"/>: persists a Pending run, applies the concurrency
/// policy, then signals the hosted runner via <see cref="BackgroundRunQueue"/>. Scoped (it uses the
/// scoped store + tenant); the durable state is the persisted row, the queue is only the signal.
/// </summary>
public sealed class BackgroundRunService(
    IBackgroundRunStore store,
    BackgroundRunQueue queue,
    ITenantContext tenant)
    : IBackgroundRunService
{
    private readonly IBackgroundRunStore _store = store;
    private readonly BackgroundRunQueue _queue = queue;
    private readonly ITenantContext _tenant = tenant;

    /// <inheritdoc />
    public async Task<Guid> EnqueueAsync(BackgroundRunRequest req, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(req.JobType))
            throw new ArgumentException("JobType is required.", nameof(req));

        // Concurrency policy: unless AllowConcurrent, a matching in-flight (Pending/Running) run of
        // the same (JobType + DedupKey) wins — return its id, start no duplicate.
        if (!req.AllowConcurrent)
        {
            var existing = await _store.FindActiveByDedupAsync(req.JobType, req.DedupKey, ct);
            if (existing is Guid inFlight) return inFlight;
        }

        var runId = await _store.CreatePendingAsync(req, ct);
        await _queue.EnqueueAsync(new BackgroundRunWorkItem(runId, _tenant.TenantId), ct);
        return runId;
    }

    /// <inheritdoc />
    public Task<BackgroundRunStatus?> GetStatusAsync(Guid runId, CancellationToken ct = default) =>
        _store.GetAsync(runId, ct);
}
