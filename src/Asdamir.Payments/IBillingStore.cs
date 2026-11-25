// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Payments;

/// <summary>
/// Billing persistence — the operational (single-account) surface the payment rails, the webhook
/// processor and the customer billing endpoints depend on. Implemented per host: the AppManagement
/// control plane over its AsdamirVault procs, an app-local (single-tenant) store, or an in-memory store
/// (<c>Persistence:UseInMemory</c>). Cross-tenant/operator reads are a separate concern (the host layers
/// its own admin store on top).
/// </summary>
public interface IBillingStore
{
    /// <summary>All active plans, ordered for display (SortOrder, Price).</summary>
    Task<IReadOnlyList<PlanDto>> ListPlansAsync(CancellationToken ct = default);

    /// <summary>A single plan by its stable code (<c>free</c>/<c>pro</c>/<c>business</c>), or null.</summary>
    Task<PlanDto?> GetPlanByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>The billing account owned by a console/app user, or null if none exists yet.</summary>
    Task<BillingAccountDto?> GetAccountByOwnerAsync(int ownerUserId, CancellationToken ct = default);

    /// <summary>The account's current trialing/active/past_due subscription, or null.</summary>
    Task<SubscriptionDto?> GetActiveSubscriptionAsync(Guid billingAccountId, CancellationToken ct = default);

    /// <summary>
    /// Create a billing account for a user; returns the new account id. <paramref name="appId"/> is the
    /// owning generated app (from the caller's JWT <c>app_code</c> claim, AsdamirVault_114/Model A) — pass
    /// null for a console/operator (self-app) account, or for an app-local (Model B) account. The
    /// account's subscriptions/invoices inherit it.
    /// </summary>
    Task<Guid> CreateAccountAsync(string name, string email, int ownerUserId, Guid? appId, CancellationToken ct = default);

    /// <summary>
    /// Create a subscription for an account on a plan; returns the new subscription id.
    /// <b>A trial is granted once per account:</b> when <paramref name="status"/> is <c>trialing</c> with a
    /// non-null <paramref name="trialEndsAtUtc"/> and the account has EVER had a subscription with a trial
    /// end (whatever its status now), the store creates the subscription as <c>incomplete</c> with no trial
    /// instead — it grants nothing until a payment webhook activates it. Enforced by the store (the
    /// <c>Subscription_Create</c> proc / the in-memory mirror) so cancel + resubscribe cannot repeat a trial
    /// through any caller; read the subscription back to see the status actually stored.
    /// </summary>
    Task<Guid> CreateSubscriptionAsync(Guid billingAccountId, Guid planId, string status, DateTime? trialEndsAtUtc, CancellationToken ct = default);

    /// <summary>Update a subscription's status (e.g. cancel); returns true if a row changed.</summary>
    Task<bool> UpdateSubscriptionStatusAsync(Guid subscriptionId, string status, CancellationToken ct = default);

    /// <summary>
    /// Claim a provider webhook by (provider, eventId) and say whether the caller must process it. Returns
    /// true when the event is new, or when it was claimed earlier but never marked processed and that claim
    /// has gone stale (the earlier attempt failed or died — this delivery is the provider's retry). Returns
    /// false when it has already been processed, or is being processed right now by another delivery — so a
    /// burst of concurrent deliveries yields exactly one true. A processed event is never applied twice and
    /// a failed one is never lost: call <see cref="MarkWebhookProcessedAsync"/> only after processing
    /// succeeded, and <see cref="ReleaseWebhookAsync"/> when it failed.
    /// </summary>
    Task<bool> TryRecordWebhookAsync(string provider, string providerEventId, string? type, string? payload, CancellationToken ct = default);

    /// <summary>
    /// Mark a claimed webhook as processed — call it only after processing SUCCEEDED. Until then the event
    /// is offered again once its claim is released or goes stale.
    /// </summary>
    Task MarkWebhookProcessedAsync(string provider, string providerEventId, CancellationToken ct = default);

    /// <summary>
    /// Give up the claim on a webhook whose processing FAILED, so the provider's retry is processed at once
    /// instead of waiting for the claim to go stale. Never affects a processed event. The default
    /// implementation does nothing (a store written before this member existed still recovers through the
    /// stale-claim path of <see cref="TryRecordWebhookAsync"/>, only later).
    /// </summary>
    Task ReleaseWebhookAsync(string provider, string providerEventId, CancellationToken ct = default) => Task.CompletedTask;

    /// <summary>Find a subscription by its provider reference (set at checkout), or null.</summary>
    Task<SubscriptionDto?> GetSubscriptionByProviderRefAsync(string provider, string providerRef, CancellationToken ct = default);

    /// <summary>Link a subscription to its provider reference (so a later webhook can find it).</summary>
    Task LinkSubscriptionToProviderAsync(Guid subscriptionId, string provider, string providerRef, CancellationToken ct = default);
}
