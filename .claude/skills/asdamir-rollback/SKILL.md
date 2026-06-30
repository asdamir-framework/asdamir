---
name: asdamir-rollback
description: Use to UNDO a generated feature on an app built on the framework — remove the entity/page code, drop its app-DB table + migration journal rows, and remove its AsdamirVault menu/permission/grants. DESTRUCTIVE. Trigger on "undo", "rollback", "remove a feature", "tear down", "delete the generated entity", "revert new feature".
---

# Asdamir: rollback (feature teardown)

`asdamir rollback <Name>` is the inverse of `new feature` / `new entity` — it removes a single generated
feature across **all tiers**. It is **DESTRUCTIVE**; read the safety notes before running with `--yes`.
Deep reference: `docs/cli.md` → `rollback`, and `CLAUDE.md` → "Deleting an app registration" (the
app-scale counterpart).

## The model (read first)
Rollback removes, **only what actually exists** (scoped by the entity's name + plural and `AppId` — no
broad globbing):
- **Code** — the Gateway slice (`Domain`/`Dtos`/`Repositories`/`Services`/`Controllers`/`Validators`),
  the Server page (`<Plural>List.razor` + `<Name>EditorDialog.razor` + its DTO), the entity tests, the
  create/seed migrations, and the `localize_`/`seed_menu_` seeds.
- **App DB** (only with a connection) — `DROP TABLE dbo.<Plural>` (if present) + delete the matching
  `dbo.__SchemaMigrations` journal rows, in one transaction.
- **AsdamirVault** (only with `--vault-connection`) — the `<plural>.view` permission, its menu row(s),
  role grants and user-menu permissions, AppId-scoped, in FK order, in one transaction.

## Run it

From the app root (the nearest `.sln`):
```bash
asdamir rollback Supplier \
  --output . \
  -S localhost -d AppDb -U sa -P <pwd> \
  --vault-connection "Server=localhost;Database=AsdamirVault;User Id=sa;Password=<pwd>;TrustServerCertificate=True" \
  --yes
```
- `--output` — app root (default: current dir). `--gateway-dir` / `--server-dir` override detection.
- `--connection`/`-c` or `-S`/`-d`/`-U`/`-P` — app DB (enables the table + journal cleanup).
- `--vault-connection` — AsdamirVault (enables the menu/permission/grant cleanup).
- `--yes`/`-y` — skip the confirmation prompt (for scripts). **Default is interactive.**

## Behavior
- **Interactive by default:** it inventories everything it would remove, prints it, and asks `[y/N]`
  before touching anything. `--yes` skips this.
- **Code is always deleted** (after confirmation). The **app-DB** step runs only with a connection; the
  **AsdamirVault** step only with `--vault-connection`.
- **A missing connection is reported as SKIPPED, never silently dropped** — the code is removed and the
  command warns; re-run later with the connection to finish the DB/menu cleanup.

## DON'T / SAFETY
- **Don't use `--yes` blindly** — the default confirmation is the safety net; review the inventory first.
- **`add field` migrations are NOT rolled back** (`*__add_<field>_to_<plural>.sql`) — they are listed as a
  warning to handle by hand.
- **Mind references** — if other code still uses `<Name>`, that code may stop compiling after rollback
  (the command warns). Verify the build (`asdamir-preflight`) afterwards.
- **Don't hand-delete in a different FK order** — the AsdamirVault delete already runs the correct order
  (`UserMenuPermissions → RolePermissions → Menus → Permissions`, one transaction, `QUOTED_IDENTIFIER ON`).

Related: `asdamir-new-feature` (what it undoes), `asdamir-new-entity` (backend-only slice).
