---
name: asdamir-background-work
description: Use for deferred/background processing in the API tier — Hangfire jobs (recurring/scheduled/worker) AND the transactional outbox (sending email/SMS). Both run in the API tier and are close kin. Trigger on "background/scheduled/recurring job", "Hangfire", "cron", "worker", "send email/SMS", "outbox", "notification delivery", "queue a message", "Twilio".
---

# Asdamir background work (Hangfire jobs + transactional outbox)

Both live in the **API tier** (it owns DB access) — never the UI. AppManagement reaches a managed app's
background work via orchestration → the app's Gateway. Deep reference:
`docs/fundamentals/background-jobs.md`, `docs/fundamentals/outbox.md`,
memory `project_layered_app_api_runs_hangfire`.

## Hangfire jobs
```csharp
builder.Services.AddCoreHangfireJobs(...);   // server + SqlServer storage, in the API tier
app.UseCoreHangfireApi();
app.UseCoreHangfireDashboard();              // authorize the route in production
recurring.AddOrUpdateRecurring(...);          // via IHangfireJobService / the scheduler
```
- A job's executor must **re-throw on failure** — returning a success-shaped result makes Hangfire mark
  it Succeeded and `AutomaticRetry` never fires (an audited bug). Use `JobExecutor` / `IJobContext`; it
  re-throws when `result.IsSuccess == false` so retry + dashboard counters work.
- **No global `[DisableConcurrentExecution]`** (it collapsed all jobs to one lock). Apply it on a job's
  **own** concrete method only when that job needs exclusivity.

## Transactional outbox (email / SMS)
**One mechanism: `IOutboxDispatcher`** — don't add a parallel send path.
- The abstraction + framework dispatchers live in `Asdamir.Data/Outbox/Dispatch` (`IOutboxDispatcher`,
  MailKit `MailDispatcher`, a dev no-op `SmsDispatcher`). The DB-coupled **`OutboxWorker`** + the real
  **Twilio** dispatcher live in **AppManagement** (they touch DB + provider).
- **Enqueue, don't send inline:** persist via `IOutboxStore.EnqueueEmail…/EnqueueSms…` in the *same unit
  of work* as your business change (that's the "transactional" part). The worker delivers later: polls
  Pending/Retrying, calls the dispatcher, retries with **exponential backoff + jitter**, dead-letters
  after the cap, reclaims stale locks.
- Config via options (`SmtpOptions`/`SmsOptions`/`TwilioOptions`/`OutboxWorkerOptions`) bound from
  `AppConfigurations` (see `asdamir-config-setting`); provider secrets via user-secrets/env, not appsettings.

## DON'T
- **Don't run Hangfire or the outbox worker in a UI host** — API tier only (DB creds live there).
- **Don't let a job swallow failures** — re-throw so retry/dashboard work.
- **Don't add a global `[DisableConcurrentExecution]`**, and don't leave the dashboard unauthorized.
- **Don't send mail/SMS inline** from a request — enqueue to the outbox (transactional + retried).
- **Don't add a second send mechanism**, put provider secrets in appsettings, or log recipient/body unmasked.
