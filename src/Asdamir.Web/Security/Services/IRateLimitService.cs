// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Microsoft.AspNetCore.Http;
using Asdamir.Core.MultiTenancy;

namespace Asdamir.Web.Security.Services;

/// <summary>
/// Fixed-window rate limiter. Counts requests per opaque key within a sliding window and reports whether
/// one more request may proceed, so callers (middleware, login/OTP flows) can throttle abuse and floods.
/// </summary>
public interface IRateLimitService
{
    /// <summary>Attempts to consume one slot for <paramref name="key"/> in the current window.</summary>
    /// <param name="key">Opaque bucket key (e.g. tenant + user/IP + path); requests sharing a key share a budget.</param>
    /// <param name="maxRequests">Maximum requests permitted within a single <paramref name="window"/>.</param>
    /// <param name="window">Length of the counting window before the budget resets.</param>
    /// <returns><c>true</c> if the request is within budget and may proceed; <c>false</c> if the limit is exceeded.</returns>
    Task<bool> TryAcquireAsync(string key, int maxRequests, TimeSpan window);
}

/// <summary>
/// Middleware that rate-limits inbound requests via <see cref="IRateLimitService"/>, keyed by tenant, the
/// authenticated user (or client IP), and request path. Requests over budget get an HTTP 429 short-circuit.
/// </summary>
public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRateLimitService _rateLimitService;
    private readonly RateLimitOptions _options;

    /// <summary>Initializes the middleware with the next delegate, the limiter, and the configured limits.</summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="rateLimitService">The rate limiter that tracks per-key budgets.</param>
    /// <param name="options">Max-requests-per-window configuration.</param>
    public RateLimitMiddleware(RequestDelegate next, IRateLimitService rateLimitService, RateLimitOptions options)
    {
        _next = next;
        _rateLimitService = rateLimitService;
        _options = options;
    }

    /// <summary>Enforces the rate limit for the current request, returning HTTP 429 when the budget is exhausted.</summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>A task that completes when the request has been forwarded or rejected.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        var key = GenerateKey(context);
        var allowed = await _rateLimitService.TryAcquireAsync(key, _options.MaxRequests, _options.Window);
        
        if (!allowed)
        {
            context.Response.StatusCode = 429;
            await context.Response.WriteAsync("Rate limit exceeded");
            return;
        }
        
        await _next(context);
    }
    
    private string GenerateKey(HttpContext context)
    {
        var userId = context.User.FindFirst("sub")?.Value;
        var ip = context.Connection.RemoteIpAddress?.ToString();
        var tenantId = context.Items["TenantContext"] as ITenantContext;
        
        return $"{tenantId?.TenantId}:{userId ?? ip}:{context.Request.Path}";
    }
}

/// <summary>
/// Configuration for <see cref="RateLimitMiddleware"/>: how many requests are allowed per counting window.
/// </summary>
public class RateLimitOptions
{
    /// <summary>Maximum number of requests permitted per key within one <see cref="Window"/> (default 100).</summary>
    public int MaxRequests { get; set; } = 100;
    /// <summary>Length of the counting window before a key's budget resets (default 1 minute).</summary>
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}
