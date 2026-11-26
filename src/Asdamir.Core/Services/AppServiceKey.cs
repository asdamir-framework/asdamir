// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.Text;
using System.Text.RegularExpressions;
using Microsoft.IdentityModel.Tokens;

namespace Asdamir.Core.Services;

/// <summary>
/// An application's OWN service key: the EC P-256 key pair its Gateway signs machine-to-machine tokens with
/// (the <c>app-log</c> and <c>agent-audit-ingest</c> service tokens). The private half stays on that one
/// Gateway (<see cref="ConfigurationKey"/>); the control plane holds only the public half, registered
/// against the app, and verifies a service token with the key registered for the <c>app_code</c> the token
/// names.
///
/// <para>This is what makes a service token speak for exactly one app. With the shared <c>Jwt:Key</c>, any
/// holder of that key could sign a service token carrying any <c>app_code</c>; with a per-app key, the
/// signature only verifies against the key registered for that app.</para>
///
/// <para>Generate a pair with <c>asdamir secrets new-signing-key</c>.</para>
/// </summary>
public sealed partial class AppServiceKey
{
    /// <summary>The configuration key holding the app's service PRIVATE key in PEM form. A secret.</summary>
    public const string ConfigurationKey = "App:ServiceKeyPem";

    /// <summary>The most public keys one app may have registered at once (two are enough to rotate).</summary>
    public const int MaxRegisteredKeys = 4;

    private readonly ECDsaSecurityKey _key;

    private AppServiceKey(ECDsaSecurityKey key)
    {
        _key = key;
        SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.EcdsaSha256);
    }

    /// <summary>ES256 credentials over the private key; the token header carries <see cref="KeyId"/>.</summary>
    public SigningCredentials SigningCredentials { get; }

    /// <summary>The key id: the RFC 7638 thumbprint of the public key.</summary>
    public string KeyId => _key.KeyId;

    /// <summary>Loads a service key from its private PEM.</summary>
    /// <param name="privateKeyPem">An EC P-256 private key in PEM form.</param>
    /// <returns>The loaded key.</returns>
    /// <exception cref="InvalidOperationException">The PEM is not a readable P-256 private key.</exception>
    public static AppServiceKey FromPem(string privateKeyPem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyPem);
        return new AppServiceKey(JwtKeyMaterial.EcKey(privateKeyPem, ConfigurationKey, requirePrivate: true));
    }

    /// <summary>Exports the public half in PEM form — the value to register with the control plane.</summary>
    /// <returns>The public key as a <c>PUBLIC KEY</c> PEM block.</returns>
    public string ExportPublicKeyPem() => _key.ECDsa.ExportSubjectPublicKeyInfoPem();

    /// <summary>
    /// Chooses the credentials a service token is signed with: the app's own service key when
    /// <paramref name="serviceKeyPem"/> is set, otherwise the shared symmetric key (the pre-2.0 behaviour,
    /// kept so an app that has not been given a service key yet keeps delivering).
    /// </summary>
    /// <param name="serviceKeyPem">The app's service private key (<see cref="ConfigurationKey"/>), or empty.</param>
    /// <param name="sharedKey">The shared <c>Jwt:Key</c>, or empty.</param>
    /// <returns>ES256 credentials over the service key, or HS256 credentials over the shared key.</returns>
    /// <exception cref="InvalidOperationException">Neither key is configured, or the PEM is unreadable.</exception>
    public static SigningCredentials CredentialsFor(string? serviceKeyPem, string? sharedKey)
    {
        if (!string.IsNullOrWhiteSpace(serviceKeyPem)) return FromPem(serviceKeyPem).SigningCredentials;
        if (string.IsNullOrWhiteSpace(sharedKey))
            throw new InvalidOperationException(
                $"No key to sign service tokens with: set {ConfigurationKey} (the app's own service key).");
        return new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(sharedKey)),
            SecurityAlgorithms.HmacSha256);
    }

    /// <summary>
    /// Reads the public keys an app has registered: one or more <c>PUBLIC KEY</c> PEM blocks. More than one
    /// is how a key is rotated without a gap — register the new key beside the old, switch the Gateway,
    /// then remove the old.
    /// </summary>
    /// <param name="publicKeysPem">The registered PEM text.</param>
    /// <returns>Public-only ES256 validation keys, each with its thumbprint as key id.</returns>
    /// <exception cref="InvalidOperationException">
    /// The text holds no key, more than <see cref="MaxRegisteredKeys"/>, a private key, or a key that is not P-256.
    /// </exception>
    public static IReadOnlyList<SecurityKey> ParsePublicKeys(string publicKeysPem)
    {
        ArgumentNullException.ThrowIfNull(publicKeysPem);
        var text = publicKeysPem.Replace("\\n", "\n", StringComparison.Ordinal);
        if (text.Contains("PRIVATE KEY", StringComparison.Ordinal))
            throw new InvalidOperationException(
                "A PRIVATE key was supplied. Register only the public key; the private key never leaves the app's Gateway.");

        var blocks = PublicKeyBlock().Matches(text);
        if (blocks.Count == 0)
            throw new InvalidOperationException("No PUBLIC KEY block in PEM form was found.");
        if (blocks.Count > MaxRegisteredKeys)
            throw new InvalidOperationException($"At most {MaxRegisteredKeys} service keys may be registered for one app.");

        var keys = new List<SecurityKey>(blocks.Count);
        foreach (Match block in blocks)
            keys.Add(JwtKeyMaterial.EcKey(block.Value, "The service public key", requirePrivate: false));
        return keys;
    }

    [GeneratedRegex("-----BEGIN PUBLIC KEY-----.*?-----END PUBLIC KEY-----", RegexOptions.Singleline)]
    private static partial Regex PublicKeyBlock();
}
