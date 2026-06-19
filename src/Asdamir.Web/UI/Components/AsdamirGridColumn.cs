// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Microsoft.AspNetCore.Components;

namespace Asdamir.Web.UI.Components;

/// <summary>
/// Optional column descriptor for <see cref="AsdamirGrid{TItem}"/>. When a caller supplies a
/// <c>Columns</c> list, the grid renders its own header + data cells from these descriptors, which
/// unlocks built-in <b>sorting</b> (per <see cref="Sortable"/> column, via <see cref="Value"/>) and
/// <b>Excel export</b> (via <see cref="Value"/>, ClosedXML). Pages that still pass
/// <c>HeaderTemplate</c>/<c>RowTemplate</c> keep working unchanged (no sort/export).
/// </summary>
/// <typeparam name="TItem">Row model type.</typeparam>
public sealed class AsdamirGridColumn<TItem>
{
    /// <summary>Header label.</summary>
    public string Title { get; set; } = "";

    /// <summary>
    /// Value accessor — the single source for sorting, Excel export and (by default) the rendered
    /// cell text. Keep it cheap; it can be called once per row per render/sort/export.
    /// </summary>
    public Func<TItem, object?> Value { get; set; } = _ => null;

    /// <summary>
    /// Optional cell-text override (e.g. an em-dash for empty values). Falls back to
    /// <c>Value(item)?.ToString()</c>. Does not affect sort/export, which always use <see cref="Value"/>.
    /// </summary>
    public Func<TItem, string>? Display { get; set; }

    /// <summary>When true the header is clickable and toggles ascending/descending sort on this column.</summary>
    public bool Sortable { get; set; }

    /// <summary>When false the column is omitted from Excel export (e.g. an actions column).</summary>
    public bool Exportable { get; set; } = true;

    /// <summary>Render the cell value in a monospace <c>&lt;code&gt;</c> wrapper.</summary>
    public bool Mono { get; set; }

    /// <summary>Optional CSS class applied to both the <c>&lt;th&gt;</c> and the <c>&lt;td&gt;</c> (e.g. "num", "wrap").</summary>
    public string? CssClass { get; set; }
}
