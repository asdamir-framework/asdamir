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
/// Immutable, in-flight description of an audited event as it is being recorded — the input shape that is
/// persisted as an <see cref="AuditLog"/> row (who did what to which entity, with before/after state).
/// </summary>
public sealed record AuditEntry
{
    /// <summary>UTC instant the audited action occurred; defaults to the moment the entry is constructed.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>The action performed (e.g. "Create", "Update", "Delete") on the audited entity.</summary>
    public string Action { get; init; } = string.Empty;

    /// <summary>Name/type of the entity that was acted upon (e.g. "User", "Order").</summary>
    public string Entity { get; init; } = string.Empty;

    /// <summary>Identifier of the specific entity instance affected; null when not applicable.</summary>
    public string? EntityId { get; init; }

    /// <summary>Identifier of the user who performed the action; null for system/anonymous actions.</summary>
    public string? UserId { get; init; }

    /// <summary>Display name of the acting user, captured for readability; null if unknown.</summary>
    public string? UserName { get; init; }

    /// <summary>Tenant/app scope the action belongs to; null for cross-tenant/system actions.</summary>
    public string? TenantId { get; init; }

    /// <summary>Client IP address the request originated from; null if unavailable.</summary>
    public string? Ip { get; init; }

    /// <summary>User-agent string of the originating client; null if unavailable.</summary>
    public string? UserAgent { get; init; }

    /// <summary>JSON snapshot of the entity's state before the action; null for creates or when not captured.</summary>
    public string? OldValuesJson { get; init; }

    /// <summary>JSON snapshot of the entity's state after the action; null for deletes or when not captured.</summary>
    public string? NewValuesJson { get; init; }

    /// <summary>JSON bag of additional context not covered by the typed fields; null when none.</summary>
    public string? ExtraJson { get; init; }
}
