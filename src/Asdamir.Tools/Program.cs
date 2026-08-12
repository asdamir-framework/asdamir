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
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using Asdamir.Tools.Commands;

namespace Asdamir.Tools;

/// <summary>
/// Entry point for the `framework` dotnet tool.
///
/// Today: working commands — `asdamir new app`, `new entity`, `new page`,
/// `new module`, `add field`, `audit lint`, and `db apply`. The `new app` command
/// bootstraps a complete managed-app skeleton (Blazor Server + REST Gateway + tests +
/// sln + migrations including the full DB schema + AdminConsole onboarding script)
/// wired into the AdminConsole orchestration pattern. `db apply` creates the database
/// and runs those migrations against SQL Server.
///
/// Roadmap (per the asdamir audit plan):
///   asdamir new app      ← shipped
///   asdamir new mobile   ← shipped (MAUI Blazor Hybrid: Mobile + Shared + Data + tests)
///   asdamir new entity   ← shipped
///   asdamir new page     ← shipped
///   asdamir new module   ← shipped
///   asdamir add field    ← shipped
///   asdamir audit lint   ← shipped
///   asdamir db apply     ← shipped (create database + run *.sql migrations)
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args) => await RunAsync(args);

    /// <summary>
    /// The real entry point, separated from <see cref="Main"/> so tests can drive the ACTUAL parse-and-invoke
    /// path rather than a helper that resembles it. The exit-code contract of <c>audit verify-archive</c> is
    /// only observable here — the cases it exists to catch are answered by the parser, before any handler runs,
    /// so a test that calls the handler directly can never see them.
    /// </summary>
    /// <param name="args">The raw command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    internal static async Task<int> RunAsync(string[] args)
    {
        var handlerReached = new HandlerReached();

        var root = new RootCommand("asdamir — scaffolding and code generation for Asdamir")
        {
            BuildNewCommand(),
            BuildAddCommand(),
            RollbackCommand.Build(),
            BuildAuditCommand(handlerReached),
            BuildDbCommand(),
            BuildLocalizationCommand(),
            BuildAppCommand(),
            SecretsCommand.Build(),
        };

        // ONE parser, built with UseDefaults(), used for the whole invocation.
        //
        // `root.Parse(args)` followed by `parseResult.InvokeAsync()` looks equivalent and is NOT: it runs
        // WITHOUT the default middleware, so parse errors stop short-circuiting the pipeline. Measured, after
        // making exactly that mistake: a mistyped command line ran the handler anyway and printed a real
        // result, an option given without its value reached the handler and crashed it unhandled (exit 134),
        // and — worst — `audit lint --bogus` stopped erroring and instead scanned 0 files and exited 0,
        // turning a build gate green. Keep the builder.
        //
        // THE LAST MIDDLEWARE IS THE EXIT-CODE CONTRACT (see ExitCodes): only an invocation that actually
        // reaches a command handler may keep the code that handler returned; everything else becomes Usage.
        //
        // TWO conditions, and BOTH were established by measurement rather than by reading the middleware
        // ordering docs — the first attempt assumed position alone was enough and was wrong:
        //
        //   * POSITION (MiddlewareOrder.Default, i.e. last) excludes `--help` and `--version`. Those are
        //     answered by earlier middleware that short-circuits, so this delegate never runs for them.
        //   * `Errors.Count == 0` excludes parse failures. Measured: for a typo'd flag, an omitted required
        //     option or a stray argument, System.CommandLine records the error and STILL runs the remaining
        //     middleware, applying its ParseErrorResult afterwards — so position alone let `--bogus` keep
        //     exit 1, which for a gate reads as "findings, build failed".
        //
        // Both conditions are POSITIVE — "the parser reported nothing wrong and we got as far as the handler
        // stage" — not a list of the parse errors that happen to exist today. That distinction is the whole
        // point: an enumerated list goes blind the moment the parser grows a new pre-handler outcome, which
        // is exactly how the previous version of this rule went blind.
        var parser = new CommandLineBuilder(root)
            .UseDefaults()
            .AddMiddleware(
                async (ctx, next) =>
                {
                    if (ctx.ParseResult.Errors.Count == 0)
                    {
                        handlerReached.Value = true;
                    }

                    try
                    {
                        await next(ctx);
                    }
                    catch (Exception ex)
                    {
                        // NO STACK TRACE. A stack trace prints absolute source paths, internal namespaces and
                        // dependency versions — noise for a user, and in an AUDIT tool a small disclosure a
                        // third party did not ask for. It also reads as "the product is broken" for what were,
                        // in both measured cases, simply mis-shaped arguments.
                        handlerReached.Value = false;

                        if (IsArgumentBindingFailure(ex))
                        {
                            // The parser accepted the token and the CONVERSION failed later, so ParseResult
                            // has no error to short-circuit on: `--path ""` typed as DirectoryInfo took this
                            // path and escaped as an unhandled exception. It is a usage error, and it is not
                            // reachable through the Errors.Count check above — measured, not assumed.
                            ctx.Console.Error.Write(ex.Message + Environment.NewLine);
                            ctx.ExitCode = ExitCodes.Usage;
                            return;
                        }

                        ctx.Console.Error.Write($"asdamir: {ex.GetType().Name}: {ex.Message}{Environment.NewLine}");
                        ctx.Console.Error.Write(
                            "This is an internal error, not a usage problem — please report it with the command line you ran."
                            + Environment.NewLine);
                        ctx.ExitCode = ExitCodes.Internal;
                    }
                },
                MiddlewareOrder.Default)
            .Build();

        var exitCode = await parser.InvokeAsync(args);

        return handlerReached.Value ? exitCode : exitCode is ExitCodes.Internal ? exitCode : ExitCodes.Usage;
    }

    /// <summary>
    /// True when the exception came from System.CommandLine converting a token to the option's declared type
    /// — i.e. the user's argument was the wrong shape, not a fault in the command.
    /// </summary>
    /// <remarks>
    /// Detected by where it was THROWN rather than by its type: <see cref="InvalidOperationException"/> is far
    /// too common to treat as "usage" on type alone, and mislabelling a real internal fault as the user's typo
    /// sends them to fix the wrong thing. The stack-frame check is narrow and, if the binder is ever
    /// restructured, fails toward <see cref="ExitCodes.Internal"/> — a wrong-but-loud answer rather than a
    /// wrong-and-reassuring one.
    /// </remarks>
    /// <param name="ex">The exception that escaped the invocation.</param>
    /// <returns>True when this is an argument-binding failure.</returns>
    private static bool IsArgumentBindingFailure(Exception ex)
        => ex is InvalidOperationException
           && (ex.StackTrace?.Contains("System.CommandLine.Binding", StringComparison.Ordinal) ?? false);

    private static Command BuildDbCommand()
    {
        var dbCmd = new Command("db", "Database operations — create the catalog and apply *.sql migrations against SQL Server.");
        dbCmd.AddCommand(DbApplyCommand.Build());
        return dbCmd;
    }

    private static Command BuildAppCommand()
    {
        var appCmd = new Command("app", "Manage apps registered in a company's AppManagement DB (via the running AdminConsole.Api).");
        appCmd.AddCommand(AppRegisterCommand.Build());
        return appCmd;
    }

    private static Command BuildAuditCommand(HandlerReached handlerReached)
    {
        var auditCmd = new Command("audit", "Static checks against the Asdamir audit pattern set.");
        auditCmd.AddCommand(AuditLintCommand.Build());
        auditCmd.AddCommand(LocalizationCheckCommand.Build());
        auditCmd.AddCommand(PermissionPolicyCheckCommand.Build());
        auditCmd.AddCommand(SeedFormCheckCommand.Build());
        auditCmd.AddCommand(VerifyArchiveCommand.Build(handlerReached));
        auditCmd.AddCommand(VerifySignaturesCommand.Build());
        return auditCmd;
    }

    private static Command BuildLocalizationCommand()
    {
        var locCmd = new Command("localization", "Localization-completeness tooling — verify seeded keys against the live vault.");
        locCmd.AddCommand(LocalizationVerifyCommand.Build());
        return locCmd;
    }

    private static Command BuildNewCommand()
    {
        var newCmd = new Command("new", "Create a new app / mobile / entity / page / module from a template.");
        newCmd.AddCommand(AppCommand.Build());
        newCmd.AddCommand(MobileCommand.Build());
        newCmd.AddCommand(EntityCommand.Build());
        newCmd.AddCommand(PageCommand.Build());
        newCmd.AddCommand(FeatureCommand.Build());
        newCmd.AddCommand(ModuleCommand.Build());
        return newCmd;
    }

    private static Command BuildAddCommand()
    {
        var addCmd = new Command("add", "Patch an existing scaffold — e.g. append a field to an entity slice.");
        addCmd.AddCommand(AddFieldCommand.Build());
        return addCmd;
    }
}
