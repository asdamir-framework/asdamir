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

**From nuget.org** (stable `1.0.0`):

```bash
dotnet tool install --global Asdamir.Tools
#   update later:  dotnet tool update --global Asdamir.Tools
```

This is the public channel — generated apps also restore `Asdamir.Core` / `.Data` / `.Web` from nuget.org.

**Fallback (no secret needed)** — the tool is also attached as an asset on the latest **GitHub Release**
(GitHub Packages' NuGet endpoint can't ingest the self-contained tool package, so it isn't on that feed):

```bash
# download Asdamir.Tools.1.0.0.nupkg from the latest release into a folder, then:
dotnet tool install --global Asdamir.Tools --add-source <folder>
```

The framework **libraries** are also on the **GitHub Packages** NuGet feed (`Asdamir.Core` / `.Data` /
`.Web`) for consumers who prefer it.

> **To activate nuget.org:** create an API key at nuget.org (scoped to push `Asdamir.*`, or "all
> packages" / glob `Asdamir.*`), then add it as the repo secret **`NUGET_API_KEY`** (Settings → Secrets
> and variables → Actions). The next `main` build publishes; before that, the nuget.org step is skipped.

## Scaffolding

| Command | Generates |
|---|---|
| `app new <Name>` | A full application (Server + Gateway) wired to the framework |
| `entity new <Name>` | Entity + DTO + repository + service + controller + tests + migration |
| `page new <Name>` | A Blazor page (+ dialog variant) |
| `module new <Name>` | A self-registering [module](fundamentals/modules.md) project |
| `mobile new <Name>` | A .NET MAUI **Blazor Hybrid** app — `.Mobile` (Android host) + `.Mobile.Shared` (UI) + `.Mobile.Data` (SQLite) + tests. Login, left nav drawer, dashboard; talks to the app's Gateway. See [Mobile App](mobile.md). |
| `add-field` | Adds a field across the entity/DTO/repository/migration set |

Generated output uses the framework's templates (`src/Asdamir.Tools/Templates/*.sbn`) and references the `Asdamir.*` packages.

> **Mobile build:** the MAUI app needs `dotnet workload install maui-android` + an Android SDK platform
> (`dotnet build … -t:InstallAndroidDependencies -f net10.0-android -p:AcceptAndroidSDKLicenses=true`),
> then build a **single RID** — `dotnet build … -f net10.0-android -r android-arm64` (a plain multi-RID
> build fails with `NETSDK1047`). Full recipe in [Mobile App → Build & run](mobile.md#build-run).

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
