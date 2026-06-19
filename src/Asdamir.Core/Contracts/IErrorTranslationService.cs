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

public interface IErrorTranslationService
{
    Task<string> GetTranslatedMessageAsync(string errorKey, string language, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default);
    Task<Dictionary<string, string>> GetTranslatedMessagesAsync(string errorKey, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default);
    Task<string> GetFallbackMessageAsync(string errorKey, string language, Exception? exception = null, CancellationToken cancellationToken = default);
}

