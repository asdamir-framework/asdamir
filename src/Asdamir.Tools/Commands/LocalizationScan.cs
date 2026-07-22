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
/// Pure, testable core of the localization-completeness gate (AUD015). Extracts the localization keys
/// a codebase <b>uses</b> (<c>L["Key"]</c> and localizer indexers), the keys a codebase <b>seeds</b>
/// (SQL tuples <c>(N'Key', N'tr-TR', …)</c> and in-memory dictionary literals <c>["Key"] = "…"</c>),
/// and compares the two so a used-but-unseeded (or seeded-in-&lt;3-cultures) key is caught before the
/// raw key renders on screen.
///
/// <para>Both <c>asdamir audit localization</c> (static gate) and <c>asdamir localization verify</c>
/// (live apply-drift) call into this, so the seed parser is shared — one implementation, one behaviour.</para>
/// </summary>
public static class LocalizationScan
{
    /// <summary>The three cultures Asdamir localizes to. A key must be seeded in ALL three.</summary>
    public static readonly IReadOnlyList<string> Cultures = new[] { "tr-TR", "en-US", "ru-RU" };

    // L["Key"] / Localizer["Key"] / _localizer["Key"] / localizer["Key"] — a static double-quoted literal.
    private static readonly Regex UsedStaticRegex = new(
        @"\b(?:L|Localizer|_localizer|localizer)\[""((?:[^""\\]|\\.)+)""\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Dynamic key: L[$"Prefix.{x}"] (interpolated) or L[identifier] (a bare variable). Captures the
    // interpolated PREFIX (group "prefix", up to the first '{') when present.
    private static readonly Regex UsedDynamicRegex = new(
        @"\b(?:L|Localizer|_localizer|localizer)\[(?:\$""(?<prefix>(?:[^""\\{]|\\.)*)(?:\{|"")|(?<var>[A-Za-z_][A-Za-z0-9_.]*)\])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // A SQL seed tuple: (N'Key.Name', N'tr-TR', …). Key may contain dots and doubled '' apostrophes.
    private static readonly Regex SqlTupleRegex = new(
        @"\(\s*N'((?:[^']|'')+)'\s*,\s*N'(tr-TR|en-US|ru-RU)'",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // An in-memory dictionary literal: ["Key"] = "…"  (C# / .sbn). Culture is unknown from this form.
    private static readonly Regex InMemorySeedRegex = new(
        @"\[""((?:[^""\\]|\\.)+)""\]\s*=\s*""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Razor `@* … *@` and HTML `<!-- … -->` comments (possibly multi-line). Blanked before key matching so
    // a `L["…"]` inside a commented-out markup block or a doc example isn't counted as a real usage. Line
    // `//`/`///` comments are stripped separately, per line (string-literal aware — see StripLineComments).
    private static readonly Regex MarkupCommentRegex = new(
        @"@\*.*?\*@|<!--.*?-->",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    /// <summary>Severity of a <see cref="LocalizationFinding"/>, mirroring <see cref="AuditSeverity"/>.</summary>
    public enum FindingSeverity
    {
        /// <summary>Informational — e.g. a dynamic key whose runtime value can't be checked statically.</summary>
        Info,

        /// <summary>A used key that is unseeded or seeded in fewer than all three cultures — the raw key renders.</summary>
        Error,
    }

    /// <summary>One localization-completeness finding, tied to the USAGE site (file+line) of the key.</summary>
    /// <param name="File">Absolute path of the file where the key is used.</param>
    /// <param name="Line">1-based line number of the usage.</param>
    /// <param name="Key">The localization key (or the dynamic prefix, for a dynamic-key info finding).</param>
    /// <param name="Severity">Info or Error.</param>
    /// <param name="Message">Human-readable explanation of the finding.</param>
    public sealed record LocalizationFinding(
        string File, int Line, string Key, FindingSeverity Severity, string Message);

    /// <summary>A localization key used in code, with its usage site and whether it was resolved dynamically.</summary>
    /// <param name="Key">The literal key, or the interpolation prefix when <paramref name="IsDynamic"/>.</param>
    /// <param name="Line">1-based line number of the usage.</param>
    /// <param name="IsDynamic">True when the key was a <c>$"…{x}"</c> interpolation or a bare variable.</param>
    public sealed record UsedKey(string Key, int Line, bool IsDynamic);

    /// <summary>
    /// Extracts every localization key USED in a single file's <paramref name="content"/>. Honors
    /// <c>// audit-lint:ignore AUD015</c> on the usage line (that line's keys are dropped) and
    /// <c>audit-lint:skip-file</c> anywhere in the file (returns nothing). Static literal keys and
    /// dynamic (interpolated/variable) keys are both returned, flagged via <see cref="UsedKey.IsDynamic"/>.
    /// </summary>
    public static IReadOnlyList<UsedKey> ExtractUsedKeys(string content)
    {
        var rawLines = content.Replace("\r\n", "\n").Split('\n');

        // File-level opt-out — same directive form as audit-lint. Tested on the RAW lines so a suppression
        // written inside a razor/HTML comment isn't blanked away before we see it.
        foreach (var l in rawLines)
        {
            if (l.TrimStart(' ', '\t', '/').StartsWith("audit-lint:skip-file", StringComparison.Ordinal))
                return Array.Empty<UsedKey>();
        }

        // Blank out razor `@* … *@` / HTML `<!-- … -->` comment spans (multi-line, newline-preserving so
        // line numbers stay accurate) BEFORE matching — a key named in a commented-out markup block or a
        // doc example must not count as a usage. Per-line `//`/`///` comments are stripped below.
        var scanLines = MarkupCommentRegex.Replace(content, BlankOutPreservingLines)
            .Replace("\r\n", "\n").Split('\n');

        var result = new List<UsedKey>();
        for (var i = 0; i < rawLines.Length; i++)
        {
            // Suppression markers live IN a comment, so test the RAW line — but match keys on the
            // comment-STRIPPED line so a key merely named in a `//`/`///` or razor/HTML comment (or a doc
            // example like `L[$"Prefix.{x}"]`) isn't mistaken for a real usage. String-literal aware.
            if (IsSuppressed(rawLines[i])) continue;
            var scan = AuditLintCommand.StripLineComments(scanLines[i]);

            foreach (Match m in UsedStaticRegex.Matches(scan))
                result.Add(new UsedKey(Unescape(m.Groups[1].Value), i + 1, IsDynamic: false));

            foreach (Match m in UsedDynamicRegex.Matches(scan))
            {
                // A dynamic $"Prefix.{x}" capture, or a bare-variable key (prefix empty → still dynamic).
                var prefix = m.Groups["prefix"].Success ? m.Groups["prefix"].Value : "";
                result.Add(new UsedKey(prefix, i + 1, IsDynamic: true));
            }
        }
        return result;
    }

    /// <summary>
    /// Extracts SEEDED keys → the set of cultures each is seeded in, from a single SQL seed file's
    /// <paramref name="sqlContent"/>. Recognizes only the three Asdamir cultures; a doubled <c>''</c>
    /// in a key is un-escaped to a single apostrophe.
    /// </summary>
    public static IReadOnlyDictionary<string, HashSet<string>> ExtractSeededKeys(string sqlContent)
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (Match m in SqlTupleRegex.Matches(sqlContent))
        {
            var key = m.Groups[1].Value.Replace("''", "'");
            var culture = m.Groups[2].Value;
            if (!map.TryGetValue(key, out var set)) map[key] = set = new HashSet<string>(StringComparer.Ordinal);
            set.Add(culture);
        }
        return map;
    }

    /// <summary>
    /// Extracts keys seeded via an in-memory dictionary literal (<c>["Key"] = "…"</c>) in C#/.sbn
    /// localization seed code. The culture cannot be determined from this form, so — per the framework
    /// rule that the in-memory seed MUST mirror the DB seed across all cultures — an in-memory-seeded key
    /// is treated as satisfying ALL THREE cultures.
    /// </summary>
    public static IReadOnlyDictionary<string, HashSet<string>> ExtractInMemorySeededKeys(string content)
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (Match m in InMemorySeedRegex.Matches(content))
        {
            var key = Unescape(m.Groups[1].Value);
            map[key] = new HashSet<string>(Cultures, StringComparer.Ordinal);
        }
        return map;
    }

    /// <summary>Merges seed maps in place — <paramref name="into"/> gains every culture from <paramref name="from"/>.</summary>
    public static void MergeSeeds(Dictionary<string, HashSet<string>> into, IReadOnlyDictionary<string, HashSet<string>> from)
    {
        foreach (var (key, cultures) in from)
        {
            if (!into.TryGetValue(key, out var set)) into[key] = set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var c in cultures) set.Add(c);
        }
    }

    /// <summary>
    /// Cross-checks USED keys against the SEEDED map and produces findings:
    /// a used static key with no seed → ERROR (raw key renders); seeded in fewer than all three cultures →
    /// ERROR listing the missing cultures; a dynamic key → INFO (its runtime value can't be verified).
    /// Seeded-but-unused keys are intentionally NOT flagged (shared chrome like <c>Common.*</c> is broad).
    /// </summary>
    /// <param name="file">Absolute path used to label the findings.</param>
    /// <param name="used">Keys used in <paramref name="file"/> (from <see cref="ExtractUsedKeys"/>).</param>
    /// <param name="seeded">The merged seed map (SQL + in-memory) across the whole scanned tree.</param>
    public static IReadOnlyList<LocalizationFinding> Compare(
        string file, IReadOnlyList<UsedKey> used, IReadOnlyDictionary<string, HashSet<string>> seeded)
    {
        var findings = new List<LocalizationFinding>();
        foreach (var u in used)
        {
            if (u.IsDynamic)
            {
                var label = string.IsNullOrEmpty(u.Key) ? "(variable)" : u.Key;
                findings.Add(new LocalizationFinding(file, u.Line, u.Key, FindingSeverity.Info,
                    $"dynamic localization key (prefix '{label}') — the value-set is resolved at runtime; " +
                    "verify every value in the set is seeded in all three cultures, or suppress with `// audit-lint:ignore AUD015`."));
                continue;
            }

            if (!seeded.TryGetValue(u.Key, out var cultures) || cultures.Count == 0)
            {
                findings.Add(new LocalizationFinding(file, u.Line, u.Key, FindingSeverity.Error,
                    $"localization key '{u.Key}' is used but never seeded (no localize_*/register_*/seed_* entry, " +
                    "no in-memory seed) — the raw key will render on screen."));
                continue;
            }

            var missing = Cultures.Where(c => !cultures.Contains(c)).ToList();
            if (missing.Count > 0)
            {
                var have = Cultures.Where(cultures.Contains);
                findings.Add(new LocalizationFinding(file, u.Line, u.Key, FindingSeverity.Error,
                    $"localization key '{u.Key}' seeded only in [{string.Join(", ", have)}]; " +
                    $"missing [{string.Join(", ", missing)}]."));
            }
        }
        return findings;
    }

    /// <summary>True when <paramref name="sqlFilePath"/> is a localization seed source we cross-check against:
    /// a <c>localize_*/register_*/seed_*.sql</c> file, or any <c>.sql</c> under a <c>db/admin-onboarding/</c>
    /// or <c>db/migrations/</c> directory.</summary>
    public static bool IsSeedSqlFile(string sqlFilePath)
    {
        var name = Path.GetFileName(sqlFilePath);
        if (name.StartsWith("localize_", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("register_", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("seed_", StringComparison.OrdinalIgnoreCase))
            return true;

        var norm = sqlFilePath.Replace('\\', '/');
        return norm.Contains("/db/admin-onboarding/", StringComparison.OrdinalIgnoreCase)
            || norm.Contains("/db/migrations/", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>True when a <c>.sbn</c>/<c>.cs</c> file's PATH marks it as a localization seed template/code
    /// (its path contains <c>Localization</c>), so its SQL tuples and in-memory literals count as seeds.</summary>
    public static bool IsLocalizationCodeFile(string filePath)
        => filePath.Replace('\\', '/').Contains("Localization", StringComparison.OrdinalIgnoreCase);

    // Replaces a matched comment span with blanks, preserving \r and \n so downstream 1-based line
    // numbers stay accurate after a multi-line `@* … *@` / `<!-- … -->` comment is removed.
    private static string BlankOutPreservingLines(Match m)
    {
        var sb = new System.Text.StringBuilder(m.Length);
        foreach (var c in m.Value) sb.Append(c is '\n' or '\r' ? c : ' ');
        return sb.ToString();
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

    /// <summary>True when the line carries a <c>// audit-lint:ignore AUD015</c> suppression marker.</summary>
    private static bool IsSuppressed(string line)
    {
        const string marker = "audit-lint:ignore";
        var idx = line.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return false;
        var tail = line[(idx + marker.Length)..];
        return Regex.IsMatch(tail, @"\bAUD015\b");
    }
}
