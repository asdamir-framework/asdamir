# Asdamir.Tools

Command-line tooling for the **Asdamir** framework (.NET 10). Command name: `asdamir`.

- **Scaffolding** — generate entities, DTOs, repositories, services, controllers, tests, migrations, full apps, modules and MAUI mobile shells following the framework's audited conventions
- **audit-lint** — static-analysis rules (sync-over-async, silent failures, leaked API surface, unsafe defaults…) that fail CI on violations

## Usage

```bash
# From source
dotnet run --project src/Asdamir.Tools -- audit lint --path src

# As a packaged tool
asdamir entity new Order
asdamir audit lint --path src
```

## Documentation

Full guide: **[CLI documentation](https://github.com/asdamir-framework/asdamir/tree/main/docs/cli.md)**.

## License

LGPL-3.0 — see the bundled `LICENSE` file.
