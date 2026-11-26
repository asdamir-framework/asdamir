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
using Microsoft.Extensions.Logging;

namespace Asdamir.Core.MultiTenancy;

/// <summary>
/// ASP.NET Core middleware that resolves tenant context for each HTTP request.
/// </summary>
/// <remarks>
/// This middleware executes early in the request pipeline to identify the current tenant
/// using the registered <see cref="ITenantResolver"/> implementation.
/// The resolved <see cref="ITenantContext"/> is stored in <see cref="HttpContext.Items"/>
/// for use throughout the request lifetime.
/// </remarks>
public sealed class TenantMiddleware
{
    /// <summary>
    /// The key used to store the tenant context in <see cref="HttpContext.Items"/>.
    /// </summary>
    public const string HttpItemsKey = "TenantContext";

    private readonly RequestDelegate _next;
    private readonly ITenantResolver _resolver;
    private readonly ILogger<TenantMiddleware> _logger;
    private readonly TenancyOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="resolver">The tenant resolver to use for identifying tenants.</param>
    /// <param name="logger">The logger for diagnostic information.</param>
    /// <param name="options">Tenancy options (registered by <c>AddMultiTenancy</c>); when absent the
    /// defaults apply, i.e. <see cref="MissingTenantBehavior.Reject"/>.</param>
    public TenantMiddleware(RequestDelegate next, ITenantResolver resolver, ILogger<TenantMiddleware> logger,
        TenancyOptions? options = null)
    {
        _options = options ?? new TenancyOptions();
        _next = next;
        _resolver = resolver;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the middleware to resolve the tenant context for the current request.
    /// </summary>
    /// <remarks>
    /// Audit fix vs. v1: the catch was a fail-open <c>catch (Exception)</c> that logged
    /// and continued the request with NO tenant context. A multi-tenant app that loses
    /// its tenant boundary mid-request is a data-leak hazard — the next downstream query
    /// would either NRE or fall back to a default tenant. We now distinguish:
    ///   - <see cref="OperationCanceledException"/>: client gave up, propagate naturally
    ///   - Any other exception: log + return 500. Resolvers that gracefully cannot
    ///     identify a tenant should RETURN an unresolved <see cref="TenantContext"/>, not throw.
    /// <para>
    /// An UNRESOLVED result is then handled here, in one place, per
    /// <see cref="TenancyOptions.MissingTenant"/>. <see cref="MissingTenantBehavior.Reject"/> (default):
    /// an <b>authenticated</b> request is refused with <see cref="TenantNotResolvedException"/> (403 via the
    /// global exception handler) — a known caller that belongs to no tenant must not be served any tenant's
    /// data; an <b>anonymous</b> request (health, login) continues with the unresolved context, which throws
    /// only if something actually reads the tenant; so does an authenticated request to an endpoint marked
    /// <see cref="AllowWithoutTenantAttribute"/> (seen only when routing ran before this middleware —
    /// otherwise the request is refused as usual). <see cref="MissingTenantBehavior.UseDefault"/>: the
    /// request runs as <see cref="TenancyOptions.DefaultTenantId"/>, as before 2.0.
    /// </para>
    /// </remarks>
    /// <exception cref="TenantNotResolvedException">Authenticated request, no tenant, <c>Reject</c>.</exception>
    public async Task InvokeAsync(HttpContext context)
    {
        ITenantContext tc;
        try
        {
            tc = await _resolver.ResolveAsync(context);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Tenant resolve error. ErrorKey: {ErrorKey}, Source: {Source}",
                "TENANT_RESOLVE_ERROR", "TenantMiddleware");
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return;
        }

        if (!tc.IsResolved)
        {
            if (_options.MissingTenant == MissingTenantBehavior.UseDefault)
            {
                tc = new TenantContext { TenantId = _options.DefaultTenantId, IsMultiTenant = true };
            }
            else if (context.User?.Identity?.IsAuthenticated == true
                     && context.GetEndpoint()?.Metadata.GetMetadata<AllowWithoutTenantAttribute>() is null)
            {
                _logger.LogWarning(
                    "Authenticated request without a tenant refused. ErrorKey: {ErrorKey}, Source: {Source}, Path: {Path}",
                    TenantNotResolvedException.ErrorCode, "TenantMiddleware", context.Request.Path.Value);
                throw new TenantNotResolvedException();
            }
        }

        context.Items[HttpItemsKey] = tc;
        await _next(context);
    }
}
