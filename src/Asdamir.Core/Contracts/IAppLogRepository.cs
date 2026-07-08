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

/// <summary>Application-log store (<c>dbo.AppLog</c>): append a log row + read it back for the error/monitoring UI.</summary>
public interface IAppLogRepository
{
    /// <summary>Inserts a log row; returns its new id.</summary>
    Task<long> CreateAsync(AppLogCreateRequest request);
    /// <summary>The most recent log rows (newest first).</summary>
    Task<List<AppLog>> GetRecentAsync(int count = 100);
    /// <summary>The most recent rows at a given level (Error/Warning/…).</summary>
    Task<List<AppLog>> GetByLevelAsync(string level, int count = 100);
    /// <summary>The most recent rows from a given source component.</summary>
    Task<List<AppLog>> GetBySourceAsync(string source, int count = 100);
    /// <summary>Rows logged within a date range.</summary>
    Task<List<AppLog>> GetByDateRangeAsync(DateTime from, DateTime to, int count = 100);
    /// <summary>Finds a log row by id, or null.</summary>
    Task<AppLog?> GetByIdAsync(long id);
    /// <summary>Purges rows older than the retention window (housekeeping).</summary>
    Task DeleteOldLogsAsync(int retentionDays = 30);
}
