# CLI — `Asdamir.Tools`

**Package:** `Asdamir.Tools` · **Command:** `asdamir`

## Introduction

`Asdamir.Tools` is a `dotnet`-based command-line tool that (1) **scaffolds** code following the framework's audited conventions and (2) runs **`audit-lint`**, the static-analysis gate (run it locally before every push; it also runs in CI when CI is enabled).

### Install as a global tool

The packaged command is `asdamir <command>` (formerly `framework`).

```bash
dotnet tool install -g Asdamir.Tools
#   update later:  dotnet tool update -g Asdamir.Tools
```

<!-- published-versions:begin — GENERATED from src/Asdamir.Tools/published-versions.json by
     packaging/sync-published-versions.sh. Do NOT hand-edit: edit the manifest and re-run. -->
| Package | Published on nuget.org | Next (in this repo) |
| --- | --- | --- |
| `Asdamir.Core` | `1.9.0` | — |
| `Asdamir.Data` | `1.5.0` | — |
| `Asdamir.Payments` | `1.2.0` | `1.3.0` built, pending publish |
| `Asdamir.Tools` | `1.8.0` | — |
| `Asdamir.Web` | `2.1.0` | — |

*A "Next" ahead of the published column is the normal pre-publish state — the version is built here but
not pushed yet. The published column is what a fresh `asdamir new app` pins.*
<!-- published-versions:end -->

## Exit codes — NORMATIVE, and the same model for every command

A third party's script may rely on this section. It is the **single** statement of the contract; a command's
own help text and its `Exit codes:` line below derive from it and may not contradict it.

**The model — two disjoint kinds of code:**

| Kind | Codes | Meaning |
|---|---|---|
| **Result** | `0`, `1`, and for `verify-archive` also `2`–`4` | The command ran and is reporting **what it examined**. Each command owns a small result band starting at `0`; see its own section. |
| **Usage** | **`64`** | The command was **invoked wrongly**, so it examined nothing and is making **no statement at all**. (`EX_USAGE`, sysexits(3).) |
| **Internal** | **`70`** | The tool itself failed unexpectedly. A **bug report**, not a supported outcome. (`EX_SOFTWARE`.) |

**The rule, in one sentence: a result code is possible only when a command handler produced it.** Everything
else — an unknown or mistyped flag, a required option omitted, an option given without its value, a value the
option cannot parse, a stray argument, **and `--help` / `--version`** — exits `64`.

`--help` exiting `64` is deliberate and against the usual convention. It follows from the rule rather than
from taste: `0` is a *claim* (a clean gate, a verified archive), and printing help establishes no such claim,
so it cannot borrow the code. If you script `--help`, treat `64` as its success.

### Side effects follow the same rule

A command that writes or deletes files must leave the filesystem **untouched** when it exits `64`. The two
are separate promises and they came apart: **`asdamir new app` with no name, in a non-interactive shell,
used to scaffold a complete application and exit `0`** — the name prompt fell back to the placeholder
`GeneratedApp` when no console was attached, and `--yes` did the same. A missing name is now a usage error
and nothing is written.

**`--yes` accepts DEFAULTS, not placeholders.** Every other prompted input derives from something (the
projects from the name, the database from the name, the SQL host from `localhost`); the app name derives
from nothing, so there is nothing to accept on your behalf. Pass it explicitly in scripts:
`asdamir new app <Name> --yes`.

**A mistyped option in the name position is reported as a mistyped option.** `asdamir new app --bogus` hands
`--bogus` to the command as the *name*; it now says so, instead of reporting that your capitalisation is
wrong — which sent you to inspect a name that was never the problem.

`3` is a **result**, not a usage error: a scaffolding command exits `3` when it refuses to write into a
non-empty target. The invocation was fine; the command examined the target and declined.

### Why the rule is worded positively

Not "these parser errors return 64" but "only a handler may return a result". An enumerated list of today's
parser errors goes blind the moment the parser grows a new pre-handler outcome — which is exactly how the
previous version of this contract went blind, and the failure was silent in the dangerous direction.

### What this replaces — read if you pin an older version

Before **`Asdamir.Tools 1.6.0`**, usage errors that `System.CommandLine` answered *before* the handler ran
leaked into the result band as **`1`**, and the handler's own usage errors used **`2`**:

| Invocation | ≤ 1.5.1 | which meant | ≥ 1.6.0 |
|---|---:|---|---:|
| `audit lint --pth src` (typo) | `1` | **"findings — build fails"** | `64` |
| any gate, required option omitted / no value / stray argument | `1` | **"findings"** | `64` |
| `--version` on a subcommand | `1` | **"findings"** | `64` |
| `--help` | `0` | **"clean"** | `64` |
| `audit seeds --path ""` | `1` + **stack trace** | crash | `64` + one line |
| `db apply` from the wrong folder | `0`/`1` + **stack trace** | crash | `64` + one line |
| any handler-raised bad argument | `2` | bad args | `64` |

So a mistyped audit invocation used to be indistinguishable from a **failing gate**, and `--help` from a
**clean run**. **`2` is no longer produced by any command.** It could not become the single usage code
because in `verify-archive` `2` is a verdict (`DIGEST_MISMATCH`) — the CLI would have needed
"`2` everywhere except one command", and a rule with an exception is the kind that gets misremembered.

**Stack traces are gone from user-facing failures.** In an audit tool they leak absolute paths and internals
a third party did not ask for, and they read as "the product is broken" when the real cause was a mistyped
argument. Errors now print one sentence; `70` says explicitly that it is *not* your invocation.

## Quick start

Scaffold and run your own app end-to-end. This is the **one canonical flow** — the same everywhere.

**Prerequisites:** **.NET 10 SDK** · **SQL Server** on `localhost:1433` (local or Docker).

```bash
# 1) Install the CLI (once)
dotnet tool install -g Asdamir.Tools

# 2) Create your app — runs ANYWHERE; the app is born in the current folder
mkdir my-apps && cd my-apps
asdamir new app DemoApp --mode free       # files + dev secrets + database + migrations; starter admin printed once

# 3) Run it
cd DemoApp && ./restart-demoapp.sh        # → https://localhost:7010 — sign in with the starter admin

# 4) Add a feature — from INSIDE the app folder
asdamir new feature Product --fields "Name:string,Price:decimal,InStock:bool"
./restart-demoapp.sh                      # → Product appears in the nav menu

# 5) Undo the whole app
asdamir rollback app DemoApp
```

> **`asdamir new app` runs anywhere** (the app is created in the current directory). **Every other command —
> `new feature`, `new entity`, `add field`, `rollback` — runs from _inside_ the app folder.** There is no
> `cd src/<App>.Gateway` and no separate `db apply`: `new app` sets up secrets + DB + migrations, and
> `new feature` applies its migration for you — then `./restart-<app>.sh` picks up the change.

