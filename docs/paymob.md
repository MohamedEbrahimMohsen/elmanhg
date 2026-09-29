# Paymob checkout

Students pay for Base and Ask a Teacher through Paymob's **unified checkout** (card and mobile wallet, PRD §11.2). The checkout rules (what can be bought, the Pending payment, the result page, settlement) are in `docs/subscriptions.md` → Checkout and Webhook. This page covers the payment gateway: configuration, the Paymob flow, the fake, the transaction webhook, refunds and going live. Admin refunds and the payment log are in `docs/subscriptions.md` → Refunds and Admin payment log.

## 1. Provider switch

`Payments:Provider` chooses the `IPaymentGateway` adapter:

| Value | Adapter | Use |
|---|---|---|
| `Fake` (default) | `FakePaymentGateway` | Development, tests, CI and the build-time OpenAPI run. No keys needed. |
| `Paymob` | `PaymobPaymentGateway` | Real payments through the Paymob Intention API, and admin refunds through the Paymob refund API (section 9). |

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
| `Paymob:NotificationUrl` | empty | no | Webhook URL for transaction callbacks: `https://<api>/api/payments/paymob/webhook` (section 8). Omitted from the request when blank |
| `Paymob:BillingCountry` | `EG` | no | `billing_data.country` |
| `Paymob:HmacSecret` | empty | **yes** | Dashboard → Settings → Account info → HMAC. Verifies every transaction webhook (section 8). Empty means every webhook is refused (401) |

## 3. Startup validation

`PaymentsOptionsValidator` runs at startup (`ValidateOnStart`):

- `AttemptTimeoutSeconds` must not exceed `TotalTimeoutSeconds`; `FakeCheckoutPath` must be an in-app path.
- With `Provider=Paymob` only: `SecretKey`, `PublicKey`, `RedirectionUrl`, `BillingCountry` and `HmacSecret` are required; `IntegrationIds` needs at least one positive id; `BaseUrl`, `CheckoutUrl`, `RedirectionUrl` and (when set) `NotificationUrl` must be absolute `https` URLs.
- The fake is never checked for Paymob keys. There is no silent fallback: choosing `Paymob` without its keys fails the boot.

## 4. The Intention API flow

