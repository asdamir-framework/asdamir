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
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;

namespace Asdamir.Core.Services;

/// <summary>
/// The JWT keys a host holds, resolved once from <c>Jwt:*</c> configuration: what it signs with (if it
/// signs at all) and what it accepts on validation, with the accepted algorithms pinned to exactly those keys.
/// </summary>
/// <remarks>
/// <para>Four shapes, chosen by which settings are present:</para>
/// <list type="bullet">
/// <item><description><b>Issuer</b> — <c>Jwt:SigningKeyPem</c> (an EC P-256 private key): signs <c>ES256</c>
/// with a <c>kid</c>, validates with the matching public key. Only the party that issues tokens holds this.</description></item>
/// <item><description><b>Validator</b> — <c>Jwt:PublicKeyPem</c> (the issuer's public key): validates
/// <c>ES256</c> and cannot sign. A host that only checks tokens holds nothing that can mint one.</description></item>
/// <item><description><b>Validator, by URL</b> — <c>Jwt:JwksUrl</c> (the issuer's
/// <c>/.well-known/jwks.json</c>, https only): like the validator above, but the public keys are fetched
/// and refreshed from the issuer, so a key rotation needs no configuration change here. Until the first
/// successful fetch no token validates (fail closed); after one, the last good key set is kept while the
/// issuer is unreachable.</description></item>
/// <item><description><b>Symmetric</b> — <c>Jwt:Key</c> alone: signs and validates <c>HS256</c>. Correct when
/// one host both issues and validates (a self-contained app); wrong as a key shared between hosts, because
/// every holder can then mint any token.</description></item>
/// </list>
/// <para>When an asymmetric key is configured, <c>Jwt:Key</c> is <b>ignored for validation</b> unless
/// <c>Jwt:AcceptLegacySymmetric=true</c> — a migration window in which tokens signed with the old shared key
/// are still accepted. Turn it off once those tokens have expired.</para>
/// <para><b>Rotating the signing key without a failed request.</b> <c>Jwt:AdditionalPublicKeysPem</c> holds
/// up to <see cref="MaxAdditionalPublicKeys"/> further public keys (<c>PUBLIC KEY</c> PEM blocks) beside
/// the main one. On the issuer they are published in the key set and accepted on validation, which covers
/// both ends of a rotation: the <i>next</i> key is published before anything is signed with it, and the
/// <i>previous</i> key stays valid until the tokens it signed have expired. On a host pinned with
/// <c>Jwt:PublicKeyPem</c> they are simply further accepted keys.</para>
/// <para><see cref="ValidAlgorithms"/> always lists only the algorithms of the keys actually loaded, so a
/// token cannot choose its own algorithm (<c>none</c>, or <c>HS256</c> presented against a public key).</para>
/// </remarks>
public sealed partial class JwtKeyMaterial
{
    /// <summary>The most keys <c>Jwt:AdditionalPublicKeysPem</c> may hold (two are enough to rotate).</summary>
    public const int MaxAdditionalPublicKeys = 4;

    private readonly IReadOnlyList<ECDsaSecurityKey> _additionalPublicKeys;

    /// <summary>Minimum byte length for an HMAC-SHA256 key (256 bits).</summary>
    private const int MinSymmetricKeyBytes = 32;

    private readonly SigningCredentials? _signingCredentials;

    private JwtKeyMaterial(SigningCredentials? signing, IReadOnlyList<SecurityKey> validationKeys,
        IReadOnlyList<string> validAlgorithms, BaseConfigurationManager? keySetManager = null,
        IReadOnlyList<ECDsaSecurityKey>? additionalPublicKeys = null)
    {
        _additionalPublicKeys = additionalPublicKeys ?? [];
        _signingCredentials = signing;
        ValidationKeys = validationKeys;
        ValidAlgorithms = validAlgorithms;
        KeySetManager = keySetManager;
    }

    /// <summary>How often the key set at <c>Jwt:JwksUrl</c> is re-fetched without being asked.</summary>
    private static readonly TimeSpan JwksAutomaticRefresh = TimeSpan.FromHours(1);

    /// <summary>The shortest gap between two fetches forced by a token whose <c>kid</c> is unknown — the
    /// bound on how fast a stream of garbage tokens can make this host call the issuer.</summary>
    private static readonly TimeSpan JwksForcedRefreshGap = TimeSpan.FromSeconds(30);

