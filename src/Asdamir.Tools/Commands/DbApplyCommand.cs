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

    private static async Task<int> RunAsync(
        string connection, string server, string database, string user, string password,
        DirectoryInfo migrations, bool createDatabase)
    {
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
                    return 2;
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
            return 2;
        }

        var dbName = builder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(dbName))
        {
            Console.Error.WriteLine("The connection string has no Initial Catalog / Database. Pass --database.");
            return 2;
        }

        if (!migrations.Exists)
        {
            Console.Error.WriteLine($"Migrations directory not found: {migrations.FullName}");
            return 2;
        }

        var files = migrations.GetFiles("*.sql")
            .OrderBy(f => f.Name, StringComparer.Ordinal)
            .ToList();
        if (files.Count == 0)
        {
            Console.Error.WriteLine($"No *.sql migrations in {migrations.FullName}.");
            return 2;
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
                        Console.Error.WriteLine("(not journaled — fix the migration and re-run; applied migrations are skipped.)");
                        return 1;
                    }
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

    /// <summary>Splits a script on lines that are exactly <c>GO</c> (the sqlcmd/SSMS batch separator).</summary>
    private static List<string> SplitBatches(string script)
    {
        var batches = new List<string>();
        var sb = new StringBuilder();
        foreach (var line in script.Replace("\r\n", "\n").Split('\n'))
        {
            if (Regex.IsMatch(line, @"^\s*GO\s*$", RegexOptions.IgnoreCase))
            {
                var batch = sb.ToString().Trim();
                if (batch.Length > 0) batches.Add(batch);
                sb.Clear();
            }
            else
            {
                sb.Append(line).Append('\n');
            }
        }
        var tail = sb.ToString().Trim();
        if (tail.Length > 0) batches.Add(tail);
        return batches;
    }
}
