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
/// Checks a signature against a resolved public key.
/// </summary>
public interface IAgentActionSignatureVerifier
{
    /// <summary>Verifies one record's signature.</summary>
    /// <param name="canonicalPrefix">The canonical body the signature should cover.</param>
    /// <param name="signature">The signature, or <see langword="null"/> when the record is unsigned.</param>
    /// <param name="key">The resolved key, or <see langword="null"/> when it could not be found.</param>
    /// <param name="signedAtUtc">When the record was signed — used to judge the key's validity window.</param>
    /// <returns>
    /// Which of the four states holds. A null <paramref name="signature"/> yields
    /// <see cref="AgentSignatureState.Unsigned"/>; a null or unusable <paramref name="key"/> yields
    /// <see cref="AgentSignatureState.KeyUnavailable"/> — never <see cref="AgentSignatureState.Invalid"/>,
    /// because nothing was checked.
    /// </returns>
    AgentSignatureState Verify(
        ReadOnlySpan<byte> canonicalPrefix,
        AgentActionSignature? signature,
        AgentSigningKey? key,
        DateTime signedAtUtc);
}
