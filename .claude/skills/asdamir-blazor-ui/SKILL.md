---
name: asdamir-blazor-ui
description: Use when adding or editing a Blazor component/page in Asdamir.Web, the AdminConsole, or a generated Server. Encodes the strict UI rules — CSS isolation (no inline styles), the FluentUI combobox/select z-index trap, FluentUI registration, and notifications/confirms. Trigger on "add a component/page", "style this", "the dropdown is behind", "show a confirm/toast", any .razor/.razor.css work.
---

# Asdamir Blazor UI conventions

Deep reference: `CLAUDE.md` → "Blazor styling" + "Combobox dropdown layering", `docs/ui-components.md`,
memory `2026-06-14-notification-service-real`.

> **Scaffolding a page is more than `.razor`.** `asdamir new page` (and `asdamir new feature`) emit not
> just `<Plural>List.razor` + `<Name>EditorDialog.razor` but also `db/admin-onboarding/seed_menu_<plural>.sql`
> (a role-based menu + `<plural>.view` permission seed) and `localize_<plural>.sql` — apply both to
> **AsdamirVault**. So a generated page already ships its nav-menu entry + permission; you don't register
> the menu by hand. See `asdamir-new-feature`.

## Theme + components come FROM the framework — apps consume, never copy (rule)
The whole visual identity is the **INSPINIA "Pixel" theme**, and it lives in **ONE place**: the
`Asdamir.Web` package ships it as a static web asset **`_content/Asdamir.Web/asdamir-theme.css`** (the
palette/tokens `:root`/`--asd-*`, the full `.asd-*` design system — buttons/tables/panels/pills/toolbars —,
self-hosted Inter fonts under `_content/Asdamir.Web/fonts/`, global FluentUI element theming, the
`html[data-skin]` skins, the disabled-button + `.asd-vscroll-*` scrollbar primitives).
- **Every app LINKS it and does NOT copy it.** `App.razor` links `@Assets["_content/Asdamir.Web/asdamir-theme.css"]`
  **before** the app's own `app.css`. `asdamir new app` emits exactly this (`ServerAppRazor.sbn`), and the
  generated `app.css` (`ServerAppCss.sbn`) is a **thin OVERRIDES-only stub** — NOT a copy of the design
  system. AppManagement links it too.
- **Consuming apps get it from NuGet.** A generated app references `Asdamir.Web` by `PackageReference`, so a
  theme change ships when a **new `Asdamir.Web` is published** (bump `<Version>` → pack → `dotnet nuget push`);
  the app then bumps its `Asdamir.Web` `PackageVersion` and re-restores. (AppManagement uses a
  `ProjectReference` in the dev repo, so it picks up theme edits immediately.)
- **NEVER re-declare `.asd-*` / `:root --asd-*` / fonts in an app's `app.css`.** That per-app copy is exactly
  what drifted (AppManagement 804 lines vs a generated app's 694, buttons diverging). If a rule is generic
  (a new button variant, a shared primitive), it goes in **`asdamir-theme.css`** (the framework), not in an
  app. An app's `app.css` carries only genuinely app-specific rules (e.g. Recon's `.mmc-candidates`).

## Shared chrome components — prefer them over hand-rolled markup
`Asdamir.Web/UI/Components` ships reusable, theme-consuming components — use them instead of re-writing the
markup + classes on every page:
- **`<AsdamirButton>`** — the button. `<AsdamirButton Variant="AsdamirButton.ButtonVariant.Primary"
  OnClick="SaveAsync">@L["Common.Save"]</AsdamirButton>` instead of `<button class="asd-btn asd-btn-primary">`.
  Variants `Default/Primary/Secondary/Ghost/Danger`, `Size` `Normal/Small`, `Disabled`, `Class` for a
  page-scoped modifier; other attributes (`autocomplete`, `title`, `aria-*`) splat onto the `<button>`. It
  renders the shared `.asd-btn` classes, so it follows the active `html[data-skin]` skin.
