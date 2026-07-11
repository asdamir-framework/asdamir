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
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Asdamir.Payments.Providers;

/// <summary>
/// Crypto rail of <see cref="IPaymentProvider"/> — Bitcoin / Ethereum / USDC via a hosted crypto gateway
/// (Coinbase Commerce). No SDK — a typed <see cref="HttpClient"/> against the Commerce REST API. Crypto is
/// charge-based (one-time), so <see cref="CreateCheckoutSessionAsync"/> (a hosted charge) and
/// <see cref="VerifyWebhookAsync"/> (X-CC-Webhook-Signature HMAC) are the real surface; native recurring and
/// automated refunds don't exist on-chain, so those return an honest typed failure. Fail-closed with no key.
/// </summary>
public sealed class CryptoPaymentProvider : IPaymentProvider
{
    private readonly HttpClient _http;
    private readonly CryptoOptions _options;
    private readonly IHttpContextAccessor _httpContext;
    private readonly ILogger<CryptoPaymentProvider> _logger;

    /// <summary>Create the crypto provider over a typed <see cref="HttpClient"/>, bound options and the request context (for the geo-gate).</summary>
    public CryptoPaymentProvider(HttpClient http, IOptions<PaymentProviderOptions> options, IHttpContextAccessor httpContext, ILogger<CryptoPaymentProvider> logger)
    {
        _http = http;
        _options = options.Value.Crypto;
        _httpContext = httpContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "crypto";

    private bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    /// <summary>A single geo header (ISO-3166 alpha-2), or null when absent/undetectable.</summary>
    private string? HeaderCountry(string name)
    {
        var c = _httpContext.HttpContext?.Request.Headers[name].ToString().Trim().ToUpperInvariant();
        // Cloudflare emits XX (unknown) / T1 (Tor). Treat those + empty as undetectable.
        return c is null or "" or "XX" or "T1" ? null : c;
    }

    private bool IsBlocked(string country) => _options.BlockedBuyerCountries.Contains(country, StringComparer.OrdinalIgnoreCase);

    private static Result<CheckoutSession> RegionBlocked() => Result<CheckoutSession>.Fail(
        "billing.crypto.region_blocked", "Kripto ödeme bölgenizde kullanılamıyor.", "Crypto payment is not available in your region.", "Крипто-платёж недоступен в вашем регионе.");

    private static Result<CheckoutSession> RegionUnknown() => Result<CheckoutSession>.Fail(
        "billing.crypto.region_unknown", "Ülke belirlenemediği için kripto ödeme kullanılamıyor.", "Crypto payment is unavailable because the country could not be determined.", "Крипто-платёж недоступен: страна не определена.");

    private static Result<T> NotConfigured<T>() =>
        Result<T>.Fail("billing.crypto.not_configured", "Kripto ödeme yapılandırılmadı.", "The crypto payment gateway is not configured.", "Крипто-платёж не настроен.");

    private Result<T> Failed<T>(Exception ex, string op)
    {
        _logger.LogWarning(ex, "Crypto {Operation} failed.", op);
        return Result<T>.Fail("billing.crypto.error", "Kripto ödeme hatası.", "Crypto payment error.", "Ошибка крипто-платежа.");
    }

    /// <inheritdoc />
    // Crypto has no customer entity — the billing account's own id is the reference.
    public Task<Result<ProviderCustomer>> CreateCustomerAsync(CreateCustomerRequest request, CancellationToken ct = default)
        => Task.FromResult(Result<ProviderCustomer>.Ok(new ProviderCustomer(request.ExternalRef ?? request.Email)));

    /// <inheritdoc />
    public async Task<Result<CheckoutSession>> CreateCheckoutSessionAsync(CreateCheckoutRequest request, CancellationToken ct = default)
    {
        // Policy gate (CLAUDE.md "Crypto rail policy" / docs/design/billing.md §2.1):
        // default-disabled + TR-buyer geo-gate, fail-closed on an undetectable country.
        if (!_options.Enabled)
            return Result<CheckoutSession>.Fail("billing.crypto.disabled",
                "Kripto ödeme şu anda kapalı.", "Crypto payment is currently disabled.", "Крипто-платёж сейчас отключён.");

        // Trust ONLY the edge-set CF-IPCountry (a client cannot forge it behind Cloudflare, which strips any
        // client-sent value). The client-supplied X-Buyer-Country is spoofable, so it may only BLOCK a
        // self-declared blocked country — it can NEVER let a checkout through. So a TR buyer sending
        // "X-Buyer-Country: US" does not pass: with no trusted geo we fail-closed to region_unknown.
        var trusted = HeaderCountry("CF-IPCountry");
        if (trusted is not null)
        {
            if (IsBlocked(trusted))
            {
                _logger.LogWarning("Crypto checkout blocked for country {Country} (trusted geo).", trusted);
                return RegionBlocked();
            }
            // trusted + not blocked → allowed to proceed.
        }
        else
        {
            var claimed = HeaderCountry("X-Buyer-Country");
            if (claimed is not null && IsBlocked(claimed))
            {
                _logger.LogWarning("Crypto checkout blocked by self-declared country {Country} (no trusted geo).", claimed);
                return RegionBlocked();
            }
            _logger.LogWarning("Crypto checkout blocked: no trusted buyer country (a client claim cannot pass; fail-closed).");
            return RegionUnknown();
        }

        if (!IsConfigured) return NotConfigured<CheckoutSession>();
        if (request.Amount is not { } amount)
            return Result<CheckoutSession>.Fail("billing.crypto.amount_required", "Kripto ödeme için tutar gerekli.", "An amount is required for a crypto charge.", "Для крипто-платежа требуется сумма.");
        try
        {
            var body = new
            {
                name = request.Description ?? "Subscription",
                description = request.Description ?? "Subscription payment",
                pricing_type = "fixed_price",
                local_price = new { amount = amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), currency = request.Currency },
                redirect_url = request.SuccessUrl,
                cancel_url = request.CancelUrl,
            };
            using var resp = await _http.PostAsJsonAsync("charges", body, ct);
            if (!resp.IsSuccessStatusCode) return Failed<CheckoutSession>(await ApiError(resp, ct), nameof(CreateCheckoutSessionAsync));

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var data = doc.RootElement.GetProperty("data");
            var code = data.GetProperty("code").GetString() ?? "";
            var url = data.TryGetProperty("hosted_url", out var u) ? u.GetString() : null;
            return Result<CheckoutSession>.Ok(new CheckoutSession(code, url));
        }
        catch (HttpRequestException ex) { return Failed<CheckoutSession>(ex, nameof(CreateCheckoutSessionAsync)); }
        catch (JsonException ex) { return Failed<CheckoutSession>(ex, nameof(CreateCheckoutSessionAsync)); }
    }

