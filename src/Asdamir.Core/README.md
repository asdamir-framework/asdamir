# Asdamir.Core

Core foundation for the **Asdamir** application framework (.NET 10).

Provides the building blocks the rest of the framework composes on:

- **Models, DTOs & contracts** — shared types and service/repository interfaces
- **Module system** — self-registering `IModule`s with a managed lifecycle
- **Multi-tenancy** — claims/header tenant resolution + ambient `ITenantContext`
- **Error handling** — `Result`, RFC-7807 ProblemDetails mapping, multi-language error translation, dead-letter queue, correlation IDs
- **Validation** — FluentValidation integration, custom attributes (phone via libphonenumber), business-rule engine
- **Security primitives** — JWT (`IJwtService`), AES-GCM encryption (`IEncryptionService`, PBKDF2 key derivation)
- **Observability** — Serilog wiring (console/file/SQL sinks), Polly resilience

## Install

```bash
dotnet add package Asdamir.Core
```

```csharp
builder.Services.AddFramework(builder.Configuration);
app.UseFramework();
```

## Documentation

Full guides: **[Asdamir documentation](https://github.com/asdamir-framework/asdamir/tree/main/docs)** — see *Fundamentals* for each building block.

## License

MIT
