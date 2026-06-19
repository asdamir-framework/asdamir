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
/// Row shape returned by <c>dbo.Outbox_ClaimBatch</c>. Worker-only DTO (not exposed via API);
/// kept distinct from <see cref="OutboxMessageDto"/> so admin-view formatting can evolve without
/// breaking worker dispatch.
/// </summary>
public sealed record ClaimedOutboxMessage(
    Guid Id,
    byte MessageType,        // 1 = SMS, 2 = Email
    string Destination,
    string? Subject,
    string Body,
    string? SourceApp,
    string? CorrelationKey,
    Guid? CorrelationId,
    string? DedupKey,
    byte Status,
    int TryCount,
    int MaxTryCount,
    DateTime NextAttemptUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string? LockedBy,
    DateTime? LockAcquiredUtc,
    string? LastError,
    string[]? ToAddresses,
    string[]? CcAddresses,
    string[]? BccAddresses,
    string? ReplyTo,
    string? FromAddress,
    string? FromName,
    bool IsHtml,
    string? AttachmentsJson);
