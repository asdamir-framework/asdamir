---
name: asdamir-localization
description: Use when adding or changing any user-facing UI string / label / message in Asdamir (AdminConsole or a generated app). Localization is DB-backed — there are NO .resx files; a key must be seeded for all three cultures. Trigger on "add a label", "translate", "localize", "new UI text", hardcoded-string review.
---

# Asdamir localization (DB-backed, no .resx)

UI strings live in the **`LocalizationResource`** table (one row per `Key` × `Culture`, AppId-scoped),
seeded via SQL migrations and resolved through a DB-backed `IStringLocalizer` (`ApiStringLocalizer`) —
**never** `.resx`. The UI fetches all keys for the current culture from the API
(`GET /api/admin/localization/ui?culture=…` → `Localization_GetAllByCulture`), with an in-memory mirror
for `Persistence:UseInMemory=true` (dev/test). Deep reference: `docs/fundamentals/localization.md` and
`CLAUDE.md` → "Localization is DB-backed".

**Cultures (all three, always):** `tr-TR` (default) · `en-US` · `ru-RU`.

## To add / change a key

1. **Pick a dot-namespaced key** — e.g. `Apps.Subtitle`, `App.Dash.Welcome`. Reuse an existing namespace.

2. **Seed it in a migration** (AsdamirVault). Add a new file
   `AppManagement/db/migrations/AsdamirVault_0XX__<slug>_localization.sql` (next number — see the
   `asdamir-migration` skill) inserting the key for **all three cultures**, idempotently. Scope by
   `AppId`: `NULL` for global / admin-pool strings; the app's `AppId` for an app-specific string.
   ```sql
   MERGE dbo.LocalizationResource AS t
   USING (VALUES
     (N'Apps.Subtitle', N'tr-TR', N'<TR metni>'),
     (N'Apps.Subtitle', N'en-US', N'<EN text>'),
     (N'Apps.Subtitle', N'ru-RU', N'<RU текст>')
   ) AS s([Key],[Culture],[Value])
     ON t.[Key]=s.[Key] AND t.[Culture]=s.[Culture] AND t.[AppId] IS NULL
   WHEN NOT MATCHED THEN INSERT([Key],[Culture],[Value],[AppId]) VALUES(s.[Key],s.[Culture],s.[Value],NULL);
   ```
   (Match the exact column/proc shapes already in `AppManagement/db/migrations/AsdamirVault_001…` and the
   most recent `*_localization.sql`.)

3. **Add the same key to the in-memory seed mirror** so `UseInMemory` (dev/test, and the AdminConsole's
   default mode) sees it — `AppManagement/src/Asdamir.AdminConsole.Api/Localization/UiLocalization.cs`
   (`UiLocalizationSeed`), all three cultures. **If you skip this, in-memory runs show the raw key.**

4. **Use it** in the component:
   ```razor
   @inject Asdamir.Web.Localization.ApiStringLocalizer L
   <span>@L["Apps.Subtitle"]</span>
   ```

5. **Apply the migration** with the journaled runner (`asdamir-migration` → `asdamir db apply`).

6. **Interactive Server pages:** culture is re-applied per circuit by `CultureCircuitHandler`
   (`UseRequestLocalization` only covers SSR) — already wired; don't re-implement.

### Generated apps
A generated app's UI strings live centrally in AsdamirVault scoped to its `AppId`, seeded via its
`db/admin-onboarding/register_<app>.sql` (run against AsdamirVault), not the app's own DB. Add new keys
there (+ the scaffold's localization seed) for all three cultures.

## DON'T
- **Don't create or reintroduce `.resx`** files anywhere.
- **Don't seed only one culture** — always tr-TR + en-US + ru-RU, or the others fall back to the key.
- **Don't forget the in-memory mirror** (step 3) — the migration alone won't show in `UseInMemory` mode.
- **Don't hardcode the literal** in markup/`Program.cs` — every user-facing string is a key.
