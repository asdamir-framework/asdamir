// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.Data;
using Asdamir.Core.Contracts;
using Dapper;

namespace Asdamir.Payments;

/// <summary>
/// The free-mode (Model B) billing store: a Dapper-backed <see cref="IBillingStore"/> over an app's OWN
/// single-tenant database. It is the app-local counterpart of AppManagement's control-plane store — same
/// operational procs (Plan_List / BillingAccount_Create / Subscription_Create / …), but the <b>AppId-free</b>
/// versions emitted into the app's own <c>db/migrations</c> by <c>FreeModeBillingSchema.sql</c> /
/// <c>FreeModeBillingProcs.sql</c>. Because a free app IS the tenant, there is no AppId dimension: the
/// <c>appId</c> parameter on <see cref="CreateAccountAsync"/> exists only to satisfy the shared
/// interface (Model A needs it) and is deliberately ignored here — no AppId column is written.
///
/// <para>Connections come from <see cref="IDbConnectionFactory"/> (AUD002 — never a bare SqlConnection). Action
/// procs <c>SET NOCOUNT ON</c> and <c>SELECT @@ROWCOUNT</c>, so affected-row counts are read as a scalar, never
/// inferred from <c>ExecuteAsync()</c> (the NOCOUNT/@@ROWCOUNT rule).</para>
/// </summary>
public sealed class LocalDbBillingStore : IBillingStore
{
    private readonly IDbConnectionFactory _factory;

    /// <summary>Create the store over the app's own-database connection factory.</summary>
    public LocalDbBillingStore(IDbConnectionFactory factory) => _factory = factory;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PlanDto>> ListPlansAsync(CancellationToken ct = default)
    {
        using var conn = await _factory.CreateAsync(ct);
        var rows = await conn.QueryAsync<PlanDto>("dbo.Plan_List", commandType: CommandType.StoredProcedure);
        return rows.ToList();
    }

    /// <inheritdoc/>
    public async Task<PlanDto?> GetPlanByCodeAsync(string code, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<PlanDto>("dbo.Plan_GetByCode",
            new { code }, commandType: CommandType.StoredProcedure);
    }

    /// <inheritdoc/>
    public async Task<BillingAccountDto?> GetAccountByOwnerAsync(int ownerUserId, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<BillingAccountDto>("dbo.BillingAccount_GetByOwner",
            new { ownerUserId }, commandType: CommandType.StoredProcedure);
    }

    /// <inheritdoc/>
    public async Task<SubscriptionDto?> GetActiveSubscriptionAsync(Guid billingAccountId, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<SubscriptionDto>("dbo.Subscription_GetActiveByAccount",
            new { billingAccountId }, commandType: CommandType.StoredProcedure);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Model B is single-tenant — <paramref name="appId"/> is accepted for interface parity but IGNORED
    /// (never passed to the proc, never written): the free-mode <c>BillingAccount_Create</c> has no
    /// <c>@appId</c> parameter and the table has no AppId column.
    /// </remarks>
    public async Task<Guid> CreateAccountAsync(string name, string email, int ownerUserId, Guid? appId, CancellationToken ct = default)
    {
        // appId intentionally unused: the whole DB belongs to one app (the app IS the tenant).
        using var conn = await _factory.CreateAsync(ct);
        return await conn.ExecuteScalarAsync<Guid>("dbo.BillingAccount_Create",
            new { name, email, ownerUserId }, commandType: CommandType.StoredProcedure);
    }

    /// <inheritdoc/>
    public async Task<Guid> CreateSubscriptionAsync(Guid billingAccountId, Guid planId, string status, DateTime? trialEndsAtUtc, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateAsync(ct);
        return await conn.ExecuteScalarAsync<Guid>("dbo.Subscription_Create",
            new { billingAccountId, planId, status, trialEndsAtUtc }, commandType: CommandType.StoredProcedure);
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateSubscriptionStatusAsync(Guid subscriptionId, string status, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateAsync(ct);
        var rows = await conn.ExecuteScalarAsync<int>("dbo.Subscription_UpdateStatus",
            new { id = subscriptionId, status }, commandType: CommandType.StoredProcedure);
        return rows > 0;   // proc SELECTs @@ROWCOUNT (NOCOUNT ON)
    }

    /// <inheritdoc/>
    public async Task<bool> TryRecordWebhookAsync(string provider, string providerEventId, string? type, string? payload, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateAsync(ct);
        var recorded = await conn.ExecuteScalarAsync<int>("dbo.BillingWebhookEvent_TryRecord",
            new { provider, providerEventId, type, payload }, commandType: CommandType.StoredProcedure);
        return recorded == 1;   // 1 = newly recorded, 0 = duplicate
    }

    /// <inheritdoc/>
    public async Task MarkWebhookProcessedAsync(string provider, string providerEventId, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateAsync(ct);
        await conn.ExecuteScalarAsync<int>("dbo.BillingWebhookEvent_MarkProcessed",
            new { provider, providerEventId }, commandType: CommandType.StoredProcedure);
    }

    /// <inheritdoc/>
    public async Task<SubscriptionDto?> GetSubscriptionByProviderRefAsync(string provider, string providerRef, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<SubscriptionDto>("dbo.Subscription_GetByProviderRef",
            new { provider, providerRef }, commandType: CommandType.StoredProcedure);
    }

    /// <inheritdoc/>
    public async Task LinkSubscriptionToProviderAsync(Guid subscriptionId, string provider, string providerRef, CancellationToken ct = default)
    {
        using var conn = await _factory.CreateAsync(ct);
        await conn.ExecuteScalarAsync<int>("dbo.Subscription_SetProviderRef",
            new { id = subscriptionId, provider, providerRef }, commandType: CommandType.StoredProcedure);
    }
}
