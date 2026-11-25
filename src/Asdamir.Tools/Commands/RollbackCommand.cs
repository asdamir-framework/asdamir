// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later

using System.CommandLine;
using Microsoft.Data.SqlClient;

namespace Asdamir.Tools.Commands;

/// <summary>
/// <c>asdamir rollback &lt;Name&gt; [--connection|-S -d -U -P] [--vault-connection &lt;vault&gt;] [--yes]</c>
///
/// The inverse of <c>new feature</c>: removes a generated entity + page + its menu/permission seed.
/// DESTRUCTIVE — it deletes code files, DROPs the app table, deletes the migration journal rows, and
/// removes the AsdamirVault menu/permission/grants. Every step is conditional ("only what exists"),
/// scoped by exact name (no broad globbing) and AppId, and shown for confirmation before anything is
/// touched. Code files are always removed (after confirmation); DB cleanup happens only when the matching
/// connection is supplied (otherwise it is reported as skipped, never silently dropped).
/// </summary>
public static class RollbackCommand
{
    public static Command Build()
    {
        var nameArg = new Argument<string>("name", "PascalCase entity name to roll back (e.g. Invoice).");
        var outputOpt = new Option<DirectoryInfo>(new[] { "--output", "-o" },
            description: "App root (nearest ancestor with a .sln). Defaults to the current directory.",
            getDefaultValue: () => new DirectoryInfo(Directory.GetCurrentDirectory()));
        var gatewayDirOpt = new Option<string>("--gateway-dir", description: "Override: the Gateway/API project directory.", getDefaultValue: () => "");
        var serverDirOpt = new Option<string>("--server-dir", description: "Override: the Server/UI project directory.", getDefaultValue: () => "");
        var connOpt = new Option<string>(new[] { "--connection", "-c" }, description: "App-DB connection string (to DROP the table + clean the migration journal).", getDefaultValue: () => "");
        var serverOpt = new Option<string>(new[] { "--server", "-S" }, description: "App-DB server (when --connection is omitted).", getDefaultValue: () => "localhost");
        var databaseOpt = new Option<string>(new[] { "--database", "-d" }, description: "App-DB name.", getDefaultValue: () => "");
        var userOpt = new Option<string>(new[] { "--user", "-U" }, description: "App-DB SQL login.", getDefaultValue: () => "");
        var passwordOpt = new Option<string>(new[] { "--password", "-P" }, description: "Password for --user.", getDefaultValue: () => "");
        var vaultConnOpt = new Option<string>("--vault-connection", description: "AsdamirVault connection (to remove the menu/permission/grants).", getDefaultValue: () => "");
        var yesOpt = new Option<bool>(new[] { "--yes", "-y" }, description: "Skip the interactive confirmation (for scripts). Default is to ask.", getDefaultValue: () => false);

        var cmd = new Command("rollback", "Remove a generated feature (entity + page + menu/permission). DESTRUCTIVE — asks before deleting.")
        {
            nameArg, outputOpt, gatewayDirOpt, serverDirOpt, connOpt, serverOpt, databaseOpt, userOpt, passwordOpt, vaultConnOpt, yesOpt,
        };

        // `asdamir rollback app <Name>` — the symmetric inverse of `new app` (whole-app teardown). A subcommand,
        // so the bare `rollback <Feature>` form is unaffected (only a feature literally named "app" is shadowed).
        cmd.AddCommand(RollbackAppCommand.Build());

        cmd.SetHandler(async ctx =>
        {
            ctx.ExitCode = await RunAsync(
                ctx.ParseResult.GetValueForArgument(nameArg),
                ctx.ParseResult.GetValueForOption(outputOpt)!,
                ctx.ParseResult.GetValueForOption(gatewayDirOpt) ?? "",
                ctx.ParseResult.GetValueForOption(serverDirOpt) ?? "",
                ctx.ParseResult.GetValueForOption(connOpt) ?? "",
                ctx.ParseResult.GetValueForOption(serverOpt) ?? "localhost",
                ctx.ParseResult.GetValueForOption(databaseOpt) ?? "",
                ctx.ParseResult.GetValueForOption(userOpt) ?? "",
                ctx.ParseResult.GetValueForOption(passwordOpt) ?? "",
                ctx.ParseResult.GetValueForOption(vaultConnOpt) ?? "",
                ctx.ParseResult.GetValueForOption(yesOpt));
        });
        return cmd;
    }

