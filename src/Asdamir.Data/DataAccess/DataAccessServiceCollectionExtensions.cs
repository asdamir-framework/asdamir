// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Asdamir.Data.DataAccess;

/// <summary>
/// DI registration for the canonical <see cref="IDbConnectionFactory"/>. Call <c>AddDataAccess(...)</c>
/// in an API/Gateway tier so repositories and controllers can inject the factory (never `new SqlConnection`).
/// </summary>
public static class DataAccessServiceCollectionExtensions
{
    /// <summary>Registers the canonical SQL Server <see cref="IDbConnectionFactory"/> bound to a connection string.</summary>
    public static IServiceCollection AddDataAccess(this IServiceCollection services, string connectionString)
    {
        services.AddSingleton<IDbConnectionFactory>(_ => new SqlServerConnectionFactory(connectionString));
        return services;
    }

    /// <summary>Reads the connection string from configuration (default name "Default") and registers the factory.</summary>
    public static IServiceCollection AddDataAccess(
        this IServiceCollection services, IConfiguration configuration, string connectionName = "Default")
    {
        var cs = configuration.GetConnectionString(connectionName)
            ?? throw new InvalidOperationException(
                $"ConnectionStrings:{connectionName} is required for AddDataAccess (set it via user-secrets / env).");
        return services.AddDataAccess(cs);
    }
}
