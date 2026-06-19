// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Data.Outbox;

/// <summary>
/// Config for the outbox worker. Bound from configuration section <c>Outbox:Worker</c>.
/// Defaults: enabled on Dapper persistence only, 1s poll, batch of 25, exponential backoff
/// starting at 30s capped at 1h with ±20% jitter.
/// </summary>
public sealed class OutboxWorkerOptions
{
    public const string Section = "Outbox:Worker";

    public bool Enabled { get; set; } = true;
    public int BatchSize { get; set; } = 25;
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan IdleBackoff { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan ReclaimInterval { get; set; } = TimeSpan.FromMinutes(2);
    public int ReclaimStaleThresholdMinutes { get; set; } = 5;

    // Backoff: delay = clamp(BackoffBaseSeconds * 2^(tryCount-1) ± jitter%, max=BackoffMaxSeconds).
    public int BackoffBaseSeconds { get; set; } = 30;
    public int BackoffMaxSeconds { get; set; } = 3600;
    public double JitterPercent { get; set; } = 0.20;

    /// <summary>Worker id stamped into <c>LockedBy</c> — defaults to machine name + process id.</summary>
    public string? WorkerId { get; set; }
}

/// <summary>
/// SMTP transport config for <see cref="MailDispatcher"/>. Bound from section <c>Smtp</c>.
/// SecureSocketOption maps onto MailKit's enum: <c>None</c>/<c>Auto</c>/<c>StartTls</c>/<c>SslOnConnect</c>.
/// </summary>
public sealed class SmtpOptions
{
    public const string Section = "Smtp";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 25;
    public string Security { get; set; } = "Auto"; // None / Auto / StartTls / SslOnConnect
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string DefaultFromAddress { get; set; } = "noreply@localhost";
    public string? DefaultFromName { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// SMS dispatcher selection. <c>Provider="Stub"</c> (default) keeps logs-only behavior;
/// <c>Provider="Twilio"</c> wires <see cref="TwilioSmsDispatcher"/> using <see cref="TwilioOptions"/>.
/// Bound from <c>Sms</c> section.
/// </summary>
public sealed class SmsOptions
{
    public const string Section = "Sms";

    public string Provider { get; set; } = "Stub"; // Stub | Twilio
}

/// <summary>
/// Twilio REST client credentials + default from-number. AccountSid + AuthToken are
/// secrets — store via env / user-secrets, not in appsettings. Bound from <c>Twilio</c>.
/// </summary>
public sealed class TwilioOptions
{
    public const string Section = "Twilio";

    public string? AccountSid { get; set; }
    public string? AuthToken { get; set; }
    /// <summary>E.164 phone number registered with the Twilio account (sender id).</summary>
    public string? FromPhoneNumber { get; set; }
}
