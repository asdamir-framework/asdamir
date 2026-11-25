---
name: asdamir-security
description: Use when working on authentication OR authorization — login/JWT/2FA/refresh, AND permissions/policies/RBAC/guarding endpoints. These go together (you secure an endpoint by validating identity and checking access). Trigger on "login / JWT / 2FA / refresh token / sign-in", "authorize / permission / policy / RBAC / roles", "[Authorize] / AllowAnonymous", "guard this endpoint", "who can access".
---

# Asdamir security (authentication + authorization)

Identity and access control are one concern — securing an endpoint means *both*. Deep reference:
`docs/fundamentals/authentication.md`, `docs/fundamentals/authorization.md` (console-side user, role and
permission administration is done in the commercial AppManagement app).

## Authentication

**Who issues vs who validates**
- **JWTs are issued ONLY by AppManagement** (`IJwtService` in `Asdamir.Core`). Managed-app **Gateways
  validate** them with the **shared** `Jwt:Key` + matching `Jwt:Issuer`/`Jwt:Audience`. A Gateway never
  mints tokens — it proxies credentials to AppManagement's auth API.
- **One identity table.** Console operators AND app end-users both live in **`dbo.Users`**: an operator
  sits at `AppId = NULL` and is flagged `IsSuperAdmin`; an app end-user carries that app's `AppId`. There
  are **no `Admin*` tables** — migration 008 split them out, migration 078 folded them back
  (`AdminUsers → Users`, `AdminUserAppRoles → UserAppRoles`) and dropped the `Admin*` set. (The C#
  `IAdminUserStore` abstraction name persists but maps to `dbo.Users` via the `User_*` procs.) No `Ent*`
  table names (legacy).

**Per-app login** — `api/admin/auth/app-login` / `app-twofactor/verify` / `app-refresh` issue **app-scoped**
JWTs whose permissions are the user's role codes **on that app** (`dbo.UserAppRoles`), never the
control-plane "all permissions". The generated Gateway exposes these as `gateway/auth/login` etc. (proxy).

**Free mode** (`new app --mode free`) — a self-contained app has **no control plane**: its **Gateway issues
and validates its own JWT** (`Asdamir.Core` `JwtService`) against the app's **own** `dbo.Users`, with the
login gate "user exists + is active". The starter admin ships with **`ForcePasswordChange = 1`**, so the
**first sign-in is forced through a change-password page** before the dashboard — the `gateway/auth/change-password`
endpoint verifies the current password, sets the new one, clears the flag, and **revokes all refresh tokens**.
Subsequent logins are normal. See `docs/cli.md` → *free vs commercial mode*.

