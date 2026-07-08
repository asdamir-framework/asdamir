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
/// Navigation-menu store: the menu catalog, per-user visible menus (by permission), the menu tree,
/// CRUD, ordering, and per-user menu-permission grants. Scoped per app/tenant by the host.
/// </summary>
public interface IMenuRepository
{
    /// <summary>All menu items, optionally filtered to a parent (null = top level).</summary>
    Task<List<MenuDto>> GetAllAsync(int? parentId = null);
    /// <summary>Menu items the user may see, resolved from their permissions.</summary>
    Task<List<MenuDto>> GetByUserPermissionsAsync(int userId);
    /// <summary>Flat menu list, optionally filtered to a parent (enumerable form of <see cref="GetAllAsync"/>).</summary>
    Task<IEnumerable<MenuDto>> ListAsync(int? parentId = null);
    /// <summary>The full menu hierarchy as a parent/child tree.</summary>
    Task<List<MenuTreeDto>> GetMenuTreeAsync();
    /// <summary>Finds a menu item by id, or null if not found.</summary>
    Task<MenuDto?> GetByIdAsync(int id);
    /// <summary>Creates a menu item and returns the created record.</summary>
    Task<MenuDto> CreateAsync(CreateMenuRequestDto request);
    /// <summary>Updates a menu item; returns the updated record, or null if not found.</summary>
    Task<MenuDto?> UpdateAsync(int id, UpdateMenuRequestDto request);
    /// <summary>Deletes a menu item; returns true if a row was removed.</summary>
    Task<bool> DeleteAsync(int id);
    /// <summary>Applies a new display order to the given menu items.</summary>
    Task ReorderAsync(List<MenuOrderDto> menuOrders);
    /// <summary>Returns the per-user menu-visibility overrides for a user.</summary>
    Task<List<MenuPermissionDto>> GetUserMenuPermissionsAsync(int userId);
    /// <summary>Replaces the per-user menu-visibility overrides for a user.</summary>
    Task UpdateUserMenuPermissionsAsync(int userId, List<MenuPermissionDto> permissions);
}
