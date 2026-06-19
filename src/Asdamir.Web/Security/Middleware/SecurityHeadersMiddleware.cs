// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Web.Security.Options;
using Asdamir.Web.Security.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
namespace Asdamir.Web.Security.Middleware;


public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SecurityHeadersOptions _options;
    private readonly ICspNonceProvider _nonceProvider;

    public SecurityHeadersMiddleware(RequestDelegate next, IOptions<SecurityHeadersOptions> options, ICspNonceProvider nonceProvider)
    {
        _next = next;
        _options = options.Value;
        _nonceProvider = nonceProvider;
    }

    public async Task Invoke(HttpContext context)
    {
        var headers = context.Response.Headers;
        if (_options.UseHsts)
            headers["Strict-Transport-Security"] = _options.HstsValue;
        if (_options.UseXContentTypeOptions)
            headers["X-Content-Type-Options"] = "nosniff";
        if (_options.UseXFrameOptions)
            headers["X-Frame-Options"] = _options.XFrameOptionsValue;
        if (_options.UseReferrerPolicy)
            headers["Referrer-Policy"] = _options.ReferrerPolicyValue;
        if (_options.UsePermissionsPolicy)
            headers["Permissions-Policy"] = _options.PermissionsPolicyValue;
        if (_options.UseContentSecurityPolicy && !string.IsNullOrWhiteSpace(_options.ContentSecurityPolicy))
        {
            var csp = _options.ContentSecurityPolicy!;
            if (csp.Contains("{nonce}", StringComparison.Ordinal))
            {
                // Fallback nonce uses CSPRNG, not Guid — see CspNonceMiddleware for rationale.
                var nonce = _nonceProvider.GetNonce();
                if (string.IsNullOrEmpty(nonce))
                {
                    Span<byte> buffer = stackalloc byte[16];
                    RandomNumberGenerator.Fill(buffer);
                    nonce = Convert.ToBase64String(buffer);
                }
                csp = csp.Replace("{nonce}", nonce, StringComparison.Ordinal);
            }
            headers["Content-Security-Policy"] = csp;
        }

        await _next(context);
    }
}


