// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Http;
using Asdamir.Web.Security.Http;

namespace Asdamir.Web.Security.Services;

/// <summary>
/// Service to capture and store client information (IP address and User Agent)
/// for the current Blazor Server circuit.
/// </summary>
public class ClientInfoService
{
    /// <summary>The client IP address captured for the circuit (IPv6 loopback normalized to <c>127.0.0.1</c>); <c>null</c>/"Unknown" if unavailable.</summary>
    public string? IpAddress { get; set; }
    /// <summary>The client <c>User-Agent</c> header captured for the circuit; <c>null</c>/"Unknown" if unavailable.</summary>
    public string? UserAgent { get; set; }
}

/// <summary>
/// Circuit handler that captures client information when a Blazor Server circuit is created.
/// This is the proper way to capture HttpContext information in Blazor Server.
/// </summary>
public class ClientInfoCircuitHandler : CircuitHandler
{
    private readonly ILogger<ClientInfoCircuitHandler> _logger;
    private readonly ClientInfoService _clientInfoService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>Initializes the circuit handler with its logger, the per-circuit client-info holder, and the HTTP context accessor.</summary>
    /// <param name="logger">Logger for circuit lifecycle and client-info capture.</param>
    /// <param name="clientInfoService">Scoped holder populated with the captured IP/User-Agent.</param>
    /// <param name="httpContextAccessor">Accessor used to read the originating HTTP request during circuit setup.</param>
    public ClientInfoCircuitHandler(
        ILogger<ClientInfoCircuitHandler> logger,
        ClientInfoService clientInfoService,
        IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _clientInfoService = clientInfoService;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>Captures the client IP/User-Agent from the HTTP context and publishes the circuit id to downstream async flows when the connection comes up.</summary>
    /// <param name="circuit">The circuit that connected.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>A task that completes when capture and base handling finish.</returns>
    public override Task OnConnectionUpAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        try
        {
            var circuitId = circuit.Id;

            // Make CircuitId available to downstream async flows (including HttpClient handlers)
            CircuitExecutionContext.CurrentCircuitId = circuitId;
            
            // Register circuit context for HttpClient handlers
            var context = new CircuitContext { CircuitId = circuitId };
            CircuitServicesAccessor.RegisterCircuit(circuitId, context);
            
            var httpContext = _httpContextAccessor.HttpContext;
            
            if (httpContext != null)
            {
                // CRITICAL: Store CircuitId in HttpContext.Items for reliable access across async boundaries
                httpContext.Items["CircuitId"] = circuitId;
                
                // Get IP address and convert IPv6 loopback to IPv4 format for better readability
                var remoteIp = httpContext.Connection?.RemoteIpAddress?.ToString() ?? "Unknown";
                _clientInfoService.IpAddress = remoteIp == "::1" ? "127.0.0.1" : remoteIp;
                _clientInfoService.UserAgent = httpContext.Request?.Headers["User-Agent"].FirstOrDefault() ?? "Unknown";
                
                // SECURITY: Store IP address for session validation (prevent hijacking)
                httpContext.Items["CircuitIP"] = _clientInfoService.IpAddress;
                
                _logger.LogInformation("[Asdamir.Web.Security] Circuit {CircuitId} UP - IP: {IP}", 
                    circuitId, _clientInfoService.IpAddress);
            }
            else
            {
                // HttpContext null during circuit initialization is normal - client info captured later
                _logger.LogDebug("[Asdamir.Web.Security] HttpContext not available during circuit init, will capture on first request");
                _clientInfoService.IpAddress = "Unknown";
                _clientInfoService.UserAgent = "Unknown";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Asdamir.Web.Security] Error capturing client info in circuit handler");
            _clientInfoService.IpAddress = "Unknown";
            _clientInfoService.UserAgent = "Unknown";
        }

        return base.OnConnectionUpAsync(circuit, cancellationToken);
    }

