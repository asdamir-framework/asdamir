// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Microsoft.Extensions.Options;

namespace Asdamir.Web.Security.Services;

/// <summary>
/// Auto logout configuration options
/// </summary>
public class AutoLogoutOptions
{
    /// <summary>
    /// Time before showing warning (default: 4 minutes)
    /// After this time, warning will be shown with remaining time until logout
    /// </summary>
    public TimeSpan WarningTime { get; set; } = TimeSpan.FromMinutes(4);

    /// <summary>
    /// Total inactivity time before auto logout (default: 5 minutes)
    /// </summary>
    public TimeSpan LogoutTime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Check interval for activity (default: 30 seconds)
    /// </summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Enable activity tracking (default: true)
    /// </summary>
    public bool EnableActivityTracking { get; set; } = true;

    /// <summary>
    /// Show warning dialog before logout (default: true)
    /// </summary>
    public bool ShowWarningDialog { get; set; } = true;
}

/// <summary>
/// Service for automatic logout based on user inactivity
/// </summary>
public class AutoLogoutService : IAsyncDisposable
{
    private readonly AuthState _authState;
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<AutoLogoutService> _logger;
    private readonly AutoLogoutOptions _options;
    private readonly NavigationManager _navigation;

    private Timer? _activityTimer;
    private DateTime _lastActivity = DateTime.UtcNow;
    private bool _warningShown = false;
    private bool _disposed = false;
    private bool _isMonitoring = false;
    private DotNetObjectReference<AutoLogoutService>? _dotNetObjectRef;

    // Audit fix: timer callbacks run off the Blazor sync context. Any JS interop /
    // NavigationManager / event raising MUST be marshalled through the Dispatcher so the
    // hosting circuit's synchronization context is preserved. The consuming component is
    // expected to call SetDispatcher(...) after construction.
    private Dispatcher? _dispatcher;

    /// <summary>Registers the hosting circuit's <see cref="Dispatcher"/> so timer callbacks are marshalled onto the Blazor synchronization context; must be called by the consuming component after construction.</summary>
    /// <param name="dispatcher">The circuit dispatcher to marshal activity checks through.</param>
    public void SetDispatcher(Dispatcher dispatcher) => _dispatcher = dispatcher;

    /// <summary>Raised when the warning threshold is crossed; the argument is the remaining time until auto-logout.</summary>
    public event EventHandler<TimeSpan>? WarningTimeReached;
    /// <summary>Raised when inactivity reaches the logout threshold and the session is being cleared.</summary>
    public event EventHandler? LogoutTimeReached;

    /// <summary>Initializes the auto-logout service with auth state, JS interop, logging, options, and navigation.</summary>
    /// <param name="authState">Current authentication state; monitoring stops when unauthenticated.</param>
    /// <param name="jsRuntime">JS runtime used to register/unregister browser activity listeners.</param>
    /// <param name="logger">Logger for lifecycle and logout diagnostics.</param>
    /// <param name="options">Warning/logout thresholds and check interval.</param>
    /// <param name="navigation">Navigation manager used to redirect to the login page on logout.</param>
    public AutoLogoutService(
        AuthState authState,
        IJSRuntime jsRuntime,
        ILogger<AutoLogoutService> logger,
        IOptions<AutoLogoutOptions> options,
        NavigationManager navigation)
    {
        _authState = authState;
        _jsRuntime = jsRuntime;
        _logger = logger;
        _options = options.Value;
        _navigation = navigation;
    }

    /// <summary>
    /// Start monitoring user activity
    /// </summary>
    public async Task StartMonitoringAsync()
    {
        if (!_authState.IsAuthenticated || !_options.EnableActivityTracking || _isMonitoring)
            return;

        try
        {
            // Create DotNetObjectReference only once
            if (_dotNetObjectRef == null)
            {
                _dotNetObjectRef = DotNetObjectReference.Create(this);
            }

            // Register JS activity listeners
            await _jsRuntime.InvokeVoidAsync("SecurityFramework.registerActivityListeners", _dotNetObjectRef);

            // Start activity timer
            _activityTimer = new Timer(CheckActivity, null, _options.CheckInterval, _options.CheckInterval);
            
            _isMonitoring = true;
            _logger.LogDebug("AutoLogoutService started monitoring");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start activity monitoring");
        }
    }

