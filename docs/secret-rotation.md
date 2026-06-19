# Secret Management & Key Rotation

Operational runbook for the secrets AppManagement and every managed app depend on: where they live, how
they're stored, and how to rotate them **without losing data or locking users out**.

> See also: [Encryption](fundamentals/encryption.md) (the `IEncryptionService` design),
> [Authentication](fundamentals/authentication.md) (JWT issuance/validation).

## Secret inventory

| Secret | Protects | Storage | Rotation impact |
|---|---|---|---|
| **`Jwt:Key`** | Signs & validates all JWTs (HMAC-SHA256). **Shared** by AppManagement and every managed app's Gateway so tokens are mutually trusted. | env var `Jwt__Key` (prod) / user-secrets (dev). ≥64 bytes. | All **access** tokens signed with the old key are rejected. Clients silently refresh (refresh tokens are stored as SHA-256 **hashes**, not signed, so they survive). Must be rolled out to **all** tiers together. |
| **`Security:EncryptionKey`** | Derives the AES-256-GCM key (PBKDF2) that encrypts at-rest values: `Apps.EncryptedClientSecret`, `AppConfigurations` rows with `IsEncrypted=1`, and any `v2:`-prefixed Companies connection string. | env var `Security__EncryptionKey` / user-secrets. ≥32 chars. | **Destructive** — every value encrypted under the old key becomes unreadable the moment the key changes. **Re-encrypt first** with `framework secrets rotate-key` (below). |
| **`Security:EncryptionSalt`** | Optional explicit PBKDF2 salt. When unset, a deterministic salt is derived from the key. | env / user-secrets. ≥16 chars. | Changing it changes the derived key → same impact as rotating `EncryptionKey`. Rotate the two **together**. |
| **`ConnectionStrings:*`** | DB credentials (AsdamirVault; each app's own business DB on its API tier). | env / user-secrets only — **never** `appsettings.json`. | Restart the affected tier. No re-encryption. |
| **Per-app `ClientSecret`** | The `client_credentials` secret a managed app validates on `gateway/auth/client-token`; AppManagement stores it as `Apps.EncryptedClientSecret`. | AsdamirVault (encrypted at rest). The app keeps its copy in its own secret store. | Rotate via the AdminConsole / API (below); update the app's configured copy. Not tied to `EncryptionKey` rotation. |

**Storage rule:** secrets never live in `appsettings.json`. Dev uses `dotnet user-secrets`; prod uses
environment variables or a secret store (Azure Key Vault, AWS Secrets Manager, a mounted secret file).
The read-only **Environment Health** screen (AdminConsole) reports each secret's *presence* (✅/❌) —
never its value — so you can confirm a host is configured before cutover.

---

## Rotating `Security:EncryptionKey` (re-encryption required)

Because this key encrypts data at rest, you must re-encrypt that data **before** switching the deployment
to the new key. The CLI does the re-encryption pass in one transaction.

`framework secrets rotate-key` re-encrypts, from the OLD key to the NEW key:

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
framework secrets rotate-key --server <sql> --database AsdamirVault --user <login> --password <pwd>

# 3) APPLY — commit the re-encrypted values (single transaction).
framework secrets rotate-key --server <sql> --database AsdamirVault --user <login> --password <pwd> --apply

# 4) Deploy the NEW key as Security:EncryptionKey on AppManagement (and restart it).
#    Token cache auto-evicts on 401, so orchestration picks up the re-encrypted secrets.

# 5) Verify: AdminConsole → an app action that needs the client secret (orchestration) succeeds.
```

Auth flags work like `db apply`: `--connection "<connstr>"` (wins over the parts) or
`--server/--database/--user/--password`; omit `--user` on Windows for integrated auth.

### Not covered by the tool (do these manually)
- **Companies connection strings in `appsettings.json`** (multi-company catalog, `v2:`-prefixed): produce
  new ciphertext under the new key with `framework secrets encrypt` and paste it in.
- **Managed apps' own encrypted config** in their own databases: run `rotate-key` against each app's DB
  too if it stores `IsEncrypted=1` values (the column/table names are the same).

---

## Producing an encrypted value (`secrets encrypt`)

To seed an encrypted `AppConfigurations` value or a `v2:` Companies connection string:

```bash
export ASDAMIR_ENCRYPTION_KEY='<Security:EncryptionKey>'
framework secrets encrypt --value 'the-plaintext'      # prints: v2:....
echo 'the-plaintext' | framework secrets encrypt        # or pipe it on stdin
```

The output uses the exact format the runtime expects (v2 AES-GCM), so it decrypts in-app with no further
steps. There is intentionally **no** `decrypt` command — the tool never prints a stored secret back.

---

## Rotating `Jwt:Key`

`Jwt:Key` is **shared** across AppManagement and every managed app Gateway (so a token issued by
AppManagement validates at any app). Rotation is therefore a **coordinated** change:

1. Roll the new `Jwt__Key` to **all** tiers — AppManagement.Api **and** every managed app's Gateway — in
   the same window. If they diverge, cross-tier token validation fails until they match.
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
