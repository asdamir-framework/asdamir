// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Asdamir.Core.Configuration;
using Asdamir.Core.Contracts.Billing;
using Asdamir.Core.ErrorHandling.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Asdamir.Payments.Providers;

/// <summary>
/// Paddle Billing implementation of <see cref="IPaymentProvider"/> — the global Merchant-of-Record.
/// Paddle handles card data, tax and invoicing (MoR asymmetry): we store Paddle transaction/subscription
/// references, never a card token. No SDK — a typed <see cref="HttpClient"/> against the Paddle Billing
/// REST API (base URL + bearer key configured in DI from <see cref="PaddleOptions"/>). Fail-closed with no
/// API key; provider/HTTP errors map to <see cref="Result{T}"/>. Webhooks verify the <c>Paddle-Signature</c>
/// HMAC header (<see cref="VerifyWebhookAsync"/>).
/// </summary>
public sealed class PaddlePaymentProvider : IPaymentProvider
{
    private const long ToleranceSeconds = 300;

    private readonly HttpClient _http;
    private readonly PaddleOptions _options;
    private readonly ILogger<PaddlePaymentProvider> _logger;

    /// <summary>Create the Paddle provider over a typed <see cref="HttpClient"/> and bound options.</summary>
    public PaddlePaymentProvider(HttpClient http, IOptions<PaymentProviderOptions> options, ILogger<PaddlePaymentProvider> logger)
    {
        _http = http;
        _options = options.Value.Paddle;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "paddle";

    private bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    private static Result<T> NotConfigured<T>() =>
        Result<T>.Fail("billing.paddle.not_configured", "Ödeme sağlayıcı (Paddle) yapılandırılmadı.", "The payment provider (Paddle) is not configured.", "Платёжный провайдер (Paddle) не настроен.");

    private Result<T> Failed<T>(Exception ex, string op)
    {
        _logger.LogWarning(ex, "Paddle {Operation} failed.", op);
        return Result<T>.Fail("billing.paddle.error", "Ödeme sağlayıcı hatası.", "Payment provider error.", "Ошибка платёжного провайдера.");
    }

    private static Result NotConfiguredVoid() =>
        Result.Fail("billing.paddle.not_configured", "Ödeme sağlayıcı (Paddle) yapılandırılmadı.", "The payment provider (Paddle) is not configured.", "Платёжный провайдер (Paddle) не настроен.");

    private Result FailedVoid(Exception ex, string op)
    {
        _logger.LogWarning(ex, "Paddle {Operation} failed.", op);
        return Result.Fail("billing.paddle.error", "Ödeme sağlayıcı hatası.", "Payment provider error.", "Ошибка платёжного провайдера.");
    }

    /// <inheritdoc />
    public async Task<Result<ProviderCustomer>> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken ct = default)
    {
        if (!IsConfigured) return NotConfigured<ProviderCustomer>();
        try
        {
            using var resp = await _http.PostAsJsonAsync("customers", new { email = request.Email, name = request.Name }, ct);
            if (!resp.IsSuccessStatusCode) return Failed<ProviderCustomer>(await ApiError(resp, ct), nameof(CreateCustomerAsync));
            var id = await ReadDataStringAsync(resp, "id", ct);
            return id is null
                ? Failed<ProviderCustomer>(new InvalidOperationException("no customer id"), nameof(CreateCustomerAsync))
                : Result<ProviderCustomer>.Ok(new ProviderCustomer(id));
        }
        catch (HttpRequestException ex) { return Failed<ProviderCustomer>(ex, nameof(CreateCustomerAsync)); }
    }

    /// <inheritdoc />
    public async Task<Result<CheckoutSession>> CreateCheckoutSessionAsync(CreateCheckoutRequest request, CancellationToken ct = default)
    {
        if (!IsConfigured) return NotConfigured<CheckoutSession>();
        try
        {
            // A Paddle transaction with a recurring price yields a hosted-checkout URL (data.checkout.url).
            var body = new
            {
                items = new[] { new { price_id = request.PlanPriceRef, quantity = 1 } },
                customer_id = request.ProviderCustomerId,
                collection_mode = "automatic",
            };
            using var resp = await _http.PostAsJsonAsync("transactions", body, ct);
            if (!resp.IsSuccessStatusCode) return Failed<CheckoutSession>(await ApiError(resp, ct), nameof(CreateCheckoutSessionAsync));

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var data = doc.RootElement.GetProperty("data");
            var txnId = data.GetProperty("id").GetString() ?? "";
            var url = data.TryGetProperty("checkout", out var co) && co.ValueKind == JsonValueKind.Object
                      && co.TryGetProperty("url", out var u) ? u.GetString() : null;
            return Result<CheckoutSession>.Ok(new CheckoutSession(txnId, url));
        }
        catch (HttpRequestException ex) { return Failed<CheckoutSession>(ex, nameof(CreateCheckoutSessionAsync)); }
        catch (JsonException ex) { return Failed<CheckoutSession>(ex, nameof(CreateCheckoutSessionAsync)); }
    }

