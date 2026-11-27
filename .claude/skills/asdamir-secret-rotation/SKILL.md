---
name: asdamir-secret-rotation
description: Use when rotating a secret — the at-rest encryption key (Security:EncryptionKey), the JWT signing key (Jwt:SigningKeyPem, or a shared/free-mode Jwt:Key), an app's service key, or a managed app's client secret. The EncryptionKey case is destructive without a re-encryption pass; this drives the safe procedure. Trigger on "rotate key/secret", "change the encryption key", "key compromised", "new signing key".
---

# Asdamir secret / key rotation

Deep reference: `docs/secret-rotation.md` (full runbook + secret inventory), `docs/cli.md` → `secrets`,
memory `2026-06-14-secrets-rotation-and-health-probes`.

**Security rule:** never write a real key to an inspectable file. Pass keys via **environment variables**
(below), not flags (flags leak into shell history / process args).

## Security:EncryptionKey (at-rest encryption) — re-encrypt FIRST
Rotating this key without re-encrypting bricks every encrypted value (`Apps.EncryptedClientSecret`,
`AppConfigurations` rows with `IsEncrypted=1`). Use `asdamir secrets rotate-key`:
```bash
# 0) BACK UP AsdamirVault first.
export ASDAMIR_OLD_ENCRYPTION_KEY='<current key>'
export ASDAMIR_NEW_ENCRYPTION_KEY='<new 32+ char key>'   # + ASDAMIR_OLD/NEW_ENCRYPTION_SALT if you use a salt
# 1) DRY-RUN (verifies + counts, writes nothing):
asdamir secrets rotate-key --server <sql> --database AsdamirVault --user <login> --password <pwd>
# 2) APPLY (one transaction, idempotent, aborts+rolls back on a key that can't decrypt):
asdamir secrets rotate-key --server <sql> --database AsdamirVault --user <login> --password <pwd> --apply
# 3) Deploy the NEW key as Security:EncryptionKey, restart AppManagement. Token cache evicts on 401.
```
Not covered by the tool (do manually): `appsettings.json` Companies connection strings (`v2:`-prefixed —
re-encrypt with `asdamir secrets encrypt`) and each managed app's own `IsEncrypted=1` config (run
`rotate-key` against that app's DB too).

## Jwt:SigningKeyPem (ES256, 2.0 line) — issuer only
`asdamir secrets new-signing-key --out <dir>` generates the pair. The private key goes to the ISSUER's
`Jwt:SigningKeyPem`; Gateways hold no key and follow `/.well-known/jwks.json` (`Jwt:JwksUrl`).
**Planned rotation, no failed request:** (1) put the NEW public key in `Jwt:AdditionalPublicKeysPem`,
restart, wait ≥1 h; (2) switch `Jwt:SigningKeyPem` to the new key and list the OLD public key there;
(3) after the access-token lifetime, remove the old one. **After a leak:** new key, nothing in
`AdditionalPublicKeysPem`, clear `dbo.RefreshTokens`.
**Moving a deployment off the shared `Jwt:Key`** is three ordered steps with a gate in the middle —
(1) generate the NEW pair, every Gateway accepts both (`Jwt:PublicKeyPem` = the new PUBLIC key +
`Jwt:Key` + `Jwt:AcceptLegacySymmetric=true`, plus its service key) → **gate:**
`asdamir secrets check-gateways --key-file <new jwt-signing-key.pem> --connection <AsdamirVault>` must exit
`0` (every app `ACCEPTED`) → (2) AppManagement switches to that same key (`Jwt:SigningKeyPem`, keeps the
window) → (3) the shared key and the Gateways' `Jwt:PublicKeyPem` are removed (they then follow the JWKS),
re-run the check, discard the old key. Full procedure: `docs/secret-rotation.md`.
- **Pin at step 1, not `Jwt:JwksUrl`:** AppManagement publishes the new key only once it signs with it, so a
  JWKS Gateway cannot trust it before step 2 — `check-gateways` reads it `REFUSED`, correctly.
- `check-gateways` is read-only (one GET per Gateway to `gateway/admin/audittrail?limit=1` with a 2-minute
  user-shaped app token; `403` = trusted). `REFUSED` = the key is not trusted; `INCONCLUSIVE` names what
  else is wrong (issuer/audience/URL/code); `UNREACHABLE` is not a pass; no active app is not a pass.
  `--allow-untrusted-certs` only for dev certificates. Exit `0` all accepted · `1` not ready · `64` usage /
  unreadable vault.

## App:ServiceKeyPem (a managed app's service key) — per-app, no gap
Generate a new pair, then: (1) register BOTH public keys (`PUT api/admin/apps/{appId}/service-key`, two
PEM blocks), (2) set the new private key on the Gateway (`App:ServiceKeyPem`, and
`AgentAudit:ServiceKeyPem` if used) and restart it, (3) register the new public key alone. After a leak,
skip step 1: register only the new key — the old one is refused from the next request.

## Jwt:Key (free-mode app, or a deployment not yet migrated) — coordinated
A free-mode app holds its own `Jwt:Key`. In a not-yet-migrated deployment it is still **shared** by
AppManagement and every Gateway: roll the new `Jwt__Key` to **all** tiers in the same window and restart. Live access tokens stop validating; clients silently exchange their
refresh token (stored as a SHA-256 hash, so it survives) for a new one. To force every session to
re-login (suspected compromise), also clear `dbo.RefreshTokens`. No data to re-encrypt.

## A managed app's ClientSecret — per-app
AdminConsole → the app → **Rotate secret** (or `PUT /api/admin/apps/{appId}` with `RotateClientSecret`);
AppManagement re-encrypts + stores it, and its token cache for that app evicts on the next 401. Then
update the **managed app's own** configured secret to match and restart its Gateway.

## Seed an encrypted config value
```bash
export ASDAMIR_ENCRYPTION_KEY='<Security:EncryptionKey>'
asdamir secrets encrypt --value 'the-plaintext'      # prints v2:…  (no `decrypt` command, by design)
```

## DON'T
- **Don't switch `Security:EncryptionKey` before running `rotate-key --apply`** — existing ciphertext
  becomes unreadable.
- **Don't pass keys as flags / commit them** — use the `ASDAMIR_*` env vars; never write them to a file.
- **Don't diverge a still-shared `Jwt:Key`** across tiers — roll all of them together.
- **Don't reuse the old shared key** after the migration, and don't leave `Jwt:AcceptLegacySymmetric` on.
