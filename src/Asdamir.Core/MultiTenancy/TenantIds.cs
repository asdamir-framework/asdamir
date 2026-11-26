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

/// <summary>Well-known tenancy constants shared by token issuers and resolvers.</summary>
public static class TenantIds
{
    /// <summary>The claim that carries the tenant in a signed token (<c>tid</c>).</summary>
    public const string ClaimType = "tid";

    /// <summary>
    /// What <see cref="ClaimType"/> becomes on the principal when the JWT handler's default inbound claim
    /// mapping is on (<c>MapInboundClaims = true</c>, the ASP.NET Core default).
    /// <c>ClaimsTenantResolver</c> accepts either spelling.
    /// </summary>
    public const string MappedClaimType = "http://schemas.microsoft.com/identity/claims/tenantid";

    /// <summary>
    /// The tenant id a deliberately single-tenant application stamps into its tokens. It is the value the
    /// scaffold's sample seed rows use. The point is that the ISSUER writes it, signed — it is never
    /// assumed by the API when a token lacks the claim.
    /// </summary>
    public const string Single = "default";
}
