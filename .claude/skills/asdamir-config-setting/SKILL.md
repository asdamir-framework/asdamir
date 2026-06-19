---
name: asdamir-config-setting
description: Use when adding a runtime/UI tunable (timeout, toggle, limit, etc.) to AppManagement or a generated app. Settings are DB-backed in AppConfigurations (AsdamirVault, AppId-scoped) — never hardcoded in code/markup. Trigger on "add a setting/option/flag", "make X configurable", "where do I put this value", "feature toggle".
---

# Asdamir: add a DB-backed setting

Runtime/UI settings are DB-backed in **`AppConfigurations`** (`[Key]`/`[Value]`/`[IsActive]`/`[Category]`/…),
never literals in code/markup. Per the **CENTRAL model**, `AppConfigurations` lives **once in AsdamirVault,
scoped by `AppId`** — a generated app does **NOT** keep its own `AppConfigurations` table. Deep reference:
`CLAUDE.md` → CENTRAL RULE + "Per-app `AppConfigurations`", `docs/fundamentals/configuration.md`.

## Where config lives + how it's read (two cases)
- **AppManagement (the control plane)** owns AsdamirVault as its OWN database, so its API registers the table
  into `IConfiguration` at startup via Core's DB-backed source —
  `builder.Configuration.AddDatabaseConfiguration(...)` (`Asdamir.Data.Configuration`; loads every `IsActive=1`
  row, refreshes on an interval). Reachable as `builder.Configuration["Key"]`, `GetSection(...)`, and
  strongly-typed `Configure<TOptions>(GetSection(...))`.
- **A generated app** does **NOT** read `AppConfigurations` from its own DB (it has none) and does **NOT** call
  `AddDatabaseConfiguration` against its own DB. Its settings live in AsdamirVault **AppId-scoped** and are
  reached **through AppManagement's API** — exactly like menus/localization (the Gateway proxies
  `gateway/client-settings` → AppManagement, which resolves the app's `AppId` and returns only that app's slice
  via `IOptionsSnapshot<T>`, re-bound per request so a DB edit applies without redeploy). The UI/client tier
  never touches the DB (Layered rule). (This mirrors how mobile localization comes from the Gateway, not a
  local store.)

## To add a tunable
1. **Seed the key** into `AppConfigurations` **in AsdamirVault**, AppId-scoped:
   - AppManagement: an AsdamirVault seed migration (`asdamir-migration`).
   - a generated app: its `db/admin-onboarding/register_<app>.sql` (tagged with the app's `AppId`) — **NOT**
     the app's own `DbSeed.sql` (the app's DB carries business data only).
   Give it a sensible default + `Category`. Example keys: `Session:IdleSeconds`, `Session:CountdownSeconds`,
   `Jwt:AccessTokenLifetimeMinutes`.
2. **Bind it** with a strongly-typed options class on the tier that reads it —
   `Configure<MyOptions>(...GetSection(MyOptions.Section))` (e.g. `Asdamir.Core.Configuration.SessionTimeoutOptions`).
3. **For the UI**, consume it via the client-settings endpoint (AppManagement `GET /api/admin/client-settings`;
   a generated app's `GET /gateway/client-settings`, which proxies to AppManagement) — never read the DB from the UI.
4. Read via `IOptions<T>` / `IOptionsSnapshot<T>` — not `builder.Configuration["…"]` scattered around.

## DON'T
- **Don't bake the value** into a component, `Program.cs`, or `appsettings.json` — it goes in `AppConfigurations`
  (AsdamirVault, AppId-scoped) + an options class.
- **Don't give a generated app its own `AppConfigurations` table**, and **don't call `AddDatabaseConfiguration`
  against the app's own DB** — config is central (AsdamirVault, AppId-scoped), reached via AppManagement's API.
  `AddDatabaseConfiguration` is for **AppManagement** reading its OWN AsdamirVault DB.
- **Don't seed the key into the app's `DbSeed.sql`** — seed it into AsdamirVault via `register_<app>.sql`.
- **Don't read `AppConfigurations` from the UI tier** — surface it via the client-settings API endpoint.
