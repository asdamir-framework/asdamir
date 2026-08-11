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
/// <c>asdamir audit seeds [--path dir]… [--format text|json] [--include-tests] [--allowlist file]</c>
///
/// The SEED-FORM gates: <b>AUD018</b> (a localization seed may be written in exactly ONE approved spelling)
/// and <b>AUD017</b> (a permission grant may not be selected by a <c>LIKE</c> pattern). Both scan
/// <c>.sql</c> and <c>.sbn</c> under <c>--path</c>; the rule logic lives in <see cref="SeedFormScan"/>.
///
/// <para><b>There is NO inline suppression — deliberately.</b> The other rules take
/// <c>// audit-lint:ignore AUDxxx</c>; these two do not. An applied migration is immutable, so the only
/// legitimate exemption is a pre-existing file, and that belongs in a REVIEWED, COMMENTED allowlist
/// (<c>packaging/seed-form-allowlist.txt</c>) where every entry states why it is exempt and when it goes —
/// not in a marker anyone can paste into a new file to make a red gate green. A NEW seed has no way to opt
/// out: it is written in the canonical form or the build fails.</para>
///
/// Exit codes (this is a GATE — findings fail the build):
///   0  no findings outside the allowlist
///   1  at least one AUD017/AUD018 finding
///   2  invalid arguments
/// </summary>
public static class SeedFormCheckCommand
{
    /// <summary>Repo-relative default location of the grandfather allowlist.</summary>
    public const string DefaultAllowlistPath = "packaging/seed-form-allowlist.txt";

