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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Asdamir.Web.Security.Services;

/// <summary>
/// Startup service that runs security analysis on application start
/// </summary>
public class SecurityStartupService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SecurityStartupService> _logger;
    private readonly SecurityAnalysisOptions _options;

    public SecurityStartupService(
        IServiceProvider serviceProvider,
        ILogger<SecurityStartupService> logger,
        SecurityAnalysisOptions options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.RunOnStartup)
        {
            _logger.LogInformation("🔍 Startup security analysis disabled");
            return;
        }

        _logger.LogInformation("🔍 Running startup security analysis...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var analyzer = scope.ServiceProvider.GetRequiredService<SecurityCodeAnalyzer>();
            
            var result = await analyzer.AnalyzeAsync();

            // Log startup analysis summary
            _logger.LogInformation("🔍 Startup Security Analysis Complete:");
            _logger.LogInformation("   📊 Security Score: {Score}/100", result.SecurityScore);
            _logger.LogInformation("   🚨 Total Violations: {Total}", result.TotalViolations);

            // Check minimum security score
            if (result.SecurityScore < _options.MinimumSecurityScore)
            {
                _logger.LogWarning("⚠️ Application security score ({Score}) is below minimum threshold ({Minimum})", 
                    result.SecurityScore, _options.MinimumSecurityScore);
            }

            // Alert on critical violations
            if (result.CriticalViolations > 0)
            {
                _logger.LogError("🚨 CRITICAL: {Count} critical security violations detected on startup!", result.CriticalViolations);
                
                foreach (var violation in result.Violations.Where(v => v.Severity == SecuritySeverity.Critical))
                {
                    _logger.LogError("   💥 {Code}: {Title}", violation.Code, violation.Title);
                }
            }

            // Development environment warnings
            var environment = scope.ServiceProvider.GetService<IHostEnvironment>();
            if (environment?.IsDevelopment() == true)
            {
                _logger.LogInformation("🔧 Development environment detected - security analysis results may include development-specific warnings");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Startup security analysis failed");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}