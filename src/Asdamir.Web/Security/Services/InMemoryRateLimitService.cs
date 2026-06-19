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
using System.Collections.Concurrent;

namespace Asdamir.Web.Security.Services;

/// <summary>
/// Simple per-key fixed-window rate limiter.
///
/// Audit fix: v1 ran TryGet → check Count → Increment without synchronization.
/// Under concurrent load N callers could each read Count = maxRequests-1 and all
/// increment to maxRequests, letting all of them through. That defeats the limiter
/// in exactly the burst case it exists to handle (login flood, OTP enumeration).
///
/// We now keep one SemaphoreSlim per key and serialize the read-decide-write inside
/// it. The semaphore dictionary is bounded by the cache TTL: when the counter
/// expires we remove the corresponding lock via PostEvictionCallback.
/// </summary>
public class InMemoryRateLimitService : IRateLimitService
{
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public InMemoryRateLimitService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public async Task<bool> TryAcquireAsync(string key, int maxRequests, TimeSpan window)
    {
        var cacheKey = $"rate_limit:{key}";
        var gate = _locks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var now = DateTimeOffset.UtcNow;

            if (_cache.TryGetValue(cacheKey, out RateLimitCounter? counter) && counter is not null)
            {
                if (now - counter.WindowStart > window)
                {
                    // Window expired — start fresh.
                    StoreCounter(cacheKey, new RateLimitCounter { WindowStart = now, Count = 1 }, window);
                    return true;
                }

                if (counter.Count >= maxRequests)
                {
                    return false;
                }

                counter.Count++;
                // Already in cache; AbsoluteExpiration was set when first inserted.
                return true;
            }

            StoreCounter(cacheKey, new RateLimitCounter { WindowStart = now, Count = 1 }, window);
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    public Task<RateLimitInfo> GetLimitInfoAsync(string key)
    {
        var cacheKey = $"rate_limit:{key}";

        if (_cache.TryGetValue(cacheKey, out RateLimitCounter? counter) && counter is not null)
        {
            var remaining = Math.Max(0, 100 - counter.Count);
            var resetTime = TimeSpan.FromMinutes(1) - (DateTimeOffset.UtcNow - counter.WindowStart);
            var isBlocked = counter.Count >= 100;

            return Task.FromResult(new RateLimitInfo(remaining, resetTime, isBlocked));
        }

        return Task.FromResult(new RateLimitInfo(100, TimeSpan.FromMinutes(1), false));
    }

    private void StoreCounter(string cacheKey, RateLimitCounter counter, TimeSpan window)
    {
        var entryOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = window,
        };
        entryOptions.RegisterPostEvictionCallback((evictedKey, _, _, _) =>
        {
            if (evictedKey is string sk && _locks.TryRemove(sk, out var semaphore))
            {
                semaphore.Dispose();
            }
        });
        _cache.Set(cacheKey, counter, entryOptions);
    }
}

internal class RateLimitCounter
{
    public DateTimeOffset WindowStart { get; set; }
    public int Count { get; set; }
}
