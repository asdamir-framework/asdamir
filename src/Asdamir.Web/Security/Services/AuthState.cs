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
using Microsoft.AspNetCore.Http;
using Blazored.SessionStorage;
using System.Net.Http.Json;

namespace Asdamir.Web.Security.Services;

/// <summary>
/// Simple enterprise authentication state management
/// </summary>
public sealed class AuthState
{
    private readonly ISessionStorageService _sessionStorage;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuthState>? _logger;
    private readonly SemaphoreSlim _sessionLock = new(1, 1);
    private readonly string _instanceId = Guid.NewGuid().ToString("N");
    private readonly IAuthorizationCache? _authCache;
    private string? _cachedCircuitId; // Cache circuit ID on first access
    private TaskCompletionSource<bool> _authenticationReadyTcs = new();
    private bool _isInitialized = false;

    // Authentication state
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(AccessToken);
    public bool IsInitialized => _isInitialized;
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string UserId { get; private set; } = string.Empty;
    public HashSet<string> Permissions { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

    // State change notification
    public event Action? Changed;

    public AuthState(ISessionStorageService sessionStorage, IHttpContextAccessor httpContextAccessor, ILogger<AuthState>? logger = null, IAuthorizationCache? authCache = null)
    {
        _sessionStorage = sessionStorage;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
        _authCache = authCache;
    }

    /// <summary>
    /// Waits for authentication to complete initialization.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for timeout control.</param>
    public Task WaitForAuthenticationAsync(CancellationToken cancellationToken = default)
    {
        return _authenticationReadyTcs.Task.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Initialize authentication state from session storage
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            // Load tokens
            var tokens = await GetFromSessionAsync<TokenResponseDto>("auth.tokens");
            if (tokens != null)
            {
                SetTokensInMemory(tokens);
                PopulateUserFromToken(tokens.AccessToken);
                
                // Store in TokenStore and CircuitContext
                var circuitId = GetCircuitId();
                if (!string.IsNullOrEmpty(circuitId) && !string.IsNullOrEmpty(tokens.AccessToken))
                {
                    // Store CircuitId in HttpContext.Items for reliable handler access
                    var httpContext = _httpContextAccessor.HttpContext;
                    if (httpContext != null)
                    {
                        httpContext.Items["CircuitId"] = circuitId;
                        
                        // SECURITY: Validate IP if available (optional - don't fail if missing)
                        var currentIp = httpContext.Connection?.RemoteIpAddress?.ToString();
                        if (currentIp == "::1") currentIp = "127.0.0.1";
                        
                        var storedIp = httpContext.Items["CircuitIP"] as string;
                        
                        // Only validate if BOTH IPs exist and differ
                        if (!string.IsNullOrEmpty(storedIp) && !string.IsNullOrEmpty(currentIp) && currentIp != storedIp)
                        {
                            _logger?.LogWarning("[Security] IP address changed during session - clearing tokens");
                            await ClearAsync();
                            return; // Finally block will signal completion
                        }
                        
                        _logger?.LogInformation("CircuitId {CircuitId} restored", circuitId);
                    }
                    
                    // Store circuit ID in session for retrieval across async boundaries
                    await SaveToSessionAsync("auth.circuitId", circuitId);
                    
                    // Extract userId from token for tracking
                    var userId = UserId;
                    var displayName = DisplayName;
                    if (string.IsNullOrEmpty(userId))
                    {
                        PopulateUserFromToken(tokens.AccessToken);
                        userId = UserId;
                        displayName = DisplayName;
                    }
                    
                    TokenStore.SetToken(circuitId, tokens.AccessToken, userId, displayName);
                    
                    var context = Http.CircuitServicesAccessor.GetContext(circuitId);
                    if (context == null)
                    {
                        context = new Http.CircuitContext { CircuitId = circuitId, AccessToken = tokens.AccessToken };
                        Http.CircuitServicesAccessor.RegisterCircuit(circuitId, context);
                    }
                    else
                    {
                        context.AccessToken = tokens.AccessToken;
                    }
                    
                    _logger?.LogInformation("Token restored for circuit {CircuitId}", circuitId);
                }
            }

            // Load profile
            var profile = await GetFromSessionAsync<MeResponseDto>("auth.profile");
            if (profile != null)
            {
                DisplayName = profile.Name ?? string.Empty;
                Email = profile.Email ?? string.Empty;
                UserId = profile.UserId.ToString();
                Permissions = profile.Permissions?.ToHashSet(StringComparer.OrdinalIgnoreCase)
                    ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            NotifyStateChanged();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize authentication state");
            _authenticationReadyTcs.TrySetException(ex);
        }
        finally
        {
            // Mark as initialized (even if failed)
            _isInitialized = true;
            
            // CRITICAL: Always signal completion, even on early returns or errors
            // Components are waiting on this signal with 5-second timeout
            if (!_authenticationReadyTcs.Task.IsCompleted)
            {
                _authenticationReadyTcs.TrySetResult(IsAuthenticated);
            }
        }
    }

    /// <summary>
    /// Set authentication tokens after login
    /// </summary>
    public async Task SetTokensAsync(TokenResponseDto tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        SetTokensInMemory(tokens);
        
        // CRITICAL: Parse UserId from token immediately to prevent state leak
        if (!string.IsNullOrEmpty(tokens.AccessToken))
        {
            try
            {
                var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(tokens.AccessToken);
                var subClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub");
                if (subClaim != null)
                {
                    UserId = subClaim.Value;
                    _logger?.LogInformation("UserId set from token: {UserId}", UserId);
                }
                
                // Also extract email and name from token
                var emailClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "email");
                var nameClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "name");
                
                if (emailClaim != null) Email = emailClaim.Value;
                if (nameClaim != null) DisplayName = nameClaim.Value;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to parse claims from token");
            }
        }
        
