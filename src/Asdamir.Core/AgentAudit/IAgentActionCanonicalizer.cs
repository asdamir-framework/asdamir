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
/// ONE canonicalization version's byte layout — the serialisation the agent action ledger's hash chain is
/// computed over.
///
/// <para><b>Why this is an interface at all, given there is exactly one version today.</b> The
/// canonicalization specification requires a verifier to <b>dispatch on the record's stored
/// <c>HashVersion</c> rather than assume the newest</b>: <c>HashVersion</c> is field 1 and is itself inside the
/// hash, so guessing it wrong changes every byte that follows. That dispatch needs a keyed set of
/// implementations, and <see cref="HashVersion"/> is the key. <see cref="AgentLedgerArchiveVerifier"/> is
/// built from such a set, which is also what lets it tell the two halves of the mixed-version rule apart — a
/// row whose version it <i>implements</i> but that contradicts the manifest is a content failure
/// (<see cref="ArchiveVerificationStatus.Broken"/>), while a row whose version it does <i>not</i> implement is
/// a format error, because it could not rebuild that row at all.</para>
///
/// <para><b>A version is frozen the day it ships.</b> Do not "improve" an existing implementation: every hash
/// ever written under it is a function of these bytes, so a tidy-up silently invalidates history that nobody
/// touched. A genuine change is a NEW implementation with a NEW <see cref="HashVersion"/>, published with its
/// own specification page and its own golden vectors; records written under the old one keep verifying under
/// the old one, forever.</para>
///
/// <para>The normative byte layout is <c>docs/fundamentals/agent-audit-canonicalization-v1.md</c>.</para>
/// </summary>
public interface IAgentActionCanonicalizer
{
    /// <summary>
    /// The canonicalization version this implementation produces and understands — canonical field 1, and the
    /// key a verifier dispatches on.
    /// </summary>
    int HashVersion { get; }

    /// <summary>
    /// Builds the canonical prefix (fields 1-23) for a record being WRITTEN.
    /// </summary>
    /// <param name="appId">The chain's application id — canonical field 2. Resolved server-side from the ingest
    /// service token, never taken from the payload, so a client cannot write into another application's chain.</param>
    /// <param name="record">The action being recorded.</param>
    /// <param name="recordKind">Canonical field 4: 0 = Event, 1 = Seal. A fold tombstone (2) carries no prefix
    /// at all — its row hash is carried over from the folded range rather than derived.</param>
    /// <returns>The canonical prefix bytes.</returns>
    byte[] BuildPrefix(Guid appId, AgentActionRecord record, byte recordKind = 0);

    /// <summary>
    /// Rebuilds the canonical prefix for a row being VERIFIED, <b>from that row's own columns</b>.
    ///
    /// <para>This overload exists because re-derivation and writing are not the same operation. A writer holds a
    /// <c>DateTime</c> and formats canonical field 6; a verifier holds the exact text that was hashed and must
    /// use it verbatim. Re-formatting a parsed timestamp would change the bytes for any archive whose text is
    /// not what this runtime's formatter would have produced, and report an intact archive as broken.</para>
    ///
    /// <para>It deliberately does NOT consult <see cref="AgentLedgerArchiveRow.CanonicalPrefix"/>: hashing the
    /// stored prefix would be circular, and catching an edited projection column is the entire reason a
    /// canonical verifier exists.</para>
    /// </summary>
    /// <param name="row">The archive row whose prefix is rebuilt. Its <c>HashVersion</c> is expected to equal
    /// <see cref="HashVersion"/>; the caller dispatches on that.</param>
    /// <returns>The canonical prefix bytes as this row's columns produce them.</returns>
    byte[] BuildPrefix(AgentLedgerArchiveRow row);

    /// <summary>
    /// Computes a row hash: <c>SHA-256(PrevHash ‖ canonicalPrefix ‖ SeqNo ‖ RecordedAtTicks)</c>, with the two
    /// trailing integers written big-endian.
    /// </summary>
    /// <param name="prevHash">The previous row's hash (32 bytes), or 32 zero bytes for a chain's first row.</param>
    /// <param name="canonicalPrefix">The canonical prefix — the REBUILT bytes when verifying, never the stored ones.</param>
    /// <param name="seqNo">The row's sequence number within its chain.</param>
    /// <param name="recordedAtTicks">100-nanosecond ticks since <c>1970-01-01T00:00:00Z</c>.</param>
    /// <returns>The 32-byte row hash.</returns>
    byte[] ComputeRowHash(
        ReadOnlySpan<byte> prevHash, ReadOnlySpan<byte> canonicalPrefix, long seqNo, long recordedAtTicks);
}
