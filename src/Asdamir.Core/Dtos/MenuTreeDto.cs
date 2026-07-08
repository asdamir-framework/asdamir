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

/// <summary>Hierarchical (parent→children) form of a menu item, built from the flat <see cref="MenuDto"/> list for nav rendering.</summary>
public class MenuTreeDto
{
    /// <summary>Primary key of the menu row in AsdamirVault.</summary>
    public int Id { get; set; }

    /// <summary>Raw menu name; the fallback label only — the UI prefers the localized key derived from <see cref="Url"/>.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional free-text note describing the item's purpose (admin-facing).</summary>
    public string? Description { get; set; }

    /// <summary>Optional icon identifier rendered beside the label; null shows no icon.</summary>
    public string? Icon { get; set; }

    /// <summary>Client-side route this item navigates to (e.g. "/order-items"); also the source for the localization key (→ "Menu.OrderItems"). Null for a grouping node.</summary>
    public string? Url { get; set; }

    /// <summary>Id of the parent menu item; null for a top-level (root) node.</summary>
    public int? ParentId { get; set; }

    /// <summary>Ascending display order among siblings.</summary>
    public int Order { get; set; }

    /// <summary>Whether the item is enabled; inactive nodes are excluded from the served menu.</summary>
    public bool IsActive { get; set; }

    /// <summary>Whether the item is shown in the nav (defaults to true; an active-but-hidden node can still gate a route).</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>Id of the permission that gates this item (its <c>&lt;x&gt;.view</c> permission); null means ungated.</summary>
    public int? PermissionId { get; set; }

    /// <summary>UTC timestamp when the row was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the last update; null if never updated since creation.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Identifier of the operator who created the row; null if unattributed.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>Identifier of the operator who last updated the row; null if never updated.</summary>
    public string? UpdatedBy { get; set; }

    /// <summary>Nested child items, ordered by <see cref="Order"/> (empty for a leaf).</summary>
    public List<MenuTreeDto> Children { get; set; } = new();
}
