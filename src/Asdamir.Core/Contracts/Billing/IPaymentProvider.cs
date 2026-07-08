// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.ErrorHandling.Abstractions;

namespace Asdamir.Core.Contracts.Billing;

/// <summary>
/// Contract for the payment/subscription backend — a single provider, <b>Paddle</b> (global
/// Merchant-of-Record). The billing feature (in the API tier) talks only to this interface; the concrete
/// provider is wired by <see cref="Asdamir.Core.Configuration.PaymentProviderOptions"/>.
/// <para>
/// <b>PCI / MoR:</b> no raw card data ever crosses this interface. Card entry, tax and invoicing happen at
/// Paddle (hosted checkout); we only ever hold Paddle transaction/subscription references.
/// </para>
/// <para>
/// Every method returns a <see cref="Result{T}"/> (never throws for expected provider failures) so the
/// caller maps a failed <see cref="Error"/> to the two-channel user message (<c>ProblemDetails.Title</c>).
/// </para>
/// </summary>
public interface IPaymentProvider
{
    /// <summary>Stable provider key — <c>"paddle"</c>. Stamped onto stored rows.</summary>
    string Name { get; }

    /// <summary>Create (or look up) the provider-side customer for a billing account.</summary>
    Task<Result<ProviderCustomer>> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken ct = default);

    /// <summary>
    /// Start a hosted checkout for a plan. Returns the URL/token the UI renders (redirect or iframe) so the
    /// customer enters card details on the provider, never on us.
    /// </summary>
    Task<Result<CheckoutSession>> CreateCheckoutSessionAsync(CreateCheckoutRequest request, CancellationToken ct = default);

    /// <summary>Create a recurring subscription for a customer on a plan (after checkout, or directly).</summary>
    Task<Result<ProviderSubscription>> CreateSubscriptionAsync(CreateSubscriptionRequest request, CancellationToken ct = default);

    /// <summary>Cancel a subscription immediately or at period end.</summary>
    Task<Result> CancelSubscriptionAsync(string providerSubscriptionId, bool atPeriodEnd, CancellationToken ct = default);

    /// <summary>
    /// Verify a webhook's signature (fail-closed) and parse it to a canonical <see cref="ProviderWebhookEvent"/>.
    /// The caller then deduplicates by <see cref="ProviderWebhookEvent.ProviderEventId"/> before acting.
    /// </summary>
    Task<Result<ProviderWebhookEvent>> VerifyWebhookAsync(WebhookVerificationRequest request, CancellationToken ct = default);

    /// <summary>Refund a captured payment (full when <paramref name="amount"/> is null).</summary>
    Task<Result> RefundAsync(string providerPaymentId, decimal? amount, CancellationToken ct = default);
}

/// <summary>Create-customer input. <paramref name="ExternalRef"/> is our BillingAccount id (idempotency).</summary>
public sealed record CreateCustomerRequest(string Name, string Email, string? ExternalRef = null);

/// <summary>The provider-side customer handle to store on the billing account.</summary>
public sealed record ProviderCustomer(string ProviderCustomerId);

/// <summary>
/// Hosted-checkout input. <paramref name="PlanPriceRef"/> is the Paddle price reference (<c>Plans.PaddlePriceId</c>).
/// </summary>
public sealed record CreateCheckoutRequest(
    string PlanPriceRef,
    string ProviderCustomerId,
    string SuccessUrl,
    string CancelUrl,
    string Currency = "TRY",
    decimal? Amount = null,        // crypto rail: the charge amount (Paddle uses PlanPriceRef instead)
    string? Description = null);   // crypto rail: the charge name/description

/// <summary>
/// Hosted-checkout output. <paramref name="CheckoutUrl"/> for redirect flows; <paramref name="ClientToken"/>
/// for iframe/embedded flows (Paddle hosted checkout). At least one is populated.
/// </summary>
public sealed record CheckoutSession(string SessionId, string? CheckoutUrl = null, string? ClientToken = null);

/// <summary>Create-subscription input.</summary>
public sealed record CreateSubscriptionRequest(string ProviderCustomerId, string PlanPriceRef, int TrialDays = 0);

/// <summary>The provider-side subscription handle + status snapshot.</summary>
public sealed record ProviderSubscription(string ProviderSubscriptionId, string Status, DateTimeOffset? CurrentPeriodEndUtc = null);

/// <summary>Raw webhook to verify: the untouched body + the request headers (signature lives in a header).</summary>
public sealed record WebhookVerificationRequest(string RawBody, IReadOnlyDictionary<string, string> Headers);

/// <summary>A verified, canonical webhook event. <paramref name="ProviderEventId"/> is the dedup key.</summary>
public sealed record ProviderWebhookEvent(string ProviderEventId, string Type, string RawPayload);
