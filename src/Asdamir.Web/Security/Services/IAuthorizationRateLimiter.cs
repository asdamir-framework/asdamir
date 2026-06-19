// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Web.Security.Services;

/// <summary>
/// Service for rate limiting authorization attempts to prevent brute force attacks
/// </summary>
public interface IAuthorizationRateLimiter
{
    /// <summary>
    /// Checks if user has exceeded authorization failure rate limit
    /// </summary>
    Task<bool> IsRateLimitExceededAsync(string userId);

    /// <summary>
    /// Records an authorization failure
    /// </summary>
    Task RecordFailureAsync(string userId);

    /// <summary>
    /// Resets rate limit for a user (after successful authorization)
    /// </summary>
    Task ResetAsync(string userId);

    /// <summary>
    /// Gets remaining time until rate limit is reset
    /// </summary>
    Task<TimeSpan> GetRemainingLockoutTimeAsync(string userId);
}
