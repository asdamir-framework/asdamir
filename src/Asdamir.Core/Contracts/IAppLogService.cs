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

public interface IAppLogService
{
    Task LogAsync(string level, string message, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default);
    Task LogErrorAsync(string message, Exception? exception = null, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default);
    Task LogWarningAsync(string message, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default);
    Task LogInformationAsync(string message, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default);
    Task LogCriticalAsync(string message, Exception? exception = null, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default);
    Task LogDebugAsync(string message, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default);
    
    // DetaylÄ± error logging
    Task LogErrorWithDetailsAsync(string message, Exception? exception = null, string? source = null, Dictionary<string, object>? properties = null, string? errorKey = null, string? userLanguage = null, CancellationToken cancellationToken = default);
}

