// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Web.Security.Models;

/// <summary>
/// Audit event for authorization attempts
/// </summary>
public class AuthorizationAuditEvent
{
    public string UserId { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string? RequiredPermission { get; set; }
    public string? RequiredRole { get; set; }
    public string[]? RequiredPermissions { get; set; }
    public string[]? RequiredRoles { get; set; }
    public bool IsAuthorized { get; set; }
    public string? DenialReason { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}

/// <summary>
/// Result of authorization check with detailed information
/// </summary>
public class AuthorizationResult
{
    public bool IsAuthorized { get; set; }
    public string? DenialReason { get; set; }
    public List<string> MissingPermissions { get; set; } = new();
    public List<string> MissingRoles { get; set; } = new();
}
