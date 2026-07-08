// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Asdamir.Core.Models;

namespace Asdamir.Core.Contracts;

/// <summary>DB-backed application-configuration store (<c>dbo.AppConfigurations</c>): read/write runtime settings by key.</summary>
public interface IAppConfigurationRepository
{
    /// <summary>All configuration entries.</summary>
    Task<List<AppConfiguration>> GetAllAsync(CancellationToken cancellationToken = default);
    /// <summary>The raw value for a key, or null if the key is absent.</summary>
    Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default);
    /// <summary>The full configuration entry for a key, or null.</summary>
    Task<AppConfiguration?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
    /// <summary>Creates a configuration entry and returns it.</summary>
    Task<AppConfiguration> CreateAsync(AppConfiguration config, CancellationToken cancellationToken = default);
    /// <summary>Updates a configuration entry and returns it.</summary>
    Task<AppConfiguration> UpdateAsync(AppConfiguration config, CancellationToken cancellationToken = default);
    /// <summary>Deletes the configuration entry with the given key.</summary>
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
