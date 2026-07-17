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
/// <c>asdamir add field &lt;EntityName&gt; --field "Name:type" [--output dir]</c>
///
/// Extends a previously-scaffolded entity slice with one new field. The two files
/// that are mechanical enough to patch automatically are touched in-place; the
/// non-mechanical files (Repository / Service / Validator) get printed snippets
/// the caller can paste in by hand — patching SQL strings would be too brittle
/// once consumers start editing them.
///
/// Touched in place:
///   - <c>Domain/&lt;Entity&gt;.cs</c>        — new property inserted before <c>CreatedAtUtc</c>
///   - <c>Dtos/&lt;Entity&gt;Dto.cs</c>       — new property + DataAnnotations inserted before <c>CreatedAtUtc</c>
///   - <c>db/migrations/V&lt;stamp&gt;__add_&lt;field&gt;_to_&lt;plural&gt;.sql</c> — new ALTER TABLE script
///
/// Printed for manual integration:
///   - Repository SELECT / INSERT / UPDATE column list updates
///   - Validator rule
///
/// Both file patches are anchor-based on the <c>public DateTime CreatedAtUtc</c> line
/// that every freshly-generated entity carries. If a target file has lost the
/// anchor (heavily edited), the command refuses to patch it and prints the snippet
/// instead — fail-closed, never produce broken code.
/// </summary>
public static class AddFieldCommand
{
    public static Command Build()
    {
        var nameArg = new Argument<string>("entity", "PascalCase entity name that was previously created via `asdamir new entity`.");

        var fieldOpt = new Option<string>(
            new[] { "--field", "-f" },
            description: "Single 'Name:type' spec — same syntax as `new entity --fields`. Add '?' for nullable. Example: \"Age:int\" or \"Note:string?\".")
        { IsRequired = true };

        var outputOpt = new Option<DirectoryInfo>(
            new[] { "--output", "-o" },
            description: "The app root OR the Gateway project. Defaults to the current directory (run from the app root).",
            getDefaultValue: () => new DirectoryInfo(Directory.GetCurrentDirectory()));
        var noDbOpt = new Option<bool>("--no-db",
            description: "Generate the ALTER migration only — don't apply it. (By default `add field` applies it via `db apply`.)",
            getDefaultValue: () => false);

        var fieldCmd = new Command("field", "Append one field to an existing entity scaffold (patches Entity.cs + Dto.cs, generates + applies a new ALTER TABLE migration, prints the snippets you still need to add by hand).")
        {
            nameArg, fieldOpt, outputOpt, noDbOpt,
        };

        fieldCmd.SetHandler(async ctx => await Run(
            ctx.ParseResult.GetValueForArgument(nameArg),
            ctx.ParseResult.GetValueForOption(fieldOpt) ?? "",
            ctx.ParseResult.GetValueForOption(outputOpt)!,
            ctx.ParseResult.GetValueForOption(noDbOpt)));
        return fieldCmd;
    }