1. `POST /api/subscriptions/checkout` creates the `Payment` (its id is known), then calls the gateway.
2. The adapter sends `POST {BaseUrl}/v1/intention/` with `Authorization: Token <SecretKey>` and `User-Agent: Elmanhg/1.0`. Body:
   - `amount`: integer minor units (piastres); `currency`: the payment currency.
   - `payment_methods`: `IntegrationIds`.
   - `items`: one item, `name` and `description` `Elmanhg <Plan> <Period>`, the same `amount`, `quantity` 1.
   - `billing_data`: `first_name` and `last_name` split from the display name, `email`, `phone_number`, `country`; every missing or unused field (`apartment`, `floor`, `street`, `building`, `city`, `state`, a blank email or phone, a one-word name's last name) is `"NA"`.
   - `special_reference`: the payment id (the merchant order id the webhook matches on).
   - `notification_url` (when set) and `redirection_url` = `{RedirectionUrl}/{paymentId}`.
3. The adapter reads `client_secret` from the response and returns the redirect URL `{CheckoutUrl}?publicKey=<PublicKey>&clientSecret=<client_secret>`. It also reads `intention_order_id` (a number or a non-blank string) and the payment stores it as `ProviderOrderId`, the signed order id the webhook carries as `obj.order.id`. A missing value leaves it null.
4. Only then is the Pending payment saved. Any failure (a transport error, a timeout, a non-2xx status, an unreadable body, no `client_secret`) returns 503 `PAYMENT_GATEWAY_UNAVAILABLE` and saves nothing.

The request is a POST without an idempotency key, so retries are disabled (the standard resilience handler with `Retry.DisableForUnsafeHttpMethods()`); a failed checkout is retried by the student. Logs name the payment id and the HTTP status only, never the body, the keys or the billing data.

## 5. Verify before go-live

The adapter was built and tested against a stubbed HTTP handler only; no Paymob merchant account was available. Check these in the Paymob dashboard and docs before switching the provider:

- Whether `special_reference` comes back as `obj.order.merchant_order_id` in the transaction webhook. The webhook matches on it first and falls back to `ProviderOrderId`.
- That the Intention response carries `intention_order_id`, and that it equals the webhook's `obj.order.id`.
- The exact HMAC field formatting of the transaction callback (section 8): how a `null` value is concatenated, the case of booleans, and the exact `created_at` string. Send one test-mode payment and compare the computed digest with `hmac`.
- The shape of refund and void callbacks: a child transaction with `has_parent_transaction` true, `is_capture` false and its own `id` and `amount_cents` (section 8). The fixture `transaction-refund.json` also carries `parent_transaction` and `is_refund`, which are not signed and not read.
- That `POST api/acceptance/void_refund/refund` accepts `Authorization: Token <SecretKey>` (the secret key, not the legacy auth token).
- That the refund body's `transaction_id` may be sent as a string (the stored `PaymobTransactionId`) and `amount_cents` as an integer.
- That the refund response carries `id` (number or string), `success` and `pending` at the top level.
- Whether a same-day card payment must be voided instead of refunded. Today there is no void call: Paymob's refusal shows as 400 `PAYMENT_REFUND_DECLINED` and the admin retries the next day.
- Whether `billing_data.email` accepts `"NA"` for phone-only students.
- The phone format Paymob expects: local `010…` (sent today) or `+20…`.
- The query-parameter names `publicKey` and `clientSecret`, and the trailing slash on `/unifiedcheckout/`.
- Whether `payment_methods` may mix integer ids with method names; only integer ids are sent today.
- That the response `client_secret` is a JSON string. The adapter reads it and `intention_order_id`; every other field (including `id`, string or number) is ignored.

## 6. The fake

With `Provider=Fake`, checkout returns the in-app path `{FakeCheckoutPath}/{paymentId}`. That page shows the plan and amount and offers **success**, **failure** and **cancel**. Success and failure call `POST /api/subscriptions/payments/{paymentId}/fake-completion { succeeded }`, which runs the same `PaymentSettlement.Succeed` / `Fail` the webhook calls, so the entitlement changes server-side exactly as it does with Paymob. There is no extra re-check: like the webhook, a success for a plan the student already holds extends it. Only a Pending payment can be completed (400 `PAYMENT_NOT_PENDING`).

**Production lock:** in the `Production` environment the fake refuses checkout (503 `PAYMENT_GATEWAY_UNAVAILABLE`) and fake completion (404 `FAKE_CHECKOUT_UNAVAILABLE`), so a misconfigured server can never grant free plans. With `Provider=Paymob`, fake completion is always 404.

## 7. Going live

1. In the Paymob dashboard, create the **card** and **mobile wallet** integrations and note their integration ids.
2. Copy the **secret key** and **public key**.
3. Copy the **HMAC** secret.
4. Set `Payments__Provider=Paymob`, `Payments__Paymob__SecretKey`, `Payments__Paymob__PublicKey`, `Payments__Paymob__HmacSecret`, `Payments__Paymob__IntegrationIds__0`, `Payments__Paymob__IntegrationIds__1`, `Payments__Paymob__RedirectionUrl` (the web `…/student/checkout-result` base) and `Payments__Paymob__NotificationUrl=https://<api>/api/payments/paymob/webhook`.
5. Work through section 5 with a test-mode payment before taking real money.

## 8. Transaction webhook

Paymob posts the transaction-processed callback to `POST /api/payments/paymob/webhook?hmac=<digest>` (`PaymobWebhooksController`). The endpoint is anonymous (the HMAC is its authentication), limited to 64 KiB, and hidden from OpenAPI and the web client. `PaymobNotificationReader` parses and verifies it; `ProcessPaymentNotification` settles it (`docs/subscriptions.md` → Webhook).

**Signature.** HMAC-SHA512 with key `HmacSecret` (UTF-8) over the concatenation, with no separator, of these `obj` values in this exact order:

`amount_cents`, `created_at`, `currency`, `error_occured`, `has_parent_transaction`, `id`, `integration_id`, `is_3d_secure`, `is_auth`, `is_capture`, `is_refunded`, `is_standalone_payment`, `is_voided`, `order.id`, `owner`, `pending`, `source_data.pan`, `source_data.sub_type`, `source_data.type`, `success`.

- A JSON string contributes its value; `true`/`false` contribute `"true"`/`"false"`; a number contributes its raw JSON text; `null`, a missing field, an object or an array contribute `""`.
- The digest is lower-case hex. The provided signature is trimmed and lower-cased, then compared in constant time (`CryptographicOperations.FixedTimeEquals`).
- **Uncertain:** the `null` formatting and the `created_at` text are unverified against a live account (section 5).
- The signature is read from the `hmac` query parameter; when that is absent, from a top-level `hmac` string in the body.
- **Fail closed:** an empty `HmacSecret`, a missing signature or a mismatch is 401 `PAYMOB_WEBHOOK_SIGNATURE_INVALID`, nothing changes, and the audit log records a Failure row. The secret and the raw payload are never logged.

**Classification** (signed fields only; the comment in `PaymobNotificationReader.KindOf` states why): `has_parent_transaction` true and `is_capture` false → `Reversal` (a refund or void child transaction); `has_parent_transaction` true and `is_capture` true → `Other`; no parent but `is_refunded` or `is_voided` true → `Other` (the parent's update; the child carries the reversal); otherwise `Charge`. The unsigned `is_refund` / `is_void` flags are never read: they could be added to a replayed signed body.

**Ignored callbacks** (200 `Ignored`, no change): a `type` other than `TRANSACTION` (for example `TOKEN`, which uses another field set, so it is not verified); a verified transaction that is `pending` or classified `Other`.

**Reversals.** A signed `Reversal` is matched and bound like a charge (below), with its own amount: a full reversal of a Succeeded payment marks it Refunded and revokes the paid time (`PaymentRefundSettlement`, `docs/subscriptions.md` → Refunds), a partial one flags the payment `PartialRefundAtProvider` for review, a reversal of a Pending payment is 409 `PAYMENT_NOT_SETTLED` so Paymob retries after the charge settles, and a repeat is a `Duplicate` (keyed by `RefundTransactionId`). The full outcome table is in `docs/subscriptions.md` → Webhook.

**Matching.** `obj.order.merchant_order_id` parsed as a Guid is the `Payment.Id` (the Intention `special_reference`). When it is missing or not a Guid, the payment whose `ProviderOrderId` equals `obj.order.id` is used. No match is 404 `PAYMENT_NOT_FOUND`, so Paymob retries and nothing is lost to a race with the checkout save.

**Binding.** `merchant_order_id` is not in the signed field set, so a replayed signed body could point at another payment. The signed `amount_cents` and `currency` must equal the payment snapshot, and when the payment has a `ProviderOrderId` it must equal the signed `obj.order.id`. A mismatch is 400 `PAYMENT_NOTIFICATION_MISMATCH` (audited, nothing changes).

**Status codes.** 200 with `{ paymentId, outcome }` (`Succeeded`, `MarkedFailed`, `Duplicate`, `Ignored`, `OutOfOrder`, `Refunded`, `FlaggedForReview`); 400 without a `code` for malformed JSON (MVC model binding rejects the body before the handler runs, like every `[FromBody]` endpoint); 400 `PAYMOB_WEBHOOK_PAYLOAD_INVALID` (no `obj`, or a signed transaction missing `id`, `success`, `pending`, `amount_cents`, `currency` or `order.id`) or `PAYMENT_NOTIFICATION_MISMATCH`; 401 signature; 404 unmatched; 409 on a concurrent write (`PAYMENT_MODIFIED_CONCURRENTLY`, `SUBSCRIPTION_MODIFIED_CONCURRENTLY`, `PAYMENT_TRANSACTION_ALREADY_RECORDED`), which Paymob retries as a `Duplicate`, or `PAYMENT_NOT_SETTLED` for a reversal that arrives before its charge.

**The return URL never settles.** Paymob also redirects the browser to `/student/checkout-result/{paymentId}` with a query string that includes `success` and `hmac`. The page ignores it and only polls the server (PRD §17 rule 12).

## 9. Refunds

`IPaymentGateway.RefundAsync(PaymentRefundRequest(paymentId, providerTransactionId, amount))` refunds a settled payment in full. The admin flow around it (reason, idempotency key, revocation) is in `docs/subscriptions.md` → Refunds.

**Request.** `POST {BaseUrl}/api/acceptance/void_refund/refund` with `Authorization: Token <SecretKey>`, `User-Agent: Elmanhg/1.0` and the body `{ "transaction_id": "<PaymobTransactionId>", "amount_cents": <AmountMinor> }`. The response fields read are `id` (number or string, stored as `RefundTransactionId`), `success` and `pending`; every other field is ignored.

**Error mapping.**

| Paymob answer | Result |
|---|---|
| Transport error, timeout, 5xx or 429 | 503 `PAYMENT_GATEWAY_UNAVAILABLE` (logged at Error) |
| Another 4xx | 400 `PAYMENT_REFUND_DECLINED` (logged at Warning) |
| 2xx with `success` false (with or without an `id`) | 400 `PAYMENT_REFUND_DECLINED`: Paymob did nothing |
| 2xx with an unreadable body, or no `id` when `success` is not false | 503 `PAYMENT_GATEWAY_UNAVAILABLE` |
| 2xx with an `id` and `success` missing, or `pending` true | 400 `PAYMENT_REFUND_DECLINED`; a pending refund that completes later arrives as a signed reversal callback |
| 2xx with `success` true | the refund transaction id |

Nothing is saved locally unless Paymob confirms the refund. Bodies and keys are never logged.

**No retries.** The refund call is not idempotent at Paymob (no idempotency key is documented), so the typed client keeps `Retry.DisableForUnsafeHttpMethods()`: a POST is sent once. A timeout after Paymob processed the refund leaves the payment Succeeded locally; the signed reversal callback then marks it Refunded.

**The fake.** With `Provider=Fake`, `RefundAsync` returns `fake-refund-{paymentId:N}` at once. In `Production` it refuses with 503 `PAYMENT_GATEWAY_UNAVAILABLE`, like fake checkout.

**Void.** There is no separate void call; see section 5.
