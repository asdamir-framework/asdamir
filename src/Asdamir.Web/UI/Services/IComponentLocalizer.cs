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
/// Simple localization service interface for Asdamir.Web.UI components.
/// Implementation should be provided by the host application (Server project).
/// </summary>
public interface IComponentLocalizer
{
    /// <summary>Resolves the localized string for the given resource key.</summary>
    /// <param name="key">The localization resource key.</param>
    /// <returns>The localized value, or the key itself when no translation exists.</returns>
    string this[string key] { get; }

    /// <summary>Resolves the localized string for the given key and formats it with the supplied arguments.</summary>
    /// <param name="key">The localization resource key (a composite/format string).</param>
    /// <param name="arguments">Values substituted into the format placeholders.</param>
    /// <returns>The formatted localized value, or the key itself when no translation exists.</returns>
    string this[string key, params object[] arguments] { get; }
}
