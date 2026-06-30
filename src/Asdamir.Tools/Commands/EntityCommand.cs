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
/// <c>asdamir new entity &lt;Name&gt; --fields "F1:type,F2:type?,..." [--output path] [--namespace MyApp]</c>
///
/// Produces (one set per invocation):
///   - Domain/&lt;Name&gt;.cs            — POCO entity, audit-aware ([AuditLog])
///   - Dtos/&lt;Name&gt;Dto.cs           — DTO with [Required] / [MaxLength] attributes
///   - Repositories/I&lt;Name&gt;Repository.cs + &lt;Name&gt;Repository.cs — Dapper, tenant-scoped
///   - Services/I&lt;Name&gt;Service.cs + &lt;Name&gt;Service.cs   — application service
///   - Controllers/&lt;Name&gt;sController.cs  — REST controller, [Authorize], [AuditLog]
///   - Validators/&lt;Name&gt;DtoValidator.cs  — FluentValidation
///   - Tests/&lt;Name&gt;Tests.cs        — happy-path + at least one regression pin
///   - db/migrations/V*__create_&lt;name&gt;s.sql — SQL Server migration
/// </summary>
public static class EntityCommand
{
    public static Command Build()
    {
        var nameArg = new Argument<string>("name", "PascalCase entity name (e.g. Customer).");

        var fieldsOpt = new Option<string>(
            new[] { "--fields", "-f" },
            description: "Comma-separated 'Name:type' list. Types: string, int, long, bool, decimal, double, DateTime, Guid. Add '?' for nullable. Example: \"Name:string,Email:string?,IsActive:bool\".")
        { IsRequired = true };

        var outputOpt = new Option<DirectoryInfo>(
            new[] { "--output", "-o" },
            description: "Output directory. Defaults to the current working directory.",
            getDefaultValue: () => new DirectoryInfo(Directory.GetCurrentDirectory()));

        var namespaceOpt = new Option<string>(
            new[] { "--namespace", "-n" },
            description: "Root namespace for generated code. Defaults to the entity name.",
            getDefaultValue: () => "");

        // Optional: apply the generated migration right away (reuses `db apply`'s journaled runner against
        // <output>/db/migrations). Connection options mirror `db apply` exactly — required when --apply is set.
        var applyOpt = new Option<bool>(
            "--apply",
            description: "After generating, apply the migration via the journaled `db apply` runner. Requires a connection (below).",
            getDefaultValue: () => false);
        var connOpt = new Option<string>(
            new[] { "--connection", "-c" },
            description: "Full ADO.NET connection string for --apply. Takes precedence over --server/--database/--user/--password.",
            getDefaultValue: () => "");
        var serverOpt = new Option<string>(
            new[] { "--server", "-S" }, description: "SQL Server instance for --apply (when --connection is omitted).",
            getDefaultValue: () => "localhost");
        var databaseOpt = new Option<string>(
            new[] { "--database", "-d" }, description: "Target database for --apply (required when --connection is omitted).",
            getDefaultValue: () => "");
        var userOpt = new Option<string>(
            new[] { "--user", "-U" }, description: "SQL login for --apply (SQL auth). Omit for Windows integrated auth.",
            getDefaultValue: () => "");
        var passwordOpt = new Option<string>(
            new[] { "--password", "-P" }, description: "Password for --user.",
            getDefaultValue: () => "");

        var entityCmd = new Command("entity", "Generate a complete entity slice (POCO + DTO + repo + service + controller + tests + migration).")
        {
            nameArg, fieldsOpt, outputOpt, namespaceOpt, applyOpt, connOpt, serverOpt, databaseOpt, userOpt, passwordOpt,
        };

        entityCmd.SetHandler(async ctx =>
        {
            ctx.ExitCode = await RunAsync(
                ctx.ParseResult.GetValueForArgument(nameArg),
                ctx.ParseResult.GetValueForOption(fieldsOpt) ?? "",
                ctx.ParseResult.GetValueForOption(outputOpt)!,
                ctx.ParseResult.GetValueForOption(namespaceOpt) ?? "",
                ctx.ParseResult.GetValueForOption(applyOpt),
                ctx.ParseResult.GetValueForOption(connOpt) ?? "",
                ctx.ParseResult.GetValueForOption(serverOpt) ?? "localhost",
                ctx.ParseResult.GetValueForOption(databaseOpt) ?? "",
                ctx.ParseResult.GetValueForOption(userOpt) ?? "",
                ctx.ParseResult.GetValueForOption(passwordOpt) ?? "");
        });
        return entityCmd;
    }

    internal static async Task<int> RunAsync(string name, string fieldsRaw, DirectoryInfo output, string nsOverride,
        bool apply, string connection, string server, string database, string user, string password)
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
        var pluralLower = NameHelper.Pluralize(name).ToLowerInvariant();

        var model = new
        {
            EntityName = name,                // "Customer"
            EntityCamel = char.ToLowerInvariant(name[0]) + name[1..], // "customer"
            EntityPlural = NameHelper.Pluralize(name),   // "Customers"
            EntityPluralLower = pluralLower,             // "customers"
            TableName = NameHelper.Pluralize(name),      // "Customers"
            Namespace = ns,                   // root namespace
            Fields = fields,
            GeneratedAtUtc = DateTime.UtcNow.ToString("u"),
            // ms precision (…fff) so two entities scaffolded in the same second get distinct, ordered
            // migration stamps instead of colliding on V<yyyyMMddHHmmss>__create_*.sql.
            MigrationStamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"),
            // Drives EntitySeed.sbn (the in-house templater can't loop a literal array): 3 sample rows.
            // String values get the Index suffix; numeric fields reuse IntVal/DecVal (pre-formatted, so
            // the template needs no arithmetic, which the templater also lacks).
            SeedRows = new[]
            {
                new { Index = "1", IntVal = "10", DecVal = "100.00" },
                new { Index = "2", IntVal = "20", DecVal = "200.00" },
                new { Index = "3", IntVal = "30", DecVal = "300.00" },
            },
        };

