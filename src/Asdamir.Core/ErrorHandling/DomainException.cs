// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.ErrorHandling.Domain;

/// <summary>
/// An expected, business-level failure carrying a stable <c>Code</c> that maps to a localized user
/// message; the global middleware translates it to a 400 ProblemDetails instead of an opaque 500, so
/// throwing one is the intended way to signal a rule violation (not-found, conflict, etc.) to the caller.
/// </summary>
public class DomainException : Exception
{
    /// <summary>
    /// Stable error key (e.g. <c>"user.not_found"</c>) used to resolve the localized message and to
    /// group occurrences in the log — matched by value, never shown raw to the user.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Creates a domain exception with its stable <paramref name="code"/> and a developer-facing message.
    /// </summary>
    /// <param name="code">Stable error key that drives localization and grouping (see <c>Code</c>).</param>
    /// <param name="message">Developer/log-facing description; never surfaced verbatim to the end user.</param>
    public DomainException(string code, string message) : base(message)
    {
        Code = code;
    }

    /// <summary>
    /// Creates a domain exception with its stable <paramref name="code"/>, a message, and the underlying
    /// cause, preserving the original exception chain for logging.
    /// </summary>
    /// <param name="code">Stable error key that drives localization and grouping (see <c>Code</c>).</param>
    /// <param name="message">Developer/log-facing description; never surfaced verbatim to the end user.</param>
    /// <param name="innerException">The lower-level exception that triggered this domain failure.</param>
    public DomainException(string code, string message, Exception innerException) : base(message, innerException)
    {
        Code = code;
    }
}
