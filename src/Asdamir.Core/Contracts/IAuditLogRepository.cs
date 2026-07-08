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

/// <summary>Audit-trail store (who did what, when): filtered read + append + retention housekeeping.</summary>
public interface IAuditLogRepository
{
    /// <summary>Filtered, paged audit rows (by date range, entity, action, user).</summary>
    Task<List<AuditLog>> GetLogsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? entity = null,
        string? action = null,
        string? userId = null,
        int pageSize = 100);

    /// <summary>Finds an audit row by id, or null.</summary>
    Task<AuditLog?> GetByIdAsync(long id);

    /// <summary>Appends an audit row; returns its new id.</summary>
    Task<long> CreateAsync(AuditLog auditLog);

    /// <summary>Purges audit rows older than the retention window.</summary>
    Task DeleteOldLogsAsync(int retentionDays = 90);
}
