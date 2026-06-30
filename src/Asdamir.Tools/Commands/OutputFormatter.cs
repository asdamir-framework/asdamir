// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later

namespace Asdamir.Tools.Commands;

/// <summary>
/// Renders a generator's written-file list as a grouped, aligned table (no box-drawing): a fixed-width
/// left column for the layer name (shown once per consecutive group) and the project-relative path on the
/// right. Presentation only — the commands decide WHAT to generate; this only decides how it's printed.
/// </summary>
internal static class OutputFormatter
{
    /// <param name="rows">Ordered (Layer, Path, Skipped). Consecutive rows with the same Layer print the
    /// label once; the rest are blank-aligned under it.</param>
    public static void PrintGroupedFiles(IReadOnlyList<(string Layer, string Path, bool Skipped)> rows, int labelWidth = 14)
    {
        string? prev = null;
        foreach (var (layer, path, skipped) in rows)
        {
            var left = string.Equals(layer, prev, StringComparison.Ordinal) ? new string(' ', labelWidth) : layer.PadRight(labelWidth);
            Console.WriteLine($"  {left}{path}{(skipped ? "   (exists, skipped)" : "")}");
            prev = layer;
        }
    }
}
