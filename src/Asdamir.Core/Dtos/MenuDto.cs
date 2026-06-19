// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.ComponentModel.DataAnnotations;

namespace Asdamir.Core.Dtos;

public class MenuDto
{
    public int Id { get; set; }
    
    [Required(ErrorMessage = "Menu name is required")]
    [StringLength(100, ErrorMessage = "Menu name cannot exceed 100 characters")]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }
    
    public string? Icon { get; set; }
    
    [StringLength(200, ErrorMessage = "URL cannot exceed 200 characters")]
    public string? Url { get; set; }
    
    public int? ParentId { get; set; }
    
    [Range(0, 9999, ErrorMessage = "Order must be between 0 and 9999")]
    public int Order { get; set; }
    
    public bool IsActive { get; set; }
    public bool IsVisible { get; set; }
    public int? PermissionId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}

public class CreateMenuRequestDto
{
    [Required(ErrorMessage = "Menu name is required")]
    [StringLength(100, ErrorMessage = "Menu name cannot exceed 100 characters")]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }
    
    public string? Icon { get; set; }
    
    [StringLength(200, ErrorMessage = "URL cannot exceed 200 characters")]
    public string? Url { get; set; }
    
    public int? ParentId { get; set; }
    
    [Range(0, 9999, ErrorMessage = "Order must be between 0 and 9999")]
    public int Order { get; set; }
    
    public bool IsActive { get; set; } = true;
    public bool IsVisible { get; set; } = true;
    public int? PermissionId { get; set; }
}

public class UpdateMenuRequestDto
{
    [Required(ErrorMessage = "Menu name is required")]
    [StringLength(100, ErrorMessage = "Menu name cannot exceed 100 characters")]
    public string Name { get; set; } = string.Empty;
    
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }
    
    public string? Icon { get; set; }
    
    [StringLength(200, ErrorMessage = "URL cannot exceed 200 characters")]
    public string? Url { get; set; }
    
    public int? ParentId { get; set; }
    
    [Range(0, 9999, ErrorMessage = "Order must be between 0 and 9999")]
    public int Order { get; set; }
    
    public bool IsActive { get; set; }
    public bool IsVisible { get; set; }
    public int? PermissionId { get; set; }
}

public class MenuOrderDto
{
    public int Id { get; set; }
    public int Order { get; set; }
    public int? ParentId { get; set; }
}

public class MenuPermissionDto
{
    public int MenuId { get; set; }
    public string MenuName { get; set; } = string.Empty;
    public bool HasPermission { get; set; }
    public bool CanView { get; set; }
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}