    private static async Task<int> RunAsync(string name, DirectoryInfo output, string gatewayOverride, string serverOverride,
        string connection, string server, string database, string user, string password, string vaultConnection, bool yes)
    {
        if (!NameArgument.IsValid(name))
        {
            Console.Error.WriteLine(NameArgument.Explain(name, "entity name", "asdamir rollback <Name>"));
            return ExitCodes.Usage;
        }

        var appRoot = FeatureCommand.FindAppRoot(output);
        if (appRoot is null) { Console.Error.WriteLine("Could not find an app root (no .sln above the output dir). Pass --output."); return ExitCodes.Usage; }

        var gatewayDir = !string.IsNullOrWhiteSpace(gatewayOverride) ? gatewayOverride : FeatureCommand.FindProject(appRoot, isGateway: true);
        var serverDir = !string.IsNullOrWhiteSpace(serverOverride) ? serverOverride : FeatureCommand.FindProject(appRoot, isGateway: false);

        // A free-mode app keeps its management data (menu/permission/localization) in its OWN database and
        // seeds it via V*__freemode_{menu,localize}_<plural>.sql migrations (not AsdamirVault). Detected the
        // same way `new feature` does — so rollback tears those down symmetrically, over the app connection.
        var isFree = PageCommand.IsFreeModeApp(appRoot);

        var plural = NameHelper.Pluralize(name);
        var pluralLower = plural.ToLowerInvariant();
        var permName = $"{pluralLower}.view";

        // 1) Inventory: code files that actually exist (exact paths, no broad globbing).
        var codeFiles = new List<string>();
        void AddIfExists(string? baseDir, params string[] rel)
        {
            if (baseDir is null) return;
            foreach (var r in rel) { var p = Path.Combine(baseDir, r); if (File.Exists(p)) codeFiles.Add(p); }
        }
        AddIfExists(gatewayDir,
            $"Domain/{name}.cs", $"Dtos/{name}Dto.cs",
            $"Repositories/I{name}Repository.cs", $"Repositories/{name}Repository.cs",
            $"Services/I{name}Service.cs", $"Services/{name}Service.cs",
            $"Controllers/{plural}Controller.cs", $"Validators/{name}DtoValidator.cs");
        AddIfExists(serverDir,
            $"Dtos/{name}Dto.cs",
            $"Components/Pages/{plural}List.razor", $"Components/Pages/{name}EditorDialog.razor");
        AddIfExists(appRoot,
            Path.Combine("db", "admin-onboarding", $"localize_{pluralLower}.sql"),
            Path.Combine("db", "admin-onboarding", $"seed_menu_{pluralLower}.sql"));
        // Migration files + the entity's test file (globbed by EXACT suffix).
        codeFiles.AddRange(GlobMigrations(gatewayDir, $"__create_{pluralLower}.sql"));
        codeFiles.AddRange(GlobMigrations(gatewayDir, $"__seed_{pluralLower}.sql"));
        codeFiles.AddRange(Glob(Path.Combine(appRoot, "tests"), $"{name}Tests.cs"));
        // Free mode: the menu/permission + localization seeds are migrations in the app-root db/migrations.
        var appMigrations = Path.Combine(appRoot, "db", "migrations");
        if (isFree)
        {
            codeFiles.AddRange(Glob(appMigrations, $"*__freemode_menu_{pluralLower}.sql"));
            codeFiles.AddRange(Glob(appMigrations, $"*__freemode_localize_{pluralLower}.sql"));
        }

        var addFieldMigs = GlobMigrations(gatewayDir, $"_to_{pluralLower}.sql").ToList(); // *__add_<field>_to_<plural>.sql

        // 2) DB inventory (only when the matching connection is given).
        var appConn = BuildAppConnection(connection, server, database, user, password);
        var appCode = Path.GetFileNameWithoutExtension(Directory.GetFiles(appRoot, "*.sln").FirstOrDefault() ?? "");
        bool tableExists, appRegistered, permExists;
        int journalRows, menuCount, grantCount;
        try
        {
            (tableExists, journalRows) = appConn is null ? (false, 0) : await ProbeAppDbAsync(appConn, plural, pluralLower);
            (appRegistered, permExists, menuCount, grantCount) = string.IsNullOrWhiteSpace(vaultConnection)
                ? (false, false, 0, 0)
                : await ProbeVaultAsync(vaultConnection, appCode, permName);
        }
        catch (SqlException ex)
        {
            // A connection was GIVEN and cannot be used. Going on would delete the code files while leaving
            // the table/menu behind — exactly the half-rollback the ordering below exists to prevent.
            Console.Error.WriteLine($"Could not inspect the database: {ex.Message}");
            Console.Error.WriteLine("Nothing was removed — fix the connection (or omit it to remove the code files only) and re-run.");
            return 1;
        }

        // Is dbo.<Plural> THIS entity's table? Decided from evidence gathered BEFORE anything is deleted (the
        // create-migration file is one of the code files removed below).
        var hasCreateMigration = GlobMigrations(gatewayDir, $"__create_{pluralLower}.sql").Any();
        var tableDrop = DecideTableDrop(plural, hasCreateMigration, journalRows);
        // Free mode: the management rows live in the app's OWN db, so they come off the app connection.
        var free = (isFree && appConn is not null) ? await ProbeFreeSeedsAsync(appConn, name, plural, pluralLower) : (perm: 0, menu: 0, loc: 0, journal: 0);

        // 3) Confirmation — show EVERYTHING first.
        Console.WriteLine($"Will remove feature '{name}':");
        Console.WriteLine($"  Code ({codeFiles.Count} files):");
        foreach (var f in codeFiles) Console.WriteLine($"    {Path.GetRelativePath(appRoot, f)}");
        if (codeFiles.Count == 0) Console.WriteLine("    (none found)");
        Console.WriteLine(appConn is null
            ? "  App DB: SKIP — no --connection (table/journal not removed)"
            : tableExists || journalRows > 0
                ? tableDrop.Drop
                    ? $"  App DB: DROP TABLE dbo.{plural}{(tableExists ? "" : " (absent — skip)")} + its 5 CRUD procedures + {journalRows} journal row(s)"
                    : $"  App DB: dbo.{plural} is KEPT — {tableDrop.Reason}{(journalRows > 0 ? $" ({journalRows} journal row(s) are removed)" : "")}"
                : $"  App DB: nothing to remove (table absent, no journal rows)");
        if (isFree)
        {
            Console.WriteLine(appConn is null
                ? "  Free-mode mgmt (app DB): SKIP — no --connection (menu/permission/localization not removed)"
                : free.perm > 0 || free.menu > 0 || free.loc > 0 || free.journal > 0
                    ? $"  Free-mode mgmt (app DB): permission '{permName}', {free.menu} menu(s), {free.loc} localization key(s), {free.journal} seed-journal row(s)"
                    : "  Free-mode mgmt (app DB): nothing to remove");
        }
        else
        {
            Console.WriteLine(string.IsNullOrWhiteSpace(vaultConnection)
                ? "  AsdamirVault: SKIP — no --vault-connection (menu/permission not removed)"
                : !appRegistered
                    ? $"  AsdamirVault: nothing to remove — app '{appCode}' is not registered in this vault"
                    : permExists
                        ? $"  AsdamirVault (app '{appCode}' only): permission '{permName}', {menuCount} menu(s), {grantCount} grant(s)"
                        : $"  AsdamirVault: nothing to remove (permission '{permName}' absent for app '{appCode}')");
        }
        if (addFieldMigs.Count > 0)
        {
            Console.WriteLine($"  ⚠️  add-field migrations found (NOT removed — handle manually):");
            foreach (var f in addFieldMigs) Console.WriteLine($"      {Path.GetRelativePath(appRoot, f)}");
        }
        Console.WriteLine($"  ⚠️  if other code references '{name}', it may stop compiling.");
        Console.WriteLine();

        if (!yes)
        {
            Console.Write("Continue? [y/N] ");
            var answer = Console.ReadLine();
            if (!string.Equals(answer?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Aborted — nothing was removed.");
                return 0;
            }
        }

        // 4) Delete — DATABASES FIRST, code files LAST.
        //
        // The order is the retry story. Each database step is one transaction that either completes or rolls
        // back; if one fails we stop with every code file — including the create-migration that proves the
        // table is this entity's — still on disk, so the command can simply be run again. Files-first (the
        // previous order) left a failed run with the table still there and the evidence for dropping it gone.
        if (appConn is not null)
        {
            try
            {
                await RemoveFromAppDbAsync(appConn, plural, pluralLower, tableDrop.Drop);
                Console.WriteLine(tableDrop.Drop
                    ? $"App DB: dropped dbo.{plural} + its CRUD procedures (if they existed) + cleaned journal."
                    : $"App DB: dbo.{plural} KEPT ({tableDrop.Reason}); journal cleaned.");
                // Free mode: also remove the management rows the free-mode seeds wrote into THIS db.
                if (isFree)
                {
                    await RemoveFreeSeedsFromAppDbAsync(appConn, name, plural, pluralLower);
                    Console.WriteLine("App DB (free-mode mgmt): removed menu/permission/grants + localization + seed-journal rows.");
                }
            }
            catch (SqlException ex)
            {
                Console.Error.WriteLine($"App DB cleanup failed (rolled back): {ex.Message}");
                Console.Error.WriteLine("Nothing was deleted from disk — fix the cause and re-run.");
                return 1;
            }
        }
        else
        {
            Console.WriteLine(isFree
                ? "App DB: NOT cleaned (no --connection). Re-run with --connection/-S -d -U -P to DROP the table + remove the free-mode menu/permission/localization + clean the journal."
                : "App DB: NOT cleaned (no --connection). Re-run with --connection/-S -d -U -P to DROP the table + clean the journal.");
        }

        // AsdamirVault is a commercial-only concept — a free app has no control plane, so skip it entirely.
        if (!isFree)
        {
            if (!string.IsNullOrWhiteSpace(vaultConnection))
            {
                try
                {
                    var n = await RemoveFromVaultAsync(vaultConnection, appCode, permName);
                    Console.WriteLine(n < 0
                        ? $"AsdamirVault: app '{appCode}' is not registered in this vault — nothing removed."
                        : $"AsdamirVault: removed menu/permission/grants of app '{appCode}' ({n} row(s)).");
                }
                catch (SqlException ex)
                {
                    Console.Error.WriteLine($"AsdamirVault cleanup failed (rolled back): {ex.Message}");
                    Console.Error.WriteLine("The code files were NOT deleted — fix the cause and re-run.");
                    return 1;
                }
            }
            else
            {
                Console.WriteLine("AsdamirVault: NOT cleaned (no --vault-connection). Re-run with --vault-connection to remove the menu/permission.");
            }
        }

        var deleted = 0;
        foreach (var f in codeFiles) { try { File.Delete(f); deleted++; } catch (IOException ex) { Console.Error.WriteLine($"  ! could not delete {f}: {ex.Message}"); } }
        Console.WriteLine($"Removed {deleted} code file(s).");

        Console.WriteLine($"\n✓ Rolled back '{name}'.");
        return 0;
    }

    /// <summary>
    /// Decides whether <c>rollback</c> may drop <c>dbo.&lt;table&gt;</c> — only when there is evidence that THIS
    /// entity's own migration created it.
    /// </summary>
    /// <remarks>
    /// <para>The table name is just the plural of the entity name, so it can coincide with a table the
    /// framework owns: in a <c>--billing</c> app, <c>rollback Invoice</c> used to drop the billing
    /// <c>dbo.Invoices</c> (likewise <c>Plan</c>, <c>Subscription</c>, <c>User</c>, <c>Role</c>, <c>Menu</c>).
    /// Two independent conditions must hold:</para>
    /// <list type="number">
    /// <item><description>the table is NOT framework-owned (<see cref="ReservedEntityNames"/>). For such a
    /// name even a create-migration file proves nothing — its <c>CREATE TABLE</c> is guarded by
    /// <c>IF OBJECT_ID … IS NULL</c>, so it was skipped and the table that exists is the framework's;</description></item>
    /// <item><description>the entity's <c>*__create_&lt;plural&gt;.sql</c> migration exists on disk, or the
    /// migration journal records it as applied.</description></item>
    /// </list>
    /// </remarks>
    /// <param name="table">The table name (the plural of the entity name).</param>
    /// <param name="hasCreateMigrationFile">The entity's create migration is on disk.</param>
    /// <param name="createJournalRows">Journal rows recording the entity's create/seed migrations.</param>
    /// <returns>Whether to drop, and — when not — why.</returns>
    internal static (bool Drop, string Reason) DecideTableDrop(string table, bool hasCreateMigrationFile, int createJournalRows)
    {
        if (ReservedEntityNames.IsFrameworkTable(table))
        {
            return (false, $"it is a framework-owned table, not this entity's (drop it by hand only if you are certain: DROP TABLE dbo.[{table}])");
        }

        if (!hasCreateMigrationFile && createJournalRows == 0)
        {
            return (false, $"no evidence this entity created it — no *__create_{table.ToLowerInvariant()}.sql migration on disk and no journal row (drop it by hand if it is yours: DROP TABLE dbo.[{table}])");
        }

        return (true, "");
    }

    private static IEnumerable<string> GlobMigrations(string? gatewayDir, string suffix)
        => gatewayDir is null ? Array.Empty<string>() : Glob(Path.Combine(gatewayDir, "db", "migrations"), "*" + suffix);

    private static List<string> Glob(string dir, string pattern)
    {
        if (!Directory.Exists(dir)) return new List<string>();
        return Directory.GetFiles(dir, pattern, SearchOption.AllDirectories).ToList();
    }

    private static string? BuildAppConnection(string connection, string server, string database, string user, string password)
    {
        if (!string.IsNullOrWhiteSpace(connection))
        {
            var b = new SqlConnectionStringBuilder(connection);
            if (!string.IsNullOrWhiteSpace(database)) b.InitialCatalog = database;
            return b.ConnectionString;
        }
        if (string.IsNullOrWhiteSpace(database)) return null;  // no target → app-DB step skipped
        var sb = new SqlConnectionStringBuilder { DataSource = server, InitialCatalog = database, TrustServerCertificate = true };
        if (!string.IsNullOrWhiteSpace(user)) { sb.UserID = user; sb.Password = password; } else sb.IntegratedSecurity = true;
        return sb.ConnectionString;
    }

    private static async Task<(bool tableExists, int journalRows)> ProbeAppDbAsync(string conn, string plural, string pluralLower)
    {
        await using var c = new SqlConnection(conn); // audit-lint:ignore AUD002
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText =
            "SELECT (SELECT COUNT(*) FROM sys.tables WHERE name=@t), " +
            "(SELECT COUNT(*) FROM dbo.__SchemaMigrations WHERE Id LIKE '%[_][_]create[_]'+@p+'.sql' OR Id LIKE '%[_][_]seed[_]'+@p+'.sql')";
        cmd.Parameters.AddWithValue("@t", plural);
        cmd.Parameters.AddWithValue("@p", pluralLower);
        await using var r = await cmd.ExecuteReaderAsync();
        await r.ReadAsync();
        return (r.GetInt32(0) > 0, r.GetInt32(1));
    }

    /// <summary>
    /// The app-DB teardown batch: optionally the entity's table and its five generated CRUD procedures, always
    /// its create/seed journal rows — one transaction (<c>XACT_ABORT ON</c>, so any error rolls all of it back).
    /// </summary>
    /// <param name="dropTable">False keeps the table and procedures (see <see cref="DecideTableDrop"/>).</param>
    /// <returns>The parameterised T-SQL (<c>@t</c> = table, <c>@p</c> = lower-case plural).</returns>
    internal static string BuildAppDbCleanupSql(bool dropTable)
    {
        var drop = dropTable
            ? "DECLARE @sql nvarchar(max) = N'DROP TABLE IF EXISTS dbo.' + QUOTENAME(@t) + N';'"
              + " + N' DROP PROCEDURE IF EXISTS dbo.' + QUOTENAME(@t + N'_GetById') + N';'"
              + " + N' DROP PROCEDURE IF EXISTS dbo.' + QUOTENAME(@t + N'_List') + N';'"
              + " + N' DROP PROCEDURE IF EXISTS dbo.' + QUOTENAME(@t + N'_Insert') + N';'"
              + " + N' DROP PROCEDURE IF EXISTS dbo.' + QUOTENAME(@t + N'_Update') + N';'"
              + " + N' DROP PROCEDURE IF EXISTS dbo.' + QUOTENAME(@t + N'_Delete') + N';'; EXEC sp_executesql @sql; "
            : "";
        return "SET QUOTED_IDENTIFIER ON; SET XACT_ABORT ON; BEGIN TRAN; " + drop
               + "DELETE FROM dbo.__SchemaMigrations WHERE Id LIKE '%[_][_]create[_]'+@p+'.sql' OR Id LIKE '%[_][_]seed[_]'+@p+'.sql'; "
               + "COMMIT;";
    }

    private static async Task RemoveFromAppDbAsync(string conn, string plural, string pluralLower, bool dropTable)
    {
        await using var c = new SqlConnection(conn); // audit-lint:ignore AUD002
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = BuildAppDbCleanupSql(dropTable);
        cmd.Parameters.AddWithValue("@t", plural);
        cmd.Parameters.AddWithValue("@p", pluralLower);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Free mode: the management rows live in the app's OWN db (single-tenant — no AppId) ──────────────
    // Localization keys the free-mode page-localization seed writes: Page.<Name>.Title, Field.<Name>.*, and
    // the nav-label key Menu.<Plural> (derived from the default route /<plural>). Matched by exact name.
    private static async Task<(int perm, int menu, int loc, int journal)> ProbeFreeSeedsAsync(string conn, string name, string plural, string pluralLower)
    {
        try
        {
            await using var c = new SqlConnection(conn); // audit-lint:ignore AUD002
            await c.OpenAsync();
            await using var cmd = c.CreateCommand();
            cmd.CommandText =
                "IF OBJECT_ID('dbo.Permissions') IS NULL BEGIN SELECT 0,0,0,0; END ELSE BEGIN " +
                "DECLARE @pid INT = (SELECT TOP 1 Id FROM dbo.Permissions WHERE Name=@perm); " +
                "SELECT CASE WHEN @pid IS NULL THEN 0 ELSE 1 END, " +
                "(SELECT COUNT(*) FROM dbo.Menus WHERE PermissionId=@pid), " +
                "(SELECT COUNT(*) FROM dbo.LocalizationResource WHERE [Key]=@pageKey OR [Key] LIKE @fieldPrefix OR [Key]=@menuKey), " +
                "(SELECT COUNT(*) FROM dbo.__SchemaMigrations WHERE Id LIKE '%__freemode_menu_'+@p+'.sql' OR Id LIKE '%__freemode_localize_'+@p+'.sql') END";
            AddFreeParams(cmd, name, plural, pluralLower);
            await using var r = await cmd.ExecuteReaderAsync();
            await r.ReadAsync();
            return (r.GetInt32(0), r.GetInt32(1), r.GetInt32(2), r.GetInt32(3));
        }
        catch (SqlException) { return (0, 0, 0, 0); } // freemode schema not applied on this DB — nothing to probe
    }

    /// <summary>Removes the free-mode menu + permission + grants + localization + seed-journal rows from the
    /// app's OWN db (single-tenant), in FK order (UserMenuPermissions → RolePermissions → Menus →
    /// Permissions), in one transaction. Mirror of the AsdamirVault teardown, minus the AppId scoping.</summary>
    private static async Task RemoveFreeSeedsFromAppDbAsync(string conn, string name, string plural, string pluralLower)
    {
        await using var c = new SqlConnection(conn); // audit-lint:ignore AUD002
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText =
            "SET QUOTED_IDENTIFIER ON; BEGIN TRAN; " +
            "DECLARE @pid INT = (SELECT Id FROM dbo.Permissions WHERE Name=@perm); " +
            "IF @pid IS NOT NULL BEGIN " +
            "  DELETE FROM dbo.UserMenuPermissions WHERE MenuId IN (SELECT Id FROM dbo.Menus WHERE PermissionId=@pid); " +
            "  DELETE FROM dbo.RolePermissions WHERE PermissionId=@pid; " +
            "  DELETE FROM dbo.Menus WHERE PermissionId=@pid; " +
            "  DELETE FROM dbo.Permissions WHERE Id=@pid; " +
            "END " +
            "DELETE FROM dbo.LocalizationResource WHERE [Key]=@pageKey OR [Key] LIKE @fieldPrefix OR [Key]=@menuKey; " +
            "DELETE FROM dbo.__SchemaMigrations WHERE Id LIKE '%__freemode_menu_'+@p+'.sql' OR Id LIKE '%__freemode_localize_'+@p+'.sql'; " +
            "COMMIT;";
        AddFreeParams(cmd, name, plural, pluralLower);
        await cmd.ExecuteNonQueryAsync();
    }

    private static void AddFreeParams(SqlCommand cmd, string name, string plural, string pluralLower)
    {
        cmd.Parameters.AddWithValue("@perm", $"{pluralLower}.view");
        cmd.Parameters.AddWithValue("@pageKey", $"Page.{name}.Title");
        cmd.Parameters.AddWithValue("@fieldPrefix", $"Field.{name}.%");
        cmd.Parameters.AddWithValue("@menuKey", $"Menu.{plural}");
        cmd.Parameters.AddWithValue("@p", pluralLower);
    }

    /// <summary>The AsdamirVault probe — every count is scoped to the app's AppId.</summary>
    /// <remarks>
    /// It used to look the permission up by NAME alone (<c>SELECT TOP 1 … WHERE Name=@n</c>). Permission
    /// codes such as <c>orders.view</c> are per-app, so in a vault holding two apps with an <c>Order</c>
    /// feature the confirmation screen counted — and described as about to be removed — ANOTHER app's menu
    /// and grants.
    /// </remarks>
    internal const string VaultProbeSql =
        "DECLARE @aid UNIQUEIDENTIFIER = (SELECT AppId FROM dbo.Apps WHERE Code=@code); " +
        "DECLARE @pid INT = (SELECT TOP 1 Id FROM dbo.Permissions WHERE Name=@n AND AppId=@aid); " +
        "SELECT CASE WHEN @aid IS NULL THEN 0 ELSE 1 END, CASE WHEN @pid IS NULL THEN 0 ELSE 1 END, " +
        "(SELECT COUNT(*) FROM dbo.Menus WHERE PermissionId=@pid AND AppId=@aid), " +
        "(SELECT COUNT(*) FROM dbo.RolePermissions WHERE PermissionId=@pid)";

    /// <summary>The AsdamirVault teardown — AppId-scoped, FK order, one transaction. Returns -1 when the app is not registered.</summary>
    internal const string VaultRemoveSql =
        "SET QUOTED_IDENTIFIER ON; SET XACT_ABORT ON; " +
        "DECLARE @aid UNIQUEIDENTIFIER = (SELECT AppId FROM dbo.Apps WHERE Code=@code); " +
        "IF @aid IS NULL BEGIN SELECT -1; RETURN; END " +
        "DECLARE @pid INT = (SELECT Id FROM dbo.Permissions WHERE Name=@pn AND AppId=@aid); " +
        "IF @pid IS NULL BEGIN SELECT 0; RETURN; END " +
        "DECLARE @cnt INT = 0; BEGIN TRAN; " +
        "DELETE FROM dbo.UserMenuPermissions WHERE MenuId IN (SELECT Id FROM dbo.Menus WHERE PermissionId=@pid AND AppId=@aid); SET @cnt += @@ROWCOUNT; " +
        "DELETE FROM dbo.RolePermissions WHERE PermissionId=@pid; SET @cnt += @@ROWCOUNT; " +
        "DELETE FROM dbo.Menus WHERE PermissionId=@pid AND AppId=@aid; SET @cnt += @@ROWCOUNT; " +
        "DELETE FROM dbo.Permissions WHERE Id=@pid AND AppId=@aid; SET @cnt += @@ROWCOUNT; " +
        "COMMIT; SELECT @cnt;";

    private static async Task<(bool appRegistered, bool permExists, int menuCount, int grantCount)> ProbeVaultAsync(string vaultConn, string appCode, string permName)
    {
        await using var c = new SqlConnection(vaultConn); // audit-lint:ignore AUD002
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = VaultProbeSql;
        cmd.Parameters.AddWithValue("@code", appCode);
        cmd.Parameters.AddWithValue("@n", permName);
        await using var r = await cmd.ExecuteReaderAsync();
        await r.ReadAsync();
        return (r.GetInt32(0) > 0, r.GetInt32(1) > 0, r.GetInt32(2), r.GetInt32(3));
    }

    /// <summary>Removes the page's menu + permission + grants from AsdamirVault, AppId-scoped, in FK order
    /// (UserMenuPermissions → RolePermissions → Menus → Permissions), in one transaction.</summary>
    private static async Task<int> RemoveFromVaultAsync(string vaultConn, string appCode, string permName)
    {
        await using var c = new SqlConnection(vaultConn); // audit-lint:ignore AUD002
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = VaultRemoveSql;
        cmd.Parameters.AddWithValue("@code", appCode);
        cmd.Parameters.AddWithValue("@pn", permName);
        var result = await cmd.ExecuteScalarAsync();
        return result is int i ? i : 0;
    }
}
