// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Web.Security.Services;
namespace Asdamir.Web.Security.Extensions;

using Microsoft.AspNetCore.DataProtection;

public static class DataProtectionTypedExtensions
{
    public static string ProtectObject<T>(this IDataProtectionService service, T value, string purpose)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value);
        return service.Encrypt(json, purpose);
    }

    public static T? UnprotectObject<T>(this IDataProtectionService service, string payload, string purpose)
    {
        var json = service.Decrypt(payload, purpose);
        return System.Text.Json.JsonSerializer.Deserialize<T>(json);
    }
}

public static class PurposeStrings
{
    public const string AntiForgery = "security:antiforgery";
    public const string EmailToken = "security:email-token";
    public const string RefreshToken = "security:refresh-token";
}


