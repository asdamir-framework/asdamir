// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.CommandLine;

namespace Asdamir.Tools.Commands;

/// <summary>
/// <c>asdamir new module &lt;Name&gt; [--description "..."] [--output dir]</c>
///
/// Produces an audit-shaped Core.* module skeleton that builds out-of-the-box and
/// matches the conventions used by existing modules (Core.Validation, Web.Security, …):
///
///   - &lt;Project&gt;/&lt;Project&gt;.csproj            — net9.0, IsPackable, PackageId, README pack
///   - &lt;Project&gt;/README.md                     — module overview, NuGet metadata target
///   - &lt;Project&gt;/Extensions/ServiceCollectionExtensions.cs — idempotent TryAdd registrations
///   - &lt;Project&gt;/Options/&lt;Bare&gt;Options.cs     — bound options class
///   - &lt;Project&gt;/Abstractions/I&lt;Bare&gt;Service.cs — public abstraction
///   - &lt;Project&gt;/Services/&lt;Bare&gt;Service.cs    — concrete skeleton (logger-injected)
///
/// Both <c>Telemetry</c> and <c>Core.Telemetry</c> are accepted — the <c>Core.</c> prefix is
/// normalized so the resulting project is always <c>Core.&lt;Bare&gt;</c>.
/// </summary>
public static class ModuleCommand
{
    public static Command Build()
    {
        var nameArg = new Argument<string>("name",
            "Module name. Either bare ('Telemetry') or fully qualified ('Core.Telemetry'). The 'Core.' prefix is added automatically.");

        var descriptionOpt = new Option<string>(
            new[] { "--description", "-d" },
            description: "NuGet package description. Defaults to a placeholder you should edit before publishing.",
            getDefaultValue: () => "");

        var outputOpt = new Option<DirectoryInfo>(
            new[] { "--output", "-o" },
            description: "Parent directory under which '<Project>/' will be created. Defaults to the current working directory.",
            getDefaultValue: () => new DirectoryInfo(Directory.GetCurrentDirectory()));

        var moduleCmd = new Command("module", "Generate a Core.* framework module skeleton (csproj + DI extension + abstraction + service + README).")
        {
            nameArg, descriptionOpt, outputOpt,
        };

        moduleCmd.SetHandler(Run, nameArg, descriptionOpt, outputOpt);
        return moduleCmd;
    }

    private static void Run(string name, string description, DirectoryInfo output)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Console.Error.WriteLine("Module name is required.");
            Environment.Exit(2);
            return;
        }

        // Accept "Telemetry" OR "Core.Telemetry" — strip the prefix if present, then validate.
        var bareName = name.StartsWith("Core.", StringComparison.Ordinal) ? name.Substring(5) : name;
        if (bareName.Length == 0 || !char.IsUpper(bareName[0]))
        {
            Console.Error.WriteLine("Module name (after stripping the optional 'Core.' prefix) must be PascalCase (e.g. Telemetry).");
            Environment.Exit(2);
            return;
        }

        var projectName = "Core." + bareName;        // Core.Telemetry
        var packageId = "Framework." + projectName;  // Core.Telemetry
        var ns = projectName;                        // namespace = Core.Telemetry
        var resolvedDescription = string.IsNullOrWhiteSpace(description)
            ? $"{projectName} module for the Asdamir framework. TODO: replace this description before publishing."
            : description;

        var model = new
        {
            BareName = bareName,
            ProjectName = projectName,
            PackageId = packageId,
            Namespace = ns,
            Description = resolvedDescription,
            GeneratedAtUtc = DateTime.UtcNow.ToString("u"),
        };

        var projectRoot = Path.Combine(output.FullName, projectName);
        if (Directory.Exists(projectRoot) && Directory.EnumerateFileSystemEntries(projectRoot).Any())
        {
            // Don't blow over an existing project — bail with an actionable hint instead.
            Console.Error.WriteLine($"Refusing to write into non-empty directory '{projectRoot}'. Remove it first or choose a different --output.");
            Environment.Exit(3);
            return;
        }
        Directory.CreateDirectory(projectRoot);

        var outputs = new[]
        {
            ($"{projectName}.csproj",                                "Module.csproj"),
            ("README.md",                                            "ModuleReadme"),
            ("Extensions/ServiceCollectionExtensions.cs",            "ModuleServiceCollectionExtensions"),
            ($"Options/{bareName}Options.cs",                        "ModuleOptions"),
            ($"Abstractions/I{bareName}Service.cs",                  "ModuleAbstraction"),
            ($"Services/{bareName}Service.cs",                       "ModuleService"),
        };

        var written = 0;
        var skipped = 0;
        foreach (var (relPath, templateName) in outputs)
        {
            var target = Path.Combine(projectRoot, relPath);
            var dir = Path.GetDirectoryName(target)!;
            Directory.CreateDirectory(dir);

            if (File.Exists(target))
            {
                Console.WriteLine($"  SKIP (exists): {projectName}/{relPath}");
                skipped++;
                continue;
            }

            var content = TemplateRenderer.Render(templateName, model);
            File.WriteAllText(target, content);
            Console.WriteLine($"  WROTE: {projectName}/{relPath}");
            written++;
        }

        Console.WriteLine();
        Console.WriteLine($"Done. {written} written, {skipped} skipped.");
        Console.WriteLine($"Next: add '<ProjectReference Include=\"../{projectName}/{projectName}.csproj\" />' to consumers, and call services.Add{bareName}() in Program.cs.");
    }
}