        var outputs = new[]
        {
            ($"Domain/{name}.cs",                                    "Entity"),
            ($"Dtos/{name}Dto.cs",                                   "Dto"),
            ($"Repositories/I{name}Repository.cs",                   "IRepository"),
            ($"Repositories/{name}Repository.cs",                    "Repository"),
            ($"Services/I{name}Service.cs",                          "IService"),
            ($"Services/{name}Service.cs",                           "Service"),
            ($"Controllers/{NameHelper.Pluralize(name)}Controller.cs",          "Controller"),
            ($"Validators/{name}DtoValidator.cs",                    "Validator"),
            ($"Tests/{name}Tests.cs",                                "Tests"),
            ($"db/migrations/V{model.MigrationStamp}__create_{pluralLower}.sql", "Migration"),
            // Idempotent sample-seed so the new entity's grid isn't empty on first run. Same stamp as
            // the create migration; "create" sorts before "seed" so the table exists when it runs.
            ($"db/migrations/V{model.MigrationStamp}__seed_{pluralLower}.sql", "EntitySeed"),
        };

        // The entity is scaffolded from its project dir (e.g. <app>/src/<App>.Gateway). xUnit test
        // files must NOT land in that app project (it has no test refs → CS0246). Route them to the
        // matching test project (<app>/tests/<App>.Gateway.Tests/Entities/) when it exists.
        var testTarget = ResolveTestTarget(output, name);

        var written = 0;
        var skipped = 0;
        var testCount = 0;
        var rows = new List<(string Layer, string Path, bool Skipped)>();
        foreach (var (relPath, templateName) in outputs)
        {
            var target = (templateName == "Tests" && testTarget is not null)
                ? testTarget
                : Path.Combine(output.FullName, relPath);
            var display = (templateName == "Tests" && testTarget is not null)
                ? Path.GetRelativePath(output.FullName, target)
                : relPath;
            var dir = Path.GetDirectoryName(target)!;
            Directory.CreateDirectory(dir);

            if (File.Exists(target))
            {
                rows.Add((LayerOf(templateName), display, true));
                skipped++;
                continue;
            }

            var content = TemplateRenderer.Render(templateName, model);
            File.WriteAllText(target, content);
            if (templateName == "Tests")
                testCount = content.Split("[Fact]").Length - 1 + (content.Split("[Theory]").Length - 1);
            rows.Add((LayerOf(templateName), display, false));
            written++;
        }

        Console.WriteLine();
        Console.WriteLine($"✓ Generated entity '{name}' ({written} files{(skipped > 0 ? $", {skipped} skipped" : "")})");
        Console.WriteLine();
        OutputFormatter.PrintGroupedFiles(rows);
        Console.WriteLine();
        var testNote = testCount > 0 ? $"{testCount} tests" : "";

        if (apply)
        {
            if (testNote.Length > 0) Console.WriteLine($"  {testNote} · applying migration via db apply…");
            Console.WriteLine();
            // Reuse db apply's journaled runner: it validates the connection (errors if neither --connection
            // nor --database is given), prints applied/skipped, and returns the exit code we propagate.
            // createDatabase:false — the app DB already exists by the time you add entities.
            var migDir = new DirectoryInfo(Path.Combine(output.FullName, "db", "migrations"));
            return await DbApplyCommand.RunAsync(connection, server, database, user, password, migDir, createDatabase: false);
        }

        var prefix = testNote.Length > 0 ? testNote + " · " : "";
        Console.WriteLine($"  {prefix}next: asdamir db apply  (apply the migration · DI auto-registers by convention)");
        return 0;
    }

    // Maps a template name to the human-readable layer label shown in the grouped output table.
    private static string LayerOf(string template) => template switch
    {
        "Entity" => "Domain",
        "Dto" => "DTO",
        "IRepository" or "Repository" => "Repository",
        "IService" or "Service" => "Service",
        "Controller" => "Controller",
        "Validator" => "Validator",
        "Tests" => "Tests",
        "Migration" or "EntitySeed" => "Migration",
        _ => template,
    };

    // Finds the conventional test project for the project the entity is scaffolded into: walk up to the
    // app root (the nearest ancestor containing a 'tests' folder) and look for tests/<ProjectDir>.Tests.
    // Returns the absolute path for the entity's test file there, or null to fall back to writing it
    // next to the entity (non-standard layout — the file would still need to be moved by hand).
    private static string? ResolveTestTarget(DirectoryInfo projectDir, string name)
    {
        for (var dir = projectDir; dir is not null; dir = dir.Parent)
        {
            var testsRoot = Path.Combine(dir.FullName, "tests");
            if (!Directory.Exists(testsRoot)) continue;
            var testProj = Path.Combine(testsRoot, projectDir.Name + ".Tests");
            return Directory.Exists(testProj)
                ? Path.Combine(testProj, "Entities", $"{name}Tests.cs")
                : null;
        }
        return null;
    }

}
