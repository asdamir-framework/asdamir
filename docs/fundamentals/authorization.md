# Authorization

**Packages:** `Asdamir.Core` (permission/policy constants), `Asdamir.Web` (route authorization, audit)

## Introduction

Authorization in Asdamir is **permission-based**: roles are granted permissions, policies map to those permissions, and endpoints/pages require a policy. The framework adds route-level authorization, an authorization cache and an authorization audit trail on top of ASP.NET Core's primitives.

## Registration

```csharp
builder.Services.AddBasicAuthorization();        // policy + permission plumbing
// or, with the enterprise add-ons (cache, audit, rate-limited checks):
builder.Services.AddEnterpriseAuthorization();
```

## Permissions & policies

Permission and policy names are centralized as constants in `Asdamir.Core.Authorization`:

```csharp
using Asdamir.Core.Authorization;

[Authorize(Policy = PolicyNames.ProductsRead)]
public IActionResult List() => ...;
```

Define permissions once and reference them everywhere — this keeps endpoint guards, role definitions and the admin UI in sync.

## Route authorization (Blazor / middleware)

`Asdamir.Web.Security` provides route-level authorization so unauthenticated/unauthorized navigation is blocked before a page renders, plus components such as `AuthorizeView`, `AuthenticationGuard` and `AccessDenied`. Authorization decisions are cached (route-normalized — the query string is stripped so high-cardinality URLs don't explode the cache) and can be audited.

## Two-tier RBAC (AdminConsole)

The management application layers two independent RBAC scopes:

1. **Admin-pool RBAC** — gates which *managed apps* an operator may administer. SuperAdmins reach every app; everyone else reaches only the apps assigned to them.
2. **Managed-app RBAC** — each managed app defines its own roles, permissions and role assignments for its end users.

See [Architecture → Security & multi-tenancy](../ARCHITECTURE.md#security-multi-tenancy).

## Authorization audit

Authorization decisions (grants and denials) can be written to the audit trail for security review — see [Audit Logging](audit-logging.md).

## See also

- [Authentication](authentication.md) · [Web Security](../web-security.md) · [Audit Logging](audit-logging.md)
