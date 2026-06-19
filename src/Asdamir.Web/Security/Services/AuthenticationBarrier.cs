// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.


namespace Asdamir.Web.Security.Services;

/// <summary>
/// Synchronisation barrier for Blazor circuit authentication.
///
/// Replaces the v1 SemaphoreSlim(0, 1) implementation, which only released ONE waiter on
/// SetReady() — any subsequent waiters that entered the await before _isReady became visible
/// would block until cancellation. This caused intermittent dead-lock-like hangs during
/// circuit warm-up.
///
/// Backed by a TaskCompletionSource&lt;bool&gt; with RunContinuationsAsynchronously so all
/// waiters resume after SetReady().
/// </summary>
public class AuthenticationBarrier
{
    private readonly ILogger<AuthenticationBarrier>? _logger;
    private TaskCompletionSource<bool> _tcs;
    private volatile bool _isReady;

    public AuthenticationBarrier(ILogger<AuthenticationBarrier>? logger = null)
    {
        _logger = logger;
        _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public bool IsReady => _isReady;

    /// <summary>
    /// Waits until <see cref="SetReady"/> is called. Returns immediately if already ready.
    /// </summary>
    public async Task WaitForReadyAsync(CancellationToken cancellationToken = default)
    {
        if (_isReady)
        {
            _logger?.LogDebug("Authentication already ready — no wait needed");
            return;
        }

        _logger?.LogDebug("Waiting for authentication to be ready...");

        // Snapshot the TCS so a concurrent Reset() can't swap it out while we await.
        var tcs = _tcs;

        using (cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken)))
        {
            await tcs.Task.ConfigureAwait(false);
        }

        _logger?.LogDebug("Authentication ready — proceeding");
    }

    /// <summary>Signals that authentication is ready. All waiters are released.</summary>
    public void SetReady()
    {
        if (_isReady)
        {
            _logger?.LogDebug("Authentication already marked ready");
            return;
        }

        _isReady = true;
        _logger?.LogInformation("Authentication marked ready — releasing barrier");
        _tcs.TrySetResult(true);
    }

    /// <summary>Resets the barrier for a new circuit. Cancels any in-flight waiters.</summary>
    public void Reset()
    {
        _logger?.LogDebug("Resetting authentication barrier");

        // Cancel any waiters on the previous TCS so they don't hang forever.
        _tcs.TrySetCanceled();

        _isReady = false;
        _tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
