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

        var iconOpt = new Option<string>(
            new[] { "--icon", "-i" },
            description: "Nav-menu icon for the generated menu row (seed_menu_<plural>.sql). Defaults to 'list'.",
            getDefaultValue: () => "list");

        var pageCmd = new Command("page", "Generate a Blazor CRUD page (DataGrid + edit dialog + delete confirm) bound to an entity DTO.")
        {
            nameArg, fieldsOpt, routeOpt, outputOpt, namespaceOpt, policyOpt, iconOpt,
        };

        pageCmd.SetHandler(ctx => ctx.ExitCode = Run(
            ctx.ParseResult.GetValueForArgument(nameArg),
            ctx.ParseResult.GetValueForOption(fieldsOpt) ?? "",
            ctx.ParseResult.GetValueForOption(routeOpt) ?? "",
            ctx.ParseResult.GetValueForOption(outputOpt)!,
            ctx.ParseResult.GetValueForOption(namespaceOpt) ?? "",
            ctx.ParseResult.GetValueForOption(policyOpt) ?? "AdminAccess",
            ctx.ParseResult.GetValueForOption(iconOpt) ?? "list"));
        return pageCmd;
    }

    internal static int Run(string name, string fieldsRaw, string route, DirectoryInfo output, string nsOverride, string policy, string icon)
    {
        if (string.IsNullOrWhiteSpace(name) || !char.IsUpper(name[0]))
        {
            Console.Error.WriteLine("Entity name must be PascalCase (e.g. Customer).");
            return 2;
        }

        IReadOnlyList<FieldSpec> fields;
        try
        {
            fields = FieldSpecParser.Parse(fieldsRaw);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Field parse error: {ex.Message}");
            return 2;
        }

        if (fields.Count == 0)
        {
            Console.Error.WriteLine("At least one field is required via --fields.");
            return 2;
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
            MenuKey = MenuKeyFromUrl(resolvedRoute),
            Policy = string.IsNullOrWhiteSpace(policy) ? "AdminAccess" : policy,
            Icon = string.IsNullOrWhiteSpace(icon) ? "list" : icon,
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
        var rows = new List<(string Layer, string Path, bool Skipped)>();

        void Emit(string templateName, string target, string display)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (File.Exists(target)) { rows.Add((LayerOf(templateName), display, true)); skipped++; return; }
            File.WriteAllText(target, TemplateRenderer.Render(templateName, model));
            rows.Add((LayerOf(templateName), display, false)); written++;
        }

        foreach (var (relPath, templateName) in outputs)
            Emit(templateName, Path.Combine(output.FullName, relPath), relPath);

        // Menu/permission + localization seeds. Free mode (app has a free-mode management schema) → into
        // THIS app's OWN db as journaled migrations (auto-applied by `db apply`, no AppId, no AsdamirVault);
        // commercial → AppId-scoped AsdamirVault scripts under db/admin-onboarding/ (run manually).
        var isFree = IsFreeModeApp(appRoot);
        if (isFree)
        {
            var migDir = Path.Combine(appRoot!, "db", "migrations");
            var stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var locTarget = Path.Combine(migDir, $"V{stamp}__freemode_localize_{pluralLower}.sql");
            var menuTarget = Path.Combine(migDir, $"V{stamp}__freemode_menu_{pluralLower}.sql");
            Emit("FreeModePageLocalization", locTarget, Path.GetRelativePath(output.FullName, locTarget));
            Emit("FreeModePageMenuSeed", menuTarget, Path.GetRelativePath(output.FullName, menuTarget));
        }
        else
        {
            // Land under <app-root>/db/admin-onboarding/ next to register_*.sql; fall back to the output dir.
            var locDir = appRoot is not null ? Path.Combine(appRoot, "db", "admin-onboarding") : output.FullName;
            var locTarget = Path.Combine(locDir, $"localize_{pluralLower}.sql");
            var menuTarget = Path.Combine(locDir, $"seed_menu_{pluralLower}.sql");
            Emit("PageLocalization", locTarget, Path.GetRelativePath(output.FullName, locTarget));
            Emit("PageMenuSeed", menuTarget, Path.GetRelativePath(output.FullName, menuTarget));
        }

        Console.WriteLine();
        Console.WriteLine($"✓ Generated page '{name}' ({written} files{(skipped > 0 ? $", {skipped} skipped" : "")})");
        Console.WriteLine();
        OutputFormatter.PrintGroupedFiles(rows);
        Console.WriteLine();
        if (isFree)
            Console.WriteLine($"  next: apply db/migrations/V*__freemode_{{localize,menu}}_{pluralLower}.sql to the app's own DB via `asdamir db apply` (menu + permission + localization)");
        else
        {
            Console.WriteLine($"  next: apply db/admin-onboarding/{{localize,seed_menu}}_{pluralLower}.sql to AsdamirVault (menu + permission + localization)");
            Console.WriteLine($"        register the 'AdminApi' HttpClient in DI · ensure the '{model.Policy}' policy is configured");
            if (string.IsNullOrEmpty(appCode))
                Console.WriteLine("        NOTE: app Code unresolved (no .sln) — set @AppCode at the top of those SQL files before running them");
        }
        return 0;
    }

    // Maps a template name to the human-readable layer label shown in the grouped output table.
    private static string LayerOf(string template) => template switch
    {
        "Dto" => "DTO",
        "Page" or "PageDialog" => "Page",
        "PageLocalization" or "FreeModePageLocalization" => "Localization",
        "PageMenuSeed" or "FreeModePageMenuSeed" => "Menu+Perms",
        _ => template,
    };

    /// <summary>
    /// Derives the stable menu-label localization key from the page Url — MUST match the generated
    /// NavMenu's MenuKey() so the central menu entry resolves the same key ("/order-items" -> "Menu.OrderItems").
    /// </summary>
    private static string MenuKeyFromUrl(string url)
    {
        var parts = url.Trim('/').Split(new[] { '/', '-' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "Menu.Item";
        var slug = string.Concat(parts.Select(p => char.ToUpperInvariant(p[0]) + p[1..]));
        return "Menu." + slug;
    }

    /// <summary>
    /// A free-mode app carries a <c>*__freemode_management_schema.sql</c> migration in its
    /// <c>db/migrations</c> folder (emitted by <c>new app --mode free</c>). Its presence is the signal to
    /// seed page menu/localization into the app's OWN DB rather than the central AsdamirVault.
    /// </summary>
    internal static bool IsFreeModeApp(string? appRoot)
    {
        if (appRoot is null) return false;
        var migDir = Path.Combine(appRoot, "db", "migrations");
        return Directory.Exists(migDir)
            && Directory.GetFiles(migDir, "*__freemode_management_schema.sql").Length > 0;
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
