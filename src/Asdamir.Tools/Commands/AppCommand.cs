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
using System.CommandLine.Invocation;

namespace Asdamir.Tools.Commands;

/// <summary>
/// <c>asdamir new app [Name] [--server-name N] [--api-name N] [--database D] [--db-server S]
///                      [--connection-string CS] [--gateway-url U] [--output dir] [--namespace ns]
///                      [--local-feed dir] [--yes]</c>
///
/// Generates a STANDALONE app that runs on its own but consumes the framework's features
/// (error handling, auth/JWT validation, localization, UI) from the Asdamir.* packages via DI —
/// it does NOT re-implement them and does NOT copy the management schema into its own database.
///
/// Identity &amp; management (apps / users / roles / permissions / menus) live in AsdamirVault and
/// are administered from the AppManagement control plane; this app only VALIDATES the JWT that
/// AppManagement issued. Its OWN database holds only its business data (starts with one demo table;
/// grow it with `asdamir new entity`).
///
///   &lt;Name&gt;/
///   ├── &lt;Name&gt;.sln, Directory.Packages.props, Directory.Build.props, nuget.config, .gitignore, README.md
///   ├── src/&lt;ServerProject&gt;/   Blazor Web App — cookie session; login delegates to AppManagement
///   ├── src/&lt;GatewayProject&gt;/  REST API — AddGlobalExceptionHandling + JWT validation + Health
///   ├── tests/                    bUnit + WebApplicationFactory smoke tests
///   └── db/migrations/            demo schema/seed only (V*__schema.sql = DemoItems)
///
/// When args are omitted and the console is interactive, the core inputs are prompted; `--yes`
/// (or redirected stdin) accepts defaults silently (CI).
/// </summary>
public static class AppCommand
{
    public static Command Build()
    {
        var nameArg = new Argument<string>("name", () => "",
            "PascalCase application name (e.g. GeneratedApp). Solution name + project-name prefix. Prompted when omitted.")
        { Arity = ArgumentArity.ZeroOrOne };

        var outputOpt = new Option<DirectoryInfo>(new[] { "--output", "-o" },
            description: "Parent directory under which '<Name>/' will be created. Defaults to the current directory.",
            getDefaultValue: () => new DirectoryInfo(Directory.GetCurrentDirectory()));
        var namespaceOpt = new Option<string>(new[] { "--namespace", "-n" },
            description: "Root namespace. Defaults to the app name.", getDefaultValue: () => "");
        var localFeedOpt = new Option<string>(new[] { "--local-feed" },
            description: "Absolute path to a local NuGet feed for locally-packed Asdamir.* packages.", getDefaultValue: () => "");
        var serverNameOpt = new Option<string>(new[] { "--server-name" },
            description: "Blazor (Server) project name. Defaults to '<Name>.Server'. Prompted when omitted.", getDefaultValue: () => "");
        var apiNameOpt = new Option<string>(new[] { "--api-name" },
            description: "REST API (Gateway) project name. Defaults to '<Name>.Gateway'. Prompted when omitted.", getDefaultValue: () => "");
        var databaseOpt = new Option<string>(new[] { "--database" },
            description: "SQL Server database name for this app's own (business) data. Defaults to the app name.", getDefaultValue: () => "");
        var dbServerOpt = new Option<string>(new[] { "--db-server" },
            description: "SQL Server host (for the default connection string). Defaults to 'localhost'.", getDefaultValue: () => "");
        var connStringOpt = new Option<string>(new[] { "--connection-string" },
            description: "Full connection string override. A password-bearing string is NOT written to appsettings.json — a user-secrets command is printed instead.", getDefaultValue: () => "");
        var gatewayUrlOpt = new Option<string>(new[] { "--gateway-url" },
            description: "Base URL the Server uses to reach this app's Gateway. Defaults to 'https://localhost:7001/'.", getDefaultValue: () => "");
        var adminEmailOpt = new Option<string>(new[] { "--admin-email" },
            description: "Email of the app's starter admin (seeded into AsdamirVault scoped by AppId). Defaults to admin@<name>.local.", getDefaultValue: () => "");
        var adminPasswordOpt = new Option<string>(new[] { "--admin-password" },
            description: "Password for the starter admin (only its PBKDF2 hash is seeded; the password is printed once). Defaults to a generated one.", getDefaultValue: () => "");
        var yesOpt = new Option<bool>(new[] { "--yes", "-y" },
            description: "Non-interactive: accept every default without prompting (CI).", getDefaultValue: () => false);
        var modeOpt = new Option<string>(new[] { "--mode" },
            description: "'commercial' (default) — identity/menus/permissions/localization/config live CENTRALLY in AsdamirVault and are managed from AppManagement. 'free' — self-contained: those management tables are emitted into the app's OWN database (single-tenant), so the app needs no AppManagement.",
            getDefaultValue: () => "commercial");
        var billingOpt = new Option<bool>(new[] { "--billing" },
            description: "Opt-in: scaffold an end-user billing/payment page. Adds a Payment page (Server) plus the billing menu/permission/localization seeds. In commercial mode (Model A) the Gateway proxies gateway/billing/* → AppManagement and the billing data + Paddle secret live centrally in AsdamirVault; in free mode (Model B) the Gateway serves billing LOCALLY from the app's OWN DB via the Asdamir.Payments package (LocalDbBillingStore + payment rails + webhook). Default off: without it the app is unchanged.",
            getDefaultValue: () => false);

        var appCmd = new Command("app", "Generate a standalone Asdamir app (Server + Gateway + tests + sln + demo DB) that consumes the framework via DI and is managed from AppManagement.")
        {
            nameArg, outputOpt, namespaceOpt, localFeedOpt,
            serverNameOpt, apiNameOpt, databaseOpt, dbServerOpt, connStringOpt, gatewayUrlOpt,
            adminEmailOpt, adminPasswordOpt, yesOpt, modeOpt, billingOpt,
        };

        appCmd.SetHandler((InvocationContext ctx) =>
        {
            var r = ctx.ParseResult;
            Run(new RawInputs(
                Name: r.GetValueForArgument(nameArg),
                Output: r.GetValueForOption(outputOpt)!,
                NsOverride: r.GetValueForOption(namespaceOpt) ?? "",
                LocalFeed: r.GetValueForOption(localFeedOpt) ?? "",
                ServerName: r.GetValueForOption(serverNameOpt) ?? "",
                ApiName: r.GetValueForOption(apiNameOpt) ?? "",
                Database: r.GetValueForOption(databaseOpt) ?? "",
                DbServer: r.GetValueForOption(dbServerOpt) ?? "",
                ConnString: r.GetValueForOption(connStringOpt) ?? "",
                GatewayUrl: r.GetValueForOption(gatewayUrlOpt) ?? "",
                AdminEmail: r.GetValueForOption(adminEmailOpt) ?? "",
                AdminPassword: r.GetValueForOption(adminPasswordOpt) ?? "",
                Yes: r.GetValueForOption(yesOpt),
                Mode: r.GetValueForOption(modeOpt) ?? "commercial",
                Billing: r.GetValueForOption(billingOpt)));
        });
        return appCmd;
    }

