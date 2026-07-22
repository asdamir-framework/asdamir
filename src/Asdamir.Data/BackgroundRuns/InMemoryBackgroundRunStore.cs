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
/// Singleton holder for <see cref="InMemoryBackgroundRunStore"/> rows. The store itself is scoped
/// (it depends on the scoped <see cref="ITenantContext"/>), so the data lives here — NEVER in
/// <c>static</c> fields, which would leak across parallel <c>WebApplicationFactory</c> instances in
/// tests (the InMemory-store isolation rule).
/// </summary>
public sealed class BackgroundRunData
{
    /// <summary>Mutable row for an in-memory run (kept internal to the holder + store).</summary>
    internal sealed class Row
    {
        public required Guid RunId { get; init; }
        public required string TenantId { get; init; }
        public required string JobType { get; init; }
        public string? Payload { get; init; }
        public string? DedupKey { get; init; }
        public BackgroundRunState State { get; set; }
        public int? ProgressCompleted { get; set; }
        public int? ProgressTotal { get; set; }
        public string? ResultRef { get; set; }
        public string? ErrorSummary { get; set; }
        public string? OwnerToken { get; set; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
    }

    /// <summary>Guards <see cref="Rows"/>.</summary>
    internal readonly object Gate = new();

    /// <summary>All runs across all tenants (tenant filtering is applied by the store).</summary>
    internal readonly List<Row> Rows = new();
}

/// <summary>
/// In-memory <see cref="IBackgroundRunStore"/> for <c>Persistence:UseInMemory</c> (dev/tests). Mirrors
/// the Dapper store's tenant-scoping and guarded transitions. State is held in a singleton
/// <see cref="BackgroundRunData"/> holder (never static).
/// </summary>
public sealed class InMemoryBackgroundRunStore(BackgroundRunData data, ITenantContext tenant)
    : IBackgroundRunStore
{
    private readonly BackgroundRunData _data = data;
    private readonly ITenantContext _tenant = tenant;

    /// <inheritdoc />
    public Task<Guid> CreatePendingAsync(BackgroundRunRequest req, CancellationToken ct = default)
    {
        var runId = Guid.NewGuid();
        lock (_data.Gate)
        {
            _data.Rows.Add(new BackgroundRunData.Row
            {
                RunId = runId,
                TenantId = _tenant.TenantId,
                JobType = req.JobType,
                Payload = req.Payload,
                DedupKey = req.DedupKey,
                State = BackgroundRunState.Pending,
                CreatedAtUtc = DateTime.UtcNow,
            });
        }
        return Task.FromResult(runId);
    }

    /// <inheritdoc />
    public Task<Guid?> FindActiveByDedupAsync(string jobType, string? dedupKey, CancellationToken ct = default)
    {
        lock (_data.Gate)
        {
            var row = _data.Rows
                .Where(r => r.TenantId == _tenant.TenantId
                    && r.JobType == jobType
                    && r.DedupKey == dedupKey
                    && (r.State == BackgroundRunState.Pending || r.State == BackgroundRunState.Running))
                .OrderBy(r => r.CreatedAtUtc)
                .FirstOrDefault();
            return Task.FromResult(row?.RunId);
        }
    }

    /// <inheritdoc />
    public Task<BackgroundRunStatus?> GetAsync(Guid runId, CancellationToken ct = default)
    {
        lock (_data.Gate)
        {
            var row = _data.Rows.FirstOrDefault(r => r.TenantId == _tenant.TenantId && r.RunId == runId);
            return Task.FromResult(row is null ? null : ToStatus(row));
        }
    }

    /// <inheritdoc />
    public Task<string?> GetPayloadAsync(Guid runId, CancellationToken ct = default)
    {
        lock (_data.Gate)
        {
            var row = _data.Rows.FirstOrDefault(r => r.TenantId == _tenant.TenantId && r.RunId == runId);
            return Task.FromResult(row?.Payload);
        }
    }

    /// <inheritdoc />
    public Task<bool> MarkRunningAsync(Guid runId, string ownerToken, CancellationToken ct = default)
    {
        lock (_data.Gate)
        {
            var row = Find(runId);
            if (row is null || row.State != BackgroundRunState.Pending) return Task.FromResult(false);
            row.State = BackgroundRunState.Running;
            row.OwnerToken = ownerToken;
            row.StartedAtUtc = DateTime.UtcNow;
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    public Task<bool> MarkCompletedAsync(Guid runId, string? resultRef, CancellationToken ct = default)
    {
        lock (_data.Gate)
        {
            var row = Find(runId);
            if (row is null || row.State != BackgroundRunState.Running) return Task.FromResult(false);
            row.State = BackgroundRunState.Completed;
            row.ResultRef = resultRef;
            row.CompletedAtUtc = DateTime.UtcNow;
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    public Task<bool> MarkFailedAsync(Guid runId, string errorSummary, CancellationToken ct = default)
    {
        lock (_data.Gate)
        {
            var row = Find(runId);
            if (row is null || row.State != BackgroundRunState.Running) return Task.FromResult(false);
            row.State = BackgroundRunState.Failed;
            row.ErrorSummary = errorSummary.Length <= 2000 ? errorSummary : errorSummary[..2000];
            row.CompletedAtUtc = DateTime.UtcNow;
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    public Task UpdateProgressAsync(Guid runId, int completed, int? total, CancellationToken ct = default)
    {
        lock (_data.Gate)
        {
            var row = Find(runId);
            if (row is not null && (row.State == BackgroundRunState.Pending || row.State == BackgroundRunState.Running))
            {
                row.ProgressCompleted = completed;
                row.ProgressTotal = total;
            }
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<int> RecoverInterruptedAsync(CancellationToken ct = default)
    {
        lock (_data.Gate)
        {
            var stuck = _data.Rows
                .Where(r => r.State == BackgroundRunState.Pending || r.State == BackgroundRunState.Running)
                .ToList();
            foreach (var row in stuck)
            {
                row.State = BackgroundRunState.Interrupted;
                row.ErrorSummary ??= "Interrupted: owning process ended before completion.";
                row.CompletedAtUtc = DateTime.UtcNow;
            }
            return Task.FromResult(stuck.Count);
        }
    }

    // Caller holds the gate.
    private BackgroundRunData.Row? Find(Guid runId) =>
        _data.Rows.FirstOrDefault(r => r.TenantId == _tenant.TenantId && r.RunId == runId);

    private static BackgroundRunStatus ToStatus(BackgroundRunData.Row r) => new(
        r.RunId, r.JobType, r.State, r.ProgressCompleted, r.ProgressTotal,
        r.ResultRef, r.ErrorSummary, r.CreatedAtUtc, r.StartedAtUtc, r.CompletedAtUtc);
}
