// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Serilog.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Asdamir.Core.ErrorHandling.Http;

/// <summary>
/// Middleware that adds correlation ID to requests for tracing. Populates the scoped
/// <see cref="ICorrelationIdAccessor"/> so downstream services (including
/// <see cref="CorrelationIdForwardingHandler"/> on outbound HttpClient calls) can
/// resolve the id from DI without reaching back into <see cref="HttpContext"/>.
/// </summary>
public class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx)
    {
        var correlationId = GetOrCreateCorrelationId(ctx);

        // Populate the scoped accessor (set once per request).
        var mutator = ctx.RequestServices.GetService<ICorrelationIdMutator>();
        if (mutator is not null) mutator.CurrentId = correlationId;

        // Add to response headers
        ctx.Response.Headers[HeaderName] = correlationId;

        // Add to log context
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(ctx);
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext ctx)
    {
        // Try to get from request headers first
        if (ctx.Request.Headers.TryGetValue(HeaderName, out var headerValue) && 
            !string.IsNullOrWhiteSpace(headerValue))
        {
            return headerValue.ToString();
        }

        // Try to get from existing context items
        if (ctx.Items.TryGetValue(HeaderName, out var existingId) && 
            existingId is string existingCorrelationId && 
            !string.IsNullOrWhiteSpace(existingCorrelationId))
        {
            return existingCorrelationId;
        }

        // Generate new correlation ID
        var newCorrelationId = Guid.NewGuid().ToString("N");
        ctx.Items[HeaderName] = newCorrelationId;
        return newCorrelationId;
    }
}
