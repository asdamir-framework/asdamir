# UI Components

**Package:** `Asdamir.Web` · **Namespace:** `Asdamir.Web.UI`

## Introduction

`Asdamir.Web` is a **Blazor Razor Class Library** built on **Microsoft FluentUI** that provides enterprise UI building blocks — a feature-rich data grid, dialogs, charts, exports, notifications and several specialized inputs — plus theming and a set of UI services.

## Registration

```csharp
builder.Services.AddFluentUIComponents();   // FluentUI primitives
builder.Services.AddUIServices();           // dialog/notification/export/loading/theme services
builder.Services.AddUICharts();             // Chart.js interop
```

In `_Imports.razor`:

```razor
@using Asdamir.Web.UI.Components
```

## Component catalogue

| Component | Purpose |
|---|---|
| `DataGrid` | Enterprise grid: sorting, paging, deterministic column templates, CSP-clean JS module |
| `Dialog` | Modal dialogs and confirmations (FluentUI) |
| `Chart` | Chart.js charts via a typed ES module |
| `Notification` | Toast / inline notifications |
| `ContentCard` | Consistent card/section layout |
| `DataExporter` | Export grids to **Excel** (ClosedXML), **PDF** (QuestPDF) and **CSV** |
| `IntPhone` | International phone input (libphonenumber-validated) |
| `BarcodeScanner` | Camera barcode scanning (Quagga) |
| `OCR` | In-browser OCR (Tesseract) |
| `SignaturePad` | Captures a signature image |
| `FilePicker` | General-purpose file picker: drop zone with native drag & drop (no JS interop), `Accept`/`MaxFileSize` gates, hands the `IBrowserFile` to the caller |
| `AsdamirTextInput` | **Isolation** text field — native `<input>` + design tokens, no FluentUI dependency; `InputBase<string?>` form integration, typing/paste character filter, `MaxLength`, `InputType` |
| `AsdamirNumberInput<T>` | **Isolation** numeric field — culture-aware `type="text"` entry (`decimal`/`int`/`long`), no FluentUI dependency; `ToEven` rounding, `Min`/`Max`, wheel-safe |
| `AsdamirSelect<TOption>` | **Isolation** drop-down — native `<select>` + design tokens, no FluentUI dependency; generic `TOption` with `OptionText`/`OptionValue` projections, string `@bind-Value` (works with `@bind-Value:after`), `Placeholder`, `Disabled`, attribute splat |
| `AsdamirTextArea` | **Isolation** multi-line field (replaces `FluentTextArea`) — native `<textarea>` + design tokens, no FluentUI dependency; `InputBase<string?>` form integration (`@bind-Value`), `Rows`, `MaxLength`, `Label`/`Placeholder`, `Disabled`/`ReadOnly`, attribute splat |
| `AsdamirSwitch` | **Isolation** toggle (replaces `FluentSwitch`) — accessible native checkbox `role="switch"` drawn as a track+knob + design tokens, no FluentUI dependency; bool `@bind-Value` (works with `@bind-Value:after`), `Label` **or** `ChildContent` caption, `Disabled` |
| `AsdamirCheckbox` | **Isolation** checkbox (replaces `FluentCheckbox`) — native `<input type="checkbox">` + design tokens, no FluentUI dependency; bool `@bind-Value` **or** explicit `Value`/`ValueChanged` (works with `@bind-Value:after`), `Label` **or** `ChildContent` caption, `Indeterminate`, `Disabled` |
| `AsdamirDatePicker` | **Isolation** date field (replaces `FluentDatePicker`) — native `<input type="date">` + design tokens, no FluentUI dependency; `DateTime?` `@bind-Value` (works with `@bind-Value:after`; cleared → `null`), fixed ISO `yyyy-MM-dd` on the wire (locale only affects display), `Label`, `Min`/`Max`, `Disabled`, attribute splat |
| `AsdamirRadioGroup` + `AsdamirRadio` | **Isolation** radio group (replaces `FluentRadioGroup`/`FluentRadio`) — native `<input type="radio">` children sharing one group `name` (auto-guid, or explicit `Name`) via a cascading context, no FluentUI dependency; group takes string `@bind-Value` **or** explicit `Value`/`ValueChanged`, `Disabled`, `Class`; each `AsdamirRadio` has a required `Value`, `ChildContent` label, own `Disabled` |
| `AsdamirStack` | **Isolation** flex layout (replaces `FluentStack`) — native `<div class="asd-stack">`, no FluentUI dependency; `Orientation`, axis-aware `HorizontalAlignment`/`VerticalAlignment`, `HorizontalGap`/`VerticalGap`, `Wrap`, `Reversed`, `Width` (all mapped to `.asd-stack-*` classes + CSS vars, no inline style) |
| `AsdamirMessageBar` | **Isolation** status bar (replaces `FluentMessageBar`) — native `<div class="asd-messagebar">`, no FluentUI dependency; `Intent` (Info/Success/Warning/Error → color rail + tint + icon), plain-text `Title`, `AllowDismiss` (default true) with a `Dismissed` callback |
| `AsdamirLabel` | **Isolation** typographic label (replaces `FluentLabel`) — semantic `h1..h6`/`p`/`span` element + `.asd-label-*` class, no FluentUI dependency; `Typo` (H1–H6/Body/Subject → the matching element so a heading stays a real heading), optional `Weight` |
| `AsdamirSpacer` | **Isolation** flex spacer (replaces `FluentSpacer`) — native `<div class="asd-spacer">`, no FluentUI dependency; grows to push flex siblings apart (`flex: 1`), or a fixed gap when `Width` is set |
| `AsdamirAnchor` | **Isolation** link-button (replaces `FluentAnchor`) — native `<a class="asd-btn">`, no FluentUI dependency; `Href` + `Appearance` (Accent → `.asd-btn-primary`, Neutral → outline `.asd-btn`) |
| `Menu` | Navigation menu bound to the menu service |
| `ErrorBoundaryEx` | Error boundary with friendly fallback |
| `AsdamirThemeProvider` | Design-theme facade — drives the design system's accent + light/dark tokens from the active skin; callers never see FluentUI (see below) |
| `AuditTrail`, `ErrorMonitoring`, `Hangfire`, `Outbox`, `PermissionManager`, `Users`, … | Admin building blocks reused by the AdminConsole |

