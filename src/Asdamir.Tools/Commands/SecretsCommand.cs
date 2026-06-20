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
using Microsoft.Data.SqlClient;

namespace Asdamir.Tools.Commands;

/// <summary>
/// <c>asdamir secrets …</c> — operator tooling for the at-rest encryption key
/// (<c>Security:EncryptionKey</c>) used by Core's EncryptionService. The crypto is re-implemented locally
/// in <see cref="SecretCrypto"/> (byte-compatible) so this CLI tool doesn't bundle all of Asdamir.Core.
///
/// Rotating that key is destructive: every value encrypted under it (the AsdamirVault
/// <c>Apps.EncryptedClientSecret</c> rows and any <c>AppConfigurations</c> row with
/// <c>IsEncrypted = 1</c>) becomes undecryptable the moment the deployment switches keys,
/// which breaks AppManagement's orchestration (it can't fetch app client secrets).
/// <c>secrets rotate-key</c> performs the re-encryption pass so the rotation is safe.
///
/// Keys are NEVER persisted by this tool. They are read from environment variables by default
/// (so they don't land in shell history); flags override only for interactive use.
/// </summary>
public static class SecretsCommand
{
    // Env var names (preferred over flags so secrets stay out of shell history / process args).
    private const string EnvOldKey = "ASDAMIR_OLD_ENCRYPTION_KEY";
    private const string EnvOldSalt = "ASDAMIR_OLD_ENCRYPTION_SALT";
    private const string EnvNewKey = "ASDAMIR_NEW_ENCRYPTION_KEY";
    private const string EnvNewSalt = "ASDAMIR_NEW_ENCRYPTION_SALT";
    private const string EnvKey = "ASDAMIR_ENCRYPTION_KEY";
    private const string EnvSalt = "ASDAMIR_ENCRYPTION_SALT";

    public static Command Build()
    {
        var cmd = new Command("secrets",
            "At-rest encryption-key tooling (Security:EncryptionKey): re-encrypt stored secrets on key rotation, or produce an encrypted value.");
        cmd.AddCommand(BuildRotateKey());
        cmd.AddCommand(BuildEncrypt());
        return cmd;
    }

    // ───────────────────────────── rotate-key ─────────────────────────────

    private static Command BuildRotateKey()
    {
        var connOpt = new Option<string>(new[] { "--connection", "-c" },
            () => "", "Full ADO.NET connection string to AsdamirVault. Wins over --server/--database/--user/--password.");
        var serverOpt = new Option<string>(new[] { "--server", "-S" }, () => "localhost", "SQL Server instance (when --connection is omitted).");
        var databaseOpt = new Option<string>(new[] { "--database", "-d" }, () => "AsdamirVault", "AsdamirVault database name (when --connection is omitted).");
        var userOpt = new Option<string>(new[] { "--user", "-U" }, () => "", "SQL login (SQL auth). Omit for Windows integrated auth.");
        var passwordOpt = new Option<string>(new[] { "--password", "-P" }, () => "", "Password for --user.");

        var oldKeyOpt = new Option<string>("--old-key", () => "", $"Current encryption key. Default: ${EnvOldKey}.");
        var oldSaltOpt = new Option<string>("--old-salt", () => "", $"Current encryption salt (if Security:EncryptionSalt is set). Default: ${EnvOldSalt}.");
        var newKeyOpt = new Option<string>("--new-key", () => "", $"New encryption key (≥32 chars). Default: ${EnvNewKey}.");
        var newSaltOpt = new Option<string>("--new-salt", () => "", $"New encryption salt (if you use one). Default: ${EnvNewSalt}.");
        var applyOpt = new Option<bool>("--apply", () => false, "Write the re-encrypted values. Without it, runs a DRY-RUN (decrypt + re-encrypt + verify in memory, no writes).");

        var cmd = new Command("rotate-key",
            "Re-encrypt every at-rest secret in AsdamirVault from the OLD key to the NEW key (Apps.EncryptedClientSecret + AppConfigurations where IsEncrypted=1). Dry-run unless --apply.")
        {
            connOpt, serverOpt, databaseOpt, userOpt, passwordOpt,
            oldKeyOpt, oldSaltOpt, newKeyOpt, newSaltOpt, applyOpt,
        };

        cmd.SetHandler(async ctx =>
        {
            var p = ctx.ParseResult;
            ctx.ExitCode = await RotateAsync(
                p.GetValueForOption(connOpt) ?? "", p.GetValueForOption(serverOpt) ?? "localhost",
                p.GetValueForOption(databaseOpt) ?? "AsdamirVault", p.GetValueForOption(userOpt) ?? "",
                p.GetValueForOption(passwordOpt) ?? "",
                Resolve(p.GetValueForOption(oldKeyOpt), EnvOldKey), Resolve(p.GetValueForOption(oldSaltOpt), EnvOldSalt),
                Resolve(p.GetValueForOption(newKeyOpt), EnvNewKey), Resolve(p.GetValueForOption(newSaltOpt), EnvNewSalt),
                p.GetValueForOption(applyOpt));
        });
        return cmd;
    }

