# Secret Management & Key Rotation

Operational runbook for the secrets AppManagement and every managed app depend on: where they live, how
they're stored, and how to rotate them **without losing data or locking users out**.

> See also: [Encryption](fundamentals/encryption.md) (the `IEncryptionService` design),
> [Authentication](fundamentals/authentication.md) (JWT issuance/validation).

## Secret inventory

| Secret | Protects | Storage | Rotation impact |
|---|---|---|---|
| **`Jwt:SigningKeyPem`** | The private key AppManagement signs every token with (ES256). **Only the issuer holds it.** | env var `Jwt__SigningKeyPem` / user-secrets. EC P-256 PEM. | None for validators that follow the published key set, when rotated with the procedure below. |
| **`Jwt:AdditionalPublicKeysPem`** | Not a secret. Further public keys the issuer publishes and accepts: the *next* key before a rotation, the *previous* one after it. | Same place as the signing key. Up to four `PUBLIC KEY` blocks. | — |
| **`App:ServiceKeyPem`** | A managed app's own private key: its Gateway signs the tokens it presents to AppManagement (central log, agent audit). | That app's Gateway only — env var `App__ServiceKeyPem` / user-secrets. | None when rotated with the procedure below (two keys can be registered at once). |
| **`Jwt:Key`** | A symmetric signing key (HMAC-SHA256). Correct for a **self-contained (free-mode) app**, which issues and validates its own tokens. In a deployment not yet moved to asymmetric signing it is still **shared** by AppManagement and every Gateway — see [the migration](#moving-from-the-shared-jwtkey-to-asymmetric-signing). | env var `Jwt__Key` / user-secrets. ≥64 bytes. | All **access** tokens signed with the old key are rejected. Clients silently refresh (refresh tokens are stored as SHA-256 **hashes**, not signed, so they survive). |
| **`Security:EncryptionKey`** | Derives the AES-256-GCM key (PBKDF2) that encrypts at-rest values: `Apps.EncryptedClientSecret`, `AppConfigurations` rows with `IsEncrypted=1`, and any `v2:`-prefixed Companies connection string. | env var `Security__EncryptionKey` / user-secrets. ≥32 chars. | **Destructive** — every value encrypted under the old key becomes unreadable the moment the key changes. **Re-encrypt first** with `asdamir secrets rotate-key` (below). |
| **`Security:EncryptionSalt`** | Optional explicit PBKDF2 salt. When unset, a deterministic salt is derived from the key. | env / user-secrets. ≥16 chars. | Changing it changes the derived key → same impact as rotating `EncryptionKey`. Rotate the two **together**. |
| **`ConnectionStrings:*`** | DB credentials (AsdamirVault; each app's own business DB on its API tier). | env / user-secrets only — **never** `appsettings.json`. | Restart the affected tier. No re-encryption. |
| **Per-app `ClientSecret`** | The `client_credentials` secret a managed app validates on `gateway/auth/client-token`; AppManagement stores it as `Apps.EncryptedClientSecret`. | AsdamirVault (encrypted at rest). The app keeps its copy in its own secret store. | Rotate via the AdminConsole / API (below); update the app's configured copy. Not tied to `EncryptionKey` rotation. Used only for the pre-2.0 token exchange: a control plane that signs with `Jwt:SigningKeyPem` does not send it to a generated app. |

**Storage rule:** secrets never live in `appsettings.json`. Dev uses `dotnet user-secrets`; prod uses
environment variables or a secret store (Azure Key Vault, AWS Secrets Manager, a mounted secret file).
The read-only **Environment Health** screen (AdminConsole) reports each secret's *presence* (✅/❌) —
never its value — so you can confirm a host is configured before cutover.

---

## Rotating `Security:EncryptionKey` (re-encryption required)

Because this key encrypts data at rest, you must re-encrypt that data **before** switching the deployment
to the new key. The CLI does the re-encryption pass in one transaction.

`asdamir secrets rotate-key` re-encrypts, from the OLD key to the NEW key:

- `dbo.Apps.EncryptedClientSecret` (every registered app's client secret)
- `dbo.AppConfigurations` rows where `IsEncrypted = 1`

It is a **dry-run by default** (decrypt + re-encrypt + verify in memory, no writes); `--apply` commits.
It is **idempotent** — rows already on the new key are detected and skipped, so an interrupted run is
safe to re-run. If a row can't be decrypted with the old key (and isn't already on the new key) it
**aborts and rolls back** the whole pass — nothing is half-rotated.

### Procedure

```bash
# 0) BACK UP AsdamirVault first (this rewrites encrypted columns).

# 1) Provide both keys via env vars (kept out of shell history / process args).
export ASDAMIR_OLD_ENCRYPTION_KEY='<current Security:EncryptionKey>'
export ASDAMIR_NEW_ENCRYPTION_KEY='<new 32+ char key>'
# If you use an explicit salt, also export ASDAMIR_OLD_ENCRYPTION_SALT / ASDAMIR_NEW_ENCRYPTION_SALT.

# 2) DRY-RUN — verify every value re-encrypts & round-trips, see the counts, write nothing.
asdamir secrets rotate-key --server <sql> --database AsdamirVault --user <login> --password <pwd>

# 3) APPLY — commit the re-encrypted values (single transaction).
asdamir secrets rotate-key --server <sql> --database AsdamirVault --user <login> --password <pwd> --apply

# 4) Deploy the NEW key as Security:EncryptionKey on AppManagement (and restart it).
#    Token cache auto-evicts on 401, so orchestration picks up the re-encrypted secrets.

# 5) Verify: AdminConsole → an app action that needs the client secret (orchestration) succeeds.
```

Auth flags work like `db apply`: `--connection "<connstr>"` (wins over the parts) or
`--server/--database/--user/--password`; omit `--user` on Windows for integrated auth.

### Not covered by the tool (do these manually)
- **Companies connection strings in `appsettings.json`** (multi-company catalog, `v2:`-prefixed): produce
  new ciphertext under the new key with `asdamir secrets encrypt` and paste it in.
- **Managed apps' own encrypted config** in their own databases: run `rotate-key` against each app's DB
  too if it stores `IsEncrypted=1` values (the column/table names are the same).

---

## Producing an encrypted value (`secrets encrypt`)

To seed an encrypted `AppConfigurations` value or a `v2:` Companies connection string:

```bash
export ASDAMIR_ENCRYPTION_KEY='<Security:EncryptionKey>'
asdamir secrets encrypt --value 'the-plaintext'      # prints: v2:....
echo 'the-plaintext' | asdamir secrets encrypt        # or pipe it on stdin
```

The output uses the exact format the runtime expects (v2 AES-GCM), so it decrypts in-app with no further
steps. There is intentionally **no** `decrypt` command — the tool never prints a stored secret back.

---

## Rotating the token signing key (`Jwt:SigningKeyPem`)

AppManagement signs with a private key only it holds and publishes the public half at
`/.well-known/jwks.json`. Gateways fetch that document (hourly, and when a token names a key they do not
know), so a rotation is a change on AppManagement alone.

### Planned rotation — no request fails

```bash
asdamir secrets new-signing-key --out ./new-key     # jwt-signing-key.pem + jwt-public-key.pem
```

1. **Announce.** Add the NEW public key to `Jwt:AdditionalPublicKeysPem` on AppManagement and restart it.
   It still signs with the old key; the new one is now published. Wait at least **one hour** (the
   Gateways' refresh interval) so every Gateway has it.
2. **Switch.** Set `Jwt:SigningKeyPem` to the NEW private key and put the OLD public key in
   `Jwt:AdditionalPublicKeysPem`. Restart. New tokens are signed with the new key; tokens signed before
   the switch stay valid.
3. **Retire.** After the access-token lifetime has passed (`Jwt:AccessTokenLifetimeMinutes`, 15 by
   default — and again at least an hour), remove the old public key from `Jwt:AdditionalPublicKeysPem`.

| If you… | Then… |
|---|---|
| skip step 1 | Each Gateway rejects the first request carrying a new-key token (401) and accepts from the next: the unknown key id triggers a re-fetch. |
| skip listing the old key in step 2 | Every token signed before the switch is rejected at once, on AppManagement and on the Gateways; clients refresh or sign in again. |
| run a Gateway with `Jwt:PublicKeyPem` (a pinned key) instead of the URL | It does not follow the published set. Give it the new key in its own `Jwt:AdditionalPublicKeysPem` before step 2. |

### After a leak

Do not announce and do not keep the old key: set `Jwt:SigningKeyPem` to a new key with **nothing** in
`Jwt:AdditionalPublicKeysPem`, restart, and clear `dbo.RefreshTokens` to force every session to sign in
again. Gateways drop the old key at their next fetch — within the hour, or immediately on restart.

---

## Rotating an app's service key (`App:ServiceKeyPem`)

Per app, on that app's Gateway and its registration only:

1. Generate a pair (`asdamir secrets new-signing-key`).
2. Register **both** public keys for the app — `PUT /api/admin/apps/{appId}/service-key` with the old and
   the new `PUBLIC KEY` blocks.
3. Set the new private key as `App:ServiceKeyPem` on the Gateway (and `AgentAudit:ServiceKeyPem` if the
   app uses the agent-audit sink) and restart it.
4. Register the new public key alone.

After a leak, skip step 2: register only the new key. Tokens signed with the old one are refused from the
next request.

---

## Moving from the shared `Jwt:Key` to asymmetric signing

Before the 2.0 line, one `Jwt:Key` was held by AppManagement **and** by every managed app's Gateway, so
whoever held any Gateway's key could sign an operator token. The move takes three steps, in this order,
and no step logs anyone out.

**What each host holds**

| | Before | After step 1 | After step 2 | After step 3 |
|---|---|---|---|---|
| AppManagement | `Jwt:Key` | `Jwt:Key` | `Jwt:SigningKeyPem` + `Jwt:Key` + `Jwt:AcceptLegacySymmetric=true` | `Jwt:SigningKeyPem` |
| Each Gateway | `Jwt:Key` | `Jwt:JwksUrl` + `Jwt:Key` + `Jwt:AcceptLegacySymmetric=true` + `App:ServiceKeyPem` | unchanged | `Jwt:JwksUrl` + `App:ServiceKeyPem` |
| Signs user tokens | shared key | shared key | AppManagement's private key | AppManagement's private key |

### Step 1 — every Gateway accepts both

For each managed app, on Asdamir 2.0 packages:

1. Make the Gateway validate through the framework. An app generated before 2.0 builds the key by hand in
   `Program.cs`; replace that with:

   ```csharp
   var jwtKeys = JwtKeyMaterial.FromConfiguration(builder.Configuration);   // Asdamir.Core.Services
   // …inside AddJwtBearer, after TokenValidationParameters is assigned (remove IssuerSigningKey from it):
   jwtKeys.ApplyTo(opts.TokenValidationParameters);
   ```

   and pass the service key to the log sink: `ServiceKeyPem = ctx.Configuration["App:ServiceKeyPem"]` in
   `AppLogForwardOptions`. (A freshly generated app already has both.)
2. Configure it: `Jwt:JwksUrl = https://<AppManagement>/.well-known/jwks.json`, keep `Jwt:Key`, add
   `Jwt:AcceptLegacySymmetric=true`.
3. Give the app its service key: generate a pair, set the private key as `App:ServiceKeyPem` on the
   Gateway, restart it, **then** register the public key (`PUT /api/admin/apps/{appId}/service-key`).
   Registering first would stop the app's log forwarding until the Gateway has the key.

AppManagement has not changed, so it publishes no key yet; the Gateways keep validating with the shared key.

### Step 2 — AppManagement switches

Generate the signing key, set `Jwt:SigningKeyPem` on AppManagement, keep `Jwt:Key` and add
`Jwt:AcceptLegacySymmetric=true`, restart. It now signs ES256 and still accepts the tokens it issued
before the restart. Each Gateway picks the new key up without a restart: the first request with a new
token triggers the fetch (that one request may get a 401), every later one validates.

**Do this only when every Gateway has finished step 1.** A Gateway still validating by hand with the
shared key rejects every token from this point on.

### Step 3 — remove the shared key

After the access-token lifetime has passed, remove `Jwt:Key` and `Jwt:AcceptLegacySymmetric` from
AppManagement and from every Gateway, and restart them. Then **discard the old key**: it must not be
reused anywhere.

| Check | Expected |
|---|---|
| `GET https://<AppManagement>/.well-known/jwks.json` | One key (`"alg":"ES256"`). Empty means AppManagement is still signing with `Jwt:Key`. |
| Sign in to an app, open a page that calls its API | Works. A 401 right after a successful sign-in means that Gateway does not trust the published key. |
| An app without a registered service key | Its central log forwarding stops at step 3: it was still signing with the shared key. Finish step 1.3 for it. |

---

## Rotating `Jwt:Key`

This applies to a **self-contained (free-mode) app**, where the key is held by that one app, and to a
deployment that has not yet moved to asymmetric signing, where it is still shared.

1. Set the new `Jwt__Key` — on the app's Gateway, or, while it is still shared, on AppManagement.Api **and**
   every managed app's Gateway in the same window. If they diverge, token validation fails until they match.
2. Restart each tier. Live **access** tokens signed with the old key stop validating; clients transparently
   exchange their **refresh** token (validated by stored SHA-256 hash, not by signature, so it survives) for
   a fresh access token signed with the new key.
3. Users with no valid refresh token simply re-authenticate. There is no data to re-encrypt.

To force every session to re-login (e.g. suspected key compromise), also clear `dbo.RefreshTokens`.

---

## Rotating a managed app's `ClientSecret`

Per-app, no `EncryptionKey` involvement:

1. AdminConsole → the app → **Rotate secret** (or `PUT /api/admin/apps/{appId}` with `RotateClientSecret`).
   AppManagement re-encrypts and stores the new value; its token cache for that app evicts on the next 401.
2. Update the **managed app's own** configured secret (its `client_credentials` secret) to match, and
   restart that app's Gateway.

---

## See also

- [Encryption](fundamentals/encryption.md) · [Authentication](fundamentals/authentication.md) ·
  [CLI → `secrets`](cli.md)
