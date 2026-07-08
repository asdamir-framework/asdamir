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

/// <summary>Reads DB-backed application settings (the AppId-scoped <c>AppConfigurations</c> store).</summary>
public interface IAppConfigurationService
{
    /// <summary>The value for a configuration key, or null if it is absent/inactive.</summary>
    Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default);
    /// <summary>Every active configuration row.</summary>
    Task<List<Asdamir.Core.Models.AppConfiguration>> GetAllAsync(CancellationToken cancellationToken = default);
}
