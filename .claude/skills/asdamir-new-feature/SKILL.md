---
name: asdamir-new-feature
description: Use when you want a COMPLETE CRUD feature on an app built on the framework in one command — the entity (Gateway), the page (Server), and the menu/permission seed together, optionally applied. The fast path; prefer this over running new entity + new page + a menu seed by hand. Trigger on "add a feature", "new feature", "scaffold a complete CRUD", "entity plus page", "full CRUD for X end to end".
---

# Asdamir: new feature (one-command CRUD)

`asdamir new feature <Name>` does in ONE command what `new entity` + `new page` + a menu/permission seed
do separately. It is the **primary path for a full, navigable feature**; reach for the individual
commands only when you need just the backend slice (`asdamir-new-entity`) or just a page
(`asdamir-blazor-ui`). Deep reference: `docs/cli.md` → `new feature`, and `CLAUDE.md` → CENTRAL + Layered
rules.

## The model (read first)
`new feature` generates three things and routes each to the right tier (detected by content — Gateway =
`Controllers/` or `db/migrations`; Server = `Components/Pages/`; override with `--gateway-dir` /
`--server-dir`):
- **Entity → the Gateway/API project** — `Domain` + `Dtos` + `Repositories` + `Services` + `Controllers`
  + `Validators` + tests + the create & sample-seed migrations (the app's **own** business DB).
- **Page → the Server/UI project** — `<Plural>List.razor` + `<Name>EditorDialog.razor` + the UI tier's
  own DTO (layering — the UI never touches the DB).
- **Menu/permission + localization seeds → `db/admin-onboarding/`** — `seed_menu_<plural>.sql` (a
  role-based `<plural>.view` permission + an Admin-role grant + a guarded, AppId-scoped `dbo.Menus` row)
  and `localize_<plural>.sql` (the `Page.*` / `Field.*` / `Menu.*` keys in all three cultures). These are
  **central** in AsdamirVault, not the app's own DB.

## Scaffold it

Run from the app root (the nearest `.sln`):
```bash
cd MyApp                                   # the entity migration is applied automatically
asdamir new feature Supplier \
  --fields "Name:string,Phone:string,Email:string" \
  --route /suppliers --icon truck \
  --vault-connection "Server=localhost;Database=AsdamirVault;User Id=sa;Password=<pwd>;TrustServerCertificate=True"
```
- `--fields` — `Name:type,...` (same syntax as `new entity`/`new page`; required).
- `--route` — page route (default `/<plural-lowercase>`). `--icon` — nav-menu icon (default `list`).
  `--policy` — page authorization policy (default `AdminAccess`).
- **The entity migration is applied by default** to the **app DB** via the journaled `db apply` runner —
  the connection resolves from the Gateway user-secret (override with `--connection`/`-S`/`-d`/`-U`/`-P`).
  Pass **`--no-db`** to scaffold files only.
- **`--vault-connection`** applies the menu/permission + localization seeds to **AsdamirVault** — opt-in
  and explicit (no connection guessing).
- `--gateway-dir` / `--server-dir` override the auto-detected projects; `--namespace` overrides the root
  namespace.

## After scaffolding
1. **With `--vault-connection`:** the feature is ready — the entity migration is applied and, sign in as an
   admin, the new menu appears (the seed grants `<plural>.view` to the Admin role, which also has `admin.access`).
2. **Without it (or with `--no-db`):** the entity migration still applies by default (unless `--no-db`), but
   the AsdamirVault seeds are written and not applied — run
   `db/admin-onboarding/{seed_menu,localize}_<plural>.sql` **against AsdamirVault** (see `asdamir-migration`).
3. **Translate** the `tr-TR`/`ru-RU` values in `localize_<plural>.sql` — the generator seeds the English
   name as the default for all three cultures (an untranslated `tr-TR` label renders the wrong dotted-İ
   under the uppercase house style).
4. **Verify** with `asdamir-preflight` before pushing.

## DON'T
- **Don't expect the AsdamirVault seed to auto-apply without `--vault-connection`** — without it the menu
  seed is only generated; the menu won't appear until you apply it.
- **Don't re-run blind hoping to fix a half-run** — generation is idempotent (existing files are skipped,
  never overwritten); if the **entity step fails the page is not generated** (fail-fast) — fix the error
  and re-run.
- **Don't seed management tables into the app's own DB** — menus/permissions are central (AsdamirVault).
- To undo a feature, use the `asdamir-rollback` skill — don't hand-delete files + tables.

Related: `asdamir-new-entity` (backend slice only), `asdamir-blazor-ui` (page/UI styling),
`asdamir-migration` (`db apply`), `asdamir-rollback` (tear a feature down).
