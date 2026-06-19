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

    public static string? CurrentCircuitId
    {
        get => _currentCircuitId.Value;
        set => _currentCircuitId.Value = value;
    }

    public static void RegisterCircuit(string circuitId, CircuitContext context)
    {
        _contexts[circuitId] = context;
    }

    public static void UnregisterCircuit(string circuitId)
    {
        _contexts.TryRemove(circuitId, out _);
    }

    public static CircuitContext? GetContext(string? circuitId)
    {
        if (string.IsNullOrEmpty(circuitId))
            return null;

        return _contexts.TryGetValue(circuitId, out var context) ? context : null;
    }

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
    public string CircuitId { get; init; } = string.Empty;
    public string? AccessToken { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
