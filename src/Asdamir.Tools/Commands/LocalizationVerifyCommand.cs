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
using Microsoft.Data.SqlClient;

namespace Asdamir.Tools.Commands;

/// <summary>
/// <c>asdamir localization verify --path dir (--connection … | --server … --database …) (--app-code … | --app-id …)</c>
///
/// Layer C of the localization-completeness gate: live apply-drift. Collects the (key,culture) pairs the
/// tree's SQL seeds DEFINE (same parser as <c>audit localization</c>), then queries the live
/// <c>dbo.LocalizationResource</c> for the resolved AppId and diffs the two. A seed pair that is NOT
/// present live is <b>unapplied</b> drift — exactly the class of bug where a key is committed to a seed
/// file but never applied to the running vault, so the raw key renders even though the repo looks correct.
///
/// Exit codes:
///   0  every seeded (key,culture) is present live (no unapplied drift)
///   1  at least one seeded (key,culture) is missing live
///   2  invalid arguments (no connection, no app-code/app-id, bad path)
/// </summary>
public static class LocalizationVerifyCommand
{
    /// <summary>Builds the <c>localization verify</c> subcommand.</summary>
    public static Command Build()
    {
        var pathOpt = new Option<DirectoryInfo>(
            new[] { "--path", "-p" },
            description: "Directory whose SQL seeds define the expected (key,culture) pairs. Defaults to the current working directory.",
            getDefaultValue: () => new DirectoryInfo(Directory.GetCurrentDirectory()));

        // Connection options — copied from DbApplyCommand so the flags match `db apply`.
        var connOpt = new Option<string>(
            new[] { "--connection", "-c" },
            description: "Full ADO.NET connection string to the vault. Takes precedence over --server/--database/--user/--password.",
            getDefaultValue: () => "");

        var serverOpt = new Option<string>(
            new[] { "--server", "-S" },
            description: "SQL Server instance (used when --connection is omitted).",
            getDefaultValue: () => "localhost");

        var databaseOpt = new Option<string>(
            new[] { "--database", "-d" },
            description: "Vault database name (e.g. AsdamirVault). Required when --connection is omitted.",
            getDefaultValue: () => "");

        var userOpt = new Option<string>(
            new[] { "--user", "-U" },
            description: "SQL Server login (SQL auth). When omitted, Windows integrated auth is used.",
            getDefaultValue: () => "");

        var passwordOpt = new Option<string>(
            new[] { "--password", "-P" },
            description: "Password for --user. Prefer a full --connection from a secret instead.",
            getDefaultValue: () => "");

        var appCodeOpt = new Option<string>(
            "--app-code",
            description: "The app's Code in dbo.Apps (e.g. Recon) — resolves its AppId. Provide this OR --app-id.",
            getDefaultValue: () => "");

        var appIdOpt = new Option<string>(
            "--app-id",
            description: "The app's AppId (GUID) directly. Provide this OR --app-code.",
            getDefaultValue: () => "");

        var formatOpt = new Option<string>(
            new[] { "--format", "-f" },
            description: "Output format. One of: text (default), json.",
            getDefaultValue: () => "text");

        var cmd = new Command("verify", "Diff the tree's seeded localization keys against the live vault (apply-drift).")
        {
            pathOpt, connOpt, serverOpt, databaseOpt, userOpt, passwordOpt, appCodeOpt, appIdOpt, formatOpt,
        };

        cmd.SetHandler(async ctx =>
        {
            ctx.ExitCode = await RunAsync(
                ctx.ParseResult.GetValueForOption(pathOpt)!,
                ctx.ParseResult.GetValueForOption(connOpt) ?? "",
                ctx.ParseResult.GetValueForOption(serverOpt) ?? "localhost",
                ctx.ParseResult.GetValueForOption(databaseOpt) ?? "",
                ctx.ParseResult.GetValueForOption(userOpt) ?? "",
                ctx.ParseResult.GetValueForOption(passwordOpt) ?? "",
                ctx.ParseResult.GetValueForOption(appCodeOpt) ?? "",
                ctx.ParseResult.GetValueForOption(appIdOpt) ?? "",
                ctx.ParseResult.GetValueForOption(formatOpt) ?? "text");
        });
        return cmd;
    }

