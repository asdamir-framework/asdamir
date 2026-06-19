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
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = new HttpClientHandler();
            // Skip SSL validation in development for self-signed certificates
            if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
            {
                handler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
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
}
