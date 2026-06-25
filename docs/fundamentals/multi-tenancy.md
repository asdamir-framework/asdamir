# Multi-Tenancy

**Package:** `Asdamir.Core` · **Namespace:** `Asdamir.Core.MultiTenancy`

## Introduction

Multi-tenancy lets one deployment serve many tenants while keeping their data and configuration isolated. Asdamir resolves the current tenant per request and exposes it through an ambient `ITenantContext` that any service can inject.

## Registration

```csharp
builder.Services.AddMultiTenancy();
...
app.UseMultiTenancy();   // resolves the tenant early in the pipeline
```

`UseMultiTenancy` runs the configured resolver and populates `ITenantContext` for the rest of the request.

## Resolving the tenant

A resolver implements `ITenantResolver`. Two implementations ship out of the box:

| Resolver | Source |
|---|---|
| `ClaimsTenantResolver` | a `TenantId` claim on the authenticated principal |
| `HeaderTenantResolver` | a request header (e.g. `X-Tenant-Id`) |

Provide your own by registering a custom `ITenantResolver`.

## Using the tenant context

```csharp
public sealed class ReportService(ITenantContext tenant)
{
    public Task<Report> BuildAsync()
    {
        var tenantId = tenant.TenantId;   // ambient, per-request
        // scope queries / configuration to this tenant …
    }
}
```

## Configuration

```jsonc
"Tenancy": {
  "HeaderName": "X-Tenant-Id"
}
```

Bind options through `AddMultiTenancy` (it accepts an optional `Action<TenancyOptions>`).

## Tenant-aware features

Other building blocks honour the resolved tenant — for example [database-backed configuration / feature flags](configuration.md) can return tenant-specific values via `IFeatureManager.GetConfigurationAsync<T>(key, tenantId)`.

## AppManagement: the company (firma) model

The framework primitives above (`ITenantResolver`/`ITenantContext`) are the generic, request-scoped
tenant abstraction. **AppManagement** builds a concrete multi-company control plane on top of them —
one management database per company, every app's admin data sliced by `AppId`:

```
Company (firma)                         one management DB per company
  └── App            (Apps row)         CLI-registered; `asdamir app register`
        └── User                        company-unique identity
              └── Permissions           role + menu, scoped by AppId (Tier-2)
```

How a request resolves its slice:

| Dimension | Carried by | Resolved into | Picks |
|---|---|---|---|
| **Company** | JWT `company` claim (chosen pre-login) | `ICompanyContext` | which management **database** (company-aware `IDbConnectionFactory`) |
| **App** | `X-App-Id` request header (chosen post-login) | `IAppContext` | which **AppId slice** of Roles/Permissions/Menus/AppConfig/Localization |

- **Catalog.** Companies + their (encrypted) connection strings live in a bootstrap catalog
  *outside* any company DB — config-backed today (`ICompanyCatalog`), with a `Companies` table as
  the DB-backed alternative. A single-company deployment degenerates to the lone
  `ConnectionStrings:AsdamirVault`, so nothing changes for one company.
- **Admin authz.** AppManagement admins are **company-level full authority**
  (`Users.IsFullAuthority`) — no per-admin role/permission machinery. The role/permission/menu
  tables are exclusively **Tier-2** (the managed apps' end-user RBAC), keyed by `AppId`.
- **Selection flow.** `open → pick company → login → pick app → AppId-scoped reads`. Both pickers
  auto-hide when there's only one choice.
- **Background jobs.** Apps sharing a company's single Hangfire schema isolate via a per-app queue
  + `{appCode}:{jobName}` recurring-id prefix (see [Background Jobs](background-jobs.md)).

Full multi-company architecture is covered in the AppManagement documentation.

## See also

- [Configuration & Feature Flags](configuration.md) · [Authentication](authentication.md)
