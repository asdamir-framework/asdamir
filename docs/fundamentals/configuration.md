# Configuration & Feature Flags

**Package:** `Asdamir.Data` · **Namespace:** `Asdamir.Data.Configuration`

## Introduction

Beyond `appsettings.json`, Asdamir supports **database-backed dynamic configuration**: settings and feature flags that operators change at runtime — per tenant — without a redeploy. Values are layered on top of the standard .NET configuration system and cached for performance.

## Registration

```csharp
builder.Services.AddFeatureManager(builder.Configuration);
```

This wires the `IFeatureManager`, the dynamic configuration source and its caching.

## Feature flags & typed configuration

```csharp
public sealed class CheckoutService(IFeatureManager features)
{
    public async Task CheckoutAsync(string? tenantId)
    {
        if (await features.IsEnabledAsync("BulkOrdering", tenantId))
        {
            var cfg = await features.GetConfigurationAsync<DiscountConfig>("OrderDiscounts", tenantId);
            // apply tenant-specific configuration …
        }
    }
}
```

- `IsEnabledAsync(name, tenantId?)` — check a flag globally or for a specific tenant.
- `GetConfigurationAsync<T>(key, tenantId?)` — fetch and deserialize typed configuration.

A `null` `tenantId` reads the global value; a tenant value overrides the global one (see [Multi-Tenancy](multi-tenancy.md)).

## Dynamic configuration provider

`AddFeatureManager` also contributes an `IConfigurationProvider` that surfaces database values through `IConfiguration`, so existing `IOptions<T>` bindings can read DB-managed settings. Refreshes are cached to keep the hot path cheap.

### Per-app DB configuration (`AppConfigurations`)

Each app keeps its runtime settings **per application** in `dbo.AppConfigurations` (`[Key]`/`[Value]`/`[Category]`, AppId-scoped). The API tier registers that table into `IConfiguration` at startup with `builder.Configuration.AddDatabaseConfiguration(...)` (loads every `IsActive=1` row, refreshes on an interval), so the values are reachable like any other config (`Configuration["Key"]`, `Configure<TOptions>(GetSection(...))`). The UI/client tier never touches the DB — a small endpoint serves the client-facing subset over HTTP (`/gateway/client-settings`).

Seeded keys include `Session:IdleSeconds`/`Session:CountdownSeconds` and the **token lifetimes**
`Jwt:AccessTokenLifetimeMinutes` / `Jwt:RefreshTokenLifetimeDays`. AppManagement reads the latter from
the **issuing app's** `AppConfigurations` when minting that app's JWTs (re-read per login/refresh, so a
change applies without redeploy) — see [Authentication](authentication.md).

## Layering precedence

Standard .NET precedence applies — later sources win. The database provider sits above static JSON so an operator override takes effect without touching files.

## See also

- [Multi-Tenancy](multi-tenancy.md) · [Data Access](data-access.md)
