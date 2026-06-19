// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Core.Sanitization;

/// <summary>
/// Masks email addresses for inclusion in log lines. Keeps the first character + domain
/// for debugging (so ops can correlate "this recipient" across log lines) while removing
/// the bulk of the PII surface — central log stores no longer accumulate full address books.
///
/// Examples:
///   <c>alice@example.com</c> → <c>a***@example.com</c>
///   <c>x@example.com</c>     → <c>*@example.com</c>     (single-char local must not be expanded)
///   <c>""</c>                → <c>(empty)</c>
///   <c>"not-an-email"</c>    → <c>***</c>                (refuses to guess)
/// </summary>
/// <remarks>
/// Audit fix: extracted from <c>Data.Outbox.Services.EmailService</c>'s private static
/// method so it can be regression-tested AND reused by other services (audit middleware,
/// SMS sender, etc.) that need to log a recipient without leaking PII.
/// </remarks>
public static class RecipientMasker
{
    /// <summary>
    /// Returns a log-safe rendering of <paramref name="address"/>:
    /// first char of local-part + <c>***</c> + domain (incl. <c>@</c>). Non-email inputs collapse to <c>***</c>.
    /// </summary>
    public static string Mask(string? address)
    {
        if (string.IsNullOrEmpty(address)) return "(empty)";

        var atIndex = address.IndexOf('@');
        if (atIndex <= 0) return "***"; // no local-part or no '@' at all

        var local = address[..atIndex];
        var domain = address[atIndex..];

        return local.Length <= 1
            ? "*" + domain
            : local[0] + "***" + domain;
    }
}
