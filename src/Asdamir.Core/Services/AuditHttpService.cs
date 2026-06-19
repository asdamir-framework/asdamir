// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.Contracts;
using Asdamir.Core.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace Asdamir.Core.Services;

/// <summary>
/// HttpClient-based audit service implementation.
/// Sends audit entries to Gateway endpoint.
/// </summary>
public sealed class AuditHttpService : IAuditService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AuditHttpService> _logger;

    public AuditHttpService(HttpClient httpClient, ILogger<AuditHttpService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task LogAsync(AuditEntry entry, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("[AuditHttpService] LogAsync called - Action: {Action}, Entity: {Entity}, EntityId: {EntityId}, UserId: {UserId}, IP: {IP}", 
                entry.Action, entry.Entity, entry.EntityId, entry.UserId, entry.Ip);
            
            var dto = new
            {
                Timestamp = entry.Timestamp, // Use DateTimeOffset directly (matches AuditLog model)
                entry.Action,
                entry.Entity,
                entry.EntityId,
                entry.UserId,
                entry.UserName,
                entry.TenantId,
                entry.Ip,
                entry.UserAgent,
                entry.OldValuesJson,
                entry.NewValuesJson,
                entry.ExtraJson
            };

            _logger.LogInformation("[AuditHttpService] Sending POST to gateway/audit/log - BaseAddress: {BaseAddress}", 
                _httpClient.BaseAddress);

            var response = await _httpClient.PostAsJsonAsync("gateway/audit/log", dto, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("[AuditHttpService] Failed to send audit entry. Status: {StatusCode}, Error: {Error}", 
                    response.StatusCode, errorContent);
            }
            else
            {
                _logger.LogInformation("[AuditHttpService] Successfully sent audit entry to API");
            }
        }
        catch (Exception ex)
        {
            // Don't throw - audit failures should not break application flow
            _logger.LogError(ex, "[AuditHttpService] Failed to send audit entry: {Action} on {Entity}", entry.Action, entry.Entity);
        }
    }
}
