// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.Threading.Channels;

namespace Asdamir.Data.BackgroundRuns;

/// <summary>
/// The tenant + run id a queued work item carries. The runner re-resolves the tenant scope from
/// this so the handler + store execute under the enqueuing request's tenant, not the host's.
/// </summary>
/// <param name="RunId">The persisted run to execute.</param>
/// <param name="TenantId">The tenant the run belongs to.</param>
public readonly record struct BackgroundRunWorkItem(Guid RunId, string TenantId);

/// <summary>
/// In-process hand-off between <c>BackgroundRunService.EnqueueAsync</c> (producer) and the
/// <c>BackgroundRunProcessor</c> hosted runner (consumer). Registered as a singleton. This is the
/// SIGNAL only — the durable state is the persisted <c>BackgroundRuns</c> row, so a run survives a
/// restart even though this channel does not (restart-recovery flips any orphan to Interrupted).
/// </summary>
public sealed class BackgroundRunQueue
{
    private readonly Channel<BackgroundRunWorkItem> _channel =
        Channel.CreateUnbounded<BackgroundRunWorkItem>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
        });

    /// <summary>Signals the runner that a persisted (Pending) run is ready to execute.</summary>
    public ValueTask EnqueueAsync(BackgroundRunWorkItem item, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(item, ct);

    /// <summary>The consumer stream the hosted runner reads work items from.</summary>
    public IAsyncEnumerable<BackgroundRunWorkItem> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
