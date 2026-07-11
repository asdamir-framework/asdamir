// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.Configuration;
using Asdamir.Core.Contracts.Billing;
using Asdamir.Payments.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Asdamir.Payments;

/// <summary>
/// DI wiring for the Payment Service and its rails. Binds <see cref="PaymentProviderOptions"/> from the
/// <c>Payment</c> section (secrets arrive decrypted via the host's DB-backed configuration +
/// <c>IEncryptionService</c>) and registers two rails, each a typed <see cref="HttpClient"/> (no SDK; AUD001
/// — never a bare new HttpClient): <b>Paddle</b> (card / Apple Pay / Google Pay) and <b>Crypto</b>
/// (BTC / ETH / USDC). Both are exposed as <see cref="IPaymentProvider"/> so <see cref="PaymentService"/>
/// can resolve either by name, and the store-agnostic <see cref="IBillingWebhookProcessor"/> is wired to
/// turn verified webhook events into subscription transitions.
/// </summary>
public static class PaymentsServiceCollectionExtensions
{
    /// <summary>
    /// Register the payment rails (Paddle + Crypto), the <see cref="IPaymentService"/> facade and the
    /// <see cref="IBillingWebhookProcessor"/>. The host still supplies an <see cref="IBillingStore"/>
    /// implementation (control-plane, app-local or in-memory).
    /// </summary>
    public static IServiceCollection AddPayments(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PaymentProviderOptions>(configuration.GetSection(PaymentProviderOptions.Section));

        // Rail 1 — Paddle (card / Apple Pay / Google Pay).
        services.AddHttpClient<PaddlePaymentProvider>((sp, http) =>
        {
            var paddle = sp.GetRequiredService<IOptions<PaymentProviderOptions>>().Value.Paddle;
            http.BaseAddress = new Uri(paddle.BaseUrl.TrimEnd('/') + "/");
            if (!string.IsNullOrWhiteSpace(paddle.ApiKey))
                http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", paddle.ApiKey);
        });

        // Rail 2 — Crypto (Bitcoin / Ethereum / USDC via Coinbase Commerce).
        services.AddHttpClient<CryptoPaymentProvider>((sp, http) =>
        {
            var crypto = sp.GetRequiredService<IOptions<PaymentProviderOptions>>().Value.Crypto;
            http.BaseAddress = new Uri(crypto.BaseUrl.TrimEnd('/') + "/");
            if (!string.IsNullOrWhiteSpace(crypto.ApiKey))
            {
                http.DefaultRequestHeaders.Add("X-CC-Api-Key", crypto.ApiKey);
                http.DefaultRequestHeaders.Add("X-CC-Version", "2018-03-22");
            }
        });

        // Expose both rails as IPaymentProvider so the facade can enumerate/resolve by name.
        services.AddScoped<IPaymentProvider>(sp => sp.GetRequiredService<PaddlePaymentProvider>());
        services.AddScoped<IPaymentProvider>(sp => sp.GetRequiredService<CryptoPaymentProvider>());

        services.AddScoped<IPaymentService, PaymentService>();

        // Webhook processor turns verified events into subscription status transitions.
        services.AddScoped<IBillingWebhookProcessor, BillingWebhookProcessor>();

        return services;
    }
}
