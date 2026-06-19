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
/// Defines a strategy for resolving tenant context from an HTTP request.
/// </summary>
/// <remarks>
/// Implementations can resolve tenants from various sources such as:
/// - HTTP headers (see <see cref="HeaderTenantResolver"/>)
/// - Route values
/// - Subdomains
/// - Query strings
/// - Custom authentication tokens
/// </remarks>
public interface ITenantResolver
{
    /// <summary>
    /// Resolves the tenant context for the given HTTP request.
    /// </summary>
    /// <param name="httpContext">The HTTP context containing request information.</param>
    /// <param name="ct">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// The task result contains the resolved <see cref="TenantContext"/>.
    /// </returns>
    Task<TenantContext> ResolveAsync(HttpContext httpContext, CancellationToken ct = default);
}
