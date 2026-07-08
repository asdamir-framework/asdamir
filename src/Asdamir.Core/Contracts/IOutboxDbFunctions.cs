// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System;
using Asdamir.Core.Models;

namespace Asdamir.Core.Contracts;

/// <summary>
/// Low-level transactional-outbox DB operations used by the outbox worker: enqueue, claim-a-batch
/// (with a per-row lock), status transitions, the status-change audit trail, and single-row read.
/// </summary>
public interface IOutboxDbFunctions
{
    /// <summary>Enqueues a message for later delivery (within the caller's transaction).</summary>
    Task EnqueueAsync(OutboxMessage message, CancellationToken ct = default);
    /// <summary>Atomically claims up to <paramref name="batchSize"/> pending messages for a worker (locks them).</summary>
    Task<List<OutboxMessageDbModel>> ClaimBatchAsync(int batchSize, string workerName, CancellationToken ct = default);
    /// <summary>Transitions a message to a new status, recording an error + the holding worker.</summary>
    Task UpdateStatusAsync(Guid id, byte newStatus, string? error, string lockedBy, CancellationToken ct = default);
    /// <summary>Appends a status-change row to the message's audit trail (old→new, try count, note/error).</summary>
    Task LogStatusChangeAsync(Guid messageId, byte oldStatus, byte newStatus, int tryCount, string? note, string? error, CancellationToken ct = default);
    /// <summary>Reads a single outbox message by id, or null.</summary>
    Task<OutboxMessageDbModel?> GetAsync(Guid id, CancellationToken ct = default);
}
