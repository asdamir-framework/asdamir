// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

/// <summary>
/// A single DB-backed localization resource row (from <c>LocalizationResource</c>): the translated
/// value of one key for one culture, categorized by usage (UI / VALIDATION / ERROR).
/// </summary>
public sealed class ResourceDto
{
    /// <summary>Auto-increment primary key of the resource row.</summary>
    public int Id { get; set; }
    /// <summary>Culture code the value belongs to (e.g. "tr-TR", "en-US", "ru-RU").</summary>
    public string Culture { get; set; } = string.Empty;
    /// <summary>Stable lookup key resolved via <c>IStringLocalizer</c> (e.g. "Common.Save").</summary>
    public string Key { get; set; } = string.Empty;
    /// <summary>Translated text for this key/culture; null when not yet provided.</summary>
    public string? Value { get; set; }
    /// <summary>Usage category grouping the key ("UI", "VALIDATION", "ERROR").</summary>
    public string? Category { get; set; }
    /// <summary>Tenant scope for a tenant-specific override; null for a shared/global resource.</summary>
    public Guid? TenantId { get; set; }
    /// <summary>True when the value contains HTML markup and must be rendered unescaped.</summary>
    public bool IsHtml { get; set; }
    /// <summary>UTC timestamp the resource row was created.</summary>
    public DateTime CreatedAtUtc { get; set; }
    /// <summary>UTC timestamp of the last edit to the value.</summary>
    public DateTime UpdatedAtUtc { get; set; }
}