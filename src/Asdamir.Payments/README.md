# Asdamir.Payments

Payment rails for the [Asdamir](https://www.nuget.org/packages/Asdamir.Core) stack.

`Asdamir.Core` ships the payment **contract** (`IPaymentProvider`, `PaymentProviderOptions`). This package
ships the concrete **rails and plumbing** on top of it, so an app can take end-user payments:

- **Providers** — `PaddlePaymentProvider` (Paddle Billing, Merchant-of-Record; each tenant connects its own
  Paddle account — pass-through, so Asdamir is never in the money path) and a default-off crypto provider.
- **`PaymentService`** — a facade that resolves the active rail by name.
- **`BillingWebhookHandler`** — a store-agnostic verify → dedup → process pipeline; each host wires a thin
  controller to it (no MVC/route dependency leaks into the package).
- **`LocalDbBillingStore`** — an app-local (single-tenant) store over `Asdamir.Core`'s `IDbConnectionFactory`,
  for a standalone app that has no central control plane.

```bash
dotnet add package Asdamir.Payments
```

```csharp
builder.Services.AddPayments(builder.Configuration);   // Paddle + crypto rails + the facade
```

The crypto rail ships **disabled by default** with a buyer geo-gate — enable it only after an accountant
sign-off (see the framework docs). Licensed under LGPL-3.0.
