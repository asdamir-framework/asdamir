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
    /// <summary>
    /// The HTTP header carrying the correlation id, both inbound (to reuse an upstream id) and
    /// outbound (echoed on the response and forwarded on downstream calls).
    /// </summary>
    public const string HeaderName = "X-Correlation-Id";

    /// <summary>
    /// The longest inbound correlation id that is accepted. Long enough for a GUID in any format, a W3C
    /// trace id or a ULID; short enough that a caller cannot use the header to inflate every log line.
    /// </summary>
    public const int MaxLength = 64;
    private readonly RequestDelegate _next;

    /// <summary>Creates the middleware with the next delegate in the request pipeline.</summary>
    /// <param name="next">The next middleware to invoke.</param>
    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    /// <summary>
    /// Resolves the correlation id for the request (reusing an inbound header/context value or
    /// generating a new one — an inbound value is reused only when it is at most
    /// <see cref="MaxLength"/> characters of letters, digits, <c>-</c>, <c>_</c> or <c>.</c>; anything else
    /// is discarded and replaced, because the value is echoed into the response and every log line), publishes it to the scoped mutator and the response header, and pushes
    /// it onto the Serilog log context so every log line emitted while handling the request is tagged.
    /// </summary>
    /// <param name="ctx">The current HTTP context.</param>
    /// <returns>A task that completes when the rest of the pipeline has run.</returns>
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
        // The value is CALLER-CONTROLLED and goes straight into the response header and the log context, so
        // it is reused only when it looks like an id. An oversized or oddly-charactered value (log forging via
        // control characters, megabyte headers, a comma-joined multi-value header) is dropped, not sanitised:
        // a "cleaned" id would correlate with nothing upstream anyway.
        if (ctx.Request.Headers.TryGetValue(HeaderName, out var headerValue) && IsAcceptable(headerValue.ToString()))
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

    /// <summary>Whether an inbound correlation id is safe to echo: 1–<see cref="MaxLength"/> ASCII letters, digits, <c>-</c>, <c>_</c>, <c>.</c>.</summary>
    internal static bool IsAcceptable(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxLength) return false;
        foreach (var c in value)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')) return false;
        }
        return true;
    }
}
