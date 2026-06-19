# Asdamir.Web

Blazor UI, web security and localization for the **Asdamir** framework (.NET 10). A Razor Class Library built on Microsoft FluentUI.

- **UI components** — enterprise DataGrid, dialogs, charts, notifications, data export (Excel/PDF/CSV), barcode scanner, OCR, signature pad, international phone input, theming
- **Web security** — JWT auth state + token propagation, CSP nonce, security headers, request rate limiting, Data Protection, route authorization, auto-logout
- **Localization** — API-backed, multi-culture (TR / EN / RU) string localization with caching

## Install

```bash
dotnet add package Asdamir.Web
```

```csharp
builder.Services.AddFluentUIComponents();
builder.Services.AddUIServices();
builder.Services.AddFrameworkSecurity(builder.Configuration);
```

## Documentation

Full guides: **[Asdamir documentation](https://github.com/asdamir-framework/asdamir/tree/main/docs)** — see *UI Components*, *Web Security* and *Localization*.

## License

MIT
