# CLI — `Asdamir.Tools`

**Package:** `Asdamir.Tools` · **Command:** `asdamir`

## Introduction

`Asdamir.Tools` is a `dotnet`-based command-line tool that (1) **scaffolds** code following the framework's audited conventions and (2) runs **`audit-lint`**, the static-analysis gate (run it locally before every push; it also runs in CI when CI is enabled).

During development run it from source:

```bash
dotnet run --project src/Asdamir.Tools -- <command> [options]
```

### Install as a global tool

The packaged command is `asdamir <command>` (formerly `framework`).

```bash
dotnet tool install --global Asdamir.Tools
#   update later:  dotnet tool update --global Asdamir.Tools
```

`Asdamir.Tools` is published on **nuget.org** (currently `1.1.3`). Generated apps restore the framework
**libraries** — `Asdamir.Core` / `.Data` / `.Web` (currently `1.1.1`) — from nuget.org as well.

## Scaffolding

| Command | Generates |
|---|---|
| `new app <Name>` | A full application (Server + Gateway) wired to the framework — including `Properties/launchSettings.json` on fixed dev ports (Gateway `7001` = the Server's `Gateway:BaseUrl`, Server `7010`) so `dotnet run` starts on a known port out of the box instead of the default `:5000`. **`--mode free\|commercial`** (default `commercial`) picks the identity model — see [free vs commercial mode](#asdamir-new-app-free-vs-commercial-mode). **`--billing`** (opt-in, off by default) adds an end-user payment page + billing proxy + seeds — see [billing](#asdamir-new-app-billing). |
| `new entity <Name>` | Entity + DTO + repository + service + controller + tests + a create migration + an **idempotent sample-seed migration**. The generated tests (since 1.1.0) cover create/get round-trip, validator rejection, delete, **update round-trip**, **list**, and an **API auth-guard** (`GET` without a token → 401) — all DB-free (in-memory fake repo + `WebApplicationFactory`). The seed migration (since 1.1.1) writes 3 typed sample rows (guarded by `IF NOT EXISTS`, `TenantId='default'`) so the entity's grid is populated after `db apply` instead of empty. |
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

The starter admin's email + one-time password are printed once by `new app`. That password is a **bootstrap
credential**, so the app **forces a change on first sign-in**: the starter admin ships with a
`ForcePasswordChange` flag, and login redirects to a **change-password** page instead of the dashboard until
a new password is set.

**First sign-in — forced password change (free mode).**

1. Sign in with the starter admin's email + one-time password.
2. The app detects the flag and sends you to `/change-password` (not the dashboard).
3. Enter the current password + a new one. The change-password endpoint verifies the current password,
   stores the new one, **clears `ForcePasswordChange`, and revokes all existing sessions** (so any other
   session is signed out).
4. You're returned to the sign-in page; sign in again with the **new** password — the flag is now clear, so
   you land on the dashboard. Subsequent logins are normal (no redirect).

(This is a free-mode flow — the starter admin lives in the app's own DB. Commercial-mode identity is managed
in the control plane and is unaffected.) Add features exactly as in commercial mode with `asdamir new
feature …` (see the free-mode note under it).

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

### Suppressing a finding

- Single line: `// audit-lint:ignore AUDxxx` — leave a sibling comment explaining *why*.
- Whole file: `// audit-lint:skip-file` within the first 10 lines.

Suppressions are deliberate and reviewable; prefer fixing the finding.

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

The **symmetric inverse of [`new app`](#asdamir-new-app)** — tears down a whole generated app, not just one
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

Exit codes: `0` ok · `1` API/HTTP error (401/403/unreachable/non-2xx) · `2` bad arguments. The
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
