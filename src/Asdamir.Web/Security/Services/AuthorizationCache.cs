// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.MultiTenancy;
using Asdamir.Web.Security.Models;
using Microsoft.Extensions.Caching.Memory;

namespace Asdamir.Web.Security.Services;

/// <summary>
/// Authorization cache backed by IMemoryCache with a hard size limit.
///
/// Audit fixes vs. v1:
/// - Eliminates the process-wide static ConcurrentDictionary that grew unbounded.
/// - Includes <c>tenantId</c> in the cache key (resolved via <see cref="ITenantContext"/>) so
///   identical userIds across tenants never collide.
/// - Normalizes the route key by stripping query string and fragment — different query
///   parameters for the same page no longer create separate cache entries that never expire.
/// - SizeLimit bounds the cache; entries are evicted by LRU automatically.
/// </summary>
public class AuthorizationCache : IAuthorizationCache, IDisposable
{
    private const int DefaultSizeLimit = 10_000;
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);

    private readonly ILogger<AuthorizationCache> _logger;
    private readonly MemoryCache _cache;
    private readonly ITenantContext? _tenant;
    private readonly Timer _sweeper;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationCache"/> class with a size-bounded memory cache and a periodic TTL sweeper.
    /// </summary>
    /// <param name="logger">Logger for cache hit/miss/eviction diagnostics.</param>
    /// <param name="tenant">Optional tenant context; when present its tenant id is included in every cache key to prevent cross-tenant collisions.</param>
    public AuthorizationCache(ILogger<AuthorizationCache> logger, ITenantContext? tenant = null)
    {
        _logger = logger;
        _tenant = tenant;
        _cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = DefaultSizeLimit,
            CompactionPercentage = 0.1
        });

        // Audit fix: MemoryCache only evicts on access. If an authorization result
        // is set and the user never returns, the entry stays until SizeLimit pressure
        // triggers compaction. A periodic 1% compact prods expired entries out and
        // keeps the working set tight.
        _sweeper = new Timer(_ => SafeSweep(), state: null, SweepInterval, SweepInterval);
    }

    private void SafeSweep()
    {
        try
        {
            _cache.Compact(0.01); // 1% — cheap; only meant to expire stale TTL entries
        }
        catch (ObjectDisposedException)
        {
            // Sweeper fired after Dispose — ignore.
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "AuthZ cache sweep failed");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _sweeper.Dispose();
        _cache.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc/>
    public Task<AuthorizationResult?> GetAsync(string userId, string route)
    {
        var key = GetCacheKey(userId, route);
        if (_cache.TryGetValue<AuthorizationResult>(key, out var result))
        {
            _logger.LogDebug("AuthZ cache HIT for {Key}", key);
            return Task.FromResult<AuthorizationResult?>(result);
        }
        _logger.LogDebug("AuthZ cache MISS for {Key}", key);
        return Task.FromResult<AuthorizationResult?>(null);
    }

    /// <inheritdoc/>
    public Task SetAsync(string userId, string route, AuthorizationResult result, TimeSpan? expiration = null)
    {
        var key = GetCacheKey(userId, route);
        var entryOpts = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiration ?? DefaultExpiration,
            Size = 1, // each entry counts as 1 unit against SizeLimit
            Priority = CacheItemPriority.Normal
        };
        _cache.Set(key, result, entryOpts);
        _logger.LogDebug("AuthZ cache SET for {Key}", key);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task InvalidateUserAsync(string userId)
    {
        // IMemoryCache doesn't expose key enumeration; we use a CancellationTokenSource per user
        // to bulk-invalidate. For simplicity we rely on absolute expiration here — explicit
        // invalidation by user is provided only for the current scope; cross-process invalidation
        // requires a distributed cache (out of scope for this audit fix).
        // Workaround: clear the entire cache. Coarse but safe.
        if (_cache is MemoryCache mc)
        {
            mc.Compact(1.0); // evict 100% — pragmatic; if this becomes a hot path, introduce CTS-based eviction
        }
        _logger.LogInformation("AuthZ cache cleared on user {UserId} invalidation", userId);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task InvalidateRouteAsync(string route)
    {
        if (_cache is MemoryCache mc) mc.Compact(1.0);
        _logger.LogInformation("AuthZ cache cleared on route {Route} invalidation", route);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ClearAllAsync()
    {
        if (_cache is MemoryCache mc) mc.Compact(1.0);
        _logger.LogInformation("AuthZ cache cleared (ClearAllAsync)");
        return Task.CompletedTask;
    }

    private string GetCacheKey(string userId, string route)
    {
        var tenant = _tenant?.TenantId ?? "default";
        return $"{tenant}:{userId}:{NormalizeRoute(route)}";
    }

    private static string NormalizeRoute(string route)
    {
        if (string.IsNullOrWhiteSpace(route)) return string.Empty;

        // Strip query string and fragment — two visits to /users with different filters share
        // the same authorization decision.
        var queryIndex = route.IndexOf('?');
        if (queryIndex >= 0) route = route[..queryIndex];
        var fragIndex = route.IndexOf('#');
        if (fragIndex >= 0) route = route[..fragIndex];

        // If absolute URL, strip the scheme+host so different host bindings don't fragment cache.
        if (Uri.TryCreate(route, UriKind.Absolute, out var abs))
            route = abs.AbsolutePath;

        return route.TrimEnd('/').ToLowerInvariant();
    }
}
