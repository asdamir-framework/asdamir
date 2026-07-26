# Observability

**Package:** `Asdamir.Core` · **Namespace:** `Asdamir.Core.ErrorHandling`

## Introduction

Asdamir ships structured logging and request correlation so a single user action can be traced across the UI, the API and downstream calls.

## Structured logging (Serilog)

Serilog is wired by the framework with console, file and SQL Server sinks plus enrichers (application name, correlation id). Bootstrap it early so even startup failures are captured:

```csharp
// Serilog is configured by the framework's bootstrap; the application name
// comes from the LOG_APPLICATION environment variable (defaults to "Asdamir").
builder.Host.UseSerilog(...);   // see SerilogBootstrap
```

Log levels per namespace are controlled from configuration, e.g.:

```jsonc
"Serilog": { "MinimumLevel": { "Override": { "Asdamir.AdminConsole.Api.Outbox.Worker": "Debug" } } }
```

## Correlation IDs

```csharp
builder.Services.AddCorrelationIdAccessor();
```

A correlation-ID middleware assigns/propagates an id per request and pushes it into the Serilog `LogContext`. `CorrelationIdForwardingHandler` attaches it to outbound `HttpClient` calls, and it appears in ProblemDetails responses — so one id ties the UI request, the API logs and the downstream call together.

```csharp
public sealed class MyService(ICorrelationIdAccessor correlation)
{
    public void Work() => _logger.LogInformation("processing {CorrelationId}", correlation.CorrelationId);
}
```

## Database log sink & app log service

`IAppLogService` writes application log entries to the database (with a Serilog fallback), powering the AdminConsole's log views without scraping files.

## Central error visibility for generated apps (the forward sink)

Every tier writes **three Serilog sinks: Console + File + a central `dbo.AppLog`** — but *how* the central sink is reached differs by tier, because a generated app must never write to the control-plane database directly (the layered / central-data rule):

- **The control plane** (AppManagement's API) owns the log database, so its third sink writes **directly** to `dbo.AppLog`.
- **A generated app's Gateway** cannot touch that database. Its third sink is **`AppLogForwardSink`** (in `Asdamir.Data.Logging`): it forwards **Warning and above** to the control plane's authenticated ingest endpoint (`POST api/admin/applogs/ingest`) over HTTP. The endpoint resolves *which* app the row belongs to **from the Gateway's `app-log` service token — never from the request body** — so one app can only write into its own log slice, and a forged app id is impossible.

The forward sink is built to never get in the app's way:

- **Fail-safe, not fail-open.** If the control plane is down or slow, the sink swallows the error and keeps writing Console + File. A dropped log line is acceptable; a blocked or crashed app is not.
- **No feedback loop.** It never forwards its *own* HTTP-transport log events, so a failing forward can't create a log storm.
- **Batched and bounded.** Events go through an in-memory queue (async `System.Threading.Channels`) and are posted in batches; when the queue is full the oldest entry is dropped (with a one-time warning) rather than blocking the caller.

**Configuration.** The forward is on by default and opt-out per app:

```jsonc
// appsettings.json — generated Gateway
"AppLog": { "ForwardToCentral": true }   // set false to keep logs local only
```

**Free mode (self-contained apps)** has no control plane to forward to, so only **Console + File** are wired — there is no forward sink.

## Distributed tracing & metrics (OpenTelemetry)

Serilog owns **logs**; OpenTelemetry adds **traces** and **metrics**. The API tiers — AppManagement's API and every generated app's Gateway — emit:

- **Traces:** incoming ASP.NET Core requests + outbound `HttpClient` calls (AppManagement → managed-app Gateways, Gateway → AppManagement), so one trace id follows a request across tiers.
- **Metrics:** ASP.NET Core + HttpClient + .NET runtime (GC, thread pool, exceptions).

Everything is exported over **OTLP** to any collector (OpenTelemetry Collector, Jaeger, Tempo, Prometheus via the collector, Grafana, Azure Monitor, …).

### Enabling it (opt-in)

Tracing/metrics turn on **only when an OTLP endpoint is configured**, so local/dev/test runs without a collector stay quiet:

```jsonc
// appsettings.json (or env)
"OpenTelemetry": {
  "ServiceName": "my-app-gateway",        // optional; defaults to the assembly/project name
  "Otlp": { "Endpoint": "http://otel-collector:4317" }
}
```

The standard `OTEL_EXPORTER_OTLP_ENDPOINT` environment variable is honoured too (handy for k8s sidecar/collector injection) — set either one. Empty/unset = OpenTelemetry is not registered (Serilog logging is unaffected).

### What's wired

`AddOpenTelemetry().WithTracing(…).WithMetrics(…)` with `AddAspNetCoreInstrumentation()`, `AddHttpClientInstrumentation()`, `AddSqlClientInstrumentation()` (traces), `AddRuntimeInstrumentation()` (metrics) and `AddOtlpExporter()`. The resource carries the service name + assembly version.

**SQL / Dapper spans:** the API tiers add `AddSqlClientInstrumentation()`. Dapper executes through `Microsoft.Data.SqlClient`, so every query/stored-proc call becomes a child **DB span** (`db.system=mssql`, `db.name`, duration, error status) nested under the request + correlated to the rest of the trace — you see exactly which DB calls a request made and how long each took. The SQL **statement text is NOT captured by default** (avoids logging query text/PII); enable it deliberately with `AddSqlClientInstrumentation(o => o.SetDbStatementForText = true)` if you want the command text on spans.

The **UI tiers** (AppManagement's AdminConsole + generated Blazor Servers) are instrumented too, under the same opt-in switch. There the win is **HttpClient** instrumentation: every call from the UI to the API/Gateway joins the same trace, so a UI action links end-to-end to its API server span and DB. (A Blazor Server's interactive circuit isn't request-per-action, so AspNetCore spans there are sparser than on the API tiers — the HttpClient hop is the useful signal.)

## See also

- [Error Handling](error-handling.md) · [Audit Logging](audit-logging.md)
