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
/// Signs with ECDSA over NIST P-256 and SHA-256 — see <see cref="AgentSignatureAlgorithms.EcdsaP256Sha256"/>.
/// </summary>
public sealed class EcdsaP256AgentActionSigner : IAgentActionSigner, IDisposable
{
    private readonly ECDsa _key;
    private readonly string _keyId;

    /// <summary>Creates a signer over a key the caller owns.</summary>
    /// <param name="keyId">The registered key id, recorded on every signature this signer produces.</param>
    /// <param name="key">The private key. This instance takes ownership and disposes it.</param>
    public EcdsaP256AgentActionSigner(string keyId, ECDsa key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        ArgumentNullException.ThrowIfNull(key);
        _keyId = keyId;
        _key = key;
    }

    /// <summary>Creates a signer from a PKCS#8 private key.</summary>
    /// <param name="keyId">The registered key id.</param>
    /// <param name="pkcs8PrivateKey">The private key in unencrypted PKCS#8 DER form.</param>
    /// <returns>A signer that owns the imported key.</returns>
    /// <remarks>
    /// <para><b>This is the only place in the public API that accepts a private key, and the caller owns what
    /// happens to it.</b> The framework does not log it, does not persist it, does not copy it anywhere, and
    /// never transmits it — the bytes are imported into an <see cref="ECDsa"/> instance and nothing else.</para>
    /// <para>Everything before that call is the caller's responsibility: where the key is stored, who can read
    /// it, whether it reaches a log line, a crash dump, an exception message or a configuration file checked
    /// into source control. A signing key in a log is a signing key that is gone, and no API can prevent that
    /// from the inside.</para>
    /// </remarks>
    public static EcdsaP256AgentActionSigner FromPkcs8(string keyId, ReadOnlySpan<byte> pkcs8PrivateKey)
    {
        var key = ECDsa.Create();
        try
        {
            key.ImportPkcs8PrivateKey(pkcs8PrivateKey, out _);
            return new EcdsaP256AgentActionSigner(keyId, key);
        }
        catch
        {
            key.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public AgentActionSignature Sign(ReadOnlySpan<byte> canonicalPrefix)
    {
        // SignData, not SignHash: the algorithm identifier names SHA-256, so the hashing belongs inside the
        // contract rather than at each call site where a caller could hash with something else and still
        // produce a "valid" signature under the wrong name.
        var value = _key.SignData(
            canonicalPrefix, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return new AgentActionSignature(AgentSignatureAlgorithms.EcdsaP256Sha256, _keyId, value);
    }

    /// <summary>Disposes the private key.</summary>
    public void Dispose() => _key.Dispose();
}
