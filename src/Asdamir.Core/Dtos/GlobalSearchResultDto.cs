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

/// <summary>
/// Global search result DTO
/// </summary>
public class GlobalSearchResultDto
{
    /// <summary>
    /// Result ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Title/Name of the result
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Description or excerpt
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Module/Entity type (e.g., "Users", "Products", "Menu", "Localization")
    /// </summary>
    public string Module { get; set; } = string.Empty;

    /// <summary>
    /// Icon for the module
    /// </summary>
    public string Icon { get; set; } = "📄";

    /// <summary>
    /// Navigation URL
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Match score/relevance (0-100)
    /// </summary>
    public int Score { get; set; }

    /// <summary>
    /// Last modified date
    /// </summary>
    public DateTime? LastModified { get; set; }
}