        // Store in static TokenStore for cross-context access
        var circuitId = GetCircuitId();
        if (!string.IsNullOrEmpty(circuitId) && !string.IsNullOrEmpty(tokens.AccessToken))
        {
            // Store CircuitId in HttpContext.Items during login
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null)
            {
                httpContext.Items["CircuitId"] = circuitId;
                
                // Store IP for session validation (optional - only if available)
                var currentIp = httpContext.Connection?.RemoteIpAddress?.ToString();
                if (currentIp == "::1") currentIp = "127.0.0.1";
                httpContext.Items["CircuitIP"] = currentIp;
                
                _logger?.LogInformation("CircuitId {CircuitId} authenticated", circuitId);
            }
            
            // Store circuit ID in session for retrieval across async boundaries
            await SaveToSessionAsync("auth.circuitId", circuitId);
            
            // Store with userId and displayName for proper user tracking
            TokenStore.SetToken(circuitId, tokens.AccessToken, UserId, DisplayName);
            
            // Update or create CircuitContext
            var context = Http.CircuitServicesAccessor.GetContext(circuitId);
            if (context == null)
            {
                context = new Http.CircuitContext { CircuitId = circuitId, AccessToken = tokens.AccessToken };
                Http.CircuitServicesAccessor.RegisterCircuit(circuitId, context);
            }
            else
            {
                context.AccessToken = tokens.AccessToken;
            }
            
