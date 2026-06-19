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

public interface IOutboxDbFunctions
{
    Task EnqueueAsync(OutboxMessage message, CancellationToken ct = default);
    Task<List<OutboxMessageDbModel>> ClaimBatchAsync(int batchSize, string workerName, CancellationToken ct = default);
    Task UpdateStatusAsync(Guid id, byte newStatus, string? error, string lockedBy, CancellationToken ct = default);
    Task LogStatusChangeAsync(Guid messageId, byte oldStatus, byte newStatus, int tryCount, string? note, string? error, CancellationToken ct = default);
    Task<OutboxMessageDbModel?> GetAsync(Guid id, CancellationToken ct = default);
}
