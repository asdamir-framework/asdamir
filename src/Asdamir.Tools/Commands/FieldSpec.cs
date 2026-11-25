// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

namespace Asdamir.Tools.Commands;

/// <summary>
/// A single field on a generated entity.
/// Parsed from the CLI's <c>--fields</c> argument, e.g. <c>"Name:string,Email:string?,IsActive:bool"</c>.
/// </summary>
public sealed record FieldSpec(
    string Name,
    string CSharpType,   // e.g. "string", "int", "DateTime", "bool?"
    string SqlType,      // e.g. "NVARCHAR(200)", "INT", "DATETIME2", "BIT"
    bool IsNullable,
    bool IsRequired)
{
    /// <summary>Lowercase first-character version (for parameter names).</summary>
    public string CamelCase =>
        Name.Length switch
        {
            0 => Name,
            1 => Name.ToLowerInvariant(),
            _ => char.ToLowerInvariant(Name[0]) + Name[1..],
        };
}

/// <summary>
/// Parses the comma-separated --fields argument into a list of FieldSpec.
/// </summary>
public static class FieldSpecParser
{
    private static readonly HashSet<string> ReservedFieldNames =
        new(StringComparer.OrdinalIgnoreCase) { "Id", "TenantId", "CreatedAtUtc", "UpdatedAtUtc" };

    /// <summary>
    /// Parses a <c>--fields</c> / <c>--field</c> spec. Throws <see cref="ArgumentException"/> — with a message
    /// naming the offending token — for a malformed pair, a name that is not an identifier, a reserved or
    /// duplicated name, or an unsupported type.
    /// </summary>
    /// <param name="input">The comma-separated <c>Name:type</c> list.</param>
    /// <returns>The parsed fields, names PascalCased.</returns>
    public static IReadOnlyList<FieldSpec> Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Array.Empty<FieldSpec>();

        var result = new List<FieldSpec>();
        foreach (var raw in input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = raw.Split(':', 2);
            if (parts.Length != 2)
                throw new ArgumentException($"Field spec '{raw}' must be in 'Name:type' form (e.g. 'Email:string').");

            var name = parts[0].Trim();
            var rawType = parts[1].Trim();

            // The field name becomes a C# property, a SQL column, a stored-procedure parameter (@Name) and a
            // localization key segment. "Starts with a letter" (the previous rule) let `Na me`, `Total]; DROP
            // TABLE x--` and `A.B` through to all four. PascalCase is applied first, then the same identifier
            // rule the entity/app name uses.
            var pascal = PascalCase(name);
            if (!NameArgument.IsValid(pascal))
                throw new ArgumentException(
                    $"Field name '{name}' is not a valid identifier — a letter first, then letters, digits or underscores only (no spaces, dots, brackets or quotes).");

            // Columns every generated entity already has. A field with one of these names produces a duplicate
            // property (CS0102) and a duplicate column — refuse it here rather than at compile/apply time.
            if (ReservedFieldNames.Contains(pascal))
                throw new ArgumentException(
                    $"Field name '{pascal}' is reserved — every generated entity already has Id, TenantId, CreatedAtUtc and UpdatedAtUtc.");

            if (result.Any(f => string.Equals(f.Name, pascal, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException($"Field name '{pascal}' is listed more than once.");

            var (csharpType, sqlType, isNullable) = MapType(rawType);
            // "Required" defaults to "not nullable AND not Id-like". The template uses this
            // to emit FluentValidation NotEmpty and SQL NOT NULL.
            var isRequired = !isNullable;

            result.Add(new FieldSpec(
                Name: pascal,
                CSharpType: csharpType,
                SqlType: sqlType,
                IsNullable: isNullable,
                IsRequired: isRequired));
        }
        return result;
    }

    private static (string CSharp, string Sql, bool Nullable) MapType(string raw)
    {
        var nullable = raw.EndsWith('?');
        var bare = nullable ? raw[..^1] : raw;

        var (cs, sql) = bare.ToLowerInvariant() switch
        {
            "string" => ("string", "NVARCHAR(500)"),
            "int" => ("int", "INT"),
            "long" => ("long", "BIGINT"),
            "bool" => ("bool", "BIT"),
            "decimal" => ("decimal", "DECIMAL(18, 4)"),
            // `double` was accepted here and then failed later: the generated edit dialog binds numeric fields
            // to AsdamirNumberInput, which rejects double/float at RUNTIME (binary floating point cannot hold
            // exact decimals), so the page threw the first time it was opened. Rejected up front, with the fix.
            "double" or "float" => throw new ArgumentException(
                $"Field type '{bare}' is not supported — use 'decimal' (the generated UI's number input rejects binary floating point, and a money/quantity column should not be FLOAT)."),
            "datetime" => ("DateTime", "DATETIME2"),
            "guid" => ("Guid", "UNIQUEIDENTIFIER"),
            _ => throw new ArgumentException(
                $"Unsupported field type '{bare}'. Supported: string, int, long, bool, decimal, DateTime, Guid (add ? for nullable, e.g. 'Note:string?').")
        };

        // string is reference-type so the "?" is purely a hint; non-nullable string is "string" with NOT NULL.
        if (cs == "string" && nullable) cs = "string?";
        else if (nullable && cs != "string") cs = cs + "?";

        return (cs, sql, nullable);
    }

    private static string PascalCase(string s) =>
        s.Length switch
        {
            0 => s,
            1 => s.ToUpperInvariant(),
            _ => char.ToUpperInvariant(s[0]) + s[1..],
        };
}
