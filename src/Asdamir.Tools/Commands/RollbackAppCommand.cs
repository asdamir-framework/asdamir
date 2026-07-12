// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later

using System.CommandLine;
using Microsoft.Data.SqlClient;

namespace Asdamir.Tools.Commands;

/// <summary>
/// <c>asdamir rollback app &lt;Name&gt; [--output dir] [--connection|-S -d -U -P] [--vault-connection &lt;vault&gt;] [--yes]</c>
///
/// The symmetric inverse of <c>new app</c>: tears down a generated app — (a) the generated directory
/// (the app root, an ancestor named after the app's <c>.sln</c>), (b) the app's OWN database
/// (<c>DROP DATABASE</c>, so it works for both free and commercial apps), and (c) — for a commercial app —
/// its AsdamirVault registration + all AppId-scoped rows (via the existing <c>dbo.App_Purge</c> proc).
///
/// DESTRUCTIVE — it shows EXACTLY what will be removed (the full directory path + the server/database name +
/// the vault app code) and asks before touching anything (<c>-y</c>/<c>--yes</c> skips for scripts).
/// Fail-closed: it NEVER drops a system DB or <c>AsdamirVault</c>, and <c>App_Purge</c> refuses the self-app
/// (<c>EnvironmentName='Self'</c>). Every step is conditional + idempotent: a missing directory / database /
/// registration is reported as "already gone", never an error. DB/vault cleanup runs only when the matching
/// connection is supplied (otherwise it is reported as skipped, never silently dropped).
/// </summary>
public static class RollbackAppCommand
{
    // Never DROP these — the control plane + SQL Server's own system databases.
    private static readonly HashSet<string> ProtectedDatabases =
        new(StringComparer.OrdinalIgnoreCase) { "AsdamirVault", "master", "model", "msdb", "tempdb" };

