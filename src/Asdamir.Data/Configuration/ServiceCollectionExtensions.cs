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

namespace Asdamir.Data.Configuration;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add dynamic configuration from database
    /// </summary>
    public static IServiceCollection AddDynamicConfiguration(
        this IServiceCollection services,
        Action<DynamicConfigurationOptions>? configure = null)
    {
        var options = new DynamicConfigurationOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);

        return services;
    }

    /// <summary>
    /// Add feature manager for feature flags
    /// </summary>
    public static IServiceCollection AddFeatureManager(this IServiceCollection services)
    {
        services.AddScoped<IFeatureManager, FeatureManager>();

        return services;
    }
}

public class DynamicConfigurationOptions
{
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(5);
    public string? ConnectionString { get; set; }
}
