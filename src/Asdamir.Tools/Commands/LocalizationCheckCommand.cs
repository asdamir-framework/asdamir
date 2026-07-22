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
/// Layer A of the localization-completeness gate (AUD015). Cross-checks every localization key USED in
/// <c>.razor</c>/<c>.cs</c> code (<c>L["Key"]</c> / localizer indexers) against the keys SEEDED in the
/// tree's SQL seeds (<c>localize_*/register_*/seed_*.sql</c>, anything under <c>db/admin-onboarding/</c>
/// or <c>db/migrations/</c>) and localization seed code (files whose path contains <c>Localization</c>).
/// A used key with no seed — or seeded in fewer than all three cultures — falls through to the raw key on
/// screen; this catches that class of bug before a commit.
///
/// Exit codes:
///   0  no findings at or above --min-severity (also: no seed sources found — nothing to cross-check)
///   1  at least one finding at or above --min-severity
///   2  invalid arguments
///
/// Suppression: <c>// audit-lint:ignore AUD015</c> on the usage line, <c>audit-lint:skip-file</c> per file.
/// Dynamic keys (<c>L[$"Prefix.{x}"]</c> / <c>L[variable]</c>) are reported as INFO — never an error,
/// because the runtime value-set can't be resolved statically.
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

        var cmd = new Command("localization", "Cross-check used localization keys against the tree's seeds (AUD015).")
        {
            pathOpt, severityOpt, formatOpt, includeTestsOpt,
        };

        cmd.SetHandler(Run, pathOpt, severityOpt, formatOpt, includeTestsOpt);
        return cmd;
    }

    private static void Run(DirectoryInfo path, string severityRaw, string formatRaw, bool includeTests)
    {
        if (!path.Exists)
        {
            Console.Error.WriteLine($"Path '{path.FullName}' does not exist.");
            Environment.Exit(2);
            return;
        }

        // AUD015 emits Info and Error only (never Warning). `warning` (the default) therefore surfaces
        // exactly the Error findings — the same threshold audit-lint uses to gate a commit.
        if (!Enum.TryParse<AuditSeverity>(severityRaw, ignoreCase: true, out var minSeverity))
        {
            Console.Error.WriteLine($"Invalid --min-severity '{severityRaw}'. Use: info, warning, error.");
            Environment.Exit(2);
            return;
        }

        var format = formatRaw.ToLowerInvariant();
        if (format != "text" && format != "json")
        {
            Console.Error.WriteLine($"Invalid --format '{formatRaw}'. Use: text, json.");
            Environment.Exit(2);
            return;
        }

        // Pass 1: collect the merged seed map (SQL seeds + in-memory localization code) across the tree.
        var seeded = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var seedSources = 0;
        var filesScanned = 0;

        foreach (var file in EnumerateFiles(path.FullName, includeTests, "*.sql", "*.sbn", "*.cs", "*.razor"))
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".sql" && LocalizationScan.IsSeedSqlFile(file))
            {
                var text = TryRead(file);
                if (text is null) continue;
                LocalizationScan.MergeSeeds(seeded, LocalizationScan.ExtractSeededKeys(text));
                seedSources++;
            }
            else if ((ext == ".sbn" || ext == ".cs") && LocalizationScan.IsLocalizationCodeFile(file))
            {
                var text = TryRead(file);
                if (text is null) continue;
                LocalizationScan.MergeSeeds(seeded, LocalizationScan.ExtractSeededKeys(text));       // SQL tuples inside .sbn
                LocalizationScan.MergeSeeds(seeded, LocalizationScan.ExtractInMemorySeededKeys(text)); // ["Key"] = "…"
                seedSources++;
            }
        }

        // Zero-seed-source guard: pointing the scan at a dir with only code (seeds live elsewhere) would
        // otherwise report every used key as unseeded — a false alarm. Say so and exit clean.
        if (seedSources == 0)
        {
            Console.WriteLine($"audit localization: no localization seed files found under '{path.FullName}' " +
                              "— nothing to cross-check; seeds may live elsewhere (point --path at the tree that holds them).");
            Environment.Exit(0);
            return;
        }

        // Pass 2: collect used keys from code and compare against the seed map.
        var findings = new List<LocalizationScan.LocalizationFinding>();
        foreach (var file in EnumerateFiles(path.FullName, includeTests, "*.cs", "*.razor"))
        {
            filesScanned++;
            var text = TryRead(file);
            if (text is null) continue;
            var used = LocalizationScan.ExtractUsedKeys(text);
            if (used.Count == 0) continue;
            findings.AddRange(LocalizationScan.Compare(file, used, seeded));
        }

        // Filter by min severity after scanning so JSON output is filtered too. Info < warning ≤ Error.
        var threshold = minSeverity == AuditSeverity.Info
            ? LocalizationScan.FindingSeverity.Info
            : LocalizationScan.FindingSeverity.Error;
        findings.RemoveAll(f => f.Severity < threshold);

        if (format == "json") EmitJson(findings, filesScanned, seedSources, minSeverity);
        else EmitText(findings, filesScanned, seedSources, minSeverity, path.FullName);

        Environment.Exit(findings.Count == 0 ? 0 : 1);
    }

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

    private static void EmitText(
        List<LocalizationScan.LocalizationFinding> findings, int filesScanned, int seedSources,
        AuditSeverity minSeverity, string rootPath)
    {
        if (findings.Count == 0)
        {
            Console.WriteLine($"audit localization: {filesScanned} code file(s) scanned against {seedSources} seed source(s), " +
                              $"0 findings at or above {minSeverity.ToString().ToLowerInvariant()}.");
            return;
        }

        var errors = findings.Count(f => f.Severity == LocalizationScan.FindingSeverity.Error);
        var infos = findings.Count(f => f.Severity == LocalizationScan.FindingSeverity.Info);

        // Errors first, then infos — each ordered by file/line.
        foreach (var group in new[] { LocalizationScan.FindingSeverity.Error, LocalizationScan.FindingSeverity.Info })
        {
            var inGroup = findings.Where(f => f.Severity == group)
                .OrderBy(f => f.File, StringComparer.Ordinal).ThenBy(f => f.Line).ToList();
            if (inGroup.Count == 0) continue;
            Console.WriteLine();
            Console.WriteLine($"[{group.ToString().ToUpperInvariant()}] AUD015: localization key completeness");
            foreach (var f in inGroup)
            {
                var rel = TryRelativize(rootPath, f.File);
                Console.WriteLine($"    {rel}:{f.Line}");
                Console.WriteLine($"      {f.Message}");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"audit localization: {filesScanned} code file(s) scanned against {seedSources} seed source(s) — " +
                          $"{errors} error(s), {infos} info(s) at or above {minSeverity.ToString().ToLowerInvariant()}.");
        Console.WriteLine("  (Suppress a usage line with `// audit-lint:ignore AUD015` — please leave a comment explaining why.)");
    }

    private static void EmitJson(
        List<LocalizationScan.LocalizationFinding> findings, int filesScanned, int seedSources, AuditSeverity minSeverity)
    {
        var sb = new StringBuilder();
        sb.Append("{\"filesScanned\":").Append(filesScanned);
        sb.Append(",\"seedSources\":").Append(seedSources);
        sb.Append(",\"minSeverity\":\"").Append(minSeverity.ToString().ToLowerInvariant()).Append('"');
        sb.Append(",\"findings\":[");
        for (var i = 0; i < findings.Count; i++)
        {
            if (i > 0) sb.Append(',');
            var f = findings[i];
            sb.Append('{');
            sb.Append("\"ruleId\":\"AUD015\",");
            sb.Append("\"severity\":\"").Append(f.Severity.ToString().ToLowerInvariant()).Append("\",");
            sb.Append("\"key\":\"").Append(JsonEscape(f.Key)).Append("\",");
            sb.Append("\"file\":\"").Append(JsonEscape(f.File)).Append("\",");
            sb.Append("\"line\":").Append(f.Line).Append(',');
            sb.Append("\"message\":\"").Append(JsonEscape(f.Message)).Append('"');
            sb.Append('}');
        }
        sb.Append("]}");
        Console.WriteLine(sb.ToString());
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
