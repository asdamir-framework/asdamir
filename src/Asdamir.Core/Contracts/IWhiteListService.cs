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
/// Service for managing SMS WhiteList operations.
///
/// Audit fix vs. v1: synchronous overloads (<c>InsertWhiteListSync</c>, <c>DeactivateWhiteListSync</c>)
/// have been removed. They wrapped <c>HttpClient.SendAsync(...).GetAwaiter().GetResult()</c>, which
/// on any thread-pool-starved code path (Blazor Server circuit, Hangfire job, ASP.NET request)
/// pinned a TP thread and risked dead-locks. All call sites must use the async API.
/// </summary>
public interface IWhiteListService
{
    /// <summary>
    /// Adds a phone number to the SMS WhiteList asynchronously.
    /// </summary>
    Task<InsertWhiteListResponse> InsertWhiteListAsync(SendInsertWhiteListRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates a phone number from the SMS WhiteList asynchronously.
    /// </summary>
    Task<bool> DeactivateWhiteListAsync(DeactivateWhiteListRequest request, CancellationToken cancellationToken = default);
}
