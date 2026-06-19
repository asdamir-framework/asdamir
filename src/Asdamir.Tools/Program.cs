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
using Asdamir.Tools.Commands;

namespace Asdamir.Tools;

/// <summary>
/// Entry point for the `framework` dotnet tool.
///
/// Today: working commands — `framework new app`, `new entity`, `new page`,
/// `new module`, `add field`, `audit lint`, and `db apply`. The `new app` command
/// bootstraps a complete managed-app skeleton (Blazor Server + REST Gateway + tests +
/// sln + migrations including the full DB schema + AdminConsole onboarding script)
/// wired into the AdminConsole orchestration pattern. `db apply` creates the database
/// and runs those migrations against SQL Server.
///
/// Roadmap (per the framework audit plan):
///   framework new app      ← shipped
///   framework new mobile   ← shipped (MAUI Blazor Hybrid: Mobile + Shared + Data + tests)
///   framework new entity   ← shipped
///   framework new page     ← shipped
///   framework new module   ← shipped
///   framework add field    ← shipped
///   framework audit lint   ← shipped
///   framework db apply     ← shipped (create database + run *.sql migrations)
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var root = new RootCommand("framework — scaffolding and code generation for Asdamir")
        {
            BuildNewCommand(),
            BuildAddCommand(),
            BuildAuditCommand(),
            BuildDbCommand(),
            BuildAppCommand(),
            SecretsCommand.Build(),
        };

        return await root.InvokeAsync(args);
    }

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

    private static Command BuildAuditCommand()
    {
        var auditCmd = new Command("audit", "Static checks against the Asdamir audit pattern set.");
        auditCmd.AddCommand(AuditLintCommand.Build());
        return auditCmd;
    }

    private static Command BuildNewCommand()
    {
        var newCmd = new Command("new", "Create a new app / mobile / entity / page / module from a template.");
        newCmd.AddCommand(AppCommand.Build());
        newCmd.AddCommand(MobileCommand.Build());
        newCmd.AddCommand(EntityCommand.Build());
        newCmd.AddCommand(PageCommand.Build());
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
