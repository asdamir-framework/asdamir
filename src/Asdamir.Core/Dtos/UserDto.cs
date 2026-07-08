// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.ComponentModel.DataAnnotations;

namespace Asdamir.Core.Dtos;

/// <summary>Read model for a user record from the central <c>dbo.Users</c> table (AsdamirVault, AppId-scoped); returned by the users list/detail endpoints and carried across tiers.</summary>
public class UserDto
{
    /// <summary>Surrogate primary key of the user row in <c>dbo.Users</c>.</summary>
    public int Id { get; set; }
    /// <summary>Login email; also the unique sign-in identifier for the user.</summary>
    public string Email { get; set; } = string.Empty;
    /// <summary>Display name shown in the console and app UIs.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>PBKDF2-SHA256 hash of the password — never the plaintext; write-path only.</summary>
    public string PasswordHash { get; set; } = string.Empty;
    /// <summary>Whether the account may sign in; false disables login without deleting the record.</summary>
    public bool IsActive { get; set; }
    /// <summary>UTC timestamp when the user was first created.</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>UTC timestamp of the last successful login; null if the user has never signed in.</summary>
    public DateTime? LastLoginAt { get; set; }
    /// <summary>UTC timestamp of the most recent update to the record; null if never modified since creation.</summary>
    public DateTime? UpdatedAt { get; set; }
    /// <summary>Free-text operator notes about the account; null when none.</summary>
    public string? Notes { get; set; }
    /// <summary>RBAC role name assigned to the user; defaults to "User".</summary>
    public string Role { get; set; } = "User";
    /// <summary>Effective permission strings granted to the user, resolved from the role and any direct grants.</summary>
    public List<string> Permissions { get; set; } = new();
    /// <summary>True for a platform operator with cross-app privileges (paired with a NULL AppId); false for an app-scoped end-user.</summary>
    public bool IsSuperAdmin { get; set; }

    // 2FA fields
    /// <summary>Whether phone/OTP two-factor authentication is required at login for this user.</summary>
    public bool IsTwoFactorEnabled { get; set; }
    /// <summary>E.164 phone number used to deliver the 2FA one-time code; null if not set.</summary>
    public string? PhoneNumber { get; set; }
    /// <summary>Whether <see cref="PhoneNumber"/> has been confirmed via a verification code.</summary>
    public bool PhoneNumberVerified { get; set; }

    // Company mapping
    /// <summary>Id of the company (firma) the user belongs to; null for users not scoped to a single company.</summary>
    public Guid? CompanyId { get; set; }
    /// <summary>Denormalized display name of the mapped company; null when unmapped.</summary>
    public string? CompanyName { get; set; }

    // Service Points mapping
    /// <summary>Ids of the service points (branches) this user is scoped to; empty means no branch restriction.</summary>
    public List<Guid> ServicePointIds { get; set; } = new();
}

/// <summary>Request body for creating a new user in <c>dbo.Users</c> (AppId-scoped).</summary>
/// <param name="Email">Login email; must be unique — becomes the sign-in identifier.</param>
/// <param name="Name">Display name for the new user.</param>
/// <param name="Password">Plaintext password to hash on the server; never stored as-is.</param>
/// <param name="Role">RBAC role to assign at creation (e.g. "User", "Admin").</param>
/// <param name="Permissions">Optional direct permission grants beyond the role; null means role-only.</param>
/// <param name="IsActive">Whether the account is enabled for login immediately.</param>
/// <param name="Notes">Optional free-text operator notes; null when none.</param>
/// <param name="IsTwoFactorEnabled">Whether to require phone/OTP two-factor at login.</param>
/// <param name="PhoneNumber">E.164 phone for 2FA delivery (validated as a phone, max 25 chars); null if unset.</param>
/// <param name="PhoneNumberVerified">Whether the phone is pre-marked as verified at creation.</param>
/// <param name="CompanyId">Company (firma) to scope the user to; null for no company scope.</param>
/// <param name="ServicePointIds">Service points (branches) to scope the user to; null/empty means no branch restriction.</param>
/// <param name="CreatedBy">Identifier of the operator performing the creation, for audit; null if unattributed.</param>
public record CreateUserRequestDto(
    string Email,
    string Name,
    string Password,
    string Role,
    List<string>? Permissions,
    bool IsActive,
    string? Notes,
    bool IsTwoFactorEnabled = false,
    [Phone]
    [StringLength(25)]
    string? PhoneNumber = null,
    bool PhoneNumberVerified = false,
    Guid? CompanyId = null,
    List<Guid>? ServicePointIds = null,
    string? CreatedBy = null
);

