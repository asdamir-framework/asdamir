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
/// Drives the forgot/reset-password flow from the UI tier by calling the Gateway. Reset tokens are
/// single-use and time-limited: a token is issued on request, validated before the form is shown, and
/// consumed when the new password is set.
/// </summary>
public interface IPasswordResetService
{
    /// <summary>Requests a password-reset email for the given address (does not reveal whether the account exists).</summary>
    /// <param name="email">The account email to send the reset link to.</param>
    /// <returns>Success flag and a user-facing message describing the outcome.</returns>
    Task<(bool Success, string Message)> RequestPasswordResetAsync(string email);
    /// <summary>Checks whether a reset token is still valid (unused and unexpired) before presenting the reset form.</summary>
    /// <param name="token">The reset token from the emailed link.</param>
    /// <returns>Validity flag and, when valid, the associated account email.</returns>
    Task<(bool IsValid, string? Email)> ValidateResetTokenAsync(string token);
    /// <summary>Consumes a valid reset token to set a new password; the token cannot be reused afterwards.</summary>
    /// <param name="token">The single-use reset token.</param>
    /// <param name="newPassword">The new password to apply.</param>
    /// <returns>Success flag and a user-facing message describing the outcome.</returns>
    Task<(bool Success, string Message)> ResetPasswordAsync(string token, string newPassword);
}
