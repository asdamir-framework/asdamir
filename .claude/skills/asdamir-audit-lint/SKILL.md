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

## DON'T
- **Don't bulk-suppress** to make the gate green — each suppression is reviewable and needs a reason.
- **Don't disable a rule globally** to dodge one site — fix the site or `// audit-lint:ignore` it locally.
- **Don't skip running both `--path` scopes** (`src` *and* `AppManagement/src`).
