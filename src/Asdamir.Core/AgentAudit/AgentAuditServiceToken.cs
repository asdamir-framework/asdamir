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

namespace Asdamir.Core.AgentAudit;

/// <summary>
/// Mints the short-lived <c>agent-audit-ingest</c> SERVICE token an application presents to the control
/// plane's agent-audit ingest endpoint. The exact sibling of the app-log service token: signed with the SAME
/// symmetric key the application already shares with the control plane (<c>Jwt:Key</c>), carrying only
/// <c>token_use=agent-audit-ingest</c> and the app's <c>app_code</c> — no user.
///
/// <para><b>The token does two jobs at once, and the second one is the security property that matters.</b> It
/// AUTHENTICATES the caller (only a holder of the shared key can sign it) and it SCOPES the write: the server
/// resolves the ledger's AppId from the token's <c>app_code</c> and NEVER from the request body, so an
/// application can only ever append to its own chain. That is also why the record itself carries no AppId.</para>
///
/// <para>This is transport identity, not agent identity. It proves which APPLICATION is submitting records; it
/// says nothing about which agent acted. The agent's identity is data the application asserts — see
/// <see cref="AgentActionRecord"/>.</para>
/// </summary>
public static class AgentAuditServiceToken
{
    /// <summary>The token-use marker distinguishing an agent-audit ingest token from every other token.</summary>
    public const string TokenUse = "agent-audit-ingest";

    /// <summary>
    /// Mints a signed <c>agent-audit-ingest</c> JWT for <paramref name="appCode"/>, valid for
    /// <paramref name="lifetime"/>.
    /// </summary>
    /// <param name="signingKey">The shared HMAC signing key (<c>Jwt:Key</c>, at least 32 bytes / 256 bits).</param>
    /// <param name="issuer">The token issuer (<c>Jwt:Issuer</c>) — must match the control plane, or null to omit.</param>
    /// <param name="audience">The token audience (<c>Jwt:Audience</c>) — the managed-app audience.</param>
    /// <param name="appCode">The calling app's registered code (<c>App:Code</c>); resolved to an AppId server-side.</param>
    /// <param name="lifetime">How long the token is valid. Keep it short — a couple of minutes is ample.</param>
    /// <returns>A compact JWS string to send as <c>Authorization: Bearer</c>.</returns>
    /// <exception cref="ArgumentException">The signing key or the app code is missing.</exception>
    public static string Mint(string signingKey, string? issuer, string? audience, string appCode, TimeSpan lifetime)
    {
        if (string.IsNullOrWhiteSpace(signingKey))
            throw new ArgumentException("A signing key is required to mint an agent-audit ingest token.", nameof(signingKey));
        if (string.IsNullOrWhiteSpace(appCode))
            throw new ArgumentException("An app code is required to mint an agent-audit ingest token.", nameof(appCode));

        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(signingKey));
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: string.IsNullOrWhiteSpace(issuer) ? null : issuer,
            audience: string.IsNullOrWhiteSpace(audience) ? null : audience,
            claims: new[]
            {
                new Claim("token_use", TokenUse),
                new Claim("app_code", appCode),
                new Claim("sub", $"agent-audit:{appCode}"),
            },
            notBefore: now,
            expires: now.Add(lifetime),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
