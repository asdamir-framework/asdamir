// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Asdamir.Core.Contracts;

/// <summary>
/// The framework's single canonical factory for database connections (multi-provider:
/// SQL Server / Oracle / PostgreSQL). Inject this everywhere data access happens — never
/// allocate a connection directly (audit-lint AUD002). The concrete factory is the one
/// legitimate place to construct a connection.
///
/// <para>Two creation paths, both first-class:</para>
/// <list type="bullet">
///   <item><see cref="CreateAsync"/> — the preferred RUNTIME path: returns an already-OPEN
///   connection and is cancellation-aware. Used by repositories/controllers under DI.</item>
///   <item><see cref="Create()"/> (+ provider overloads) — the synchronous BOOTSTRAP path:
///   returns a closed connection for use before the host/DI exists (e.g. loading
///   <c>AppConfigurations</c> into configuration via <c>AddDatabaseConfiguration</c>), and for
///   explicit provider selection.</item>
/// </list>
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>Creates a (closed) connection using the default provider — synchronous bootstrap path.</summary>
    IDbConnection Create();

    /// <summary>Creates a (closed) connection for a specific provider type.</summary>
    IDbConnection Create(DbProviderType provider);

    /// <summary>Creates a (closed) connection for a specific provider name.</summary>
    IDbConnection Create(string providerName);

    /// <summary>Creates and OPENS a connection using the default provider — preferred runtime path.</summary>
    Task<IDbConnection> CreateAsync(CancellationToken ct = default);
}
