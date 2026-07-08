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

    /// <summary>
    /// Captures the pipeline continuation and resolves the <see cref="IProblemDetailsMapper"/> up front,
    /// so a missing or misconfigured mapper fails fast at startup rather than while handling a live error.
    /// </summary>
    /// <param name="next">The next middleware in the request pipeline.</param>
    /// <param name="logger">Sink for the full, operator-facing error record (flows to Console + File + DB).</param>
    /// <param name="mapper">Maps an exception to its status code, stable error code, and base ProblemDetails.</param>
    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IProblemDetailsMapper mapper)
    {
        _next = next;
        _logger = logger;
        _mapper = mapper;
    }

    /// <summary>
    /// Runs the rest of the pipeline and, on any unhandled exception, logs the full error for operators
    /// and writes a localized <c>application/problem+json</c> response to the caller instead of letting
    /// the exception escape as a bare 500.
    /// </summary>
    /// <param name="context">The current request context; its aborted token and culture drive handling.</param>
    /// <returns>A task that completes once the request — or its error response — has been written.</returns>
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

        // Resolve the USER-FACING message from LocalizationResource (Category='ERROR') by culture.
        // Fallback chain: error.<specificKey> → generic error message → a neutral last resort. We
        // never surface the raw error key (an internal token) to the user.
        var localizationService = context.RequestServices.GetService<ILocalizationService>();
        problemDetails.Title = await ResolveUserMessageAsync(localizationService, userLanguage, errorKey);
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

    // Translate the error to a localized, user-safe message. Tries the specific key first, then a
    // generic error message, then a neutral last resort — so the user never sees a raw key or code.
    private async Task<string> ResolveUserMessageAsync(ILocalizationService? localizationService, string userLanguage, string errorKey)
    {
        if (localizationService is null)
        {
            return DefaultUserMessage;
        }

        var specific = await TryLocalizeAsync(localizationService, userLanguage, $"error.{errorKey}");
        if (!string.IsNullOrEmpty(specific))
        {
            return specific;
        }

        var generic = await TryLocalizeAsync(localizationService, userLanguage, GenericErrorKey);
        return !string.IsNullOrEmpty(generic) ? generic : DefaultUserMessage;
    }

    private async Task<string?> TryLocalizeAsync(ILocalizationService localizationService, string userLanguage, string key)
    {
        try
        {
            return await localizationService.GetAsync(userLanguage, key, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Asdamir.Core.ErrorHandling.GlobalException: localization lookup failed - Key={Key}, Language={Language}", key, userLanguage);
            return null;
        }
    }

    // The generic error message key seeded in LocalizationResource (Category='ERROR') for all cultures.
    private const string GenericErrorKey = "error.error.generic";
    // Absolute last resort if localization is entirely unavailable (never a raw status code or key).
    private const string DefaultUserMessage = "An unexpected error occurred. Please try again.";

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
