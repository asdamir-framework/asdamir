// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Data.BackgroundRuns;

/// <summary>
/// Tunables for the background-run runner + progress throttling. Bind these from
/// <c>AppConfigurations</c> (the DB-backed config rule) — never hardcode. Defaults are safe for a
/// single-node Gateway.
/// </summary>
public sealed class BackgroundRunOptions
{
    /// <summary>
    /// Max concurrent runs the hosted runner executes at once (default 2). A heavy single-job app
    /// typically keeps this low so one run doesn't starve request handling.
    /// </summary>
    public int MaxConcurrency { get; set; } = 2;

    /// <summary>
    /// Progress-throttle: minimum wall-clock gap between two progress FLUSHES to the store
    /// (default 750 ms). A handler may call <c>Report</c> per row; only one write lands per window.
    /// </summary>
    public int ProgressFlushIntervalMs { get; set; } = 750;

    /// <summary>
    /// Progress-throttle: also flush when completed advances by at least this percent of total
    /// since the last flush (default 5%), so short jobs still show movement. Ignored when total is
    /// unknown (then only the time window applies). The final value is always flushed at terminal state.
    /// </summary>
    public int ProgressFlushPercentStep { get; set; } = 5;

    /// <summary>
    /// Owner-token for runs this node claims (node/process identity). Defaults to
    /// <c>{MachineName}#{ProcessId}</c>. Recorded on a Running row so multi-node ownership can be
    /// reasoned about later (single-node today — see the HA note in the docs).
    /// </summary>
    public string? NodeId { get; set; }
}
