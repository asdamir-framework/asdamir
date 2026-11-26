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

namespace Asdamir.Core.Services;

/// <summary>
/// The short-lived token a control plane presents to ONE managed app's Gateway when it calls that app's
/// management endpoints (<c>gateway/admin/*</c>). The control plane signs it with its own token-signing
/// key, so the Gateway verifies it against the same published keys it already trusts for user tokens — no
/// secret is exchanged and the Gateway stores none.
///
/// <para>Three things keep it from being anything else: its own audience (<see cref="DefaultAudience"/>),
/// so neither the control plane nor an end-user endpoint accepts it; <c>token_use</c> =
/// <see cref="TokenUse"/>; and the <c>app_code</c> of the one app it was issued for, so a Gateway that
/// receives it cannot replay it against another app.</para>
/// </summary>
public static class OrchestrationToken
{
    /// <summary>The <c>token_use</c> value that marks an orchestration token.</summary>
    public const string TokenUse = "orchestration";

    /// <summary>The configuration key both sides read the audience from.</summary>
    public const string AudienceConfigurationKey = "Jwt:OrchestrationAudience";

    /// <summary>The audience used when <see cref="AudienceConfigurationKey"/> is not set.</summary>
    public const string DefaultAudience = "asdamir-app-orchestration";

    /// <summary>The <c>client_id</c> a Gateway expects when it has not been configured with another.</summary>
    public const string DefaultClientId = "admin-console";

    /// <summary>Mints an orchestration token for one app.</summary>
    /// <param name="credentials">The control plane's signing credentials.</param>
    /// <param name="issuer">The token issuer (<c>Jwt:Issuer</c>), or null to omit.</param>
    /// <param name="audience">The orchestration audience.</param>
    /// <param name="appCode">The registered code of the app being called.</param>
    /// <param name="clientId">The <c>client_id</c> the app's Gateway authorizes.</param>
    /// <param name="lifetime">How long the token is valid. Keep it to minutes.</param>
    /// <returns>A compact JWS string to send as <c>Authorization: Bearer</c>.</returns>
    /// <exception cref="ArgumentException">The audience, app code or client id is missing.</exception>
    public static string Mint(SigningCredentials credentials, string? issuer, string audience, string appCode,
        string clientId, TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience);
        ArgumentException.ThrowIfNullOrWhiteSpace(appCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: string.IsNullOrWhiteSpace(issuer) ? null : issuer,
            audience: audience,
            claims:
            [
                new Claim("token_use", TokenUse),
                new Claim("app_code", appCode),
                new Claim("client_id", clientId),
                new Claim("sub", $"client-{clientId}"),
            ],
            notBefore: now,
            expires: now.Add(lifetime),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Whether an already signature-verified principal is an orchestration token for this app: the
    /// orchestration audience, <c>token_use</c> = <see cref="TokenUse"/>, and this app's <c>app_code</c>.
    /// A Gateway calls this for any token carrying a <c>client_id</c> claim and fails authentication when
    /// it returns false.
    /// </summary>
    /// <param name="principal">The validated principal.</param>
    /// <param name="appCode">This app's code (<c>App:Code</c>); compared case-insensitively.</param>
    /// <param name="audience">The orchestration audience this Gateway accepts.</param>
    /// <returns><c>true</c> only when all three match.</returns>
    public static bool IsFor(ClaimsPrincipal principal, string appCode, string audience)
    {
        ArgumentNullException.ThrowIfNull(principal);
        return principal.HasClaim("aud", audience)
               && principal.HasClaim("token_use", TokenUse)
               && principal.Claims.Any(c => c.Type == "app_code"
                                            && string.Equals(c.Value, appCode, StringComparison.OrdinalIgnoreCase));
    }
}
