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

namespace Asdamir.Tools.Commands;

/// <summary>
/// Computes the starter admin's password hash for the generated AsdamirVault onboarding seed
/// (register_&lt;app&gt;.sql). The hash is what AppManagement's CryptographyService.VerifyPassword
/// reads at login, so the format MUST match it.
/// </summary>
public static class SeedPasswordHasher
{
    // MUST match Asdamir.Core.Services.CryptographyService (Pbkdf2Iterations / SaltSizeBytes /
    // Pbkdf2HashSizeBytes) — changing either side locks the seeded admin out.
    private const int Iterations = 210_000;
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password: password,
            salt: salt,
            iterations: Iterations,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: HashSizeBytes);
        return $"$pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// A random starter password: 16 chars from an unambiguous alphabet (no 0/O/1/l) + a guaranteed
    /// digit and symbol, so it passes typical complexity validators.
    /// </summary>
    public static string GeneratePassword()
    {
        const string alphabet = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
        const string digits = "23456789";
        const string symbols = "!*+-";
        var chars = new char[16];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        chars[^2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        chars[^1] = symbols[RandomNumberGenerator.GetInt32(symbols.Length)];
        return new string(chars);
    }
}
