# Background Runs

**Package:** `Asdamir.Core` (abstraction) · `Asdamir.Data` (implementation) · **Namespace:** `Asdamir.Core.BackgroundRuns` / `Asdamir.Data.BackgroundRuns`

## Introduction

Some operations are too heavy to run inside an HTTP request — a 100k×100k reconciliation, a bulk import,
a large export. The **background-run primitive** gives the API/Gateway tier a reusable facility to
**trigger → run in the background → query status/progress**: a controller enqueues a run and returns its
id immediately; callers poll for lifecycle and progress.

The framework manages the run **lifecycle** and **persistence**; the job's **content** is app-defined.
This means an existing engine — e.g. an `IReconciliationEngine.RunAsync(...)` — is **wrapped without any
signature change**; writing the wrapping handler is the application's job.

This is distinct from [Background Jobs](background-jobs.md) (Hangfire — recurring/scheduled fire-and-forget
work) and the [Transactional Outbox](outbox.md) (reliable mail/SMS). Reach for a background run when a
**caller needs to trigger a one-off long operation and watch its progress**.

## Why not Hangfire?

A generated Gateway registers **no Hangfire server** (Hangfire lives in AppManagement's job tier). Hangfire
also has no first-class per-run *progress query* against the app's own schema. So the primitive ships its
**own** runner — a `BackgroundService` + an in-process channel (no external broker) — plus DB-persisted run
state you can query. It mirrors the choice `AppManagement.Api` already makes (a `BackgroundService`, not a
Hangfire server).

## Registration

One line in the Gateway `Program.cs`, then one handler per job type:

```csharp
builder.Services.AddDataAccess(connString);   // IDbConnectionFactory
builder.Services.AddMultiTenancy();           // ITenantContext (runs are tenant-scoped)

builder.Services.AddBackgroundRuns(builder.Configuration);   // service + runner + restart-recovery + store
builder.Services.AddBackgroundJob<ReconciliationJobHandler>(); // one per JobType
```

`AddBackgroundRuns` picks the **Dapper** store or the **InMemory** store from `Persistence:UseInMemory`,
registers the hosted runner and the startup restart-recovery service, and binds `BackgroundRunOptions` from
the `BackgroundRuns` configuration section (DB-backed `AppConfigurations`, never hardcoded).

## The API surface

```csharp
public interface IBackgroundRunService
{
    Task<Guid> EnqueueAsync(BackgroundRunRequest req, CancellationToken ct = default);
    Task<BackgroundRunStatus?> GetStatusAsync(Guid runId, CancellationToken ct = default);
}

public sealed record BackgroundRunRequest(
    string JobType, string? Payload = null, string? DedupKey = null, bool AllowConcurrent = false);

public sealed record BackgroundRunStatus(
    Guid RunId, string JobType, BackgroundRunState State,
    int? ProgressCompleted, int? ProgressTotal, string? ResultRef, string? ErrorSummary,
    DateTime CreatedAtUtc, DateTime? StartedAtUtc, DateTime? CompletedAtUtc);

public enum BackgroundRunState { Pending, Running, Completed, Failed, Interrupted }

public interface IBackgroundJobHandler   // the app-supplied job BODY
{
    string JobType { get; }
    Task<string?> ExecuteAsync(BackgroundRunContext context);   // one context, not loose params
}

public sealed record BackgroundRunContext(   // everything the runner knows about the run
    Guid RunId, string TenantId, string? Payload,
    IProgressReporter Progress, CancellationToken CancellationToken);
```

### Run context

The handler receives ONE `BackgroundRunContext` rather than loose parameters, so future fields extend the
context without another signature break:

| Field | Meaning |
| ----- | ------- |
| `RunId` | The store record's run id — **identical** to the value `EnqueueAsync` returned and `GetStatusAsync(runId).RunId` reports. |
| `TenantId` | The run's tenant; the runner has already re-established ambient tenant scope from it. |
| `Payload` | The opaque input from `BackgroundRunRequest.Payload` (typically JSON), or `null`. |
| `Progress` | The cheap, throttled progress sink (call `Report(done, total)` per unit of work). |
| `CancellationToken` | Cooperative cancellation for shutdown / abort — honour it. |

**`RunId` is the two-way back-link.** Because `context.RunId` is the same id the caller holds, the handler
can persist it on its own business record (e.g. a domain "run" row) so traceability goes **both ways** — the
framework's `BackgroundRuns` row points at nothing app-specific, but the app row points back at the run,
closing the audit / maker-checker loop. A forward-reference-only design (before the context) could not do
this: the handler never saw its own RunId.

A controller triggers and polls:

```csharp
[HttpPost("reconciliations")]
public async Task<IActionResult> Start([FromBody] StartDto dto, CancellationToken ct)
{
    var runId = await _runs.EnqueueAsync(
        new BackgroundRunRequest("reconciliation", Payload: JsonSerializer.Serialize(dto), DedupKey: dto.Period), ct);
    return Accepted(new { runId });
}

[HttpGet("reconciliations/{runId:guid}")]
public async Task<IActionResult> Status(Guid runId, CancellationToken ct)
    => await _runs.GetStatusAsync(runId, ct) is { } s ? Ok(s) : NotFound();
```

## Lifecycle & state machine

```
Pending ──▶ Running ──▶ Completed
                  └────▶ Failed
(Pending|Running) ──▶ Interrupted   (restart-recovery only)
```

Transitions are **guarded and WHERE-scoped** (mirroring the AppManagement proc convention: `SET NOCOUNT ON`
+ `SELECT @@ROWCOUNT`) — an illegal transition affects **0 rows** and returns `false`. The app handler never
sets state directly: it returns (optionally a `ResultRef`) on success, or **throws** on failure — a thrown
exception is what moves the run to `Failed` with the error recorded. Honour the supplied `CancellationToken`.

## Progress (throttled / batched)

The handler reports via a cheap `IProgressReporter.Report(completed, total)` that it can call **per row**.
The reporter **coalesces in memory** and flushes to the store at most:

- once per `ProgressFlushIntervalMs` (default **750 ms**), **or**
- once per `ProgressFlushPercentStep` advance of the total (default **5%**, so short jobs still move).

So 100k `Report` calls become a handful of DB writes, not 100k. Progress writes are advisory
(fire-and-forget; a flush failure never fails the run), and the **final** value is force-flushed just before
a terminal transition so no progress is lost.

## Concurrency policy

With the default (`AllowConcurrent = false`), enqueuing a run whose (`JobType` + `DedupKey`) already has a
**Pending/Running** run for the tenant does **not** start a duplicate — it returns the **existing** RunId.
Set `AllowConcurrent = true` to always start a fresh run. The runner also bounds parallelism with
`BackgroundRunOptions.MaxConcurrency` (default 2).

## Persistence, tenancy & restart-recovery

Runs are **persisted** — in-memory-only is forbidden in production. The `dbo.BackgroundRuns` table lives in
the **app's OWN business database** (this is app-operational state, not central management data, per the
CENTRAL rule — it is not in AsdamirVault). Every read/write is **tenant-scoped**: tenant A's runs are
invisible to tenant B.

