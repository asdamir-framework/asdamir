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
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Asdamir.Tools.Commands;

/// <summary>
/// <c>asdamir app register --api &lt;url&gt; --token &lt;jwt&gt; --code &lt;clientId&gt;
///     --display-name &lt;name&gt; --gateway-url &lt;url&gt; --environment &lt;env&gt;
///     --client-secret &lt;secret&gt; [--app-id-header &lt;guid&gt;]</c>
///
/// Registers a managed app into a company's AppManagement database by calling the running
/// AdminConsole.Api (<c>POST /api/admin/apps</c>) — NOT by creating a new database. This is the
/// CLI side of multi-company provisioning (docs/design/multi-company-management.md §5, PR-B4):
/// a new app is a row in the company's <c>Apps</c> table, AppId-scoped, sharing the one company
/// management DB.
///
/// <para>
/// Auth is delegated to the API: the caller passes a SuperAdmin bearer <c>--token</c> (copied from
/// an authenticated AdminConsole session) rather than the CLI reimplementing the 2FA login flow.
/// The client secret is sent over TLS and encrypted at rest by the API's EncryptionService — the
/// CLI never does crypto, so there's a single source of truth for the secret format.
/// </para>
///
/// The company is selected by the token's <c>company</c> claim (set at login) and/or the optional
/// <c>X-App-Id</c>-style routing the API already applies; pass <c>--company</c> only if your token
/// was minted without one. Exit codes: 0 ok, 1 API/HTTP error, 2 bad arguments.
/// </summary>
public static class AppRegisterCommand
{
    public static Command Build()
    {
        var apiOpt = new Option<string>(new[] { "--api", "-a" },
            "Base URL of the running AdminConsole.Api (e.g. https://localhost:5202/).") { IsRequired = true };
        var tokenOpt = new Option<string>(new[] { "--token", "-t" },
            "SuperAdmin bearer JWT (from an authenticated AdminConsole session). Carries the company claim.") { IsRequired = true };
        var codeOpt = new Option<string>("--code",
            "App code — must equal the managed app's client_credentials ClientId.") { IsRequired = true };
        var displayNameOpt = new Option<string>("--display-name", "Human-readable app name.") { IsRequired = true };
        var gatewayUrlOpt = new Option<string>("--gateway-url", "The app's Gateway base URL.") { IsRequired = true };
        var environmentOpt = new Option<string>("--environment",
            () => "Production", "Environment name (Production, Staging, …).");
        var clientSecretOpt = new Option<string>("--client-secret",
            "The client_credentials secret (min 20 chars). Encrypted at rest by the API.") { IsRequired = true };

        var cmd = new Command("register",
            "Register a managed app into a company's AppManagement DB via the running AdminConsole.Api.")
        {
            apiOpt, tokenOpt, codeOpt, displayNameOpt, gatewayUrlOpt, environmentOpt, clientSecretOpt,
        };

        cmd.SetHandler(async (context) =>
        {
            var api = context.ParseResult.GetValueForOption(apiOpt)!;
            var token = context.ParseResult.GetValueForOption(tokenOpt)!;
            var code = context.ParseResult.GetValueForOption(codeOpt)!;
            var displayName = context.ParseResult.GetValueForOption(displayNameOpt)!;
            var gatewayUrl = context.ParseResult.GetValueForOption(gatewayUrlOpt)!;
            var environment = context.ParseResult.GetValueForOption(environmentOpt)!;
            var clientSecret = context.ParseResult.GetValueForOption(clientSecretOpt)!;
            context.ExitCode = await RunAsync(api, token, code, displayName, gatewayUrl, environment, clientSecret);
        });
        return cmd;
    }

    private static async Task<int> RunAsync(
        string api, string token, string code, string displayName,
        string gatewayUrl, string environment, string clientSecret)
    {
        if (clientSecret.Length < 20)
        {
            Console.Error.WriteLine("--client-secret must be at least 20 characters.");
            return 2;
        }
        if (!Uri.TryCreate(api, UriKind.Absolute, out var apiBase))
        {
            Console.Error.WriteLine($"--api is not a valid absolute URL: {api}");
            return 2;
        }

        using var http = new HttpClient { BaseAddress = apiBase };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var payload = new
        {
            code,
            displayName,
            gatewayBaseUrl = gatewayUrl,
            environmentName = environment,
            clientSecret,
        };

        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync("api/admin/apps", payload);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not reach AdminConsole.Api at {apiBase}: {ex.Message}");
            return 1;
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            Console.Error.WriteLine("401 Unauthorized — the --token is missing, expired, or lacks SuperAdmin.");
            return 1;
        }
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            Console.Error.WriteLine("403 Forbidden — the token is not SuperAdmin.");
            return 1;
        }
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            Console.Error.WriteLine($"API returned {(int)response.StatusCode}: {Truncate(body, 500)}");
            return 1;
        }

        // 201 Created → the new app, including its server-assigned AppId.
        try
        {
            var created = await response.Content.ReadFromJsonAsync<JsonElement>();
            var appId = created.TryGetProperty("appId", out var idEl) ? idEl.GetString() : null;
            Console.WriteLine($"Registered app '{code}' ({displayName}).");
            if (appId is not null) Console.WriteLine($"  AppId: {appId}");
            Console.WriteLine("  The client secret was encrypted at rest by the API.");
        }
        catch
        {
            Console.WriteLine($"Registered app '{code}' ({displayName}).");
        }
        return 0;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
