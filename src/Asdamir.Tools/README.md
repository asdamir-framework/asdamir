# Asdamir.Tools

Command-line tooling for the **Asdamir** framework (.NET 10). Command name: `asdamir`.

- **Scaffolding** — generate entities, DTOs, repositories, services, controllers, tests, migrations, full apps, modules and MAUI mobile shells following the framework's audited conventions
- **audit-lint** — static-analysis rules (sync-over-async, silent failures, leaked API surface, unsafe defaults…) that fail CI on violations
- **audit verify-archive** — verify a folded agent-audit segment **offline**: no network, no database, no control plane, no licence. The one command here that is not a build gate — it is what a third party runs to check an exported ledger archive without taking the vendor's word for it

## Usage

```bash
# From source
dotnet run --project src/Asdamir.Tools -- audit lint --path src

# As a packaged tool
asdamir entity new Order
asdamir audit lint --path src

# verify an exported agent-audit archive — WITHOUT --expected-digest the result is
# INTERNALLY_CONSISTENT (UNANCHORED), never VERIFIED
asdamir audit verify-archive --path ./segment.zip --expected-digest <FoldSegmentDigest>
```

Exit codes for `verify-archive` are a contract a third party's audit script may rely on: `0`–`4` are claims
about an archive (`VERIFIED` / `INTERNALLY_CONSISTENT` / `DIGEST_MISMATCH` / `BROKEN` / `FORMAT_ERROR`) and
**nothing else may occupy them** — every other invocation, `--help` included, exits `64`. See the CLI docs.

## Documentation

Full guide: **[CLI documentation](https://github.com/asdamir-framework/asdamir/tree/main/docs/cli.md)**.

## License

LGPL-3.0 — see the bundled `LICENSE` file.
