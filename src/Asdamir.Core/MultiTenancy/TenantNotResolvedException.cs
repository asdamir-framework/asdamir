// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.ErrorHandling.Domain;

namespace Asdamir.Core.MultiTenancy;

/// <summary>
/// The request needs a tenant and none was resolved — typically an authenticated token without the tenant
/// claim. It is a <see cref="DomainException"/> with the stable code <see cref="ErrorCode"/>, which the
/// default ProblemDetails mapper answers with <b>403</b>: the caller is known, but is not placed in any
/// tenant, so no tenant's data may be served. Under <see cref="MissingTenantBehavior.Reject"/> this replaces
/// the former silent fallback to the tenant <c>"default"</c>.
/// </summary>
public sealed class TenantNotResolvedException : DomainException
{
    /// <summary>Stable error key; seed <c>error.tenant.unresolved</c> for the user-facing message.</summary>
    public const string ErrorCode = "tenant.unresolved";

    /// <summary>Creates the exception with the default developer-facing message.</summary>
    public TenantNotResolvedException()
        : this("No tenant was resolved for this scope. The token carries no tenant claim (or, outside a request, " +
               "no tenant was set on the scope). Nothing is defaulted: set TenancyOptions.MissingTenant = UseDefault " +
               "only for a deliberately single-tenant host.")
    {
    }

    /// <summary>Creates the exception with a specific developer-facing message.</summary>
    /// <param name="message">Developer/log-facing description; never shown to the end user.</param>
    public TenantNotResolvedException(string message) : base(ErrorCode, message)
    {
    }
}

/// <summary>
/// Multi-tenancy is registered but not wired into the request pipeline: <c>AddMultiTenancy()</c> was called
/// and <c>app.UseMultiTenancy()</c> was not, so nothing ever resolves a tenant. This is a host wiring defect,
/// not a caller error — it is deliberately NOT a domain exception and surfaces as a 500.
/// </summary>
public sealed class TenancyNotConfiguredException : Exception
{
    /// <summary>Creates the exception with the wiring instruction as its message.</summary>
    public TenancyNotConfiguredException()
        : base("ITenantContext was requested during an HTTP request, but the tenant middleware never ran. " +
               "AddMultiTenancy() registers the services only — call app.UseMultiTenancy() after " +
               "UseAuthentication()/UseAuthorization(). (Before this check the request silently ran as the " +
               "tenant \"default\".)")
    {
    }
}
