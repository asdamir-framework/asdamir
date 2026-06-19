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

public class AppLogCreateRequest
{
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Properties { get; set; }
    public string? Source { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    
    // Gerçek exception bilgileri
    public string? RealExceptionType { get; set; }
    public string? RealExceptionMessage { get; set; }
    public string? RealExceptionStackTrace { get; set; }
    public string? RealExceptionInnerException { get; set; }
    public string? RealExceptionData { get; set; }
    
    // ErrorTranslations tablosuyla ilişki
    public string? ErrorKey { get; set; }
    public string? UserLanguage { get; set; }

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
