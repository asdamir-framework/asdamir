// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.Text.Json;
using Asdamir.Core.Contracts.Billing;
using Microsoft.Extensions.Logging;

namespace Asdamir.Payments;

/// <summary>
/// Turns a verified provider webhook into a subscription status transition. The event (its type and, for
/// Paddle subscription events, the authoritative <c>data.status</c>) maps to a target status; the provider
/// references in the payload find our subscription (linked at checkout). Unknown types and unmatched refs
/// are safe no-ops (logged). A storage failure is NOT swallowed: it propagates so the caller leaves the
/// event unprocessed and the provider's retry is applied.
/// </summary>
public interface IBillingWebhookProcessor
{
    /// <summary>Apply a verified webhook event to the linked subscription (no-op when it maps to nothing).</summary>
    Task ProcessAsync(string providerName, ProviderWebhookEvent evt, CancellationToken ct = default);
}

/// <inheritdoc />
/// <remarks>
/// <para><b>Finding the subscription (Paddle).</b> Checkout stores the Paddle <i>transaction</i> id
/// (<c>txn_…</c>) because the Paddle subscription does not exist until the transaction is paid, while
/// subscription events identify themselves by the <i>subscription</i> id (<c>sub_…</c>). The lookup
/// therefore tries, in order, <c>data.id</c>, <c>data.subscription_id</c> and <c>data.transaction_id</c>,
/// and once the real <c>sub_…</c> id is known it is persisted, so later events (which carry no transaction
/// id) match directly.</para>
/// <para><b>Ordering.</b> A canceled subscription is terminal: a late or replayed event cannot bring it
/// back. There is no general out-of-order guard (that needs the last-applied event time stored per
/// subscription).</para>
/// </remarks>
public sealed class BillingWebhookProcessor : IBillingWebhookProcessor
{
    private readonly IBillingStore _store;
    private readonly ILogger<BillingWebhookProcessor> _logger;