    /// <inheritdoc />
    // On-chain payments are one-time; recurring subscriptions are handled as prepaid charges per period.
    public Task<Result<ProviderSubscription>> CreateSubscriptionAsync(CreateSubscriptionRequest request, CancellationToken ct = default)
        => Task.FromResult(Result<ProviderSubscription>.Fail("billing.crypto.recurring_unsupported", "Kripto rayında otomatik yenilenen abonelik yoktur; her dönem ön ödemeli ücretlendirilir.", "The crypto rail has no native recurring subscription; each period is charged as a prepaid checkout.", "На крипто-канале нет автоматической подписки; каждый период оплачивается заранее."));

    /// <inheritdoc />
    public Task<Result> CancelSubscriptionAsync(string providerSubscriptionId, bool atPeriodEnd, CancellationToken ct = default)
        => Task.FromResult(Result.Fail("billing.crypto.recurring_unsupported", "Kripto rayında iptal edilecek otomatik abonelik yoktur.", "The crypto rail has no auto-renewing subscription to cancel.", "На крипто-канале нет автоматической подписки для отмены."));

    /// <inheritdoc />
    public Task<Result> RefundAsync(string providerPaymentId, decimal? amount, CancellationToken ct = default)
        => Task.FromResult(Result.Fail("billing.crypto.refund_manual", "Kripto iadeleri elle (on-chain) yapılır.", "Crypto refunds are handled manually on-chain.", "Возвраты в крипте выполняются вручную в блокчейне."));

    /// <inheritdoc />
    public Task<Result<ProviderWebhookEvent>> VerifyWebhookAsync(WebhookVerificationRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookSecret))
            return Task.FromResult(NotConfigured<ProviderWebhookEvent>());
        if (!request.Headers.TryGetValue("X-CC-Webhook-Signature", out var signature) || string.IsNullOrEmpty(signature))
            return Task.FromResult(Result<ProviderWebhookEvent>.Fail(
                "billing.webhook.no_signature", "Webhook imzası yok.", "Missing webhook signature.", "Отсутствует подпись вебхука."));

        return Task.FromResult(Verify(signature, request.RawBody));
    }

    // Coinbase Commerce: X-CC-Webhook-Signature = HMAC-SHA256(rawBody, sharedSecret) hex. Fail-closed.
    private Result<ProviderWebhookEvent> Verify(string signature, string rawBody)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.WebhookSecret));
        var computed = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));

        byte[] provided;
        try { provided = Convert.FromHexString(signature); }
        catch (FormatException) { return Invalid(); }

        if (provided.Length != computed.Length || !CryptographicOperations.FixedTimeEquals(provided, computed))
            return Invalid();

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            if (!doc.RootElement.TryGetProperty("event", out var evt))
                return Invalid();
            var eventId = evt.TryGetProperty("id", out var i) ? i.GetString() : null;
            var eventType = evt.TryGetProperty("type", out var t) ? t.GetString() : null;
            if (string.IsNullOrEmpty(eventId))
                return Invalid();
            return Result<ProviderWebhookEvent>.Ok(new ProviderWebhookEvent(eventId, eventType ?? "unknown", rawBody));
        }
        catch (JsonException) { return Invalid(); }
    }

    private Result<ProviderWebhookEvent> Invalid()
    {
        _logger.LogWarning("Crypto webhook signature verification failed.");
        return Result<ProviderWebhookEvent>.Fail(
            "billing.webhook.invalid_signature", "Webhook imzası doğrulanamadı.", "Webhook signature verification failed.", "Не удалось проверить подпись вебхука.");
    }

    private static async Task<Exception> ApiError(HttpResponseMessage resp, CancellationToken ct)
    {
        var body = await resp.Content.ReadAsStringAsync(ct);
        return new HttpRequestException($"Crypto gateway {(int)resp.StatusCode}: {body}");
    }
}
