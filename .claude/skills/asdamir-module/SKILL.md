---
name: asdamir-module
description: Use when packaging a feature as a self-registering module (IModule) or scaffolding a new module project. Trigger on "add a module", "IModule", "self-registering feature", "framework new module", "plug-in / feature package".
---

# Asdamir modules

Deep reference: `docs/fundamentals/modules.md`, `CLAUDE.md` → DI entry points.

A **module** packages a feature as a self-registering unit with a managed lifecycle — implement `IModule`
and it's discovered + wired without hand-editing the composition root.

## Add one
```bash
framework new module Billing          # scaffolds a self-registering IModule project
```
- Implement `IModule` (registration + lifecycle hooks); the module system (`AddModuleSystem()`) discovers
  and orders it. Keep the module's services behind its own `Add…` extension so it composes cleanly.
- A module follows the same rules as the rest of the framework: layered (API owns data), DB-backed
  config/localization, `audit-lint`-clean, no secrets in `appsettings.json`.

## DON'T
- **Don't hand-wire a feature into the root `Program.cs`** when it should be a discoverable `IModule`.
- **Don't bypass the module's `Add…` extension** — keep registration encapsulated.
- **Don't break the layered/central rules inside a module** (see `asdamir-new-entity`, `asdamir-data-access`).
