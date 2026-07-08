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

/// <summary>Delivery channel of an outbox message — which transport dispatches it.</summary>
public enum OutboxMessageType : byte
{
    /// <summary>Deliver via SMS (Twilio dispatcher).</summary>
    Sms = 1,
    /// <summary>Deliver via email (mail dispatcher).</summary>
    Email = 2
}

/// <summary>Lifecycle state of an outbox message as it moves through the dispatch pipeline.</summary>
public enum OutboxStatus : byte
{
    /// <summary>Persisted with the business transaction, awaiting its first dispatch attempt.</summary>
    Pending = 0,
    /// <summary>Currently locked by a worker and being dispatched.</summary>
    Sending = 1,
    /// <summary>Delivered to the transport successfully; terminal.</summary>
    Succeeded = 2,
    /// <summary>Last attempt failed but retries remain; will be retried after the back-off window.</summary>
    Failed = 3,
    /// <summary>Exhausted its retry budget (dead-lettered); terminal, no further attempts.</summary>
    Dead = 4
}

/// <summary>Configuration for the outbox dispatch worker — connection, worker identity, and claim batch size.</summary>
public sealed class OutboxOptions
{
    /// <summary>Connection string of the database holding the outbox table the worker polls.</summary>
    public string ConnectionString { get; set; } = string.Empty;
    /// <summary>Identifier this worker stamps on rows it locks; distinguishes competing workers.</summary>
    public string WorkerName { get; set; } = "outbox-worker";
    /// <summary>Maximum number of pending messages claimed and dispatched per poll cycle.</summary>
    public int BatchSize { get; set; } = 25;
}

/// <summary>A message already persisted in the outbox and ready for dispatch (carries its assigned id).</summary>
public sealed class OutboxItem
{
    /// <summary>Unique identifier of the persisted outbox row.</summary>
    public Guid Id { get; set; }
    /// <summary>Transport channel (SMS or Email) used to deliver this message.</summary>
    public OutboxMessageType MessageType { get; set; }

    /// <summary>To recipients. One or more addresses separated by ';' or ','.</summary>
    public string Destination { get; set; } = string.Empty;

    /// <summary>Optional CC recipients. One or more addresses separated by ';' or ','.</summary>
    public string? Cc { get; set; }

    /// <summary>Email subject line; null/ignored for SMS.</summary>
    public string? Subject { get; set; }
    /// <summary>Message payload — email body or SMS text.</summary>
    public string Body { get; set; } = string.Empty;
}

/// <summary>An outgoing message enqueued into the outbox in the same transaction as the business change (id is null until persisted).</summary>
public sealed class OutboxMessage
{
    /// <summary>Assigned identifier once persisted; null when a new message is being enqueued.</summary>
    public Guid? Id { get; set; }
    /// <summary>Transport channel (SMS or Email) to deliver this message.</summary>
    public OutboxMessageType MessageType { get; set; }

    /// <summary>To recipients. One or more addresses separated by ';' or ','.</summary>
    public string Destination { get; set; } = string.Empty;

    /// <summary>Optional CC recipients. One or more addresses separated by ';' or ','.</summary>
    public string? Cc { get; set; }

    /// <summary>Email subject line; null/ignored for SMS.</summary>
    public string? Subject { get; set; }
    /// <summary>Message payload — email body or SMS text.</summary>
    public string Body { get; set; } = string.Empty;
    /// <summary>Name of the application that enqueued the message; used for attribution and filtering.</summary>
    public string? SourceApp { get; set; }
    /// <summary>Human-readable correlation label linking this message to a business flow.</summary>
    public string? CorrelationKey { get; set; }
    /// <summary>Correlation identifier tying this message to a request/trace for end-to-end tracking.</summary>
    public Guid? CorrelationId { get; set; }
    /// <summary>Idempotency key used to suppress duplicate enqueues of the same logical message.</summary>
    public string? DedupKey { get; set; }
}
