// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Web.UI.Components;

/// <summary>
/// Size presets for the shared dialog component.
/// </summary>
public enum DialogSize
{
    /// <summary>Compact dialog for short prompts or confirmations.</summary>
    Small,

    /// <summary>Default, general-purpose dialog size.</summary>
    Medium,

    /// <summary>Roomy dialog for longer forms or content.</summary>
    Large,

    /// <summary>Extra-large dialog for dense or wide content.</summary>
    XLarge,

    /// <summary>Dialog that fills the entire viewport.</summary>
    Fullscreen,

    /// <summary>Caller-specified dimensions rather than a preset.</summary>
    Custom
}
