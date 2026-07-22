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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Asdamir.Data.BackgroundRuns;

/// <summary>
/// Startup restart-recovery. On boot it flips every run left <c>Pending</c>/<c>Running</c> by a
/// prior process that died to <see cref="BackgroundRunState.Interrupted"/> — so NO ghost "Running"
/// survives a restart. Runs once at <see cref="StartAsync"/> (before the processor picks up new work),
/// across all tenants.
/// <para>
/// SINGLE-NODE assumption: with one node this is correct, since a boot means the previous owner is
/// gone. In a multi-node deployment this would wrongly interrupt a peer's live runs — distributed run
/// ownership / leader election is a KNOWN LIMITATION, not solved here (see the HA note in the docs).
/// </para>
/// </summary>
public sealed class BackgroundRunRecoveryService(
    IServiceScopeFactory scopes,
    ILogger<BackgroundRunRecoveryService> logger)
    : IHostedService
{
    private readonly IServiceScopeFactory _scopes = scopes;
    private readonly ILogger<BackgroundRunRecoveryService> _logger = logger;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IBackgroundRunStore>();
            var recovered = await store.RecoverInterruptedAsync(cancellationToken);
            if (recovered > 0)
                _logger.LogWarning("BackgroundRun recovery: {Count} run(s) left by a prior process marked Interrupted.", recovered);
            else
                _logger.LogInformation("BackgroundRun recovery: no orphaned runs.");
        }
        catch (Exception ex)
        {
            // Never block host startup on recovery; the processor + guarded transitions still hold.
            _logger.LogError(ex, "BackgroundRun recovery failed at startup.");
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
