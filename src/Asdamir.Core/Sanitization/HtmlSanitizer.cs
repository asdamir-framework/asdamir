// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.Text.RegularExpressions;

namespace Asdamir.Core.Sanitization;

/// <summary>
/// Narrow HTML sanitizer used by admin-side localization editing and other places where
/// trusted operators paste small HTML fragments. NOT a general-purpose sanitizer — do not
/// route untrusted external input through this class; use a vetted library
/// (HtmlSanitizer NuGet, Ganss.Xss) for that.
///
/// Audit fix vs. v1: this logic used to live as a private static method inside
/// <c>Localization.razor</c>, which made it impossible to write regression pins. Extracted
/// here so the rules — strip dangerous blocks, strip inline event handlers, neutralize
/// javascript:/vbscript:/data: URLs — can be locked in by tests.
/// </summary>
public static class HtmlSanitizer
{
    // Compiled regexes: each runs hot when a non-trivial localization value is saved.
    private static readonly Regex DangerousBlocksPattern = new(
        @"<\s*(script|style|iframe|object|embed)\b[^>]*>[\s\S]*?<\s*/\s*\1\s*>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex InlineEventHandlersPattern = new(
        @"\s*on[a-z]+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DangerousUrlSchemesPattern = new(
        @"(?<=\b(?:href|src|action|formaction)\s*=\s*[""'])\s*(?:javascript|vbscript|data)\s*:[^""']*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Returns a copy of <paramref name="value"/> with the following neutralized:
    /// <list type="bullet">
    ///   <item>&lt;script&gt;, &lt;style&gt;, &lt;iframe&gt;, &lt;object&gt;, &lt;embed&gt; blocks (entire element + content removed).</item>
    ///   <item>Inline event handler attributes (<c>onclick=</c>, <c>onerror=</c>, …).</item>
    ///   <item>URL schemes <c>javascript:</c> / <c>vbscript:</c> / <c>data:</c> inside <c>href/src/action/formaction</c> attributes — rewritten to <c>about:blank</c>.</item>
    /// </list>
    /// </summary>
    public static string Sanitize(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        value = DangerousBlocksPattern.Replace(value, string.Empty);
        value = InlineEventHandlersPattern.Replace(value, string.Empty);
        value = DangerousUrlSchemesPattern.Replace(value, "about:blank");

        return value;
    }
}
