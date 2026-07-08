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

namespace Asdamir.Web.Security.Http;

/// <summary>
/// Thread-safe accessor for circuit-scoped services
/// Allows DelegatingHandlers (transient) to access circuit-specific data
/// </summary>
public sealed class CircuitServicesAccessor
{
    private static readonly ConcurrentDictionary<string, CircuitContext> _contexts = new();
    private static readonly AsyncLocal<string?> _currentCircuitId = new();

    /// <summary>
    /// The circuit id flowing with the current execution context (via <see cref="AsyncLocal{T}"/>),
    /// used to look up the active circuit from code outside the circuit's DI scope.
    /// </summary>
    public static string? CurrentCircuitId
    {
        get => _currentCircuitId.Value;
        set => _currentCircuitId.Value = value;
    }

    /// <summary>
    /// Registers (or replaces) the context for a circuit so handlers can later resolve its data by id.
    /// </summary>
    /// <param name="circuitId">The Blazor circuit identifier.</param>
    /// <param name="context">The context to associate with the circuit.</param>
    public static void RegisterCircuit(string circuitId, CircuitContext context)
    {
        _contexts[circuitId] = context;
    }

    /// <summary>
    /// Removes a circuit's context, typically when the circuit is torn down or on logout.
    /// </summary>
    /// <param name="circuitId">The Blazor circuit identifier to remove.</param>
    public static void UnregisterCircuit(string circuitId)
    {
        _contexts.TryRemove(circuitId, out _);
    }

    /// <summary>
    /// Resolves the context for a specific circuit.
    /// </summary>
    /// <param name="circuitId">The circuit identifier; may be null or empty.</param>
    /// <returns>The registered <see cref="CircuitContext"/>, or <c>null</c> if the id is empty or unknown.</returns>
    public static CircuitContext? GetContext(string? circuitId)
    {
        if (string.IsNullOrEmpty(circuitId))
            return null;

        return _contexts.TryGetValue(circuitId, out var context) ? context : null;
    }

    /// <summary>
    /// Resolves the context for the circuit flowing with the current execution context
    /// (<see cref="CurrentCircuitId"/>).
    /// </summary>
    /// <returns>The current <see cref="CircuitContext"/>, or <c>null</c> if none is active.</returns>
    public static CircuitContext? GetCurrentContext()
    {
        var circuitId = CurrentCircuitId;
        return GetContext(circuitId);
    }
}

/// <summary>
/// Context information for a specific circuit
/// </summary>
public sealed class CircuitContext
{
    /// <summary>The Blazor circuit identifier this context belongs to.</summary>
    public string CircuitId { get; init; } = string.Empty;

    /// <summary>The bearer access token associated with the circuit, attached to outbound calls.</summary>
    public string? AccessToken { get; set; }

    /// <summary>The UTC timestamp at which this context was created.</summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