    private sealed record RawInputs(
        string Name, DirectoryInfo Output, string NsOverride, string LocalFeed,
        string ServerName, string ApiName, string Database, string DbServer,
        string ConnString, string GatewayUrl, string AdminEmail, string AdminPassword, bool Yes,
        string Mode, bool Billing);

    private static void Run(RawInputs raw)
    {
        var interactive = !raw.Yes && !Console.IsInputRedirected;

        // Tier mode: 'commercial' (default, unchanged behaviour) vs 'free' (self-contained — emit the
        // management schema into the app's own DB). Any other value is an error.
        var isFreeMode = string.Equals(raw.Mode, "free", StringComparison.OrdinalIgnoreCase);
        if (!isFreeMode && !string.Equals(raw.Mode, "commercial", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("--mode must be 'free' or 'commercial'.");
            Environment.Exit(2);
            return;
        }

        // Billing is opt-in (--billing) and works in BOTH modes now:
        //   • Model A (commercial + billing): the Gateway PROXIES gateway/billing/* → AppManagement; the
        //     billing data + the Paddle secret live centrally in AsdamirVault, AppId-scoped.
        //   • Model B (free + billing): self-contained — the Gateway serves gateway/billing/* LOCALLY from
        //     the app's OWN DB via Asdamir.Payments (LocalDbBillingStore + the payment rails). No control plane.
        var hasBilling = raw.Billing;
        // Only free + billing needs the Asdamir.Payments package + the app-local store/webhook + billing
        // migrations; Model A billing is a thin proxy that needs none of that. Precomputed here so the
        // Gateway program/csproj templates can branch on a single boolean (the templater has no `&&` in
        // its model, only in conditions — but keeping the flag explicit avoids leaking Model-A billing into
        // the Gateway wiring, which must stay byte-identical to R3).
        var hasLocalBilling = hasBilling && isFreeMode;

        var name = raw.Name;
        if (string.IsNullOrWhiteSpace(name))
            name = Ask(interactive, "Uygulama adı / App name", "GeneratedApp");
        if (string.IsNullOrWhiteSpace(name) || !char.IsUpper(name[0]))
        {
            Console.Error.WriteLine("App name must be PascalCase (e.g. GeneratedApp).");
            Environment.Exit(2);
            return;
        }

        var serverProject = FirstNonEmpty(raw.ServerName, () => Ask(interactive, "UI (Server) proje adı / project name", $"{name}.Server"));
        var gatewayProject = FirstNonEmpty(raw.ApiName, () => Ask(interactive, "REST API (Gateway) proje adı / project name", $"{name}.Gateway"));
        var database = FirstNonEmpty(raw.Database, () => Ask(interactive, "Veritabanı adı / Database name", name));
        var dbServer = FirstNonEmpty(raw.DbServer, () => Ask(interactive, "SQL Server", "localhost"));

        var connString = raw.ConnString;
        if (string.IsNullOrWhiteSpace(connString) && interactive)
            connString = Ask(true, "Connection string", ComposeConnString(dbServer, database));
        if (string.IsNullOrWhiteSpace(connString))
            connString = ComposeConnString(dbServer, database);

        var connHasSecret = connString.Contains("password=", StringComparison.OrdinalIgnoreCase)
                            || connString.Contains("pwd=", StringComparison.OrdinalIgnoreCase);
        var connIsWindowsAuth = connString.Contains("Trusted_Connection", StringComparison.OrdinalIgnoreCase)
                                || connString.Contains("Integrated Security", StringComparison.OrdinalIgnoreCase);
        // appsettings.json holds ONLY a portable, secret-free connection string. A Windows-auth
        // (Trusted_Connection) string isn't portable to Linux/macOS/containers, and a password is a
        // secret — both are left empty here and set via user-secrets / env (printed in next steps).
        var connForAppsettings = (connHasSecret || connIsWindowsAuth) ? "" : connString;
        var connNeedsSecret = string.IsNullOrEmpty(connForAppsettings);
        // Cross-platform (SQL auth) example for the secret command; the user supplies the password.
        var connSecretExample = connHasSecret
            ? connString
            : $"Server={dbServer},1433;Database={database};User Id=sa;Password=<your-password>;TrustServerCertificate=True;";

        var gatewayUrl = FirstNonEmpty(raw.GatewayUrl, () => Ask(interactive, "Gateway BaseUrl", "https://localhost:7001/"));
        if (!gatewayUrl.EndsWith('/')) gatewayUrl += "/";

        var ns = string.IsNullOrWhiteSpace(raw.NsOverride) ? name : raw.NsOverride;
        var appRoot = Path.Combine(raw.Output.FullName, name);
        if (Directory.Exists(appRoot) && Directory.EnumerateFileSystemEntries(appRoot).Any())
        {
            Console.Error.WriteLine($"Refusing to write into non-empty directory '{appRoot}'. Remove it first or choose a different --output.");
            Environment.Exit(3);
            return;
        }

        // Starter admin — seeded into AsdamirVault (scoped by this app's AppId) by the generated
        // register_<app>.sql. Only the PBKDF2 hash is stored; the password is printed once below.
        var adminEmail = FirstNonEmpty(raw.AdminEmail, () => $"admin@{name.ToLowerInvariant()}.local");
        var adminPassword = FirstNonEmpty(raw.AdminPassword, SeedPasswordHasher.GeneratePassword);
        var adminPasswordHash = SeedPasswordHasher.Hash(adminPassword);

        var now = DateTime.UtcNow;
        var model = new
        {
            AppName = name,
            AdminEmail = adminEmail,
            AdminPasswordHash = adminPasswordHash,
            AppNameLower = name.ToLowerInvariant(),
            AppNameUpper = name.ToUpperInvariant(),
            // Short badge for the sidebar/login logo (1 letter) — the full name overflows the badge.
            AppInitial = name[..1].ToUpperInvariant(),
            ServerProject = serverProject,
            GatewayProject = gatewayProject,
            ServerTestsProject = $"{serverProject}.Tests",
            GatewayTestsProject = $"{gatewayProject}.Tests",
            Namespace = ns,
            ServerNamespace = serverProject,
            GatewayNamespace = gatewayProject,
            // Free mode → the Gateway issues JWTs + reads identity/menu/localization from its OWN DB
            // (local controllers); commercial → proxies to AppManagement. Templates branch on this.
            IsFreeMode = isFreeMode,
            // Opt-in end-user billing. Templates + the conditional emit below branch on these; both false
            // leaves the app byte-identical to a non-billing scaffold.
            //   HasBilling      — any billing (Model A proxy OR Model B local): emits the Payment page.
            //   HasLocalBilling — free + billing (Model B) ONLY: the Gateway needs the Asdamir.Payments
            //                     package + AddPayments + LocalDbBillingStore. Commercial billing (Model A)
            //                     leaves the Gateway program/csproj byte-identical to R3 (it's a proxy).
            HasBilling = hasBilling,
            HasLocalBilling = hasLocalBilling,
            GeneratedAtUtc = now.ToString("u"),
            SchemaStamp = now.ToString("yyyyMMddHHmmss"),
            MigrationStamp = now.AddSeconds(1).ToString("yyyyMMddHHmmss"),
            SeedStamp = now.AddSeconds(2).ToString("yyyyMMddHHmmss"),
            // Free-mode management schema applies before the business schema (earliest stamp); its
            // stored procs apply right after the tables exist (-4s), then the management seed (-3s) —
            // all still before the business schema.
            FreeModeSchemaStamp = now.AddSeconds(-5).ToString("yyyyMMddHHmmss"),
            FreeModeProcsStamp = now.AddSeconds(-4).ToString("yyyyMMddHHmmss"),
            FreeModeSeedStamp = now.AddSeconds(-3).ToString("yyyyMMddHHmmss"),
            // Free-mode (Model B) billing migrations — self-contained billing tables/procs/seed in THIS app's
            // OWN DB. Placed AFTER the business schema (+3/+4/+5): billing is independent of the demo/business
            // tables, and the free-management schema/procs it depends on (dbo.Menus/Permissions +
            // LocalizationResource_UpsertValue) are already applied at -5/-4. Schema → procs → seed order.
            FreeModeBillingSchemaStamp = now.AddSeconds(3).ToString("yyyyMMddHHmmss"),
            FreeModeBillingProcsStamp = now.AddSeconds(4).ToString("yyyyMMddHHmmss"),
            FreeModeBillingSeedStamp = now.AddSeconds(5).ToString("yyyyMMddHHmmss"),
            LocalFeedPath = string.IsNullOrWhiteSpace(raw.LocalFeed) ? "" : raw.LocalFeed.Replace('\\', '/'),
            HasLocalFeed = !string.IsNullOrWhiteSpace(raw.LocalFeed),
            DatabaseName = database,
            ConnectionStringForAppsettings = connForAppsettings,
            GatewayBaseUrl = gatewayUrl,
            // launchSettings.json applicationUrl values — fixed ports so `dotnet run` never falls back to
            // the ASP.NET default :5000 (which collides with macOS AirPlay Receiver). The Gateway port
            // MUST equal Gateway:BaseUrl (the Server calls it there); the slash is trimmed for Kestrel.
            GatewayUrlNoSlash = gatewayUrl.TrimEnd('/'),
            ServerUrl = "https://localhost:7010",
        };

        // (relative target path, template resource name)
        var outputs = new[]
        {
            // Root
            ($"{name}.sln",                                               "Solution"),
            ("Directory.Packages.props",                                  "DirectoryPackages"),
            ("Directory.Build.props",                                     "DirectoryBuild"),
            ("nuget.config",                                              "NugetConfig"),
            // Clean test output: `./run-tests.sh` runs the suite and prints one PASS/FAIL line per test
            // (parses the TRX), with zero build/host/xUnit noise. See the script header.
            ("run-tests.sh",                                              "RunTestsSh"),
            // Per-app stop+start of both tiers (Gateway + Server). Restart after a config/secret/migration
            // /localization-seed change — a running host caches DB-backed localization + config at startup.
            ($"restart-{model.AppNameLower}.sh",                          "RestartSh"),
            (".gitignore",                                                "GitIgnore"),
            // Free apps get a self-contained README (no control plane); commercial gets the proxy one.
            ("README.md",                                                 isFreeMode ? "FreeAppReadme" : "AppReadme"),

            // Server (consumes framework UI/auth/localization via DI)
            ($"src/{serverProject}/{serverProject}.csproj",               "ServerCsproj"),
            ($"src/{serverProject}/Program.cs",                           "ServerProgram"),
            // Re-applies the picked culture on the interactive circuit so L["..."] localizes on
            // @rendermode InteractiveServer pages too (SSR-only UseRequestLocalization isn't enough).
            ($"src/{serverProject}/CultureCircuitHandler.cs",             "ServerCultureCircuitHandler"),
            ($"src/{serverProject}/Components/App.razor",                 "ServerAppRazor"),
            ($"src/{serverProject}/Components/Routes.razor",              "ServerRoutesRazor"),
            ($"src/{serverProject}/Components/_Imports.razor",            "ServerImportsRazor"),
            ($"src/{serverProject}/Components/Layout/MainLayout.razor",   "ServerMainLayout"),
            ($"src/{serverProject}/Components/Layout/MainLayout.razor.css", "ServerMainLayoutCss"),
            ($"src/{serverProject}/Components/Layout/AppTheme.razor",     "ServerAppTheme"),
            ($"src/{serverProject}/Components/Layout/DarkModeToggle.razor", "ServerDarkModeToggle"),
            ($"src/{serverProject}/Components/Layout/DarkModeToggle.razor.css", "ServerDarkModeToggleCss"),
            ($"src/{serverProject}/Components/Layout/ThemeSelector.razor", "ServerThemeSelector"),
            ($"src/{serverProject}/Components/Layout/ThemeSelector.razor.css", "ServerThemeSelectorCss"),
            ($"src/{serverProject}/Components/Layout/LanguageSelector.razor", "ServerLanguageSelector"),
            ($"src/{serverProject}/Components/Layout/LanguageSelector.razor.css", "ServerLanguageSelectorCss"),
            ($"src/{serverProject}/Components/Layout/EmptyLayout.razor",  "ServerEmptyLayout"),
            ($"src/{serverProject}/Components/Layout/EmptyLayout.razor.css", "ServerEmptyLayoutCss"),
            ($"src/{serverProject}/Components/Layout/SessionTimeout.razor","ServerSessionTimeout"),
            ($"src/{serverProject}/Components/Layout/NavMenu.razor",      "ServerNavMenu"),
            ($"src/{serverProject}/Components/Pages/Home.razor",          "ServerHomePage"),
            ($"src/{serverProject}/Components/Pages/Home.razor.css",      "ServerHomePageCss"),
            ($"src/{serverProject}/Components/Pages/Login.razor",         "ServerLoginPage"),
            ($"src/{serverProject}/Components/Pages/Login.razor.css",     "ServerLoginPageCss"),
            ($"src/{serverProject}/Components/Pages/ForgotPassword.razor",     "ServerForgotPasswordPage"),
            ($"src/{serverProject}/Components/Pages/ForgotPassword.razor.css", "ServerForgotPasswordPageCss"),
            ($"src/{serverProject}/Components/Pages/AccessDenied.razor",  "ServerAccessDeniedPage"),
            ($"src/{serverProject}/Auth/AuthEndpoints.cs",                "ServerAuthEndpoints"),
            ($"src/{serverProject}/Auth/ThemeEndpoints.cs",               "ServerThemeEndpoints"),
            ($"src/{serverProject}/Services/LocalizationWarmupService.cs", "ServerLocalizationWarmup"),
            ($"src/{serverProject}/appsettings.json",                     "ServerAppsettings"),
            ($"src/{serverProject}/Properties/launchSettings.json",       "ServerLaunchSettings"),
            ($"src/{serverProject}/wwwroot/app.css",                      "ServerAppCss"),

            // Gateway (framework error handling + JWT validation + health)
            ($"src/{gatewayProject}/{gatewayProject}.csproj",             "GatewayCsproj"),
            ($"src/{gatewayProject}/Program.cs",                          "GatewayProgram"),
            ($"src/{gatewayProject}/Controllers/HealthController.cs",     "GatewayHealthController"),
            // Free mode → local controllers that read the app's OWN DB + issue JWTs; commercial → proxies
            // to AppManagement. Same target filenames, different templates.
            ($"src/{gatewayProject}/Controllers/AuthController.cs",       isFreeMode ? "FreeGatewayAuthController" : "GatewayAuthController"),
            ($"src/{gatewayProject}/Controllers/MenuController.cs",       isFreeMode ? "FreeGatewayMenuController" : "GatewayMenuController"),
            ($"src/{gatewayProject}/Controllers/LocalizationController.cs", isFreeMode ? "FreeGatewayLocalizationController" : "GatewayLocalizationController"),
            ($"src/{gatewayProject}/Controllers/DemoItemsController.cs",   "GatewayDemoItemsController"),
            ($"src/{gatewayProject}/appsettings.json",                   "GatewayAppsettings"),
            ($"src/{gatewayProject}/Properties/launchSettings.json",     "GatewayLaunchSettings"),

            // Tests
            ($"tests/{serverProject}.Tests/{serverProject}.Tests.csproj", "ServerTestsCsproj"),
            ($"tests/{serverProject}.Tests/HomePageRenderTests.cs",       "ServerTestsHome"),
            ($"tests/{serverProject}.Tests/Usings.cs",                    "ServerTestsUsings"),
            ($"tests/{gatewayProject}.Tests/{gatewayProject}.Tests.csproj","GatewayTestsCsproj"),
            ($"tests/{gatewayProject}.Tests/SmokeFactory.cs",             "GatewayTestsSmokeFactory"),
            ($"tests/{gatewayProject}.Tests/HealthEndpointTests.cs",      "GatewayTestsHealth"),
            ($"tests/{gatewayProject}.Tests/Usings.cs",                   "GatewayTestsUsings"),

            // Migrations (rendered placeholder; schema/seed are static assets below)
            ($"db/migrations/V{model.MigrationStamp}__bootstrap.sql",     "AppMigrationBootstrap"),
            // NOTE: the AppManagement onboarding script (register_<app>.sql) is emitted below, COMMERCIAL
            // ONLY — a free app is self-contained and has no control plane to register against.
        };

        var written = 0;
        foreach (var (relPath, templateName) in outputs)
        {
            var target = Path.Combine(appRoot, relPath);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (File.Exists(target)) { Console.WriteLine($"  SKIP (exists): {relPath}"); continue; }
            File.WriteAllText(target, TemplateRenderer.Render(templateName, model));
            // Shell scripts (run-tests.sh) need the executable bit so `./run-tests.sh` works out of the box.
            if (!OperatingSystem.IsWindows() && relPath.EndsWith(".sh", StringComparison.Ordinal))
                File.SetUnixFileMode(target, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                    | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            Console.WriteLine($"  WROTE: {relPath}");
            written++;
        }

        // Free-mode ONLY: the management schema (identity / RBAC / menu / localization / config) into
        // THIS app's OWN database — single-tenant, no AppId. Commercial mode omits it (that data lives
        // centrally in AsdamirVault, managed from AppManagement).
        if (isFreeMode)
        {
            written += WriteAsset(appRoot, $"db/migrations/V{model.FreeModeSchemaStamp}__freemode_management_schema.sql", "FreeModeManagementSchema.sql");
            // Companion procs (login / RBAC / menu / localization / config) — AppId-free, single-tenant.
            written += WriteAsset(appRoot, $"db/migrations/V{model.FreeModeProcsStamp}__freemode_management_procs.sql", "FreeModeManagementProcs.sql");
            // Management seed (admin user + Admin role + grants + Dashboard menu + config + localization)
            // into THIS app's OWN DB — rendered (placeholders), so it goes through Render, not WriteAsset.
            written += WriteRendered(appRoot, $"db/migrations/V{model.FreeModeSeedStamp}__freemode_management_seed.sql", "FreeModeManagementSeed", model);
            // Local identity data access (login/RBAC procs) — auto-registered by the Gateway's
            // DI-by-convention scan (IFreeIdentityRepository -> FreeIdentityRepository).
            written += WriteRendered(appRoot, $"src/{gatewayProject}/Auth/FreeIdentityRepository.cs", "FreeGatewayIdentityRepository", model);
            // Local menu / localization / config reads (auto-registered) + the client-settings controller
            // that the commercial Gateway never served (a gap) — all reading the app's OWN DB.
            written += WriteRendered(appRoot, $"src/{gatewayProject}/Auth/FreeManagementRepository.cs", "FreeGatewayManagementRepository", model);
            // ILocalizationService over the app's OWN DB — GlobalExceptionHandling needs it to LOCALIZE error
            // messages (error.<code>); without it every error falls back to the generic English string.
            written += WriteRendered(appRoot, $"src/{gatewayProject}/Auth/FreeLocalizationService.cs", "FreeGatewayLocalizationService", model);
            written += WriteRendered(appRoot, $"src/{gatewayProject}/Controllers/ClientSettingsController.cs", "FreeGatewayClientSettingsController", model);
            // First-login / forced change-password page (self-contained auth). Reached via the login redirect
            // when the user's ForcePasswordChange flag is set; posts to the Gateway's change-password endpoint.
            // Commercial mode has no local change-password flow (identity is central), so this is free-only.
            written += WriteRendered(appRoot, $"src/{serverProject}/Components/Pages/ChangePassword.razor", "ServerChangePasswordPage", model);
            written += WriteRendered(appRoot, $"src/{serverProject}/Components/Pages/ChangePassword.razor.css", "ServerChangePasswordPageCss", model);
        }
        else
        {
            // Commercial ONLY: the AppManagement onboarding script (run against the AsdamirVault DB, not
            // this app's) — registers the app + seeds its central AppId-scoped management data. A free app
            // has no control plane, so this is not emitted there.
            written += WriteRendered(appRoot, $"db/admin-onboarding/register_{model.AppNameLower}.sql", "AppAdminOnboarding", model);
        }

        // Opt-in billing (--billing). Emitted ONLY when --billing is passed; without it not a single billing
        // file is written, so a non-billing scaffold is byte-identical to before this feature.
        if (hasBilling)
        {
            // Shared BOTH modes — the end-user Payment page (+ scoped CSS). Its gateway/billing/* calls are
            // served by the local Gateway in free mode and proxied to AppManagement in commercial mode; the
            // page (and its template) is identical either way.
            written += WriteRendered(appRoot, $"src/{serverProject}/Components/Pages/Payment.razor", "ServerPaymentPage", model);
            written += WriteRendered(appRoot, $"src/{serverProject}/Components/Pages/Payment.razor.css", "ServerPaymentPageCss", model);

            if (isFreeMode)
            {
                // Model B (free + billing): self-contained LOCAL billing REST at the SAME gateway/billing/*
                // routes the page calls, backed by Asdamir.Payments (LocalDbBillingStore + IPaymentService) over
                // THIS app's OWN DB — no proxy, no AppManagement. Plus the app-local webhook sink, and the
                // single-tenant billing tables + operational procs + permission/menu/localization seed as
                // journaled migrations (so `db apply` sets it all up).
                written += WriteRendered(appRoot, $"src/{gatewayProject}/Controllers/BillingController.cs", "FreeGatewayBillingController", model);
                written += WriteRendered(appRoot, $"src/{gatewayProject}/Controllers/BillingWebhookController.cs", "FreeGatewayBillingWebhookController", model);
                written += WriteAsset(appRoot, $"db/migrations/V{model.FreeModeBillingSchemaStamp}__freemode_billing_schema.sql", "FreeModeBillingSchema.sql");
                written += WriteAsset(appRoot, $"db/migrations/V{model.FreeModeBillingProcsStamp}__freemode_billing_procs.sql", "FreeModeBillingProcs.sql");
                written += WriteAsset(appRoot, $"db/migrations/V{model.FreeModeBillingSeedStamp}__freemode_billing_seed.sql", "FreeModeBillingSeed.sql");
            }
            else
            {
                // Model A (commercial + billing): the Gateway PROXIES gateway/billing/* → AppManagement (no DB,
                // no Paddle secret here); the seed adds the billing menu/permission + localization + Paddle
                // config templates into AsdamirVault (AppId-scoped). BYTE-IDENTICAL to the R3 output.
                written += WriteRendered(appRoot, $"src/{gatewayProject}/Controllers/BillingController.cs", "GatewayBillingController", model);
                written += WriteRendered(appRoot, $"db/admin-onboarding/seed_billing.sql", "BillingSeed", model);
            }
        }

        // Demo-only schema + seed (this app's OWN business DB — no management tables/data).
        written += WriteAsset(appRoot, $"db/migrations/V{model.SchemaStamp}__schema.sql", "DbSchema.sql");
        written += WriteAsset(appRoot, $"db/migrations/V{model.SeedStamp}__seed.sql", "DbSeed.sql");

        Console.WriteLine();
        Console.WriteLine($"Done. {written} files written to '{appRoot}'.");
        Console.WriteLine();
        if (isFreeMode)
        {
            // Free mode — self-contained app. The starter admin + menu + localization + config are seeded
            // into THIS app's OWN database by the free-mode migrations (applied by `db apply`); there is no
            // control plane to register against, and the Gateway issues + validates its own JWTs.
            Console.WriteLine("  ┌─────────────────────────────────────────────────────────────────────────┐");
            Console.WriteLine("  │  STARTER ADMIN — seeded into THIS app's OWN database, applied");
            Console.WriteLine("  │  automatically by `asdamir db apply`. Change it after first sign-in.");
            Console.WriteLine($"  │    Email:    {adminEmail,-58}│");
            Console.WriteLine($"  │    Password: {adminPassword,-58}│");
            Console.WriteLine("  └─────────────────────────────────────────────────────────────────────────┘");
            Console.WriteLine();
            Console.WriteLine("Next steps (free mode — self-contained; no control plane):");
            Console.WriteLine($"  1. cd {name}");
            Console.WriteLine($"  2. Dev secrets (NEVER in appsettings.json):");
            Console.WriteLine($"     cd src/{gatewayProject}");
            Console.WriteLine($"     # the Gateway ISSUES + validates its own JWTs — use any 64+ byte CSPRNG-generated key");
            Console.WriteLine($"     dotnet user-secrets set \"Jwt:Key\" \"<a 64+ byte random key>\"");
            Console.WriteLine($"     # at-rest encryption key (32+ chars) — REQUIRED (the Gateway fails closed without it, no demo default)");
            Console.WriteLine($"     dotnet user-secrets set \"Security:EncryptionKey\" \"<a 32+ char random key>\"");
            if (connNeedsSecret)
            {
                Console.WriteLine($"     # this app's OWN database (management + business tables) — cross-platform (SQL auth). On Windows you may use Trusted_Connection=True instead.");
                Console.WriteLine($"     dotnet user-secrets set \"ConnectionStrings:Default\" \"{connSecretExample}\"");
            }
            Console.WriteLine($"     cd ../..");
            Console.WriteLine($"  3. dotnet build {name}.sln && dotnet test {name}.sln");
            Console.WriteLine($"  4. Create the app's own database + apply ALL migrations (journaled — the management");
            Console.WriteLine($"     schema/procs/seed AND the business schema in one pass; this seeds the starter admin +");
            Console.WriteLine($"     menu + localization + config into THIS app's OWN database):");
            Console.WriteLine($"     asdamir db apply --create-database --migrations db/migrations");
            Console.WriteLine($"     # reads ConnectionStrings:Default from the Gateway user-secret you set in step 2 — no");
            Console.WriteLine($"     # password on the command line. (You can still pass --connection / --server / --user / --password.)");
            Console.WriteLine($"  5. Run both tiers, then sign in with the starter admin above:");
            Console.WriteLine($"     dotnet run --project src/{gatewayProject}   # {gatewayUrl}");
            Console.WriteLine($"     dotnet run --project src/{serverProject}");
            Console.WriteLine($"  6. Add your first real table/page: cd src/{gatewayProject} && asdamir new entity <Name> --fields \"...\"");
        }
        else
        {
        Console.WriteLine("  ┌─────────────────────────────────────────────────────────────────────────┐");
        Console.WriteLine("  │  STARTER ADMIN — seeded into AsdamirVault (scoped by this app's AppId)     │");
        Console.WriteLine("  │  when you run register_" + model.AppNameLower + ".sql. Change it after first sign-in.");
        Console.WriteLine($"  │    Email:    {adminEmail,-58}│");
        Console.WriteLine($"  │    Password: {adminPassword,-58}│");
        Console.WriteLine("  └─────────────────────────────────────────────────────────────────────────┘");
        Console.WriteLine();
        Console.WriteLine("Next steps:");
        Console.WriteLine($"  1. cd {name}");
        Console.WriteLine($"  2. Dev secrets (NEVER in appsettings.json):");
        Console.WriteLine($"     cd src/{gatewayProject}");
        Console.WriteLine($"     dotnet user-secrets set \"Jwt:Key\" \"<the SAME 64+ byte key AppManagement signs with>\"");
        if (connNeedsSecret)
        {
            Console.WriteLine($"     # this app's OWN (business) DB — cross-platform (SQL auth). On Windows you may use Trusted_Connection=True instead.");
            Console.WriteLine($"     dotnet user-secrets set \"ConnectionStrings:Default\" \"{connSecretExample}\"");
        }
        Console.WriteLine($"     cd ../..");
        Console.WriteLine($"  3. dotnet build {name}.sln && dotnet test {name}.sln");
        Console.WriteLine($"  4. Create the app's own (business) database + demo table (migrations are journaled — re-runs apply only new ones):");
        Console.WriteLine($"     asdamir db apply --create-database --migrations db/migrations");
        Console.WriteLine($"     # reads ConnectionStrings:Default from the Gateway user-secret you set in step 2 — no");
        Console.WriteLine($"     # password on the command line. (You can still pass --connection / --server / --user / --password.)");
        Console.WriteLine($"  5. Register + seed the app in AppManagement (control plane): run");
        Console.WriteLine($"     db/admin-onboarding/register_{model.AppNameLower}.sql against the AsdamirVault DB —");
        Console.WriteLine($"     it registers the app and seeds its users / roles / permissions / menus / config /");
        Console.WriteLine($"     localization there, scoped by AppId (this app reads them via AppManagement's API).");
        Console.WriteLine($"  6. Run both tiers:");
        Console.WriteLine($"     dotnet run --project src/{gatewayProject}   # {gatewayUrl}");
        Console.WriteLine($"     dotnet run --project src/{serverProject}");
        Console.WriteLine($"  7. Add your first real table/page: cd src/{gatewayProject} && asdamir new entity <Name> --fields \"...\"");
        }
    }

    private static int WriteAsset(string appRoot, string relPath, string assetName)
    {
        var target = Path.Combine(appRoot, relPath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (File.Exists(target)) { Console.WriteLine($"  SKIP (exists): {relPath}"); return 0; }
        File.WriteAllText(target, TemplateRenderer.ReadAsset(assetName));
        Console.WriteLine($"  WROTE: {relPath}");
        return 1;
    }

    // Like WriteAsset, but renders a .sbn template against the model first (for assets that carry
    // placeholders — e.g. the free-mode seed needs AppName / AdminEmail / AdminPasswordHash).
    private static int WriteRendered(string appRoot, string relPath, string templateName, object model)
    {
        var target = Path.Combine(appRoot, relPath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (File.Exists(target)) { Console.WriteLine($"  SKIP (exists): {relPath}"); return 0; }
        File.WriteAllText(target, TemplateRenderer.Render(templateName, model));
        Console.WriteLine($"  WROTE: {relPath}");
        return 1;
    }

    private static string ComposeConnString(string server, string database) =>
        $"Server={server};Database={database};Trusted_Connection=True;TrustServerCertificate=True;";

    private static string FirstNonEmpty(string flagValue, Func<string> fallback) =>
        string.IsNullOrWhiteSpace(flagValue) ? fallback() : flagValue.Trim();

    private static string Ask(bool interactive, string label, string defaultValue)
    {
        if (!interactive) return defaultValue;
        Console.Write($"{label} [{defaultValue}]: ");
        var input = Console.ReadLine();
        return string.IsNullOrWhiteSpace(input) ? defaultValue : input.Trim();
    }
}
