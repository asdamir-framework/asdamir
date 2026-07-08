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

/// <summary>Role store: the role catalog and each role's permission grants (RBAC).</summary>
public interface IRoleRepository
{
    /// <summary>All roles in the current scope.</summary>
    Task<List<RoleDto>> GetAllAsync();
    /// <summary>Finds a role by id, or null if not found.</summary>
    Task<RoleDto?> GetByIdAsync(int id);
    /// <summary>The permissions granted to a role.</summary>
    Task<List<PermissionDto>> GetRolePermissionsAsync(int roleId);
    /// <summary>Replaces a role's permission grants with the given permission ids.</summary>
    Task UpdateRolePermissionsAsync(int roleId, List<int> permissionIds);
}

/// <summary>Permission catalog store (read-only): the permissions RBAC roles can grant.</summary>
public interface IPermissionRepository
{
    /// <summary>All permissions in the catalog.</summary>
    Task<List<PermissionDto>> GetAllAsync();
    /// <summary>Finds a permission by id, or null if not found.</summary>
    Task<PermissionDto?> GetByIdAsync(int id);
    /// <summary>Permissions filtered to a category (grouping in the UI).</summary>
    Task<List<PermissionDto>> GetByCategoryAsync(string category);
}
