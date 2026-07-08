// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Hangfire;
using Hangfire.States;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Asdamir.Data.HangfireJobs;

/// <summary>
/// Hangfire-based job scheduler implementation. Honours the per-app queue + recurring-id prefix
/// from <see cref="HangfireJobsOptions"/> (design §5.1): when an <see cref="HangfireJobsOptions.AppQueue"/>
/// is configured, enqueued / scheduled / recurring jobs are routed to that queue so apps sharing a
/// company Hangfire schema stay isolated. Empty options = Hangfire's <c>default</c> queue (today's
/// behaviour, unchanged).
/// </summary>
public class HangfireJobScheduler : IJobScheduler
{
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly ILogger<HangfireJobScheduler> _logger;
    private readonly string _appQueue;
    private readonly string _recurringIdPrefix;

    /// <summary>Creates the scheduler over Hangfire's background/recurring job clients.</summary>
    public HangfireJobScheduler(
        IBackgroundJobClient backgroundJobClient,
        IRecurringJobManager recurringJobManager,
        ILogger<HangfireJobScheduler> logger,
        IOptions<HangfireJobsOptions> options)
    {
        _backgroundJobClient = backgroundJobClient;
        _recurringJobManager = recurringJobManager;
        _logger = logger;
        var opt = options.Value;
        _appQueue = opt.AppQueue?.Trim() ?? string.Empty;
        _recurringIdPrefix = opt.RecurringJobIdPrefix?.Trim() ?? string.Empty;
    }

    private bool HasQueue => _appQueue.Length > 0;

    /// <summary>Applies the recurring-id prefix when configured: <c>{prefix}:{jobId}</c>.</summary>
    private string PrefixedId(string jobId)
        => _recurringIdPrefix.Length > 0 ? $"{_recurringIdPrefix}:{jobId}" : jobId;

    /// <summary>
    /// Enqueue a job for immediate execution
    /// </summary>
    /// <typeparam name="T">Job type</typeparam>
    /// <returns>Job ID</returns>
    public Task<string> EnqueueAsync<T>() where T : class, IJob
    {
        return EnqueueAsync<T>(null);
    }

