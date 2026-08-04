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
/// Records an agent's actions into the tamper-evident agent action ledger. This is the ONLY surface an
/// application calls; everything behind it — batching, the ingest transport, the hash chain, verification —
/// is the framework's concern.
///
/// <para><b>The call does not block on the ledger.</b> The shipped implementation hands the record to a
/// bounded in-memory queue and returns; a background pump batches and posts them. So a slow or unreachable
/// control plane never slows down the action being audited, and <c>RecordAsync</c> completing is NOT a
/// guarantee that the row is durable in the ledger yet.</para>
///
/// <para><b>What happens when delivery fails</b> is a configured policy, not silence — see
/// <see cref="AgentAuditOptions.OnSinkFailure"/>. The default spools failed records to disk and replays them
/// at startup, because an audit gap that nobody is told about is the one failure mode this feature exists to
/// prevent.</para>
/// </summary>
public interface IAgentActionAuditor
{
    /// <summary>
    /// Submits one agent action for recording.
    /// </summary>
    /// <param name="record">The action to record. Reuse its <see cref="AgentActionRecord.EventId"/> when
    /// retrying the SAME action so ingest stays idempotent.</param>
    /// <param name="cancellationToken">Cancels the enqueue (not the eventual delivery, which outlives the call).</param>
    /// <returns>A task that completes once the record has been accepted for delivery.</returns>
    Task RecordAsync(AgentActionRecord record, CancellationToken cancellationToken = default);
}
