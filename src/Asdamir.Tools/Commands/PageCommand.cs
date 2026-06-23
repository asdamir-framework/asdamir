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
/// <c>asdamir new page &lt;Name&gt; --fields "F1:type,..." [--route /customers] [--output dir] [--namespace MyApp]</c>
///
/// Produces a Blazor Web App CRUD page that mirrors the AppsList / Users pattern in
/// the admin console: FluentDataGrid + inline edit dialog + delete confirmation,
/// with [Authorize] applied and standard loading/empty/error states.
///
/// Pair with <c>asdamir new entity &lt;Name&gt; --fields ...</c> — use the same field list
/// so the generated page binds to the matching DTO. The page assumes the entity's
/// REST controller lives at <c>api/&lt;plural-lowercase&gt;</c>.
/// </summary>
public static class PageCommand
{
    public static Command Build()
    {
        var nameArg = new Argument<string>("name", "PascalCase entity name (e.g. Customer). The page is named <Name>List.razor.");

        var fieldsOpt = new Option<string>(
            new[] { "--fields", "-f" },
            description: "Comma-separated 'Name:type' list — same syntax as `new entity`. Drives the grid columns and the edit form.")
        { IsRequired = true };

        var routeOpt = new Option<string>(
            new[] { "--route", "-r" },
            description: "Page route. Defaults to '/<plural-lowercase>'.",
            getDefaultValue: () => "");

        var outputOpt = new Option<DirectoryInfo>(
            new[] { "--output", "-o" },
            description: "Output directory. Defaults to the current working directory.",
            getDefaultValue: () => new DirectoryInfo(Directory.GetCurrentDirectory()));

        var namespaceOpt = new Option<string>(
            new[] { "--namespace", "-n" },
            description: "Root namespace for generated code. Defaults to the entity name.",
            getDefaultValue: () => "");

        var policyOpt = new Option<string>(
            new[] { "--policy", "-p" },
            description: "Authorization policy applied to the page. Defaults to 'AdminAccess'.",
            getDefaultValue: () => "AdminAccess");

        var pageCmd = new Command("page", "Generate a Blazor CRUD page (DataGrid + edit dialog + delete confirm) bound to an entity DTO.")
        {
            nameArg, fieldsOpt, routeOpt, outputOpt, namespaceOpt, policyOpt,
        };

        pageCmd.SetHandler(Run, nameArg, fieldsOpt, routeOpt, outputOpt, namespaceOpt, policyOpt);
        return pageCmd;
    }

    private static void Run(string name, string fieldsRaw, string route, DirectoryInfo output, string nsOverride, string policy)
    {
        if (string.IsNullOrWhiteSpace(name) || !char.IsUpper(name[0]))
        {
            Console.Error.WriteLine("Entity name must be PascalCase (e.g. Customer).");
            Environment.Exit(2);
            return;
        }

        IReadOnlyList<FieldSpec> fields;
        try
        {
            fields = FieldSpecParser.Parse(fieldsRaw);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Field parse error: {ex.Message}");
            Environment.Exit(2);
            return;
        }

        if (fields.Count == 0)
        {
            Console.Error.WriteLine("At least one field is required via --fields.");
            Environment.Exit(2);
            return;
        }

        // --namespace wins; else the containing project's namespace; else (no project) the entity name.
        var ns = !string.IsNullOrWhiteSpace(nsOverride) ? nsOverride
               : NameHelper.ResolveProjectNamespace(output) is { Length: > 0 } proj ? proj
               : name;
        var plural = NameHelper.Pluralize(name);
        var pluralLower = plural.ToLowerInvariant();
        var resolvedRoute = string.IsNullOrWhiteSpace(route) ? "/" + pluralLower : route;
        if (!resolvedRoute.StartsWith('/')) resolvedRoute = "/" + resolvedRoute;

        var fieldViews = fields.Select(f => new FieldView(f)).ToList();

        // The per-page localization seed must resolve this app's AppId in AsdamirVault by the app Code.
        // `new page` doesn't take a Code, so derive it from the app root's .sln file name (the same value
        // `new app` used for dbo.Apps.Code). If we can't find one, leave a clear placeholder the dev fills.
        var (appRoot, appCode) = ResolveAppRootAndCode(output);

        var model = new
        {
            EntityName = name,
            EntityCamel = char.ToLowerInvariant(name[0]) + name[1..],
            EntityPlural = plural,
            EntityPluralLower = pluralLower,
            Namespace = ns,
            Route = resolvedRoute,
            Policy = string.IsNullOrWhiteSpace(policy) ? "AdminAccess" : policy,
            Fields = fieldViews,
            ApiPath = $"api/{pluralLower}",
            AppCode = appCode,
            GeneratedAtUtc = DateTime.UtcNow.ToString("u"),
        };

        var outputs = new[]
        {
            // The UI tier keeps its OWN DTO copy (layering rule) so the page compiles standalone —
            // it does NOT reference the Gateway's DTO. Rendered into this (Server) project's Dtos/.
            ($"Dtos/{name}Dto.cs",                         "Dto"),
            ($"Components/Pages/{plural}List.razor",       "Page"),
            ($"Components/Pages/{name}EditorDialog.razor", "PageDialog"),
        };

        var written = 0;
        var skipped = 0;
        foreach (var (relPath, templateName) in outputs)
        {
            var target = Path.Combine(output.FullName, relPath);
            var dir = Path.GetDirectoryName(target)!;
            Directory.CreateDirectory(dir);

            if (File.Exists(target))
            {
                Console.WriteLine($"  SKIP (exists): {relPath}");
                skipped++;
                continue;
            }

            var content = TemplateRenderer.Render(templateName, model);
            File.WriteAllText(target, content);
            Console.WriteLine($"  WROTE: {relPath}");
            written++;
        }

        // Per-entity localization seed (Page.Title + Field.* in 3 cultures), scoped by AppId in AsdamirVault.
        // Lands under <app-root>/db/admin-onboarding/localize_<pluralLower>.sql (next to register_*.sql); if no
        // app root was found, fall back to the output dir so the file is never silently dropped.
        var locDir = appRoot is not null
            ? Path.Combine(appRoot, "db", "admin-onboarding")
            : output.FullName;
        Directory.CreateDirectory(locDir);
        var locTarget = Path.Combine(locDir, $"localize_{pluralLower}.sql");
        var locRel = Path.GetRelativePath(output.FullName, locTarget);
        if (File.Exists(locTarget))
        {
            Console.WriteLine($"  SKIP (exists): {locRel}");
            skipped++;
        }
        else
        {
            File.WriteAllText(locTarget, TemplateRenderer.Render("PageLocalization", model));
            Console.WriteLine($"  WROTE: {locRel}");
            written++;
        }

        Console.WriteLine();
        Console.WriteLine($"Done. {written} written, {skipped} skipped.");
        Console.WriteLine($"Next: register the AdminApi HttpClient (named 'AdminApi') in DI and ensure the [Authorize] policy '{model.Policy}' is configured.");
        Console.WriteLine($"Next: apply {Path.Combine("db", "admin-onboarding", $"localize_{pluralLower}.sql")} against AsdamirVault to seed the page's localization keys.");
        if (string.IsNullOrEmpty(appCode))
            Console.WriteLine("  NOTE: couldn't resolve the app Code (no .sln found) — edit @AppCode at the top of that SQL before running it.");
    }

