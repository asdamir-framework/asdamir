// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.Linq.Expressions;

namespace Asdamir.Core.Contracts;

/// <summary>
/// Hangfire job service interface for background job management
/// </summary>
public interface IHangfireJobService
{
    /// <summary>
    /// Enqueue a job to be executed immediately
    /// </summary>
    string Enqueue<T>(Expression<Action<T>> methodCall) where T : class;

    /// <summary>
    /// Enqueue a job with parameters to be executed immediately
    /// </summary>
    string Enqueue<T>(Expression<Func<T, Task>> methodCall) where T : class;

    /// <summary>
    /// Schedule a job to be executed after a delay
    /// </summary>
    string Schedule<T>(Expression<Action<T>> methodCall, TimeSpan delay) where T : class;

    /// <summary>
    /// Schedule a job to be executed at a specific time
    /// </summary>
    string Schedule<T>(Expression<Action<T>> methodCall, DateTimeOffset enqueueAt) where T : class;

    /// <summary>
    /// Create a recurring job with cron expression
    /// </summary>
    string Recurring<T>(string jobId, Expression<Action<T>> methodCall, string cronExpression) where T : class;

    /// <summary>
    /// Create a recurring job with cron expression and timezone
    /// </summary>
    string Recurring<T>(string jobId, Expression<Action<T>> methodCall, string cronExpression, TimeZoneInfo timeZone) where T : class;

    /// <summary>
    /// Delete a recurring job
    /// </summary>
    void RemoveRecurring(string jobId);

    /// <summary>
    /// Delete a job by its ID
    /// </summary>
    bool Delete(string jobId);

    /// <summary>
    /// Get job status by ID
    /// </summary>
    string? GetJobState(string jobId);
}

