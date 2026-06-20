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

        var entityCmd = new Command("entity", "Generate a complete entity slice (POCO + DTO + repo + service + controller + tests + migration).")
        {
            nameArg, fieldsOpt, outputOpt, namespaceOpt,
        };

        entityCmd.SetHandler(Run, nameArg, fieldsOpt, outputOpt, namespaceOpt);
        return entityCmd;
    }

    private static void Run(string name, string fieldsRaw, DirectoryInfo output, string nsOverride)
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

        var ns = string.IsNullOrWhiteSpace(nsOverride) ? name : nsOverride;
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
            MigrationStamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
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

        Console.WriteLine();
        Console.WriteLine($"Done. {written} written, {skipped} skipped.");
        Console.WriteLine($"Next: review the generated files, register {name}Service / {name}Repository in DI, and apply the migration.");
    }

}
