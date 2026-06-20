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
/// <c>asdamir new mobile &lt;Name&gt; [--output dir] [--namespace ns] [--gateway-url URL] [--local-feed PATH]</c>
///
/// Bootstraps a .NET MAUI Blazor Hybrid mobile app skeleton:
///
///   &lt;Name&gt;/
///   ├── &lt;Name&gt;.sln, Directory.Packages.props, Directory.Build.props,
///   │   nuget.config, .gitignore, README.md
///   ├── src/
///   │   ├── &lt;Name&gt;.Mobile/         MAUI host app (Android-only by default;
///   │   │                            iOS / Windows commented out, ready to enable).
///   │   │                            – MauiProgram bootstraps DI, HttpClient, SecureStorage.
///   │   │                            – Single BlazorWebView hosts the Razor pages.
///   │   ├── &lt;Name&gt;.Mobile.Shared/  Razor class library — pages, services, navigation.
///   │   │                            Reused 1:1 by any future web companion.
///   │   └── &lt;Name&gt;.Mobile.Data/    SQLite entities for offline caching.
///   └── tests/
///       └── &lt;Name&gt;.Mobile.Shared.Tests/  xUnit smoke (services + DTO contracts).
///
/// Audit pattern enforcements baked into the template:
///   – Tokens land in <c>ISecureStorage</c> (NOT Preferences / SharedPreferences).
///   – HttpClient is named + created via <c>IHttpClientFactory</c> (AUD001).
///   – Sensitive log placeholders ({Token}, {Password}) are absent; redacted prefix only.
///   – Localization client points at <c>{gateway}/gateway/localization</c> with tr-TR
///     as the default and en-US / ru-RU as supported alternates.
/// </summary>
public static class MobileCommand
{
    public static Command Build()
    {
        var nameArg = new Argument<string>("name",
            "PascalCase application name (e.g. MobileV2). Becomes the solution + project name prefix.");

        var outputOpt = new Option<DirectoryInfo>(
            new[] { "--output", "-o" },
            description: "Parent directory under which '<Name>/' will be created. Defaults to the current working directory.",
            getDefaultValue: () => new DirectoryInfo(Directory.GetCurrentDirectory()));

        var namespaceOpt = new Option<string>(
            new[] { "--namespace", "-n" },
            description: "Root namespace. Defaults to the app name.",
            getDefaultValue: () => "");

        var gatewayUrlOpt = new Option<string>(
            new[] { "--gateway-url" },
            description: "Default Gateway base URL the app will hit. Burns into appsettings; can be overridden at runtime.",
            getDefaultValue: () => "https://localhost:7001/");

        var localFeedOpt = new Option<string>(
            new[] { "--local-feed" },
            description: "Optional absolute path to a local NuGet feed directory.",
            getDefaultValue: () => "");

        var mobileCmd = new Command("mobile", "Generate a .NET MAUI Blazor Hybrid app skeleton (Mobile + Mobile.Shared + Mobile.Data + tests).")
        {
            nameArg, outputOpt, namespaceOpt, gatewayUrlOpt, localFeedOpt,
        };

        mobileCmd.SetHandler(Run, nameArg, outputOpt, namespaceOpt, gatewayUrlOpt, localFeedOpt);
        return mobileCmd;
    }

