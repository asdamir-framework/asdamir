// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Microsoft.Extensions.DependencyInjection;
using Asdamir.Web.Security.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Asdamir.Web.Security.Extensions;

/// <summary>
/// Extension methods for registering enterprise authorization services.
///
/// Audit fix vs. v1: registrations were <see cref="ServiceCollectionServiceExtensions.AddScoped{TService,TImplementation}(IServiceCollection)"/>,
/// not <c>TryAdd*</c>. Calling <see cref="AddEnterpriseAuthorization"/> twice (or once
/// after <see cref="AddBasicAuthorization"/>) double-registered every interface, which
/// for <c>IEnumerable&lt;IAuthorizationAuditService&gt;</c>-style resolution would have
/// yielded two instances and double-fired the audit pipeline. Switching to TryAdd makes
/// these extensions idempotent.
/// </summary>
public static class EnterpriseAuthorizationExtensions
{
    /// <summary>
    /// Adds enterprise-grade authorization services including token management,
    /// audit logging, rate limiting, and caching.
    /// Idempotent — safe to call from multiple framework registrations.
    /// </summary>
    public static IServiceCollection AddEnterpriseAuthorization(this IServiceCollection services)
    {
        services.TryAddScoped<IAuthorizationTokenService, AuthorizationTokenService>();
        services.TryAddScoped<IAuthorizationAuditService, AuthorizationAuditService>();
        services.TryAddSingleton<IAuthorizationRateLimiter, AuthorizationRateLimiter>();
        services.TryAddSingleton<IAuthorizationCache, AuthorizationCache>();
        return services;
    }

    /// <summary>
    /// Adds only basic authorization services without caching and rate limiting.
    /// Idempotent.
    /// </summary>
    public static IServiceCollection AddBasicAuthorization(this IServiceCollection services)
    {
        services.TryAddScoped<IAuthorizationTokenService, AuthorizationTokenService>();
        services.TryAddScoped<IAuthorizationAuditService, AuthorizationAuditService>();
        return services;
    }
}
