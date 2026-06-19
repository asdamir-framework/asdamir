---
name: asdamir-observability
description: Use when wiring OpenTelemetry tracing/metrics, health probes, or adding an OTel package to a tier (AppManagement or a generated app). Trigger on "add tracing/metrics", "OpenTelemetry", "OTLP", "health check / liveness / readiness", "instrument SQL/Dapper", "observability".
---

# Asdamir observability

Serilog owns logs (Console/File/DB); OpenTelemetry adds traces + metrics; health probes gate traffic.
Deep reference: `docs/fundamentals/observability.md`, memory `2026-06-14-opentelemetry-api-tiers`,
`2026-06-15-opentelemetry-ui-tiers`, `2026-06-15-publish-nuget-fix-and-sql-otel`.

## OpenTelemetry (opt-in via config)
Registered **only when an OTLP endpoint is set** (`OpenTelemetry:Otlp:Endpoint` or the standard
`OTEL_EXPORTER_OTLP_ENDPOINT`), so dev/test stay quiet. The block (mirror an existing Program):
```csharp
var otlp = builder.Configuration["OpenTelemetry:Otlp:Endpoint"]
        ?? builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];
if (!string.IsNullOrWhiteSpace(otlp))
{
    var uri = new Uri(otlp);
    builder.Services.AddOpenTelemetry()
        .ConfigureResource(r => r.AddService(serviceName: "<name>", serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString()))
        .WithTracing(t => t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation()
            .AddSqlClientInstrumentation()              // API TIERS ONLY (Dapper runs on SqlClient)
            .AddOtlpExporter(o => o.Endpoint = uri))
        .WithMetrics(m => m.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation().AddOtlpExporter(o => o.Endpoint = uri));
}
```
- **API tiers** (AppManagement.Api, generated Gateway) get `AddSqlClientInstrumentation()`; **UI tiers**
  (AdminConsole, generated Server) don't (no SQL). SQL **statement text is off by default** (PII) — opt in
  with `SetDbStatementForText = true` only if you must.
- **Package versions** are pinned centrally: Hosting/OTLP `1.16.0`, Instrumentation.AspNetCore `1.15.2`,
  Http/Runtime/SqlClient `1.15.x`. Adding a new OTel package: it must resolve `OpenTelemetry.Api` to
  `1.16.0` (advisory-free) — verify with a throwaway restore, because **NU1902/NU1903 fail the build**
  (TreatWarningsAsErrors).

## Health probes
- **API tiers:** `MapHealthChecks("/health")` + `/health/live` (`Predicate = _ => false`) + `/health/ready`
  (a "ready"-tagged DB `SELECT 1` check, **Dapper mode only**; in-memory skips it so tests stay green).
- **UI tiers:** liveness-only — `/health`, `/health/live`, `/health/ready` all with no gating check (a UI
  should stay up to show an error even when the backend is down).
- k8s: liveness probe → `…/health/live`, readiness → `…/health/ready`.

## DON'T
- **Don't add SqlClient instrumentation to a UI tier** (it has no DB).
- **Don't register OTel unconditionally** — gate on the OTLP endpoint, or dev/test spew export errors.
- **Don't pick an OTel version that drags `OpenTelemetry.Api` below 1.16.0** — it trips NU1902 (build fails).
- **Don't gate UI readiness on the backend** — UI `/health/ready` carries no check by design.
