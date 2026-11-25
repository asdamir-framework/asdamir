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
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.CommandLine;
using Microsoft.Data.SqlClient;

namespace Asdamir.Tools.Commands;

/// <summary>
/// <c>asdamir db apply --connection "&lt;connstr&gt;" [--migrations db/migrations] [--create-database]</c>
/// (or <c>--server S --database D [--user U --password P]</c>).
///
/// A small, journaled migration runner: creates the target database (optional), then applies every
/// <c>*.sql</c> migration in a directory **in filename order, exactly once**. Applied migrations are
/// recorded in <c>dbo.__SchemaMigrations</c>, so re-runs skip what's already applied — incremental
/// deploys are safe and a migration that isn't itself idempotent is never re-executed. Each file is
/// split into batches on <c>GO</c> (SqlClient, unlike sqlcmd, doesn't understand <c>GO</c>).
///
/// No outer transaction is imposed: several migrations manage their own <c>BEGIN TRAN</c> and some
/// contain DDL that can't run inside a transaction (e.g. <c>ALTER DATABASE</c>). A migration is
/// journaled only after all its batches succeed; a partial failure leaves it un-journaled so the next
/// run retries it.
/// </summary>
public static class DbApplyCommand
{
    private const string JournalTable = "dbo.__SchemaMigrations";

    public static Command Build()
    {
        var connOpt = new Option<string>(
            new[] { "--connection", "-c" },
            description: "Full ADO.NET connection string to the target database. Takes precedence over --server/--database/--user/--password.",
            getDefaultValue: () => "");

        var serverOpt = new Option<string>(
            new[] { "--server", "-S" },
            description: "SQL Server instance (used when --connection is omitted).",
            getDefaultValue: () => "localhost");

        var databaseOpt = new Option<string>(
            new[] { "--database", "-d" },
            description: "Target database name. Required when --connection is omitted; otherwise overrides the connection's Initial Catalog.",
            getDefaultValue: () => "");

        var userOpt = new Option<string>(
            new[] { "--user", "-U" },
            description: "SQL Server login (SQL auth — cross-platform). When omitted, Windows integrated auth is used.",
            getDefaultValue: () => "");

        var passwordOpt = new Option<string>(
            new[] { "--password", "-P" },
            description: "Password for --user. Prefer passing the full --connection from a secret instead of this flag.",
            getDefaultValue: () => "");

        var migrationsOpt = new Option<DirectoryInfo>(
            new[] { "--migrations", "-m" },
            description: "Directory of *.sql migrations to apply in filename order.",
            getDefaultValue: () => new DirectoryInfo(Path.Combine(Directory.GetCurrentDirectory(), "db", "migrations")));

        var createDbOpt = new Option<bool>(
            "--create-database",
            description: "Create the target database (CREATE DATABASE) if it does not already exist, before applying migrations.",
            getDefaultValue: () => false);

        var cmd = new Command("apply", "Create the database (optional) and apply *.sql migrations once each (journaled) against SQL Server.")
        {
            connOpt, serverOpt, databaseOpt, userOpt, passwordOpt, migrationsOpt, createDbOpt,
        };

        cmd.SetHandler(async ctx =>
        {
            ctx.ExitCode = await RunAsync(
                ctx.ParseResult.GetValueForOption(connOpt) ?? "",
                ctx.ParseResult.GetValueForOption(serverOpt) ?? "localhost",
                ctx.ParseResult.GetValueForOption(databaseOpt) ?? "",
                ctx.ParseResult.GetValueForOption(userOpt) ?? "",
                ctx.ParseResult.GetValueForOption(passwordOpt) ?? "",
                ctx.ParseResult.GetValueForOption(migrationsOpt)!,
                ctx.ParseResult.GetValueForOption(createDbOpt));
        });
        return cmd;
    }

