// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Web.Localization.Caching;

/// <summary>
/// Cache generation tracker for invalidation
/// </summary>
public interface ILocalizationCacheGeneration
{
    /// <summary>
    /// Current cache generation number
    /// </summary>
    int Current { get; }
    
    /// <summary>
    /// Increment generation to invalidate all caches
    /// </summary>
    void Bump();
}

/// <summary>
/// Thread-safe cache generation implementation
/// </summary>
public sealed class LocalizationCacheGeneration : ILocalizationCacheGeneration
{
    private int _generation = 1;

    /// <inheritdoc/>
    public int Current => Volatile.Read(ref _generation);

    /// <inheritdoc/>
    public void Bump() => Interlocked.Increment(ref _generation);
}