    private static async Task Run(string entityName, string fieldRaw, DirectoryInfo output, bool noDb)
    {
        if (string.IsNullOrWhiteSpace(entityName) || !char.IsUpper(entityName[0]))
        {
            Console.Error.WriteLine("Entity name must be PascalCase (e.g. Customer).");
            Environment.Exit(2);
            return;
        }

        IReadOnlyList<FieldSpec> fields;
        try
        {
            fields = FieldSpecParser.Parse(fieldRaw);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Field parse error: {ex.Message}");
            Environment.Exit(2);
            return;
        }

        if (fields.Count != 1)
        {
            Console.Error.WriteLine("Pass exactly one field via --field. For multiple, re-run the command per field.");
            Environment.Exit(2);
            return;
        }

        // Run from the app ROOT (no `cd src/<App>.Gateway` needed) — resolve the Gateway project from --output
        // (the .sln + src/<Gateway>), or use --output verbatim when it already IS the Gateway (backward-compat).
        var gatewayDir = FeatureCommand.ResolveProjectDir(output, isGateway: true);
        if (gatewayDir is null)
        {
            Console.Error.WriteLine("Not inside an Asdamir app (no .sln found, and this isn't a Gateway project). Run this from the app root or pass --output <app root or Gateway project>.");
            Environment.Exit(2);
            return;
        }
        output = new DirectoryInfo(gatewayDir);

        var field = fields[0];
        var entityPath = Path.Combine(output.FullName, "Domain", $"{entityName}.cs");
        var dtoPath = Path.Combine(output.FullName, "Dtos", $"{entityName}Dto.cs");

        var entityResult = PatchFile(entityPath, RenderEntityProperty(field));
        var dtoResult = PatchFile(dtoPath, RenderDtoProperty(field));

        // Both files missing → the user pointed at the wrong directory / mistyped the
        // entity name. Don't produce a dangling ALTER TABLE for a table that doesn't
        // exist in this slice — report and exit non-zero so CI catches the typo.
        if (entityResult == PatchOutcome.FileMissing && dtoResult == PatchOutcome.FileMissing)
        {
            Console.Error.WriteLine($"Neither '{entityPath}' nor '{dtoPath}' exists.");
            Console.Error.WriteLine($"Did you run this in the wrong --output, or mistype '{entityName}'?");
            Environment.Exit(2);
            return;
        }

        // If both code files already had the property, the field is fully applied —
        // don't add yet another timestamped migration. The user already has one from
        // the original `add field` invocation; piling up new ALTER TABLEs would force
        // them to clean each duplicate up by hand.
        var allAlreadyPresent =
            entityResult == PatchOutcome.AlreadyPresent &&
            dtoResult == PatchOutcome.AlreadyPresent;

        var pluralLower = NameHelper.Pluralize(entityName).ToLowerInvariant();
        var migrationStamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var migrationRel = Path.Combine("db", "migrations",
            $"V{migrationStamp}__add_{field.Name.ToLowerInvariant()}_to_{pluralLower}.sql");
        var migrationAbs = Path.Combine(output.FullName, migrationRel);
        var migrationWritten = false;
        if (allAlreadyPresent)
        {
            Console.WriteLine($"  SKIP migration: field '{field.Name}' is already present in both code files.");
        }
        else
        {
            Directory.CreateDirectory(Path.GetDirectoryName(migrationAbs)!);
            var migrationContent = RenderMigration(entityName, field, migrationStamp);
            File.WriteAllText(migrationAbs, migrationContent);
            Console.WriteLine($"  WROTE: {migrationRel}");
            migrationWritten = true;
        }

        Console.WriteLine();
        Report("Domain/" + entityName + ".cs", entityResult, RenderEntityProperty(field));
        Report("Dtos/" + entityName + "Dto.cs", dtoResult, RenderDtoProperty(field));

        // Repository / Service / Validator: print snippets, don't try to splice SQL ourselves.
        Console.WriteLine();
        Console.WriteLine($"Manual edits still needed:");
        Console.WriteLine();
        Console.WriteLine($"  Repositories/{entityName}Repository.cs — add '{field.Name}' to SELECT, INSERT, UPDATE column lists:");
        Console.WriteLine($"    SELECT  …, {field.Name}, …");
        Console.WriteLine($"    INSERT  (…, {field.Name}, …) VALUES (…, @{field.Name}, …)");
        Console.WriteLine($"    UPDATE  SET …, {field.Name} = @{field.Name}, …");
        Console.WriteLine();
        Console.WriteLine($"  Validators/{entityName}DtoValidator.cs — append rule inside the ctor:");
        Console.WriteLine("    " + RenderValidatorRule(field).Replace("\n", "\n    "));
        Console.WriteLine();
        Console.WriteLine($"Done. {(entityResult == PatchOutcome.Patched ? 1 : 0) + (dtoResult == PatchOutcome.Patched ? 1 : 0)} files patched, {(migrationWritten ? 1 : 0)} migration written.");

        // Auto-apply the ALTER migration by DEFAULT (unless --no-db), reusing `db apply`'s journaled runner and
        // resolving the connection from the Gateway user-secret (same as `new entity`). Idempotent; if no
        // connection resolves, the file is written and the manual command is printed — never a hard failure.
        if (!migrationWritten) return;
        var migDir = new DirectoryInfo(Path.Combine(output.FullName, "db", "migrations"));
        var appRoot = FeatureCommand.FindAppRoot(output);
        var migRel = appRoot is not null ? Path.GetRelativePath(appRoot, migDir.FullName).Replace('\\', '/') : "db/migrations";
        var applyCmd = $"asdamir db apply --migrations {migRel}";
        if (noDb)
        {
            Console.WriteLine($"  --no-db: apply the ALTER with:  {applyCmd}");
            return;
        }
        if (DbApplyCommand.TryResolveConnectionFromApp(migDir, out _) is not { Length: > 0 })
        {
            Console.WriteLine($"  no connection resolved — apply the ALTER with:  {applyCmd}  (or set the Gateway ConnectionStrings:Default secret)");
            return;
        }
        Console.WriteLine();
        var exit = await DbApplyCommand.RunAsync("", "localhost", "", "", "", migDir, createDatabase: false);
        if (exit != 0) Console.Error.WriteLine($"  ⚠️  ALTER not applied (exit {exit}) — apply it with:  {applyCmd}");
        else FeatureCommand.PrintRestartHint(appRoot);   // schema + code changed → rebuild/restart to pick it up
    }

    private enum PatchOutcome { Patched, AnchorMissing, FileMissing, AlreadyPresent }

