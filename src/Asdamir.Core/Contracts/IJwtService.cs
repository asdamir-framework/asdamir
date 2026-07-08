// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.Models;
using Asdamir.Core.Dtos;

namespace Asdamir.Core.Contracts;

/// <summary>
/// Mints signed JWT access + refresh token pairs for authenticated users, embedding the tenant/company,
/// audience-boundary (<c>console</c> vs <c>app</c>) and permission claims that downstream endpoints authorize against.
/// </summary>
public interface IJwtService
{
    /// <summary>
    /// Issues an access + refresh token pair. When <paramref name="company"/> is provided it is
    /// embedded as a <c>company</c> claim so downstream requests know which company (tenant)
    /// management database the session belongs to (multi-company; see
    /// docs/design/multi-company-management.md). Null/empty omits the claim (single-company).
    /// <para>
    /// <paramref name="tokenUse"/> marks the token's audience boundary — <c>"console"</c> for a
    /// control-plane operator, <c>"app"</c> for a managed-app end-user (with <paramref name="appCode"/>
    /// as the app's code). Every token shares the same signing key/issuer/audience, so this claim is what
    /// lets control-plane endpoints reject app-login tokens. Null omits both claims (treated as console/legacy).
    /// </para>
    /// </summary>
    TokenResponseDto IssueTokens(UserAuth user, IEnumerable<string> permissions, string? company = null,
        string? tokenUse = null, string? appCode = null);

    /// <summary>
    /// Same as the default issuer but with explicit access/refresh lifetimes — used when the lifetime
    /// is resolved at runtime (e.g. per-app, from the app's DB-backed configuration) rather than the
    /// host's startup configuration. See the other overload for <paramref name="tokenUse"/>/<paramref name="appCode"/>.
    /// </summary>
    TokenResponseDto IssueTokens(UserAuth user, IEnumerable<string> permissions,
        TimeSpan accessLifetime, TimeSpan refreshLifetime, string? company = null,
        string? tokenUse = null, string? appCode = null);
}



