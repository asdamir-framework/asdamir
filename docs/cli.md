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
| `new app <Name>` | A full application (Server + Gateway) wired to the framework — including `Properties/launchSettings.json` on fixed dev ports (Gateway `7001` = the Server's `Gateway:BaseUrl`, Server `7010`) so `dotnet run` starts on a known port out of the box instead of the default `:5000`. **`--mode free\|commercial`** (default `commercial`) picks the identity model — see [free vs commercial mode](#asdamir-new-app--free-vs-commercial-mode). |
| `new entity <Name>` | Entity + DTO + repository + service + controller + tests + a create migration + an **idempotent sample-seed migration**. The generated tests (since 1.1.0) cover create/get round-trip, validator rejection, delete, **update round-trip**, **list**, and an **API auth-guard** (`GET` without a token → 401) — all DB-free (in-memory fake repo + `WebApplicationFactory`). The seed migration (since 1.1.1) writes 3 typed sample rows (guarded by `IF NOT EXISTS`, `TenantId='default'`) so the entity's grid is populated after `db apply` instead of empty. |
| `new page <Name>` | A Blazor CRUD page (DataGrid + edit dialog + delete confirm) **plus** its localization seed (`localize_<plural>.sql`) and an idempotent, role-based **menu + permission seed** (`seed_menu_<plural>.sql`, AppId-scoped). `--icon` sets the nav icon. See [below](#asdamir-new-page-localization-menu-permission-seeds). |
| `new feature <Name>` | **The one-command path for a complete CRUD feature** — an entity slice (Gateway) + a CRUD page (Server) + the menu/permission & localization seeds, in one go. With `--apply` it also runs the entity migration, and with `--vault-connection` the AsdamirVault menu/permission seed. See [below](#asdamir-new-feature). |
| `new module <Name>` | A self-registering [module](fundamentals/modules.md) project |
| `new mobile <Name>` | A .NET MAUI **Blazor Hybrid** app — `.Mobile` (Android host) + `.Mobile.Shared` (UI) + `.Mobile.Data` (SQLite) + tests. Login, left nav drawer, dashboard; talks to the app's Gateway. See [Mobile App](mobile.md). |
| `add field <Name>` | Adds a field across the entity/DTO/repository/migration set |

> The verb comes first: **`asdamir new app|entity|page|feature|module|mobile`**, **`asdamir add field`**, and **`asdamir rollback`** (the inverse of `new feature`).

Generated output uses the framework's templates (`src/Asdamir.Tools/Templates/*.sbn`) and references the `Asdamir.*` packages.

> **Mobile build:** the MAUI app needs `dotnet workload install maui-android` + an Android SDK platform
> (`dotnet build … -t:InstallAndroidDependencies -f net10.0-android -p:AcceptAndroidSDKLicenses=true`),
> then build a **single RID** — `dotnet build … -f net10.0-android -r android-arm64` (a plain multi-RID
> build fails with `NETSDK1047`). Full recipe in [Mobile App → Build & run](mobile.md#build-run).

### `asdamir new app` — free vs commercial mode

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

**Free quick-start** (verified end-to-end, with AppManagement not running):

```bash
asdamir new app MyApp --mode free --yes
cd MyApp

# One-time secrets on the Gateway (NEVER in appsettings.json). The Gateway signs + validates its
# own JWTs, so Jwt:Key is just a 64+ byte random value (not shared with anything).
cd src/MyApp.Gateway
dotnet user-secrets set "Jwt:Key" "<a 64+ byte random key>"
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost,1433;Database=MyApp;User Id=<login>;Password=<pwd>;TrustServerCertificate=True"
cd ../..

dotnet build MyApp.sln
# Creates the DB and applies EVERY migration (management schema/procs/seed + business) in one pass.
# No password on the command line — the runner reads ConnectionStrings:Default from the secret above.
asdamir db apply --create-database --migrations db/migrations

dotnet run --project src/MyApp.Gateway    # then, in another shell:
dotnet run --project src/MyApp.Server     # open the Server, sign in with the starter admin printed by `new app`
```

The starter admin's email + one-time password are printed once by `new app` — change the password after
first sign-in. Add features exactly as in commercial mode with `asdamir new feature …` (see the free-mode
note under it).

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
| `--apply` | After generating, apply the entity migration to the app DB (needs a connection below). |
| `--connection`/`-c`, `--server`/`-S`, `--database`/`-d`, `--user`/`-U`, `--password`/`-P` | App-DB connection for `--apply` (the same flags as `db apply`). |
| `--vault-connection` | AsdamirVault connection. **With `--apply`**, the menu/permission + localization seeds are applied to AsdamirVault too. |

**What `--apply` does — two databases, opt-in each:**

- The **entity migration** goes to the **app's own (business) DB** via the journaled `db apply` runner —
  `--apply` + a connection (`--connection` or `-S`/`-d`/`-U`/`-P`).
- The **menu/permission + localization seeds** go to **AsdamirVault** only when you ALSO pass
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
asdamir new feature Supplier \
  --fields "Name:string,Phone:string,Email:string" \
  --route /suppliers --icon truck \
  --apply -S localhost -d AppDb -U sa -P <pwd> \
  --vault-connection "Server=localhost;Database=AsdamirVault;User Id=sa;Password=<pwd>;TrustServerCertificate=True"
```

**Fail-fast:** if the entity step fails the page is **not** generated — fix the error and re-run
(generation is idempotent: existing files are skipped, never overwritten). Afterwards, translate the
`tr-TR`/`ru-RU` values in `localize_<plural>.sql` (the generator seeds the English name as the default for
all three cultures). To undo a feature, see [`rollback`](#rollback).

### `asdamir new entity --apply`

By default `new entity` only **writes files** (review-first) and prints `next: asdamir db apply`. Pass
`--apply` to run the create + sample-seed migrations immediately, through the same journaled `db apply`
runner, against `<output>/db/migrations`:

```bash
asdamir new entity Supplier --fields "Name:string,Phone:string" \
  --apply -S localhost -d AppDb -U sa -P <pwd>
```

The connection flags are identical to `db apply`. `--apply` **requires** a connection — if neither
`--connection` nor `--database` is given the runner errors (no silent skip). It does **not** create the
database (the app DB already exists by the time you add entities).

### `asdamir new page` — localization, menu & permission seeds

Besides the page and its editor dialog, `new page` writes two idempotent, AppId-scoped SQL seeds under
`db/admin-onboarding/`:

- `localize_<plural>.sql` — `Page.<Entity>.Title`, one `Field.<Entity>.<Field>` per field, and the
  `Menu.<Slug>` label, in all three cultures.
- `seed_menu_<plural>.sql` — a `<plural>.view` permission, an Admin-role grant, and a guarded `dbo.Menus`
  row (so the page appears in the nav for users who may view it). `--icon` sets that row's icon.

Apply **both** against **AsdamirVault** (not the app's own DB) after generating — the same place
`register_<app>.sql` runs. (`new feature` with `--vault-connection` applies them for you.)

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
# Cross-platform (SQL auth):
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
