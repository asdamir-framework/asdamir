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
using System.Net;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Asdamir.Tools.Commands;

/// <summary>
/// <c>asdamir secrets check-gateways</c> — the read-only gate between step 1 and step 2 of the move from the
/// shared <c>Jwt:Key</c> to asymmetric signing: does every managed app's Gateway already trust the NEW
/// signing key? For each active app in <c>dbo.Apps</c> (the control plane's own row skipped) it signs one
/// short-lived token with that key and sends ONE GET to the app's Gateway, then reports ACCEPTED / REFUSED /
/// UNREACHABLE / INCONCLUSIVE per app. It writes nothing anywhere.
/// </summary>
/// <remarks>
/// <para><b>Which token, and why.</b> A user-shaped app token (<c>token_use=app</c>, <c>app_code</c> = the
/// app's registered code, <c>aud</c> = <c>Jwt:Audience</c>, a <c>tid</c>) — the token every Gateway generation
/// has always had to accept. An orchestration token would be the narrower choice, but a Gateway generated
/// before 2.0, or a hand-written one, does not know the orchestration audience and would answer 401 while in
/// fact trusting the key: a false REFUSED, which is the one wrong answer that sends an operator to fix a
/// Gateway that is fine.</para>
/// <para><b>Which endpoint, and why.</b> <c>gateway/admin/audittrail?limit=1</c>. A generated Gateway guards
/// it with the <c>AppMgmtClient</c> policy, which requires a <c>client_id</c> the probe deliberately does not
/// carry: so an authenticated probe is refused by AUTHORIZATION (403) before the controller runs — nothing is
/// read — and an unauthenticated one by AUTHENTICATION (401). That split is the whole measurement. The
/// <c>tid</c> claim is there so the tenancy middleware resolves a tenant instead of refusing the request with a
/// logged warning (which a generated Gateway forwards to the central log).</para>
/// <para><b>Classification is deliberately conservative.</b> Only a 403 is ACCEPTED. A 401 is REFUSED only when
/// the Gateway's <c>WWW-Authenticate</c> names the signature ("The signature key was not found" / "The
/// signature is invalid"); a 401 for any other reason (issuer, audience, lifetime, the app boundary) — or for
/// no stated reason — is INCONCLUSIVE with the reason quoted, because it says nothing about the key. A 2xx is
/// INCONCLUSIVE too: an endpoint that serves a user token does not show whether the token was checked.</para>
/// <para><b>Crypto is local</b> (<see cref="GatewayProbeToken"/>), like <see cref="SecretCrypto"/>: ES256 is
/// one ECDSA call plus base64url, and a Core reference would drag Core's runtime graph into the tool.</para>
/// </remarks>
public static class GatewayKeyCheckCommand
{
    /// <summary>The control plane's own <c>dbo.Apps</c> row (AsdamirVault_033) — it has no Gateway to probe.</summary>
    internal static readonly Guid SelfAppId = Guid.Parse("2826118b-646e-4824-833b-6df8f903ded7");

    /// <summary>The Gateway path probed — authenticated, and refused by authorization before any code runs.</summary>
    internal const string ProbePath = "gateway/admin/audittrail?limit=1";

    /// <summary>The framework's default <c>Jwt:Issuer</c> (AppManagement and the generated Gateway agree on it).</summary>
    internal const string DefaultIssuer = "asdamir-adminconsole-api";

    /// <summary>The framework's default <c>Jwt:Audience</c> for app tokens.</summary>
    internal const string DefaultAudience = "asdamir-adminconsole-clients";

