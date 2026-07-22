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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Asdamir.Data.BackgroundRuns;

/// <summary>
/// The hosted RUNNER: reads work items off <see cref="BackgroundRunQueue"/> and executes each run's
/// app-supplied <see cref="IBackgroundJobHandler"/> under the run's own tenant scope, driving the
/// state machine (Pending → Running → Completed|Failed) via <see cref="IBackgroundRunStore"/>.
/// <para>
/// A generated Gateway registers NO Hangfire server, so this primitive ships its OWN runner: an
/// <see cref="BackgroundService"/> + an in-process channel (no external broker). Concurrency is bounded
/// by <c>BackgroundRunOptions.MaxConcurrency</c>. Because each run re-establishes tenant scope from the
/// work item, a run executes under the enqueuing request's tenant — not the host's.
/// </para>
/// </summary>
public sealed class BackgroundRunProcessor(
    BackgroundRunQueue queue,
    IServiceScopeFactory scopes,
    IReadOnlyDictionary<string, IBackgroundJobHandler> handlers,
    IOptions<BackgroundRunOptions> options,
    ILogger<BackgroundRunProcessor> logger)
    : BackgroundService
{
    private readonly BackgroundRunQueue _queue = queue;
    private readonly IServiceScopeFactory _scopes = scopes;
    private readonly IReadOnlyDictionary<string, IBackgroundJobHandler> _handlers = handlers;
    private readonly BackgroundRunOptions _opts = options.Value;
    private readonly ILogger<BackgroundRunProcessor> _logger = logger;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var maxConcurrency = Math.Max(1, _opts.MaxConcurrency);
        var gate = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        var nodeId = _opts.NodeId ?? $"{Environment.MachineName}#{Environment.ProcessId}";
        _logger.LogInformation("BackgroundRunProcessor started (node={NodeId}, maxConcurrency={Max}).", nodeId, maxConcurrency);

        try
        {
            await foreach (var item in _queue.ReadAllAsync(stoppingToken))
            {
                await gate.WaitAsync(stoppingToken);
                _ = Task.Run(async () =>
                {
                    try { await RunOneAsync(item, nodeId, stoppingToken); }
                    finally { gate.Release(); }
                }, CancellationToken.None);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // graceful shutdown
        }

        _logger.LogInformation("BackgroundRunProcessor stopping.");
    }

    private async Task RunOneAsync(BackgroundRunWorkItem item, string nodeId, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var sp = scope.ServiceProvider;

        // Re-establish the run's tenant scope inside this background scope. The scoped
        // ITenantContext falls back to "default" without an HttpContext, so set it explicitly.
        if (sp.GetRequiredService<ITenantContext>() is TenantContext tc)
        {
            tc.TenantId = item.TenantId;
            tc.IsMultiTenant = true;
        }

        var store = sp.GetRequiredService<IBackgroundRunStore>();

        var status = await store.GetAsync(item.RunId, ct);
        if (status is null)
        {
            _logger.LogWarning("BackgroundRun {RunId} not found for tenant {Tenant} — skipping.", item.RunId, item.TenantId);
            return;
        }

        if (!_handlers.TryGetValue(status.JobType, out var handler))
        {
            if (await store.MarkRunningAsync(item.RunId, nodeId, ct))
                await store.MarkFailedAsync(item.RunId, $"No handler registered for JobType '{status.JobType}'.", ct);
            _logger.LogError("BackgroundRun {RunId}: no handler for JobType {JobType}.", item.RunId, status.JobType);
            return;
        }

        // Guarded Pending -> Running. 0 rows => someone else owns it (or it's already terminal): skip.
        if (!await store.MarkRunningAsync(item.RunId, nodeId, ct))
        {
            _logger.LogInformation("BackgroundRun {RunId} not in Pending state — another owner or already terminal.", item.RunId);
            return;
        }

        var payload = await store.GetPayloadAsync(item.RunId, ct);
        var reporter = new ThrottledProgressReporter(store, item.RunId, _opts);
        try
        {
            // Hand the handler ONE context carrying everything the runner already knows. context.RunId
            // is the store record's RunId (== the enqueued id == GetStatusAsync(runId).RunId), so the
            // handler can write a two-way back-link from its own business record to the run row.
            var context = new BackgroundRunContext(item.RunId, item.TenantId, payload, reporter, ct);
            var resultRef = await handler.ExecuteAsync(context);
            await reporter.FlushFinalAsync(ct);
            await store.MarkCompletedAsync(item.RunId, resultRef, ct);
            _logger.LogInformation("BackgroundRun {RunId} ({JobType}) completed.", item.RunId, status.JobType);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutdown mid-run: leave it Running; restart-recovery flips it to Interrupted next boot.
            _logger.LogWarning("BackgroundRun {RunId} cancelled by shutdown — will recover as Interrupted.", item.RunId);
        }
        catch (Exception ex)
        {
            await reporter.FlushFinalAsync(CancellationToken.None);
            await store.MarkFailedAsync(item.RunId, ex.Message, CancellationToken.None);
            _logger.LogError(ex, "BackgroundRun {RunId} ({JobType}) failed.", item.RunId, status.JobType);
        }
    }
}
