# Modules

**Package:** `Asdamir.Core` · **Namespace:** `Asdamir.Core.Contracts`, `Asdamir.Core.Modules`

## Introduction

A *module* packages a feature as a self-registering, dynamically discoverable unit with a managed lifecycle. Modules let you compose an application from independent capabilities without hand-wiring every service in `Program.cs`.

## The `IModule` contract

```csharp
public interface IModule
{
    string Name { get; }
    string Version { get; }
    IEnumerable<string> Dependencies => Array.Empty<string>();

    void ConfigureServices(IServiceCollection services, IConfiguration config);
    void Configure(IApplicationBuilder app, IHostEnvironment env);
    Task InitializeAsync(IServiceProvider services, CancellationToken ct = default) => Task.CompletedTask;
}
```

Lifecycle, in order:

1. **`ConfigureServices`** — register services with the DI container (called before the provider is built).
2. **`Configure`** — add middleware / map endpoints (called after the provider is built).
3. **`InitializeAsync`** — async startup work (migrations, cache warm-up, connectivity checks). The default is a no-op.

`Dependencies` declares other module names that must load first; the loader resolves the order and rejects cycles.

## Registration

```csharp
builder.Services.AddModuleSystem();
```

`AddModuleSystem` registers the `ModuleLoader`, which discovers `IModule` implementations and runs their lifecycle. Implement a module like this:

```csharp
public sealed class TelemetryModule : IModule
{
    public string Name => "Telemetry";
    public string Version => "1.0.0";

    public void ConfigureServices(IServiceCollection services, IConfiguration config)
        => services.AddSingleton<IMetricsSink, SqlMetricsSink>();

    public void Configure(IApplicationBuilder app, IHostEnvironment env) { }
}
```

## Scaffolding a module

The CLI generates a ready-to-fill module project (abstraction, options, service, DI extension, README):

```bash
dotnet run --project src/Asdamir.Tools -- module new Telemetry
```

See the [CLI guide](../cli.md).

## See also

- [Getting Started](../getting-started.md)
- [Dependency configuration & feature flags](configuration.md)
