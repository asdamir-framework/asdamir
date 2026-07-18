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

## Theming

Themes ship as CSS (`light`, `dark`, `high-contrast`) with design tokens; `ThemeService` switches them at runtime. Static assets are served as **static web assets** (no embedded-resource manifest needed).

## See also

- [Localization](fundamentals/localization.md) · [Web Security](web-security.md)
