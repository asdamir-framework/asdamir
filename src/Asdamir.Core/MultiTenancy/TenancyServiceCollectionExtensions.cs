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
    /// The tenant context is populated by <see cref="TenantMiddleware"/> during request processing —
    /// <b>registration alone resolves nothing; <see cref="UseMultiTenancy"/> is the other half</b>. What
    /// <see cref="ITenantContext"/> yields when no tenant is available depends on
    /// <see cref="TenancyOptions.MissingTenant"/>: under the default <see cref="MissingTenantBehavior.Reject"/>
    /// there is no implicit tenant — the middleware refuses an authenticated request without one (403), a
    /// request whose pipeline never ran the middleware fails with <see cref="TenancyNotConfiguredException"/>
    /// on first use, and a non-HTTP scope (a background job) gets an unresolved, settable
    /// <see cref="TenantContext"/> that it must fill in itself. <see cref="MissingTenantBehavior.UseDefault"/>
    /// restores the pre-2.0 behaviour (everything missing becomes <see cref="TenancyOptions.DefaultTenantId"/>).
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

        services.TryAddSingleton(opt);
        services.TryAddSingleton<ITenantResolver>(new HeaderTenantResolver(opt.HeaderName));
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        // ITenantContext her istek için HttpContext.Items üzerinden resolve edilir
        services.TryAddScoped<ITenantContext>(sp =>
        {
            var accessor = sp.GetRequiredService<IHttpContextAccessor>();
            var items = accessor.HttpContext?.Items;
            if (items is not null && items.TryGetValue(TenantMiddleware.HttpItemsKey, out var val) && val is ITenantContext tc)
                return tc;

            var options = sp.GetRequiredService<TenancyOptions>();
            if (options.MissingTenant == MissingTenantBehavior.UseDefault)
                return new TenantContext { TenantId = options.DefaultTenantId, IsMultiTenant = true };

            // Reject (default). Inside a request with no middleware result, the pipeline is mis-wired:
            // say so on first use instead of serving the request as some tenant. Outside a request
            // (background work) hand back an unresolved, settable context — the job sets its own tenant,
            // and reading it before that throws.
            return accessor.HttpContext is not null
                ? UnwiredTenantContext.Instance
                : new TenantContext { IsMultiTenant = true };
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
    /// What happens when no tenant can be resolved. Defaults to <see cref="MissingTenantBehavior.Reject"/>:
    /// a missing tenant is an error, never a silent default.
    /// </summary>
    public MissingTenantBehavior MissingTenant { get; set; } = MissingTenantBehavior.Reject;

    /// <summary>
    /// The tenant used when <see cref="MissingTenant"/> is <see cref="MissingTenantBehavior.UseDefault"/>.
    /// Ignored under <see cref="MissingTenantBehavior.Reject"/>.
    /// </summary>
    public string DefaultTenantId { get; set; } = "default";

    /// <summary>
    /// Gets or sets the name of the HTTP header used to extract the tenant ID.
    /// </summary>
    /// <value>
    /// The header name. Defaults to "X-Tenant-Id".
    /// </value>
    public string HeaderName { get; set; } = "X-Tenant-Id";
}

/// <summary>How the tenancy layer treats a scope for which no tenant could be resolved.</summary>
public enum MissingTenantBehavior
{
    /// <summary>
    /// No implicit tenant. An authenticated request without a tenant is refused (403,
    /// <see cref="TenantNotResolvedException"/>); reading <see cref="ITenantContext.TenantId"/> anywhere
    /// else without a tenant throws. This is the default.
    /// </summary>
    Reject = 0,

    /// <summary>
    /// Every scope without a tenant runs as <see cref="TenancyOptions.DefaultTenantId"/> — the pre-2.0
    /// behaviour. Choose it only for a deliberately single-tenant host; it removes the tenant boundary
    /// for any token that lacks the claim.
    /// </summary>
    UseDefault = 1,
}

/// <summary>
/// What <see cref="ITenantContext"/> resolves to inside an HTTP request whose pipeline never ran
/// <see cref="TenantMiddleware"/>: reports unresolved, and throws the wiring error on any tenant read.
/// </summary>
internal sealed class UnwiredTenantContext : ITenantContext
{
    public static readonly UnwiredTenantContext Instance = new();
    private static readonly IReadOnlyDictionary<string, object?> Empty = new Dictionary<string, object?>();

    public string TenantId => throw new TenancyNotConfiguredException();
    public bool IsResolved => false;
    public string? TenantName => null;
    public bool IsMultiTenant => false;
    public IReadOnlyDictionary<string, object?> Properties => Empty;
}
