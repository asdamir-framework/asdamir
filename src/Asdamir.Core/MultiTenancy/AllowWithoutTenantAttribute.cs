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
/// Marks an endpoint that an <b>authenticated caller with no tenant</b> may reach — an endpoint that serves
/// the application as a whole rather than one tenant's data, called by a principal that belongs to no
/// tenant (a control plane's orchestration token, for example).
///
/// <para>Under <see cref="MissingTenantBehavior.Reject"/> the <see cref="TenantMiddleware"/> refuses every
/// authenticated request without a tenant. On an endpoint carrying this attribute it lets the request
/// continue instead, with an <b>unresolved</b> <see cref="ITenantContext"/>: nothing is defaulted, so code
/// behind the endpoint that reads <see cref="ITenantContext.TenantId"/> still fails with
/// <see cref="TenantNotResolvedException"/>. The attribute removes the up-front refusal only; it does not
/// supply a tenant, and it does not authorize anything — pair it with an authorization policy that names
/// the tenant-less caller.</para>
///
/// <para>A request that does carry a tenant is unaffected and is served as that tenant.</para>
///
/// <para><b>Pipeline order.</b> The middleware reads this from the matched endpoint, so routing must have
/// run before <c>UseMultiTenancy()</c> (in a <c>WebApplication</c> without an explicit <c>UseRouting()</c>
/// it has). Where it has not, the attribute is not seen and the request is refused as usual.</para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class AllowWithoutTenantAttribute : Attribute
{
}
