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

namespace Asdamir.Core.MultiTenancy;

/// <summary>
/// Resolves tenant context from the authenticated user's <c>tid</c> claim, falling back to a
/// secondary resolver (typically <see cref="HeaderTenantResolver"/>) for unauthenticated requests
/// such as login.
///
/// This is the recommended production tenant resolver. The header-only path is insecure because
/// any client can send <c>X-Tenant-Id: B</c> while authenticated as a user from tenant A — and v1
/// downstream code that trusts <c>ITenantContext.TenantId</c> would scope queries to tenant B.
///
/// Audit fix: in v2 the header value is treated as ADVISORY only. The signed JWT claim is the
/// authoritative source.
/// </summary>
public sealed class ClaimsTenantResolver : ITenantResolver
{
    private const string DefaultClaimType = "tid";
    private readonly string _claimType;
    private readonly ITenantResolver? _fallback;

    public ClaimsTenantResolver(string claimType = DefaultClaimType, ITenantResolver? fallbackForAnonymous = null)
    {
        _claimType = string.IsNullOrWhiteSpace(claimType) ? DefaultClaimType : claimType;
        _fallback = fallbackForAnonymous;
    }

    public Task<TenantContext> ResolveAsync(HttpContext ctx, CancellationToken ct = default)
    {
        var user = ctx.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var tid = user.FindFirst(_claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(tid))
            {
                return Task.FromResult(new TenantContext
                {
                    TenantId = tid,
                    TenantName = null,
                    IsMultiTenant = true
                });
            }
        }

        if (_fallback is not null)
        {
            return _fallback.ResolveAsync(ctx, ct);
        }

        return Task.FromResult(new TenantContext
        {
            TenantId = "default",
            TenantName = null,
            IsMultiTenant = true
        });
    }
}
