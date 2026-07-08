// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Web.Security.Attributes;

/// <summary>
/// Declaratively caps how many times a page or method may be invoked within a rolling time window.
/// Applied to a Blazor page or an action to throttle abusive or accidental request bursts.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = true)]
public sealed class RateLimitAttribute : Attribute
{
    /// <summary>The maximum number of allowed invocations within <see cref="WindowSeconds"/>.</summary>
    public int Limit { get; }

    /// <summary>The length of the rolling rate-limit window, in seconds.</summary>
    public int WindowSeconds { get; }

    /// <summary>
    /// Initializes the attribute with a request cap and its time window.
    /// </summary>
    /// <param name="limit">The maximum number of invocations permitted in the window.</param>
    /// <param name="windowSeconds">The window length in seconds over which <paramref name="limit"/> applies.</param>
    public RateLimitAttribute(int limit, int windowSeconds)
    {
        Limit = limit;
        WindowSeconds = windowSeconds;
    }
}


