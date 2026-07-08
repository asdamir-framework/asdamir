---
name: asdamir-data-access
description: Use when writing data-access code (Dapper repositories/stores, DB connections, InMemory/Dapper store pairs) AND when scoping by tenant/company/app — tenant scoping is part of data access here. Trigger on "repository / store", "Dapper", "DbConnection / IDbConnectionFactory", "query the database", "UseInMemory", "InMemory store", "multi-tenant / multi-company / firma", "X-App-Id", "IAppContext / ICompanyContext / ITenantContext", "AppId-scoped / per-tenant data".
---

# Asdamir data access (Dapper) + tenancy scoping

Deep reference: `docs/fundamentals/data-access.md`, `docs/fundamentals/multi-tenancy.md`,
`CLAUDE.md` → Layered + CENTRAL rules + Persistence switch.

## Open connections via the factory — never `new SqlConnection`
There is **ONE canonical interface — `Asdamir.Core.Contracts.IDbConnectionFactory`** (the former duplicate
`Asdamir.Data.Configuration.IDbConnectionFactory` was removed). It has **both** creation paths:
- **`Task<IDbConnection> CreateAsync(CancellationToken)`** — the preferred **runtime** path: returns an
  already-**open**, cancellation-aware connection. Use this in repositories/controllers.
- **`Create()` / `Create(DbProviderType)` / `Create(string)`** — synchronous **bootstrap/provider** path:
  a closed connection for use before the host/DI exists (e.g. `AddDatabaseConfiguration` loading
  `AppConfigurations` at config build) and for explicit provider selection.

**Register it** in the API tier with **`AddDataAccess(connectionString)`** (`Asdamir.Data.DataAccess`),
which wires the concrete **`SqlServerConnectionFactory`** (SQL Server primary; the interface is
multi-provider for Oracle/PostgreSQL). A raw `new SqlConnection(...)` anywhere else is an **audit-lint
AUD002** failure — the factory (and one-shot CLI tools with `// audit-lint:ignore AUD002` + reason) is the
only legitimate place to allocate one.
```csharp
public sealed class DapperFooStore(IDbConnectionFactory factory) : IFooStore
{
    public async Task<...> ListAsync(CancellationToken ct)
    {
        using var conn = await factory.CreateAsync(ct);   // canonical: open, cancellable
        return await conn.QueryAsync(...);
    }
}
```
Data access lives in the **API tier only** — the UI never opens a connection (Layered rule).

> **Multi-company note:** AppManagement layers its OWN *company-aware* concrete factory on this same
> interface (routes to the selected company's DB via `ICompanyCatalog` + the JWT `company` claim). A
> CLI-generated app instead uses the framework's `SqlServerConnectionFactory` bound to its own DB
> (`ConnectionStrings:Default`). Same interface, different concrete — inject the interface, not a concrete.

## The persistence switch
`Persistence:UseInMemory` — `true` swaps every `Dapper*Store` for an `InMemory*Store` (tests/demos; no
DB/secrets); `false` uses SQL via Dapper. When you add a store, provide **both** an `InMemory*Store` and a
`Dapper*Store` and register them in the matching branch of `Program.cs`.

## Tenant / company / AppId scoping (part of data access)
The framework resolves the request's tenancy (`AddMultiTenancy()` + `app.UseMultiTenancy()`) into ambient,
**scoped** contexts: `ITenantContext`, `ICompanyContext`, `IAppContext`.
- **Central data is AppId-scoped.** Management tables (`Users`/`Roles`/`Menus`/`AppConfigurations`/
  `LocalizationResource`/…) live once in AsdamirVault with an `AppId` column. When you read/write them,
  read `IAppContext.AppId` (the selected app — carried as the **`X-App-Id`** header) and pass it to the
  AppId-scoped proc/store. A request with no app selected = admin-pool scope (`AppId` null).
- **Multi-company:** `IDbConnectionFactory` is **company-aware** — it routes to the right company DB via
  `ICompanyCatalog` using the JWT `company` claim. So your store hits the correct DB without per-call work.
- These contexts are **scoped** — inject them per request; never capture them in a singleton (use the
  singleton `*Data` holder pattern below for state that must persist).

## InMemory stores: no static state
Hold an InMemory store's data in **instance fields** (singleton store) or, for a **scoped** store (one
that depends on a scoped service like `IAppContext`), in an injected **singleton `*Data` holder** —
**never `static` fields**. Static state is shared across every `WebApplicationFactory` in a test process
and races under xUnit's parallel test classes (this caused a flaky CI failure; the fix was `*Data` holders).

## DON'T
- **Don't `new SqlConnection(...)`** in a store/service — inject `IDbConnectionFactory` (AUD002).
- **Don't open a DB connection from the UI tier** — call the API.
- **Don't read/return central data without the `AppId` filter** — it would leak across apps.
- **Don't capture scoped `IAppContext`/`ICompanyContext` in a singleton** — inject per request (or use a singleton `*Data` holder).
- **Don't hardcode/scatter a connection string** — register it once via `AddDataAccess(...)` and inject the
  canonical `IDbConnectionFactory` (AppManagement's company-aware concrete resolves the DB per request).
- **Don't use `static` fields in an InMemory store** — use instance fields / a singleton `*Data` holder.
- **Don't add a Dapper store without its InMemory pair** (or vice-versa) — the `UseInMemory` switch needs both.
