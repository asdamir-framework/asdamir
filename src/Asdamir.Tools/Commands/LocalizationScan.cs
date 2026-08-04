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

    /// <summary>Rule id of the localization-COMPLETENESS check — a used key must be seeded SOMEWHERE, in all
    /// three cultures (<see cref="Compare"/>).</summary>
    public const string Aud015 = "AUD015";

    /// <summary>Rule id of the SQL-BACKING check — the seed a used key relies on must be a SQL seed, in all
    /// three cultures; an in-memory mirror alone does not count (<see cref="CompareSqlBacking"/>).</summary>
    public const string Aud019 = "AUD019";

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

    // A call to a localization upsert proc. THREE spellings exist in the tree and all three really write the
    // row, so all three are recognized:
    //   • dbo.LocalizationResource_UpsertValue — AsdamirVault (@appId, @key, @category, @culture, @value)
    //   • dbo.LocalizationResource_UpsertValue — the single-tenant free-mode proc of the SAME name, one
    //     parameter shorter (@key, @category, @culture, @value): no @appId, because a free-mode app's DB is
    //     its own tenant. Positional pairing handles both arities (see ExtractExecSeededPairs).
    //   • dbo.Localization_Upsert — the ORIGINAL migration-001 proc (@Key, @Culture, @Value, @Category, …).
    //     It predates the AppId column's use and always writes AppId NULL (= console scope), so it can seed a
    //     console key but can NEVER seed an app-scoped one. Legacy: still recognized here (the rows exist), and
    //     flagged as non-canonical by AUD018.
    // Only the call HEAD is matched — the ARGUMENTS are lexed, not regexed (see ExtractExecSeededPairs).
    private static readonly Regex ExecUpsertRegex = new(
        @"\bEXEC(?:UTE)?\s+(?:\[?dbo\]?\s*\.\s*)?\[?(?:LocalizationResource_UpsertValue|Localization_Upsert)\]?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    // A named argument, `@name = <rest>`. Used to pair @key/@culture by NAME before falling back to position.
    private static readonly Regex NamedArgRegex = new(
        @"^@(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<rest>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

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

    /// <summary>One localization finding, tied to the USAGE site (file+line) of the key.</summary>
    /// <param name="File">Absolute path of the file where the key is used.</param>
    /// <param name="Line">1-based line number of the usage.</param>
    /// <param name="Key">The localization key (or the dynamic prefix, for a dynamic-key info finding).</param>
    /// <param name="Severity">Info or Error.</param>
    /// <param name="Message">Human-readable explanation of the finding.</param>
    /// <param name="RuleId">Which rule produced it — <see cref="Aud015"/> (completeness, the default) or
    /// <see cref="Aud019"/> (SQL backing).</param>
    public sealed record LocalizationFinding(
        string File, int Line, string Key, FindingSeverity Severity, string Message, string RuleId = Aud015);

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
    ///
    /// <para><b>TWO seed spellings are recognized</b>, because both exist in the tree and both really do
    /// write the row:</para>
    /// <list type="number">
    ///   <item><description>the <b>tuple</b> form — <c>(N'Key', N'tr-TR', N'…')</c> in a <c>VALUES</c> list
    ///   (a <c>@Seed</c> table variable, a <c>MERGE … USING (VALUES …)</c>, an <c>INSERT … VALUES</c>);</description></item>
    ///   <item><description>the <b>EXEC</b> form — <c>EXEC dbo.LocalizationResource_UpsertValue …</c>, in
    ///   both arities: the AsdamirVault proc <c>(@appId, @key, @category, @culture, @value)</c> and the
    ///   single-tenant free-mode proc of the same name <c>(@key, @category, @culture, @value)</c>, named or
    ///   positional.</description></item>
    /// </list>
    ///
    /// <para><b>The blind spot this closes.</b> Until this was taught, the gate matched the tuple form ONLY —
    /// so every key seeded exclusively through <c>EXEC … _UpsertValue</c> (13 AsdamirVault migrations at the
    /// time of writing: the whole billing surface, the audit action labels, the agent-audit ledger seed) was
    /// invisible to it. That direction is loud, not silent (a really-seeded key gets reported as unseeded), but
    /// it trains readers to distrust the gate — and it is exactly the "the gate reads SQL as TEXT, so it goes
    /// blind when the spelling changes" failure that <see cref="SqlTextScanner"/> was written for. AUD018
    /// attacks the same problem from the other end: it constrains which spellings a NEW seed may use.</para>
    ///
    /// <para>SQL comments are stripped first (<see cref="SqlTextScanner.StripComments"/>): a seed tuple or an
    /// <c>EXEC</c> sitting inside a <c>--</c> or <c>/* … */</c> comment is commented-OUT, so it never reaches
    /// the database and must not count as a seeded key.</para>
    /// </summary>
    public static IReadOnlyDictionary<string, HashSet<string>> ExtractSeededKeys(string sqlContent)
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var code = SqlTextScanner.StripComments(sqlContent);

        foreach (Match m in SqlTupleRegex.Matches(code))
            Record(map, m.Groups[1].Value.Replace("''", "'"), m.Groups[2].Value);

        foreach (var (key, culture) in ExtractExecSeededPairs(code))
            Record(map, key, culture);

        return map;
    }

    private static void Record(Dictionary<string, HashSet<string>> map, string key, string culture)
    {
        if (!map.TryGetValue(key, out var set)) map[key] = set = new HashSet<string>(StringComparer.Ordinal);
        set.Add(culture);
    }

    /// <summary>
    /// Pulls <c>(key, culture)</c> pairs out of every <c>EXEC … LocalizationResource_UpsertValue</c> call in
    /// already-comment-stripped <paramref name="code"/>. The argument list is LEXED
    /// (<see cref="SqlTextScanner.SplitArguments"/>), never regexed, so a comma inside a literal
    /// (<c>N'Sayfa 1, 2'</c>) cannot shift the argument positions.
    ///
    /// <para>Pairing is tried by NAME first (<c>@key = N'…'</c> + <c>@culture = N'…'</c>, order-independent);
    /// when the call is positional it falls back to POSITION — for every argument that is a bare culture
    /// literal, the key is the bare literal <b>two arguments earlier</b>, which holds for BOTH proc arities
    /// because <c>key, category, culture</c> are adjacent in both:</para>
    /// <code>
    ///   EXEC dbo.LocalizationResource_UpsertValue @SelfAppId, N'Key', N'UI', N'tr-TR', N'…'   -- AsdamirVault
    ///   EXEC dbo.LocalizationResource_UpsertValue            N'Key', N'UI', N'tr-TR', N'…'   -- free mode
    /// </code>
    ///
    /// <para>A call whose key or culture is a VARIABLE (the canonical <c>@Seed</c>-table + cursor loop) yields
    /// nothing here on purpose — its keys are already collected from the tuple <c>VALUES</c> list.</para>
    /// </summary>
    private static IEnumerable<(string Key, string Culture)> ExtractExecSeededPairs(string code)
    {
        var pairs = new List<(string, string)>();

        foreach (var batch in SqlTextScanner.SplitBatches(code))
        {
            foreach (var statement in SqlTextScanner.SplitStatements(batch.Text))
            {
                var m = ExecUpsertRegex.Match(statement.Text);
                if (!m.Success) continue;

                var args = SqlTextScanner.SplitArguments(statement.Text[(m.Index + m.Length)..]);

                // --- named pairing (order-independent) ---
                string? namedKey = null, namedCulture = null;
                foreach (var arg in args)
                {
                    var nm = NamedArgRegex.Match(arg);
                    if (!nm.Success) continue;
                    if (!SqlTextScanner.TryParseStringLiteral(nm.Groups["rest"].Value, out var lit) || lit is null) continue;
                    var name = nm.Groups["name"].Value;
                    if (name.Equals("key", StringComparison.OrdinalIgnoreCase)) namedKey = lit;
                    else if (name.Equals("culture", StringComparison.OrdinalIgnoreCase)) namedCulture = lit;
                }
                if (namedKey is not null && namedCulture is not null && Cultures.Contains(namedCulture))
                {
                    pairs.Add((namedKey, namedCulture));
                    continue;
                }

                // --- positional pairing: key, category, culture are adjacent in both proc arities ---
                for (var i = 2; i < args.Count; i++)
                {
                    if (!SqlTextScanner.TryParseStringLiteral(args[i], out var culture) || culture is null) continue;
                    if (!Cultures.Contains(culture)) continue;
                    if (!SqlTextScanner.TryParseStringLiteral(args[i - 2], out var key) || key is null) continue;
                    pairs.Add((key, culture));
                }
            }
        }

        return pairs;
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
    /// Cross-checks USED keys against the MERGED (SQL + in-memory) seed map and produces AUD015 findings:
    /// a used static key with no seed → ERROR (raw key renders); seeded in fewer than all three cultures →
    /// ERROR listing the missing cultures; a dynamic key → INFO (its runtime value can't be verified).
    /// Seeded-but-unused keys are intentionally NOT flagged (shared chrome like <c>Common.*</c> is broad).
    ///
    /// <para>Because the map is MERGED, this cannot tell a real SQL seed from an in-memory mirror entry — a
    /// key present only in the mirror passes here and still renders raw in production. That direction is
    /// <see cref="CompareSqlBacking"/> (AUD019); run both.</para>
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

    /// <summary>
    /// Cross-checks USED keys against the <b>SQL</b> seed map alone (AUD019) — the direction
    /// <see cref="Compare"/> cannot see, because it compares against the MERGED corpus in which an in-memory
    /// mirror entry is indistinguishable from a real SQL seed.
    ///
    /// <para><b>The rule.</b> Every key used in code must have a <b>SQL</b> seed in all three cultures. The
    /// in-memory seed (<c>UiLocalization.cs</c> and its siblings) is a <c>Persistence:UseInMemory</c> mirror
    /// for tests and demos — it is NOT what <c>asdamir db apply</c> runs. A key that exists only there passes
    /// AUD015 and still renders as the raw key (or blank) in production, because the live
    /// <c>LocalizationResource</c> table has no row for it. An in-memory mirror does not substitute for a SQL
    /// seed; it is required IN ADDITION (the framework rule that the two must mirror each other is unchanged —
    /// this adds a requirement, it does not swap one for the other).</para>
    ///
    /// <para><b>Two failure kinds, deliberately distinguished</b> — they have different fixes:</para>
    /// <list type="number">
    ///   <item><description><b>no SQL seed at all</b> — the key resolves only from the in-memory mirror; a
    ///   whole seed row set is missing from the migrations;</description></item>
    ///   <item><description><b>SQL seed in fewer than three cultures</b> — the SQL seed exists but is partial,
    ///   and the in-memory mirror (which counts for all three cultures, since its form carries no culture)
    ///   silently papers over the gap.</description></item>
    /// </list>
    ///
    /// <para><b>No overlap with AUD015, by construction.</b> A key that is in NEITHER corpus is already
    /// AUD015's "used but never seeded" error, so AUD019 stays quiet for it — one key, one finding, one fix.
    /// AUD019 therefore fires exactly on the keys AUD015 lets through: those whose green comes from the
    /// in-memory mirror.</para>
    ///
    /// <para><b>Why it exists even though it reports zero today.</b> The offending set is empty right now, so
    /// adding the gate costs nothing — it lands as a no-op. But today's zero is the result of a FIX, not of
    /// discipline: 186 keys were in this state an hour before it was written, and without a gate the count
    /// climbs straight back. Adding it later would first require a cleanup migration. AUD018 does not cover
    /// this: AUD018 constrains the seed's FORM, AUD019 asserts the seed's EXISTENCE.</para>
    ///
    /// <para>There is deliberately no AUD019-specific suppression and no allowlist — the set is empty, so no
    /// exemption is needed, and a NEW key has no legitimate reason to be in-memory-only. (A usage line already
    /// carrying <c>audit-lint:ignore AUD015</c> contributes no used keys at all — see
    /// <see cref="ExtractUsedKeys"/> — so it is out of scope for both rules; that filter is shared, not an
    /// AUD019 escape hatch.)</para>
    /// </summary>
    /// <param name="file">Absolute path used to label the findings.</param>
    /// <param name="used">Keys used in <paramref name="file"/> (from <see cref="ExtractUsedKeys"/>).</param>
    /// <param name="sqlSeeded">Keys seeded by SQL seeds/templates ONLY (from <see cref="ExtractSeededKeys"/>).</param>
    /// <param name="mergedSeeded">The full corpus (SQL + in-memory) — used only to defer to AUD015.</param>
    /// <returns>The AUD019 findings for this file (never Info — a dynamic key is skipped entirely).</returns>
    public static IReadOnlyList<LocalizationFinding> CompareSqlBacking(
        string file,
        IReadOnlyList<UsedKey> used,
        IReadOnlyDictionary<string, HashSet<string>> sqlSeeded,
        IReadOnlyDictionary<string, HashSet<string>> mergedSeeded)
    {
        var findings = new List<LocalizationFinding>();
        foreach (var u in used)
        {
            // A dynamic key's runtime value-set can't be resolved statically — AUD015 already reports it as
            // INFO; repeating that under AUD019 would add noise and no information.
            if (u.IsDynamic) continue;

            // Not seeded ANYWHERE → AUD015's "used but never seeded". Don't double-report the same key.
            if (!mergedSeeded.TryGetValue(u.Key, out var merged) || merged.Count == 0) continue;

            var hasSql = sqlSeeded.TryGetValue(u.Key, out var sqlCultures) && sqlCultures.Count > 0;
            if (!hasSql)
            {
                findings.Add(new LocalizationFinding(file, u.Line, u.Key, FindingSeverity.Error,
                    $"localization key '{u.Key}' has NO SQL seed — it resolves only from an in-memory seed " +
                    "(the `Persistence:UseInMemory` mirror), which is NOT what `db apply` runs. The live " +
                    "LocalizationResource table has no row for it, so the raw key renders in production. " +
                    "Add it to a SQL seed (localize_*/register_*/seed_*.sql or a migration) in all three " +
                    "cultures — the in-memory mirror is required IN ADDITION, never INSTEAD.",
                    Aud019));
                continue;
            }

            var missing = Cultures.Where(c => !sqlCultures!.Contains(c)).ToList();
            if (missing.Count > 0)
            {
                var have = Cultures.Where(sqlCultures!.Contains);
                findings.Add(new LocalizationFinding(file, u.Line, u.Key, FindingSeverity.Error,
                    $"localization key '{u.Key}' is SQL-seeded only in [{string.Join(", ", have)}]; " +
                    $"missing [{string.Join(", ", missing)}] in SQL. An in-memory seed covers the gap, so " +
                    "AUD015 is green — but the live database is short those cultures and will render the " +
                    "raw key there. Seed the missing culture(s) in SQL.",
                    Aud019));
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
