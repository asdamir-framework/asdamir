# Changelog — Asdamir

All notable changes to this repo. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versioning: [SemVer](https://semver.org/spec/v2.0.0.html).

The open-core packages (`Asdamir.Core`, `Asdamir.Data`, `Asdamir.Web`) share one version via
`Directory.Build.props`; `Asdamir.Payments` is cohort-aligned; the CLI (`Asdamir.Tools`) versions
independently. Current published state (nuget.org): **Core `1.3.0`** (the `Jwt:ConsoleAudience` boundary),
**Data/Web `1.2.0`**, **`Asdamir.Payments 1.2.0`**, **Tools `1.3.8`** (`rollback app` reads the DB connection from the Gateway user-secret + hides the vault line when the mode is undetermined; clearer AsdamirVault wording; generated `restart-<app>.sh` frees the port; `new app` is generate → run: writes the
Gateway dev user-secrets + creates the DB + applies migrations). **Pending publish: Data `1.2.1`** (the FeatureManager value-type
fallback fix; Core stays `1.3.0`, Web/Payments stay `1.2.0`).
AppManagement (the commercial control plane) is not packed to NuGet — it ships as a compiled release for
commercial customers.

## [Unreleased]

## [Tools 1.3.8]

### Fixed — `rollback app`: hide the AsdamirVault line when the mode can't be determined (`Asdamir.Tools`)

When the app **directory was already gone**, `rollback app` couldn't tell free from commercial (the free signal
lives in `db/migrations`), so it fell back to printing the commercial vault lines — a scary, irrelevant
"App registration (AsdamirVault): NOT purged …" for what may well have been a free app that never had a
registration (and nothing could act anyway without a connection). The vault line is now shown **only** when the
app is **known-commercial** (its directory is present and not free-mode) **or** `--vault-connection` was passed
explicitly (honoured in any mode). Undetermined mode + no `--vault-connection` → **silent** (the `App DB` line
stays — it's still actionable with `--connection`). Behaviour unchanged; visibility only.

## [Tools 1.3.7]

### Fixed — `rollback app` resolves the DB connection from the Gateway user-secret (no orphan DBs) (`Asdamir.Tools`)

`rollback app <Name>` said "App DB: SKIP — no connection (database not dropped)" even though `new app` had
prompted for the SQL password and stored it in the Gateway user-secret (`ConnectionStrings:Default`) — the same
value `db apply` reads. So `rollback app` deleted the directory but left the database behind (an orphan). It now
resolves the connection in the **same order as `db apply`** (reusing its resolver, no duplication): explicit
`--connection` → `-S/-d/-U/-P` flags → the **Gateway user-secret**. So `rollback app <Name>` with **no flags**
drops the DB. The secret is read **before** the directory is deleted (it holds the UserSecretsId); the
confirmation shows the resolved server + database and its source (**never** the password), and DROP DATABASE
stays idempotent (`IF DB_ID IS NULL`). Works in free + commercial mode.

## [Tools 1.3.6]

### Fixed — `rollback app`: clearer AsdamirVault wording + hidden in free mode (`Asdamir.Tools`)

`rollback app`'s teardown lines read `AsdamirVault: NOT purged …`, which sounded like the **AsdamirVault
database** could be purged (it can't) and frightened users. Two fixes: (1) the wording is now **"App
registration (AsdamirVault)"** and spells out that it removes the app's **registration + AppId-scoped rows** via
`App_Purge`, **NOT** the AsdamirVault DB (which is never dropped); (2) a **free-mode** app has no control-plane
registration, so the line is **hidden entirely** (no more "N/A — free-mode app" noise). Behaviour is unchanged —
only the messaging and the free-mode visibility. When the mode can't be determined (the app directory is already
gone), the line still shows, with the clear wording.

## [Tools 1.3.5]

### Changed — generated `restart-<app>.sh` frees the PORT, not just the process by name (`Asdamir.Tools`)

The generated restart script killed the old tiers by process name (`pkill -f "<App>.Gateway"/"<App>.Server"`),
which misses the real failure: a **different** app squatting the port (e.g. DemoPay's Server on `7010`) — name-kill
never finds it, so the new app can't bind. Restart stopping is now **two-layered**: the name-targeted kill (kept)
**plus** a **port-targeted** one that frees the Gateway/Server port whoever holds it (`lsof -ti:<port>`, `fuser`
fallback). It **warns before killing another process** ("Port 7010 is held by PID … — stopping it."), then
**verifies the port actually freed** (3×1s, escalating to SIGKILL) and **fails fast** (`exit 1`) rather than a
blind start into a bound port. Ports are parsed from the script's `GATEWAY_URL`/`SERVER_URL` (not hardcoded).

## [Tools 1.3.4]

### Changed — `asdamir new app` creates the database + applies migrations too (generate → run) (`Asdamir.Tools`)

Building on the auto-secret configuration, `new app` now also **sets up the database** — it runs
`db apply --create-database` for you (reusing the SAME journaled runner, no duplication; `CREATE DATABASE` is
idempotent via `IF DB_ID IS NULL`, and applied migrations are skipped). So a free app is **generate → run**:
`asdamir new app` → `./restart-<app>.sh`. It runs only when a real password was supplied (a masked prompt or a
full `--connection-string`); an **empty password** or the new **`--no-db`** flag scaffolds files only and prints
the `db apply` line for later (offline / CI / review-first). If the DB setup fails (server unreachable, no
rights) the files are still generated and the exact `cd <app> && asdamir db apply …` recovery command is printed
— never left half-done. `db apply` itself is unchanged and still used over the app's lifetime.

## [Tools 1.3.3]

### Changed — `asdamir new app` is run-ready: it auto-configures the Gateway dev user-secrets (`Asdamir.Tools`)

`new app` already asks for the SQL user + a masked password; it now **writes the Gateway's dev user-secrets**
itself so the app runs with no hand-editing — cutting the old 6-step "next steps" checklist to **2 commands**
(`asdamir db apply` → `./restart-<app>.sh`). It sets: a **CSPRNG `Jwt:Key`** (free mode only — the Gateway owns
its JWT; commercial mode's `Jwt:Key` must equal AppManagement's, so it stays manual), a **CSPRNG
`Security:EncryptionKey`**, and **`ConnectionStrings:Default`** (only when a real password was supplied — masked
prompt or a full `--connection-string`; an empty password keeps the printed manual line). Secrets go to
**user-secrets, NEVER `appsettings.json`** (the security model is unchanged), and the masked password is never
echoed back. New **`--no-secrets`** flag opts out (CI / external secret store) and restores the full manual
block. `--yes`/CI is unaffected (the CSPRNG keys are still generated; the connection string comes from
`--connection-string` if given).

## [Tools 1.3.2]

### Added — `asdamir rollback app <Name>`: whole-app teardown, the symmetric inverse of `new app` (`Asdamir.Tools`)

`rollback` gained an `app` subcommand that tears down a generated app end-to-end — the inverse of `new app`
(previously there was no CLI path back; you had to `DROP DATABASE` + `rm -rf` by hand). It removes: the generated
**directory** (the dir whose `<Name>.sln` exists — the guard against deleting the wrong dir), the app's **OWN
database** (`DROP DATABASE`, free **and** commercial), and — in commercial mode with `--vault-connection` — the
**AsdamirVault registration** + all AppId-scoped rows via the existing `dbo.App_Purge` proc. DESTRUCTIVE +
interactive by default (shows the full path + server/database + vault code, asks `[y/N]`; `-y`/`--yes` for
scripts). Fail-closed: it NEVER drops a protected DB (`AsdamirVault`/`master`/`model`/`msdb`/`tempdb`) and
`App_Purge` refuses the self-app. Every step is conditional + idempotent (a missing dir/DB/registration is
"already gone", never an error). Implemented as a subcommand, so the bare `rollback <Feature>` form is unaffected.

## [Tools 1.3.1]

### Fixed — `asdamir new app` prompts for SQL auth (user/password), not a Windows-only Trusted_Connection (`Asdamir.Tools`)

The interactive `new app` flow defaulted the connection string to `Trusted_Connection=True` (Windows integrated
auth), which is not portable to Linux/macOS/containers. It now asks for the **SQL user** (default `sa`) and a
**masked SQL password**, and composes a cross-platform **SQL-auth** connection string
(`Server=<host>,1433;Database=<db>;User Id=<user>;Password=…;TrustServerCertificate=True;`). A real password is
never written to `appsettings.json` (left empty, secret-free) — it goes to `dotnet user-secrets` per the printed
next steps (which use the entered user + a `<your-password>` placeholder, so the masked password is never echoed
back). The generated Gateway smoke-test factory's placeholder connection string was made portable too.

## [Data 1.2.1]

### Fixed — `FeatureManager.GetConfigurationAsync<T>` global fallback for value types (`Asdamir.Data`)

- **`GetConfigurationAsync<int>` / `<bool>` / any value type now correctly falls back to the global key.** It
  decided the tenant-scoped key's presence with `Get<T>() is not null`, but a value type binds to `default(T)`
  (0 / false) when the key is ABSENT — so the guard passed and the global fallback was unreachable, returning
  `default(T)` instead of the global value. Now uses `IConfigurationSection.Exists()`. Reference types were
  unaffected. Patch over the published `1.2.0`.

## [Core 1.3.0]

### Added — `Jwt:ConsoleAudience` for a cryptographic control-plane token boundary (`Asdamir.Core`)

- **`JwtService` now supports an optional distinct audience for control-plane tokens.** When
  `Jwt:ConsoleAudience` is configured, a token minted with `token_use=console` is stamped with THAT audience
  instead of `Jwt:Audience`; every other token keeps `Jwt:Audience` unchanged. This lets a host run two
  audience-scoped JWT bearer schemes so a lower-privilege token is rejected at the **authentication layer** on
  control-plane endpoints — not merely by a claim filter. **Additive and backward-compatible**: unset →
  console tokens fall back to `Jwt:Audience` (prior behavior). **Only `Asdamir.Core` is bumped;
  `Asdamir.Data`/`Web`/`Payments` remain `1.2.0` (no code change).**

## [Tools 1.3.0]

### Added — opt-in end-user billing scaffold (`asdamir new app --billing`)

- **`asdamir new app --billing`** (off by default) scaffolds an **end-user payment page** into a generated
  app (commercial mode only). It emits, all fully conditional on the flag:
  - a **Payment page** (`Payment.razor` at `/billing`) — lists plans, shows the current subscription, starts
    checkout with a **redirect to the tenant's Paddle hosted page** (pass-through Merchant-of-Record). When
    Paddle isn't configured yet it shows a calm localized message (never a raw status code / crash). Scoped
    CSS, no inline style, no inline script (CSP-safe).
  - a **Gateway proxy** (`gateway/billing/*` → AppManagement `api/admin/billing/*`) — forwards the bearer
    (whose `app_code` claim AppManagement uses to resolve the app's `AppId`); holds no DB, no Paddle secret.
  - an **AsdamirVault seed** (`db/admin-onboarding/seed_billing.sql`) — the `billing.view` permission + Admin
    grant + `/billing` nav menu row + `Billing.Page.*` / `Menu.Billing` localization (tr-TR/en-US/ru-RU) +
    `Payment:Paddle:*` / `Payment:Crypto:*` config templates (secrets seeded empty + encrypted, per-tenant).
- **Backward-compatible:** WITHOUT `--billing` a generated app is byte-identical to before — not one billing
  file is emitted.
- **`--billing --mode free` (Model B) is supported** — a free app gets **self-contained** billing: the
  Gateway serves billing LOCALLY from the app's own DB via the new open-core **`Asdamir.Payments`** package
  (`LocalDbBillingStore` + Paddle/crypto rails + local webhook) — no control plane, no central secret. It
  emits a local Gateway billing + webhook controller, the shared Payment page, and the app-own-DB billing
  schema/procs/seed migrations. Model A (commercial) output is unchanged.

## [Asdamir.Payments 1.2.0]

### Added — new package: the payment rails

- **`Asdamir.Payments`** (nuget) — the concrete payment plumbing on top of `Asdamir.Core`'s `IPaymentProvider`
  contract: `PaddlePaymentProvider` (Merchant-of-Record, pass-through — each tenant/app connects its own
  account) + a default-off crypto provider, a `PaymentService` facade, a store-agnostic `BillingWebhookProcessor`,
  the operational `IBillingStore`, the shared billing DTOs, an `AddPayments(...)` DI extension, and
  **`LocalDbBillingStore`** (app-local, single-tenant, over Core's `IDbConnectionFactory`). Free-mode apps
  consume it for self-contained (Model B) billing. Cohort-aligned at `1.2.0`.

- **FluentUI v5 migration** (`feature/fluentui-v5`) — pinned to the v5 RC (`5.0.0-rc.4-26180.1`); awaiting
  GA before merge to `main`. Not yet released.
- **No CI/CD** — `.github/workflows/*` were removed (GitHub Actions billing). The process is plain
  `git pull` / `git push` to `main`; verify locally before every push (`dotnet build` 0 warnings ·
  `./run-tests.sh` · `audit lint`).

## [Tools 1.2.2] — 2026-07-08

### Added
- **`audit lint` gains `AUD013` — the inline-style gate.** A raw inline `style="…"` attribute in a
  `.razor`/`.sbn` file now fails the audit (use scoped `.razor.css` classes instead — the framework's CSS
  isolation convention). `audit lint` now scans `.razor` + `.sbn` as well as `.cs`; each rule declares its
  file types. **Exempt:** the CSS-variable pattern `style="--x:@value"` (the supported way to pass a
  per-item dynamic value into scoped CSS) and a FluentUI `Style="…"` component parameter (capital `S`).

### Changed
- Framework auth components (`AccessDenied`, `ForgotPasswordDialog`, `ResetPasswordComponent`,
  `SessionWarningDialog`) moved their inline styles to scoped `.razor.css` — no visual change. The unused
  `Style` passthrough parameter on `AsdamirContentCard` was removed (use `AdditionalClass` + scoped CSS).

## [1.2.0] — 2026-07-08  ·  Core / Data / Web

### Added
- **`IPaymentProvider` + `PaymentProviderOptions` (`Asdamir.Core`)** — a new open-core abstraction for
  payment rails: `CreateCustomer` / `CreateCheckoutSession` / `CreateSubscription` / `CancelSubscription`
  / `VerifyWebhook` / `Refund`, all returning `Result<T>` (no exceptions on expected failure), with
  bind-time options. No SDK dependency — implement it to plug in your own rail. (This additive public API
  is the minor-version driver, `1.1.2` → `1.2.0`.)

### Changed
- **Public XML-doc gate is ON for all of open core (`Asdamir.Core` + `Asdamir.Data` + `Asdamir.Web`).**
  Every public member now carries an XML-doc comment; `GenerateDocumentationFile=true` with CS1591 no
  longer suppressed, so a new undocumented public member fails the build. Full IntelliSense coverage for
  consumers. Docs-only — no behaviour change.

## [1.1.2] — 2026-07-04  ·  Core / Data / Web

- **`Asdamir.Core`** — token-audience support: `IJwtService.IssueTokens` gains optional `tokenUse` /
  `appCode` parameters that emit `token_use` (control-plane vs app) and `app_code` claims, so a
  control-plane endpoint can reject an app-login token at the authorization layer.
- **`Asdamir.Web`** — removed unused code: the uncalled `IRateLimitService.GetLimitInfoAsync` /
  `RateLimitInfo`, and the dead `DatabaseDynamicResourceStore` (the active DB-backed localizer is
  `LocalizationHttpClient`). No behaviour change.
- **`Asdamir.Data`** — no change; republished at 1.1.2 to keep the Core/Data/Web trio version-aligned.
- Still FluentUI **v4**-based — the v5 migration stays on its branch until GA.

## [Tools 1.2.1] — 2026-07-07

- **`Asdamir.Tools` 1.2.1 — free-mode auth hardening + first-login password change** (PATCH;
  backward-compatible — only the generated **free-mode** output changes, a `commercial` app is unchanged).
  - **Constant-time login (no user-enumeration):** a free app's login pays the same password-hash verify
    cost on an unknown email as on a known one, so response timing no longer reveals whether an account
    exists.
  - **Seeded config now actually applies:** a free app synchronously loads its own `AppConfigurations` at
    startup, so seeded `RateLimiting:*` / `Security:*` / OTP settings reach the rate-limiter and options
    instead of silently falling back to code defaults.
  - **Fail-closed at-rest key:** a free app now throws at startup if `Security:EncryptionKey` is missing
    (like `Jwt:Key`) — no demo-key fallback; the quick-start banner adds the key step.
  - **First-login forced password change:** a free app ships a `ForcePasswordChange` flag on the starter
    admin; login reports it, the UI redirects to a new change-password page, and the endpoint verifies the
    current password, sets the new one, clears the flag, and revokes existing sessions.
  - **Cleaner quick-start:** `new app` no longer prints a plaintext database password — it uses the
    passwordless `db apply` (which resolves the connection from the Gateway user-secret).

## [Tools 1.2.0] — 2026-07-06

- **`Asdamir.Tools` 1.2.0 — a new self-contained FREE app mode** (MINOR; backward-compatible, the default
  is still `commercial`). `asdamir new app <Name> --mode free|commercial` (default `commercial`) picks where
  a generated app's identity/RBAC/menu/localization/config live:
  - **free** = the app is self-contained with **no control plane**. The management tables + `AppId`-free,
    single-tenant stored procs are emitted into the app's **own** database (schema + procs + a seed for the
    starter admin, Admin role, permissions, Dashboard menu, config and localization), and the Gateway
    **issues + validates its own JWTs** (Asdamir.Core `JwtService`) and serves auth/menu/localization/
    client-settings locally. Login gate is "user exists + active"; logging is file + console.
  - **commercial** (default) = unchanged: identity/menus/permissions/localization/config live centrally in
    `AsdamirVault`, managed from AppManagement; the Gateway proxies to it.
- **`db apply` passwordless connection fallback** — when no connection flags are given, it resolves
  `ConnectionStrings:Default` from the Gateway user-secret (via the `*.Gateway` project's `UserSecretsId`),
  then the `ConnectionStrings__Default` env var. So `asdamir db apply --create-database` works with no SQL
  password on the command line. Explicit flags still take precedence.
- **`new feature` / `new page`** in a free app emit the menu/permission + localization seeds as
  `V*__freemode_{menu,localize}_<plural>.sql` migrations into the app's own `db/migrations` (no
  `--vault-connection`). **`rollback`** in a free app tears those down symmetrically over the app
  connection (menu/permission/grants + localization + seed-journal + the seed migration files).
- **Scaffolding polish:** the onboarding banner is mode-branched (free wording; zero `AsdamirVault`/
  `AppManagement` references), `register_<app>.sql` is emitted for commercial only, generated apps pin
  `Asdamir.Core/Data/Web = 1.1.2` (published — was a `0.1.0-preview.*` float that broke restore), and the
  sample-seed rows are English (`Sample <Field> N`). Core/Data/Web unchanged at 1.1.2.

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
