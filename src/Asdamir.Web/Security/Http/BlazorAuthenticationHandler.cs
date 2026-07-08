// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Web.Security.Services;
using Microsoft.AspNetCore.Http;
using System.Net.Http.Headers;

namespace Asdamir.Web.Security.Http;

/// <summary>
/// Blazor-specific authentication handler that works with TokenStore
/// Uses static TokenStore to avoid scope issues with HttpClient handlers
/// Gets CircuitId from HttpContext.Items which is set during Circuit UP
/// </summary>
public class BlazorAuthenticationHandler : DelegatingHandler
{
    private readonly ILogger<BlazorAuthenticationHandler> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuthState _authState;

    /// <summary>
    /// Initializes the handler with the services needed to resolve and attach the circuit's token.
    /// </summary>
    /// <param name="logger">Logger for token-resolution diagnostics.</param>
    /// <param name="httpContextAccessor">Accessor used to read circuit id, stored IP and User-Agent.</param>
    /// <param name="authState">The current auth state, used to decide retry/logging behavior.</param>
    public BlazorAuthenticationHandler(
        ILogger<BlazorAuthenticationHandler> logger,
        IHttpContextAccessor httpContextAccessor,
        AuthState authState)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _authState = authState;
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            string? token = null;
            string? source = null;
            string? circuitId = null;
            var isAuthenticated = _authState.IsAuthenticated;
            
            // CRITICAL: Get CircuitId from AsyncLocal (survives across async calls and circuit restarts)
            circuitId = CircuitExecutionContext.CurrentCircuitId;
            
            // FALLBACK: Try HttpContext.Items (only works during initial Circuit UP)
            if (string.IsNullOrEmpty(circuitId))
            {
                var httpContext = _httpContextAccessor.HttpContext;
                if (httpContext != null && httpContext.Items.TryGetValue("CircuitId", out var circuitIdObj))
                {
                    circuitId = circuitIdObj as string;
                }
            }
            
            // SECURITY: Validate IP if we have HttpContext
            var httpContext2 = _httpContextAccessor.HttpContext;
            if (httpContext2 != null && !string.IsNullOrEmpty(circuitId))
            {
                var currentIp = httpContext2.Connection?.RemoteIpAddress?.ToString();
                if (currentIp == "::1") currentIp = "127.0.0.1";
                
                var storedIp = httpContext2.Items["CircuitIP"] as string;
                
                // Only validate if BOTH IPs are available
                if (!string.IsNullOrEmpty(storedIp) && !string.IsNullOrEmpty(currentIp) && currentIp != storedIp)
                {
                    _logger.LogWarning("[Security] IP address mismatch - Expected: {Expected}, Got: {Actual}", 
                        storedIp, currentIp);
                    // Clear token for security
                    TokenStore.RemoveToken(circuitId);
                    CircuitServicesAccessor.UnregisterCircuit(circuitId);
                    circuitId = null;
                }
            }
            
            // Get token from CircuitId-based stores with retry for token sync race condition
            if (!string.IsNullOrEmpty(circuitId))
            {
                // Try to get token up to 3 times with delays to handle token restore race condition
                const int maxAttempts = 3;
                const int delayMs = 50;
                
                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    var context = CircuitServicesAccessor.GetContext(circuitId);
                    if (context?.AccessToken != null)
                    {
                        token = context.AccessToken;
                        source = $"CircuitContext[{circuitId}]";
                        break;
                    }
                    
                    if (TokenStore.TryGet(circuitId, out var entry))
                    {
                        token = entry.AccessToken;
                        source = $"TokenStore[{circuitId}]";
                        TokenStore.Touch(circuitId);
                        break;
                    }
                    
                    // Token not found but circuit exists - likely token restore in progress
                    if (attempt < maxAttempts)
                    {
                        if (isAuthenticated)
                        {
                            _logger.LogWarning("[BlazorAuthenticationHandler] Token not found on attempt {Attempt}/{Max} for {Path}, CircuitId={CircuitId}, waiting {Delay}ms for token restore...",
                                attempt, maxAttempts, request.RequestUri?.PathAndQuery ?? "unknown", circuitId, delayMs);
                        }
                        else
                        {
                            _logger.LogInformation("[BlazorAuthenticationHandler] Token missing for signed-out user; skipping retries for {Path}, CircuitId={CircuitId}",
                                request.RequestUri?.PathAndQuery ?? "unknown", circuitId);
                            break; // Avoid noisy retries during/after logout
                        }
                        await Task.Delay(delayMs, cancellationToken);
                    }
                }
            }
            else
            {
                _logger.LogWarning("[BlazorAuthenticationHandler] No CircuitId available for request: {Path}",
                    request.RequestUri?.PathAndQuery ?? "unknown");
            }
            
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                _logger.LogDebug("[BlazorAuthenticationHandler] Token attached from: {Source}", source);
            }
            else
            {
                // CIRCUIT-AWARE GUARD: If no circuit context exists (post-logout/disposed), fail fast
                // This prevents orphaned component API calls from spamming logs after logout
                if (string.IsNullOrEmpty(circuitId))
                {
                    // No circuit = component rendering after logout or during disposal
                    // Return 401 immediately without logging (normal during logout)
                    return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized)
                    {
                        RequestMessage = request,
                        ReasonPhrase = "No active circuit"
                    };
                }

                if (isAuthenticated)
                {
                    _logger.LogWarning("[BlazorAuthenticationHandler] Authentication required for: {Path}", 
                        request.RequestUri?.PathAndQuery ?? "unknown");
                }
                else
                {
                    _logger.LogInformation("[BlazorAuthenticationHandler] Request while signed-out: {Path}, CircuitId={CircuitId}",
                        request.RequestUri?.PathAndQuery ?? "unknown", circuitId);
                }
            }

            // Forward browser User-Agent from HttpContext to outgoing request
            var httpContextForUA = _httpContextAccessor.HttpContext;
            if (httpContextForUA != null)
            {
                var userAgent = httpContextForUA.Request.Headers["User-Agent"].ToString();
                if (!string.IsNullOrEmpty(userAgent) && !request.Headers.Contains("User-Agent"))
                {
                    request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                }
            }

            // Execute request
            var response = await base.SendAsync(request, cancellationToken);
            return response;
        }
        catch (TaskCanceledException)
        {
            // Normal during hard refresh (Ctrl+F5) or circuit disconnect - silently propagate
            throw;
        }
        catch (OperationCanceledException)
        {
            // Operation cancelled - silently propagate
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BlazorAuthenticationHandler] Error in token resolution");
            throw;
        }
    }
}