## Isolation facades — keep callers off `Microsoft.FluentUI.*`

Several `Asdamir.Web/UI/Components` types are **facades**: they wrap a FluentUI component (or service) *inside* the framework so **calling code never references `Microsoft.FluentUI.*`**. When FluentUI ships a breaking major (v5 removes/renames these), the migration is a handful of facade files instead of every page — the callers (AppManagement pages/layouts and every generated app) are untouched.

### `AsdamirThemeProvider` — the design-theme facade

Wraps FluentUI's `FluentDesignTheme` and owns the whole skin→accent-hex map + the light/dark decision, reading the `data-skin` / `data-bs-theme` attributes `App.razor` stamps on `<html>`. Render it once (usually behind a layout-local `AppTheme` alias):

```razor
<AsdamirThemeProvider />
```

| Parameter | Type | Default | Purpose |
|---|---|---|---|
| `DefaultAccent` | `string` | `#8e7fe0` | Accent used before the first client read and when the active skin has no map entry. |
| `SkinAccents` | `IReadOnlyDictionary<string,string>?` | `null` (built-in map) | Optional override of the skin→accent map (keys matched case-insensitively). |

**Why a facade:** in v5, `FluentDesignTheme` is removed in favour of a CSS-variable brand ramp. With this facade, that change lands *inside* `AsdamirThemeProvider.razor` only; `AppTheme.razor` and the generator's `ServerAppTheme.sbn` stay `<AsdamirThemeProvider />`. It also collapses the skin→accent map that was previously copy-pasted into every app's `AppTheme.razor` into one source.

### `AsdamirAppProviders` — the overlay-providers facade

Wraps the four FluentUI overlay providers (toast, dialog, tooltip, message bar) behind one component. Render it once near the end of a layout instead of listing the individual providers:

```razor
<AsdamirAppProviders />                                  @* all four *@
<AsdamirAppProviders Dialog="false" Tooltip="false" />   @* auth screen: toast + message bar only *@
```

