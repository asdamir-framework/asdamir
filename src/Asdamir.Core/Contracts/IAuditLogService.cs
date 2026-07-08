// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.Models;

namespace Asdamir.Core.Contracts;

/// <summary>
/// Records a business audit entry (entity + action + optional before/after values). Higher-level than
/// <see cref="IAuditLogRepository"/> — it composes the row from the current request/user context.
/// </summary>
public interface IAuditLogService
{
    /// <summary>Records an action on an entity, capturing old/new values and optional actor + description.</summary>
    Task LogActionAsync(string entity, string action, string? entityId = null, string? tenantId = null, string? oldValues = null, string? newValues = null, string? extra = null, string? userId = null, string? userName = null, string? description = null);
    /// <summary>Records an action already attributed to a known user (userId + userName supplied by the caller).</summary>
    Task LogUserActionAsync(string userId, string userName, string entity, string action, string? entityId = null, string? tenantId = null, string? description = null);
}