- **`<AsdamirPill>`** (status badge, `Tone` Neutral/Ok/Err/Warn/Info), **`<AsdamirPanel>`** (`.asd-panel`
  surface + optional `Head`/`Foot`), **`<AsdamirToolbar>`** (`.asd-toolbar` + `Actions` group),
  **`<AsdamirPageBar>`** (page title + a `List<AsdamirCrumb>` breadcrumb trail) — use these for the page
  chrome instead of `<span class="asd-pill …">` / `<div class="asd-panel">` / the pagebar boilerplate.
- Also available: `AsdamirModal`, `AsdamirGrid`, `AsdamirContentCard`, `AsdamirNotificationHost`, `AsdamirChart`,
  `AsdamirFilePicker`, … — reach for these before writing bespoke markup. New generic chrome → add it here
  (a shared component) + a semantic token in the theme (`--asd-success`/`--asd-warning`/`--asd-danger` exist),
  not a per-app hardcoded color.

## CSS — isolation only, no inline styles
- Styles live in a **co-located `<Component>.razor.css`** (scoped). Use **`::deep`** to reach child /
  FluentUI rendered elements.
- **NO inline `style="…"` attributes.** (AdminConsole pages drifted to ~140 inline styles — migrate to
  scoped CSS when you touch a component; never add new ones.)
- The theme palette/tokens (`:root`, `--asd-*`), `html, body`, global FluentUI element theming, and the
  `.asd-*` design system live in the **shared `_content/Asdamir.Web/asdamir-theme.css`** (above) — NOT in an
  app's `app.css`. An app's `app.css` is overrides-only.

## The combobox / select z-index trap (don't regress this)
FluentUI `fluent-select` / `fluent-combobox` render their dropdown **inside** the component (no portal),
so it's confined by ancestors' overflow + stacking contexts. The single source of truth is in `app.css`:
`fluent-select, fluent-combobox { position: relative; z-index: 100 }` + card-like wrappers
(`fluent-card`, `.asd-filter`, `.asd-toolbar`) use `overflow: visible; contain: none`.
- **NEVER put `z-index` (nor `position`+`z-index`) on a filter / toolbar / card wrapper that contains a
  select** — a positioned ancestor with a lower z-index creates a stacking context that *traps* the
  dropdown behind the next card. (This regressed once when `.asd-filter` had `z-index:5`.)

## FluentUI registration
- **Don't add `using Asdamir.Web.UI;`** in a Server `Program.cs` — it shadows Microsoft's
  `AddFluentUIComponents()` with the framework's no-op stub and drops `IToastService` et al. Call
  `Asdamir.Web.UI.UIExtensions.AddUIServices(...)` fully-qualified instead.

## Notifications / confirmations / loading
Use `INotificationService` (`@inject`), rendered by **one `<AsdamirNotificationHost />`** in the layout
(the generated Server's `MainLayout` already has it):
```razor
Notify.ShowSuccess("Saved.");
if (await Notify.ConfirmDangerAsync("Delete this?")) await Delete();
Notify.ShowLoading("Importing…"); /* … */ Notify.HideLoading();
```
`AsdamirNotificationHost` is FluentUI-free (works in interactive islands + MAUI). **Without a host
rendered, `ConfirmAsync`/`ConfirmDangerAsync` return `false`** (deny) — a destructive action never
proceeds un-confirmed.

## Localization & culture
User-facing strings are DB-backed — use `@L["Key"]`; see the `asdamir-localization` skill. Theme / dark
mode / culture are held in cookies / `UiState`; don't reinvent the selectors.

## DON'T
- No inline `style=`; no `z-index` on a wrapper containing a select; no `using Asdamir.Web.UI;` in a host.
- Don't render more than one `<AsdamirNotificationHost />` (global overlays would collide).
- Don't hardcode user-facing text — localize it.
