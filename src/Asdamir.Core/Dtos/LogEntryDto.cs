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

/// <summary>A generic log row (any level) surfaced from the Serilog DB sink for the monitoring/log-viewer feed.</summary>
public class LogEntryDto
{
    /// <summary>UTC time the entry was written.</summary>
    public DateTime CreatedAtUtc { get; set; }
    /// <summary>Severity level of the entry (e.g. "Information", "Warning", "Error").</summary>
    public string Level { get; set; } = string.Empty;
    /// <summary>Rendered log message text.</summary>
    public string Message { get; set; } = string.Empty;
    /// <summary>Originating app/component tag; null if not set.</summary>
    public string? Source { get; set; }
    /// <summary>Formatted exception detail attached to the entry; null when none.</summary>
    public string? Exception { get; set; }
    /// <summary>Structured Serilog properties captured with the entry (e.g. ErrorKey, CaughtBy, AppId); null if none.</summary>
    public Dictionary<string, object>? Properties { get; set; }
}
