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
/// A registered public key an agent signs with.
/// </summary>
/// <remarks>
/// Rotation needs an overlap window — the old key must stay verifiable for records already written — so an
/// agent holds a LIST of keys, not one. <see cref="ValidToUtc"/> expresses a planned rotation and
/// <see cref="RevokedAtUtc"/> an unplanned withdrawal; both are needed because they answer different
/// questions, and a separate revocation list would only duplicate the second.
/// </remarks>
/// <param name="KeyId">Stable identifier, recorded on every record this key signs.</param>
/// <param name="AgentId">The agent this key belongs to.</param>
/// <param name="Algorithm">Which algorithm the key is for — see <see cref="AgentSignatureAlgorithms"/>.</param>
/// <param name="PublicKey">The public key in SubjectPublicKeyInfo (SPKI) DER form.</param>
/// <param name="ValidFromUtc">When the key becomes usable.</param>
/// <param name="ValidToUtc">When it stops being usable, or null for open-ended.</param>
/// <param name="RevokedAtUtc">When it was revoked, or null.</param>
public sealed record AgentSigningKey(
    string KeyId,
    string AgentId,
    string Algorithm,
    byte[] PublicKey,
    DateTime ValidFromUtc,
    DateTime? ValidToUtc,
    DateTime? RevokedAtUtc)
{
    /// <summary>Whether this key may be used to verify something signed at <paramref name="utc"/>.</summary>
    /// <param name="utc">The instant to test, in UTC.</param>
    /// <returns><see langword="true"/> when the key is within its window and not revoked.</returns>
    /// <remarks>
    /// Revocation is judged against the SIGNING time, not against now. A record legitimately signed before a
    /// key was withdrawn stays verifiable; treating revocation as retroactive would silently invalidate
    /// history that was honest when it was written.
    /// </remarks>
    public bool IsUsableAt(DateTime utc)
        => utc >= ValidFromUtc
        && (ValidToUtc is null || utc < ValidToUtc)
        && (RevokedAtUtc is null || utc < RevokedAtUtc);
}