| Parameter | Type | Default | Purpose |
|---|---|---|---|
| `Toast` | `bool` | `true` | Render the toast provider. |
| `Dialog` | `bool` | `true` | Render the dialog provider. |
| `Tooltip` | `bool` | `true` | Render the tooltip provider. |
| `MessageBar` | `bool` | `true` | Render the message-bar provider. |

Each provider is opt-out so a layout keeps its exact set; an unused provider is a harmless empty overlay region.

### Toast / dialog facade services

For code that needs to raise a FluentUI toast or a framework dialog directly, inject the Asdamir-owned interfaces instead of FluentUI's `IToastService` / `IDialogService` — both are registered by `AddUIServices()`:

```csharp
@inject Asdamir.Web.UI.Services.IAsdamirToastService Toast
Toast.ShowSuccess(L["Saved"].Value);   // ShowSuccess / ShowError / ShowInfo

@inject Asdamir.Web.UI.Services.IAsdamirDialogService Dialogs
await Dialogs.ShowInfoAsync(title, message);
```

> Prefer `IAsdamirNotificationService` + `<AsdamirNotificationHost />` (below) for app notifications — it is FluentUI-free and works in MAUI. The toast/dialog facades exist for the few framework components (Login, the route-authorization notice) that use FluentUI's toast/dialog directly; the facades keep those callers off `Microsoft.FluentUI.*`.

**Why facades:** a FluentUI major that removes/renames the providers and the toast/dialog services touches only `AsdamirAppProviders.razor` + `AsdamirToastService.cs` + `AsdamirDialogService.cs`, not the layouts and pages.

### `AsdamirSpinner` — the loading-spinner facade

The indeterminate loading spinner, wrapping FluentUI's `FluentProgressRing`. Every "loading…" placeholder on a page uses it:

```razor
@if (_loading) { <AsdamirSpinner /> }
```

| Parameter | Type | Default | Purpose |
|---|---|---|---|
| *(splatted attributes)* | `aria-label`, `class`, `title`, … | — | Any attribute splats onto the underlying ring; none required — the default matches the former parameterless spinner. |

**Why a facade:** v5 renames `FluentProgressRing` → `FluentSpinner`; with this facade every "loading" placeholder across AppManagement and every generated CRUD/payment page stays `<AsdamirSpinner />`, and only the one facade file changes.

### `AsdamirButton` — the button facade (all buttons go through it)

`AsdamirButton` renders the shared `.asd-btn` design-system markup and is now the **only** button component in the framework's UI — the last raw `<FluentButton>` usages (auth screens, session/access dialogs, the generated payment page) were migrated to it. Map FluentUI's `Appearance` to `Variant`:

| FluentUI `Appearance` | `AsdamirButton.Variant` |
|---|---|
| `Accent` | `Primary` |
| `Neutral` / `Outline` | `Default` |
| `Stealth` | `Ghost` |

It gained a **`Loading`** parameter (inline spinner + auto-disable) that mirrors `FluentButton.Loading`, so busy submit buttons carry over cleanly:

```razor
<AsdamirButton Type="submit" Variant="AsdamirButton.ButtonVariant.Primary" Loading="@_isSubmitting">
    @L["Login.Submit"]
</AsdamirButton>
```

For a full-width button use a `Class` (page/scoped CSS `width:100%`), never an inline `Style` — inline styles are gated (AUD013).

## UI services

`AddUIServices()` registers, among others: `IDialogService`-style dialogs, `IAsdamirNotificationService`, `IExportService` (Excel/PDF/CSV), `LoadingService`, `IGlobalSearchService` and `ThemeService`.

### Notifications, confirmations & loading (`IAsdamirNotificationService`)

`IAsdamirNotificationService` raises events; the dependency-light **`<AsdamirNotificationHost />`** renders them (toasts, confirmation dialogs, a loading overlay, action toasts). Drop **one** host in your layout — the generated app's `MainLayout` already includes it:

```razor
<AsdamirNotificationHost />   @* or: <AsdamirNotificationHost CancelText="@L["Common.Cancel"].Value" ConfirmText="@L["Common.Confirm"].Value" /> *@
```

