# Subscriptions, entitlement and payments

This is the contract for plans, subscriptions, entitlement and payments (PRD §11, story #99). Paymob checkout (#100), the webhook, renewal, cancel and the lapse sweep (#101) and refunds (#102) build on it.

## Plans and configuration

The plan catalogue lives in configuration, section `Subscriptions` (`SubscriptionsOptions`). There is no Plans table and no admin plan editor. The options are validated at startup: a missing or invalid value fails the boot.

| Key | Default | Meaning |
|---|---|---|
| `Currency` | `EGP` | ISO 4217 code (three upper-case letters) for every price. |
| `GracePeriodDays` | `3` | Days of access after a paid period ends while the subscription is Active or PastDue. |
| `FreeDailyQuizQuestions` | `10` | Free tier: quiz questions per day. |
| `FreeDailyAvatarMessages` | `5` | Free tier: Avatar messages per day. |
| `FreeOpenLessonsPerUnit` | `1` | Free tier: lessons open per unit (the first ones). |
| `BaseDailyAvatarMessages` | `50` | Base: Avatar messages per day. |
| `BasePrices:{Monthly,Termly,Yearly}:Months` | no default: must be set | Length of each Base period in months (1–36). |
| `BasePrices:{Monthly,Termly,Yearly}:AmountMinor` | no default: must be set | Price of each Base period in minor units (> 0). At least one period is required. |
| `AskTeacherMonthlyQuestions` | `20` | Ask a Teacher: questions per month. |
| `AskTeacherReplySlaHours` | `24` | Ask a Teacher: reply SLA in hours. |
| `AskTeacherMonthlyPriceMinor` | no default: must be set | Ask a Teacher monthly price in minor units. The add-on is sold monthly only (PRD §11.1). |
| `PaymentHistoryMaxPageSize` | `50` | Largest page size of the student payment log. |
| `RenewalWindowDays` | `7` | Days before `CurrentPeriodEnd` from which checkout sells the plan the student already holds (a renewal). 0–30. |
| `LapseSweepEnabled` | `true` | Runs the lapse sweep (`SubscriptionLapseWorker`). Off in the integration test host. |
| `LapseSweepIntervalSeconds` | `300` | Seconds between sweeps (5–86400). |
| `LapseSweepBatchSize` | `100` | Subscriptions lapsed per sweep batch (1–1000). |

Placeholder prices ship in `appsettings.example.json` (and the test host): Base 19900 / 69900 / 179900 (199 / 699 / 1,799 EGP for 1 / 4 / 12 months) and Ask a Teacher 9900 (99 EGP a month). Final prices are still open (PRD §19 Q1); copy the section into your local `appsettings.json` and change the numbers.

## Money

Money is an integer number of minor units (piastres for EGP) plus an ISO 4217 code: `Money(AmountMinor, Currency)` in the domain, `{ "amountMinor": 19900, "currency": "EGP" }` on the wire (schema `Money`). No `decimal` or float is used for money. The web formats it with `formatMoney` (`shared/lib/format.ts`).

A `Payment` stores a snapshot of the amount it charged (`AmountMinor`, `Currency` columns). The price paid is never recomputed from configuration.

## Subscription lifecycle

A `Subscription` belongs to one student and one plan (`Base` or `AskTeacher`) with a billing period (`Monthly`, `Termly`, `Yearly`). States are `Active`, `PastDue`, `Cancelled` and `Expired`. Trialing is not used in v1.

| Transition | From | To | Rejected with |
|---|---|---|---|
| `Start(periodMonths, startsAt, …)` | — | Active, period `startsAt` → `startsAt + months` | `SUBSCRIPTION_PERIOD_INVALID` when months < 1 |
| `Renew(period, periodMonths, reference?, renewedAt, grace)` | Active or PastDue before end + grace; Cancelled before end (entitled at `renewedAt`) | Active; the new period starts at the previous `CurrentPeriodEnd` (even inside grace, so grace days are paid days); `Period` switches to the paid period; a Cancelled subscription resumes (`CancelledAt` cleared) | `SUBSCRIPTION_ENDED` when not entitled at `renewedAt` (Expired, or past its end); `SUBSCRIPTION_PERIOD_INVALID` when months < 1 |
| `MarkPastDue()` | Active | PastDue | `SUBSCRIPTION_NOT_ACTIVE` from any other state |
| `Cancel(cancelledAt)` | Active, PastDue | Cancelled | `SUBSCRIPTION_ENDED` from Cancelled or Expired |
| `Expire(expiredAt)` | Active, PastDue, Cancelled | Expired | `SUBSCRIPTION_ALREADY_EXPIRED` from Expired |
| `Lapse(now, grace)` returns bool | Active, PastDue, Cancelled | Active past `CurrentPeriodEnd` but inside grace → PastDue; Active or PastDue at or past end + grace → Expired with `ExpiredAt = end + grace`; Cancelled at or past end → Expired with `ExpiredAt = end`; otherwise no change (`false`) | never throws |

`Renew` keeps the previous Paymob reference when none is given. Every rejected transition is a `400` business-rule error. `Subscription` carries an `xmin` concurrency token (`Version`): a concurrent write returns 409 `SUBSCRIPTION_MODIFIED_CONCURRENTLY`.

## Entitlement

Access is derived at read time from status and dates. It is never stored.

- `EntitledUntil(grace)`: Active or PastDue → `CurrentPeriodEnd + grace`; Cancelled → `CurrentPeriodEnd`; Expired → none.
- A subscription is entitled while `EntitledUntil > now`.
- `SubscriptionEntitlementSpecification.EntitledFor(studentId, now, grace)` is the single SQL definition of that rule. A domain test proves it agrees with `IsEntitledAt` for every status and time.
- Per plan, the entitled subscription with the latest `EntitledUntil` wins.
- **Free** has no subscription row: a student with no entitled Base is Free.
- **Ask a Teacher requires Base**: the add-on counts only while an entitled Base exists (`HasAskTeacher`). Checkout (#100) refuses to sell it alone; the read-time rule also covers Base lapsing while the add-on is still paid.
- A lapse takes effect on time without waiting for a status sweep: an Active row past its end plus grace (for example after a lost webhook) already reads as Free.
- **Cancel keeps paid time.** A Cancelled subscription keeps access until `CurrentPeriodEnd` and then lapses. PRD §17 rule 12 lets only Paymob webhooks change entitlement, so a cancel must not revoke time already paid for. The prototype revokes access at once; the prototype is a simulation and the PRD wins.

The resolved entitlement (`EntitlementResult`) carries the limits: Free gets the Free quotas and `canTakeExams: false`; Base gets unlimited quizzes and open lessons (`null`), the Base Avatar limit and exams. `monthlyAskTeacherQuestionLimit` is the Ask a Teacher quota when the add-on counts, else 0.

## Payments

A `Payment` records one checkout: student, plan, period, amount snapshot, period-length snapshot, status and completion time. States are `Pending`, `Succeeded` and `Failed`. `MarkFailed` only accepts a Pending payment; `MarkSucceeded` accepts Pending **or Failed** (one Paymob order can carry a declined attempt and then an approved one, and captured money always wins); both refuse otherwise with `PAYMENT_NOT_PENDING`. `Payment.Create(studentId, plan, period, periodMonths, amount)` rejects a non-positive amount or a currency that is not three upper-case letters (`PAYMENT_AMOUNT_INVALID`) and months < 1 (`SUBSCRIPTION_PERIOD_INVALID`).

- `PeriodMonths` is snapshotted at checkout, like the amount. Settlement never reads the price configuration, so a configuration change can never make a paid webhook unsettleable. The #101 migration backfilled existing rows (Monthly 1, Termly 4, Yearly 12).
- `ProviderOrderId` is the Paymob order id (`intention_order_id` of the Intention response), stored before the first save when the gateway returns one (the fake returns none). It has a non-unique index filtered to non-null values (`IX_Payments_ProviderOrderId`).
- `ReviewReason` (`AskTeacherWithoutBase`) flags a settled payment for an admin decision (#102).
- `Payment` carries an `xmin` concurrency token (`Version`): a concurrent write returns 409 `PAYMENT_MODIFIED_CONCURRENTLY`.

- `Payment.Id` is the Paymob `merchant_order_id`; no separate order column is needed.
- `SubscriptionId` is set on success. A first purchase has no subscription yet while the payment is Pending.
- `PaymobTransactionId` has a unique index filtered to non-null values (`IX_Payments_PaymobTransactionId`). It is the webhook idempotency key; a unique-index hit returns 409 `PAYMENT_TRANSACTION_ALREADY_RECORDED`.
- The raw webhook body is kept in full as jsonb (`RawWebhook`) as dispute evidence. It carries billing PII, so it is excluded from audit diffs (the `Core:AuditExcluded` annotation, `docs/audit-log.md`).
- The student payment log lists **completed** payments only (Succeeded and Failed), newest first. A Pending payment is an abandoned or in-flight checkout and is not shown.

## Checkout

A student buys a plan with `POST /api/subscriptions/checkout { plan, period }` (story #100). The gateway and its configuration are in `docs/paymob.md`.

- **What can be bought.** `StudentEntitlement.EnsureCanPurchase(plan, now, renewalWindow)`: Ask a Teacher without an entitled Base is refused with 400 `CHECKOUT_REQUIRES_BASE`. A plan the student already holds (an entitled subscription of that plan, including a Cancelled one still inside its paid period) is refused with 400 `CHECKOUT_PLAN_ALREADY_ACTIVE` **until the renewal window opens**: from `CurrentPeriodEnd − RenewalWindowDays` (default 7 days) the held plan can be bought again, in any period, and the payment renews it (see Settlement). There are no automatic charges in v1: a student renews by paying again.
- **Prices and periods** come from `SubscriptionsOptions.PriceFor(plan, period)`: Base sells the periods configured in `BasePrices`; Ask a Teacher sells `Monthly` only. Any other pair is 422 `CHECKOUT_PERIOD_UNAVAILABLE` (400 if the configuration changes between validation and handling). A missing or unknown plan or period is 422 `CHECKOUT_PLAN_REQUIRED` / `CHECKOUT_PLAN_INVALID` / `CHECKOUT_PERIOD_REQUIRED` / `CHECKOUT_PERIOD_INVALID`.
- **Pending payment first.** The handler creates the `Payment` at the configured price and months, asks the gateway for a redirect URL (and the Paymob order id, stored as `ProviderOrderId`), and only then saves the Pending payment. A gateway failure (503 `PAYMENT_GATEWAY_UNAVAILABLE`) saves nothing. The response is `CheckoutResult { paymentId, redirectUrl, amount }`.
- **Return page.** Paymob returns the browser to `{Payments:Paymob:RedirectionUrl}/{paymentId}` (`/student/checkout-result/{paymentId}`); any query string Paymob appends is ignored. The page only reads `GET /api/subscriptions/payments/{paymentId}` (owner-scoped, Pending included; another student's payment is 404 `PAYMENT_NOT_FOUND`). It polls every 2 s while the payment is Pending, for up to 60 s after the page opened, then shows "confirmation has not arrived yet" with a **Check again** button. The browser never activates a plan (PRD §17 rule 12).
- **Settlement.** `PaymentSettlement.Succeed(payment, entitlement, transactionId, rawNotification, grace, completedAt)` and `PaymentSettlement.Fail(payment, transactionId, rawNotification, completedAt)` are the one settle routine, shared by the webhook and the fake. `Succeed` **extends** a held plan: when the student holds an entitled subscription of the paid plan, the payment is marked Succeeded against it and the subscription is renewed (`Renew`, from the old end, in the paid period, resuming a Cancelled one); otherwise a new subscription starts at `completedAt` for `PeriodMonths`. The money was captured, so a double payment becomes more paid time; no refund is needed. An Ask a Teacher payment settled while the student has no entitled Base still settles and is flagged `ReviewReason = AskTeacherWithoutBase` for an admin refund decision (#102). The routine touches no repository; the caller adds a started subscription and saves once.
- **Fake gateway (default).** Checkout redirects to `/student/fake-checkout/{paymentId}`, a simulated Paymob page. Its success and failure buttons call `POST /api/subscriptions/payments/{paymentId}/fake-completion { succeeded }`, which settles exactly like the webhook (same `PaymentSettlement`) with transaction id `fake-<paymentId>`. Only a Pending payment can be completed (400 `PAYMENT_NOT_PENDING`); a success for a plan the student already holds extends it. In Production the fake refuses checkout (503) and fake completion (404 `FAKE_CHECKOUT_UNAVAILABLE`); with the Paymob provider fake completion is always 404.
- Both commands are audited (`Payment.StartCheckout`, `Payment.CompleteFake`).

## Webhook

Paymob's signed transaction callback is the thing that changes entitlement in production: `POST /api/payments/paymob/webhook?hmac=…` (anonymous, HMAC-verified, hidden from OpenAPI). The route, the HMAC rules, matching and binding are in `docs/paymob.md` §8. `ProcessPaymentNotification` is audited as `Payment.ProcessNotification` (system actor).

| Callback | Outcome (200 body `{ paymentId, outcome }`) |
|---|---|
| Not a transaction (e.g. `TOKEN`), or `pending`, refunded, voided or a child transaction | `Ignored`, no change (a non-transaction is not verified) |
| A transaction id already recorded on a payment | `Duplicate`, no change |
| Success for a Pending or Failed payment | `Succeeded`: settles through `PaymentSettlement.Succeed` |
| Failure for a Pending payment | `MarkedFailed` |
| Failure for a payment that is no longer Pending, or a new success for an already Succeeded payment | `OutOfOrder`, no change |

- **Idempotency** is keyed by the Paymob transaction id. Concurrent deliveries of one callback race on the `xmin` tokens of Payment and Subscription: the loser gets 409, Paymob retries, and the retry is a `Duplicate`.
- **Ordering.** A success always wins over an earlier decline (Failed → Succeeded); a decline after a success is ignored.
- **Times.** `CompletedAt` and a new subscription's start are the server receipt time, not Paymob's `created_at`, so a delayed callback never shortens paid time.
- **Known limit.** Two *different* payments for the same plan that settle within the same few milliseconds can both start a subscription (two overlapping rows). It needs two card forms completed at the same instant; admin review is #102.

## Lapse sweep

`SubscriptionLapseWorker` (a hosted service, the #81 worker pattern) wakes every `LapseSweepIntervalSeconds`, lists due subscriptions with `GetLapsedSubscriptionIds` (`SubscriptionLapseSpecification.DueAt(now, grace, excludedIds)`, the SQL twin of `Lapse`, oldest end first, `LapseSweepBatchSize` per batch), and sends `LapseSubscription` for each one in its own scope. A failing id is logged at Warning and left out of later batches until a sweep reaches the end of the backlog, so failures cannot starve the rest. `LapseSubscription` is audited as `Subscription.Lapse` (system actor).

Entitlement stays read-time, so the sweep is status bookkeeping for the UI and dashboards: Active past its end becomes PastDue (the 3-day grace), then Expired at end + grace, which is the downgrade to Free. `ExpiredAt` is the true lapse time even when the sweep runs late.

## Student cancel

`POST /api/subscriptions/{subscriptionId}/cancel` (owner-scoped; another student's subscription is 404 `SUBSCRIPTION_NOT_FOUND`) cancels a subscription that is still entitled (Active, or PastDue within grace); otherwise 400 `SUBSCRIPTION_ENDED`. It keeps paid time (`EntitledUntil = CurrentPeriodEnd`), does not cascade to Ask a Teacher (the read-time "requires Base" rule covers that) and returns the fresh `EntitlementResult`. Audited as `Subscription.Cancel`. Buying the plan again inside the renewal window resumes it.

## API

| Method | Route | Policy | Response |
|---|---|---|---|
| GET | `/api/plans` | anonymous | `PlanCatalogueResult`: `free` limits, `base` Avatar limit and prices ordered by months, `askTeacher` quota, SLA and its single monthly price |
| GET | `/api/subscriptions/entitlement` | `Subscription.Manage` (Student) | `EntitlementResult`: `tier` (`Free`/`Base`), `hasAskTeacher`, `canTakeExams`, the limits (`null` = unlimited) and the entitled `subscriptions` with `entitledUntil`, `inGracePeriod` (Active or PastDue with `currentPeriodEnd` ≤ now) and `canRenew` (checkout would accept this plan now) |
| GET | `/api/subscriptions/payments?pageNumber&pageSize` | `Subscription.Manage` (Student) | `PageData<PaymentResult>`; `422 PAYMENT_HISTORY_PAGE_NUMBER_INVALID` / `PAYMENT_HISTORY_PAGE_SIZE_INVALID` |
| POST | `/api/subscriptions/checkout` | `Subscription.Manage` (Student) | `CheckoutResult`; `400 CHECKOUT_PLAN_ALREADY_ACTIVE` (held plan outside the renewal window) / `CHECKOUT_REQUIRES_BASE`; `422` validation; `503 PAYMENT_GATEWAY_UNAVAILABLE` |
| GET | `/api/subscriptions/payments/{paymentId}` | `Subscription.Manage` (Student) | `PaymentResult` (Pending included); `404 PAYMENT_NOT_FOUND` |
| POST | `/api/subscriptions/payments/{paymentId}/fake-completion` | `Subscription.Manage` (Student) | `PaymentResult`; `400 PAYMENT_NOT_PENDING`; `404 PAYMENT_NOT_FOUND` / `FAKE_CHECKOUT_UNAVAILABLE`; `409` concurrency |
| POST | `/api/subscriptions/{subscriptionId}/cancel` | `Subscription.Manage` (Student) | `EntitlementResult`; `400 SUBSCRIPTION_ENDED`; `404 SUBSCRIPTION_NOT_FOUND`; `409 SUBSCRIPTION_MODIFIED_CONCURRENTLY` |
| POST | `/api/payments/paymob/webhook?hmac=` | anonymous (HMAC), not in OpenAPI | `PaymentNotificationResult`; `401 PAYMOB_WEBHOOK_SIGNATURE_INVALID`; `400 PAYMOB_WEBHOOK_PAYLOAD_INVALID` / `PAYMENT_NOTIFICATION_MISMATCH`; `404 PAYMENT_NOT_FOUND`; `409` concurrency or `PAYMENT_TRANSACTION_ALREADY_RECORDED` |

Enum values travel as PascalCase strings. The web page `/student/subscription` shows the subscribe header, the current plan, the Free / Base / Ask a Teacher plan cards and the paged payment log (hidden when empty). Each Base plan card has one subscribe button per configured period ("Subscribe monthly", "Subscribe for a term", "Subscribe yearly"); the Ask a Teacher card has one subscribe button, disabled with a visible hint while the student has no Base. An active plan shows its Active badge and no button until its renewal window opens (`canRenew`), then one Renew button per period ("Renew monthly", "Renew for a term", "Renew yearly"; "Renew" for Ask a Teacher). The Free card has none. Every subscribe or renew button is disabled while a checkout is starting. Each Active or PastDue line of the current-plan card has a Danger "Cancel" button that opens a confirm dialog ("You keep access until {date}"); confirming cancels, updates the card from the response and shows a toast. An Active plan past its period end (`inGracePeriod`) reads "period ended, available until {entitledUntil}".

## For later stories

- **Gates (#87 free tier, #94 Ask a Teacher quota)** call `StudentEntitlementLoader.LoadAsync(subscriptionRepository, studentId, options, now, cancellationToken)`. It is the one definition of entitlement and limits.
- **#100** (done): checkout, the Pending payment, the fake gateway and the result page (see Checkout).
- **#101** (done): the webhook, renewal, the lapse sweep and the student cancel (see above).
- **#102** adds `PaymentStatus.Refunded`, the refund and void callbacks (acknowledged and ignored today) and the admin payments page, which lists payments flagged with a `ReviewReason` and reviews the known overlap limit (see Webhook).
- **#106** grants complimentary plans with `Subscription.Start(..., paymobReference: null, ...)`.
