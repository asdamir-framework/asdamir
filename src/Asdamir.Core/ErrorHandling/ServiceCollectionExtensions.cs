// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.ErrorHandling.Abstractions;
using Asdamir.Core.ErrorHandling.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Asdamir.Core.ErrorHandling.Http;

namespace Asdamir.Core.ErrorHandling.Extensions;

/// <summary>
/// Extension methods for configuring Asdamir.Core.ErrorHandling services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds global exception handling middleware with configuration options
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Action to configure ErrorHandlingOptions</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddGlobalExceptionHandling(
        this IServiceCollection services,
        Action<ErrorHandlingOptions> configureOptions)
    {
        // Register options
        services.Configure(configureOptions);

        // The middleware requires IProblemDetailsMapper and resolves it in its constructor (middleware
        // are singletons), so the mapper must be singleton-resolvable. DefaultProblemDetailsMapper is
        // stateless (only an optional ILocalizationService), so Singleton is safe. TryAdd → a custom
        // mapper registered earlier wins. Without this, the pipeline throws at startup/first request.
        services.TryAddSingleton<IProblemDetailsMapper, DefaultProblemDetailsMapper>();

        return services;
    }
    
    /// <summary>
    /// Adds global exception handling middleware with default configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddGlobalExceptionHandling(this IServiceCollection services)
    {
        return services.AddGlobalExceptionHandling(options => { });
    }
    
    /// <summary>
    /// Uses the global exception handling middleware in the application pipeline
    /// </summary>
    /// <param name="app">The application builder</param>
    /// <returns>The application builder for chaining</returns>
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionMiddleware>();
    }
}
