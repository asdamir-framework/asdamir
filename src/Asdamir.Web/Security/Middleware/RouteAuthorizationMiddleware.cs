// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Routing;
using Asdamir.Web.Security.Attributes;
using Asdamir.Web.Security.Models;
using Asdamir.Web.Security.Services;
using System.Security.Claims;
using System.Reflection;

namespace Asdamir.Web.Security.Middleware;

/// <summary>
/// Enterprise-grade Blazor component that handles route-level authorization
/// with token validation, rate limiting, audit logging, and caching.
/// Designed to work seamlessly in Blazor Server without interfering with component rendering.
/// </summary>
public class RouteAuthorizationMiddleware : ComponentBase, IDisposable
{
    [Inject] private AuthenticationStateProvider AuthProvider { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ILogger<RouteAuthorizationMiddleware> Logger { get; set; } = default!;
    [Inject] private IAuthorizationTokenService? TokenService { get; set; }
    [Inject] private IAuthorizationAuditService? AuditService { get; set; }
    [Inject] private IAuthorizationRateLimiter? RateLimiter { get; set; }
    [Inject] private IAuthorizationCache? AuthCache { get; set; }
    [Inject] private AuthState AuthState { get; set; } = default!;
    [Inject] private ClientInfoService? ClientInfoService { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public Type? RouteComponentType { get; set; }

    private bool _isChecking = true; // Start as checking to avoid flash
    private bool _isAuthorized = false; // Default to not authorized until check completes
    private AuthorizationResult? _authResult;
    private bool _hasNavigated = false;
    private string? _lastCheckedRoute;
    private string? _lastAuditedRoute;
    private DateTime _lastAuditTime = DateTime.MinValue;
    private Guid _currentCheckId = Guid.Empty;

    protected override void OnInitialized()
    {
        // Register for location changes to perform checks on navigation
        Navigation.LocationChanged += OnLocationChanged;
    }

    protected override async Task OnParametersSetAsync()
    {
        // Check authorization when parameters are set (including route changes)
        // This runs after component initialization but before rendering
        await CheckRouteAuthorizationAsync();
    }

    private void OnLocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
    {
        // Reset state for new route
        _hasNavigated = false;
        _lastCheckedRoute = null;
        
        // Check authorization on every navigation (use InvokeAsync for proper Blazor threading)
        _ = InvokeAsync(async () =>
        {
            try
            {
                await CheckRouteAuthorizationAsync();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[Asdamir.Web.Security] Error in location changed handler");
            }
        });
    }

    private async Task CheckRouteAuthorizationAsync()
    {
        var currentRoute = Navigation.Uri;
        
        // Avoid duplicate checks for the same route (but allow first check)
        if (_hasNavigated || (_lastCheckedRoute == currentRoute && !_isChecking)) return;
        
        _lastCheckedRoute = currentRoute;
        var checkId = Guid.NewGuid();
        _currentCheckId = checkId;

        try
        {
            _isChecking = true;

            if (RouteComponentType == null)
            {
                _isAuthorized = true;
                _isChecking = false;
                StateHasChanged();
                return;
            }

            // Check for AllowAnonymous attribute first
            var allowAnonymousAttribute = RouteComponentType.GetCustomAttribute<Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute>();
            if (allowAnonymousAttribute != null)
            {
                // Allow anonymous access
                _isAuthorized = true;
                _isChecking = false;
                StateHasChanged();
                return;
            }

            var authorizeAttribute = RouteComponentType.GetCustomAttribute<AuthorizePageAttribute>();
            if (authorizeAttribute == null)
            {
                // No authorization required
                _isAuthorized = true;
                _isChecking = false;
                _hasNavigated = true; // Prevent re-checking on re-render
                StateHasChanged();
                return;
            }

            // CRITICAL: Wait for App.razor's authentication initialization to complete
            // This prevents race condition where route authorization runs before AuthState is ready.
            if (!AuthState.IsInitialized)
            {
                try
                {
                    var timeout = TimeSpan.FromSeconds(1);
                    var cts = new CancellationTokenSource(timeout);
                    await AuthState.WaitForAuthenticationAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // SHORT-LIVED circuit timeout - ignore (circuit will be disposed soon)
                    Logger.LogDebug("Auth not ready for short-lived circuit {Route} - giving up after timeout", Navigation.Uri);
                    _isAuthorized = false;
                    _isChecking = false;
                    return; // Don't navigate, let circuit dispose naturally
                }
            }

            var authState = await AuthProvider.GetAuthenticationStateAsync();
            var user = authState.User;
            var isAuthenticated = user?.Identity?.IsAuthenticated == true;

            if (!isAuthenticated)
            {
                Logger.LogInformation("User not authenticated for route {Route}, redirecting to {RedirectTo}", 
                    Navigation.Uri, authorizeAttribute.RedirectTo);
                Navigation.NavigateTo(authorizeAttribute.RedirectTo, forceLoad: true);
                return;
            }

            // Get userId from AuthState (preferred) or fallback to claims
            var userId = !string.IsNullOrEmpty(this.AuthState.UserId) ? this.AuthState.UserId
                : !string.IsNullOrEmpty(this.AuthState.Email) ? this.AuthState.Email
                : user?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? user?.FindFirst("sub")?.Value 
                ?? user?.Identity?.Name 
                ?? "Unknown";
            
            var userEmail = !string.IsNullOrEmpty(this.AuthState.Email) ? this.AuthState.Email
                : user?.FindFirst(ClaimTypes.Email)?.Value ?? "Unknown";

            // 1. Check token validity and expiration
            if (TokenService != null)
            {
                var isTokenValid = await TokenService.IsStructurallyValidAsync();
                if (!isTokenValid)
                {
                    Logger.LogWarning("Invalid token for user {UserId}, clearing session", userId);
                    if (AuthState != null)
                    {
                        await AuthState.ClearAsync();
                    }
                    Navigation.NavigateTo("/login?reason=invalid_token", forceLoad: true);
                    return;
                }

                // Check if token is expiring soon and attempt refresh
                var isExpiring = await TokenService.IsTokenExpiredOrExpiringAsync(5);
                if (isExpiring)
                {
                    Logger.LogInformation("Token expiring for user {UserId}, attempting refresh", userId);
                    var refreshed = await TokenService.TryRefreshTokenAsync();
                    
                    if (!refreshed)
                    {
                        Logger.LogWarning("Token refresh failed for user {UserId}, redirecting to login", userId);
                        if (AuthState != null)
                        {
                            await AuthState.ClearAsync();
                        }
                        Navigation.NavigateTo("/login?reason=token_expired", forceLoad: true);
                        return;
                    }
                }
            }
            else
            {
                // Enterprise hardening: TokenService yoksa riskli - oturumu düşür
                Logger.LogError("TokenService is null - cannot validate token. Redirecting to login.");
                if (AuthState != null)
                {
                    await AuthState.ClearAsync();
                }
                Navigation.NavigateTo("/login?reason=token_service_missing", forceLoad: true);
                return;
            }

            // 2. Check rate limiting
            if (RateLimiter != null)
            {
                var isRateLimited = await RateLimiter.IsRateLimitExceededAsync(userId);
                if (isRateLimited)
                {
                    var remainingTime = await RateLimiter.GetRemainingLockoutTimeAsync(userId);
                    Logger.LogWarning("User {UserId} is rate limited for {Seconds} seconds", 
                        userId, remainingTime.TotalSeconds);
                    
                    _authResult = new AuthorizationResult
                    {
                        IsAuthorized = false,
                        DenialReason = $"Too many authorization failures. Please try again in {remainingTime.TotalMinutes:F0} minutes."
                    };

                    await LogAuditEventAsync(userId, userEmail, authorizeAttribute, false, _authResult.DenialReason);
                    Navigation.NavigateTo("/access-denied?reason=rate_limited", forceLoad: true);
                    return;
                }
            }

            // 3. Check cache
            if (AuthCache != null)
            {
                var cachedResult = await AuthCache.GetAsync(userId, Navigation.Uri);
                if (cachedResult != null)
                {
                    Logger.LogDebug("Using cached authorization result for user {UserId} on route {Route}", 
                        userId, Navigation.Uri);
                    
                    _authResult = cachedResult;
                    _isAuthorized = cachedResult.IsAuthorized;
                    _isChecking = false;

                    // Log audit even for cached results
                    await LogAuditEventAsync(userId, userEmail, authorizeAttribute, _isAuthorized, _authResult.DenialReason);

                    if (!_isAuthorized)
                    {
                        await HandleAuthorizationFailureAsync(userId, authorizeAttribute);
                        return;
                    }

                    StateHasChanged();
                    return;
                }
            }

            // 4. Perform authorization check
            _authResult = CheckAuthorizationDetailed(user!, authorizeAttribute);
            _isAuthorized = _authResult.IsAuthorized;

            // 5. Cache result
            if (AuthCache != null && _authResult.IsAuthorized)
            {
                await AuthCache.SetAsync(userId, Navigation.Uri, _authResult, TimeSpan.FromMinutes(5));
            }

            // 6. Audit logging
            await LogAuditEventAsync(userId, userEmail, authorizeAttribute, _isAuthorized, _authResult.DenialReason);

            // 7. Handle authorization result
            if (!_isAuthorized)
            {
                _hasNavigated = true;
                await HandleAuthorizationFailureAsync(userId, authorizeAttribute);
                return;
            }
            else
            {
                // Reset rate limit on successful authorization
                if (RateLimiter != null)
                {
                    await RateLimiter.ResetAsync(userId);
                }
            }
        }
        catch (Microsoft.JSInterop.JSDisconnectedException)
        {
            // Expected during logout/navigation - circuit is being disposed
            Logger.LogDebug("Route authorization check cancelled - circuit disconnected");
            _isAuthorized = false;
        }
        catch (TaskCanceledException)
        {
            // Expected during logout/navigation - operation cancelled
            Logger.LogDebug("Route authorization check cancelled - operation cancelled");
            _isAuthorized = false;
        }
        catch (Exception ex)
        {
            // SECURITY: fail-closed. Previously this set _isAuthorized = true on any unexpected
            // exception, which meant any bug in token/claim/cache code rendered the protected
            // component. Default to denying access and redirect to /access-denied.
            Logger.LogError(ex, "Error during route authorization check — denying access (fail-closed)");
            _isAuthorized = false;
            _hasNavigated = true;
            try
            {
                Navigation.NavigateTo("/access-denied?reason=authorization_error", forceLoad: false);
            }
            catch (Exception navEx)
            {
                Logger.LogDebug(navEx, "Navigation to /access-denied failed (circuit likely disposing)");
            }
        }
        finally
        {
            _isChecking = false;
            StateHasChanged(); // Trigger re-render after authorization check completes
        }
    }

    private async Task HandleAuthorizationFailureAsync(string userId, AuthorizePageAttribute attribute)
    {
        if (RateLimiter != null)
        {
            await RateLimiter.RecordFailureAsync(userId);
        }

        // Audit fix: do NOT echo missing_permissions / missing_roles / detailed
        // denial reason on the URL. The v1 behavior gave anonymous probers a list
        // of valid permission names — they could enumerate the whole authorization
        // surface just by visiting a denied page. Server-side log keeps the detail;
        // client gets an opaque correlation id they can quote to support.
        var correlationId = Guid.NewGuid().ToString("N").Substring(0, 12);
        Logger.LogWarning(
            "Access denied. correlationId={CorrelationId} userId={UserId} route={Route} reason={Reason} missingPermissions={MissingPermissions} missingRoles={MissingRoles}",
            correlationId,
            userId,
            Navigation.Uri,
            _authResult?.DenialReason,
            _authResult?.MissingPermissions is { Count: > 0 } mp ? string.Join(",", mp) : null,
            _authResult?.MissingRoles is { Count: > 0 } mr ? string.Join(",", mr) : null);

        var queryString = $"?ref={Uri.EscapeDataString(correlationId)}";

        try
        {
            Navigation.NavigateTo($"/access-denied{queryString}", forceLoad: false);
        }
        catch (Microsoft.JSInterop.JSDisconnectedException ex)
        {
            // Circuit already disconnected - navigation not needed (this is expected)
            Logger.LogDebug("Authorization failure navigation skipped - circuit disconnected: {Message}", ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("circuit") || ex.Message.Contains("disconnect"))
        {
            // Circuit being disposed - navigation not needed (this is expected)
            Logger.LogDebug("Authorization failure navigation skipped - circuit disposing: {Message}", ex.Message);
        }
        catch (Exception ex)
        {
            // Log other unexpected errors but don't throw - authorization denial already logged
            Logger.LogWarning(ex, "Unexpected error during authorization failure navigation");
        }
    }

    private async Task LogAuditEventAsync(
        string userId, 
        string userEmail, 
        AuthorizePageAttribute attribute, 
        bool isAuthorized, 
        string? denialReason)
    {
        if (AuditService == null)
        {
            Logger.LogWarning("AuditService is null - cannot log authorization audit");
            return;
        }

        try
        {
            var currentRoute = Navigation.Uri;
            
            // Prevent duplicate audit logs for the same route within 2 seconds
            // This catches OnParametersSetAsync + OnLocationChanged double-triggers
            if (_lastAuditedRoute == currentRoute && (DateTime.UtcNow - _lastAuditTime).TotalSeconds < 2)
            {
                Logger.LogDebug("Skipping duplicate audit for route: {Route} (last audit: {LastTime}ms ago)", 
                    currentRoute, (DateTime.UtcNow - _lastAuditTime).TotalMilliseconds);
                return;
            }
            
            // Update tracking BEFORE async call to prevent race condition
            _lastAuditedRoute = currentRoute;
            _lastAuditTime = DateTime.UtcNow;

            // Use ClientInfoService (captured at circuit start) instead of HttpContextAccessor
            var ipAddress = ClientInfoService?.IpAddress ?? "Unknown";
            var userAgent = ClientInfoService?.UserAgent ?? "Unknown";

            Logger.LogInformation("Logging authorization audit - Route: {Route}, UserId: {UserId}, IsAuthorized: {IsAuthorized}, IP: {IP}, UserAgent: {UA}", 
                currentRoute, userId, isAuthorized, ipAddress, userAgent);

            var auditEvent = new AuthorizationAuditEvent
            {
                UserId = userId,
                UserEmail = userEmail,
                Route = currentRoute,
                RequiredPermission = attribute.RequiredPermission,
                RequiredRole = attribute.RequiredRole,
                RequiredPermissions = attribute.RequiredPermissions,
                RequiredRoles = attribute.RequiredRoles,
                IsAuthorized = isAuthorized,
                DenialReason = denialReason,
                IpAddress = ipAddress,
                Timestamp = DateTime.UtcNow
            };

            await AuditService.LogAuthorizationAttemptAsync(auditEvent);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error logging audit event from IAuthorizationAuditService");
        }
    }

    private static AuthorizationResult CheckAuthorizationDetailed(ClaimsPrincipal user, AuthorizePageAttribute attribute)
    {
        var result = new AuthorizationResult();

        if (user == null)
        {
            result.IsAuthorized = false;
            result.DenialReason = "User is not authenticated";
            return result;
        }

        // If no requirements specified, just check authentication
        if (string.IsNullOrEmpty(attribute.RequiredPermission) && 
            string.IsNullOrEmpty(attribute.RequiredRole) && 
            (attribute.RequiredPermissions?.Length ?? 0) == 0 && 
            (attribute.RequiredRoles?.Length ?? 0) == 0)
        {
            result.IsAuthorized = true;
            return result;
        }

        var (hasPermissions, missingPermissions) = CheckPermissionsDetailed(user, attribute);
        var (hasRoles, missingRoles) = CheckRolesDetailed(user, attribute);

        result.MissingPermissions = missingPermissions;
        result.MissingRoles = missingRoles;

        if (attribute.RequireAll)
        {
            result.IsAuthorized = hasPermissions && hasRoles;
            
            if (!result.IsAuthorized)
            {
                var denials = new List<string>();
                if (!hasPermissions) denials.Add($"permissions: {string.Join(", ", missingPermissions)}");
                if (!hasRoles) denials.Add($"roles: {string.Join(", ", missingRoles)}");
                result.DenialReason = $"Missing required {string.Join(" and ", denials)}";
            }
        }
        else
        {
            result.IsAuthorized = hasPermissions || hasRoles;
            
            if (!result.IsAuthorized)
            {
                result.DenialReason = $"Missing any of the required permissions or roles";
            }
        }

        return result;
    }

    private static (bool hasPermissions, List<string> missing) CheckPermissionsDetailed(
        ClaimsPrincipal user, 
        AuthorizePageAttribute attribute)
    {
        var permissions = new List<string>();
        
        if (!string.IsNullOrEmpty(attribute.RequiredPermission))
            permissions.Add(attribute.RequiredPermission);
            
        if (attribute.RequiredPermissions?.Length > 0)
            permissions.AddRange(attribute.RequiredPermissions);

        if (permissions.Count == 0) 
            return (true, new List<string>());

        var missing = new List<string>();

        if (attribute.RequireAll)
        {
            foreach (var perm in permissions)
            {
                if (!user.HasClaim("perm", perm))
                {
                    missing.Add(perm);
                }
            }
            return (missing.Count == 0, missing);
        }
        else
        {
            var hasAny = permissions.Any(p => user.HasClaim("perm", p));
            if (!hasAny)
            {
                missing.AddRange(permissions);
            }
            return (hasAny, missing);
        }
    }

    private static (bool hasRoles, List<string> missing) CheckRolesDetailed(
        ClaimsPrincipal user, 
        AuthorizePageAttribute attribute)
    {
        var roles = new List<string>();
        
        if (!string.IsNullOrEmpty(attribute.RequiredRole))
            roles.Add(attribute.RequiredRole);
            
        if (attribute.RequiredRoles?.Length > 0)
            roles.AddRange(attribute.RequiredRoles);

        if (roles.Count == 0) 
            return (true, new List<string>());

        var missing = new List<string>();

        if (attribute.RequireAll)
        {
            foreach (var role in roles)
            {
                if (!user.IsInRole(role))
                {
                    missing.Add(role);
                }
            }
            return (missing.Count == 0, missing);
        }
        else
        {
            var hasAny = roles.Any(r => user.IsInRole(r));
            if (!hasAny)
            {
                missing.AddRange(roles);
            }
            return (hasAny, missing);
        }
    }

    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
    {
        // Show content only if authorized
        if (_isAuthorized && !_isChecking && ChildContent != null)
        {
            builder.AddContent(0, ChildContent);
        }
        else if (_isChecking)
        {
            // Show nothing while checking - authorization happens so fast that no loading indicator is needed
            // If unauthorized, CheckRouteAuthorizationAsync will redirect before this renders
            builder.AddContent(0, (Microsoft.AspNetCore.Components.RenderFragment)(b => { }));
        }
        // If not authorized and not checking, do nothing (redirect already happened)
    }

    public void Dispose()
    {
        Navigation.LocationChanged -= OnLocationChanged;
    }
}
