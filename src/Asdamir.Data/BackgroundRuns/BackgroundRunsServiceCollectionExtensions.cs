// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.BackgroundRuns;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Asdamir.Data.BackgroundRuns;

/// <summary>
/// DI wiring for the background-run primitive. A generated Gateway registers the whole thing in one
/// line — <c>services.AddBackgroundRuns(configuration)</c> — then adds one handler per job type with
/// <see cref="AddBackgroundJob{THandler}(IServiceCollection)"/>.
/// </summary>
public static class BackgroundRunsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the background-run service, hosted runner, restart-recovery, store (Dapper vs
    /// InMemory per <c>Persistence:UseInMemory</c>), queue and options. Requires <c>AddDataAccess</c>
    /// + <c>AddMultiTenancy</c> already wired (the store injects <c>IDbConnectionFactory</c> +
    /// <c>ITenantContext</c>).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration (reads <c>Persistence:UseInMemory</c> + binds <c>BackgroundRuns</c> options).</param>
    /// <param name="configure">Optional extra options mutation (applied after config binding).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBackgroundRuns(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<BackgroundRunOptions>? configure = null)
    {
        var opts = services.AddOptions<BackgroundRunOptions>();
        opts.Bind(configuration.GetSection("BackgroundRuns"));
        if (configure is not null) opts.Configure(configure);

        var useInMemory = configuration.GetValue("Persistence:UseInMemory", false);
        if (useInMemory)
        {
            services.TryAddSingleton<BackgroundRunData>();
            services.TryAddScoped<IBackgroundRunStore, InMemoryBackgroundRunStore>();
        }
        else
        {
            services.TryAddScoped<IBackgroundRunStore, DapperBackgroundRunStore>();
        }

        services.TryAddSingleton<BackgroundRunQueue>();
        services.TryAddScoped<IBackgroundRunService, BackgroundRunService>();

        // The runner resolves a JobType -> handler map once, from all registered handlers.
        services.TryAddSingleton<IReadOnlyDictionary<string, IBackgroundJobHandler>>(sp =>
        {
            var map = new Dictionary<string, IBackgroundJobHandler>(StringComparer.Ordinal);
            foreach (var h in sp.GetServices<IBackgroundJobHandler>())
                map[h.JobType] = h; // last registration wins for a duplicate JobType
            return map;
        });

        services.AddHostedService<BackgroundRunProcessor>();
        services.AddHostedService<BackgroundRunRecoveryService>();
        return services;
    }

    /// <summary>
    /// Registers an app job handler. Handlers are singletons (they are stateless bodies that create
    /// their own scopes for per-run work) and are discovered by the runner via their
    /// <see cref="IBackgroundJobHandler.JobType"/>.
    /// </summary>
    /// <typeparam name="THandler">The handler implementation.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBackgroundJob<THandler>(this IServiceCollection services)
        where THandler : class, IBackgroundJobHandler
    {
        services.AddSingleton<IBackgroundJobHandler, THandler>();
        return services;
    }
}
