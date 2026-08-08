# Contributing to Asdamir (draft)

> Draft contribution guidelines for the **open core** (LGPL-3.0). AppManagement is closed/commercial and
> is not open to outside contributions.

## Scope
Contributions are welcome to the open-core projects: `Asdamir.Core`, `Asdamir.Data`, `Asdamir.Web`,
`Asdamir.Tools`, the `.claude/skills` + `.claude/agents`, and `docs`. Issues and PRs for AppManagement
are not accepted (it is proprietary).

## Before you start
- Open an issue describing the change first for anything non-trivial.
- Keep PRs focused and small; one concern per PR.

## Quality gate (non-negotiable)
Every change must pass the project's local gate before it can be merged (CI is off):
- `dotnet build Asdamir.sln` — **0 warnings** (`TreatWarningsAsErrors` is on).
- `dotnet test Asdamir.sln` — green.
- `dotnet run --project src/Asdamir.Tools -- audit lint --path src` — no findings.

See the `asdamir-preflight` skill for the full sequence.

## Conventions
- Follow the existing code style and the rules in the relevant `.claude/skills/*/SKILL.md`.
- New `.cs` files must carry the LGPL-3.0 header — copy it verbatim from the top of any existing
  `src/Asdamir.Core/**/*.cs` file. (The header is applied in bulk by a script that lives in the
  development repository's `packaging/` directory, which is not part of this distribution — see
  "What is not in this repository" below.)
- No secrets in `appsettings.json`; DB-backed config/localization; layered architecture (UI → API → DB).

## What is not in this repository — and why

This is the **open-core distribution**, produced from a larger development repository. Two directories a
newcomer often looks for are deliberately absent, and their absence is not an incomplete upload:

- **`tests/`** — the framework's test suites (unit, bUnit, Testcontainers-backed integration and the
  Playwright E2E harness) exercise the open core *together with* AppManagement, the commercial control
  plane. Publishing them would publish the commercial component's expected behaviour in detail, so the
  suites stay in the development repository. The open-core sources here still build standalone:
  `dotnet build` at the repository root is clean.
- **`packaging/`** — the release machinery (the script that produces *this* repository from the
  development one, the leak checks that guard what may be published, the NuGet packing scripts). It
  describes how the distribution is cut, which is of no use inside the distribution itself.

Everything required to **use** the framework is here: the five package sources, the docs, and the LICENSE.
Everything absent is about **producing** it.

## ⚠ Contributor License Agreement (CLA) — important
Asdamir is **dual-licensed** (open core LGPL-3.0 + a commercial license for AppManagement). To keep the
commercial license offerable, the project must hold consistent rights over all contributed code.
**Therefore outside contributions may require signing a Contributor License Agreement (CLA)** assigning
or licensing your contribution to the project before it can be merged.

> This is only a notice. The CLA text itself is a legal document and is **not** drafted here — it should
> be prepared with a lawyer before accepting external contributions. Until a CLA process is in place,
> external PRs may be held.

## Licensing of your contribution
By contributing to the open core you agree your contribution is provided under the project's open-core
license (LGPL-3.0) and, subject to the CLA above, may also be included in the commercially-licensed
distribution.
