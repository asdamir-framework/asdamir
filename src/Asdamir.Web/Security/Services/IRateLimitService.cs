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

public interface IRateLimitService
{
    Task<bool> TryAcquireAsync(string key, int maxRequests, TimeSpan window);
    Task<RateLimitInfo> GetLimitInfoAsync(string key);
}

public record RateLimitInfo(int Remaining, TimeSpan ResetTime, bool IsBlocked);

public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IRateLimitService _rateLimitService;
    private readonly RateLimitOptions _options;
    
    public RateLimitMiddleware(RequestDelegate next, IRateLimitService rateLimitService, RateLimitOptions options)
    {
        _next = next;
        _rateLimitService = rateLimitService;
        _options = options;
    }
    
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

public class RateLimitOptions
{
    public int MaxRequests { get; set; } = 100;
    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}
