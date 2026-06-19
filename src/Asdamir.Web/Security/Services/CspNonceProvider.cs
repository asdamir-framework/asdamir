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
using Asdamir.Web.Security.Middleware;
namespace Asdamir.Web.Security.Services;


public sealed class CspNonceProvider : ICspNonceProvider
{
    private readonly IHttpContextAccessor _http;

    public CspNonceProvider(IHttpContextAccessor http)
    {
        _http = http;
    }

    public string? GetNonce()
    {
        return _http.HttpContext?.Items.TryGetValue(CspNonceMiddleware.NonceItemsKey, out var value) == true
            ? value as string
            : null;
    }
}


