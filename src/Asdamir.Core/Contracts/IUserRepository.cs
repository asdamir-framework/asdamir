// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.Dtos;

namespace Asdamir.Core.Contracts;

/// <summary>
/// User identity + account store: lookup, CRUD, permission read, refresh-token and password-reset flows.
/// Implementations are tenant/app-scoped by the host; all methods operate on the resolved scope.
/// </summary>
public interface IUserRepository
{
    /// <summary>Finds a user by email address, or null if none matches.</summary>
    Task<UserDto?> GetByEmailAsync(string email);
    /// <summary>Lists all users in the current scope.</summary>
    Task<List<UserDto>> ListAsync();
    /// <summary>Finds a user by id, or null if not found.</summary>
    Task<UserDto?> GetAsync(int id);
    /// <summary>Creates a new user from the request and returns the created record.</summary>
    Task<UserDto> CreateAsync(CreateUserRequestDto request);
    /// <summary>Updates the user with the given id; returns the updated record, or null if not found.</summary>
    Task<UserDto?> UpdateAsync(int id, UpdateUserRequestDto request);
    /// <summary>Deletes the user with the given id.</summary>
    Task DeleteAsync(int id);
    /// <summary>Flips the user's active/inactive status and returns the updated record.</summary>
    Task<UserDto> ToggleStatusAsync(int id);
    /// <summary>Enables/disables two-factor authentication for the user and returns the updated record.</summary>
    Task<UserDto> Toggle2FAAsync(int id);
    /// <summary>Sets a new password (operator-initiated reset) for the user.</summary>
    Task ResetPasswordAsync(int id, ResetPasswordRequestDto request);
    /// <summary>Returns the effective permission codes granted to the user (via roles).</summary>
    Task<IEnumerable<string>> GetPermissionsAsync(int userId);
    /// <summary>Stores/replaces the user's refresh token (hashed by the caller) with its expiry.</summary>
    Task UpsertRefreshTokenAsync(int userId, string refreshToken, DateTime expiresAtUtc);
    /// <summary>Finds the user that owns a (non-expired) refresh token, or null.</summary>
    Task<UserDto?> GetByRefreshTokenAsync(string refreshToken);
    /// <summary>Records the user's last successful login time.</summary>
    Task UpdateLastLoginAsync(int userId);

    // Password Reset
    /// <summary>
    /// Issues a password-reset token for the email (valid <paramref name="expiryHours"/> hours). Returns
    /// success + the user; success stays true even for an unknown email to avoid account enumeration.
    /// </summary>
    Task<(bool Success, UserDto? User)> CreatePasswordResetTokenAsync(string email, string token, int expiryHours = 1);
    /// <summary>Consumes a valid reset token and sets the new (already-hashed) password; returns success + user + an error code on failure.</summary>
    Task<(bool Success, UserDto? User, string? ErrorCode)> ResetPasswordWithTokenAsync(string token, string newPasswordHash);
    /// <summary>Checks a reset token without consuming it; returns whether it is valid + the owning user.</summary>
    Task<(bool IsValid, UserDto? User)> ValidatePasswordResetTokenAsync(string token);

    // Additional methods for management
    /// <summary>Paged, optionally-searched user list for management screens.</summary>
    Task<List<UserDto>> GetAllAsync(int page = 1, int pageSize = 50, string? search = null);
    /// <summary>Aggregate user statistics (counts by status, etc.) for dashboards.</summary>
    Task<UserStatisticsDto> GetStatisticsAsync();
}