    /// <summary>The journaled migration runner. Exposed (internal) so other commands — e.g.
    /// <c>new entity --apply</c> — can reuse the exact same apply path instead of re-implementing it.</summary>
    internal static async Task<int> RunAsync(
        string connection, string server, string database, string user, string password,
        DirectoryInfo migrations, bool createDatabase)
    {
        // Passwordless fallback: when NO connection details are on the command line, resolve
        // ConnectionStrings:Default from the app's Gateway user-secret (the value `asdamir new app`
        // tells you to set) or the ConnectionStrings__Default environment variable. This keeps the SQL
        // password off the terminal — `asdamir db apply --create-database` just works after the secret
        // The migrations directory is checked HERE, before anything reads it. It used to be validated further
        // down, after the connection-resolution walk had already enumerated the tree — so `asdamir db apply`
        // run from the wrong folder threw DirectoryNotFoundException out of that walk and printed a stack
        // trace with absolute paths, instead of the one-line "not found" message that was waiting a few lines
        // below. Being in the wrong directory is the single most likely way to invoke this command wrongly.
        if (!migrations.Exists)
        {
            Console.Error.WriteLine($"Migrations directory not found: {migrations.FullName}");
            Console.Error.WriteLine(
                "Run this from an app root (it defaults to ./db/migrations), or pass --migrations <dir>.");
            return ExitCodes.Usage;
        }

        // is set. Explicit --connection / --server / --user / --password always take precedence.
        var connectionSource = "the command line";
        if (string.IsNullOrWhiteSpace(connection) && string.IsNullOrWhiteSpace(database)
            && string.IsNullOrWhiteSpace(user) && string.IsNullOrWhiteSpace(password))
        {
            var resolved = TryResolveConnectionFromApp(migrations, out var source);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                connection = resolved;
                connectionSource = source;
                Console.WriteLine($"Using ConnectionStrings:Default from {source} (no credentials on the command line).");
            }
        }

        SqlConnectionStringBuilder builder;
        try
        {
            if (!string.IsNullOrWhiteSpace(connection))
            {
                builder = new SqlConnectionStringBuilder(connection);
                if (!string.IsNullOrWhiteSpace(database)) builder.InitialCatalog = database;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(database))
                {
                    Console.Error.WriteLine("Provide --database (with --server) or a full --connection string.");
                    return ExitCodes.Usage;
                }
                builder = new SqlConnectionStringBuilder
                {
                    DataSource = server,
                    InitialCatalog = database,
                    TrustServerCertificate = true,
                };
                if (!string.IsNullOrWhiteSpace(user))
                {
                    // SQL auth — cross-platform (Windows integrated auth is Windows-only).
                    builder.UserID = user;
                    builder.Password = password;
                }
                else
                {
                    builder.IntegratedSecurity = true;
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Invalid connection settings: {ex.Message}");
            return ExitCodes.Usage;
        }

        var dbName = builder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(dbName))
        {
            Console.Error.WriteLine("The connection string has no Initial Catalog / Database. Pass --database.");
            return ExitCodes.Usage;
        }

        if (!migrations.Exists)
        {
            Console.Error.WriteLine($"Migrations directory not found: {migrations.FullName}");
            return ExitCodes.Usage;
        }

        var files = migrations.GetFiles("*.sql")
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .ToList();
        if (files.Count == 0)
        {
            Console.Error.WriteLine(DescribeEmptyMigrationsDirectory(migrations));
            return ExitCodes.Usage;
        }

