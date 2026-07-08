// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.Dtos;

/// <summary>An RBAC role and the set of permissions it grants, used by role-management screens.</summary>
public class RoleDto
{
    /// <summary>Identifier of the role.</summary>
    public int Id { get; set; }
    /// <summary>Role name (unique per app), e.g. "Admin".</summary>
    public string Name { get; set; } = "";
    /// <summary>Human-readable purpose of the role.</summary>
    public string Description { get; set; } = "";
    /// <summary>Whether the role is currently assignable; inactive roles are hidden/disabled.</summary>
    public bool IsActive { get; set; } = true;
    /// <summary>UTC timestamp when the role was created.</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>Number of users currently assigned this role (for management display).</summary>
    public int UserCount { get; set; }
    /// <summary>Permissions carried by this role; each entry's <see cref="PermissionDto.IsGranted"/> reflects whether the role holds it.</summary>
    public List<PermissionDto> Permissions { get; set; } = new();
}

/// <summary>A single permission and, in a role-editing context, whether the current role grants it.</summary>
public class PermissionDto
{
    /// <summary>Identifier of the permission.</summary>
    public int Id { get; set; }
    /// <summary>Permission key/string checked at authorization time, e.g. "orders.view".</summary>
    public string Name { get; set; } = "";
    /// <summary>Human-readable description of what the permission allows.</summary>
    public string Description { get; set; } = "";
    /// <summary>Grouping used to organize permissions in the UI, e.g. "Orders".</summary>
    public string Category { get; set; } = "";
    /// <summary>True when the role being edited currently grants this permission.</summary>
    public bool IsGranted { get; set; }
    /// <summary>True for built-in system permissions that must not be deleted/renamed.</summary>
    public bool IsSystemPermission { get; set; }
}
