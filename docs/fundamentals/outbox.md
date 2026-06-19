# Transactional Outbox

**Package:** `Asdamir.Data` · **Namespace:** `Asdamir.Data.Outbox`

## Introduction

The **transactional outbox** delivers messages (email, SMS) reliably. Instead of calling an SMTP/SMS gateway inline — where a crash or a slow gateway loses the message or blocks the request — you write the message to an outbox table in the same transaction as your business change. A background worker then claims and dispatches it, with retries and dead-lettering.

This gives **at-least-once delivery** decoupled from the request path.

## How it works

```
enqueue row ──▶ Outbox_ClaimBatch ──▶ IOutboxDispatcher (by MessageType)
                     ▲                         │
                     │                  success │ transient fail │ permanent fail
              reclaim stale locks       MarkDone │ MarkRetry      │ MarkDead
```

- **Claim:** the worker claims a batch (`Outbox_ClaimBatch`) and stamps a worker lock.
- **Dispatch:** each row is routed to the `IOutboxDispatcher` for its `MessageType` (1 = SMS, 2 = Email).
- **Outcome:** success → `MarkDone`; transient failure → `MarkRetry` with **exponential backoff + jitter**; permanent failure (or attempts exhausted) → `MarkDead`.
- **Self-healing:** locks abandoned by a crashed worker are reclaimed after a stale threshold.

## Dispatchers

A dispatcher implements `IOutboxDispatcher`:

```csharp
public interface IOutboxDispatcher
{
    byte MessageType { get; }                                   // 1 = SMS, 2 = Email
    Task DispatchAsync(ClaimedOutboxMessage message, CancellationToken ct);
}
```

- `MailDispatcher` (MailKit) — multi-To/Cc/Bcc, ReplyTo, per-message From, HTML and attachments.
- `SmsDispatcher` — a stub you replace with your provider; a Twilio dispatcher is provided in the management app.

Throw a `PermanentDispatchException` to skip retries and dead-letter immediately (e.g. an unparseable address); throw anything else to schedule a retry.

## Worker options

The worker is bound from `Outbox:Worker`:

```jsonc
"Outbox": {
  "Worker": {
    "Enabled": true,
    "BatchSize": 25,
    "PollInterval": "00:00:01",
    "IdleBackoff": "00:00:05",
    "BackoffBaseSeconds": 30,
    "BackoffMaxSeconds": 3600,
    "JitterPercent": 0.20,
    "ReclaimStaleThresholdMinutes": 5
  }
}
```

SMTP transport is configured under `Smtp`; SMS provider selection under `Sms` (`Stub` | `Twilio`) with credentials under `Twilio`.

## Fail-closed by design

An unconfigured gateway is treated as a **permanent failure**, not a silent success — the row surfaces in the AdminConsole "Failed" view instead of pretending it was sent.

## See also

- [Background Jobs](background-jobs.md) · [AdminConsole](../ARCHITECTURE.md)
