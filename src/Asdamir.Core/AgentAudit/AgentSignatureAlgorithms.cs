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
/// The signature algorithm identifiers stored in the ledger's <c>SignatureAlgo</c> column.
/// </summary>
/// <remarks>
/// <para>These strings are a STORED CONTRACT, not a display label: a value written today must still be
/// interpretable by a verifier years later, so they live in one place rather than as literals scattered
/// across a signer, a verifier, a migration and a test.</para>
/// <para><b>Why ECDSA P-256 and not Ed25519.</b> Ed25519 would have been the better fit on paper —
/// deterministic, small, fast. It is <b>not available in .NET 10</b>: there is no EdDSA type in the base
/// class library, <c>curve25519</c> is not a supported named curve, and the OID resolves to nothing.
/// Measured, not assumed. Obtaining it would mean a third-party crypto dependency in BOTH
/// <c>Asdamir.Core</c> (and therefore every generated app) and <c>Asdamir.Tools</c>, whose whole reason for
/// source-linking the verifier is to avoid exactly that weight. P-256 is in the box, produces the same
/// 64-byte signature, and signs in well under a millisecond.</para>
/// <para><b>The one thing lost is determinism</b>, and its consequence is bounded: the signature sits
/// OUTSIDE the canonical body, so it affects neither the chain nor an archive's segment digest. It only
/// means a golden vector must VERIFY a signature rather than compare it byte-for-byte.</para>
/// </remarks>
public static class AgentSignatureAlgorithms
{
    /// <summary>
    /// ECDSA over NIST P-256 with SHA-256, signature in IEEE P1363 fixed-field form (64 bytes: r ‖ s).
    /// </summary>
    /// <remarks>
    /// The fixed-field form is chosen over DER on purpose: it is constant-length, so a stored signature has
    /// no parsing surface and no length ambiguity between producers.
    /// </remarks>
    public const string EcdsaP256Sha256 = "ecdsa-p256-sha256";
}
