// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

/// <summary>Payload for bulk-importing localized strings for a single culture into the LocalizationResource table.</summary>
public sealed class LocalizationImportDto
{
    /// <summary>Culture code (tr-TR / en-US / ru-RU) all imported items apply to; null defers to each item.</summary>
    public string? Culture { get; set; }
    /// <summary>Resource key/value entries to upsert in this import batch.</summary>
    public List<ResourceDto> Items { get; set; } = new();
}