# Asdamir.Data

Data access and background processing for the **Asdamir** framework (.NET 10).

- **Repositories** — Dapper over a multi-provider connection abstraction (SQL Server / Oracle / PostgreSQL)
- **Configuration & feature flags** — database-backed dynamic configuration with per-tenant values (`IFeatureManager`)
- **Background jobs** — Hangfire scheduling, a uniform `IJob` contract, executor and dashboard
- **Transactional outbox** — reliable mail/SMS delivery with claim-batch worker, exponential backoff + jitter, dead-lettering and stale-lock reclaim

## Install

```bash
dotnet add package Asdamir.Data
```

```csharp
builder.Services.AddFeatureManager(builder.Configuration);
builder.Services.AddCoreHangfireJobs(builder.Configuration);
```

## Documentation

Full guides: **[Asdamir documentation](https://github.com/asdamir-framework/asdamir/tree/main/docs)** — see *Data Access*, *Configuration & Feature Flags*, *Background Jobs* and *Transactional Outbox*.

## License

LGPL-3.0 — see the bundled `LICENSE` file.
