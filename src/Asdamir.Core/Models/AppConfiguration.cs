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
/// A DB-backed configuration row in AsdamirVault (AppId-scoped) that Core's database configuration
/// source loads into <c>IConfiguration</c> at startup, letting settings be changed without a redeploy.
/// Only <see cref="IsActive"/> rows are loaded.
/// </summary>
public class AppConfiguration
{
    /// <summary>Primary key of the configuration row.</summary>
    public int Id { get; set; }

    /// <summary>Configuration key using the colon-delimited hierarchy expected by <c>IConfiguration</c> (e.g. <c>Session:IdleSeconds</c>).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>The raw string value; if <see cref="IsEncrypted"/> it is ciphertext that must be decrypted before use.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Only active rows are loaded into <c>IConfiguration</c>; false hides the setting without deleting the row.</summary>
    public bool IsActive { get; set; }

    /// <summary>When true <see cref="Value"/> is stored encrypted at rest and is decrypted on load.</summary>
    public bool IsEncrypted { get; set; }

    /// <summary>Optional grouping label used to organize settings in the admin UI; not part of the config key.</summary>
    public string? Category { get; set; }

    /// <summary>Human-readable note explaining what the setting controls; for operators, not consumed at runtime.</summary>
    public string? Description { get; set; }

    /// <summary>UTC timestamp when the row was first created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the last change; used to detect edits on refresh.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>Identifier of the operator who created the row, for audit purposes.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>Identifier of the operator who last modified the row, for audit purposes.</summary>
    public string? UpdatedBy { get; set; }
}
