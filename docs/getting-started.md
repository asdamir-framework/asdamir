# Getting Started

This guide gets a new application running on Asdamir in a few minutes.

## Prerequisites

- **.NET 10 SDK**
- A database for persistence (SQL Server / Oracle / PostgreSQL). For local exploration the management app can run fully in-memory.

## Install the packages

Reference the packages you need. Most apps start with `Asdamir.Core` and add others as required.

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
