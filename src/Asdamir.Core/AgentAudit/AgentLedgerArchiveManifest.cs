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
/// The <c>manifest.json</c> of an agent action ledger archive — the frozen v1 archive format's descriptor for a
/// folded segment.
///
/// <para><b>This type is the archive READING model, and it is open core on purpose.</b> A folded range leaves the
/// live database and survives only as an archive; if the only thing able to read that archive were the closed
/// component that produced it, the assurance would collapse to "trust the vendor". The producer writes these
/// members and an independent verifier reads them — one model, so the two cannot drift.</para>
///
/// <para><b>Every one of the twelve members is REQUIRED.</b> A missing member is a format error, not a
/// best-effort parse: an archive whose shape is not understood has not been checked, and presenting an unchecked
/// archive as checked is the failure this whole format exists to prevent. The <c>required</c> modifiers make that
/// mechanical — deserializing a manifest with a member missing throws rather than yielding a default.</para>
///
/// <para><b><see cref="SegmentDigest"/> is NOT an anchor.</b> It is derived from the very rows it accompanies, so
/// an attacker who rewrites the rows simply recomputes it. It proves "this manifest describes these rows" and
/// nothing more. Proof that this segment is the one folded out of a particular live ledger is the
/// <c>FoldSegmentDigest</c> recorded on the tombstone in that ledger — a value the archive cannot produce for
/// itself, which is exactly why it is evidence.</para>
///
/// <para>The normative specification is <c>docs/fundamentals/agent-audit-archive-format-v1.md</c>; the byte
/// layout the rows are hashed under is <c>docs/fundamentals/agent-audit-canonicalization-v1.md</c>.</para>
/// </summary>
public sealed record AgentLedgerArchiveManifest
{
    /// <summary>The only archive format version in existence. Its layout, member names and encodings are frozen.</summary>
    public const int FormatVersion1 = 1;

    /// <summary>The container's manifest entry name. Case-sensitive, at the root of the ZIP.</summary>
    public const string ManifestEntryName = "manifest.json";

    /// <summary>The container's segment entry name. Case-sensitive, at the root of the ZIP.</summary>
    public const string SegmentEntryName = "segment.ndjson";

    /// <summary>
    /// This specification's version — <see cref="FormatVersion1"/> for every v1 archive. A reader that does not
    /// implement the value it finds here must REFUSE the archive with a distinct "unsupported format version"
    /// error, never attempt a best-effort parse.
    /// </summary>
    public required int FormatVersion { get; init; }

    /// <summary>
    /// The canonicalization version the rows were hashed under. It is field 1 of the canonical body, so assuming
    /// the wrong one changes every hash; a reader that does not implement it must refuse rather than guess.
    /// </summary>
    public required int HashVersion { get; init; }

    /// <summary>The chain's application — half of the scope <c>(AppId, TenantId)</c>. Canonical <c>8-4-4-4-12</c> text form.</summary>
    public required Guid AppId { get; init; }

    /// <summary>The chain's tenant — the other half of the scope.</summary>
    public required string TenantId { get; init; }

    /// <summary>The <c>SeqNo</c> of the first row in the segment.</summary>
    public required long SeqStart { get; init; }

    /// <summary>The <c>SeqNo</c> of the last row in the segment.</summary>
    public required long SeqEnd { get; init; }

    /// <summary>
    /// How many rows the segment holds. It must equal both <see cref="SeqEnd"/> − <see cref="SeqStart"/> + 1 and
    /// the actual line count of <c>segment.ndjson</c> — the segment is dense by definition.
    /// </summary>
    public required long RowCount { get; init; }

    /// <summary>
    /// Lowercase hex of <c>SHA-256</c> over the segment's raw 32-byte row hashes concatenated in ascending
    /// <c>SeqNo</c> order. A verifier recomputes it from the rows it read; comparing it to a digest supplied by
    /// the caller is the separate, and only real, anchoring check.
    /// </summary>
    public required string SegmentDigest { get; init; }

    /// <summary>
    /// Lowercase hex of the <c>PrevHash</c> of the row at <see cref="SeqStart"/> — the link back to the row that
    /// preceded the segment, so it can be re-attached to the live ledger.
    /// </summary>
    public required string PrevHashAtStart { get; init; }

    /// <summary>
    /// Lowercase hex of the <c>RowHash</c> of the row at <see cref="SeqEnd"/> — the link forward to the row that
    /// followed the segment. The tombstone that replaced this range carries the same two values.
    /// </summary>
    public required string RowHashAtEnd { get; init; }

    /// <summary>
    /// When the archive was produced, ISO-8601 round-trip in UTC (<c>…Z</c>). <b>Informational — outside every
    /// hash</b>, so editing it breaks nothing and it is not evidence of anything.
    /// </summary>
    public required string ExportedAtUtc { get; init; }

    /// <summary>
    /// The component and version that produced the archive. <b>Informational</b>, for the same reason as
    /// <see cref="ExportedAtUtc"/>.
    /// </summary>
    public required string ProducerVersion { get; init; }
}
