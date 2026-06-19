# Encryption

**Package:** `Asdamir.Core` · **Namespace:** `Asdamir.Core.Services`, `Asdamir.Core.Contracts`

## Introduction

Asdamir provides authenticated symmetric encryption for protecting sensitive values at rest (configuration secrets, stored tokens, PII columns). It uses modern **AES-GCM** with a key derived from your master key via **PBKDF2**, and keeps a read-only legacy path so existing data can be migrated transparently.

## `IEncryptionService`

```csharp
public interface IEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
    bool   IsEncrypted(string text);
}
```

```csharp
public sealed class SecretStore(IEncryptionService crypto)
{
    public string Protect(string value)   => crypto.Encrypt(value);
    public string Reveal(string protec)    => crypto.Decrypt(protec);
    public bool   AlreadyProtected(string v) => crypto.IsEncrypted(v);
}
```

## How it works

- **v2 (default):** `AES-GCM` — authenticated encryption with a random per-message nonce and an auth tag, so tampering is detected on decrypt.
- **Legacy (read path):** `AES-CBC` values written by older versions still decrypt, so you can re-encrypt on next write during a migration.
- **Key derivation:** the symmetric key is derived from `Security:EncryptionKey` with `PBKDF2` (`Rfc2898DeriveBytes.Pbkdf2`) — the configured key is a passphrase, not the raw AES key.

`IsEncrypted` lets call sites detect already-protected values (idempotent encrypt-on-save).

## Configuration

```jsonc
// appsettings.json holds NO secret. Supply the key out-of-band:
//   dev : dotnet user-secrets set "Security:EncryptionKey" "<32+ char>"
//   prod: environment variable  Security__EncryptionKey
```

The key must be **≥ 32 characters**. Rotating it is a data-migration event: values encrypted under the old key cannot be read under a new one, so decrypt-then-re-encrypt during rotation — use `framework secrets rotate-key`. See the **[Secret Management & Key Rotation runbook](../secret-rotation.md)**.

## Hashing

`CryptographyService` provides hashing helpers used for non-reversible needs (e.g. the management app hashes refresh tokens with SHA-256 before storage — the raw token is never persisted). Use encryption for values you must read back, hashing for values you only need to compare.

## Related: Data Protection (web)

For browser-facing token protection (email/refresh tokens in cookies), `Asdamir.Web` exposes ASP.NET Core **Data Protection** via `DataProtectionService` with keyed purposes — see [Web Security](../web-security.md).

## See also

- [Authentication](authentication.md) · [Web Security](../web-security.md) · [Getting Started → secrets](../getting-started.md#configuration-secrets)
