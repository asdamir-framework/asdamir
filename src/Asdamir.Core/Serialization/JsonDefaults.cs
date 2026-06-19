// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.Text.Json.Serialization;
using System.Text.Json;

namespace Asdamir.Core.Serialization;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> instances used across the framework.
///
/// Audit fix: v1 exposed <see cref="Web"/> as a mutable static field. Any consumer
/// could call <c>JsonDefaults.Web.Converters.Add(...)</c> and silently change the
/// serialization contract for the entire process — particularly dangerous in tests
/// running in parallel. We now build the instance lazily and call
/// <see cref="JsonSerializerOptions.MakeReadOnly()"/> so post-init mutations throw.
/// </summary>
public static class JsonDefaults
{
    private static readonly Lazy<JsonSerializerOptions> _web = new(BuildWeb, isThreadSafe: true);

    public static JsonSerializerOptions Web => _web.Value;

    private static JsonSerializerOptions BuildWeb()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        o.Converters.Add(new JsonStringEnumConverter());
        o.Converters.Add(new UtcDateTimeConverter());
        o.MakeReadOnly();
        return o;
    }
}

public sealed class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => DateTime.SpecifyKind(reader.GetDateTime(), DateTimeKind.Utc);

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToUniversalTime());
}
