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

## Theming

Themes ship as CSS (`light`, `dark`, `high-contrast`) with design tokens; `ThemeService` switches them at runtime. Static assets are served as **static web assets** (no embedded-resource manifest needed).

## See also

- [Localization](fundamentals/localization.md) · [Web Security](web-security.md)