**Refresh tokens** — single-use **rotation with reuse detection** (re-presenting a used token revokes all
of the user's tokens); stored as **SHA-256 hashes** in `dbo.RefreshTokens` (DB-backed in Dapper mode).
Rotation is **atomic** (`dbo.RefreshToken_ValidateAndRotate`, `AsdamirVault_143`): of two concurrent spends
exactly one rotates and **the loser is treated as reuse → revoke-all** — so any client must serialize its
refreshes per session. Tokens are **context-bound** (`console` / `app:<code>`).

**Console session (AdminConsole Blazor host)** — the host holds **no `Jwt:Key`**. `/api/admin-session/establish`
validates the handed-in token by presenting it to the API's `GET api/admin/auth/me` (`AdminTokenValidator.cs`)
and builds the cookie principal from the **API's answer**; no answer → no session (fail closed). **Never
`ReadJwtToken` a caller-supplied token and trust its claims.** `AdminUserSessionStore` is keyed by a random
**`sid`** claim, not `sub`; a cookie without `sid` is rejected (operators re-login once after upgrade). Still
in-memory / single-node.

**Logout / forced end** — `POST api/admin/auth/logout` with `{ refreshToken }` ends that one session; with no
body it revokes all. An admin password reset or user deactivation (`Rbac/UsersController`) revokes the user's
refresh tokens **and** sets the session-revocation cutoff.

**2FA & lifetimes** — 2FA is conditional on `IsTwoFactorEnabled`. A challenge is **bound to its context**
(`console` or `app:<code>`, `AsdamirVault_144`) and redeemable only at that surface's verify endpoint; the
console verify **re-checks the operator gate + lockout**; a **wrong code counts toward lockout**. Outside
Development the code goes out by **SMS via the outbox** (`TwoFactor:SmsTemplate`), so a 2FA user **without a
phone number cannot sign in**. Default code lifetime **180 s** (`Security.Otp.TtlMinutes`). Access/refresh
**lifetimes are DB-driven per app** (`AppConfigurations` → `Jwt:AccessTokenLifetimeMinutes` /
`Jwt:RefreshTokenLifetimeDays`). Mobile keeps tokens in `ITokenStore` (SecureStorage) and auto-refreshes on 401.

**Rate limits** — every anonymous auth endpoint carries `[RateLimitPolicy]` (per IP, own bucket,
`RateLimiting:<Bucket>:PermitLimit`/`WindowSeconds`): Login 5/60, TwoFactorSend 3/60, Refresh 10/60,
**ForgotPassword 5/60, ResetPassword 5/60**. A new anonymous endpoint gets one too.

## Authorization (two-tier RBAC)
1. **Admin-pool** (AppManagement.Api): the admin × app × role matrix `dbo.UserAppRoles`; the
   `OrchestrationAppAccessFilter` enforces it — SuperAdmins reach every app, others only their granted
   apps. **Membership is read-only:** a mutating verb also needs the write permission the action declares
   with **`[RequiresAppPermission("…")]`** (`appconfig.write`, `localization.write`, `outbox.write`,
   `hangfire.write`, `roles.write`, `userapproles.write`; `reports.read` for `reports/run`). A mutating
   action with **no** attribute is **super-admin only** (fail closed — currently error-resolve and menu
   upsert/delete). Use only codes seeded in the admin permission catalogue (literal match → an unseeded
   code is a guaranteed 403). The seeded `AppAdmin` role is read-only. Permissions resolve **through roles**
   (`UserRoles → RolePermissions → Permissions`) — never granted directly to users.
2. **Managed-app** (each Gateway): the end-user's permissions are their role codes on that app (from the
   app-scoped JWT above). Gate UI/actions by those permission claims.

**Wiring** — `AddBasicAuthorization()` / `AddEnterpriseAuthorization()`. A generated Gateway is
**fail-closed**: a `FallbackPolicy` requires auth, so every endpoint needs it unless it carries explicit
`[AllowAnonymous]` (login/health). `gateway/admin/*` orchestration endpoints are guarded by the
**`AppMgmtClient`** policy (callable only by AppManagement's `client_credentials` JWT, matched on
`client_id`). Route authorization blocks unauthorized navigation before render (cached + audited).

## DON'T
- **Don't mint JWTs in a Gateway** — only AppManagement issues; Gateways validate (shared key/issuer/audience).
- **Don't store refresh tokens in plaintext / log raw tokens** — hashed only.
- **Don't grant app permissions from the admin pool** — a managed app's perms come from `UserAppRoles`.
- **Don't grant permissions directly to a user** — assign roles; permissions flow through roles.
- **Don't add `[AllowAnonymous]`** without a comment justifying it (audit-lint / review flags it).
- **Don't open `gateway/admin/*`** beyond the `AppMgmtClient` policy, or bypass `OrchestrationAppAccessFilter`.
- **Don't add a mutating per-app endpoint without `[RequiresAppPermission]`** unless super-admin-only is the intent.
- **Don't build a cookie/session from an unvalidated JWT**, and don't key server-side session state by user id.

Validating the *shape* of a login/sign-in request (required fields, email format) is **input validation →
`asdamir-validation`**, not this skill — this one is the auth *logic* (who issues/validates, RBAC, guarding).

Rotating `Jwt:Key` / encryption keys / client secrets → see the `asdamir-secret-rotation` skill.
