// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.AgentAudit;

/// <summary>
/// Configuration for the agent-audit client — the queue, the ingest transport, and above all what happens
/// when delivery fails. Bound from the <c>AgentAudit</c> configuration section.
/// </summary>
public sealed class AgentAuditOptions
{
    /// <summary>
    /// What to do when a record cannot be delivered to the ledger. The choice is a compliance decision, not a
    /// performance one: it decides whether an audit gap is buffered, refused, or accepted.
    /// </summary>
    public enum SinkFailureMode
    {
        /// <summary>
        /// DEFAULT. Write undelivered records to a bounded local NDJSON spool and replay them at startup.
        /// Availability is preserved and nothing is lost while the spool has room; if the spool fills, the
        /// OLDEST file is dropped with a warning — so the cap is a real, if last-resort, data-loss boundary.
        /// </summary>
        Spool = 0,

        /// <summary>
        /// Refuse to proceed: surface the failure to the caller instead of buffering it. Choose this when a
        /// regime requires that an unauditable action must not happen at all — it trades availability for the
        /// guarantee that no action goes unrecorded.
        /// </summary>
        Throw = 1,

        /// <summary>
        /// Drop the record and log a warning. The audit gap is accepted and merely reported. Only appropriate
        /// where the ledger is advisory; never for a record you may later have to produce as evidence.
        /// </summary>
        DropAndWarn = 2,
    }

    /// <summary>Master switch. When false, <c>RecordAsync</c> is a no-op and no background pump runs.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The ingest endpoint path, relative to the agent-audit client's base address.</summary>
    public string IngestPath { get; set; } = "api/agent-audit/events";

    /// <summary>
    /// The shared HMAC signing key (<c>Jwt:Key</c>) used to mint the <c>agent-audit-ingest</c> service token.
    /// A secret: supply it through user-secrets or the environment, never <c>appsettings.json</c>.
    /// </summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>The token issuer (<c>Jwt:Issuer</c>) — must match the control plane's.</summary>
    public string? Issuer { get; set; }

    /// <summary>The token audience (<c>Jwt:Audience</c>) — the managed-app audience.</summary>
    public string? Audience { get; set; }

    /// <summary>
    /// This application's registered code (<c>App:Code</c>). The server resolves the ledger's AppId from it,
    /// which is why the record itself carries no AppId and one app can never write into another's chain.
    /// </summary>
    public string AppCode { get; set; } = string.Empty;

    /// <summary>Records per POST. Values above the server's cap of 200 are clamped to 200.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>Coalescing window between batches, so a burst rides one request instead of many.</summary>
    public TimeSpan FlushInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Bounded in-memory queue capacity. On overflow the OLDEST queued record is handed to the configured
    /// <see cref="OnSinkFailure"/> policy rather than being silently discarded.
    /// </summary>
    public int QueueCapacity { get; set; } = 1000;

    /// <summary>What to do when a record cannot be delivered. Defaults to <see cref="SinkFailureMode.Spool"/>.</summary>
    public SinkFailureMode OnSinkFailure { get; set; } = SinkFailureMode.Spool;

    /// <summary>Directory holding the NDJSON spool and the dead-letter file.</summary>
    public string SpoolDirectory { get; set; } = "logs/agent-audit-spool";

    /// <summary>
    /// Total size cap for the spool directory. When exceeded, the oldest spool file is deleted and a warning is
    /// emitted — the one place this client can lose a record it accepted, so it is deliberately generous.
    /// </summary>
    public long SpoolMaxBytes { get; set; } = 64L * 1024 * 1024;

    /// <summary>
    /// How many times a record may be re-attempted before it is treated as undeliverable and dead-lettered.
    ///
    /// <para>This cap is what stops an UNKNOWN per-item status from retrying forever. An unrecognised status is
    /// always treated as transient first (a newer server may consider it retryable), so without a cap such a
    /// record would circulate indefinitely; with one it lands in the dead-letter file, where it is counted and
    /// reported rather than lost.</para>
    /// </summary>
    public int MaxDeliveryAttempts { get; set; } = 5;

    /// <summary>
    /// How often the dead-letter alarm is repeated while records remain dead-lettered. This warning is
    /// deliberately RECURRING, not warn-once: a permanently rejected audit record is an audit GAP, and a single
    /// startup line that scrolls away is exactly how such a gap goes unnoticed.
    /// </summary>
    public TimeSpan DeadLetterWarningInterval { get; set; } = TimeSpan.FromMinutes(5);
}
