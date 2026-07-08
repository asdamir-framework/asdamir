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

/// <summary>Flat representation of a single AppId-scoped menu row (AsdamirVault); the source list the UI folds into a <see cref="MenuTreeDto"/> hierarchy.</summary>
public class MenuDto
{
    /// <summary>Primary key of the menu row in AsdamirVault.</summary>
    public int Id { get; set; }

    /// <summary>Raw menu name; the fallback label only — the UI prefers the localized key derived from <see cref="Url"/>.</summary>
    [Required(ErrorMessage = "Menu name is required")]
    [StringLength(100, ErrorMessage = "Menu name cannot exceed 100 characters")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional free-text note describing the item's purpose (admin-facing).</summary>
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }

    /// <summary>Optional icon identifier rendered beside the label; null shows no icon.</summary>
    public string? Icon { get; set; }

    /// <summary>Client-side route this item navigates to (e.g. "/order-items"); also the source for the localization key (→ "Menu.OrderItems"). Null for a non-navigable grouping node.</summary>
    [StringLength(200, ErrorMessage = "URL cannot exceed 200 characters")]
    public string? Url { get; set; }

    /// <summary>Id of the parent menu item; null for a top-level item.</summary>
    public int? ParentId { get; set; }

    /// <summary>Ascending display order among siblings.</summary>
    [Range(0, 9999, ErrorMessage = "Order must be between 0 and 9999")]
    public int Order { get; set; }

    /// <summary>Whether the item is enabled; inactive rows are excluded from the served menu.</summary>
    public bool IsActive { get; set; }

    /// <summary>Whether the item is shown in the nav (an active-but-hidden item can still gate a route).</summary>
    public bool IsVisible { get; set; }

    /// <summary>Id of the permission that gates this item (its <c>&lt;x&gt;.view</c> permission); null means ungated.</summary>
    public int? PermissionId { get; set; }

    /// <summary>UTC timestamp when the row was created.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>UTC timestamp of the last update; null if never updated since creation.</summary>
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>Identifier of the operator who created the row; null if unattributed.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>Identifier of the operator who last updated the row; null if never updated.</summary>
    public string? UpdatedBy { get; set; }
}

/// <summary>Payload to create a new menu item under a given app; server assigns the Id and timestamps.</summary>
public class CreateMenuRequestDto
{
    /// <summary>Raw menu name; the fallback label when no localized key is seeded for the item's route.</summary>
    [Required(ErrorMessage = "Menu name is required")]
    [StringLength(100, ErrorMessage = "Menu name cannot exceed 100 characters")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional free-text note describing the item's purpose (admin-facing).</summary>
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }

    /// <summary>Optional icon identifier rendered beside the label; null shows no icon.</summary>
    public string? Icon { get; set; }

    /// <summary>Client-side route the item navigates to; also the source for the derived localization key. Null for a grouping node.</summary>
    [StringLength(200, ErrorMessage = "URL cannot exceed 200 characters")]
    public string? Url { get; set; }

    /// <summary>Id of the parent menu item; null to create a top-level item.</summary>
    public int? ParentId { get; set; }

    /// <summary>Ascending display order among siblings.</summary>
    [Range(0, 9999, ErrorMessage = "Order must be between 0 and 9999")]
    public int Order { get; set; }

    /// <summary>Whether the item is enabled on creation (defaults to true).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Whether the item is shown in the nav on creation (defaults to true).</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>Id of the permission that gates this item; null creates it ungated.</summary>
    public int? PermissionId { get; set; }
}

/// <summary>Payload to update an existing menu item's fields (the target Id is supplied out-of-band, e.g. route).</summary>
public class UpdateMenuRequestDto
{
    /// <summary>New raw menu name; the fallback label when no localized key is seeded for the item's route.</summary>
    [Required(ErrorMessage = "Menu name is required")]
    [StringLength(100, ErrorMessage = "Menu name cannot exceed 100 characters")]
    public string Name { get; set; } = string.Empty;

    /// <summary>New free-text note describing the item's purpose; null clears it.</summary>
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }

    /// <summary>New icon identifier rendered beside the label; null shows no icon.</summary>
    public string? Icon { get; set; }

    /// <summary>New client-side route the item navigates to; also the source for the derived localization key. Null for a grouping node.</summary>
    [StringLength(200, ErrorMessage = "URL cannot exceed 200 characters")]
    public string? Url { get; set; }

    /// <summary>New parent menu item id; null promotes the item to top-level.</summary>
    public int? ParentId { get; set; }

    /// <summary>Ascending display order among siblings.</summary>
    [Range(0, 9999, ErrorMessage = "Order must be between 0 and 9999")]
    public int Order { get; set; }

    /// <summary>Whether the item is enabled after the update.</summary>
    public bool IsActive { get; set; }

    /// <summary>Whether the item is shown in the nav after the update.</summary>
    public bool IsVisible { get; set; }

    /// <summary>Id of the permission that gates this item; null makes it ungated.</summary>
    public int? PermissionId { get; set; }
}

/// <summary>Minimal payload to reorder/re-parent a menu item during a drag-and-drop or bulk reorder operation.</summary>
public class MenuOrderDto
{
    /// <summary>Id of the menu item being repositioned.</summary>
    public int Id { get; set; }

    /// <summary>New ascending display order among siblings.</summary>
    public int Order { get; set; }

    /// <summary>New parent id after the move; null places the item at top-level.</summary>
    public int? ParentId { get; set; }
}

/// <summary>Per-menu access flags for a given user/role, used to render and evaluate menu permission grids.</summary>
public class MenuPermissionDto
{
    /// <summary>Id of the menu item these flags apply to.</summary>
    public int MenuId { get; set; }

    /// <summary>Display name of the menu item (grid label for the permission row).</summary>
    public string MenuName { get; set; } = string.Empty;

    /// <summary>Whether any access at all is granted to this menu item.</summary>
    public bool HasPermission { get; set; }

    /// <summary>Whether the subject may view the item.</summary>
    public bool CanView { get; set; }

    /// <summary>Whether the subject may create records under the item.</summary>
    public bool CanCreate { get; set; }

    /// <summary>Whether the subject may edit records under the item.</summary>
    public bool CanEdit { get; set; }

    /// <summary>Whether the subject may delete records under the item.</summary>
    public bool CanDelete { get; set; }
}