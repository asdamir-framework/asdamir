---
name: asdamir-secret-rotation
description: Use when rotating a secret — the at-rest encryption key (Security:EncryptionKey), the JWT signing key (Jwt:Key), or a managed app's client secret. The EncryptionKey case is destructive without a re-encryption pass; this drives the safe procedure. Trigger on "rotate key/secret", "change the encryption key", "key compromised", "new signing key".
---

# Asdamir secret / key rotation

Deep reference: `docs/secret-rotation.md` (full runbook + secret inventory), `docs/cli.md` → `secrets`,
memory `2026-06-14-secrets-rotation-and-health-probes`.

**Security rule:** never write a real key to an inspectable file. Pass keys via **environment variables**
(below), not flags (flags leak into shell history / process args).

## Security:EncryptionKey (at-rest encryption) — re-encrypt FIRST
Rotating this key without re-encrypting bricks every encrypted value (`Apps.EncryptedClientSecret`,
`AppConfigurations` rows with `IsEncrypted=1`). Use `framework secrets rotate-key`:
```bash
# 0) BACK UP AsdamirVault first.
export ASDAMIR_OLD_ENCRYPTION_KEY='<current key>'
export ASDAMIR_NEW_ENCRYPTION_KEY='<new 32+ char key>'   # + ASDAMIR_OLD/NEW_ENCRYPTION_SALT if you use a salt
# 1) DRY-RUN (verifies + counts, writes nothing):
framework secrets rotate-key --server <sql> --database AsdamirVault --user <login> --password <pwd>
# 2) APPLY (one transaction, idempotent, aborts+rolls back on a key that can't decrypt):
framework secrets rotate-key --server <sql> --database AsdamirVault --user <login> --password <pwd> --apply
# 3) Deploy the NEW key as Security:EncryptionKey, restart AppManagement. Token cache evicts on 401.
```
Not covered by the tool (do manually): `appsettings.json` Companies connection strings (`v2:`-prefixed —
re-encrypt with `framework secrets encrypt`) and each managed app's own `IsEncrypted=1` config (run
`rotate-key` against that app's DB too).

## Jwt:Key (token signing) — coordinated
`Jwt:Key` is **shared** by AppManagement and every managed app Gateway. Roll the new `Jwt__Key` to **all**
tiers in the same window and restart. Live access tokens stop validating; clients silently exchange their
refresh token (stored as a SHA-256 hash, so it survives) for a new one. To force every session to
re-login (suspected compromise), also clear `dbo.RefreshTokens`. No data to re-encrypt.

## A managed app's ClientSecret — per-app
AdminConsole → the app → **Rotate secret** (or `PUT /api/admin/apps/{appId}` with `RotateClientSecret`);
AppManagement re-encrypts + stores it, and its token cache for that app evicts on the next 401. Then
update the **managed app's own** configured secret to match and restart its Gateway.

## Seed an encrypted config value
```bash
export ASDAMIR_ENCRYPTION_KEY='<Security:EncryptionKey>'
framework secrets encrypt --value 'the-plaintext'      # prints v2:…  (no `decrypt` command, by design)
```

## DON'T
- **Don't switch `Security:EncryptionKey` before running `rotate-key --apply`** — existing ciphertext
  becomes unreadable.
- **Don't pass keys as flags / commit them** — use the `ASDAMIR_*` env vars; never write them to a file.
- **Don't diverge `Jwt:Key`** across tiers — roll all of them together.
