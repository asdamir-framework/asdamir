// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Asdamir.Core.Modules;

/// <summary>
/// Provides extension methods for registering the module system with the dependency injection container.
/// </summary>
public static class ModuleServiceCollectionExtensions
{
    /// <summary>
    /// Adds the module system to the service collection using configuration.
    /// </summary>
    /// <param name="services">The service collection to add the module system to</param>
    /// <param name="configuration">The application configuration to read module settings from</param>
    /// <returns>The service collection for method chaining</returns>
    /// <remarks>
    /// This method registers:
    /// - <see cref="ModuleLoader"/> as a singleton for managing module lifecycle
    /// - <see cref="ModuleOptions"/> with default values
    /// <para>
    /// Example usage:
    /// <code>
    /// services.AddModuleSystem(configuration);
    /// </code>
    /// </para>
    /// </remarks>
    public static IServiceCollection AddModuleSystem(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ModuleLoader>();
        services.Configure<ModuleOptions>(_ => { });

        return services;
    }

    /// <summary>
    /// Adds the module system to the service collection with optional configuration.
    /// </summary>
    /// <param name="services">The service collection to add the module system to</param>
    /// <param name="configure">An optional action to configure <see cref="ModuleOptions"/></param>
    /// <returns>The service collection for method chaining</returns>
    /// <remarks>
    /// This method registers:
    /// - <see cref="ModuleLoader"/> as a singleton for managing module lifecycle
    /// - <see cref="ModuleOptions"/> with custom configuration if provided, otherwise defaults
    /// <para>
    /// Example usage with custom configuration:
    /// <code>
    /// services.AddModuleSystem(options => {
    ///     options.ModulesFolder = "CustomModules";
    ///     options.AutoLoadModules = true;
    ///     options.EnableHotReload = false;
    /// });
    /// </code>
    /// </para>
    /// <para>
    /// Example usage with defaults:
    /// <code>
    /// services.AddModuleSystem();
    /// </code>
    /// </para>
    /// </remarks>
    public static IServiceCollection AddModuleSystem(this IServiceCollection services, Action<ModuleOptions>? configure = null)
    {
        services.AddSingleton<ModuleLoader>();

        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<ModuleOptions>(_ => { });
        }

        return services;
    }
}

/// <summary>
/// Configuration options for the module system.
/// </summary>
public sealed class ModuleOptions
{
    /// <summary>
    /// Gets or sets the folder path where module .dll files are located.
    /// </summary>
    /// <value>The folder path relative to the application root. Default is "Modules".</value>
    public string ModulesFolder { get; set; } = "Modules";

    /// <summary>
    /// Gets or sets a value indicating whether modules should be automatically loaded at startup.
    /// </summary>
    /// <value><c>true</c> to load modules automatically; otherwise, <c>false</c>. Default is <c>true</c>.</value>
    /// <remarks>
    /// When set to <c>false</c>, modules must be loaded manually by calling
    /// <see cref="ModuleLoader.LoadModulesAsync"/> explicitly.
    /// </remarks>
    public bool AutoLoadModules { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether hot reload of modules is enabled.
    /// </summary>
    /// <value><c>true</c> to enable hot reload; otherwise, <c>false</c>. Default is <c>false</c>.</value>
    /// <remarks>
    /// When enabled, the module system will watch for changes to module .dll files and
    /// automatically reload them without restarting the application.
    /// This feature is currently not fully implemented and should remain disabled in production.
    /// </remarks>
    public bool EnableHotReload { get; set; } = false;
}
