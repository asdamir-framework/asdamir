---
name: asdamir-audit-lint
description: Use when audit-lint reports a finding, or when you need to suppress one deliberately. audit-lint is the static anti-pattern gate (sync-over-async, silent failures, leaked API surface, unsafe defaults, raw DbConnection, …). Trigger on "audit-lint failed / finding", "AUDxxx", "suppress this warning", "why is the gate red".
---

# Asdamir audit-lint

`audit-lint` scans for the framework's anti-pattern rule set and **fails on any error/warning** — it's
part of the pre-push gate (`asdamir-preflight`). Rules live in
`src/Asdamir.Tools/Commands/AuditRules.cs`. Reference: `CLAUDE.md` → "CLI / audit-lint", `docs/cli.md`.

## Run it
```bash
dotnet run --project src/Asdamir.Tools -c Release --no-build -- audit lint --path src
dotnet run --project src/Asdamir.Tools -c Release --no-build -- audit lint --path AppManagement/src
```
Expect `… files scanned, 0 findings at or above warning.`

## Fixing vs suppressing
**Prefer fixing the finding.** Suppress only when the pattern is genuinely correct in context:
- One line: `// audit-lint:ignore AUDxxx` on (or just above) the flagged line — **always add a sibling
  comment saying why**.
- Whole file: `// audit-lint:skip-file` within the first 10 lines (rare — justify it).

## Common findings
- **AUD002 — raw `new SqlConnection(...)`**: inject and use `IDbConnectionFactory` instead. The factory
  itself, and one-shot CLI/maintenance code (e.g. `DbApplyCommand`, `SecretsCommand`) that legitimately
  opens a connection, carry `// audit-lint:ignore AUD002` with a reason. (Using the fully-qualified
  `new Microsoft.Data.SqlClient.SqlConnection(...)` also sidesteps the bare-pattern match.)
- Sync-over-async (`.Result`/`.Wait()`), empty catch / swallowed exceptions, `[AllowAnonymous]` without a
  reason, secrets/PII in logs → fix them; these exist to catch real regressions.
- **AUD013 — inline `style="…"` in `.razor`/`.sbn`** (the only markup rule; audit-lint also scans
  `.razor`/`.sbn`, not just `.cs`): move the declaration to a co-located scoped `.razor.css` class. **Exempt:**
  the CSS-variable pattern `style="--x:@value"` (per-item dynamic value into scoped CSS) and a FluentUI
  `Style="…"` component parameter (capital `S`). Suppress only for a truly unavoidable case (e.g. a pre-boot
  loading placeholder) with a reason.
