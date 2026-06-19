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

public interface IDataProtectionService
{
    string Encrypt(string plainText, string purpose = "default");
    string Decrypt(string cipherText, string purpose = "default");
    Task<byte[]> EncryptPersonalDataAsync(byte[] data, string userId);
    Task<byte[]> DecryptPersonalDataAsync(byte[] encryptedData, string userId);
}

public class DataProtectionService : IDataProtectionService
{
    private readonly IDataProtector _protector;
    private readonly IDataProtectionProvider _provider;
    
    public DataProtectionService(IDataProtectionProvider provider)
    {
        _provider = provider;
        _protector = provider.CreateProtector("Data.Protection");
    }
    
    public string Encrypt(string plainText, string purpose = "default")
    {
        var protector = _provider.CreateProtector($"Framework.{purpose}");
        return protector.Protect(plainText);
    }
    
    public string Decrypt(string cipherText, string purpose = "default")
    {
        var protector = _provider.CreateProtector($"Framework.{purpose}");
        return protector.Unprotect(cipherText);
    }
    
    public Task<byte[]> EncryptPersonalDataAsync(byte[] data, string userId)
    {
        var protector = _provider.CreateProtector($"Framework.PersonalData.{userId}");
        var encrypted = protector.Protect(data);
        return Task.FromResult(encrypted);
    }
    
    public Task<byte[]> DecryptPersonalDataAsync(byte[] encryptedData, string userId)
    {
        var protector = _provider.CreateProtector($"Framework.PersonalData.{userId}");
        var decrypted = protector.Unprotect(encryptedData);
        return Task.FromResult(decrypted);
    }
}
