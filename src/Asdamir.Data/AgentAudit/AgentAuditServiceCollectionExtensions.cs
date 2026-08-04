// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.AgentAudit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Asdamir.Data.AgentAudit;

/// <summary>
/// Registers the agent-audit client — the application's route into the tamper-evident agent action ledger.
/// </summary>
public static class AgentAuditServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IAgentActionAuditor"/>, its background delivery pump, and the named HTTP client it
    /// posts through.
    ///
    /// <para>Options bind from the <c>AgentAudit</c> configuration section, then from
    /// <paramref name="configure"/>. Two of them are SECRETS or environment-specific and must come from
    /// user-secrets or the environment rather than <c>appsettings.json</c>: <c>AgentAudit:SigningKey</c> (the
    /// shared <c>Jwt:Key</c>) and the control-plane base address.</para>
    ///
    /// <para><b>This registers a WRITER only.</b> Reading, verifying and folding the ledger live in the control
    /// plane, because the ledger is central and an application is never allowed to reach it directly.</para>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration root; the <c>AgentAudit</c> section is bound.</param>
    /// <param name="controlPlaneBaseAddress">Base address of the control plane's API (its <c>BaseUrl</c>).</param>
    /// <param name="configure">Optional code-side overrides, applied after configuration binding.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="ArgumentException">The control-plane base address is missing or not absolute.</exception>
    public static IServiceCollection AddAgentAudit(
        this IServiceCollection services,
        IConfiguration configuration,
        string controlPlaneBaseAddress,
        Action<AgentAuditOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        if (!Uri.TryCreate(controlPlaneBaseAddress, UriKind.Absolute, out var baseUri))
        {
            throw new ArgumentException(
                "An absolute control-plane base address is required to deliver agent-audit records.",
                nameof(controlPlaneBaseAddress));
        }

        var options = services.AddOptions<AgentAuditOptions>();
        options.Bind(configuration.GetSection("AgentAudit"));
        if (configure is not null) options.Configure(configure);

        services.AddHttpClient(AgentAuditSink.HttpClientName, client =>
        {
            client.BaseAddress = baseUri;
            // Short, because the pump retries with backoff and spools on failure. Hanging on a slow control
            // plane would only grow the in-memory queue toward its overflow policy.
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        // ONE instance serves both roles: the auditor the application calls and the hosted pump that delivers.
        // They share the queue and the spool, so they cannot be separated.
        services.TryAddSingleton<AgentAuditSink>();
        services.TryAddSingleton<IAgentActionAuditor>(sp => sp.GetRequiredService<AgentAuditSink>());
        services.AddHostedService(sp => sp.GetRequiredService<AgentAuditSink>());

        return services;
    }
}
