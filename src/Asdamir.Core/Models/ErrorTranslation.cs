// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.Models;

/// <summary>
/// One error-key → localized-message row (per culture) backing the two-channel error model: the
/// middleware maps an exception to a stable key and resolves the matching row for the request culture
/// to produce the user-facing message, keeping raw exception detail on the operator channel only.
/// </summary>
public class ErrorTranslation
{
    /// <summary>Primary key of the translation row.</summary>
    public int Id { get; set; }

    /// <summary>Stable, culture-independent error key (e.g. <c>error.user.notfound</c>) the middleware maps an exception to.</summary>
    public string ErrorKey { get; set; } = string.Empty;

    /// <summary>Culture this translation applies to (e.g. <c>tr-TR</c>, <c>en-US</c>, <c>ru-RU</c>).</summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>Localized user-facing message shown for this key and culture; never the raw key or a status code.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Optional longer explanation or operator note; not necessarily shown to end-users.</summary>
    public string? Description { get; set; }

    /// <summary>When false the translation is ignored, letting the resolver fall back to the generic message.</summary>
    public bool IsActive { get; set; }

    /// <summary>UTC timestamp when the translation was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>UTC timestamp of the last edit to the translation.</summary>
    public DateTime UpdatedAt { get; set; }
}
