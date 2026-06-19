---
name: asdamir-migration
description: Use when writing or applying a SQL schema/seed migration for AsdamirVault (the control-plane DB) or a generated app's own database. Covers the journaled `db apply` runner, idempotency rules, GO batching, and the strict central-vs-business table placement. Trigger on "add a migration", "change the schema", "new table/proc/column", "seed data", "apply migrations".
---

# Asdamir migrations

Migrations are plain `*.sql` applied **in filename order, exactly once each**, by the journaled runner
`framework db apply` (records applied files in `dbo.__SchemaMigrations`; re-runs skip what's done). Deep
reference: `AppManagement/db/README.md`, `docs/cli.md` → `db apply`, memory `2026-06-14-journaled-migration-runner`.

## Where a migration goes (the placement RULE — highest priority)

- **AsdamirVault** (`AppManagement/db/migrations/`) owns ALL the **management/central** tables, with an
  **`AppId` column** separating each app's rows: `Users`, `Roles`, `Permissions`, `RolePermissions`,
  `UserRoles`, `Menus`, `UserMenuPermissions`, `AppConfigurations`, `LocalizationResource`, `AppLog`, …
- **A generated app's own DB** (`<App>/db/migrations/`, e.g. `GeneratedAppDb`) holds **ONLY its business
  data** (starts with `DemoItems`; grows via `framework new entity`).
- **NEVER** put `Users`/`Roles`/`Permissions`/`Menus`/`AppConfigurations`/… or their seed into a generated
  app's `DbSchema.sql` / `DbSeed.sql`. The app reaches that central data through AppManagement's API.
  (See `CLAUDE.md` → the CENTRAL RULE.)

## Writing the file

1. **Name (two conventions — both apply in filename order):**
   - **AsdamirVault control plane** (`AppManagement/db/migrations/`): `AsdamirVault_0XX__<slug>.sql` — `XX` =
     the next number after the highest existing (`ls AppManagement/db/migrations/ | sort | tail -1`).
   - **A generated app's OWN DB** (`<App>/db/migrations/`): a **timestamped Flyway-style** name
     `V<yyyyMMddHHmmss>__<phase>.sql` — e.g. `V20260613065354__schema.sql`, `…__bootstrap.sql`,
     `…__seed.sql` (the CLI's `Migration.sbn` emits `V{{ MigrationStamp }}__<slug>`). The timestamp
     guarantees ordering for app-side migrations added over time.
2. **Be idempotent** (guards everywhere — the runner skips applied files, but idempotency keeps a
   re-apply / partial-failure safe):
   - Tables/indexes/constraints: `IF OBJECT_ID(N'[dbo].[X]', N'U') IS NULL CREATE TABLE …` / `IF NOT EXISTS (…)`.
   - Procedures: `CREATE OR ALTER PROCEDURE …`.
   - Seed rows: `MERGE … WHEN NOT MATCHED THEN INSERT` or `IF NOT EXISTS … INSERT`.
3. **`GO` batches:** the runner splits on lines that are exactly `GO` (SqlClient doesn't understand `GO`).
   `CREATE OR ALTER PROCEDURE` must be the first statement in its batch → put a `GO` before it.
4. **No outer transaction is imposed** by the runner (some migrations self-manage `BEGIN TRAN`; some carry
   DDL that can't run in a transaction). If a migration needs atomicity, wrap it itself.

## Applying + verifying (cross-platform)
```bash
framework db apply --server <sql> --database AsdamirVault --user <login> --password <pwd> \
  --migrations AppManagement/db/migrations
#   add --create-database for a fresh catalog; on Windows you may drop --user for integrated auth.
```
Before pushing, prove it on a throwaway DB: apply (N applied) → re-run (`Done. 0 new … N skipped`).

## DON'T
- **Don't add management tables/seed to a generated app's DB** — central data lives in AsdamirVault, AppId-scoped.
- **Don't edit an already-applied migration** — the runner detects the changed content, warns, and will
  NOT re-run it (so your edit silently won't take). **Add a new migration instead.**
- **Don't rely on a re-apply** of a non-idempotent script — write idempotent guards; the journal is the
  safety net, not an excuse.
