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
/// Lifecycle state of a background run. The state machine is strictly forward:
/// <c>Pending → Running → (Completed | Failed)</c>, plus <see cref="Interrupted"/> which is a
/// terminal state applied by restart-recovery to a run whose owning process died mid-flight.
/// The framework owns these transitions; the app-supplied job body never sets them directly.
/// </summary>
public enum BackgroundRunState
{
    /// <summary>Enqueued, not yet picked up by the runner.</summary>
    Pending = 0,

    /// <summary>Claimed by the runner and executing the app-supplied handler.</summary>
    Running = 1,

    /// <summary>The handler returned successfully. Terminal.</summary>
    Completed = 2,

    /// <summary>The handler threw (or was rejected). Terminal; see <c>ErrorSummary</c>.</summary>
    Failed = 3,

    /// <summary>
    /// The owning process died while the run was <see cref="Pending"/> or <see cref="Running"/>;
    /// startup restart-recovery flipped it here so no ghost "Running" survives a restart. Terminal.
    /// </summary>
    Interrupted = 4,
}
