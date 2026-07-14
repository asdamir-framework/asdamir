# Billing

Billing is an **opt-in** feature family: a generated app can host an **end-user payment page** so its own
users can subscribe and pay. It is **off by default** — without the flag, not a single billing file is
emitted and a generated app is byte-identical to one scaffolded without it.

```bash
asdamir new app MyApp --billing            # commercial (Model A)
asdamir new app MyApp --mode free --billing  # free / self-contained (Model B)
```

Both add an end-user page at **`/billing`** — plans, the current subscription, and checkout.

## Two models

Billing follows the same **commercial vs free** split as the rest of the framework:

| | **Model A — commercial** (`--billing`) | **Model B — free** (`--mode free --billing`) |
|---|---|---|
| Where billing data lives | Central **AsdamirVault**, scoped by the app's `AppId` | The app's **own** database (single-tenant) |
| Payment secret | Held centrally by AppManagement (the app never sees it) | The app owns its own `Payment:Paddle:*` config |
| How the app serves it | Gateway **proxies** `gateway/billing/*` → AppManagement | Gateway serves billing **locally** via the `Asdamir.Payments` package (`LocalDbBillingStore` + a local webhook) |
| Operator view | Monitored from AppManagement's **`/billing` console** | No control plane — the app owns it end to end |

In both models the end-user page is identical; only the wiring behind it differs.

## Payment rails

Billing is provider-agnostic behind `IPaymentProvider`. Two rails ship in `Asdamir.Payments`:

- **Paddle — Merchant of Record (default).** Card / Apple Pay / Google Pay via Paddle's hosted checkout.
  Paddle is the **Merchant of Record**, so tax/VAT and invoicing are handled by Paddle (pass-through — the
  framework is not in the money path). Each tenant connects its **own** Paddle account; checkout redirects to
  that tenant's Paddle hosted page. Webhooks are **signature-verified** before any subscription state changes.
- **Crypto — default-off.** A crypto rail exists behind the same `IPaymentProvider` contract but ships
  **disabled** (`Payment:Crypto:Enabled=false`) and is not Merchant-of-Record. Keep it off unless your
  finance/legal setup explicitly allows it.

## Subscription lifecycle

Webhook events (checkout completed, renewed, cancelled, payment failed) are turned into subscription status
transitions by the billing webhook processor. Recurring renewals and lifecycle transitions are driven by a
background worker on the API tier, so a subscription's state stays current without a user action.

## Operator console (`/billing`) — commercial

In commercial mode, operators monitor billing from **AppManagement's `/billing` page**: per-app
subscriptions and their status, and each app's Paddle configuration (keys are seeded **empty + encrypted** by
`seed_billing.sql`, then set from AppManagement — checkout goes live once they're set). Because the app
proxies to AppManagement, the generated app never holds the payment secret.

## Configuration

The `Payment:Paddle:*` (and `Payment:Crypto:*`) keys are seeded into `AppConfigurations` — empty and
encrypted — by the billing seed (`seed_billing.sql` for Model A, a `V*__freemode_billing_*` migration for
Model B). Set the real keys via AppManagement (Model A) or the app's own user-secrets / env (Model B). Never
put a payment secret in `appsettings.json`.

## Localization

The billing page's strings (`Menu.Billing`, `Billing.Page.*`) are seeded in all three cultures
(`tr-TR` / `en-US` / `ru-RU`) by the billing seed, like any other generated feature.