On startup, a restart-recovery `IHostedService` flips every run left `Pending`/`Running` by a prior process
that died to **`Interrupted`** — so **no ghost "Running" survives a restart**. (Verified by an integration
test: seed a `Running` row, run recovery, assert it becomes `Interrupted`.)

## HA — known limitation

The primitive assumes a **single node**. Multi-node leader election / distributed run ownership is **not
solved here**: startup recovery would wrongly mark a peer's live runs `Interrupted`, and the in-process queue
is per-process. A scale-out deployment needs a shared/distributed run owner — the same caveat as the
in-memory session store. (`OwnerToken` is recorded on a Running row so this can be built on later.)

## Wrapping an existing engine (the hook)

A handler wraps an existing `RunAsync`-style engine **without changing its signature** — the framework owns
the lifecycle, the engine owns the work:

```csharp
public sealed class ReconciliationJobHandler(IReconciliationEngine engine) : IBackgroundJobHandler
{
    public string JobType => "reconciliation";

    public async Task<string?> ExecuteAsync(BackgroundRunContext context)
    {
        var req = JsonSerializer.Deserialize<ReconRequest>(context.Payload!)!;     // decode app input
        req.BackgroundRunId = context.RunId;                                       // back-link: my record -> the run row
        var result = await engine.RunAsync(req, onProgress: (done, total) =>       // engine signature UNCHANGED
            context.Progress.Report(done, total), context.CancellationToken);      // just forward progress
        return result.ReportId.ToString();                                         // stored as ResultRef
    }
}
```

## Configuration

| Key | Default | Purpose |
| --- | ------- | ------- |
| `BackgroundRuns:MaxConcurrency` | `2` | Max runs executed at once by the runner |
| `BackgroundRuns:ProgressFlushIntervalMs` | `750` | Min gap between progress flushes |
| `BackgroundRuns:ProgressFlushPercentStep` | `5` | Also flush every this-percent advance |
| `BackgroundRuns:NodeId` | `{Machine}#{Pid}` | Owner token recorded on a Running row |

Bind these from `AppConfigurations` (see [Configuration](configuration.md)); do not hardcode them.
