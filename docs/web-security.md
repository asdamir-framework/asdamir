# Web Security

**Package:** `Asdamir.Web` · **Namespace:** `Asdamir.Web.Security`

## Introduction

`Asdamir.Web` hardens Blazor/HTTP apps with a set of composable middleware and services: a Content-Security-Policy nonce, security headers, request rate limiting, ASP.NET Core Data Protection, route authorization, audit logging and auto-logout.

## Registration

```csharp
builder.Services.AddFrameworkSecurity(builder.Configuration);
...
app.UseSecurityHeaders();   // HSTS, X-Content-Type-Options, frame options, etc.
app.UseCspNonce();          // per-request CSP nonce (no inline-script holes)
app.UseRateLimiting();      // per-key request throttling
app.UseAuditLogging();      // capture audited requests
app.UseNoCache();           // no-store on sensitive responses
```

Add only what you need — each middleware has its own `Use…` extension.

## Building blocks

| Concern | What it provides |
|---|---|
| **CSP nonce** | `ICspNonceProvider` + middleware; pages emit a per-request nonce so scripts are allow-listed without `unsafe-inline` |
| **Security headers** | `SecurityHeadersOptions` — HSTS, `X-Content-Type-Options`, frame/referrer/permissions policies |
| **Rate limiting** | `IRateLimitService` (in-memory **or** DB-backed for scale-out) + `[RateLimit]` attribute + middleware |
| **Data Protection** | `DataProtectionService` — encrypt/decrypt tokens (e.g. email/refresh) with keyed purposes |
| **Route authorization** | blocks unauthorized navigation before render; decisions are cached and audited |
| **Auto-logout** | idle detection + session activity tracking + session-warning dialog |
| **Code analysis** | `AddSecurityAnalysis()` runs static security checks (optionally at startup) |

## CSP nonce example

```razor
<script nonce="@CspNonce.Current">/* allow-listed by the per-request nonce */</script>
```

Apps scaffolded with `asdamir new app` ship this **enforced by default**: the Server wires `UseCspNonce` + a
strict `script-src 'self' 'nonce-…'` policy, and its `App.razor` stamps the per-request nonce on its inline
script. Keep page JavaScript out of per-page inline `<script>` blocks (fold it into that one nonced block, or
serve it from `wwwroot` under `script-src 'self'`) so the policy stays clean; likewise self-host fonts rather
than importing them from a CDN.

## Rate limiting

```csharp
[RateLimit(3, 300)]   // limit, windowSeconds — e.g. ForgotPassword
public async Task<IActionResult> ForgotPassword(...) { ... }
```

`IRateLimitService` is a fixed-window limiter. The default `InMemoryRateLimitService` is **per-process** —
fine for a single instance, but behind a load balancer each replica counts independently (a brute-force
attacker gets `N × limit`). For **scale-out**, back it with a **shared store** so counters aggregate
across instances. AppManagement does this in Dapper mode: a `SqlRateLimitService` (counters in
`dbo.RateLimitCounters`, atomic via the `dbo.RateLimit_TryAcquire` proc) guards the login / 2FA endpoints
so the limit holds across all API instances. Swap the registered `IRateLimitService` to choose per-host
vs. shared.

## Data Protection keys (cookies + antiforgery)

Auth cookies and antiforgery tokens are encrypted with ASP.NET **Data Protection** keys. By default
that key ring is per-host and **ephemeral in containers** — every restart silently logs users out and
breaks antiforgery, and separate instances can't read each other's cookies (scale-out). Call
`AddFrameworkDataProtection(builder.Configuration)` on each UI host to fix that:

```jsonc
// appsettings.json (host)
"DataProtection": {
  // Stable name so all instances share one key ring (the default discriminator is the content-root
  // path, which differs per deployment/container and would isolate the keys).
  "ApplicationName": "MyApp",
  // A durable, shared directory — a persistent / mounted volume in production. Empty = the ASP.NET
  // default location (fine for single-host dev). Filesystem keeps this UI-tier-friendly (no DB).
  "KeyPath": "/var/asdamir/dpkeys"
}
```

The generated app's Server and AppManagement's AdminConsole already call it; set `KeyPath` to a shared
volume for restart-safe, multi-instance auth. (To encrypt the keys at rest or use a DB/Redis ring,
swap the provider in `AddFrameworkDataProtection`.)

## See also

- [Authentication](fundamentals/authentication.md) · [Authorization](fundamentals/authorization.md) · [Audit Logging](fundamentals/audit-logging.md)
