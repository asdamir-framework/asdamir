// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later

using System.CommandLine;
using System.Globalization;
using Asdamir.Core.AgentAudit;
using Microsoft.Data.SqlClient;

namespace Asdamir.Tools.Commands;

/// <summary>
/// <c>asdamir audit verify-signatures</c> — checks agent signatures in a live ledger.
/// </summary>
/// <remarks>
/// <para><b>Why this is not part of <c>verify-archive</c>.</b> Archive Format v1 is frozen and its published
/// specification says a conforming verifier <b>MUST NOT</b> treat the signature fields as evidence. Making the
/// archive verifier check them would therefore be a format change by the format's own rules, and Faz 3a
/// deliberately does not produce a v2. So the split is: <c>verify-archive</c> answers "is this archive
/// internally intact", and this command answers "who signed these records" — the second needs the key
/// registry, which lives in the database and is not in the ZIP.</para>
///
/// <para><b>Signature verification requires ledger access. That is a scope statement, not a shortfall.</b> A
/// third party holding only an archive can still verify the chain end to end; identifying the signer is a
/// separate question that needs the registry, exactly as verifying a certificate needs its issuer.</para>
///
/// <para><b>Exit codes stay inside the existing band.</b> Invalid signatures are FINDINGS (1), the same as any
/// other gate in this CLI. A new verdict number would collide with <c>verify-archive</c>'s 0–4, where the
/// numbers already mean something else.</para>
/// </remarks>
public static class VerifySignaturesCommand
{
    /// <summary>Builds the command.</summary>
    /// <returns>The configured <c>verify-signatures</c> command.</returns>
    public static Command Build()
    {
        var connection = new Option<string>("--connection", "AsdamirVault connection string.") { IsRequired = true };
        var appCode = new Option<string>("--app-code", "Restrict to one registered application code.");
        var tenant = new Option<string>("--tenant", "Restrict to one tenant.");

        var cmd = new Command(
            "verify-signatures",
            "Verify agent signatures in the live ledger against the registered public keys.");
        cmd.AddOption(connection);
        cmd.AddOption(appCode);
        cmd.AddOption(tenant);

        cmd.SetHandler(
            (string cs, string? code, string? tn) => Run(cs, code, tn),
            connection, appCode, tenant);

        return cmd;
    }

