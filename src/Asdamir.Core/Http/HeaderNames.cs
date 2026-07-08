// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.Http;

/// <summary>
/// Well-known custom HTTP header names exchanged between the framework's tiers. Reference these
/// constants when reading or writing the headers so the wire contract stays consistent across the
/// UI, Gateway, and API.
/// </summary>
public static class HeaderNames
{
    /// <summary>HTTP header carrying the per-request correlation/trace id, propagated across tiers to stitch logs together.</summary>
    public const string CorrelationId = "X-Correlation-Id";

    /// <summary>HTTP header selecting the target database dialect for the request (values: <c>SqlServer</c> | <c>Oracle</c>).</summary>
    public const string DbProvider = "X-Db-Provider"; // values: SqlServer | Oracle
}


