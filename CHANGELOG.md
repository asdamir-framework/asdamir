# Changelog — Asdamir

All notable changes to this repo. Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versioning: [SemVer](https://semver.org/spec/v2.0.0.html).

The open-core packages (`Asdamir.Core`, `Asdamir.Data`, `Asdamir.Web`) share one version via
`Directory.Build.props`; `Asdamir.Payments` is cohort-aligned; the CLI (`Asdamir.Tools`) versions
independently. Current published state (nuget.org): **Core `1.4.0`** · **Data `1.3.0`** · **Web `1.3.1`** · **`Asdamir.Payments 1.2.0`** · **Tools `1.4.0`** (the Gateway background-run primitive + the localization-completeness gate — see the 1.4.0 entry below). Earlier: **Tools `1.3.15`** (generated SQL bracket-quotes every table/column identifier so reserved-word field names stay valid, and the generated `run-tests.sh` keeps a Docker-free default run; generated apps enforce a nonce-based CSP + ship an audit trail; `new entity`/`new page`/`new feature`/`add field` run from the app root + auto-apply the generated migration, with `--no-db` to skip, and print a restart reminder after applying; generated apps bind the auth cookie to a server-side session registry so a restart / re-create ends the session; `rollback app` reads the DB connection from the Gateway user-secret + hides the vault line when the mode is undetermined; generated `restart-<app>.sh` frees the port; `new app` is generate → run: writes the
Gateway dev user-secrets + creates the DB + applies migrations; a profile menu + self-service change-password page in BOTH modes, and the forced first-login change-password flow removed). Data `1.2.1`'s FeatureManager value-type fallback fix shipped **inside Data `1.3.0`** (never published separately).
AppManagement (the commercial control plane) is not packed to NuGet — it ships as a compiled release for
commercial customers.

## [1.4.0 — Core 1.4.0 · Data 1.3.0 · Web 1.3.1 · Tools 1.4.0] — 2026-07-20

### Added — Gateway background-run / progress primitive (Core 1.4.0 + Data 1.3.0)

The reusable way to run a heavy operation off the request thread: **trigger → run in the background → poll
status/progress**, so a long op (a large reconciliation, a bulk import) never blocks a request.
`IBackgroundRunService` (enqueue → RunId; get status = state + progress + result ref + error summary) with an
app-defined `IBackgroundJobHandler` (keyed by a `JobType` string — it can wrap an existing engine without
changing its signature). Durable per-run state (survives a restart — a ghost "Running" is recovered to
`Interrupted` at startup), throttled progress reporting (no per-row DB write), a default reject-duplicate
concurrency policy, and a single-node HA note. Self-hosted `BackgroundService` + channel (no external broker).
`AddBackgroundRuns()` + `AddBackgroundJob<T>()`. See [Background Runs](fundamentals/background-runs.md).

### Added — generator wiring for the background-run primitive (Tools 1.4.0)

`asdamir new app` (BOTH modes) now scaffolds the primitive by default: `AddBackgroundRuns` in the Gateway, a
fail-closed tenant-scoped `GET /background-runs/{id}` status endpoint (another tenant's run id → 404), and the
store migration into the app's own DB. No job handlers are registered — the runner idles until you add one.

### Added — localization-completeness gate (Tools 1.4.0)

- **`audit localization` (AUD015)** — a static gate that cross-checks every `L["…"]` used in `.razor`/`.cs`
  against every seeded key; a key that is unseeded or seeded in fewer than all three cultures is a build
  error (the raw key would otherwise render on screen). Dynamic `L[$"…{x}"]` keys are reported as INFO.
- **`localization verify`** — diffs the seed files' `(key, culture)` pairs against the live database to catch
  a key that is seeded but was never applied (apply-drift).

### Changed — `ApiStringLocalizer` missing-key warning deduped (Web 1.3.1)

The "localization key not found" warning now fires once per (cache-generation, culture, key) instead of on
every resolution, so a missing key surfaces in the logs without flooding them.

### Fixed — FeatureManager value-type fallback (Data 1.3.0, folds in the never-published Data 1.2.1)

The Data `1.2.1` patch (FeatureManager value-type fallback fix) ships inside Data `1.3.0` — it was never
published as a separate package.

## [Tools 1.3.15] — 2026-07-19

### Added — generated apps: profile menu + self-service password change (BOTH modes)

- **Profile menu (topbar):** every generated app's `MainLayout` now renders the avatar/name as a native
  `<details>/<summary>` dropdown (no JS, keyboard accessible, CSP-clean) with **Change Password**
  (→ `/change-password`) and **Sign out** (moved inside the menu — no standalone logout icon). New localized
  keys `App.Shell.UserMenu` / `App.Shell.ChangePassword` seeded in both models (tr/en/ru).
- **Self-service `/change-password` page in BOTH modes** (previously free-only and forced): a neutral
  current + new + confirm card rendered inside the app shell (`current-password`/`new-password`
  autocomplete). Both modes post to the SAME route, **`gateway/auth/change-password`** — the free Gateway
  serves it locally (unchanged endpoint), the commercial Gateway gains a **new proxy** to AppManagement's
  `app-change-password` (appCode injected exactly like the login proxy; failures surface the engine's ONE
  opaque localized ProblemDetails via `ToUserMessageAsync`). On success every refresh token is revoked
  server-side, so the page signs the user out and returns to login with a localized success note
  (`App.ChangePw.Done`).

### Removed — the forced first-login password-change flow (free mode)

- Product decision (Orhan): **no app forces a first-login password change; every app offers self-service
  instead.** The login redirect (`MustChangePassword`), the `AppLoginResponse` flag, the
  `User_GetForcePasswordChange` proc + repository read and the `ForcedHint` copy are gone; the free seed now
  creates the starter admin with `ForcePasswordChange = 0` (the column stays, defaulted off, and
  `User_ChangePassword` still resets it).
- **Trade-off, accepted:** the printed starter/bootstrap password now stays valid until the operator changes
  it — change it promptly via the profile menu (the docs say so explicitly).
- E2E updated: the free-app slice now walks login → **direct dashboard** → profile menu → self-service
  change → revoked sessions → re-login with the new password.

## [Web 1.3.0] — 2026-07-18

### Changed — `AsdamirJsonFilePicker` → `AsdamirFilePicker` (Web; general-purpose, real drag & drop)

The Asdamir.Web file-picker component is renamed and generalized into a **content-agnostic**
`AsdamirFilePicker`. The old component's JSON-specific logic (parse/validate/analyze/preview and its
models) had zero usage — dead code — and is removed; the component now only selects a file and hands the
`IBrowserFile` to the caller (`OnFileSelected` / `OnCleared` — the caller streams the content). Drag & drop
is now REAL: the raw `<InputFile>` stretches invisibly over the styled drop zone (via a `::deep`-scoped
overlay — a child component's rendered input carries no CSS-isolation scope attribute), so click-to-browse
AND a native drop land on the same input — no JS interop. Surface: `Accept` (also enforced component-side,
since a drop bypasses the browse-dialog filter), `MaxFileSize`, `Disabled`, `IsProcessing`, text parameters
with English defaults, a parent-side `Clear()`. Single-file only. This is a breaking rename for any external
consumer of the old component. Published as `Asdamir.Web 1.3.0`.

## [Tools 1.3.14]

### Fixed — reserved-word column names now produce valid SQL (identifier bracket-quoting)

`asdamir new entity` / `new feature` / `add field` now **[bracket]-quote every table and column identifier** in
the generated DDL, CRUD stored procedures, and sample-seed migration. Before this, a field whose name is a
T-SQL reserved word — `RowCount`, `Order`, `User`, `Group`, `Key`, `Percent`, … — emitted a bare
`RowCount INT NOT NULL` column definition, which fails at apply time with *"Incorrect syntax near the keyword
'RowCount'"* so the `CREATE TABLE` never runs. Bracketing is applied to **all** identifiers (not a reserved-word
allowlist, which can never be complete); parameters (`@Name`) are intentionally left unquoted — a reserved word
is valid as an `@`-prefixed parameter name. Verified end to end: a reserved-word entity migration now applies to
a real SQL Server and round-trips, pinned by a `Category=Scaffold` regression test.

### Changed — generated `run-tests.sh` excludes `Category=Integration`/`E2E` from the fast default run

The scaffolded `run-tests.sh` now runs `dotnet test --filter "Category!=Integration&Category!=E2E"`, so once a
developer adds Docker-only Integration or real-browser E2E tests to a generated app, the default fast run stays
Docker-free (run them explicitly with `--filter "Category=Integration"`). A fresh app has no such tests, so the
filter changes nothing for it.

## [Tools 1.3.12]

Generated apps (`asdamir new app`, free + commercial) gain two direct security/observability capabilities,
both proven end to end by the scaffold + browser E2E.

### Added — generated apps now enforce a nonce-based Content-Security-Policy

The Server previously shipped with CSP off. Generated apps now wire `UseCspNonce` + a strict
`Content-Security-Policy` (`script-src 'self' 'nonce-…'`); `App.razor` stamps the same per-request nonce
(URL-safe base64 so header + attribute match) on its single inline `<script>`. The change-password page's
inline toggle folds into that one nonced block (no per-page scripts), and the external Google-Fonts `@import`
is dropped (it violates the strict policy — self-host a woff2 to restore Inter).

### Added — generated apps now have an audit trail

A global `AuditActionFilter` records every state-changing request (POST/PUT/PATCH/DELETE) to the app's OWN
`dbo.AuditLog` — who did what, to which target, with what outcome. `AuditTrailController` serves it at
`GET gateway/admin/audittrail`; endpoints opt out with `[SkipAudit]` and refine the label with `[AuditAction]`.
The `dbo.AuditLog` table + `AuditLog_Insert`/`AuditLog_List` procs ship in the app's schema migration.

## [Tools 1.3.11]

### Fixed — generated apps: a session survived app teardown / restart (server-side session registry) (`Asdamir.Tools`)

A generated app's auth cookie was **self-contained**: the server held no session, so a cookie stayed valid
as long as it decrypted and hadn't expired. Two things combined into a real hole: **(1)** Data Protection
keys persist **outside** the app folder (`~/.aspnet/DataProtection-Keys/`, keyed by the stable
`DataProtection:ApplicationName`), so **deleting an app and re-creating it under the same name reused the same
key ring** and the old cookie still verified; **(2)** cookie auth had no server-side validation, so a restart
or a fresh (empty) DB didn't drop the session — the old cookie **landed on the dashboard without a login**.

The generated Server template now ships a server-side session registry:

- A new **`Auth/AppUserSessionStore.cs`** — an in-memory (singleton) registry of active sessions by `sub`.
- **Sign-in registers** the session (`Login.razor` → `Sessions.Register(sub)`); **sign-out removes** it
  (`AuthEndpoints` `/logout`).
- **`OnValidatePrincipal`** rejects any cookie whose `sub` is not in the store. Because the store is
  in-memory, a **process restart clears it → every cookie is rejected until the user signs in again**, and a
  torn-down + re-created app starts with an empty store.

Verified end-to-end on a free-mode app: same auth cookie returns **HTTP 200** on `/` before a restart and
**HTTP 302 → /login** after.

## [Tools 1.3.10]

### Added — restart reminder after `new entity`/`new page`/`new feature`/`add field` auto-applies a migration (`Asdamir.Tools`)

Now that these commands **apply the migration by default** (1.3.9), the one missing step was easy to forget:
a running generated app **caches its DB-backed menu + localization + config at startup** and **registers new
controllers at startup**, so a freshly-applied page/menu/field does **not** appear until the app is restarted.
That produced the confusing "I added a page but its menu didn't show" — the DB was correct; the app was still
serving its startup cache.

The scaffolders now print a restart reminder **after a migration is actually applied** (not on `--no-db`),
naming the app's own script:

```
✓ Feature 'Order' ready.
  ↻ Restart the app for the change to show (menu/localization is cached at startup):  ./restart-myapp.sh
```

- One reminder per command — `new feature` suppresses the entity/page steps' reminders and prints a single one.
- Locates the app's `restart-<app>.sh` by glob (every generated app ships one); falls back to a generic line.
- Commercial `new page` (whose AsdamirVault seeds are applied separately) prints a matching "then restart"
  note next to its "apply to AsdamirVault" guidance.

## [Tools 1.3.9]

### Changed — `new entity` / `new page` / `new feature` / `add field` run from the app root + auto-apply the migration (`Asdamir.Tools`)

These four commands no longer need you to `cd src/<App>.Gateway` first — run them **from the app root** and
each finds the right project itself (the nearest `.sln`, then `src/<App>.Gateway` / `src/<App>.Server`).
Running from inside the Gateway/Server directory still works (backward-compatible); `--output` /
`--gateway-dir` / `--server-dir` override the auto-detection. If you're not inside an Asdamir app they now
fail fast with *"Not inside an Asdamir app (no .sln found…)"* instead of scaffolding into the wrong place.

They also **apply the generated migration by default** — through the same journaled `db apply` runner,
resolving the connection from the Gateway user-secret `ConnectionStrings:Default` (the passwordless
resolution `db apply` already uses; explicit `--connection`/`-S`/`-d`/`-U`/`-P` override it). So
`asdamir new feature Order …` from the app root scaffolds **and** creates the table in one step — no `cd`,
no separate `db apply`.

- **`--no-db`** (new, on all four) scaffolds files only — don't touch SQL (offline / CI / review-first).
  It prints the exact `asdamir db apply --migrations <path>` line to run later.
- If **no connection is resolvable** (no secret, no flags), the migration is still **generated** and the
  command prints the `db apply` recovery line — never left blind, never a hard failure.
- `--apply` (the old opt-in flag on `new entity` / `new feature`) is now a **deprecated no-op** — apply is
  the default; use `--no-db` to opt out.
- `db apply` is unchanged and stays — it's what you run over the app's lifetime (clone, CI, prod);
  `new entity`/`new feature` just run it once for you at generation. Idempotent (the journaled runner skips
  already-applied migrations).

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
