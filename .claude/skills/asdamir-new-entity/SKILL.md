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

Run from the app's API/Gateway project (where data access lives):
```bash
cd src/<App>.Gateway        # the REST API tier of the generated app
asdamir new entity Invoice --fields "Number:string,Total:decimal,IsPaid:bool,DueAtUtc:datetime"
# optional: apply the migration immediately (opt-in; default is files-only = review-first)
asdamir new entity Invoice --fields "…" --apply -S <sql> -d <AppDb> -U <login> -P <pwd>
```
This emits the full slice following audited conventions: **entity + DTO + repository + service +
controller + tests + a migration** for the app's own DB. By default it only **writes files** (review
first); add **`--apply`** (+ a connection — same flags as `db apply`) to run the migration right away via
the journaled runner (it errors if no connection is given, and does not create the DB).
(`asdamir add field <Entity> --fields "…"` later appends a field across the whole set.)

## After scaffolding

1. **Apply the generated migration** to the app's own DB with the journaled runner (see
   `asdamir-migration`) — **or** skip this by passing `--apply` to `new entity` above:
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