`--mode free` (above) is **self-contained** and the fastest way to start. **Commercial mode**
(`--mode commercial`, the default) keeps identity/menus/config centrally in **AppManagement** and needs more
setup — see [free vs commercial mode](#asdamir-new-app-free-vs-commercial-mode).

## Scaffolding

| Command | Generates |
|---|---|
| `new app <Name>` | A full application (Server + Gateway) wired to the framework — including `Properties/launchSettings.json` on fixed dev ports (Gateway `7001` = the Server's `Gateway:BaseUrl`, Server `7010`) so `dotnet run` starts on a known port out of the box instead of the default `:5000`. **`--mode free\|commercial`** (default `commercial`) picks the identity model — see [free vs commercial mode](#asdamir-new-app-free-vs-commercial-mode). **`--billing`** (opt-in, off by default) adds an end-user payment page + billing proxy + seeds — see [billing](#asdamir-new-app-billing). The Gateway ships a global **audit trail** (every state-changing request → `dbo.AuditLog`, served to the control plane at `gateway/admin/audittrail`) and the Server enforces a **nonce-based Content-Security-Policy** by default. |
| `new entity <Name>` | Entity + DTO + repository + service + controller + tests + a create migration + an **idempotent sample-seed migration**. The generated tests (since 1.1.0) cover create/get round-trip, validator rejection, delete, **update round-trip**, **list**, and an **API auth-guard** (`GET` without a token → 401) — all DB-free (in-memory fake repo + `WebApplicationFactory`). The seed migration (since 1.1.1) writes 3 typed sample rows (guarded by `IF NOT EXISTS`, `TenantId='default'`) so the entity's grid is populated after `db apply` instead of empty. Every generated table/column identifier is **`[bracket]`-quoted** (since 1.3.14), so a field named after a T-SQL reserved word (`RowCount`, `Order`, `User`, `Group`, `Key`, …) still produces valid SQL. |
| `new page <Name>` | A Blazor CRUD page (DataGrid + edit dialog + delete confirm) **plus** its localization seed (`localize_<plural>.sql`) and an idempotent, role-based **menu + permission seed** (`seed_menu_<plural>.sql`, AppId-scoped). `--icon` sets the nav icon. See [below](#asdamir-new-page-localization-menu-permission-seeds). |
| `new feature <Name>` | **The one-command path for a complete CRUD feature** — an entity slice (Gateway) + a CRUD page (Server) + the menu/permission & localization seeds, in one go. Runs from the app root and **applies the entity migration automatically** (`--no-db` to skip); `--vault-connection` also applies the AsdamirVault menu/permission seed. See [below](#asdamir-new-feature). |
| `new module <Name>` | A self-registering [module](fundamentals/modules.md) project |
| `new mobile <Name>` | A .NET MAUI **Blazor Hybrid** app — `.Mobile` (Android host) + `.Mobile.Shared` (UI) + `.Mobile.Data` (SQLite) + tests. Login, left nav drawer, dashboard; talks to the app's Gateway. See [Mobile App](mobile.md). |
| `add field <Name>` | Adds a field across the entity/DTO/repository/migration set. Runs from the app root and **applies the ALTER migration automatically** (`--no-db` to skip). |

> The verb comes first: **`asdamir new app|entity|page|feature|module|mobile`**, **`asdamir add field`**, **`asdamir rollback`** (the inverse of `new feature`), and **`asdamir rollback app`** (the inverse of `new app` — whole-app teardown).

Generated output uses the framework's templates (`src/Asdamir.Tools/Templates/*.sbn`) and references the `Asdamir.*` packages.

> **Mobile build:** the MAUI app needs `dotnet workload install maui-android` + an Android SDK platform
> (`dotnet build … -t:InstallAndroidDependencies -f net10.0-android -p:AcceptAndroidSDKLicenses=true`),
> then build a **single RID** — `dotnet build … -f net10.0-android -r android-arm64` (a plain multi-RID
> build fails with `NETSDK1047`). Full recipe in [Mobile App → Build & run](mobile.md#build-run).

### `asdamir new app`: free vs commercial mode

`new app` takes **`--mode free|commercial`** (default `commercial`). It picks **where a generated app's
identity, RBAC, menu, localization and config live** — the app's business layering is identical either way
(a Server/UI tier that only ever calls its Gateway/API tier).

| | `--mode commercial` (default) | `--mode free` |
|---|---|---|
| Management data (users, roles, permissions, menus, localization, config) | **Central**, in the AppManagement control plane, scoped by the app's `AppId` | **Local**, in the app's **own** database, single-tenant (the app *is* the tenant) |
| Login / JWT | AppManagement issues the JWT; the Gateway validates it | The **Gateway issues + validates its own JWT** (Asdamir.Core `JwtService`) |
| Gateway auth / menu / localization / client-settings | **Proxy** to AppManagement | **Local** — served straight from the app's own DB |
| Login gate | Per-app role grant (`UserAppRoles`) | "user exists + is active" (single app — no cross-app matrix) |
| Logging | DB structured log + the central cross-app console | File + console (`ILogger`) — no DB log sink |
| Runs standalone? | **No** — needs AppManagement running | **Yes** — fully self-contained; **no control plane** |
| Onboarding | `register_<app>.sql` against the control-plane DB | Emitted as ordinary migrations into the app's own DB — **no register step** |

Commercial is the default and is unchanged; **free** is the self-contained option. In free mode `new app`
emits an extra set of migrations (the management schema/procs + a seed for a starter admin, the Admin role
+ permissions, the Dashboard menu, config and localization) into the app's own `db/migrations`, so a single
`db apply` sets the whole thing up. It does **not** emit `register_<app>.sql` (there is no control plane to
register against), and its onboarding banner reflects the self-contained flow.

**Free quick-start** (verified end-to-end, with AppManagement not running) — **generate → run.** The interactive
`new app` asks for the SQL user + a **masked** password, then does everything to make the app run-ready:
auto-configures the Gateway's dev user-secrets (a CSPRNG `Jwt:Key` — free mode owns its JWT — plus
`Security:EncryptionKey` + `ConnectionStrings:Default`, in **user-secrets, NEVER `appsettings.json`**) **AND
creates the database + applies every migration** (reusing the `db apply --create-database` runner — idempotent).

```bash
asdamir new app MyApp --mode free          # asks SQL user + masked password → secrets + DB created + migrations applied
cd MyApp && ./restart-myapp.sh             # starts both tiers → open the Server, sign in with the starter admin printed by `new app`
```

- **`--no-db`**: scaffold files only — don't touch SQL (offline / CI / review-first). `new app` then prints the
  `asdamir db apply --create-database --migrations db/migrations` line to run when you're ready.
- **Empty password** (defer it): the DB can't be set up (no connection), so it's skipped like `--no-db`, and
  `new app` prints the `ConnectionStrings:Default` + `db apply` lines.
- **DB setup failure** (server unreachable, no rights): the files are still generated — `new app` prints the
  exact `cd <app> && asdamir db apply …` recovery command (never left half-done).
- **`--no-secrets`**: skip the user-secrets auto-config (manage them yourself); combine with `--no-db` for a
  pure files-only scaffold. **`--yes`/CI**: a password only via `--connection-string`; the CSPRNG keys are still
  generated.
- **Commercial mode** is the same, minus one thing the CLI can't know: `Jwt:Key` **must equal AppManagement's**
  signing key, so it stays a manual step (the Gateway only validates tokens AppManagement issued);
  `Security:EncryptionKey` + `ConnectionStrings:Default` + the DB are still auto-set-up, and you also run
  `register_<app>.sql` against AsdamirVault.

> `asdamir db apply` isn't going away — it's what you run over the app's lifetime (after `new entity`, when a
> teammate clones the repo, in CI, in prod). `new app` just runs it **once** for you at creation.

The starter admin's email + password are printed once by `new app`. Since **1.3.15** there is **no forced
first-login change** in any mode (product decision: no app forces it; every app *offers* it) — sign-in lands
directly on the dashboard, and the printed starter password stays valid until you change it, so **change it
promptly** via the profile menu.

**Self-service password change (BOTH modes, since 1.3.15).** Every generated app's top-right **profile
menu** (avatar/name dropdown; sign-out lives there too) has a **Change Password** item → `/change-password`:
current password + new + confirm. The page posts to the app's own Gateway at
`gateway/auth/change-password` — **free mode** serves it locally from the app's own DB; **commercial mode**
proxies it to AppManagement's `app-change-password` (the credential is central; failures come back as ONE
opaque localized message so accounts can't be probed). On success **every refresh token is revoked** and the
user is signed out to re-authenticate with the new password. Add features exactly as in commercial mode with
`asdamir new feature …` (see the free-mode note under it).

**Session lifetime — a restart (or a delete + re-create) ends every session.** The generated Server ties
each auth cookie to a **server-side session registry** (`Auth/AppUserSessionStore.cs`, an in-memory
singleton): sign-in registers the session, sign-out removes it, and `OnValidatePrincipal` rejects any cookie
whose user isn't in the registry. Because the registry is in-memory, **restarting the app (or tearing it down
and re-creating it under the same name) clears it → every outstanding cookie is rejected until the user signs
in again**. This closes a subtle gap: Data Protection keys persist outside the app folder
(`~/.aspnet/DataProtection-Keys/`, keyed by `DataProtection:ApplicationName`), so without the registry a
self-contained cookie would still decrypt after a re-create and land on the dashboard with no login.

**Background-run infrastructure (BOTH modes, default-on, since 1.4.0).** Every generated app scaffolds the
API-tier [background-run primitive](fundamentals/background-runs.md) — the reusable way to run a heavy
operation off the request thread (**trigger → run in the background → poll status/progress**), so a long op
(e.g. a large reconciliation, a bulk import) never blocks a request. `new app` wires it into the Gateway by
default: `builder.Services.AddBackgroundRuns(builder.Configuration)` in `Program.cs`, a fail-closed
tenant-scoped **status endpoint** `GET /background-runs/{id}` (`[Authorize]`; another tenant's run id returns
404, never leaks), and the store migration `db/migrations/V*__background_runs.sql` into the app's **own**
business DB (applied by `asdamir db apply`). It registers **no job handlers** — the runner idles until you
add one. To use it, implement `Asdamir.Core.BackgroundRuns.IBackgroundJobHandler` (keyed by a `JobType`
string; it can wrap an existing engine without changing its signature), register it in `Program.cs` with
`builder.Services.AddBackgroundJob<MyJobHandler>()`, then enqueue from a controller via
`IBackgroundRunService.EnqueueAsync(...)` and poll the status endpoint. Single-node today (see the
[background-run fundamentals page](fundamentals/background-runs.md) for the HA caveat). Generated apps
therefore pin **Asdamir.Core `1.4.0` / Data `1.3.0`** (the primitive's home).

### `asdamir new app`: billing

`new app` takes an opt-in **`--billing`** flag (off by default). Without it a generated app is exactly as
before — not a single billing file is emitted. With it, the app gains an **end-user payment page** so an
app's own users can subscribe/pay:

```bash
asdamir new app MyApp --billing --yes
```

`--billing` works in **both** modes; the wiring differs:

- **Commercial (Model A)** — the billing data and the payment secret live centrally in AsdamirVault, scoped
  by the app's `AppId`, reached through AppManagement. The generated app never touches that DB and never holds
  the Paddle secret; its Gateway **proxies** `gateway/billing/*` → AppManagement.
- **Free (`--mode free`, Model B)** — self-contained: the Gateway serves billing **locally** from the app's
  **own** database via the open-core **`Asdamir.Payments`** package (`LocalDbBillingStore` + the Paddle/crypto
  rails + a local webhook). No control plane, no central secret — the app owns its own Paddle config.

`--billing` (commercial, Model A) emits three things, all fully conditional on the flag:

| Piece | Where | What it does |
|---|---|---|
| **Payment page** | `src/<App>.Server/Components/Pages/Payment.razor` (+ `.razor.css`) | The end-user checkout UI at `/billing`: lists plans, shows the current subscription, and starts checkout. Checkout **redirects to the tenant's Paddle hosted page** (pass-through Merchant-of-Record). If Paddle isn't configured yet, it shows a calm localized message (never a raw status code / crash). |
| **Gateway proxy** | `src/<App>.Gateway/Controllers/BillingController.cs` | Forwards `gateway/billing/*` → AppManagement's `api/admin/billing/*` (bearer forwarded; AppManagement resolves the app's `AppId` from the token's `app_code` claim). No DB, no secret here. |
| **Seed** | `db/admin-onboarding/seed_billing.sql` | The `billing.view` permission + Admin grant + the `/billing` nav menu row + `Billing.Page.*` / `Menu.Billing` localization (tr-TR / en-US / ru-RU) + the `Payment:Paddle:*` / `Payment:Crypto:*` config templates (secrets seeded **empty + encrypted** — set this app's own Paddle keys, then checkout goes live). |

Apply `seed_billing.sql` **after** the app is registered — it needs the app's `AppId` to exist. Each tenant
connects its **own** Paddle account (pass-through: the framework is not in the money path). The crypto rail
ships **default-off** with a TR-buyer geo-gate.

`--billing --mode free` (Model B) instead emits, into the app's **own** tiers (no control plane, no proxy):

| Piece | Where | What it does |
|---|---|---|
| **Payment page** | `src/<App>.Server/Components/Pages/Payment.razor` (+ `.razor.css`) | The same end-user page at `/billing` — here its `gateway/billing/*` calls are served **locally** by the Gateway (not proxied). |
| **Local billing API** | `src/<App>.Gateway/Controllers/BillingController.cs` + `BillingWebhookController.cs` | Serve plans/subscription/checkout/cancel + the Paddle webhook **locally**, backed by `Asdamir.Payments` (`LocalDbBillingStore` over the app's own DB, single-tenant). |
| **App-DB billing migrations** | `db/migrations/V*__freemode_billing_{schema,procs,seed}.sql` | The single-tenant billing tables + procs + a starter plan seed, applied by the app's own `asdamir db apply`. |

The free app reads its **own** `Payment:Paddle:*` config (user-secrets / env) — it never holds a central
secret. The Gateway pins `Asdamir.Payments` (published on nuget alongside Core/Data/Web).

### `asdamir new feature`

`new feature` is `new entity` **and** `new page` **and** the menu/permission seed in a single command —
the fast path when you want a complete, navigable CRUD feature rather than wiring the three steps by
hand. It locates the app from the nearest `.sln` and routes each part to the right project:

- **Entity → the Gateway/API project** (detected by its `Controllers/` or `db/migrations/`):
  `Domain/<Name>.cs`, `Dtos/<Name>Dto.cs`, `Repositories/I<Name>Repository.cs` + `<Name>Repository.cs`,
  `Services/I<Name>Service.cs` + `<Name>Service.cs`, `Controllers/<Plural>Controller.cs`,
  `Validators/<Name>DtoValidator.cs`, the create + sample-seed migrations, and the entity tests.
- **Page → the Server/UI project** (detected by its `Components/Pages/`): `Components/Pages/<Plural>List.razor`,
  `<Name>EditorDialog.razor`, and the UI tier's own `Dtos/<Name>Dto.cs` (layering — the page keeps its own DTO).
- **Menu/permission + localization seeds → `db/admin-onboarding/`**: `seed_menu_<plural>.sql` (a
  `<plural>.view` permission + an Admin-role grant + a guarded, AppId-scoped `dbo.Menus` row) and
  `localize_<plural>.sql` (the `Page.*` / `Field.*` / `Menu.*` keys in all three cultures).

| Option | Meaning |
|---|---|
| `<Name>` | PascalCase entity name (e.g. `Supplier`). |
| `--fields` / `-f` | `Name:type,...` — same syntax as `new entity`/`new page` (required). |
| `--route` / `-r` | Page route. Defaults to `/<plural-lowercase>`. |
| `--icon` / `-i` | Nav-menu icon for the generated menu row. Defaults to `list`. |
| `--policy` / `-p` | Authorization policy applied to the page. Defaults to `AdminAccess`. |
| `--namespace` / `-n` | Root namespace override (defaults to each project's namespace). |
| `--output` / `-o` | App root (nearest ancestor with a `.sln`). Defaults to the current directory. |
| `--gateway-dir` / `--server-dir` | Override the auto-detected Gateway / Server project directories. |
| `--no-db` | Scaffold files only — don't apply the entity migration (offline / CI / review-first). |
| `--connection`/`-c`, `--server`/`-S`, `--database`/`-d`, `--user`/`-U`, `--password`/`-P` | App-DB connection override (the same flags as `db apply`; defaults to the Gateway user-secret). |
| `--vault-connection` | AsdamirVault connection — applies the menu/permission + localization seeds to AsdamirVault too. |

**Two databases — the entity migration is applied by default, the vault seeds are opt-in:**

- The **entity migration** goes to the **app's own (business) DB** via the journaled `db apply` runner,
  **automatically** — the connection resolves from the Gateway user-secret (override with `--connection`
  or `-S`/`-d`/`-U`/`-P`). Pass **`--no-db`** to scaffold files only. If no connection is resolvable the
  migration is still generated and the command prints the `db apply` recovery line.
- The **menu/permission + localization seeds** go to **AsdamirVault** only when you pass
  `--vault-connection` (explicit — there is no connection guessing). Without it the seeds are still
  generated and the command prints how to apply them later.

> **In a free-mode app** (one generated with `new app --mode free`), `new feature` detects it and emits the
> menu/permission + localization seeds as ordinary migrations into the app's **own** `db/migrations`
> (`V*__freemode_menu_<plural>.sql` + `V*__freemode_localize_<plural>.sql`) instead of AsdamirVault scripts.
> They are applied by the app's normal `asdamir db apply` — **`--vault-connection` is not used** (there is no
> control plane). `new page` behaves the same way in a free app.

**Authorization:** the menu row is gated by the role-based `<plural>.view` permission, and the seed grants
it to the **Admin** role (which also holds `admin.access`, so admins see every menu). Other roles/users are
granted from the AppManagement UI — generation does not touch them.

```bash
cd MyApp                                   # the app root — the entity migration is applied automatically
asdamir new feature Supplier \
  --fields "Name:string,Phone:string,Email:string" \
  --route /suppliers --icon truck \
  --vault-connection "Server=localhost;Database=AsdamirVault;User Id=sa;Password=<pwd>;TrustServerCertificate=True"
```

**Fail-fast:** if the entity step fails the page is **not** generated — fix the error and re-run
(generation is idempotent: existing files are skipped, never overwritten). Afterwards, translate the
`tr-TR`/`ru-RU` values in `localize_<plural>.sql` (the generator seeds the English name as the default for
all three cultures). To undo a feature, see [`rollback`](#rollback).

**Restart the app when it's done.** `new feature` prints a single `↻ … ./restart-<app>.sh` reminder after it
applies the migration — the running app caches its menu/localization at startup, so the new page's menu won't
appear until you restart. (In commercial mode, apply the AsdamirVault seeds first, then restart.)

### `asdamir new entity` — runs from the app root, auto-applies the migration

`new entity` **runs from the app root** — you no longer `cd src/<App>.Gateway` first. It finds the Gateway
project itself (the nearest `.sln`, then `src/<App>.Gateway`) and writes the entity slice + create/sample-seed
migrations **there**. Running it from inside the Gateway directory still works (backward-compatible); pass
`--output` / `--gateway-dir` to point it elsewhere.

```bash
cd MyApp                                              # the app root — no cd into src/…
asdamir new entity Supplier --fields "Name:string,Phone:string"
```

By default it **also applies** the create + sample-seed migrations immediately, through the same journaled
`db apply` runner — resolving the connection from the **Gateway user-secret** `ConnectionStrings:Default`
(the same passwordless resolution as `db apply`; explicit `--connection`/`-S`/`-d`/`-U`/`-P` override it).
No `cd`, no separate `db apply` — the new table is ready when the command returns.

- **`--no-db`**: scaffold files only — don't touch SQL (offline / CI / review-first). It prints the exact
  `asdamir db apply --migrations <path>` line to run when you're ready.
- **No connection resolvable** (no secret set, no flags): the migration is still **generated** — the command
  prints the `db apply` recovery line instead of failing (never left blind).
- It does **not** create the database (the app DB already exists by the time you add entities). Idempotent:
  the journaled runner skips an already-applied migration.
- **After it applies, restart the app.** A running generated app **caches its DB-backed menu + localization +
  config at startup** and **registers new controllers at startup**, so a freshly-applied page/menu/field does
  **not** show until a restart. The command prints a reminder naming the app's own script
  (`↻ … ./restart-<app>.sh`); run it (don't hand-kill/re-run). This is the #1 cause of "I added a page but its
  menu didn't appear" — the DB is correct; the app is still serving its startup cache.

`asdamir db apply` isn't going away — it's what you run over the app's lifetime (a teammate clones the repo,
CI, prod). `new entity` just runs it **once** for you at generation.

### `asdamir new page` — localization, menu & permission seeds

Besides the page and its editor dialog, `new page` writes two idempotent, AppId-scoped SQL seeds under
`db/admin-onboarding/`:

- `localize_<plural>.sql` — `Page.<Entity>.Title`, one `Field.<Entity>.<Field>` per field, and the
  `Menu.<Slug>` label, in all three cultures.
- `seed_menu_<plural>.sql` — a `<plural>.view` permission, an Admin-role grant, and a guarded `dbo.Menus`
  row (so the page appears in the nav for users who may view it). `--icon` sets that row's icon.

`new page` **runs from the app root** — it finds the Server project itself (the nearest `.sln`, then
`src/<App>.Server`); running from inside the Server directory still works. Apply **both** seeds against
**AsdamirVault** (not the app's own DB) after generating — the same place `register_<app>.sql` runs.
(`new feature` with `--vault-connection` applies them for you.)

> **In a free-mode app**, `new page` emits the menu/localization seeds as ordinary app-DB migrations and
> **applies them automatically** (resolving the Gateway user-secret, like `new entity`) — pass **`--no-db`**
> to scaffold files only.

## `audit-lint`

`audit-lint` scans source for the framework's anti-pattern rule set (sync-over-async, silent failures, leaked API surface, unsafe defaults, …) and **fails on any error/warning**. Run it locally before pushing (CI runs it too when enabled).

```bash
dotnet run --project src/Asdamir.Tools -- audit lint --path src --min-severity warning
dotnet run --project src/Asdamir.Tools -- audit lint --path AppManagement/src --min-severity warning
```

Exit codes: `0` (clean — no finding at or above `--min-severity`), `1` (findings — **fails the build**),
`64` (usage — see [Exit codes — NORMATIVE](#exit-codes-normative-and-the-same-model-for-every-command)).

> This line is new in **`1.6.0`**, and it was written **before** the behaviour was made to match it. Until
> then `audit lint` — the most-run gate in the repository — was the only one whose exit codes were **not
> documented anywhere**, while `1` was in practice returned both for real findings and for a mistyped flag.
> Writing down what it did would have made that official; the contract above is what it *should* do, and the
> code now does it.

### Suppressing a finding

- Single line: `// audit-lint:ignore AUDxxx` — leave a sibling comment explaining *why*.
- Whole file: `// audit-lint:skip-file` within the first 10 lines.

Suppressions are deliberate and reviewable; prefer fixing the finding.

## `audit localization` — the localization gates (AUD015 + AUD019)

A whole class of "raw localization key shows on screen" bugs comes from a `L["X"]` in code whose key was
never seeded — or seeded in fewer than all three cultures (`tr-TR`/`en-US`/`ru-RU`). It falls through
silently to the raw key on the UI, and there is no compiler error for it. `audit localization` is the
**static** gate that closes this: it cross-checks every localization key **used** in `.razor`/`.cs` code
against every key **seeded** in the tree. It carries two rules:

| Rule | Asserts | Fails when |
| ---- | ------- | ---------- |
| **AUD015** — completeness | the key is seeded **somewhere**, in all three cultures | no seed at all, or fewer than three cultures across the whole corpus |
| **AUD019** — SQL backing (since Tools `1.4.6`) | that seed is a **SQL** seed, in all three cultures | the key resolves only from the **in-memory mirror**, or its SQL seed covers fewer than three cultures |

```bash
dotnet run --project src/Asdamir.Tools -- audit localization --path src --min-severity warning
dotnet run --project src/Asdamir.Tools -- audit localization --path . --format json
```

**What it collects.**

- **Used keys** — `L["Key"]` and localizer indexers (`Localizer["Key"]` / `_localizer["Key"]` /
  `localizer["Key"]`) in `.razor`/`.cs`.
- **Seeded keys → cultures** — from every `localize_*` / `register_*` / `seed_*.sql` and any `.sql` under a
  `db/admin-onboarding/` or `db/migrations/` directory, **plus** localization seed code (files whose path
  contains `Localization`, e.g. `AppAdminOnboarding.sbn`, `UiLocalization.cs`), in **both** SQL spellings:
  - the **tuple** form — `(N'Key', N'tr-TR', N'…')` in a `VALUES` list (a `@Seed` table variable, a
    `MERGE … USING (VALUES …)`, an `INSERT … VALUES`);
  - the **EXEC** form (since Tools `1.4.6`) — `EXEC dbo.LocalizationResource_UpsertValue …`, both the
    AsdamirVault arity `(@appId, @key, @category, @culture, @value)` and the single-tenant free-mode arity
    `(@key, @category, @culture, @value)`, named **or** positional; plus the legacy migration-001 proc
    `EXEC dbo.Localization_Upsert @Key=…, @Culture=…`.

  …and the in-memory dictionary literals (`["Key"] = "…"`) in that same seed code. An in-memory-seeded key
  counts as satisfying **all three** cultures for **AUD015** — the framework rule requires the in-memory seed
  to mirror the DB seed. The two corpora are kept **separate** internally, though, because an in-memory entry
  is not interchangeable with a SQL seed — that is what **AUD019** checks (below). Note that a SQL tuple
  inside a `.sbn` seed **template** counts as SQL backing (it renders into SQL that `db apply` runs); only a
  `["Key"] = "…"` dictionary literal is the in-memory mirror.

  The proc **arguments are lexed, not regexed** (shared `SqlTextScanner`), so a comma inside a value
  (`N'Sayfa 1, 2 / 3'`) cannot shift the argument positions, and an `EXEC` sitting inside a `--` or `/* … */`
  comment is never counted. Before `1.4.6` the gate matched the tuple form **only**, so every key seeded
  exclusively through an upsert proc was wrongly reported as "never seeded": **276 keys across 20
  AsdamirVault migrations** — the whole billing surface, the audit action labels, the agent-audit ledger.
  Write **new** seeds in the canonical form only — see [`audit seeds`](#audit-seeds-the-seed-form-gates-aud018-aud017).

**Seed auto-discovery (since Tools `1.4.2`).** A Model-A (central-model) app keeps its central seeds under
`db/admin-onboarding/*.sql` at the **repo root** — *outside* a `--path src` scope. So in addition to
`--path`, seed sources are also discovered from the **repo root** (the nearest `.git` ancestor of `--path`;
if there is none, auto-discovery is skipped and only `--path` is used). The **usage** scan stays
`--path`-scoped — only seed *discovery* widens. The widening is **visible**, never silent: the summary
prints `N seed source(s) (+M auto-discovered outside --path)`. The upshot: **`--path src` and `--path .` now
agree** — you no longer have to remember to scan from the root to avoid phantom "never seeded" errors on
central keys.

**What it catches.**

- A used static key with **no** seed anywhere → **ERROR** ("the raw key will render on screen").
- A used static key seeded in **fewer than all three** cultures → **ERROR** (lists the missing cultures).
- A **dynamic** key — `L[$"Prefix.{x}"]` or `L[variable]` — → **INFO** (never an error; the runtime
  value-set can't be resolved statically). For an interpolation, the literal prefix is reported so you can
  eyeball the value-set. Suppress the info once you've verified the set is fully seeded.

Seeded-but-**unused** keys are intentionally *not* flagged (shared chrome like `Common.*` is broad).

### AUD019 — an in-memory mirror does not substitute for a SQL seed

AUD015 compares against the **merged** corpus, in which an in-memory entry looks exactly like a real SQL
seed. So a key can be green **purely because it is mirrored into the in-memory seed**
(`UiLocalization.cs` and its siblings) while the SQL migration the **live database** actually runs is
missing it, or has it in fewer than three cultures. The in-memory seed is the `Persistence:UseInMemory`
mirror for tests and demos — it is **not** what `asdamir db apply` runs. Production then renders the raw
key or a blank, with a green gate.

**AUD019** closes exactly that direction: every key used in code must have a **SQL** seed in all three
cultures. The in-memory mirror is required **in addition**, never **instead** (the framework rule that the
two must mirror each other is unchanged — this adds a requirement, it does not replace one).

Two failure kinds, deliberately distinguished because they have different fixes:

```
[ERROR] AUD019: localization key SQL backing (an in-memory mirror is not a SQL seed)
    src/Pages/Orders.razor:14
      localization key 'Page.Orders.Title' has NO SQL seed — it resolves only from an in-memory seed
      (the `Persistence:UseInMemory` mirror), which is NOT what `db apply` runs. …
    src/Pages/Orders.razor:21
      localization key 'Field.Orders.Quantity' is SQL-seeded only in [tr-TR, en-US]; missing [ru-RU] in
      SQL. An in-memory seed covers the gap, so AUD015 is green — but the live database is short those
      cultures and will render the raw key there. …
```

**No overlap with AUD015, by construction.** A key seeded **nowhere** is already AUD015's "used but never
seeded", so AUD019 stays quiet for it — one key, one finding, one fix. AUD019 fires exactly on the keys
AUD015 lets through. Dynamic keys (`L[$"Prefix.{x}"]`) are skipped by AUD019 (AUD015 already reports them
as INFO).

**The honest scope limit — read this before quoting the gate.** AUD019 covers **statically resolved keys
only**. A key built at runtime is invisible to it, and the nav menu is exactly that case: `NavMenu.razor`
derives its label key from the row's Url (`"Menu." + slug`), so **no** localization gate — not AUD015, not
AUD019 — sees a nav label. That is a real hole, not a technicality: `Menu.UserAppRoles` lives in the
in-memory mirror with no SQL seed either gate recognises, and both are green on it. A second such entry,
`Menu.Systemreports`, advertised a key `AsdamirVault_043` had deliberately DELETED from SQL — and it was
found by reading the file, not by a gate. **A green `audit localization` therefore means "every statically
resolved key has a three-culture SQL seed" — it does not mean "every string the UI can render is seeded."**

A second, related limit: the gates model a seed as an **insert/upsert only**. A migration that **deletes** a
key (`AsdamirVault_043` removed `Menu.Systemreports` with the feature) or **renames** one
(`AsdamirVault_077`: `Menu.EntUserAppRoles` → `Menu.UserAppRoles`) changes the effective key set, and no rule
follows that chain — so a key can read as "seeded" from a migration that later removed it. Both limits are
tracked as a Faz 2 item in the roadmap; neither is closed today.

**No suppression, no allowlist — deliberately.** Unlike AUD015 there is no `// audit-lint:ignore AUD019`
and no `packaging/seed-form-allowlist.txt` entry: the offending set across the whole repo is **empty**, and
a new key has no legitimate reason to live only in the mirror. (A usage line already carrying
`// audit-lint:ignore AUD015` drops out of the *usage* corpus entirely, so it is out of scope for both
rules — that filter is shared, not an AUD019 escape hatch.)

**Why it was added while reporting zero.** The offending set is empty *today*, so the rule landed as a
no-op — which is precisely the cheapest moment to add it: adding it later would first require a cleanup
migration. And today's zero is the result of a **fix**, not of discipline — it was **186 keys** before the
Tools `1.4.6` AUD015 EXEC-form repair, and without a gate the count climbs straight back. AUD018 does not
cover this: **AUD018 constrains the seed's *form*, AUD019 asserts the seed's *existence*.**

**Zero-seed guard.** If the scan finds **no** seed sources under `--path` **nor auto-discovered from the
repo root** (e.g. you point it at a code-only folder outside any git repo — seeds live elsewhere), it prints
a notice and exits `0` rather than reporting every used key as unseeded (that would be a false alarm).

Options mirror `audit lint`: `--path/-p` (default cwd), `--min-severity/-s` (`info`|`warning`|`error`,
default `warning` — AUD015 emits Info and Error, AUD019 Error only, so `warning` gates exactly on the
errors), `--format/-f` (`text`|`json`), `--include-tests`. Suppress an AUD015 usage line with
`// audit-lint:ignore AUD015` (leave a comment why); skip a file with `audit-lint:skip-file`. JSON findings
carry a `ruleId` of `AUD015` or `AUD019`.



## `audit permissions` — the permission/policy-completeness gate (AUD016)

A Gateway defines authorization policies like `RequireClaim("perm", "reconciliation.run")`. The app-login
JWT carries the signed-in user's **role codes** (e.g. `Admin`) **plus** the fine-grained **permission
codes** their roles grant (e.g. `reconciliation.run`), one per `perm` claim. A policy that requires a
`perm` value which is **neither a seeded permission code nor a role code** can never be satisfied → every
request `403`s, **silently** — and tests that inject claims directly never notice. `audit permissions` is
the **static** gate that closes this (rule **AUD016**): it cross-checks every `perm` value **required** by
a policy in `.cs` code (`RequireClaim("perm", "X")` / `HasClaim("perm", "X")`, including inside a
`RequireAssertion(ctx => …)`) against the codes the tree's SQL seeds **supply** — the `Name`s inserted into
`dbo.Permissions` plus the role codes in `dbo.Roles` / `dbo.UserAppRoles`.

```bash
# Point one --path at the policies (src) and one at the seeds (db):
dotnet run --project src/Asdamir.Tools -- audit permissions --path src --path db
asdamir audit permissions --path . --format json
```

A required `perm` found in **neither** the supplied permission codes nor the role codes is an **AUD016
error**:

```
[ERROR] AUD016: permission/policy completeness
    src/App.Gateway/Program.cs:128
      Gateway policy requires perm 'reconciliation.typo-run' but no seed defines it as a permission or
      role code — the app-login token can never carry it (guaranteed 403). Seed it in dbo.Permissions
      (+ grant it to a role) or fix the policy.
```

`--path/-p` is **repeatable** — pass it once per tree so the policies (`src`) and the seeds (`db`) both
fall under the scan; the supplied codes are unioned across all paths. The SQL scan is deliberately
**tolerant/over-collecting** (it recognizes the `MERGE … USING (VALUES …)` / `INSERT … VALUES` / table-var
shapes by collecting every literal in a file whose **code** mentions the table) — a false *supplied* only
ever makes a perm look OK, never wrongly fails one. Options: `--format/-f` (`text`|`json`),
`--include-tests`. Suppress a policy line with `// audit-lint:ignore AUD016` (leave a comment why); skip a
file with `audit-lint:skip-file`.

**SQL comments are not code (since Tools `1.4.6`).** The seeded codes are read with a real T-SQL scanner,
not a literal regex: `--` line comments and (nestable) `/* … */` block comments are stripped **before**
anything is collected, and an apostrophe inside a literal is escaped by **doubling** it (`N'the agent''s
ledger'` is one literal, not two). This matters in both directions. Before the scanner, one unpaired
apostrophe in prose — `catalogue's`, `it's`, a `don't` next to a `VALUES` row — shifted literal pairing for
the **rest of the file**, so (a) genuinely seeded permissions went missing and a correct policy was flagged,
and (b) — the dangerous half — **comment prose was collected as a seeded code**, letting a policy that no
seed backs pass the gate and 403 every user in production with a green build. Consequences you can rely on
now: a permission mentioned **only** in a comment (`-- TODO: seed 'x.write'`) does **not** count as
supplied, a seed tuple that is **commented out** does not count as applied, and a file that merely *names*
`dbo.Permissions` in a header comment contributes nothing.

Exit codes: `0` (no findings — the gate is green; every required perm is supplied), `1` (at least one
AUD016 finding — this **fails the build**), `64` (usage — see [Exit codes — NORMATIVE](#exit-codes-normative-and-the-same-model-for-every-command)). Run it alongside `audit lint`,
`audit localization` and `audit seeds` before a push.

## `audit seeds` — the seed-form gates (AUD018 + AUD017)

The two gates above answer *"is this seeded?"* by **reading SQL as text** — and twice in a row they went
blind because the SQL was written in a spelling they did not recognise: an apostrophe in a comment shifted
AUD016's literal pairing, and the `EXEC …_UpsertValue` form was invisible to AUD015 across **276 keys**.
Both were fixed by teaching the scanner, but that is an infinite race: a third spelling always exists.
`audit seeds` ends it from the other side by constraining the **input**. It carries two rules: **AUD018** — a
localization seed may be written in exactly ONE approved way — and **AUD017** ([below](#aud017-a-permission-grant-may-not-be-a-like-pattern)),
the same principle applied to permission grants. Anything else fails the build.

```bash
dotnet run --project src/Asdamir.Tools -- audit seeds --path AppManagement/db --path src
```

**The canonical form** — a `@Seed` table variable of `(N'Key', N'<culture>', N'Value')` tuples, fed through
`dbo.LocalizationResource_UpsertValue`:

```sql
DECLARE @Seed TABLE ([Key] NVARCHAR(200), [Culture] NVARCHAR(20), [Value] NVARCHAR(MAX));
INSERT INTO @Seed ([Key],[Culture],[Value]) VALUES
    (N'Page.Title', N'tr-TR', N'Başlık'),
    (N'Page.Title', N'en-US', N'Title'),
    (N'Page.Title', N'ru-RU', N'Заголовок');
-- … cursor over @Seed …
EXEC dbo.LocalizationResource_UpsertValue @appId = @SelfApp, @key = @Key,
     @category = N'UI', @culture = @Culture, @value = @Value;
```

**Why this one and not a plain tuple `INSERT`/`MERGE`.** The proc is not a stylistic wrapper: it maps the
SelfApp GUID to `AppId = NULL` (the console scope) before the MERGE, and it owns the table shape
AppManagement evolves. A raw `MERGE dbo.LocalizationResource … VALUES (@SelfApp, …)` writes the GUID
*literally*, and the row is then invisible to the console's `AppId IS NULL` read path — a silent,
data-level divergence, not a style nit. So the mandate is **tuples fed through the proc**: the tuples make
the rows machine-readable as a *set*, the proc keeps the scoping semantics. `AsdamirVault_128` and every
scaffold template (`AppAdminOnboarding.sbn`, `PageLocalization.sbn`, `BillingSeed.sbn`, the free-mode
variants) are already written this way — the rule codifies the existing house form, it does not invent one.

**Two violations:**

| Violation | Why it fails |
|---|---|
| an ad-hoc `INSERT`/`MERGE`/`UPDATE` straight at `dbo.LocalizationResource` | bypasses the proc's `AppId` mapping — the row can land in a scope nothing reads |
| one `EXEC …_UpsertValue` **per row** with the key as an inline literal | the rows are a list of statements, not a data set — unreadable to any tool that does not know the proc's parameter order (exactly how 276 keys hid from AUD015) |

A `SELECT` from the table is fine, and a raw write **inside a stored-procedure body** is exempt — the
canonical writer is itself a `MERGE` on the table, and T-SQL requires `CREATE PROCEDURE` to open its batch,
so the exemption is decided per batch.

### AUD017 — a permission grant may not be a `LIKE` pattern

Same command, same idea applied to **authorization**. A grant written as

```sql
JOIN dbo.Permissions p ON p.Name LIKE N'%.read'      -- ❌ AUD017
```

does not say what it grants. The set is whatever the catalogue holds when the statement runs, so **both**
directions are silent: add a permission next year whose code ends in `.read` and it is granted to that role
retroactively — nobody edited a grant, no review saw it, and AUD016 cannot cross-check it because a wildcard
supplies **no names**; conversely a permission that does *not* match is never granted, which surfaces as a
403 nobody wrote down (`ent.agentaudit.verify` does not end in `.read`). A grant is an authorization
decision, so it must be a reviewable list:

```sql
WHERE p.Name IN (N'ent.agentaudit.read', N'ent.agentaudit.verify')   -- ✅
```

**AUD017 reads `.cs` as well as `.sql`, and it had to.** The rule was built for SQL and was blind to C#, so
the in-memory stores kept doing exactly what it forbids:

```csharp
["AppAdmin"] = catalogue.All.Where(p => p.EndsWith(".read")).ToArray()   // ❌ AUD017
```

That is the same authorization decision, written in another language. When a ledger permission ending in
`.read` was later added to the catalogue, it joined that role **retroactively and silently** — a grant the
database has never held. *A gate built in one language and left absent in another is not a gate.*

The C# check is narrow on purpose: it fires only on a LINQ filter (`Where`/`Any`/`All`) whose predicate uses
`EndsWith` / `StartsWith` / `Contains` **and** whose receiver is named after permissions, a catalogue or a
grant. Filtering file paths or culture codes by suffix is ordinary code and is not flagged — a rule that
produces noise gets suppressed, which is worse than no rule. Write the list:

```csharp
["AppAdmin"] = ["appconfig.read", "apps.read", "audit.read", /* … */]   // ✅
```

The rule fires only on a **statement that writes** `dbo.Permissions` or `dbo.RolePermissions` while
containing a `LIKE`. A read-path `SELECT … WHERE Name LIKE @Category + '%'` in a lookup proc is legitimate;
a write to a *different* table that merely JOINs `dbo.Permissions` (e.g. the `dbo.UserMenuPermissions`
capability computation) is not a grant; a `LIKE` against an audit search or `sys.sql_modules` is irrelevant.
Two applied migrations are grandfathered: **`AsdamirVault_003`** (the original "AppAdmin gets every `%.read`"
bootstrap grant — the rule's namesake) and **`AsdamirVault_060`** (a one-off `ent.%` prefix-strip rename;
the pattern is the point of that migration, but it is still a pattern reaching into the permission table, so
it is listed rather than carved out of the rule).

**There is no inline suppression — deliberately.** Every other rule takes `// audit-lint:ignore AUDxxx`;
these two do not. An applied migration is immutable (see the migration-immutability rule in `CLAUDE.md`),
so the only legitimate exemption is a *pre-existing* file, and that belongs in a reviewed, commented
allowlist — **`packaging/seed-form-allowlist.txt`**, same idiom as `packaging/removed-public-types.txt`:
one `<RULE>  <repo-relative path>` per line, `#` comments, every entry stating why it is exempt. A **new**
seed has no opt-out: canonical form, or the build fails. The allowlist currently grandfathers **63 applied
AsdamirVault migrations** — 61 for AUD018 in two eras (002–077, written before the proc existed; 093–129,
per-row `EXEC`s) and 2 for AUD017 (003, 060). Because those files are frozen, the list can only shrink; when
a later migration supersedes one, the gate prints `NOTE — allowlist entry no longer matches anything` and the
line is deleted.

Options: `--path/-p` (repeatable), `--format/-f` (`text`|`json`), `--include-tests`, `--allowlist <file>`.
Exit codes: `0` (clean), `1` (findings — **fails the build**), `64` (usage — see
[Exit codes — NORMATIVE](#exit-codes-normative-and-the-same-model-for-every-command)).

## `audit verify-archive` — verify a folded agent-audit segment, offline

Unlike every other `audit` subcommand, this one is **not a build gate**. It is the tool a third party runs on
an **exported agent action ledger archive** — with **no AppManagement, no database, no network and no
commercial licence**.

When a closed range of the [agent action ledger](fundamentals/agent-audit.md) is *folded*, those rows leave the
live database and survive only as an archive ([Archive Format v1](fundamentals/agent-audit-archive-format-v1.md)
— a ZIP holding `manifest.json` + `segment.ndjson`). If the only thing able to check that archive were the
closed component that produced it, the assurance would collapse to *"trust the vendor"*, and a system's own
clean report about itself is not audit evidence. So the verifier lives in **`Asdamir.Core` (open core, LGPL)**
and this command is its front end.

```bash
# UNANCHORED — proves the archive has not been altered; proves nothing about where it came from
asdamir audit verify-archive --path ./segment.zip

# ANCHORED — additionally proves these rows ARE the segment folded from that ledger
asdamir audit verify-archive --path ./segment.zip --expected-digest <FoldSegmentDigest from the tombstone>

# machine-readable
asdamir audit verify-archive --path ./unpacked-dir --json
```

`--path` takes either the `.zip` container or a **directory** holding the two entries.



The rule fires only on a **statement that writes** `dbo.Permissions` or `dbo.RolePermissions` while
containing a `LIKE`. A read-path `SELECT … WHERE Name LIKE @Category + '%'` in a lookup proc is legitimate;
a write to a *different* table that merely JOINs `dbo.Permissions` (e.g. the `dbo.UserMenuPermissions`
capability computation) is not a grant; a `LIKE` against an audit search or `sys.sql_modules` is irrelevant.
Two applied migrations are grandfathered: **`AsdamirVault_003`** (the original "AppAdmin gets every `%.read`"
bootstrap grant — the rule's namesake) and **`AsdamirVault_060`** (a one-off `ent.%` prefix-strip rename;
the pattern is the point of that migration, but it is still a pattern reaching into the permission table, so
it is listed rather than carved out of the rule).

**There is no inline suppression — deliberately.** Every other rule takes `// audit-lint:ignore AUDxxx`;
these two do not. An applied migration is immutable (see the migration-immutability rule in `CLAUDE.md`),
so the only legitimate exemption is a *pre-existing* file, and that belongs in a reviewed, commented
allowlist — **`packaging/seed-form-allowlist.txt`**, same idiom as `packaging/removed-public-types.txt`:
one `<RULE>  <repo-relative path>` per line, `#` comments, every entry stating why it is exempt. A **new**
seed has no opt-out: canonical form, or the build fails. The allowlist currently grandfathers **63 applied
AsdamirVault migrations** — 61 for AUD018 in two eras (002–077, written before the proc existed; 093–129,
per-row `EXEC`s) and 2 for AUD017 (003, 060). Because those files are frozen, the list can only shrink; when
a later migration supersedes one, the gate prints `NOTE — allowlist entry no longer matches anything` and the
line is deleted.

Options: `--path/-p` (repeatable), `--format/-f` (`text`|`json`), `--include-tests`, `--allowlist <file>`.


## `audit verify-archive` — verify a folded agent-audit segment, offline

Unlike every other `audit` subcommand, this one is **not a build gate**. It is the tool a third party runs on
an **exported agent action ledger archive** — with **no AppManagement, no database, no network and no
commercial licence**.

When a closed range of the [agent action ledger](fundamentals/agent-audit.md) is *folded*, those rows leave the
live database and survive only as an archive ([Archive Format v1](fundamentals/agent-audit-archive-format-v1.md)
— a ZIP holding `manifest.json` + `segment.ndjson`). If the only thing able to check that archive were the
closed component that produced it, the assurance would collapse to *"trust the vendor"*, and a system's own
clean report about itself is not audit evidence. So the verifier lives in **`Asdamir.Core` (open core, LGPL)**
and this command is its front end.

```bash
# UNANCHORED — proves the archive has not been altered; proves nothing about where it came from
asdamir audit verify-archive --path ./segment.zip

# ANCHORED — additionally proves these rows ARE the segment folded from that ledger
asdamir audit verify-archive --path ./segment.zip --expected-digest <FoldSegmentDigest from the tombstone>

# machine-readable
asdamir audit verify-archive --path ./unpacked-dir --json
```

`--path` takes either the `.zip` container or a **directory** holding the two entries.

### The two claims, never blurred — read this before quoting a result

An archive can attest to **two different things**, and a tool that reports one tick for both is lying:

| | Claim | Provable from the archive alone? |
|---|---|---|
| **A** | This archive has not been altered since it was written. Every row's `RowHash` re-derives **from its own columns**, the links hold, the sequence is dense. | **yes** |
| **B** | These rows genuinely **are** the segment folded out of a particular live ledger. | **no** |

The proof of **B** is the **`FoldSegmentDigest` recorded on the tombstone in the live ledger** — a value the
archive cannot produce for itself (if it could, a forged archive could produce it too). Pass it as
`--expected-digest`. **The `SegmentDigest` inside `manifest.json` is NOT an anchor**: it is derived from the
very rows it accompanies, so an attacker who rewrites the rows simply recomputes it.

So without `--expected-digest` the command reports **`INTERNALLY_CONSISTENT` — UNANCHORED**, never `VERIFIED`,
and both the text and the `--json` output state A and B **separately** (`provesNotAltered` / `provesAnchored`).
Same discipline as the ledger's three-valued `ChainStatus`, where `Degraded` is never rendered as success: do
not dilute the green.

### Exit codes — NORMATIVE

This table is the **single, normative** statement of the contract. A third party's audit script may rely on
it. It is stated **exhaustively**: the command emits these values and **no others**.

| Code | Outcome | Meaning |
|---:|---|---|
| `0` | `VERIFIED` | internal checks pass **and** the supplied digest matches — A **and** B |
| `1` | `INTERNALLY_CONSISTENT` | internal checks pass, **no** digest supplied — A only, **unanchored** |
| `2` | `DIGEST_MISMATCH` | internal checks pass, the supplied digest does **not** match — these rows are not that segment |
| `3` | `BROKEN` | an internal check failed — the archive is altered or corrupt (the finding names the `SeqNo`) |
| `4` | `FORMAT_ERROR` | the shape or version was not understood — **nothing was checked** |
| `64` | usage error | the command was **invoked wrongly**. No archive was judged |

**The rule, in one sentence: `0`–`4` are claims about an archive, and nothing else may occupy them.** Every
invocation that produces no such claim exits `64` — a typo'd or unknown flag, a missing or empty `--path`, an
option given without its value, a stray argument, a malformed `--expected-digest`, a path that does not
exist, and **`--help` / `--version` as well**.

Help and version are included **against the usual convention that `--help` exits `0`**, and the reason is the
whole point of the band: on this command `0` does not mean *the program ran*, it means *this archive is
unaltered and anchored to the ledger it claims to come from*. Help cannot borrow that code. (Nothing else in
the CLI is affected — the rule is scoped to `verify-archive`, where an exit code is evidence.)

`FORMAT_ERROR` sits inside the band even though it reports no verdict, because it is still a statement about
the archive — *this file was refused* — whereas `64` says nothing about any archive at all.

Codes `0`–`4` are identical to the specification's own reference fixture, so a script wired to one
implementation behaves the same against the other.

> **Fixed in `Asdamir.Tools 1.5.1`.** In `1.5.0` the invocations that `System.CommandLine` answered *before*
> the handler ran leaked into the band: a typo'd flag exited `1` (`INTERNALLY_CONSISTENT`) and `--help` exited
> `0` (`VERIFIED`). A script could therefore log a verification result for a command that verified nothing. If
> you are pinned to `1.5.0`, treat any exit code as trustworthy **only** when the command also printed a
> verdict line; upgrading is the real fix.

### What it actually checks

It **re-derives**; it never re-hashes what it was handed. Each row carries the canonical prefix that was hashed
when it was written, and the verifier rebuilds that prefix **from the row's own columns** before comparing —
because hashing the stored prefix would be circular: someone who edits a projection column (`AgentId`,
`ActionType`, `TargetId`, …) and leaves the prefix and hash alone would pass. That circularity is exactly why
the ledger's in-database check is *partial* by construction, and the archive layer must not import it.

Output is **English only**, deliberately: a verification finding is evidence quoted in an audit, and a message
that reads differently depending on the reader's locale is a finding two people cannot compare.

You do not have to use this command at all — [Archive Format v1](fundamentals/agent-audit-archive-format-v1.md)
and [Canonicalization v1](fundamentals/agent-audit-canonicalization-v1.md) are normative and complete, and the
[golden vectors](fundamentals/agent-audit-golden-vectors-v1.json) let you check your own implementation before
pointing it at real data. That it is *optional* is the point.

## `localization verify` — live apply-drift

`audit localization` catches keys missing from the **seed files**. It cannot catch a key that *is* in a
seed file but was **never applied** to the running database (someone forgot to `db apply`) — the seed looks
correct in the repo, yet the raw key still renders live. `localization verify` closes that gap: it collects
the `(key, culture)` pairs the tree's SQL seeds **define** (same parser as `audit localization`), queries the
live `dbo.LocalizationResource` for the app, and **diffs** the two.

```bash
# Resolve the AppId from the app's Code:
asdamir localization verify --path . --server localhost --database MyAppDb \
  --user <login> --password <pwd> --app-code MyApp

# …or pass the AppId directly, and a full connection string:
asdamir localization verify --path . --connection "<connstr>" --app-id <guid>
```

Options: `--path/-p` (where the seed files are), the connection flags copied from `db apply`
(`--connection/-c`, `--server/-S`, `--database/-d`, `--user/-U`, `--password/-P`), one of `--app-code` /
`--app-id`, and `--format/-f`. A **live connection is required** (never a silent skip), as is one of
`--app-code`/`--app-id`. It reports **unapplied** pairs (a seeded `(key, culture)` not present live — the
drift you're hunting; apply the missing migration) and exits non-zero when any are found.

## `db apply`

A small, **journaled** migration runner. It applies the `*.sql` scripts in a directory in filename
order, **exactly once each** — applied migrations are recorded in `dbo.__SchemaMigrations`, so re-runs
skip what's already applied (incremental, restart-safe deploys; a migration that isn't itself
idempotent is never re-executed). Each file is split into batches on `GO`. No outer transaction is
imposed (some migrations manage their own `BEGIN TRAN` and some carry DDL that can't run in a
transaction); a migration is journaled only after all its batches succeed, so a partial failure is
retried on the next run.

```bash
# Preferred — no SQL password on the command line: set the Gateway user-secret once, then apply.
# db apply resolves ConnectionStrings:Default from the Gateway user-secret (see "Connection resolution" below).
asdamir db apply --create-database --migrations db/migrations

# Or pass connection details explicitly (SQL auth):
asdamir db apply --server localhost --database MyAppDb \
  --user <login> --password <pwd> --create-database --migrations db/migrations

# Or a full connection string (e.g. from a secret); on Windows you may omit --user for integrated auth:
asdamir db apply --connection "<connstr>" --migrations db/migrations
```

Options: `--connection/-c` (full string, wins over the parts), `--server/-S`, `--database/-d`,
`--user/-U` + `--password/-P` (SQL auth — omit for Windows integrated), `--migrations/-m`,
`--create-database`. If an already-applied migration's file content later changes, the runner warns and
**does not** re-run it — add a new migration instead.

**Connection resolution (passwordless-friendly).** When you pass **no** connection details at all (no
`--connection` / `--server` / `--database` / `--user` / `--password`), `db apply` falls back — in this order
— to:

1. the **Gateway project's user-secret** `ConnectionStrings:Default` (it walks up from `--migrations` to the
   nearest `*.Gateway` project and reads its `<UserSecretsId>` store), then
2. the **`ConnectionStrings__Default` environment variable**.

It prints which source it used. This is what lets `asdamir db apply --create-database --migrations
db/migrations` run with **no SQL password on the command line** once you've set the secret (see the free
quick-start above — the same flow works in commercial mode too). Any explicit flag always takes precedence
over the fallback.

## `rollback`

The inverse of [`new feature`](#asdamir-new-feature) — removes a generated feature across **code, the app
DB, and AsdamirVault**. It is **destructive** and, by default, **interactive**: it inventories exactly what
exists (by the entity's name + plural — no broad globbing, scoped by name and `AppId`), prints it, and asks
`[y/N]` before touching anything.

What it can remove:

- **Code** (whichever files exist): the Gateway slice (`Domain`/`Dtos`/`Repositories`/`Services`/
  `Controllers`/`Validators`), the Server page (`<Plural>List.razor` + `<Name>EditorDialog.razor` + its DTO),
  the entity tests, the create/seed migrations, and the `localize_`/`seed_menu_` seeds.
- **App DB** (only with a connection): `DROP TABLE dbo.<Plural>` (if present) + delete the matching
  `dbo.__SchemaMigrations` journal rows, in one transaction.
- **AsdamirVault** (only with `--vault-connection`): the `<plural>.view` permission, its menu row(s), role
  grants, and user-menu permissions — AppId-scoped, in FK order, in one transaction.

```bash
asdamir rollback Supplier \
  --output . \
  -S localhost -d AppDb -U sa -P <pwd> \
  --vault-connection "Server=localhost;Database=AsdamirVault;User Id=sa;Password=<pwd>;TrustServerCertificate=True"
```

Options: `<Name>`, `--output`/`-o` (app root), `--gateway-dir`/`--server-dir` (overrides),
`--connection`/`-c` or `--server`/`-S`+`--database`/`-d`+`--user`/`-U`+`--password`/`-P` (app DB),
`--vault-connection` (AsdamirVault), `--yes`/`-y` (skip the prompt — for scripts).

**Conditional & safe:**

- It removes only what's actually there (a missing table skips the `DROP`).
- **Code is always deleted** (after confirmation); the **DB** step runs only with a connection and the
  **Vault** step only with `--vault-connection` — a missing connection is reported as **skipped, never
  silently dropped** (re-run later with the connection to finish).
- **`add field` migrations are NOT rolled back** (`*__add_<field>_to_<plural>.sql`) — they're listed as a
  warning to handle by hand.
- It warns that if other code still references `<Name>`, that code may stop compiling.

> **Free-mode apps:** `rollback` detects free mode and tears the feature down **symmetrically over the app
> connection** (there is no control plane). It removes the code — **including** the
> `V*__freemode_{menu,localize}_<plural>.sql` seed migrations — drops the app-DB table + its create/seed
> journal rows, and, with a connection, also removes the free-mode **menu, permission, role grant,
> localization keys** and the freemode seed-journal rows from the app's **own** database (single-tenant, in
> FK order, one transaction). `--vault-connection` is not used in free mode (there is no AsdamirVault).

### `rollback app`

The **symmetric inverse of [`new app`](#asdamir-new-app-free-vs-commercial-mode)** — tears down a whole generated app, not just one
feature. Like the feature rollback it is **destructive** and **interactive by default**: it shows EXACTLY what
will be removed (the full directory path + the server/database name + the vault app code) and asks `[y/N]`
before touching anything. Works in **both** modes (detected from the app).

What it removes:

- **Directory** — the generated app root (the ancestor/child dir whose `<Name>.sln` exists; requiring that
  `.sln` is the guard against deleting the wrong directory), removed recursively.
- **App database** — `DROP DATABASE [<Name>]` (the app's OWN DB — free **or** commercial; name defaults to the
  app name, override with `--database`). Kicks open connections first (`SINGLE_USER WITH ROLLBACK IMMEDIATE`).
  The connection resolves in the **same order as `db apply`** (reusing its resolver): explicit `--connection` →
  `-S/-d/-U/-P` flags → the **Gateway user-secret** (`ConnectionStrings:Default`, the value `new app` wrote). So
  `rollback app <Name>` with **no flags** drops the DB just like `db apply` with no flags applies to it — no
  orphan DB left behind. The secret is read **before** the directory is deleted (it holds the id); the
  confirmation shows the server + database (never the password).
- **App registration in AsdamirVault** (commercial only, with `--vault-connection`) — purges the app's `dbo.Apps`
  row + ALL its AppId-scoped rows (users/roles/menus/permissions/config/localization/logs/audit) via the existing
  `dbo.App_Purge` proc (FK-safe, one transaction). This removes the app's **registration**, **not** the
  AsdamirVault database itself (which is never dropped). A **free** app has no registration, so this line is
  hidden entirely in free-mode teardown — **and** when the mode can't be determined (the app directory is already
  gone), unless you pass `--vault-connection` explicitly (then it's shown + acted on, in any mode).

```bash
asdamir rollback app CustomerOrders \
  --output ~/src/asdamirgenerated \
  -S localhost -U sa -P <pwd> \
  --vault-connection "Server=localhost;Database=AsdamirVault;User Id=sa;Password=<pwd>;TrustServerCertificate=True"
```

Options: `<Name>`, `--output`/`-o` (the app's parent dir OR the app dir itself), `--connection`/`-c` or
`--server`/`-S`+`--database`/`-d`+`--user`/`-U`+`--password`/`-P` (app DB), `--vault-connection` (commercial),
`--yes`/`-y` (skip the prompt).

**Fail-closed & idempotent:**

- It **NEVER** drops a protected database — `AsdamirVault`, `master`, `model`, `msdb`, `tempdb` are refused;
  and `App_Purge` refuses the self-app (`EnvironmentName='Self'`, i.e. AppManagement itself).
- Each step is conditional: a missing directory / database / registration is reported as **"already gone"**,
  never an error (re-run any time).
- The **DB** drop runs when a connection resolves (flags **or** the Gateway user-secret); the **vault** purge
  runs only with `--vault-connection`. When neither resolves, the step is **skipped and reported** (with how to
  supply it), never silently dropped.
- It's a subcommand, so the bare `rollback <Feature>` form is unaffected (only a feature literally named
  `app` is shadowed by `rollback app`).

## `app register`

Registers a managed app into a company's AppManagement database — an `Apps` row, **not** a new
database — by calling the running AdminConsole.Api (`POST /api/admin/apps`). Auth and at-rest
secret encryption are delegated to the API, so the CLI stays credential- and crypto-free.

```bash
asdamir app register \
  --api https://admin-api.example.com/ \
  --token <SuperAdmin JWT from an authenticated console session> \
  --code acme.portal \
  --display-name "Acme Portal" \
  --gateway-url https://gw.acme.example.com/ \
  --client-secret <client_credentials secret> \
  [--environment Production]
```

Exit codes: `0` ok · `1` API/HTTP error (401/403/unreachable/non-2xx) · `64` usage (see [Exit codes — NORMATIVE](#exit-codes-normative-and-the-same-model-for-every-command)). The
company is taken from the token's `company` claim. See the AppManagement console's multi-company (firma) operation.

## `secrets`

Tooling for the at-rest encryption key (`Security:EncryptionKey`). Keys are read from environment
variables by default (so they stay out of shell history); flags override for interactive use.

### `secrets rotate-key`

Re-encrypts every at-rest secret in AsdamirVault from an OLD key to a NEW key — `Apps.EncryptedClientSecret`
and `AppConfigurations` rows with `IsEncrypted=1` — so you can rotate `Security:EncryptionKey` without
losing data. **Dry-run by default** (decrypt + re-encrypt + verify, no writes); `--apply` commits in a
single transaction. Idempotent (rows already on the new key are skipped) and aborts+rolls back if a value
can't be decrypted with the old key.

```bash
export ASDAMIR_OLD_ENCRYPTION_KEY='<current key>'
export ASDAMIR_NEW_ENCRYPTION_KEY='<new 32+ char key>'
asdamir secrets rotate-key --server <sql> --database AsdamirVault --user <login> --password <pwd>          # dry-run
asdamir secrets rotate-key --server <sql> --database AsdamirVault --user <login> --password <pwd> --apply  # commit
```

Connection flags match `db apply` (`--connection` / `--server`+`--database`+`--user`+`--password`; omit
`--user` for Windows integrated auth). Keys/salts: `--old-key`/`--new-key` (+ `--old-salt`/`--new-salt`)
or the `ASDAMIR_OLD_ENCRYPTION_KEY` / `ASDAMIR_NEW_ENCRYPTION_KEY` (+ `…_SALT`) env vars.

### `secrets encrypt`

Encrypts a value with `Security:EncryptionKey` and prints the `v2:` ciphertext — to seed an encrypted
`AppConfigurations` value or a `v2:` Companies connection string. (There is no `decrypt` — it never prints
a stored secret back.)

```bash
export ASDAMIR_ENCRYPTION_KEY='<key>'
asdamir secrets encrypt --value 'the-plaintext'     # or: echo 'the-plaintext' | asdamir secrets encrypt
```

Full procedures (incl. `Jwt:Key` and per-app client-secret rotation): **[Secret Management & Key Rotation](secret-rotation.md)**.

## See also

- [Getting Started](getting-started.md) · [Modules](fundamentals/modules.md) · [Data Access](fundamentals/data-access.md)
