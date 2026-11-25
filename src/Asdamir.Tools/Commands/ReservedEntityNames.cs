// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.Text.RegularExpressions;

namespace Asdamir.Tools.Commands;

/// <summary>
/// The tables a generated app's database can already hold that were NOT created by <c>new entity</c> — the
/// framework's own (identity, RBAC, menus, localization, config, audit, billing, background runs, the
/// migration journal).
/// </summary>
/// <remarks>
/// <para><b>Why this exists.</b> An entity's table is the plural of its name, with no prefix. So
/// <c>asdamir new entity Invoice</c> targets <c>dbo.Invoices</c> — the billing table of a <c>--billing</c>
/// app. Its <c>CREATE TABLE</c> is guarded by <c>IF OBJECT_ID(…) IS NULL</c>, so it silently adopts the
/// framework's table (with different columns), and <c>asdamir rollback Invoice</c> then ran
/// <c>DROP TABLE dbo.Invoices</c> on it. The same for <c>Plan</c>, <c>Subscription</c>, <c>User</c>,
/// <c>Role</c>, <c>Menu</c>, <c>Permission</c>. Two guards use this type: <c>new entity</c> refuses the name
/// up front, and <c>rollback</c> refuses to drop such a table whatever evidence it finds.</para>
/// <para><b>The list is DERIVED, not hand-written.</b> The table names are read from the SQL assets the CLI
/// itself emits into an app (every <c>CREATE TABLE</c> in the embedded <c>*.sql</c> templates), so a table
/// added to a schema template is covered with no edit here. A short explicit list covers the tables that live
/// only in the control plane (never emitted into an app) and the journal, whose DDL is in C#.</para>
/// </remarks>
public static class ReservedEntityNames
{
    // Control-plane-only tables (AsdamirVault) and the journal — not present in any embedded app template.
    private static readonly string[] ExplicitTables =
    [
        "__SchemaMigrations", "Apps", "AppLog", "UserAppRoles",
    ];

    private static readonly Regex CreateTable = new(
        @"CREATE\s+TABLE\s+(?:\[?dbo\]?\s*\.\s*)?\[?([A-Za-z_][A-Za-z0-9_]*)\]?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Lazy<IReadOnlySet<string>> Tables = new(Load);

    /// <summary>Every framework-owned table name (case-insensitive).</summary>
    public static IReadOnlySet<string> FrameworkTables => Tables.Value;

    /// <summary>True when <paramref name="tableName"/> is a table the framework owns.</summary>
    /// <param name="tableName">A table name without schema.</param>
    /// <returns>True for a framework-owned table.</returns>
    public static bool IsFrameworkTable(string tableName) => Tables.Value.Contains(tableName);

    /// <summary>
    /// True when an entity called <paramref name="entityName"/> would map onto a framework-owned table.
    /// </summary>
    /// <param name="entityName">The PascalCase entity name.</param>
    /// <param name="reason">Why the name is refused — names the colliding table.</param>
    /// <returns>True when the name must be refused.</returns>
    public static bool TryGetReason(string entityName, out string reason)
    {
        var table = NameHelper.Pluralize(entityName);
        if (IsFrameworkTable(table))
        {
            reason = $"its table would be dbo.{table}, which the framework owns (identity / RBAC / menu / "
                     + "localization / config / audit / billing / background-run tables are never an entity's).";
            return true;
        }

        reason = "";
        return false;
    }

    private static IReadOnlySet<string> Load()
    {
        var set = new HashSet<string>(ExplicitTables, StringComparer.OrdinalIgnoreCase);
        var asm = typeof(ReservedEntityNames).Assembly;

        foreach (var resource in asm.GetManifestResourceNames())
        {
            if (!resource.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)) continue;

            using var stream = asm.GetManifestResourceStream(resource);
            if (stream is null) continue;
            using var reader = new StreamReader(stream);

            foreach (Match m in CreateTable.Matches(reader.ReadToEnd()))
            {
                var name = m.Groups[1].Value;

                // `CREATE TABLE dbo` alone (schema matched as the name when the real name sits on the next
                // token in an unusual layout) is not a table.
                if (!string.Equals(name, "dbo", StringComparison.OrdinalIgnoreCase)) set.Add(name);
            }
        }

        return set;
    }
}
