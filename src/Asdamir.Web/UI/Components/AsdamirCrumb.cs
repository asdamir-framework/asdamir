// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Asdamir.Web.UI.Components;

/// <summary>
/// One entry in an <see cref="AsdamirPageBar"/> breadcrumb trail. Non-final entries link via
/// <see cref="Href"/>; the final entry (the current page) is rendered as plain text.
/// </summary>
/// <param name="Label">The visible breadcrumb text.</param>
/// <param name="Href">The link target for a non-current crumb; <c>null</c> for the current (last) crumb.</param>
public sealed record AsdamirCrumb(string Label, string? Href = null);
