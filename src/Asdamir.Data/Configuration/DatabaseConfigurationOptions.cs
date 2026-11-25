// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Microsoft.Extensions.Logging;

namespace Asdamir.Data.Configuration;

/// <summary>
/// Options for <see cref="ConfigurationBuilderExtensions.AddDatabaseConfiguration(Microsoft.Extensions.Configuration.IConfigurationBuilder, Asdamir.Core.Contracts.IDbConnectionFactory, Action{DatabaseConfigurationOptions})"/>:
/// which <c>dbo.AppConfigurations</c> rows are loaded into <c>IConfiguration</c>, and which keys a database
/// row is never allowed to supply.
/// </summary>
public sealed class DatabaseConfigurationOptions
{
    /// <summary>How often the table is re-read. Default: 5 minutes.</summary>
    public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The application scope to load. When set, only rows whose <c>AppId</c> equals this value are loaded
    /// (plus the global <c>AppId IS NULL</c> rows when <see cref="IncludeGlobal"/> is true) — a host that
    /// shares the table with other applications (the control plane) MUST set it, otherwise every
    /// application's rows land in this host's configuration. When <c>null</c> (the default, kept for
    /// backward compatibility) the table is read unscoped and is assumed to belong to this host alone;
    /// the <c>AppId</c> column is then not referenced at all, so a single-tenant table without that column
    /// keeps working.
    /// </summary>
    public Guid? AppId { get; set; }

    /// <summary>
    /// Only meaningful with <see cref="AppId"/>: also load rows whose <c>AppId</c> is NULL (global rows).
    /// A row in the application's own scope always wins over a global row with the same key. Default: true.
    /// </summary>
    public bool IncludeGlobal { get; set; } = true;

    /// <summary>
    /// Keys a database row may never supply. The DB source is added after appsettings / user-secrets /
    /// environment variables and therefore outranks them, so without this list anyone who can write a
    /// config row could replace the signing key, the at-rest encryption key, a connection string, or flip
    /// the host into in-memory persistence. Matching is case-insensitive; an entry ending in <c>:*</c>
    /// protects the whole section (<c>ConnectionStrings:*</c>). Matching rows are dropped (and reported
    /// once per distinct set through the logger). The set is mutable: add host-specific keys, or
    /// <c>Clear()</c> it to opt out entirely.
    /// </summary>
    public ISet<string> ProtectedKeys { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Jwt:Key",
        "Security:EncryptionKey",
        "ConnectionStrings:*",
        "Persistence:UseInMemory",
    };

    /// <summary>
    /// Optional logger factory. Configuration is built before the host's logging exists, so this is
    /// usually null at registration time; attach the real one after the host is built with
    /// <see cref="ConfigurationBuilderExtensions.AttachDatabaseConfigurationLogging"/>.
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; set; }

    /// <summary>True when <paramref name="key"/> is covered by <see cref="ProtectedKeys"/>.</summary>
    public bool IsProtected(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        foreach (var entry in ProtectedKeys)
        {
            if (entry.EndsWith(":*", StringComparison.Ordinal))
            {
                var section = entry[..^2];
                if (key.Equals(section, StringComparison.OrdinalIgnoreCase) ||
                    key.StartsWith(section + ":", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else if (key.Equals(entry, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}
