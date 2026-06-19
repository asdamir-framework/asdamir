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
- New `.cs` files must carry the LGPL-3.0 header (see `packaging/apply-headers.cs`).
- No secrets in `appsettings.json`; DB-backed config/localization; layered architecture (UI → API → DB).

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
