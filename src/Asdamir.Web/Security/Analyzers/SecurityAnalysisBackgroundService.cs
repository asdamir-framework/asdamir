// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Asdamir.Web.Security.Analyzers;

/// <summary>
/// Background service that periodically runs security analysis
/// </summary>
public class SecurityAnalysisBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SecurityAnalysisBackgroundService> _logger;
    private readonly SecurityAnalysisOptions _options;

    public SecurityAnalysisBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<SecurityAnalysisBackgroundService> logger,
        SecurityAnalysisOptions options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.EnablePeriodicAnalysis)
        {
            _logger.LogInformation("Periodic security analysis disabled");
            return;
        }

        _logger.LogInformation("Starting periodic security analysis service (interval: {Interval})", _options.AnalysisInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan nextDelay = _options.AnalysisInterval;
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var analyzer = scope.ServiceProvider.GetRequiredService<SecurityCodeAnalyzer>();

                _logger.LogInformation("Running scheduled security analysis...");
                var result = await analyzer.AnalyzeAsync().ConfigureAwait(false);

                if (result.CriticalViolations > 0)
                {
                    _logger.LogError("SECURITY ALERT: {Count} critical security violations detected!", result.CriticalViolations);
                    if (_options.NotifyOnCriticalViolations)
                    {
                        await NotifyCriticalViolationsAsync(result).ConfigureAwait(false);
                    }
                }

                if (_options.GenerateReports)
                {
                    await GenerateSecurityReportAsync(result).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Audit fix: graceful shutdown — don't log this as an error.
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during periodic security analysis");
                nextDelay = TimeSpan.FromMinutes(5);
            }

            try
            {
                await Task.Delay(nextDelay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task NotifyCriticalViolationsAsync(SecurityAnalysisResult result)
    {
        // Here you could implement notifications:
        // - Email alerts
        // - Slack notifications  
        // - Teams notifications
        // - Push notifications to monitoring systems

        _logger.LogError("🚨 Critical Security Violations Detected:");
        foreach (var violation in result.Violations.Where(v => v.Severity == SecuritySeverity.Critical))
        {
            _logger.LogError("   💥 {Code}: {Title}", violation.Code, violation.Title);
        }

        await Task.CompletedTask;
    }

    private async Task GenerateSecurityReportAsync(SecurityAnalysisResult result)
    {
        try
        {
            var reportPath = Path.Combine("logs", $"security-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);

            var reportData = new
            {
                Timestamp = DateTime.UtcNow,
                SecurityScore = result.SecurityScore,
                Summary = new
                {
                    Total = result.TotalViolations,
                    Critical = result.CriticalViolations,
                    High = result.HighViolations,
                    Medium = result.MediumViolations,
                    Low = result.LowViolations
                },
                Violations = result.Violations.Select(v => new
                {
                    v.Code,
                    v.Title,
                    v.Description,
                    Severity = v.Severity.ToString(),
                    v.Category,
                    v.DetectedAt
                }),
                AnalysisTime = result.AnalysisTime.TotalMilliseconds
            };

            var json = System.Text.Json.JsonSerializer.Serialize(reportData, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(reportPath, json);
            _logger.LogInformation("📊 Security report generated: {Path}", reportPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Failed to generate security report");
        }
    }
}

/// <summary>
/// Configuration options for security analysis
/// </summary>
public class SecurityAnalysisOptions
{
    /// <summary>
    /// Enable periodic security analysis
    /// </summary>
    public bool EnablePeriodicAnalysis { get; set; } = false;

    /// <summary>
    /// Interval between security analyses
    /// </summary>
    public TimeSpan AnalysisInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Generate JSON reports of security analysis
    /// </summary>
    public bool GenerateReports { get; set; } = true;

    /// <summary>
    /// Send notifications when critical violations are found
    /// </summary>
    public bool NotifyOnCriticalViolations { get; set; } = true;

    /// <summary>
    /// Run analysis on application startup
    /// </summary>
    public bool RunOnStartup { get; set; } = true;

    /// <summary>
    /// Minimum security score to consider acceptable
    /// </summary>
    public int MinimumSecurityScore { get; set; } = 80;
}