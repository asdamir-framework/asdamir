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
/// Pure, testable core of the SEED-FORM gates — <b>AUD018</b> (a localization seed may be written in exactly
/// ONE approved spelling) and <b>AUD017</b> (a permission grant may not be selected by a <c>LIKE</c> pattern).
/// Both read T-SQL through the shared <see cref="SqlTextScanner"/> and do no IO (the command layer,
/// <see cref="SeedFormCheckCommand"/>, reads files and feeds their text in).
///
/// <para><b>Why these rules exist — the disease they treat.</b> Twice in one work chain a completeness gate
/// went blind because the SQL was written in a spelling it did not recognise: an apostrophe in a comment
/// shifted AUD016's literal pairing, and the <c>EXEC …_UpsertValue</c> form was invisible to AUD015 across 276
/// keys. Both were fixed by teaching the scanner — but that is an infinite race, because a third spelling
/// always exists. <b>Constraining the input terminates it:</b> if a seed may be written in exactly one approved
/// way, "does the gate understand this file?" stops being a question. AUD015's reader was widened because it
/// must read what is already applied and frozen; AUD017/AUD018 make sure nothing NEW widens it again.</para>
///
/// <para>AUD017 is the same idea applied to <b>authorization</b> rather than to text: a wildcard grant is not
/// merely hard to parse, it is <b>unlistable in principle</b> — the set of permissions it confers is whatever
/// the catalogue happens to hold at run time, so no reader, human or static, can state it.</para>
/// </summary>
public static class SeedFormScan
{
    /// <summary>Severity of a <see cref="SeedFormFinding"/>. Both seed-form rules only ever error.</summary>
    public enum FindingSeverity
    {
        /// <summary>A non-canonical seed spelling (AUD018) or a pattern-based grant (AUD017).</summary>
        Error,
    }

    /// <summary>One seed-form finding, tied to the offending statement (file + line).</summary>
    /// <param name="RuleId"><c>AUD017</c> or <c>AUD018</c>.</param>
    /// <param name="File">Absolute path of the SQL/template file.</param>
    /// <param name="Line">1-based line number of the offending construct.</param>
    /// <param name="Severity">Always <see cref="FindingSeverity.Error"/>.</param>
    /// <param name="Message">Human-readable explanation, including the required form.</param>
    public sealed record SeedFormFinding(
        string RuleId, string File, int Line, FindingSeverity Severity, string Message);

    /// <summary>The AUD018 rule id — one canonical localization-seed spelling.</summary>
    public const string Aud018 = "AUD018";

    /// <summary>The AUD017 rule id — no pattern-based permission grants.</summary>
    public const string Aud017 = "AUD017";

