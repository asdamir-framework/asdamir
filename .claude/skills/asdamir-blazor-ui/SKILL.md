---
name: asdamir-blazor-ui
description: Use when adding or editing a Blazor component/page in Asdamir.Web, the AdminConsole, or a generated Server. Encodes the strict UI rules — CSS isolation (no inline styles), the FluentUI combobox/select z-index trap, FluentUI registration, and notifications/confirms. Trigger on "add a component/page", "style this", "the dropdown is behind", "show a confirm/toast", any .razor/.razor.css work.
---

# Asdamir Blazor UI conventions

Deep reference: `CLAUDE.md` → "Blazor styling" + "Combobox dropdown layering", `docs/ui-components.md`,
memory `2026-06-14-notification-service-real`.

## CSS — isolation only, no inline styles
- Styles live in a **co-located `<Component>.razor.css`** (scoped). Use **`::deep`** to reach child /
  FluentUI rendered elements.
- **NO inline `style="…"` attributes.** (AdminConsole pages drifted to ~140 inline styles — migrate to
  scoped CSS when you touch a component; never add new ones.)
- Only genuinely global concerns belong in `wwwroot/app.css`: the theme palette/tokens (`:root`,
  `--asd-*`), `html, body`, and global FluentUI element theming.

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
