---
name: asdamir-mobile
description: Use for MAUI Blazor Hybrid MOBILE work — the `framework new mobile` output (`<App>.Mobile` / `.Mobile.Shared` / `.Mobile.Data`). Mobile-only concerns: SecureStorage token store, the named "gateway" HttpClient + 401 refresh-and-retry, offline SQLite cache, MauiProgram DI, appsettings Gateway:BaseUrl, Android build/platform notes. NOT web Blazor Server UI (that's asdamir-blazor-ui). Trigger on "MAUI / mobile app / MauiProgram", "token store / SecureStorage on mobile", "offline / SQLite cache", "gateway client / 401 refresh on mobile", "framework new mobile", "Android / iOS build".
---

# Asdamir mobile (MAUI Blazor Hybrid)

Distilled from the **single source of truth**: the CLI scaffold templates `src/Asdamir.Tools/Templates/Mobile*.sbn`
(what `framework new mobile <Name>` generates). Deep reference: `docs/mobile.md`, memory
`2026-06-14-mobile-offline-hardening`. For shared web Blazor UI rules see `asdamir-blazor-ui`; for the
identity/JWT model behind login see `asdamir-security`.

## Structure (`framework new mobile <Name>` → 4 projects)
- **`<App>.Mobile`** — MAUI head: `MauiProgram` (DI bootstrap), platform glue, `App.xaml`. Target today is
  **`net10.0-android`** only (iOS/maccatalyst/windows are commented-out in the csproj — not yet enabled).
- **`<App>.Mobile.Shared`** — shared Razor UI + `Services/` (token store, api client, auth, localization,
  `UiState`). This is the Hybrid layer shared with the web app's component model.
- **`<App>.Mobile.Data`** — offline SQLite cache (`ICacheStore`/`SqliteCacheStore`, entities). `sqlite-net-pcl`
  + `SQLitePCLRaw.bundle_green`.
- **`<App>.Mobile.Shared.Tests`** — bUnit/xUnit.

## The patterns (all live in the templates)

**Token storage — `ITokenStore` / `SecureStorageTokenStore`** (`MobileTokenStore.sbn`). Tokens live in
`ISecureStorage` (`SetAsync`/`GetAsync`/`Remove`), **never `IPreferences`/SharedPreferences**. An in-memory
cache (`_cachedAccess`/`_cachedRefresh`/`_loaded`) covers Android's known SecureStorage read-after-write
race — writes cache first, then persist; later reads bypass the platform store.

**HttpClient — named `gateway` client** (`MauiProgram.sbn`). `builder.Services.AddHttpClient("gateway", c => { c.BaseAddress = …; c.Timeout = 30s; })`.
Every call site injects `IHttpClientFactory` and `CreateClient("gateway")` — **never `new HttpClient()`** (AUD001).
`#if DEBUG` adds `ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator })`
so the device trusts the dev Gateway's self-signed HTTPS cert — `#if DEBUG` keeps it **out of Release**.

**Gateway URL — `appsettings.json` `Gateway:BaseUrl`** (`MobileAppsettings.sbn`). Loaded as an **embedded
resource** in `MauiProgram` via `Assembly.GetManifestResourceStream("<MobileNamespace>.appsettings.json")`
→ `AddJsonStream`. Required (`?? throw`). Configurable per build flavour; **never a real secret here**
(secrets are runtime, in SecureStorage).

**API call flow — `MobileApiClient`** (`MobileApiClient.sbn`). Stamps `Bearer` on every request; on **401**
it refreshes **once** (`_auth.RefreshAsync()`) and retries; if refresh fails → `_auth.LogoutAsync()` +
`_nav.NavigateTo("/login", replace: true)`. A **transport failure** (offline/DNS/timeout → `HttpRequestException`
or `TaskCanceledException`) returns `null` and **never throws to the UI**. Each attempt builds **fresh
`JsonContent`** (retry-safe). `GetCachedAsync<T>(path, cacheKey)` writes fresh responses to the SQLite cache
and serves the cached copy when offline (`FromCache` flag).

**Auth — `MobileAuthService`** (`MobileAuthService.sbn`). `LoginAsync` → `gateway/auth/login`;
`RefreshAsync` → `gateway/auth/refresh` (rotates both tokens); `LogoutAsync` → clears tokens;
`IsAuthenticatedAsync`; `GetCurrentUserAsync` decodes the JWT payload **locally** (no network).
**AUD007:** never log password/email/raw token — only an 8-char access-token prefix.

**Localization — `MobileLocalizationService`** (`MobileLocalizationService.sbn`). Loads
`gateway/localization/all?culture=…` and caches in-process; `SupportedCultures` = `tr-TR`/`en-US`/`ru-RU`,
default **`tr-TR`**; missing key → returns the key; load failure → empty map. **DB-backed via the Gateway,
no `.resx`** (same model as the web's `DatabaseDynamicResourceStore`). UI calls `Ui.T("key")`.

**Offline cache — `ICacheStore`/`SqliteCacheStore`** (`MobileDataCacheStore.sbn`). JSON key/value over local
SQLite under `FileSystem.AppDataDirectory` (`<app>-cache.db3`), so screens render last-known data offline.
Tolerates a stale shape (`JsonException` → default). **NOT for secrets** — SQLite is plain on-disk; tokens
belong in SecureStorage.

**DI bootstrap — `MauiProgram`** (`MauiProgram.sbn`). Singletons: `SecureStorage.Default`,
`Preferences.Default`, `Connectivity.Current`, `ITokenStore→SecureStorageTokenStore`, `UiState`,
`ICacheStore→SqliteCacheStore(path)`, the named `gateway` client, and `MobileAuthService`/`MobileApiClient`/
`MobileLocalizationService`. `#if DEBUG` adds `AddBlazorWebViewDeveloperTools()` + `AddDebug()`.

**Shared Razor** (`MobileShared*.sbn`, e.g. login). `@page` pages inject the Mobile.Shared services + `UiState`
(theme + culture, persisted in `Preferences`), use `EditForm`/`DataAnnotationsValidator`, and resolve text via
`Ui.T(key)`. These are the Hybrid components shared with the web app — owned by the same frontend engineer.

## Build / platform notes (`docs/mobile.md`)
- Prereqs: the MAUI workload + Android SDK (min API 23). 
- **Build ONE RID:** `dotnet build -f net10.0-android -r android-arm64` (or `android-x64` for an x64 emulator).
  A plain multi-RID build fails with **NETSDK1047** — the `<RuntimeIdentifiers>` list was a .NET 9-era
  workaround the template omits.

## DON'T
- **Don't store tokens in `Preferences`/SharedPreferences** — only `ISecureStorage` via `ITokenStore`.
- **Don't `new HttpClient()`** — inject `IHttpClientFactory` and `CreateClient("gateway")` (AUD001).
- **Don't leave self-signed cert acceptance outside `#if DEBUG`** — it must never reach Release.
- **Don't put secrets in `appsettings.json`** (only `Gateway:BaseUrl`) **or in the SQLite cache** (plain on-disk).
- **Don't log password/email/raw token** — 8-char prefix only (AUD007).
- **Don't let transport failures throw to the UI** — return null/false and fall back to the cache.
- **Don't use `.resx`** — mobile localization is Gateway/DB-backed (`gateway/localization/all`).
- **Don't do a plain multi-RID build** — build a single RID (NETSDK1047).
- **Don't treat this as web Blazor Server** — server-only rules (CSS isolation, FluentUI z-index) live in
  `asdamir-blazor-ui`; mobile shares the Razor model but adds the SecureStorage/gateway/offline concerns above.
