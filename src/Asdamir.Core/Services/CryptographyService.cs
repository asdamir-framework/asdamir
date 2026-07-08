// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Asdamir.Core.Services;

/// <summary>
/// Şifreleme, password hash ve genel hash işlemleri için servis.
///
/// PASSWORD HASH:
///   Format: <c>$pbkdf2-sha256$&lt;iterations&gt;$&lt;salt-b64&gt;$&lt;hash-b64&gt;</c>
///   - PBKDF2-HMAC-SHA256, 210.000 iter (OWASP 2025 önerisi)
///   - Per-record 16 byte rastgele salt
///   - SHA1 desteklenmez (audit fix: zayıf algoritma kaldırıldı)
///
/// SİMETRİK ŞİFRELEME (Encrypt/Decrypt):
///   AES-256-CBC, **per-call rastgele IV** (ciphertext'in başına eklenir).
///   Önceki sürümde IV PBKDF2'den deterministik türeyiyordu — bu, aynı plaintext
///   her zaman aynı ciphertext üretmesine neden oluyordu (semantik güvenlik ihlali).
/// </summary>
public class CryptographyService
{
    /// <summary>OWASP 2025 minimum PBKDF2-SHA256 iteration count.</summary>
    public const int Pbkdf2Iterations = 210_000;

    /// <summary>Per-record salt length in bytes.</summary>
    public const int SaltSizeBytes = 16;

    /// <summary>Derived password hash length in bytes (256-bit).</summary>
    public const int Pbkdf2HashSizeBytes = 32;

    /// <summary>AES block size in bytes.</summary>
    private const int AesIvSizeBytes = 16;

    /// <summary>AES key size in bytes (256-bit).</summary>
    private const int AesKeySizeBytes = 32;

    private readonly string _encryptionKey;

    /// <summary>
    /// Creates the service with the symmetric passphrase used to derive the AES-256 key for
    /// <c>Encrypt</c>/<c>Decrypt</c>. The passphrase is kept in memory only; never log it.
    /// </summary>
    /// <param name="encryptionKey">Master passphrase; must be at least 32 characters.</param>
    /// <exception cref="ArgumentNullException">The key is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentException">The key is shorter than 32 characters.</exception>
    public CryptographyService(string encryptionKey)
    {
        if (string.IsNullOrWhiteSpace(encryptionKey))
            throw new ArgumentNullException(nameof(encryptionKey), "Encryption key cannot be null or empty.");

        if (encryptionKey.Length < 32)
            throw new ArgumentException(
                "Encryption key must be at least 32 characters for AES-256 derivation.",
                nameof(encryptionKey));

        _encryptionKey = encryptionKey;
    }

    #region Hash Methods

    /// <summary>Hash algorithm selector for the generic <see cref="ComputeHash"/> (content hashing only, not passwords).</summary>
    public enum HashAlgorithm
    {
        /// <summary>SHA-256 (default).</summary>
        SHA256,
        /// <summary>SHA-384.</summary>
        SHA384,
        /// <summary>SHA-512.</summary>
        SHA512
    }

    /// <summary>
    /// Generic content hash (NOT for passwords — use HashPassword/VerifyPassword for credentials).
    /// Plain SHA-2 with no salt; unsuitable for anything low-entropy or secret.
    /// </summary>
    /// <param name="plainText">Content to hash; empty input returns an empty string.</param>
    /// <param name="algorithm">SHA-2 variant to use (default SHA-256).</param>
    /// <returns>Base64-encoded digest.</returns>
    public string ComputeHash(string plainText, HashAlgorithm algorithm = HashAlgorithm.SHA256)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        using var hashAlgorithm = CreateHashAlgorithm(algorithm);
        var inputBytes = Encoding.UTF8.GetBytes(plainText);
        var hashBytes = hashAlgorithm.ComputeHash(inputBytes);
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>Convenience SHA-256 content hash. Not for passwords (unsalted, fast).</summary>
    /// <param name="plainText">Content to hash.</param>
    /// <returns>Base64-encoded SHA-256 digest.</returns>
    public string ComputeSHA256Hash(string plainText) => ComputeHash(plainText, HashAlgorithm.SHA256);

    private static System.Security.Cryptography.HashAlgorithm CreateHashAlgorithm(HashAlgorithm algorithm) =>
        algorithm switch
        {
            HashAlgorithm.SHA256 => SHA256.Create(),
            HashAlgorithm.SHA384 => SHA384.Create(),
            HashAlgorithm.SHA512 => SHA512.Create(),
            _ => SHA256.Create()
        };

    #endregion

    #region Password Hash (PBKDF2-SHA256)