    /// <summary>Create the processor over the billing store used to look up and mutate subscriptions.</summary>
    public BillingWebhookProcessor(IBillingStore store, ILogger<BillingWebhookProcessor> logger)
    {
        _store = store;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ProcessAsync(string providerName, ProviderWebhookEvent evt, CancellationToken ct = default)
    {
        var refs = ExtractRefs(providerName, evt.Type, evt.RawPayload);

        var status = MapStatus(evt.Type, refs.ProviderStatus);
        if (status is null)
        {
            _logger.LogInformation("Billing webhook {Provider}/{Type} carries no status transition — ignored.", providerName, evt.Type);
            return;
        }

        if (refs.Candidates.Count == 0)
        {
            _logger.LogWarning("Billing webhook {Provider}/{Type}: no provider reference in payload — skipped.", providerName, evt.Type);
            return;
        }

        SubscriptionDto? sub = null;
        foreach (var candidate in refs.Candidates)
        {
            sub = await _store.GetSubscriptionByProviderRefAsync(providerName, candidate, ct);
            if (sub is not null) break;
        }
        if (sub is null)
        {
            _logger.LogWarning("Billing webhook {Provider}/{Type}: no subscription linked to any of {Refs} — skipped.",
                providerName, evt.Type, string.Join(", ", refs.Candidates));
            return;
        }

        // Persist the provider's real subscription id the first time we learn it (checkout could only
        // store the transaction id) — later subscription events carry nothing else to match on.
        if (refs.SubscriptionRef is { Length: > 0 } real &&
            !string.Equals(sub.ProviderSubscriptionId, real, StringComparison.Ordinal))
        {
            await _store.LinkSubscriptionToProviderAsync(sub.Id, providerName, real, ct);
            _logger.LogInformation("Billing webhook {Provider}/{Type}: subscription {Sub} linked to provider subscription {Ref}.",
                providerName, evt.Type, sub.Id, real);
        }

        if (string.Equals(sub.Status, "canceled", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(status, "canceled", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Billing webhook {Provider}/{Type}: subscription {Sub} is canceled — a late '{Status}' event does not reopen it.",
                providerName, evt.Type, sub.Id, status);
            return;
        }

        if (string.Equals(sub.Status, status, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Billing webhook {Provider}/{Type}: subscription {Sub} already {Status}.", providerName, evt.Type, sub.Id, status);
            return;
        }

        await _store.UpdateSubscriptionStatusAsync(sub.Id, status, ct);
        _logger.LogInformation("Billing webhook {Provider}/{Type}: subscription {Sub} → {Status}.", providerName, evt.Type, sub.Id, status);
    }

    /// <summary>Event type → subscription status, or null when the event type alone isn't a transition.</summary>
    public static string? MapStatus(string type) => type switch
    {
        "subscription.activated" or "subscription.created" or "subscription.resumed"
            or "transaction.completed"
            or "charge:confirmed" or "charge:resolved" => "active",
        "subscription.past_due" or "charge:failed" => "past_due",
        "subscription.canceled" => "canceled",
        _ => null,
    };

    /// <summary>
    /// Event → subscription status, preferring the status the provider REPORTS over the one the event name
    /// implies. For a Paddle <c>subscription.*</c> event <paramref name="providerStatus"/> is
    /// <c>data.status</c>: a <c>subscription.created</c> whose status is <c>trialing</c> is a trial, not a
    /// paid activation, and a <c>subscription.updated</c> (no status in its name) can still carry
    /// <c>past_due</c> or <c>canceled</c>. A status with no local equivalent (e.g. <c>paused</c>) and every
    /// non-subscription event fall back to <see cref="MapStatus(string)"/>.
    /// </summary>
    public static string? MapStatus(string type, string? providerStatus)
    {
        if (type.StartsWith("subscription.", StringComparison.Ordinal))
        {
            switch (providerStatus)
            {
                case "active": return "active";
                case "trialing": return "trialing";
                case "past_due": return "past_due";
                case "canceled": return "canceled";
            }
        }
        return MapStatus(type);
    }

    /// <summary>What a webhook payload says about which subscription it concerns.</summary>
    /// <param name="Candidates">Provider references to look the subscription up by, most specific first.</param>
    /// <param name="SubscriptionRef">The provider's own subscription id when the payload states it.</param>
    /// <param name="ProviderStatus">The subscription status the provider reports (<c>data.status</c>), if any.</param>
    private sealed record PayloadRefs(IReadOnlyList<string> Candidates, string? SubscriptionRef, string? ProviderStatus);

    private static readonly PayloadRefs NoRefs = new(Array.Empty<string>(), null, null);

    /// <summary>Pull the provider references (and reported status) from a rail's webhook payload.</summary>
    private static PayloadRefs ExtractRefs(string providerName, string type, string rawPayload)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawPayload);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return NoRefs;

            if (string.Equals(providerName, "crypto", StringComparison.OrdinalIgnoreCase))
            {
                // Coinbase Commerce: { "event": { "data": { "code": "CHG..." } } }
                if (root.TryGetProperty("event", out var ev) && ev.ValueKind == JsonValueKind.Object &&
                    ev.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
                {
                    var charge = Str(data, "code") ?? Str(data, "id");
                    return charge is null ? NoRefs : new PayloadRefs(new[] { charge }, null, null);
                }
                return NoRefs;
            }

            // Paddle Billing: { "event_type": "...", "data": { "id", "status", "subscription_id"?, "transaction_id"? } }
            if (!root.TryGetProperty("data", out var pdata) || pdata.ValueKind != JsonValueKind.Object)
                return NoRefs;

            var id = Str(pdata, "id");
            var subscriptionId = Str(pdata, "subscription_id");
            var transactionId = Str(pdata, "transaction_id");
            var isSubscriptionEvent = type.StartsWith("subscription.", StringComparison.Ordinal);

            var candidates = new List<string>(3);
            foreach (var candidate in new[] { id, subscriptionId, transactionId })
                if (!string.IsNullOrEmpty(candidate) && !candidates.Contains(candidate, StringComparer.Ordinal))
                    candidates.Add(candidate);

            // data.id IS the subscription id on a subscription.* event; on a transaction.* event the
            // subscription is named by data.subscription_id (null until Paddle has created it).
            var subscriptionRef = isSubscriptionEvent ? id : subscriptionId;
            var providerStatus = isSubscriptionEvent ? Str(pdata, "status") : null;
            return new PayloadRefs(candidates, subscriptionRef, providerStatus);
        }
        catch (JsonException)
        {
            return NoRefs;
        }
    }

    private static string? Str(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
