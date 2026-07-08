// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.Dtos;

namespace Asdamir.Core.Contracts;

/// <summary>Read-only access to application log entries for the log-viewer UI (list + export).</summary>
public interface ILogRepository
{
    /// <summary>The most recent log entries (capped at <paramref name="top"/>).</summary>
    Task<IReadOnlyList<LogEntryDto>> ListAsync(int top = 200);
    /// <summary>Log entries for export, filtered by level and/or a date range.</summary>
    Task<IReadOnlyList<LogEntryDto>> ExportAsync(string? level, DateTime? startDate, DateTime? endDate);
}
