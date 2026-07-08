// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Web.Security.Services;
namespace Asdamir.Web.Security.Middleware;

using Asdamir.Web.Security.Attributes;
using Microsoft.AspNetCore.Http;

/// <summary>
/// Enforces per-endpoint rate limits declared via <see cref="RateLimitAttribute"/>. Builds a key from the
/// client IP, user identity and request path, and returns <c>429 Too Many Requests</c> when the endpoint's
/// limit for the rolling window is exceeded; endpoints without the attribute pass through untouched.
/// </summary>
public sealed class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRateLimitService _service;

    /// <summary>Creates the middleware with the next delegate and the rate-limit counting service.</summary>
    /// <param name="next">The next delegate in the request pipeline.</param>
    /// <param name="service">The service that tracks and enforces request counts per key.</param>
    public RateLimitingMiddleware(RequestDelegate next, IRateLimitService service)
    {
        _next = next;
        _service = service;
    }

    /// <summary>Checks the endpoint's <see cref="RateLimitAttribute"/> budget; short-circuits with 429 if exceeded, otherwise invokes the pipeline.</summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A task that completes when the request is served or rejected.</returns>
    public async Task Invoke(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        if (endpoint is not null)
        {
            var rl = endpoint.Metadata.GetMetadata<RateLimitAttribute>();
            if (rl is not null)
            {
                var key = BuildKey(context);
                var allowed = await _service.TryAcquireAsync(key, rl.Limit, TimeSpan.FromSeconds(rl.WindowSeconds));
                if (!allowed)
                {
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    return;
                }
            }
        }

        await _next(context);
    }

    private static string BuildKey(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var path = context.Request.Path.ToString();
        var user = context.User?.Identity?.Name ?? "anon";
        return $"{ip}:{user}:{path}";
    }
}


