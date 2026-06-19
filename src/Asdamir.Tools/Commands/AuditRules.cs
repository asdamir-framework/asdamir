// Copyright (C) 2026 Orhan Özşahin — Asdamir.
// Licensed under the GNU Lesser General Public License v3.0. See LICENSE.
// SPDX-License-Identifier: LGPL-3.0-or-later
//
// This file is part of the Asdamir open core. It is free software: you can redistribute it
// and/or modify it under the terms of the GNU Lesser General Public License as published by the
// Free Software Foundation, either version 3 of the License, or (at your option) any later
// version. It is distributed WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU LGPL for more details.

using System.Text.RegularExpressions;
// audit-lint:skip-file — this file's `Title:` literals deliberately contain the
// anti-pattern names (e.g. `new HttpClient()`) so the rule set documents itself.
// Without this skip, the lint would report its own dictionary entries.

namespace Asdamir.Tools.Commands;

/// <summary>
/// Severity ranking the audit lint reports. Mirrors the typical compiler ladder so
/// the CLI's exit code can be driven by the highest severity finding.
/// </summary>
public enum AuditSeverity { Info, Warning, Error }

/// <summary>
/// One pattern the audit lint scans for.
///
/// <para>
/// Each rule is intentionally simple — a line-level regex. The lint is a
/// fast smoke check, not a Roslyn-grade analyzer. False positives are tolerable
/// as long as the <see cref="Rationale"/> + <see cref="Fix"/> guidance is
/// actionable enough that the reader can suppress with confidence.
/// </para>
/// </summary>
public sealed record AuditRule(
    string Id,
    AuditSeverity Severity,
    Regex Pattern,
    string Title,
    string Rationale,
    string Fix);

/// <summary>
/// The canonical rule set. Every entry corresponds to a finding from the original
/// Asdamir audit — adding a rule here documents that the audit's lesson is
/// now enforceable in code review, not just memorized.
/// </summary>
public static class AuditRuleSet
{
    private static Regex R(string pattern, RegexOptions extra = RegexOptions.None) =>
        new(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant | extra);

