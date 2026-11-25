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

Each app keeps its runtime settings **per application** in `dbo.AppConfigurations` (`[Key]`/`[Value]`/`[Category]`, AppId-scoped). The API tier registers that table into `IConfiguration` at startup with `builder.Configuration.AddDatabaseConfiguration(...)` (loads the active rows of the configured scope, refreshes on an interval — see below), so the values are reachable like any other config (`Configuration["Key"]`, `Configure<TOptions>(GetSection(...))`). The UI/client tier never touches the DB — a small endpoint serves the client-facing subset over HTTP (`/gateway/client-settings`).

Seeded keys include `Session:IdleSeconds`/`Session:CountdownSeconds` and the **token lifetimes**
`Jwt:AccessTokenLifetimeMinutes` / `Jwt:RefreshTokenLifetimeDays`. AppManagement reads the latter from
the **issuing app's** `AppConfigurations` when minting that app's JWTs (re-read per login/refresh, so a
change applies without redeploy) — see [Authentication](authentication.md).

### `AddDatabaseConfiguration` — scope, protected keys, failure behaviour

```csharp
builder.Configuration.AddDatabaseConfiguration(connectionFactory, o =>
{
    o.AppId = myAppId;                              // load only this application's rows (+ global rows)
    o.RefreshInterval = TimeSpan.FromMinutes(5);    // default
    // o.IncludeGlobal = true;                      // default — also load AppId IS NULL rows
    // o.ProtectedKeys.Add("MyHost:Secret");        // keys a database row may never supply
});

var app = builder.Build();
app.Configuration.AttachDatabaseConfigurationLogging(app.Services.GetRequiredService<ILoggerFactory>());
```

| `DatabaseConfigurationOptions` | Default | Effect |
|---|---|---|
| `AppId` | `null` | **Set it whenever the table is shared between applications.** Only rows with this `AppId` are loaded, plus the global (`AppId IS NULL`) rows; when both define a key, **the application's own row wins**. Another application's rows are never read. `null` reads the table unscoped (every active row, ordered by key) and does not reference the `AppId` column at all — for a table that belongs to one host alone. |
| `IncludeGlobal` | `true` | With `AppId` set, also load the global rows. |
| `ProtectedKeys` | `Jwt:Key`, `Security:EncryptionKey`, `ConnectionStrings:*`, `Persistence:UseInMemory` | Keys a database row can **never** supply — matching rows are dropped and reported through the logger. The DB source outranks appsettings / user-secrets / environment variables, so without this anyone able to write a config row could replace the signing key or a connection string. Matching is case-insensitive; an entry ending in `:*` protects the whole section. Add host-specific keys, or `Clear()` the set to opt out. |
| `RefreshInterval` | 5 minutes | How often the table is re-read. |

The `AddDatabaseConfiguration(connectionFactory, refreshInterval)` overload is the unscoped form with the same default protected keys.

- **Only `IsActive = 1` rows are loaded**, and keys are **case-insensitive** (as `IConfiguration` keys are).
- **The first load is synchronous** (one attempt, made when the configuration is built), so startup code that reads a DB-backed key sees it. If that first attempt fails the host still starts — without DB-backed values — and the refresh loop retries.
- **A failed reload keeps the last-known-good values.** A transient database error never replaces good configuration with an empty set; the provider keeps serving what it has, retries on the next interval and logs the failure.
- **Logging is attached after `Build()`.** Configuration is built before the host's logging exists, so call `AttachDatabaseConfigurationLogging(...)` once the host is built; it reports a load failure that happened in the meantime and any protected keys that were dropped. It is a no-op when no DB-backed provider is registered.

> **`IsEncrypted` is not acted on by this provider.** A row's `Value` is loaded exactly as stored — there is no decrypt-on-load (and no encrypt-on-write). Keep real secrets in user-secrets / environment variables, which is also why the bootstrap secrets above are protected keys.

## Layering precedence

Standard .NET precedence applies — later sources win. The database provider sits above static JSON so an operator override takes effect without touching files.

## See also

- [Multi-Tenancy](multi-tenancy.md) · [Data Access](data-access.md)
