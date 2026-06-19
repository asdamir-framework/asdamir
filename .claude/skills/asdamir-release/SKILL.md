---
name: asdamir-release
description: Use when packaging/publishing the Asdamir NuGet packages or the CLI tool, or cutting a version. CI is OFF, so packing + pushing is manual; and GitHub Packages can't ingest the CLI tool package. Trigger on "publish", "release", "pack", "push to nuget", "cut a version", "ship the packages".
---

# Asdamir release / publish (manual — CI is off)

Deep reference: `docs/RELEASE.md` (versioning, CHANGELOG, tagging), `docs/cli.md` → tool install, memory
`2026-06-15-cli-tool-release-distribution` + `2026-06-15-publish-nuget-fix-and-sql-otel`.

**CI/GitHub Actions is disabled (cost)** — the old `pack` / `publish-nuget` jobs don't run. Pack and push
yourself. (When CI is re-enabled, the tagging sequence in `docs/RELEASE.md` drives it automatically.)

## Pack
```bash
dotnet build Asdamir.sln -c Release          # 0 warnings first (asdamir-preflight)
dotnet pack  Asdamir.sln -c Release -o artifacts -p:Version=0.X.0          # stable
#   or a preview suffix:  -p:Version=0.X.0-preview.<n>
```
Produces `Asdamir.Core`, `Asdamir.Data`, `Asdamir.Web`, `Asdamir.Tools` `.nupkg`s.

## Push — two different channels (this matters)
1. **Framework libraries** (`Asdamir.Core/.Data/.Web` — what generated apps reference): push to your feed.
   ```bash
   dotnet nuget push "artifacts/Asdamir.{Core,Data,Web}.*.nupkg" --source <feed> --api-key <key> --skip-duplicate
   ```
2. **The CLI tool** (`Asdamir.Tools`): **GitHub Packages DETERMINISTICALLY STALLS** on this self-contained
   tool package (the push hangs to the timeout — confirmed at 5 MB and 8 MB). Publish it to **nuget.org**
   (`dotnet nuget push artifacts/Asdamir.Tools.*.nupkg --source https://api.nuget.org/v3/index.json
   --api-key <nuget-key> --skip-duplicate`) **or** attach the `.nupkg` to a GitHub Release
   (`gh release upload cli-preview artifacts/Asdamir.Tools.*.nupkg --clobber`). Then install per
   `docs/cli.md`.

`nuget.org` ingests the tool fine and gives the one-liner `dotnet tool install -g Asdamir.Tools --prerelease`
(needs the `NUGET_API_KEY` secret + CI re-enabled, or push it manually).

## Version & changelog
SemVer; pre-1.0 minors may break. Update `CHANGELOG.md` (`[Unreleased]` → `[0.X.0] - <date>`),
then tag `v0.X.0`. Full checklist + rollback in `docs/RELEASE.md`.

## DON'T
- **Don't push `Asdamir.Tools` to GitHub Packages** — it stalls. Use nuget.org or a Release asset.
- **Don't re-add `.github/workflows/*`** to automate this unless the user wants CI back on (it bills).
- **Don't pack with warnings** or hardcode `<Version>` in the csprojs (pass `-p:Version=` at pack time).