    public static readonly IReadOnlyList<AuditRule> All = new[]
    {
        new AuditRule(
            Id: "AUD001",
            Severity: AuditSeverity.Error,
            Pattern: R(@"\bnew\s+HttpClient\s*\("),
            Title: "Bare `new HttpClient()` — sockets exhausted under load.",
            Rationale: "`HttpClient` instances each open a connection pool. Constructing them ad-hoc leaks sockets and bypasses any DelegatingHandler chain (auth, retry, logging).",
            Fix: "Inject `IHttpClientFactory` and call `factory.CreateClient(...)`, registered via `AddHttpClient(...)` in DI."),

        new AuditRule(
            Id: "AUD002",
            Severity: AuditSeverity.Error,
            Pattern: R(@"\bnew\s+SqlConnection\s*\("),
            Title: "Direct `new SqlConnection(...)` — bypasses tenant connection factory.",
            Rationale: "Constructing SQL connections inline leaks credentials, skips connection pooling tuning, and circumvents the multi-tenant `IDbConnectionFactory` that scopes connections per tenant.",
            Fix: "Inject `IDbConnectionFactory` / `IDatabaseContext`. Use `await using var conn = factory.Create();`."),

        new AuditRule(
            Id: "AUD003",
            Severity: AuditSeverity.Error,
            Pattern: R(@"\.BuildServiceProvider\s*\("),
            Title: "`BuildServiceProvider()` inside application code creates a parallel container.",
            Rationale: "Calling `BuildServiceProvider` outside of bootstrapping forks DI state — singletons resolved from this container are different objects than the host's. Use only in tests or composition root.",
            Fix: "Refactor so the service is resolved from the existing `IServiceProvider` / `IServiceScope`. If you truly need an isolated container, document why and suppress this rule on that line."),

        new AuditRule(
            Id: "AUD004",
            Severity: AuditSeverity.Warning,
            Pattern: R(@"\bstatic\s+(?:readonly\s+)?SemaphoreSlim\b"),
            Title: "`static SemaphoreSlim` shares lock state across the whole process.",
            Rationale: "Process-wide locks serialize every caller, including unrelated tenants/requests. They also outlive the scope they appear to protect — in tests, prior leaks corrupt the next test.",
            Fix: "Make the semaphore instance-level and own the lifecycle on the service that needs it (typically scoped or singleton-DI). Inject it where needed."),

        new AuditRule(
            Id: "AUD005",
            Severity: AuditSeverity.Warning,
            Pattern: R(@"while\s*\([^)]+\)\s*\{[^}]*Task\.Delay\s*\(\s*\d+\s*\)|do\s*\{[^}]*Task\.Delay\s*\(\s*\d+\s*\)"),
            Title: "Polling loop using `Task.Delay(...)` — replace with event-driven async.",
            Rationale: "Polling loops waste cycles and add latency proportional to the delay. They're also a common source of test flakes when timeouts straddle CI clock skew.",
            Fix: "Wait on a `TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously)` signalled when the condition flips. For Web.UI handlers, use Dispatcher.PostAsync."),

        new AuditRule(
            Id: "AUD006",
            Severity: AuditSeverity.Error,
            // Matches the two common .NET entry points: `new Rfc2898DeriveBytes(...)` and
            // `Pbkdf2(...)` (Rfc2898DeriveBytes static helpers + KeyDerivation), with the
            // iteration arg below 210k. Iteration count is positional argument 3
            // (password, salt, iterations) — match digits before the next comma or close-paren.
            Pattern: R(@"(?:new\s+Rfc2898DeriveBytes\s*\(|\bPbkdf2(?:\.[A-Za-z]+)?\s*\()[^)]*?,\s*(?:1|2|3|4|5|6|7|8|9|10|20|50|100|200|500|1000|2000|5000|10000|20000|50000|100000|150000|200000)\s*[,)]"),
            Title: "PBKDF2 iteration count below OWASP minimum (210,000 for SHA-256).",
            Rationale: "Low iteration counts make offline brute force trivial on modern hardware. The v1 framework shipped with 1,000 iterations; v2 must use ≥210k.",
            Fix: "Use ≥210,000 iterations with SHA-256, or migrate to Argon2id. Re-hash existing credentials lazily on next successful login."),

        new AuditRule(
            Id: "AUD007",
            Severity: AuditSeverity.Error,
            Pattern: R(@"(?:LogInformation|LogWarning|LogDebug|LogError|LogTrace)\s*\([^)]*\{(?:Token|Password|NewPassword|RefreshToken|Otp|OTP|SessionId|Cookie|Secret|ApiKey)\}"),
            Title: "Sensitive value in structured log template.",
            Rationale: "Tokens / passwords / OTPs landing in log destinations propagate through indexing, alerting, and incident-response screenshots. Treat them as unloggable.",
            Fix: "Drop the placeholder, or mask via a redaction enricher. For diagnostics, log a stable hash (e.g. first 8 chars of SHA-256) — never the raw secret."),

        new AuditRule(
            Id: "AUD008",
            Severity: AuditSeverity.Error,
            Pattern: R(@"\.GetAwaiter\s*\(\s*\)\s*\.GetResult\s*\(\s*\)"),
            Title: "Sync-over-async (`.GetAwaiter().GetResult()`) — deadlock risk.",
            Rationale: "Blocking on a Task can deadlock under certain SynchronizationContexts (Blazor Server in particular) and starves the thread pool under load.",
            Fix: "Make the calling method `async` and `await` the call. If you truly need a sync wrapper at a top-level boundary, isolate it and document why."),

        new AuditRule(
            Id: "AUD009",
            Severity: AuditSeverity.Warning,
            Pattern: R(@"\.Message\.Contains\s*\(\s*""[^""]*(?:database|connection|timeout|deadlock|sql)""\s*[,)]", RegexOptions.IgnoreCase),
            Title: "Error classification by `.Message.Contains(...)` — locale-sensitive and fragile.",
            Rationale: "Exception messages are localized in some runtimes / SQL drivers. String matching on them silently breaks when the host locale changes or a driver updates wording.",
            Fix: "Match on exception type (`SqlException.Number`, `DbException`, `TimeoutException`) or a stable error code, not the human-readable `.Message`."),

        new AuditRule(
            Id: "AUD010",
            Severity: AuditSeverity.Error,
            Pattern: R(@"catch[^{}]*\{[^{}]*(?:isAuthorized|_authorized|allowAccess|_canAccess)\s*=\s*true"),
            Title: "Fail-open authorization — exception path grants access.",
            Rationale: "A bug or transient downstream failure must NEVER promote an anonymous caller to an authorized one. Defaulting `isAuthorized = true` in `catch` does exactly that.",
            Fix: "Initialize the authorization flag to `false`. In `catch`, log and explicitly redirect to `/access-denied` (or return 401/403). Only the success path may set it `true`."),

        new AuditRule(
            Id: "AUD011",
            Severity: AuditSeverity.Warning,
            Pattern: R(@"new\s+MemoryCache\s*\(\s*new\s+MemoryCacheOptions\s*\(\s*\)\s*\)"),
            Title: "`MemoryCache` constructed without `SizeLimit` — unbounded growth.",
            Rationale: "An in-memory cache with no `SizeLimit` will absorb every item it's asked to. Under attack or misuse, it OOMs the process.",
            Fix: "Set `SizeLimit` on the options, and provide `Size` per `MemoryCacheEntryOptions` when caching. Or register `IMemoryCache` via DI and configure once."),

        new AuditRule(
            Id: "AUD012",
            Severity: AuditSeverity.Warning,
            Pattern: R(@"\bnew\s+Random\s*\(\s*\)"),
            Title: "Non-cryptographic `new Random()` — wrong primitive for security material.",
            Rationale: "`System.Random` is NOT cryptographically secure. If this is for an OTP, token, salt, or session id, it leaks predictable values.",
            Fix: "For security primitives, use `RandomNumberGenerator.GetBytes(...)` or `RandomNumberGenerator.GetInt32(...)`. For non-security uses (jitter, shuffle), prefer `Random.Shared` to avoid the same-seed-per-millisecond pitfall."),
    };
}
