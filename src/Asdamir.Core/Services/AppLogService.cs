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
using System.Text.Json;

namespace Asdamir.Core.Contracts;

/// <summary>
/// Default <c>IAppLogService</c> — persists structured log rows to the <c>dbo.AppLog</c> DB sink
/// (AppId-scoped) via <c>IAppLogRepository</c>. This is the operator channel of the two-channel
/// error model: full technical detail (level, source, serialized properties, exception dump) goes
/// here for operators, never to end-users. Convenience level methods (<c>LogErrorAsync</c>,
/// <c>LogWarningAsync</c>, …) funnel through the core <c>LogAsync</c>; exception details are
/// captured as a nested property object.
/// </summary>
public class AppLogService : IAppLogService
{
    private readonly IAppLogRepository _repository;

    /// <summary>Creates the service over the <c>dbo.AppLog</c>-backed repository that persists each entry.</summary>
    /// <param name="repository">Repository that writes log rows to the <c>dbo.AppLog</c> sink.</param>
    public AppLogService(IAppLogRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc/>
    public async Task LogAsync(string level, string message, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default)
    {
        var request = new AppLogCreateRequest
        {
            Level = level,
            Message = message,
            Source = source,
            Properties = properties != null ? JsonSerializer.Serialize(properties) : null
        };

        await _repository.CreateAsync(request);
    }

    /// <inheritdoc/>
    public async Task LogErrorAsync(string message, Exception? exception = null, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default)
    {
        var errorProperties = new Dictionary<string, object>();

        if (exception != null)
        {
            errorProperties["Exception"] = new
            {
                Type = exception.GetType().Name,
                Message = exception.Message,
                StackTrace = exception.StackTrace,
                InnerException = exception.InnerException?.Message
            };
        }

        if (properties != null)
        {
            foreach (var kvp in properties)
            {
                errorProperties[kvp.Key] = kvp.Value;
            }
        }

        await LogAsync("Error", message, source, errorProperties, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task LogWarningAsync(string message, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default)
    {
        await LogAsync("Warning", message, source, properties, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task LogInformationAsync(string message, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default)
    {
        await LogAsync("Information", message, source, properties, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task LogCriticalAsync(string message, Exception? exception = null, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default)
    {
        var errorProperties = new Dictionary<string, object>();
        
        if (exception != null)
        {
            errorProperties["Exception"] = new
            {
                Type = exception.GetType().Name,
                Message = exception.Message,
                StackTrace = exception.StackTrace,
                InnerException = exception.InnerException?.Message
            };
        }

        if (properties != null)
        {
            foreach (var kvp in properties)
            {
                errorProperties[kvp.Key] = kvp.Value;
            }
        }

        await LogAsync("Critical", message, source, errorProperties, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task LogDebugAsync(string message, string? source = null, Dictionary<string, object>? properties = null, CancellationToken cancellationToken = default)
    {
        await LogAsync("Debug", message, source, properties, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task LogErrorWithDetailsAsync(string message, Exception? exception = null, string? source = null, Dictionary<string, object>? properties = null, string? errorKey = null, string? userLanguage = null, CancellationToken cancellationToken = default)
    {
        var request = AppLogCreateRequest.CreateError(message, exception, source, properties, errorKey, userLanguage);
        await _repository.CreateAsync(request);
    }
}
