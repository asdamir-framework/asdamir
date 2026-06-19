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
/// Represents the result of a job execution
/// </summary>
public class JobResult
{
    /// <summary>
    /// Gets or sets whether the job executed successfully
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Gets or sets the error message if the job failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the exception that caused the job to fail
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets or sets the execution duration
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Gets or sets additional metadata about the job execution
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the job output data
    /// </summary>
    public object? Output { get; set; }

    /// <summary>
    /// Creates a successful job result
    /// </summary>
    /// <param name="duration">Execution duration</param>
    /// <param name="output">Job output</param>
    /// <returns>Success result</returns>
    public static JobResult Success(TimeSpan duration, object? output = null)
    {
        return new JobResult
        {
            IsSuccess = true,
            Duration = duration,
            Output = output
        };
    }

    /// <summary>
    /// Creates a failed job result
    /// </summary>
    /// <param name="errorMessage">Error message</param>
    /// <param name="exception">Exception that caused the failure</param>
    /// <param name="duration">Execution duration</param>
    /// <returns>Failure result</returns>
    public static JobResult Failure(string errorMessage, Exception? exception = null, TimeSpan duration = default)
    {
        return new JobResult
        {
            IsSuccess = false,
            ErrorMessage = errorMessage,
            Exception = exception,
            Duration = duration
        };
    }

    /// <summary>
    /// Creates a failed job result from an exception
    /// </summary>
    /// <param name="exception">Exception that caused the failure</param>
    /// <param name="duration">Execution duration</param>
    /// <returns>Failure result</returns>
    public static JobResult Failure(Exception exception, TimeSpan duration = default)
    {
        return new JobResult
        {
            IsSuccess = false,
            ErrorMessage = exception.Message,
            Exception = exception,
            Duration = duration
        };
    }
}

/// <summary>
/// Represents the status of a job
/// </summary>
public enum JobStatus
{
    /// <summary>
    /// Job is enqueued and waiting to be processed
    /// </summary>
    Enqueued,

    /// <summary>
    /// Job is currently being processed
    /// </summary>
    Processing,

    /// <summary>
    /// Job completed successfully
    /// </summary>
    Succeeded,

    /// <summary>
    /// Job failed
    /// </summary>
    Failed,

    /// <summary>
    /// Job was deleted
    /// </summary>
    Deleted,

    /// <summary>
    /// Job is scheduled for future execution
    /// </summary>
    Scheduled,

    /// <summary>
    /// Job status is unknown
    /// </summary>
    Unknown
}

/// <summary>
/// Job schedule configuration
/// </summary>
public class JobSchedule
{
    /// <summary>
    /// Gets or sets the cron expression for recurring jobs
    /// </summary>
    public string? CronExpression { get; set; }

    /// <summary>
    /// Gets or sets the timezone for the schedule
    /// </summary>
    public TimeZoneInfo? TimeZone { get; set; }

    /// <summary>
    /// Gets or sets the delay for scheduled jobs
    /// </summary>
    public TimeSpan? Delay { get; set; }

    /// <summary>
    /// Gets or sets the execution time for scheduled jobs
    /// </summary>
    public DateTimeOffset? ExecuteAt { get; set; }

    /// <summary>
    /// Gets or sets whether this is a recurring job
    /// </summary>
    public bool IsRecurring => !string.IsNullOrEmpty(CronExpression);

    /// <summary>
    /// Gets or sets whether this is a delayed job
    /// </summary>
    public bool IsDelayed => Delay.HasValue || ExecuteAt.HasValue;

    /// <summary>
    /// Creates a recurring schedule
    /// </summary>
    /// <param name="cronExpression">Cron expression</param>
    /// <param name="timeZone">Timezone (optional)</param>
    /// <returns>Recurring schedule</returns>
    public static JobSchedule Recurring(string cronExpression, TimeZoneInfo? timeZone = null)
    {
        return new JobSchedule
        {
            CronExpression = cronExpression,
            TimeZone = timeZone
        };
    }

    /// <summary>
    /// Creates a delayed schedule
    /// </summary>
    /// <param name="delay">Delay</param>
    /// <returns>Delayed schedule</returns>
    public static JobSchedule Delayed(TimeSpan delay)
    {
        return new JobSchedule
        {
            Delay = delay
        };
    }

    /// <summary>
    /// Creates a scheduled execution
    /// </summary>
    /// <param name="executeAt">Execution time</param>
    /// <returns>Scheduled execution</returns>
    public static JobSchedule At(DateTimeOffset executeAt)
    {
        return new JobSchedule
        {
            ExecuteAt = executeAt
        };
    }
}

/// <summary>
/// Job statistics information
/// </summary>
public class JobStatistics
{
    /// <summary>
    /// Gets or sets the number of enqueued jobs
    /// </summary>
    public long Enqueued { get; set; }

    /// <summary>
    /// Gets or sets the number of failed jobs
    /// </summary>
    public long Failed { get; set; }

    /// <summary>
    /// Gets or sets the number of processing jobs
    /// </summary>
    public long Processing { get; set; }

    /// <summary>
    /// Gets or sets the number of scheduled jobs
    /// </summary>
    public long Scheduled { get; set; }

    /// <summary>
    /// Gets or sets the number of succeeded jobs
    /// </summary>
    public long Succeeded { get; set; }

    /// <summary>
    /// Gets or sets the number of deleted jobs
    /// </summary>
    public long Deleted { get; set; }

    /// <summary>
    /// Gets or sets the number of recurring jobs
    /// </summary>
    public long Recurring { get; set; }

    /// <summary>
    /// Gets or sets the total number of servers
    /// </summary>
    public int Servers { get; set; }

    /// <summary>
    /// Gets the total number of jobs
    /// </summary>
    public long Total => Enqueued + Failed + Processing + Scheduled + Succeeded + Deleted;
}

/// <summary>
/// Job information
/// </summary>
public class JobInfo
{
    /// <summary>
    /// Gets or sets the job ID
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the job name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the job status
    /// </summary>
    public JobStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the job creation time
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the job execution start time
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the job completion time
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the job error message
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the job execution duration
    /// </summary>
    public TimeSpan? Duration => CompletedAt.HasValue && StartedAt.HasValue 
        ? CompletedAt.Value - StartedAt.Value 
        : null;

    /// <summary>
    /// Gets or sets the retry count
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Gets or sets the job arguments
    /// </summary>
    public Dictionary<string, object> Arguments { get; set; } = new();
}