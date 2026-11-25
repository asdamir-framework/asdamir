// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Web.Localization.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Asdamir.Web.Localization.Abstractions;
using Polly;

namespace Asdamir.Web.Localization.Extensions;

/// <summary>
/// Dependency injection registration for Asdamir.Web.Localization NuGet package.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds localization service (HttpClient only - Framework NuGet).
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="apiBaseUrl">API base URL (e.g., "https://localhost:7001")</param>
    /// <returns>Service collection for chaining</returns>
    /// <remarks>
    /// <para><b>TLS certificate validation is on, except in Development — and never silently.</b> So that a
    /// developer's self-signed certificate works, validation is skipped only when BOTH hold: the
    /// <c>ASPNETCORE_ENVIRONMENT</c> variable is exactly <c>Development</c>, and the host's own
    /// <c>IHostEnvironment</c> (when one is registered) also reports Development. The environment variable
    /// alone used to decide this, so a process that inherited <c>ASPNETCORE_ENVIRONMENT=Development</c>
    /// while actually hosted as Staging/Production talked to its API without verifying the certificate, and
    /// nothing said so. Every time validation is skipped a warning is logged.</para>
    /// </remarks>
    public static IServiceCollection AddFrameworkLocalization(
        this IServiceCollection services,
        string apiBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
            throw new ArgumentException("API base URL is required", nameof(apiBaseUrl));

        // Register typed HTTP client
        services.AddHttpClient<ILocalizationService, LocalizationHttpClient>(client =>
        {
            client.BaseAddress = new Uri(apiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .ConfigurePrimaryHttpMessageHandler(sp =>
        {
            var handler = new HttpClientHandler();

            // Skip TLS validation in development for self-signed certificates — see the remarks above.
            if (IsDevelopment(sp))
            {
                handler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

                sp.GetService<ILoggerFactory>()?
                    .CreateLogger("Asdamir.Web.Localization")
                    .LogWarning(
                        "TLS certificate validation is DISABLED for the localization API client ({BaseUrl}) because "
                        + "the environment is Development. This must never appear outside a developer machine.",
                        apiBaseUrl);
            }
            return handler;
        })
        // Retry transient failures so a brief Gateway hiccup at startup does not propagate
        // an empty result up to the cache layer.
        .AddResilienceHandler("LocalizationRetry", pipeline =>
        {
            pipeline.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(1),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
                    .HandleResult(r => (int)r.StatusCode >= 500 || r.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
            });
        });

        return services;
    }

    /// <summary>
    /// Development by BOTH definitions: the process environment variable and, when the host registers one,
    /// its <see cref="IHostEnvironment"/>. Requiring both can only narrow the old variable-only rule.
    /// </summary>
    private static bool IsDevelopment(IServiceProvider services)
    {
        var variable = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (!string.Equals(variable, "Development", StringComparison.Ordinal)) return false;

        var host = services.GetService<IHostEnvironment>();
        return host is null || host.IsDevelopment();
    }
}
