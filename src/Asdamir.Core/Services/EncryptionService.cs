// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;

namespace Asdamir.Core.Contracts;

/// <summary>
/// AEAD symmetric encryption using AES-256-GCM. New ciphertexts carry a "v2:" prefix
/// so the decrypt path can distinguish them from legacy AES-CBC output and migrate
/// transparently.
///
/// Audit fix (CRITICAL):
///  - v1 used AES-CBC + PKCS7 with no MAC. Malleable; vulnerable to padding-oracle
///    attacks when error paths leak distinguishable exceptions.
///  - v1 derived the AES key as SHA256(passphrase) — a single hash round, not a KDF.
///    Low-entropy passphrases were brute-forceable.
///
/// v2 uses:
///  - AES-GCM (built-in authenticated encryption; tag detects tampering).
///  - PBKDF2-SHA256 with 200 000 iterations over a per-deployment salt to derive the
///    256-bit key. The salt comes from <c>Security:EncryptionSalt</c> when set, else
///    a deterministic SHA-256 of the passphrase (so existing deployments don't need
///    extra configuration; new deployments should set the salt for full hygiene).
///
/// Ciphertext format:
///   "v2:" + base64( nonce[12] || ciphertext || tag[16] )
///
/// Legacy ciphertexts (raw base64 of iv[16] || ciphertext) are still decryptable
/// during a migration window so existing stored values keep working. On the next
/// re-encrypt, the value is rewritten as v2.
/// </summary>
public class EncryptionService : IEncryptionService
{
    private const string V2Prefix = "v2:";
    private const int NonceSizeBytes = 12;       // AES-GCM standard nonce
    private const int TagSizeBytes = 16;
    private const int LegacyIvSizeBytes = 16;    // AES-CBC IV size (v1)
    private const int Pbkdf2Iterations = 200_000;

    private readonly byte[] _key;

    public EncryptionService(IConfiguration configuration)
    {
        var encryptionKey = configuration["Security:EncryptionKey"]
            ?? throw new InvalidOperationException("Security:EncryptionKey not configured");

        if (encryptionKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Security:EncryptionKey must be at least 32 characters to derive a strong 256-bit key.");
        }

        // Salt: prefer an explicit value, fall back to a deterministic SHA-256 of the
        // passphrase so existing deployments continue to function without new config.
        var saltSource = configuration["Security:EncryptionSalt"];
        byte[] salt;
        if (!string.IsNullOrWhiteSpace(saltSource) && saltSource.Length >= 16)
        {
            salt = Encoding.UTF8.GetBytes(saltSource);
        }
        else
        {
            using var sha = SHA256.Create();
            salt = sha.ComputeHash(Encoding.UTF8.GetBytes("Asdamir:salt:" + encryptionKey));
        }

        _key = Rfc2898DeriveBytes.Pbkdf2(
            password: Encoding.UTF8.GetBytes(encryptionKey),
            salt: salt,
            iterations: Pbkdf2Iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: 32); // 256-bit key
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSizeBytes];

        using var aes = new AesGcm(_key, TagSizeBytes);
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);

        var output = new byte[NonceSizeBytes + cipherBytes.Length + TagSizeBytes];
        Buffer.BlockCopy(nonce, 0, output, 0, NonceSizeBytes);
        Buffer.BlockCopy(cipherBytes, 0, output, NonceSizeBytes, cipherBytes.Length);
        Buffer.BlockCopy(tag, 0, output, NonceSizeBytes + cipherBytes.Length, TagSizeBytes);

        return V2Prefix + Convert.ToBase64String(output);
    }

    public string Decrypt(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText))
            return cipherText;

        try
        {
            return cipherText.StartsWith(V2Prefix, StringComparison.Ordinal)
                ? DecryptV2(cipherText.AsSpan(V2Prefix.Length))
                : DecryptLegacyCbc(cipherText);
        }
        catch (CryptographicException ex)
        {
            // GCM auth-tag failure or CBC padding error: surface a generic exception so
            // callers can't distinguish "wrong key" from "tampered ciphertext".
            throw new InvalidOperationException("Failed to decrypt value.", ex);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Ciphertext is not valid base64.", ex);
        }
    }

    private string DecryptV2(ReadOnlySpan<char> base64Body)
    {
        var combined = Convert.FromBase64String(base64Body.ToString());
        if (combined.Length < NonceSizeBytes + TagSizeBytes)
            throw new InvalidOperationException("v2 ciphertext is too short.");

        var nonce = new byte[NonceSizeBytes];
        Buffer.BlockCopy(combined, 0, nonce, 0, NonceSizeBytes);

        var cipherLen = combined.Length - NonceSizeBytes - TagSizeBytes;
        var cipherBytes = new byte[cipherLen];
        Buffer.BlockCopy(combined, NonceSizeBytes, cipherBytes, 0, cipherLen);

        var tag = new byte[TagSizeBytes];
        Buffer.BlockCopy(combined, NonceSizeBytes + cipherLen, tag, 0, TagSizeBytes);

        var plainBytes = new byte[cipherLen];
        using var aes = new AesGcm(_key, TagSizeBytes);
        aes.Decrypt(nonce, cipherBytes, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }

    private string DecryptLegacyCbc(string cipherText)
    {
        // Backward-compat path so v1-encrypted values keep working during migration.
        // Callers should re-encrypt on next save; new values use v2 format.
        var combined = Convert.FromBase64String(cipherText);
        if (combined.Length <= LegacyIvSizeBytes)
            throw new InvalidOperationException("Legacy ciphertext is too short.");

        var iv = new byte[LegacyIvSizeBytes];
        Buffer.BlockCopy(combined, 0, iv, 0, LegacyIvSizeBytes);
        var cipherBytes = new byte[combined.Length - LegacyIvSizeBytes];
        Buffer.BlockCopy(combined, LegacyIvSizeBytes, cipherBytes, 0, cipherBytes.Length);

        using var aes = Aes.Create();
        aes.Key = _key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }

    public bool IsEncrypted(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        if (text.StartsWith(V2Prefix, StringComparison.Ordinal))
            return true;

        // Legacy CBC: looks-like-base64 + at least one block of payload.
        try
        {
            var buffer = Convert.FromBase64String(text);
            return buffer.Length > LegacyIvSizeBytes;
        }
        catch
        {
            return false;
        }
    }
}
