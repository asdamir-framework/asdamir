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
    /// <summary>Gets or sets the identifier of the user whose access attempt is being audited.</summary>
    public string UserId { get; set; } = string.Empty;
    /// <summary>Gets or sets the email address of the user whose access attempt is being audited.</summary>
    public string UserEmail { get; set; } = string.Empty;
    /// <summary>Gets or sets the route (path or URL) the user attempted to access.</summary>
    public string Route { get; set; } = string.Empty;
    /// <summary>Gets or sets the single permission required for the route, when the decision was permission-based.</summary>
    public string? RequiredPermission { get; set; }
    /// <summary>Gets or sets the single role required for the route, when the decision was role-based.</summary>
    public string? RequiredRole { get; set; }
    /// <summary>Gets or sets the set of permissions required for the route, when multiple are involved.</summary>
    public string[]? RequiredPermissions { get; set; }
    /// <summary>Gets or sets the set of roles required for the route, when multiple are involved.</summary>
    public string[]? RequiredRoles { get; set; }
    /// <summary>Gets or sets a value indicating whether access was granted (<c>true</c>) or denied (<c>false</c>).</summary>
    public bool IsAuthorized { get; set; }
    /// <summary>Gets or sets the reason access was denied; <c>null</c> when access was granted.</summary>
    public string? DenialReason { get; set; }
    /// <summary>Gets or sets the UTC time the authorization attempt occurred. Defaults to the current UTC time.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    /// <summary>Gets or sets the IP address the request originated from, when known.</summary>
    public string? IpAddress { get; set; }
    /// <summary>Gets or sets the user agent of the client that made the request, when known.</summary>
    public string? UserAgent { get; set; }
}

/// <summary>
/// Result of authorization check with detailed information
/// </summary>
public class AuthorizationResult
{
    /// <summary>Gets or sets a value indicating whether the authorization check passed.</summary>
    public bool IsAuthorized { get; set; }
    /// <summary>Gets or sets a human-readable reason the check failed; <c>null</c> when authorized.</summary>
    public string? DenialReason { get; set; }
    /// <summary>Gets or sets the permissions the user was missing that caused the denial.</summary>
    public List<string> MissingPermissions { get; set; } = new();
    /// <summary>Gets or sets the roles the user was missing that caused the denial.</summary>
    public List<string> MissingRoles { get; set; } = new();
}
