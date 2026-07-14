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
        var noSecretsOpt = new Option<bool>(new[] { "--no-secrets" },
            description: "Skip auto-configuring the Gateway's dev user-secrets (CSPRNG Jwt:Key [free mode] + Security:EncryptionKey + ConnectionStrings:Default). By default `new app` writes them so the app is run-ready; pass this to manage secrets yourself.",
            getDefaultValue: () => false);
        var noDbOpt = new Option<bool>(new[] { "--no-db" },
            description: "Skip creating the database + applying migrations. By default (when a SQL password was supplied) `new app` runs `db apply --create-database` so the app is ready to run; pass this to scaffold files only (offline / CI / review-first).",
            getDefaultValue: () => false);

        var appCmd = new Command("app", "Generate a standalone Asdamir app (Server + Gateway + tests + sln + demo DB) that consumes the framework via DI and is managed from AppManagement.")
        {
            nameArg, outputOpt, namespaceOpt, localFeedOpt,
            serverNameOpt, apiNameOpt, databaseOpt, dbServerOpt, connStringOpt, gatewayUrlOpt,
            adminEmailOpt, adminPasswordOpt, yesOpt, modeOpt, billingOpt, noSecretsOpt, noDbOpt,
        };

        appCmd.SetHandler(async (InvocationContext ctx) =>
        {
            var r = ctx.ParseResult;
            await Run(new RawInputs(
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
                Billing: r.GetValueForOption(billingOpt),
                NoSecrets: r.GetValueForOption(noSecretsOpt),
                NoDb: r.GetValueForOption(noDbOpt)));
        });
        return appCmd;
    }

    private sealed record RawInputs(
        string Name, DirectoryInfo Output, string NsOverride, string LocalFeed,
        string ServerName, string ApiName, string Database, string DbServer,
        string ConnString, string GatewayUrl, string AdminEmail, string AdminPassword, bool Yes,
        string Mode, bool Billing, bool NoSecrets, bool NoDb);

    private static async Task Run(RawInputs raw)
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

        // If a full connection string was passed via --connection-string, honor it verbatim. Otherwise gather
        // SQL-auth credentials (portable, unlike Windows Trusted_Connection) — the user + a MASKED password.
        var connString = raw.ConnString;
        var dbUser = "sa";
        var dbPassword = "";
        if (string.IsNullOrWhiteSpace(connString))
        {
            dbUser = interactive ? Ask(true, "SQL kullanıcı / SQL user", "sa") : "sa";
            dbPassword = interactive
                ? AskSecret("SQL şifre / SQL password (boş = user-secrets ile ayarla / empty = set via user-secrets)")
                : "";
            connString = ComposeConnString(dbServer, database, dbUser, dbPassword);
        }

        var connHasSecret = connString.Contains("password=", StringComparison.OrdinalIgnoreCase)
                            || connString.Contains("pwd=", StringComparison.OrdinalIgnoreCase);
        // A REAL secret we can write to the Gateway user-secrets: a masked password from the prompt, OR a full
        // --connection-string that carries one. (ComposeConnString uses a <your-password> PLACEHOLDER when the
        // prompt password is empty, so `connHasSecret` alone can't distinguish real from placeholder.)
        var hasRealSecret = !string.IsNullOrEmpty(dbPassword)
                         || (!string.IsNullOrWhiteSpace(raw.ConnString) && connHasSecret);
        var connIsWindowsAuth = connString.Contains("Trusted_Connection", StringComparison.OrdinalIgnoreCase)
                                || connString.Contains("Integrated Security", StringComparison.OrdinalIgnoreCase);
        // appsettings.json holds ONLY a portable, secret-free connection string. A Windows-auth
        // (Trusted_Connection) string isn't portable to Linux/macOS/containers, and a password is a
        // secret — both are left empty here and set via user-secrets / env (printed in next steps).
        var connForAppsettings = (connHasSecret || connIsWindowsAuth) ? "" : connString;
        var connNeedsSecret = string.IsNullOrEmpty(connForAppsettings);
        // Cross-platform (SQL auth) example for the user-secrets command. If a full string came via
        // --connection-string it's already in the caller's shell history, so echo it verbatim; otherwise show a
        // placeholder (User Id from the prompt) so the MASKED password is never echoed back to the terminal.
        var connSecretExample = (connHasSecret && !string.IsNullOrWhiteSpace(raw.ConnString))
            ? connString
            : $"Server={dbServer},1433;Database={database};User Id={dbUser};Password=<your-password>;TrustServerCertificate=True;";

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
            ($"src/{serverProject}/Auth/AppUserSessionStore.cs",          "ServerAppUserSessionStore"),
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

        // Auto-configure the Gateway's dev user-secrets so the app is run-ready with no hand-editing (unless
        // --no-secrets). Secrets NEVER go to appsettings.json — always to user-secrets. Free mode: the Gateway
        // issues + validates its OWN JWT, so a fresh CSPRNG Jwt:Key is correct. Commercial: the Jwt:Key MUST
        // equal AppManagement's signing key (the Gateway only validates tokens AppManagement issued) — the CLI
        // can't know that, so it's left for the user. ConnectionStrings:Default is written only when a real
        // password was supplied (masked prompt or a full --connection-string); an empty password keeps the
        // manual placeholder instruction.
        var gatewayCsproj = Path.Combine(appRoot, "src", gatewayProject, $"{gatewayProject}.csproj");
        var secrets = raw.NoSecrets
            ? SecretsResult.Skipped
            : ConfigureSecrets(gatewayCsproj, isFreeMode, hasRealSecret, connString);

        // Create the database + apply migrations so the app is ready to run (unless --no-db). Reuses the SAME
        // journaled runner as `asdamir db apply --create-database` (no duplication) — CREATE DATABASE is
        // idempotent (IF DB_ID IS NULL), and the runner skips already-applied migrations. Needs a real
        // connection: an empty password (or --no-db) skips it and leaves the `db apply` step in next-steps.
        // A failure never leaves the user blind — the files are generated and the exact command is printed.
        var dbProvisioned = false;
        var dbAttempted = !raw.NoDb && hasRealSecret;
        if (dbAttempted)
        {
            Console.WriteLine("Setting up the database (db apply --create-database)…");
            Console.WriteLine();
            var appMigrations = new DirectoryInfo(Path.Combine(appRoot, "db", "migrations"));
            int dbExit;
            try { dbExit = await DbApplyCommand.RunAsync(connString, "localhost", "", "", "", appMigrations, createDatabase: true); }
            catch (Exception ex) { dbExit = 1; Console.Error.WriteLine($"Database setup error: {ex.Message}"); }
            dbProvisioned = dbExit == 0;
            Console.WriteLine();
            if (!dbProvisioned)
            {
                Console.Error.WriteLine($"⚠️  Database setup did not complete (exit {dbExit}). The files ARE generated — finish it by hand:");
                Console.Error.WriteLine($"      cd {name} && asdamir db apply --create-database --migrations db/migrations");
                Console.Error.WriteLine();
            }
        }

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
            if (dbProvisioned) Console.WriteLine("  ✓ Database created + all migrations applied (starter admin/menu/localization/config seeded).");
            PrintConfigured(secrets, hasRealSecret, isFreeMode);
            Console.WriteLine("Next steps (free mode — self-contained; no control plane):");
            var freeStep = 1;
            if (dbProvisioned)
            {
                Console.WriteLine($"  {freeStep++}. cd {name} && ./restart-{model.AppNameLower}.sh    # starts both tiers → open {gatewayUrl}, sign in with the starter admin above");
            }
            else
            {
                Console.WriteLine($"  {freeStep++}. cd {name}");
                PrintManualSecretSteps(secrets, hasRealSecret, isFreeMode, gatewayProject, connSecretExample, ref freeStep);
                Console.WriteLine($"  {freeStep++}. asdamir db apply --create-database --migrations db/migrations   # creates the DB + applies ALL migrations (reads ConnectionStrings:Default from the secret, or pass -S -d -U -P)");
                Console.WriteLine($"  {freeStep++}. ./restart-{model.AppNameLower}.sh              # starts both tiers → open {gatewayUrl} and sign in with the starter admin above");
            }
            Console.WriteLine();
            Console.WriteLine($"  Optional: dotnet build {name}.sln && dotnet test {name}.sln  ·  add a feature: asdamir new feature <Name> --fields \"...\"  ·  undo: asdamir rollback app {name}");
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
        if (dbProvisioned) Console.WriteLine($"  ✓ App (business) database created + all migrations applied.");
        PrintConfigured(secrets, hasRealSecret, isFreeMode);
        Console.WriteLine("Next steps:");
        var step = 1;
        Console.WriteLine($"  {step++}. cd {name}");
        // Commercial: the Jwt:Key MUST match AppManagement's signing key — the CLI can't know it, so it stays
        // manual (on BOTH tiers). ConnectionStrings + EncryptionKey were auto-configured above (when possible).
        Console.WriteLine($"  {step++}. Set Jwt:Key to match AppManagement's (the Gateway validates tokens AppManagement issued):");
        Console.WriteLine($"     cd src/{gatewayProject} && dotnet user-secrets set \"Jwt:Key\" \"<AppManagement's Jwt:Key>\" && cd ../..");
        PrintManualSecretSteps(secrets, hasRealSecret, isFreeMode, gatewayProject, connSecretExample, ref step);
        if (!dbProvisioned)
        {
            Console.WriteLine($"  {step++}. asdamir db apply --create-database --migrations db/migrations   # creates the app's own (business) DB (reads ConnectionStrings:Default from the secret, or pass -S -d -U -P)");
        }
        Console.WriteLine($"  {step++}. Register + seed in AppManagement: run db/admin-onboarding/register_{model.AppNameLower}.sql");
        Console.WriteLine($"     against AsdamirVault — registers the app + seeds its users/roles/permissions/menus/config/localization (AppId-scoped).");
        Console.WriteLine($"  {step++}. ./restart-{model.AppNameLower}.sh              # starts both tiers → open {gatewayUrl}");
        Console.WriteLine();
        Console.WriteLine($"  Optional: dotnet build {name}.sln && dotnet test {name}.sln  ·  add a feature: asdamir new feature <Name> --fields \"...\"  ·  undo: asdamir rollback app {name}");
        }
    }

    // ── Auto-configure the Gateway's dev user-secrets (run-ready out of the box) ────────────────────────
    private readonly record struct SecretsResult(bool Attempted, bool JwtSet, bool EncSet, bool ConnSet, string? Error)
    {
        public static readonly SecretsResult Skipped = new(false, false, false, false, null);
    }

    private static SecretsResult ConfigureSecrets(string gatewayCsproj, bool isFreeMode, bool hasRealSecret, string connStringWithSecret)
    {
        if (!File.Exists(gatewayCsproj))
            return new SecretsResult(true, false, false, false, $"Gateway csproj not found ({gatewayCsproj})");

        var errors = new List<string>();
        bool Set(string key, string value)
        {
            var (ok, log) = RunUserSecretSet(gatewayCsproj, key, value);
            if (!ok) errors.Add($"{key} ({log.Trim()})");
            return ok;
        }

        // Free mode → the Gateway owns its JWT, so a fresh 64-byte CSPRNG key is correct. Commercial → the key
        // must equal AppManagement's, which we don't have; skip it (left as a manual step in next-steps).
        var jwtSet = isFreeMode && Set("Jwt:Key", NewSecret(64));
        // At-rest encryption key (≥32 chars) — REQUIRED by the Gateway; a fresh CSPRNG key is fine for both modes.
        var encSet = Set("Security:EncryptionKey", NewSecret(32));
        // The connection string carries the real password only when one was supplied; otherwise it's left manual.
        var connSet = hasRealSecret && Set("ConnectionStrings:Default", connStringWithSecret);

        return new SecretsResult(true, jwtSet, encSet, connSet, errors.Count == 0 ? null : string.Join("; ", errors));
    }

    /// <summary>A base64 CSPRNG secret from <paramref name="bytes"/> random bytes (64 → an 88-char Jwt:Key,
    /// 32 → a 44-char encryption key — both well over their minimums).</summary>
    private static string NewSecret(int bytes)
        => Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(bytes));

    /// <summary>Shells out to <c>dotnet user-secrets set</c> for the Gateway project. Uses ArgumentList so the
    /// secret value + csproj path need no shell quoting, and captures output so a failure can fall back to the
    /// printed manual instruction. The secret is never echoed to the console by us.</summary>
    private static (bool ok, string log) RunUserSecretSet(string gatewayCsproj, string key, string value)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add("user-secrets");
            psi.ArgumentList.Add("set");
            psi.ArgumentList.Add(key);
            psi.ArgumentList.Add(value);
            psi.ArgumentList.Add("--project");
            psi.ArgumentList.Add(gatewayCsproj);
            using var p = System.Diagnostics.Process.Start(psi)!;
            var outp = p.StandardOutput.ReadToEnd();
            var err = p.StandardError.ReadToEnd();
            p.WaitForExit();
            return (p.ExitCode == 0, p.ExitCode == 0 ? "" : (err + outp));
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    /// <summary>Prints the "✓ configured" summary line for whatever secrets were written.</summary>
    private static void PrintConfigured(SecretsResult s, bool hasRealSecret, bool isFreeMode)
    {
        if (!s.Attempted) return;  // --no-secrets → say nothing (the manual steps are printed below)
        var set = new List<string>();
        if (s.JwtSet) set.Add("Jwt:Key");
        if (s.EncSet) set.Add("Security:EncryptionKey");
        if (s.ConnSet) set.Add("ConnectionStrings:Default");
        if (set.Count > 0)
            Console.WriteLine($"  ✓ Configured Gateway dev user-secrets: {string.Join(" + ", set)}.");
        if (s.Error is not null)
            Console.WriteLine($"  ⚠️  Some user-secrets could not be set ({s.Error}) — set them by hand (see below).");
        Console.WriteLine();
    }

    /// <summary>Prints the manual `user-secrets set` steps ONLY for what auto-config could NOT do (an empty
    /// password → ConnectionStrings; a failed/skipped run → all of them), bumping the step counter.</summary>
    private static void PrintManualSecretSteps(SecretsResult s, bool hasRealSecret, bool isFreeMode, string gatewayProject, string connSecretExample, ref int step)
    {
        // Fully skipped (--no-secrets) or a hard failure → print the whole secret block.
        if (!s.Attempted || (!s.EncSet && s.Error is not null))
        {
            Console.WriteLine($"  {step++}. Dev secrets (NEVER in appsettings.json) — cd src/{gatewayProject}, then:");
            if (isFreeMode)
                Console.WriteLine($"     dotnet user-secrets set \"Jwt:Key\" \"<a 64+ byte random key>\"    # the Gateway issues+validates its own JWT");
            Console.WriteLine($"     dotnet user-secrets set \"Security:EncryptionKey\" \"<a 32+ char random key>\"");
            Console.WriteLine($"     dotnet user-secrets set \"ConnectionStrings:Default\" \"{connSecretExample}\"    # then cd ../..");
            return;
        }
        // Configured, but the connection string was left manual (empty password).
        if (!s.ConnSet)
            Console.WriteLine($"  {step++}. Set the DB connection (empty password entered): cd src/{gatewayProject} && dotnet user-secrets set \"ConnectionStrings:Default\" \"{connSecretExample}\" && cd ../..");
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

    // SQL auth (portable to Linux/macOS/containers) — NOT Trusted_Connection, which is Windows-only and breaks
    // a cross-platform dev/CI setup. An empty password becomes a clear <your-password> placeholder; either way a
    // real secret is routed to user-secrets, never written to appsettings.json (see the caller).
    private static string ComposeConnString(string server, string database, string user, string password) =>
        $"Server={server},1433;Database={database};User Id={(string.IsNullOrWhiteSpace(user) ? "sa" : user)};" +
        $"Password={(string.IsNullOrEmpty(password) ? "<your-password>" : password)};TrustServerCertificate=True;";

    private static string FirstNonEmpty(string flagValue, Func<string> fallback) =>
        string.IsNullOrWhiteSpace(flagValue) ? fallback() : flagValue.Trim();

    private static string Ask(bool interactive, string label, string defaultValue)
    {
        if (!interactive) return defaultValue;
        Console.Write($"{label} [{defaultValue}]: ");
        var input = Console.ReadLine();
        return string.IsNullOrWhiteSpace(input) ? defaultValue : input.Trim();
    }

    /// <summary>Reads a secret from the console WITHOUT echoing it (masked). Falls back to a plain read when
    /// no interactive console is attached (redirected input). Enter with nothing typed returns empty.</summary>
    private static string AskSecret(string label)
    {
        Console.Write($"{label}: ");
        try
        {
            var buffer = new System.Text.StringBuilder();
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); break; }
                if (key.Key == ConsoleKey.Backspace)
                {
                    if (buffer.Length > 0) buffer.Length--;
                    continue;
                }
                if (!char.IsControl(key.KeyChar)) buffer.Append(key.KeyChar);
            }
            return buffer.ToString();
        }
        catch (InvalidOperationException)
        {
            // Input redirected / no console — fall back to a normal line read.
            return Console.ReadLine()?.Trim() ?? "";
        }
    }
}