    /// <summary>
    /// Enqueue a job with parameters for immediate execution
    /// </summary>
    /// <typeparam name="T">Job type</typeparam>
    /// <param name="parameters">Job parameters</param>
    /// <returns>Job ID</returns>
    public Task<string> EnqueueAsync<T>(object? parameters) where T : class, IJob
    {
        try
        {
            var jobTypeName = typeof(T).Name;
            // Route to the app queue when configured (EnqueuedState(queue)), else Hangfire's default.
            var jobId = HasQueue
                ? _backgroundJobClient.Create<JobExecutor>(
                    x => x.ExecuteAsync(jobTypeName, parameters), new EnqueuedState(_appQueue))
                : _backgroundJobClient.Enqueue<JobExecutor>(
                    x => x.ExecuteAsync(jobTypeName, parameters));

            _logger.LogInformation("Job {JobType} enqueued with ID {JobId} (queue {Queue})",
                jobTypeName, jobId, HasQueue ? _appQueue : "default");
            return Task.FromResult(jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue job {JobType}", typeof(T).Name);
            throw;
        }
    }

    /// <summary>
    /// Schedule a job to be executed after a delay
    /// </summary>
    /// <typeparam name="T">Job type</typeparam>
    /// <param name="delay">Delay before execution</param>
    /// <returns>Job ID</returns>
    public Task<string> ScheduleAsync<T>(TimeSpan delay) where T : class, IJob
    {
        try
        {
            var jobTypeName = typeof(T).Name;
            // Note: ScheduledState carries no queue in Hangfire 1.8 — a delayed job picks its queue
            // only when it transitions to Enqueued. Per-app isolation is delivered via the Enqueue
            // and Recurring paths (both queue-routed); delayed Schedule stays on the default queue.
            var jobId = _backgroundJobClient.Schedule<JobExecutor>(
                x => x.ExecuteAsync(jobTypeName), delay);

            _logger.LogInformation("Job {JobType} scheduled with delay {Delay}, ID {JobId}",
                jobTypeName, delay, jobId);
            return Task.FromResult(jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule job {JobType} with delay {Delay}", typeof(T).Name, delay);
            throw;
        }
    }

    /// <summary>
    /// Schedule a job to be executed at a specific time
    /// </summary>
    /// <typeparam name="T">Job type</typeparam>
    /// <param name="executeAt">Execution time</param>
    /// <returns>Job ID</returns>
    public Task<string> ScheduleAsync<T>(DateTimeOffset executeAt) where T : class, IJob
    {
        try
        {
            var jobTypeName = typeof(T).Name;
            var jobId = _backgroundJobClient.Schedule<JobExecutor>(
                x => x.ExecuteAsync(jobTypeName), executeAt);

            _logger.LogInformation("Job {JobType} scheduled for {ExecuteAt}, ID {JobId}", 
                jobTypeName, executeAt, jobId);
            return Task.FromResult(jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to schedule job {JobType} for {ExecuteAt}", typeof(T).Name, executeAt);
            throw;
        }
    }

    /// <summary>
    /// Create a recurring job with cron expression
    /// </summary>
    /// <typeparam name="T">Job type</typeparam>
    /// <param name="cronExpression">Cron expression</param>
    /// <returns>Job ID</returns>
    public Task<string> RecurringAsync<T>(string cronExpression) where T : class, IJob
    {
        var jobId = typeof(T).Name;
        return RecurringAsync<T>(jobId, cronExpression);
    }

    /// <summary>
    /// Create a recurring job with custom ID and cron expression
    /// </summary>
    /// <typeparam name="T">Job type</typeparam>
    /// <param name="jobId">Custom job ID</param>
    /// <param name="cronExpression">Cron expression</param>
    /// <returns>Job ID</returns>
    public Task<string> RecurringAsync<T>(string jobId, string cronExpression) where T : class, IJob
    {
        // Audit fix: default to UTC. Local time + DST + mixed-zone cluster nodes leads to
        // drift / double-fire. Callers that need a specific zone use the 3-arg overload.
        return RecurringAsync<T>(jobId, cronExpression, TimeZoneInfo.Utc);
    }

    /// <summary>
    /// Create a recurring job with cron expression and timezone
    /// </summary>
    /// <typeparam name="T">Job type</typeparam>
    /// <param name="jobId">Job ID</param>
    /// <param name="cronExpression">Cron expression</param>
    /// <param name="timeZone">Timezone</param>
    /// <returns>Job ID</returns>
    public Task<string> RecurringAsync<T>(string jobId, string cronExpression, TimeZoneInfo timeZone) where T : class, IJob
    {
        try
        {
            var jobTypeName = typeof(T).Name;

            // Hangfire serializes a method reference; at execution time it asks the
            // job activator to resolve the target. Pointing at JobExecutor (already
            // used by EnqueueAsync) lets us share one DI-resolvable executor for
            // both enqueued and recurring jobs. v1 fix: previously pointed at a
            // throw-NotImplementedException stub method on this scheduler.
            // Prefix the id so apps sharing the company Hangfire schema don't collide on a common
            // recurring name; route to the app queue when configured (design §5.1). Hangfire 1.8
            // takes the queue as an explicit AddOrUpdate parameter (RecurringJobOptions.QueueName
            // is obsolete).
            var effectiveId = PrefixedId(jobId);
            var recurringOptions = new RecurringJobOptions { TimeZone = timeZone };

            if (HasQueue)
            {
                _recurringJobManager.AddOrUpdate<JobExecutor>(
                    effectiveId,
                    _appQueue,
                    x => x.ExecuteAsync(jobTypeName, null),
                    cronExpression,
                    recurringOptions);
            }
            else
            {
                _recurringJobManager.AddOrUpdate<JobExecutor>(
                    effectiveId,
                    x => x.ExecuteAsync(jobTypeName, null),
                    cronExpression,
                    recurringOptions);
            }

            _logger.LogInformation("Recurring job {JobId} for {JobType} created with cron {Cron}, timezone {TimeZone}, queue {Queue}",
                effectiveId, jobTypeName, cronExpression, timeZone.Id, HasQueue ? _appQueue : "default");

            return Task.FromResult(effectiveId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create recurring job {JobId} for {JobType}", jobId, typeof(T).Name);
            throw;
        }
    }

    /// <summary>
    /// Cancel a job by its ID
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <returns>True if cancelled successfully</returns>
    public Task<bool> CancelAsync(string jobId)
    {
        try
        {
            var result = _backgroundJobClient.Delete(jobId);
            _logger.LogInformation("Job {JobId} cancellation result: {Result}", jobId, result);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel job {JobId}", jobId);
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Remove a recurring job
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <returns>True if removed successfully</returns>
    public Task<bool> RemoveRecurringAsync(string jobId)
    {
        try
        {
            _recurringJobManager.RemoveIfExists(jobId);
            _logger.LogInformation("Recurring job {JobId} removed successfully", jobId);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove recurring job {JobId}", jobId);
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Get job status by ID
    /// </summary>
    /// <param name="jobId">Job ID</param>
    /// <returns>Job status</returns>
    public Task<JobStatus> GetStatusAsync(string jobId)
    {
        try
        {
            using var connection = JobStorage.Current.GetConnection();
            var jobData = connection.GetJobData(jobId);
            
            if (jobData == null)
            {
                return Task.FromResult(JobStatus.Unknown);
            }

            var status = jobData.State switch
            {
                "Enqueued" => JobStatus.Enqueued,
                "Processing" => JobStatus.Processing,
                "Succeeded" => JobStatus.Succeeded,
                "Failed" => JobStatus.Failed,
                "Deleted" => JobStatus.Deleted,
                "Scheduled" => JobStatus.Scheduled,
                _ => JobStatus.Unknown
            };

            return Task.FromResult(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get status for job {JobId}", jobId);
            return Task.FromResult(JobStatus.Unknown);
        }
    }

    // Recurring jobs delegate to JobExecutor (see RecurringAsync above). The
    // previously stubbed ExecuteRecurringJob method on this class has been
    // removed — it was a NotImplementedException landmine.
}
