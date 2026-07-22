// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.Data;
using Asdamir.Core.BackgroundRuns;
using Asdamir.Core.Contracts;
using Asdamir.Core.MultiTenancy;
using Dapper;

namespace Asdamir.Data.BackgroundRuns;

/// <summary>
/// SQL (Dapper) persistence for background runs, backed by the <c>dbo.BackgroundRuns_*</c> procs
/// (see <c>BackgroundRuns.sql</c>). Every read/write is scoped to <see cref="ITenantContext.TenantId"/>
/// so one tenant's runs are invisible to another; only restart-recovery crosses tenants (startup).
/// </summary>
public sealed class DapperBackgroundRunStore(IDbConnectionFactory factory, ITenantContext tenant)
    : IBackgroundRunStore
{
    private readonly IDbConnectionFactory _factory = factory;
    private readonly ITenantContext _tenant = tenant;

    /// <inheritdoc />
    public async Task<Guid> CreatePendingAsync(BackgroundRunRequest req, CancellationToken ct = default)
    {
        var runId = Guid.NewGuid();
        using var conn = await _factory.CreateAsync(ct);
        await conn.ExecuteAsync("dbo.BackgroundRuns_CreatePending", new
        {
            RunId = runId,
            TenantId = _tenant.TenantId,
            req.JobType,
            req.Payload,
            req.DedupKey,
        }, commandType: CommandType.StoredProcedure);
        return runId;
    }

    /// <inheritdoc />
    public async Task<Guid?> FindActiveByDedupAsync(string jobType, string? dedupKey, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateAsync(ct);
        return await conn.ExecuteScalarAsync<Guid?>("dbo.BackgroundRuns_FindActiveByDedup", new
        {
            TenantId = _tenant.TenantId,
            JobType = jobType,
            DedupKey = dedupKey,
        }, commandType: CommandType.StoredProcedure);
    }

    /// <inheritdoc />
    public async Task<BackgroundRunStatus?> GetAsync(Guid runId, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateAsync(ct);
        var row = await conn.QuerySingleOrDefaultAsync<StatusRow>("dbo.BackgroundRuns_Get", new
        {
            TenantId = _tenant.TenantId,
            RunId = runId,
        }, commandType: CommandType.StoredProcedure);
        return row is null
            ? null
            : new BackgroundRunStatus(
                row.RunId, row.JobType, (BackgroundRunState)row.State,
                row.ProgressCompleted, row.ProgressTotal, row.ResultRef, row.ErrorSummary,
                row.CreatedAtUtc, row.StartedAtUtc, row.CompletedAtUtc);
    }

    // Column-shaped DTO so Dapper maps by name (positional-record + enum defeats its ctor matcher).
    private sealed class StatusRow
    {
        public Guid RunId { get; init; }
        public string JobType { get; init; } = string.Empty;
        public byte State { get; init; }
        public int? ProgressCompleted { get; init; }
        public int? ProgressTotal { get; init; }
        public string? ResultRef { get; init; }
        public string? ErrorSummary { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime? StartedAtUtc { get; init; }
        public DateTime? CompletedAtUtc { get; init; }
    }

    /// <inheritdoc />
    public async Task<string?> GetPayloadAsync(Guid runId, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateAsync(ct);
        return await conn.ExecuteScalarAsync<string?>("dbo.BackgroundRuns_GetPayload", new
        {
            TenantId = _tenant.TenantId,
            RunId = runId,
        }, commandType: CommandType.StoredProcedure);
    }

    /// <inheritdoc />
    public async Task<bool> MarkRunningAsync(Guid runId, string ownerToken, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateAsync(ct);
        var rows = await conn.ExecuteScalarAsync<int>("dbo.BackgroundRuns_MarkRunning", new
        {
            TenantId = _tenant.TenantId,
            RunId = runId,
            OwnerToken = ownerToken,
        }, commandType: CommandType.StoredProcedure);
        return rows > 0;
    }

    /// <inheritdoc />
    public async Task<bool> MarkCompletedAsync(Guid runId, string? resultRef, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateAsync(ct);
        var rows = await conn.ExecuteScalarAsync<int>("dbo.BackgroundRuns_MarkCompleted", new
        {
            TenantId = _tenant.TenantId,
            RunId = runId,
            ResultRef = resultRef,
        }, commandType: CommandType.StoredProcedure);
        return rows > 0;
    }

    /// <inheritdoc />
    public async Task<bool> MarkFailedAsync(Guid runId, string errorSummary, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateAsync(ct);
        var rows = await conn.ExecuteScalarAsync<int>("dbo.BackgroundRuns_MarkFailed", new
        {
            TenantId = _tenant.TenantId,
            RunId = runId,
            ErrorSummary = Truncate(errorSummary, 2000),
        }, commandType: CommandType.StoredProcedure);
        return rows > 0;
    }

    /// <inheritdoc />
    public async Task UpdateProgressAsync(Guid runId, int completed, int? total, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateAsync(ct);
        await conn.ExecuteAsync("dbo.BackgroundRuns_UpdateProgress", new
        {
            TenantId = _tenant.TenantId,
            RunId = runId,
            Completed = completed,
            Total = total,
        }, commandType: CommandType.StoredProcedure);
    }

    /// <inheritdoc />
    public async Task<int> RecoverInterruptedAsync(CancellationToken ct = default)
    {
        using var conn = await _factory.CreateAsync(ct);
        return await conn.ExecuteScalarAsync<int>("dbo.BackgroundRuns_RecoverInterrupted",
            commandType: CommandType.StoredProcedure);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
