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

/// <summary>
/// Database provider types supported by the framework
/// </summary>
public enum DbProviderType
{
    /// <summary>Microsoft SQL Server (the default provider).</summary>
    SqlServer,
    /// <summary>Oracle Database.</summary>
    Oracle,
    /// <summary>PostgreSQL.</summary>
    PostgreSQL
}

/// <summary>
/// Represents a database provider configuration
/// </summary>
public interface IDbProvider
{
    /// <summary>
    /// Gets the database provider type
    /// </summary>
    DbProviderType Type { get; }
    
    /// <summary>
    /// Gets the connection string for this provider
    /// </summary>
    string GetConnectionString();
}