    // A batch that DEFINES a routine (procedure / function / trigger / view). T-SQL requires CREATE PROCEDURE
    // to be the first statement of its batch, so "is this write inside a routine body?" is answered at the
    // batch level — no need to find the matching END. A routine body's writes are the WRITER's implementation
    // (dbo.LocalizationResource_UpsertValue itself MERGEs the table); a write outside one is an ad-hoc seed.
    private static readonly Regex RoutineDefinitionRegex = new(
        @"\bCREATE\s+(?:OR\s+ALTER\s+)?(?:PROC(?:EDURE)?|FUNCTION|TRIGGER|VIEW)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    // A direct write to dbo.LocalizationResource — INSERT / MERGE / UPDATE. The negative lookahead keeps
    // `LocalizationResource_UpsertValue` (the proc) from matching the table name it is prefixed with.
    private static readonly Regex RawLocalizationWriteRegex = new(
        @"\b(?:INSERT\s+(?:INTO\s+)?|MERGE\s+(?:INTO\s+)?|UPDATE\s+)(?:\[?dbo\]?\s*\.\s*)?\[?LocalizationResource\]?(?![_\w])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    // A call to a localization upsert proc (all three spellings AUD015 reads — see LocalizationScan).
    private static readonly Regex ExecUpsertRegex = new(
        @"\bEXEC(?:UTE)?\s+(?:\[?dbo\]?\s*\.\s*)?\[?(?:LocalizationResource_UpsertValue|Localization_Upsert)\]?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    // A named argument, `@name = <rest>`.
    private static readonly Regex NamedArgRegex = new(
        @"^@(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<rest>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    // A write to the permission catalogue or the role→permission grant table. The negative lookahead keeps
    // `dbo.UserMenuPermissions` (a DIFFERENT table, whose name merely ends in the same word) from matching —
    // it is anchored on `dbo.` + the exact table name.
    private static readonly Regex PermissionWriteRegex = new(
        @"\b(?:INSERT\s+(?:INTO\s+)?|MERGE\s+(?:INTO\s+)?|UPDATE\s+|DELETE\s+FROM\s+)(?:\[?dbo\]?\s*\.\s*)?\[?(?<table>RolePermissions|Permissions)\]?(?![_\w])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex LikeRegex = new(
        @"\bLIKE\b", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// <b>AUD018 — one canonical localization-seed spelling.</b> Scans a single SQL/template file's
    /// <paramref name="content"/> and reports every seed written in a NON-canonical way.
    ///
    /// <para><b>The canonical form</b> (what every scaffold template and <c>AsdamirVault_128</c> already do):
    /// put the rows in a <c>@Seed</c> table variable as <c>(N'Key', N'&lt;culture&gt;', N'Value')</c> tuples,
    /// then loop them through <c>dbo.LocalizationResource_UpsertValue</c>:</para>
    /// <code>
    ///   DECLARE @Seed TABLE ([Key] NVARCHAR(200), [Culture] NVARCHAR(20), [Value] NVARCHAR(MAX));
    ///   INSERT INTO @Seed ([Key],[Culture],[Value]) VALUES
    ///       (N'Page.Title', N'tr-TR', N'Başlık'), (N'Page.Title', N'en-US', N'Title'), …;
    ///   -- … cursor over @Seed …
    ///   EXEC dbo.LocalizationResource_UpsertValue @appId = @SelfApp, @key = @Key,
    ///        @category = N'UI', @culture = @Culture, @value = @Value;
    /// </code>
    ///
    /// <para><b>Why this one and not a plain tuple INSERT/MERGE.</b> The proc is not a stylistic wrapper — it
    /// carries scoping semantics a raw write does NOT reproduce: it maps the SelfApp GUID to
    /// <c>AppId = NULL</c> (the console scope) before the MERGE, and it owns the table shape AppManagement
    /// evolves. A raw <c>MERGE dbo.LocalizationResource … VALUES (@SelfApp, …)</c> would write the GUID
    /// literally and the row would then be invisible to the console's <c>AppId IS NULL</c> read path — a
    /// silent, data-level divergence. So mandating "tuples" ALONE would be wrong; the mandate is
    /// <b>tuples FED THROUGH the proc</b>, which is also the form both completeness gates already parse.</para>
    ///
    /// <para><b>Two violations are reported:</b></para>
    /// <list type="number">
    ///   <item><description><b>ad-hoc write</b> — an <c>INSERT</c>/<c>MERGE</c>/<c>UPDATE</c> straight at
    ///   <c>dbo.LocalizationResource</c>, outside a routine body (a routine body is the writer's own
    ///   implementation and is exempt);</description></item>
    ///   <item><description><b>inline-literal EXEC</b> — one <c>EXEC … _UpsertValue</c> per row with the key
    ///   spelled as a literal argument. It writes the right row, but the rows are then a list of statements
    ///   instead of a data table: unreadable to a machine that does not know the proc's parameter order (this
    ///   is exactly what made AUD015 blind to 276 keys) and unreviewable as a set.</description></item>
    /// </list>
    /// </summary>
    /// <param name="file">Absolute path used to label the findings.</param>
    /// <param name="content">Raw file text (comments are stripped internally).</param>
    public static IReadOnlyList<SeedFormFinding> CheckLocalizationSeedForm(string file, string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var findings = new List<SeedFormFinding>();
        var code = SqlTextScanner.StripComments(content);

        foreach (var batch in SqlTextScanner.SplitBatches(code))
        {
            var inRoutine = RoutineDefinitionRegex.IsMatch(batch.Text);

            if (!inRoutine)
            {
                foreach (Match m in RawLocalizationWriteRegex.Matches(batch.Text))
                {
                    findings.Add(new SeedFormFinding(Aud018, file, LineOf(batch, m.Index), FindingSeverity.Error,
                        "localization seed writes dbo.LocalizationResource directly. The row must be written " +
                        "through dbo.LocalizationResource_UpsertValue — it maps the SelfApp GUID to AppId NULL " +
                        "and owns the table shape, so an ad-hoc INSERT/MERGE can silently land the row in a " +
                        "scope the console never reads. Canonical form: a @Seed table variable of " +
                        "(N'Key', N'<culture>', N'Value') tuples looped through the proc."));
                }
            }

            foreach (var statement in SqlTextScanner.SplitStatements(batch.Text))
            {
                var m = ExecUpsertRegex.Match(statement.Text);
                if (!m.Success) continue;
                if (!HasLiteralKeyArgument(statement.Text[(m.Index + m.Length)..])) continue;

                findings.Add(new SeedFormFinding(Aud018, file, LineOf(batch, statement, m.Index), FindingSeverity.Error,
                    "localization seed calls the upsert proc with the key as an INLINE LITERAL, one statement " +
                    "per row. Put the rows in a @Seed table variable as (N'Key', N'<culture>', N'Value') tuples " +
                    "and loop them through the proc (@key/@culture fed from the table) — a list of EXEC " +
                    "statements is not machine-readable as a seed SET, which is exactly how 276 seeded keys " +
                    "became invisible to AUD015."));
            }
        }

        return findings;
    }

    /// <summary>
    /// <b>AUD017 — no pattern-based permission grants.</b> Scans a single SQL/template file's
    /// <paramref name="content"/> for a statement that writes <c>dbo.Permissions</c> or
    /// <c>dbo.RolePermissions</c> while selecting rows with a <c>LIKE</c> pattern.
    ///
    /// <para><b>What is wrong with <c>JOIN dbo.Permissions p ON p.Name LIKE N'%.read'</c>.</b> The set of
    /// permissions that grant actually confers is written down NOWHERE — it is whatever the catalogue happens
    /// to contain when the statement runs. Add a permission next year whose code merely ends in <c>.read</c>
    /// and it is granted to that role <b>retroactively and silently</b>: nobody edited a grant, no review saw
    /// it, and no static gate can enumerate the result (AUD016 cross-checks NAMES; a wildcard supplies none).
    /// The same wildcard is equally able to MISS a permission that should have been granted —
    /// <c>ent.agentaudit.verify</c> does not end in <c>.read</c> — which is a silent 403 instead. A grant is
    /// an authorization decision, so it must be a reviewable list:
    /// <c>WHERE p.Name IN (N'ent.agentaudit.read', N'ent.agentaudit.verify')</c>.</para>
    ///
    /// <para><b>Scope.</b> Only a statement that WRITES one of those two tables is considered — a read-path
    /// <c>SELECT … WHERE Name LIKE @Category + '%'</c> inside a lookup proc is legitimate and is not flagged,
    /// a write to a different table that merely JOINs <c>dbo.Permissions</c> is not flagged, and a
    /// <c>LIKE</c> against anything else (an audit search, <c>sys.sql_modules</c>) is irrelevant.</para>
    /// </summary>
    /// <param name="file">Absolute path used to label the findings.</param>
    /// <param name="content">Raw file text (comments are stripped internally).</param>
    public static IReadOnlyList<SeedFormFinding> CheckPatternGrants(string file, string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var findings = new List<SeedFormFinding>();
        var code = SqlTextScanner.StripComments(content);

        foreach (var batch in SqlTextScanner.SplitBatches(code))
        {
            foreach (var statement in SqlTextScanner.SplitStatements(batch.Text))
            {
                var write = PermissionWriteRegex.Match(statement.Text);
                if (!write.Success) continue;

                var like = LikeRegex.Match(statement.Text);
                if (!like.Success) continue;

                var table = write.Groups["table"].Value;
                findings.Add(new SeedFormFinding(Aud017, file, LineOf(batch, statement, like.Index), FindingSeverity.Error,
                    $"statement writes dbo.{table} while selecting rows with a LIKE pattern. A pattern-based " +
                    "grant is not a reviewable list: it silently sweeps in every future permission whose code " +
                    "happens to match (and silently misses every one that does not), and no static gate can " +
                    "enumerate what it granted. Name the codes explicitly — p.Name IN (N'…', N'…')."));
            }
        }

        return findings;
    }

    // True when the upsert call's KEY argument is a bare string literal (an inline row) rather than a variable
    // fed from a tuple table. Named form is checked by parameter name; positional form keys off the fact that
    // `key, category, culture` are adjacent in BOTH proc arities — see LocalizationScan.
    private static bool HasLiteralKeyArgument(string argText)
    {
        var args = SqlTextScanner.SplitArguments(argText);

        var sawNamedKey = false;
        foreach (var arg in args)
        {
            var nm = NamedArgRegex.Match(arg);
            if (!nm.Success) continue;
            if (!nm.Groups["name"].Value.Equals("key", StringComparison.OrdinalIgnoreCase)) continue;
            sawNamedKey = true;
            if (SqlTextScanner.TryParseStringLiteral(nm.Groups["rest"].Value, out _)) return true;
        }
        if (sawNamedKey) return false;   // named call whose @key is a variable — canonical

        for (var i = 2; i < args.Count; i++)
        {
            if (!SqlTextScanner.TryParseStringLiteral(args[i], out var culture) || culture is null) continue;
            if (!LocalizationScan.Cultures.Contains(culture)) continue;
            if (SqlTextScanner.TryParseStringLiteral(args[i - 2], out _)) return true;
        }
        return false;
    }

    private static int LineOf(SqlTextScanner.SqlSlice batch, int offsetInBatch)
        => batch.Line + CountNewlines(batch.Text, offsetInBatch);

    private static int LineOf(SqlTextScanner.SqlSlice batch, SqlTextScanner.SqlSlice statement, int offsetInStatement)
        => batch.Line + (statement.Line - 1) + CountNewlines(statement.Text, offsetInStatement);

    private static int CountNewlines(string s, int upTo)
    {
        var count = 0;
        for (var i = 0; i < upTo && i < s.Length; i++) if (s[i] == '\n') count++;
        return count;
    }
}