    /// <summary>
    /// The source of remotely published keys when <c>Jwt:JwksUrl</c> is configured; <c>null</c> otherwise.
    /// Those keys are not in <see cref="ValidationKeys"/> — they are resolved per validation.
    /// </summary>
    public BaseConfigurationManager? KeySetManager { get; }

    /// <summary>Whether this host holds a key it can sign tokens with.</summary>
    public bool CanSign => _signingCredentials is not null;

    /// <summary>The signing algorithm (<c>ES256</c> or <c>HS256</c>), or <c>null</c> for a validate-only host.</summary>
    public string? SigningAlgorithm => _signingCredentials?.Algorithm;

    /// <summary>The <c>kid</c> stamped into issued tokens (the RFC 7638 thumbprint of the public key);
    /// <c>null</c> when signing symmetrically or not at all.</summary>
    public string? SigningKeyId => _signingCredentials?.Key is ECDsaSecurityKey k ? k.KeyId : null;

    /// <summary>The credentials tokens are signed with.</summary>
    /// <exception cref="InvalidOperationException">This host holds no signing key (it is validate-only).</exception>
    public SigningCredentials SigningCredentials => _signingCredentials
        ?? throw new InvalidOperationException(
            "This host cannot issue tokens: it holds only Jwt:PublicKeyPem. Tokens are signed by the issuer " +
            "that holds Jwt:SigningKeyPem.");

    /// <summary>Every key a presented token may be verified against.</summary>
    public IReadOnlyList<SecurityKey> ValidationKeys { get; }

    /// <summary>The only algorithms a presented token may use — exactly those of <see cref="ValidationKeys"/>.</summary>
    public IReadOnlyList<string> ValidAlgorithms { get; }

    /// <summary>
    /// Sets the signature half of <paramref name="parameters"/>: the accepted keys, the pinned algorithm
    /// list, and <c>ValidateIssuerSigningKey</c>/<c>RequireSignedTokens</c>. Issuer, audience and lifetime
    /// are the caller's to set.
    /// </summary>
    /// <param name="parameters">The validation parameters to fill in.</param>
    public void ApplyTo(TokenValidationParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        parameters.ValidateIssuerSigningKey = true;
        parameters.RequireSignedTokens = true;
        parameters.IssuerSigningKey = null;
        parameters.IssuerSigningKeys = ValidationKeys;
        parameters.ValidAlgorithms = ValidAlgorithms;
        parameters.ConfigurationManager = KeySetManager;
    }

    /// <summary>
    /// The public keys this host signs with, as a JSON Web Key Set (RFC 7517) — the document an issuer
    /// serves at <c>/.well-known/jwks.json</c> and a validator points <c>Jwt:JwksUrl</c> at. It never
    /// contains private or symmetric key material; a host that signs symmetrically publishes an empty set.
    /// An issuer also publishes its <c>Jwt:AdditionalPublicKeysPem</c> keys (the next and the previous key
    /// of a rotation); a host that cannot sign publishes nothing.
    /// </summary>
    /// <returns>The JWKS JSON document.</returns>
    public string ExportJwks()
    {
        var keys = new List<Dictionary<string, string>>();
        if (_signingCredentials?.Key is ECDsaSecurityKey signingKey)
        {
            var jwk = JsonWebKeyConverter.ConvertFromECDsaSecurityKey(PublicOnly(signingKey));
            keys.Add(Jwk(jwk, signingKey.KeyId));
            foreach (var extra in _additionalPublicKeys)
            {
                if (extra.KeyId == signingKey.KeyId) continue;
                keys.Add(Jwk(JsonWebKeyConverter.ConvertFromECDsaSecurityKey(extra), extra.KeyId));
            }
        }
        return JsonSerializer.Serialize(new Dictionary<string, object> { ["keys"] = keys });
    }

    private static Dictionary<string, string> Jwk(JsonWebKey jwk, string keyId) => new()
    {
        ["kty"] = jwk.Kty, ["crv"] = jwk.Crv, ["x"] = jwk.X, ["y"] = jwk.Y,
        ["kid"] = keyId, ["use"] = "sig", ["alg"] = SecurityAlgorithms.EcdsaSha256,
    };

