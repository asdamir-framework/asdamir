// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.Dtos;

namespace Asdamir.Core.Contracts;

/// <summary>
/// Localization-resource store (the DB-backed strings, per key + culture): management CRUD, import/export,
/// and the per-culture key→value map the UI is warmed from.
/// </summary>
public interface ILocalizationRepository
{
    /// <summary>The most recent resources for the management list (capped at <paramref name="top"/>).</summary>
    Task<IReadOnlyList<ResourceDto>> ListResourcesAsync(int top = 200);
    /// <summary>Exports resources, optionally for a single culture (null = all cultures).</summary>
    Task<IReadOnlyList<ResourceDto>> ExportAsync(string? culture);
    /// <summary>Imports/upserts a batch of resources; returns the number of rows affected.</summary>
    Task<int> ImportAsync(LocalizationImportDto input);
    /// <summary>Finds a resource by id, or null if not found.</summary>
    Task<ResourceDto?> GetByIdAsync(int id);
    /// <summary>Creates a resource and returns the created record.</summary>
    Task<ResourceDto> CreateAsync(CreateResourceRequestDto request);
    /// <summary>Updates a resource; returns true if a row changed.</summary>
    Task<bool> UpdateAsync(int id, UpdateResourceRequestDto request);
    /// <summary>Deletes a resource; returns true if a row was removed.</summary>
    Task<bool> DeleteAsync(int id);
    
    /// <summary>
    /// Get all localization resources for a specific culture as key-value dictionary
    /// </summary>
    Task<Dictionary<string, string>> GetResourcesByCultureAsync(string cultureName);
}