    private static async Task<int> RotateAsync(
        string connection, string server, string database, string user, string password,
        string oldKey, string oldSalt, string newKey, string newSalt, bool apply)
    {
        if (string.IsNullOrWhiteSpace(oldKey) || string.IsNullOrWhiteSpace(newKey))
        {
            Console.Error.WriteLine($"Both keys are required. Set --old-key/--new-key or the {EnvOldKey}/{EnvNewKey} env vars.");
            return 2;
        }
        if (oldKey == newKey && oldSalt == newSalt)
        {
            Console.Error.WriteLine("The new key/salt are identical to the old — nothing to rotate.");
            return 2;
        }

        SecretCrypto oldSvc, newSvc;
        try
        {
            oldSvc = MakeService(oldKey, oldSalt);
            newSvc = MakeService(newKey, newSalt);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Invalid key/salt: {ex.Message}");
            return 2;
        }

        string connStr;
        try { connStr = BuildConnString(connection, server, database, user, password); }
        catch (Exception ex) { Console.Error.WriteLine($"Invalid connection settings: {ex.Message}"); return 2; }

        Console.WriteLine(apply
            ? "Rotating encryption key (APPLY — writing re-encrypted values, single transaction)."
            : "Rotating encryption key (DRY-RUN — verifying re-encryption, no writes; pass --apply to commit).");

        try
        {
            // One-shot maintenance tool; IDbConnectionFactory is a runtime multi-tenant abstraction.
            await using var conn = new SqlConnection(connStr); // audit-lint:ignore AUD002
            await conn.OpenAsync();
            await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();

            var apps = await RotateTableAsync(conn, tx, oldSvc, newSvc, apply,
                table: "dbo.Apps", keyCol: "AppId", valueCol: "EncryptedClientSecret", filter: null);
            var cfgs = await RotateTableAsync(conn, tx, oldSvc, newSvc, apply,
                table: "dbo.AppConfigurations", keyCol: "Id", valueCol: "Value", filter: "IsEncrypted = 1");

            if (apply) await tx.CommitAsync(); else await tx.RollbackAsync();

            Console.WriteLine();
            Console.WriteLine($"Apps.EncryptedClientSecret : {apps.Rotated} re-encrypted, {apps.AlreadyNew} already on new key, {apps.Total} total.");
            Console.WriteLine($"AppConfigurations (IsEncrypted=1): {cfgs.Rotated} re-encrypted, {cfgs.AlreadyNew} already on new key, {cfgs.Total} total.");
            Console.WriteLine(apply
                ? "\n✅ Committed. Now deploy the NEW key as Security:EncryptionKey and restart AppManagement."
                : "\n(DRY-RUN ok — re-run with --apply to commit. Back up the database first.)");
            return 0;
        }
        catch (RotationException ex)
        {
            Console.Error.WriteLine($"\n❌ {ex.Message}");
            Console.Error.WriteLine("No changes were committed (transaction rolled back).");
            return 1;
        }
        catch (SqlException ex)
        {
            Console.Error.WriteLine($"SQL error: {ex.Message}");
            return 1;
        }
    }

    private readonly record struct TableResult(int Total, int Rotated, int AlreadyNew);

    /// <summary>Re-encrypts one table's encrypted column. Idempotent: rows already on the new key are skipped.</summary>
    private static async Task<TableResult> RotateTableAsync(
        SqlConnection conn, SqlTransaction tx, SecretCrypto oldSvc, SecretCrypto newSvc,
        bool apply, string table, string keyCol, string valueCol, string? filter)
    {
        // Read the (key, cipher) pairs first, then update — so the read cursor isn't open during writes.
        var rows = new List<(object Key, string Cipher)>();
        var where = filter is null ? "" : $" WHERE {filter}";
        await using (var read = conn.CreateCommand())
        {
            read.Transaction = tx;
            read.CommandText = $"SELECT {keyCol}, {valueCol} FROM {table}{where};";
            await using var r = await read.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                if (r.IsDBNull(1)) continue;
                var v = r.GetString(1);
                if (!string.IsNullOrEmpty(v)) rows.Add((r.GetValue(0), v));
            }
        }

