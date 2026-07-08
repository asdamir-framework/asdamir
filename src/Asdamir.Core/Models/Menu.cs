// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.Models;

/// <summary>
/// A central navigation entry (stored in AsdamirVault, AppId-scoped) describing one item in an app's
/// menu tree: its route, label, icon, parent, sort order and the permission required to see it. The UI
/// derives a stable localization key from <see cref="Url"/> and renders the localized label, falling
/// back to <see cref="Name"/> only when no key is seeded.
/// </summary>
public class Menu
{
    /// <summary>Surrogate primary key of the menu row.</summary>
    public int Id { get; set; }

    /// <summary>Raw label text; used only as the fallback when no localization key is seeded for the item.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional free-text description of what the menu item leads to.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Name/identifier of the icon rendered next to the label (FluentUI icon key).</summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>Client-side route the item navigates to (e.g. "/users"); also the source of the derived localization key for the label.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Id of the parent menu; null for a top-level item.</summary>
    public int? ParentId { get; set; }

    /// <summary>Sort position among siblings; lower values appear first.</summary>
    public int Order { get; set; }

    /// <summary>When false, the item is disabled/soft-deleted and not served to the UI.</summary>
    public bool IsActive { get; set; }

    /// <summary>When false, the item is hidden from the rendered menu even though it remains active.</summary>
    public bool IsVisible { get; set; }

    /// <summary>Id of the permission a user must hold to see this item; null means it is visible to everyone.</summary>
    public int? PermissionId { get; set; }

    /// <summary>UTC timestamp when the row was created.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>UTC timestamp of the last update; null if never updated.</summary>
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>Identifier of the operator who created the row.</summary>
    public string CreatedBy { get; set; } = string.Empty;

    /// <summary>Identifier of the operator who last updated the row.</summary>
    public string UpdatedBy { get; set; } = string.Empty;
}
