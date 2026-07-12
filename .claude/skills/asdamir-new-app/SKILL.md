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
`new app` asks for the SQL user + a **masked** password, then makes the app run-ready end-to-end:
auto-configures the Gateway dev user-secrets (CSPRNG `Jwt:Key` in free mode + `Security:EncryptionKey` +
`ConnectionStrings:Default` — in **user-secrets, NEVER appsettings.json**) **AND creates the DB + applies every
migration** (reusing the idempotent `db apply --create-database` runner). So a **free** app is generate → run:
```bash
asdamir new app MyPortal --mode free       # asks SQL user + password → secrets + DB created + migrations applied
cd MyPortal && ./restart-myportal.sh       # both tiers → sign in with the starter admin printed above
```
Notes:
- **`--no-db`** → files only, don't touch SQL (offline/CI); `new app` prints the `db apply` line. **Empty
  password** → same (no connection → DB skipped). **DB setup failure** → files still generated + the exact
  `cd <app> && asdamir db apply …` recovery command is printed. **`--no-secrets`** → skip user-secrets auto-config.
- **`db apply` stays** — it's what you run over the app's lifetime (after `new entity`, on clone, in CI/prod);
  `new app` just runs it once at creation.
- **Commercial mode** is the same **except `Jwt:Key`**: it MUST equal AppManagement's signing key (the CLI can't
  know it), so it stays manual — and you also run `db/admin-onboarding/register_<app>.sql` against **AsdamirVault**
  (registers the app + seeds its users/roles/permissions/menus/config/localization, AppId-scoped).
- Optional: `dotnet build/test MyPortal.sln`; add features **from the app root** (`cd MyPortal`, then
  `asdamir new feature <Name> --fields "…"` — entity + page + menu, migration applied automatically; or
  `asdamir-new-entity` for the API-only slice). Undo the whole app with `asdamir rollback app MyPortal` (see Teardown).

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
