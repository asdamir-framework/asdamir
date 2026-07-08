// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.Models;

namespace Asdamir.Core.Contracts;

/// <summary>Store of error-key → localized-message rows backing <see cref="IErrorTranslationService"/>.</summary>
public interface IErrorTranslationRepository
{
    /// <summary>The translation row for an error key in one language, or null.</summary>
    Task<ErrorTranslation?> GetTranslationAsync(string errorKey, string language);
    /// <summary>All languages for an error key (culture→message).</summary>
    Task<Dictionary<string, string>> GetTranslationsAsync(string errorKey);
    /// <summary>Every error-translation row (for management/export).</summary>
    Task<List<ErrorTranslation>> GetAllTranslationsAsync();
}