    /// <summary>
    /// Stop monitoring user activity
    /// </summary>
    public async Task StopMonitoringAsync()
    {
        if (!_isMonitoring)
            return;

        try
        {
            _activityTimer?.Dispose();
            _activityTimer = null;

            await _jsRuntime.InvokeVoidAsync("SecurityFramework.unregisterActivityListeners");
            
            _isMonitoring = false;
            _logger.LogDebug("AutoLogoutService stopped monitoring");
        }
        catch (JSDisconnectedException)
        {
            // Circuit already disconnected - this is normal during page navigation/culture change
            _isMonitoring = false;
            _logger.LogDebug("AutoLogoutService stopped (circuit disconnected)");
        }
        catch (TaskCanceledException)
        {
            // JS interop was canceled because the circuit closed - expected behavior
            _isMonitoring = false;
            _logger.LogDebug("AutoLogoutService stopped (circuit canceled)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop activity monitoring");
        }
    }

    /// <summary>
    /// Record user activity (called from JavaScript)
    /// </summary>
    [JSInvokable]
    public void RecordActivity()
    {
        _lastActivity = DateTime.UtcNow;
        _warningShown = false;
    }

    /// <summary>
    /// Get time since last activity
    /// </summary>
    public TimeSpan TimeSinceLastActivity => DateTime.UtcNow - _lastActivity;

    /// <summary>
    /// Extend session by resetting activity timer (called when user clicks "Continue Working")
    /// </summary>
    public void ExtendSession()
    {
        _lastActivity = DateTime.UtcNow;
        _warningShown = false;
        _logger.LogInformation("Session extended by user action");
    }

    /// <summary>
    /// Get time until warning
    /// </summary>
    public TimeSpan TimeUntilWarning
    {
        get
        {
            var remaining = _options.WarningTime - TimeSinceLastActivity;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    /// <summary>
    /// Get time until logout
    /// </summary>
    public TimeSpan TimeUntilLogout
    {
        get
        {
            var remaining = _options.LogoutTime - TimeSinceLastActivity;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    private void CheckActivity(object? state)
    {
        if (_disposed)
            return;

        // Marshal the whole check onto the Blazor sync context. Without this,
        // _authState reads, JS interop, NavigationManager and event invocation all race
        // with the circuit's own renders — caught by the audit ("AutoLogoutService timer
        // callback'lerini Blazor Dispatcher üzerinden post et").
        var work = RunCheckAsync;
        if (_dispatcher is { } dispatcher)
        {
            _ = dispatcher.InvokeAsync(work);
        }
        else
        {
            // No dispatcher registered yet (rare — only between construction and the
            // component's OnInitializedAsync). Fall back to direct invocation; the next
            // tick will use the dispatcher once SetDispatcher has been called.
            _ = work();
        }
    }

    private async Task RunCheckAsync()
    {
        if (_disposed) return;

        if (!_authState.IsAuthenticated)
        {
            await StopMonitoringAsync();
            return;
        }

        var timeSinceActivity = TimeSinceLastActivity;

        if (timeSinceActivity >= _options.LogoutTime)
        {
            try
            {
                _logger.LogInformation("Auto logout triggered due to inactivity");
                await _authState.ClearAsync();
                LogoutTimeReached?.Invoke(this, EventArgs.Empty);
                _navigation.NavigateTo("/login", forceLoad: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during auto logout");
            }
            return;
        }

        if (!_warningShown && timeSinceActivity >= _options.WarningTime)
        {
            _warningShown = true;
            var timeUntilLogout = TimeUntilLogout;
            _logger.LogInformation("Warning: User will be logged out in {TimeUntilLogout}", timeUntilLogout);
            WarningTimeReached?.Invoke(this, timeUntilLogout);
        }
    }

    /// <summary>Stops activity monitoring and releases the JS object reference and timer.</summary>
    /// <returns>A task that completes when teardown finishes.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await StopMonitoringAsync();
        
        // Dispose DotNetObjectReference
        _dotNetObjectRef?.Dispose();
        _dotNetObjectRef = null;
        
        GC.SuppressFinalize(this);
    }
}