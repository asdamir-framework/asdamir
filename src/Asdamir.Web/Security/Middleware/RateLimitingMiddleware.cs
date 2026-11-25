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
/// <para>The path part of the key is NORMALISED (lower-cased, trailing slashes removed): routing matches
/// <c>/Api/Login</c>, <c>/api/login</c> and <c>/api/login/</c> to the same endpoint, so keying on the raw
/// path gave a caller a fresh budget per spelling. A rejected request carries a <c>Retry-After</c> header
/// with the endpoint's window in seconds — the longest a caller can need to wait.</para>
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
                    // The service does not expose how much of the window is left, so the window itself is
                    // the honest upper bound: after that long the budget has certainly been refilled.
                    context.Response.Headers.RetryAfter =
                        Math.Max(1, rl.WindowSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    return;
                }
            }
        }

        await _next(context);
    }

    private static string BuildKey(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var path = NormalizePath(context.Request.Path.Value);
        var user = context.User?.Identity?.Name ?? "anon";
        return $"{ip}:{user}:{path}";
    }

    /// <summary>
    /// Folds the spellings routing treats as one endpoint into one key: case-insensitive, trailing slashes
    /// ignored. The root path stays <c>/</c>.
    /// </summary>
    internal static string NormalizePath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return "/";
        var trimmed = path.TrimEnd('/');
        return trimmed.Length == 0 ? "/" : trimmed.ToLowerInvariant();
    }
}