```csharp
@inject IAsdamirNotificationService Notify

Notify.ShowSuccess("Saved.");                          // toast
if (await Notify.ConfirmDangerAsync("Delete this?"))   // awaits the user's choice
    await Delete();
Notify.ShowLoading("Importing…"); /* … */ Notify.HideLoading();
```

The host is FluentUI-free, so it works inside a Blazor Web App interactive island and the MAUI Blazor-Hybrid app. **Without a host rendered, `ConfirmAsync`/`ConfirmDangerAsync` return `false`** (deny) — a destructive action never proceeds un-confirmed.

## Framework UI Components (FluentUI-isolation inputs)

`AsdamirTextInput` and `AsdamirNumberInput<T>` are **isolation** form fields: a native `<input>` styled with the shared design tokens, deliberately **not** wrapping any FluentUI component. Binding to them keeps consumer code free of a `Microsoft.FluentUI.*` dependency while staying visually consistent (same tokens, focus/hover/disabled, error display, label alignment) with the FluentUI fields alongside. Both derive from Blazor's `InputBase<TValue>`, so `Value` / `ValueChanged` / `ValueExpression` flow through the ambient `EditContext` and DataAnnotations validation works with zero extra wiring (put a `<DataAnnotationsValidator/>` in your `EditForm`). No JS interop is used anywhere in either component.

All user-facing text (`Label`, `Placeholder`) is an **English-defaulted parameter** — a framework component cannot assume a DB-seeded localization key exists, so the caller passes `@L["…"]` values in. On a validation error the field sets `aria-invalid="true"`, links `aria-describedby` to a `role="alert"` message, and renders the message below; the `<label for>` associates the label to the input for full keyboard/screen-reader use.

### `AsdamirTextInput`

| Parameter | Type | Default | Purpose |
|---|---|---|---|
| `Value` / `ValueChanged` / `ValueExpression` | `string?` | — | Two-way binding (`@bind-Value`); `ValueExpression` drives EditContext/DataAnnotations |
| `MaxLength` | `int` | `0` (none) | Max characters — enforced by the native `maxlength` **and** re-clamped component-side (a paste can exceed the attribute) |
| `AllowedCharacter` | `Func<char,bool>?` | `null` (all) | Caller-supplied per-character predicate; the component runs it over **typing AND paste/IME** runs. The caller owns the RULE, the component owns the plumbing — no format regex is baked into the framework |
| `InputType` | `string` | `text` | `text` / `password` / `email` / `tel` / `search` / `url`; a non-text type throws (numbers → `AsdamirNumberInput`) |
| `Disabled` / `ReadOnly` | `bool` | `false` | Disabled = greyed + inert; ReadOnly = value shown, not editable |
| `Required` | `bool` | `false` | Renders the label asterisk + native `required`/`aria-required`; cosmetic + native (enforcement via a DataAnnotations `[Required]`) |
| `Class` | `string?` | `null` | Extra class(es) on the **root wrapper** (`.asd-textinput`) so a width/layout class (`asd-form-w`, `u-w-160`) sizes the whole field |
| `Label` / `Placeholder` | `string?` | `null` | Localized text passed by the caller |
| `Immediate` | `bool` | `false` | `true` commits each keystroke; `false` commits on change/blur |
| `@attributes` | — | — | Additional attributes (e.g. `autocomplete`) splat onto the `<input>` |

```razor
<AsdamirTextInput @bind-Value="model.Code"
                  Label="@L["Field.Order.Code"]"
                  MaxLength="12"
                  AllowedCharacter="ch => char.IsLetterOrDigit(ch)"
                  autocomplete="off" />
```

### `AsdamirNumberInput<TValue>`

`TValue` is constrained to `decimal` / `int` / `long` (nullable variants allowed). **`double`/`float` are rejected at runtime** with a clear `NotSupportedException` — binary floating point cannot represent decimal money exactly, and exact decimal capture is the whole point of a numeric entry field.

