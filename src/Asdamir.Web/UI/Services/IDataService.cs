// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.Dtos;

namespace Asdamir.Web.UI.Services
{
    /// <summary>
    /// Read-side abstraction the Blazor UI depends on to fetch management and monitoring data (jobs,
    /// localization resources, users, menus, outbox, error logs) from the API over HTTP. Implementations
    /// are expected to be fault-tolerant — a failed call yields an empty collection or a default instance
    /// rather than throwing — so consumers can bind the result straight to the view.
    /// </summary>
    public interface IDataService
    {
        /// <summary>Gets the current Hangfire background jobs from the API.</summary>
        /// <returns>The jobs, or an empty list if the request fails.</returns>
        Task<List<JobDto>> GetJobsAsync();

        /// <summary>Gets aggregate Hangfire job statistics (counts by state) from the API.</summary>
        /// <returns>The statistics, or an empty <see cref="JobStatistics"/> if the request fails.</returns>
        Task<JobStatistics> GetJobStatisticsAsync();

        /// <summary>Gets the configured recurring (scheduled) Hangfire jobs from the API.</summary>
        /// <returns>The recurring jobs, or an empty list if the request fails.</returns>
        Task<List<RecurringJobDto>> GetRecurringJobsAsync();

        /// <summary>Gets localization resource entries from the API, optionally filtered to one culture.</summary>
        /// <param name="culture">Culture to filter by (e.g. <c>tr-TR</c>); when null or empty, all cultures are returned.</param>
        /// <returns>The resources, or an empty list if the request fails.</returns>
        Task<List<ResourceDto>> GetResourcesAsync(string? culture = null);

        /// <summary>Gets the users from the API.</summary>
        /// <returns>The users, or an empty list if the request fails.</returns>
        Task<List<UserDto>> GetUsersAsync();

        /// <summary>Gets the navigation menu items from the API.</summary>
        /// <returns>The menu items, or an empty list if the request fails.</returns>
        Task<List<MenuDto>> GetMenuItemsAsync();

        /// <summary>Gets the dead-lettered outbox messages (failed mail/SMS deliveries) from the API.</summary>
        /// <returns>The dead outbox items, or an empty list if the request fails.</returns>
        Task<List<OutboxDto>> GetOutboxItemsAsync();

        /// <summary>Gets the error-dashboard overview (aggregate error statistics) from the API.</summary>
        /// <returns>The overview, or an empty <see cref="ErrorDashboardOverviewResponse"/> if the request fails.</returns>
        Task<ErrorDashboardOverviewResponse> GetErrorStatisticsAsync();

        /// <summary>Queries the error log with the supplied filter and projects each hit to an <see cref="ErrorLogDto"/>.</summary>
        /// <param name="request">The filter (level, source, paging, …) posted to the query endpoint.</param>
        /// <returns>The matching error-log entries, or an empty list if the request fails.</returns>
        Task<List<ErrorLogDto>> GetErrorLogsAsync(ErrorQueryRequest request);
    }
}
