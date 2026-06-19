// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.


namespace Asdamir.Web.Security.Http;

/// <summary>
/// Provides a per-execution-flow CircuitId via AsyncLocal.
/// This allows HttpClient handlers created outside the Blazor circuit DI scope
/// (e.g., IHttpClientFactory handler scope) to still resolve the current circuit.
/// </summary>
public static class CircuitExecutionContext
{
    private static readonly AsyncLocal<string?> _currentCircuitId = new();

    public static string? CurrentCircuitId
    {
        get => _currentCircuitId.Value;
        set => _currentCircuitId.Value = value;
    }
}
