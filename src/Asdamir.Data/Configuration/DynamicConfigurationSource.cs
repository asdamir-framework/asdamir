// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Asdamir.Data.Configuration;

/// <summary>
/// Configuration source for dynamic, periodically-refreshed configuration data.
/// Integrates with <see cref="ILoggerFactory"/> for structured logging.
/// </summary>
public sealed class DynamicConfigurationSource : IConfigurationSource
{
    /// <summary>
    /// Gets or sets the function that loads configuration data asynchronously.
    /// </summary>
    public Func<CancellationToken, Task<Dictionary<string, string?>>> Loader { get; set; } = _ => Task.FromResult(new Dictionary<string, string?>());

    /// <summary>
    /// Gets or sets the interval between configuration refresh checks.
    /// Default is 5 minutes.
    /// </summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Optional logger factory for creating loggers. If null, NullLoggerFactory is used.
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; set; }

    /// <summary>
    /// Builds the configuration provider with optional logger support.
    /// </summary>
    /// <param name="builder">The configuration builder.</param>
    /// <returns>A new <see cref="DynamicConfigurationProvider"/> instance.</returns>
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        // Use LoggerFactory if set, otherwise try to get from builder properties
        ILoggerFactory? loggerFactory = LoggerFactory;

        if (loggerFactory == null && builder.Properties.TryGetValue("Services", out var servicesObj))
        {
            if (servicesObj is IServiceProvider serviceProvider)
            {
                loggerFactory = serviceProvider.GetService<ILoggerFactory>();
            }
        }

        var logger = loggerFactory?.CreateLogger<DynamicConfigurationProvider>()
                  ?? NullLogger<DynamicConfigurationProvider>.Instance;

        return new DynamicConfigurationProvider(this, logger);
    }
}