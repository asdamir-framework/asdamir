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
/// Resolves tenant context from an HTTP header.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ SECURITY NOTE (audit fix): This resolver trusts an arbitrary client-supplied header value.
/// A logged-in user of tenant A can send <c>X-Tenant-Id: B</c> and downstream code that consumes
/// <see cref="ITenantContext"/> will scope queries to tenant B's data — a cross-tenant data leak.
/// </para>
/// <para>
/// Use this resolver only:
///   - in single-tenant deployments, OR
///   - for unauthenticated endpoints (login, health), OR
///   - as the FALLBACK passed to <see cref="ClaimsTenantResolver"/> for anonymous requests.
/// </para>
/// <para>
/// For authenticated multi-tenant production traffic, prefer <see cref="ClaimsTenantResolver"/>
/// which reads the tenant id from a signed JWT claim.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Use default header name (X-Tenant-Id)
/// services.AddMultiTenancy();
/// 
/// // Use custom header name
/// services.AddMultiTenancy(opt => opt.HeaderName = "X-Custom-Tenant");
/// </code>
/// </example>
public sealed class HeaderTenantResolver : ITenantResolver
{
    private readonly string _headerName;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeaderTenantResolver"/> class.
    /// </summary>
    /// <param name="headerName">The name of the HTTP header containing the tenant ID. Defaults to "X-Tenant-Id".</param>
    public HeaderTenantResolver(string headerName = "X-Tenant-Id") => _headerName = headerName;

    /// <summary>
    /// Resolves the tenant context from the specified HTTP header.
    /// </summary>
    /// <param name="ctx">The HTTP context containing the request headers.</param>
    /// <param name="ct">A cancellation token (not used in this implementation).</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains a <see cref="TenantContext"/> with the tenant ID from the header,
    /// or "default" if the header is missing or empty.
    /// </returns>
    public Task<TenantContext> ResolveAsync(HttpContext ctx, CancellationToken ct = default)
    {
        ctx.Request.Headers.TryGetValue(_headerName, out var id);
        var tc = new TenantContext
        {
            TenantId = string.IsNullOrWhiteSpace(id) ? "default" : id.ToString(),
            TenantName = null,
            IsMultiTenant = true
        };
        return Task.FromResult(tc);
    }
}