    /// <summary>
    /// Resolves the app root and the app Code for the localization seed. Walks up from the output dir to
    /// the nearest ancestor that contains a <c>db/admin-onboarding</c> folder or a <c>.sln</c> file; the
    /// app Code is that <c>.sln</c>'s file name (the value <c>new app</c> wrote to <c>dbo.Apps.Code</c>).
    /// Returns (null, "") when neither marker is found — the caller then falls back to the output dir and
    /// emits a placeholder the developer fills in.
    /// </summary>
    private static (string? AppRoot, string AppCode) ResolveAppRootAndCode(DirectoryInfo output)
    {
        for (var dir = output; dir is not null; dir = dir.Parent)
        {
            var sln = dir.GetFiles("*.sln").FirstOrDefault();
            var hasOnboarding = Directory.Exists(Path.Combine(dir.FullName, "db", "admin-onboarding"));
            if (sln is not null)
                return (dir.FullName, Path.GetFileNameWithoutExtension(sln.Name));
            if (hasOnboarding)
                return (dir.FullName, "");
        }
        return (null, "");
    }

    /// <summary>
    /// View model for one field as rendered in the .razor template — adds form-input
    /// metadata (FluentTextField / FluentNumberField / FluentCheckbox / date picker) so
    /// the template doesn't have to branch on type strings.
    /// </summary>
    public sealed record FieldView(
        string Name,
        string CamelCase,
        string CSharpType,
        bool IsNullable,
        bool IsRequired,
        string InputKind,   // "text" | "number" | "bool" | "date" | "guid"
        string DefaultExpr) // e.g. "string.Empty", "0", "false", "DateTime.UtcNow", "Guid.Empty"
    {
        public FieldView(FieldSpec f) : this(
            f.Name,
            f.CamelCase,
            f.CSharpType,
            f.IsNullable,
            f.IsRequired,
            ResolveInputKind(f.CSharpType),
            ResolveDefault(f.CSharpType, f.IsNullable))
        { }

        private static string ResolveInputKind(string cs) => cs.TrimEnd('?') switch
        {
            "string" => "text",
            "int" or "long" or "decimal" or "double" => "number",
            "bool" => "bool",
            "DateTime" => "date",
            "Guid" => "guid",
            _ => "text",
        };

        private static string ResolveDefault(string cs, bool nullable)
        {
            if (nullable) return "null";
            return cs switch
            {
                "string" => "string.Empty",
                "int" => "0",
                "long" => "0L",
                "decimal" => "0m",
                "double" => "0d",
                "bool" => "false",
                "DateTime" => "DateTime.UtcNow",
                "Guid" => "Guid.Empty",
                _ => "default!",
            };
        }
    }

}
