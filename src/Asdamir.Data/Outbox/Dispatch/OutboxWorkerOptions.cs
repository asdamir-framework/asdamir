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
    /// <summary>Configuration section this binds from (<c>Outbox:Worker</c>).</summary>
    public const string Section = "Outbox:Worker";

    /// <summary>Whether the outbox worker runs at all (typically enabled only on Dapper persistence).</summary>
    public bool Enabled { get; set; } = true;
    /// <summary>How many messages a single poll claims and dispatches per batch.</summary>
    public int BatchSize { get; set; } = 25;
    /// <summary>Delay between polls when the last batch had work.</summary>
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);
    /// <summary>Longer delay applied when the last poll found nothing to do (idle back-off).</summary>
    public TimeSpan IdleBackoff { get; set; } = TimeSpan.FromSeconds(5);
    /// <summary>How often stale in-flight locks (from a crashed worker) are reclaimed.</summary>
    public TimeSpan ReclaimInterval { get; set; } = TimeSpan.FromMinutes(2);
    /// <summary>Minutes a claimed message may stay locked before it is considered stale and reclaimable.</summary>
    public int ReclaimStaleThresholdMinutes { get; set; } = 5;

    // Backoff: delay = clamp(BackoffBaseSeconds * 2^(tryCount-1) ± jitter%, max=BackoffMaxSeconds).
    /// <summary>Base seconds for the per-attempt exponential retry back-off.</summary>
    public int BackoffBaseSeconds { get; set; } = 30;
    /// <summary>Upper cap (seconds) on the retry back-off delay.</summary>
    public int BackoffMaxSeconds { get; set; } = 3600;
    /// <summary>Random jitter fraction (0–1) applied to each back-off delay to avoid thundering herds.</summary>
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
    /// <summary>Configuration section this binds from (<c>Smtp</c>).</summary>
    public const string Section = "Smtp";

    /// <summary>SMTP server host name.</summary>
    public string Host { get; set; } = "localhost";
    /// <summary>SMTP server port.</summary>
    public int Port { get; set; } = 25;
    /// <summary>Transport security: <c>None</c> / <c>Auto</c> / <c>StartTls</c> / <c>SslOnConnect</c>.</summary>
    public string Security { get; set; } = "Auto"; // None / Auto / StartTls / SslOnConnect
    /// <summary>SMTP auth username (null = no authentication).</summary>
    public string? Username { get; set; }
    /// <summary>SMTP auth password — a secret; supply via env / user-secrets, not appsettings.</summary>
    public string? Password { get; set; }
    /// <summary>Default sender email address when a message doesn't set one.</summary>
    public string DefaultFromAddress { get; set; } = "noreply@localhost";
    /// <summary>Default sender display name (optional).</summary>
    public string? DefaultFromName { get; set; }
    /// <summary>Connection/operation timeout for the SMTP client.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// SMS dispatcher selection. <c>Provider="Stub"</c> (default) keeps logs-only behavior;
/// <c>Provider="Twilio"</c> wires <c>TwilioSmsDispatcher</c> using <see cref="TwilioOptions"/>.
/// Bound from <c>Sms</c> section.
/// </summary>
public sealed class SmsOptions
{
    /// <summary>Configuration section this binds from (<c>Sms</c>).</summary>
    public const string Section = "Sms";

    /// <summary>SMS provider selector: <c>Stub</c> (logs-only) or <c>Twilio</c>.</summary>
    public string Provider { get; set; } = "Stub"; // Stub | Twilio
}

/// <summary>
/// Twilio REST client credentials + default from-number. AccountSid + AuthToken are
/// secrets — store via env / user-secrets, not in appsettings. Bound from <c>Twilio</c>.
/// </summary>
public sealed class TwilioOptions
{
    /// <summary>Configuration section this binds from (<c>Twilio</c>).</summary>
    public const string Section = "Twilio";

    /// <summary>Twilio account SID — a secret; supply via env / user-secrets.</summary>
    public string? AccountSid { get; set; }
    /// <summary>Twilio auth token — a secret; supply via env / user-secrets.</summary>
    public string? AuthToken { get; set; }
    /// <summary>E.164 phone number registered with the Twilio account (sender id).</summary>
    public string? FromPhoneNumber { get; set; }
}
