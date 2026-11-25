# Error Handling

**Package:** `Asdamir.Core` · **Namespace:** `Asdamir.Core.ErrorHandling`

## Introduction

Asdamir favours **explicit, structured errors** over unhandled exceptions:

- A functional `Result` type for expected failures.
- A global exception middleware that converts anything uncaught into an **RFC-7807 ProblemDetails** response.
- Multi-language **error translation** so clients receive a localized, safe message.
- A **dead-letter queue (DLQ)** for failures that must not be lost.

## Registration

```csharp
builder.Services.AddGlobalExceptionHandling();
...
app.UseGlobalExceptionHandling();   // place early so it wraps the whole pipeline
```

This is also included in the `AddFramework()` / `UseFramework()` umbrella.

## ProblemDetails mapping

Uncaught exceptions are mapped to a ProblemDetails body by `IProblemDetailsMapper`. The default mapper:

- maps `DomainException` (and known infrastructure exceptions) to the right status code,
- sets a stable `type` URN and an `Extensions["code"]` error code,
- localizes the `Title` via the error-translation service,
- **never leaks** raw exception messages, stack traces or backend/library versions.

If the response has **already started** when the exception surfaces (a streamed body, flushed headers), no error document can be written any more: the middleware still logs the exception in full and then **rethrows**, so the server aborts the connection rather than appending JSON to a half-sent body.

```csharp
throw new DomainException("orders.not_found", "Order was not found.");
// → 404 ProblemDetails { type, title (localized), extensions.code = "orders.not_found" }
```

## The `Result` type

Use `Result` / `Result<T>` for expected, non-exceptional failures:

```csharp
public async Task<Result<Order>> GetAsync(Guid id)
{
    var order = await _repo.FindAsync(id);
    return order is null
        ? Result<Order>.Fail("orders.not_found")
        : Result<Order>.Ok(order);
}
```

## Error translation

`IErrorTranslationService` resolves an error key + language to a localized message, with fallbacks (requested culture → English → generic). Translations are sourced from the database, so operations can edit them without a redeploy.

## Dead-letter queue

Failures that cannot be retried inline are written through `IDlqWriter` (a file-backed writer ships by default) so they can be inspected and replayed rather than silently dropped.

## Correlation

Every request carries a correlation ID (see [Observability](observability.md)); it flows into logs, ProblemDetails and outbound HTTP calls so a single failure can be traced end-to-end.

An inbound `X-Correlation-Id` header is reused only when it looks like an id: **at most 64 characters** of ASCII letters, digits, `.`, `_` or `-`. Anything else — an oversized value, control characters, a comma-joined multi-value header — is discarded and a new id is generated, because the value is echoed into the response header and every log line. A rejected value is replaced, not sanitised: a "cleaned" id would correlate with nothing upstream.

## See also

- [Validation](validation.md) · [Observability](observability.md) · [Localization](localization.md)
