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
/// Turns a verified, deduped provider webhook into a subscription status transition. The event type maps
/// to a target status (activate / past-due / cancel); the provider reference in the payload finds our
/// subscription (linked at checkout). Unknown types and unmatched refs are safe no-ops (logged).
/// </summary>
public interface IBillingWebhookProcessor
{
    /// <summary>Apply a verified webhook event to the linked subscription (no-op when it maps to nothing).</summary>
    Task ProcessAsync(string providerName, ProviderWebhookEvent evt, CancellationToken ct = default);
}

/// <inheritdoc />
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
        var status = MapStatus(evt.Type);
        if (status is null)
        {
            _logger.LogInformation("Billing webhook {Provider}/{Type} carries no status transition — ignored.", providerName, evt.Type);
            return;
        }

        var providerRef = ExtractProviderRef(providerName, evt.RawPayload);
        if (providerRef is null)
        {
            _logger.LogWarning("Billing webhook {Provider}/{Type}: no provider reference in payload — skipped.", providerName, evt.Type);
            return;
        }

        var sub = await _store.GetSubscriptionByProviderRefAsync(providerName, providerRef, ct);
        if (sub is null)
        {
            _logger.LogWarning("Billing webhook {Provider}/{Type}: no subscription linked to ref {Ref} — skipped.", providerName, evt.Type, providerRef);
            return;
        }

        await _store.UpdateSubscriptionStatusAsync(sub.Id, status, ct);
        _logger.LogInformation("Billing webhook {Provider}/{Type}: subscription {Sub} → {Status}.", providerName, evt.Type, sub.Id, status);
    }

    /// <summary>Event type → subscription status, or null when the event isn't a transition.</summary>
    public static string? MapStatus(string type) => type switch
    {
        "subscription.activated" or "subscription.created" or "subscription.resumed"
            or "charge:confirmed" or "charge:resolved" => "active",
        "subscription.past_due" or "charge:failed" => "past_due",
        "subscription.canceled" => "canceled",
        _ => null,
    };

    /// <summary>Pull the provider subscription/charge reference from a rail's webhook payload.</summary>
    private static string? ExtractProviderRef(string providerName, string rawPayload)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawPayload);
            var root = doc.RootElement;

            if (string.Equals(providerName, "crypto", StringComparison.OrdinalIgnoreCase))
            {
                // Coinbase Commerce: { "event": { "data": { "code": "CHG..." } } }
                if (root.TryGetProperty("event", out var ev) && ev.TryGetProperty("data", out var data))
                    return data.TryGetProperty("code", out var code) ? code.GetString()
                         : data.TryGetProperty("id", out var id) ? id.GetString() : null;
                return null;
            }

            // Paddle: { "data": { "id": "sub_..." } }
            return root.TryGetProperty("data", out var pdata) && pdata.TryGetProperty("id", out var pid)
                ? pid.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
