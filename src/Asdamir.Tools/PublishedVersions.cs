// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later

using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;

namespace Asdamir.Tools;

/// <summary>
/// The versions currently PUBLISHED on nuget.org, read from the embedded
/// <c>published-versions.json</c> — the single physical source for that fact.
///
/// <para>Why this type exists: "what is currently published" used to be hand-written in four places
/// (the <c>AppCommand</c> pin constants, <c>docs/cli.md</c>, <c>CHANGELOG.md</c>, and the public repo's
/// own copies of the last two) and derived from nuget.org in none. When Core <c>1.7.0</c> and Data
/// <c>1.5.0</c> shipped, every one of those copies stayed stale for days and <c>asdamir new app</c> kept
/// pinning two releases behind. Collapsing them onto one file makes the drift a single edit instead of
/// four, and lets one gate compare that edit to nuget.org.</para>
///
/// <para>PUBLISHED, not "next". A csproj <c>&lt;Version&gt;</c> ahead of the number here is the normal
/// pre-publish state and is deliberately NOT an error; a number here ahead of nuget.org is a defect,
/// because a generated app would pin a version that cannot restore. Both directions are asserted by
/// <c>PublishedVersionsGateTests</c>.</para>
/// </summary>
internal static class PublishedVersions
{
    /// <summary>The manifest's file name, as embedded and as it sits in <c>src/Asdamir.Tools/</c>.</summary>
    internal const string ManifestFileName = "published-versions.json";

    private static readonly ImmutableSortedDictionary<string, string> Map = Load();

    /// <summary>Every published package id → its published version, ordinal-sorted by id.</summary>
    internal static ImmutableSortedDictionary<string, string> All => Map;

    /// <summary>The published <c>Asdamir.Core</c> version a fresh generated app pins.</summary>
    internal static string Core => Of("Asdamir.Core");

    /// <summary>The published <c>Asdamir.Data</c> version a fresh generated app pins.</summary>
    internal static string Data => Of("Asdamir.Data");

    /// <summary>The published <c>Asdamir.Web</c> version a fresh generated app pins.</summary>
    internal static string Web => Of("Asdamir.Web");

    /// <summary>The published <c>Asdamir.Payments</c> version a fresh generated app pins.</summary>
    internal static string Payments => Of("Asdamir.Payments");

    /// <summary>The published <c>Asdamir.Tools</c> (CLI) version. Not a generated-app pin — the CLI is a
    /// tool, not a library reference — but it is part of the same published set the docs quote.</summary>
    internal static string Tools => Of("Asdamir.Tools");

    /// <summary>The published version of <paramref name="packageId"/>.</summary>
    /// <exception cref="KeyNotFoundException">The manifest does not list that package.</exception>
    internal static string Of(string packageId) =>
        Map.TryGetValue(packageId, out var v)
            ? v
            : throw new KeyNotFoundException(
                $"'{packageId}' is not listed in {ManifestFileName}. Add it there — that file is the single " +
                "source for the published version set; nothing else may carry a version literal.");

    private static ImmutableSortedDictionary<string, string> Load()
    {
        var asm = typeof(PublishedVersions).Assembly;
        var resource = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(ManifestFileName, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"{ManifestFileName} is not embedded in {asm.GetName().Name}. It must stay an <EmbeddedResource> " +
                "in Asdamir.Tools.csproj — the generated-app pins are read from it at runtime.");

        using var stream = asm.GetManifestResourceStream(resource)!;
        using var doc = JsonDocument.Parse(stream);
        var packages = doc.RootElement.GetProperty("packages");

        var builder = ImmutableSortedDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var p in packages.EnumerateObject())
            builder[p.Name] = p.Value.GetString()
                ?? throw new InvalidOperationException($"{ManifestFileName}: '{p.Name}' has no version string.");
        return builder.ToImmutable();
    }
}