        try
        {
            if (createDatabase)
                await EnsureDatabaseAsync(builder, dbName);

            // CLI scaffolding tool; IDbConnectionFactory is a runtime (multi-tenant)
            // abstraction that doesn't exist in this one-shot setup context.
            await using var conn = new SqlConnection(builder.ConnectionString); // audit-lint:ignore AUD002
            await conn.OpenAsync();
            Console.WriteLine($"Connected to '{dbName}' on '{builder.DataSource}'.");

            // Say WHERE the migrations are about to land, and where that answer came from, BEFORE anything is
            // applied. The connection can be resolved implicitly (a Gateway user-secret, an environment
            // variable); an operator who cannot see which one won has no chance to notice that it is the
            // wrong database until the schema is already there.
            Console.WriteLine($"Target: database '{dbName}' on '{builder.DataSource}' — connection from {connectionSource}.");
            Console.WriteLine($"Migrations: {migrations.FullName}");

            // ONE apply at a time per database. Two concurrent runs both read the journal, both see the same
            // migration as un-applied and both execute it — the second either fails half-way or, for a
            // non-idempotent script, applies it twice. A session-owned applock serialises them; it is released
            // when this connection closes, so a crashed run cannot leave it held.
            if (!await TryAcquireApplyLockAsync(conn))
            {
                Console.Error.WriteLine(
                    $"Another `asdamir db apply` is already running against '{dbName}' (waited {ApplyLockTimeoutMs / 1000}s for it). "
                    + "Nothing was applied — re-run once it finishes.");
                return 1;
            }

            await EnsureJournalAsync(conn);
            var applied = await GetAppliedAsync(conn);
            Console.WriteLine($"{applied.Count} migration(s) already applied; {files.Count} on disk.");

            var ran = 0;
            var skipped = 0;
            foreach (var file in files)
            {
                var checksum = Sha256(await File.ReadAllTextAsync(file.FullName));
                if (applied.TryGetValue(file.Name, out var priorChecksum))
                {
                    if (priorChecksum is not null && !string.Equals(priorChecksum, checksum, StringComparison.OrdinalIgnoreCase))
                        Console.WriteLine($"  ~ {file.Name} (already applied, but its content CHANGED since — not re-run; add a NEW migration instead)");
                    skipped++;
                    continue;
                }

                Console.WriteLine($"  + {file.Name}");
                var batches = SplitBatches(await File.ReadAllTextAsync(file.FullName));
                var n = 0;
                foreach (var batch in batches)
                {
                    n++;
                    await using var cmd = conn.CreateCommand();
                    cmd.CommandText = batch;
                    cmd.CommandTimeout = 180;
                    try
                    {
                        await cmd.ExecuteNonQueryAsync();
                    }
                    catch (SqlException ex)
                    {
                        Console.Error.WriteLine($"\nFailed in {file.Name}, batch #{n}: {ex.Message}");

                        // A batch that failed AFTER its own BEGIN TRAN leaves that transaction open on this
                        // session. Roll it back explicitly rather than relying on the connection teardown, so
                        // the state reported below ("not journaled") is the state the database is really in.
                        if (await RollbackOpenTransactionAsync(conn))
                            Console.Error.WriteLine("(the migration left a transaction open — it was rolled back.)");
                        Console.Error.WriteLine("(not journaled — fix the migration and re-run; applied migrations are skipped.)");
                        return 1;
                    }
                }

                // Every batch succeeded — but a script that opens a transaction and never commits it would
                // have the journal row below written INSIDE that transaction, and the whole lot (the
                // migration and its journal row) rolled back when the connection closes, while this run
                // reported it applied. Refuse to journal it instead.
                if (await RollbackOpenTransactionAsync(conn))
                {
                    Console.Error.WriteLine($"\nFailed in {file.Name}: the script finished with a transaction still OPEN (a BEGIN TRAN without a matching COMMIT).");
                    Console.Error.WriteLine("(it was rolled back and NOT journaled — add the missing COMMIT and re-run.)");
                    return 1;
                }

                await RecordAppliedAsync(conn, file.Name, checksum);
                ran++;
            }

            Console.WriteLine($"\nDone. {ran} new migration(s) applied, {skipped} skipped (already applied), in '{dbName}'.");
            return 0;
        }
        catch (SqlException ex)
        {
            Console.Error.WriteLine($"SQL error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Resolves <c>ConnectionStrings:Default</c> without any credential on the command line: first from the
    /// app's Gateway user-secret (the <c>*.Gateway.csproj</c> of the app that OWNS <paramref name="migrations"/>
    /// — see <see cref="FindGatewayCsproj"/> — and its <c>&lt;UserSecretsId&gt;</c>), then from the
    /// <c>ConnectionStrings__Default</c> environment variable. Returns null when neither is set (the caller
    /// then reports the normal "provide --database" error).
    /// </summary>
    /// <remarks>
    /// Exposed (internal) so <c>new entity</c> / <c>rollback app</c> reuse the exact same passwordless
    /// resolution as <c>db apply</c> instead of re-implementing it. Read it BEFORE deleting the app directory
    /// (the csproj holds the secret id).
    /// </remarks>
    /// <param name="migrations">The migrations directory (it need not exist yet).</param>
    /// <param name="source">A human-readable description of where the connection came from, naming the project.</param>
    /// <returns>The connection string, or null when nothing resolves.</returns>
    internal static string? TryResolveConnectionFromApp(DirectoryInfo migrations, out string source)
    {
        source = "";
        var gatewayCsproj = FindGatewayCsproj(migrations);
        if (gatewayCsproj is not null)
        {
            var id = Regex.Match(File.ReadAllText(gatewayCsproj), @"<UserSecretsId>\s*([^<\s]+)\s*</UserSecretsId>").Groups[1].Value;
            if (!string.IsNullOrWhiteSpace(id))
            {
                var conn = ReadUserSecretDefaultConnection(id);
                if (!string.IsNullOrWhiteSpace(conn))
                {
                    source = $"the Gateway user-secret of {gatewayCsproj} (UserSecretsId {id})";
                    return conn;
                }
            }
        }

        var env = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                  ?? Environment.GetEnvironmentVariable("ConnectionStrings:Default");
        if (!string.IsNullOrWhiteSpace(env)) { source = "the ConnectionStrings__Default environment variable"; return env; }
        return null;
    }

    /// <summary>
    /// Finds the <c>*.Gateway.csproj</c> of the app that owns <paramref name="migrations"/> — and ONLY that app.
    /// </summary>
    /// <remarks>
    /// <para><b>The search is bounded by the app root.</b> It walks up from the migrations directory and stops
    /// at the first directory that contains a <c>.sln</c> or a <c>.git</c> entry; it never looks above it. It
    /// used to walk all the way to the file-system root and adopt the first <c>*.Gateway.csproj</c> found under
    /// ANY ancestor's <c>src/</c> — so <c>asdamir db apply</c> run against a folder with no Gateway of its own
    /// (the control-plane migrations, a loose folder of scripts, a sibling app) silently picked up a DIFFERENT
    /// app's connection string and applied the migrations to that app's database.</para>
    /// <para>When no <c>.sln</c>/<c>.git</c> boundary exists above the directory at all, only the directory
    /// itself is examined: with no evidence of where the app ends, guessing upward is exactly the defect.</para>
    /// <para>An app with MORE than one <c>*.Gateway.csproj</c> under <c>src/</c> resolves to nothing — picking
    /// "the first" would be a coin toss between two databases; the caller then asks for explicit flags.</para>
    /// </remarks>
    /// <param name="migrations">The migrations directory (it need not exist).</param>
    /// <returns>The csproj path, or null when the owning app has no unambiguous Gateway project.</returns>
    internal static string? FindGatewayCsproj(DirectoryInfo migrations)
    {
        // Nearest ancestor (inclusive) that marks an app/repository root.
        DirectoryInfo? boundary = null;
        for (var dir = migrations; dir is not null; dir = dir.Parent)
        {
            if (!dir.Exists) continue;
            if (dir.EnumerateFiles("*.sln").Any()
                || Directory.Exists(Path.Combine(dir.FullName, ".git"))
                || File.Exists(Path.Combine(dir.FullName, ".git")))
            {
                boundary = dir;
                break;
            }
        }

        for (var dir = migrations; dir is not null; dir = dir.Parent)
        {
            if (dir.Exists)
            {
                // The Gateway dir itself (entity migrations live under it) …
                var here = dir.GetFiles("*.Gateway.csproj");
                if (here.Length == 1) return here[0].FullName;
                if (here.Length > 1) return null;

                // … or a sibling under this app root's src/ (management migrations live at <app>/db/migrations).
                var srcDir = Path.Combine(dir.FullName, "src");
                if (Directory.Exists(srcDir))
                {
                    var underSrc = Directory.GetFiles(srcDir, "*.Gateway.csproj", SearchOption.AllDirectories);
                    if (underSrc.Length == 1) return underSrc[0];
                    if (underSrc.Length > 1) return null;
                }
            }

            // Never above the app root; and with no root at all, never above the starting directory.
            if (boundary is null || string.Equals(dir.FullName, boundary.FullName, StringComparison.Ordinal)) break;
        }

        return null;
    }

    /// <summary>
    /// The message for a migrations directory that holds no <c>*.sql</c> file directly — it names the
    /// sub-folders that DO hold scripts, so the user knows what to pass.
    /// </summary>
    /// <remarks>
    /// The scan is deliberately NOT recursive: a migrations root may hold one folder per database dialect
    /// (<c>MSSQL/</c>, <c>Oracle/</c>), and recursing would run every dialect's scripts against SQL Server.
    /// A bare "No *.sql migrations" for a directory that visibly contains them one level down sent people
    /// looking for a missing file instead of the missing path segment.
    /// </remarks>
    /// <param name="migrations">The directory that was scanned.</param>
    /// <returns>The error text.</returns>
    internal static string DescribeEmptyMigrationsDirectory(DirectoryInfo migrations)
    {
        var sb = new StringBuilder($"No *.sql migrations directly in {migrations.FullName}.");
        var subs = migrations.GetDirectories().OrderBy(d => d.Name, StringComparer.Ordinal).ToList();
        if (subs.Count == 0) return sb.ToString();

        sb.AppendLine().Append("It has sub-folders (this command does not recurse — a migrations root can hold one folder per database dialect):");
        foreach (var sub in subs)
        {
            var count = sub.GetFiles("*.sql").Length;
            sb.AppendLine().Append($"  {sub.Name}/  — {count} *.sql file(s)");
        }

        var withSql = subs.Where(d => d.GetFiles("*.sql").Length > 0).ToList();
        if (withSql.Count > 0)
        {
            sb.AppendLine().Append(
                $"Pass the folder for THIS database engine (SQL Server), e.g.:  --migrations {Path.Combine(migrations.FullName, withSql[0].Name)}");
        }

        return sb.ToString();
    }

    /// <summary>Reads ConnectionStrings:Default from a project's dotnet user-secrets store (secrets.json).</summary>
    private static string? ReadUserSecretDefaultConnection(string userSecretsId)
    {
        var root = OperatingSystem.IsWindows()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "UserSecrets")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".microsoft", "usersecrets");
        var file = Path.Combine(root, userSecretsId, "secrets.json");
        if (!File.Exists(file)) return null;
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));
            // `dotnet user-secrets set "ConnectionStrings:Default" …` stores the flat colon key; also accept the nested form.
            if (doc.RootElement.TryGetProperty("ConnectionStrings:Default", out var flat)) return flat.GetString();
            if (doc.RootElement.TryGetProperty("ConnectionStrings", out var cs)
                && cs.ValueKind == JsonValueKind.Object
                && cs.TryGetProperty("Default", out var nested)) return nested.GetString();
        }
        catch (JsonException) { /* malformed secrets.json — fall through to env/error */ }
        return null;
    }

    private static async Task EnsureDatabaseAsync(SqlConnectionStringBuilder target, string dbName)
    {
        var master = new SqlConnectionStringBuilder(target.ConnectionString) { InitialCatalog = "master" };
        // Bootstrap connects to `master` to CREATE DATABASE, before any tenant
        // DB or IDbConnectionFactory exists.
        await using var conn = new SqlConnection(master.ConnectionString); // audit-lint:ignore AUD002
        await conn.OpenAsync();

        await using var cmd = conn.CreateCommand();
        // QUOTENAME escapes the identifier, so the database name can't break out into injection.
        cmd.CommandText =
            "IF DB_ID(@db) IS NULL BEGIN " +
            "DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@db); " +
            "EXEC sp_executesql @sql; END";
        cmd.Parameters.AddWithValue("@db", dbName);
        await cmd.ExecuteNonQueryAsync();
        Console.WriteLine($"Database '{dbName}' ensured (created if it was missing).");
    }

    /// <summary>Creates the migration journal table if it doesn't exist.</summary>
    private static async Task EnsureJournalAsync(SqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            $"IF OBJECT_ID(N'{JournalTable}', N'U') IS NULL " +
            $"CREATE TABLE {JournalTable} (" +
            "  Id NVARCHAR(260) NOT NULL CONSTRAINT PK___SchemaMigrations PRIMARY KEY, " +
            "  Checksum NVARCHAR(64) NULL, " +
            "  AppliedAtUtc DATETIME2(3) NOT NULL CONSTRAINT DF___SchemaMigrations_AppliedAt DEFAULT SYSUTCDATETIME());";
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Returns applied migration ids → stored checksum.</summary>
    private static async Task<Dictionary<string, string?>> GetAppliedAsync(SqlConnection conn)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"SELECT Id, Checksum FROM {JournalTable};";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result[reader.GetString(0)] = reader.IsDBNull(1) ? null : reader.GetString(1);
        return result;
    }

    private static async Task RecordAppliedAsync(SqlConnection conn, string id, string checksum)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"INSERT INTO {JournalTable} (Id, Checksum) VALUES (@id, @sum);";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@sum", checksum);
        await cmd.ExecuteNonQueryAsync();
    }

    private static string Sha256(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n"))));

    private const string ApplyLockResource = "asdamir:db-apply";

    /// <summary>How long a second <c>db apply</c> waits for the first before giving up.</summary>
    private const int ApplyLockTimeoutMs = 60_000;

    /// <summary>Takes the session-owned exclusive applock that serialises applies against one database.</summary>
    private static async Task<bool> TryAcquireApplyLockAsync(SqlConnection conn)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandTimeout = (ApplyLockTimeoutMs / 1000) + 30;
        cmd.CommandText =
            "DECLARE @r INT; EXEC @r = sp_getapplock @Resource = @res, @LockMode = N'Exclusive', "
            + "@LockOwner = N'Session', @LockTimeout = @timeout; SELECT @r;";
        cmd.Parameters.AddWithValue("@res", ApplyLockResource);
        cmd.Parameters.AddWithValue("@timeout", ApplyLockTimeoutMs);

        // sp_getapplock: 0 = granted, 1 = granted after waiting; negative = timeout / cancelled / deadlock / error.
        return await cmd.ExecuteScalarAsync() is int result && result >= 0;
    }

    /// <summary>Rolls back any transaction the session still holds; true when there was one.</summary>
    private static async Task<bool> RollbackOpenTransactionAsync(SqlConnection conn)
    {
        try
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "IF @@TRANCOUNT > 0 BEGIN ROLLBACK TRANSACTION; SELECT 1; END ELSE SELECT 0;";
            return await cmd.ExecuteScalarAsync() is int had && had == 1;
        }
        catch (SqlException)
        {
            // The failure that brought us here may have doomed the session (severity >= 20 closes it). The
            // server has already rolled back in that case; there is nothing left to do and nothing to report.
            return false;
        }
    }

    /// <summary>
    /// Splits a script into batches on <c>GO</c> separator lines, the way <c>sqlcmd</c>/SSMS do (SqlClient does
    /// not understand <c>GO</c>).
    /// </summary>
    /// <remarks>
    /// <para>A separator is a line that — outside any comment or string — consists of <c>GO</c>, an optional
    /// repeat count and an optional trailing <c>--</c> comment. Three shapes the previous line-regex got wrong:</para>
    /// <list type="bullet">
    /// <item><description><c>GO 5</c> — was not recognised at all, so the two batches around it were sent as
    /// one and the literal text <c>GO 5</c> reached the server as a syntax error. The batch before it is now
    /// emitted five times, as <c>sqlcmd</c> does.</description></item>
    /// <item><description>a <c>GO</c> line inside a <c>/* … */</c> block comment — split the comment in half,
    /// leaving an unterminated comment in one batch and a stray <c>*/</c> in the next.</description></item>
    /// <item><description>a <c>GO</c> line inside a multi-line <c>'…'</c> string literal (a procedure body
    /// built as dynamic SQL, a seeded help text) — split the literal and corrupted both batches.</description></item>
    /// </list>
    /// <para>Internal so other commands (e.g. <c>new feature</c> applying AsdamirVault seeds) reuse the same split.</para>
    /// </remarks>
    /// <param name="script">The full script text.</param>
    /// <returns>The non-empty batches, in order, with repeat counts expanded.</returns>
    internal static List<string> SplitBatches(string script)
    {
        var batches = new List<string>();
        var sb = new StringBuilder();

        // Lexical state carried ACROSS lines — the whole point: a line is a separator only when it starts
        // outside every comment and literal.
        var blockDepth = 0;      // T-SQL block comments nest
        var inString = false;    // '…' ('' is an escaped quote)
        var inBracket = false;   // […] (]] is an escaped bracket)
        var inQuoted = false;    // "…" quoted identifier

        foreach (var line in script.Replace("\r\n", "\n").Split('\n'))
        {
            if (blockDepth == 0 && !inString && !inBracket && !inQuoted)
            {
                var m = GoLine.Match(line);
                if (m.Success)
                {
                    var batch = sb.ToString().Trim();
                    sb.Clear();
                    if (batch.Length > 0)
                    {
                        var repeat = m.Groups[1].Success ? Math.Max(1, int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)) : 1;
                        for (var r = 0; r < repeat; r++) batches.Add(batch);
                    }

                    continue;
                }
            }

            sb.Append(line).Append('\n');

            for (var i = 0; i < line.Length; i++)
            {
                var c = line[i];
                var next = i + 1 < line.Length ? line[i + 1] : '\0';

                if (blockDepth > 0)
                {
                    if (c == '/' && next == '*') { blockDepth++; i++; }
                    else if (c == '*' && next == '/') { blockDepth--; i++; }
                }
                else if (inString)
                {
                    if (c == '\'' && next == '\'') i++;
                    else if (c == '\'') inString = false;
                }
                else if (inBracket)
                {
                    if (c == ']' && next == ']') i++;
                    else if (c == ']') inBracket = false;
                }
                else if (inQuoted)
                {
                    if (c == '"' && next == '"') i++;
                    else if (c == '"') inQuoted = false;
                }
                else if (c == '-' && next == '-') break;               // line comment — rest of the line is inert
                else if (c == '/' && next == '*') { blockDepth = 1; i++; }
                else if (c == '\'') inString = true;
                else if (c == '[') inBracket = true;
                else if (c == '"') inQuoted = true;
            }
        }

        var tail = sb.ToString().Trim();
        if (tail.Length > 0) batches.Add(tail);
        return batches;
    }

    // `GO`, an optional repeat count (sqlcmd: `GO 5`), an optional trailing line comment. Nothing else on the line.
    private static readonly Regex GoLine = new(
        @"^\s*GO(?:\s+(\d{1,6}))?\s*(?:--.*)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
