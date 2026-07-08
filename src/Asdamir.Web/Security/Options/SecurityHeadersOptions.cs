// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Web.Security.Options;

/// <summary>
/// Bound configuration for the HTTP security headers emitted by <c>SecurityHeadersMiddleware</c>.
/// Each header can be toggled independently and given a custom value; the defaults are the
/// hardened baseline (HSTS on, frames denied, no referrer). Bind from the <c>Security:Headers</c>
/// configuration section or configure in code via <c>AddFrameworkSecurity</c>.
/// </summary>
public sealed class SecurityHeadersOptions
{
    /// <summary>Emit the <c>Strict-Transport-Security</c> (HSTS) header. Config key <c>Security:Headers:UseHsts</c>; default <c>true</c>.</summary>
    public bool UseHsts { get; set; } = true;

    /// <summary>The <c>Strict-Transport-Security</c> value when <see cref="UseHsts"/> is on. Config key <c>Security:Headers:HstsValue</c>; default <c>"max-age=31536000; includeSubDomains"</c> (one year).</summary>
    public string HstsValue { get; set; } = "max-age=31536000; includeSubDomains";

    /// <summary>Emit <c>X-Content-Type-Options: nosniff</c> to block MIME-type sniffing. Config key <c>Security:Headers:UseXContentTypeOptions</c>; default <c>true</c>.</summary>
    public bool UseXContentTypeOptions { get; set; } = true;

    /// <summary>Emit the <c>X-Frame-Options</c> header to guard against click-jacking. Config key <c>Security:Headers:UseXFrameOptions</c>; default <c>true</c>.</summary>
    public bool UseXFrameOptions { get; set; } = true;

    /// <summary>The <c>X-Frame-Options</c> value when <see cref="UseXFrameOptions"/> is on. Config key <c>Security:Headers:XFrameOptionsValue</c>; default <c>"DENY"</c>.</summary>
    public string XFrameOptionsValue { get; set; } = "DENY";

    /// <summary>Emit the <c>Referrer-Policy</c> header. Config key <c>Security:Headers:UseReferrerPolicy</c>; default <c>true</c>.</summary>
    public bool UseReferrerPolicy { get; set; } = true;

    /// <summary>The <c>Referrer-Policy</c> value when <see cref="UseReferrerPolicy"/> is on. Config key <c>Security:Headers:ReferrerPolicyValue</c>; default <c>"no-referrer"</c>.</summary>
    public string ReferrerPolicyValue { get; set; } = "no-referrer";

    /// <summary>Emit the <c>Permissions-Policy</c> header to opt out of browser features. Config key <c>Security:Headers:UsePermissionsPolicy</c>; default <c>true</c>.</summary>
    public bool UsePermissionsPolicy { get; set; } = true;

    /// <summary>The <c>Permissions-Policy</c> value when <see cref="UsePermissionsPolicy"/> is on. Config key <c>Security:Headers:PermissionsPolicyValue</c>; default disables geolocation and microphone.</summary>
    public string PermissionsPolicyValue { get; set; } = "geolocation=(), microphone=()";

    /// <summary>Emit the <c>Content-Security-Policy</c> header. Off by default (<c>false</c>) because CSP must be tuned per app to avoid breaking legitimate scripts. Config key <c>Security:Headers:UseContentSecurityPolicy</c>.</summary>
    public bool UseContentSecurityPolicy { get; set; } = false;

    /// <summary>The <c>Content-Security-Policy</c> value when <see cref="UseContentSecurityPolicy"/> is on. A <c>{nonce}</c> placeholder is substituted per request with the CSP nonce. Config key <c>Security:Headers:ContentSecurityPolicy</c>; default is a strict self-only policy.</summary>
    public string? ContentSecurityPolicy { get; set; }
        = "default-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'self'";
}


