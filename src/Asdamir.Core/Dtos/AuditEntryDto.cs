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

/// <summary>
/// A single audit-trail record: who performed which action on which entity, with the before/after
/// snapshots and request context needed to reconstruct the change later.
/// </summary>
public record AuditEntryDto
{
    /// <summary>UTC moment the audited action occurred.</summary>
    public DateTime Timestamp { get; init; }
    /// <summary>Operation performed (e.g. "Create", "Update", "Delete", "Login").</summary>
    public string Action { get; init; } = string.Empty;
    /// <summary>Logical name/type of the affected entity (e.g. "User", "Order").</summary>
    public string Entity { get; init; } = string.Empty;
    /// <summary>Primary-key of the affected entity instance; null for entity-less actions.</summary>
    public string? EntityId { get; init; }
    /// <summary>Identifier of the actor who performed the action; null when anonymous/system.</summary>
    public string? UserId { get; init; }
    /// <summary>Display name of the actor at the time of the action.</summary>
    public string? UserName { get; init; }
    /// <summary>Tenant/app scope the action belonged to, for multi-tenant filtering.</summary>
    public string? TenantId { get; init; }
    /// <summary>Client IP address the request originated from.</summary>
    public string? Ip { get; init; }
    /// <summary>User-agent string of the client that issued the request.</summary>
    public string? UserAgent { get; init; }
    /// <summary>JSON snapshot of the entity's state before the change (null on create).</summary>
    public string? OldValuesJson { get; init; }
    /// <summary>JSON snapshot of the entity's state after the change (null on delete).</summary>
    public string? NewValuesJson { get; init; }
    /// <summary>JSON bag of extra, action-specific context that doesn't fit the fixed columns.</summary>
    public string? ExtraJson { get; init; }
    /// <summary>Human-readable summary of the action for display in the audit log.</summary>
    public string? Description { get; init; }
}