    public static Command Build()
    {
        var nameArg = new Argument<string>("name", "PascalCase app name to tear down (e.g. CustomerOrders).");
        var outputOpt = new Option<DirectoryInfo>(new[] { "--output", "-o" },
            description: "Where the app lives: its parent dir OR the app dir itself. Defaults to the current directory.",
            getDefaultValue: () => new DirectoryInfo(Directory.GetCurrentDirectory()));
        var connOpt = new Option<string>(new[] { "--connection", "-c" }, description: "App-DB connection string (its server is used to DROP the app database).", getDefaultValue: () => "");
        var serverOpt = new Option<string>(new[] { "--server", "-S" }, description: "App-DB server (when --connection is omitted).", getDefaultValue: () => "localhost");
        var databaseOpt = new Option<string>(new[] { "--database", "-d" }, description: "App database name to drop (defaults to the app name).", getDefaultValue: () => "");
        var userOpt = new Option<string>(new[] { "--user", "-U" }, description: "SQL login.", getDefaultValue: () => "");
        var passwordOpt = new Option<string>(new[] { "--password", "-P" }, description: "Password for --user.", getDefaultValue: () => "");
        var vaultConnOpt = new Option<string>("--vault-connection", description: "AsdamirVault connection (commercial mode: purge the app registration + AppId-scoped data).", getDefaultValue: () => "");
        var yesOpt = new Option<bool>(new[] { "--yes", "-y" }, description: "Skip the interactive confirmation (for scripts). Default is to ask.", getDefaultValue: () => false);

        var cmd = new Command("app", "Tear down a generated app — its directory + database + (commercial) AsdamirVault registration. DESTRUCTIVE — asks before deleting.")
        {
            nameArg, outputOpt, connOpt, serverOpt, databaseOpt, userOpt, passwordOpt, vaultConnOpt, yesOpt,
        };

        cmd.SetHandler(async ctx =>
        {
            ctx.ExitCode = await RunAsync(
                ctx.ParseResult.GetValueForArgument(nameArg),
                ctx.ParseResult.GetValueForOption(outputOpt)!,
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

    private static async Task<int> RunAsync(string name, DirectoryInfo output, string connection, string server,
        string database, string user, string password, string vaultConnection, bool yes)
    {
        if (string.IsNullOrWhiteSpace(name) || !char.IsUpper(name[0]))
        {
            Console.Error.WriteLine("App name must be PascalCase (e.g. CustomerOrders).");
            return 2;
        }

        // 1) Resolve the app root — the directory whose .sln is named after the app. Accept either the parent
        //    dir (<output>/<Name>) or the app dir itself (<output>). Requiring the <Name>.sln to be present is
        //    the guard against deleting the wrong directory. A missing dir is fine (idempotent — DB/vault may
        //    still need cleanup).
        var appRoot = ResolveAppRoot(output, name);

        // The app's OWN database defaults to the app name (the no-Db-suffix naming standard).
        var dbName = string.IsNullOrWhiteSpace(database) ? name : database.Trim();

        // 2) Fail-closed: never drop the control plane or a system database.
        if (ProtectedDatabases.Contains(dbName))
        {
            Console.Error.WriteLine($"Refusing to drop protected database '{dbName}' (control plane / system DB). A generated app's database is named after the app — pass the correct --database.");
            return 2;
        }

        // 3) Mode + code. Free apps have no control plane, so the vault step is skipped for them.
        var isFree = appRoot is not null && PageCommand.IsFreeModeApp(appRoot);
        var appCode = name; // == the .sln basename == dbo.Apps.Code that `new app` wrote

        // The DB step runs ONLY when the caller actually supplied DB targeting (a connection string, a SQL
        // login, an explicit --database, or a non-default server). Without any of these it is skipped +
        // reported — never attempted with integrated security (which fails on Linux/macOS).
        var hasDbTarget = !string.IsNullOrWhiteSpace(connection)
                       || !string.IsNullOrWhiteSpace(user)
                       || !string.IsNullOrWhiteSpace(database)
                       || !string.Equals(server, "localhost", StringComparison.OrdinalIgnoreCase);
        var masterConn = hasDbTarget ? BuildMasterConnection(connection, server, dbName, user, password) : null;

        // 4) Probe for the confirmation screen (best-effort — a probe failure never blocks the report).
        var serverLabel = masterConn is null ? "" : SafeServer(connection, server);
        var dbExists = masterConn is not null && await DatabaseExistsAsync(masterConn, dbName);
        (bool exists, bool isSelf, Guid appId) vault = (false, false, Guid.Empty);
        if (!isFree && !string.IsNullOrWhiteSpace(vaultConnection))
            vault = await ProbeVaultAppAsync(vaultConnection, appCode);

        // 5) Refuse the self-app on the vault side too (App_Purge is the DB-level backstop; this is the friendly stop).
        if (vault.exists && vault.isSelf)
        {
            Console.Error.WriteLine($"Refusing: the AsdamirVault registration for '{appCode}' is the self-app (AppManagement) — it can never be purged.");
            return 2;
        }

        // 6) Confirmation — show EVERYTHING first (full path + server/db + vault code).
        Console.WriteLine($"Will TEAR DOWN app '{name}':");
        Console.WriteLine(appRoot is null
            ? $"  Directory: (not found under '{output.FullName}' — skip)"
            : $"  Directory: {appRoot}  ← deleted recursively");
        Console.WriteLine(masterConn is null
            ? "  App DB: SKIP — no --connection/-S -d (database not dropped)"
            : dbExists
                ? $"  App DB: DROP DATABASE [{dbName}]  on server '{serverLabel}'"
                : $"  App DB: [{dbName}] on '{serverLabel}' — absent (skip)");
        if (isFree)
            Console.WriteLine("  AsdamirVault: N/A — free-mode app (no control plane).");
        else
            Console.WriteLine(string.IsNullOrWhiteSpace(vaultConnection)
                ? "  AsdamirVault: SKIP — no --vault-connection (registration not purged)"
                : vault.exists
                    ? $"  AsdamirVault: purge registration '{appCode}' (AppId {vault.appId}) + ALL its AppId-scoped rows"
                    : $"  AsdamirVault: registration '{appCode}' absent (skip)");
        Console.WriteLine();

        if (!yes)
        {
            Console.Write("This is DESTRUCTIVE. Continue? [y/N] ");
            var answer = Console.ReadLine();
            if (!string.Equals(answer?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Aborted — nothing was removed.");
                return 0;
            }
        }

        // 7) Tear down — directory, then DB, then vault. Each idempotent.
        if (appRoot is not null)
        {
            try { Directory.Delete(appRoot, recursive: true); Console.WriteLine($"Directory: removed {appRoot}."); }
            catch (Exception ex) { Console.Error.WriteLine($"Directory delete failed: {ex.Message}"); return 1; }
        }
        else Console.WriteLine("Directory: nothing to remove (already gone).");

        if (masterConn is not null)
        {
            try { var dropped = await DropDatabaseAsync(masterConn, dbName); Console.WriteLine(dropped ? $"App DB: dropped [{dbName}]." : $"App DB: [{dbName}] already absent."); }
            catch (SqlException ex) { Console.Error.WriteLine($"App DB drop failed: {ex.Message}"); return 1; }
        }
        else Console.WriteLine("App DB: NOT dropped (no --connection/-S -d). Re-run with a connection to DROP the database.");

        if (!isFree)
        {
            if (!string.IsNullOrWhiteSpace(vaultConnection))
            {
                try { var n = await PurgeVaultAsync(vaultConnection, appCode); Console.WriteLine(n > 0 ? $"AsdamirVault: purged registration '{appCode}'." : $"AsdamirVault: registration '{appCode}' already absent."); }
                catch (SqlException ex) { Console.Error.WriteLine($"AsdamirVault purge failed: {ex.Message}"); return 1; }
            }
            else Console.WriteLine("AsdamirVault: NOT purged (no --vault-connection). Re-run with --vault-connection to remove the registration.");
        }

        Console.WriteLine($"\n✓ Tore down app '{name}'.");
        return 0;
    }

    /// <summary>App root = the ancestor/child directory whose <c>&lt;Name&gt;.sln</c> exists. Checks
    /// <c>&lt;output&gt;/&lt;Name&gt;</c> (ran from the parent) then <c>&lt;output&gt;</c> and its ancestors
    /// (ran from inside). Returns null when no matching app is found (already deleted).</summary>
    private static string? ResolveAppRoot(DirectoryInfo output, string name)
    {
        var child = Path.Combine(output.FullName, name);
        if (File.Exists(Path.Combine(child, $"{name}.sln"))) return child;
        for (var d = output; d is not null; d = d.Parent)
            if (File.Exists(Path.Combine(d.FullName, $"{name}.sln"))) return d.FullName;
        return null;
    }

    private static string? BuildMasterConnection(string connection, string server, string database, string user, string password)
    {
        if (!string.IsNullOrWhiteSpace(connection))
        {
            var b = new SqlConnectionStringBuilder(connection) { InitialCatalog = "master" };
            return b.ConnectionString;
        }
        var sb = new SqlConnectionStringBuilder { DataSource = server, InitialCatalog = "master", TrustServerCertificate = true };
        if (!string.IsNullOrWhiteSpace(user)) { sb.UserID = user; sb.Password = password; } else sb.IntegratedSecurity = true;
        return sb.ConnectionString;
    }

    private static string SafeServer(string connection, string server)
    {
        if (string.IsNullOrWhiteSpace(connection)) return server;
        try { return new SqlConnectionStringBuilder(connection).DataSource; } catch { return server; }
    }

    private static async Task<bool> DatabaseExistsAsync(string masterConn, string dbName)
    {
        try
        {
            await using var c = new SqlConnection(masterConn); // audit-lint:ignore AUD002
            await c.OpenAsync();
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT CASE WHEN DB_ID(@db) IS NULL THEN 0 ELSE 1 END";
            cmd.Parameters.AddWithValue("@db", dbName);
            return (int)(await cmd.ExecuteScalarAsync())! == 1;
        }
        catch (SqlException) { return false; } // unreachable server → report as absent; the drop step will surface the real error
    }

    /// <summary>Drops the app database (idempotent). Kicks existing connections (SINGLE_USER WITH ROLLBACK
    /// IMMEDIATE) so the drop can't hang on an open handle. The name is injected via QUOTENAME (DROP DATABASE
    /// cannot take a parameter), and it is guarded against the protected set before we ever get here.</summary>
    private static async Task<bool> DropDatabaseAsync(string masterConn, string dbName)
    {
        await using var c = new SqlConnection(masterConn); // audit-lint:ignore AUD002
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText =
            "IF DB_ID(@db) IS NULL BEGIN SELECT 0; END ELSE BEGIN " +
            "DECLARE @sql nvarchar(max) = N'ALTER DATABASE ' + QUOTENAME(@db) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE ' + QUOTENAME(@db) + N';'; " +
            "EXEC sp_executesql @sql; SELECT 1; END";
        cmd.Parameters.AddWithValue("@db", dbName);
        return (int)(await cmd.ExecuteScalarAsync())! == 1;
    }

    private static async Task<(bool exists, bool isSelf, Guid appId)> ProbeVaultAppAsync(string vaultConn, string appCode)
    {
        try
        {
            await using var c = new SqlConnection(vaultConn); // audit-lint:ignore AUD002
            await c.OpenAsync();
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT AppId, CASE WHEN EnvironmentName = N'Self' THEN 1 ELSE 0 END FROM dbo.Apps WHERE Code = @code";
            cmd.Parameters.AddWithValue("@code", appCode);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return (false, false, Guid.Empty);
            return (true, r.GetInt32(1) == 1, r.GetGuid(0));
        }
        catch (SqlException) { return (false, false, Guid.Empty); }
    }

    /// <summary>Purges the app registration + all AppId-scoped rows via the existing <c>dbo.App_Purge</c>
    /// proc (which cascades in FK-safe order inside one transaction and refuses the self-app). Idempotent —
    /// returns 0 when the app isn't registered.</summary>
    private static async Task<int> PurgeVaultAsync(string vaultConn, string appCode)
    {
        await using var c = new SqlConnection(vaultConn); // audit-lint:ignore AUD002
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText =
            "SET NOCOUNT ON; DECLARE @aid UNIQUEIDENTIFIER = (SELECT AppId FROM dbo.Apps WHERE Code = @code); " +
            "IF @aid IS NULL BEGIN SELECT 0; RETURN; END " +
            "DECLARE @out TABLE (n INT); INSERT INTO @out EXEC dbo.App_Purge @aid; SELECT ISNULL((SELECT TOP 1 n FROM @out), 0);";
        cmd.Parameters.AddWithValue("@code", appCode);
        var result = await cmd.ExecuteScalarAsync();
        return result is int i ? i : 0;
    }
}
