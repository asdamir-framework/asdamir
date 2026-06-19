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

namespace Asdamir.Tools.Commands;

/// <summary>
/// Standalone re-implementation of Core's <c>EncryptionService</c> crypto, used by the <c>secrets</c>
/// command. Kept here (instead of referencing Asdamir.Core) so the published <c>asdamir</c> dotnet tool
/// stays small — a ProjectReference to Core dragged its whole runtime graph (libphonenumber ~5 MB,
/// FluentValidation, IdentityModel/Azure, Polly, Serilog…) into the tool package, ~8 MB that stalled the
/// GitHub Packages push.
///
/// The ciphertext format is byte-for-byte Core's: v2 = <c>"v2:" + base64(nonce[12] || ciphertext ||
/// tag[16])</c> (AES-256-GCM), key = PBKDF2-SHA256(passphrase, salt, 200_000) where salt is
/// <c>Security:EncryptionSalt</c> or <c>SHA256("Asdamir:salt:" + key)</c>. Legacy v1 (AES-CBC) is still
/// decryptable for rotating old data. A cross-compat test (Tools.Tests ↔ Core.EncryptionService) pins this.
/// </summary>
public sealed class SecretCrypto
{
    private const string V2Prefix = "v2:";
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;
    private const int LegacyIvSizeBytes = 16;
    private const int Pbkdf2Iterations = 200_000;

    private readonly byte[] _key;

    public SecretCrypto(string encryptionKey, string? encryptionSalt = null)
    {
        if (string.IsNullOrEmpty(encryptionKey))
            throw new ArgumentException("Encryption key is required.", nameof(encryptionKey));
        if (encryptionKey.Length < 32)
            throw new ArgumentException("Encryption key must be at least 32 characters to derive a strong 256-bit key.", nameof(encryptionKey));

        byte[] salt;
        if (!string.IsNullOrWhiteSpace(encryptionSalt) && encryptionSalt.Length >= 16)
        {
            salt = Encoding.UTF8.GetBytes(encryptionSalt);
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
            outputLength: 32);
    }

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return plainText;

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
        if (string.IsNullOrEmpty(cipherText)) return cipherText;

        try
        {
            return cipherText.StartsWith(V2Prefix, StringComparison.Ordinal)
                ? DecryptV2(cipherText.AsSpan(V2Prefix.Length))
                : DecryptLegacyCbc(cipherText);
        }
        catch (CryptographicException ex)
        {
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
}
