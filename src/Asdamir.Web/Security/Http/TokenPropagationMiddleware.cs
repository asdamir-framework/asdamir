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
using Microsoft.AspNetCore.Http;

namespace Asdamir.Web.Security.Http;

/// <summary>
/// Middleware that propagates authentication token to HttpContext for use in HttpClient handlers
/// </summary>
public class TokenPropagationMiddleware
{
    private readonly RequestDelegate _next;

    public TokenPropagationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, AuthState authState)
    {
        // Store token in HttpContext.Items for access by DelegatingHandlers
        // This is the PRIMARY source for BearerHandler
        var token = authState.GetAccessToken();
        if (!string.IsNullOrWhiteSpace(token))
        {
            context.Items["AccessToken"] = token;
        }

        await _next(context);
    }
}
