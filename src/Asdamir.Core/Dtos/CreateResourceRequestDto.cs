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

/// <summary>Request to create a new localized string in the LocalizationResource table for one culture.</summary>
public class CreateResourceRequestDto
{
    /// <summary>Stable resource key for the new entry (e.g. "Menu.Orders") — reused across cultures.</summary>
    public string Key { get; set; } = "";
    /// <summary>Culture code (tr-TR / en-US / ru-RU) this value is created for.</summary>
    public string Culture { get; set; } = "";
    /// <summary>Localized text to store for the key in this culture.</summary>
    public string Value { get; set; } = "";
    /// <summary>Resource bucket: UI, VALIDATION, or ERROR.</summary>
    public string Category { get; set; } = "";
}
