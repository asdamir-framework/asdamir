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
/// <c>asdamir audit permissions [--path dir]… [--format text|json] [--include-tests]</c>
///
/// The permission/policy-completeness gate (AUD016). Cross-checks every <c>perm</c> value a Gateway's
/// authorization policies REQUIRE (<c>RequireClaim("perm", "X")</c> / <c>HasClaim("perm", "X")</c>, also
/// inside <c>RequireAssertion</c>) across the tree's <c>.cs</c> files against the codes the tree's SQL
/// seeds SUPPLY — the <c>Name</c>s in <c>dbo.Permissions</c> plus the role codes in <c>dbo.Roles</c> /
/// <c>dbo.UserAppRoles</c>. A policy requiring a <c>perm</c> that is neither a seeded permission nor a role
/// code can never be satisfied → every request 403s, silently (claim-injecting tests miss it). This gate
/// catches that class at build/preflight time.
///
/// Exit codes (this is a GATE — findings fail the build; NOT the AUD015 exit-0-on-error bug):
///   0  no findings (also: no policies require a perm — nothing to cross-check)
///   1  at least one AUD016 finding
///   2  invalid arguments
///
/// Suppression: <c>// audit-lint:ignore AUD016</c> on the policy line, <c>audit-lint:skip-file</c> per file.
///
/// <para>Multiple <c>--path</c> may be given (repeatable) — typical use points one at the app's
/// <c>src</c> (the policies) and one at its <c>db</c> (the seeds), so both live under the scan.</para>
/// </summary>
public static class PermissionPolicyCheckCommand
{
    private static readonly HashSet<string> SkippedDirNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj", "node_modules", ".git", "artifacts", "publish", "TestResults",
    };

    private static readonly HashSet<string> TestDirNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "tests", "test",
    };

    /// <summary>Builds the <c>audit permissions</c> subcommand.</summary>
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

        var cmd = new Command("permissions", "Cross-check Gateway policy `perm` requirements against the tree's seeds (AUD016).")
        {
            pathOpt, formatOpt, includeTestsOpt,
        };

        cmd.SetHandler(Run, pathOpt, formatOpt, includeTestsOpt);
        return cmd;
    }

    private static void Run(List<DirectoryInfo> paths, string formatRaw, bool includeTests)
    {
        var roots = (paths is { Count: > 0 } ? paths : new List<DirectoryInfo> { new(Directory.GetCurrentDirectory()) })
            .Select(p => p.FullName).Distinct(StringComparer.Ordinal).ToList();

        foreach (var root in roots)
        {
            if (!Directory.Exists(root))
            {
                Console.Error.WriteLine($"Path '{root}' does not exist.");
                Environment.Exit(2);
                return;
            }
        }

        var format = formatRaw.ToLowerInvariant();
        if (format != "text" && format != "json")
        {
            Console.Error.WriteLine($"Invalid --format '{formatRaw}'. Use: text, json.");
            Environment.Exit(2);
            return;
        }

        // Pass 1: collect the union of supplied permission codes + role codes across every SQL seed in all roots.
        var supplied = new HashSet<string>(StringComparer.Ordinal);
        var sqlFilesScanned = 0;
        foreach (var root in roots)
        {
            foreach (var file in EnumerateFiles(root, includeTests, "*.sql"))
            {
                if (!PermissionPolicyScan.IsSeedSqlFile(file)) continue;
                var text = TryRead(file);
                if (text is null) continue;
                sqlFilesScanned++;
                supplied.UnionWith(PermissionPolicyScan.ExtractSuppliedPermissionCodes(text));
                supplied.UnionWith(PermissionPolicyScan.ExtractSuppliedRoleCodes(text));
            }
        }

        // Pass 2: collect required perms from C# and compare against the supplied union.
        var findings = new List<PermissionPolicyScan.PermissionFinding>();
        var csFilesScanned = 0;
        var requiredCount = 0;
        foreach (var root in roots)
        {
            foreach (var file in EnumerateFiles(root, includeTests, "*.cs"))
            {
                var text = TryRead(file);
                if (text is null) continue;
                csFilesScanned++;
                var required = PermissionPolicyScan.ExtractRequiredPerms(text);
                if (required.Count == 0) continue;
                requiredCount += required.Count;
                findings.AddRange(PermissionPolicyScan.Compare(file, required, supplied));
            }
        }

        var rootForRel = roots[0];
        if (format == "json") EmitJson(findings, csFilesScanned, sqlFilesScanned, requiredCount);
        else EmitText(findings, csFilesScanned, sqlFilesScanned, requiredCount, rootForRel);

        // GATE: any AUD016 finding fails. (Every finding is an Error — this gate has no Info/Warning tier.)
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
        List<PermissionPolicyScan.PermissionFinding> findings, int csFiles, int sqlFiles, int requiredCount, string rootPath)
    {
        if (findings.Count == 0)
        {
            Console.WriteLine($"audit permissions: {csFiles} cs file(s), {sqlFiles} sql file(s) scanned, " +
                              $"{requiredCount} required perms, 0 findings.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("[ERROR] AUD016: permission/policy completeness");
        foreach (var f in findings.OrderBy(f => f.File, StringComparer.Ordinal).ThenBy(f => f.Line))
        {
            var rel = TryRelativize(rootPath, f.File);
            Console.WriteLine($"    {rel}:{f.Line}");
            Console.WriteLine($"      {f.Message}");
        }

        Console.WriteLine();
        Console.WriteLine($"audit permissions: {csFiles} cs file(s), {sqlFiles} sql file(s) scanned, " +
                          $"{requiredCount} required perms, {findings.Count} finding(s).");
        Console.WriteLine("  (Suppress a policy line with `// audit-lint:ignore AUD016` — please leave a comment explaining why.)");
    }

    private static void EmitJson(
        List<PermissionPolicyScan.PermissionFinding> findings, int csFiles, int sqlFiles, int requiredCount)
    {
        var sb = new StringBuilder();
        sb.Append("{\"csFilesScanned\":").Append(csFiles);
        sb.Append(",\"sqlFilesScanned\":").Append(sqlFiles);
        sb.Append(",\"requiredPerms\":").Append(requiredCount);
        sb.Append(",\"findings\":[");
        for (var i = 0; i < findings.Count; i++)
        {
            if (i > 0) sb.Append(',');
            var f = findings[i];
            sb.Append('{');
            sb.Append("\"ruleId\":\"AUD016\",");
            sb.Append("\"severity\":\"error\",");
            sb.Append("\"perm\":\"").Append(JsonEscape(f.Perm)).Append("\",");
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
