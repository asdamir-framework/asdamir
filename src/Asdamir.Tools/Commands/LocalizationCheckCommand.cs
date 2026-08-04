// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.CommandLine;
using System.Text;

namespace Asdamir.Tools.Commands;

/// <summary>
/// <c>asdamir audit localization [--path dir] [--min-severity info|warning|error] [--format text|json] [--include-tests]</c>
///
/// Layer A of the localization gate. Cross-checks every localization key USED in <c>.razor</c>/<c>.cs</c>
/// code (<c>L["Key"]</c> / localizer indexers) against the keys SEEDED in the tree's SQL seeds
/// (<c>localize_*/register_*/seed_*.sql</c>, anything under <c>db/admin-onboarding/</c> or
/// <c>db/migrations/</c>) and localization seed code (files whose path contains <c>Localization</c>).
/// It runs TWO rules over the same scan:
///
/// <list type="bullet">
///   <item><description><b>AUD015 — completeness.</b> A used key with no seed anywhere, or seeded in fewer
///   than all three cultures, falls through to the raw key on screen.</description></item>
///   <item><description><b>AUD019 — SQL backing.</b> The seed a used key relies on must be a <b>SQL</b> seed,
///   in all three cultures. An in-memory seed (the <c>Persistence:UseInMemory</c> mirror, e.g.
///   <c>UiLocalization.cs</c>) is NOT what <c>db apply</c> runs, so a key that exists only there passes
///   AUD015 and still renders raw against a live database. The in-memory mirror is required IN ADDITION to
///   the SQL seed, never INSTEAD of it. The two rules never both fire on one key: a key seeded nowhere is
///   AUD015's, a key seeded only in memory is AUD019's.</description></item>
/// </list>
///
/// <para><b>Seed auto-discovery.</b> A Model-A (central-model) app keeps its seeds under
/// <c>db/admin-onboarding/*.sql</c> at the repo ROOT — <b>outside</b> a <c>--path src</c> scope — so a
/// naive scan of <c>src</c> would report every central key as "never seeded" (a false alarm). To keep the
/// rule in the command and not in human memory, seed sources are therefore ALSO discovered from the repo
/// root (the nearest <c>.git</c> ancestor of <c>--path</c>) IN ADDITION to <c>--path</c>. The USAGE scan
/// stays <c>--path</c>-scoped. The expansion is VISIBLE, never silent: the summary prints
/// <c>N seed source(s) (+M auto-discovered outside --path)</c>.</para>
///
/// Exit codes:
///   0  no findings at or above --min-severity (also: no seed sources found — nothing to cross-check)
///   1  at least one finding at or above --min-severity
///   2  invalid arguments
///
/// Suppression: <c>// audit-lint:ignore AUD015</c> on the usage line, <c>audit-lint:skip-file</c> per file.
/// Those drop the line from the USAGE corpus entirely, so they apply to both rules; <b>AUD019 has no
/// suppression of its own and no allowlist</b> — the offending set is empty, and a new key has no legitimate
/// reason to live only in the in-memory mirror.
/// Dynamic keys (<c>L[$"Prefix.{x}"]</c> / <c>L[variable]</c>) are reported as INFO by AUD015 — never an
/// error, because the runtime value-set can't be resolved statically — and are skipped by AUD019.
/// </summary>
public static class LocalizationCheckCommand
{
    private static readonly HashSet<string> SkippedDirNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "node_modules", ".git", "artifacts", "publish", "TestResults",
    };

    private static readonly HashSet<string> TestDirNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "tests", "test",
    };

    /// <summary>Builds the <c>audit localization</c> subcommand.</summary>
    public static Command Build()
    {
        var pathOpt = new Option<DirectoryInfo>(
            new[] { "--path", "-p" },
            description: "Directory to scan recursively. Defaults to the current working directory.",
            getDefaultValue: () => new DirectoryInfo(Directory.GetCurrentDirectory()));

        var severityOpt = new Option<string>(
            new[] { "--min-severity", "-s" },
            description: "Minimum severity to surface and to use for the exit code. One of: info, warning, error.",
            getDefaultValue: () => "warning");

        var formatOpt = new Option<string>(
            new[] { "--format", "-f" },
            description: "Output format. One of: text (default), json.",
            getDefaultValue: () => "text");

        var includeTestsOpt = new Option<bool>(
            new[] { "--include-tests" },
            description: "Also scan `tests/` and `test/` directories. Off by default.",
            getDefaultValue: () => false);

        var cmd = new Command("localization",
            "Cross-check used localization keys against the tree's seeds (AUD015 completeness, AUD019 SQL backing).")
        {
            pathOpt, severityOpt, formatOpt, includeTestsOpt,
        };

        cmd.SetHandler(
            (path, severity, format, includeTests) => Environment.Exit(Execute(path, severity, format, includeTests)),
            pathOpt, severityOpt, formatOpt, includeTestsOpt);
        return cmd;
    }

    /// <summary>
    /// Runs the gate and RETURNS the exit code (0 clean / 1 findings / 2 bad args) instead of terminating,
    /// so it is unit-testable. <see cref="Build"/>'s handler wraps this in <see cref="Environment.Exit(int)"/>.
    /// Output goes to <paramref name="outWriter"/> (default <see cref="Console.Out"/>) and errors to
    /// <paramref name="errWriter"/> (default <see cref="Console.Error"/>).
    /// </summary>
    /// <param name="path">The directory whose CODE is scanned for used keys (the usage scope).</param>
    /// <param name="severityRaw">Minimum severity for surfacing + the exit code (info|warning|error).</param>
    /// <param name="formatRaw">Output format (text|json).</param>
    /// <param name="includeTests">When true, also scans <c>tests/</c>/<c>test/</c> directories.</param>
    /// <param name="outWriter">Where normal output is written; defaults to <see cref="Console.Out"/>.</param>
    /// <param name="errWriter">Where argument errors are written; defaults to <see cref="Console.Error"/>.</param>
    /// <returns>0 no findings, 1 findings at/above the threshold, 2 invalid arguments.</returns>
    internal static int Execute(
        DirectoryInfo path, string severityRaw, string formatRaw, bool includeTests,
        TextWriter? outWriter = null, TextWriter? errWriter = null)
    {
        var @out = outWriter ?? Console.Out;
        var err = errWriter ?? Console.Error;

        if (!path.Exists)
        {
            err.WriteLine($"Path '{path.FullName}' does not exist.");
            return 2;
        }

        // AUD015 emits Info and Error, AUD019 Error only — neither emits Warning. `warning` (the default)
        // therefore surfaces exactly the Error findings — the same threshold audit-lint uses to gate a commit.
        if (!Enum.TryParse<AuditSeverity>(severityRaw, ignoreCase: true, out var minSeverity))
        {
            err.WriteLine($"Invalid --min-severity '{severityRaw}'. Use: info, warning, error.");
            return 2;
        }

        var format = formatRaw.ToLowerInvariant();
        if (format != "text" && format != "json")
        {
            err.WriteLine($"Invalid --format '{formatRaw}'. Use: text, json.");
            return 2;
        }

        // Pass 1: collect the seed corpus from UNDER --path. The SQL seeds and the in-memory mirror are kept
        // in SEPARATE maps — merging them (as this used to) makes an in-memory-only key indistinguishable
        // from a really-seeded one, which is exactly the hole AUD019 closes. AUD015 compares against the
        // union; AUD019 compares against `sqlSeeded` alone.
        var sqlSeeded = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var inMemorySeeded = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var seenSeedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // absolute paths already merged
        var seedSources = 0;

        foreach (var file in EnumerateFiles(path.FullName, includeTests, "*.sql", "*.sbn", "*.cs", "*.razor"))
            if (TryMergeSeedFile(file, sqlSeeded, inMemorySeeded, seenSeedFiles))
                seedSources++;

        // Pass 1b: SEED AUTO-DISCOVERY from the repo root. A Model-A app's central seeds live at the repo
        // root's db/admin-onboarding (outside a `--path src`), so scanning only --path false-positives every
        // central key. Discover the repo root as the nearest .git ancestor of --path (never the home dir /
        // an unrelated parent), then scan it for seed sources not already counted under --path. The USAGE
        // scan below stays --path-scoped — only SEED discovery widens.
        var autoDiscovered = 0;
        var repoRoot = FindRepoRoot(path.FullName);
        if (repoRoot is not null && !PathsEqual(repoRoot, path.FullName))
        {
            foreach (var file in EnumerateFiles(repoRoot, includeTests, "*.sql", "*.sbn", "*.cs", "*.razor"))
                if (TryMergeSeedFile(file, sqlSeeded, inMemorySeeded, seenSeedFiles))
                    autoDiscovered++;
        }

        // Zero-seed-source guard: pointing the scan at a dir with only code AND finding no seeds anywhere in
        // the repo would otherwise report every used key as unseeded — a false alarm. Say so and exit clean.
        if (seedSources + autoDiscovered == 0)
        {
            @out.WriteLine($"audit localization: no localization seed files found under '{path.FullName}' " +
                           "(nor auto-discovered from the repo root) — nothing to cross-check; " +
                           "seeds may live elsewhere (point --path at the tree that holds them).");
            return 0;
        }

        // The union of both corpora — what AUD015 has always compared against.
        var seeded = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        LocalizationScan.MergeSeeds(seeded, sqlSeeded);
        LocalizationScan.MergeSeeds(seeded, inMemorySeeded);

        // Pass 2: collect used keys from code UNDER --path and compare against the (widened) seed maps —
        // AUD015 against the union, AUD019 against the SQL corpus alone.
        var findings = new List<LocalizationScan.LocalizationFinding>();
        var filesScanned = 0;
        foreach (var file in EnumerateFiles(path.FullName, includeTests, "*.cs", "*.razor"))
        {
            filesScanned++;
            var text = TryRead(file);
            if (text is null) continue;
            var used = LocalizationScan.ExtractUsedKeys(text);
            if (used.Count == 0) continue;
            findings.AddRange(LocalizationScan.Compare(file, used, seeded));
            findings.AddRange(LocalizationScan.CompareSqlBacking(file, used, sqlSeeded, seeded));
        }

        // Filter by min severity after scanning so JSON output is filtered too. Info < warning ≤ Error.
        var threshold = minSeverity == AuditSeverity.Info
            ? LocalizationScan.FindingSeverity.Info
            : LocalizationScan.FindingSeverity.Error;
        findings.RemoveAll(f => f.Severity < threshold);

        if (format == "json") EmitJson(@out, findings, filesScanned, seedSources, autoDiscovered, minSeverity);
        else EmitText(@out, findings, filesScanned, seedSources, autoDiscovered, minSeverity, path.FullName);

        return findings.Count == 0 ? 0 : 1;
    }

    // Merges one file's seeds if it is a seed source not already merged (deduped by absolute path so a file
    // reachable from both --path and the repo root is counted once). Returns true when it contributed a NEW
    // seed source. Per-extension rule: a *.sql seed file yields SQL tuples/EXECs; a Localization *.sbn/*.cs
    // yields SQL tuples/EXECs (inside the template, which renders into a real seed file) AND in-memory
    // `["K"]="…"` literals.
    //
    // The two kinds go into SEPARATE maps on purpose: a SQL tuple inside a `.sbn` template is a real seed
    // (it becomes SQL that `db apply` runs), whereas a `["K"]="…"` dictionary literal is only the
    // `Persistence:UseInMemory` mirror. AUD019 can only tell them apart if they are never conflated here.
    private static bool TryMergeSeedFile(
        string file,
        Dictionary<string, HashSet<string>> sqlSeeded,
        Dictionary<string, HashSet<string>> inMemorySeeded,
        HashSet<string> seen)
    {
        var ext = Path.GetExtension(file).ToLowerInvariant();
        var isSeed = (ext == ".sql" && LocalizationScan.IsSeedSqlFile(file))
                  || ((ext == ".sbn" || ext == ".cs") && LocalizationScan.IsLocalizationCodeFile(file));
        if (!isSeed) return false;

        var full = Path.GetFullPath(file);
        if (seen.Contains(full)) return false;

        var text = TryRead(file);
        if (text is null) return false;

        seen.Add(full);
        LocalizationScan.MergeSeeds(sqlSeeded, LocalizationScan.ExtractSeededKeys(text));
        if (ext != ".sql")
            LocalizationScan.MergeSeeds(inMemorySeeded, LocalizationScan.ExtractInMemorySeededKeys(text));
        return true;
    }

    // Finds the repo root by walking up from `startDir` to the nearest directory that contains a `.git`
    // entry (a directory for a normal clone, a file for a worktree/submodule). Returns null when none is
    // found before the filesystem root — in that case auto-discovery is skipped (we never scan the home
    // dir or an unrelated parent tree). This keeps the widening bounded to the current repository.
    private static string? FindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(Path.GetFullPath(startDir));
        while (dir is not null)
        {
            var git = Path.Combine(dir.FullName, ".git");
            if (Directory.Exists(git) || File.Exists(git)) return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }

    private static bool PathsEqual(string a, string b)
        => string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
            StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<string> EnumerateFiles(string root, bool includeTests, params string[] globs)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            foreach (var sub in Directory.EnumerateDirectories(dir))
            {
                var name = Path.GetFileName(sub);
                if (SkippedDirNames.Contains(name)) continue;
                if (!includeTests && TestDirNames.Contains(name)) continue;
                stack.Push(sub);
            }
            foreach (var pattern in globs)
                foreach (var file in Directory.EnumerateFiles(dir, pattern))
                    yield return file;
        }
    }

    private static string? TryRead(string path)
    {
        try { return File.ReadAllText(path); }
        catch { return null; }
    }

    // Renders "N seed source(s)" plus the "(+M auto-discovered outside --path)" suffix when M > 0 — so a
    // reader always sees exactly which seed corpus was cross-checked (visible widening, never silent).
    private static string SeedCorpus(int seedSources, int autoDiscovered)
        => autoDiscovered > 0
            ? $"{seedSources} seed source(s) (+{autoDiscovered} auto-discovered outside --path)"
            : $"{seedSources} seed source(s)";

    private static void EmitText(
        TextWriter @out, List<LocalizationScan.LocalizationFinding> findings, int filesScanned,
        int seedSources, int autoDiscovered, AuditSeverity minSeverity, string rootPath)
    {
        var corpus = SeedCorpus(seedSources, autoDiscovered);
        if (findings.Count == 0)
        {
            @out.WriteLine($"audit localization: {filesScanned} code file(s) scanned against {corpus}, " +
                           $"0 findings at or above {minSeverity.ToString().ToLowerInvariant()}.");
            return;
        }

        var errors = findings.Count(f => f.Severity == LocalizationScan.FindingSeverity.Error);
        var infos = findings.Count(f => f.Severity == LocalizationScan.FindingSeverity.Info);

        // Grouped by RULE then severity — AUD015 (completeness) before AUD019 (SQL backing), errors before
        // infos, each ordered by file/line. Keeping the rules in separate blocks matters: they have different
        // fixes (seed the key at all vs. move/extend the seed into SQL), so a mixed list would read as noise.
        foreach (var rule in new[] { LocalizationScan.Aud015, LocalizationScan.Aud019 })
        {
            foreach (var group in new[] { LocalizationScan.FindingSeverity.Error, LocalizationScan.FindingSeverity.Info })
            {
                var inGroup = findings.Where(f => f.RuleId == rule && f.Severity == group)
                    .OrderBy(f => f.File, StringComparer.Ordinal).ThenBy(f => f.Line).ToList();
                if (inGroup.Count == 0) continue;
                @out.WriteLine();
                @out.WriteLine($"[{group.ToString().ToUpperInvariant()}] {rule}: {RuleTitle(rule)}");
                foreach (var f in inGroup)
                {
                    var rel = TryRelativize(rootPath, f.File);
                    @out.WriteLine($"    {rel}:{f.Line}");
                    @out.WriteLine($"      {f.Message}");
                }
            }
        }

        @out.WriteLine();
        @out.WriteLine($"audit localization: {filesScanned} code file(s) scanned against {corpus} — " +
                       $"{errors} error(s), {infos} info(s) at or above {minSeverity.ToString().ToLowerInvariant()}.");
        if (findings.Any(f => f.RuleId == LocalizationScan.Aud015))
            @out.WriteLine("  (Suppress an AUD015 usage line with `// audit-lint:ignore AUD015` — please leave a comment explaining why.)");
        if (findings.Any(f => f.RuleId == LocalizationScan.Aud019))
            @out.WriteLine("  (AUD019 has no suppression: an in-memory mirror does not substitute for a SQL seed. Add the SQL seed.)");
    }

    // One-line description per rule, used as the block header in text output.
    private static string RuleTitle(string ruleId) => ruleId == LocalizationScan.Aud019
        ? "localization key SQL backing (an in-memory mirror is not a SQL seed)"
        : "localization key completeness";

    private static void EmitJson(
        TextWriter @out, List<LocalizationScan.LocalizationFinding> findings, int filesScanned,
        int seedSources, int autoDiscovered, AuditSeverity minSeverity)
    {
        var sb = new StringBuilder();
        sb.Append("{\"filesScanned\":").Append(filesScanned);
        sb.Append(",\"seedSources\":").Append(seedSources);
        sb.Append(",\"autoDiscoveredSeedSources\":").Append(autoDiscovered);
        sb.Append(",\"minSeverity\":\"").Append(minSeverity.ToString().ToLowerInvariant()).Append('"');
        sb.Append(",\"findings\":[");
        for (var i = 0; i < findings.Count; i++)
        {
            if (i > 0) sb.Append(',');
            var f = findings[i];
            sb.Append('{');
            sb.Append("\"ruleId\":\"").Append(JsonEscape(f.RuleId)).Append("\",");
            sb.Append("\"severity\":\"").Append(f.Severity.ToString().ToLowerInvariant()).Append("\",");
            sb.Append("\"key\":\"").Append(JsonEscape(f.Key)).Append("\",");
            sb.Append("\"file\":\"").Append(JsonEscape(f.File)).Append("\",");
            sb.Append("\"line\":").Append(f.Line).Append(',');
            sb.Append("\"message\":\"").Append(JsonEscape(f.Message)).Append('"');
            sb.Append('}');
        }
        sb.Append("]}");
        @out.WriteLine(sb.ToString());
    }

    private static string JsonEscape(string s)
    {
        var sb = new StringBuilder(s.Length + 8);
        foreach (var c in s)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                    else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    private static string TryRelativize(string root, string file)
    {
        try { return Path.GetRelativePath(root, file); }
        catch { return file; }
    }
}