    /// <summary>
    /// Resolves the key material from the <c>Jwt</c> section (<c>SigningKeyPem</c>, <c>PublicKeyPem</c>,
    /// <c>AdditionalPublicKeysPem</c>, <c>JwksUrl</c>, <c>Key</c>, <c>AcceptLegacySymmetric</c>). Key values are held in memory only; never log them.
    /// </summary>
    /// <param name="configuration">Application configuration supplying the <c>Jwt:*</c> section.</param>
    /// <param name="jwksClient">The HTTP client used to fetch <c>Jwt:JwksUrl</c>; <c>null</c> uses a default
    /// one. Supply it to control the timeout or, in development, certificate validation.</param>
    /// <exception cref="InvalidOperationException">No key is configured, a key is malformed or too weak,
    /// more than one of the three asymmetric settings is present, <c>Jwt:PublicKeyPem</c> holds a private
    /// key, <c>Jwt:JwksUrl</c> is not an absolute https URL, or <c>Jwt:AdditionalPublicKeysPem</c> is set
    /// without <c>Jwt:SigningKeyPem</c> / <c>Jwt:PublicKeyPem</c>, holds a private key, a key that is not
    /// P-256, no key, or more than <see cref="MaxAdditionalPublicKeys"/>.</exception>
    public static JwtKeyMaterial FromConfiguration(IConfiguration configuration, HttpClient? jwksClient = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var signingPem = configuration["Jwt:SigningKeyPem"];
        var publicPem = configuration["Jwt:PublicKeyPem"];
        var jwksUrl = configuration["Jwt:JwksUrl"];
        var symmetric = configuration["Jwt:Key"];
        var hasSigning = !string.IsNullOrWhiteSpace(signingPem);
        var hasPublic = !string.IsNullOrWhiteSpace(publicPem);
        var hasJwks = !string.IsNullOrWhiteSpace(jwksUrl);
        var additionalPem = configuration["Jwt:AdditionalPublicKeysPem"];
        var hasAdditional = !string.IsNullOrWhiteSpace(additionalPem);

        if ((hasSigning ? 1 : 0) + (hasPublic ? 1 : 0) + (hasJwks ? 1 : 0) > 1)
            throw new InvalidOperationException(
                "Set exactly one of Jwt:SigningKeyPem (the issuer), Jwt:PublicKeyPem or Jwt:JwksUrl (a validator), not both.");

        // Extra keys only mean something beside a main key held as a PEM. A JWKS validator takes its key
        // set from the issuer, and a symmetric host has no public keys at all — refuse rather than ignore.
        if (hasAdditional && !hasSigning && !hasPublic)
            throw new InvalidOperationException(
                "Jwt:AdditionalPublicKeysPem needs Jwt:SigningKeyPem (the issuer) or Jwt:PublicKeyPem (a pinned validator) beside it.");

        if (hasJwks)
        {
            if (!Uri.TryCreate(jwksUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException(
                    "Jwt:JwksUrl must be an absolute https URL: keys fetched over plain http can be replaced in transit.");
            var retriever = jwksClient is null ? new HttpDocumentRetriever() : new HttpDocumentRetriever(jwksClient);
            retriever.RequireHttps = true;
            var manager = new ConfigurationManager<JwksConfiguration>(uri.AbsoluteUri, new JwksRetriever(), retriever)
            {
                AutomaticRefreshInterval = JwksAutomaticRefresh,
                RefreshInterval = JwksForcedRefreshGap,
                // Off on purpose. With it on, the handler falls back to the PREVIOUS key set for up to an
                // hour after a refresh, so a key the issuer has withdrawn (the point of a rotation after a
                // leak) would go on being trusted. A failed refresh still keeps the current set.
                UseLastKnownGoodConfiguration = false,
            };
            var legacy = LegacyKeys(configuration, symmetric);
            return new JwtKeyMaterial(null, legacy.Keys,
                [SecurityAlgorithms.EcdsaSha256, .. legacy.Algorithms], manager);
        }

        if (!hasSigning && !hasPublic)
        {
            // Symmetric only: one key signs and validates. The message names Jwt:Key first because that is
            // what a host with no JWT configuration at all is missing.
            if (symmetric is null)
                throw new InvalidOperationException(
                    "Jwt:Key is not configured (or set Jwt:SigningKeyPem / Jwt:PublicKeyPem for ES256).");
            var hmac = SymmetricKey(symmetric);
            return new JwtKeyMaterial(
                new SigningCredentials(hmac, SecurityAlgorithms.HmacSha256),
                [hmac], [SecurityAlgorithms.HmacSha256]);
        }

        ECDsaSecurityKey ecKey;
        SigningCredentials? signing = null;
        if (hasSigning)
        {
            ecKey = EcKey(signingPem!, "Jwt:SigningKeyPem", requirePrivate: true);
            signing = new SigningCredentials(ecKey, SecurityAlgorithms.EcdsaSha256);
        }
        else
        {
            ecKey = EcKey(publicPem!, "Jwt:PublicKeyPem", requirePrivate: false);
        }

        // Validation never needs the private half: verify against a public-only copy of the key.
        var legacyKeys = LegacyKeys(configuration, symmetric);
        var additional = hasAdditional ? AdditionalPublicKeys(additionalPem!) : [];
        return new JwtKeyMaterial(signing,
            [PublicOnly(ecKey), .. additional.Where(k => k.KeyId != ecKey.KeyId), .. legacyKeys.Keys],
            [SecurityAlgorithms.EcdsaSha256, .. legacyKeys.Algorithms],
            additionalPublicKeys: additional);
    }

    /// <summary>Reads <c>Jwt:AdditionalPublicKeysPem</c>: one or more <c>PUBLIC KEY</c> blocks, public only.</summary>
    private static List<ECDsaSecurityKey> AdditionalPublicKeys(string pem)
    {
        const string setting = "Jwt:AdditionalPublicKeysPem";
        var text = pem.Replace("\\n", "\n", StringComparison.Ordinal);
        if (text.Contains("PRIVATE KEY", StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"{setting} holds a PRIVATE key. It takes public keys only; a private key belongs in Jwt:SigningKeyPem on the issuer.");
        var blocks = PublicKeyBlock().Matches(text);
        if (blocks.Count == 0)
            throw new InvalidOperationException($"{setting} holds no PUBLIC KEY block in PEM form.");
        if (blocks.Count > MaxAdditionalPublicKeys)
            throw new InvalidOperationException($"{setting} may hold at most {MaxAdditionalPublicKeys} keys.");
        var keys = new List<ECDsaSecurityKey>(blocks.Count);
        foreach (Match block in blocks) keys.Add(EcKey(block.Value, setting, requirePrivate: false));
        return keys;
    }

    [GeneratedRegex("-----BEGIN PUBLIC KEY-----.*?-----END PUBLIC KEY-----", RegexOptions.Singleline)]
    private static partial Regex PublicKeyBlock();

    /// <summary>The old shared key, only while <c>Jwt:AcceptLegacySymmetric</c> is on; otherwise nothing.</summary>
    private static (SecurityKey[] Keys, string[] Algorithms) LegacyKeys(IConfiguration configuration, string? symmetric)
    {
        if (!configuration.GetValue("Jwt:AcceptLegacySymmetric", false)) return ([], []);
        if (string.IsNullOrWhiteSpace(symmetric))
            throw new InvalidOperationException(
                "Jwt:AcceptLegacySymmetric is on but Jwt:Key is not configured — there is no legacy key to accept.");
        return ([SymmetricKey(symmetric)], [SecurityAlgorithms.HmacSha256]);
    }

    /// <summary>The key set fetched from <c>Jwt:JwksUrl</c>, in the shape the token handler consumes.</summary>
    private sealed class JwksConfiguration : BaseConfiguration;

    /// <summary>
    /// Reads a JWKS document and keeps only what this framework signs with: public EC P-256 signature keys.
    /// Anything else in the document — a symmetric key, an RSA key, a key carrying its private part — is
    /// dropped, so the document cannot widen what is accepted. A document with no usable key is an error,
    /// which makes the manager keep the previous good set instead of replacing it with nothing.
    /// </summary>
    private sealed class JwksRetriever : IConfigurationRetriever<JwksConfiguration>
    {
        public async Task<JwksConfiguration> GetConfigurationAsync(string address, IDocumentRetriever retriever,
            CancellationToken cancel)
        {
            var document = await retriever.GetDocumentAsync(address, cancel).ConfigureAwait(false);
            var result = new JwksConfiguration();
            foreach (var jwk in new JsonWebKeySet(document).Keys)
            {
                if (jwk.Kty != JsonWebAlgorithmsKeyTypes.EllipticCurve || jwk.Crv != JsonWebKeyECTypes.P256) continue;
                if (!string.IsNullOrEmpty(jwk.D) || string.IsNullOrEmpty(jwk.X) || string.IsNullOrEmpty(jwk.Y)) continue;
                if (!string.IsNullOrEmpty(jwk.Use) && jwk.Use != JsonWebKeyUseNames.Sig) continue;
                if (string.IsNullOrEmpty(jwk.Kid)) jwk.KeyId = Base64UrlEncoder.Encode(jwk.ComputeJwkThumbprint());
                result.SigningKeys.Add(jwk);
            }
            if (result.SigningKeys.Count == 0)
                throw new InvalidOperationException($"The key set at {address} contains no usable ES256 public key.");
            return result;
        }
    }

    /// <summary>
    /// Generates a new EC P-256 signing key as an unencrypted PKCS#8 PEM, suitable for
    /// <c>Jwt:SigningKeyPem</c>. Store it as a secret; hand out only <see cref="ExportPublicKeyPem"/>.
    /// </summary>
    /// <returns>The private key PEM.</returns>
    public static string GenerateSigningKeyPem()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return ecdsa.ExportPkcs8PrivateKeyPem();
    }

    /// <summary>
    /// Derives the public key PEM (SubjectPublicKeyInfo) from a signing key PEM — the value a validating
    /// host sets as <c>Jwt:PublicKeyPem</c>.
    /// </summary>
    /// <param name="signingKeyPem">An EC P-256 private key PEM.</param>
    /// <returns>The matching public key PEM.</returns>
    /// <exception cref="InvalidOperationException">The PEM is not a usable EC P-256 private key.</exception>
    public static string ExportPublicKeyPem(string signingKeyPem)
        => EcKey(signingKeyPem, "the signing key", requirePrivate: true).ECDsa.ExportSubjectPublicKeyInfoPem();

    private static SymmetricSecurityKey SymmetricKey(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length < MinSymmetricKeyBytes)
            throw new InvalidOperationException(
                $"Jwt:Key must be at least {MinSymmetricKeyBytes} bytes (got {bytes.Length}) for HMAC-SHA256. " +
                "Generate a 64+ byte random key and store it outside source control.");
        return new SymmetricSecurityKey(bytes);
    }

    internal static ECDsaSecurityKey EcKey(string pem, string setting, bool requirePrivate)
    {
        // A secret store or env var often flattens newlines to the two characters "\n".
        pem = pem.Replace("\\n", "\n", StringComparison.Ordinal).Trim();
        var isPrivatePem = pem.Contains("PRIVATE KEY", StringComparison.Ordinal);
        if (!requirePrivate && isPrivatePem)
            throw new InvalidOperationException(
                $"{setting} holds a PRIVATE key. A validating host must hold only the public key — " +
                "export it with `asdamir secrets new-signing-key` (it prints both) and keep the private key on the issuer.");
        if (requirePrivate && !isPrivatePem)
            throw new InvalidOperationException($"{setting} must be an EC P-256 PRIVATE key in PEM form.");

        var ecdsa = ECDsa.Create();
        try
        {
            ecdsa.ImportFromPem(pem);
        }
        catch (Exception ex) when (ex is ArgumentException or CryptographicException)
        {
            ecdsa.Dispose();
            // The inner exception is deliberately dropped: it can quote key material.
            throw new InvalidOperationException($"{setting} is not a readable EC key in PEM form.");
        }
        if (ecdsa.KeySize != 256)
        {
            var size = ecdsa.KeySize;
            ecdsa.Dispose();
            throw new InvalidOperationException($"{setting} must be a P-256 key for ES256 (got a {size}-bit key).");
        }

        var key = new ECDsaSecurityKey(ecdsa);
        key.KeyId = Base64UrlEncoder.Encode(
            JsonWebKeyConverter.ConvertFromECDsaSecurityKey(PublicOnly(key, withKeyId: false)).ComputeJwkThumbprint());
        return key;
    }

    internal static ECDsaSecurityKey PublicOnly(ECDsaSecurityKey key, bool withKeyId = true)
    {
        var pub = ECDsa.Create(key.ECDsa.ExportParameters(includePrivateParameters: false));
        var result = new ECDsaSecurityKey(pub);
        if (withKeyId) result.KeyId = key.KeyId;
        return result;
    }
}
