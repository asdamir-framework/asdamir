# Asdamir Licensing

Asdamir uses an **open-core / dual-licensing** model (the ABP / Volosoft pattern): an LGPL-3.0 open core
that anyone can build on — including in closed, commercial apps — plus a closed, commercially-licensed
management product (AppManagement).

## What is licensed how

| Project / asset | Bucket | License | Ships in public repo? |
|-----------------|--------|---------|------------------------|
| `src/Asdamir.Core` | open core | **LGPL-3.0** | yes |
| `src/Asdamir.Data` | open core | **LGPL-3.0** | yes |
| `src/Asdamir.Web` (UI components) | open core | **LGPL-3.0** | yes |
| `src/Asdamir.Tools` (CLI `framework`) | open core | **LGPL-3.0** | yes |
| `.claude/skills` (19) | open core | **LGPL-3.0** | yes |
| `.claude/agents` (6) | open core | **LGPL-3.0** | yes |
| `docs/` (excl. `AUDIT.md`, `DEPLOYMENT.md`) | open core | **LGPL-3.0** | yes |
| `AppManagement/src/Asdamir.AdminConsole` | commercial | **Proprietary** (Team/Business/Enterprise) | **no** |
| `AppManagement/src/Asdamir.AdminConsole.Api` | commercial | **Proprietary** (Team/Business/Enterprise) | **no** |
| `tests/Asdamir.Tools.Tests` | dev-only | LGPL-3.0 header (not shipped) | no (deps are public-only) |
| `AppManagement/tests/**` | dev-only | Proprietary header (not shipped) | no |
| `docs/AUDIT.md`, `docs/DEPLOYMENT.md`, `.claude/memory/`, `AppManagement/db/`, `CLAUDE.md` | private-internal | not distributed | no |

Source files carry a matching per-file header (`SPDX-License-Identifier: LGPL-3.0-or-later` for the open
core; `LicenseRef-Asdamir-Commercial` for AppManagement). The full open-core text is in the repo-root
[`LICENSE`](../LICENSE) (LGPL-3.0 + the GPL-3.0 it incorporates); the commercial terms placeholder is
[`LICENSE-COMMERCIAL.txt`](LICENSE-COMMERCIAL.txt).

## License ↔ pricing tiers

| Tier | License | Includes | Price |
|------|---------|----------|-------|
| **Open Source** | LGPL-3.0 | the full open core (framework + CLI + UI + skills + agents + docs) | **free** |
| **Team** | Commercial | open core **+ AppManagement** (control plane, orchestration, AsdamirVault) | commercial _[placeholder]_ |
| **Business** | Commercial | Team + _[placeholder: e.g. priority support, more seats]_ | commercial _[placeholder]_ |
| **Enterprise** | Commercial | Business + _[placeholder: e.g. SLA, on-prem, custom]_ | commercial _[placeholder]_ |

> The open core is genuinely free for commercial use. You only pay when you want the **AppManagement**
> control plane (the part that registers/configures/orchestrates the apps built on the framework).

## The LGPL boundary in one line
Use the open core freely (even in a closed product); if you **modify the open core itself**, publish
those modifications under the LGPL. Code that merely *consumes* Asdamir is unaffected.

## Why dual licensing needs centralized copyright
To offer the commercial license, the project must hold (or be licensed for) all rights in the code it
sells. That's why outside contributions may require a CLA — see [CONTRIBUTING.md](CONTRIBUTING.md).
