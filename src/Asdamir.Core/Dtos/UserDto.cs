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

public class UserDto
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? Notes { get; set; }
    public string Role { get; set; } = "User";
    public List<string> Permissions { get; set; } = new();
    public bool IsSuperAdmin { get; set; }

    // 2FA fields
    public bool IsTwoFactorEnabled { get; set; }
    public string? PhoneNumber { get; set; }
    public bool PhoneNumberVerified { get; set; }

    // Company mapping
    public Guid? CompanyId { get; set; }
    public string? CompanyName { get; set; }

    // Service Points mapping
    public List<Guid> ServicePointIds { get; set; } = new();
}

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

public record ResetPasswordRequestDto(string NewPassword);

public record ForgotPasswordRequestDto(string Email, string? Language = null);

public record ForgotPasswordResponseDto(bool Success, string Message);

public record ResetPasswordWithTokenRequestDto(string Token, string NewPassword);

public record ResetPasswordWithTokenResponseDto(bool Success, string Message);

public record ValidateResetTokenRequestDto(string Token);

public record ValidateResetTokenResponseDto(bool IsValid, string? Email);
