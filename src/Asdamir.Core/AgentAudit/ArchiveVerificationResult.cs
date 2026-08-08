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
/// What <see cref="AgentLedgerArchiveVerifier"/> concluded about one archive — and, just as importantly, what
/// it did NOT conclude.
///
/// <para><b>Read <see cref="ProvesNotAltered"/> and <see cref="ProvesAnchored"/>, not just
/// <see cref="Status"/>.</b> They are the two independent claims an archive can carry, kept as two separate
/// booleans precisely so a caller cannot collapse them into one green tick. "Verified ✓" on its own is not an
/// acceptable message; a result that proves the bytes are unaltered but cannot prove they are the segment that
/// left a live ledger must SAY so.</para>
///
/// <para><b>How a FORMAT ERROR is surfaced without a fifth status.</b> The specification allows exactly four
/// verdicts, and states that an archive whose shape or version was not understood must NOT be reported as any
/// of them — nothing was checked, and reporting a verdict would present an unchecked archive as checked. So a
/// format error is represented by <see cref="Status"/> being <c>null</c> (<see cref="IsFormatError"/>), with
/// <see cref="Code"/> and <see cref="Message"/> saying what was not understood. The absence of a verdict IS the
/// finding, and the type makes it unrepresentable to claim otherwise: both proof flags are false whenever
/// <see cref="Status"/> is null, so no caller can accidentally read a refusal as a pass.</para>
/// </summary>
public sealed record ArchiveVerificationResult
{
    /// <summary>
    /// The verdict, or <c>null</c> for a FORMAT ERROR — the archive's shape or version was not understood, so
    /// no integrity check was performed and no verdict is honest. See the type remarks.
    /// </summary>
    public ArchiveVerificationStatus? Status { get; init; }

    /// <summary>
    /// True when the archive was refused for its SHAPE rather than judged on its content: an unsupported
    /// <c>FormatVersion</c> or <c>HashVersion</c>, a missing container entry, a missing or malformed required
    /// member, or a fold tombstone inside the segment. Equivalent to <see cref="Status"/> being <c>null</c>.
    /// </summary>
    public bool IsFormatError => Status is null;

    /// <summary>
    /// A stable, machine-readable code for the outcome — <c>verified</c>, <c>internally_consistent</c>,
    /// <c>digest_mismatch</c>, or the specific failure (<c>canonical_prefix_mismatch</c>,
    /// <c>row_hash_mismatch</c>, <c>chain_link_mismatch</c>, <c>row_count_mismatch</c>, <c>seq_no_not_dense</c>,
    /// <c>manifest_digest_mismatch</c>, <c>unsupported_format_version</c>, <c>segment_contains_tombstone</c>, …).
    /// Meant for scripts and tests; <see cref="Message"/> is meant for people.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// The finding in plain English, including the position when there is one. Deliberately not localized: this
    /// text is evidence quoted in an audit, and a finding that reads differently depending on the reader's
    /// locale is a finding two people cannot compare.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// The <c>SeqNo</c> the finding is about, when it is about one row; <c>null</c> for whole-archive findings.
    /// A verifier that says "the archive is broken" without a position is not actionable.
    /// </summary>
    public long? SeqNo { get; init; }

    /// <summary>
    /// <b>Claim A</b> — this archive has not been altered since it was written. True for
    /// <see cref="ArchiveVerificationStatus.Verified"/>, <see cref="ArchiveVerificationStatus.InternallyConsistent"/>
    /// and <see cref="ArchiveVerificationStatus.DigestMismatch"/> (whose internal checks all passed — it is
    /// simply a different segment than the one asked about), false for
    /// <see cref="ArchiveVerificationStatus.Broken"/> and for a format error.
    /// </summary>
    public bool ProvesNotAltered =>
        Status is ArchiveVerificationStatus.Verified
               or ArchiveVerificationStatus.InternallyConsistent
               or ArchiveVerificationStatus.DigestMismatch;

    /// <summary>
    /// <b>Claim B</b> — this segment is the one that was folded out of the live ledger the caller named. True
    /// ONLY for <see cref="ArchiveVerificationStatus.Verified"/>, because the only evidence for it is an
    /// expected digest supplied from outside the archive.
    /// </summary>
    public bool ProvesAnchored => Status is ArchiveVerificationStatus.Verified;

    /// <summary>How many rows were fully re-derived and re-hashed before the result was reached.</summary>
    public long RowsChecked { get; init; }

    /// <summary>
    /// Lowercase hex of the segment digest RECOMPUTED from the rows that were read; <c>null</c> when the run
    /// never got that far. This is the value to compare against the tombstone's <c>FoldSegmentDigest</c>.
    /// </summary>
    public string? ComputedSegmentDigest { get; init; }

    /// <summary>
    /// Lowercase hex of the digest the caller supplied as the anchor, if any. <c>null</c> means the run was
    /// UNANCHORED and <see cref="ProvesAnchored"/> is false by construction.
    /// </summary>
    public string? ExpectedSegmentDigest { get; init; }

    /// <summary>
    /// For a row-level divergence: lowercase hex of the bytes the row's own columns produce — what SHOULD be
    /// there. <c>null</c> otherwise.
    /// </summary>
    public string? ExpectedHex { get; init; }

    /// <summary>
    /// For a row-level divergence: lowercase hex of the bytes actually stored in the archive. <c>null</c>
    /// otherwise.
    /// </summary>
    public string? ActualHex { get; init; }

    /// <summary>
    /// Which part diverged — <c>canonical_prefix</c> or <c>row_hash</c> — for a row-level finding; <c>null</c>
    /// otherwise.
    /// </summary>
    public string? DivergedIn { get; init; }

    /// <summary>
    /// The manifest as read, once it parsed; <c>null</c> when the container or the manifest itself could not be
    /// read. Carried so a caller can report the segment's scope and range alongside the verdict.
    /// </summary>
    public AgentLedgerArchiveManifest? Manifest { get; init; }
}