    private static void Run(string name, DirectoryInfo output, string nsOverride, string gatewayUrl, string localFeed)
    {
        if (string.IsNullOrWhiteSpace(name) || !char.IsUpper(name[0]))
        {
            Console.Error.WriteLine("App name must be PascalCase (e.g. MobileV2).");
            Environment.Exit(2);
            return;
        }

        var ns = string.IsNullOrWhiteSpace(nsOverride) ? name : nsOverride;
        var appRoot = Path.Combine(output.FullName, name);
        if (Directory.Exists(appRoot) && Directory.EnumerateFileSystemEntries(appRoot).Any())
        {
            Console.Error.WriteLine($"Refusing to write into non-empty directory '{appRoot}'. Remove it first or choose a different --output.");
            Environment.Exit(3);
            return;
        }

        var now = DateTime.UtcNow;
        var model = new
        {
            AppName = name,
            AppNameLower = name.ToLowerInvariant(),
            AppInitial = name.Length > 0 ? name[..1].ToUpperInvariant() : "A",
            MobileProject = $"{name}.Mobile",
            SharedProject = $"{name}.Mobile.Shared",
            DataProject = $"{name}.Mobile.Data",
            SharedTestsProject = $"{name}.Mobile.Shared.Tests",
            Namespace = ns,
            MobileNamespace = $"{ns}.Mobile",
            SharedNamespace = $"{ns}.Mobile.Shared",
            DataNamespace = $"{ns}.Mobile.Data",
            // Android app id needs reverse-DNS style; namespace lowercased + ".app" is a safe default.
            AndroidApplicationId = $"com.{ns.ToLowerInvariant()}.mobile",
            GatewayBaseUrl = gatewayUrl,
            GeneratedAtUtc = now.ToString("u"),
            LocalFeedPath = string.IsNullOrWhiteSpace(localFeed) ? "" : localFeed.Replace('\\', '/'),
            HasLocalFeed = !string.IsNullOrWhiteSpace(localFeed),
        };

        var outputs = new[]
        {
            // Root
            ($"{name}.sln",                                                        "MobileSolution"),
            ("Directory.Packages.props",                                           "MobileDirectoryPackages"),
            ("Directory.Build.props",                                              "DirectoryBuild"),
            ("nuget.config",                                                       "NugetConfig"),
            (".gitignore",                                                         "MobileGitIgnore"),
            ("README.md",                                                          "MobileReadme"),

            // Mobile (MAUI)
            ($"src/{name}.Mobile/{name}.Mobile.csproj",                            "MobileMauiCsproj"),
            ($"src/{name}.Mobile/MauiProgram.cs",                                  "MobileMauiProgram"),
            ($"src/{name}.Mobile/App.xaml",                                        "MobileAppXaml"),
            ($"src/{name}.Mobile/App.xaml.cs",                                     "MobileAppXamlCs"),
            ($"src/{name}.Mobile/MainPage.xaml",                                   "MobileMainPageXaml"),
            ($"src/{name}.Mobile/MainPage.xaml.cs",                                "MobileMainPageXamlCs"),
            ($"src/{name}.Mobile/wwwroot/index.html",                              "MobileIndexHtml"),
            ($"src/{name}.Mobile/wwwroot/css/app.css",                             "MobileAppCss"),
            ($"src/{name}.Mobile/Platforms/Android/AndroidManifest.xml",           "MobileAndroidManifest"),
            ($"src/{name}.Mobile/Platforms/Android/MainActivity.cs",               "MobileMainActivity"),
            ($"src/{name}.Mobile/Platforms/Android/MainApplication.cs",            "MobileMainApplication"),
            ($"src/{name}.Mobile/appsettings.json",                                "MobileAppsettings"),

            // Shared (Razor class library)
            ($"src/{name}.Mobile.Shared/{name}.Mobile.Shared.csproj",              "MobileSharedCsproj"),
            ($"src/{name}.Mobile.Shared/_Imports.razor",                           "MobileSharedImports"),
            ($"src/{name}.Mobile.Shared/Routes.razor",                             "MobileSharedRoutes"),
            ($"src/{name}.Mobile.Shared/Layout/MainLayout.razor",                  "MobileSharedMainLayout"),
            ($"src/{name}.Mobile.Shared/Pages/Login.razor",                        "MobileSharedLoginPage"),
            ($"src/{name}.Mobile.Shared/Pages/ForgotPassword.razor",               "MobileSharedForgotPasswordPage"),
            ($"src/{name}.Mobile.Shared/Pages/Home.razor",                         "MobileSharedHomePage"),
            ($"src/{name}.Mobile.Shared/Services/UiState.cs",                       "MobileUiState"),
            ($"src/{name}.Mobile.Shared/Services/MobileAuthService.cs",            "MobileAuthService"),
            ($"src/{name}.Mobile.Shared/Services/MobileApiClient.cs",              "MobileApiClient"),
            ($"src/{name}.Mobile.Shared/Services/MobileLocalizationService.cs",    "MobileLocalizationService"),
            ($"src/{name}.Mobile.Shared/Services/ITokenStore.cs",                  "MobileTokenStore"),

            // Data (SQLite)
            ($"src/{name}.Mobile.Data/{name}.Mobile.Data.csproj",                  "MobileDataCsproj"),
            ($"src/{name}.Mobile.Data/Entities/CachedSetting.cs",                  "MobileDataCachedSetting"),
            ($"src/{name}.Mobile.Data/CacheStore.cs",                              "MobileDataCacheStore"),

            // Tests
            ($"tests/{name}.Mobile.Shared.Tests/{name}.Mobile.Shared.Tests.csproj","MobileSharedTestsCsproj"),
            ($"tests/{name}.Mobile.Shared.Tests/Usings.cs",                        "ServerTestsUsings"),
            ($"tests/{name}.Mobile.Shared.Tests/TokenStoreContractTests.cs",       "MobileTokenStoreTests"),
        };

        var written = 0;
        foreach (var (relPath, templateName) in outputs)
        {
            var target = Path.Combine(appRoot, relPath);
            var dir = Path.GetDirectoryName(target)!;
            Directory.CreateDirectory(dir);

            if (File.Exists(target))
            {
                Console.WriteLine($"  SKIP (exists): {relPath}");
                continue;
            }

            var content = TemplateRenderer.Render(templateName, model);
            File.WriteAllText(target, content);
            Console.WriteLine($"  WROTE: {relPath}");
            written++;
        }

        Console.WriteLine();
        Console.WriteLine($"Done. {written} files written to '{appRoot}'.");
        Console.WriteLine();
        Console.WriteLine("Next steps:");
        Console.WriteLine($"  1. cd {name}");
        Console.WriteLine($"  2. Ensure the MAUI workload + an Android SDK platform are installed:");
        Console.WriteLine($"       dotnet workload install maui-android");
        Console.WriteLine($"       dotnet build src/{name}.Mobile/{name}.Mobile.csproj -t:InstallAndroidDependencies -f net10.0-android -p:AcceptAndroidSDKLicenses=true");
        Console.WriteLine($"  3. Build a single target RID (a plain multi-RID build fails with NETSDK1047):");
        Console.WriteLine($"       dotnet build src/{name}.Mobile/{name}.Mobile.csproj -f net10.0-android -r android-arm64");
        Console.WriteLine($"  4. Open in Visual Studio / VS Code and run on an Android emulator or device.");
    }
}
