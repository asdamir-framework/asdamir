# Getting Started

This guide gets a new application running on Asdamir in a few minutes.

## Prerequisites

- **.NET 10 SDK**
- **SQL Server** running on `localhost:1433` (local or Docker). Oracle / PostgreSQL are also supported.

> **Package signing.** The `Asdamir.*` packages carry the **nuget.org repository signature** (applied by
> NuGet.org at publish, and what `dotnet nuget verify` checks by default) but **not yet an author
> signature**. If your organisation enforces author-signed packages via `<trustedSigners>` in
> `nuget.config`, restore will be rejected by that policy — author signing is on the roadmap. Stated here
> rather than left to be discovered: a framework whose selling point is verifiability should not be quiet
> about the limits of its own supply chain.

## Scaffold and run your first app

The fastest path is the **`asdamir` CLI** — it generates a complete, run-ready app (both tiers, dev secrets,
database, migrations). This is the **one canonical flow**, identical across the docs, the README and the CLI guide.

```bash
# 1) Install the CLI (once)
dotnet tool install -g Asdamir.Tools

# 2) Create your app — runs ANYWHERE; the app is born in the current folder
mkdir my-apps && cd my-apps
asdamir new app DemoApp --mode free       # files + dev secrets + database + migrations; starter admin printed once

# 3) Run it
cd DemoApp && ./restart-demoapp.sh        # → https://localhost:7010 — sign in with the starter admin

# 4) Add a feature — from INSIDE the app folder
asdamir new feature Product --fields "Name:string,Price:decimal,InStock:bool"
./restart-demoapp.sh                      # → Product appears in the nav menu

# 5) Undo the whole app
asdamir rollback app DemoApp
```

> **`asdamir new app` runs anywhere** (the app is created in the current directory). **Every other command —
> `new feature`, `new entity`, `add field`, `rollback` — runs from _inside_ the app folder.** No `cd src/…`
> and no separate `db apply`: `new app` sets up the DB + migrations, `new feature` applies its own migration,
> then `./restart-<app>.sh` picks up the change.

`--mode free` (above) is **self-contained** and the fastest way to start. **Commercial mode**
(`--mode commercial`, the default) keeps identity/menus/config centrally in **AppManagement** and needs more
setup. Full command reference: **[CLI guide](cli.md)**.

## Add the framework to an existing project

If instead you're adding Asdamir to an app you already have, reference the packages directly. Most apps start
with `Asdamir.Core` and add others as required.

```bash
dotnet add package Asdamir.Core
dotnet add package Asdamir.Data    # data access, configuration, jobs, outbox
dotnet add package Asdamir.Web     # Blazor + FluentUI, web security, localization
```

> Inside this repository the four packages are consumed via **ProjectReference** (lockstep versioning). External consumers use **PackageReference** against the published `Asdamir.*` NuGet packages.

## Wire up the framework

The umbrella extensions register the core building blocks and add the matching middleware to the pipeline:

```csharp
using Asdamir.Core;

var builder = WebApplication.CreateBuilder(args);

// Registers the framework building blocks (error handling, validation,
// correlation IDs, options, Serilog wiring, …).
builder.Services.AddFramework(builder.Configuration);

var app = builder.Build();

// Adds the framework middleware (global exception handling, correlation id, …)
// early in the pipeline.
app.UseFramework();

app.Run();
```

You can also opt in to building blocks individually instead of the umbrella — every feature has its own `Add…` extension:

```csharp
builder.Services.AddGlobalExceptionHandling();
builder.Services.AddValidation();
builder.Services.AddMultiTenancy();
builder.Services.AddModuleSystem();
builder.Services.AddFeatureManager(builder.Configuration);
```

See **[Fundamentals](README.md#fundamentals)** for each feature.

## Configuration & secrets

`appsettings.json` holds **non-secret defaults only**. Secrets come from user-secrets in development and environment variables in CI/production:

| Secret | Development | CI / Stage / Prod |
|---|---|---|
| `Jwt:Key` (≥ 64 bytes) | `dotnet user-secrets` | `Jwt__Key` |
| `ConnectionStrings:Default` | `dotnet user-secrets` | `ConnectionStrings__Default` |
| `Security:EncryptionKey` (≥ 32 chars) | `dotnet user-secrets` | `Security__EncryptionKey` |

```bash
dotnet user-secrets init
dotnet user-secrets set "Jwt:Key" "<64+ byte base64 value>"
dotnet user-secrets set "Security:EncryptionKey" "<32+ char value>"
dotnet user-secrets set "ConnectionStrings:Default" "Server=...;Database=...;"
```

## Run the reference management app

```bash
cd AppManagement/src/Asdamir.AdminConsole.Api
dotnet user-secrets set "Persistence:UseInMemory" "false"   # or "true" to skip the DB
dotnet run
```

The first-run database setup (schema + first SuperAdmin) is handled by AppManagement (the commercial control plane).

## Next steps

- [Authentication](fundamentals/authentication.md) — issue and validate JWTs, enable 2FA
- [Authorization](fundamentals/authorization.md) — permissions, policies, route authorization
- [Architecture](ARCHITECTURE.md) — how the framework and AdminConsole fit together
