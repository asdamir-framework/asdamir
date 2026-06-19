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

public class AppLog
{
    public long Id { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Properties { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? Source { get; set; }
    
    // Gerçek exception bilgileri
    public string? RealExceptionType { get; set; }
    public string? RealExceptionMessage { get; set; }
    public string? RealExceptionStackTrace { get; set; }
    public string? RealExceptionInnerException { get; set; }
    public string? RealExceptionData { get; set; }
    
    // ErrorTranslations tablosuyla ilişki
    public string? ErrorKey { get; set; }
    public string? UserLanguage { get; set; }
    
    // Navigation property (opsiyonel)
    public ErrorTranslation? ErrorTranslation { get; set; }
    
    // Helper method - gerçek exception bilgilerini JSON olarak döndür
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
    public bool HasRealException()
    {
        return !string.IsNullOrEmpty(RealExceptionType);
    }
}
