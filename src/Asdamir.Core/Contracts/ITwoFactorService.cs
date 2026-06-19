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
/// Service for two-factor authentication operations
/// </summary>
public interface ITwoFactorService
{
    /// <summary>
    /// Generate and send OTP code to user's phone
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> SendCodeAsync(int userId, string phoneNumber, string? culture = null, string? ipAddress = null, string? userAgent = null);

    /// <summary>
    /// Verify OTP code entered by user
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> VerifyCodeAsync(int userId, string code);

    /// <summary>
    /// Check if user can request new code (cooldown check)
    /// </summary>
    Task<(bool CanRequest, int SecondsRemaining)> CanRequestNewCodeAsync(int userId);

    /// <summary>
    /// Invalidate all active codes for user (e.g., on logout)
    /// </summary>
    Task InvalidateUserCodesAsync(int userId);
}