    /// <summary>Wraps inbound circuit activity so the current circuit id is set for every request, then restored afterwards — keeping it available even when the HTTP context is null.</summary>
    /// <param name="next">The next inbound-activity handler in the chain.</param>
    /// <returns>A handler that sets/restores the ambient circuit id around <paramref name="next"/>.</returns>
    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(Func<CircuitInboundActivityContext, Task> next)
    {
        // Ensure CircuitExecutionContext is set for EVERY inbound activity.
        // This makes the circuit id available to AuthState / HttpClient handlers even when HttpContext is null.
        return async context =>
        {
            var previousCircuitId = CircuitExecutionContext.CurrentCircuitId;
            CircuitExecutionContext.CurrentCircuitId = context.Circuit.Id;

            try
            {
                await next(context);
            }
            finally
            {
                CircuitExecutionContext.CurrentCircuitId = previousCircuitId;
            }
        };
    }

    /// <summary>Publishes the circuit id to the ambient execution context as early as possible in the circuit lifecycle.</summary>
    /// <param name="circuit">The circuit being opened.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>A task that completes when base handling finishes.</returns>
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        // Ensure circuit id is available as early as possible in the circuit lifecycle
        CircuitExecutionContext.CurrentCircuitId = circuit.Id;
        return base.OnCircuitOpenedAsync(circuit, cancellationToken);
    }

    /// <summary>Handles a (possibly transient, e.g. Ctrl+F5) disconnect: keeps the circuit id and defers token cleanup to the definitive CLOSED event.</summary>
    /// <param name="circuit">The circuit that disconnected.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>A task that completes when base handling finishes.</returns>
    public override async Task OnConnectionDownAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        var circuitId = circuit.Id;

        // Keep current circuit id during transient disconnects (Ctrl+F5)
        CircuitExecutionContext.CurrentCircuitId = circuitId;

        // DOWN süreci transient olabilir (Ctrl+F5 gibi). Temizlik Closed aşamasına taşındı.
        _logger.LogDebug("[Asdamir.Web.Security] Circuit {CircuitId} DOWN - tokens kept until CLOSED event", circuitId);
        
        await base.OnConnectionDownAsync(circuit, cancellationToken);
    }

    /// <summary>Handles definitive circuit close: clears the ambient circuit id and, after a short grace period, removes the circuit's cached auth tokens unless they were reused after close.</summary>
    /// <param name="circuit">The circuit that closed.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>A task that completes when base handling finishes (grace cleanup runs in the background).</returns>
    public override async Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        var circuitId = circuit.Id;
        // Circuit is definitively closed; clear execution context if it matches
        if (CircuitExecutionContext.CurrentCircuitId == circuitId)
        {
            CircuitExecutionContext.CurrentCircuitId = null;
        }

        // Graceful cleanup: delay token removal to allow late in-flight HTTP calls
        var closedAtUtc = DateTime.UtcNow;
        var grace = TimeSpan.FromSeconds(5);

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(grace, CancellationToken.None);

                lock (TokenStore.GetLockObject())
                {
                    if (TokenStore.TryGet(circuitId, out var entry))
                    {
                        if (entry.LastUsedUtc <= closedAtUtc)
                        {
                            TokenStore.RemoveToken(circuitId);
                            CircuitServicesAccessor.UnregisterCircuit(circuitId);
                            _logger.LogInformation("[Asdamir.Web.Security] Grace-cleaned circuit {CircuitId} after {Seconds}s - RemainingTokens: {Count}",
                                circuitId, grace.TotalSeconds, TokenStore.GetAllTokens().Count);
                        }
                        else
                        {
                            _logger.LogDebug("[Asdamir.Web.Security] Skipped cleanup for circuit {CircuitId} (token reused after close, LastUsedUtc={LastUsedUtc:o})",
                                circuitId, entry.LastUsedUtc);
                        }
                    }
                    else
                    {
                        CircuitServicesAccessor.UnregisterCircuit(circuitId);
                        _logger.LogDebug("[Asdamir.Web.Security] Circuit {CircuitId} CLOSED - no token to clean (after grace)", circuitId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Asdamir.Web.Security] Error during grace cleanup for circuit {CircuitId}", circuitId);
            }
        });

        await base.OnCircuitClosedAsync(circuit, cancellationToken);
    }
}
