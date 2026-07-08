// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.Contracts;

/// <summary>
/// At-rest encryption for secrets (AES-256-GCM, versioned <c>v2:</c> ciphertext). Fail-closed: the
/// implementation throws at construction if the encryption key is missing/too short.
/// </summary>
public interface IEncryptionService
{
    /// <summary>Encrypts plaintext to a self-describing (versioned) ciphertext string.</summary>
    string Encrypt(string plainText);
    /// <summary>Decrypts a ciphertext produced by <see cref="Encrypt"/>; throws a generic error on tamper/wrong key.</summary>
    string Decrypt(string cipherText);
    /// <summary>True if the text looks like an Asdamir ciphertext (carries the version prefix).</summary>
    bool IsEncrypted(string text);
}

