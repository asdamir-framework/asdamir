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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Asdamir.Core.Contracts;

/// <summary>
/// Defines the contract for a pluggable module in the application.
/// Modules provide a way to organize functionality into independent, reusable components
/// that can be dynamically loaded and configured at runtime.
/// </summary>
/// <remarks>
/// <para>
/// Modules follow a three-phase lifecycle:
/// 1. <see cref="ConfigureServices"/>: Register services with the DI container
/// 2. <see cref="Configure"/>: Configure the ASP.NET Core pipeline
/// 3. <see cref="InitializeAsync"/>: Perform asynchronous initialization
/// </para>
/// <para>
/// Implement this interface to package a feature as a self-registering, dynamically loaded module.
/// </para>
/// </remarks>
public interface IModule
{
    /// <summary>
    /// Gets the unique name of the module.
    /// </summary>
    /// <value>A string identifier for the module, e.g., "MyFeatureModule"</value>
    string Name { get; }

    /// <summary>
    /// Gets the version of the module.
    /// </summary>
    /// <value>A semantic version string, e.g., "1.0.0"</value>
    string Version { get; }

    /// <summary>
    /// Gets the names of other modules that this module depends on.
    /// </summary>
    /// <value>
    /// An enumerable of module names that must be loaded before this module.
    /// Returns an empty array by default if the module has no dependencies.
    /// </value>
    /// <remarks>
    /// The module system uses this to determine the load order.
    /// Circular dependencies are not supported and will cause initialization failures.
    /// </remarks>
    IEnumerable<string> Dependencies => Array.Empty<string>();

    /// <summary>
    /// Configures services for this module by registering them with the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to register services with</param>
    /// <param name="config">The application configuration to read module settings from</param>
    /// <remarks>
    /// This method is called during application startup, before the service provider is built.
    /// Typical operations include:
    /// - Registering interfaces and implementations (AddScoped, AddSingleton, AddTransient)
    /// - Configuring options using IOptions pattern
    /// - Adding middleware or filters
    /// </remarks>
    void ConfigureServices(IServiceCollection services, IConfiguration config);

    /// <summary>
    /// Configures the ASP.NET Core pipeline for this module.
    /// </summary>
    /// <param name="app">The application builder to configure middleware with</param>
    /// <param name="env">The hosting environment information</param>
    /// <remarks>
    /// This method is called after the service provider is built but before the application starts.
    /// Typical operations include:
    /// - Adding middleware to the pipeline (UseMiddleware)
    /// - Mapping endpoints or routes
    /// - Configuring static file serving
    /// </remarks>
    void Configure(IApplicationBuilder app, Microsoft.Extensions.Hosting.IHostEnvironment env);

    /// <summary>
    /// Performs asynchronous initialization for the module after services are configured.
    /// </summary>
    /// <param name="services">The service provider to resolve dependencies from</param>
    /// <param name="ct">A cancellation token to cancel the initialization</param>
    /// <returns>A task representing the asynchronous initialization operation</returns>
    /// <remarks>
    /// This method is called after <see cref="Configure"/> and allows modules to perform
    /// startup operations such as:
    /// - Database migrations or schema validation
    /// - Warming up caches
    /// - Establishing external connections
    /// - Validating configuration settings
    /// Default implementation returns a completed task (no initialization required).
    /// </remarks>
    Task InitializeAsync(IServiceProvider services, CancellationToken ct = default) => Task.CompletedTask;
}
