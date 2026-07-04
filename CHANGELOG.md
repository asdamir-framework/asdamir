# Changelog — Asdamir

All notable changes to this repo. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versioning: [SemVer](https://semver.org/spec/v2.0.0.html).

The open-core packages (`Asdamir.Core`, `Asdamir.Data`, `Asdamir.Web`) share one version via
`Directory.Build.props`; the CLI (`Asdamir.Tools`) versions independently. Current published state:
**Core/Data/Web `1.1.1`**, **Tools `1.1.4`** (nuget.org). AppManagement (the commercial control plane)
is not packed to NuGet — it ships as a compiled release for commercial customers.

## [Unreleased]

- **FluentUI v5 migration** (`feature/fluentui-v5`) — pinned to the v5 RC (`5.0.0-rc.4-26180.1`); awaiting
  GA before merge to `main`. Not yet released.
- **No CI/CD** — `.github/workflows/*` were removed (GitHub Actions billing). The process is plain
  `git pull` / `git push` to `main`; verify locally before every push (`dotnet build` 0 warnings ·
  `./run-tests.sh` · `audit lint`).

## [Tools 1.1.4] — 2026-07-04

- **`Asdamir.Tools` 1.1.4** — generated-template fixes: pin **`Microsoft.OpenApi 2.7.5`** in the generated
  `Directory.Packages` (closes NU1903/GHSA-v5pm so a scaffolded app builds under `TreatWarningsAsErrors`),
  templater **fail-fast** on an unknown member / unsupported parenthesis (was silently emitting empty/wrong
  fragments), and the **stale-TRX + skip-as-fail** fix in the generated `run-tests.sh`. Core/Data/Web
  unchanged at 1.1.1.

## [Tools 1.1.3] — 2026-06-30

- **`Asdamir.Tools` 1.1.3** — core workflow automation: `asdamir new feature <Name>` (entity + page +
  menu/permission/localization seeds in one command) and `asdamir rollback <Name>` (undo a generated
  feature across code + app-DB table + AsdamirVault menu/permissions). Two-DB apply is opt-in
  (`--apply` for the app DB, `--vault-connection` for AsdamirVault).

## [Tools 1.1.2] — 2026-06-28

- **`Asdamir.Tools` 1.1.2** — every generated app now ships an executable `run-tests.sh` (clean
  PASS/FAIL per test via TRX parse, same as the framework's).

## [1.1.1] — 2026-06-27  ·  Core / Data / Web

- **`Asdamir.Web`** — rate-limiter fix (fixed-window counter correctness).
- **`Asdamir.Tools`** generator changes rolled into the open-core release.

## [Tools 1.1.0] — 2026-06-26

- **`Asdamir.Tools` 1.1.0** — richer generated test suite: `asdamir new entity` now emits an update
  round-trip test, a list test (both service-level against an in-memory fake repo) and an **API
  auth-guard test** (`WebApplicationFactory`, token-less `GET` → 401), in addition to the existing
  create/get/delete/validator tests — **6 tests per entity, all DB-free**. `ScaffoldSmokeTests` now runs
  `dotnet test` on the generated solution, so a broken generated test fails the build. (Core/Data/Web
  unchanged at 1.0.4 at the time.)

## [1.0.4] — 2026-06-23  ·  Core / Data / Web / Tools

- Auto-DI by convention in generated Gateways (reflection scan registers `I<Name>Repository`/
  `I<Name>Service` — no per-entity `AddScoped`), DB-backed UI localization end-to-end, the topbar
  (theme / dark-mode / language) in generated Server hosts, and nav-menu label localization via
  `Menu.<Slug>` keys. Scaffold smoke test cut from ~16 min to ~10 s.
- Rolls up the intermediate `1.0.1`–`1.0.3` open-core bumps:
  - **1.0.1** (2026-06-22) — `Asdamir.Web.Http.ToUserMessageAsync` (the shared user-facing-error helper)
    + Core error-key fallback chain.
  - **1.0.2 / 1.0.3** (2026-06-23) — `Asdamir.Tools` scaffold template/command fixes, generated-app
    UI-auth + topbar wiring.

## [1.0.0] — 2026-06-20  ·  initial nuget.org publish

First public open-core release: `Asdamir.Core`, `Asdamir.Data`, `Asdamir.Web`, `Asdamir.Tools`
(LGPL-3.0). The CLI command was renamed `framework` → `asdamir` to match the brand/package/docs.

### Security

- **`CryptographyService`** — password hashing on **PBKDF2-SHA256, 210 000 iterations**, with a **16-byte
  per-record random salt**. Format: `$pbkdf2-sha256$210000$<salt>$<hash>`. `NeedsRehash` flags old-format
  hashes so callers can lazy-upgrade on login.
- **`EncryptionService`** — every encrypt uses a fresh 16-byte random IV (prepended to the ciphertext).
  A deterministic IV would leak plaintext equality.
- **`JwtService`** — enforces a minimum 64-byte signing key, reads access/refresh lifetimes from config,
  generates refresh tokens via `RandomNumberGenerator.GetBytes(32)`. The caller hashes (SHA-256) before
  DB insert.
- **`RouteAuthorizationMiddleware`** — exception path is **fail-closed** (`_isAuthorized = false` +
  redirect to `/access-denied` + Error log), never falling through to `true` on a catch.
- **`AppAuthStateProvider`** — does not carry the raw JWT in a claim; token access goes through
  `ITokenStore`.
- **`BearerHandler`** — 401 retry: on 401, call `IAuthorizationTokenService.TryRefreshTokenAsync` and
  replay the request once on success.
- **`AuthorizationRateLimiter`** — `IMemoryCache` + `SizeLimit` + thread-safety (bounded, no unbounded
  dictionary growth).
- **`AuthorizationCache`** — key includes `tenantId`, strips query strings, uses `IMemoryCache` +
  `SizeLimit`.
- **`AuthenticationBarrier`** — `TaskCompletionSource<bool>(RunContinuationsAsynchronously)` replaces a
  `SemaphoreSlim(0,1)` race.

### Reliability

- **`Web.Localization` (`SimpleStringLocalizer` + `LocalizationHttpClient`)** — cache-pin fix (`Lazy<T>`
  + force-refresh on lookup miss) and a background cache refresher.
- **`GlobalExceptionMiddleware`** — exception classification by type hierarchy (`DbException`, custom
  `EmailDeliveryException`) instead of locale-dependent `Message.Contains("database")`.
- **`HeaderTenantResolver`** — **advisory only**; tenant authority is the `tid` claim. The header survives
  for dev/test tooling.
- **`BaseValidator`** — `ApplyContextualRules` no longer appends rules on every call.
- **`WhiteListService`** — async-only API; sync-over-async overloads removed.
- **`RestErrorTranslator`** — uses `IHttpClientFactory`, not `new HttpClient()`.
- **`ModuleLoader`** — takes `IServiceCollection` parametrically; silent failures now log + throw.

### Added

- **`Asdamir.Web`** — shared FluentUI components (single version across the board).
- **`Asdamir.Data.HangfireJobs`** — `[DisableConcurrentExecution]`-wrapped base job + DI helpers.
- **`Asdamir.Data.Outbox`** — dispatcher background service + email/sms abstractions.
- **`Asdamir.Core.Modules`** — pluggable module loader for managed apps.

## AppManagement (control plane)

Asdamir also ships a commercial control plane — **AppManagement** — a Blazor admin console + REST API that
registers, configures and operates the apps built on the framework: central identity, roles, permissions,
menus, localization, configuration and logs, held once (scoped per app) and administered from one console.
It is not part of the open-core packages and is versioned and released separately.

## Naming

Package prefix is **`Asdamir.*`** throughout (NuGet ID, namespace) — `Asdamir.Core`, `Asdamir.Data`,
`Asdamir.Web`, `Asdamir.Tools`. The GitHub repo folder is kept as `entframework` on purpose; the
product/brand is **Asdamir**. No source, comment, or test references any brand name from a legacy upstream
integration, and the legacy `Ent*` / `Framework.*` names are fully retired.
