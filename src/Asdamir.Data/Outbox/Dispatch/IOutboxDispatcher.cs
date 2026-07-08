// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Data.Outbox;

/// <summary>
/// Strategy for sending a single claimed outbox message. One implementation per
/// <see cref="ClaimedOutboxMessage.MessageType"/>; the worker fans out by type.
/// Throw on transient failure (worker schedules retry with backoff). Throw a
/// <see cref="PermanentDispatchException"/> to skip retries and mark dead immediately.
/// </summary>
public interface IOutboxDispatcher
{
    /// <summary>Message type this dispatcher handles. 1=SMS, 2=Email.</summary>
    byte MessageType { get; }

    /// <summary>
    /// Deliver one claimed outbox message. Throw <see cref="PermanentDispatchException"/> for a
    /// non-retryable failure (moved to dead-letter); any other exception is treated as transient (retried).
    /// </summary>
    Task DispatchAsync(ClaimedOutboxMessage message, CancellationToken ct);
}

/// <summary>
/// Throw from a dispatcher to skip the retry schedule and route the message to Dead
/// (Status=4) on the next worker tick. Use when the failure is clearly non-transient
/// (invalid address, body too large, missing required attachment, etc.).
/// </summary>
public sealed class PermanentDispatchException : Exception
{
    /// <summary>Creates a permanent (non-retryable) dispatch failure with a message.</summary>
    public PermanentDispatchException(string message) : base(message) { }
    /// <summary>Creates a permanent (non-retryable) dispatch failure wrapping an inner cause.</summary>
    public PermanentDispatchException(string message, Exception inner) : base(message, inner) { }
}