    /// <summary>
    /// Hashes a password using PBKDF2-HMAC-SHA256 (210k iterations) with a fresh 16-byte random salt.
    /// Returns a self-describing string: $pbkdf2-sha256$&lt;iter&gt;$&lt;salt-b64&gt;$&lt;hash-b64&gt;.
    /// Store only this string; never persist or log the plaintext password.
    /// </summary>
    /// <param name="password">Plaintext password to hash.</param>
    /// <returns>The self-describing PBKDF2 hash string, safe to store.</returns>
    /// <exception cref="ArgumentException">The password is null or empty.</exception>
    public string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be null or empty.", nameof(password));

        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password: Encoding.UTF8.GetBytes(password),
            salt: salt,
            iterations: Pbkdf2Iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: Pbkdf2HashSizeBytes);

        return $"$pbkdf2-sha256${Pbkdf2Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Verifies a password against a hash produced by <see cref="HashPassword"/>, re-deriving with
    /// the salt/iterations embedded in the stored hash and comparing in constant time
    /// (<c>CryptographicOperations.FixedTimeEquals</c>) to avoid timing leaks.
    /// Returns false (never throws) for a malformed or unrecognised hash.
    /// </summary>
    /// <param name="password">Candidate plaintext password.</param>
    /// <param name="storedHash">The stored PBKDF2 hash string to verify against.</param>
    /// <returns><c>true</c> if the password matches; otherwise <c>false</c>.</returns>
    public bool VerifyPassword(string password, string storedHash)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(storedHash))
            return false;

        var parts = storedHash.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || parts[0] != "pbkdf2-sha256")
            return false;

        if (!int.TryParse(parts[1], out var iterations) || iterations < 1000)
            return false;

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            password: Encoding.UTF8.GetBytes(password),
            salt: salt,
            iterations: iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>
    /// Returns true if the stored hash uses fewer than the current recommended iteration count,
    /// or is in an unknown/legacy format, and should be rehashed on the next successful login.
    /// </summary>
    /// <param name="storedHash">The stored PBKDF2 hash string to inspect.</param>
    /// <returns><c>true</c> if the hash should be upgraded; otherwise <c>false</c>.</returns>
    public bool NeedsRehash(string storedHash)
    {
        if (string.IsNullOrEmpty(storedHash))
            return true;

        var parts = storedHash.Split('$', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4 || parts[0] != "pbkdf2-sha256")
            return true; // unknown / legacy format → rehash

        if (!int.TryParse(parts[1], out var iter))
            return true;

        return iter < Pbkdf2Iterations;
    }

    #endregion

    #region Encryption / Decryption Methods (AES-256-GCM with random nonce; PBKDF2-derived key)

    private const string EncryptedV2Prefix = "v2:";
    private const int GcmNonceSize = 12;
    private const int GcmTagSize = 16;
    private const int KdfIterations = 200_000;

    /// <summary>
    /// Encrypts plaintext with AES-256-GCM. Output is <c>"v2:" + base64(nonce || ciphertext || tag)</c>.
    ///
    /// Audit fix: previously used AES-CBC + PKCS7 with NO MAC (malleable; padding-oracle
    /// vulnerable) and derived the AES key as a single SHA-256 of the passphrase
    /// (not a real KDF). v2 uses AEAD + PBKDF2. Never log the plaintext.
    /// </summary>
    /// <param name="plainText">Value to encrypt; null/empty is returned unchanged.</param>
    /// <returns>The versioned <c>"v2:"</c> ciphertext string.</returns>
    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return plainText;

        var key = DeriveKey();
        var nonce = RandomNumberGenerator.GetBytes(GcmNonceSize);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[GcmTagSize];

        using var aes = new AesGcm(key, GcmTagSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        var output = new byte[GcmNonceSize + cipher.Length + GcmTagSize];
        Buffer.BlockCopy(nonce, 0, output, 0, GcmNonceSize);
        Buffer.BlockCopy(cipher, 0, output, GcmNonceSize, cipher.Length);
        Buffer.BlockCopy(tag, 0, output, GcmNonceSize + cipher.Length, GcmTagSize);
        return EncryptedV2Prefix + Convert.ToBase64String(output);
    }

    /// <summary>
    /// Decrypts a value produced by <see cref="Encrypt"/>. The GCM auth tag detects tampering.
    /// Legacy CBC ciphertexts (no v2 prefix) are decrypted via the migration path so existing
    /// stored values keep working; callers should re-encrypt on next save.
    /// </summary>
    /// <param name="encryptedText">Ciphertext to decrypt; null/empty is returned unchanged.</param>
    /// <returns>The recovered plaintext.</returns>
    /// <exception cref="InvalidOperationException">The ciphertext is malformed, or the key is wrong / the payload was tampered with.</exception>
    public string Decrypt(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
            return encryptedText;

        if (encryptedText.StartsWith(EncryptedV2Prefix, StringComparison.Ordinal))
        {
            return DecryptV2(encryptedText.AsSpan(EncryptedV2Prefix.Length));
        }

        // Legacy CBC migration path.
        byte[] combined;
        try
        {
            combined = Convert.FromBase64String(encryptedText);
        }
        catch (FormatException)
        {
            // Audit-tolerant call-sites used to rely on "returns input on bad base64".
            // We preserve that bug-compatible behaviour for non-v2 inputs because
            // some callers pass already-plaintext values. v2 prefix is the safe signal
            // that the caller intended an encrypted payload.
            return encryptedText;
        }

        if (combined.Length <= AesIvSizeBytes)
            throw new InvalidOperationException("Ciphertext is too short to contain an IV.");

        var iv = new byte[AesIvSizeBytes];
        Buffer.BlockCopy(combined, 0, iv, 0, AesIvSizeBytes);
        var legacyCipher = new byte[combined.Length - AesIvSizeBytes];
        Buffer.BlockCopy(combined, AesIvSizeBytes, legacyCipher, 0, legacyCipher.Length);

        var key = DeriveKey();
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.BlockSize = 128;
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        try
        {
            using var decryptor = aes.CreateDecryptor();
            var plain = decryptor.TransformFinalBlock(legacyCipher, 0, legacyCipher.Length);
            return Encoding.UTF8.GetString(plain);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException("Decryption failed. Invalid key or corrupted data.", ex);
        }
    }

    private string DecryptV2(ReadOnlySpan<char> base64Body)
    {
        byte[] combined;
        try
        {
            combined = Convert.FromBase64String(base64Body.ToString());
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("v2 ciphertext is not valid base64.", ex);
        }
        if (combined.Length < GcmNonceSize + GcmTagSize)
            throw new InvalidOperationException("v2 ciphertext is too short.");

        var nonce = new byte[GcmNonceSize];
        Buffer.BlockCopy(combined, 0, nonce, 0, GcmNonceSize);

        var cipherLen = combined.Length - GcmNonceSize - GcmTagSize;
        var cipher = new byte[cipherLen];
        Buffer.BlockCopy(combined, GcmNonceSize, cipher, 0, cipherLen);

        var tag = new byte[GcmTagSize];
        Buffer.BlockCopy(combined, GcmNonceSize + cipherLen, tag, 0, GcmTagSize);

        var plain = new byte[cipherLen];
        var key = DeriveKey();
        try
        {
            using var aes = new AesGcm(key, GcmTagSize);
            aes.Decrypt(nonce, cipher, tag, plain);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidOperationException("Decryption failed. Invalid key or tampered ciphertext.", ex);
        }
        return Encoding.UTF8.GetString(plain);
    }

    private byte[]? _derivedKeyCache;

    private byte[] DeriveKey()
    {
        // Cached: PBKDF2 is expensive (~200ms first time at 200k iterations). We hold
        // the derived key in a private field — same lifetime as the service instance.
        // CryptographyService is typically registered scoped, so the cache is per request
        // scope, not process-wide.
        if (_derivedKeyCache is not null) return _derivedKeyCache;

        // Salt: derive a per-deployment salt from the passphrase. Production deployments
        // should override via `Security:EncryptionSalt` if available; that's wired in
        // EncryptionService. Here we keep CryptographyService independent of IConfiguration
        // so the salt is deterministic from the passphrase.
        using var sha = SHA256.Create();
        var salt = sha.ComputeHash(Encoding.UTF8.GetBytes("Core:CryptographyService:" + _encryptionKey));

        _derivedKeyCache = Rfc2898DeriveBytes.Pbkdf2(
            password: Encoding.UTF8.GetBytes(_encryptionKey),
            salt: salt,
            iterations: KdfIterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: 32); // 256-bit AES key
        return _derivedKeyCache;
    }

    #endregion

    #region Session ID Generation

    /// <summary>
    /// Derives a session identifier as a SHA-256 over the caller context plus a UTC timestamp and
    /// 16 bytes of CSPRNG entropy, so two calls never collide (guards against session collision / replay).
    /// </summary>
    /// <param name="unitId">Originating unit/terminal identifier.</param>
    /// <param name="userName">User the session belongs to.</param>
    /// <param name="proxyName">Proxy/gateway name in the request path.</param>
    /// <param name="networkCard">Client network-card identifier.</param>
    /// <returns>Base64-encoded SHA-256 session id.</returns>
    public string GenerateSessionId(string unitId, string userName, string proxyName, string networkCard)
    {
        // Audit fix: v1 hashed only date (yyyyMMdd) — same user+unit+proxy produced the
        // same session id for every request in a 24-hour window (trivial session
        // collision / replay risk). v2 mixes in 16 bytes of CSPRNG entropy.
        var entropy = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        var sessionData = $"{unitId}|{userName}|{DateTime.UtcNow:O}|{proxyName}|{networkCard}|{entropy}";
        return ComputeSHA256Hash(sessionData);
    }

    #endregion
}
