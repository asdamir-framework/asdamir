# Demo: Build a Customer/Order app with the Asdamir CLI (offline, AI-free)

> A hands-on tutorial: scaffold a small Customer/Order management web app with the `asdamir` CLI —
> **100% offline, no AI required** — then (optionally) accelerate the integration work with the Claude
> Code agent/skill layer. Every command below was really run and verified. Requires the `asdamir` CLI
> **1.0.4+** (since 1.0.4 the generated app wires its DI automatically; 1.0.5 also localizes nav-menu labels).
> A **mobile (MAUI)** version of the same app can be generated too — see [Mobile](#mobile-maui) below.

## What we build

| Entity | Fields | Relationship |
| ------ | ------ | ------------ |
| `Customer` | Name, Email, Phone? | 1—N Order |
| `Product` | Name, Sku, Price, Stock | 1—N OrderItem |
| `Order` | CustomerId(FK), OrderDate, Status, Total | 1—N OrderItem · N—1 Customer |
| `OrderItem` | OrderId(FK), ProductId(FK), Quantity, UnitPrice | N—1 Order · N—1 Product |

Each entity gets CRUD + a list/form page + a REST API + a migration.

**Layering (the rule):** business data (Customer/Product/Order/OrderItem) lives in the **app's own
database**, reached only through the app's **Gateway (REST API)** — the UI never touches the DB. Identity,
roles, menus and localization are **central** (managed by AppManagement), never copied into the app's DB.

## Prerequisites

- **.NET 10 SDK**.
- The CLI: `dotnet tool install --global Asdamir.Tools` → command `asdamir`.
- Open-core packages from a **local feed** for the offline loop (next step), or pin `Asdamir.* = 1.0.4`
  from nuget.org in the generated `Directory.Packages.props`.
- **SQL Server** is needed to run CRUD (the generated app's business data uses Dapper/SQL). The app boots
  and serves `/gateway/health` without a DB; business endpoints need a connection + applied migrations.

---

## 1. Pack a local preview feed (offline)

The generated app consumes `Asdamir.Core/Data/Web` as `0.1.0-preview.*` from a local feed:

```bash
mkdir -p /tmp/asdamir-demo/feed
for pkg in Core Data Web; do
  dotnet pack <repo>/src/Asdamir.$pkg/Asdamir.$pkg.csproj \
    -c Release -o /tmp/asdamir-demo/feed -p:Version=0.1.0-preview.999999
done
```
> `0.1.0-preview.999999` sorts highest so the float resolves to it. (Online alternative: pin
> `Asdamir.* = 1.0.4` and restore from nuget.org.)

## 2. Generate the app

```bash
mkdir -p /tmp/asdamir-demo/app && cd /tmp/asdamir-demo/app
asdamir new app CustomerOrders --yes --local-feed /tmp/asdamir-demo/feed --output /tmp/asdamir-demo/app
```
Emits a `CustomerOrders.sln` with `src/CustomerOrders.Server` (Blazor Web App), `src/CustomerOrders.Gateway`
(REST API: global exception handling + JWT + health), and `tests/*.Tests`.

## 3. Entities — run from the Gateway (API) project

> FK fields match the `int IDENTITY` primary key, so use **`:int`** (e.g. `CustomerId:int`). The namespace
> is taken from the project automatically — no `--namespace` needed.

```bash
cd /tmp/asdamir-demo/app/CustomerOrders/src/CustomerOrders.Gateway
asdamir new entity Customer  --fields "Name:string,Email:string,Phone:string?"
asdamir new entity Product   --fields "Name:string,Sku:string,Price:decimal,Stock:int"
asdamir new entity Order     --fields "CustomerId:int,OrderDate:datetime,Status:string,Total:decimal"
asdamir new entity OrderItem --fields "OrderId:int,ProductId:int,Quantity:int,UnitPrice:decimal"
```
Each entity → Domain + DTO + Repository(+interface) + Service(+interface) + Controller + Validator +
a migration, plus tests placed in the test project automatically. **Since 1.1.0, 6 tests per entity**
(all DB-free): create/get round-trip, validator rejection, delete, **update round-trip**, **list**
(service-level, in-memory fake repo) + an **API auth-guard** (`WebApplicationFactory`, token-less
`GET` → 401, proving the controller is `[Authorize]`-protected). The WAF fixture (`SmokeFactory`) is
shared with the health smoke test.

## 4. Pages — run from the Server (UI) project

```bash
cd /tmp/asdamir-demo/app/CustomerOrders/src/CustomerOrders.Server
asdamir new page Customer  --fields "Name:string,Email:string,Phone:string?"                    --route /customers
asdamir new page Product   --fields "Name:string,Sku:string,Price:decimal,Stock:int"          --route /products
asdamir new page Order     --fields "CustomerId:int,OrderDate:datetime,Status:string,Total:decimal" --route /orders
asdamir new page OrderItem --fields "OrderId:int,ProductId:int,Quantity:int,UnitPrice:decimal" --route /order-items
```
Each page → a `<Plural>List.razor` (DataGrid + delete confirm) + an `<E>EditorDialog.razor` (form) + a
Server-side DTO (the UI keeps its own copy — layering).

## 5. Build / lint / test — no manual fixes

```bash
cd /tmp/asdamir-demo/app/CustomerOrders
dotnet build CustomerOrders.sln     # → 0 warnings / 0 errors
asdamir audit lint --path src       # → 46 files, 0 findings
dotnet test CustomerOrders.sln      # → 27/27 (26 Gateway + 1 Server)
```
The pure-CLI output **builds clean with zero manual edits** (since 1.0.2).

## 6. To make CRUD fully run (normal integration, not defects)

The output builds and tests green. The runtime wiring is **generated for you**: the Gateway auto-registers
its repositories/services + `AddMultiTenancy()` by convention (since 1.0.4), and the Server ships the
authenticated `AdminApi` client + `AdminAccess` policy + the per-call Bearer token from the login claim
(since 1.0.3) — so clickable CRUD works once the app is wired to its data + identity. What's still app-specific:
1. **Relationships** — an FK-constraint/index migration (the CLI has no relationship concept).
2. **SQL Server** + `ConnectionStrings:Default` + `asdamir db apply` for the business data.
3. **Identity is central** — register the app + an admin in AppManagement (the control plane), and add the page nav entries there (menus are central). See the AppManagement Help for the full run-and-see runbook.

## 7. Run it locally (verified)

The Gateway needs `Jwt:Key` (≥64 bytes) to boot (fail-closed); it boots without a DB:

```bash
cd /tmp/asdamir-demo/app/CustomerOrders/src/CustomerOrders.Gateway
export JWT__KEY="<a 64+ byte dev key>"
ASPNETCORE_ENVIRONMENT=Development dotnet run --no-launch-profile --urls http://localhost:7055
curl -s http://localhost:7055/gateway/health     # → 200 {"status":"ok","app":"CustomerOrders",…}
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:7055/api/customers   # → 401 (auth enforced)
```

## Full login + on-screen CRUD (identity is central)

The generated app's **identity is central** — login is proxied to **AppManagement** (the control plane),
which owns users/roles/menus in a central store, and the JWT it signs is validated by your Gateway
(shared `Jwt:Key`). So the complete *log in → open the pages → add/see data* flow needs AppManagement
running and your app **registered** there with a seeded admin. That full run-and-see runbook (DB setup,
secrets, app registration, both hosts, login + CRUD proof) ships with the AppManagement product Help.
The open-core build above (generate → build 0/0 → run → health) is fully self-contained and offline.

---

## Optional: the Claude Code agent/skill accelerator

The same app can be produced (and finished) by Asdamir's Claude Code layer — a 6-role agent team
(architect, backend, frontend, database, security, reviewer) that reads the project's skills as its source
of truth. The agents **use the same CLI** above, and on top of it they automate what the CLI leaves to you:
the relationship (FK constraint/index) migration with the correct column types, and a non-negotiable quality
gate (0-warning build + tests + audit-lint). (Runtime DI/policy wiring is now emitted by the scaffold itself.)

**Is AI required? No.** The CLI alone is fully offline and AI-free, and its output builds, passes audit
and tests. The agent layer is an **optional accelerator** (it runs with Claude Code) that does the
integration + relationship correctness + review for you.

## Mobile (MAUI)

The same app has a mobile side: `asdamir new mobile CustomerOrders` scaffolds a **.NET MAUI Blazor Hybrid**
app (a Mobile host + a shared Blazor layer + a **real SQLite offline cache**) that talks to the same Gateway
API. Tokens are kept in native secure storage (Keychain/Keystore), login is proxied centrally, and the UI is
localized (TR/EN/RU).

```bash
asdamir new mobile CustomerOrders --gateway-url https://localhost:7001/
# build a single RID (else NETSDK1047), then deploy + run on a running emulator:
dotnet build src/CustomerOrders.Mobile/CustomerOrders.Mobile.csproj -f net10.0-android -r android-arm64 -t:Run
```

> **Scope (honest):** `new mobile` currently generates the **skeleton** — login + dashboard, with the offline
> SQLite cache and secure token storage. Per-entity mobile CRUD screens are not yet auto-generated (roadmap).
> For store publishing the usual steps apply (app icon, signing, privacy policy, versioning). See [Mobile](../mobile.md).

## Verification (this demo)

| Check | Command | Result |
| ----- | ------- | ------ |
| Build 0/0 (no manual fixes) | `dotnet build CustomerOrders.sln` | ✅ 0 warnings / 0 errors |
| audit-lint | `asdamir audit lint --path src` | ✅ 46 files, 0 findings |
| Tests | `dotnet test CustomerOrders.sln` | ✅ 27/27 (26 Gateway + 1 Server) |
| Runs locally | `curl …/gateway/health` | ✅ HTTP 200 |
| Security enforced | `curl …/api/customers` | ✅ HTTP 401 |
