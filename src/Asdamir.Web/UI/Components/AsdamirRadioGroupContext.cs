// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later

using System;
using System.Threading.Tasks;

namespace Asdamir.Web.UI.Components;

/// <summary>
/// The value an <see cref="AsdamirRadioGroup"/> cascades to its <see cref="AsdamirRadio"/> children: the
/// shared HTML radio <c>name</c>, the currently selected value, the group-wide disabled flag, and the
/// callback a child invokes when it becomes selected. Kept as a small immutable record so the isolation radio
/// pair coordinates without any FluentUI dependency.
/// </summary>
/// <param name="Name">The shared <c>name</c> attribute tying the group's radios together.</param>
/// <param name="SelectedValue">The group's currently selected value (a child is checked when its value equals this).</param>
/// <param name="Disabled">When <see langword="true"/>, the whole group is disabled.</param>
/// <param name="NotifySelectedAsync">Invoked by a child when it becomes selected, carrying that child's value.</param>
public sealed record AsdamirRadioGroupContext(
    string Name,
    string? SelectedValue,
    bool Disabled,
    Func<string?, Task> NotifySelectedAsync);
