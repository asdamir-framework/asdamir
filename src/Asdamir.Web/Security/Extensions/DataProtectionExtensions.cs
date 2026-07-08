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

/// <summary>
/// Typed convenience wrappers over <see cref="IDataProtectionService"/> that JSON-serialize an object before
/// protecting it and deserialize it after unprotecting — so callers can round-trip a payload instead of a raw string.
/// </summary>
public static class DataProtectionTypedExtensions
{
    /// <summary>Serializes <paramref name="value"/> to JSON and encrypts it under the given purpose.</summary>
    /// <typeparam name="T">The payload type to serialize.</typeparam>
    /// <param name="service">The data-protection service.</param>
    /// <param name="value">The value to protect.</param>
    /// <param name="purpose">The purpose string binding the ciphertext to a use (see <see cref="PurposeStrings"/>).</param>
    /// <returns>The protected (encrypted) payload string.</returns>
    public static string ProtectObject<T>(this IDataProtectionService service, T value, string purpose)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value);
        return service.Encrypt(json, purpose);
    }

    /// <summary>Decrypts <paramref name="payload"/> under the given purpose and deserializes it back to <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The payload type to deserialize.</typeparam>
    /// <param name="service">The data-protection service.</param>
    /// <param name="payload">The protected payload produced by <c>ProtectObject</c>.</param>
    /// <param name="purpose">The purpose string; must match the one used to protect (see <see cref="PurposeStrings"/>).</param>
    /// <returns>The deserialized value, or <c>null</c> if the JSON deserializes to null.</returns>
    public static T? UnprotectObject<T>(this IDataProtectionService service, string payload, string purpose)
    {
        var json = service.Decrypt(payload, purpose);
        return System.Text.Json.JsonSerializer.Deserialize<T>(json);
    }
}

/// <summary>
/// Canonical Data Protection purpose strings. A purpose isolates a ciphertext to one use so a token protected
/// for one context cannot be unprotected in another; always pass the matching constant to protect and unprotect.
/// </summary>
public static class PurposeStrings
{
    /// <summary>Purpose for anti-forgery (CSRF) tokens.</summary>
    public const string AntiForgery = "security:antiforgery";

    /// <summary>Purpose for email confirmation / action tokens.</summary>
    public const string EmailToken = "security:email-token";

    /// <summary>Purpose for refresh tokens.</summary>
    public const string RefreshToken = "security:refresh-token";
}


