// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.ErrorHandling.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Asdamir.Core.ErrorHandling.Domain;
using Asdamir.Core.Contracts;
using Microsoft.Extensions.DependencyInjection;
using System.Data.Common;
using System.Text.Json;


namespace Asdamir.Core.ErrorHandling.Http;

/// <summary>
/// Global exception handling middleware that catches unhandled exceptions and translates them
/// into ProblemDetails responses.
///
/// Audit fixes vs. v1:
///  - <see cref="IProblemDetailsMapper"/> is resolved once in the middleware constructor instead of
///    on every request — if the mapper isn't registered or throws in its ctor we fail at startup
///    rather than after a real exception (which would otherwise lose the original error).
///  - Exception classification no longer relies on locale-sensitive substring matches on the
///    <c>Message</c> property. We use type hierarchy (<see cref="DbException"/>, well-known custom
///    types) instead. The v1 path mapped <c>InvalidOperationException</c> whose message contained
///    "database" or "email" — that would have miscategorised Turkish/Russian error messages and
///    been fooled by any unrelated InvalidOperationException whose text happened to mention the word.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IProblemDetailsMapper _mapper;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IProblemDetailsMapper mapper)
    {
        _next = next;
        _logger = logger;
        _mapper = mapper;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex, _mapper);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception, IProblemDetailsMapper mapper)
    {
        var traceId = context.TraceIdentifier;
        var mapResult = await mapper.MapAsync(exception, traceId, context.RequestAborted);
        int statusCode = mapResult.StatusCode;
        string errorCode = mapResult.ErrorCode;
        var problemDetails = mapResult.ProblemDetails;

        var userLanguage = GetUserLanguage(context);
        var errorKey = GetErrorKey(exception, errorCode);

        // Structured fields flow to every Serilog sink (Console + File + the DB AppLog sink).
        // ErrorKey lands in AppLog.ErrorKey (ErrorMonitoring groups on it); CaughtBy marks that this
        // was an UNHANDLED exception surfaced by the global middleware (vs. a deliberate log call).
        _logger.LogError(exception,
            "Unhandled exception caught by GlobalExceptionMiddleware - ErrorCode={ErrorCode}, StatusCode={StatusCode}, TraceId={TraceId}, ErrorKey={ErrorKey}, Path={Path}, CaughtBy={CaughtBy}",
            errorCode, statusCode, traceId, errorKey, context.Request.Path, nameof(GlobalExceptionMiddleware));

        // Get translated message from LocalizationResource (Category='ERROR')
        var localizationService = context.RequestServices.GetService<ILocalizationService>();
        string userMessage = errorKey; // fallback to error key

        if (localizationService != null)
        {
            try
            {
                var translatedMessage = await localizationService.GetAsync(userLanguage, $"error.{errorKey}", CancellationToken.None);
                if (!string.IsNullOrEmpty(translatedMessage))
                {
                    userMessage = translatedMessage;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Asdamir.Core.ErrorHandling.GlobalException: Failed to get translated message - ErrorKey={ErrorKey}, Language={Language}", 
                    errorKey, userLanguage);
            }
        }

        problemDetails.Title = userMessage;
        problemDetails.Extensions["errorKey"] = errorKey;
        problemDetails.Extensions["userLanguage"] = userLanguage;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        var json = JsonSerializer.Serialize(problemDetails);
        await context.Response.WriteAsync(json);
    }

    private string GetUserLanguage(HttpContext context)
    {
        // First try to get from user claims (if authenticated)
        var cultureClaim = context.User?.FindFirst("culture")?.Value 
                        ?? context.User?.FindFirst("preferred_language")?.Value;
        if (!string.IsNullOrEmpty(cultureClaim))
        {
            if (cultureClaim.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                return "en-US";
            if (cultureClaim.StartsWith("ru", StringComparison.OrdinalIgnoreCase))
                return "ru-RU";
            if (cultureClaim.StartsWith("tr", StringComparison.OrdinalIgnoreCase))
                return "tr-TR";
        }
        
        // Fallback to Accept-Language header
        var lang = context.Request.Headers["Accept-Language"].ToString();
        if (string.IsNullOrWhiteSpace(lang))
            return "tr-TR";
        if (lang.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            return "en-US";
        if (lang.StartsWith("ru", StringComparison.OrdinalIgnoreCase))
            return "ru-RU";
        if (lang.StartsWith("tr", StringComparison.OrdinalIgnoreCase))
            return "tr-TR";
        return "tr-TR";
    }

    private string GetErrorKey(Exception exception, string errorCode)
    {
        // DomainException ise Code'u direkt kullan
        if (exception is DomainException domainEx)
        {
            return domainEx.Code;
        }

        // Classification by exception type — NOT by message substring (audit fix).
        // The walk handles wrapped exceptions (AggregateException, TaskCanceledException) and
        // any DbException subclass (SqlException, NpgsqlException, etc.).
        for (var ex = exception; ex is not null; ex = ex.InnerException)
        {
            switch (ex)
            {
                case UnauthorizedAccessException: return "auth.unauthorized";
                case ArgumentNullException: return "validation.required";
                case ArgumentException: return "validation.required";
                case FileNotFoundException: return "file.not.found";
                case TimeoutException: return "network.timeout";
                case TaskCanceledException: return "network.timeout";
                case OperationCanceledException: return "network.timeout";
                case DbException: return "database.connection.error";
                case System.Net.Http.HttpRequestException: return "network.error";
            }
        }

        return GetErrorKeyFromCode(errorCode);
    }

    private string GetErrorKeyFromCode(string errorCode)
    {
        // Error code'a göre key belirle
        return errorCode switch
        {
            "UNAUTHORIZED" => "auth.unauthorized",
            "FORBIDDEN" => "auth.forbidden",
            "NOT_FOUND" => "error.not.found",
            "VALIDATION_FAILED" => "validation.required",
            "INTERNAL_ERROR" => "error.internal.server",
            "BAD_REQUEST" => "error.bad.request",
            _ => "error.generic"
        };
    }

}
