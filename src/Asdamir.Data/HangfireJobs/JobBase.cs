// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Asdamir.Data.HangfireJobs;

/// <summary>
/// Base class for jobs with common functionality
/// </summary>
public abstract class JobBase : IJob
{

    /// <summary>
    /// Gets the unique name of the job
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Executes the job with automatic error handling and timing
    /// </summary>
    /// <param name="context">Job execution context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Job execution result</returns>
    public virtual async Task<JobResult> ExecuteAsync(IJobContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var output = await ExecuteJobAsync(context, cancellationToken);
            stopwatch.Stop();

            return JobResult.Success(stopwatch.Elapsed, output);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return JobResult.Failure("Job was cancelled", null, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return JobResult.Failure(ex, stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Implement this method with your job logic
    /// </summary>
    /// <param name="context">Job execution context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Job output (optional)</returns>
    protected abstract Task<object?> ExecuteJobAsync(IJobContext context, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a required service from the job context
    /// </summary>
    /// <typeparam name="T">Service type</typeparam>
    /// <param name="context">Job context</param>
    /// <returns>Service instance</returns>
    protected T GetRequiredService<T>(IJobContext context) where T : notnull
    {
        return context.Services.GetRequiredService<T>();
    }

    /// <summary>
    /// Gets a service from the job context
    /// </summary>
    /// <typeparam name="T">Service type</typeparam>
    /// <param name="context">Job context</param>
    /// <returns>Service instance or null</returns>
    protected T? GetService<T>(IJobContext context)
    {
        return context.Services.GetService<T>();
    }

    /// <summary>
    /// Creates a new service scope from the job context
    /// </summary>
    /// <param name="context">Job context</param>
    /// <returns>Service scope</returns>
    protected IServiceScope CreateScope(IJobContext context)
    {
        return context.Services.CreateScope();
    }
}

/// <summary>
/// Default <see cref="IJobContext"/> implementation. The DI container always returns this
/// type via the <see cref="IJobContext"/> contract; consumers should never depend on the
/// concrete type directly.
/// </summary>
/// <remarks>
/// Audit fix: was <c>public class</c> with no other consumers — switched to
/// <c>internal sealed</c> so the framework's NuGet contract is the interface only.
/// </remarks>
internal sealed class JobContext : IJobContext
{
    /// <summary>
    /// Gets or sets the job ID
    /// </summary>
    public string JobId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the job name
    /// </summary>
    public string JobName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the job parameters
    /// </summary>
    public IDictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets or sets the service provider
    /// </summary>
    public IServiceProvider Services { get; set; } = default!;

    /// <summary>
    /// Gets or sets the cancellation token
    /// </summary>
    public CancellationToken CancellationToken { get; set; }

    /// <summary>
    /// Gets a parameter value with default
    /// </summary>
    /// <typeparam name="T">Parameter type</typeparam>
    /// <param name="key">Parameter key</param>
    /// <param name="defaultValue">Default value if parameter not found</param>
    /// <returns>Parameter value or default</returns>
    public T GetParameter<T>(string key, T defaultValue)
    {
        if (Parameters.TryGetValue(key, out var value))
        {
            if (value is T typedValue)
            {
                return typedValue;
            }

            // Try to convert
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// Gets a parameter value
    /// </summary>
    /// <typeparam name="T">Parameter type</typeparam>
    /// <param name="key">Parameter key</param>
    /// <returns>Parameter value or default</returns>
    public T? GetParameter<T>(string key)
    {
        if (Parameters.TryGetValue(key, out var value))
        {
            if (value is T typedValue)
            {
                return typedValue;
            }

            // Try to convert
            try
            {
                return (T?)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return default;
            }
        }

        return default;
    }

    /// <summary>
    /// Sets a parameter value
    /// </summary>
    /// <param name="key">Parameter key</param>
    /// <param name="value">Parameter value</param>
    public void SetParameter(string key, object value)
    {
        Parameters[key] = value;
    }
}

/// <summary>
/// Job executor service for executing jobs
/// </summary>
public class JobExecutor
{
    private readonly IServiceProvider _services;
    private readonly ILogger<JobExecutor> _logger;

    /// <summary>Creates the job executor over the service provider (for per-job scopes) and logger.</summary>
    public JobExecutor(IServiceProvider services, ILogger<JobExecutor> logger)
    {
        _services = services;
        _logger = logger;
    }

    /// <summary>
    /// Execute a job by type name
    /// </summary>
    /// <param name="jobTypeName">Job type name</param>
    /// <returns>Task</returns>
    public async Task ExecuteAsync(string jobTypeName)
    {
        await ExecuteAsync(jobTypeName, null, null);
    }

    /// <summary>
    /// Execute a job by type name with parameters
    /// </summary>
    /// <param name="jobTypeName">Job type name</param>
    /// <param name="parameters">Job parameters</param>
    /// <returns>Task</returns>
    public async Task ExecuteAsync(string jobTypeName, object? parameters)
    {
        await ExecuteAsync(jobTypeName, parameters, null);
    }

    /// <summary>
    /// Execute a job by type name with parameters and job ID
    /// </summary>
    /// <param name="jobTypeName">Job type name</param>
    /// <param name="parameters">Job parameters</param>
    /// <param name="jobId">Job ID</param>
    /// <returns>Task</returns>
    public async Task ExecuteAsync(string jobTypeName, object? parameters, string? jobId)
    {
        using var scope = _services.CreateScope();

        try
        {
            // Resolve job by name
            var jobType = FindJobType(jobTypeName);
            if (jobType == null)
            {
                _logger.LogError("Job type {JobTypeName} not found", jobTypeName);
                throw new InvalidOperationException($"Job type {jobTypeName} not found");
            }

            var job = scope.ServiceProvider.GetService(jobType) as IJob;
            if (job == null)
            {
                _logger.LogError("Job {JobTypeName} could not be resolved from DI container", jobTypeName);
                throw new InvalidOperationException($"Job {jobTypeName} could not be resolved from DI container");
            }

            // Create job context
            var context = new JobContext
            {
                JobId = jobId ?? Guid.NewGuid().ToString(),
                JobName = jobTypeName,
                Parameters = ConvertParametersToDictionary(parameters),
                Services = scope.ServiceProvider,
                CancellationToken = CancellationToken.None
            };

            // Execute job
            _logger.LogInformation("Executing job {JobName} with ID {JobId}", context.JobName, context.JobId);

            var result = await job.ExecuteAsync(context);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Job {JobName} completed successfully in {Duration}ms",
                    context.JobName, result.Duration.TotalMilliseconds);

                if (result.Output != null)
                {
                    _logger.LogDebug("Job {JobName} output: {@Output}", context.JobName, result.Output);
                }
            }
            else
            {
                _logger.LogError("Job {JobName} failed: {ErrorMessage}",
                    context.JobName, result.ErrorMessage);

                if (result.Exception != null)
                {
                    _logger.LogError(result.Exception, "Job {JobName} exception details", context.JobName);
                }

                // Audit fix: previously this method only logged and returned success-shaped.
                // Hangfire then marked the job Succeeded and AutomaticRetry never fired.
                // Re-throw so Hangfire sees the failure, applies retry policy, and the
                // monitor/dashboard counters reflect reality.
                throw new InvalidOperationException(
                    $"Job '{context.JobName}' reported failure: {result.ErrorMessage}",
                    result.Exception);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute job {JobTypeName}", jobTypeName);
            throw;
        }
    }

    // Process-wide cache: every job invocation previously scanned every loaded assembly
    // via reflection (an O(N×types) hot path on each execute). Cache resolved types so
    // subsequent runs are O(1).
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Type?> JobTypeCache =
        new(StringComparer.Ordinal);

    /// <summary>
    /// Find job type by simple name. Cached after the first successful resolution.
    /// </summary>
    private Type? FindJobType(string jobTypeName)
    {
        return JobTypeCache.GetOrAdd(jobTypeName, name =>
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException)
                {
                    continue;
                }

                var type = types.FirstOrDefault(t => t.Name == name && typeof(IJob).IsAssignableFrom(t));
                if (type != null) return type;
            }
            return null;
        });
    }

    /// <summary>
    /// Convert parameters object to dictionary
    /// </summary>
    /// <param name="parameters">Parameters object</param>
    /// <returns>Parameters dictionary</returns>
    private Dictionary<string, object> ConvertParametersToDictionary(object? parameters)
    {
        if (parameters == null)
        {
            return new Dictionary<string, object>();
        }

        if (parameters is Dictionary<string, object> dict)
        {
            return dict;
        }

        // Convert using reflection
        var result = new Dictionary<string, object>();
        var properties = parameters.GetType().GetProperties();

        foreach (var property in properties)
        {
            if (property.CanRead)
            {
                var value = property.GetValue(parameters);
                if (value != null)
                {
                    result[property.Name] = value;
                }
            }
        }

        return result;
    }
}