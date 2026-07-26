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
- **AUD016 — permission/policy-completeness** (a **separate command**, `audit permissions`, not a regex rule —
  `PermissionPolicyScan.cs` + `PermissionPolicyCheckCommand.cs`): a Gateway policy that requires a `perm`
  value (`RequireClaim("perm", "X")` / `HasClaim("perm", "X")`, incl. inside `RequireAssertion`) which is
  **neither a seeded permission code** (`dbo.Permissions.Name`) **nor a role code** (`dbo.Roles`/`dbo.UserAppRoles`)
  → ERROR (the app-login token can never carry it → guaranteed silent 403; claim-injecting tests miss it). The
  app-login JWT carries role codes AND granted permission codes as `perm` claims, so a policy keyed on either is
  satisfiable. `--path` is repeatable — point one at `src` (policies) and one at `db` (seeds). Any finding
  **fails the build** (exit 1). Suppress a policy line with `// audit-lint:ignore AUD016`. Run it as a companion
  gate: `dotnet run --project src/Asdamir.Tools -- audit permissions --path src --path db`.

## DON'T
- **Don't bulk-suppress** to make the gate green — each suppression is reviewable and needs a reason.
- **Don't disable a rule globally** to dodge one site — fix the site or `// audit-lint:ignore` it locally.
- **Don't skip running both `--path` scopes** (`src` *and* `AppManagement/src`).
