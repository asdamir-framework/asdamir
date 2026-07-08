// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.Dtos;

/// <summary>Read model for a customer order in the demo order-management sample.</summary>
/// <param name="Id">Order identifier (primary key).</param>
/// <param name="CustomerName">Name of the customer who placed the order.</param>
/// <param name="Amount">Order total in the app's currency.</param>
/// <param name="Status">Workflow state of the order (e.g. Pending, Shipped, Cancelled).</param>
public record OrderDto(int Id, string CustomerName, decimal Amount, string Status);

/// <summary>Query filter for listing/searching demo orders.</summary>
/// <param name="Query">Free-text search term (e.g. customer name); null returns all orders.</param>
public record OrderFilter(string? Query);
