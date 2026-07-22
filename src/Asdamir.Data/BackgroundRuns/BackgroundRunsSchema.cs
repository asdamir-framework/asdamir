// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.Reflection;

namespace Asdamir.Data.BackgroundRuns;

/// <summary>
/// Exposes the embedded <c>BackgroundRuns.sql</c> (table + guarded state-transition procs) so a host
/// or a test can apply it programmatically. In a generated app it is normally applied as a journaled
/// migration under the app's own <c>db/migrations</c> (the template follow-up emits it there) — this
/// accessor is the source of truth both share.
/// </summary>
public static class BackgroundRunsSchema
{
    private const string ResourceName = "Asdamir.Data.BackgroundRuns.BackgroundRuns.sql";

    /// <summary>
    /// The full DDL (SQL Server dialect) for the background-run store: <c>dbo.BackgroundRuns</c> +
    /// its <c>BackgroundRuns_*</c> procs. Idempotent and <c>GO</c>-batched.
    /// </summary>
    /// <returns>The SQL script text.</returns>
    public static string GetSqlServerScript()
    {
        var asm = typeof(BackgroundRunsSchema).Assembly;
        using var stream = asm.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// The script split on <c>GO</c> batch separators (each non-empty batch trimmed), ready to feed
    /// to a command one batch at a time (ADO.NET does not honour <c>GO</c>).
    /// </summary>
    /// <returns>The ordered, non-empty SQL batches.</returns>
    public static IReadOnlyList<string> GetSqlServerBatches()
    {
        var script = GetSqlServerScript();
        return script
            .Split(["\nGO\n", "\nGO\r\n", "\r\nGO\r\n"], StringSplitOptions.None)
            .Select(b => b.Trim())
            .Where(b => b.Length > 0 && !string.Equals(b, "GO", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }
}
