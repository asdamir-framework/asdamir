// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;

namespace Asdamir.Web.Security.Services;

/// <summary>
/// Implementation of authorization token service
/// </summary>
public class AuthorizationTokenService : IAuthorizationTokenService
{
    private readonly AuthState _authState;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AuthorizationTokenService> _logger;

    // Instance-level lock (NOT static): in v1 a static SemaphoreSlim serialised refresh across
    // every user/circuit in the process. Whenever user A refreshed, user B blocked up to 10s.
    // AuthorizationTokenService is registered scoped (per circuit), so an instance lock is the
    // right granularity — it still prevents concurrent refresh requests from the same circuit.
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationTokenService"/> class.
    /// </summary>
    /// <param name="authState">The per-circuit authentication state holding the current tokens.</param>
    /// <param name="httpClientFactory">Factory for the unauthenticated gateway client used to call the refresh endpoint.</param>
    /// <param name="logger">Logger for token lifecycle diagnostics.</param>
    public AuthorizationTokenService(
        AuthState authState,
        IHttpClientFactory httpClientFactory,
        ILogger<AuthorizationTokenService> logger)
    {
        _authState = authState;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> IsTokenExpiredOrExpiringAsync(int expirationThresholdMinutes = 5)
    {
        try
        {
            var expiration = await GetTokenExpirationAsync();
            if (!expiration.HasValue)
            {
                _logger.LogWarning("Token expiration time not found");
                return true;
            }

            var threshold = DateTime.UtcNow.AddMinutes(expirationThresholdMinutes);
            var isExpiring = expiration.Value <= threshold;

            if (isExpiring)
            {
                _logger.LogInformation("Token is expiring - Expires at: {Expiration}, Current: {Current}, Threshold: {Threshold}",
                    expiration.Value, DateTime.UtcNow, threshold);
            }

            return isExpiring;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking token expiration");
            return true; // Assume expired on error
        }
    }

    /// <inheritdoc/>
    public async Task<bool> TryRefreshTokenAsync()
    {
        // Prevent multiple simultaneous refresh attempts
        if (!await _refreshLock.WaitAsync(TimeSpan.FromSeconds(10)))
        {
            _logger.LogWarning("Token refresh already in progress, skipping");
            return false;
        }

        try
        {
            // Get refresh token from AuthState property
            var refreshToken = _authState.RefreshToken;
            if (string.IsNullOrEmpty(refreshToken))
            {
                _logger.LogWarning("No refresh token available");
                return false;
            }

            _logger.LogInformation("Attempting to refresh access token");

            var httpClient = _httpClientFactory.CreateClient("gatewayNoAuth");
            var response = await httpClient.PostAsJsonAsync("gateway/auth/refresh", new
            {
                RefreshToken = refreshToken
            });

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Token refresh failed - Status: {Status}", response.StatusCode);
                
                // If refresh fails with 401, clear session (refresh token invalid)
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning("Refresh token invalid, clearing session");
                    await _authState.ClearAsync();
                }
                
                return false;
            }

            var newTokens = await response.Content.ReadFromJsonAsync<Asdamir.Core.Dtos.TokenResponseDto>();
            if (newTokens == null)
            {
                _logger.LogError("Failed to parse token response");
                return false;
            }

            await _authState.SetTokensAsync(newTokens);
            _logger.LogInformation("Token refreshed successfully - New expiration: {Expiration}", 
                await GetTokenExpirationAsync());

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return false;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task<DateTime?> GetTokenExpirationAsync()
    {
        try
        {
            var token = await _authState.GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                return null;
            }

            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token))
            {
                _logger.LogWarning("Invalid JWT token format");
                return null;
            }

            var jwtToken = handler.ReadJwtToken(token);
            return jwtToken.ValidTo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading token expiration");
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsStructurallyValidAsync()
    {
        // Client-side sanity check only — see interface XML doc.
        try
        {
            var token = await _authState.GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token))
            {
                _logger.LogWarning("JWT format invalid (cannot parse)");
                return false;
            }

            var jwtToken = handler.ReadJwtToken(token);

            if (jwtToken.ValidTo <= DateTime.UtcNow)
            {
                _logger.LogWarning("Token has expired (client-side exp claim)");
                return false;
            }

            if (!jwtToken.Claims.Any())
            {
                _logger.LogWarning("Token has no claims");
                return false;
            }

            var userIdClaim = jwtToken.Claims.FirstOrDefault(c =>
                c.Type == "sub" || c.Type == "nameid" || c.Type == "userId");

            if (userIdClaim == null)
            {
                _logger.LogWarning("Token missing user identifier claim");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing structural validation on token");
            return false;
        }
    }
}
