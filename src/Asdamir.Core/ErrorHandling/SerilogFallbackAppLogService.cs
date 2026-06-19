// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.Contracts;
﻿using Microsoft.Extensions.Logging;

namespace Asdamir.Core.ErrorHandling.Services;

/// <summary>
/// Fallback implementation of IAppLogService that logs to Serilog.
/// Used when no database-backed AppLogService is registered.
/// </summary>
internal class SerilogFallbackAppLogService : IAppLogService
{
    private readonly ILogger<SerilogFallbackAppLogService> _logger;

    public SerilogFallbackAppLogService(ILogger<SerilogFallbackAppLogService> logger)
    {
        _logger = logger;
    }

    public Task LogAsync(string level, string message, string? source = null, 
        Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default)
    {
        var logLevel = ParseLogLevel(level);
        _logger.Log(logLevel, "[{Source}] {Message} {@Properties}", 
            source ?? "Unknown", message, properties);
        
        return Task.CompletedTask;
    }

    public Task LogErrorAsync(string message, Exception? exception = null, 
        string? source = null, Dictionary<string, object>? properties = null, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogError(exception, "[{Source}] {Message} {@Properties}", 
            source ?? "Unknown", message, properties);
        
        return Task.CompletedTask;
    }

    public Task LogWarningAsync(string message, string? source = null, 
        Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("[{Source}] {Message} {@Properties}", 
            source ?? "Unknown", message, properties);
        
        return Task.CompletedTask;
    }

    public Task LogInformationAsync(string message, string? source = null, 
        Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[{Source}] {Message} {@Properties}", 
            source ?? "Unknown", message, properties);
        
        return Task.CompletedTask;
    }

    public Task LogCriticalAsync(string message, Exception? exception = null, 
        string? source = null, Dictionary<string, object>? properties = null, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogCritical(exception, "[{Source}] {Message} {@Properties}", 
            source ?? "Unknown", message, properties);
        
        return Task.CompletedTask;
    }

    public Task LogDebugAsync(string message, string? source = null, 
        Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[{Source}] {Message} {@Properties}", 
            source ?? "Unknown", message, properties);
        
        return Task.CompletedTask;
    }

    public Task LogErrorWithDetailsAsync(string message, Exception? exception = null, 
        string? source = null, Dictionary<string, object>? properties = null, 
        string? errorKey = null, string? userLanguage = null, CancellationToken cancellationToken = default)
    {
        var enrichedProperties = properties ?? new Dictionary<string, object>();
        if (errorKey != null) enrichedProperties["ErrorKey"] = errorKey;
        if (userLanguage != null) enrichedProperties["UserLanguage"] = userLanguage;

        _logger.LogError(exception, "[{Source}] {Message} {@Properties}", 
            source ?? "Unknown", message, enrichedProperties);
        
        return Task.CompletedTask;
    }

    private static LogLevel ParseLogLevel(string level) => level switch
    {
        "Critical" => LogLevel.Critical,
        "Error" => LogLevel.Error,
        "Warning" => LogLevel.Warning,
        "Information" => LogLevel.Information,
        "Debug" => LogLevel.Debug,
        "Trace" => LogLevel.Trace,
        _ => LogLevel.Information
    };
}

