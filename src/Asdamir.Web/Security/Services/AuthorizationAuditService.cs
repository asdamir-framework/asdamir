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
using Asdamir.Core.MultiTenancy;
using Asdamir.Web.Security.Models;
using System.Collections.Concurrent;
using Asdamir.Core.Models;

namespace Asdamir.Web.Security.Services;

/// <summary>
/// Authorization audit service that logs to AuditLog table via IAuditService.
///
/// Audit fix (CRITICAL): v1 used a process-wide <c>static ConcurrentBag</c> of audit
/// events that any caller could enumerate via <see cref="GetRecentFailuresAsync"/>
/// — cross-tenant disclosure. <see cref="AuditEntry.TenantId"/> was hardcoded to
/// <c>"1"</c>, destroying multi-tenant audit isolation. v2 changes:
///  - The in-memory recent-failures cache is now instance-scoped (per DI scope), so
///    different request scopes don't see each other's denials.
///  - <c>TenantId</c> is resolved from <see cref="ITenantContext"/> (advisory tenant
///    resolver in Asdamir.Core.MultiTenancy), falling back to "default" only when no tenant
///    is established.
///  - Trim now uses a queue-style FIFO drop instead of <c>ConcurrentBag.TryTake</c>
///    (non-deterministic eviction in v1 evicted recent events and kept old ones).
/// </summary>
public class AuthorizationAuditService : IAuthorizationAuditService
{
    private readonly ILogger<AuthorizationAuditService> _logger;
    private readonly IAuditService _auditService;
    private readonly ClientInfoService? _clientInfoService;
    private readonly ITenantContext? _tenantContext;

    // Instance-scoped FIFO queue for recent failures. Each DI scope gets its own —
    // GetRecentFailuresAsync no longer leaks cross-tenant data.
    private readonly ConcurrentQueue<AuthorizationAuditEvent> _auditEvents = new();
    private const int MaxInMemoryEvents = 10000;

    public AuthorizationAuditService(
        ILogger<AuthorizationAuditService> logger,
        IAuditService auditService,
        ClientInfoService? clientInfoService = null,
        ITenantContext? tenantContext = null)
    {
        _logger = logger;
        _auditService = auditService;
        _clientInfoService = clientInfoService;
        _tenantContext = tenantContext;
    }