    private static int Run(string connectionString, string? appCode, string? tenantId)
    {
        var verifier = new EcdsaP256AgentActionSignatureVerifier();
        int unsigned = 0, valid = 0, invalid = 0, unavailable = 0, silentlyUnsigned = 0;
        var offenders = new List<string>();
        var silent = new List<string>();

        // CLI setup context; IDbConnectionFactory is a runtime multi-tenant abstraction that does not exist
        // here — the same reason db-apply and localization-verify carry this suppression.
        using var conn = new SqlConnection(connectionString); // audit-lint:ignore AUD002
        conn.Open();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT l.AppId, l.TenantId, l.SeqNo, l.AgentId, l.OccurredAtUtc, l.HashVersion,
                   l.RecordKind, l.EventId, l.AgentVersion, l.ModelId, l.AuthorityKind,
                   l.OnBehalfOfUserId, l.SessionId, l.InvocationId, l.CorrelationId, l.ActionType,
                   l.TargetType, l.TargetId, l.Decision, l.Outcome, l.InputDigest, l.OutputDigest,
                   l.ErrorCode, l.ErrorDigest,
                   l.SignatureAlgo, l.Signature, l.SigningKeyId,
                   k.Algo AS KeyAlgo, k.PublicKey, k.ValidFromUtc, k.ValidToUtc, k.RevokedAtUtc,
                   CAST(CASE WHEN EXISTS (
                        SELECT 1 FROM dbo.AgentSigningKeys u
                         WHERE u.AppId = l.AppId AND u.AgentId = l.AgentId
                           AND l.OccurredAtUtc >= u.ValidFromUtc
                           AND (u.ValidToUtc   IS NULL OR l.OccurredAtUtc <  u.ValidToUtc)
                           AND (u.RevokedAtUtc IS NULL OR l.OccurredAtUtc <  u.RevokedAtUtc))
                        THEN 1 ELSE 0 END AS BIT) AS HadUsableKey
              FROM dbo.AgentActionLedger l
              LEFT JOIN dbo.Apps a ON a.AppId = l.AppId
              LEFT JOIN dbo.AgentSigningKeys k
                     ON k.AppId = l.AppId AND k.AgentId = l.AgentId AND k.KeyId = l.SigningKeyId
             WHERE l.RecordKind <> 2
               AND (@appCode IS NULL OR a.Code = @appCode)
               AND (@tenantId IS NULL OR l.TenantId = @tenantId)
             ORDER BY l.AppId, l.TenantId, l.SeqNo;
            """;
        cmd.Parameters.AddWithValue("@appCode", (object?)appCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@tenantId", (object?)tenantId ?? DBNull.Value);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var r = reader;
            var state = Evaluate(reader, verifier);
            switch (state)
            {
                case AgentSignatureState.Valid: valid++; break;
                case AgentSignatureState.Invalid:
                    invalid++;
                    offenders.Add(string.Create(
                        CultureInfo.InvariantCulture,
                        $"  {reader.GetString(reader.GetOrdinal("TenantId"))} #{reader.GetInt64(reader.GetOrdinal("SeqNo"))} — signature does NOT verify"));
                    break;
                case AgentSignatureState.KeyUnavailable: unavailable++; break;
                default:
                    unsigned++;

                    // The Faz 3a finding the chain structurally cannot produce. Signature columns are OUTSIDE
                    // the canonical body, so REMOVING a signature breaks no hash and the chain still reports
                    // Valid. The only way to notice is to ask a different question: did this agent hold a
                    // usable key at the moment it acted? If it did and the record is unsigned, either the
                    // signature was stripped or the agent was misconfigured — both are worth seeing, and
                    // neither is visible anywhere else.
                    //
                    // It is a FINDING, not a refusal. Rejecting unsigned records would be a policy, and policy
                    // is deliberately out of scope: this reports, it does not enforce.
                    if (!r.IsDBNull(r.GetOrdinal("HadUsableKey")) && r.GetBoolean(r.GetOrdinal("HadUsableKey")))
                    {
                        silentlyUnsigned++;
                        silent.Add(string.Create(
                            CultureInfo.InvariantCulture,
                            $"  {r.GetString(r.GetOrdinal("TenantId"))} #{r.GetInt64(r.GetOrdinal("SeqNo"))} — unsigned, but {r.GetString(r.GetOrdinal("AgentId"))} held a usable key"));
                    }

                    break;
            }
        }

        var total = unsigned + valid + invalid + unavailable;
        Console.WriteLine($"verify-signatures: {total} record(s) — {valid} valid, {unsigned} unsigned, {unavailable} not checked (key unavailable), {invalid} INVALID.");

        // The three non-valid states are never collapsed. "Unsigned" claims nothing, "not checked" means the
        // check did not happen, and "invalid" is a claim that failed — reporting them as one number would
        // hide the only one that is an alarm.
        if (unavailable > 0)
            Console.WriteLine($"  ⚠️  {unavailable} signed record(s) could NOT be checked — their key is unknown, expired or revoked. A skip is not a pass.");

        if (silentlyUnsigned > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"⚠️  {silentlyUnsigned} record(s) are UNSIGNED although the agent held a usable key at the time.");
            Console.WriteLine("    The chain cannot see this: signatures are outside the canonical body, so a stripped");
            Console.WriteLine("    signature leaves every hash intact. Either the signature was removed, or the agent");
            Console.WriteLine("    was not configured to sign. Reported, not refused — enforcing would be a policy.");
            foreach (var line in silent.Take(20)) Console.WriteLine(line);
            if (silent.Count > 20) Console.WriteLine($"    … and {silent.Count - 20} more.");
        }

        if (invalid == 0) return ExitCodes.Success;

        Console.WriteLine();
        Console.WriteLine("INVALID signatures (worse than unsigned — an unsigned record claims nothing, a broken signature is a claim that failed):");
        foreach (var line in offenders.Take(50)) Console.WriteLine(line);
        if (offenders.Count > 50) Console.WriteLine($"  … and {offenders.Count - 50} more.");

        return ExitCodes.Findings;
    }

    private static AgentSignatureState Evaluate(SqlDataReader r, IAgentActionSignatureVerifier verifier)
    {
        var algo = Str(r, "SignatureAlgo");
        var sig = Bytes(r, "Signature");
        if (algo is null || sig is null || sig.Length == 0) return AgentSignatureState.Unsigned;

        var keyId = Str(r, "SigningKeyId");
        var publicKey = Bytes(r, "PublicKey");
        if (keyId is null || publicKey is null) return AgentSignatureState.KeyUnavailable;

        var key = new AgentSigningKey(
            keyId, r.GetString(r.GetOrdinal("AgentId")), Str(r, "KeyAlgo") ?? string.Empty, publicKey,
            r.GetDateTime(r.GetOrdinal("ValidFromUtc")), Date(r, "ValidToUtc"), Date(r, "RevokedAtUtc"));

        var record = new AgentActionRecord
        {
            EventId = r.GetGuid(r.GetOrdinal("EventId")),
            TenantId = r.GetString(r.GetOrdinal("TenantId")),
            OccurredAtUtc = r.GetDateTime(r.GetOrdinal("OccurredAtUtc")),
            AgentId = r.GetString(r.GetOrdinal("AgentId")),
            AgentVersion = Str(r, "AgentVersion"),
            ModelId = Str(r, "ModelId"),
            AuthorityKind = (AgentAuthorityKind)r.GetByte(r.GetOrdinal("AuthorityKind")),
            OnBehalfOfUserId = r.IsDBNull(r.GetOrdinal("OnBehalfOfUserId")) ? null : r.GetInt32(r.GetOrdinal("OnBehalfOfUserId")),
            SessionId = Str(r, "SessionId"),
            InvocationId = Str(r, "InvocationId"),
            CorrelationId = Str(r, "CorrelationId"),
            ActionType = r.GetString(r.GetOrdinal("ActionType")),
            TargetType = Str(r, "TargetType"),
            TargetId = Str(r, "TargetId"),
            Decision = (AgentActionDecision)r.GetByte(r.GetOrdinal("Decision")),
            Outcome = (AgentActionOutcome)r.GetByte(r.GetOrdinal("Outcome")),
            InputDigest = Bytes(r, "InputDigest"),
            OutputDigest = Bytes(r, "OutputDigest"),
            ErrorCode = Str(r, "ErrorCode"),
            ErrorDigest = Bytes(r, "ErrorDigest"),
        };

        // The ZERO AppId is the signing contract, not a placeholder: canonical field 2 is resolved from the
        // ingest token, so an agent cannot know it and does not sign it. Both sides zero it or nothing verifies.
        var body = AgentActionCanonicalizer.BuildPrefix(
            Guid.Empty, record, r.GetByte(r.GetOrdinal("RecordKind")), r.GetByte(r.GetOrdinal("HashVersion")));

        return verifier.Verify(
            body, new AgentActionSignature(algo, keyId, sig), key, record.OccurredAtUtc);
    }

    private static string? Str(SqlDataReader r, string c)
        => r.IsDBNull(r.GetOrdinal(c)) ? null : r.GetString(r.GetOrdinal(c));

    private static byte[]? Bytes(SqlDataReader r, string c)
        => r.IsDBNull(r.GetOrdinal(c)) ? null : (byte[])r[c];

    private static DateTime? Date(SqlDataReader r, string c)
        => r.IsDBNull(r.GetOrdinal(c)) ? null : r.GetDateTime(r.GetOrdinal(c));
}