    private static async Task<int> RunAsync(
        DirectoryInfo path, string connection, string server, string database, string user, string password,
        string appCode, string appId, string format)
    {
        if (!path.Exists)
        {
            Console.Error.WriteLine($"Path '{path.FullName}' does not exist.");
            return 2;
        }

        format = format.ToLowerInvariant();
        if (format != "text" && format != "json")
        {
            Console.Error.WriteLine($"Invalid --format '{format}'. Use: text, json.");
            return 2;
        }

        // A live connection is REQUIRED — never silently skip (same policy as `db apply`).
        var connString = BuildConnectionString(connection, server, database, user, password, out var connError);
        if (connString is null)
        {
            Console.Error.WriteLine(connError);
            return 2;
        }

        if (string.IsNullOrWhiteSpace(appCode) && string.IsNullOrWhiteSpace(appId))
        {
            Console.Error.WriteLine("Provide --app-code <code> (to resolve the AppId) or --app-id <guid> directly.");
            return 2;
        }

        // Collect the seeded (key,culture) pairs from the tree's SQL seed files (shared parser).
        var seeded = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var seedSources = 0;
        foreach (var file in EnumerateSql(path.FullName))
        {
            if (!LocalizationScan.IsSeedSqlFile(file)) continue;
            string text;
            try { text = await File.ReadAllTextAsync(file); } catch { continue; }
            LocalizationScan.MergeSeeds(seeded, LocalizationScan.ExtractSeededKeys(text));
            seedSources++;
        }

        if (seedSources == 0)
        {
            Console.WriteLine($"localization verify: no localization seed files found under '{path.FullName}' — nothing to verify.");
            return 0;
        }

        try
        {
            // CLI setup context; IDbConnectionFactory is a runtime multi-tenant abstraction not present here.
            await using var conn = new SqlConnection(connString); // audit-lint:ignore AUD002
            await conn.OpenAsync();

            var resolvedAppId = appId;
            if (string.IsNullOrWhiteSpace(resolvedAppId))
            {
                resolvedAppId = await ResolveAppIdAsync(conn, appCode);
                if (resolvedAppId is null)
                {
                    Console.Error.WriteLine($"No app found in dbo.Apps with Code = '{appCode}'.");
                    return 2;
                }
            }

            var live = await LoadLiveAsync(conn, resolvedAppId);

            // Diff both directions.
            var unapplied = new List<(string Key, string Culture)>();
            foreach (var (key, cultures) in seeded)
                foreach (var culture in cultures)
                    if (!live.TryGetValue(key, out var liveCultures) || !liveCultures.Contains(culture))
                        unapplied.Add((key, culture));

            var reverse = new List<(string Key, string Culture)>();
            foreach (var (key, cultures) in live)
                foreach (var culture in cultures)
                    if (!seeded.TryGetValue(key, out var seedCultures) || !seedCultures.Contains(culture))
                        reverse.Add((key, culture));

            if (format == "json") EmitJson(resolvedAppId, seedSources, unapplied, reverse);
            else EmitText(resolvedAppId, seedSources, unapplied, reverse);

            return unapplied.Count == 0 ? 0 : 1;
        }
        catch (SqlException ex)
        {
            Console.Error.WriteLine($"SQL error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>Builds the ADO.NET connection string from either a full --connection or the discrete
    /// --server/--database/--user/--password flags (same logic as <c>db apply</c>). Returns null and sets
    /// <paramref name="error"/> when neither a --connection nor a --database is supplied.</summary>
    private static string? BuildConnectionString(
        string connection, string server, string database, string user, string password, out string error)
    {
        error = "";
        try
        {
            SqlConnectionStringBuilder builder;
            if (!string.IsNullOrWhiteSpace(connection))
            {
                builder = new SqlConnectionStringBuilder(connection);
                if (!string.IsNullOrWhiteSpace(database)) builder.InitialCatalog = database;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(database))
                {
                    error = "A live connection is required — provide --database (with --server) or a full --connection string.";
                    return null;
                }
                builder = new SqlConnectionStringBuilder
                {
                    DataSource = server,
                    InitialCatalog = database,
                    TrustServerCertificate = true,
                };
                if (!string.IsNullOrWhiteSpace(user))
                {
                    builder.UserID = user;
                    builder.Password = password;
                }
                else
                {
                    builder.IntegratedSecurity = true;
                }
            }

            if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
            {
                error = "The connection string has no Initial Catalog / Database. Pass --database.";
                return null;
            }
            return builder.ConnectionString;
        }
        catch (Exception ex)
        {
            error = $"Invalid connection settings: {ex.Message}";
            return null;
        }
    }

    /// <summary>Resolves an app's AppId from its Code in <c>dbo.Apps</c>. Null when no such row exists.</summary>
    private static async Task<string?> ResolveAppIdAsync(SqlConnection conn, string appCode)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT TOP 1 CONVERT(nvarchar(64), AppId) FROM dbo.Apps WHERE Code = @code;";
        cmd.Parameters.AddWithValue("@code", appCode);
        var result = await cmd.ExecuteScalarAsync();
        return result is null || result is DBNull ? null : (string)result;
    }

    /// <summary>Loads the live (key → cultures) map from <c>dbo.LocalizationResource</c> for an AppId.</summary>
    private static async Task<Dictionary<string, HashSet<string>>> LoadLiveAsync(SqlConnection conn, string appId)
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT [Key], [Culture] FROM dbo.LocalizationResource WHERE AppId = @appId;";
        cmd.Parameters.AddWithValue("@appId", appId);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var key = reader.GetString(0);
            var culture = reader.GetString(1);
            if (!map.TryGetValue(key, out var set)) map[key] = set = new HashSet<string>(StringComparer.Ordinal);
            set.Add(culture);
        }
        return map;
    }

    private static IEnumerable<string> EnumerateSql(string root)
    {
        var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "bin", "obj", "node_modules", ".git", "artifacts", "publish", "TestResults",
        };
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            foreach (var sub in Directory.EnumerateDirectories(dir))
            {
                if (skip.Contains(Path.GetFileName(sub))) continue;
                stack.Push(sub);
            }
            foreach (var file in Directory.EnumerateFiles(dir, "*.sql"))
                yield return file;
        }
    }

    private static void EmitText(
        string appId, int seedSources,
        List<(string Key, string Culture)> unapplied, List<(string Key, string Culture)> reverse)
    {
        Console.WriteLine($"localization verify: AppId {appId}, {seedSources} seed source(s).");

        if (unapplied.Count == 0)
        {
            Console.WriteLine("  OK — every seeded (key, culture) is present live. No apply-drift.");
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine($"  UNAPPLIED — {unapplied.Count} seeded (key, culture) pair(s) are missing from the live vault:");
            foreach (var (key, culture) in unapplied.OrderBy(p => p.Key, StringComparer.Ordinal).ThenBy(p => p.Culture, StringComparer.Ordinal))
                Console.WriteLine($"    {key}  [{culture}]");
            Console.WriteLine("  → apply the seed (`asdamir db apply` against the vault); the raw key renders until then.");
        }

        if (reverse.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"  INFO — {reverse.Count} live (key, culture) pair(s) with no seed-file entry (reverse drift, informational):");
            foreach (var (key, culture) in reverse.OrderBy(p => p.Key, StringComparer.Ordinal).ThenBy(p => p.Culture, StringComparer.Ordinal).Take(50))
                Console.WriteLine($"    {key}  [{culture}]");
            if (reverse.Count > 50) Console.WriteLine($"    … and {reverse.Count - 50} more.");
        }
    }

    private static void EmitJson(
        string appId, int seedSources,
        List<(string Key, string Culture)> unapplied, List<(string Key, string Culture)> reverse)
    {
        var sb = new StringBuilder();
        sb.Append("{\"appId\":\"").Append(JsonEscape(appId)).Append('"');
        sb.Append(",\"seedSources\":").Append(seedSources);
        sb.Append(",\"unapplied\":");
        AppendPairs(sb, unapplied);
        sb.Append(",\"appliedNotSeeded\":");
        AppendPairs(sb, reverse);
        sb.Append('}');
        Console.WriteLine(sb.ToString());
    }

    private static void AppendPairs(StringBuilder sb, List<(string Key, string Culture)> pairs)
    {
        sb.Append('[');
        for (var i = 0; i < pairs.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append("{\"key\":\"").Append(JsonEscape(pairs[i].Key))
              .Append("\",\"culture\":\"").Append(JsonEscape(pairs[i].Culture)).Append("\"}");
        }
        sb.Append(']');
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