    /// <summary>Builds the <c>secrets check-gateways</c> subcommand.</summary>
    /// <returns>The command.</returns>
    public static Command Build()
    {
        var keyFileOpt = new Option<string>(new[] { "--key-file", "-k" }, () => "",
            "Path to the NEW private signing key (PKCS#8 / SEC1 EC P-256 PEM — what `secrets new-signing-key` wrote). The key itself is never accepted on the command line.");
        var connOpt = new Option<string>(new[] { "--connection", "-c", "--vault-connection" }, () => "",
            "Full ADO.NET connection string to AsdamirVault. Wins over --server/--database/--user/--password.");
        var serverOpt = new Option<string>(new[] { "--server", "-S" }, () => "localhost", "SQL Server instance (when --connection is omitted).");
        var databaseOpt = new Option<string>(new[] { "--database", "-d" }, () => "", "AsdamirVault database name. Required when --connection is omitted — a connection is never defaulted.");
        var userOpt = new Option<string>(new[] { "--user", "-U" }, () => "", "SQL login (SQL auth). Omit for Windows integrated auth.");
        var passwordOpt = new Option<string>(new[] { "--password", "-P" }, () => "", "Password for --user.");
        var issuerOpt = new Option<string>("--issuer", () => DefaultIssuer, "The token issuer the Gateways expect (AppManagement's Jwt:Issuer).");
        var audienceOpt = new Option<string>("--audience", () => DefaultAudience, "The app-token audience the Gateways expect (AppManagement's Jwt:Audience).");
        var appOpt = new Option<string[]>("--app", () => [], "Probe only this app Code (repeatable). Default: every active app.")
        {
            AllowMultipleArgumentsPerToken = false,
        };
        var timeoutOpt = new Option<int>("--timeout", () => 10, "Seconds to wait for each Gateway (1–120).");
        var insecureOpt = new Option<bool>("--allow-untrusted-certs", () => false,
            "Development only: accept any TLS server certificate (e.g. the ASP.NET dev certificate). Prints a warning.");

        var cmd = new Command("check-gateways",
            "Read-only: before AppManagement switches to Jwt:SigningKeyPem, check that every active managed app's Gateway already trusts the NEW signing key. One harmless GET per Gateway; nothing is written.")
        {
            keyFileOpt, connOpt, serverOpt, databaseOpt, userOpt, passwordOpt,
            issuerOpt, audienceOpt, appOpt, timeoutOpt, insecureOpt,
        };

        cmd.SetHandler(async ctx =>
        {
            var p = ctx.ParseResult;
            ctx.ExitCode = await RunFromVaultAsync(new Settings(
                p.GetValueForOption(keyFileOpt) ?? "",
                p.GetValueForOption(connOpt) ?? "", p.GetValueForOption(serverOpt) ?? "localhost",
                p.GetValueForOption(databaseOpt) ?? "", p.GetValueForOption(userOpt) ?? "",
                p.GetValueForOption(passwordOpt) ?? "",
                p.GetValueForOption(issuerOpt) ?? DefaultIssuer, p.GetValueForOption(audienceOpt) ?? DefaultAudience,
                p.GetValueForOption(appOpt) ?? [], p.GetValueForOption(timeoutOpt),
                p.GetValueForOption(insecureOpt)), Console.Out, Console.Error);
        });
        return cmd;
    }

    /// <summary>Everything the command was given.</summary>
    internal sealed record Settings(
        string KeyFile, string Connection, string Server, string Database, string User, string Password,
        string Issuer, string Audience, string[] Apps, int TimeoutSeconds, bool AllowUntrustedCerts);

    /// <summary>One registered app to probe.</summary>
    /// <param name="Code">The app's <c>dbo.Apps.Code</c> — stamped as <c>app_code</c>.</param>
    /// <param name="GatewayBaseUrl">The app's <c>dbo.Apps.GatewayBaseUrl</c>.</param>
    internal sealed record ManagedGateway(string Code, string GatewayBaseUrl);

    /// <summary>The four answers a probe can give.</summary>
    internal enum Verdict
    {
        /// <summary>Authenticated with the new key (and then refused by authorization, as designed).</summary>
        Accepted,

        /// <summary>401 naming the signature: this Gateway does not trust the new key.</summary>
        Refused,

        /// <summary>No HTTP answer at all: connection, DNS, TLS or timeout.</summary>
        Unreachable,

        /// <summary>An answer that says nothing about the key; the detail says which.</summary>
        Inconclusive,
    }

    /// <summary>The verdict for one app plus the evidence behind it.</summary>
    internal sealed record ProbeResult(Verdict Verdict, string Detail);

