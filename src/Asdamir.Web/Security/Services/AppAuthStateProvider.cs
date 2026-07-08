// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.Security.Claims;

namespace Asdamir.Web.Security.Services;

/// <summary>
/// Enterprise authentication state provider
/// Integrates AuthState with ASP.NET Core's authentication system
/// Provides synchronous and async authentication state management
/// </summary>
public sealed class AppAuthStateProvider : AuthenticationStateProvider, IDisposable
{
    private readonly AuthState _auth;
    private readonly ILogger<AppAuthStateProvider>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AppAuthStateProvider"/> class and subscribes to <see cref="AuthState"/> changes.
    /// </summary>
    /// <param name="auth">The per-circuit authentication state that backs the produced <see cref="ClaimsPrincipal"/>.</param>
    /// <param name="logger">Optional logger for authentication-state diagnostics.</param>
    public AppAuthStateProvider(AuthState auth, ILogger<AppAuthStateProvider>? logger = null)
    {
        _auth = auth;
        _logger = logger;
        _auth.Changed += OnAuthChanged;
    }

    /// <inheritdoc/>
    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        ClaimsPrincipal principal;

        // Try to load token from session if not in memory
        if (!_auth.IsAuthenticated)
        {
            var token = await _auth.GetAccessTokenAsync();
            if (!string.IsNullOrEmpty(token))
            {
                _logger?.LogDebug("Token loaded from session storage");
            }
        }

        if (_auth.IsAuthenticated && !_auth.IsSessionExpired)
        {
            try
            {
                var claims = new List<Claim>
                {
                    new(ClaimTypes.Name, _auth.DisplayName ?? "User"),
                    new(ClaimTypes.NameIdentifier, _auth.UserId ?? _auth.Email ?? "Unknown"),
                    new(ClaimTypes.Email, _auth.Email ?? "Unknown"),
                    new("sub", _auth.UserId ?? _auth.Email ?? "Unknown"),
                    new("DisplayName", _auth.DisplayName ?? "User")
                };

                // Add permissions as claims
                if (_auth.Permissions is not null)
                {
                    foreach (var permission in _auth.Permissions)
                    {
                        claims.Add(new Claim("permission", permission));
                        claims.Add(new Claim("perm", permission)); // Required by RouteAuthorizationMiddleware
                    }
                    
                    _logger?.LogDebug("Added {Count} permissions to claims", _auth.Permissions.Count);
                }

                // SECURITY: do NOT add the access_token to the ClaimsPrincipal.
                // In v1, the JWT was stored as a "access_token" claim on the principal — which
                // got serialised into circuit state, surfaced in `user.Claims` enumerations
                // (admin/debug UIs), and could be picked up by any component that inspects
                // claims. The token lives only in AuthState/ITokenStore from now on.

                // Add expiry information (non-sensitive)
                if (_auth.ExpiresAtUtc.HasValue)
                {
                    claims.Add(new Claim("expires_at", _auth.ExpiresAtUtc.Value.ToString("O")));
                }

                var identity = new ClaimsIdentity(claims, authenticationType: "jwt");
                principal = new ClaimsPrincipal(identity);
                
                _logger?.LogDebug("Authentication state created for user: {UserId}", _auth.UserId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error creating authentication state, clearing session");
                // If there's an error creating claims, clear the auth state
                await _auth.ClearAsync();
                principal = new ClaimsPrincipal(new ClaimsIdentity());
            }
        }
        else
        {
            principal = new ClaimsPrincipal(new ClaimsIdentity()); // Anonymous
            _logger?.LogDebug("User not authenticated or session check in progress");
        }

        return new AuthenticationState(principal);
    }

    /// <summary>
    /// Notify that user has been authenticated
    /// Call this after successful login
    /// </summary>
    public void NotifyUserAuthentication()
    {
        _logger?.LogInformation("Notifying user authentication for: {UserId}", _auth.UserId);
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    /// <summary>
    /// Notify that user has been logged out
    /// Call this after logout
    /// </summary>
    public void NotifyUserLogout()
    {
        _logger?.LogInformation("Notifying user logout");
        NotifyAuthenticationStateChanged(Task.FromResult(
            new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()))));
    }

    private void OnAuthChanged()
    {
        _logger?.LogDebug("Auth state changed, notifying authentication state change");
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    /// <inheritdoc/>
    public void Dispose() => _auth.Changed -= OnAuthChanged;
}