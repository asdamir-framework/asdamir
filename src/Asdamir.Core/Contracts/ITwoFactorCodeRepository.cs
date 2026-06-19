// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Shared.Models;

namespace Asdamir.Core.Contracts;

/// <summary>
/// Repository interface for two-factor authentication codes
/// </summary>
public interface ITwoFactorCodeRepository
{
    /// <summary>
    /// Create a new two-factor authentication code
    /// </summary>
    Task<TwoFactorCode> CreateAsync(TwoFactorCode code);

    /// <summary>
    /// Get an active (not used, not expired) code by user ID
    /// </summary>
    Task<TwoFactorCode?> GetActiveCodeByUserIdAsync(int userId);

    /// <summary>
    /// Get a specific code by user ID and code value
    /// </summary>
    Task<TwoFactorCode?> GetByUserIdAndCodeAsync(int userId, string code);

    /// <summary>
    /// Get recent codes by user ID within specified minutes
    /// Used for cooldown period checking
    /// </summary>
    Task<List<TwoFactorCode>> GetRecentCodesByUserIdAsync(int userId, int withinMinutes);

    /// <summary>
    /// Increment attempt count for a code
    /// </summary>
    Task IncrementAttemptCountAsync(int codeId);

    /// <summary>
    /// Mark a code as used
    /// </summary>
    Task MarkAsUsedAsync(int codeId);

    /// <summary>
    /// Invalidate all active codes for a user
    /// Sets IsUsed = true for all unused codes
    /// </summary>
    Task InvalidateUserCodesAsync(int userId);

    /// <summary>
    /// Get the most recent code for a user (for cooldown checks)
    /// </summary>
    Task<TwoFactorCode?> GetMostRecentCodeByUserIdAsync(int userId);

    /// <summary>
    /// Delete expired codes (cleanup job)
    /// </summary>
    Task DeleteExpiredCodesAsync(int olderThanDays = 7);
}
