// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Tools.Commands;

/// <summary>
/// Shared name conventions used by the CLI commands.
///
/// English-only pluralizer — handles the common patterns the generator needs to
/// produce plausible REST routes ("api/customers"), table names ("Customers"), and
/// page identifiers ("CustomersList.razor"). Override at the call site (e.g. via
/// <c>--namespace</c> / <c>--route</c>) when you need a non-English noun.
/// </summary>
public static class NameHelper
{
    public static string Pluralize(string word)
    {
        if (string.IsNullOrEmpty(word)) return word;
        if (word.EndsWith("y", StringComparison.OrdinalIgnoreCase) &&
            word.Length > 1 && !"aeiou".Contains(char.ToLowerInvariant(word[^2])))
        {
            return word[..^1] + "ies";
        }
        if (word.EndsWith("s", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("x", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("ch", StringComparison.OrdinalIgnoreCase) ||
            word.EndsWith("sh", StringComparison.OrdinalIgnoreCase))
        {
            return word + "es";
        }
        return word + "s";
    }
}
