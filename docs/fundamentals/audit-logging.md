# Audit Logging

**Packages:** `Asdamir.Core` (contracts, `AuditEntry`), `Asdamir.Web` (audit middleware)

## Introduction

Audit logging records **security-relevant actions** — who did what, to which entity, when, and from where — into a durable audit trail, separate from operational logs. It answers "who changed this order?" months later.

## The audit entry

`IAuditService.LogAsync(AuditEntry entry, …)` persists an immutable record:

```csharp
await audit.LogAsync(new AuditEntry(
    Timestamp:   DateTimeOffset.UtcNow,
    Action:      "Update",
    Entity:      "Order",
    EntityId:    order.Id.ToString(),
    UserId:      currentUser.Id,
    UserName:    currentUser.Name,
    TenantId:    tenant.TenantId,
    Ip:          httpContext.Connection.RemoteIpAddress?.ToString(),
    UserAgent:   httpContext.Request.Headers.UserAgent,
    OldValuesJson: JsonSerializer.Serialize(before),
    NewValuesJson: JsonSerializer.Serialize(after),
    ExtraJson:   null));
```

## Automatic audit middleware

`Asdamir.Web.Security` provides middleware that captures audited requests automatically:

```csharp
app.UseAuditLogging();
```

Configure it via `AuditLoggingOptions`. The middleware caps the request body it reads (it does not buffer unbounded streams) and records authorization decisions (grant/deny) for security review.

## PII safety

Audit records flow through the framework's **recipient masking** and **HTML sanitization** helpers (`Asdamir.Core.Sanitization`) so emails/phone numbers and markup are stored safely.

## Viewing the trail

The AdminConsole exposes an **Audit Trail** page with filtering by user, app, action and time window.

## See also

- [Authorization](authorization.md) · [Observability](observability.md) · [Web Security](../web-security.md)
