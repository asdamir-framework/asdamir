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

**Order matters, and `AddMultiTenancy()` alone does nothing.** A resolver that reads the authenticated user
(`ClaimsTenantResolver`) only sees a populated `HttpContext.User` **after** `UseAuthentication()`, so place
`UseMultiTenancy()` after it:

```csharp
app.UseAuthentication();
app.UseMultiTenancy();
app.UseAuthorization();
```

If `UseMultiTenancy()` is missing, the first code that reads `ITenantContext.TenantId` during a request fails
with `TenancyNotConfiguredException` (a 500 — it is a host wiring defect). It no longer runs every request as
one tenant.

## Resolving the tenant

A resolver implements `ITenantResolver`. Two implementations ship out of the box:

| Resolver | Source |
|---|---|
| `ClaimsTenantResolver` | the signed `tid` claim on the authenticated principal (`TenantIds.ClaimType`) |
| `HeaderTenantResolver` | a request header (e.g. `X-Tenant-Id`) — any caller can set it; do not use it as a security boundary |

Provide your own by registering a custom `ITenantResolver` **before** `AddMultiTenancy()` (it registers the
header resolver with `TryAdd`). A resolver that cannot identify a tenant returns an unresolved
`new TenantContext()` — it does not invent one.

## There is no default tenant

A missing tenant is an error, not a fallback. With the default `TenancyOptions.MissingTenant = Reject`:

| Situation | Result |
|---|---|
| Authenticated request, token has no `tid` | **403**, error code `tenant.unresolved` (`TenantNotResolvedException`), refused by the middleware before any endpoint runs |
| Anonymous request (health, login) | continues; reading `TenantId` on that request throws the same 403 |
| Authenticated request, no `tid`, endpoint marked `[AllowWithoutTenant]` | continues with an unresolved tenant; reading `TenantId` behind it throws the same 403 |
| `AddMultiTenancy()` without `UseMultiTenancy()` | **500** `TenancyNotConfiguredException` on first tenant read |
| Outside a request (background job) | `ITenantContext` is an unresolved, settable `TenantContext`; the job sets its tenant, reading it first throws |

Where the tenant is only advisory (a cache key, a log tag), check `ITenantContext.IsResolved` instead of
reading `TenantId`.

**The issuer states the tenant.** `IJwtService.IssueTokens(..., tenantId: …)` writes the `tid` claim. A
deliberately single-tenant application passes `TenantIds.Single` (`"default"`), so its tokens say so
explicitly; `asdamir new app` emits exactly that, and the generated sample rows use the same value. To serve
several tenants, pass each user's tenant — nothing downstream changes. Seed `error.tenant.unresolved` for
the user-facing message (the scaffold does, in all three cultures).

**An endpoint for a caller that belongs to no tenant.** Some endpoints serve the application as a whole
and are called by a principal no tenant owns — a control plane reading the app's audit trail, for example.
Mark such an endpoint (controller or action) with `[AllowWithoutTenant]`:

```csharp
[Authorize(Policy = "AppMgmtClient")]
[AllowWithoutTenant]
[Route("gateway/admin")]
public sealed class AuditTrailController : ControllerBase { … }
```

The attribute removes the up-front refusal for that endpoint only. It supplies no tenant (code behind it
that reads `TenantId` still gets the 403) and it authorizes nothing, so pair it with a policy that names
the tenant-less caller. The middleware reads it from the matched endpoint: routing must have run before
`UseMultiTenancy()`, which is the case in a `WebApplication` that does not call `UseRouting()` itself.
Where it has not, the attribute is not seen and the request is refused. The generated Gateway's
`gateway/admin/audittrail` carries it.

**Opting out.** A host that is single-tenant by design and cannot change its issuer may restore the old
behaviour explicitly:

```csharp
builder.Services.AddMultiTenancy(o =>
{
    o.MissingTenant = MissingTenantBehavior.UseDefault;   // every scope without a tenant → DefaultTenantId
    o.DefaultTenantId = "default";
});
```

This removes the tenant boundary for any token that lacks the claim; do not use it in a multi-tenant host.

### Upgrading from 1.x

Before 2.0 a request without a tenant silently ran as `"default"`. After upgrading:

1. Upgrade the **token issuer first** so tokens carry `tid` (for a commercial app that is AppManagement; a
   free-mode app issues its own — regenerate or add the `tenantId:` argument in its `AuthController`).
2. Then upgrade the API. Tokens issued before step 1 are refused until the session refreshes.
3. Custom `ITenantContext` implementations keep compiling (`IsResolved` has a default implementation).
   Code that relied on `TenantContext.TenantId` returning `""` now gets an exception — check `IsResolved`.

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
