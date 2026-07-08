// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.Authorization;

/// <summary>
/// Stable permission strings (<c>&lt;area&gt;.&lt;action&gt;</c>) evaluated by the two-tier RBAC. These
/// are the durable identifiers stored in <c>Permissions</c>/<c>RolePermissions</c> and carried on the
/// user's claims — bind checks to these constants rather than repeating the literal string.
/// </summary>
public static class Permissions
{
    /// <summary>Grants read access to the orders area (list/detail).</summary>
    public const string ProductsRead = "orders.read";

    /// <summary>Grants the right to create a new order.</summary>
    public const string ProductsCreate = "orders.create";

    /// <summary>Grants the right to modify an existing order.</summary>
    public const string ProductsUpdate = "orders.update";

    /// <summary>Grants the right to delete an order.</summary>
    public const string ProductsDelete = "orders.delete";
}


