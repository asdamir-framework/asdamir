# Data Access

**Package:** `Asdamir.Data` · **Namespace:** `Asdamir.Data`, `Asdamir.Core.Contracts`

## Introduction

Asdamir uses **Dapper** for data access — fast, explicit SQL with no change-tracking surprises — over a provider-agnostic connection abstraction so the same repository code runs against **SQL Server, Oracle or PostgreSQL**.

## Connection abstraction

`IDbConnectionFactory` (`Asdamir.Core.Contracts`) hands out open `IDbConnection`s; `IDbProvider` describes the active database engine (`DbProviderType`). Repositories depend on the factory, not on a concrete provider:

```csharp
public sealed class OrderRepository(IDbConnectionFactory factory)
{
    public async Task<Order?> GetAsync(Guid id)
    {
        using var conn = factory.Create();
        return await conn.QuerySingleOrDefaultAsync<Order>(
            "dbo.Order_Get", new { id }, commandType: CommandType.StoredProcedure);
    }
}
```

> The management app uses an async factory variant (`CreateAsync(ct)`) for its stores; pick the shape that matches your hosting.

## Conventions

- **Stored procedures over inline SQL** where practical — the management app routes every operation through canonical `dbo.*` procs.
- Wrap connections (and Dapper `using var` readers) in `using` so they're disposed deterministically.
- Pass `CancellationToken` through `CommandDefinition`.

## Custom type handlers

Register Dapper type handlers (e.g. JSON columns ⇄ `string[]`) once at startup. The management app ships a `JsonStringArrayHandler` as a reference.

## Migrations

Schema is managed with ordered SQL migration scripts. The CLI can scaffold a new migration:

```bash
dotnet run --project src/Asdamir.Tools -- entity new Order   # generates entity + repository + migration
```

## See also

- [Configuration & Feature Flags](configuration.md) · [Background Jobs](background-jobs.md) · [CLI](../cli.md)
