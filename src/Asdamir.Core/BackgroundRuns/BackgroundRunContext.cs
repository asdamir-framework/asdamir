// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.BackgroundRuns;

/// <summary>
/// The run CONTEXT handed to an <see cref="IBackgroundJobHandler"/> when the framework executes a run.
/// It carries everything the runner already knows about the run — its identity, tenant, input, a
/// progress sink and cancellation — in ONE object, so future additions extend the context without
/// another handler-signature break (there is deliberately no mix of loose parameters + context).
/// <para>
/// The key member is <see cref="RunId"/>: it is the same value <c>EnqueueAsync</c> returned and
/// <c>IBackgroundRunService.GetStatusAsync</c> reports, so a handler can write a two-way back-link from
/// its own business record (e.g. a domain "run" row) to the framework's <c>BackgroundRuns</c> row —
/// closing the audit/maker-checker traceability loop that a forward-reference-only design cannot.
/// </para>
/// </summary>
/// <param name="RunId">
/// The store record's run id — identical to the value <c>EnqueueAsync</c> returned and
/// <c>GetStatusAsync(runId).RunId</c> reports. Use it to persist a back-link from the handler's own
/// business record to the framework's <c>BackgroundRuns</c> row.
/// </param>
/// <param name="TenantId">
/// The tenant this run belongs to. The runner has already re-established the ambient tenant scope from
/// this value, so scoped services (stores, repositories) resolve tenant-correct data; it is exposed
/// here for logging / back-link rows that need the tenant explicitly.
/// </param>
/// <param name="Payload">
/// The opaque, app-defined input from <see cref="BackgroundRunRequest.Payload"/>, handed verbatim to
/// the handler (typically JSON). <c>null</c> for parameter-less jobs. The framework never inspects it.
/// </param>
/// <param name="Progress">Cheap, throttled progress sink for this run (call it per unit of work).</param>
/// <param name="CancellationToken">Cooperative cancellation for shutdown / abort — honour it.</param>
public sealed record BackgroundRunContext(
    Guid RunId,
    string TenantId,
    string? Payload,
    IProgressReporter Progress,
    CancellationToken CancellationToken);
