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
/// Describes a single Hangfire background job for display/monitoring: its identity, current state,
/// lifecycle timestamps, and — on failure — the captured error.
/// </summary>
public class JobDto
{
    /// <summary>Hangfire job identifier.</summary>
    public string Id { get; set; } = "";
    /// <summary>Display name of the job (typically the invoked method).</summary>
    public string Name { get; set; } = "";
    /// <summary>Current Hangfire state ("Enqueued", "Processing", "Succeeded", "Failed", …).</summary>
    public string State { get; set; } = "";
    /// <summary>UTC time the job was created/enqueued.</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>UTC time processing began; null while still enqueued/scheduled.</summary>
    public DateTime? StartedAt { get; set; }
    /// <summary>UTC time the job finished (succeeded or failed); null while still running.</summary>
    public DateTime? CompletedAt { get; set; }
    /// <summary>Failure message when the job errored; null on success.</summary>
    public string? ErrorMessage { get; set; }
    /// <summary>Name of the Hangfire queue the job was dispatched to.</summary>
    public string Queue { get; set; } = "";
    /// <summary>Serialized invocation arguments passed to the job method.</summary>
    public string? Arguments { get; set; }
}
