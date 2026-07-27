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
/// Dialog facade — the Asdamir-owned surface for framework-level modal dialogs, so components inject
/// <see cref="IAsdamirDialogService"/> instead of FluentUI's <c>IDialogService</c> directly. The
/// FluentUI dependency lives inside the implementation only. Requires a dialog provider to be rendered
/// in the layout (see <c>AsdamirAppProviders</c>).
/// </summary>
public interface IAsdamirDialogService
{
    /// <summary>
    /// Shows a simple informational dialog with a title and message, and completes when the user
    /// dismisses it (the framework's unauthorized-access notice uses this).
    /// </summary>
    /// <param name="title">The already-localized dialog title.</param>
    /// <param name="message">The already-localized dialog body.</param>
    Task ShowInfoAsync(string title, string message);
}
