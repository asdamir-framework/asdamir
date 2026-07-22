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
/// Handed to an app's job handler so it can publish coarse progress for a background run.
/// <para>
/// <see cref="Report"/> is deliberately CHEAP to call from a hot loop — implementations
/// THROTTLE/BATCH persistence (flush at most every N milliseconds or every X% change) so a job
/// iterating 100k×100k rows can call it per row without one DB write per call. The last reported
/// value is always flushed when the run reaches a terminal state. See
/// <c>docs/fundamentals/background-runs.md</c> for the throttle strategy.
/// </para>
/// </summary>
public interface IProgressReporter
{
    /// <summary>
    /// Records progress for the current run. Cheap and non-blocking on the caller's happy path
    /// (the value is coalesced in memory and flushed to the run store on the throttle schedule).
    /// </summary>
    /// <param name="completed">Units done so far (monotonic, clamped to <c>&gt;= 0</c>).</param>
    /// <param name="total">Total units expected, or <c>null</c> if not yet known.</param>
    void Report(int completed, int? total = null);
}
