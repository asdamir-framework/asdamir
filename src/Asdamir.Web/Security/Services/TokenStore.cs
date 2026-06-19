// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Asdamir.Web.Security.Services;

/// <summary>
/// Thread-safe per-circuit token store.
///
/// Audit fix: v1 leaked entries whenever a circuit died without firing its
/// Disconnect callback (browser crash, network drop, kill switch). A long-lived
/// process accumulated thousands of stale tokens; periodic snapshots got
/// progressively slower and the process held on to JWTs whose subject had
/// long since logged out.
///
/// The sweeper below removes any entry whose <c>LastUsedUtc</c> is older than
/// <see cref="IdleTtl"/>. Circuits that are still alive call <see cref="Touch"/>
/// on each authenticated request, so an active session never expires.
/// </summary>
public static class TokenStore
{
    public sealed record TokenEntry(
        string CircuitId,
        string AccessToken,
        string? UserId,
        string? DisplayName,
        DateTime CreatedAtUtc,
        DateTime LastUsedUtc);

    /// <summary>How long an unused entry is allowed to live before the sweeper removes it.</summary>
    private static readonly TimeSpan IdleTtl = TimeSpan.FromHours(2);

    /// <summary>How often the sweeper scans.</summary>
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);

    private static readonly ConcurrentDictionary<string, TokenEntry> _entries = new();
    private static readonly Timer _sweeper = new(_ => SafeSweep(), state: null, SweepInterval, SweepInterval);

    internal static int SafeSweep()
    {
        try
        {
            var cutoff = DateTime.UtcNow - IdleTtl;
            var removed = 0;
            foreach (var kvp in _entries)
            {
                if (kvp.Value.LastUsedUtc < cutoff)
                {
                    if (_entries.TryRemove(kvp.Key, out _)) removed++;
                }
            }
            return removed;
        }
        catch
        {
            // Sweeper must never throw on a Timer thread.
            return 0;
        }
    }

    /// <summary>Test/diagnostics hook: run a sweep immediately. Returns count removed.</summary>
    public static int ForceSweep() => SafeSweep();

    public static TokenEntry SetOrReplace(string circuitId, string accessToken, string? userId, string? displayName)
    {
        var now = DateTime.UtcNow;
        var entry = new TokenEntry(circuitId, accessToken, userId, displayName, now, now);
        _entries.AddOrUpdate(circuitId, entry, (_, _) => entry);
        return entry;
    }

    public static bool TryGet(string circuitId, [NotNullWhen(true)] out TokenEntry? entry)
    {
        if (_entries.TryGetValue(circuitId, out var existing))
        {
            entry = existing;
            return true;
        }

        entry = null;
        return false;
    }

    public static bool Touch(string circuitId)
    {
        if (_entries.TryGetValue(circuitId, out var existing))
        {
            var updated = existing with { LastUsedUtc = DateTime.UtcNow };
            _entries[circuitId] = updated;
            return true;
        }

        return false;
    }

    public static bool Remove(string circuitId)
    {
        return _entries.TryRemove(circuitId, out _);
    }

    public static int InvalidateByUser(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return 0;
        }

        var removed = 0;
        foreach (var kvp in _entries)
        {
            if (string.Equals(kvp.Value.UserId, userId, StringComparison.Ordinal))
            {
                if (_entries.TryRemove(kvp.Key, out _))
                {
                    removed++;
                }
            }
        }

        return removed;
    }

    public static IReadOnlyCollection<TokenEntry> GetSnapshot()
    {
        return _entries.Values.ToArray();
    }

    public static void Clear()
    {
        _entries.Clear();
    }

    // ===== Legacy API for backward compatibility =====
    // These methods are deprecated and will be removed in future versions

    public static void SetToken(string circuitId, string accessToken, string? userId = null, string? displayName = null)
    {
        SetOrReplace(circuitId, accessToken, userId, displayName);
    }

    public static string? GetToken(string circuitId)
    {
        return TryGet(circuitId, out var entry) ? entry.AccessToken : null;
    }

    public static void RemoveToken(string circuitId)
    {
        Remove(circuitId);
    }

    public static Dictionary<string, string> GetAllTokens()
    {
        return _entries.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.AccessToken);
    }

    private static readonly object _lock = new();
    public static object GetLockObject() => _lock;
}
