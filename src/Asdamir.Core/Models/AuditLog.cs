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

/// <summary>
/// A persisted audit-trail record capturing who did what to which entity, with before/after state — the
/// stored, queryable counterpart of <see cref="AuditEntry"/>, kept for compliance and forensics.
/// </summary>
public class AuditLog
{
    /// <summary>Surrogate primary key of the audit row.</summary>
    public long Id { get; set; }

    /// <summary>UTC instant at which the audited action occurred.</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>The action performed (e.g. "Create", "Update", "Delete") on the audited entity.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Name/type of the entity that was acted upon (e.g. "User", "Order").</summary>
    public string Entity { get; set; } = string.Empty;

    /// <summary>Identifier of the specific entity instance affected; null when not applicable.</summary>
    public string? EntityId { get; set; }

    /// <summary>Identifier of the user who performed the action; null for system/anonymous actions.</summary>
    public string? UserId { get; set; }

    /// <summary>Display name of the acting user, captured for readability; null if unknown.</summary>
    public string? UserName { get; set; }

    /// <summary>Tenant/app scope the action belongs to; null for cross-tenant/system actions.</summary>
    public string? TenantId { get; set; }

    /// <summary>Client IP address the request originated from; null if unavailable.</summary>
    public string? Ip { get; set; }

    /// <summary>User-agent string of the originating client; null if unavailable.</summary>
    public string? UserAgent { get; set; }

    /// <summary>JSON snapshot of the entity's state before the action; null for creates or when not captured.</summary>
    public string? OldValuesJson { get; set; }

    /// <summary>JSON snapshot of the entity's state after the action; null for deletes or when not captured.</summary>
    public string? NewValuesJson { get; set; }

    /// <summary>JSON bag of additional context not covered by the typed fields; null when none.</summary>
    public string? ExtraJson { get; set; }

    /// <summary>Optional human-readable summary of the audited action.</summary>
    public string? Description { get; set; }
}
