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
/// Default <see cref="IAsdamirToastService"/> implementation — delegates to FluentUI's
/// <see cref="IToastService"/>. This is the ONLY place in the framework's UI toast path that
/// references <c>Microsoft.FluentUI.*</c>; a FluentUI major reshapes this file, not the callers.
/// </summary>
public sealed class AsdamirToastService : IAsdamirToastService
{
    private readonly IToastService _toast;

    /// <summary>Creates the facade over the injected FluentUI toast service.</summary>
    /// <param name="toast">The underlying FluentUI toast service (registered by <c>AddFluentUIComponents</c>).</param>
    public AsdamirToastService(IToastService toast) => _toast = toast;

    /// <inheritdoc/>
    public void ShowSuccess(string message) => _toast.ShowSuccess(message);

    /// <inheritdoc/>
    public void ShowError(string message) => _toast.ShowError(message);

    /// <inheritdoc/>
    public void ShowInfo(string message) => _toast.ShowInfo(message);
}
