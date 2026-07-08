// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Web.Security.Options;
namespace Asdamir.Web.Security.Extensions;

using Blazored.LocalStorage;
using Blazored.SessionStorage;
using Asdamir.Web.Security.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Asdamir.Web.Security.Middleware;
using Microsoft.Extensions.Options;

/// <summary>
/// DI + pipeline wiring for the Asdamir.Web security stack — Data Protection key persistence, the security
/// header/rate-limit/CSP-nonce/no-cache middleware, and the authentication/authorization services.
/// </summary>
public static class SecurityServiceCollectionExtensions
{
    /// <summary>
    /// Persists Data Protection keys so auth cookies + antiforgery tokens survive process restarts and
    /// work across instances (scale-out). Without this the default key ring is per-host and ephemeral in
    /// containers — every restart silently logs users out and breaks antiforgery.
    /// <list type="bullet">
    /// <item><c>DataProtection:ApplicationName</c> — a stable shared name (the default discriminator is
    /// the content-root path, which differs per deployment/container and isolates the keys).</item>
    /// <item><c>DataProtection:KeyPath</c> — a durable, shared directory (a persistent/mounted volume in
    /// production). Unset = the ASP.NET default location (fine for single-host dev, lost in ephemeral
    /// containers). Filesystem keeps this UI-tier-friendly (no DB — honours the layered rule).</item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddFrameworkDataProtection(this IServiceCollection services, IConfiguration configuration)
    {
        var dp = services.AddDataProtection();

        var appName = configuration["DataProtection:ApplicationName"];
        if (!string.IsNullOrWhiteSpace(appName))
            dp.SetApplicationName(appName);

        var keyPath = configuration["DataProtection:KeyPath"];
        if (!string.IsNullOrWhiteSpace(keyPath))
            dp.PersistKeysToFileSystem(new System.IO.DirectoryInfo(keyPath));

        return services;
    }

    /// <summary>
    /// Registers the core security services: Data Protection, memory cache, the CSP nonce provider, the
    /// data-protection service, the in-memory rate-limit service, ASP.NET Core authorization, and the
    /// <see cref="SecurityHeadersOptions"/> (+ its validator). Call once at composition root.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureHeaders">Optional configuration of the security headers; omit to use the hardened defaults.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddFrameworkSecurity(this IServiceCollection services, Action<SecurityHeadersOptions>? configureHeaders = null)
    {
        services.AddDataProtection();
        services.AddMemoryCache();
        services.AddHttpContextAccessor();
        services.AddSingleton<ICspNonceProvider, CspNonceProvider>();
        services.AddSingleton<IDataProtectionService, DataProtectionService>();
        services.AddSingleton<IRateLimitService, InMemoryRateLimitService>();

        // Add Authorization
        services.AddAuthorizationCore();

        if (configureHeaders is not null)
        {
            services.Configure(configureHeaders);
        }
        else
        {
            services.Configure<SecurityHeadersOptions>(opts => { });
        }

        services.AddSingleton<IValidateOptions<SecurityHeadersOptions>, SecurityHeadersOptionsValidator>();
        return services;
    }

    /// <summary>
    /// Adds the comprehensive Security Framework: authentication state, circuit barrier, bearer handler.
    ///
    /// Audit fix vs. v1: the previous implementation used <c>Configure&lt;IServiceProvider&gt;(async ...)</c>
    /// to read option values from <see cref="Asdamir.Core.Contracts.IAppConfigurationService"/>. That signature
    /// takes <see cref="Action{T1,T2}"/>, so the async lambda became fire-and-forget — the await ran unobserved
    /// and Configure returned BEFORE values were set. SessionTimeoutOptions/AutoLogoutOptions silently fell
    /// back to their CLR defaults.
    ///
    /// Now: options use their declared defaults; consumers override them with a strongly-typed
    /// <c>.Configure(opts =&gt; ...)</c> call (or by binding a configuration section) at composition root.
    /// </summary>
    public static IServiceCollection AddSecurityFramework(this IServiceCollection services)
    {
        services.AddOptions<AutoLogoutOptions>();

        // Core authentication services
        services.AddScoped<AuthState>();
        services.AddScoped<AuthenticationStateProvider, AppAuthStateProvider>();
        services.AddScoped<AutoLogoutService>();
        services.AddScoped<CorrelationIdProvider>();
        services.AddScoped<Asdamir.Web.Security.Http.BearerHandler>();

        // Authentication barrier for circuit synchronisation
        services.AddScoped<AuthenticationBarrier>();

        // NOTE: SessionTimeoutService (BackgroundService) was removed in v2.
        // Audit determined it never actually refreshed any session — it created a fresh DI scope
        // and called authState.IsAuthenticated on an empty AuthState, which always returned false.

        // Required dependencies
        services.AddBlazoredLocalStorage();
        services.AddBlazoredSessionStorage();
        services.AddHttpContextAccessor();

        return services;
    }

    /// <summary>Adds Security Framework with authentication state only (minimal setup).</summary>
    public static IServiceCollection AddSecurityAuthenticationState(this IServiceCollection services)
    {
        services.AddScoped<AuthState>();
        services.AddScoped<AuthenticationStateProvider, AppAuthStateProvider>();
        services.AddScoped<CorrelationIdProvider>();
        services.AddScoped<Asdamir.Web.Security.Http.BearerHandler>();
        services.AddBlazoredLocalStorage();
        services.AddBlazoredSessionStorage();
        services.AddHttpContextAccessor();

        return services;
    }

    /// <summary>Adds Security Framework auto-logout services.</summary>
    public static IServiceCollection AddSecurityAutoLogout(this IServiceCollection services,
        Action<AutoLogoutOptions>? configure = null)
    {
        if (configure != null)
        {
            services.Configure(configure);
        }

        services.AddScoped<AutoLogoutService>();
        return services;
    }


    /// <summary>Adds the middleware that emits the configured HTTP security headers (HSTS, X-Frame-Options, CSP, …) on every response.</summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same <see cref="IApplicationBuilder"/> for chaining.</returns>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SecurityHeadersMiddleware>();
    }

    /// <summary>Adds the middleware that enforces per-endpoint rate limits declared via <c>RateLimitAttribute</c> (returns 429 when exceeded).</summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same <see cref="IApplicationBuilder"/> for chaining.</returns>
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RateLimitingMiddleware>();
    }

    /// <summary>Adds the middleware that generates the per-request CSP nonce consumed by the security-headers CSP and inline scripts.</summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The same <see cref="IApplicationBuilder"/> for chaining.</returns>
    public static IApplicationBuilder UseCspNonce(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CspNonceMiddleware>();
    }

    /// <summary>
    /// Adds no-cache middleware to prevent browser caching of authenticated pages.
    /// </summary>
    public static IApplicationBuilder UseNoCache(this IApplicationBuilder app)
    {
        return app.UseMiddleware<NoCacheMiddleware>();
    }
}
