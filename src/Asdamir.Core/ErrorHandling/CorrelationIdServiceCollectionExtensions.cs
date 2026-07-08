// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Asdamir.Core.ErrorHandling.Http;

/// <summary>
/// DI helpers that wire up correlation-id propagation — the scoped accessor/mutator plus the
/// outbound forwarding handler — so requests stay traceable across service boundaries.
/// </summary>
public static class CorrelationIdServiceCollectionExtensions
{
    /// <summary>
    /// Registers the scoped <see cref="ICorrelationIdAccessor"/> and the
    /// <see cref="CorrelationIdForwardingHandler"/> so outbound HttpClient calls can
    /// opt in via <c>AddHttpMessageHandler&lt;CorrelationIdForwardingHandler&gt;()</c>.
    ///
    /// Pair with <c>app.UseMiddleware&lt;CorrelationIdMiddleware&gt;()</c> in the
    /// request pipeline — the middleware sets the id, the accessor surfaces it, the
    /// handler propagates it downstream.
    ///
    /// Audit fix vs. v1: registrations use <c>TryAdd*</c> so calling
    /// <c>AddCorrelationIdAccessor()</c> from multiple framework entry points
    /// (e.g. both <c>AddFrameworkSecurity</c> and an app's own setup) doesn't
    /// install two handler instances on every outbound HttpClient pipeline.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same collection for chaining.</returns>
    public static IServiceCollection AddCorrelationIdAccessor(this IServiceCollection services)
    {
        services.TryAddScoped<ScopedCorrelationIdAccessor>();
        services.TryAddScoped<ICorrelationIdAccessor>(sp => sp.GetRequiredService<ScopedCorrelationIdAccessor>());
        services.TryAddScoped<ICorrelationIdMutator>(sp => sp.GetRequiredService<ScopedCorrelationIdAccessor>());
        services.TryAddScoped<CorrelationIdForwardingHandler>();
        return services;
    }
}
