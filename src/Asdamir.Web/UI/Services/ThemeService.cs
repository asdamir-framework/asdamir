// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Web.UI.Services;

/// <summary>
/// Theme service for managing dark/light mode and theme customization
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Current theme mode
    /// </summary>
    ThemeMode CurrentTheme { get; }

    /// <summary>
    /// Toggle between dark and light mode
    /// </summary>
    Task ToggleThemeAsync();

    /// <summary>
    /// Set specific theme
    /// </summary>
    Task SetThemeAsync(ThemeMode theme);

    /// <summary>
    /// Event raised when theme changes
    /// </summary>
    event EventHandler<ThemeMode>? ThemeChanged;
}

/// <summary>
/// Theme mode enumeration
/// </summary>
public enum ThemeMode
{
    /// <summary>Light color scheme.</summary>
    Light,
    /// <summary>Dark color scheme.</summary>
    Dark,
    /// <summary>Follow the operating-system / browser color-scheme preference.</summary>
    System
}

/// <summary>
/// Theme service implementation
/// </summary>
public sealed class ThemeService : IThemeService
{
    private ThemeMode _currentTheme = ThemeMode.Light;

    /// <inheritdoc/>
    public ThemeMode CurrentTheme => _currentTheme;

    /// <inheritdoc/>
    public event EventHandler<ThemeMode>? ThemeChanged;

    /// <inheritdoc/>
    public Task ToggleThemeAsync()
    {
        var newTheme = _currentTheme == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
        return SetThemeAsync(newTheme);
    }

    /// <inheritdoc/>
    public Task SetThemeAsync(ThemeMode theme)
    {
        if (_currentTheme != theme)
        {
            _currentTheme = theme;
            ThemeChanged?.Invoke(this, theme);
        }
        return Task.CompletedTask;
    }
}
