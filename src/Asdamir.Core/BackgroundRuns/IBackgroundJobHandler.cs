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
/// The app-supplied BODY of a background job. One handler is registered per
/// <see cref="JobType"/> key (see <c>AddBackgroundJob&lt;THandler&gt;()</c>); the framework resolves
/// it when a run of that type is dequeued. The framework owns the run's lifecycle — the handler
/// owns only the WORK.
/// <para>
/// The handler must be pure work: on success it returns (optionally a <c>ResultRef</c>); on failure
/// it THROWS. Do not swallow exceptions — a thrown exception is what moves the run to
/// <see cref="BackgroundRunState.Failed"/> with the error recorded. Honour the supplied
/// <see cref="BackgroundRunContext.CancellationToken"/> for cooperative shutdown.
/// </para>
/// </summary>
public interface IBackgroundJobHandler
{
    /// <summary>
    /// The stable <c>JobType</c> key this handler serves (must match the value used in
    /// <see cref="BackgroundRunRequest.JobType"/>). Case-sensitive.
    /// </summary>
    string JobType { get; }

    /// <summary>
    /// Executes the job. Everything the runner knows about the run — its <see cref="BackgroundRunContext.RunId"/>,
    /// tenant, payload, a throttled progress sink and cancellation — arrives in the single
    /// <paramref name="context"/> (see <see cref="BackgroundRunContext"/>). Report progress via
    /// <c>context.Progress</c>, read input from <c>context.Payload</c>, honour
    /// <c>context.CancellationToken</c>, and use <c>context.RunId</c> to write a back-link from your own
    /// business record to the framework's run row. Return an optional app-defined result pointer (stored
    /// as the run's <c>ResultRef</c>); THROW on failure — a thrown exception is what moves the run to
    /// <see cref="BackgroundRunState.Failed"/> with the error recorded.
    /// </summary>
    /// <param name="context">The run context (id, tenant, payload, progress, cancellation).</param>
    /// <returns>An optional result reference to record on the run, or <c>null</c>.</returns>
    Task<string?> ExecuteAsync(BackgroundRunContext context);
}
