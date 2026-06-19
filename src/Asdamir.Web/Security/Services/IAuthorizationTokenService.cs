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
/// Service for managing authorization tokens including expiration checks and refresh
/// </summary>
public interface IAuthorizationTokenService
{
    /// <summary>
    /// Checks if the current token is expired or about to expire
    /// </summary>
    /// <param name="expirationThresholdMinutes">Minutes before expiration to consider token as expiring</param>
    Task<bool> IsTokenExpiredOrExpiringAsync(int expirationThresholdMinutes = 5);

    /// <summary>
    /// Attempts to refresh the access token using the refresh token
    /// </summary>
    /// <returns>True if refresh was successful, false otherwise</returns>
    Task<bool> TryRefreshTokenAsync();

    /// <summary>
    /// Gets the token expiration time
    /// </summary>
    Task<DateTime?> GetTokenExpirationAsync();

    /// <summary>
    /// Performs a CLIENT-SIDE structural sanity check on the JWT — i.e. it parses,
    /// has not visibly expired, and has the expected user-identifier claim.
    ///
    /// IMPORTANT: this method does NOT verify the signature, audience, issuer,
    /// or any other authoritative claim. A token whose signature was tampered
    /// with will still return <c>true</c>. The Gateway is the only authority
    /// that can validate a token — this method exists only to short-circuit
    /// obviously-bad tokens on the client before sending them on the wire.
    ///
    /// The v1 name <c>ValidateTokenAsync</c> implied a real validation and led
    /// callers to omit server-side checks. Renamed in the audit to make the
    /// contract unmistakable.
    /// </summary>
    Task<bool> IsStructurallyValidAsync();
}