    private static PatchOutcome PatchFile(string path, string newPropertyBlock)
    {
        if (!File.Exists(path)) return PatchOutcome.FileMissing;

        var text = File.ReadAllText(path);

        // Idempotency: if the exact property block is already in the file, skip.
        if (text.Contains(newPropertyBlock.TrimEnd('\r', '\n'))) return PatchOutcome.AlreadyPresent;

        // Anchor: every freshly-scaffolded entity / dto has this exact line. We insert
        // the new property + a blank line right before it. If the anchor is missing
        // (file was heavily edited), refuse to patch — fail-closed.
        const string anchor = "    public DateTime CreatedAtUtc";
        var anchorIdx = text.IndexOf(anchor, StringComparison.Ordinal);
        if (anchorIdx < 0) return PatchOutcome.AnchorMissing;

        // Walk back to the start of the anchor's line to insert before it cleanly.
        var insertAt = anchorIdx;
        while (insertAt > 0 && text[insertAt - 1] != '\n') insertAt--;

        var patched = text.Insert(insertAt, newPropertyBlock);
        File.WriteAllText(path, patched);
        return PatchOutcome.Patched;
    }

    private static void Report(string relPath, PatchOutcome outcome, string snippet)
    {
        switch (outcome)
        {
            case PatchOutcome.Patched:
                Console.WriteLine($"  PATCHED: {relPath}");
                break;
            case PatchOutcome.AlreadyPresent:
                Console.WriteLine($"  SKIP (already present): {relPath}");
                break;
            case PatchOutcome.FileMissing:
                Console.Error.WriteLine($"  MISSING: {relPath} (was the entity scaffolded here?). Paste manually:");
                Console.Error.WriteLine(IndentForReport(snippet));
                break;
            case PatchOutcome.AnchorMissing:
                Console.Error.WriteLine($"  ANCHOR LOST: {relPath} — heavily edited, refusing to patch. Paste manually before CreatedAtUtc:");
                Console.Error.WriteLine(IndentForReport(snippet));
                break;
        }
    }

    private static string IndentForReport(string snippet) =>
        "    " + snippet.TrimEnd('\r', '\n').Replace("\n", "\n    ");

    // Produces the same shape Entity.sbn emits for one field, including its trailing
    // newline. Inserted as a block, immediately before the CreatedAtUtc anchor.
    private static string RenderEntityProperty(FieldSpec f)
    {
        var initializer = (f.CSharpType == "string" && !f.IsNullable) ? " = string.Empty;" : "";
        return $"    public {f.CSharpType} {f.Name} {{ get; set; }}{initializer}\n";
    }

    private static string RenderDtoProperty(FieldSpec f)
    {
        var attr = (f.IsRequired, f.CSharpType) switch
        {
            (true, "string") => "    [Required, MaxLength(500)]\n",
            (true, _) => "    [Required]\n",
            (false, "string?") => "    [MaxLength(500)]\n",
            _ => "",
        };
        var initializer = (f.CSharpType == "string" && !f.IsNullable) ? " = string.Empty;" : "";
        return $"{attr}    public {f.CSharpType} {f.Name} {{ get; set; }}{initializer}\n";
    }

    private static string RenderValidatorRule(FieldSpec f)
    {
        if (f.IsRequired && f.CSharpType == "string")
        {
            return $"RuleFor(x => x.{f.Name})\n    .NotEmpty().WithMessage(\"{f.Name} is required.\")\n    .MaximumLength(500);";
        }
        if (f.IsRequired)
        {
            return $"RuleFor(x => x.{f.Name}).NotEmpty();";
        }
        return $"// {f.Name} is optional — no validator rule needed by default.";
    }

    private static string RenderMigration(string entityName, FieldSpec f, string stamp)
    {
        var table = NameHelper.Pluralize(entityName);
        var nullClause = f.IsNullable ? "NULL" : "NOT NULL";
        var defaultClause = "";

        // Non-nullable string columns need a default so the ALTER TABLE doesn't fail on
        // existing rows. Other non-nullable types use type-appropriate defaults too.
        if (!f.IsNullable)
        {
            defaultClause = f.CSharpType switch
            {
                "string" => " DEFAULT ('')",
                "int" or "long" or "decimal" or "double" => " DEFAULT (0)",
                "bool" => " DEFAULT (0)",
                "DateTime" => " DEFAULT (SYSUTCDATETIME())",
                "Guid" => " DEFAULT (NEWID())",
                _ => "",
            };
        }

        return $"""
            -- <auto-generated>
            --   Migration: add `{f.Name}` ({f.CSharpType}) to dbo.{table}
            --   Generated by `asdamir add field {entityName} --field "{f.Name}:{f.CSharpType.TrimEnd('?')}{(f.IsNullable ? "?" : "")}"` on {DateTime.UtcNow:u}.
            --   Stamp: V{stamp}
            -- </auto-generated>

            ALTER TABLE dbo.[{table}] ADD [{f.Name}] {f.SqlType} {nullClause}{defaultClause};
            GO

            """;
    }
}
