// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Web.Security.Extensions;

/// <summary>
/// Security Framework configuration options
/// </summary>
public class SecurityFrameworkOptions
{
    /// <summary>
    /// Session timeout duration (default: 30 minutes)
    /// </summary>
    public TimeSpan SessionTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Cleanup interval for expired sessions (default: 5 minutes)
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Auto-refresh interval before token expiry (default: 2 minutes)
    /// </summary>
    public TimeSpan AutoRefreshInterval { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Enable automatic token refresh (default: true)
    /// </summary>
    public bool EnableAutoRefresh { get; set; } = true;

    /// <summary>
    /// Enable session cleanup (default: true)
    /// </summary>
    public bool EnableSessionCleanup { get; set; } = true;

    /// <summary>
    /// Warning time before auto logout (default: 5 minutes)
    /// </summary>
    public TimeSpan AutoLogoutWarningTime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Auto logout time (default: 30 minutes)
    /// </summary>
    public TimeSpan AutoLogoutTime { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Check interval for activity (default: 30 seconds)
    /// </summary>
    public TimeSpan ActivityCheckInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Enable activity tracking (default: true)
    /// </summary>
    public bool EnableActivityTracking { get; set; } = true;

    /// <summary>
    /// Show warning dialog before logout (default: true)
    /// </summary>
    public bool ShowLogoutWarning { get; set; } = true;
}