// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Data.HangfireJobs;

/// <summary>
/// Represents a job that can be executed by the job scheduler
/// </summary>
public interface IJob
{
    /// <summary>
    /// Gets the unique name of the job
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Executes the job with the provided context
    /// </summary>
    /// <param name="context">Job execution context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Job execution result</returns>
    Task<JobResult> ExecuteAsync(IJobContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Job scheduling service interface
/// </summary>
public interface IJobScheduler
{
    /// <summary>
    /// Enqueue a job for immediate execution
    /// </summary>
    /// <typeparam name="T">Job type</typeparam>
    /// <returns>Job ID</returns>
    Task<string> EnqueueAsync<T>() where T : class, IJob;

    /// <summary>
    /// Enqueue a job with parameters for immediate execution
    /// </summary>
    /// <typeparam name="T">Job type</typeparam>
    /// <param name="parameters">Job parameters</param>
    /// <returns>Job ID</returns>
    Task<string> EnqueueAsync<T>(object parameters) where T : class, IJob;

    /// <summary>
    /// Schedule a job to be executed after a delay
    /// </summary>
    /// <typeparam name="T">Job type</typeparam>
    /// <param name="delay">Delay before execution</param>
    /// <returns>Job ID</returns>
    Task<string> ScheduleAsync<T>(TimeSpan delay) where T : class, IJob;

    /// <summary>
    /// Schedule a job to be executed at a specific time
    /// </summary>
    /// <typeparam name="T">Job type</typeparam>
    /// <param name="executeAt">Execution time</param>
    /// <returns>Job ID</returns>
    Task<string> ScheduleAsync<T>(DateTimeOffset executeAt) where T : class, IJob;

    /// <summary>
    /// Create a recurring job with cron expression
    /// </summary>
    /// <typeparam name="T">Job type</typeparam>
    /// <param name="cronExpression">Cron expression</param>
    /// <returns>Job ID</returns>
    Task<string> RecurringAsync<T>(string cronExpression) where T : class, IJob;

    /// <summary>
    /// Create a recurring job with custom ID and cron expression
    /// </summary>
    /// <typeparam name="T">Job type</typeparam>
    /// <param name="jobId">Custom job ID</param>
    /// <param name="cronExpression">Cron expression</param>
    /// <returns>Job ID</returns>
    Task<string> RecurringAsync<T>(string jobId, string cronExpression) where T : class, IJob;

    /// <summary>
    /// Create a recurring job with cron expression and timezone
    /// </summary>
    /// <typeparam name="T">Job type</typeparam>
    /// <param name="jobId">Job ID</param>
    /// <param name="cronExpression">Cron expression</param>
    /// <param name="timeZone">Timezone</param>
    /// <returns>Job ID</returns>
    Task<string> RecurringAsync<T>(string jobId, string cronExpression, TimeZoneInfo timeZone) where T : class, IJob;

    /// <summary>
    /// Cancel a job by its ID
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <returns>True if cancelled successfully</returns>
    Task<bool> CancelAsync(string jobId);

    /// <summary>
    /// Remove a recurring job
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <returns>True if removed successfully</returns>
    Task<bool> RemoveRecurringAsync(string jobId);

    /// <summary>
    /// Get job status by ID
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <returns>Job status</returns>
    Task<JobStatus> GetStatusAsync(string jobId);
}

/// <summary>
/// Job execution context interface
/// </summary>
public interface IJobContext
{
    /// <summary>
    /// Gets the unique job execution ID
    /// </summary>
    string JobId { get; }

    /// <summary>
    /// Gets the job name
    /// </summary>
    string JobName { get; }

    /// <summary>
    /// Gets the job parameters
    /// </summary>
    IDictionary<string, object> Parameters { get; }

    /// <summary>
    /// Gets the cancellation token for the job execution
    /// </summary>
    CancellationToken CancellationToken { get; }

    /// <summary>
    /// Gets the service provider for dependency injection
    /// </summary>
    IServiceProvider Services { get; }

    /// <summary>
    /// Gets a typed parameter value
    /// </summary>
    /// <typeparam name="T">Parameter type</typeparam>
    /// <param name="key">Parameter key</param>
    /// <returns>Parameter value or default</returns>
    T? GetParameter<T>(string key);

    /// <summary>
    /// Sets a parameter value
    /// </summary>
    /// <param name="key">Parameter key</param>
    /// <param name="value">Parameter value</param>
    void SetParameter(string key, object value);
}

/// <summary>
/// Job monitoring interface
/// </summary>
public interface IJobMonitor
{
    /// <summary>
    /// Get all job statistics
    /// </summary>
    /// <returns>Job statistics</returns>
    Task<JobStatistics> GetStatisticsAsync();

    /// <summary>
    /// Get failed jobs
    /// </summary>
    /// <param name="skip">Number of jobs to skip</param>
    /// <param name="take">Number of jobs to take</param>
    /// <returns>Failed jobs</returns>
    Task<IEnumerable<JobInfo>> GetFailedJobsAsync(int skip = 0, int take = 50);

    /// <summary>
    /// Get processing jobs
    /// </summary>
    /// <param name="skip">Number of jobs to skip</param>
    /// <param name="take">Number of jobs to take</param>
    /// <returns>Processing jobs</returns>
    Task<IEnumerable<JobInfo>> GetProcessingJobsAsync(int skip = 0, int take = 50);

    /// <summary>
    /// Get succeeded jobs
    /// </summary>
    /// <param name="skip">Number of jobs to skip</param>
    /// <param name="take">Number of jobs to take</param>
    /// <returns>Succeeded jobs</returns>
    Task<IEnumerable<JobInfo>> GetSucceededJobsAsync(int skip = 0, int take = 50);
}