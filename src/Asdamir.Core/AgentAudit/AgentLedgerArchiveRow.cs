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
/// One line of an archive's <c>segment.ndjson</c> — a FLAT JSON object carrying everything needed to rebuild the
/// row's canonical body from scratch, plus the two chain hashes and the stored prefix.
///
/// <para><b>Flat, not nested, on purpose.</b> The archive is read by verifiers we will never see, in languages
/// we do not control. Every additional level of nesting is another place for two implementations to disagree
/// about where a field lives, so the line carries the hashed fields exactly in the order the canonical body
/// consumes them.</para>
///
/// <para><b>Which members are inside the hash.</b> <see cref="HashVersion"/> through <see cref="ErrorDigest"/>
/// are canonical fields 1-23, in that order; <see cref="SeqNo"/> and <see cref="RecordedAtTicks"/> are appended
/// by the server inside the chain lock. The last four —
/// <see cref="OnBehalfOfUserName"/>, <see cref="ErrorMessage"/>, <see cref="SignatureAlgo"/> and
/// <see cref="Signature"/> — are <b>outside</b> the hash: they are carried because a reader wants to see them,
/// but editing one breaks nothing and a verifier must never treat them as evidence.</para>
///
/// <para><b><see cref="CanonicalPrefix"/> is stored but NEVER trusted.</b> A conforming verifier rebuilds the
/// prefix from this row's own columns, compares the rebuilt bytes to the stored ones, and hashes the
/// <i>rebuilt</i> bytes. Re-hashing the stored prefix would be circular — it is precisely why the ledger's
/// in-database verification is partial by construction, and this format must not import that weakness.</para>
///
/// <para>Every binary value is <b>lowercase, unseparated hex with no <c>0x</c> prefix</b>; a 32-byte hash is 64
/// characters. Hex rather than base64 throughout, matching the ledger's API surface, the canonicalization
/// specification and its published golden vectors — one convention to get wrong instead of two.</para>
/// </summary>
public sealed record AgentLedgerArchiveRow
{
    /// <summary>Position in the chain: 1-based, gapless within the scope. Hashed (appended by the server, big-endian).</summary>
    public required long SeqNo { get; init; }

    /// <summary>
    /// The authoritative receipt time as 100-nanosecond ticks since <c>1970-01-01T00:00:00Z</c> — a documented
    /// integer, never a cast of a database timestamp type. Hashed (appended by the server, big-endian).
    /// </summary>
    public required long RecordedAtTicks { get; init; }

    /// <summary>The canonicalization version this row was hashed under. Canonical field 1.</summary>
    public required int HashVersion { get; init; }

    /// <summary>The chain's application. Canonical field 2 (16 bytes, RFC 4122 big-endian).</summary>
    public required Guid AppId { get; init; }

    /// <summary>The chain's tenant. Canonical field 3.</summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// 0 = Event, 1 = Seal, 2 = FoldTombstone. Canonical field 4.
    /// <para>A v1 segment MUST NOT contain a tombstone: the live ledger refuses to re-fold a range that already
    /// holds one, so an archive that carries one was not produced by the fold flow. A verifier must reject it
    /// with a distinct format error naming the offending <see cref="SeqNo"/>.</para>
    /// </summary>
    public required int RecordKind { get; init; }

    /// <summary>The client-generated idempotency key. Canonical field 5 (16 bytes, RFC 4122 big-endian).</summary>
    public required Guid EventId { get; init; }

    /// <summary>
    /// The client's clock reading, as the ISO-8601 round-trip text with exactly 7 fractional digits and a
    /// <c>Z</c> suffix. Canonical field 6, hashed as that exact UTF-8 text — carried as a string precisely so no
    /// verifier has to re-derive the formatting.
    /// </summary>
    public required string OccurredAtUtc { get; init; }

    /// <summary>The acting agent's logical identity. Canonical field 7.</summary>
    public required string AgentId { get; init; }

    /// <summary>The agent's version. Canonical field 8; null and empty hash differently.</summary>
    public string? AgentVersion { get; init; }

    /// <summary>The model behind the action. Canonical field 9.</summary>
    public string? ModelId { get; init; }

    /// <summary>0 = Delegated, 1 = Autonomous, 2 = Scheduled. Canonical field 10.</summary>
    public required int AuthorityKind { get; init; }

    /// <summary>The delegating user's id. Canonical field 11 (nullable 32-bit, big-endian two's complement).</summary>
    public int? OnBehalfOfUserId { get; init; }

    /// <summary>The agent session this action belongs to. Canonical field 12.</summary>
    public string? SessionId { get; init; }

    /// <summary>The single invocation this action belongs to. Canonical field 13.</summary>
    public string? InvocationId { get; init; }

    /// <summary>The request correlation id. Canonical field 14.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>What the agent did, as <c>&lt;area&gt;.&lt;action&gt;</c>. Canonical field 15.</summary>
    public required string ActionType { get; init; }

    /// <summary>The type of entity acted upon. Canonical field 16.</summary>
    public string? TargetType { get; init; }

    /// <summary>The entity acted upon — an opaque or surrogate identifier only. Canonical field 17.</summary>
    public string? TargetId { get; init; }

    /// <summary>0 = Allowed, 1 = Denied, 2 = RequiresApproval. Canonical field 18.</summary>
    public required int Decision { get; init; }

    /// <summary>0 = Success, 1 = Failure, 2 = Partial. Canonical field 19.</summary>
    public required int Outcome { get; init; }

    /// <summary>Lowercase hex SHA-256 of the input. Canonical field 20.</summary>
    public string? InputDigest { get; init; }

    /// <summary>Lowercase hex SHA-256 of the output. Canonical field 21.</summary>
    public string? OutputDigest { get; init; }

    /// <summary>The machine-readable error code. Canonical field 22.</summary>
    public string? ErrorCode { get; init; }

    /// <summary>Lowercase hex SHA-256 of the full error message. Canonical field 23.</summary>
    public string? ErrorDigest { get; init; }

    /// <summary>Lowercase hex of the previous row's hash — the chain link. 32 zero bytes for a chain's first row.</summary>
    public required string PrevHash { get; init; }

    /// <summary>Lowercase hex of this row's hash.</summary>
    public required string RowHash { get; init; }

    /// <summary>
    /// Lowercase hex of the canonical prefix that was hashed when the row was written — <b>stored, never
    /// trusted</b> (see the type remarks). Null only for a fold tombstone, which carries no prefix at all; such a
    /// row must not appear in a conforming v1 segment.
    /// </summary>
    public string? CanonicalPrefix { get; init; }

    /// <summary>The delegating user's display name. <b>Outside the hash</b> — redactable, and not evidence.</summary>
    public string? OnBehalfOfUserName { get; init; }

    /// <summary>The readable error text. <b>Outside the hash</b> — redactable, bound to the chain only by <see cref="ErrorDigest"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>RESERVED for a future version. Always null in v1, and outside the hash.</summary>
    public string? SignatureAlgo { get; init; }

    /// <summary>RESERVED for a future version. Always null in v1, and outside the hash.</summary>
    public string? Signature { get; init; }
}
