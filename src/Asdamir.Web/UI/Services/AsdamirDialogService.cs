// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Microsoft.FluentUI.AspNetCore.Components;

namespace Asdamir.Web.UI.Services;

/// <summary>
/// Default <see cref="IAsdamirDialogService"/> implementation — delegates to FluentUI's
/// <see cref="IDialogService"/>. This is the ONLY place in the framework's dialog-service path that
/// references <c>Microsoft.FluentUI.*</c>; a FluentUI major reshapes this file, not the callers.
/// </summary>
public sealed class AsdamirDialogService : IAsdamirDialogService
{
    private readonly IDialogService _dialogs;

    /// <summary>Creates the facade over the injected FluentUI dialog service.</summary>
    /// <param name="dialogs">The underlying FluentUI dialog service (registered by <c>AddFluentUIComponents</c>).</param>
    public AsdamirDialogService(IDialogService dialogs) => _dialogs = dialogs;

    /// <inheritdoc/>
    public async Task ShowInfoAsync(string title, string message)
    {
        // Preserve the exact call the framework's RouteAuthorizationHandler made against FluentUI v4's
        // 2-arg ShowInfoAsync(message, primaryAction) overload — behaviour is byte-for-byte unchanged.
        var dialog = await _dialogs.ShowInfoAsync(title, message);
        await dialog.Result;
    }
}
