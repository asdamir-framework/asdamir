# Testing the Asdamir skills

A reusable check that the `.claude/skills/` playbooks (1) load, (2) trigger on the right task, and
(3) produce skill-correct output. Run it after adding/editing/merging skills, or when "a skill didn't
fire" is suspected.

## How skills actually work (so you test the right thing)
Skills are **model-invoked**. There are two layers:
- **Description** (the frontmatter line, ~80–160 tokens each) — **always in context**, so it steers the
  model's planning even before any invocation. A correct plan that mentions "3 cultures / AsdamirVault
  migration" is the *description* working.
- **Body** (the full `SKILL.md`) — loads **only when the model invokes the skill** (when it commits to
  executing the task). You'll see a line like `Skill(asdamir-localization) · Successfully loaded skill`.

So "no skill was used" is usually NOT a bug — it means the task stalled before execution (e.g. the model
asked a clarifying question and waited). Skills fire when the task is **executable and unambiguous**.

## 0. Confirm loading (once per session)
1. Launch from the repo root: `cd <repo> && claude` (skills load relative to cwd, up to repo root).
2. If you created/moved `.claude/skills/` while a session was open, **restart** it.
3. In-session: `/skills` → all `asdamir-*` should show `✓ on · project`. `/doctor` → `Search: OK`.

If they don't appear: wrong cwd, session not restarted, or a malformed SKILL.md (need `name:` matching
the dir + a non-empty `description:` ≤ ~1500 chars).

## 1. What "pass" means (three dimensions)
- **Trigger correctness** — the intended skill activates for a realistic prompt (and unrelated prompts
  fire nothing).
- **Selectivity** — one task loads only the relevant skill(s), not 4–5 (this is why we merged the
  co-triggered ones).
- **Content correctness** — when invoked, following it yields the right action (no stale/wrong steps).

To confirm a skill *actually fired* (not just description-steered): after the task, ask **"which skill(s)
did you use?"** — it should name them, and you should have seen `Skill(<name>) · Successfully loaded skill`.

## 2. Trigger matrix
Give each prompt (English or Turkish both work) and record: which skill fired · one or many · output correct.
Prefer **unambiguous, executable** prompts (an ambiguous one legitimately stalls at a clarifying question
— that's correct behavior, not a failure).

| # | Prompt | Expect | Check |
|---|---|---|---|
| 1 | "Add a fixed subtitle under the Apps page title: '…' — tr/en/ru, just text." | localization | 3 cultures seed + in-memory mirror + `L["…"]`; no `.resx` |
| 2 | "Add a `CreatedBy` column to the Invoice table." | migration | idempotent guard + `db apply`; correct DB (app vs AsdamirVault) |
| 3 | "Scaffold a `Product` entity (Name, Price)." | new-entity | into the app's own DB; never management tables |
| 4 | "Create a new app called Acme Portal." | new-app | `register_<app>.sql` step; Gateway secrets |
| 5 | "The combobox dropdown renders behind the next card." | blazor-ui | the z-index trap rule |
| 6 | "Rotate the encryption key." | secret-rotation | dry-run → `--apply` → deploy |
| 7 | "Publish the packages / cut a release." | release | tool → nuget.org/Release (GitHub Packages stalls) |
| 8 | "Make the session timeout configurable." | config-setting | `AppConfigurations` + options + client-settings |
| 9 | "Add SQL query spans to the traces." | observability | `AddSqlClientInstrumentation()`, API tier only |
| 10 | "audit-lint is failing with AUD002." | audit-lint | `IDbConnectionFactory` / deliberate ignore |
| 11 | "I'm about to push — verify everything." | preflight | build 0-warn → tests → audit-lint → push to main |
| **12** | "Secure this new endpoint — who can call it?" | **security** | **exactly ONE** skill (auth+authz merged) — **live-confirmed 2026-06-15** |
| **13** | "Send a welcome email when a user signs up." | **background-work** | **exactly ONE** skill (outbox+jobs merged) — **live-confirmed 2026-06-15** |
| 14 | "Schedule a nightly cleanup job." | background-work | API tier + retry re-throw |
| **15** | "Write a Dapper store for Orders, scoped to the selected app." | **data-access** | **exactly ONE** skill (tenancy folded in) — **live-confirmed 2026-06-15** |
| 16 | "Add a validator for the login request." | validation | request-shape, not auth logic — disambiguated on both descriptions; **live-confirmed 2026-06-15** (was the one overlap) |
| 17 | "Package the Billing feature as a module." | module | — |
| 18 | "Wrap up — save the session memory." | session-memory | bilingual EN + TR |
| **19** | "On the mobile app, where is the auth token stored? Add an offline-capable mobile dashboard." | **mobile** | `ITokenStore`/SecureStorage (not Preferences), named `gateway` client + 401 refresh, `SqliteCacheStore` offline; **NOT** `blazor-ui` as primary — Path-C behavior proxy-validated 2026-06-18 |
| **20** | "Add a Blazor page to the AdminConsole." | **blazor-ui** | web Blazor Server; **NOT** `mobile` — mobile↔web triggers are disjoint |
| N1 | "What's the capital of France?" | **none** | no `asdamir-*` fires — **live-confirmed 2026-06-15** |
| N2 | "Refactor this LINQ for readability." | **none** | generic coding ≠ a skill — **live-confirmed 2026-06-15** (proves no over-triggering) |

## 3. Pass criteria
- Positive rows (1–20): the expected skill is the primary one that fires.
- **Overlap rows (12, 13, 15): exactly ONE skill fires** — proves the merges removed double-loading.
- **Mobile vs web (19, 20): disjoint** — a mobile prompt fires `asdamir-mobile` (not `blazor-ui`); a web
  Blazor prompt fires `asdamir-blazor-ui` (not `mobile`). No double-load. (Live auto-routing of 19/20 needs
  a fresh session; the frontend agent's Path-C reading of `asdamir-mobile` on a mobile task is proxy-validated.)
- Negative rows (N1, N2): no `asdamir-*` skill fires.
- Content: run 2–3 end-to-end (e.g. #1) and confirm the output follows the rule (3 cultures, no `.resx`, …).

## 4. On failure → tune the trigger, not the body
- **Wrong/under-firing:** broaden or sharpen that skill's `description` keywords (the trigger), not its body.
- **Two skills fire for one task:** either a merge is missing, or two descriptions overlap → tighten them.
- **Stale step in output:** the body drifted from the code — fix it and re-run the accuracy check
  (grep the asserted commands/types/procs against the codebase; see how the identity-split drift was caught).

> Note: skills are written content, not battle-tested by their mere existence. Trust them only after a
> green run here, and re-run the relevant rows after any skill or framework change.
