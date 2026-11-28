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
| `Encryption` | `null` | An `IEncryptionService`. When set, rows flagged `IsEncrypted = 1` are **decrypted before they reach `IConfiguration`** (see below). When `null`, the `IsEncrypted` column is not read and values load exactly as stored. |
| `RefreshInterval` | 5 minutes | How often the table is re-read. |

The `AddDatabaseConfiguration(connectionFactory, refreshInterval)` overload is the unscoped form with the same default protected keys.

- **Only `IsActive = 1` rows are loaded**, and keys are **case-insensitive** (as `IConfiguration` keys are).
- **The first load is synchronous** (one attempt, made when the configuration is built), so startup code that reads a DB-backed key sees it. If that first attempt fails the host still starts — without DB-backed values — and the refresh loop retries.
- **A failed reload keeps the last-known-good values.** A transient database error never replaces good configuration with an empty set; the provider keeps serving what it has, retries on the next interval and logs the failure.
- **Logging is attached after `Build()`.** Configuration is built before the host's logging exists, so call `AttachDatabaseConfigurationLogging(...)` once the host is built; it reports a load failure that happened in the meantime and any protected keys that were dropped. It is a no-op when no DB-backed provider is registered.

### Encrypted values (`IsEncrypted`)

```csharp
builder.Configuration.AddDatabaseConfiguration(connectionFactory, o =>
{
    o.AppId = myAppId;
    // Built from the configuration available so far (environment / user-secrets):
    o.Encryption = new EncryptionService(builder.Configuration);
});
```

With `Encryption` set, each row flagged `IsEncrypted = 1` is resolved on load, so every consumer — options binding included — receives the secret itself:

| The row holds | Result |
|---|---|
| ciphertext (`v2:` prefix) that decrypts | the **plaintext** is applied |
| ciphertext that does **not** decrypt (wrong key, damaged value) | the key is **dropped** and logged as an error (`CONFIG_SECRET_UNDECRYPTABLE`). Ciphertext is never passed on as if it were the secret, and a global row of the same key does not take its place |
| a value that is not ciphertext | applied as stored and logged as a warning — a secret sitting in plaintext |
| an empty value | stays empty (an unconfigured secret); nothing is logged |

A row that is **not** flagged is never decrypted, whatever its value looks like. `Security:EncryptionKey` itself is a protected key: it must come from the environment or user-secrets, never from the table being read. Produce ciphertext with `asdamir secrets encrypt`, or let a writer that encrypts do it (the AppManagement console encrypts every value saved as *secret*). Without `Encryption` the provider behaves as it always did: no decrypt-on-load.

## Layering precedence

Standard .NET precedence applies — later sources win. The database provider sits above static JSON so an operator override takes effect without touching files.

## See also

- [Multi-Tenancy](multi-tenancy.md) · [Data Access](data-access.md)