    /// <summary>The real path: validate the arguments, read <c>dbo.Apps</c>, probe.</summary>
    private static async Task<int> RunFromVaultAsync(Settings s, TextWriter stdout, TextWriter stderr)
    {
        if (!TryValidate(s, stderr)) return ExitCodes.Usage;

        if (!TryLoadSigningKey(s.KeyFile, out var key, out var keyError))
        {
            stderr.WriteLine(keyError);
            return ExitCodes.Usage;
        }

        using (key)
        {
            string connString;
            try { connString = BuildConnString(s.Connection, s.Server, s.Database, s.User, s.Password); }
            catch (Exception ex) when (ex is ArgumentException or FormatException or KeyNotFoundException)
            {
                stderr.WriteLine($"Invalid connection settings: {ex.Message}");
                return ExitCodes.Usage;
            }

            List<ManagedGateway> all;
            int inactive;
            try
            {
                (all, inactive) = await LoadAppsAsync(connString);
            }
            catch (SqlException ex)
            {
                // Same rule as `audit verify-signatures` against an unreadable ledger: nothing was examined, so it
                // is neither a finding (1 must keep meaning "a Gateway is not ready") nor an internal error.
                stderr.WriteLine($"Could not read dbo.Apps from the vault: {ex.Message} Nothing was checked.");
                return ExitCodes.Usage;
            }

            List<ManagedGateway> apps;
            if (s.Apps.Length > 0)
            {
                var unknown = s.Apps.Where(a => !all.Any(g => string.Equals(g.Code, a, StringComparison.OrdinalIgnoreCase))).ToList();
                if (unknown.Count > 0)
                {
                    stderr.WriteLine($"No active managed app with Code {string.Join(", ", unknown.Select(u => $"'{u}'"))} in dbo.Apps.");
                    return ExitCodes.Usage;
                }
                apps = all.Where(g => s.Apps.Any(a => string.Equals(g.Code, a, StringComparison.OrdinalIgnoreCase))).ToList();
            }
            else
            {
                apps = all;
            }

            if (inactive > 0 && s.Apps.Length == 0)
                stdout.WriteLine($"({inactive} inactive app(s) in dbo.Apps skipped.)");

            using var handler = CreateHandler(s.AllowUntrustedCerts);
            return await RunAsync(s, key!, apps, handler, stdout, stderr);
        }
    }

    /// <summary>
    /// The probing core: mint a token per app, send one GET, classify, print the table, return the exit code.
    /// Separate from the vault read so a test can drive it with a fixed app list and a stub handler.
    /// </summary>
    /// <param name="s">The command settings.</param>
    /// <param name="key">The new private signing key.</param>
    /// <param name="apps">The apps to probe.</param>
    /// <param name="handler">The HTTP handler (a real one, or a stub in tests). Not disposed here.</param>
    /// <param name="stdout">Where the table goes.</param>
    /// <param name="stderr">Where warnings go.</param>
    /// <returns><see cref="ExitCodes.Success"/> only when at least one app was probed and every one ACCEPTED;
    /// otherwise <see cref="ExitCodes.Findings"/>.</returns>
    internal static async Task<int> RunAsync(Settings s, ECDsa key, IReadOnlyList<ManagedGateway> apps,
        HttpMessageHandler handler, TextWriter stdout, TextWriter stderr)
    {
        if (s.AllowUntrustedCerts)
            stderr.WriteLine("WARNING: --allow-untrusted-certs is on — TLS server certificates are NOT validated. Use it only against development certificates.");

        var keyId = GatewayProbeToken.KeyId(key);
        stdout.WriteLine($"Probing with the key in --key-file (kid {keyId}) — iss '{s.Issuer}', aud '{s.Audience}', GET {ProbePath}");

        if (apps.Count == 0)
        {
            stdout.WriteLine("No active managed app found in dbo.Apps (the control plane's own row is skipped) — nothing was checked. This is NOT a pass.");
            return ExitCodes.Findings;
        }

        using var http = new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(s.TimeoutSeconds) }; // audit-lint:ignore AUD001 one-shot CLI, one client for the whole run over one handler, no DI host
        var rows = new List<(ManagedGateway App, ProbeResult Result)>(apps.Count);
        foreach (var app in apps)
        {
            var token = GatewayProbeToken.Mint(key, s.Issuer, s.Audience, app.Code, DateTimeOffset.UtcNow);
            rows.Add((app, await ProbeAsync(http, app, token, s.TimeoutSeconds)));
        }