    private static readonly HashSet<string> SkippedDirNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "node_modules", ".git", "artifacts", "publish", "TestResults",
    };

    private static readonly HashSet<string> TestDirNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "tests", "test",
    };

    /// <summary>Builds the <c>audit seeds</c> subcommand.</summary>
    public static Command Build()
    {
        var pathOpt = new Option<List<DirectoryInfo>>(
            new[] { "--path", "-p" },
            description: "Directory to scan recursively. Repeatable — pass once per tree (e.g. src and db). " +
                         "Defaults to the current working directory.",
            getDefaultValue: () => new List<DirectoryInfo> { new(Directory.GetCurrentDirectory()) })
        {
            AllowMultipleArgumentsPerToken = true,
        };

        var formatOpt = new Option<string>(
            new[] { "--format", "-f" },
            description: "Output format. One of: text (default), json.",
            getDefaultValue: () => "text");

        var includeTestsOpt = new Option<bool>(
            new[] { "--include-tests" },
            description: "Also scan `tests/` and `test/` directories. Off by default.",
            getDefaultValue: () => false);

        var allowlistOpt = new Option<FileInfo?>(
            new[] { "--allowlist" },
            description: "Grandfather allowlist file. Defaults to `" + DefaultAllowlistPath + "` at the repo root.",
            getDefaultValue: () => null);

        var cmd = new Command("seeds", "Enforce the canonical SQL seed form (AUD018) and ban pattern-based permission grants (AUD017).")
        {
            pathOpt, formatOpt, includeTestsOpt, allowlistOpt,
        };

        // ctx.ExitCode, never Environment.Exit — see ExitCodes and the note in AuditLintCommand.
        cmd.SetHandler(
            ctx => ctx.ExitCode = Execute(
                ctx.ParseResult.GetValueForOption(pathOpt)!,
                ctx.ParseResult.GetValueForOption(formatOpt)!,
                ctx.ParseResult.GetValueForOption(includeTestsOpt),
                ctx.ParseResult.GetValueForOption(allowlistOpt)));
        return cmd;
    }

    /// <summary>
    /// Runs the gate and RETURNS the exit code (0 clean / 1 findings / 64 usage) instead of terminating, so
    /// it is unit-testable.
    /// </summary>
    /// <param name="paths">Trees whose <c>.sql</c>/<c>.sbn</c> files are scanned.</param>
    /// <param name="formatRaw">Output format (text|json).</param>
    /// <param name="includeTests">When true, also scans <c>tests/</c>/<c>test/</c> directories.</param>
    /// <param name="allowlistFile">Explicit allowlist path; when null it is resolved from the repo root.</param>
    /// <param name="outWriter">Where normal output is written; defaults to <see cref="Console.Out"/>.</param>
    /// <param name="errWriter">Where argument errors are written; defaults to <see cref="Console.Error"/>.</param>
    /// <returns>0 no findings, 1 findings, 2 invalid arguments.</returns>
    internal static int Execute(
        List<DirectoryInfo> paths, string formatRaw, bool includeTests, FileInfo? allowlistFile,
        TextWriter? outWriter = null, TextWriter? errWriter = null)
    {
        var @out = outWriter ?? Console.Out;
        var err = errWriter ?? Console.Error;

        var roots = (paths is { Count: > 0 } ? paths : new List<DirectoryInfo> { new(Directory.GetCurrentDirectory()) })
            .Select(p => p.FullName).Distinct(StringComparer.Ordinal).ToList();

        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                err.WriteLine($"Path '{root}' does not exist.");
                return ExitCodes.Usage;
            }
        }

        var format = formatRaw.ToLowerInvariant();
        if (format != "text" && format != "json")
        {
            err.WriteLine($"Invalid --format '{formatRaw}'. Use: text, json.");
            return ExitCodes.Usage;
        }

        var repoRoot = FindRepoRoot(roots[0]) ?? roots[0];
        var allowlistPath = allowlistFile?.FullName ?? Path.Combine(repoRoot, DefaultAllowlistPath);
        var allowlist = SeedFormAllowlist.Load(allowlistPath);

        var findings = new List<SeedFormScan.SeedFormFinding>();
        var suppressed = 0;
        var usedEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var filesScanned = 0;

        foreach (var root in roots)
        {
            foreach (var file in EnumerateFiles(root, includeTests, "*.sql", "*.sbn"))
            {
                var text = TryRead(file);
                if (text is null) continue;
                filesScanned++;

                var relative = Relative(repoRoot, file);
                foreach (var f in SeedFormScan.CheckLocalizationSeedForm(file, text)
                             .Concat(SeedFormScan.CheckPatternGrants(file, text)))
                {
                    if (allowlist.IsExempt(f.RuleId, relative))
                    {
                        suppressed++;
                        usedEntries.Add(SeedFormAllowlist.EntryKey(f.RuleId, relative));
                        continue;
                    }
                    findings.Add(f);
                }
            }

            // AUD017 also reads C#. The rule bans a permission set selected by a PATTERN, and a wildcard
            // written as `catalogue.Where(p => p.EndsWith(".read"))` is the same authorization decision as
            // the SQL form — it was simply invisible to a gate that only read .sql. It stayed invisible long
            // enough to sweep a ledger permission into a role the database never granted it.
            foreach (var file in EnumerateFiles(root, includeTests, "*.cs"))
            {
                var text = TryRead(file);
                if (text is null) continue;
                filesScanned++;

                var relative = Relative(repoRoot, file);
                foreach (var f in SeedFormScan.CheckCSharpPatternGrants(file, text))
                {
                    if (allowlist.IsExempt(f.RuleId, relative))
                    {
                        suppressed++;
                        usedEntries.Add(SeedFormAllowlist.EntryKey(f.RuleId, relative));
                        continue;
                    }
                    findings.Add(f);
                }
            }
        }

        var staleEntries = allowlist.Entries
            .Where(e => !usedEntries.Contains(SeedFormAllowlist.EntryKey(e.RuleId, e.Path)))
            .ToList();

        if (format == "json") EmitJson(@out, findings, filesScanned, suppressed, staleEntries);
        else EmitText(@out, findings, filesScanned, suppressed, staleEntries, repoRoot, allowlist.Loaded, allowlistPath);

        return findings.Count == 0 ? 0 : 1;
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
                // packaging/output is a generated publish staging tree — a byte copy of files already scanned
                // at their real location, so scanning it would double every finding.
                if (sub.Replace('\\', '/').EndsWith("/packaging/output", StringComparison.OrdinalIgnoreCase)) continue;
                // Never descend into a NESTED repository or git worktree (`.claude/worktrees/*`, a vendored
                // clone): it is a second copy of the same tree, so every finding — and every allowlist
                // decision — would be counted twice under a path that does not exist for anyone else.
                if (Directory.Exists(Path.Combine(sub, ".git")) || File.Exists(Path.Combine(sub, ".git"))) continue;
                stack.Push(sub);
            }
            foreach (var pattern in globs)
                foreach (var file in Directory.EnumerateFiles(dir, pattern))
                    yield return file;
        }
    }

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

    private static string Relative(string root, string file)
    {
        try { return Path.GetRelativePath(root, file).Replace('\\', '/'); }
        catch { return file.Replace('\\', '/'); }
    }

    private static string? TryRead(string path)
    {
        try { return File.ReadAllText(path); }
        catch { return null; }
    }

    private static void EmitText(
        TextWriter @out, List<SeedFormScan.SeedFormFinding> findings, int filesScanned, int suppressed,
        List<SeedFormAllowlist.Entry> stale, string repoRoot, bool allowlistLoaded, string allowlistPath)
    {
        foreach (var rule in new[] { SeedFormScan.Aud017, SeedFormScan.Aud018 })
        {
            var inRule = findings.Where(f => f.RuleId == rule)
                .OrderBy(f => f.File, StringComparer.Ordinal).ThenBy(f => f.Line).ToList();
            if (inRule.Count == 0) continue;

            @out.WriteLine();
            @out.WriteLine(rule == SeedFormScan.Aud017
                ? "[ERROR] AUD017: pattern-based permission grant"
                : "[ERROR] AUD018: non-canonical localization seed form");
            foreach (var f in inRule)
            {
                @out.WriteLine($"    {Relative(repoRoot, f.File)}:{f.Line}");
                @out.WriteLine($"      {f.Message}");
            }
        }

        if (!allowlistLoaded)
            @out.WriteLine($"audit seeds: no allowlist at '{allowlistPath}' — every finding is reported.");

        foreach (var e in stale)
            @out.WriteLine($"audit seeds: NOTE — allowlist entry no longer matches anything: {e.RuleId} {e.Path}");

        @out.WriteLine();
        @out.WriteLine($"audit seeds: {filesScanned} sql/sbn/cs file(s) scanned, {findings.Count} finding(s), " +
                       $"{suppressed} grandfathered.");
        if (findings.Count > 0)
            @out.WriteLine("  (There is no inline suppression for AUD017/AUD018 — write the seed in the " +
                           "canonical form, or add a REVIEWED, COMMENTED entry to " + DefaultAllowlistPath + ".)");
    }

    private static void EmitJson(
        TextWriter @out, List<SeedFormScan.SeedFormFinding> findings, int filesScanned, int suppressed,
        List<SeedFormAllowlist.Entry> stale)
    {
        var sb = new StringBuilder();
        sb.Append("{\"filesScanned\":").Append(filesScanned);
        sb.Append(",\"grandfathered\":").Append(suppressed);
        sb.Append(",\"staleAllowlistEntries\":").Append(stale.Count);
        sb.Append(",\"findings\":[");
        for (var i = 0; i < findings.Count; i++)
        {
            if (i > 0) sb.Append(',');
            var f = findings[i];
            sb.Append('{');
            sb.Append("\"ruleId\":\"").Append(f.RuleId).Append("\",");
            sb.Append("\"severity\":\"error\",");
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
}

/// <summary>
/// The grandfather allowlist behind <c>audit seeds</c> — the same idiom as
/// <c>packaging/removed-public-types.txt</c>: a plain text file, one entry per line, <c>#</c> starts a
/// comment, and every entry is expected to carry a comment saying WHY it is exempt and WHEN it goes.
///
/// <para>Entry format: <c>&lt;RULE&gt;&#160;&lt;repo-relative path&gt;</c>, e.g.
/// <c>AUD017  AppManagement/db/migrations/AsdamirVault_003__admin_pool_extras.sql</c>. An entry exempts that
/// ONE file from that ONE rule.</para>
///
/// <para><b>Why a file-level exemption is safe here.</b> Nearly every entry is an APPLIED migration, and an
/// applied migration is immutable — so a grandfathered file can never grow a new violation. The list can only
/// shrink (a later migration supersedes an old seed), never widen.</para>
/// </summary>
public sealed class SeedFormAllowlist
{
    /// <summary>One allowlist entry: a rule id plus the repo-relative file it exempts.</summary>
    /// <param name="RuleId">The rule the file is exempt from (e.g. <c>AUD018</c>).</param>
    /// <param name="Path">Repo-relative path, forward slashes.</param>
    public sealed record Entry(string RuleId, string Path);

    private readonly HashSet<string> _keys;

    private SeedFormAllowlist(IReadOnlyList<Entry> entries, bool loaded)
    {
        Entries = entries;
        Loaded = loaded;
        _keys = new HashSet<string>(entries.Select(e => EntryKey(e.RuleId, e.Path)), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Every parsed entry, in file order.</summary>
    public IReadOnlyList<Entry> Entries { get; }

    /// <summary>True when the allowlist file existed and was read (false = no file, empty allowlist).</summary>
    public bool Loaded { get; }

    /// <summary>Builds the lookup key for a (rule, path) pair — case-insensitive, forward slashes.</summary>
    /// <param name="ruleId">Rule id.</param>
    /// <param name="path">Repo-relative path.</param>
    /// <returns>The canonical key.</returns>
    public static string EntryKey(string ruleId, string path)
        => ruleId + "|" + path.Replace('\\', '/').TrimStart('.', '/');

    /// <summary>Reads the allowlist at <paramref name="path"/>; a missing file yields an EMPTY allowlist
    /// (a gate that silently passes because its allowlist vanished would be worse than a loud one).</summary>
    /// <param name="path">Absolute path to the allowlist file.</param>
    /// <returns>The parsed allowlist.</returns>
    public static SeedFormAllowlist Load(string path)
    {
        if (!File.Exists(path)) return new SeedFormAllowlist(Array.Empty<Entry>(), loaded: false);

        var entries = new List<Entry>();
        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw;
            var hash = line.IndexOf('#');
            if (hash >= 0) line = line[..hash];
            line = line.Trim();
            if (line.Length == 0) continue;

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;
            entries.Add(new Entry(parts[0].ToUpperInvariant(), parts[1].Replace('\\', '/')));
        }
        return new SeedFormAllowlist(entries, loaded: true);
    }

    /// <summary>True when <paramref name="relativePath"/> is exempt from <paramref name="ruleId"/>.</summary>
    /// <param name="ruleId">Rule id of the finding.</param>
    /// <param name="relativePath">Repo-relative path of the file.</param>
    /// <returns>Whether the finding is grandfathered.</returns>
    public bool IsExempt(string ruleId, string relativePath) => _keys.Contains(EntryKey(ruleId, relativePath));
}
