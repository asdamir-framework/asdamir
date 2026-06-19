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
using Hangfire;

namespace Asdamir.Data.HangfireJobs;

/// <summary>
/// Extension methods for configuring Data.HangfireJobs services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add Data.HangfireJobs services to the service collection
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddCoreHangfireJobs(this IServiceCollection services)
    {
        return services.AddCoreHangfireJobs(_ => { });
    }

    /// <summary>
    /// Add Data.HangfireJobs services to the service collection with configuration
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configure">Configuration action</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddCoreHangfireJobs(
        this IServiceCollection services,
        Action<HangfireJobsOptions> configure)
    {
        // Configure options
        services.Configure(configure);

        // Register core services
        services.AddScoped<IJobScheduler, HangfireJobScheduler>();
        services.AddScoped<IJobMonitor, HangfireJobMonitor>();
        services.AddScoped<JobExecutor>();

        // Register job context as scoped
        services.AddScoped<IJobContext, JobContext>();

        return services;
    }

    /// <summary>
    /// Register a job type in the service collection
    /// </summary>
    /// <typeparam name="TJob">Job type</typeparam>
    /// <param name="services">Service collection</param>
    /// <param name="lifetime">Service lifetime (default: Scoped)</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddJob<TJob>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
        where TJob : class, IJob
    {
        services.Add(new ServiceDescriptor(typeof(TJob), typeof(TJob), lifetime));
        return services;
    }

    /// <summary>
    /// Register multiple job types in the service collection
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="jobTypes">Job types to register</param>
    /// <param name="lifetime">Service lifetime (default: Scoped)</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddJobs(
        this IServiceCollection services,
        IEnumerable<Type> jobTypes,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        foreach (var jobType in jobTypes)
        {
            if (!typeof(IJob).IsAssignableFrom(jobType))
            {
                throw new ArgumentException($"Type {jobType.Name} does not implement IJob interface", nameof(jobTypes));
            }

            services.Add(new ServiceDescriptor(jobType, jobType, lifetime));
        }

        return services;
    }

    /// <summary>
    /// Register all job types from the specified assembly
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="assembly">Assembly to scan for job types</param>
    /// <param name="lifetime">Service lifetime (default: Scoped)</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddJobsFromAssembly(
        this IServiceCollection services,
        System.Reflection.Assembly assembly,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        var jobTypes = assembly.GetTypes()
            .Where(t => typeof(IJob).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
            .ToList();

        return services.AddJobs(jobTypes, lifetime);
    }

    /// <summary>
    /// Register all job types from the calling assembly
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="lifetime">Service lifetime (default: Scoped)</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddJobsFromCallingAssembly(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Scoped)
    {
        var callingAssembly = System.Reflection.Assembly.GetCallingAssembly();
        return services.AddJobsFromAssembly(callingAssembly, lifetime);
    }
}

/// <summary>
/// Configuration options for Data.HangfireJobs
/// </summary>
public class HangfireJobsOptions
{
    /// <summary>
    /// Default job timeout (default: 30 minutes)
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Default retry attempts (default: 3)
    /// </summary>
    public int DefaultRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Enable detailed logging (default: false)
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// Default queue name (default: "default")
    /// </summary>
    public string DefaultQueue { get; set; } = "default";

    /// <summary>
    /// Per-app queue for multi-company isolation (design §5.1). When set (e.g. the app's
    /// <c>Code</c>), the scheduler routes enqueued / scheduled / recurring jobs to this queue and
    /// the app's worker listens only on it — so one app's job surge can't starve another sharing
    /// the company Hangfire schema. Empty (default) = Hangfire's <c>default</c> queue (today's
    /// single-app behaviour, unchanged).
    /// </summary>
    public string AppQueue { get; set; } = string.Empty;

    /// <summary>
    /// Prefix for recurring-job ids, so two apps sharing the company Hangfire schema can both
    /// define e.g. "daily-cleanup" without colliding (design §5.1). When set (e.g. the app's
    /// <c>Code</c>), recurring ids become <c>{prefix}:{jobId}</c>. Empty (default) = no prefix.
    /// </summary>
    public string RecurringJobIdPrefix { get; set; } = string.Empty;

