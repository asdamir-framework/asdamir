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
using Microsoft.Extensions.Options;

namespace Asdamir.Payments;

/// <summary>
/// The single Payment Service facade. One entry point over the payment rails, each an
/// <see cref="IPaymentProvider"/>: <c>paddle</c> (card / Apple Pay / Google Pay) and <c>crypto</c>
/// (Bitcoin / Ethereum / USDC). Callers pick a rail by name; the facade resolves the provider. Adding a
/// rail is one more <see cref="IPaymentProvider"/> registration — no caller change.
/// </summary>
public interface IPaymentService
{
    /// <summary>The available rails (provider names), e.g. <c>["paddle","crypto"]</c>.</summary>
    IReadOnlyCollection<string> Rails { get; }

    /// <summary>Resolve the provider for a rail; false when the rail is unknown.</summary>
    bool TryGetProvider(string rail, out IPaymentProvider provider);
}

/// <inheritdoc />
public sealed class PaymentService : IPaymentService
{
    private readonly IReadOnlyDictionary<string, IPaymentProvider> _byName;
    private readonly bool _cryptoEnabled;

    /// <summary>Index the registered rails by name and capture whether the crypto rail is enabled.</summary>
    public PaymentService(IEnumerable<IPaymentProvider> providers, IOptions<PaymentProviderOptions> options)
    {
        _byName = providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        _cryptoEnabled = options.Value.Crypto.Enabled;
    }

    // The crypto rail is hidden from the list while disabled (policy) — but stays RESOLVABLE via
    // TryGetProvider so a crypto checkout returns the specific billing.crypto.disabled, not rail_unknown.
    /// <inheritdoc />
    public IReadOnlyCollection<string> Rails =>
        _byName.Keys.Where(n => _cryptoEnabled || !n.Equals("crypto", StringComparison.OrdinalIgnoreCase)).ToArray();

    /// <inheritdoc />
    public bool TryGetProvider(string rail, out IPaymentProvider provider)
    {
        if (rail is not null && _byName.TryGetValue(rail, out var p))
        {
            provider = p;
            return true;
        }
        provider = default!;
        return false;
    }
}
