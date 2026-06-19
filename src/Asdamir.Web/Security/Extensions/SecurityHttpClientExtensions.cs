// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Web.Security.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Asdamir.Web.Security.Extensions;

/// <summary>
/// Extension methods for adding Security Framework HTTP services
/// </summary>
public static class SecurityHttpClientExtensions
{
    /// <summary>
    /// Add Security Framework Bearer token handler to HTTP client
    /// </summary>
    public static IHttpClientBuilder AddSecurityBearerHandler(this IHttpClientBuilder builder)
    {
        return builder.AddHttpMessageHandler<BearerHandler>();
    }

    /// <summary>
    /// Configure HTTP client with Security Framework authentication
    /// </summary>
    public static IServiceCollection AddSecurityHttpClient(this IServiceCollection services, 
        string name, 
        Action<HttpClient>? configureClient = null)
    {
        var builder = configureClient != null 
            ? services.AddHttpClient(name, configureClient)
            : services.AddHttpClient(name);
        builder.AddSecurityBearerHandler();
        return services;
    }

    /// <summary>
    /// Configure HTTP client with Security Framework authentication (generic)
    /// </summary>
    public static IServiceCollection AddSecurityHttpClient<TClient>(this IServiceCollection services, 
        Action<HttpClient>? configureClient = null) where TClient : class
    {
        var builder = configureClient != null 
            ? services.AddHttpClient<TClient>(configureClient)
            : services.AddHttpClient<TClient>();
        builder.AddSecurityBearerHandler();
        return services;
    }
}