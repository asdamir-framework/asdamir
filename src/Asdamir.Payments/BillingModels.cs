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

/// <summary>A subscription plan (catalog row). Maps to <c>dbo.Plans</c> / <c>Plan_List</c>.</summary>
public sealed record PlanDto(
    Guid Id, string Code, string Name, decimal Price, string Currency, string Interval,
    int TrialDays, string? FeaturesJson, string? PaddlePriceId,
    bool IsActive, int SortOrder);

/// <summary>
/// The subscriber. Maps to <c>dbo.BillingAccounts</c> / <c>BillingAccount_GetByOwner</c>.
/// <paramref name="AppId"/> is the owning generated app (AsdamirVault_114, Model A) — NULL for a
/// console/operator (self-app) account, or for an app-local (Model B) account.
/// </summary>
public sealed record BillingAccountDto(
    Guid Id, string Name, string Email, int? OwnerUserId, Guid? TenantId,
    string? PaddleCustomerRef, bool IsActive, Guid? AppId = null);

/// <summary>An account's active subscription joined with its plan. Maps to <c>Subscription_GetActiveByAccount</c>.</summary>
public sealed record SubscriptionDto(
    Guid Id, Guid BillingAccountId, Guid PlanId, string PlanCode, string PlanName, string? FeaturesJson,
    string Status, string? Provider, string? ProviderSubscriptionId,
    DateTime? CurrentPeriodStartUtc, DateTime? CurrentPeriodEndUtc, DateTime? TrialEndsAtUtc, bool CancelAtPeriodEnd);

/// <summary>An issued invoice. Maps to <c>dbo.Invoices</c> / <c>Invoice_ListByAccount</c>.</summary>
public sealed record InvoiceDto(
    Guid Id, Guid BillingAccountId, Guid? SubscriptionId, string? Number, decimal Amount, string Currency,
    string Status, DateTime? DueAtUtc, DateTime? PaidAtUtc, string? ProviderInvoiceId, string? PdfPath, DateTime CreatedAtUtc);

/// <summary>A stored payment-method reference (tokens only). Maps to <c>dbo.PaymentMethods</c> / <c>PaymentMethod_ListByAccount</c>.</summary>
public sealed record PaymentMethodDto(
    Guid Id, Guid BillingAccountId, string Provider, string? Type, string? Brand, string? Last4,
    int? ExpMonth, int? ExpYear, bool IsDefault, DateTime CreatedAtUtc);

/// <summary>Subscribe request body — the plan code to subscribe to (<c>free</c>/<c>pro</c>/<c>business</c>).</summary>
public sealed record SubscribeRequest(string PlanCode);

/// <summary>Checkout request — pay for a plan on a rail (<c>paddle</c> / <c>crypto</c>).</summary>
public sealed record CheckoutRequest(string PlanCode, string Rail = "paddle", string? SuccessUrl = null, string? CancelUrl = null);

/// <summary>Checkout result — the hosted-checkout handle to redirect/embed for the chosen rail.</summary>
public sealed record CheckoutResult(string SessionId, string? CheckoutUrl, string? ClientToken, string Rail);
