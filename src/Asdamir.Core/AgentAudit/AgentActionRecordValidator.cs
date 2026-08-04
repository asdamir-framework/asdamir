// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.AgentAudit;

/// <summary>
/// A reason a record can never be written to the ledger, however many times it is retried.
/// </summary>
/// <param name="Field">The offending field.</param>
/// <param name="Reason">A stable, machine-readable code (see <see cref="AgentActionRecordValidator"/>).</param>
/// <param name="Message">An operator-readable explanation.</param>
internal sealed record AgentActionViolation(string Field, string Reason, string Message);

/// <summary>
/// Validates a record BEFORE it reaches <c>dbo.AgentActionLedger_Append</c>. Everything it rejects is a
/// PERMANENT rejection: the same bytes will fail identically forever, so the client must dead-letter rather
/// than retry.
///
/// <para><b>Why the length checks live here and not only in the database — this is load-bearing, do not
/// "simplify" it away.</b> The procedure's parameters are declared <c>NVARCHAR(n)</c>, and SQL Server
/// SILENTLY TRUNCATES a longer argument at the parameter boundary. By the time the procedure's own
/// <c>LEN(@targetId) &gt; 128</c> guard runs, the value already fits, so that guard and the matching CHECK
/// constraint can NEVER fire for length. The consequence is worse than a rejected row: the canonical prefix is
/// built from the FULL value while the column stores the TRUNCATED one, so the in-database verification still
/// reports Valid while the canonical verifier — the authority — reports a divergence. An honest write would
/// become a permanent, uncorrectable accusation of tampering on an append-only table. Rejecting it up front is
/// the only place this can be fixed. A real-SQL regression test pins the truncation behaviour
/// (<c>OverLongFields_AreSilentlyTruncatedByTheProcParameters_WhichPoisonsCanonicalVerification</c>).</para>
///
/// <para>The same reasoning applies to the digests: <c>BINARY(32)</c> zero-PADS a shorter value, which would
/// again make the stored column disagree with the hashed prefix.</para>
/// </summary>
internal static class AgentActionRecordValidator
{
    // Reason codes. Stable strings — they travel to the client in the ingest response and drive dead-lettering.
    internal const string ReasonRequired = "field_required";
    internal const string ReasonTooLong = "field_too_long";
    internal const string ReasonCharset = "field_charset";
    internal const string ReasonDigestLength = "digest_length";

    /// <summary>Column limits, mirroring dbo.AgentActionLedger exactly.</summary>
    private const int MaxTenantId = 64, MaxAgentId = 200, MaxAgentVersion = 100, MaxModelId = 200,
        MaxUserName = 256, MaxSessionId = 200, MaxInvocationId = 200, MaxCorrelationId = 64,
        MaxActionType = 200, MaxTargetType = 128, MaxTargetId = 128, MaxErrorCode = 64;

    /// <summary>
    /// Returns the first violation, or null when the record can be written.
    /// </summary>
    /// <param name="record">The record to check.</param>
    /// <returns>The violation, or null when the record is acceptable.</returns>
    internal static AgentActionViolation? Validate(AgentActionRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (string.IsNullOrEmpty(record.TenantId))
            return new(nameof(record.TenantId), ReasonRequired, "TenantId is required (use 'default' for a single-tenant application).");
        if (string.IsNullOrEmpty(record.AgentId))
            return new(nameof(record.AgentId), ReasonRequired, "AgentId is required.");
        if (string.IsNullOrEmpty(record.ActionType))
            return new(nameof(record.ActionType), ReasonRequired, "ActionType is required.");

        var tooLong =
            TooLong(record.TenantId, MaxTenantId, nameof(record.TenantId)) ??
            TooLong(record.AgentId, MaxAgentId, nameof(record.AgentId)) ??
            TooLong(record.AgentVersion, MaxAgentVersion, nameof(record.AgentVersion)) ??
            TooLong(record.ModelId, MaxModelId, nameof(record.ModelId)) ??
            TooLong(record.OnBehalfOfUserName, MaxUserName, nameof(record.OnBehalfOfUserName)) ??
            TooLong(record.SessionId, MaxSessionId, nameof(record.SessionId)) ??
            TooLong(record.InvocationId, MaxInvocationId, nameof(record.InvocationId)) ??
            TooLong(record.CorrelationId, MaxCorrelationId, nameof(record.CorrelationId)) ??
            TooLong(record.ActionType, MaxActionType, nameof(record.ActionType)) ??
            TooLong(record.TargetType, MaxTargetType, nameof(record.TargetType)) ??
            TooLong(record.TargetId, MaxTargetId, nameof(record.TargetId)) ??
            TooLong(record.ErrorCode, MaxErrorCode, nameof(record.ErrorCode));
        if (tooLong is not null) return tooLong;

        if (record.TargetId is not null && !IsOpaqueIdentifier(record.TargetId))
            return new(nameof(record.TargetId), ReasonCharset,
                "TargetId must be an opaque identifier: only [0-9 A-Z a-z . _ : / -] are permitted. It is inside the hash and can never be redacted, so free text is refused.");

        if (record.ErrorCode is not null && !IsStructuralCode(record.ErrorCode))
            return new(nameof(record.ErrorCode), ReasonCharset,
                "ErrorCode must be a structural code: only [0-9 A-Z a-z . _ -] are permitted. Put the readable text in ErrorMessage and its SHA-256 in ErrorDigest.");

        return WrongDigestLength(record.InputDigest, nameof(record.InputDigest))
            ?? WrongDigestLength(record.OutputDigest, nameof(record.OutputDigest))
            ?? WrongDigestLength(record.ErrorDigest, nameof(record.ErrorDigest));
    }

    private static AgentActionViolation? TooLong(string? value, int max, string field) =>
        value is not null && value.Length > max
            ? new(field, ReasonTooLong,
                $"{field} exceeds its maximum of {max} characters ({value.Length}). It cannot be truncated silently: the hash is computed over the full value while the column would store the truncated one, which would permanently break canonical verification for this row.")
            : null;

    private static AgentActionViolation? WrongDigestLength(byte[]? value, string field) =>
        value is not null && value.Length != 32
            ? new(field, ReasonDigestLength,
                $"{field} must be exactly 32 bytes (SHA-256); {value.Length} were supplied. A shorter value would be zero-padded by the BINARY(32) column and would no longer match the hashed prefix.")
            : null;

    /// <summary>ASCII-only by construction — the database enforces the same set under a BINARY collation.</summary>
    private static bool IsOpaqueIdentifier(string value)
    {
        foreach (var c in value)
        {
            var ok = c is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z'
                  or '.' or '_' or ':' or '/' or '-';
            if (!ok) return false;
        }
        return true;
    }

    private static bool IsStructuralCode(string value)
    {
        foreach (var c in value)
        {
            var ok = c is >= '0' and <= '9' or >= 'A' and <= 'Z' or >= 'a' and <= 'z'
                  or '.' or '_' or '-';
            if (!ok) return false;
        }
        return true;
    }
}
