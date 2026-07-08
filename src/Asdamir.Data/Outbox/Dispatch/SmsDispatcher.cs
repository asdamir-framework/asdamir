// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Microsoft.Extensions.Logging;
namespace Asdamir.Data.Outbox;

/// <summary>
/// Stub SMS dispatcher — logs the would-be send and succeeds. Real provider (Twilio /
/// Vonage / per-tenant gateway) plugs in later by replacing this implementation; the
/// worker contract (MessageType=1) stays the same.
/// </summary>
public sealed class SmsDispatcher : IOutboxDispatcher
{
    /// <inheritdoc/>
    public byte MessageType => 1; // SMS

    private readonly ILogger<SmsDispatcher> _logger;

    /// <summary>Creates the logs-only SMS dispatcher (the stub provider).</summary>
    public SmsDispatcher(ILogger<SmsDispatcher> logger) => _logger = logger;

    /// <inheritdoc/>
    public Task DispatchAsync(ClaimedOutboxMessage message, CancellationToken ct)
    {
        _logger.LogInformation("SmsDispatcher (stub) would send to {Phone}: {Body}", message.Destination, message.Body);
        return Task.CompletedTask;
    }
}
