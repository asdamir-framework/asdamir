# Localization

**Package:** `Asdamir.Web` · **Namespace:** `Asdamir.Web.Localization`

## Introduction

Asdamir localizes UI strings through an **API-backed** store rather than static `.resx` files, so translations are edited centrally (by operators, in the AdminConsole) and refreshed at runtime. It ships multi-culture support out of the box (**TR / EN / RU**) and caches resolved strings for performance.

## How it works

- An `IStringLocalizer`-compatible localizer (`ApiStringLocalizer`) resolves keys against a central localization API.
- Results are cached (`LocalizationCacheRefresher`) and refreshed when the source version changes.
- Authority lives in the AdminConsole's database; managed apps read from it and cache locally.

## Usage in components

```razor
@inject IStringLocalizer<MyComponent> L

<FluentLabel>@L["Users.Title"]</FluentLabel>
```

Missing keys fall back gracefully (requested culture → default culture → the key itself) so the UI never shows blanks.

## Editing translations

Operators manage keys and per-culture values in the AdminConsole **Localization editor**; changes propagate to managed apps via the orchestration sync push. There is no inline upsert/delete from consumer code — the central store is the single source of truth.

## Menu labels

Nav menu items are composed centrally (`dbo.Menus`, per app/`AppId`) and **localized**, not rendered from the raw stored `Name`. The generated `NavMenu` derives a stable key from each item's Url — `/order-items` → `Menu.OrderItems` — and resolves `L["Menu.<Slug>"]`, falling back to the raw `Name` only when no key is seeded. `asdamir new page` seeds the matching `Menu.<Slug>` key (all cultures) in its `localize_<entity>.sql`, so the menu label, page title and field labels all translate together when the culture changes.

## Scope: which application a string belongs to

Every row in `LocalizationResource` carries an **`AppId`**, and it decides who can read the string.

| `AppId` | Meaning | Who reads it |
|---|---|---|
| a managed app's id | that application's own strings | that app, through its Gateway |
| **the self-app id** | the **AdminConsole's own** strings | the console only |
| **`NULL`** | **global** — inherited by every managed app | every managed app; **not** the console |

The console reads with `AppId = SelfApp` and therefore does **not** inherit global rows: its own strings are
always stored against the self-app explicitly. That asymmetry is deliberate — it is what stops a row left
global by one application from silently becoming part of the operator console's vocabulary.

**`NULL` is empty by design, and nothing seeds it.** `AsdamirVault_137` moved every stranded row onto the
self-app, and no migration writes a global row on purpose. The slot stays open because
`Localization_GetMapForApp` genuinely implements inheritance and that mechanism is worth keeping for
framework-wide strings — but a key that lands there **by accident** is invisible to the console and visible to
every app at once, which is exactly the defect 137 repaired. **If you seed a global row, mean it.**

`(AppId, Key, Culture)` is **unique**. That mirrors the canonical writer's own MERGE key, so the same key can
never exist twice for one scope — the state in which an edit updates one copy while a reader serves another,
and a stale translation reads as a correct one.

### History, because the shape repeats

`AsdamirVault_033` mapped the self-app id to `NULL` on write, when `NULL` *was* the console's scope.
`AsdamirVault_080` moved the console's rows onto the self-app id and `_082` taught the readers to filter on it
— but the **writer was never updated**, so for 362 keys across eleven feature areas the console wrote to one
place and read from another. Its screens rendered raw keys on every fresh install while every managed app
inherited those same strings. `AsdamirVault_137` completes 080; the scoping is covered by
`LocalizationScopeIntegrationTests` and by an E2E guard that fails any page showing a raw key.

## Cultures

The framework's error-translation fallbacks cover `tr`, `en` and `ru`; add cultures by seeding their rows in the localization store.

## See also

- [Error Handling](error-handling.md) — localized error messages share the same translation pipeline
- [UI Components](../ui-components.md)