/// <summary>Request body for updating an existing user's profile, role, 2FA and scoping (password is changed via the reset flows, not here).</summary>
/// <param name="Email">Login email; changing it changes the sign-in identifier and must stay unique.</param>
/// <param name="Name">Updated display name.</param>
/// <param name="Role">Updated RBAC role name.</param>
/// <param name="Permissions">Replacement set of direct permission grants; null leaves role-only.</param>
/// <param name="IsActive">Whether the account remains enabled for login.</param>
/// <param name="Notes">Updated operator notes; null clears/omits them.</param>
/// <param name="IsTwoFactorEnabled">Whether phone/OTP two-factor is required at login.</param>
/// <param name="PhoneNumber">E.164 phone for 2FA (validated as a phone, max 25 chars); null if unset.</param>
/// <param name="PhoneNumberVerified">Whether the phone is marked verified.</param>
/// <param name="CompanyId">Company (firma) scope; null for no company scope.</param>
/// <param name="ServicePointIds">Service points (branches) to scope the user to; null/empty means no branch restriction.</param>
public record UpdateUserRequestDto(
    string Email,
    string Name,
    string Role,
    List<string>? Permissions,
    bool IsActive,
    string? Notes,
    bool IsTwoFactorEnabled = false,
    [Phone]
    [StringLength(25)]
    string? PhoneNumber = null,
    bool PhoneNumberVerified = false,
    Guid? CompanyId = null,
    List<Guid>? ServicePointIds = null
);

/// <summary>Request body for an operator-initiated password reset (sets the password directly, no token).</summary>
/// <param name="NewPassword">Plaintext password to hash and store for the target user.</param>
public record ResetPasswordRequestDto(string NewPassword);

/// <summary>Request body for the "forgot password" flow — triggers a reset-link email to the address.</summary>
/// <param name="Email">Address to send the reset link to.</param>
/// <param name="Language">Optional culture (e.g. "tr-TR") for the email; null uses the request/default culture.</param>
public record ForgotPasswordRequestDto(string Email, string? Language = null);

/// <summary>Response for the "forgot password" request — reports acceptance without leaking whether the email exists.</summary>
/// <param name="Success">Whether the request was accepted for processing.</param>
/// <param name="Message">Localized user-facing status message.</param>
public record ForgotPasswordResponseDto(bool Success, string Message);

/// <summary>Request body to complete a password reset using a one-time token from the reset email.</summary>
/// <param name="Token">The single-use reset token issued by the forgot-password flow.</param>
/// <param name="NewPassword">Plaintext new password to hash and store once the token is validated.</param>
public record ResetPasswordWithTokenRequestDto(string Token, string NewPassword);

/// <summary>Response for a token-based password reset — reports whether the password was changed.</summary>
/// <param name="Success">True when the token was valid and the password was updated.</param>
/// <param name="Message">Localized user-facing status message.</param>
public record ResetPasswordWithTokenResponseDto(bool Success, string Message);

/// <summary>Request body to check a reset token before showing the new-password form.</summary>
/// <param name="Token">The reset token to validate.</param>
public record ValidateResetTokenRequestDto(string Token);

/// <summary>Response reporting whether a reset token is still valid and whom it belongs to.</summary>
/// <param name="IsValid">True if the token exists and has not expired or been used.</param>
/// <param name="Email">Email the token was issued for when valid; null otherwise (never leaked for an invalid token).</param>
public record ValidateResetTokenResponseDto(bool IsValid, string? Email);
