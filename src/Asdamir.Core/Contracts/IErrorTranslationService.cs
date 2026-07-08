// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.Contracts;

/// <summary>
/// Resolves a stable error key to a localized, user-facing message (the two-channel error rule). Values
/// come from the localization store; supports parameter substitution and a safe fallback.
/// </summary>
public interface IErrorTranslationService
{
    /// <summary>The localized message for an error key in one language, with optional parameter substitution.</summary>
    Task<string> GetTranslatedMessageAsync(string errorKey, string language, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default);
    /// <summary>The localized message for an error key in every configured language (culture→message).</summary>
    Task<Dictionary<string, string>> GetTranslatedMessagesAsync(string errorKey, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default);
    /// <summary>A neutral fallback message when no translation exists for the key — never exposes the raw key/exception.</summary>
    Task<string> GetFallbackMessageAsync(string errorKey, string language, Exception? exception = null, CancellationToken cancellationToken = default);
}

