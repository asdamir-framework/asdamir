// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.Contracts;

namespace Asdamir.Core.Helpers;

/// <summary>
/// Convenience accessors for the Blazor client/session tunables held in DB-backed <c>AppConfigurations</c>
/// (AppId-scoped, read through <see cref="Asdamir.Core.Contracts.IAppConfigurationService"/>). Each method
/// reads one <c>Blazor:*</c> key, parses it, and falls back to a safe default when the key is absent or
/// unparseable — so the UI never hardcodes these values.
/// </summary>
public static class AppConfigHelper
{
    /// <summary>
    /// Reads <c>Blazor:ClientTimeoutMinutes</c> — minutes of inactivity before the client session times out.
    /// </summary>
    /// <param name="configService">Service used to read the DB-backed configuration value.</param>
    /// <returns>The configured value in minutes, or 30 when unset/unparseable.</returns>
    public static async Task<int> GetClientTimeoutMinutesAsync(IAppConfigurationService configService)
    {
        var value = await configService.GetValueAsync("Blazor:ClientTimeoutMinutes");
        return int.TryParse(value, out var result) ? result : 30; // Default: 30 minutes
    }

    /// <summary>
    /// Reads <c>Blazor:CleanupIntervalMinutes</c> — how often (minutes) the expired-session cleanup sweep runs.
    /// </summary>
    /// <param name="configService">Service used to read the DB-backed configuration value.</param>
    /// <returns>The configured value in minutes, or 5 when unset/unparseable.</returns>
    public static async Task<int> GetCleanupIntervalMinutesAsync(IAppConfigurationService configService)
    {
        var value = await configService.GetValueAsync("Blazor:CleanupIntervalMinutes");
        return int.TryParse(value, out var result) ? result : 5; // Default: 5 minutes
    }

    /// <summary>
    /// Reads <c>Blazor:AutoRefreshIntervalMinutes</c> — interval (minutes) between automatic UI data refreshes.
    /// </summary>
    /// <param name="configService">Service used to read the DB-backed configuration value.</param>
    /// <returns>The configured value in minutes, or 1 when unset/unparseable.</returns>
    public static async Task<int> GetAutoRefreshIntervalMinutesAsync(IAppConfigurationService configService)
    {
        var value = await configService.GetValueAsync("Blazor:AutoRefreshIntervalMinutes");
        return int.TryParse(value, out var result) ? result : 1; // Default: 1 minute
    }

    /// <summary>
    /// Reads <c>Blazor:EnableAutoRefresh</c> — master toggle for the periodic auto-refresh of UI data.
    /// </summary>
    /// <param name="configService">Service used to read the DB-backed configuration value.</param>
    /// <returns>The configured flag, or <c>true</c> when unset/unparseable.</returns>
    public static async Task<bool> GetEnableAutoRefreshAsync(IAppConfigurationService configService)
    {
        var value = await configService.GetValueAsync("Blazor:EnableAutoRefresh");
        return bool.TryParse(value, out var result) ? result : true; // Default: true
    }

    /// <summary>
    /// Reads <c>Blazor:EnableSessionCleanup</c> — master toggle for the background expired-session cleanup sweep.
    /// </summary>
    /// <param name="configService">Service used to read the DB-backed configuration value.</param>
    /// <returns>The configured flag, or <c>true</c> when unset/unparseable.</returns>
    public static async Task<bool> GetEnableSessionCleanupAsync(IAppConfigurationService configService)
    {
        var value = await configService.GetValueAsync("Blazor:EnableSessionCleanup");
        return bool.TryParse(value, out var result) ? result : true; // Default: true
    }

    /// <summary>
    /// Reads <c>Blazor:AutoLogoutWarningTimeMinutes</c> — minutes before auto-logout that the warning is shown.
    /// </summary>
    /// <param name="configService">Service used to read the DB-backed configuration value.</param>
    /// <returns>The configured value in minutes, or 1 when unset/unparseable.</returns>
    public static async Task<int> GetAutoLogoutWarningTimeMinutesAsync(IAppConfigurationService configService)
    {
        var value = await configService.GetValueAsync("Blazor:AutoLogoutWarningTimeMinutes");
        return int.TryParse(value, out var result) ? result : 1; // Default: 1 minute
    }

    /// <summary>
    /// Reads <c>Blazor:AutoLogoutTimeMinutes</c> — minutes of inactivity after which the user is auto-logged-out.
    /// </summary>
    /// <param name="configService">Service used to read the DB-backed configuration value.</param>
    /// <returns>The configured value in minutes, or 5 when unset/unparseable.</returns>
    public static async Task<int> GetAutoLogoutTimeMinutesAsync(IAppConfigurationService configService)
    {
        var value = await configService.GetValueAsync("Blazor:AutoLogoutTimeMinutes");
        return int.TryParse(value, out var result) ? result : 5; // Default: 5 minutes
    }

    /// <summary>
    /// Reads <c>Blazor:ActivityCheckIntervalSeconds</c> — how often (seconds) the client polls for user activity.
    /// </summary>
    /// <param name="configService">Service used to read the DB-backed configuration value.</param>
    /// <returns>The configured value in seconds, or 30 when unset/unparseable.</returns>
    public static async Task<int> GetActivityCheckIntervalSecondsAsync(IAppConfigurationService configService)
    {
        var value = await configService.GetValueAsync("Blazor:ActivityCheckIntervalSeconds");
        return int.TryParse(value, out var result) ? result : 30; // Default: 30 seconds
    }

    /// <summary>
    /// Reads <c>Blazor:EnableActivityTracking</c> — master toggle for client-side user-activity tracking.
    /// </summary>
    /// <param name="configService">Service used to read the DB-backed configuration value.</param>
    /// <returns>The configured flag, or <c>true</c> when unset/unparseable.</returns>
    public static async Task<bool> GetEnableActivityTrackingAsync(IAppConfigurationService configService)
    {
        var value = await configService.GetValueAsync("Blazor:EnableActivityTracking");
        return bool.TryParse(value, out var result) ? result : true; // Default: true
    }

    /// <summary>
    /// Reads <c>Blazor:ShowLogoutWarning</c> — whether the countdown warning dialog is displayed before auto-logout.
    /// </summary>
    /// <param name="configService">Service used to read the DB-backed configuration value.</param>
    /// <returns>The configured flag, or <c>true</c> when unset/unparseable.</returns>
    public static async Task<bool> GetShowLogoutWarningAsync(IAppConfigurationService configService)
    {
        var value = await configService.GetValueAsync("Blazor:ShowLogoutWarning");
        return bool.TryParse(value, out var result) ? result : true; // Default: true
    }
}

