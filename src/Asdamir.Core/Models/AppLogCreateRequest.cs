// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.Collections;
using System.Text.Json;

namespace Asdamir.Core.Models;

/// <summary>
/// Input DTO used to insert a new <see cref="AppLog"/> row into the operator-only <c>dbo.AppLog</c> DB sink.
/// Carries the same failure detail as the persisted row minus the generated key; the static factories build a
/// pre-shaped request per severity. AppId-scoped within the central AsdamirVault.
/// </summary>
public class AppLogCreateRequest
{
    /// <summary>Severity to record (e.g. <c>Information</c>, <c>Warning</c>, <c>Error</c>); the factories set this per method.</summary>
    public string Level { get; set; } = string.Empty;
    /// <summary>Human-readable operator message to persist.</summary>
    public string Message { get; set; } = string.Empty;
    /// <summary>Structured contextual data serialized as JSON; null when no properties were supplied.</summary>
    public string? Properties { get; set; }
    /// <summary>Component/layer that emitted the entry (e.g. controller, job, tier); null when unspecified.</summary>
    public string? Source { get; set; }
    /// <summary>UTC timestamp for the entry; defaults to the moment the request is constructed.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Gerçek exception bilgileri
    /// <summary>Fully-qualified CLR type name of the captured exception; null for non-exception entries.</summary>
    public string? RealExceptionType { get; set; }
    /// <summary>The captured exception's own message (operator detail, not the localized user-facing text); null when no exception.</summary>
    public string? RealExceptionMessage { get; set; }
    /// <summary>Full stack trace of the captured exception; null when no exception was recorded.</summary>
    public string? RealExceptionStackTrace { get; set; }
    /// <summary>Flattened "type: message" of the exception's inner exception, if any; null otherwise.</summary>
    public string? RealExceptionInnerException { get; set; }
    /// <summary>The exception's <c>Data</c> dictionary serialized as JSON; null when the exception carried no data entries.</summary>
    public string? RealExceptionData { get; set; }

    // ErrorTranslations tablosuyla ilişki
    /// <summary>Stable error key linking the row to the localized user-facing message; null for non-error entries.</summary>
    public string? ErrorKey { get; set; }
    /// <summary>Culture in effect for the request (e.g. <c>tr-TR</c>) used to resolve the user-facing translation; null when unknown.</summary>
    public string? UserLanguage { get; set; }

    /// <summary>
    /// Builds an <c>Error</c>-level request, extracting the exception's type, message, stack trace, inner exception and
    /// <c>Data</c> into the dedicated real-exception fields and serializing <paramref name="properties"/> to JSON.
    /// </summary>
    /// <param name="message">Operator message describing the failure.</param>
    /// <param name="exception">Exception to capture into the real-exception fields; when null those fields stay unset.</param>
    /// <param name="source">Component/layer that emitted the entry.</param>
    /// <param name="properties">Structured context serialized to JSON into <see cref="Properties"/>.</param>
    /// <param name="errorKey">Stable error key linking to the localized user-facing message.</param>
    /// <param name="userLanguage">Request culture used to resolve the user-facing translation.</param>
    /// <returns>A populated <see cref="AppLogCreateRequest"/> ready to persist.</returns>
    public static AppLogCreateRequest CreateError(string message, Exception? exception = null, string? source = null, Dictionary<string, object>? properties = null, string? errorKey = null, string? userLanguage = null)
    {
        var request = new AppLogCreateRequest
        {
            Level = "Error",
            Message = message,
            Source = source,
            CreatedAtUtc = DateTime.UtcNow,
            ErrorKey = errorKey,
            UserLanguage = userLanguage
        };

        if (properties != null)
        {
            request.Properties = JsonSerializer.Serialize(properties);
        }

        if (exception != null)
        {
            // Gerçek exception bilgilerini ayrı alanlarda sakla
            request.RealExceptionType = exception.GetType().FullName ?? "Unknown";
            request.RealExceptionMessage = exception.Message;
            request.RealExceptionStackTrace = exception.StackTrace ?? "";
            
            if (exception.InnerException != null)
            {
                request.RealExceptionInnerException = $"{exception.InnerException.GetType().FullName}: {exception.InnerException.Message}";
            }

            // Exception data'sını JSON olarak sakla
            if (exception.Data.Count > 0)
            {
                var exceptionData = new Dictionary<string, object>();
                foreach (DictionaryEntry entry in exception.Data)
                {
                    exceptionData[entry.Key?.ToString() ?? "Unknown"] = entry.Value?.ToString() ?? "";
                }
                request.RealExceptionData = JsonSerializer.Serialize(exceptionData);
            }
        }

        return request;
    }

    /// <summary>Builds a <c>Warning</c>-level request with the given message/source, serializing <paramref name="properties"/> to JSON when supplied.</summary>
    /// <param name="message">Operator message describing the warning.</param>
    /// <param name="source">Component/layer that emitted the entry.</param>
    /// <param name="properties">Structured context serialized to JSON into <see cref="Properties"/>.</param>
    /// <returns>A populated <see cref="AppLogCreateRequest"/> ready to persist.</returns>
    public static AppLogCreateRequest CreateWarning(string message, string? source = null, Dictionary<string, object>? properties = null)
    {
        var request = new AppLogCreateRequest
        {
            Level = "Warning",
            Message = message,
            Source = source,
            CreatedAtUtc = DateTime.UtcNow
        };

        if (properties != null)
        {
            request.Properties = JsonSerializer.Serialize(properties);
        }

        return request;
    }

    /// <summary>Builds an <c>Information</c>-level request with the given message/source, serializing <paramref name="properties"/> to JSON when supplied.</summary>
    /// <param name="message">Operator message to record.</param>
    /// <param name="source">Component/layer that emitted the entry.</param>
    /// <param name="properties">Structured context serialized to JSON into <see cref="Properties"/>.</param>
    /// <returns>A populated <see cref="AppLogCreateRequest"/> ready to persist.</returns>
    public static AppLogCreateRequest CreateInformation(string message, string? source = null, Dictionary<string, object>? properties = null)
    {
        var request = new AppLogCreateRequest
        {
            Level = "Information",
            Message = message,
            Source = source,
            CreatedAtUtc = DateTime.UtcNow
        };

        if (properties != null)
        {
            request.Properties = JsonSerializer.Serialize(properties);
        }

        return request;
    }
}
