// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Web.Security.Models;

namespace Asdamir.Web.Security.Services;

/// <summary>
/// Service for caching authorization results to improve performance
/// </summary>
public interface IAuthorizationCache
{
    /// <summary>
    /// Gets cached authorization result
    /// </summary>
    Task<AuthorizationResult?> GetAsync(string userId, string route);

    /// <summary>
    /// Caches authorization result
    /// </summary>
    Task SetAsync(string userId, string route, AuthorizationResult result, TimeSpan? expiration = null);

    /// <summary>
    /// Invalidates cache for a user
    /// </summary>
    Task InvalidateUserAsync(string userId);

    /// <summary>
    /// Invalidates cache for a route
    /// </summary>
    Task InvalidateRouteAsync(string route);

    /// <summary>
    /// Clears all cached authorization results
    /// </summary>
    Task ClearAllAsync();
}
