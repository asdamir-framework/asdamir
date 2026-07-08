// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.Dtos;

/// <summary>A persisted transactional-outbox message (Mail/SMS) awaiting or tracking asynchronous dispatch by the outbox worker.</summary>
public class OutboxDto
{
    /// <summary>Unique identifier of this outbox row.</summary>
    public Guid Id { get; set; }
    /// <summary>Channel of the message: 1 = SMS, 2 = Email.</summary>
    public byte MessageType { get; set; } // 1=SMS, 2=Email
    /// <summary>Primary recipient — an email address or phone number depending on <see cref="MessageType"/>.</summary>
    public string Destination { get; set; } = string.Empty;
    /// <summary>Optional carbon-copy recipients (email only); null when not applicable.</summary>
    public string? Cc { get; set; }
    /// <summary>Email subject line; null for SMS.</summary>
    public string? Subject { get; set; }
    /// <summary>Message body/content to deliver.</summary>
    public string? Body { get; set; }
    /// <summary>Name of the app that enqueued this message, for attribution/filtering.</summary>
    public string? SourceApp { get; set; }
    /// <summary>Delivery lifecycle state: 0 = Pending, 1 = Sending, 2 = Succeeded, 3 = Failed, 4 = Dead (exhausted retries).</summary>
    public byte Status { get; set; } // 0=Pending, 1=Sending, 2=Succeeded, 3=Failed, 4=Dead
    /// <summary>Business correlation label (e.g. "PasswordReset") grouping related messages.</summary>
    public string? CorrelationKey { get; set; }
    /// <summary>Correlation id tying this message to an originating operation/request for tracing.</summary>
    public Guid? CorrelationId { get; set; }
    /// <summary>Number of delivery attempts made so far for this message.</summary>
    public int TryCount { get; set; }
    /// <summary>Maximum attempts before the message is marked Dead.</summary>
    public int MaxTryCount { get; set; }
    /// <summary>Error detail from the most recent failed dispatch attempt; null while healthy.</summary>
    public string? LastError { get; set; }
    /// <summary>UTC timestamp when the message was enqueued.</summary>
    public DateTime CreatedAtUtc { get; set; }
    /// <summary>UTC timestamp of the last status/attempt update.</summary>
    public DateTime UpdatedAtUtc { get; set; }
}