| Parameter | Type | Default | Purpose |
|---|---|---|---|
| `Value` / `ValueChanged` / `ValueExpression` | `TValue` | — | Two-way binding (`@bind-Value`); drives EditContext/DataAnnotations |
| `Decimals` | `int` | `2` | Fraction digits shown/rounded to, using `MidpointRounding.ToEven` (banker's rounding) |
| `Min` / `Max` | `decimal?` | `null` | Inclusive bounds; a committed value is clamped into range |
| `AllowNegative` | `bool` | `false` | When `false`, a leading minus is stripped |
| `MaxIntegerDigits` | `int` | `0` (none) | Caps digits before the separator as the user types |
| `ShowGroupSeparator` | `bool` | `false` | Show the thousands separator in the committed display (always tolerated on parse) |
| `Disabled` / `ReadOnly` | `bool` | `false` | As above |
| `Required` | `bool` | `false` | Renders the label asterisk + native `required`/`aria-required` |
| `Class` | `string?` | `null` | Extra class(es) on the **root wrapper** (`.asd-numinput`) so a width class (`u-w-120`, `u-w-160`) sizes the whole field |
| `Label` / `Placeholder` | `string?` | `null` | Localized text passed by the caller |
| `Immediate` | `bool` | `false` | Commit on keystroke vs. change/blur (re-formatting only ever happens on commit, so the caret is stable while typing) |
| `@attributes` | — | — | Additional attributes splat onto the `<input>` |

```razor
<AsdamirNumberInput TValue="decimal" @bind-Value="model.UnitPrice"
                    Label="@L["Field.OrderItem.UnitPrice"]"
                    Decimals="2" Min="0" MaxIntegerDigits="9" />
```

**Why `type="text"` + `inputmode="decimal"`, NOT `type="number"`.** A native `type="number"` input (1) reinterprets the decimal separator per the **browser's** locale, not the app culture — a `tr-TR` user typing `1234,56` against an `en-US` browser silently loses the fraction; (2) ignores `maxlength`; (3) lets the **mouse wheel** change the value; (4) accepts `e`/`+`/`-` and scientific notation. `AsdamirNumberInput` instead uses `type="text"` + `inputmode="decimal"` (mobile numeric keypad) and a controlled, **culture-aware** parse/format we own: the decimal separator comes from `CultureInfo.CurrentCulture.NumberFormat`, the group separator is tolerated on parse, and the wheel is neutralized with Blazor's `@onwheel:preventDefault` (an attribute directive, not inline JS — no CSP concern). Filtering covers **both** typing and paste/IME so a pasted `$1,299.99` (or an IME commit) is sanitized the same as keystrokes.

**Why these are not FluentUI wrappers (isolation).** Both components carry **zero** `Microsoft.FluentUI.*` references — they render a native `<input>` and read only the shared `--asd-*` / Fluent-2 design tokens. The goal is to let consumer code (generated apps, AppManagement pages) bind to a framework field **without** taking a dependency on the FluentUI component API, so the FluentUI surface can evolve (or be swapped) behind the framework boundary without touching every page. Visually they sit alongside the FluentUI fields unchanged: same tokens, same focus ring/hover, same error treatment, same label alignment.

## Theming

Themes ship as CSS (`light`, `dark`, `high-contrast`) with design tokens; `ThemeService` switches them at runtime. Static assets are served as **static web assets** (no embedded-resource manifest needed).

### Border tokens — decorative vs. interactive (they are NOT interchangeable)

There are two border tokens and picking the wrong one is an accessibility defect, not a taste question:

| Token | Use it for | Contrast rule |
| ----- | ---------- | ------------- |
| `--asd-border-strong` | **Decorative structure** — panel/card outlines, table rules, modal and toast edges, dividers | none (it is a tone, not a boundary) |
| `--asd-border-field` | **The boundary of anything the user operates** — text/number inputs, buttons, pager boxes, the grid search, file dropzones, the signature pad | **≥ 3:1** against the adjacent surface (WCAG 2.1 SC 1.4.11) |

`--asd-border-strong` is `#e3e8f1`, which is **1.23:1** on white. Used as a control boundary it is one rounding error away from invisible — a correctly rendered login field was reported as "the input box does not render" for exactly this reason. Every skin that re-tones `--asd-border-strong` **must** re-tone `--asd-border-field` in the same block; `ThemeFieldBorderContrastTests` fails the build if a declared value drops below 3:1 against its own block's `--asd-surface`, or if a skin overrides one token without the other.

## See also

- [Localization](fundamentals/localization.md) · [Web Security](web-security.md)
