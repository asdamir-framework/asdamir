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

public static class AppConfigHelper
{
    public static async Task<int> GetClientTimeoutMinutesAsync(IAppConfigurationService configService)
    {
        var value = await configService.GetValueAsync("Blazor:ClientTimeoutMinutes");
        return int.TryParse(value, out var result) ? result : 30; // Default: 30 minutes
    }

    public static async Task<int> GetCleanupIntervalMinutesAsync(IAppConfigurationService configService)
    {
        var value = await configService.GetValueAsync("Blazor:CleanupIntervalMinutes");
        return int.TryParse(value, out var result) ? result : 5; // Default: 5 minutes
    }

    public static async Task<int> GetAutoRefreshIntervalMinutesAsync(IAppConfigurationService configService)
    {
        var value = await configService.GetValueAsync("Blazor:AutoRefreshIntervalMinutes");
        return int.TryParse(value, out var result) ? result : 1; // Default: 1 minute
    }

    public static async Task<bool> GetEnableAutoRefreshAsync(IAppConfigurationService configService)
    {
        var value = await configService.GetValueAsync("Blazor:EnableAutoRefresh");
        return bool.TryParse(value, out var result) ? result : true; // Default: true
    }

    public static async Task<bool> GetEnableSessionCleanupAsync(IAppConfigurationService configService)
    {
        var value = await configService.GetValueAsync("Blazor:EnableSessionCleanup");
        return bool.TryParse(value, out var result) ? result : true; // Default: true
    }

    public static async Task<int> GetAutoLogoutWarningTimeMinutesAsync(IAppConfigurationService configService)
    {
        var value = await configService.GetValueAsync("Blazor:AutoLogoutWarningTimeMinutes");
        return int.TryParse(value, out var result) ? result : 1; // Default: 1 minute
    }

    public static async Task<int> GetAutoLogoutTimeMinutesAsync(IAppConfigurationService configService)
    {
        var value = await configService.GetValueAsync("Blazor:AutoLogoutTimeMinutes");
        return int.TryParse(value, out var result) ? result : 5; // Default: 5 minutes
    }

    public static async Task<int> GetActivityCheckIntervalSecondsAsync(IAppConfigurationService configService)
    {
        var value = await configService.GetValueAsync("Blazor:ActivityCheckIntervalSeconds");
        return int.TryParse(value, out var result) ? result : 30; // Default: 30 seconds
    }

    public static async Task<bool> GetEnableActivityTrackingAsync(IAppConfigurationService configService)
    {
        var value = await configService.GetValueAsync("Blazor:EnableActivityTracking");
        return bool.TryParse(value, out var result) ? result : true; // Default: true
    }

    public static async Task<bool> GetShowLogoutWarningAsync(IAppConfigurationService configService)
    {
        var value = await configService.GetValueAsync("Blazor:ShowLogoutWarning");
        return bool.TryParse(value, out var result) ? result : true; // Default: true
    }
}

