# Authentication

**Packages:** `Asdamir.Core` (JWT, encryption, contracts), `Asdamir.Web` (Blazor auth state, token propagation)

## Introduction

Asdamir authenticates users with **JSON Web Tokens (JWT)** and supports refresh-token rotation and two-factor authentication (2FA). The building blocks live in `Asdamir.Core`; the Blazor-side auth state and bearer-token propagation live in `Asdamir.Web`.

## JWT issuance & validation

`IJwtService` (`Asdamir.Core.Contracts`) issues access tokens and reads claims. Configure it from the `Jwt` section:

```jsonc
// appsettings.json — non-secret defaults; Jwt:Key comes from secrets
"Jwt": {
  "Issuer":   "asdamir-adminconsole-api",
  "Audience": "asdamir-adminconsole-clients",
  "AccessTokenLifetimeMinutes": 15,   // fallback; see DB-driven lifetimes below
  "RefreshTokenLifetimeDays": 14
}
```

> **DB-driven, per-app lifetimes.** When AppManagement issues a token for a managed app
> (`app-login`/`app-refresh`, below) it reads the lifetime from **that app's**
> `dbo.AppConfigurations` (`Jwt:AccessTokenLifetimeMinutes` / `Jwt:RefreshTokenLifetimeDays`,
> AppId-scoped) and passes it to the explicit-lifetime `IssueTokens(user, perms, accessLifetime,
> refreshLifetime)` overload. It is re-read on every login/refresh, so changing it in the DB takes
> effect without a redeploy. The `Jwt:*` config above is only the fallback. See
> [Configuration](configuration.md).

```csharp
// Jwt:Key (>= 64 bytes) is supplied via user-secrets / environment, never appsettings.
public sealed class LoginHandler(IJwtService jwt)
{
    public TokenResult Issue(UserAuth user) => jwt.IssueTokens(user);
}
```

> **Issuer/Audience must match** between the issuing service and every validator. Keep them identical across the API and any client that validates the token.

## Refresh tokens

The management app demonstrates the recommended pattern (see `Asdamir.AdminConsole.Api/Auth`):

- Refresh tokens are **SHA-256 hashed before storage** — the raw token never touches the database.
- **Rotation on use:** every refresh issues a new token and invalidates the old one.
- **Reuse detection:** presenting an already-rotated token revokes the user's entire token tree (theft response).
- Expiry is checked against `DateTime.UtcNow`.

## Per-app login (mobile token flow)

Managed apps (web + mobile) don't own users — identity is **central**. Each app's Gateway proxies
credentials to AppManagement, which validates them, checks the user's access to **that** app
(`dbo.UserAppRoles`) and issues an **app-scoped** token (permissions are exactly the user's roles on
that app, never the admin pool):

| Gateway endpoint | → AppManagement | Purpose |
|---|---|---|
| `gateway/auth/login` | `api/admin/auth/app-login` | email + password (+ `App:Code`) → token pair |
| `gateway/auth/twofactor/verify` | `api/admin/auth/app-twofactor/verify` | complete 2FA → token pair |
| `gateway/auth/refresh` | `api/admin/auth/app-refresh` | rotate the refresh token → fresh app-scoped token pair |
| `gateway/auth/forgot-password` | `api/admin/auth/forgot-password` | request a reset link (always 200) |

The Gateway injects the app's `App:Code` so the token is scoped to it.

**Mobile token storage & auto-refresh.** The MAUI app keeps the tokens in **SecureStorage**
(`ITokenStore`), not a cookie. `MobileApiClient` stamps the bearer on every call; on a **401** it calls
`gateway/auth/refresh` once, retries the request, and — if the refresh also fails — clears the tokens
and routes to `/login`. See [Mobile App](../mobile.md).

## Two-factor authentication (2FA)

`ITwoFactorService` issues and verifies a short-lived challenge:

- Challenge tokens use a **128-bit CSPRNG** value (`RandomNumberGenerator`), Base64URL-encoded — not a GUID.
- A successful password step returns `Required = true` + a challenge token; the client completes the second factor against the 2FA endpoint.
- Codes are delivered through the [transactional outbox](outbox.md) (SMS/email).

## Blazor: auth state & token propagation

In a Blazor app, `Asdamir.Web` provides the authentication-state provider and bearer-token propagation:

```csharp
builder.Services.AddSecurityAuthenticationState();
builder.Services.AddSecurityHttpClient();   // attaches the bearer token to outbound API calls
builder.Services.AddSecurityAutoLogout();   // idle/auto-logout + session activity tracking
```

The AdminConsole stores the access token in an HttpOnly cookie and attaches it as a bearer on outbound API calls via a cookie→bearer handler.

## Login failure hygiene

All login failures (unknown email, locked account, wrong password) return the **same opaque response** to avoid username-enumeration. Lockout state is enforced server-side but never disclosed in the response body.

## See also

- [Authorization](authorization.md) · [Web Security](../web-security.md) · [Outbox](outbox.md)
