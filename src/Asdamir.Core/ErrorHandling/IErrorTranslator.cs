// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.ErrorHandling.Abstractions;

/// <summary>
/// Interface for translating error codes to user-friendly messages
/// </summary>
public interface IErrorTranslator
{
    /// <summary>
    /// Translates an error code to user's language
    /// </summary>
    /// <param name="errorCode">Error code to translate</param>
    /// <param name="userLanguage">User's language (e.g., "en", "tr")</param>
    /// <returns>Translated error message</returns>
    Task<string> Translate(string errorCode, string userLanguage);
}
