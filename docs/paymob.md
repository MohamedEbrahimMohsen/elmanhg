# Paymob checkout

Students pay for Base and Ask a Teacher through Paymob's **unified checkout** (card and mobile wallet, PRD §11.2). The checkout rules (what can be bought, the Pending payment, the result page) are in `docs/subscriptions.md` → Checkout. This page covers the payment gateway: configuration, the Paymob flow, the fake, and going live.

## 1. Provider switch

`Payments:Provider` chooses the `IPaymentGateway` adapter:

| Value | Adapter | Use |
|---|---|---|
| `Fake` (default) | `FakePaymentGateway` | Development, tests, CI and the build-time OpenAPI run. No keys needed. |
| `Paymob` | `PaymobPaymentGateway` | Real payments through the Paymob Intention API. |

`Fake` is the default in code, in `appsettings.example.json` and in the test host.

## 2. Configuration reference

All keys live under `Payments` (environment variables use `__`, for example `Payments__Paymob__SecretKey`). **Secrets** go in environment variables only, never in a committed file.

| Key | Default | Secret | Notes |
|---|---|---|---|
| `Provider` | `Fake` | no | `Fake` or `Paymob` |
| `FakeCheckoutPath` | `/student/fake-checkout` | no | Web route of the simulated checkout page; the payment id is appended |
| `AttemptTimeoutSeconds` | `10` | no | 1–15, per HTTP attempt |
| `TotalTimeoutSeconds` | `30` | no | 1–120 |
| `Paymob:BaseUrl` | `https://accept.paymob.com` | no | API host (Egypt) |
| `Paymob:CheckoutUrl` | `https://accept.paymob.com/unifiedcheckout/` | no | Unified checkout page |
| `Paymob:SecretKey` | empty | **yes** | Dashboard → Settings → Account info → Secret key |
| `Paymob:PublicKey` | empty | no | Dashboard → Settings → Account info → Public key |
| `Paymob:IntegrationIds` | empty | no | One id per payment method (card, wallet), for example `Payments__Paymob__IntegrationIds__0=123` |
| `Paymob:RedirectionUrl` | empty | no | Web route base the browser returns to, for example `https://app.example/student/checkout-result`; the payment id is appended as a path segment |
| `Paymob:NotificationUrl` | empty | no | Webhook URL for transaction callbacks; set it once #101 ships. Omitted from the request when blank |
| `Paymob:BillingCountry` | `EG` | no | `billing_data.country` |

## 3. Startup validation

`PaymentsOptionsValidator` runs at startup (`ValidateOnStart`):

- `AttemptTimeoutSeconds` must not exceed `TotalTimeoutSeconds`; `FakeCheckoutPath` must be an in-app path.
- With `Provider=Paymob` only: `SecretKey`, `PublicKey`, `RedirectionUrl` and `BillingCountry` are required; `IntegrationIds` needs at least one positive id; `BaseUrl`, `CheckoutUrl`, `RedirectionUrl` and (when set) `NotificationUrl` must be absolute `https` URLs.
- The fake is never checked for Paymob keys. There is no silent fallback: choosing `Paymob` without its keys fails the boot.

## 4. The Intention API flow

1. `POST /api/subscriptions/checkout` creates the `Payment` (its id is known), then calls the gateway.
2. The adapter sends `POST {BaseUrl}/v1/intention/` with `Authorization: Token <SecretKey>` and `User-Agent: Elmanhg/1.0`. Body:
   - `amount`: integer minor units (piastres); `currency`: the payment currency.
   - `payment_methods`: `IntegrationIds`.
   - `items`: one item, `name` and `description` `Elmanhg <Plan> <Period>`, the same `amount`, `quantity` 1.
   - `billing_data`: `first_name` and `last_name` split from the display name, `email`, `phone_number`, `country`; every missing or unused field (`apartment`, `floor`, `street`, `building`, `city`, `state`, a blank email or phone, a one-word name's last name) is `"NA"`.
   - `special_reference`: the payment id (the merchant order id #101 matches webhooks on).
   - `notification_url` (when set) and `redirection_url` = `{RedirectionUrl}/{paymentId}`.
3. The adapter reads `client_secret` from the response and returns the redirect URL `{CheckoutUrl}?publicKey=<PublicKey>&clientSecret=<client_secret>`.
4. Only then is the Pending payment saved. Any failure (a transport error, a timeout, a non-2xx status, an unreadable body, no `client_secret`) returns 503 `PAYMENT_GATEWAY_UNAVAILABLE` and saves nothing.

The request is a POST without an idempotency key, so retries are disabled (the standard resilience handler with `Retry.DisableForUnsafeHttpMethods()`); a failed checkout is retried by the student. Logs name the payment id and the HTTP status only, never the body, the keys or the billing data.

## 5. Verify before go-live

The adapter was built and tested against a stubbed HTTP handler only; no Paymob merchant account was available. Check these in the Paymob dashboard and docs before switching the provider:

- Whether `special_reference` comes back as `merchant_order_id` (or under which name) in the transaction webhook. #101 depends on it.
- Whether `billing_data.email` accepts `"NA"` for phone-only students.
- The phone format Paymob expects: local `010…` (sent today) or `+20…`.
- The query-parameter names `publicKey` and `clientSecret`, and the trailing slash on `/unifiedcheckout/`.
- Whether `payment_methods` may mix integer ids with method names; only integer ids are sent today.
- That the response `client_secret` is a JSON string. It is the only field the adapter reads; every other field (including `id`, string or number) is ignored.

## 6. The fake

With `Provider=Fake`, checkout returns the in-app path `{FakeCheckoutPath}/{paymentId}`. That page shows the plan and amount and offers **success**, **failure** and **cancel**. Success and failure call `POST /api/subscriptions/payments/{paymentId}/fake-completion { succeeded }`, which runs the same `PaymentSettlement.Settle` the webhook (#101) will call, so the entitlement changes server-side exactly as it will with Paymob.

**Production lock:** in the `Production` environment the fake refuses checkout (503 `PAYMENT_GATEWAY_UNAVAILABLE`) and fake completion (404 `FAKE_CHECKOUT_UNAVAILABLE`), so a misconfigured server can never grant free plans. With `Provider=Paymob`, fake completion is always 404.

## 7. Going live

1. In the Paymob dashboard, create the **card** and **mobile wallet** integrations and note their integration ids.
2. Copy the **secret key** and **public key**.
3. Set `Payments__Provider=Paymob`, `Payments__Paymob__SecretKey`, `Payments__Paymob__PublicKey`, `Payments__Paymob__IntegrationIds__0`, `Payments__Paymob__IntegrationIds__1` and `Payments__Paymob__RedirectionUrl` (the web `…/student/checkout-result` base).
4. Once the webhook ships (#101), set `Payments__Paymob__NotificationUrl`. Until then a paid checkout stays Pending and the result page says confirmation has not arrived yet.
5. Work through section 5 with a test-mode payment before taking real money.
