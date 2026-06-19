// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;
namespace Asdamir.Web.Security.Middleware;


/// <summary>
/// Generates a per-request CSP nonce.
///
/// Audit fix: v1 used <c>Guid.NewGuid().ToByteArray()</c>. Guid v4 is not
/// guaranteed-CSPRNG on every platform/runtime, and even when it is, 6 of the
/// 16 bytes are reserved version/variant bits — leaking 48 bits of structure
/// to an attacker who scrapes the inline-script nonce. CSP nonces MUST be
/// CSPRNG (W3C Trusted Types &amp; CSP3 §6.1): if a nonce is predictable an
/// attacker can pre-inject a matching script tag.
/// </summary>
public sealed class CspNonceMiddleware
{
    public const string NonceItemsKey = "CspNonce";
    private const int NonceByteLength = 16; // 128 bits — exceeds W3C recommendation of 128 bits min.

    private readonly RequestDelegate _next;

    public CspNonceMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        Span<byte> buffer = stackalloc byte[NonceByteLength];
        RandomNumberGenerator.Fill(buffer);
        var nonce = Convert.ToBase64String(buffer);
        context.Items[NonceItemsKey] = nonce;
        await _next(context);
    }
}


