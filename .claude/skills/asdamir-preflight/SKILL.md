---
name: asdamir-preflight
description: Run before pushing changes to the Asdamir repo. CI/GitHub Actions is disabled (cost), so this is the verification gate — build (0 warnings), tests, audit-lint — then push straight to main. Use whenever you're about to git push, or the user says "push", "verify", "ready to commit", or similar.
---

# Asdamir pre-push verification

**There is NO hosted CI on this repo (GitHub Actions is off for cost).** Nothing runs on push. So *you*
are the gate: run these checks locally and only push when they're green. Pushes go **straight to `main`**
(no PRs, no merge flow). See `CLAUDE.md` → Workflow and memory `ci-disabled-cost`.

## Steps (in order)

### 1. Build — must be 0 warnings
`TreatWarningsAsErrors` + latest analyzers are on, so a warning *is* a failure (incl. NuGet NU1xxx).
```bash
dotnet build Asdamir.sln -c Release
```
Stop and fix if it's not `0 Warning(s), 0 Error(s)`.

### 2. Tests
The full suite includes one **~15-min** generate-and-build "scaffold smoke test"
(`[Trait("Category","Scaffold")]`). It only matters if you changed framework source, templates, or
package versions — so skip it otherwise.

Decide with the diff (vs the remote you'll push to):
```bash
git fetch -q origin
git diff --name-only origin/main...HEAD | grep -qE '^(src/|Directory\.Packages\.props|Directory\.Build\.props|tests/Asdamir\.Tools\.Tests/)' \
  && SCAFFOLD=1 || SCAFFOLD=0
```
Then:
```bash
# SCAFFOLD=0  → fast (~seconds), skips the slow test:
dotnet test Asdamir.sln -c Release --filter "Category!=Scaffold"
# SCAFFOLD=1  → full suite incl. the scaffold test (~15-22 min, restores NuGet for a generated app):
dotnet test Asdamir.sln -c Release
```
All assemblies must report `Passed!`.

> Heads-up: don't stack multiple `dotnet test` runs in parallel — they contend and slow each other down.

### 3. audit-lint (static gate) — both scopes
```bash
dotnet run --project src/Asdamir.Tools -c Release --no-build -- audit lint --path src
dotnet run --project src/Asdamir.Tools -c Release --no-build -- audit lint --path AppManagement/src
```
Must be `0 findings at or above warning`. Fix findings, or suppress deliberately with
`// audit-lint:ignore AUDxxx` (+ a reason) — see the `asdamir-audit-lint` skill.

### 4. (Only if you touched migrations / `db apply`) verify migrations apply
Apply all migrations to a throwaway DB, then re-run to prove the journal skips them:
```bash
framework db apply --server localhost --database AsdamirPreflight --user <login> --password <pwd> \
  --create-database --migrations AppManagement/db/migrations          # fresh: N applied
framework db apply --server localhost --database AsdamirPreflight --user <login> --password <pwd> \
  --migrations AppManagement/db/migrations                            # re-run: 0 new, N skipped
# then drop AsdamirPreflight
```

### 5. Push
```bash
git add -A && git commit -m "<conventional message>
Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
git push origin main
```
Confirm the push triggered **no** Actions run (workflows are removed):
```bash
gh run list --limit 1 --json headSha -q '.[0].headSha[0:7]'   # should NOT equal your new HEAD
```

### 6. End of session
Write the bilingual EN+TR memory entry (project rule) — use the `asdamir-session-memory` skill.

## DON'T
- **Don't push with build warnings** — they're errors here.
- **Don't run the ~15-min scaffold test** when you only touched docs / CI-config / AppManagement / tests
  (use `--filter "Category!=Scaffold"`).
- **Don't re-add `.github/workflows/*`** to "get CI back" unless the user explicitly asks (it bills).
- **Don't open a PR / wait for CI** — there is none; commit and push to `main` directly.
