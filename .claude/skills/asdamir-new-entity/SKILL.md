---
name: asdamir-new-entity
description: Use when adding a new business entity / CRUD slice to an app built on the framework (e.g. Invoice, WorkOrder, Product). Scaffolds entity + DTO + repository + service + controller + tests + migration, and gets the placement right (business data in the app's own DB; never management tables). Trigger on "add an entity", "new table for the app", "CRUD for X", "new business model".
---

# Asdamir: add a business entity

A new entity is **business data** → it belongs to the **app's OWN database**, reached through the **app's
own REST API tier**. The UI never touches the DB. Management data (users/roles/menus/…) is NOT part of
this — it's central in AsdamirVault (see the `asdamir-migration` skill and `CLAUDE.md` → CENTRAL +
Layered rules).

> **Want the WHOLE feature (entity + page + menu + permission) in one command? Use `asdamir-new-feature`.**
> This skill scaffolds just the **backend slice**; `new feature` wraps it with the page and the
> menu/permission seed. Reach for `new entity` alone when you only need the API side.

## Scaffold it

Run it **from the app root** — `new entity` finds the Gateway project itself (nearest `.sln` → `src/<App>.Gateway`):
```bash
cd MyApp                     # the app root — no cd into src/…
asdamir new entity Invoice --fields "Number:string,Total:decimal,IsPaid:bool,DueAtUtc:datetime"
# files only (offline / CI / review-first — don't touch SQL):
asdamir new entity Invoice --fields "…" --no-db
```
This emits the full slice following audited conventions: **entity + DTO + repository + service +
controller + tests + a migration** for the app's own DB, and **applies the migration by default** via the
journaled runner — resolving the connection from the Gateway user-secret `ConnectionStrings:Default` (the
same passwordless resolution as `db apply`; explicit `--connection`/`-S`/`-d`/`-U`/`-P` override it). No
`cd`, no separate `db apply`. Running from inside the Gateway dir still works (backward-compatible).
Pass **`--no-db`** to scaffold files only; if no connection is resolvable the migration is still generated
and the command prints the `db apply` recovery line. It does not create the DB.
(`asdamir add field <Entity> --fields "…"` later appends a field across the whole set — also auto-applied.)

## After scaffolding

1. **The migration is already applied** (unless you passed `--no-db`). To apply it later — a teammate clones
   the repo, CI, prod — the journaled runner is still there (idempotent; skips applied migrations):
   ```bash
   # No SQL password on the CLI: db apply resolves ConnectionStrings:Default from the app's Gateway
   # user-secret. (Or pass --server/--database/--user/--password / --connection explicitly.)
   asdamir db apply --migrations db/migrations
   ```
2. **UI** — add a page that calls the new controller over HTTP (`asdamir new page <Name>`). The UI tier
   reaches the data **through the API**, never via a `DbConnection`. `new page` also emits the page's
   **`seed_menu_<plural>.sql`** (role-based menu + permission) and `localize_<plural>.sql` — apply them to
   AsdamirVault. (Or do the entity + page + menu in one shot with `asdamir-new-feature`.)
3. **Permissions / menu** — these are now **seeded for you** by `new page`/`new feature`:
   `seed_menu_<plural>.sql` creates a role-based `<plural>.view` permission + an Admin-role grant + a
   guarded `dbo.Menus` row (AppId-scoped, in AsdamirVault). Apply it there; grant the permission to other
   roles from the AppManagement UI. Registering by hand stays a valid alternative.
4. **Verify** with the `asdamir-preflight` skill before pushing.

## DON'T
- **Don't add management tables** (`Users`/`Roles`/`Permissions`/`Menus`/`AppConfigurations`/…) or their
  seed to the app's DB — those are central (AsdamirVault, AppId-scoped).
- **Don't access the DB from the UI tier** — no connection strings, no `Dapper*Store`/`DbConnection` in
  UI hosts. The UI calls the API.
- **Don't hand-write the slice** if the scaffolder covers it — generated code already follows the
  conventions `audit-lint` enforces.
