// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace MyApp.Services;

/// <summary>
/// Immutable-per-change snapshot of the global loading overlay: whether it is visible and how it should
/// be rendered. A new instance is published on every <see cref="LoadingService.Show"/> call.
/// </summary>
public class LoadingState
{
    /// <summary>Whether the loading overlay should currently be shown.</summary>
    public bool Visible { get; set; }

    /// <summary>Optional status message rendered beside the spinner; <c>null</c> for a bare spinner.</summary>
    public string? Message { get; set; }

    /// <summary>Whether the overlay covers the whole viewport (<c>true</c>) rather than its container.</summary>
    public bool Fullscreen { get; set; }

    /// <summary>Whether a dimming backdrop is drawn behind the spinner. Defaults to <c>true</c>.</summary>
    public bool ShowBackdrop { get; set; } = true;
}

/// <summary>
/// Circuit-scoped holder for the single global loading/spinner state. It stores the current
/// <see cref="LoadingState"/> and raises <see cref="OnChanged"/> so an overlay component can re-render;
/// it renders nothing itself.
/// </summary>
public class LoadingService
{
    /// <summary>Raised whenever the loading state changes; carries the new <see cref="LoadingState"/>, or <c>null</c> when hidden.</summary>
    public event Action<LoadingState?>? OnChanged;

    private LoadingState? _state;

    /// <summary>
    /// Shows the loading overlay, replacing any current state, and notifies subscribers.
    /// </summary>
    /// <param name="message">Optional status message to display; <c>null</c> for a bare spinner.</param>
    /// <param name="fullscreen">Whether to cover the whole viewport. Defaults to <c>true</c>.</param>
    /// <param name="showBackdrop">Whether to draw a dimming backdrop. Defaults to <c>true</c>.</param>
    public void Show(string? message = null, bool fullscreen = true, bool showBackdrop = true)
    {
        _state = new LoadingState { Visible = true, Message = message, Fullscreen = fullscreen, ShowBackdrop = showBackdrop };
        OnChanged?.Invoke(_state);
    }

    /// <summary>
    /// Clears the loading state (hides the overlay) and notifies subscribers with <c>null</c>.
    /// </summary>
    public void Hide()
    {
        _state = null;
        OnChanged?.Invoke(null);
    }

    /// <summary>Gets the current loading state, or <c>null</c> when nothing is loading.</summary>
    public LoadingState? Current => _state;
}