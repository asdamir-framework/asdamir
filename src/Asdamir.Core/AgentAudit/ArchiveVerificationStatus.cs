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
/// The FOUR — and only four — verdicts a conforming archive verifier may report.
///
/// <para>An archive can attest to two different things, and a tool that blurs them is lying to its user.
/// <b>A</b> — internal consistency: every row's hash is correctly derived from its own columns, the chain
/// links hold and the sequence is dense. The archive alone CAN prove this. <b>B</b> — anchoring: that this
/// segment is genuinely the one folded out of a particular live ledger. The archive alone CANNOT prove this;
/// the proof is the <c>FoldSegmentDigest</c> recorded on the tombstone in that ledger, supplied by the caller.
/// The digest inside the manifest is derived from the very rows it accompanies, so an attacker who rewrites
/// the rows simply recomputes it — it is not an anchor.</para>
///
/// <para><b>There is deliberately no member for a FORMAT ERROR.</b> An archive whose shape or version was not
/// understood was never CHECKED, so reporting any of these four would claim a check that did not happen. That
/// case is carried by <see cref="ArchiveVerificationResult.Status"/> being <c>null</c> — see
/// <see cref="ArchiveVerificationResult.IsFormatError"/>.</para>
/// </summary>
public enum ArchiveVerificationStatus
{
    /// <summary>
    /// Internal checks pass <b>and</b> an expected digest was supplied <b>and</b> it matches — A and B. The only
    /// value that may be presented as success.
    /// </summary>
    Verified = 0,

    /// <summary>
    /// Internal checks pass, but NO expected digest was supplied — A only, <b>unanchored</b>. The archive has
    /// not been altered since it was written; whether it is the segment that left a particular ledger is
    /// unproven. A verifier MUST NOT render this as success: same discipline as never painting a
    /// <c>Degraded</c> chain green.
    /// </summary>
    InternallyConsistent = 1,

    /// <summary>
    /// Internal checks pass, an expected digest was supplied, and it does NOT match — these rows are not that
    /// segment. Loud and separate from <see cref="Broken"/> on purpose: the archive is internally sound, it is
    /// simply not the one being asked about.
    /// </summary>
    DigestMismatch = 2,

    /// <summary>
    /// An internal check failed — a rebuilt prefix that disagrees with the stored one, a hash mismatch, a
    /// broken link, a gap, a line count that contradicts <c>RowCount</c>, or a manifest digest that does not
    /// describe these rows. The archive is altered or corrupt.
    /// </summary>
    Broken = 3,
}
