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

/// <summary>
/// One row from the <c>dbo.AppLog</c> operator log sink (Serilog DB sink): a persisted diagnostic
/// entry with its level, message, source, and — for errors — the full exception detail plus the
/// stable error key used to resolve a localized user-facing message.
/// </summary>
public class AppLogDto
{
    /// <summary>Auto-increment primary key of the log row.</summary>
    public long Id { get; set; }
    /// <summary>Serilog severity level ("Warning", "Error", "Fatal", …).</summary>
    public string Level { get; set; } = string.Empty;
    /// <summary>Rendered log message text.</summary>
    public string Message { get; set; } = string.Empty;
    /// <summary>JSON-serialized structured log properties (e.g. AppId, CaughtBy).</summary>
    public string? Properties { get; set; }
    /// <summary>UTC timestamp the log entry was written.</summary>
    public DateTime CreatedAtUtc { get; set; }
    /// <summary>Component/subsystem that emitted the entry (e.g. the failing app or middleware).</summary>
    public string? Source { get; set; }

    // Real exception details
    /// <summary>Fully-qualified CLR type name of the thrown exception, when the entry is an error.</summary>
    public string? ExceptionType { get; set; }
    /// <summary>Raw exception message (operator-facing detail, never shown to end-users).</summary>
    public string? ExceptionMessage { get; set; }
    /// <summary>Captured stack trace of the exception.</summary>
    public string? StackTrace { get; set; }
    /// <summary>Flattened detail of the inner exception chain, if any.</summary>
    public string? InnerException { get; set; }
    /// <summary>Serialized <c>Exception.Data</c> key/value bag, if populated.</summary>
    public string? ExceptionData { get; set; }

    // ErrorTranslations relationship
    /// <summary>Stable error key mapped from the exception, used to look up a localized message.</summary>
    public string? ErrorKey { get; set; }
    /// <summary>Culture the user saw the error in (drives which localized error string was shown).</summary>
    public string? UserLanguage { get; set; }
}
