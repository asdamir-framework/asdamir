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
/// Pure, testable core of the permission/policy-completeness gate (AUD016). Extracts the <c>perm</c>
/// values a Gateway's authorization policies <b>require</b> (<c>RequireClaim("perm", "X")</c> /
/// <c>HasClaim("perm", "X")</c>, including inside a <c>RequireAssertion(ctx =&gt; …)</c>), the codes a
/// tree's SQL seeds <b>supply</b> (permission codes seeded into <c>dbo.Permissions</c> and role codes
/// seeded into <c>dbo.Roles</c> / <c>dbo.UserAppRoles</c>), and compares the two.
///
/// <para>The bug this prevents: the app-login JWT carries the signed-in user's ROLE codes plus the
/// fine-grained PERMISSION codes their roles grant, one per <c>perm</c> claim. A policy that requires a
/// <c>perm</c> value which is neither a seeded permission code nor a role code can NEVER be satisfied →
/// every request 403s, silently (tests that inject claims directly miss it). This gate catches that class
/// at build/preflight time.</para>
///
/// <para>No IO lives here — the command layer (<see cref="PermissionPolicyCheckCommand"/>) reads files and
/// feeds their text in — so the whole gate is unit-testable without a filesystem or a database.</para>
/// </summary>
public static class PermissionPolicyScan
{
    // A required perm: RequireClaim("perm", "X") or HasClaim("perm", "X"). The claim TYPE may itself be
    // single- or double-quoted "perm"; the second literal is the required VALUE (group "value"). Tolerant
    // of whitespace and both quote styles. Applied to a comment-stripped line (see the command layer).
    private static readonly Regex RequiredPermRegex = new(
        @"(?:RequireClaim|HasClaim)\s*\(\s*""perm""\s*,\s*""((?:[^""\\]|\\.)*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // A permission code seeded into dbo.Permissions: a string literal that is an INSERT/MERGE/table-var VALUE
    // sitting somewhere after the token `Permissions`. We collect codes from any file that mentions
    // dbo.Permissions (see ExtractSuppliedPermissionCodes) — the shape zoo (MERGE … USING (VALUES …),
    // INSERT … VALUES, a @Perms table var + cursor) all funnels through N'…' / '…' string literals.
    private static readonly Regex SqlStringLiteralRegex = new(
        @"N?'((?:[^']|'')*)'",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // A role code seeded into dbo.Roles (Name) or dbo.UserAppRoles (RoleCode). Same literal shape; we scope
    // the collection to files/statements that mention those tables (see ExtractSuppliedRoleCodes).
    // (Shares SqlStringLiteralRegex for the literal itself.)

    /// <summary>Severity of a <see cref="PermissionFinding"/>, mirroring <see cref="AuditSeverity"/>.</summary>
    public enum FindingSeverity
    {
        /// <summary>A required <c>perm</c> value that no seed supplies as a permission or role code — the
        /// app-login token can never carry it, so the policy is a guaranteed silent 403.</summary>
        Error,
    }

    /// <summary>One permission/policy-completeness finding, tied to the POLICY site (file+line).</summary>
    /// <param name="File">Absolute path of the C# file where the policy requires the perm.</param>
    /// <param name="Line">1-based line number of the <c>RequireClaim</c>/<c>HasClaim</c>.</param>
    /// <param name="Perm">The required <c>perm</c> value that is unsupplied.</param>
    /// <param name="Severity">Always <see cref="FindingSeverity.Error"/> — this gate only errors.</param>
    /// <param name="Message">Human-readable explanation of the finding.</param>
    public sealed record PermissionFinding(
        string File, int Line, string Perm, FindingSeverity Severity, string Message);

    /// <summary>A <c>perm</c> value required by a policy, with its site.</summary>
    /// <param name="Perm">The required perm value.</param>
    /// <param name="Line">1-based line number of the requirement.</param>
    public sealed record RequiredPerm(string Perm, int Line);

    /// <summary>
    /// Extracts every <c>perm</c> value REQUIRED by a single C# file's <paramref name="content"/> —
    /// <c>RequireClaim("perm", "X")</c>, <c>HasClaim("perm", "X")</c> (also inside a <c>RequireAssertion</c>).
    /// Honors <c>// audit-lint:ignore AUD016</c> on the policy line (that line's perms are dropped) and
    /// <c>audit-lint:skip-file</c> anywhere in the file (returns nothing). Comment-stripped per line so a
    /// perm merely NAMED in a <c>//</c>/<c>///</c> comment is not counted (the AUD015 false-positive lesson).
    /// </summary>
    public static IReadOnlyList<RequiredPerm> ExtractRequiredPerms(string content)
    {
        var rawLines = content.Replace("\r\n", "\n").Split('\n');

        // File-level opt-out — same directive form as audit-lint. Tested on the RAW lines.
        foreach (var l in rawLines)
        {
            if (l.TrimStart(' ', '\t', '/').StartsWith("audit-lint:skip-file", StringComparison.Ordinal))
                return Array.Empty<RequiredPerm>();
        }

        var result = new List<RequiredPerm>();
        for (var i = 0; i < rawLines.Length; i++)
        {
            // Suppression markers live IN a comment, so test the RAW line — but match perms on the
            // comment-STRIPPED line so a perm merely named in a `//`/`///` comment (or a doc example)
            // isn't mistaken for a real requirement. String-literal aware (shared with audit-lint).
            if (IsSuppressed(rawLines[i])) continue;
            var scan = AuditLintCommand.StripLineComments(rawLines[i]);

            foreach (Match m in RequiredPermRegex.Matches(scan))
                result.Add(new RequiredPerm(Unescape(m.Groups[1].Value), i + 1));
        }
        return result;
    }

    /// <summary>
    /// Extracts SUPPLIED permission codes from a single SQL file's <paramref name="sqlContent"/> — the
    /// <c>Name</c> values inserted into <c>dbo.Permissions</c>. Tolerant of the shape zoo (MERGE …
    /// USING (VALUES …), INSERT … VALUES, a table-var + cursor): if the file mentions <c>Permissions</c> at
    /// all, every SQL string literal in it is collected as a candidate code. Per the v1 policy — a false
    /// "OK" is safer than a false failure — this deliberately OVER-collects (it may pick up a description or
    /// a role name too), which can only make a required perm look supplied, never wrongly fail one.
    /// </summary>
    public static IReadOnlyCollection<string> ExtractSuppliedPermissionCodes(string sqlContent)
        => ExtractLiteralsIfMentions(sqlContent, "Permissions");

    /// <summary>
    /// Extracts SUPPLIED role codes from a single SQL file's <paramref name="sqlContent"/> — the role
    /// <c>Name</c>s seeded into <c>dbo.Roles</c> and the <c>RoleCode</c>s into <c>dbo.UserAppRoles</c>. Same
    /// tolerant, over-collecting approach as <see cref="ExtractSuppliedPermissionCodes"/>.
    /// </summary>
    public static IReadOnlyCollection<string> ExtractSuppliedRoleCodes(string sqlContent)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        set.UnionWith(ExtractLiteralsIfMentions(sqlContent, "Roles"));          // dbo.Roles (Name)
        set.UnionWith(ExtractLiteralsIfMentions(sqlContent, "UserAppRoles"));   // dbo.UserAppRoles (RoleCode)
        return set;
    }

    /// <summary>
    /// Cross-checks REQUIRED perms against the union of SUPPLIED permission codes and role codes, producing
    /// one AUD016 ERROR per required perm that neither set supplies (the app-login token can never carry it
    /// → guaranteed 403). A perm supplied by any seed — as a permission code OR a role code — is OK.
    /// </summary>
    /// <param name="file">Absolute path used to label the findings.</param>
    /// <param name="required">Perms required in <paramref name="file"/> (from <see cref="ExtractRequiredPerms"/>).</param>
    /// <param name="supplied">The union of supplied permission codes and role codes across the whole tree.</param>
    public static IReadOnlyList<PermissionFinding> Compare(
        string file, IReadOnlyList<RequiredPerm> required, IReadOnlyCollection<string> supplied)
    {
        var suppliedSet = supplied as HashSet<string> ?? new HashSet<string>(supplied, StringComparer.Ordinal);
        var findings = new List<PermissionFinding>();
        foreach (var r in required)
        {
            if (suppliedSet.Contains(r.Perm)) continue;
            findings.Add(new PermissionFinding(file, r.Line, r.Perm, FindingSeverity.Error,
                $"Gateway policy requires perm '{r.Perm}' but no seed defines it as a permission or role " +
                "code — the app-login token can never carry it (guaranteed 403). Seed it in dbo.Permissions " +
                "(+ grant it to a role) or fix the policy."));
        }
        return findings;
    }

    /// <summary>True when <paramref name="path"/> is a SQL seed source we scan for supplied codes: a
    /// <c>seed_*/register_*/localize_*.sql</c> file, or any <c>.sql</c> under a <c>db/admin-onboarding/</c>
    /// or <c>db/migrations/</c> directory. (Same recognition surface as the localization gate.)</summary>
    public static bool IsSeedSqlFile(string path)
    {
        var name = Path.GetFileName(path);
        if (name.StartsWith("seed_", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("register_", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("localize_", StringComparison.OrdinalIgnoreCase))
            return true;

        var norm = path.Replace('\\', '/');
        return norm.Contains("/db/admin-onboarding/", StringComparison.OrdinalIgnoreCase)
            || norm.Contains("/db/migrations/", StringComparison.OrdinalIgnoreCase);
    }

    // Collects every SQL string literal in `sql`, but ONLY when the file mentions `table` (e.g. "Permissions"
    // / "Roles" / "UserAppRoles") — so a file that never touches the table contributes nothing. Over-collects
    // by design (see the callers' remarks): a false-supplied is safe, a false-unsupplied would wrongly fail.
    private static IReadOnlyCollection<string> ExtractLiteralsIfMentions(string sql, string table)
    {
        if (sql.IndexOf(table, StringComparison.OrdinalIgnoreCase) < 0)
            return Array.Empty<string>();

        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in SqlStringLiteralRegex.Matches(sql))
            set.Add(m.Groups[1].Value.Replace("''", "'"));
        return set;
    }

    /// <summary>Un-escapes a C# double-quoted string literal's backslash escapes (\", \\, \n, \r, \t).</summary>
    private static string Unescape(string s)
    {
        if (!s.Contains('\\')) return s;
        var sb = new System.Text.StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length)
            {
                i++;
                sb.Append(s[i] switch { 'n' => '\n', 'r' => '\r', 't' => '\t', var c => c });
            }
            else sb.Append(s[i]);
        }
        return sb.ToString();
    }

    /// <summary>True when the line carries a <c>// audit-lint:ignore AUD016</c> suppression marker.</summary>
    private static bool IsSuppressed(string line)
    {
        const string marker = "audit-lint:ignore";
        var idx = line.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return false;
        var tail = line[(idx + marker.Length)..];
        return Regex.IsMatch(tail, @"\bAUD016\b");
    }
}
