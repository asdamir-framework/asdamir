// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Asdamir.Core.MultiTenancy;

/// <summary>
/// Extension methods for configuring multi-tenancy services.
/// </summary>
public static class TenancyServiceCollectionExtensions
{
    /// <summary>
    /// Adds multi-tenancy services to the specified <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">An optional action to configure tenancy options.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This method registers:
    /// <list type="bullet">
    /// <item><description><see cref="ITenantResolver"/> as a singleton (default: <see cref="HeaderTenantResolver"/>)</description></item>
    /// <item><description><see cref="IHttpContextAccessor"/> as a singleton</description></item>
    /// <item><description><see cref="ITenantContext"/> as scoped (resolved from <see cref="HttpContext.Items"/>)</description></item>
    /// </list>
    /// The tenant context is populated by <see cref="TenantMiddleware"/> during request processing.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Basic usage with default header (X-Tenant-Id)
    /// services.AddMultiTenancy();
    /// 
    /// // Custom header name
    /// services.AddMultiTenancy(options => 
    /// {
    ///     options.HeaderName = \"X-Custom-Tenant\";
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddMultiTenancy(this IServiceCollection services, Action<TenancyOptions>? configure = null)
    {
        var opt = new TenancyOptions();
        configure?.Invoke(opt);

        services.TryAddSingleton<ITenantResolver>(new HeaderTenantResolver(opt.HeaderName));
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        // ITenantContext her istek için HttpContext.Items üzerinden resolve edilir
        services.TryAddScoped<ITenantContext>(sp =>
        {
            var accessor = sp.GetRequiredService<IHttpContextAccessor>();
            var items = accessor.HttpContext?.Items;
            if (items is not null && items.TryGetValue(TenantMiddleware.HttpItemsKey, out var val) && val is ITenantContext tc)
                return tc;
            // Fallback: default
            return new TenantContext { TenantId = "default", IsMultiTenant = true };
        });

        return services;
    }

    /// <summary>
    /// Adds the multi-tenancy middleware to the application pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    /// <remarks>
    /// This middleware should be added early in the pipeline, before any middleware
    /// that requires tenant context (e.g., authentication, authorization, routing).
    /// </remarks>
    /// <example>
    /// <code>
    /// var app = builder.Build();
    /// app.UseMultiTenancy(); // Add early in pipeline
    /// app.UseAuthentication();
    /// app.UseAuthorization();
    /// </code>
    /// </example>
    public static IApplicationBuilder UseMultiTenancy(this IApplicationBuilder app)
        => app.UseMiddleware<TenantMiddleware>();
}

/// <summary>
/// Configuration options for multi-tenancy.
/// </summary>
public sealed class TenancyOptions
{
    /// <summary>
    /// Gets or sets the name of the HTTP header used to extract the tenant ID.
    /// </summary>
    /// <value>
    /// The header name. Defaults to "X-Tenant-Id".
    /// </value>
    public string HeaderName { get; set; } = "X-Tenant-Id";
}
