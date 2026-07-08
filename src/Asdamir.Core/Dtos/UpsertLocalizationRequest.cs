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

/// <summary>Request to insert-or-update a single localized string in the DB-backed LocalizationResource table for one culture.</summary>
/// <param name="Key">Stable resource key (e.g. "Menu.Users", "error.unauthorized") — identical across all cultures.</param>
/// <param name="Culture">Target culture code (tr-TR / en-US / ru-RU) whose value is written.</param>
/// <param name="Value">Localized text stored for the key in this culture.</param>
/// <param name="Category">Resource bucket: UI, VALIDATION, or ERROR; null keeps/derives the existing category.</param>
/// <param name="TenantId">AppId scope the row belongs to; null targets the shared/default scope.</param>
/// <param name="IsHtml">True when the value contains HTML markup to be rendered unescaped.</param>
public record UpsertLocalizationRequest(
    string Key,
    string Culture,
    string Value,
    string? Category = null,
    Guid? TenantId = null,
    bool IsHtml = false
);
