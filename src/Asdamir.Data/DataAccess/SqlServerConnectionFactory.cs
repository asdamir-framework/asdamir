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
using Asdamir.Core.Contracts;
using Microsoft.Data.SqlClient;

namespace Asdamir.Data.DataAccess;

/// <summary>
/// The canonical SQL Server implementation of <see cref="IDbConnectionFactory"/>. This factory is the
/// ONE legitimate place to allocate a connection — every other call site injects the interface (AUD002).
/// The interface is multi-provider (SQL Server / Oracle / PostgreSQL); this concrete supports SQL Server
/// (Oracle/PostgreSQL throw <see cref="NotSupportedException"/> until their factories ship).
/// </summary>
public sealed class SqlServerConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    /// <summary>Creates the factory bound to a SQL Server connection string.</summary>
    public SqlServerConnectionFactory(string connectionString)
        => _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));

    /// <inheritdoc/>
    public IDbConnection Create()
        => new SqlConnection(_connectionString); // audit-lint:ignore AUD002 the factory is the one legitimate place to allocate a connection

    /// <inheritdoc/>
    public IDbConnection Create(DbProviderType provider) => provider switch
    {
        DbProviderType.SqlServer => Create(),
        _ => throw new NotSupportedException(
            $"{provider} is not implemented; SqlServerConnectionFactory supports SQL Server only."),
    };

    /// <inheritdoc/>
    public IDbConnection Create(string providerName)
        => string.IsNullOrEmpty(providerName) || string.Equals(providerName, "SqlServer", StringComparison.OrdinalIgnoreCase)
            ? Create()
            : throw new NotSupportedException(
                $"Provider '{providerName}' is not implemented; SqlServerConnectionFactory supports SQL Server only.");

    /// <inheritdoc/>
    public async Task<IDbConnection> CreateAsync(CancellationToken ct = default)
    {
        var conn = new SqlConnection(_connectionString); // audit-lint:ignore AUD002 the factory allocates and returns an OPEN connection
        await conn.OpenAsync(ct);
        return conn;
    }
}