        int rotated = 0, alreadyNew = 0;
        foreach (var (key, cipher) in rows)
        {
            string plain;
            try
            {
                plain = oldSvc.Decrypt(cipher);
            }
            catch
            {
                // Couldn't decrypt with the OLD key. If the NEW key decrypts it, the row was already
                // rotated (e.g. an earlier interrupted run) → skip, keeping the pass idempotent.
                try { newSvc.Decrypt(cipher); alreadyNew++; continue; }
                catch
                {
                    throw new RotationException(
                        $"{table}.{valueCol} for {keyCol}={key} cannot be decrypted with the OLD key (nor the NEW key). " +
                        "Check --old-key/--old-salt — they must match the key the data was encrypted with.");
                }
            }

            var reEncrypted = newSvc.Encrypt(plain);
            // Verify the round-trip under the new key BEFORE writing.
            if (newSvc.Decrypt(reEncrypted) != plain)
                throw new RotationException($"{table}.{valueCol} for {keyCol}={key}: re-encrypted value failed verification under the new key.");

            if (apply)
            {
                await using var upd = conn.CreateCommand();
                upd.Transaction = tx;
                upd.CommandText = $"UPDATE {table} SET {valueCol} = @v WHERE {keyCol} = @k;";
                upd.Parameters.AddWithValue("@v", reEncrypted);
                upd.Parameters.AddWithValue("@k", key);
                await upd.ExecuteNonQueryAsync();
            }
            rotated++;
        }
        return new TableResult(rows.Count, rotated, alreadyNew);
    }

    // ───────────────────────────── encrypt ─────────────────────────────

    private static Command BuildEncrypt()
    {
        var valueOpt = new Option<string>(new[] { "--value", "-v" }, () => "", "Plaintext to encrypt. If omitted, read a single line from stdin.");
        var keyOpt = new Option<string>("--key", () => "", $"Encryption key (≥32 chars). Default: ${EnvKey}.");
        var saltOpt = new Option<string>("--salt", () => "", $"Encryption salt (if you use Security:EncryptionSalt). Default: ${EnvSalt}.");

        var cmd = new Command("encrypt",
            "Encrypt a value with Security:EncryptionKey and print the v2 ciphertext — e.g. to seed an encrypted AppConfigurations value or a Companies connection string.")
        {
            valueOpt, keyOpt, saltOpt,
        };

        cmd.SetHandler(ctx =>
        {
            var p = ctx.ParseResult;
            var value = p.GetValueForOption(valueOpt) ?? "";
            if (string.IsNullOrEmpty(value)) value = Console.In.ReadLine() ?? "";
            var key = Resolve(p.GetValueForOption(keyOpt), EnvKey);
            var salt = Resolve(p.GetValueForOption(saltOpt), EnvSalt);

            if (string.IsNullOrEmpty(value)) { Console.Error.WriteLine("Nothing to encrypt (pass --value or pipe a line on stdin)."); ctx.ExitCode = 2; return; }
            if (string.IsNullOrWhiteSpace(key)) { Console.Error.WriteLine($"No key. Set --key or the {EnvKey} env var."); ctx.ExitCode = 2; return; }

            try
            {
                Console.WriteLine(MakeService(key, salt).Encrypt(value));
                ctx.ExitCode = 0;
            }
            catch (Exception ex) { Console.Error.WriteLine($"Encrypt failed: {ex.Message}"); ctx.ExitCode = 1; }
        });
        return cmd;
    }

    // ───────────────────────────── helpers ─────────────────────────────

    private static string Resolve(string? flag, string envVar)
        => !string.IsNullOrWhiteSpace(flag) ? flag : Environment.GetEnvironmentVariable(envVar) ?? "";

    private static SecretCrypto MakeService(string key, string salt)
        => new(key, string.IsNullOrWhiteSpace(salt) ? null : salt);

    private static string BuildConnString(string connection, string server, string database, string user, string password)
    {
        if (!string.IsNullOrWhiteSpace(connection))
        {
            var b = new SqlConnectionStringBuilder(connection);
            if (!string.IsNullOrWhiteSpace(database)) b.InitialCatalog = database;
            return b.ConnectionString;
        }
        if (string.IsNullOrWhiteSpace(database))
            throw new ArgumentException("Provide --database (with --server) or a full --connection string.");
        var sb = new SqlConnectionStringBuilder { DataSource = server, InitialCatalog = database, TrustServerCertificate = true };
        if (!string.IsNullOrWhiteSpace(user)) { sb.UserID = user; sb.Password = password; }
        else sb.IntegratedSecurity = true;
        return sb.ConnectionString;
    }

    private sealed class RotationException(string message) : Exception(message);
}
