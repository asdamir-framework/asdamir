---
name: asdamir-new-entity
description: Use when adding a new business entity / CRUD slice to an app built on the framework (e.g. Invoice, WorkOrder, Product). Scaffolds entity + DTO + repository + service + controller + tests + migration, and gets the placement right (business data in the app's own DB; never management tables). Trigger on "add an entity", "new table for the app", "CRUD for X", "new business model".
---

# Asdamir: add a business entity

A new entity is **business data** → it belongs to the **app's OWN database**, reached through the **app's
own REST API tier**. The UI never touches the DB. Management data (users/roles/menus/…) is NOT part of
this — it's central in AsdamirVault (see the `asdamir-migration` skill and `CLAUDE.md` → CENTRAL +
Layered rules).

## Scaffold it

Run from the app's API/Gateway project (where data access lives):
```bash
cd src/<App>.Gateway        # the REST API tier of the generated app
framework new entity Invoice --fields "Number:string,Total:decimal,IsPaid:bool,DueAtUtc:datetime"
```
This emits the full slice following audited conventions: **entity + DTO + repository + service +
controller + tests + a migration** for the app's own DB. (`framework add field <Entity> --fields "…"`
later appends a field across the whole set.)

## After scaffolding

1. **Apply the generated migration** to the app's own DB with the journaled runner (see
   `asdamir-migration`):
   ```bash
   framework db apply --server <sql> --database <AppDb> --user <login> --password <pwd> \
     --migrations db/migrations
   ```
2. **UI** — add a page that calls the new controller over HTTP (`framework new page <Name>`). The UI tier
   reaches the data **through the API**, never via a `DbConnection`.
3. **Permissions** — gate the UI/actions by the user's permission claims (RBAC is central in
   AppManagement); register any new permission there, not in the app's DB.
4. **Verify** with the `asdamir-preflight` skill before pushing.

## DON'T
- **Don't add management tables** (`Users`/`Roles`/`Permissions`/`Menus`/`AppConfigurations`/…) or their
  seed to the app's DB — those are central (AsdamirVault, AppId-scoped).
- **Don't access the DB from the UI tier** — no connection strings, no `Dapper*Store`/`DbConnection` in
  UI hosts. The UI calls the API.
- **Don't hand-write the slice** if the scaffolder covers it — generated code already follows the
  conventions `audit-lint` enforces.