    public async Task LogAuthorizationAttemptAsync(AuthorizationAuditEvent auditEvent)
    {
        _logger.LogDebug("[AuthorizationAuditService] LogAuthorizationAttemptAsync called - Route: {Route}, UserId: {UserId}, IsAuthorized: {IsAuthorized}", 
            auditEvent.Route, auditEvent.UserId, auditEvent.IsAuthorized);
        
        try
        {
            // FIFO enqueue + bounded drop. v1 used ConcurrentBag.TryTake which removed
            // arbitrary items, evicting RECENT events while keeping old ones. Queue
            // dequeue is deterministic oldest-first.
            _auditEvents.Enqueue(auditEvent);
            while (_auditEvents.Count > MaxInMemoryEvents && _auditEvents.TryDequeue(out _))
            {
                // drop oldest
            }

            // Build permission/role strings
            var permissions = auditEvent.RequiredPermission ?? 
                string.Join(",", auditEvent.RequiredPermissions ?? Array.Empty<string>());
            var roles = auditEvent.RequiredRole ?? 
                string.Join(",", auditEvent.RequiredRoles ?? Array.Empty<string>());

            // Log to Serilog (Console and File)
            // Successful authorizations logged at Debug level to reduce noise in production
            if (auditEvent.IsAuthorized)
            {
                _logger.LogDebug(
                    "Authorization SUCCESS - User: {UserId} ({Email}), Route: {Route}, Permission: {Permission}, Role: {Role}, IP: {IP}",
                    auditEvent.UserId,
                    auditEvent.UserEmail,
                    auditEvent.Route,
                    permissions,
                    roles,
                    auditEvent.IpAddress);
            }
            else
            {
                _logger.LogWarning(
                    "Authorization DENIED - User: {UserId} ({Email}), Route: {Route}, Reason: {Reason}, Permission: {Permission}, Role: {Role}, IP: {IP}",
                    auditEvent.UserId,
                    auditEvent.UserEmail,
                    auditEvent.Route,
                    auditEvent.DenialReason,
                    permissions,
                    roles,
                    auditEvent.IpAddress);
            }

            // Write to AuditLog table via IAuditService
            // ONLY log DENIED attempts - successful grants create too much noise and duplicate Login logs
            // Login/Logout are logged separately in AuthController
            if (!auditEvent.IsAuthorized)
            {
                var ipAddress = _clientInfoService?.IpAddress ?? auditEvent.IpAddress ?? "Unknown";
                var userAgent = _clientInfoService?.UserAgent ?? "Blazor Server";
                
                // Extract entity name from route (e.g., "/dashboard" -> "Dashboard", "/workorders/list" -> "WorkOrders")
                var entityName = ExtractEntityFromRoute(auditEvent.Route);
                
                var auditEntry = new AuditEntry
                {
                    Timestamp = auditEvent.Timestamp, // Use the original event timestamp, not DateTime.UtcNow
                    Action = "Denied",
                    Entity = entityName,
                    EntityId = auditEvent.Route,
                    UserId = auditEvent.UserId,
                    UserName = auditEvent.UserEmail,
                    // Audit fix: previously hardcoded "1". Resolve from the request's tenant
                    // context; fall back to "default" only when no tenant is established
                    // (background tasks, system audits).
                    TenantId = _tenantContext?.TenantId ?? "default",
                    Ip = ipAddress,
                    UserAgent = userAgent,
                    ExtraJson = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        Route = auditEvent.Route,
                        IsAuthorized = false,
                        DenialReason = auditEvent.DenialReason,
                        RequiredPermission = permissions,
                        RequiredRole = roles,
                        IpAddress = ipAddress,
                        UserAgent = userAgent,
                        Timestamp = auditEvent.Timestamp
                    })
                };

                _logger.LogDebug("[AuthorizationAuditService] Logging DENIED access to database - Entity: {Entity}, Route: {Route}, UserId: {UserId}", 
                    auditEntry.Entity, auditEvent.Route, auditEvent.UserId);
                
                await _auditService.LogAsync(auditEntry);
                
                _logger.LogDebug("[AuthorizationAuditService] Successfully logged DENIED attempt - IP: {IP}, Route: {Route}", 
                    ipAddress, auditEvent.Route);
            }
            else
            {
                // Successful authorizations are NOT logged to database to avoid:
                // 1. Duplicate Login entries (Login is logged in AuthController)
                // 2. Excessive audit logs for every page navigation
                // 3. Confusion about which page user actually visited (middleware fires multiple times)
                _logger.LogDebug("[AuthorizationAuditService] Skipping database log for GRANTED access to {Route} (logged to file only)", 
                    auditEvent.Route);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AuthorizationAuditService] Error logging authorization audit event to database");
        }
    }

    public Task<List<AuthorizationAuditEvent>> GetRecentFailuresAsync(string userId, int minutes = 15)
    {
        try
        {
            var cutoffTime = DateTime.UtcNow.AddMinutes(-minutes);
            var failures = _auditEvents
                .Where(e => e.UserId == userId
                    && !e.IsAuthorized
                    && e.Timestamp >= cutoffTime)
                .OrderByDescending(e => e.Timestamp)
                .ToList();

            return Task.FromResult(failures);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent authorization failures");
            return Task.FromResult(new List<AuthorizationAuditEvent>());
        }
    }

    public Task<int> GetFailureCountAsync(string userId, int minutes = 15)
    {
        try
        {
            var cutoffTime = DateTime.UtcNow.AddMinutes(-minutes);
            var count = _auditEvents
                .Count(e => e.UserId == userId
                    && !e.IsAuthorized
                    && e.Timestamp >= cutoffTime);

            return Task.FromResult(count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting authorization failure count");
            return Task.FromResult(0);
        }
    }
    
    private string ExtractEntityFromRoute(string route)
    {
        if (string.IsNullOrWhiteSpace(route))
            return "Unknown";
        
        // Parse URL if full URL is provided (e.g., "http://localhost:5000/dashboard")
        if (route.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
            route.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(route);
                route = uri.AbsolutePath; // Extract path from URL
            }
            catch (Exception ex)
            {
                // If URL parsing fails, try to extract path manually
                _logger.LogDebug(ex, "[AuthorizationAuditService] Uri parsing failed for route '{Route}', extracting path manually", route);
                var pathStart = route.IndexOf('/', route.IndexOf("://") + 3);
                if (pathStart > 0)
                    route = route.Substring(pathStart);
            }
        }
        
        // Remove leading slash and query parameters
        route = route.TrimStart('/').Split('?')[0];
        
        // Extract first segment of route
        var segments = route.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
            return "Dashboard"; // Root route
        
        var entity = segments[0];
        
        // Convert to PascalCase. Invariant culture to avoid Turkish-i bug (ToLower("İ")
        // produces a different char in tr-TR, then permission lookups mismatch).
        return System.Globalization.CultureInfo.InvariantCulture.TextInfo
            .ToTitleCase(entity.ToLowerInvariant());
    }
}
