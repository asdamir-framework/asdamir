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
/// One pricing tier for <see cref="AsdamirPricing"/>. Dependency-light data holder — the component
/// renders a responsive row of these as INSPINIA-style pricing cards.
/// </summary>
public sealed class AsdamirPricingPlan
{
    /// <summary>Tier name, e.g. "Starter", "Pro".</summary>
    public string Name { get; set; } = "";

    /// <summary>Headline price as display text, e.g. "$29", "₺199", "Free".</summary>
    public string Price { get; set; } = "";

    /// <summary>Optional billing period suffix shown next to the price, e.g. "/mo".</summary>
    public string? Period { get; set; }

    /// <summary>Optional one-line description under the name.</summary>
    public string? Description { get; set; }

    /// <summary>Feature bullet points (each rendered with a check icon).</summary>
    public IReadOnlyList<string> Features { get; set; } = System.Array.Empty<string>();

    /// <summary>Call-to-action button label. When null/empty no button is rendered.</summary>
    public string? CtaLabel { get; set; }

    /// <summary>Highlights this card (accent border + lift) as the recommended plan.</summary>
    public bool Featured { get; set; }

    /// <summary>Optional ribbon text on a featured card, e.g. "Most popular".</summary>
    public string? Badge { get; set; }

    /// <summary>Opaque value passed back to <c>OnSelect</c> (e.g. a plan id/code).</summary>
    public string? Value { get; set; }
}
