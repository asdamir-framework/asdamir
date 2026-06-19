// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Web.Security.Models;

namespace Asdamir.Web.Security.Services;

/// <summary>
/// Service for auditing authorization events
/// </summary>
public interface IAuthorizationAuditService
{
    /// <summary>
    /// Logs an authorization attempt
    /// </summary>
    Task LogAuthorizationAttemptAsync(AuthorizationAuditEvent auditEvent);

    /// <summary>
    /// Gets recent authorization failures for a user
    /// </summary>
    Task<List<AuthorizationAuditEvent>> GetRecentFailuresAsync(string userId, int minutes = 15);

    /// <summary>
    /// Gets authorization failure count for a user within a time window
    /// </summary>
    Task<int> GetFailureCountAsync(string userId, int minutes = 15);
}
