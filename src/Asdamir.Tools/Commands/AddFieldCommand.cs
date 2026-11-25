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
/// Adds ONE column to a previously-scaffolded entity — and is deliberately a PARTIAL operation. It says so,
/// loudly, every time it runs (see <see cref="NotUpdated"/>).
///
/// Done automatically:
///   - <c>Domain/&lt;Entity&gt;.cs</c>        — new property inserted before <c>CreatedAtUtc</c>
///   - <c>Dtos/&lt;Entity&gt;Dto.cs</c>       — new property + DataAnnotations inserted before <c>CreatedAtUtc</c>
///   - <c>db/migrations/V&lt;stamp&gt;__add_&lt;field&gt;_to_&lt;plural&gt;.sql</c> — an IDEMPOTENT
///     <c>ALTER TABLE … ADD</c> (guarded by <c>COL_LENGTH</c>, so re-applying it is a no-op), applied unless
///     <c>--no-db</c>.
///
/// NOT done — and until it is, the new field's value is silently DROPPED on every save:
///   - the <c>_Insert</c> / <c>_Update</c> stored procedures (they take an explicit parameter list)
///   - the repository (two anonymous parameter objects) and the service (three mappings)
///   - the validator, the Server-side DTO copy, the page grid + editor dialog, and the localization key
///
/// These are not patched because each is a file the developer is expected to have edited: splicing a
/// parameter list into a hand-tuned procedure, or a column into a hand-arranged grid, is the kind of edit that
/// is right nine times and silently wrong the tenth. The command prints the exact edits instead.
///
/// Both file patches are anchor-based on the <c>public DateTime CreatedAtUtc</c> line that every
/// freshly-generated entity carries. If a target file has lost the anchor (heavily edited), the command
/// refuses to patch it and prints the snippet instead — fail-closed, never produce broken code.
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

        var fieldCmd = new Command("field", "Add one column to an existing entity: patches Entity.cs + Dto.cs and generates + applies an idempotent ALTER TABLE. PARTIAL — the stored procedures, repository, service, validator, page and localization are NOT updated (the value is dropped on save until you apply the printed edits).")
        {
            nameArg, fieldOpt, outputOpt, noDbOpt,
        };

        // ctx.ExitCode, never Environment.Exit — see ExitCodes and the note in AuditLintCommand.
        fieldCmd.SetHandler(async ctx => ctx.ExitCode = await Run(
            ctx.ParseResult.GetValueForArgument(nameArg),
            ctx.ParseResult.GetValueForOption(fieldOpt) ?? "",
            ctx.ParseResult.GetValueForOption(outputOpt)!,
            ctx.ParseResult.GetValueForOption(noDbOpt)));
        return fieldCmd;
    }

    private static async Task<int> Run(string entityName, string fieldRaw, DirectoryInfo output, bool noDb)
    {
        if (!NameArgument.IsValid(entityName))
        {
            Console.Error.WriteLine(NameArgument.Explain(entityName, "entity name", "asdamir add field <Entity> --field <Name:type>"));
            return ExitCodes.Usage;
        }

        IReadOnlyList<FieldSpec> fields;
        try
        {
            fields = FieldSpecParser.Parse(fieldRaw);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Field parse error: {ex.Message}");
            return ExitCodes.Usage;
        }

        if (fields.Count != 1)
        {
            Console.Error.WriteLine("Pass exactly one field via --field. For multiple, re-run the command per field.");
            return ExitCodes.Usage;
        }

        // Run from the app ROOT (no `cd src/<App>.Gateway` needed) — resolve the Gateway project from --output
        // (the .sln + src/<Gateway>), or use --output verbatim when it already IS the Gateway (backward-compat).
        var gatewayDir = FeatureCommand.ResolveProjectDir(output, isGateway: true);
        if (gatewayDir is null)
        {
            Console.Error.WriteLine("Not inside an Asdamir app (no .sln found, and this isn't a Gateway project). Run this from the app root or pass --output <app root or Gateway project>.");
            return ExitCodes.Usage;
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
            return ExitCodes.Usage;
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

        Console.WriteLine();
        Console.WriteLine($"Done. {(entityResult == PatchOutcome.Patched ? 1 : 0) + (dtoResult == PatchOutcome.Patched ? 1 : 0)} files patched, {(migrationWritten ? 1 : 0)} migration written.");
        Console.WriteLine();
        foreach (var line in NotUpdated(entityName, field)) Console.WriteLine(line);

        // Auto-apply the ALTER migration by DEFAULT (unless --no-db), reusing `db apply`'s journaled runner and
        // resolving the connection from the Gateway user-secret (same as `new entity`). If no connection
        // resolves, the file is written and the manual command is printed (the soft path, exit 0).
        if (!migrationWritten) return ExitCodes.Success;
        var migDir = new DirectoryInfo(Path.Combine(output.FullName, "db", "migrations"));
        var appRoot = FeatureCommand.FindAppRoot(output);
        var migRel = appRoot is not null ? Path.GetRelativePath(appRoot, migDir.FullName).Replace('\\', '/') : "db/migrations";
        var applyCmd = $"asdamir db apply --migrations {migRel}";
        if (noDb)
        {
            Console.WriteLine($"  --no-db: apply the ALTER with:  {applyCmd}");
            return ExitCodes.Success;
        }
        if (DbApplyCommand.TryResolveConnectionFromApp(migDir, out _) is not { Length: > 0 })
        {
            Console.WriteLine($"  no connection resolved — apply the ALTER with:  {applyCmd}  (or set the Gateway ConnectionStrings:Default secret)");
            return ExitCodes.Success;
        }
        Console.WriteLine();
        var exit = await DbApplyCommand.RunAsync("", "localhost", "", "", "", migDir, createDatabase: false);
        if (exit != 0)
        {
            // Attempted and failed → a negative result, not "success". The code now has a property the table
            // does not have a column for; reporting 0 hid that from every script.
            Console.Error.WriteLine($"  ✗ ALTER NOT applied (db apply exit {exit}) — the column does not exist yet. Fix the cause, then:  {applyCmd}");
            return ExitCodes.Findings;
        }

        FeatureCommand.PrintRestartHint(appRoot);   // schema + code changed → rebuild/restart to pick it up
        return ExitCodes.Success;
    }

    /// <summary>
    /// The exact list of what <c>add field</c> did NOT update, with the edit each place needs — printed on
    /// every run.
    /// </summary>
    /// <remarks>
    /// The previous text told the user to add the column to the repository's "SELECT / INSERT / UPDATE column
    /// lists". The generated repository has no such lists — it calls stored procedures — so the instruction
    /// could not be followed, and the pieces that actually drop the value (the two procedures and the
    /// service mappings) were not mentioned at all.
    /// </remarks>
    /// <param name="entityName">The entity.</param>
    /// <param name="field">The field that was added.</param>
    /// <returns>The lines to print.</returns>
    internal static IReadOnlyList<string> NotUpdated(string entityName, FieldSpec field)
    {
        var table = NameHelper.Pluralize(entityName);
        var n = field.Name;
        return
        [
            $"⚠️  NOT UPDATED — '{n}' is in the entity, the DTO and the table, and NOWHERE ELSE.",
            $"    Until the edits below are made, the value of '{n}' is silently DROPPED on every create and update",
            "    (the stored procedures do not take it), and it does not appear in the UI.",
            "",
            $"  1. Stored procedures — add a NEW migration that re-creates dbo.{table}_Insert and dbo.{table}_Update",
            $"     (CREATE OR ALTER) with one more parameter `@{n} {field.SqlType}`, the column [{n}] in the INSERT",
            $"     column/VALUES lists, and `[{n}] = @{n}` in the UPDATE SET list. (_GetById/_List use SELECT * — no change.)",
            $"  2. Repositories/{entityName}Repository.cs — add `entity.{n},` to BOTH anonymous parameter objects",
            "     (InsertAsync and UpdateAsync). Do step 1 first: Dapper sends a parameter the procedure must accept.",
            $"  3. Services/{entityName}Service.cs — three places: `existing.{n} = dto.{n};` in UpdateAsync,",
            $"     `{n} = entity.{n},` in MapToDto, `{n} = dto.{n},` in MapToEntity.",
            $"  4. Validators/{entityName}DtoValidator.cs — " + RenderValidatorRule(field).Replace("\n", "\n     "),
            $"  5. Server (UI) project — Dtos/{entityName}Dto.cs (the UI keeps its own DTO copy): add the same property;",
            $"     Components/Pages/{table}List.razor: a <th>@L[\"Field.{entityName}.{n}\"]</th> + its <td>;",
            $"     Components/Pages/{entityName}EditorDialog.razor: an input bound to Model.{n}.",
            $"  6. Localization — seed the key `Field.{entityName}.{n}` in tr-TR / en-US / ru-RU (the canonical @Seed +",
            "     LocalizationResource_UpsertValue form), in a new seed next to the page's localize_* / freemode_localize_* file.",
        ];
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
            return $"append inside the ctor: RuleFor(x => x.{f.Name}).NotEmpty().WithMessage(\"{f.Name} is required.\").MaximumLength(500);";
        }
        if (f.IsRequired && f.CSharpType is "Guid" or "DateTime")
        {
            return $"append inside the ctor: RuleFor(x => x.{f.Name}).NotEmpty();";
        }
        return $"no rule needed for '{f.Name}' ({f.CSharpType}): a bool/number has no \"empty\" value (NotEmpty would reject false/0), and an optional field needs none.";
    }

    internal static string RenderMigration(string entityName, FieldSpec f, string stamp)
    {
        var table = NameHelper.Pluralize(entityName);
        var nullClause = f.IsNullable ? "NULL" : "NOT NULL";
        var defaultClause = "";

        // Non-nullable string columns need a default so the ALTER TABLE doesn't fail on
        // existing rows. Other non-nullable types use type-appropriate defaults too.
        if (!f.IsNullable)
        {
            var constraint = $" CONSTRAINT DF_{table}_{f.Name}";
            defaultClause = f.CSharpType switch
            {
                "string" => constraint + " DEFAULT ('')",
                "int" or "long" or "decimal" => constraint + " DEFAULT (0)",
                "bool" => constraint + " DEFAULT (0)",
                "DateTime" => constraint + " DEFAULT (SYSUTCDATETIME())",
                "Guid" => constraint + " DEFAULT (NEWID())",
                _ => "",
            };
        }

        // IDEMPOTENT: guarded by COL_LENGTH, so applying the script twice (a re-run after a partial failure, a
        // database restored from a backup taken after it) is a no-op instead of
        // "Column names in each table must be unique". The DEFAULT constraint is NAMED for the same reason a
        // rollback needs it: an anonymous one gets a random name and cannot be dropped by script.
        return $"""
            -- <auto-generated>
            --   Migration: add `{f.Name}` ({f.CSharpType}) to dbo.{table}
            --   Generated by `asdamir add field {entityName} --field "{f.Name}:{f.CSharpType.TrimEnd('?')}{(f.IsNullable ? "?" : "")}"` on {DateTime.UtcNow:u}.
            --   Stamp: V{stamp}
            --
            --   NOTE: this adds the COLUMN only. dbo.{table}_Insert / dbo.{table}_Update do not take @{f.Name}
            --   until you re-create them in a follow-up migration (see the command's "NOT UPDATED" output).
            -- </auto-generated>

            IF COL_LENGTH(N'dbo.{table}', N'{f.Name}') IS NULL
            BEGIN
                ALTER TABLE dbo.[{table}] ADD [{f.Name}] {f.SqlType} {nullClause}{defaultClause};
            END;
            GO

            """;
    }
}
