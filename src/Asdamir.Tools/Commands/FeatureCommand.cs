// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later

using System.CommandLine;
using Microsoft.Data.SqlClient;

namespace Asdamir.Tools.Commands;

/// <summary>
/// <c>asdamir new feature &lt;Name&gt; --fields "..." [--route /x] [--icon list] [--policy AdminAccess]
/// [--apply -c &lt;app-db&gt; | -S -d -U -P] [--vault-connection &lt;vault&gt;]</c>
///
/// One-shot orchestrator over <c>new entity</c> (→ the Gateway/API project) and <c>new page</c> (→ the
/// Server/UI project): generates the entity slice, the CRUD page, and the menu/permission + localization
/// seeds. With <c>--apply</c> it applies the entity migration to the app DB (reusing <c>db apply</c>'s
/// journaled runner); the AsdamirVault seeds are applied only when <c>--vault-connection</c> is given
/// (explicit, no connection guessing). Fail-fast: if the entity step fails, the page step is skipped.
/// </summary>
public static class FeatureCommand
{
    public static Command Build()
    {
        var nameArg = new Argument<string>("name", "PascalCase entity name (e.g. Invoice).");
        var fieldsOpt = new Option<string>(new[] { "--fields", "-f" },
            description: "Comma-separated 'Name:type' list — same syntax as `new entity`/`new page`.") { IsRequired = true };
        var routeOpt = new Option<string>(new[] { "--route", "-r" },
            description: "Page route. Defaults to '/<plural-lowercase>'.", getDefaultValue: () => "");
        var iconOpt = new Option<string>(new[] { "--icon", "-i" },
            description: "Nav-menu icon for the generated menu row. Defaults to 'list'.", getDefaultValue: () => "list");
        var policyOpt = new Option<string>(new[] { "--policy", "-p" },
            description: "Authorization policy applied to the page. Defaults to 'AdminAccess'.", getDefaultValue: () => "AdminAccess");
        var nsOpt = new Option<string>(new[] { "--namespace", "-n" },
            description: "Root namespace override. Defaults to each project's namespace.", getDefaultValue: () => "");
        var outputOpt = new Option<DirectoryInfo>(new[] { "--output", "-o" },
            description: "App root (the nearest ancestor with a .sln is used). Defaults to the current directory.",
            getDefaultValue: () => new DirectoryInfo(Directory.GetCurrentDirectory()));
        var gatewayDirOpt = new Option<string>("--gateway-dir",
            description: "Override: the Gateway/API project directory (the entity is generated here).", getDefaultValue: () => "");
        var serverDirOpt = new Option<string>("--server-dir",
            description: "Override: the Server/UI project directory (the page is generated here).", getDefaultValue: () => "");

        var applyOpt = new Option<bool>("--apply",
            description: "Apply the entity migration to the app DB after generating (needs a connection below).", getDefaultValue: () => false);
        var connOpt = new Option<string>(new[] { "--connection", "-c" },
            description: "App-DB connection string for --apply (entity migration). Overrides --server/--database/etc.", getDefaultValue: () => "");
        var serverOpt = new Option<string>(new[] { "--server", "-S" },
            description: "App-DB server for --apply.", getDefaultValue: () => "localhost");
        var databaseOpt = new Option<string>(new[] { "--database", "-d" },
            description: "App-DB name for --apply.", getDefaultValue: () => "");
        var userOpt = new Option<string>(new[] { "--user", "-U" },
            description: "App-DB SQL login for --apply.", getDefaultValue: () => "");
        var passwordOpt = new Option<string>(new[] { "--password", "-P" },
            description: "Password for --user.", getDefaultValue: () => "");
        var vaultConnOpt = new Option<string>("--vault-connection",
            description: "AsdamirVault connection string. When set with --apply, the menu/permission + localization seeds are applied to AsdamirVault too.",
            getDefaultValue: () => "");

        var cmd = new Command("feature", "Generate a full feature in one shot: entity (Gateway) + CRUD page (Server) + menu/permission seed.")
        {
            nameArg, fieldsOpt, routeOpt, iconOpt, policyOpt, nsOpt, outputOpt, gatewayDirOpt, serverDirOpt,
            applyOpt, connOpt, serverOpt, databaseOpt, userOpt, passwordOpt, vaultConnOpt,
        };

        cmd.SetHandler(async ctx =>
        {
            ctx.ExitCode = await RunAsync(
                ctx.ParseResult.GetValueForArgument(nameArg),
                ctx.ParseResult.GetValueForOption(fieldsOpt) ?? "",
                ctx.ParseResult.GetValueForOption(routeOpt) ?? "",
                ctx.ParseResult.GetValueForOption(iconOpt) ?? "list",
                ctx.ParseResult.GetValueForOption(policyOpt) ?? "AdminAccess",
                ctx.ParseResult.GetValueForOption(nsOpt) ?? "",
                ctx.ParseResult.GetValueForOption(outputOpt)!,
                ctx.ParseResult.GetValueForOption(gatewayDirOpt) ?? "",
                ctx.ParseResult.GetValueForOption(serverDirOpt) ?? "",
                ctx.ParseResult.GetValueForOption(applyOpt),
                ctx.ParseResult.GetValueForOption(connOpt) ?? "",
                ctx.ParseResult.GetValueForOption(serverOpt) ?? "localhost",
                ctx.ParseResult.GetValueForOption(databaseOpt) ?? "",
                ctx.ParseResult.GetValueForOption(userOpt) ?? "",
                ctx.ParseResult.GetValueForOption(passwordOpt) ?? "",
                ctx.ParseResult.GetValueForOption(vaultConnOpt) ?? "");
        });
        return cmd;
    }

