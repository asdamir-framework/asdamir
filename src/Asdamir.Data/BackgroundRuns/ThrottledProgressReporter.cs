// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Asdamir.Core.BackgroundRuns;

namespace Asdamir.Data.BackgroundRuns;

/// <summary>
/// <see cref="IProgressReporter"/> that COALESCES calls in memory and FLUSHES to the run store at
/// most once per <c>ProgressFlushIntervalMs</c> OR once per <c>ProgressFlushPercentStep</c> advance —
/// so a handler iterating 100k×100k rows may call <see cref="Report"/> per row without one DB write
/// per call. The store write is fire-and-forget on the throttle window; <see cref="FlushFinalAsync"/>
/// force-writes the last value when the run reaches a terminal state so no progress is lost.
/// One instance is created per run by the runner (not shared / not DI-scoped).
/// </summary>
public sealed class ThrottledProgressReporter : IProgressReporter
{
    private readonly IBackgroundRunStore _store;
    private readonly Guid _runId;
    private readonly TimeSpan _minInterval;
    private readonly int _percentStep;
    private readonly object _gate = new();

    private int _completed;
    private int? _total;
    private int _lastFlushedCompleted = -1;
    private DateTime _lastFlushUtc = DateTime.MinValue;

    /// <summary>Creates a reporter for one run using the runner's throttle options.</summary>
    /// <param name="store">The run store the throttled writes target.</param>
    /// <param name="runId">The run whose progress is being reported.</param>
    /// <param name="options">Throttle window + percent step.</param>
    public ThrottledProgressReporter(IBackgroundRunStore store, Guid runId, BackgroundRunOptions options)
    {
        _store = store;
        _runId = runId;
        _minInterval = TimeSpan.FromMilliseconds(Math.Max(1, options.ProgressFlushIntervalMs));
        _percentStep = Math.Max(0, options.ProgressFlushPercentStep);
    }

    /// <inheritdoc />
    public void Report(int completed, int? total = null)
    {
        bool flush;
        int snapshotCompleted;
        int? snapshotTotal;
        lock (_gate)
        {
            _completed = Math.Max(0, completed);
            if (total.HasValue) _total = total;
            flush = ShouldFlush();
            if (!flush) return;
            _lastFlushUtc = DateTime.UtcNow;
            _lastFlushedCompleted = _completed;
            snapshotCompleted = _completed;
            snapshotTotal = _total;
        }
        // Fire-and-forget: progress is advisory, must not block or throw into the hot loop.
        _ = WriteAsync(snapshotCompleted, snapshotTotal);
    }

    /// <summary>
    /// Force-writes the most recent progress value regardless of the throttle window. Called by the
    /// runner just before it marks a run terminal so the final progress is always persisted.
    /// </summary>
    public async Task FlushFinalAsync(CancellationToken ct)
    {
        int completed;
        int? total;
        lock (_gate)
        {
            if (_lastFlushedCompleted == _completed) return; // nothing new
            completed = _completed;
            total = _total;
            _lastFlushedCompleted = _completed;
        }
        try { await _store.UpdateProgressAsync(_runId, completed, total, ct); }
        catch { /* advisory — swallow so a flush failure can't fail the run */ }
    }

    // Caller holds the gate.
    private bool ShouldFlush()
    {
        if (_completed == _lastFlushedCompleted) return false;
        if (DateTime.UtcNow - _lastFlushUtc >= _minInterval) return true;
        if (_percentStep > 0 && _total is > 0)
        {
            var step = (long)_total.Value * _percentStep / 100;
            if (step > 0 && _completed - _lastFlushedCompleted >= step) return true;
        }
        return false;
    }

    private async Task WriteAsync(int completed, int? total)
    {
        try { await _store.UpdateProgressAsync(_runId, completed, total, CancellationToken.None); }
        catch { /* advisory progress — never surface into the handler's loop */ }
    }
}
