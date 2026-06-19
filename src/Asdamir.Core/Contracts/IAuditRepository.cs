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

/// <summary>
/// Repository interface for persisting audit log entries.
/// Decouples audit logging from specific data access implementations.
/// </summary>
public interface IAuditRepository
{
    /// <summary>
    /// Persists an audit log entry asynchronously.
    /// </summary>
    /// <param name="entry">The audit entry to log containing user action details.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task LogAsync(AuditEntryDto entry);
}
