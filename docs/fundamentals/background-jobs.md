# Background Jobs

**Package:** `Asdamir.Data` · **Namespace:** `Asdamir.Data.HangfireJobs`

## Introduction

Asdamir runs background and scheduled work on **Hangfire**, with a thin framework layer that gives jobs a uniform contract, a DI-resolvable executor and a dashboard.

## Registration

```csharp
builder.Services.AddCoreHangfireJobs(builder.Configuration);
...
app.UseCoreHangfireApi();        // JSON monitoring endpoints
app.UseCoreHangfireDashboard();  // Hangfire dashboard (guard it behind auth!)
```

## Writing a job

Implement `IJob` and return a `JobResult` — **return failure, don't swallow it**:

```csharp
public sealed class NightlyReconcileJob : IJob
{
    public string Name => nameof(NightlyReconcileJob);

    public async Task<JobResult> ExecuteAsync(IJobContext ctx, CancellationToken ct = default)
    {
        try
        {
            await ReconcileAsync(ct);
            return JobResult.Success(TimeSpan.FromMinutes(2));
        }
        catch (Exception ex)
        {
            return JobResult.Failure(ex.Message);   // re-thrown so Hangfire retry fires
        }
    }
}
```

> A failing `JobResult` is re-thrown by the executor so Hangfire marks the job failed and applies `AutomaticRetry` — a job that "logs and returns success" hides outages.

## Scheduling

Use the scheduler abstraction to enqueue or schedule recurring jobs by type:

```csharp
public sealed class Bootstrap(IHangfireJobService jobs)
{
    public void Configure() => jobs.AddOrUpdateRecurring<NightlyReconcileJob>("0 2 * * *"); // 02:00 daily
}
```

(Exact scheduling helpers live on the framework's job scheduler / `IHangfireJobService`.)

## Per-app isolation (multi-company)

When several apps share one company's Hangfire schema, configure `HangfireJobsOptions` so each app
stays isolated (see the [Multi-Company design](../design/multi-company-management.md) §5.1):

- **`AppQueue`** (e.g. the app's `Code`) — enqueued and recurring jobs route to this queue and the
  app's worker listens only on it, so one app's job surge can't starve another.
- **`RecurringJobIdPrefix`** — recurring ids become `{prefix}:{jobId}`, so two apps can both define
  e.g. `daily-cleanup` without colliding.

Both default to empty → Hangfire's `default` queue (unchanged single-app behaviour). Each generated
app runs its own Hangfire server; cross-company isolation is automatic (separate company DB =
separate schema).

## Monitoring

The AdminConsole surfaces job state (enqueued/processing/succeeded/failed) and lets operators requeue or delete jobs — it observes only and hosts no workers itself.

## See also

- [Transactional Outbox](outbox.md) — for reliable message delivery, prefer the outbox over ad-hoc jobs
