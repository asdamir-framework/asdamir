// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.MultiTenancy;

/// <summary>
/// Represents the tenant context for the current request.
/// Provides tenant identification and metadata for multi-tenant applications.
/// </summary>
/// <remarks>
/// This interface is resolved per HTTP request via dependency injection.
/// The tenant is typically resolved from HTTP headers, routes, or custom strategies.
/// </remarks>
public interface ITenantContext
{
    /// <summary>
    /// Gets the unique identifier for the current tenant.
    /// </summary>
    /// <value>
    /// The tenant ID. Defaults to "default" if no tenant is resolved.
    /// </value>
    string TenantId { get; }

    /// <summary>
    /// Gets the display name of the current tenant.
    /// </summary>
    /// <value>
    /// The tenant name, or <c>null</c> if not available.
    /// </value>
    string? TenantName { get; }

    /// <summary>
    /// Gets a value indicating whether multi-tenancy is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> if multi-tenancy is enabled; otherwise, <c>false</c>.
    /// </value>
    bool IsMultiTenant { get; }

    /// <summary>
    /// Gets additional custom properties associated with the tenant.
    /// </summary>
    /// <value>
    /// A read-only dictionary of tenant-specific properties.
    /// Can be used to store custom metadata like connection strings, feature flags, etc.
    /// </value>
    IReadOnlyDictionary<string, object?> Properties { get; }
}
