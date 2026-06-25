# Architecture

How Asdamir fits together: the open-core libraries, the apps you build with them, and how those apps are managed centrally. This page stays at the conceptual level — enough to reason about the moving parts and the boundaries between them.

## The two halves

Asdamir is delivered in two parts:

- **The open core** — four NuGet packages (`Asdamir.Core`, `Asdamir.Data`, `Asdamir.Web`, `Asdamir.Tools`) plus the `asdamir` CLI. This is what you build *on*. LGPL-3.0, 100% offline/self-hosted.
- **AppManagement** — a commercial control plane (admin console + REST API) that registers, configures and operates every app built on the framework, from one place. Its internal implementation is out of scope here; what matters for building apps is the *contract* it exposes (described high-level below).

The framework is intentionally **just libraries** — there is no shared running service you depend on. Cross-version coordination is through SemVer'd NuGet packages.

## Open-core packages

| Package | Responsibility |
| ------- | -------------- |
| **`Asdamir.Core`** | Models, contracts, modules, multi-tenancy (`ITenantContext`), validation, JWT, encryption (AES-256-GCM + PBKDF2), error handling (RFC-7807), Serilog/Polly, options. |
| **`Asdamir.Data`** | Dapper repositories over a provider-agnostic connection abstraction, DB-backed configuration, Hangfire background jobs, the transactional outbox (mail/SMS) with retry/backoff. |
| **`Asdamir.Web`** | Blazor + FluentUI component library, web security (CSP nonce, security headers, rate limiting, Data Protection), DB-backed localization, the shared HTTP/error helpers. |
| **`Asdamir.Tools`** | The `asdamir` CLI — scaffolding (`new app/entity/page/module/mobile`), `audit lint`, journaled `db apply`, `secrets`. |

Each feature self-registers via an `Add…` extension on `IServiceCollection`; the umbrella `AddFramework()` / `UseFramework()` wires the common set.

## A generated app

`asdamir new app` scaffolds a **layered** application:

```text
┌──────────────────────────────────────────────┐
│  Your app                                     │
│                                               │
│  ┌─────────────────┐    HTTP    ┌──────────┐  │
│  │ Server (Blazor) │ ─────────► │ Gateway  │  │
│  │  UI / pages     │            │ REST API │  │
│  └─────────────────┘            └────┬─────┘  │
│   never touches the DB               │        │
│                                      ▼        │
│                          ┌──────────────────┐ │
│                          │ App business DB  │ │
│                          │ (its own data)   │ │
│                          └──────────────────┘ │
└──────────────────────────────────────────────┘
```

**Layered-architecture rule (the key principle):** the **API/Gateway tier owns all data access** — queries, commands, migrations and background/Hangfire jobs run *there*. The UI/client tier **never connects to a database directly** (no connection strings, no repositories in the UI host); it only calls the API over HTTP. So database credentials live on the API tier only, and cross-cutting infrastructure that touches the DB (Hangfire workers, outbox dispatch, the Serilog DB sink) lives on the API tier.

A generated app's **own database holds only its business data**. Scaffold more with `asdamir new entity` / `new page`.

## Central management (high level)

A generated app does **not** keep its own copy of identity, roles, permissions, menus, configuration or localization. That **management data lives centrally** (owned by AppManagement, scoped per app) and the app reaches it **through its own Gateway**, which proxies the relevant calls to AppManagement:

- **Login / identity** — the app shows its own login UI; the Gateway proxies authentication centrally (no local user store).
- **Menus, localization, client settings** — fetched at runtime through the Gateway, so an operator can change them centrally without redeploying the app.

This gives one console to administer many apps, with a consistent security model, while each app keeps its business data to itself. The exact administration surface and onboarding flow are part of the AppManagement (commercial) documentation.

```text
   Generated app  ──HTTP──►  its Gateway  ──►  AppManagement (central control plane)
   (business DB only)          (proxies identity / menus / localization / config)
```

## Security & multi-tenancy

- **Security-first defaults**, not bolted on: JWT with 2FA and refresh-token rotation, RFC-7807 error handling, AES-256-GCM encryption with key-rotation tooling, CSP nonce + security headers, rate limiting, audit logging, PII-safe logging. A static **audit-lint** gate fails the build on anti-patterns.
- **Multi-tenancy** is built in via `ITenantContext` (tenant resolution from claims/header); see [Multi-Tenancy](fundamentals/multi-tenancy.md).
- **Two-tier authorization** — an admin/operator tier (who can administer which app) and a per-app RBAC tier (what an app's own users can do). See [Authorization](fundamentals/authorization.md).

## Persistence switch

Both tiers share one persistence model, toggled by `Persistence:UseInMemory`:

| Value | Stores | Use case |
| ----- | ------ | -------- |
| `true` | In-memory implementations | dev, smoke tests, CI (no DB/secrets needed) |
| `false` | Dapper implementations + SQL | staging, production |

## Where to go next

| You want to… | Read |
| ------------ | ---- |
| Build your first app on the open core | [Getting Started](getting-started.md) |
| Scaffold an app / entity / page | [CLI](cli.md) |
| Understand a specific feature | [Fundamentals](README.md#fundamentals) |
| Ship to mobile | [Mobile (MAUI)](mobile.md) |
