// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Microsoft.AspNetCore.DataProtection;

namespace Asdamir.Web.Security.Services;

/// <summary>
/// Encrypts and decrypts sensitive values using ASP.NET Core Data Protection. Ciphertext is bound to
/// a named purpose (and, for personal data, to a user id), so a payload protected for one purpose/user
/// cannot be unprotected under another. Backed by the app's key ring, so protection survives restarts.
/// </summary>
public interface IDataProtectionService
{
    /// <summary>Protects <paramref name="plainText"/> under the given <paramref name="purpose"/> and returns the ciphertext.</summary>
    /// <param name="plainText">The value to encrypt.</param>
    /// <param name="purpose">Purpose string that scopes the derived key; a value must be decrypted with the same purpose.</param>
    /// <returns>The purpose-bound ciphertext.</returns>
    string Encrypt(string plainText, string purpose = "default");
    /// <summary>Recovers the plaintext for <paramref name="cipherText"/> that was protected under the same <paramref name="purpose"/>.</summary>
    /// <param name="cipherText">Ciphertext previously produced by <c>Encrypt</c>.</param>
    /// <param name="purpose">The purpose the value was encrypted with; a mismatch throws.</param>
    /// <returns>The recovered plaintext.</returns>
    string Decrypt(string cipherText, string purpose = "default");
    /// <summary>Protects a personal-data blob, binding the ciphertext to <paramref name="userId"/> so it can only be decrypted for that user.</summary>
    /// <param name="data">The raw bytes to encrypt.</param>
    /// <param name="userId">User id woven into the protector purpose; scopes the ciphertext to this user.</param>
    /// <returns>The encrypted bytes.</returns>
    Task<byte[]> EncryptPersonalDataAsync(byte[] data, string userId);
    /// <summary>Decrypts a personal-data blob previously protected for the same <paramref name="userId"/>.</summary>
    /// <param name="encryptedData">Ciphertext produced by <c>EncryptPersonalDataAsync</c>.</param>
    /// <param name="userId">The user id the data was protected for; a mismatch throws.</param>
    /// <returns>The recovered raw bytes.</returns>
    Task<byte[]> DecryptPersonalDataAsync(byte[] encryptedData, string userId);
}

/// <summary>
/// Default <see cref="IDataProtectionService"/> built on <see cref="IDataProtectionProvider"/>. Derives a
/// per-purpose (and per-user, for personal data) protector so each protected value is cryptographically
/// isolated to the context it was created in.
/// </summary>
public class DataProtectionService : IDataProtectionService
{
    private readonly IDataProtector _protector;
    private readonly IDataProtectionProvider _provider;

    /// <summary>Initializes the service with the Data Protection provider used to derive purpose-scoped protectors.</summary>
    /// <param name="provider">The Data Protection provider (backed by the app key ring).</param>
    public DataProtectionService(IDataProtectionProvider provider)
    {
        _provider = provider;
        _protector = provider.CreateProtector("Data.Protection");
    }
    
    /// <inheritdoc/>
    public string Encrypt(string plainText, string purpose = "default")
    {
        var protector = _provider.CreateProtector($"Framework.{purpose}");
        return protector.Protect(plainText);
    }

    /// <inheritdoc/>
    public string Decrypt(string cipherText, string purpose = "default")
    {
        var protector = _provider.CreateProtector($"Framework.{purpose}");
        return protector.Unprotect(cipherText);
    }

    /// <inheritdoc/>
    public Task<byte[]> EncryptPersonalDataAsync(byte[] data, string userId)
    {
        var protector = _provider.CreateProtector($"Framework.PersonalData.{userId}");
        var encrypted = protector.Protect(data);
        return Task.FromResult(encrypted);
    }

    /// <inheritdoc/>
    public Task<byte[]> DecryptPersonalDataAsync(byte[] encryptedData, string userId)
    {
        var protector = _provider.CreateProtector($"Framework.PersonalData.{userId}");
        var decrypted = protector.Unprotect(encryptedData);
        return Task.FromResult(decrypted);
    }
}