    /// <inheritdoc />
    public async Task<Result<ProviderSubscription>> CreateSubscriptionAsync(CreateSubscriptionRequest request, CancellationToken ct = default)
    {
        if (!IsConfigured) return NotConfigured<ProviderSubscription>();
        try
        {
            // In Paddle Billing a subscription materialises when a transaction is paid. We create the
            // recurring transaction and return a pending handle; the subscription id arrives via webhook.
            var body = new
            {
                items = new[] { new { price_id = request.PlanPriceRef, quantity = 1 } },
                customer_id = request.ProviderCustomerId,
                collection_mode = "automatic",
            };
            using var resp = await _http.PostAsJsonAsync("transactions", body, ct);
            if (!resp.IsSuccessStatusCode) return Failed<ProviderSubscription>(await ApiError(resp, ct), nameof(CreateSubscriptionAsync));
            var txnId = await ReadDataStringAsync(resp, "id", ct) ?? "";
            return Result<ProviderSubscription>.Ok(new ProviderSubscription(txnId, "pending"));
        }
        catch (HttpRequestException ex) { return Failed<ProviderSubscription>(ex, nameof(CreateSubscriptionAsync)); }
    }

    /// <inheritdoc />
    public async Task<Result> CancelSubscriptionAsync(string providerSubscriptionId, bool atPeriodEnd, CancellationToken ct = default)
    {
        if (!IsConfigured) return NotConfiguredVoid();
        try
        {
            var body = new { effective_from = atPeriodEnd ? "next_billing_period" : "immediately" };
            using var resp = await _http.PostAsJsonAsync($"subscriptions/{providerSubscriptionId}/cancel", body, ct);
            return resp.IsSuccessStatusCode ? Result.Ok() : FailedVoid(await ApiError(resp, ct), nameof(CancelSubscriptionAsync));
        }
        catch (HttpRequestException ex) { return FailedVoid(ex, nameof(CancelSubscriptionAsync)); }
    }

    /// <inheritdoc />
    public async Task<Result> RefundAsync(string providerPaymentId, decimal? amount, CancellationToken ct = default)
    {
        if (!IsConfigured) return NotConfiguredVoid();
        try
        {
            // Paddle refunds are "adjustments" against a transaction. A full refund when no amount is given.
            var body = new
            {
                action = "refund",
                transaction_id = providerPaymentId,
                reason = "requested_by_customer",
                items = amount.HasValue
                    ? (object)new[] { new { type = "partial", amount = ((long)(amount.Value * 100m)).ToString() } }
                    : new[] { new { type = "full" } },
            };
            using var resp = await _http.PostAsJsonAsync("adjustments", body, ct);
            return resp.IsSuccessStatusCode ? Result.Ok() : FailedVoid(await ApiError(resp, ct), nameof(RefundAsync));
        }
        catch (HttpRequestException ex) { return FailedVoid(ex, nameof(RefundAsync)); }
    }

    /// <inheritdoc />
    public Task<Result<ProviderWebhookEvent>> VerifyWebhookAsync(WebhookVerificationRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSecret))
            return Task.FromResult(NotConfigured<ProviderWebhookEvent>());
        if (!request.Headers.TryGetValue("Paddle-Signature", out var header) || string.IsNullOrEmpty(header))
            return Task.FromResult(Result<ProviderWebhookEvent>.Fail(
                "billing.webhook.no_signature", "Webhook imzası yok.", "Missing webhook signature.", "Отсутствует подпись вебхука."));

        return Task.FromResult(Verify(header, request.RawBody));
    }

    // Paddle-Signature: "ts=<unix>;h1=<hmac-sha256 hex of `ts:rawBody`>". Fail-closed on any mismatch.
    private Result<ProviderWebhookEvent> Verify(string header, string rawBody)
    {
        string? ts = null, h1 = null;
        foreach (var part in header.Split(';'))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            if (kv[0] == "ts") ts = kv[1];
            else if (kv[0] == "h1") h1 = kv[1];
        }
        if (ts is null || h1 is null || !long.TryParse(ts, out var tsVal))
            return Invalid();

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - tsVal) > ToleranceSeconds)
            return Invalid();

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.WebhookSecret));
        var computed = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{ts}:{rawBody}"));

        byte[] provided;
        try { provided = Convert.FromHexString(h1); }
        catch (FormatException) { return Invalid(); }

        if (provided.Length != computed.Length || !CryptographicOperations.FixedTimeEquals(provided, computed))
            return Invalid();

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            var eventId = root.TryGetProperty("event_id", out var e) ? e.GetString() : null;
            var eventType = root.TryGetProperty("event_type", out var t) ? t.GetString() : null;
            if (string.IsNullOrEmpty(eventId))
                return Invalid();
            return Result<ProviderWebhookEvent>.Ok(new ProviderWebhookEvent(eventId, eventType ?? "unknown", rawBody));
        }
        catch (JsonException) { return Invalid(); }
    }

    private Result<ProviderWebhookEvent> Invalid()
    {
        _logger.LogWarning("Paddle webhook signature verification failed.");
        return Result<ProviderWebhookEvent>.Fail(
            "billing.webhook.invalid_signature", "Webhook imzası doğrulanamadı.", "Webhook signature verification failed.", "Не удалось проверить подпись вебхука.");
    }

    private static async Task<string?> ReadDataStringAsync(HttpResponseMessage resp, string prop, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return doc.RootElement.TryGetProperty("data", out var data) && data.TryGetProperty(prop, out var v)
            ? v.GetString() : null;
    }

    private static async Task<Exception> ApiError(HttpResponseMessage resp, CancellationToken ct)
    {
        var body = await resp.Content.ReadAsStringAsync(ct);
        return new HttpRequestException($"Paddle API {(int)resp.StatusCode}: {body}");
    }
}
