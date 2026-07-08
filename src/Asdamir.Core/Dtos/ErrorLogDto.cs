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

/// <summary>Full detail of a single logged error (dbo.AppLog row) shown in the drill-down/export view for operators.</summary>
public class ErrorLogDto
{
    /// <summary>Primary key of the AppLog row.</summary>
    public int Id { get; set; }
    /// <summary>Severity level of the entry (e.g. "Error", "Warning").</summary>
    public string Level { get; set; } = "";
    /// <summary>Operator-facing error text (the full logged message, not the end-user's localized one).</summary>
    public string Message { get; set; } = "";
    /// <summary>Originating app/component that raised the error.</summary>
    public string Source { get; set; } = "";
    /// <summary>UTC time the error was logged.</summary>
    public DateTime Timestamp { get; set; }
    /// <summary>Whether an operator has marked this error resolved.</summary>
    public bool IsResolved { get; set; }
    /// <summary>Id of the operator/user in whose context the error occurred; null if none.</summary>
    public int? UserId { get; set; }
    /// <summary>Correlation id tying this entry to a request/trace; null if not captured.</summary>
    public string? CorrelationId { get; set; }
    /// <summary>Full exception stack trace; null when the entry carries no exception.</summary>
    public string? StackTrace { get; set; }
    /// <summary>Structured Serilog properties captured with the entry (e.g. ErrorKey, CaughtBy, AppId).</summary>
    public Dictionary<string, object> Properties { get; set; } = new();
}
