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

public interface IMenuRepository
{
    Task<List<MenuDto>> GetAllAsync(int? parentId = null);
    Task<List<MenuDto>> GetByUserPermissionsAsync(int userId);
    Task<IEnumerable<MenuDto>> ListAsync(int? parentId = null);
    Task<List<MenuTreeDto>> GetMenuTreeAsync();
    Task<MenuDto?> GetByIdAsync(int id);
    Task<MenuDto> CreateAsync(CreateMenuRequestDto request);
    Task<MenuDto?> UpdateAsync(int id, UpdateMenuRequestDto request);
    Task<bool> DeleteAsync(int id);
    Task ReorderAsync(List<MenuOrderDto> menuOrders);
    Task<List<MenuPermissionDto>> GetUserMenuPermissionsAsync(int userId);
    Task UpdateUserMenuPermissionsAsync(int userId, List<MenuPermissionDto> permissions);
}
