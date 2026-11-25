# Asdamir.Payments

Payment rails for the [Asdamir](https://www.nuget.org/packages/Asdamir.Core) stack.

`Asdamir.Core` ships the payment **contract** (`IPaymentProvider`, `PaymentProviderOptions`). This package
ships the concrete **rails and plumbing** on top of it, so an app can take end-user payments:

- **Providers** — `PaddlePaymentProvider` (Paddle Billing, Merchant-of-Record; each tenant connects its own
  Paddle account — pass-through, so Asdamir is never in the money path) and a default-off crypto provider.
- **`PaymentService`** — a facade that resolves the active rail by name.
- **`BillingWebhookProcessor`** — turns a verified webhook into a subscription status transition; each host
  wires a thin controller around it (verify → claim → process → mark processed; no MVC/route dependency leaks
  into the package).
- **`LocalDbBillingStore`** — an app-local (single-tenant) store over `Asdamir.Core`'s `IDbConnectionFactory`,
  for a standalone app that has no central control plane.

```bash
dotnet add package Asdamir.Payments
```

```csharp
builder.Services.AddPayments(builder.Configuration);   // Paddle + crypto rails + the facade
```

## Webhook handling contract

The host controller owns the pipeline; `IBillingStore` and `BillingWebhookProcessor` supply the pieces:

```csharp
var evt = (await provider.VerifyWebhookAsync(request, ct)).Value;      // fail closed on a bad signature
if (!await store.TryRecordWebhookAsync(provider.Name, evt.ProviderEventId, evt.Type, evt.RawPayload, ct))
    return Ok();                                                        // processed, or in progress elsewhere
try   { await processor.ProcessAsync(provider.Name, evt, ct); }
catch { await store.ReleaseWebhookAsync(provider.Name, evt.ProviderEventId, CancellationToken.None); throw; }
await store.MarkWebhookProcessedAsync(provider.Name, evt.ProviderEventId, ct);
return Ok();
```

- **The dedupe row is a claim; only "processed" makes it final.** `TryRecordWebhookAsync` returns `true` for a
  new event *and* for an unprocessed one whose claim has gone stale (`LocalDbBillingStore`: 60 s), and `false`
  for a processed event or one another delivery is handling. Call `MarkWebhookProcessedAsync` only after
  processing succeeded.
- **A failed event is not lost.** `ProcessAsync` lets a storage failure propagate; release the claim with
  `ReleaseWebhookAsync` and let the request fail (5xx) so the provider's retry is processed at once.
  `ReleaseWebhookAsync` has a default no-op implementation — a store that does not implement it still recovers
  through the stale-claim path, only later. `LocalDbBillingStore` calls the `dbo.BillingWebhookEvent_Release`
  procedure, which the app's billing schema must contain.
- **Paddle lookup and status.** The subscription is looked up by `data.id`, then `data.subscription_id`, then
  `data.transaction_id` (checkout can only store the transaction id); the real Paddle subscription id is
  persisted the first time it is seen. For `subscription.*` events the reported `data.status` wins over the
  status the event name implies. A `canceled` subscription is terminal — a late event does not reopen it.
- **Refunds** are sent in minor units, rounded half away from zero (two-decimal currencies).
- **One trial per account.** `IBillingStore.CreateSubscriptionAsync` stores a repeat trial as `incomplete`
  (no entitlements until a payment webhook activates it) — read the subscription back to see the stored status.

The crypto rail ships **disabled by default** with a buyer geo-gate — enable it only after an accountant
sign-off (see the framework docs). Licensed under LGPL-3.0.
