// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.


namespace Asdamir.Core.Models;

/// <summary>1:1 persistence row of the transactional outbox table; exposed publicly because <c>IOutboxDbFunctions</c> operates on it directly.</summary>
public class OutboxMessageDbModel
{
    /// <summary>Primary key of the outbox row.</summary>
    public Guid Id { get; set; }
    /// <summary>Transport channel (SMS or Email) used to deliver this message.</summary>
    public OutboxMessageType MessageType { get; set; }
    /// <summary>To recipients — one or more addresses separated by ';' or ','.</summary>
    public string Destination { get; set; } = string.Empty;
    /// <summary>Optional CC recipients — one or more addresses separated by ';' or ','.</summary>
    public string? Cc { get; set; }
    /// <summary>Email subject line; null/ignored for SMS.</summary>
    public string? Subject { get; set; }
    /// <summary>Message payload — email body or SMS text.</summary>
    public string Body { get; set; } = string.Empty;
    /// <summary>Persisted lifecycle state as the numeric <see cref="OutboxStatus"/> value (Pending/Sending/Succeeded/Failed/Dead).</summary>
    public int Status { get; set; }
    /// <summary>Number of dispatch attempts made so far; compared against <see cref="MaxTryCount"/> before dead-lettering.</summary>
    public int TryCount { get; set; }
    /// <summary>Error detail from the most recent failed attempt; null if never failed.</summary>
    public string? LastError { get; set; }
    /// <summary>UTC time the message was enqueued (persisted with the business transaction).</summary>
    public DateTime CreatedAtUtc { get; set; }
    /// <summary>UTC time the next retry becomes eligible (back-off gate); null when not scheduled for retry.</summary>
    public DateTime? NextAttemptUtc { get; set; }
    /// <summary>Worker name currently holding the processing lock on this row; null when unlocked.</summary>
    public string? LockedBy { get; set; }
    /// <summary>UTC time the current lock was acquired; used to detect and reclaim stale locks.</summary>
    public DateTime? LockAcquiredUtc { get; set; }
    /// <summary>Maximum allowed dispatch attempts; once <see cref="TryCount"/> reaches it the message is dead-lettered.</summary>
    public int MaxTryCount { get; set; }
    /// <summary>Name of the application that enqueued the message; used for attribution and filtering.</summary>
    public string? SourceApp { get; set; }
    /// <summary>Human-readable correlation label linking this message to a business flow.</summary>
    public string? CorrelationKey { get; set; }
    /// <summary>Correlation identifier tying this message to a request/trace for end-to-end tracking.</summary>
    public Guid? CorrelationId { get; set; }
    /// <summary>Idempotency key used to suppress duplicate enqueues of the same logical message.</summary>
    public string? DedupKey { get; set; }
    /// <summary>UTC time of the last status/lock change to this row; null until first updated.</summary>
    public DateTime? UpdatedAtUtc { get; set; }
}