            _logger?.LogInformation("Token stored for circuit {CircuitId}", circuitId);
        }
        else
        {
            _logger?.LogWarning("Failed to store token - CircuitId={CircuitId}, HasToken={HasToken}", 
                circuitId ?? "null", !string.IsNullOrEmpty(tokens.AccessToken));
        }
        
        await SaveToSessionAsync("auth.tokens", tokens);
        
        // ✅ CRITICAL FIX: Signal authentication ready after login
        _isInitialized = true;
        if (!_authenticationReadyTcs.Task.IsCompleted)
        {
            _authenticationReadyTcs.TrySetResult(true);
        }
        
        NotifyStateChanged();
    }

    /// <summary>
    /// Load and cache user profile from API
    /// </summary>
    public async Task LoadProfileAsync(HttpClient httpClient)
    {
        if (!IsAuthenticated) 
        {
            _logger?.LogWarning("[AuthState.LoadProfileAsync] Not authenticated - skipping profile load");
            return;
        }

        try
        {
            _logger?.LogInformation("[AuthState.LoadProfileAsync] Loading profile from gateway/auth/me... UserId from token: {UserId}", UserId);
            
            // DON'T manually add Authorization header - BlazorAuthenticationHandler does this!
            // The handler uses TokenStore which is already populated in SetTokensAsync
            using var request = new HttpRequestMessage(HttpMethod.Get, "gateway/auth/me");

            // Fast timeout to avoid blocking Asdamir.Web.UI during login
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var response = await httpClient.SendAsync(request, cts.Token);
            _logger?.LogInformation("[AuthState.LoadProfileAsync] Profile response status: {StatusCode}", response.StatusCode);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger?.LogWarning("[AuthState.LoadProfileAsync] Profile load unauthorized - clearing auth state");
                    await ClearAsync();
                    return;
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger?.LogWarning("[AuthState.LoadProfileAsync] Profile load failed. Status: {StatusCode}, Error: {Error}", 
                    response.StatusCode, errorContent);
                
                // Don't throw - we already have UserId, Email, DisplayName from token
                // Profile load is optional enhancement for permissions
                return;
            }

            var profile = await response.Content.ReadFromJsonAsync<MeResponseDto>();
            if (profile == null)
            {
                _logger?.LogWarning("[AuthState.LoadProfileAsync] Profile response is null");
                return;
            }

            _logger?.LogInformation("[AuthState.LoadProfileAsync] Profile loaded: UserId={UserId}, Name={Name}, PermCount={PermCount}", 
                profile.UserId, profile.Name, profile.Permissions?.Count ?? 0);

            // Update state with fresh data from API
            DisplayName = profile.Name ?? string.Empty;
            Email = profile.Email ?? string.Empty;
            UserId = profile.UserId.ToString();
            Permissions = profile.Permissions?.ToHashSet(StringComparer.OrdinalIgnoreCase)
                ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            await SaveToSessionAsync("auth.profile", profile);
            NotifyStateChanged();

            _logger?.LogInformation("[AuthState.LoadProfileAsync] Profile saved to session, Permissions count: {Count}", Permissions.Count);
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("[AuthState.LoadProfileAsync] Profile load timed out, skipping (token retained)");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[AuthState.LoadProfileAsync] Failed to load user profile - continuing with token claims");
            // Notify user that some features may be limited
            NotifyStateChanged(); // This will trigger Asdamir.Web.UI update to show limited state
        }
    }

    /// <summary>
    /// Alias for LoadProfileAsync for backward compatibility
    /// </summary>
    public Task RefreshProfileAsync(HttpClient httpClient) => LoadProfileAsync(httpClient);

    /// <summary>
    /// Try to refresh access token using refresh token
    /// </summary>
    public async Task<bool> TryRefreshAsync(HttpClient httpClient, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(RefreshToken))
            return false;

        try
        {
            var refreshRequest = new { RefreshToken };
            // Correct refresh endpoint (Gateway)
            using var request = new HttpRequestMessage(HttpMethod.Post, "gateway/auth/refresh")
            {
                Content = JsonContent.Create(refreshRequest)
            };

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Token refresh failed. Status: {StatusCode}", response.StatusCode);
                await ClearAsync();
                return false;
            }

            var tokens = await response.Content.ReadFromJsonAsync<TokenResponseDto>(cancellationToken: cancellationToken);
            if (tokens == null)
            {
                await ClearAsync();
                return false;
            }

            await SetTokensAsync(tokens);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Token refresh error");
            await ClearAsync();
            return false;
        }
    }

    /// <summary>
    /// Check if current session is expired
    /// </summary>
    public bool IsSessionExpired => !IsAuthenticated ||
        (ExpiresAtUtc.HasValue && ExpiresAtUtc.Value <= DateTime.UtcNow);

    /// <summary>
    /// Get time until session expires
    /// </summary>
    public TimeSpan TimeUntilExpiry => ExpiresAtUtc.HasValue
        ? ExpiresAtUtc.Value - DateTime.UtcNow
        : TimeSpan.Zero;

    /// <summary>
    /// Clear expired sessions (static method for background service)
    /// </summary>
    public static Task ClearExpiredSessions()
    {
        // This would be implemented for multi-user scenarios
        // For single-user Blazor Server, not needed
        return Task.CompletedTask;
    }

    /// <summary>
    /// Clear all authentication state (logout)
    /// </summary>
    public async Task ClearAsync()
    {
        var userId = !string.IsNullOrEmpty(UserId) ? UserId : Email; // Capture before clearing
        
        // CRITICAL: Clear memory cache FIRST to prevent race conditions
        AccessToken = null;
        RefreshToken = null;
        ExpiresAtUtc = null;
        DisplayName = string.Empty;
        Email = string.Empty;
        UserId = string.Empty;
        Permissions.Clear();
        
        // Remove from TokenStore and CircuitContext (thread-safe)
        var circuitId = GetCircuitId();
        if (!string.IsNullOrEmpty(circuitId))
        {
            lock (TokenStore.GetLockObject())
            {
                TokenStore.RemoveToken(circuitId);
                
                var context = Http.CircuitServicesAccessor.GetContext(circuitId);
                if (context != null)
                {
                    context.AccessToken = null;
                }
            }
            
            // Clear circuit user from LoggingCircuitHandler to prevent duplicate disconnect log
            try
            {
                var circuitHandlerType = Type.GetType("LoggingCircuitHandler, Server");
                if (circuitHandlerType != null)
                {
                    var clearMethod = circuitHandlerType.GetMethod("ClearCircuitUser", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    clearMethod?.Invoke(null, new object[] { circuitId });
                    _logger?.LogInformation("Circuit user cleared from LoggingCircuitHandler for circuit {CircuitId}", circuitId);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to clear circuit user from LoggingCircuitHandler");
            }
            
            // Clear circuit ID from session
            await RemoveFromSessionAsync("auth.circuitId");
            
            _logger?.LogInformation("Token removed for circuit {CircuitId}", circuitId);
        }

        // Invalidate authorization cache for this user
        if (_authCache != null && !string.IsNullOrEmpty(userId))
        {
            await _authCache.InvalidateUserAsync(userId);
            _logger?.LogInformation("[Asdamir.Web.Security] Authorization cache invalidated for user {UserId}", userId);
        }

        // Remove from session storage
        await RemoveFromSessionAsync("auth.tokens");
        await RemoveFromSessionAsync("auth.profile");

        // Reset TaskCompletionSource for next authentication cycle
        _authenticationReadyTcs = new TaskCompletionSource<bool>();

        NotifyStateChanged();
    }

    /// <summary>
    /// Check if user has specific permission
    /// </summary>
    public bool HasPermission(string permission) =>
        !string.IsNullOrWhiteSpace(permission) && Permissions.Contains(permission);

    /// <summary>
    /// Get current access token (sync version - does not access session storage)
    /// CRITICAL: Checks TokenStore FIRST to prevent stale cache issues after logout
    /// </summary>
    public string? GetAccessToken()
    {
        // Audit fix: v1 logged the first 20 chars of the JWT as a "preview". A JWT's
        // first 20 chars are the algorithm + token-type header — same across every token
        // for a given config — but a code change could bump that to 50 chars and start
        // leaking signature bytes. We log a stable SHA-256-truncated fingerprint instead,
        // which uniquely identifies the token in a debug log without exposing any of its
        // raw bytes.
        var circuitId = GetCircuitId();
        if (!string.IsNullOrEmpty(circuitId))
        {
            var token = TokenStore.GetToken(circuitId);
            if (!string.IsNullOrEmpty(token))
            {
                if (AccessToken != token)
                {
                    AccessToken = token;
                }
                _logger?.LogDebug("[AuthState.GetAccessToken] From TokenStore - InstanceId={InstanceId}, CircuitId={CircuitId}, TokenFingerprint={Fingerprint}",
                    _instanceId, circuitId, FingerprintToken(token));
                return token;
            }
            else
            {
                if (!string.IsNullOrEmpty(AccessToken))
                {
                    _logger?.LogWarning("[AuthState.GetAccessToken] TokenStore empty but memory cache has token - clearing memory cache");
                    AccessToken = null;
                }
            }
        }

        if (!string.IsNullOrEmpty(AccessToken))
        {
            _logger?.LogDebug("[AuthState.GetAccessToken] From memory (fallback) - InstanceId={InstanceId}, TokenFingerprint={Fingerprint}",
                _instanceId, FingerprintToken(AccessToken));
            return AccessToken;
        }

        _logger?.LogDebug("[AuthState.GetAccessToken] No token found - InstanceId={InstanceId}, CircuitId={CircuitId}",
            _instanceId, circuitId ?? "null");
        return null;
    }

    private static string FingerprintToken(string token)
    {
        // 6 hex chars of SHA-256: enough to differentiate tokens in a debug session,
        // not enough to reconstruct any part of them.
        Span<byte> hash = stackalloc byte[32];
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token), hash);
        return Convert.ToHexString(hash[..3]).ToLowerInvariant();
    }

    /// <summary>
    /// Get current access token (async version - loads from session if not in memory)
    /// </summary>
    public async Task<string?> GetAccessTokenAsync()
    {
        // If token is already in memory, return it
        if (!string.IsNullOrEmpty(AccessToken))
            return AccessToken;

        // Try to load from session storage
        try
        {
            var tokens = await GetFromSessionAsync<TokenResponseDto>("auth.tokens");
            if (tokens != null && !string.IsNullOrEmpty(tokens.AccessToken))
            {
                SetTokensInMemory(tokens);
                PopulateUserFromToken(tokens.AccessToken);
                _logger?.LogInformation("Token loaded from session storage: {TokenLength} chars", tokens.AccessToken.Length);
                
                // Also store in TokenStore for the current circuit
                var circuitId = GetCircuitId();
                if (!string.IsNullOrEmpty(circuitId))
                {
                    TokenStore.SetToken(circuitId, tokens.AccessToken, UserId, DisplayName);
                    _logger?.LogDebug("Token synchronized to TokenStore for circuit {CircuitId}", circuitId);
                }
                
                return tokens.AccessToken;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to load token from session storage");
        }

        return null;
    }

    public void SetTokensInMemory(TokenResponseDto tokens)
    {
        AccessToken = tokens.AccessToken;
        RefreshToken = tokens.RefreshToken;
        ExpiresAtUtc = tokens.ExpiresAtUtc;
        PopulateUserFromToken(tokens.AccessToken);
    }

    private void NotifyStateChanged() => Changed?.Invoke();

    public string? GetCircuitId()
    {
        // Return cached circuit ID if available.
        // IMPORTANT: do NOT permanently cache the instance-id fallback; once the real circuit id becomes
        // available (AsyncLocal / HttpContext.Items), we must switch to it so TokenStore keys match.
        if (!string.IsNullOrEmpty(_cachedCircuitId) && _cachedCircuitId != _instanceId)
            return _cachedCircuitId;
            
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) 
            {
                // In Blazor Server there is often no HttpContext; prefer AsyncLocal circuit id
                // so TokenStore keys match CircuitHandler registration (circuit.Id).
                var asyncCircuitId = Asdamir.Web.Security.Http.CircuitExecutionContext.CurrentCircuitId;
                if (!string.IsNullOrWhiteSpace(asyncCircuitId))
                {
                    _cachedCircuitId = asyncCircuitId;
                    return asyncCircuitId;
                }

                // Fallback when HttpContext/AsyncLocal are not available yet (startup edge cases).
                // Do NOT cache this fallback, otherwise we can end up storing tokens under the wrong key.
                return _instanceId;
            }

            // In Blazor Server, CircuitHandler sets the real circuit id on HttpContext.Items["CircuitId"].
            // Prefer it over connection/trace identifiers so TokenStore keys match circuit.Id.
            if (httpContext.Items.TryGetValue("CircuitId", out var circuitIdObj) && circuitIdObj is string itemsCircuitId && !string.IsNullOrWhiteSpace(itemsCircuitId))
            {
                _cachedCircuitId = itemsCircuitId;
                Asdamir.Web.Security.Http.CircuitExecutionContext.CurrentCircuitId = itemsCircuitId;
                return itemsCircuitId;
            }
            
            // Try connection ID first
            var connectionId = httpContext.Connection?.Id;
            if (!string.IsNullOrEmpty(connectionId))
            {
                _cachedCircuitId = connectionId; // Cache it
                Asdamir.Web.Security.Http.CircuitExecutionContext.CurrentCircuitId = connectionId;
                return connectionId;
            }
            
            // Try TraceIdentifier
            if (!string.IsNullOrEmpty(httpContext.TraceIdentifier))
            {
                _cachedCircuitId = httpContext.TraceIdentifier; // Cache it
                Asdamir.Web.Security.Http.CircuitExecutionContext.CurrentCircuitId = httpContext.TraceIdentifier;
                return httpContext.TraceIdentifier;
            }
            
            // Ultimate fallback to instance ID
            return _instanceId;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get circuit ID, using instance ID");
            return _instanceId;
        }
    }

    private async Task<T?> GetFromSessionAsync<T>(string key)
    {
        await _sessionLock.WaitAsync();
        try
        {
            return await _sessionStorage.GetItemAsync<T>(key);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to get session item: {Key}", key);
            return default;
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private async Task SaveToSessionAsync<T>(string key, T value)
    {
        await _sessionLock.WaitAsync();
        try
        {
            await _sessionStorage.SetItemAsync(key, value);
        }
        catch (JSDisconnectedException)
        {
            _logger?.LogDebug("Cannot save {Key} - circuit disconnected", key);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to save session item: {Key}", key);
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private async Task RemoveFromSessionAsync(string key)
    {
        await _sessionLock.WaitAsync();
        try
        {
            await _sessionStorage.RemoveItemAsync(key);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to remove session item: {Key}", key);
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    private void PopulateUserFromToken(string? token)
    {
        if (string.IsNullOrEmpty(token))
            return;

        try
        {
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            var subClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "sub" || c.Type == "nameid" || c.Type == "userId");
            if (subClaim != null)
            {
                UserId = subClaim.Value;
            }

            var emailClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "email");
            var nameClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "name");

            if (emailClaim != null) Email = emailClaim.Value;
            if (nameClaim != null) DisplayName = nameClaim.Value;

            _logger?.LogDebug("[AuthState] Claims populated from token - UserId={UserId}, Email={Email}", UserId, Email);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[AuthState] Failed to parse claims from token");
        }
    }
}
