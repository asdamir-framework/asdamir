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
/// Default implementation of <see cref="ITenantContext"/>.
/// Provides tenant context information for multi-tenant applications.
/// </summary>
/// <remarks>
/// This class is typically populated by <see cref="ITenantResolver"/> implementations
/// during request processing via <see cref="TenantMiddleware"/>.
/// </remarks>
public sealed class TenantContext : ITenantContext
{
    /// <summary>
    /// Gets or sets the unique identifier for the current tenant.
    /// </summary>
    /// <value>
    /// The tenant ID. Defaults to empty string.
    /// </value>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the current tenant.
    /// </summary>
    /// <value>
    /// The tenant name, or <c>null</c> if not available.
    /// </value>
    public string? TenantName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether multi-tenancy is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> if multi-tenancy is enabled; otherwise, <c>false</c>.
    /// </value>
    public bool IsMultiTenant { get; set; }

    /// <summary>
    /// Gets or sets additional custom properties associated with the tenant.
    /// </summary>
    /// <value>
    /// A read-only dictionary of tenant-specific properties.
    /// Defaults to an empty dictionary.
    /// </value>
    public IReadOnlyDictionary<string, object?> Properties { get; set; } = new Dictionary<string, object?>();
}