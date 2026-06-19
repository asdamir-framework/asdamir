// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using Serilog.Parsing;
using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;
using System.Reflection;

namespace Asdamir.Core.ErrorHandling.Logging;

/// <summary>
/// Prepends a short module prefix (<c>[Asdamir.Web.Security]</c>, <c>[Gateway]</c>, ...) to
/// every log event's message template, derived from the <c>SourceContext</c> property.
///
/// Audit fixes vs. v1:
///  - <see cref="MessageTemplateParser"/> was allocated PER log event on the hot path.
///    Logging is one of the highest-frequency paths in the process — replacing the
///    template object 1000+ times/sec generated nontrivial GC pressure. Now a single
///    static parser instance is reused.
///  - The source-context-to-prefix mapping was recomputed every call. Now memoised in
///    a bounded <see cref="ConcurrentDictionary{TKey,TValue}"/> keyed by SourceContext.
///  - <c>sourceContext.ToString()</c> with surrounding-quote trim is replaced by direct
///    <see cref="ScalarValue"/> unwrap to avoid both the allocation and the string scan.
/// </summary>
public class ApplicationPrefixEnricher : ILogEventEnricher
{
    private static readonly FieldInfo? MessageTemplateField =
        typeof(LogEvent).GetField("<MessageTemplate>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);

    private static readonly MessageTemplateParser TemplateParser = new();

    // SourceContext → "[Foo.Bar] " prefix cache. Bounded by the universe of
    // SourceContext strings (one per logger), so growth is finite in practice.
    private static readonly ConcurrentDictionary<string, string> PrefixCache = new();

    public ApplicationPrefixEnricher(string _ = "")
    {
        // Parameter kept for backward compatibility with the old DI registration.
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        if (MessageTemplateField is null || logEvent.MessageTemplate?.Text is null)
            return;

        var text = logEvent.MessageTemplate.Text;

        // Already prefixed: [Something] at the very start.
        if (text.Length > 2 && text[0] == '[')
        {
            var closeIdx = text.IndexOf(']');
            if (closeIdx > 0 && closeIdx < 20 && closeIdx + 1 < text.Length && text[closeIdx + 1] == ' ')
            {
                return;
            }
        }

        try
        {
            if (!logEvent.Properties.TryGetValue("SourceContext", out var sourceContextProp))
                return;

            if (sourceContextProp is not ScalarValue scalar || scalar.Value is not string sourceContext || sourceContext.Length == 0)
                return;

            var prefix = PrefixCache.GetOrAdd(sourceContext, ComputePrefix);
            if (prefix.Length == 0) return;

            var newText = prefix + text;
            var newTemplate = TemplateParser.Parse(newText);
            MessageTemplateField.SetValue(logEvent, newTemplate);
        }
        catch
        {
            // Logging must never throw.
        }
    }

    private static string ComputePrefix(string sourceContextValue)
    {
        var parts = sourceContextValue.Split('.');
        if (parts.Length >= 3 && parts[0] == "Core" && parts[1] == "Api" && parts[2] == "Controllers")
            return "[Gateway] ";

        if (parts.Length >= 2 && parts[0] == "Core")
            return $"[{parts[0]}.{parts[1]}] ";

        if (parts.Length >= 1)
            return $"[{parts[0]}] ";

        return string.Empty;
    }
}
