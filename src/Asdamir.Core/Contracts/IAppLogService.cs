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

/// <summary>
/// Writes structured application-log entries to the DB sink (<c>dbo.AppLog</c>, AppId-scoped) — the operator
/// channel of the two-channel error model. One method per severity, plus a details overload carrying an error key.
/// </summary>
public interface IAppLogService
{
    /// <summary>Writes an entry at the given level with optional source and structured properties.</summary>
    Task LogAsync(string level, string message, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default);
    /// <summary>Writes an Error entry, capturing the exception (message + stack) when supplied.</summary>
    Task LogErrorAsync(string message, Exception? exception = null, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default);
    /// <summary>Writes a Warning entry.</summary>
    Task LogWarningAsync(string message, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default);
    /// <summary>Writes an Information entry.</summary>
    Task LogInformationAsync(string message, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default);
    /// <summary>Writes a Critical entry, capturing the exception when supplied.</summary>
    Task LogCriticalAsync(string message, Exception? exception = null, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default);
    /// <summary>Writes a Debug entry (verbose; typically off in production).</summary>
    Task LogDebugAsync(string message, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes an Error entry enriched for error monitoring — carries the stable <paramref name="errorKey"/> and the
    /// user's language so the operator record ties back to the localized message shown to the end-user.
    /// </summary>
    Task LogErrorWithDetailsAsync(string message, Exception? exception = null, string? source = null, Dictionary<string, object>? properties = null, string? errorKey = null, string? userLanguage = null, CancellationToken cancellationToken = default);
}

