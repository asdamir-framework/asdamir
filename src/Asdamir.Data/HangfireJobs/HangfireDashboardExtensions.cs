// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Hangfire.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;

namespace Asdamir.Data.HangfireJobs;

/// <summary>
/// Extensions for configuring Hangfire Dashboard with Data.HangfireJobs
/// </summary>
public static class HangfireDashboardExtensions
{
    /// <summary>
    /// Add enhanced Hangfire Dashboard with Data.HangfireJobs features
    /// </summary>
    /// <param name="app">Application builder</param>
    /// <param name="pathMatch">Dashboard path (default: /hangfire)</param>
    /// <param name="options">Dashboard options</param>
    /// <returns>Application builder</returns>
    public static IApplicationBuilder UseCoreHangfireDashboard(
        this IApplicationBuilder app,
        string pathMatch = "/hangfire",
        DashboardOptions? options = null)
    {
        options ??= new DashboardOptions
        {
            Authorization = new[] { new CoreHangfireAuthorizationFilter() },
            DashboardTitle = "Data.HangfireJobs Dashboard",
            AppPath = "/",
            StatsPollingInterval = 5000, // 5 seconds
            DisplayStorageConnectionString = false
        };

        app.UseHangfireDashboard(pathMatch, options);
        return app;
    }

    /// <summary>
    /// Add Hangfire Dashboard API endpoints for programmatic access
    /// </summary>
    /// <param name="app">Application builder</param>
    /// <param name="pathMatch">API path (default: /api/hangfire)</param>
    /// <returns>Application builder</returns>
    public static IApplicationBuilder UseCoreHangfireApi(
        this IApplicationBuilder app,
        string pathMatch = "/api/hangfire")
    {
        app.Map(pathMatch, apiApp =>
        {
            apiApp.UseRouting();
            apiApp.UseEndpoints(endpoints =>
            {
                // Statistics endpoint
                endpoints.MapGet("/stats", async (IJobMonitor monitor) =>
                {
                    var stats = await monitor.GetStatisticsAsync();
                    return Results.Ok(stats);
                });

                // Failed jobs endpoint
                endpoints.MapGet("/failed", async (IJobMonitor monitor, int skip = 0, int take = 50) =>
                {
                    var jobs = await monitor.GetFailedJobsAsync(skip, take);
                    return Results.Ok(jobs);
                });

                // Processing jobs endpoint
                endpoints.MapGet("/processing", async (IJobMonitor monitor, int skip = 0, int take = 50) =>
                {
                    var jobs = await monitor.GetProcessingJobsAsync(skip, take);
                    return Results.Ok(jobs);
                });

                // Succeeded jobs endpoint
                endpoints.MapGet("/succeeded", async (IJobMonitor monitor, int skip = 0, int take = 50) =>
                {
                    var jobs = await monitor.GetSucceededJobsAsync(skip, take);
                    return Results.Ok(jobs);
                });

                // Job status endpoint
                endpoints.MapGet("/job/{jobId}/status", async (string jobId, IJobScheduler scheduler) =>
                {
                    var status = await scheduler.GetStatusAsync(jobId);
                    return Results.Ok(new { JobId = jobId, Status = status.ToString() });
                });

                // Cancel job endpoint
                endpoints.MapPost("/job/{jobId}/cancel", async (string jobId, IJobScheduler scheduler) =>
                {
                    var result = await scheduler.CancelAsync(jobId);
                    return Results.Ok(new { JobId = jobId, Cancelled = result });
                });

                // Remove recurring job endpoint
                endpoints.MapDelete("/recurring/{jobId}", async (string jobId, IJobScheduler scheduler) =>
                {
                    var result = await scheduler.RemoveRecurringAsync(jobId);
                    return Results.Ok(new { JobId = jobId, Removed = result });
                });
            });
        });

        return app;
    }
}

/// <summary>
/// Custom authorization filter for Hangfire Dashboard
/// </summary>
public class CoreHangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    /// <summary>
    /// Authorize dashboard access
    /// </summary>
    /// <param name="context">Dashboard context</param>
    /// <returns>True if authorized</returns>
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        
        // In development, allow all access
        if (httpContext.RequestServices.GetService<IWebHostEnvironment>()?.IsDevelopment() == true)
        {
            return true;
        }

        // In production, add proper authorization logic
        // Example: Check if user is authenticated and has admin role
        return httpContext.User.Identity?.IsAuthenticated == true && 
               httpContext.User.IsInRole("Admin");
    }
}

/// <summary>
/// Job monitoring statistics for API responses
/// </summary>
public class JobMonitoringStats
{
    public JobStatistics Statistics { get; set; } = new();
    public IEnumerable<JobInfo> RecentFailedJobs { get; set; } = Array.Empty<JobInfo>();
    public IEnumerable<JobInfo> CurrentlyProcessing { get; set; } = Array.Empty<JobInfo>();
    public Dictionary<string, object> SystemInfo { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Real-time job monitoring service
/// </summary>
public class JobMonitoringService : BackgroundService
{
    private readonly IJobMonitor _jobMonitor;
    private readonly ILogger<JobMonitoringService> _logger;
    private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(30);

    public JobMonitoringService(IJobMonitor jobMonitor, ILogger<JobMonitoringService> logger)
    {
        _jobMonitor = jobMonitor;
        _logger = logger;
    }

    /// <summary>
    /// Latest monitoring statistics
    /// </summary>
    public JobMonitoringStats? LatestStats { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdateStatistics();
                await Task.Delay(_updateInterval, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating job monitoring statistics");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private async Task UpdateStatistics()
    {
        try
        {
            var stats = await _jobMonitor.GetStatisticsAsync();
            var failedJobs = await _jobMonitor.GetFailedJobsAsync(0, 10);
            var processingJobs = await _jobMonitor.GetProcessingJobsAsync(0, 10);

            LatestStats = new JobMonitoringStats
            {
                Statistics = stats,
                RecentFailedJobs = failedJobs,
                CurrentlyProcessing = processingJobs,
                SystemInfo = new Dictionary<string, object>
                {
                    ["Environment"] = Environment.MachineName,
                    ["ProcessorCount"] = Environment.ProcessorCount,
                    ["WorkingSet"] = Environment.WorkingSet,
                    ["Version"] = typeof(JobMonitoringService).Assembly.GetName().Version?.ToString() ?? "Unknown"
                }
            };

            _logger.LogDebug("Job monitoring statistics updated. Total jobs: {TotalJobs}", stats.Total);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update job monitoring statistics");
        }
    }
}