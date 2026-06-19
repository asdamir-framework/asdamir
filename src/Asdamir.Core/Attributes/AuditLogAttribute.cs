// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.Attributes;

/// <summary>
/// Attribute to enable audit logging for API controllers and actions.
/// Can be applied at controller or action level.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class AuditLogAttribute : Attribute
{
    /// <summary>
    /// Custom entity name. If not specified, will be inferred from controller name.
    /// </summary>
    public string? Entity { get; set; }

    /// <summary>
    /// Custom action name. If not specified, will be inferred from HTTP method.
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// Whether to capture request body in audit log.
    /// Default: true
    /// </summary>
    public bool CaptureRequestBody { get; set; } = true;

    /// <summary>
    /// Whether to capture response body in audit log.
    /// Default: false (for performance)
    /// </summary>
    public bool CaptureResponseBody { get; set; } = false;

    /// <summary>
    /// Whether to include query string parameters in audit log.
    /// Default: true
    /// </summary>
    public bool IncludeQueryString { get; set; } = true;

    /// <summary>
    /// Exclude specific properties from request body logging (comma-separated).
    /// Example: "Password,CreditCard,SSN"
    /// </summary>
    public string? ExcludeProperties { get; set; }

    /// <summary>
    /// Whether this action requires authentication for audit.
    /// If false, will log even for anonymous users.
    /// Default: true
    /// </summary>
    public bool RequireAuthentication { get; set; } = true;
}
