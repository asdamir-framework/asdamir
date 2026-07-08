// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Microsoft.JSInterop;
using Asdamir.Web.Security.Services;

namespace Asdamir.Web.UI.Components;

/// <summary>
/// Base class for components that require authentication.
/// Ensures authentication is ready before component initialization completes.
/// </summary>
public abstract class AuthorizedComponentBase : ComponentBase, IDisposable
{
    /// <summary>Injected authentication state used to wait for and inspect the current session.</summary>
    [Inject] protected AuthState AuthState { get; set; } = default!;

    /// <summary>Injected logger for authentication-readiness diagnostics.</summary>
    [Inject] protected ILogger<AuthorizedComponentBase> Logger { get; set; } = default!;

    /// <summary>Injected navigation manager used to redirect to the login page when authentication is not ready.</summary>
    [Inject] protected NavigationManager Navigation { get; set; } = default!;

    private bool _disposed;

    /// <summary>
    /// Ensures authentication is ready, then calls OnAuthenticatedInitializedAsync.
    /// DO NOT override this method. Use OnAuthenticatedInitializedAsync instead.
    /// </summary>
    protected override async Task OnInitializedAsync()
    {
        var ready = await EnsureAuthenticationReadyAsync();
        if (ready)
        {
            await OnAuthenticatedInitializedAsync();
        }
    }

    /// <summary>
    /// Waits for authentication to complete. Returns false if not ready (redirects to login).
    /// GUARANTEES: Token + identity fields (UserId/Email) are available.
    /// </summary>
    private async Task<bool> EnsureAuthenticationReadyAsync()
    {
        try
        {
            var timeout = TimeSpan.FromSeconds(5);
            var cts = new CancellationTokenSource(timeout);
            
            await AuthState.WaitForAuthenticationAsync(cts.Token);

            var hasIdentity = !(string.IsNullOrWhiteSpace(AuthState.UserId) && string.IsNullOrWhiteSpace(AuthState.Email));
            if (!AuthState.IsAuthenticated || !hasIdentity)
            {
                Logger.LogWarning("[{Component}] Authentication not ready - IsAuth: {IsAuth}, UserId: {UserId}, Email: {Email}", 
                    GetType().Name, AuthState.IsAuthenticated, AuthState.UserId ?? "(null)", AuthState.Email ?? "(null)");
                TryNavigateToLogin("unauthorized");
                return false;
            }

            Logger.LogDebug("[{Component}] Authentication ready - Email: {Email}", 
                GetType().Name, AuthState.Email);
        }
        catch (OperationCanceledException)
        {
            Logger.LogWarning("[{Component}] Authentication timeout after 5 seconds", GetType().Name);
            TryNavigateToLogin("timeout");
            return false;
        }

        return true;
    }

    private void TryNavigateToLogin(string reason)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            Navigation.NavigateTo($"/login?reason={Uri.EscapeDataString(reason)}", forceLoad: true);
        }
        catch (JSDisconnectedException)
        {
            // Normal if the circuit is already disconnecting (Ctrl+F5 / tab close).
            Logger.LogDebug("[{Component}] Navigation skipped - circuit disconnected", GetType().Name);
        }
        catch (InvalidOperationException ex)
        {
            // Be defensive: navigation can fail late in circuit disposal.
            Logger.LogDebug(ex, "[{Component}] Navigation skipped - invalid operation", GetType().Name);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposed = true;
    }

    /// <summary>
    /// Override this method instead of OnInitializedAsync.
    /// Authentication is guaranteed to be ready when this method is called.
    /// </summary>
    protected virtual Task OnAuthenticatedInitializedAsync()
    {
        return Task.CompletedTask;
    }
}