    /// <summary>
    /// Job execution timeout (default: 1 hour)
    /// </summary>
    public TimeSpan ExecutionTimeout { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Enable automatic retry on failure (default: true)
    /// </summary>
    public bool EnableAutomaticRetry { get; set; } = true;

    /// <summary>
    /// Retry delay strategy (default: Exponential)
    /// </summary>
    public RetryDelayStrategy RetryDelayStrategy { get; set; } = RetryDelayStrategy.Exponential;

    /// <summary>
    /// Base retry delay (default: 30 seconds)
    /// </summary>
    public TimeSpan BaseRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Maximum retry delay (default: 10 minutes)
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Job result retention period (default: 7 days)
    /// </summary>
    public TimeSpan JobRetention { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Enable job statistics collection (default: true)
    /// </summary>
    public bool EnableStatistics { get; set; } = true;

    /// <summary>
    /// Statistics update interval (default: 5 minutes)
    /// </summary>
    public TimeSpan StatisticsUpdateInterval { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// Retry delay strategy
/// </summary>
public enum RetryDelayStrategy
{
    /// <summary>
    /// Fixed delay between retries
    /// </summary>
    Fixed,

    /// <summary>
    /// Linear increase in delay (n * base delay)
    /// </summary>
    Linear,

    /// <summary>
    /// Exponential backoff (2^n * base delay)
    /// </summary>
    Exponential,

    /// <summary>
    /// Custom delay calculation
    /// </summary>
    Custom
}

/// <summary>
/// Extension methods for configuring Hangfire with Data.HangfireJobs
/// </summary>
public static class HangfireConfigurationExtensions
{
    /// <summary>
    /// Configure Hangfire with Data.HangfireJobs defaults
    /// </summary>
    /// <param name="configuration">Hangfire configuration</param>
    /// <param name="options">Data.HangfireJobs options</param>
    /// <returns>Hangfire configuration for chaining</returns>
    public static IGlobalConfiguration UseCoreHangfireJobs(
        this IGlobalConfiguration configuration,
        HangfireJobsOptions? options = null)
    {
        options ??= new HangfireJobsOptions();

        // Configure automatic retry
        if (options.EnableAutomaticRetry)
        {
            configuration.UseFilter(new AutomaticRetryAttribute
            {
                Attempts = options.DefaultRetryAttempts,
                DelaysInSeconds = CalculateRetryDelays(options)
            });
        }

        // Audit fix: a GLOBAL DisableConcurrentExecution filter serializes every job
        // across the cluster on a shared lock — `JobExecutor.ExecuteAsync(...)` is the
        // single entry point for every job type, so this collapses parallelism to
        // one-at-a-time process-wide. Individual jobs that genuinely need exclusivity
        // should apply [DisableConcurrentExecution(N)] on their concrete IJob method.
        return configuration;
    }

    /// <summary>
    /// Calculate retry delays based on strategy
    /// </summary>
    /// <param name="options">Job options</param>
    /// <returns>Array of delay seconds</returns>
    private static int[] CalculateRetryDelays(HangfireJobsOptions options)
    {
        var delays = new List<int>();
        var baseDelay = (int)options.BaseRetryDelay.TotalSeconds;
        var maxDelay = (int)options.MaxRetryDelay.TotalSeconds;

        for (int i = 0; i < options.DefaultRetryAttempts; i++)
        {
            int delay = options.RetryDelayStrategy switch
            {
                RetryDelayStrategy.Fixed => baseDelay,
                RetryDelayStrategy.Linear => baseDelay * (i + 1),
                RetryDelayStrategy.Exponential => (int)(baseDelay * Math.Pow(2, i)),
                _ => baseDelay
            };

            delays.Add(Math.Min(delay, maxDelay));
        }

        return delays.ToArray();
    }
}