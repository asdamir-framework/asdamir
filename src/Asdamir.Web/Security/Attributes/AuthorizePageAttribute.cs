// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.


namespace Asdamir.Web.Security.Attributes;

/// <summary>
/// Specifies that a Blazor page requires authorization
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class AuthorizePageAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the required permission for accessing the page
    /// </summary>
    public string? RequiredPermission { get; set; }

    /// <summary>
    /// Gets or sets the required role for accessing the page
    /// </summary>
    public string? RequiredRole { get; set; }

    /// <summary>
    /// Gets or sets multiple required permissions for accessing the page
    /// </summary>
    public string[]? RequiredPermissions { get; set; }

    /// <summary>
    /// Gets or sets multiple required roles for accessing the page
    /// </summary>
    public string[]? RequiredRoles { get; set; }

    /// <summary>
    /// Gets or sets whether all specified permissions/roles are required (AND logic) 
    /// or any of them (OR logic). Default is true (AND logic).
    /// </summary>
    public bool RequireAll { get; set; } = true;

    /// <summary>
    /// Gets or sets the redirect path when authorization fails. Default is "/login".
    /// </summary>
    public string RedirectTo { get; set; } = "/login";

    /// <summary>
    /// Gets or sets the path to navigate to when the user is authenticated but not authorized.
    /// Default is "/access-denied". Used by RouteAuthorizationMiddleware when authorization fails.
    /// </summary>
    public string AccessDeniedPath { get; set; } = "/access-denied";

    public AuthorizePageAttribute()
    {
    }

    public AuthorizePageAttribute(string requiredPermission)
    {
        RequiredPermission = requiredPermission;
    }

    public AuthorizePageAttribute(string[] requiredPermissions, bool requireAll = true)
    {
        RequiredPermissions = requiredPermissions;
        RequireAll = requireAll;
    }
}