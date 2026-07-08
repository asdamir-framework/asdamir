// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.Dtos;

/// <summary>
/// Aggregate counters for the Hangfire dashboard: how many background jobs currently sit in each
/// state, for at-a-glance queue health.
/// </summary>
public class JobStatistics
{
    /// <summary>Jobs queued and waiting for a worker to pick them up.</summary>
    public int Enqueued { get; set; }
    /// <summary>Jobs that ended in failure after exhausting retries.</summary>
    public int Failed { get; set; }
    /// <summary>Jobs currently being executed by a worker.</summary>
    public int Processing { get; set; }
    /// <summary>Jobs that completed successfully.</summary>
    public int Succeeded { get; set; }
    /// <summary>Jobs scheduled to run at a future time.</summary>
    public int Scheduled { get; set; }
    /// <summary>Jobs that have been deleted/discarded.</summary>
    public int Deleted { get; set; }
    /// <summary>Jobs awaiting a parent/continuation to finish before they run.</summary>
    public int Awaiting { get; set; }
    /// <summary>Jobs failed once and queued for an automatic retry.</summary>
    public int Retry { get; set; }
}
