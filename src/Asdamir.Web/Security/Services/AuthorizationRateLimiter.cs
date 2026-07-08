// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Microsoft.Extensions.Caching.Memory;

namespace Asdamir.Web.Security.Services;

/// <summary>
/// Sliding-window authorization failure rate limiter backed by IMemoryCache.
///
/// Audit fixes vs. v1:
/// - No more process-wide static dictionaries that grew unbounded. IMemoryCache enforces SizeLimit.
/// - All read/modify/write operations on the per-user attempt list are guarded by the same lock
///   (v1 reassigned the list outside the lock, racing with RecordFailureAsync).
/// - Configurable via constructor — values were previously hardcoded.
/// </summary>
public class AuthorizationRateLimiter : IAuthorizationRateLimiter
{
    private const int DefaultMaxFailuresPerWindow = 5;
    private const int DefaultWindowMinutes = 15;
    private const int DefaultLockoutMinutes = 30;
    private const int DefaultSizeLimit = 100_000;

    private readonly ILogger<AuthorizationRateLimiter> _logger;
    private readonly IMemoryCache _attemptsCache;
    private readonly IMemoryCache _lockoutCache;
    private readonly int _maxFailuresPerWindow;
    private readonly TimeSpan _window;
    private readonly TimeSpan _lockout;
    private readonly object _gate = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationRateLimiter"/> class with configurable window and lockout thresholds.
    /// </summary>
    /// <param name="logger">Logger for rate-limit and lockout diagnostics.</param>
    /// <param name="maxFailuresPerWindow">Number of authorization failures within the window that triggers a lockout.</param>
    /// <param name="windowMinutes">Length of the sliding failure-counting window, in minutes.</param>
    /// <param name="lockoutMinutes">Duration a user remains locked out after exceeding the threshold, in minutes.</param>
    public AuthorizationRateLimiter(
        ILogger<AuthorizationRateLimiter> logger,
        int maxFailuresPerWindow = DefaultMaxFailuresPerWindow,
        int windowMinutes = DefaultWindowMinutes,
        int lockoutMinutes = DefaultLockoutMinutes)
    {
        _logger = logger;
        _maxFailuresPerWindow = maxFailuresPerWindow;
        _window = TimeSpan.FromMinutes(windowMinutes);
        _lockout = TimeSpan.FromMinutes(lockoutMinutes);
        _attemptsCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = DefaultSizeLimit });
        _lockoutCache = new MemoryCache(new MemoryCacheOptions { SizeLimit = DefaultSizeLimit });
    }

    /// <inheritdoc/>
    public Task<bool> IsRateLimitExceededAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return Task.FromResult(false);

        try
        {
            if (_lockoutCache.TryGetValue<DateTime>(userId, out var lockoutUntil))
            {
                if (DateTime.UtcNow < lockoutUntil)
                {
                    _logger.LogWarning("User {UserId} locked out until {LockoutTime}", userId, lockoutUntil);
                    return Task.FromResult(true);
                }
                _lockoutCache.Remove(userId);
            }

            lock (_gate)
            {
                if (!_attemptsCache.TryGetValue<List<DateTime>>(userId, out var attempts) || attempts is null)
                    return Task.FromResult(false);

                var windowStart = DateTime.UtcNow - _window;
                var recent = attempts.Where(t => t >= windowStart).ToList();

                // Persist trimmed list under the same lock that produced it.
                _attemptsCache.Set(userId, recent, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _window,
                    Size = 1
                });

                var exceeded = recent.Count >= _maxFailuresPerWindow;
                if (exceeded)
                {
                    _logger.LogWarning(
                        "Rate limit exceeded for user {UserId} — {Count} failures in {Window} minutes",
                        userId, recent.Count, _window.TotalMinutes);
                }
                return Task.FromResult(exceeded);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking rate limit for user {UserId}", userId);
            return Task.FromResult(false); // don't block on internal error
        }
    }

    /// <inheritdoc/>
    public Task RecordFailureAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return Task.CompletedTask;

        try
        {
            lock (_gate)
            {
                if (!_attemptsCache.TryGetValue<List<DateTime>>(userId, out var attempts) || attempts is null)
                    attempts = new List<DateTime>();

                attempts.Add(DateTime.UtcNow);

                var windowStart = DateTime.UtcNow - _window;
                attempts = attempts.Where(t => t >= windowStart).ToList();

                _attemptsCache.Set(userId, attempts, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _window,
                    Size = 1
                });

                if (attempts.Count >= _maxFailuresPerWindow)
                {
                    var lockoutUntil = DateTime.UtcNow.Add(_lockout);
                    _lockoutCache.Set(userId, lockoutUntil, new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = _lockout,
                        Size = 1
                    });
                    _logger.LogWarning(
                        "User {UserId} locked out until {LockoutTime} after {Count} failures",
                        userId, lockoutUntil, attempts.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording authorization failure for user {UserId}", userId);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ResetAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return Task.CompletedTask;
        lock (_gate)
        {
            _attemptsCache.Remove(userId);
            _lockoutCache.Remove(userId);
        }
        _logger.LogInformation("Rate limit reset for user {UserId}", userId);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<TimeSpan> GetRemainingLockoutTimeAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return Task.FromResult(TimeSpan.Zero);
        if (_lockoutCache.TryGetValue<DateTime>(userId, out var lockoutUntil))
        {
            var remaining = lockoutUntil - DateTime.UtcNow;
            return Task.FromResult(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
        }
        return Task.FromResult(TimeSpan.Zero);
    }
}