        PrintTable(stdout, rows);

        var accepted = rows.Count(r => r.Result.Verdict == Verdict.Accepted);
        stdout.WriteLine();
        if (accepted == rows.Count)
        {
            stdout.WriteLine($"All {rows.Count} Gateway(s) trust the new key — AppManagement may switch to it (step 2).");
            return ExitCodes.Success;
        }

        stdout.WriteLine($"{accepted} of {rows.Count} Gateway(s) trust the new key. Do NOT switch AppManagement yet.");
        if (rows.Any(r => r.Result.Verdict == Verdict.Refused))
            stdout.WriteLine("REFUSED: give that Gateway the new PUBLIC key (Jwt:PublicKeyPem, with Jwt:Key + Jwt:AcceptLegacySymmetric=true) and restart it. "
                             + "A Gateway on Jwt:JwksUrl cannot trust a key AppManagement does not publish yet — see docs/secret-rotation.md.");
        if (rows.Any(r => r.Result.Verdict == Verdict.Inconclusive))
            stdout.WriteLine("INCONCLUSIVE: the answer says nothing about the key — fix what the detail names (often --issuer / --audience, or the registered Gateway URL) and re-run.");
        return ExitCodes.Findings;
    }

    /// <summary>Sends the one probe request and classifies what came back (or did not).</summary>
    internal static async Task<ProbeResult> ProbeAsync(HttpClient http, ManagedGateway app, string token, int timeoutSeconds)
    {
        if (!TryProbeUri(app.GatewayBaseUrl, out var uri))
            return new ProbeResult(Verdict.Unreachable, "the registered Gateway URL is not an absolute http(s) URL");

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            return Classify(response);
        }
        catch (TaskCanceledException)
        {
            return new ProbeResult(Verdict.Unreachable, $"no answer within {timeoutSeconds}s");
        }
        catch (HttpRequestException ex)
        {
            return new ProbeResult(Verdict.Unreachable, DescribeNetworkFailure(ex));
        }
    }

    /// <summary>Maps one Gateway response to a verdict. Pure — the unit tests pin every branch.</summary>
    internal static ProbeResult Classify(HttpResponseMessage response)
    {
        var status = (int)response.StatusCode;
        if (response.StatusCode == HttpStatusCode.Forbidden)
            return new ProbeResult(Verdict.Accepted, "403 after authentication (refused by authorization, as designed)");

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var reason = BearerReason(response.Headers.WwwAuthenticate);
            if (reason is not null && IsSignatureReason(reason))
                return new ProbeResult(Verdict.Refused, $"401: {reason}");
            return new ProbeResult(Verdict.Inconclusive, reason is null
                ? "401 with no stated reason — cannot attribute it to the key"
                : $"401 for a reason other than the key: {reason}");
        }

        if (status is >= 300 and < 400)
            return new ProbeResult(Verdict.Inconclusive,
                $"{status} redirect to {response.Headers.Location?.ToString() ?? "(no Location)"} — register the final Gateway URL");
        if (status is >= 200 and < 300)
            return new ProbeResult(Verdict.Inconclusive,
                $"{status} — the endpoint served a user token, so it does not show whether the token was checked");
        return new ProbeResult(Verdict.Inconclusive, $"HTTP {status} {response.ReasonPhrase}".TrimEnd());
    }

    /// <summary>True when a JwtBearer failure description names the signature (the key), not a claim.</summary>
    private static bool IsSignatureReason(string reason)
        => reason.Contains("signature key was not found", StringComparison.OrdinalIgnoreCase)
           || reason.Contains("signature is invalid", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The failure the Gateway stated in its <c>Bearer</c> challenge: <c>error_description</c> when present
    /// (ASP.NET's JwtBearer writes "The signature key was not found", "The issuer 'x' is invalid", …), else the
    /// bare <c>error</c> code, else null.
    /// </summary>
    internal static string? BearerReason(HttpHeaderValueCollection<AuthenticationHeaderValue> challenges)
    {
        foreach (var challenge in challenges)
        {
            if (!string.Equals(challenge.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)) continue;
            var parameter = challenge.Parameter;
            if (string.IsNullOrWhiteSpace(parameter)) continue;
            var description = Param(parameter, "error_description");
            if (!string.IsNullOrWhiteSpace(description)) return description;
            var error = Param(parameter, "error");
            if (!string.IsNullOrWhiteSpace(error)) return $"error=\"{error}\" (no description)";
        }
        return null;
    }

    /// <summary>Reads one <c>name="value"</c> pair from a challenge parameter list (values never contain quotes here).</summary>
    private static string? Param(string parameters, string name)
    {
        var marker = name + "=\"";
        var at = 0;
        while ((at = parameters.IndexOf(marker, at, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            // Must be a whole parameter name: start of string, or preceded by a separator.
            if (at == 0 || parameters[at - 1] is ' ' or ',')
            {
                var start = at + marker.Length;
                var end = parameters.IndexOf('"', start);
                return end < 0 ? parameters[start..] : parameters[start..end];
            }
            at += marker.Length;
        }
        return null;
    }

    /// <summary>A one-line reason for a request that produced no HTTP answer.</summary>
    private static string DescribeNetworkFailure(HttpRequestException ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            if (e is AuthenticationException)
                return $"TLS failure: {e.Message} (a development certificate needs --allow-untrusted-certs)";
        }
        return ex.HttpRequestError switch
        {
            HttpRequestError.NameResolutionError => $"name resolution failed: {ex.Message}",
            HttpRequestError.ConnectionError => $"connection failed: {ex.Message}",
            HttpRequestError.SecureConnectionError => $"TLS failure: {ex.Message}",
            _ => ex.Message,
        };
    }

    private static bool TryProbeUri(string baseUrl, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(baseUrl)) return false;
        var normalized = baseUrl.Trim();
        if (!normalized.EndsWith('/')) normalized += "/";
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var b) || (b.Scheme != Uri.UriSchemeHttps && b.Scheme != Uri.UriSchemeHttp))
            return false;
        uri = new Uri(b, ProbePath);
        return true;
    }

    private static void PrintTable(TextWriter stdout, List<(ManagedGateway App, ProbeResult Result)> rows)
    {
        string[] headers = ["APP", "GATEWAY URL", "RESULT", "DETAIL"];
        var cells = rows.Select(r => new[] { r.App.Code, r.App.GatewayBaseUrl, Label(r.Result.Verdict), r.Result.Detail }).ToList();
        var widths = Enumerable.Range(0, 3).Select(i => Math.Max(headers[i].Length, cells.Count == 0 ? 0 : cells.Max(c => c[i].Length))).ToArray();
        stdout.WriteLine();
        stdout.WriteLine($"{headers[0].PadRight(widths[0])}  {headers[1].PadRight(widths[1])}  {headers[2].PadRight(widths[2])}  {headers[3]}");
        foreach (var c in cells)
            stdout.WriteLine($"{c[0].PadRight(widths[0])}  {c[1].PadRight(widths[1])}  {c[2].PadRight(widths[2])}  {c[3]}");
    }

    /// <summary>The printed name of a verdict.</summary>
    internal static string Label(Verdict v) => v switch
    {
        Verdict.Accepted => "ACCEPTED",
        Verdict.Refused => "REFUSED",
        Verdict.Unreachable => "UNREACHABLE",
        _ => "INCONCLUSIVE",
    };

    private static bool TryValidate(Settings s, TextWriter stderr)
    {
        if (s.TimeoutSeconds is < 1 or > 120)
        {
            stderr.WriteLine("--timeout must be between 1 and 120 seconds.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(s.Issuer) || string.IsNullOrWhiteSpace(s.Audience))
        {
            stderr.WriteLine("--issuer and --audience may not be empty.");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Loads the new private signing key from a PEM file. Every error message names the FILE, never its
    /// content: a parser's own message can quote key material, so none is passed through.
    /// </summary>
    /// <param name="path">The PEM file path.</param>
    /// <param name="key">The loaded P-256 key, or null.</param>
    /// <param name="error">Why it could not be loaded (safe to print).</param>
    /// <returns>True when the key is a usable EC P-256 private key.</returns>
    internal static bool TryLoadSigningKey(string path, out ECDsa? key, out string error)
    {
        key = null;
        error = "";
        if (string.IsNullOrWhiteSpace(path))
        {
            error = "--key-file is required: the path to the NEW private signing key (PEM), e.g. jwt-signing-key.pem from `asdamir secrets new-signing-key --out`. The key is never accepted on the command line.";
            return false;
        }
        if (!File.Exists(path))
        {
            error = $"Key file '{path}' does not exist.";
            return false;
        }

        string pem;
        try { pem = File.ReadAllText(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = $"Key file '{path}' cannot be read ({ex.GetType().Name}).";
            return false;
        }

        if (pem.Contains("ENCRYPTED PRIVATE KEY", StringComparison.Ordinal))
        {
            error = $"Key file '{path}' holds a passphrase-encrypted key; give the unencrypted PEM `secrets new-signing-key` writes.";
            return false;
        }
        if (!pem.Contains("PRIVATE KEY", StringComparison.Ordinal))
        {
            error = pem.Contains("PUBLIC KEY", StringComparison.Ordinal)
                ? $"Key file '{path}' holds a PUBLIC key. The probe is signed, so it needs the new PRIVATE key (jwt-signing-key.pem)."
                : $"Key file '{path}' is not a private key in PEM form.";
            return false;
        }

        var ecdsa = ECDsa.Create();
        try
        {
            ecdsa.ImportFromPem(pem);
        }
        catch (Exception ex) when (ex is ArgumentException or CryptographicException)
        {
            ecdsa.Dispose();
            // The exception is deliberately dropped: its message can quote the PEM.
            error = $"Key file '{path}' is not a readable EC private key in PEM form.";
            return false;
        }

        var curve = ecdsa.ExportParameters(includePrivateParameters: false).Curve;
        if (ecdsa.KeySize != 256 || curve.Oid?.Value != "1.2.840.10045.3.1.7")
        {
            ecdsa.Dispose();
            error = $"Key file '{path}' is not an EC P-256 key (ES256 needs P-256).";
            return false;
        }

        key = ecdsa;
        return true;
    }

    /// <summary>Reads every non-self row of <c>dbo.Apps</c>: the active ones to probe, and how many are inactive.</summary>
    private static async Task<(List<ManagedGateway> Active, int Inactive)> LoadAppsAsync(string connString)
    {
        var active = new List<ManagedGateway>();
        var inactive = 0;
        // One-shot read-only maintenance query; IDbConnectionFactory is a runtime multi-tenant abstraction.
        await using var conn = new SqlConnection(connString); // audit-lint:ignore AUD002
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Code, GatewayBaseUrl, IsActive FROM dbo.Apps WHERE AppId <> @self ORDER BY Code;";
        cmd.Parameters.AddWithValue("@self", SelfAppId);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            if (!r.GetBoolean(2)) { inactive++; continue; }
            active.Add(new ManagedGateway(r.GetString(0), r.IsDBNull(1) ? "" : r.GetString(1)));
        }
        return (active, inactive);
    }

    /// <summary>The real handler: no automatic redirects (one would drop the Authorization header and
    /// misreport the Gateway), TLS validation on unless explicitly relaxed.</summary>
    private static SocketsHttpHandler CreateHandler(bool allowUntrustedCerts)
    {
        var handler = new SocketsHttpHandler { AllowAutoRedirect = false };
        if (allowUntrustedCerts)
            handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        return handler;
    }

    private static string BuildConnString(string connection, string server, string database, string user, string password)
    {
        if (!string.IsNullOrWhiteSpace(connection))
        {
            var b = new SqlConnectionStringBuilder(connection);
            if (!string.IsNullOrWhiteSpace(database)) b.InitialCatalog = database;
            if (string.IsNullOrWhiteSpace(b.InitialCatalog))
                throw new ArgumentException("The connection string names no database — add Database=AsdamirVault or pass --database.");
            return b.ConnectionString;
        }
        if (string.IsNullOrWhiteSpace(database))
            throw new ArgumentException("A vault connection is required — provide --connection, or --database (with --server).");
        var sb = new SqlConnectionStringBuilder { DataSource = server, InitialCatalog = database, TrustServerCertificate = true };
        if (!string.IsNullOrWhiteSpace(user)) { sb.UserID = user; sb.Password = password; }
        else sb.IntegratedSecurity = true;
        return sb.ConnectionString;
    }
}

/// <summary>
/// Mints the probe token: a compact ES256 JWS with the claims a Gateway requires of an app token. Local on
/// purpose (see <see cref="GatewayKeyCheckCommand"/>); the unit tests verify it with the framework's own
/// validator so the two cannot drift.
/// </summary>
internal static class GatewayProbeToken
{
    /// <summary>How long the probe token is valid. One request needs seconds; this bounds a leaked token.</summary>
    internal static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    /// <summary>Backdating of <c>nbf</c>/<c>iat</c>, so a Gateway whose clock trails this host's by less than
    /// this does not see a token from the future (which would read as a lifetime failure, not a key one).</summary>
    internal static readonly TimeSpan Backdate = TimeSpan.FromSeconds(30);

    /// <summary>The <c>tid</c> the probe carries so the tenancy middleware resolves a tenant. Nothing reads it:
    /// authorization refuses the request first.</summary>
    internal const string ProbeTenant = "asdamir-key-probe";

    /// <summary>The <c>sub</c> of the probe — recognisable in a Gateway's request log.</summary>
    internal const string ProbeSubject = "asdamir-check-gateways";

    /// <summary>Signs one probe token for <paramref name="appCode"/>.</summary>
    internal static string Mint(ECDsa key, string issuer, string audience, string appCode, DateTimeOffset now)
    {
        var header = Json(w =>
        {
            w.WriteString("alg", "ES256");
            w.WriteString("kid", KeyId(key));
            w.WriteString("typ", "JWT");
        });
        var payload = Json(w =>
        {
            w.WriteString("iss", issuer);
            w.WriteString("aud", audience);
            w.WriteString("sub", ProbeSubject);
            w.WriteString("token_use", "app");
            w.WriteString("app_code", appCode);
            w.WriteString("tid", ProbeTenant);
            w.WriteString("jti", Guid.NewGuid().ToString("N"));
            w.WriteNumber("iat", (now - Backdate).ToUnixTimeSeconds());
            w.WriteNumber("nbf", (now - Backdate).ToUnixTimeSeconds());
            w.WriteNumber("exp", (now + Lifetime).ToUnixTimeSeconds());
        });
        var signingInput = $"{B64Url(header)}.{B64Url(payload)}";
        // ECDsa.SignData's default format is IEEE P1363 (r‖s, 64 bytes for P-256) — exactly what JWS ES256 requires.
        var signature = key.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256);
        return $"{signingInput}.{B64Url(signature)}";
    }

    /// <summary>
    /// The RFC 7638 thumbprint of the public key — the <c>kid</c> the framework stamps and publishes for the
    /// same key (<c>JwtKeyMaterial.SigningKeyId</c>), so a Gateway can find the key by id.
    /// </summary>
    internal static string KeyId(ECDsa key)
    {
        var p = key.ExportParameters(includePrivateParameters: false);
        // Members in lexicographic order, no whitespace — the canonical form RFC 7638 §3.2 hashes.
        var canonical = $"{{\"crv\":\"P-256\",\"kty\":\"EC\",\"x\":\"{B64Url(p.Q.X!)}\",\"y\":\"{B64Url(p.Q.Y!)}\"}}";
        return B64Url(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static byte[] Json(Action<Utf8JsonWriter> body)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartObject();
            body(w);
            w.WriteEndObject();
        }
        return ms.ToArray();
    }

    internal static string B64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
