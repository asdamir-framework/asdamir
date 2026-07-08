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
using System.Net.Http.Json;

namespace Asdamir.Web.UI.Services
{
    /// <summary>
    /// Concrete <see cref="IDataService"/> — a typed <see cref="HttpClient"/> wrapper the Blazor UI uses to
    /// read management/monitoring data from the API over HTTP (the UI never touches the database directly).
    /// Every call is resilient by design: a failed request is logged and returns an empty collection or a
    /// default instance rather than throwing, so a transient API error degrades a page instead of breaking it.
    /// </summary>
    public class DataService : IDataService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<DataService> _logger;

        /// <summary>
        /// Creates the service over the supplied API-bound <see cref="HttpClient"/>.
        /// </summary>
        /// <param name="httpClient">The client whose base address targets the app's API/Gateway.</param>
        /// <param name="logger">Logger used to record failed requests before falling back to an empty result.</param>
        public DataService(HttpClient httpClient, ILogger<DataService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        /// <inheritdoc/>
        public async Task<List<JobDto>> GetJobsAsync()
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<List<JobDto>>("frameworkapi/hangfire/jobs");
                return response ?? new List<JobDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching jobs");
                return new List<JobDto>();
            }
        }

        /// <inheritdoc/>
        public async Task<JobStatistics> GetJobStatisticsAsync()
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<JobStatistics>("frameworkapi/hangfire/statistics");
                return response ?? new JobStatistics();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching job statistics");
                return new JobStatistics();
            }
        }

        /// <inheritdoc/>
        public async Task<List<RecurringJobDto>> GetRecurringJobsAsync()
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<List<RecurringJobDto>>("frameworkapi/hangfire/recurring-jobs");
                return response ?? new List<RecurringJobDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching recurring jobs");
                return new List<RecurringJobDto>();
            }
        }

        /// <inheritdoc/>
        public async Task<List<ResourceDto>> GetResourcesAsync(string? culture = null)
        {
            try
            {
                var url = string.IsNullOrEmpty(culture) ? "frameworkapi/localization/resources" : $"frameworkapi/localization/resources?culture={culture}";
                var response = await _httpClient.GetFromJsonAsync<List<ResourceDto>>(url);
                return response ?? new List<ResourceDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching resources");
                return new List<ResourceDto>();
            }
        }

        /// <inheritdoc/>
        public async Task<List<UserDto>> GetUsersAsync()
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<List<UserDto>>("frameworkapi/users");
                return response ?? new List<UserDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching users");
                return new List<UserDto>();
            }
        }

        /// <inheritdoc/>
        public async Task<List<MenuDto>> GetMenuItemsAsync()
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<List<MenuDto>>("frameworkapi/menu");
                return response ?? new List<MenuDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching menu items");
                return new List<MenuDto>();
            }
        }

        /// <inheritdoc/>
        public async Task<List<OutboxDto>> GetOutboxItemsAsync()
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<List<OutboxDto>>("gateway/outbox/dead");
                return response ?? new List<OutboxDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching outbox items");
                return new List<OutboxDto>();
            }
        }

        /// <inheritdoc/>
        public async Task<ErrorDashboardOverviewResponse> GetErrorStatisticsAsync()
        {
            try
            {
                var response = await _httpClient.GetFromJsonAsync<ErrorDashboardOverviewResponse>("frameworkapi/errors/statistics");
                return response ?? new ErrorDashboardOverviewResponse();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching error statistics");
                return new ErrorDashboardOverviewResponse();
            }
        }

        /// <inheritdoc/>
        public async Task<List<ErrorLogDto>> GetErrorLogsAsync(ErrorQueryRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("frameworkapi/errors/query", request);
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<ErrorQueryResponse>();
                    // Convert RecentErrorResponse to ErrorLogDto
                    var errorLogs = result?.Errors?.Select(e => new ErrorLogDto
                    {
                        Id = e.Id,
                        Level = e.Level,
                        Message = e.Message,
                        Source = e.Source,
                        Timestamp = e.Timestamp,
                        IsResolved = e.IsResolved,
                        UserId = null,
                        CorrelationId = null,
                        StackTrace = null,
                        Properties = new Dictionary<string, object>()
                    }).ToList() ?? new List<ErrorLogDto>();
                    return errorLogs;
                }
                return new List<ErrorLogDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching error logs");
                return new List<ErrorLogDto>();
            }
        }
    }
}
