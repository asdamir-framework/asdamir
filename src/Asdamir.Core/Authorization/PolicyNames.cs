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
/// Well-known ASP.NET Core authorization <b>policy</b> names for the Products slice. Reference these
/// constants from <c>[Authorize(Policy = ...)]</c> and policy registration so endpoint guards and the
/// policy definitions never drift on a hand-typed string.
/// </summary>
public static class PolicyNames
{
    /// <summary>Policy that authorizes viewing/listing products (read-only access to the Products screens).</summary>
    public const string ProductsRead = "Products.Read";

    /// <summary>Policy that authorizes creating a new product.</summary>
    public const string ProductsCreate = "Products.Create";

    /// <summary>Policy that authorizes editing an existing product.</summary>
    public const string ProductsUpdate = "Products.Update";

    /// <summary>Policy that authorizes deleting a product.</summary>
    public const string ProductsDelete = "Products.Delete";
}


