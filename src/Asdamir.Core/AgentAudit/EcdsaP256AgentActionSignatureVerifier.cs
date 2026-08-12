// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.Security.Cryptography;

namespace Asdamir.Core.AgentAudit;

/// <summary>
/// Verifies <see cref="AgentSignatureAlgorithms.EcdsaP256Sha256"/> signatures.
/// </summary>
/// <remarks>
/// Stateless and allocation-light: it imports the public key per call rather than caching, because a cache
/// keyed on anything less than the exact key bytes is a way to verify against the wrong key.
/// </remarks>
public sealed class EcdsaP256AgentActionSignatureVerifier : IAgentActionSignatureVerifier
{
    /// <inheritdoc />
    public AgentSignatureState Verify(
        ReadOnlySpan<byte> canonicalPrefix,
        AgentActionSignature? signature,
        AgentSigningKey? key,
        DateTime signedAtUtc)
    {
        // Order matters and is normative. "Unsigned" is decided first because it is a property of the RECORD;
        // key availability second, because without a key nothing can be checked; only then is a verdict on the
        // signature itself possible. Any other order can report Invalid for a record nobody ever checked.
        if (signature is null || signature.Value.Length == 0)
            return AgentSignatureState.Unsigned;

        if (key is null || !key.IsUsableAt(signedAtUtc))
            return AgentSignatureState.KeyUnavailable;

        // An algorithm this verifier does not implement is NOT a failed check — it is an unperformed one.
        if (!string.Equals(signature.Algorithm, AgentSignatureAlgorithms.EcdsaP256Sha256, StringComparison.Ordinal)
            || !string.Equals(key.Algorithm, AgentSignatureAlgorithms.EcdsaP256Sha256, StringComparison.Ordinal))
        {
            return AgentSignatureState.KeyUnavailable;
        }

        using var ecdsa = ECDsa.Create();
        try
        {
            ecdsa.ImportSubjectPublicKeyInfo(key.PublicKey, out _);
        }
        catch (CryptographicException)
        {
            // A key that will not import cannot verify anything. Reporting Invalid here would blame the record
            // for a broken registry entry.
            return AgentSignatureState.KeyUnavailable;
        }

        var ok = ecdsa.VerifyData(
            canonicalPrefix, signature.Value,
            HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return ok ? AgentSignatureState.Valid : AgentSignatureState.Invalid;
    }
}
