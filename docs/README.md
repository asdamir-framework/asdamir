# Asdamir Documentation

Welcome to the Asdamir documentation. Asdamir is a security-first application framework for **.NET 10 + Blazor**, delivered as four NuGet packages (`Asdamir.Core`, `Asdamir.Data`, `Asdamir.Web`, `Asdamir.Tools`) plus a reference management application (`AppManagement`).

> New here? Start with **[Getting Started](getting-started.md)**, then skim the **Fundamentals** for the building blocks you need.

## Getting started

| Page | What you'll learn |
|---|---|
| [Getting Started](getting-started.md) | Install the packages, wire up `AddFramework()` / `UseFramework()`, and run your first app |
| [Architecture](ARCHITECTURE.md) | Platform topology, repository roles, two-tier RBAC, cross-app orchestration |

## Fundamentals

The framework is organized as composable building blocks. Each can be enabled independently via its `Add…` extension on `IServiceCollection`.

| Page | Package | Summary |
|---|---|---|
| [Modules](fundamentals/modules.md) | `Asdamir.Core` | Package features as self-registering `IModule`s with a managed lifecycle |
| [Authentication](fundamentals/authentication.md) | `Asdamir.Core` / `Asdamir.Web` | JWT issuance/validation, refresh-token rotation, two-factor (2FA) |
| [Authorization](fundamentals/authorization.md) | `Asdamir.Core` / `Asdamir.Web` | Permissions, policies, route authorization, two-tier RBAC |
| [Multi-Tenancy](fundamentals/multi-tenancy.md) | `Asdamir.Core` | Tenant resolution (claims/header) and the ambient `ITenantContext` |
| [Validation](fundamentals/validation.md) | `Asdamir.Core` | FluentValidation, custom attributes, and the business-rule engine |
| [Error Handling](fundamentals/error-handling.md) | `Asdamir.Core` | `Result`, RFC-7807 ProblemDetails, error translation, dead-letter queue |
| [Data Access](fundamentals/data-access.md) | `Asdamir.Data` | Dapper repositories over a multi-provider connection abstraction |
| [Configuration & Feature Flags](fundamentals/configuration.md) | `Asdamir.Data` | Database-backed dynamic configuration and feature toggles |
| [Background Jobs](fundamentals/background-jobs.md) | `Asdamir.Data` | Hangfire-based scheduling, execution and dashboard |
| [Transactional Outbox](fundamentals/outbox.md) | `Asdamir.Data` | Reliable mail/SMS delivery with retry, backoff and dead-lettering |
| [Localization](fundamentals/localization.md) | `Asdamir.Web` | API-backed, multi-culture string localization with caching |
| [Audit Logging](fundamentals/audit-logging.md) | `Asdamir.Core` / `Asdamir.Web` | Capture security-relevant actions to an audit trail |
| [Encryption](fundamentals/encryption.md) | `Asdamir.Core` | AES-GCM encryption + PBKDF2 key derivation for data at rest |
| [Observability](fundamentals/observability.md) | `Asdamir.Core` / API tiers | Serilog logs, correlation IDs, **OpenTelemetry** traces+metrics (incl. SQL/Dapper), health probes |

## Web & UI

| Page | Summary |
|---|---|
| [Web Security](web-security.md) | CSP nonce, security headers, rate limiting, Data Protection, auto-logout |
| [UI Components](ui-components.md) | The FluentUI Blazor component library (DataGrid, dialogs, charts, export…) |

## Tooling & operations

| Page | Summary |
|---|---|
| [CLI (`asdamir`)](cli.md) | Scaffolding, `audit lint`, `db apply` (journaled migrations), `secrets` |
| [Web Security](web-security.md) | CSP, headers, rate limiting (scale-out), Data Protection keys |
| [Secret Management & Rotation](secret-rotation.md) | Secret inventory + EncryptionKey / Jwt:Key / client-secret rotation |
| [Mobile App (MAUI)](mobile.md) | MAUI Blazor Hybrid scaffold — login, nav drawer, offline cache |

## Conventions used in these docs

- Code samples target **.NET 10** and assume a top-level `Program.cs`.
- `IServiceCollection` extensions are named `Add…`; pipeline (`IApplicationBuilder`) extensions are named `Use…`.
- Secrets are never read from `appsettings.json` — see [Getting Started → Configuration](getting-started.md#configuration-secrets).
