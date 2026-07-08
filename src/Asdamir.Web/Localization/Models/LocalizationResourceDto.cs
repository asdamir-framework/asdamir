// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Web.Localization.Models;

/// <summary>
/// DTO for API communication (Blazor ↔ API).
/// Represents a localized resource with key, culture, and value.
/// </summary>
public class LocalizationResourceDto
{
    /// <summary>The resource key (e.g. <c>Common.Save</c>) that UI code resolves.</summary>
    public required string Key { get; set; }

    /// <summary>The culture this value belongs to (e.g. <c>tr-TR</c>, <c>en-US</c>, <c>ru-RU</c>).</summary>
    public required string Culture { get; set; }

    /// <summary>The localized text for the key in the given culture.</summary>
    public required string Value { get; set; }
}
