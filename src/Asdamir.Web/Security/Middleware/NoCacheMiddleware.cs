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

namespace Asdamir.Web.Security.Middleware;

/// <summary>
/// Prevents the browser from caching authenticated HTML responses, so the back
/// button after logout cannot replay a private page from disk.
///
/// Audit fixes vs. v1:
///  - <c>Clear-Site-Data</c> was emitted on EVERY authenticated page response.
///    That instructed Chrome / Edge to wipe cache and storage on every navigation,
///    which (a) tanked perceived performance, (b) repeatedly destroyed legitimate
///    IndexedDB state, and (c) drowned out the one place we actually want it
///    (logout). Now it's only set on the logout endpoint's response.
///  - <c>ETag = Guid.NewGuid()</c> served zero purpose on a no-store response —
///    ETag is a cache validator, and no-store rules out caching. It's been
///    removed. <c>Last-Modified</c> is also pointless under no-store and is gone.
///  - X-Frame-Options / X-Content-Type-Options / Referrer-Policy were duplicated
///    here when <see cref="SecurityHeadersMiddleware"/> already sets them via
///    bound options. Removed to avoid header drift between the two middlewares.
/// </summary>
public class NoCacheMiddleware
{
    private readonly RequestDelegate _next;

    public NoCacheMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        var isLogout = path.StartsWithSegments("/logout")
            || path.StartsWithSegments("/auth/logout")
            || path.StartsWithSegments("/gateway/auth/logout");

        var skip = path.StartsWithSegments("/login")
            || path.StartsWithSegments("/api")
            || path.StartsWithSegments("/_framework")
            || path.StartsWithSegments("/_content")
            || path.Value?.Contains('.') == true;

        if (!skip)
        {
            context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate, private, max-age=0";
            context.Response.Headers["Pragma"] = "no-cache";
            context.Response.Headers["Expires"] = "0";

            if (isLogout)
            {
                // Hard wipe of client state on the only response where it makes sense.
                context.Response.Headers["Clear-Site-Data"] = "\"cache\", \"storage\", \"cookies\"";
            }
        }

        await _next(context);
    }
}
