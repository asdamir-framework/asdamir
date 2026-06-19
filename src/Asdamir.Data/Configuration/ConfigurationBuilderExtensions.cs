// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Dapper;
using System.Data.Common;
using Asdamir.Core.Contracts;
using Microsoft.Extensions.Configuration;

namespace Asdamir.Data.Configuration;

// The connection-factory abstraction is the single canonical Asdamir.Core.Contracts.IDbConnectionFactory.
// (The former duplicate Asdamir.Data.Configuration.IDbConnectionFactory was removed — one source of truth.)

public static class ConfigurationBuilderExtensions
{
    /// <summary>
    /// Add dynamic configuration source from database using IDbConnectionFactory
    /// </summary>
    public static IConfigurationBuilder AddDatabaseConfiguration(
        this IConfigurationBuilder builder,
        IDbConnectionFactory connectionFactory,
        TimeSpan? refreshInterval = null)
    {
        var source = new DynamicConfigurationSource
        {
            Loader = async (ct) =>
            {
                try
                {
                    using var connection = connectionFactory.Create();
                    await ((DbConnection)connection).OpenAsync(ct);

                    var sql = "SELECT [Key], [Value] FROM dbo.AppConfigurations WHERE IsActive = 1";
                    var rows = await connection.QueryAsync<ConfigurationRow>(sql);

                    var config = new Dictionary<string, string?>();
                    foreach (var row in rows)
                    {
                        config[row.Key] = row.Value;
                    }

                    return config;
                }
                catch (Exception ex)
                {
                    // Log error and return empty dictionary to allow app to start
                    Console.WriteLine($"[CONFIG ERROR] Failed to load from database: {ex.Message}");
                    return new Dictionary<string, string?>();
                }
            },
            RefreshInterval = refreshInterval ?? TimeSpan.FromMinutes(5)
        };

        builder.Add(source);
        return builder;
    }

    private class ConfigurationRow
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
