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
    /// <summary>
    /// A single stored token bound to one Blazor circuit. Holds the sensitive bearer
    /// <see cref="AccessToken"/> in memory only — never persist or log this value.
    /// </summary>
    /// <param name="CircuitId">Identifier of the Blazor circuit that owns this token; the dictionary key.</param>
    /// <param name="AccessToken">The bearer/JWT access token for the circuit. Sensitive — do not log or serialize.</param>
    /// <param name="UserId">Identifier of the authenticated subject, used for user-wide invalidation; may be null.</param>
    /// <param name="DisplayName">Optional display name of the subject, for diagnostics only.</param>
    /// <param name="CreatedAtUtc">UTC time the entry was first created.</param>
    /// <param name="LastUsedUtc">UTC time of the last activity; the idle sweeper evicts entries older than the TTL.</param>
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

    /// <summary>
    /// Stores the token for a circuit, replacing any existing entry for the same circuit.
    /// Resets both the created and last-used timestamps to now.
    /// </summary>
    /// <param name="circuitId">The circuit to store the token under.</param>
    /// <param name="accessToken">The bearer/JWT access token. Sensitive — held in memory only.</param>
    /// <param name="userId">Identifier of the authenticated subject, for later user-wide invalidation.</param>
    /// <param name="displayName">Optional display name of the subject.</param>
    /// <returns>The newly stored entry.</returns>
    public static TokenEntry SetOrReplace(string circuitId, string accessToken, string? userId, string? displayName)
    {
        var now = DateTime.UtcNow;
        var entry = new TokenEntry(circuitId, accessToken, userId, displayName, now, now);
        _entries.AddOrUpdate(circuitId, entry, (_, _) => entry);
        return entry;
    }

    /// <summary>
    /// Attempts to retrieve the stored token entry for a circuit. Does not update the last-used
    /// timestamp — call <c>Touch</c> to keep an active session alive.
    /// </summary>
    /// <param name="circuitId">The circuit whose token is requested.</param>
    /// <param name="entry">On success, the stored entry; otherwise null.</param>
    /// <returns><c>true</c> if an entry exists for the circuit; otherwise <c>false</c>.</returns>
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

    /// <summary>
    /// Marks a circuit's entry as freshly used by updating its last-used timestamp to now, so the
    /// idle sweeper will not evict an active session. Call on each authenticated request.
    /// </summary>
    /// <param name="circuitId">The circuit to refresh.</param>
    /// <returns><c>true</c> if the circuit had an entry to refresh; otherwise <c>false</c>.</returns>
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

    /// <summary>
    /// Removes the token entry for a circuit — call when the circuit disconnects or the user logs
    /// out so the sensitive token is dropped promptly rather than waiting for the idle sweeper.
    /// </summary>
    /// <param name="circuitId">The circuit whose token should be discarded.</param>
    /// <returns><c>true</c> if an entry was removed; otherwise <c>false</c>.</returns>
    public static bool Remove(string circuitId)
    {
        return _entries.TryRemove(circuitId, out _);
    }

    /// <summary>
    /// Removes every stored token belonging to a given user across all circuits — the mechanism
    /// for forcing a subject to be signed out everywhere (e.g. on password change or revocation).
    /// </summary>
    /// <param name="userId">The subject whose tokens should be invalidated. A null/blank value is a no-op.</param>
    /// <returns>The number of entries removed.</returns>
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

    /// <summary>
    /// Returns a point-in-time copy of all stored entries, for diagnostics/monitoring. The copy
    /// contains the sensitive access tokens — do not log or expose it.
    /// </summary>
    /// <returns>A snapshot array of the current entries.</returns>
    public static IReadOnlyCollection<TokenEntry> GetSnapshot()
    {
        return _entries.Values.ToArray();
    }

    /// <summary>
    /// Removes all stored tokens. Intended for shutdown and tests; clearing at runtime effectively
    /// signs out every active circuit.
    /// </summary>
    public static void Clear()
    {
        _entries.Clear();
    }

    // ===== Legacy API for backward compatibility =====
    // These methods are deprecated and will be removed in future versions

    /// <summary>
    /// Deprecated. Stores a token for a circuit; kept for backward compatibility — prefer <c>SetOrReplace</c>.
    /// </summary>
    /// <param name="circuitId">The circuit to store the token under.</param>
    /// <param name="accessToken">The bearer/JWT access token. Sensitive — held in memory only.</param>
    /// <param name="userId">Optional identifier of the authenticated subject.</param>
    /// <param name="displayName">Optional display name of the subject.</param>
    public static void SetToken(string circuitId, string accessToken, string? userId = null, string? displayName = null)
    {
        SetOrReplace(circuitId, accessToken, userId, displayName);
    }

    /// <summary>
    /// Deprecated. Returns just the access token for a circuit; kept for backward compatibility — prefer <c>TryGet</c>.
    /// </summary>
    /// <param name="circuitId">The circuit whose token is requested.</param>
    /// <returns>The stored access token, or null if none exists for the circuit.</returns>
    public static string? GetToken(string circuitId)
    {
        return TryGet(circuitId, out var entry) ? entry.AccessToken : null;
    }

    /// <summary>
    /// Deprecated. Removes a circuit's token; kept for backward compatibility — prefer <c>Remove</c>.
    /// </summary>
    /// <param name="circuitId">The circuit whose token should be discarded.</param>
    public static void RemoveToken(string circuitId)
    {
        Remove(circuitId);
    }

    /// <summary>
    /// Deprecated. Returns a map of every circuit to its access token; kept for backward
    /// compatibility — prefer <c>GetSnapshot</c>. The result contains sensitive tokens — do not log it.
    /// </summary>
    /// <returns>A copy mapping circuit id to access token.</returns>
    public static Dictionary<string, string> GetAllTokens()
    {
        return _entries.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.AccessToken);
    }

    private static readonly object _lock = new();

    /// <summary>
    /// Deprecated. Returns the legacy shared lock object; kept for backward compatibility. The
    /// store itself is backed by a concurrent dictionary and does not require external locking.
    /// </summary>
    /// <returns>The shared lock instance.</returns>
    public static object GetLockObject() => _lock;
}
