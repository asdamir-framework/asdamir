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
public class LoadingState
{
    public bool Visible { get; set; }
    public string? Message { get; set; }
    public bool Fullscreen { get; set; }
    public bool ShowBackdrop { get; set; } = true;
}

public class LoadingService
{
    // event for components to subscribe
    public event Action<LoadingState?>? OnChanged;

    private LoadingState? _state;

    public void Show(string? message = null, bool fullscreen = true, bool showBackdrop = true)
    {
        _state = new LoadingState { Visible = true, Message = message, Fullscreen = fullscreen, ShowBackdrop = showBackdrop };
        OnChanged?.Invoke(_state);
    }

    public void Hide()
    {
        _state = null;
        OnChanged?.Invoke(null);
    }

    public LoadingState? Current => _state;
}