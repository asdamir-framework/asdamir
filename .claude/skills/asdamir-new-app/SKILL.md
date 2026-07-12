---
name: asdamir-new-app
description: Use when scaffolding a brand-new application (or mobile app) on the framework — generating the Server + Gateway tiers, wiring secrets, creating its DB, and registering it in AppManagement. Trigger on "new app", "scaffold an app", "create a managed app", "new mobile app", "onboard an app".
---

# Asdamir: scaffold a new app end-to-end

`asdamir new app <Name>` emits a self-contained skeleton: **`<Name>.Server`** (Blazor Web App, the UI
tier) + **`<Name>.Gateway`** (REST API tier) + the app's own DB schema/seed + a
`db/admin-onboarding/register_<name>.sql` (the managed-app registration/integration contract is part of the
commercial AppManagement). Deep reference: `docs/cli.md`, `docs/mobile.md`, and `CLAUDE.md` → CENTRAL +
Layered rules.

> **Project names are DEFAULTS and are overridable at generation.** `<Name>.Server` / `<Name>.Gateway`
> are just the prompted defaults (`AppCommand` asks "UI (Server) proje adı" / "REST API (Gateway) proje
> adı"). A real generated app may carry custom names — e.g. the reference `GeneratedApp` was generated as
> **`GeneratedApp`** (UI) + **`GeneratedApi`** (API) under `src/`. So match the *roles* (UI tier vs
> REST/API-Gateway tier), not the literal `.Server`/`.Gateway` suffixes, when reading an existing app.

## The model (read first)
- **Business data** → the app's **own** DB (starts with `DemoItems`; grow via the `asdamir-new-entity` skill).
- **Identity / menus / RBAC / config / localization / logs** → **central** in AsdamirVault, scoped by the
  app's `AppId`. The app reaches them **only through AppManagement's API**, via its Gateway proxy
  (`gateway/auth/login` → `app-login`, `gateway/menu` → `app-menu`, `gateway/localization` →
  `app-localization`, `gateway/client-settings`).
- **Layered:** the Gateway (API) owns all DB access; the Server (UI) only calls HTTP — no DB creds in the UI.

## Steps
```bash
asdamir new app MyPortal            # → MyPortal.Server + MyPortal.Gateway + db + register_myportal.sql
cd MyPortal
```
1. **Dev secrets on the GATEWAY** (never in appsettings.json):
   ```bash
   cd src/MyPortal.Gateway
   dotnet user-secrets set "Jwt:Key" "<the SAME 64+ byte key AppManagement signs with>"
   dotnet user-secrets set "ConnectionStrings:Default" "Server=...;User Id=...;Password=...;TrustServerCertificate=True"   # cross-platform SQL auth
   cd ../..
   ```
2. `dotnet build MyPortal.sln && dotnet test MyPortal.sln`
3. **Create the app's own (business) DB** with the journaled runner (`asdamir-migration`):
   ```bash
   # No SQL password on the CLI: db apply resolves ConnectionStrings:Default from the Gateway user-secret
   # you set above. (Or pass --server/--database/--user/--password / --connection explicitly.)
   asdamir db apply --create-database --migrations db/migrations
   ```
4. **Register + seed in AppManagement** — run `db/admin-onboarding/register_myportal.sql` **against
   AsdamirVault** (not the app's DB). It registers the app and seeds its starter admin / role /
   permissions / menus / config / localization there, scoped by `AppId`.
5. **Run both tiers:** `dotnet run --project src/MyPortal.Gateway` and `dotnet run --project src/MyPortal.Server`. Sign in with the seeded admin.
6. Add real tables/pages with the `asdamir-new-entity` skill.

## Billing (opt-in `--billing`)
- **Off by default.** `asdamir new app MyPortal --billing` adds an **end-user payment page** (`/billing`):
  plans + current subscription + checkout that **redirects to the tenant's Paddle hosted page** (pass-through
  Merchant-of-Record). Without `--billing` no billing file is emitted (byte-identical scaffold).
- **Commercial (Model A)** — billing data + the Paddle secret live centrally in AsdamirVault (AppId-scoped);
  the app reaches them through AppManagement (`gateway/billing/*` → `api/admin/billing/*`, `AppId` resolved
  from the token's `app_code` claim). Apply `db/admin-onboarding/seed_billing.sql` against AsdamirVault
  **after** `register_<app>.sql` — it seeds the `billing.view` permission + `/billing` menu + `Billing.Page.*`
  localization (tr/en/ru) + the `Payment:Paddle:*` config templates (secrets empty — set the app's own keys).
- **Free (`--mode free`, Model B)** — self-contained: the Gateway serves billing **locally** from the app's
  OWN DB via the open-core `Asdamir.Payments` package (`LocalDbBillingStore` + Paddle/crypto rails + local
  webhook). No control plane, no central secret — the app owns its `Payment:Paddle:*` config. The app-DB
  billing tables are applied by its own `asdamir db apply` (`V*__freemode_billing_*.sql`).

## Mobile (`asdamir new mobile <Name>`)
MAUI Blazor Hybrid app (login, nav drawer, dashboard, 401 refresh, offline cache). Needs
`dotnet workload install maui-android` + an Android SDK platform; build a **single RID**
(`dotnet build … -f net10.0-android -r android-arm64` — a plain multi-RID build fails NETSDK1047). See
`docs/mobile.md`.

## Teardown — `asdamir rollback app <Name>` (the inverse of `new app`)
Undo a whole generated app: it removes the **directory** (the dir whose `<Name>.sln` exists), `DROP DATABASE`s
the app's **own DB** (free **and** commercial), and — commercial + `--vault-connection` — purges the
**AsdamirVault registration** + AppId-scoped rows via `dbo.App_Purge`. DESTRUCTIVE + interactive (shows the full
path + server/db + vault code, asks `[y/N]`; `-y` for scripts). Fail-closed: never drops `AsdamirVault`/system
DBs, and `App_Purge` refuses the self-app; every step is idempotent (missing = "already gone"). Prefer this over
a hand `DROP DATABASE` + `rm -rf`.
```bash
asdamir rollback app MyPortal -o <parent-dir> -S localhost -U sa -P <pwd> --vault-connection "<AsdamirVault>"
```

## DON'T
- **Don't put management tables/seed in the app's own DB** — central seed goes via `register_<app>.sql`
  into AsdamirVault.
- **Don't put a connection string in `appsettings.json`** or in the Server (UI) tier — Gateway-only, via
  user-secrets/env.
- **Don't diverge the `Jwt:Key`** from AppManagement's — the Gateway only validates tokens AppManagement
  issued; keys/issuer/audience must match.
