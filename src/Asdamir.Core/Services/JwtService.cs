// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using Asdamir.Core.Contracts;
using Asdamir.Core.Dtos;
using Asdamir.Core.Models;
using Microsoft.Extensions.Configuration;
using System.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Asdamir.Core.Services;

public class JwtService : IJwtService
{
    /// <summary>Minimum byte length for the HMAC-SHA256 signing key (≥256 bits).</summary>
    private const int MinKeyByteLength = 32;

    /// <summary>Default lifetimes if Jwt:AccessTokenLifetimeMinutes / Jwt:RefreshTokenLifetimeDays not set.</summary>
    private const int DefaultAccessTokenLifetimeMinutes = 15;
    private const int DefaultRefreshTokenLifetimeDays = 14;

    private readonly SymmetricSecurityKey _signingKey;
    private readonly SigningCredentials _signingCredentials;
    private readonly string? _issuer;
    private readonly string? _audience;
    private readonly TimeSpan _accessLifetime;
    private readonly TimeSpan _refreshLifetime;

    public JwtService(IConfiguration configuration)
    {
        var jwtKey = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Jwt:Key is not configured.");

        var keyBytes = Encoding.UTF8.GetBytes(jwtKey);
        if (keyBytes.Length < MinKeyByteLength)
        {
            throw new InvalidOperationException(
                $"Jwt:Key must be at least {MinKeyByteLength} bytes (got {keyBytes.Length}) for HMAC-SHA256. " +
                "Generate a 64+ byte random key and store it outside source control.");
        }

        _signingKey = new SymmetricSecurityKey(keyBytes);
        _signingCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
        _issuer = configuration["Jwt:Issuer"];
        _audience = configuration["Jwt:Audience"];

        var accessMinutes = configuration.GetValue<int?>("Jwt:AccessTokenLifetimeMinutes")
                            ?? DefaultAccessTokenLifetimeMinutes;
        var refreshDays = configuration.GetValue<int?>("Jwt:RefreshTokenLifetimeDays")
                          ?? DefaultRefreshTokenLifetimeDays;
        _accessLifetime = TimeSpan.FromMinutes(accessMinutes);
        _refreshLifetime = TimeSpan.FromDays(refreshDays);
    }

    public TokenResponseDto IssueTokens(UserAuth user, IEnumerable<string> permissions, string? company = null)
        => IssueTokens(user, permissions, _accessLifetime, _refreshLifetime, company);

    public TokenResponseDto IssueTokens(UserAuth user, IEnumerable<string> permissions,
        TimeSpan accessLifetime, TimeSpan refreshLifetime, string? company = null)
    {
        var now = DateTime.UtcNow;
        var expiresAtUtc = now.Add(accessLifetime);
        var refreshExpiresAtUtc = now.Add(refreshLifetime);

        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("email", user.Email),
            new("name", user.Name)
        };
        // Multi-company: the selected company travels in the token so every subsequent request
        // resolves the right management database (consumed by ICompanyContext middleware).
        if (!string.IsNullOrWhiteSpace(company))
            claims.Add(new Claim("company", company));
        claims.AddRange(permissions.Select(p => new Claim("perm", p)));

        var jwtToken = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: now,
            expires: expiresAtUtc,
            signingCredentials: _signingCredentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(jwtToken);

        // Refresh token: 32 cryptographically strong random bytes, base64url-encoded.
        // The caller is expected to store the SHA-256 hash in the database (not the raw value)
        // and rotate the token on every use (reuse detection).
        var refreshTokenBytes = RandomNumberGenerator.GetBytes(32);
        var refreshToken = Convert.ToBase64String(refreshTokenBytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        return new TokenResponseDto(accessToken, refreshToken, expiresAtUtc, refreshExpiresAtUtc);
    }
}
