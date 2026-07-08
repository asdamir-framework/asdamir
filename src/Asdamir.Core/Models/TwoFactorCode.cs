// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Shared.Models;

/// <summary>
/// Two-factor authentication code model
/// </summary>
public class TwoFactorCode
{
    /// <summary>Primary key of the OTP row.</summary>
    public int Id { get; set; }

    /// <summary>Owning user; the code is only valid for this user's pending login challenge.</summary>
    public int UserId { get; set; }

    /// <summary>The one-time OTP value sent to the user; single-use and short-lived (see <see cref="ExpiresAtUtc"/>).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Phone number the code was delivered to, recorded so delivery can be audited.</summary>
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>UTC time the code was generated; the cooldown between resends is measured from here.</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>UTC time after which the code is rejected as expired.</summary>
    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>True once the code has been redeemed; a used code can never be accepted again.</summary>
    public bool IsUsed { get; set; }

    /// <summary>UTC time the code was redeemed; null while still unused.</summary>
    public DateTime? UsedAtUtc { get; set; }

    /// <summary>Number of verification attempts; used to lock out brute-force guessing after a threshold.</summary>
    public int AttemptCount { get; set; }

    /// <summary>Client IP recorded at issuance for abuse detection and audit.</summary>
    public string? IpAddress { get; set; }

    /// <summary>Client user-agent recorded at issuance for abuse detection and audit.</summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Check if the code is still valid (not expired and not used)
    /// </summary>
    public bool IsValid => !IsUsed && DateTime.UtcNow < ExpiresAtUtc;

    /// <summary>
    /// Check if the code has expired
    /// </summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAtUtc;
}
