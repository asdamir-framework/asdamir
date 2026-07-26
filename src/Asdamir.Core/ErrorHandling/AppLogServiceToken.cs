// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace Asdamir.Core.ErrorHandling.Logging;

/// <summary>
/// Mints the short-lived <c>app-log</c> SERVICE token a generated app's Gateway presents to AppManagement's
/// log-ingest endpoint. It is signed with the SAME symmetric key the Gateway uses to validate app-login
/// tokens (<c>Jwt:Key</c>), and carries only <c>token_use=app-log</c> + the app's <c>app_code</c> — no user.
/// AppManagement resolves the AppId from that <c>app_code</c> server-side, so the token both authenticates the
/// Gateway (only holders of the shared key can sign it) and scopes the write to the app's own slice (a Gateway
/// can only stamp its OWN <c>app_code</c>). This lives in Core next to <see cref="SerilogBootstrap"/> because
/// Core already carries the JWT signing primitives; the transport sink (<c>AppLogForwardSink</c>) in
/// Asdamir.Data calls it.
/// </summary>
public static class AppLogServiceToken
{
    /// <summary>The token-use marker that distinguishes a log-forward service token from a user token.</summary>
    public const string TokenUse = "app-log";

    /// <summary>
    /// Mints a signed <c>app-log</c> JWT for <paramref name="appCode"/>, valid for <paramref name="lifetime"/>.
    /// </summary>
    /// <param name="signingKey">The shared HMAC signing key (<c>Jwt:Key</c>, ≥ 32 bytes / 256 bits).</param>
    /// <param name="issuer">The token issuer (<c>Jwt:Issuer</c>) — must match AppManagement, or null to omit.</param>
    /// <param name="audience">The token audience (<c>Jwt:Audience</c>) — the managed-app audience.</param>
    /// <param name="appCode">The calling app's registered code (<c>App:Code</c>); resolved to an AppId server-side.</param>
    /// <param name="lifetime">How long the token is valid (kept short — e.g. a couple of minutes).</param>
    /// <returns>A compact JWS string to send as <c>Authorization: Bearer</c>.</returns>
    /// <exception cref="ArgumentException">The signing key or app code is missing.</exception>
    public static string Mint(string signingKey, string? issuer, string? audience, string appCode, TimeSpan lifetime)
    {
        if (string.IsNullOrWhiteSpace(signingKey))
            throw new ArgumentException("A signing key is required to mint an app-log token.", nameof(signingKey));
        if (string.IsNullOrWhiteSpace(appCode))
            throw new ArgumentException("An app code is required to mint an app-log token.", nameof(appCode));

        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(signingKey));
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: string.IsNullOrWhiteSpace(issuer) ? null : issuer,
            audience: string.IsNullOrWhiteSpace(audience) ? null : audience,
            claims: new[]
            {
                new Claim("token_use", TokenUse),
                new Claim("app_code", appCode),
                new Claim("sub", $"app-log:{appCode}"),
            },
            notBefore: now,
            expires: now.Add(lifetime),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
