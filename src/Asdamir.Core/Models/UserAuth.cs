// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.Models;

/// <summary>
/// Authentication projection of a user: the identity, credential and authorization facts that
/// <c>IJwtService.IssueTokens</c> reads to mint the access/refresh JWT. Carries security-sensitive
/// material (the password hash) and is never returned to clients as-is.
/// </summary>
public class UserAuth
{
    /// <summary>Primary key of the user in <c>dbo.Users</c>; embedded as the JWT subject.</summary>
    public int Id { get; set; }

    /// <summary>Login identifier and, in most flows, the username claim on the issued token.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Human-readable display name surfaced in the UI; not an authorization input.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Primary role name used to derive permission grants; null until a role is assigned.</summary>
    public string? Role { get; set; } = string.Empty;

    /// <summary>Salted password hash used to verify the login credential — never emitted to clients or logged.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>When false the account is disabled and login is refused even with valid credentials.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>When true login requires a second factor (an OTP) before tokens are issued.</summary>
    public bool IsTwoFactorEnabled { get; set; } = false;

    /// <summary>Destination for the 2FA OTP (SMS); null when no phone is on file.</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>True once the phone has been confirmed; a prerequisite for using SMS as the second factor.</summary>
    public bool PhoneNumberVerified { get; set; } = false;

    /// <summary>Tenant/company the user belongs to; null for a platform-owner operator not scoped to any company.</summary>
    public Guid? CompanyId { get; set; }

    /// <summary>
    /// Platform-owner super admin. When true the admin bypasses the per-app role matrix and can
    /// manage everything (all apps, users, menus, permissions) and define other admins. Non-super
    /// admins are limited to their <c>AdminUserAppRoles</c> grants. See
    /// docs/design/admin-vs-enduser-identity.md.
    /// </summary>
    public bool IsSuperAdmin { get; set; } = false;

    /// <summary>Parameterless constructor for serializers and Dapper materialization.</summary>
    public UserAuth() { }

    /// <summary>Builds an active user projection from its core identity and credential fields.</summary>
    public UserAuth(int id, string email, string name, string role, string passwordHash)
    {
        Id = id;
        Email = email;
        Name = name;
        Role = role;
        PasswordHash = passwordHash;
    }

    /// <summary>Builds a user projection with an explicit active/disabled state.</summary>
    public UserAuth(int id, string email, string name, string role, string passwordHash, bool isActive)
    {
        Id = id;
        Email = email;
        Name = name;
        Role = role;
        PasswordHash = passwordHash;
        IsActive = isActive;
    }
}
