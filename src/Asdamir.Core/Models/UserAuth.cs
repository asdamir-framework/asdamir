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

public class UserAuth
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Role { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsTwoFactorEnabled { get; set; } = false;
    public string? PhoneNumber { get; set; }
    public bool PhoneNumberVerified { get; set; } = false;
    public Guid? CompanyId { get; set; }

    /// <summary>
    /// Platform-owner super admin. When true the admin bypasses the per-app role matrix and can
    /// manage everything (all apps, users, menus, permissions) and define other admins. Non-super
    /// admins are limited to their <c>AdminUserAppRoles</c> grants. See
    /// docs/design/admin-vs-enduser-identity.md.
    /// </summary>
    public bool IsSuperAdmin { get; set; } = false;

    public UserAuth() { }

    public UserAuth(int id, string email, string name, string role, string passwordHash)
    {
        Id = id;
        Email = email;
        Name = name;
        Role = role;
        PasswordHash = passwordHash;
    }

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
