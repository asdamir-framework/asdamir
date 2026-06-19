// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Web.Security.Analyzers;
using Microsoft.Extensions.DependencyInjection;

namespace Asdamir.Web.Security.Extensions;

/// <summary>
/// Extension methods for registering security analysis services
/// </summary>
public static class SecurityAnalysisServiceExtensions
{
    /// <summary>
    /// Add security analysis services to the service collection
    /// </summary>
    public static IServiceCollection AddSecurityAnalysis(this IServiceCollection services, Action<SecurityAnalysisOptions>? configure = null)
    {
        var options = new SecurityAnalysisOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddScoped<SecurityCodeAnalyzer>();

        if (options.EnablePeriodicAnalysis)
        {
            services.AddHostedService<SecurityAnalysisBackgroundService>();
        }

        return services;
    }

    /// <summary>
    /// Add security analysis with startup scan
    /// </summary>
    public static IServiceCollection AddSecurityAnalysisWithStartupScan(this IServiceCollection services, Action<SecurityAnalysisOptions>? configure = null)
    {
        return services.AddSecurityAnalysis(options =>
        {
            options.RunOnStartup = true;
            configure?.Invoke(options);
        });
    }

    /// <summary>
    /// Add enterprise security analysis with all features enabled
    /// </summary>
    public static IServiceCollection AddEnterpriseSecurityAnalysis(this IServiceCollection services, Action<SecurityAnalysisOptions>? configure = null)
    {
        return services.AddSecurityAnalysis(options =>
        {
            options.EnablePeriodicAnalysis = true;
            options.AnalysisInterval = TimeSpan.FromHours(1);
            options.GenerateReports = true;
            options.NotifyOnCriticalViolations = true;
            options.RunOnStartup = true;
            options.MinimumSecurityScore = 80;
            configure?.Invoke(options);
        });
    }
}