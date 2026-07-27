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
/// Toast facade — the Asdamir-owned surface for lightweight success/error/info toasts, so pages
/// inject <see cref="IAsdamirToastService"/> instead of FluentUI's <c>IToastService</c> directly.
/// The FluentUI dependency lives inside the implementation only; a FluentUI major (which removes/renames
/// the toast service) becomes a change to that one file, not to every page that raises a toast.
/// Requires a toast provider to be rendered in the layout (see <c>AsdamirAppProviders</c>).
/// </summary>
public interface IAsdamirToastService
{
    /// <summary>Shows a success (green) toast with the given message.</summary>
    /// <param name="message">The already-localized message to display.</param>
    void ShowSuccess(string message);

    /// <summary>Shows an error (red) toast with the given message.</summary>
    /// <param name="message">The already-localized message to display.</param>
    void ShowError(string message);

    /// <summary>Shows an informational (neutral) toast with the given message.</summary>
    /// <param name="message">The already-localized message to display.</param>
    void ShowInfo(string message);
}
