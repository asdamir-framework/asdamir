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
| `Menu` | Navigation menu bound to the menu service |
| `ErrorBoundaryEx` | Error boundary with friendly fallback |
| `AuditTrail`, `ErrorMonitoring`, `Hangfire`, `Outbox`, `PermissionManager`, `Users`, … | Admin building blocks reused by the AdminConsole |

## UI services

`AddUIServices()` registers, among others: `IDialogService`-style dialogs, `INotificationService`, `IExportService` (Excel/PDF/CSV), `LoadingService`, `IGlobalSearchService` and `ThemeService`.

### Notifications, confirmations & loading (`INotificationService`)

`INotificationService` raises events; the dependency-light **`<AsdamirNotificationHost />`** renders them (toasts, confirmation dialogs, a loading overlay, action toasts). Drop **one** host in your layout — the generated app's `MainLayout` already includes it:

```razor
<AsdamirNotificationHost />   @* or: <AsdamirNotificationHost CancelText="@L["Common.Cancel"].Value" ConfirmText="@L["Common.Confirm"].Value" /> *@
```

```csharp
@inject INotificationService Notify

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

## See also

- [Localization](fundamentals/localization.md) · [Web Security](web-security.md)
