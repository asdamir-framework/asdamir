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
- **Rotation on use:** every refresh issues a new token and invalidates the old one. The spend is **atomic** — when two requests present the same token at once, exactly one is rotated.
- **Reuse detection:** presenting an already-rotated token revokes the user's entire token tree (theft response). The loser of a concurrent race is a second spend like any other and is handled the same way, so a client must serialize its own refreshes (one refresh in flight per session).
- **Context binding:** a refresh token is redeemable only where it was issued — a console token at the console refresh endpoint, an app token at that app's `app-refresh`.
- Expiry is checked against `DateTime.UtcNow`.

### Sign-out and forced session end

- `POST api/admin/auth/logout` takes an optional `{ "refreshToken": "…" }` body. **With** it, only that session's refresh token is revoked (the user's other browsers/devices stay signed in); **without** a body, every refresh token of the user is revoked ("sign out everywhere").
- An administrator **resetting a user's password** or **deactivating a user** revokes all of that user's refresh tokens and ends their live console sessions immediately — the account stops working at once rather than when its tokens expire. Re-activating a user grants nothing back; they sign in again.
- A self-service password reset (`reset-password`) and a password change also revoke every refresh token of the user.

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
- **A challenge is bound to the sign-in surface that created it** — `console`, or `app:<code>` for a managed app. It can only be redeemed at that surface's verify endpoint: a code issued for an app sign-in can never complete a console sign-in (or another app's), and is refused exactly like an unknown token.
- **The console verify step re-checks the operator gate** and the lockout state itself — it is the endpoint that issues the token, so it does not rely on the password step having asked.
- **A wrong code counts toward account lockout** exactly like a wrong password, and the failed-attempt counter is cleared only when authentication is complete (after the code step for a 2FA user).
- **Delivery:** outside `Development` the code is sent by **SMS** through the [transactional outbox](outbox.md); the message body is the `TwoFactor:SmsTemplate` setting (`{0}` = the code), with a built-in default. In `Development` the code is only logged, never sent.
- **A 2FA-enabled user without a phone number cannot sign in** outside `Development`: a challenge nobody can answer is not issued, and the login is refused with the same opaque failure as a wrong password.
- **Code lifetime** defaults to **180 seconds** (`Security.Otp.TtlMinutes`, default `3`, clamped to 30 s – 30 min) — the same value the login page counts down.

## Blazor: auth state & token propagation

In a Blazor app, `Asdamir.Web` provides the authentication-state provider and bearer-token propagation:

```csharp
builder.Services.AddSecurityAuthenticationState();
builder.Services.AddSecurityHttpClient();   // attaches the bearer token to outbound API calls
builder.Services.AddSecurityAutoLogout();   // idle/auto-logout + session activity tracking
```

The AdminConsole stores the access token in an HttpOnly cookie and attaches it as a bearer on outbound API calls via a cookie→bearer handler.

The Blazor host holds **no `Jwt:Key`** (secrets live on the API tier), so it never trusts a claim it reads out of a token it was handed. Before it creates the cookie session it presents the token to the API (`GET api/admin/auth/me`) and builds the cookie principal from the **API's answer**; a rejected token — or an API that cannot be reached — means no session (fail closed). Each cookie session is identified by a random, server-minted session id (the `sid` claim), not by the user id, so two browsers of the same user are independent sessions: each keeps its own rotating refresh token, and signing out of one leaves the other alone. The server-side session map is in-memory and single-node; after a restart it is rebuilt from the cookies.

## Login failure hygiene

All login failures (unknown email, locked account, wrong password) return the **same opaque response** to avoid username-enumeration. Lockout state is enforced server-side but never disclosed in the response body.

The anonymous auth endpoints are rate limited per client IP, each in its own bucket, overridable through `RateLimiting:<Bucket>:PermitLimit` / `RateLimiting:<Bucket>:WindowSeconds`:

| Bucket | Endpoints | Default |
|---|---|---|
| `Login` | `login`, `app-login` | 5 per 60 s |
| `TwoFactorSend` | `twofactor/verify`, `app-twofactor/verify` | 3 per 60 s |
| `Refresh` | `refresh`, `app-refresh` | 10 per 60 s |
| `ForgotPassword` | `forgot-password` | 5 per 60 s |
| `ResetPassword` | `reset-password` | 5 per 60 s |

## Upgrade notes

- **Operators sign in once more after upgrading the AdminConsole.** A console cookie issued before sessions carried a `sid` claim is rejected; the next request lands on the login page.
- **Apply the database migrations before deploying the new API build.** Atomic refresh rotation and context-bound 2FA challenges rely on schema objects the new build calls; a 2FA challenge started by the previous build is not redeemable by the new one (it expires within minutes — the user simply signs in again).
- **A 2FA-enabled account needs a phone number** outside `Development`, or it can no longer sign in.

## See also

- [Authorization](authorization.md) · [Web Security](../web-security.md) · [Outbox](outbox.md)
