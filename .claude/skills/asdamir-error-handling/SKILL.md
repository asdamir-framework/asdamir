---
name: asdamir-error-handling
description: Use when wiring exception handling / ProblemDetails in an API or Gateway, or handling errors (Result, error translation, correlation IDs). Every REST/API tier MUST register the global exception handler — this is a mandatory rule. Trigger on "error handling", "ProblemDetails", "exception middleware", "new Program.cs for an API/Gateway", "unhandled exception".
---

# Asdamir error handling

Deep reference: `docs/fundamentals/error-handling.md`, `CLAUDE.md` → "Error handling is mandatory".

## MANDATORY in every API / Gateway `Program.cs`
```csharp
builder.Services.AddGlobalExceptionHandling();   // registers IProblemDetailsMapper (SINGLETON)
...
app.UseHttpsRedirection();
app.UseGlobalExceptionHandling();                // EARLY: right after UseHttpsRedirection, BEFORE auth
app.UseAuthentication();
app.UseAuthorization();
```
- It turns unhandled exceptions into **RFC-7807 ProblemDetails** + logs them via Serilog.
- `IProblemDetailsMapper` is registered **singleton** (the middleware ctor-injects it — a scoped
  registration fails at startup). Don't change the lifetime.
- Serilog writes **three sinks: Console + File + DB (`dbo.AppLog`)**; the DB sink persists Warning+ with
  the selected app's `AppId` (+ Source/ErrorKey/CaughtBy) so ErrorMonitoring shows which app failed.
- The `framework new app` scaffold already emits this in `GatewayProgram.sbn` — keep it.

## Blazor UI hosts are different
UI hosts (AdminConsole, generated Server) use the **Blazor error boundary + `app.UseExceptionHandler("/Error")`**,
**not** the global middleware (the middleware returns ProblemDetails JSON, wrong for rendered pages).

## In code
- Prefer **`Result`**-style returns over throwing for expected failures.
- `IErrorTranslationService` provides multi-language error messages; correlation IDs propagate through
  logs + the ProblemDetails response (see the `asdamir-observability` skill).
- The umbrella `AddFramework()` / `UseFramework()` wires the common set at the composition root.

## DON'T
- **Don't omit `AddGlobalExceptionHandling()` + `UseGlobalExceptionHandling()`** in an API/Gateway — it's required.
- **Don't make `IProblemDetailsMapper` scoped** — startup will fail.
- **Don't add the API middleware to a Blazor UI host** — use the error boundary + `UseExceptionHandler` there.
- **Don't swallow exceptions** (empty catch) — audit-lint flags it.
