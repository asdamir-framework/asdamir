// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.Models;

/// <summary>
/// One persisted row of the <c>dbo.AppLog</c> DB sink — the operator-only error channel written by
/// <c>GlobalExceptionMiddleware</c> (Warning+), holding the full failure detail (level, message, source,
/// exception internals, error key, culture). AppId-scoped within the central AsdamirVault; never surfaced to end-users.
/// </summary>
public class AppLog
{
    /// <summary>Auto-incrementing primary key of the log row in <c>dbo.AppLog</c>.</summary>
    public long Id { get; set; }
    /// <summary>Severity of the entry as a string (e.g. <c>Information</c>, <c>Warning</c>, <c>Error</c>); the DB sink persists Warning and above.</summary>
    public string Level { get; set; } = string.Empty;
    /// <summary>Human-readable operator message describing what happened.</summary>
    public string Message { get; set; } = string.Empty;
    /// <summary>Structured contextual data serialized as JSON (Serilog properties, request context); null when none was attached.</summary>
    public string? Properties { get; set; }
    /// <summary>UTC timestamp at which the entry was recorded.</summary>
    public DateTime CreatedAtUtc { get; set; }
    /// <summary>Component/layer that emitted the entry (e.g. controller, job, tier) — helps locate the failing app/tier; null when unspecified.</summary>
    public string? Source { get; set; }

    // Gerçek exception bilgileri
    /// <summary>Fully-qualified CLR type name of the captured exception; null for non-exception entries.</summary>
    public string? RealExceptionType { get; set; }
    /// <summary>The captured exception's own message (operator detail, distinct from the localized user-facing text); null when no exception.</summary>
    public string? RealExceptionMessage { get; set; }
    /// <summary>Full stack trace of the captured exception; null when no exception was recorded.</summary>
    public string? RealExceptionStackTrace { get; set; }
    /// <summary>Flattened "type: message" of the exception's inner exception, if any; null otherwise.</summary>
    public string? RealExceptionInnerException { get; set; }
    /// <summary>The exception's <c>Data</c> dictionary serialized as JSON; null when the exception carried no data entries.</summary>
    public string? RealExceptionData { get; set; }

    // ErrorTranslations tablosuyla ilişki
    /// <summary>Stable error key linking this operator row to the localized user-facing message; null for non-error entries.</summary>
    public string? ErrorKey { get; set; }
    /// <summary>Culture in effect for the request when the error occurred (e.g. <c>tr-TR</c>), used to resolve the user-facing translation; null when unknown.</summary>
    public string? UserLanguage { get; set; }

    // Navigation property (opsiyonel)
    /// <summary>Optional resolved localized translation for <see cref="ErrorKey"/>; populated only when the row is joined to its <c>ErrorTranslation</c>.</summary>
    public ErrorTranslation? ErrorTranslation { get; set; }

    // Helper method - gerçek exception bilgilerini JSON olarak döndür
    /// <summary>Serializes the captured exception fields (type, message, stack trace, inner exception, data) into a single JSON object; returns an empty string when no exception was recorded.</summary>
    public string GetRealExceptionAsJson()
    {
        if (string.IsNullOrEmpty(RealExceptionType))
            return string.Empty;
            
        var exceptionInfo = new
        {
            Type = RealExceptionType,
            Message = RealExceptionMessage,
            StackTrace = RealExceptionStackTrace,
            InnerException = RealExceptionInnerException,
            Data = RealExceptionData
        };
        
        return System.Text.Json.JsonSerializer.Serialize(exceptionInfo);
    }
    
    // Helper method - exception bilgilerinin var olup olmadığını kontrol et
    /// <summary>Returns <c>true</c> when this entry carries captured exception detail (i.e. <see cref="RealExceptionType"/> is set).</summary>
    public bool HasRealException()
    {
        return !string.IsNullOrEmpty(RealExceptionType);
    }
}