- **AUD015 — localization-completeness** (a **separate command**, `audit localization`, not a regex rule in
  `AuditRules.cs` — it's a cross-file check: `LocalizationScan.cs` + `LocalizationCheckCommand.cs`): a
  `L["Key"]`/`Localizer["Key"]` used in code whose key is **never seeded** (no `localize_*`/`register_*`/`seed_*`
  entry, no in-memory seed) or **seeded in <3 cultures** (tr-TR/en-US/ru-RU) → ERROR (the raw key would render
  on screen — the exact `Common.Detail`/`Batch.Status.Cancelled` bug class). A **dynamic** `L[$"Prefix.{x}"]` /
  `L[variable]` → INFO (the runtime value-set can't be checked statically — verify the set is fully seeded, or
  `// audit-lint:ignore AUD015`). Run it as a companion gate:
  `dotnet run --project src/Asdamir.Tools -- audit localization --path src --min-severity warning`. Its sibling
  `localization verify --vault-connection … --app-code …` catches the *apply-drift* case (key IS in the seed
  file but never applied to the live vault — a static gate can't see that).
  **Seeds are read in BOTH SQL spellings (Tools `1.4.6`+):** the **tuple** form `(N'Key', N'tr-TR', N'…')` **and**
  the **EXEC** form — `EXEC dbo.LocalizationResource_UpsertValue …` in both arities (AsdamirVault
  `@appId, @key, @category, @culture, @value`; free-mode single-tenant without `@appId`), named or positional,
  plus the legacy migration-001 `EXEC dbo.Localization_Upsert @Key=…, @Culture=…`. Arguments are **lexed**
  (`SqlTextScanner`), so a comma inside a value can't shift them and a commented-out `EXEC` isn't a seed. Before
  `1.4.6` only the tuple form was seen, so **276 keys across 20 migrations** (billing, audit labels, the
  agent-audit ledger) were wrongly reported as "never seeded". **Write NEW seeds in the canonical form only —
  AUD018 enforces it.**
- **AUD019 — an in-memory mirror does not substitute for a SQL seed** (same command, `audit localization`;
  `LocalizationScan.CompareSqlBacking`): every key used in code must have a **SQL** seed in **all three**
  cultures. AUD015 compares against the **merged** corpus, where an in-memory entry is indistinguishable from
  a real seed — so a key can be green *purely because it is mirrored into* `UiLocalization.cs` while the
  migration the **live DB** runs is missing it. The in-memory seed is the `Persistence:UseInMemory` mirror for
  tests/demos, **not** what `db apply` runs → production renders the raw key with a green gate. Two distinct
  failure kinds: **"NO SQL seed"** (in-memory-only) and **"SQL-seeded only in [tr-TR, en-US]"** (partial, the
  mirror hides the gap). It is required **in addition** to the in-memory mirror, never **instead** — the
  mirror-each-other rule is unchanged. **No overlap with AUD015 by construction:** a key seeded nowhere is
  AUD015's, so AUD019 stays quiet for it (one key, one finding). Dynamic keys are skipped. A SQL tuple inside
  a `.sbn` **template** counts as SQL backing; only `["Key"] = "…"` is the mirror.
  **No `audit-lint:ignore AUD019` and no allowlist, deliberately** — the offending set is empty and a new key
  has no legitimate reason to be mirror-only. **Why add a rule that reports zero:** it lands as a no-op *now*,
  which is the cheapest moment (adding it later needs a cleanup migration first), and the zero is the result of
  the `1.4.6` **fix**, not of discipline — it was **186 keys** an hour earlier and climbs back without a gate.
  AUD018 does **not** cover this: **AUD018 constrains the seed's FORM, AUD019 asserts its EXISTENCE.**
- **AUD016 — permission/policy-completeness** (a **separate command**, `audit permissions`, not a regex rule —
  `PermissionPolicyScan.cs` + `PermissionPolicyCheckCommand.cs`): a Gateway policy that requires a `perm`
  value (`RequireClaim("perm", "X")` / `HasClaim("perm", "X")`, incl. inside `RequireAssertion`) which is
  **neither a seeded permission code** (`dbo.Permissions.Name`) **nor a role code** (`dbo.Roles`/`dbo.UserAppRoles`)
  → ERROR (the app-login token can never carry it → guaranteed silent 403; claim-injecting tests miss it). The
  app-login JWT carries role codes AND granted permission codes as `perm` claims, so a policy keyed on either is
  satisfiable. `--path` is repeatable — point one at `src` (policies) and one at `db` (seeds). Any finding
  **fails the build** (exit 1). Suppress a policy line with `// audit-lint:ignore AUD016`. Run it as a companion
  gate: `dotnet run --project src/Asdamir.Tools -- audit permissions --path src --path db`.
  **Seeded codes are read by a real T-SQL scanner, not a literal regex (Tools `1.4.6`+; `SqlTextScanner.cs`):**
  `--` and (nestable) `/* … */` comments are stripped first, and `''` inside a literal is an **escaped**
  apostrophe. Before that, one unpaired apostrophe in prose (`catalogue's`, `it's`, `don't`) shifted literal
  pairing for the rest of the file — hiding real seeds (loud) **and** collecting comment prose as a "seeded
  code", which let an unbacked policy pass the gate and 403 every user in production (silent). So: a perm
  named only in a comment is **not** supplied, a commented-out seed tuple is **not** seeded, and a file that
  only names `dbo.Permissions` in a header contributes nothing. AUD015's seed reader is comment-stripped too.

- **AUD018 — canonical localization-seed form** (a **separate command**, `audit seeds` — `SeedFormScan.cs` +
  `SeedFormCheckCommand.cs`): a localization seed may be written in exactly **ONE** approved spelling — a
  `@Seed` table variable of `(N'Key', N'<culture>', N'Value')` tuples fed through
  `dbo.LocalizationResource_UpsertValue`. Two violations: an **ad-hoc `INSERT`/`MERGE`/`UPDATE` straight at
  `dbo.LocalizationResource`** (bypasses the proc's SelfApp→`AppId NULL` mapping — the row can land in a scope
  nothing reads: a **data** bug, not a style nit) and **one `EXEC …_UpsertValue` per row with a literal key**
  (the rows stop being a machine-readable set — exactly how 276 keys hid from AUD015). A raw write **inside a
  stored-procedure body** is exempt (the canonical writer is itself a MERGE on the table); a `SELECT` is fine.
  **Why the rule at all:** teaching the scanner a new spelling is an infinite race (AUD016's apostrophe,
  AUD015's EXEC form) — constraining the input terminates it.
  **There is NO `audit-lint:ignore` for this rule, deliberately.** The only exemption is a reviewed, commented
  entry in **`packaging/seed-form-allowlist.txt`** (idiom of `packaging/removed-public-types.txt`), which today
  grandfathers **61 applied AsdamirVault migrations** (eras 002–077 and 093–129). Those files are **frozen**, so
  the list can only shrink. Run it as a companion gate:
  `dotnet run --project src/Asdamir.Tools -- audit seeds --path AppManagement/db --path src`.
- **AUD017 — no pattern-based permission grants** (same command, `audit seeds`): a statement that **writes**
  `dbo.Permissions`/`dbo.RolePermissions` while selecting rows with a **`LIKE`** pattern
  (`JOIN dbo.Permissions p ON p.Name LIKE N'%.read'`) → ERROR. A wildcard grant is **unlistable in principle**:
  it sweeps in every future permission whose code happens to match (retroactively, with no review) **and**
  misses every one that does not (`ent.agentaudit.verify` doesn't end in `.read` → a 403 nobody wrote down) —
  and AUD016 can't cross-check it, because a wildcard supplies no NAMES. Use an explicit list:
  `p.Name IN (N'…', N'…')`. A read-path `SELECT … LIKE @Category + '%'` is fine; a write to a *different*
  table that merely JOINs `dbo.Permissions` (e.g. `UserMenuPermissions`) is not a grant. Same allowlist, same
  no-inline-suppression policy; grandfathers `AsdamirVault_003` (the namesake `%.read` bootstrap grant) and
  `AsdamirVault_060` (the one-off `ent.%` prefix strip).

## DON'T
- **Don't bulk-suppress** to make the gate green — each suppression is reviewable and needs a reason.
- **Don't edit an applied migration to satisfy a gate** — it is immutable; grandfather it in
  `packaging/seed-form-allowlist.txt`, and if the defect is in the scanner, fix the SCANNER.
- **Don't disable a rule globally** to dodge one site — fix the site or `// audit-lint:ignore` it locally.
- **Don't skip running both `--path` scopes** (`src` *and* `AppManagement/src`).
