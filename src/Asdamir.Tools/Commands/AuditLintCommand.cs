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
using System.Text.RegularExpressions;

namespace Asdamir.Tools.Commands;

/// <summary>
/// <c>asdamir audit lint [--path dir] [--min-severity info|warning|error] [--format text|json]</c>
///
/// Scans .cs files for the Asdamir audit pattern set (see <see cref="AuditRuleSet"/>).
/// Each rule corresponds to a real finding from the v1 → v2 audit; running this
/// before a commit catches the regressions before review.
///
/// Exit codes:
///   0  no findings at or above --min-severity
///   1  at least one finding at or above --min-severity
///   2  invalid arguments
///
/// Skipped directories: <c>bin</c>, <c>obj</c>, <c>node_modules</c>, <c>.git</c>, <c>artifacts</c>.
/// Files annotated <c>// &lt;auto-generated&gt;</c> on their first line are skipped — we
/// don't want the lint to fail on its own scaffolds, and re-running the generator
/// is the correct fix for those anyway.
///
/// Scanning detail: single-line <c>//</c> comments are stripped before regex matching,
/// so docstrings that reference an anti-pattern by name ("Audit fix: v1 allocated
/// <c>new HttpClient()</c>") don't trigger a false positive. String literals are
/// preserved — if you really wrote the anti-pattern as a hard-coded string, that's
/// worth seeing.
///
/// To suppress a specific line: append <c>// audit-lint:ignore AUD003</c> (case-sensitive).
/// To skip an entire file (e.g. this rule set, which contains the anti-pattern names
/// in its <c>Title:</c> string literals): put <c>// audit-lint:skip-file</c> in the
/// first 10 lines of the file.
/// </summary>
public static class AuditLintCommand
{
    private static readonly HashSet<string> SkippedDirNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "node_modules", ".git", "artifacts", "publish", "TestResults",
    };

    // Test directories are skipped by default — test infrastructure legitimately uses
    // patterns the rules flag (handler-injected `new HttpClient(handler)`, isolated
    // `BuildServiceProvider()` per test, unbounded `new MemoryCache(...)` stubs). Use
    // `--include-tests` if you want to run the rules across tests too.
    private static readonly HashSet<string> TestDirNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "tests", "test",
    };

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
            description: "Also scan `tests/` and `test/` directories. Off by default — test infra uses patterns the rules flag.",
            getDefaultValue: () => false);

        var lintCmd = new Command("lint", "Scan .cs files for Asdamir audit anti-patterns and report findings.")
        {
            pathOpt, severityOpt, formatOpt, includeTestsOpt,
        };

        lintCmd.SetHandler(Run, pathOpt, severityOpt, formatOpt, includeTestsOpt);
        return lintCmd;
    }

    private static void Run(DirectoryInfo path, string severityRaw, string formatRaw, bool includeTests)
    {
        if (!path.Exists)
        {
            Console.Error.WriteLine($"Path '{path.FullName}' does not exist.");
            Environment.Exit(2);
            return;
        }

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

        var rules = AuditRuleSet.All;
        var findings = new List<Finding>();
        var filesScanned = 0;

        foreach (var file in EnumerateFiles(path.FullName, includeTests))
        {
            filesScanned++;
            ScanFile(file, rules, findings);
        }

        // Filter by min severity threshold *after* scanning so JSON output is also filtered.
        findings.RemoveAll(f => f.Severity < minSeverity);

        if (format == "json") EmitJson(findings, filesScanned, minSeverity);
        else EmitText(findings, filesScanned, minSeverity, path.FullName);

        // Exit code 1 only if findings at or above the threshold remain.
        Environment.Exit(findings.Count == 0 ? 0 : 1);
    }

    private static IEnumerable<string> EnumerateFiles(string root, bool includeTests)
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
            foreach (var file in Directory.EnumerateFiles(dir, "*.cs"))
                yield return file;
        }
    }

    private static void ScanFile(string path, IReadOnlyList<AuditRule> rules, List<Finding> sink)
    {
        string[] lines;
        try { lines = File.ReadAllLines(path); }
        catch { return; }

        // Generated scaffolds skip the lint — re-running the generator is the fix.
        if (lines.Length > 0 && lines[0].TrimStart().StartsWith("// <auto-generated>", StringComparison.Ordinal))
            return;

        // File-level opt-out: useful for meta-files like the rule set itself, whose
        // Title strings contain the anti-pattern names by design. Scan the WHOLE file — a
        // prepended license header must not push the marker out of view. Match the DIRECTIVE
        // form only (a comment line that, once leading slashes/space are stripped, *starts*
        // with the marker) so a mere mention inside a string/Title doesn't exempt a file.
        foreach (var l in lines)
        {
            if (l.TrimStart(' ', '\t', '/').StartsWith("audit-lint:skip-file", StringComparison.Ordinal))
                return;
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            // Strip a `// audit-lint:ignore AUDxxx` suppression marker — supports
            // suppressing one or more rules on the same line.
            var suppressed = ExtractSuppressedRules(line);

            // Match against the line with single-line `//` comments removed (but string
            // literals preserved). Findings still report the original column / line text
            // so the reviewer can read the surrounding context.
            var matchLine = StripLineComments(line);

            foreach (var rule in rules)
            {
                if (suppressed.Contains(rule.Id)) continue;
                var match = rule.Pattern.Match(matchLine);
                if (!match.Success) continue;
                sink.Add(new Finding(path, i + 1, match.Index + 1, rule, line.TrimEnd()));
            }
        }
    }

    /// <summary>
    /// Returns the line with the trailing <c>//</c> comment removed, while preserving
    /// any <c>"..."</c> or <c>@"..."</c> string literal content. This keeps the lint
    /// from firing on documentation prose that names an anti-pattern by example.
    /// Multi-line comments (<c>/* */</c>) aren't handled — single-line scanning,
    /// and the audit rules don't currently target patterns hidden in block comments.
    /// </summary>
    private static string StripLineComments(string line)
    {
        var inString = false;
        var isVerbatim = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (!inString)
            {
                // Detect start of a string literal.
                if (c == '@' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    inString = true;
                    isVerbatim = true;
                    i++; // consume the quote on the next iteration via loop step
                    continue;
                }
                if (c == '"')
                {
                    inString = true;
                    isVerbatim = false;
                    continue;
                }
                // Comment found outside any string — strip from here on.
                if (c == '/' && i + 1 < line.Length && line[i + 1] == '/')
                    return line[..i];
            }
            else
            {
                // End-of-string handling. Verbatim string: `""` is a literal quote, a lone
                // `"` ends the string. Regular string: `\"` is an escaped quote.
                if (isVerbatim)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"') { i++; continue; }
                        inString = false;
                    }
                }
                else
                {
                    if (c == '\\' && i + 1 < line.Length) { i++; continue; }
                    if (c == '"') inString = false;
                }
            }
        }
        return line;
    }

    private static HashSet<string> ExtractSuppressedRules(string line)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        const string marker = "audit-lint:ignore";
        var idx = line.IndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return result;
        var tail = line[(idx + marker.Length)..];
        foreach (Match m in Regex.Matches(tail, @"AUD\d{3}"))
            result.Add(m.Value);
        return result;
    }

    private static void EmitText(List<Finding> findings, int filesScanned, AuditSeverity minSeverity, string rootPath)
    {
        if (findings.Count == 0)
        {
            Console.WriteLine($"audit lint: {filesScanned} files scanned, 0 findings at or above {minSeverity.ToString().ToLowerInvariant()}.");
            return;
        }

        // Group by rule for a readable, action-oriented report.
        var byRule = findings
            .GroupBy(f => f.Rule.Id)
            .OrderByDescending(g => g.First().Rule.Severity)
            .ThenBy(g => g.Key);

        foreach (var group in byRule)
        {
            var rule = group.First().Rule;
            Console.WriteLine();
            Console.WriteLine($"[{rule.Severity.ToString().ToUpperInvariant()}] {rule.Id}: {rule.Title}");
            Console.WriteLine($"  Why: {rule.Rationale}");
            Console.WriteLine($"  Fix: {rule.Fix}");
            foreach (var finding in group.OrderBy(f => f.FilePath).ThenBy(f => f.Line))
            {
                var rel = TryRelativize(rootPath, finding.FilePath);
                Console.WriteLine($"    {rel}:{finding.Line}:{finding.Column}");
                Console.WriteLine($"      {finding.LineText.Trim()}");
            }
        }

        var errors = findings.Count(f => f.Severity == AuditSeverity.Error);
        var warnings = findings.Count(f => f.Severity == AuditSeverity.Warning);
        var infos = findings.Count(f => f.Severity == AuditSeverity.Info);
        Console.WriteLine();
        Console.WriteLine($"audit lint: {filesScanned} files scanned — {errors} error(s), {warnings} warning(s), {infos} info(s) at or above {minSeverity.ToString().ToLowerInvariant()}.");
        Console.WriteLine("  (Suppress a line with `// audit-lint:ignore AUDxxx` — please leave a comment explaining why.)");
    }

    private static void EmitJson(List<Finding> findings, int filesScanned, AuditSeverity minSeverity)
    {
        // Hand-rolled JSON to keep the dependency footprint at zero. Schema is stable
        // enough for CI consumers to parse with `jq` or System.Text.Json.
        var sb = new System.Text.StringBuilder();
        sb.Append("{\"filesScanned\":").Append(filesScanned);
        sb.Append(",\"minSeverity\":\"").Append(minSeverity.ToString().ToLowerInvariant()).Append("\"");
        sb.Append(",\"findings\":[");
        for (var i = 0; i < findings.Count; i++)
        {
            if (i > 0) sb.Append(',');
            var f = findings[i];
            sb.Append('{');
            sb.Append("\"ruleId\":\"").Append(f.Rule.Id).Append("\",");
            sb.Append("\"severity\":\"").Append(f.Rule.Severity.ToString().ToLowerInvariant()).Append("\",");
            sb.Append("\"title\":\"").Append(JsonEscape(f.Rule.Title)).Append("\",");
            sb.Append("\"file\":\"").Append(JsonEscape(f.FilePath)).Append("\",");
            sb.Append("\"line\":").Append(f.Line).Append(',');
            sb.Append("\"column\":").Append(f.Column).Append(',');
            sb.Append("\"text\":\"").Append(JsonEscape(f.LineText.Trim())).Append("\"");
            sb.Append('}');
        }
        sb.Append("]}");
        Console.WriteLine(sb.ToString());
    }

    private static string JsonEscape(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length + 8);
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

    private sealed record Finding(string FilePath, int Line, int Column, AuditRule Rule, string LineText)
    {
        public AuditSeverity Severity => Rule.Severity;
    }
}
