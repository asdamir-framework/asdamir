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

## Cultures

The framework's error-translation fallbacks cover `tr`, `en` and `ru`; add cultures by seeding their rows in the localization store.

## See also

- [Error Handling](error-handling.md) — localized error messages share the same translation pipeline
- [UI Components](../ui-components.md)
