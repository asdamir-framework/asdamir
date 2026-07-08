// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.Configuration;

/// <summary>
/// Payment settings, bound from the <c>Payment</c> configuration section at the <b>API tier</b>. A single
/// <c>PaymentService</c> routes to two rails behind the one
/// <see cref="Asdamir.Core.Contracts.Billing.IPaymentProvider"/> abstraction: <b>Paddle</b> (card / Apple
/// Pay / Google Pay — global Merchant-of-Record) and <b>Crypto</b> (Bitcoin / Ethereum / USDC).
/// <para>
/// <b>Secrets:</b> the API/webhook keys are NOT in <c>appsettings.json</c>. They live encrypted in
/// <c>dbo.AppConfigurations</c> (<c>Payment:Paddle:*</c>, <c>Payment:Crypto:*</c>, <c>IsEncrypted=1</c>),
/// loaded via <c>AddDatabaseConfiguration</c> and decrypted with
/// <see cref="Asdamir.Core.Contracts.IEncryptionService"/> before binding here (rotated by
/// <c>asdamir secrets rotate-key</c>). Never commit a real key.
/// </para>
/// </summary>
public sealed class PaymentProviderOptions
{
    /// <summary>Configuration section name these options bind from (<c>Payment</c>).</summary>
    public const string Section = "Payment";

    /// <summary>Paddle credentials + environment (card / Apple Pay / Google Pay rail; global MoR).</summary>
    public PaddleOptions Paddle { get; set; } = new();

    /// <summary>Crypto gateway credentials (Bitcoin / Ethereum / USDC rail).</summary>
    public CryptoOptions Crypto { get; set; } = new();
}

/// <summary>
/// Crypto payment gateway credentials (Coinbase Commerce — hosted BTC/ETH/USDC checkout). <see cref="ApiKey"/>
/// is the <c>X-CC-Api-Key</c>; <see cref="WebhookSecret"/> verifies the <c>X-CC-Webhook-Signature</c> HMAC.
/// The rail sits behind <see cref="Asdamir.Core.Contracts.Billing.IPaymentProvider"/>, so the gateway can be
/// swapped without touching callers.
/// </summary>
public sealed class CryptoOptions
{
    /// <summary>Coinbase Commerce API key sent as the <c>X-CC-Api-Key</c> header (binds <c>Payment:Crypto:ApiKey</c>; encrypted at rest, empty until configured).</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Shared secret used to verify the <c>X-CC-Webhook-Signature</c> HMAC on inbound webhooks (binds <c>Payment:Crypto:WebhookSecret</c>; encrypted at rest, empty until configured).</summary>
    public string WebhookSecret { get; set; } = "";

    /// <summary>Coinbase Commerce REST base URL (binds <c>Payment:Crypto:BaseUrl</c>; default <c>https://api.commerce.coinbase.com</c>).</summary>
    public string BaseUrl { get; set; } = "https://api.commerce.coinbase.com";

    /// <summary>
    /// The crypto rail is <b>default-disabled</b> (policy). While false it is hidden from the payment-rail
    /// list and a crypto checkout returns <c>billing.crypto.disabled</c>. Do not flip to true without the
    /// TR-buyer geo-gate below AND written accountant sign-off (TCMB crypto-payment ban + non-MoR tax burden).
    /// See CLAUDE.md "Crypto rail policy" + docs/design/billing.md §2.1.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// ISO-3166 alpha-2 countries barred from the crypto rail (default <c>["TR"]</c> — TCMB ban). A checkout
    /// from a blocked country returns <c>billing.crypto.region_blocked</c>; an <b>undetectable</b> buyer
    /// country is treated as blocked (fail-closed) under <c>billing.crypto.region_unknown</c>.
    /// </summary>
    public string[] BlockedBuyerCountries { get; set; } = new[] { "TR" };
}

/// <summary>
/// Paddle Billing credentials. <see cref="Environment"/> is <c>sandbox</c> or <c>live</c>; the REST base
/// URL is derived from it. <see cref="WebhookSecret"/> verifies the <c>Paddle-Signature</c> HMAC header.
/// </summary>
public sealed class PaddleOptions
{
    /// <summary>Paddle Billing API key (binds <c>Payment:Paddle:ApiKey</c>; encrypted at rest, empty until configured).</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Secret used to verify the <c>Paddle-Signature</c> HMAC on inbound webhooks (binds <c>Payment:Paddle:WebhookSecret</c>; encrypted at rest, empty until configured).</summary>
    public string WebhookSecret { get; set; } = "";

    /// <summary>Target Paddle environment — <c>sandbox</c> or <c>live</c> — from which <see cref="BaseUrl"/> is derived (binds <c>Payment:Paddle:Environment</c>; default <c>sandbox</c>).</summary>
    public string Environment { get; set; } = "sandbox"; // sandbox | live

    /// <summary>Paddle Billing REST base URL for the configured environment.</summary>
    public string BaseUrl => string.Equals(Environment, "live", StringComparison.OrdinalIgnoreCase)
        ? "https://api.paddle.com"
        : "https://sandbox-api.paddle.com";
}