    private static async Task<int> RunAsync(string name, string fields, string route, string icon, string policy, string ns,
        DirectoryInfo output, string gatewayDirOverride, string serverDirOverride,
        bool apply, string connection, string server, string database, string user, string password, string vaultConnection)
    {
        var appRoot = FindAppRoot(output);
        if (appRoot is null)
        {
            Console.Error.WriteLine("Could not find an app root (no .sln above the output dir). Run inside a generated app or pass --output.");
            return 2;
        }

        var gatewayDir = !string.IsNullOrWhiteSpace(gatewayDirOverride) ? gatewayDirOverride : FindProject(appRoot, isGateway: true);
        var serverDir = !string.IsNullOrWhiteSpace(serverDirOverride) ? serverDirOverride : FindProject(appRoot, isGateway: false);
        if (gatewayDir is null) { Console.Error.WriteLine("Gateway/API project not found under src/ (needs Controllers/ or db/migrations). Use --gateway-dir."); return 2; }
        if (serverDir is null) { Console.Error.WriteLine("Server/UI project not found under src/ (needs Components/Pages/). Use --server-dir."); return 2; }

        Console.WriteLine($"App root: {appRoot}");
        Console.WriteLine($"  entity → {Path.GetRelativePath(appRoot, gatewayDir)}   ·   page → {Path.GetRelativePath(appRoot, serverDir)}");
        Console.WriteLine();

        // 1) entity slice (Gateway) — applies its migration to the app DB when --apply.
        var entityExit = await EntityCommand.RunAsync(name, fields, new DirectoryInfo(gatewayDir), ns, apply, connection, server, database, user, password);
        if (entityExit != 0)
        {
            Console.Error.WriteLine("\n✗ entity step failed — stopping (page not generated). Fix the error and re-run (generation is idempotent).");
            return entityExit;
        }

        // 2) CRUD page (Server) — also writes the AsdamirVault seeds (not applied here).
        Console.WriteLine();
        var pageExit = PageCommand.Run(name, fields, route, new DirectoryInfo(serverDir), ns, policy, icon);
        if (pageExit != 0) { Console.Error.WriteLine("\n✗ page step failed — stopping."); return pageExit; }

        // 3) Menu/permission + localization seeds. In FREE mode PageCommand emitted them as journaled
        // migrations into the app's OWN db/migrations (auto-applied by `db apply`, no AsdamirVault) — so
        // there is no Vault step. In commercial mode they are AppId-scoped AsdamirVault scripts.
        var pluralLower = NameHelper.Pluralize(name).ToLowerInvariant();
        Console.WriteLine();
        if (PageCommand.IsFreeModeApp(appRoot))
        {
            Console.WriteLine($"  free mode: menu/permission + localization seeded as db/migrations/V*__freemode_{{menu,localize}}_{pluralLower}.sql");
            Console.WriteLine("  apply to the app's own DB with `asdamir db apply` (no --vault-connection needed).");
        }
        else
        {
            var seedDir = Path.Combine(appRoot, "db", "admin-onboarding");
            var menuSeed = Path.Combine(seedDir, $"seed_menu_{pluralLower}.sql");
            var locSeed = Path.Combine(seedDir, $"localize_{pluralLower}.sql");
            if (apply && !string.IsNullOrWhiteSpace(vaultConnection))
            {
                var vaultExit = await ApplyVaultSeedsAsync(vaultConnection, new[] { menuSeed, locSeed });
                if (vaultExit != 0) return vaultExit;
            }
            else if (apply)
            {
                Console.WriteLine("  menu/permission + localization seed generated but NOT applied (no --vault-connection).");
                Console.WriteLine($"  apply to AsdamirVault: re-run with --vault-connection \"<AsdamirVault connstr>\", or run {Path.GetRelativePath(appRoot, menuSeed)} via sqlcmd.");
            }
        }

        Console.WriteLine();
        Console.WriteLine($"✓ Feature '{name}' ready.");
        return 0;
    }

