// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.Dtos;

/// <summary>Aggregate user counts for the AppId-scoped users dashboard/summary card.</summary>
public class UserStatisticsDto
{
    /// <summary>Total number of user records in scope, regardless of status.</summary>
    public int TotalUsers { get; set; }
    /// <summary>Count of users currently enabled for login (<see cref="UserDto.IsActive"/> = true).</summary>
    public int ActiveUsers { get; set; }
    /// <summary>Count of users disabled from login (IsActive = false).</summary>
    public int InactiveUsers { get; set; }
    /// <summary>Count of users created since the start of the current calendar month.</summary>
    public int NewUsersThisMonth { get; set; }
}
