// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Blazored.LocalStorage;

namespace Asdamir.Web.Security.Services;

/// <summary>
/// Framework service for managing correlation IDs across HTTP requests
/// </summary>
public sealed class CorrelationIdProvider
{
    private const string StorageKey = "correlation-id";
    private readonly ILocalStorageService _localStorage;
    private string? _cached;

    /// <summary>Initializes the provider with the browser local-storage service used to persist the correlation id.</summary>
    /// <param name="localStorage">Local storage used to read/write the stable per-browser correlation id.</param>
    public CorrelationIdProvider(ILocalStorageService localStorage)
    {
        _localStorage = localStorage;
    }

    /// <summary>Returns the browser's stable correlation id, creating and persisting a new one on first use. Cached in memory; tolerant of local-storage being unavailable (e.g. during prerender).</summary>
    /// <returns>The correlation id (a 32-char hex GUID) to attach to outgoing requests for tracing.</returns>
    public async Task<string> GetOrCreateAsync()
    {
        if (!string.IsNullOrWhiteSpace(_cached))
            return _cached!;

        string? id = null;
        try
        {
            id = await _localStorage.GetItemAsStringAsync(StorageKey);
        }
        catch (InvalidOperationException)
        {
            id = _cached;
        }
        if (string.IsNullOrWhiteSpace(id))
        {
            id = Guid.NewGuid().ToString("N");
            try { await _localStorage.SetItemAsStringAsync(StorageKey, id); }
            catch (InvalidOperationException) { }
        }

        _cached = id!;
        return id!;
    }
}