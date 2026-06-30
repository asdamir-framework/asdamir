# Asdamir Agent Team

A 6-role Claude Code subagent team for building on the **Asdamir** framework (.NET 10 + Blazor +
the AppManagement control plane), plus the **main session as orchestrator**. The agents reuse the
project's 21 skills as their single source of truth.

## The critical constraint (and how we solve it)
**Subagents do NOT load project skills** — calling `Skill(asdamir-…)` inside a subagent fails with
"Unknown skill" (proven, see `.claude/memory/2026-06-15-skills-and-testing-matrix.md`). So each agent
instead **reads the relevant `SKILL.md` files from disk** and follows them (their system prompts begin
with "read these files first"). This keeps the skills the single source of truth — no rule duplication,
no drift.

> **Path C — live-validated (2026-06-15):** a probe subagent given "read these SKILL.md, then plan
> Invoice for GeneratedApp" actually opened the files, quoted them, applied the CENTRAL model correctly
> (Invoice → the app's own DB, not AsdamirVault), and read the *corrected* `asdamir-security` skill to
> report the right RBAC table (`dbo.UserAppRoles`, not the legacy `AdminUserAppRoles`). The architecture
> works **and** produces drift-free output because agents read the current skills.

## The team
| # | Agent | Role |
|---|-------|------|
| 0 | **Orchestrator** (main session) | Splits the task, calls the right agent in the right order, integrates, writes session memory. The only place skills load via `Skill()`. |
| 1 | **architect** | Request → numbered WORK ORDER; business-vs-management data placement (CENTRAL model), which tier/DB. No code. |
| 2 | **backend-engineer** | API/Gateway C#: entity/DTO/repo/service/controller, error-handling, background-work, config, observability. |
| 3 | **frontend-engineer** | Blazor + FluentUI (web) **and MAUI Blazor Hybrid (mobile)**: scoped CSS (no inline styles), DB-backed localization (3 cultures); mobile token store / gateway client / offline cache. |
| 4 | **database-engineer** | Idempotent SQL migrations/seed, journaled runner, correct DB placement, multi-DB dialect. |
| 5 | **security-engineer** | Auth logic: JWT/RBAC/endpoint guarding (fail-closed), permission seeding, secret rotation. |
| 6 | **reviewer-release** | Quality gate: audit-lint + preflight (0-warning build + tests) + validation; manual release. |

## Orchestration flow (example: "Add an Invoice module to GeneratedApp")
```
[Orchestrator]  read session memory → plan
   → [architect]          work order: Invoice = BUSINESS data → GeneratedAppDb (NOT AsdamirVault)
   → [database-engineer]  idempotent Invoice migration (GeneratedAppDb)
   → [backend-engineer]   repo via IDbConnectionFactory; controller; error-handling wired
   → [security-engineer]  [Authorize(Policy=…)] + permission seed (AsdamirVault, AppId-scoped → hands to DB eng.)
   → [frontend-engineer]  Invoice.razor + .razor.css + tr/en/ru localization keys
   → [reviewer-release]   audit-lint clean? preflight green (0 warnings + tests)?
[Orchestrator]  integrate → PASS = done → write bilingual session memory
```
Subagents don't share context — the **orchestrator** carries each agent's output into the next agent's
task prompt (e.g. the security agent's permission-seed need is handed to the database agent).

## Agent ↔ skill coverage (● primary · ○ secondary)
| Skill | architect | backend | frontend | database | security | reviewer | orch. |
|------|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| new-app / new-entity / new-feature / module | ● | ○(entity) | | | | | ○ |
| rollback (feature teardown) | | | | ● | | | ○ |
| data-access | | ● | | ● | | | |
| error-handling / background-work / config-setting / observability | | ● | | | | | |
| blazor-ui / localization | | | ● | | | | |
| mobile (MAUI Hybrid) | | | ● | | | | |
| migration | | | | ● | | | |
| security / secret-rotation | | | | | ● | | |
| audit-lint / preflight / release | | | | | | ● | |
| validation | | ○ | | | ○ | ● | |
| session-memory | | | | | | | ● |

Every skill is owned by exactly one agent (or the orchestrator); `data-access` (backend C# + database
SQL) and `validation` (reviewer primary, backend/security advisory) are deliberate collaboration seams,
not overlaps.

## Test matrix (run in a FRESH `claude` session — agents added mid-session don't load)
Confirm with `/agents` that the 6 appear. Then give each prompt and record which agent(s) fire and in
what order. Path C (agents read SKILL.md) is already validated; these cover triggers + handoffs + the gate.

| # | Prompt | Expected | Check |
|---|--------|----------|-------|
| 1 | "Plan adding an Invoice module to GeneratedApp." | architect (first) | work order; Invoice→app DB, not AsdamirVault; steps assigned to agents |
| 2 | "Write the repository + controller for Orders." | backend-engineer | IDbConnectionFactory; error-handling; no SQL DDL |
| 3 | "Add the Orders list page." | frontend-engineer | scoped CSS, 3-culture localization, calls API only |
| 4 | "Write the migration for the Orders table." | database-engineer | idempotent; correct DB; no management tables |
| 5 | "Secure the Orders endpoints — who can call them?" | security-engineer | [Authorize] + permission seed to AsdamirVault; reads asdamir-security |
| 6 | "I'm ready to push — verify everything." | reviewer-release | preflight (0-warn + tests) + audit-lint; refuses if red |
| 7 | (full chain) "Add an Invoice feature end to end." | architect → db → backend → security → frontend → reviewer | orchestrator sequences; reviewer gates last |
| N1 | "What's the capital of France?" | none | no agent fires |

**Pass criteria:** rows 1–6 fire the expected agent as primary; row 7 runs the chain with reviewer last
and the orchestrator carrying outputs between agents; N1 fires nothing. If an agent mis-fires, tune its
`description` (the trigger) — not its body. If output drifts, the fix is in the relevant `SKILL.md`
(single source of truth), not the agent.

**Validation status (2026-06-15):** **fully validated — 8/8.** First, each agent's *behavior* was
**proxy-validated** — every agent prompt was run against its matrix task and correctly read the right
SKILL.md, stayed in its lane, and produced drift-free output (e.g. security → `dbo.UserAppRoles`, not the
legacy name; reviewer ran audit-lint — src/AppManagement clean — and **refused "done"** with the
build/test gate unverified). Then, in a **fresh session**, the two remaining layers were confirmed:
**auto-routing** (rows 1–6 each selected the correct primary agent from `description` alone; N1 fired
nothing) and **subagent→subagent handoff** (row 7 — architect→db→backend→security→frontend→reviewer ran
with the orchestrator threading each agent's output into the next: architect's fields → db's table shape →
backend's policy list → security's claims+seed need → frontend's localization keys → reviewer's gate,
which **refused to clear** "no real diff = FAIL by default"). Descriptions are non-overlapping; no
mis-fires.

## Maintenance
Agents read skills; they don't memorize rules. When the framework changes, update the **SKILL.md** — the
agents inherit it automatically. Keep agent `description` fields **non-overlapping** so the orchestrator
selects the right one (the agent-level version of the skills' TESTING.md discipline).
