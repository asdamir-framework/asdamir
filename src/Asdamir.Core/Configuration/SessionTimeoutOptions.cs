// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.Configuration;

/// <summary>
/// Client session-timeout settings for an interactive UI. The values are owned per-application by the
/// app's <c>AppConfigurations</c> table (keys <c>Session:IdleSeconds</c> / <c>Session:CountdownSeconds</c>),
/// loaded into configuration at the <b>API tier</b> startup via
/// <c>AddDatabaseConfiguration</c> and bound here with <c>Configure&lt;SessionTimeoutOptions&gt;(…)</c>.
/// The UI tier never reads the DB — it receives these from the API's client-settings endpoint.
/// See the "Client/session settings" rule in CLAUDE.md.
/// </summary>
public sealed class SessionTimeoutOptions
{
    /// <summary>Configuration section name these options bind from (<c>Session</c>).</summary>
    public const string Section = "Session";

    /// <summary>Inactivity window (seconds) before the timeout warning appears. Default 5 min.</summary>
    public int IdleSeconds { get; set; } = 300;

    /// <summary>Seconds the user has to respond before being signed out. Default 30 s.</summary>
    public int CountdownSeconds { get; set; } = 30;
}
