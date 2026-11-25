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
using Microsoft.Extensions.Logging;

namespace Asdamir.Data.Configuration;

// The connection-factory abstraction is the single canonical Asdamir.Core.Contracts.IDbConnectionFactory.
// (The former duplicate Asdamir.Data.Configuration.IDbConnectionFactory was removed — one source of truth.)

/// <summary>
/// <see cref="IConfigurationBuilder"/> extensions that add the DB-backed configuration source
/// (<c>AppConfigurations</c>) so settings live in the database, not just appsettings.
/// </summary>
public static class ConfigurationBuilderExtensions
{
    /// <summary>
    /// Add the DB-backed configuration source (<c>dbo.AppConfigurations</c>, <c>IsActive = 1</c>) using an
    /// <see cref="IDbConnectionFactory"/>, <b>unscoped</b>: every active row is loaded, ordered by key so the
    /// result is deterministic. Use this only when the table belongs to this host alone; a host that shares
    /// the table with other applications must use the overload taking
    /// <see cref="DatabaseConfigurationOptions"/> and set <see cref="DatabaseConfigurationOptions.AppId"/>.
    /// The default <see cref="DatabaseConfigurationOptions.ProtectedKeys"/> apply: a database row can no
    /// longer supply <c>Jwt:Key</c>, <c>Security:EncryptionKey</c>, <c>ConnectionStrings:*</c> or
    /// <c>Persistence:UseInMemory</c>. The first load is synchronous, and a failed load keeps the
    /// last-known-good values (see <see cref="DynamicConfigurationProvider"/>).
    /// </summary>
    public static IConfigurationBuilder AddDatabaseConfiguration(
        this IConfigurationBuilder builder,
        IDbConnectionFactory connectionFactory,
        TimeSpan? refreshInterval = null)
        => builder.AddDatabaseConfiguration(connectionFactory, o =>
        {
            if (refreshInterval.HasValue) o.RefreshInterval = refreshInterval.Value;
        });

    /// <summary>
    /// Add the DB-backed configuration source (<c>dbo.AppConfigurations</c>, <c>IsActive = 1</c>) with
    /// explicit <see cref="DatabaseConfigurationOptions"/>: the application scope to load
    /// (<see cref="DatabaseConfigurationOptions.AppId"/> — own rows win over global <c>AppId IS NULL</c>
    /// rows, other applications' rows are never read), the keys a database row may never supply
    /// (<see cref="DatabaseConfigurationOptions.ProtectedKeys"/>) and the refresh interval. The first load
    /// is synchronous; a failed load throws inside the provider, which keeps the last-known-good values
    /// and retries — it never replaces good data with an empty set.
    /// </summary>
    public static IConfigurationBuilder AddDatabaseConfiguration(
        this IConfigurationBuilder builder,
        IDbConnectionFactory connectionFactory,
        Action<DatabaseConfigurationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new DatabaseConfigurationOptions();
        configure(options);

        var source = new DynamicConfigurationSource
        {
            RefreshInterval = options.RefreshInterval,
            LoggerFactory = options.LoggerFactory,
        };

        // No try/catch here on purpose: a failure must reach the provider, which keeps the
        // last-known-good data, retries and logs. Swallowing it and returning an empty dictionary is
        // what used to wipe every DB-backed setting on a transient SQL error.
        source.Loader = async ct =>
        {
            using var connection = connectionFactory.Create();
            await ((DbConnection)connection).OpenAsync(ct);

            var rows = await connection.QueryAsync<ConfigurationRow>(new CommandDefinition(
                BuildSql(options), new { appId = options.AppId }, cancellationToken: ct));

            var dict = BuildDictionary(rows, options, out var dropped);
            source.LastDroppedProtectedKeys = dropped;   // reported by the provider once a logger exists
            return dict;
        };

        builder.Add(source);
        return builder;
    }

    /// <summary>
    /// The SELECT for the configured scope. Unscoped: no reference to <c>AppId</c> (single-tenant tables may
    /// not have the column). Scoped: own rows + (optionally) global rows, global FIRST so that the
    /// own-scope row, read later, overwrites it in <see cref="BuildDictionary"/>.
    /// </summary>
    internal static string BuildSql(DatabaseConfigurationOptions options)
    {
        if (options.AppId is null)
            return "SELECT CAST(NULL AS UNIQUEIDENTIFIER) AS AppId, [Key], [Value] FROM dbo.AppConfigurations WHERE IsActive = 1 ORDER BY [Key]";

        var scope = options.IncludeGlobal ? "(AppId = @appId OR AppId IS NULL)" : "AppId = @appId";
        return "SELECT AppId, [Key], [Value] FROM dbo.AppConfigurations WHERE IsActive = 1 AND " + scope +
               " ORDER BY CASE WHEN AppId IS NULL THEN 0 ELSE 1 END, [Key]";
    }

    /// <summary>
    /// Rows → configuration dictionary. Keys are case-insensitive (as <c>IConfiguration</c> keys are);
    /// with a scope, a row of any OTHER application is ignored even if the query returned it, an own-scope
    /// row beats a global one regardless of row order, and protected keys are dropped
    /// (<paramref name="droppedProtectedKeys"/>).
    /// </summary>
    internal static Dictionary<string, string?> BuildDictionary(
        IEnumerable<ConfigurationRow> rows, DatabaseConfigurationOptions options, out IReadOnlyList<string> droppedProtectedKeys)
    {
        var config = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var fromOwnScope = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var dropped = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Key)) continue;

            if (options.AppId is { } scope)
            {
                var isOwn = row.AppId == scope;
                var isGlobal = row.AppId is null;
                if (!isOwn && !(isGlobal && options.IncludeGlobal)) continue;   // another application's row
                if (options.IsProtected(row.Key)) { dropped.Add(row.Key); continue; }
                if (isGlobal && fromOwnScope.Contains(row.Key)) continue;       // own scope already won
                config[row.Key] = row.Value;
                if (isOwn) fromOwnScope.Add(row.Key);
                continue;
            }

            if (options.IsProtected(row.Key)) { dropped.Add(row.Key); continue; }
            config[row.Key] = row.Value;
        }

        droppedProtectedKeys = dropped.ToArray();
        return config;
    }

    /// <summary>
    /// Give every DB-backed configuration provider in <paramref name="configuration"/> a real logger.
    /// Configuration is built before the host's logging pipeline exists, so until this is called the
    /// provider can only remember its last failure; on attach it reports a pending load failure and any
    /// protected keys it dropped. Call once after the host is built, e.g.
    /// <c>app.Configuration.AttachDatabaseConfigurationLogging(app.Services.GetRequiredService&lt;ILoggerFactory&gt;())</c>.
    /// Safe (a no-op) when no DB-backed provider is registered.
    /// </summary>
    public static IConfiguration AttachDatabaseConfigurationLogging(this IConfiguration configuration, ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        if (configuration is IConfigurationRoot root)
        {
            foreach (var provider in root.Providers.OfType<DynamicConfigurationProvider>())
                provider.AttachLogger(loggerFactory.CreateLogger<DynamicConfigurationProvider>());
        }
        return configuration;
    }

    /// <summary>One <c>dbo.AppConfigurations</c> row as read by the loader.</summary>
    internal sealed class ConfigurationRow
    {
        public Guid? AppId { get; set; }
        public string Key { get; set; } = string.Empty;
        public string? Value { get; set; }
    }
}