    /// <summary>Applies idempotent AsdamirVault seed scripts (NOT journaled — they guard themselves).</summary>
    private static async Task<int> ApplyVaultSeedsAsync(string vaultConnection, IEnumerable<string> files)
    {
        try
        {
            // CLI one-shot setup; IDbConnectionFactory is a runtime multi-tenant abstraction not present here.
            await using var conn = new SqlConnection(vaultConnection); // audit-lint:ignore AUD002
            await conn.OpenAsync();
            foreach (var f in files)
            {
                if (!File.Exists(f)) continue;
                foreach (var batch in DbApplyCommand.SplitBatches(await File.ReadAllTextAsync(f)))
                {
                    await using var cmd = conn.CreateCommand();
                    cmd.CommandText = batch;
                    cmd.CommandTimeout = 120;
                    await cmd.ExecuteNonQueryAsync();
                }
                Console.WriteLine($"  applied {Path.GetFileName(f)} → AsdamirVault");
            }
            return 0;
        }
        catch (SqlException ex)
        {
            Console.Error.WriteLine($"✗ AsdamirVault seed apply failed: {ex.Message}");
            return 1;
        }
    }

    /// <summary>Walks up from <paramref name="start"/> to the nearest directory containing a .sln.</summary>
    internal static string? FindAppRoot(DirectoryInfo start)
    {
        for (var d = start; d is not null; d = d.Parent)
            if (d.GetFiles("*.sln").Any()) return d.FullName;
        return null;
    }

    /// <summary>Finds the Gateway/API project (Controllers/ or db/migrations) or the Server/UI project
    /// (Components/Pages/) under <c>&lt;appRoot&gt;/src</c>, by content — robust to custom project names.</summary>
    internal static string? FindProject(string appRoot, bool isGateway)
    {
        var srcDir = Path.Combine(appRoot, "src");
        if (!Directory.Exists(srcDir)) return null;
        foreach (var proj in Directory.GetDirectories(srcDir))
        {
            if (isGateway && (Directory.Exists(Path.Combine(proj, "Controllers")) || Directory.Exists(Path.Combine(proj, "db", "migrations"))))
                return proj;
            if (!isGateway && Directory.Exists(Path.Combine(proj, "Components", "Pages")))
                return proj;
        }
        return null;
    }
}
