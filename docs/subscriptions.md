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
| `DailyQuotaTimeZone` | `Africa/Cairo` | IANA zone whose calendar day the Free daily quotas reset on. Validated at startup. |
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
| `AdminPaymentLogMaxPageSize` | `100` | Largest page size of the admin payment log (1–100). |
| `RefundReasonMaxLength` | `500` | Longest refund reason an admin can enter (1–2000). |
| `PaymentLogReferenceMaxLength` | `100` | Longest `reference` filter of the admin payment log (1–100). |

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
- **Cancel keeps paid time.** A Cancelled subscription keeps access until `CurrentPeriodEnd` and then lapses. PRD §17 rule 12 lets only Paymob-verified events (webhooks, or Paymob's response to an admin refund) change entitlement, so a cancel must not revoke time already paid for. The prototype revokes access at once; the prototype is a simulation and the PRD wins.

The resolved entitlement (`EntitlementResult`) carries the limits: Free gets the Free quotas and `canTakeExams: false`; Base gets unlimited quizzes and open lessons (`null`), the Base Avatar limit and exams. `monthlyAskTeacherQuestionLimit` is the Ask a Teacher quota when the add-on counts, else 0.

## Payments

A `Payment` records one checkout: student, plan, period, amount snapshot, period-length snapshot, status and completion time. States are `Pending`, `Succeeded`, `Failed` and `Refunded`. `MarkFailed` only accepts a Pending payment; `MarkSucceeded` accepts Pending **or Failed** (one Paymob order can carry a declined attempt and then an approved one, and captured money always wins); both refuse otherwise with `PAYMENT_NOT_PENDING`, so a Refunded payment can never be settled again. `MarkRefunded(refundTransactionId, refundedAt, refundedBy?, reason?, idempotencyKey?)` is the only way into Refunded; it calls `EnsureRefundable()`, which refuses a Refunded payment with `PAYMENT_ALREADY_REFUNDED` and anything that is not a Succeeded payment with a Paymob transaction id with `PAYMENT_NOT_REFUNDABLE`. `Payment.Create(studentId, plan, period, periodMonths, amount)` rejects a non-positive amount or a currency that is not three upper-case letters (`PAYMENT_AMOUNT_INVALID`) and months < 1 (`SUBSCRIPTION_PERIOD_INVALID`).

- `PeriodMonths` is snapshotted at checkout, like the amount. Settlement never reads the price configuration, so a configuration change can never make a paid webhook unsettleable. The #101 migration backfilled existing rows (Monthly 1, Termly 4, Yearly 12).
- `ProviderOrderId` is the Paymob order id (`intention_order_id` of the Intention response), stored before the first save when the gateway returns one (the fake returns none). It has a non-unique index filtered to non-null values (`IX_Payments_ProviderOrderId`).
- `ReviewReason` (`AskTeacherWithoutBase`, `PartialRefundAtProvider`) flags a payment for an admin decision. `NeedsReview` (computed, not stored) is `ReviewReason != null && ReviewResolvedAt == null`. `ResolveReview(resolvedBy, resolvedAt)` closes an open review (`ReviewResolvedAt`, `ReviewResolvedBy`; otherwise 400 `PAYMENT_REVIEW_NOT_OPEN`); a refund closes it too, and `FlagForReview` reopens it (clears the resolution).
- Refund fields: `RefundedAt`, `RefundedBy` (the admin; null for a Paymob callback), `RefundReason` (text), `RefundTransactionId` (Paymob's refund transaction; unique filtered index `IX_Payments_RefundTransactionId`, a hit returns 409 `PAYMENT_TRANSACTION_ALREADY_RECORDED`) and `RefundIdempotencyKey`.
- `Payment` carries an `xmin` concurrency token (`Version`): a concurrent write returns 409 `PAYMENT_MODIFIED_CONCURRENTLY`.

- `Payment.Id` is the Paymob `merchant_order_id`; no separate order column is needed.
- `SubscriptionId` is set on success. A first purchase has no subscription yet while the payment is Pending.
- `PaymobTransactionId` has a unique index filtered to non-null values (`IX_Payments_PaymobTransactionId`). It is the webhook idempotency key; a unique-index hit returns 409 `PAYMENT_TRANSACTION_ALREADY_RECORDED`.
- The raw webhook body is kept in full as jsonb (`RawWebhook`) as dispute evidence. It carries billing PII, so it is excluded from audit diffs (the `Core:AuditExcluded` annotation, `docs/audit-log.md`).
- The student payment log lists **completed** payments only (Succeeded, Failed and Refunded), newest first; a Refunded payment shows a neutral "Refunded" badge, and the checkout result page shows "This payment was refunded". A Pending payment is an abandoned or in-flight checkout and is not shown.

## Checkout

A student buys a plan with `POST /api/subscriptions/checkout { plan, period }` (story #100). The gateway and its configuration are in `docs/paymob.md`.

- **What can be bought.** `StudentEntitlement.EnsureCanPurchase(plan, now, renewalWindow)`: Ask a Teacher without an entitled Base is refused with 400 `CHECKOUT_REQUIRES_BASE`. A plan the student already holds (an entitled subscription of that plan, including a Cancelled one still inside its paid period) is refused with 400 `CHECKOUT_PLAN_ALREADY_ACTIVE` **until the renewal window opens**: from `CurrentPeriodEnd − RenewalWindowDays` (default 7 days) the held plan can be bought again, in any period, and the payment renews it (see Settlement). There are no automatic charges in v1: a student renews by paying again.
- **Prices and periods** come from `SubscriptionsOptions.PriceFor(plan, period)`: Base sells the periods configured in `BasePrices`; Ask a Teacher sells `Monthly` only. Any other pair is 422 `CHECKOUT_PERIOD_UNAVAILABLE` (400 if the configuration changes between validation and handling). A missing or unknown plan or period is 422 `CHECKOUT_PLAN_REQUIRED` / `CHECKOUT_PLAN_INVALID` / `CHECKOUT_PERIOD_REQUIRED` / `CHECKOUT_PERIOD_INVALID`.
- **Pending payment first.** The handler creates the `Payment` at the configured price and months, asks the gateway for a redirect URL (and the Paymob order id, stored as `ProviderOrderId`), and only then saves the Pending payment. A gateway failure (503 `PAYMENT_GATEWAY_UNAVAILABLE`) saves nothing. The response is `CheckoutResult { paymentId, redirectUrl, amount }`.
- **Return page.** Paymob returns the browser to `{Payments:Paymob:RedirectionUrl}/{paymentId}` (`/student/checkout-result/{paymentId}`); any query string Paymob appends is ignored. The page only reads `GET /api/subscriptions/payments/{paymentId}` (owner-scoped, Pending included; another student's payment is 404 `PAYMENT_NOT_FOUND`). It polls every 2 s while the payment is Pending, for up to 60 s after the page opened, then shows "confirmation has not arrived yet" with a **Check again** button. The browser never activates a plan (PRD §17 rule 12).
- **Settlement.** `PaymentSettlement.Succeed(payment, entitlement, transactionId, rawNotification, grace, completedAt)` and `PaymentSettlement.Fail(payment, transactionId, rawNotification, completedAt)` are the one settle routine, shared by the webhook and the fake. `Succeed` **extends** a held plan: when the student holds an entitled subscription of the paid plan, the payment is marked Succeeded against it and the subscription is renewed (`Renew`, from the old end, in the paid period, resuming a Cancelled one); otherwise a new subscription starts at `completedAt` for `PeriodMonths`. The money was captured, so a double payment becomes more paid time; no refund is needed. An Ask a Teacher payment settled while the student has no entitled Base still settles and is flagged `ReviewReason = AskTeacherWithoutBase` for an admin refund decision (#102). The routine touches no repository; the caller adds a started subscription and saves once.
- **Fake gateway (default).** Checkout redirects to `/student/fake-checkout/{paymentId}`, a simulated Paymob page. Its success and failure buttons call `POST /api/subscriptions/payments/{paymentId}/fake-completion { succeeded }`, which settles exactly like the webhook (same `PaymentSettlement`) with transaction id `fake-<paymentId>`. Only a Pending payment can be completed (400 `PAYMENT_NOT_PENDING`); a success for a plan the student already holds extends it. Outside Development the fake refuses checkout (503) and fake completion (404 `FAKE_CHECKOUT_UNAVAILABLE`) unless `Payments:AllowFakePayments=true`; Production always refuses (docs/paymob.md §6); with the Paymob provider fake completion is always 404.
- Both commands are audited (`Payment.StartCheckout`, `Payment.CompleteFake`).

## Webhook

Paymob's signed transaction callback is the thing that changes entitlement in production: `POST /api/payments/paymob/webhook?hmac=…` (anonymous, HMAC-verified, hidden from OpenAPI). The route, the HMAC rules, matching and binding are in `docs/paymob.md` §8. `ProcessPaymentNotification` is audited as `Payment.ProcessNotification` (system actor).

| Callback | Outcome (200 body `{ paymentId, outcome }`) |
|---|---|
| Not a transaction (e.g. `TOKEN`), `pending`, or classified `Other` (see below) | `Ignored`, no change (a non-transaction is not verified) |
| A charge whose transaction id is already recorded on a payment | `Duplicate`, no change |
| Success for a Pending or Failed payment | `Succeeded`: settles through `PaymentSettlement.Succeed` |
| Failure for a Pending payment | `MarkedFailed` |
| Failure for a payment that is no longer Pending, or a new success for an already Succeeded or Refunded payment | `OutOfOrder`, no change |
| Reversal that failed or is pending | `Ignored`, no change |
| Reversal whose id is already a `RefundTransactionId`, or a reversal for a Refunded payment | `Duplicate`, no change |
| Reversal for a Pending payment | 409 `PAYMENT_NOT_SETTLED` (Paymob retries after the charge settles) |
| Reversal for a Failed payment | `OutOfOrder`, no change |
| Reversal amount ≤ 0 or above the payment amount, other currency, or other provider order | 400 `PAYMENT_NOTIFICATION_MISMATCH` |
| Partial reversal (less than the payment amount) of a Succeeded payment | `FlaggedForReview`: `ReviewReason = PartialRefundAtProvider`, no entitlement change |
| Full reversal of a Succeeded payment | `Refunded`: `PaymentRefundSettlement.Apply` (see Refunds), `RefundedBy` null |

- **Classification uses signed fields only.** `has_parent_transaction` true and `is_capture` false → `Reversal` (a refund or void child); `has_parent_transaction` true and `is_capture` true → `Other`; no parent but `is_refunded` or `is_voided` true → `Other` (the parent update; the child carries the reversal); otherwise `Charge`. All four fields are in the HMAC field set. The unsigned `is_refund` / `is_void` flags are never read, so they cannot be forged onto a replayed signed body.
- **Idempotency** is keyed by the Paymob transaction id (charges) and the refund transaction id (reversals). Concurrent deliveries of one callback race on the `xmin` tokens of Payment and Subscription: the loser gets 409, Paymob retries, and the retry is a `Duplicate`.
- **Ordering.** A success always wins over an earlier decline (Failed → Succeeded); a decline after a success is ignored.
- **Times.** `CompletedAt` and a new subscription's start are the server receipt time, not Paymob's `created_at`, so a delayed callback never shortens paid time.
- **Known limit.** Two *different* payments for the same plan that settle within the same few milliseconds can both start a subscription (two overlapping rows). It needs two card forms completed at the same instant. There is no automatic detection yet (it needs a per-student settlement lock); an admin can find the pair in the payment log and refund one (follow-up issue).

## Refunds

An admin refunds a Succeeded payment from the payment log: `POST /api/payments/{paymentId}/refund { reason }` with an `Idempotency-Key` header (policy `Payments.Manage`, Admin only). Audited as `Payment.Refund`; the diff shows the status, the refund fields, the review resolution and the subscription's new end and status.

- **Full amount only.** The request carries no amount; a partial refund made in the Paymob dashboard arrives as a callback and is flagged for review (see Webhook).
- **Reason required**, trimmed, at most `RefundReasonMaxLength` (500) characters; stored as text and shown in the log. Missing → 422 `PAYMENT_REFUND_REASON_REQUIRED`, too long → 422 `PAYMENT_REFUND_REASON_TOO_LONG`.
- **Effect on entitlement.** `PaymentRefundSettlement.Apply(payment, subscription?, refundTransactionId, refundedAt, refundedBy?, reason?, idempotencyKey?)` marks the payment Refunded and calls `Subscription.RevokePaidPeriod(payment.PeriodMonths, refundedAt)` on the payment's subscription. The end moves back by the payment's months; if the new end is at or before now, the subscription becomes Expired at `now` (no grace after a refund). Refunding a first purchase or a duplicate subscription ends access; refunding a renewal returns the student to the previously paid end. An already Expired subscription is untouched; a Cancelled one with time left is shortened and stays Cancelled. The month clamp of `AddMonths` (#191) can cost up to 3 days. The routine is shared with the webhook and touches no repository.
- **When it takes effect.** On Paymob's synchronous success response to the refund call (server to server, authenticated with our secret key), in the same save as the local record. The signed refund callback that follows is a `Duplicate`.
- **Idempotency.** The `Idempotency-Key` header (a non-empty UUID; missing → 422 `PAYMENT_REFUND_IDEMPOTENCY_KEY_REQUIRED`) is stored as `RefundIdempotencyKey`. The same key on an already Refunded payment replays the current result (200, no gateway call, no save); a different key → 400 `PAYMENT_ALREADY_REFUNDED`. There is no idempotency table and no in-progress 409: concurrent calls with the same key race on `xmin` (409 `PAYMENT_MODIFIED_CONCURRENTLY`) and Paymob refuses a second full refund.
- **Refusals.** A Pending or Failed payment → 400 `PAYMENT_NOT_REFUNDABLE`; unknown id → 404 `PAYMENT_NOT_FOUND`.
- **Provider outcomes.** Transport error, timeout, 5xx or 429 → 503 `PAYMENT_GATEWAY_UNAVAILABLE`; another 4xx, `success` not true or `pending` true → 400 `PAYMENT_REFUND_DECLINED`. Nothing is saved in either case. A pending refund that completes later arrives as a signed child callback and is applied then. The adapter is in `docs/paymob.md` §9; the fake returns `fake-refund-{paymentId:N}` and is locked like fake checkout (503, docs/paymob.md §6).

## Admin payment log

`GET /api/payments` (policy `Payments.Manage`) is the admin transaction log; the web page is `/admin/payments`.

- **Contents.** Every payment, Pending included, newest first (`CreationDate` desc, then `Id` desc), with offset paging `pageNumber` / `pageSize` (max `AdminPaymentLogMaxPageSize`, 100). Each item is an `AdminPaymentResult`: the payment fields, `studentName` (`DisplayName`) and `studentContact` (email, else phone), the review state (`reviewReason`, `needsReview`, `reviewResolvedAt`), the refund fields and `canRefund`.
- **Filters.** `status`, `plan`, `needsReview` (default false), `studentId`, `reference` (exact match on the payment id, Paymob transaction id, refund transaction id or provider order id; at most `PaymentLogReferenceMaxLength`), `from` (inclusive) and `to` (exclusive) on the creation time. Invalid values → 422 `PAYMENT_LOG_*`.
- **Review queue.** The log with `needsReview=true`: payments whose review is open. An admin closes a review by refunding or with "Keep payment": `POST /api/payments/{paymentId}/review-resolution` (audited `Payment.ResolveReview`; 400 `PAYMENT_REVIEW_NOT_OPEN` when none is open).
- **Student names** are batch-loaded for the page with one user query (no cross-aggregate join).

## Lapse sweep

`SubscriptionLapseWorker` (a hosted service, the #81 worker pattern) wakes every `LapseSweepIntervalSeconds`, lists due subscriptions with `GetLapsedSubscriptionIds` (`SubscriptionLapseSpecification.DueAt(now, grace, excludedIds)`, the SQL twin of `Lapse`, oldest end first, `LapseSweepBatchSize` per batch), and sends `LapseSubscription` for each one in its own scope. A failing id is logged at Warning and left out of later batches until a sweep reaches the end of the backlog, so failures cannot starve the rest. `LapseSubscription` is audited as `Subscription.Lapse` (system actor).

Entitlement stays read-time, so the sweep is status bookkeeping for the UI and dashboards: Active past its end becomes PastDue (the 3-day grace), then Expired at end + grace, which is the downgrade to Free. `ExpiredAt` is the true lapse time even when the sweep runs late.

## Student cancel

`POST /api/subscriptions/{subscriptionId}/cancel` (owner-scoped; another student's subscription is 404 `SUBSCRIPTION_NOT_FOUND`) cancels a subscription that is still entitled (Active, or PastDue within grace); otherwise 400 `SUBSCRIPTION_ENDED`. It keeps paid time (`EntitledUntil = CurrentPeriodEnd`), does not cascade to Ask a Teacher (the read-time "requires Base" rule covers that) and returns the fresh `EntitlementResult`. Audited as `Subscription.Cancel`. Buying the plan again inside the renewal window resumes it.

## Free tier gates

Every gate loads entitlement through `StudentEntitlementLoader.LoadAsync` before any write and throws 403 `ForbiddenCoreException` from `FreeTierGate` (`Application/Subscriptions/Shared`). A failed lookup throws and nothing is saved (fail closed). The limits come only from `SubscriptionsOptions` through `EntitlementResult`.

- **What counts toward the daily quiz quota:** new `Attempt` rows in non-test `Quiz` sessions whose `CreatedAt` falls on today's date in `DailyQuotaTimeZone` (`ISessionRepository.CountQuizAttemptsOnDayAsync`, SQL `AT TIME ZONE`). A replayed identical answer creates no row and does not count. Exam answers do not count.
- **Quiz start** (`POST /api/sessions/quiz`): a locked lesson returns `403 LESSON_LOCKED` (start and resume). A **new** session when used ≥ limit returns `403 QUIZ_DAILY_LIMIT_REACHED` (context `limit`). Resuming an open session is allowed; its answers are still gated. The size of a new session is not capped to the remaining quota.
- **Answer submit** (`POST /api/sessions/{id}/answers`): only a new attempt is gated — the lesson lock (`LESSON_LOCKED`, which covers a quiz started while subscribed and continued after a lapse) and then the quota (`QUIZ_DAILY_LIMIT_REACHED`). A replay and any test-mode session are not gated.
- **Lesson lock:** a Free student opens the first `FreeOpenLessonsPerUnit` Published lessons of each unit, ordered by `Order`, then `CreationDate`, then `Id` (`LessonAccess` in Domain). See `docs/browsing.md` → Free tier.
- **Avatar** (`POST /api/avatar/messages`): before the model is called, a student at the daily limit (Free 5, Base 50, counted on the `DailyQuotaTimeZone` day) gets `403 AVATAR_DAILY_LIMIT_REACHED` with context `limit`. A message counts only once the assistant has replied. See [avatar.md](avatar.md).
- **Exams:** a new unit exam or multi-unit exam needs Base (`403 EXAM_REQUIRES_SUBSCRIPTION`), checked before the lesson-opened gate. Resume, save and submit are not gated. See `docs/exams.md`.
- **Exempt:** Admins (role claim `Admin`, the same test that sets `IsTestMode`) on quiz start and exam start; any test-mode session on answer submit. Teachers cannot reach these endpoints (`Assessments.Take`). An unknown or missing role is gated.
- **Known limit:** the quota is a soft limit. Two answers sent in parallel at 9/10 can both pass; there is no lock.
- `GET /api/subscriptions/usage` returns today's count for the counters on Home, the practice tab and the quiz screen. The web opens a paywall dialog on each of the three codes; «اشترك» opens `/student/subscription`, «لاحقًا» closes it. A fake payment success refreshes the entitlement, usage, browse, mastery and exam views.

## Ask a Teacher quota

`AskTeacherGate` (`Application/TeacherThreads/Shared`) guards `POST /api/teacher-threads`, after the entitlement is loaded through `StudentEntitlementLoader`.

- **Gate order:** validation `422` → user `401` → no add-on `403 ASK_TEACHER_REQUIRES_SUBSCRIPTION` → quota `403 ASK_TEACHER_MONTHLY_LIMIT_REACHED` (context `limit`) → context `404`s → the photo is stored → one save. Nothing is stored when a check fails.
- **What counts:** threads the student submitted in the current calendar month of `DailyQuotaTimeZone` (Africa/Cairo); the month's bounds are converted to UTC. The quota resets on the 1st. A follow-up (#97) does not count. The limit is `AskTeacherMonthlyQuestions`, 0 without the add-on.
- **Known limit:** the quota is a soft limit, like the quiz quota. Two questions sent in parallel at 19/20 can both pass.
- See `docs/ask-teacher.md` for the thread model, context rules and photo access.

## API

| Method | Route | Policy | Response |
|---|---|---|---|
| GET | `/api/plans` | anonymous | `PlanCatalogueResult`: `free` limits, `base` Avatar limit and prices ordered by months, `askTeacher` quota, SLA and its single monthly price |
| GET | `/api/subscriptions/entitlement` | `Subscription.Manage` (Student) | `EntitlementResult`: `tier` (`Free`/`Base`), `hasAskTeacher`, `canTakeExams`, the limits (`null` = unlimited) and the entitled `subscriptions` with `entitledUntil`, `inGracePeriod` (Active or PastDue with `currentPeriodEnd` ≤ now) and `canRenew` (checkout would accept this plan now) |
| GET | `/api/subscriptions/usage` | `Subscription.Manage` (Student) | `UsageResult`: `tier`, `hasAskTeacher`, `dailyQuizQuestionLimit` (`null` = unlimited), `quizQuestionsUsedToday`, `quizQuestionsRemainingToday` (`null` = unlimited, never below 0), `dailyAvatarMessageLimit`, `monthlyAskTeacherQuestionLimit` (0 without the add-on), `askTeacherQuestionsUsedThisMonth` and `askTeacherQuestionsRemainingThisMonth` (never below 0) |
| GET | `/api/subscriptions/payments?pageNumber&pageSize` | `Subscription.Manage` (Student) | `PageData<PaymentResult>`; `422 PAYMENT_HISTORY_PAGE_NUMBER_INVALID` / `PAYMENT_HISTORY_PAGE_SIZE_INVALID` |
| POST | `/api/subscriptions/checkout` | `Subscription.Manage` (Student) | `CheckoutResult`; `400 CHECKOUT_PLAN_ALREADY_ACTIVE` (held plan outside the renewal window) / `CHECKOUT_REQUIRES_BASE`; `422` validation; `503 PAYMENT_GATEWAY_UNAVAILABLE` |
| GET | `/api/subscriptions/payments/{paymentId}` | `Subscription.Manage` (Student) | `PaymentResult` (Pending included); `404 PAYMENT_NOT_FOUND` |
| POST | `/api/subscriptions/payments/{paymentId}/fake-completion` | `Subscription.Manage` (Student) | `PaymentResult`; `400 PAYMENT_NOT_PENDING`; `404 PAYMENT_NOT_FOUND` / `FAKE_CHECKOUT_UNAVAILABLE`; `409` concurrency |
| POST | `/api/subscriptions/{subscriptionId}/cancel` | `Subscription.Manage` (Student) | `EntitlementResult`; `400 SUBSCRIPTION_ENDED`; `404 SUBSCRIPTION_NOT_FOUND`; `409 SUBSCRIPTION_MODIFIED_CONCURRENTLY` |
| GET | `/api/payments?status&plan&needsReview&studentId&reference&from&to&pageNumber&pageSize` | `Payments.Manage` (Admin) | `PageData<AdminPaymentResult>`; `422 PAYMENT_LOG_*` |
| POST | `/api/payments/{paymentId}/refund` + header `Idempotency-Key` | `Payments.Manage` (Admin) | `AdminPaymentResult`; `400 PAYMENT_ALREADY_REFUNDED` / `PAYMENT_NOT_REFUNDABLE` / `PAYMENT_REFUND_DECLINED`; `404 PAYMENT_NOT_FOUND`; `409` concurrency; `422` validation; `503 PAYMENT_GATEWAY_UNAVAILABLE` |
| POST | `/api/payments/{paymentId}/review-resolution` | `Payments.Manage` (Admin) | `AdminPaymentResult`; `400 PAYMENT_REVIEW_NOT_OPEN`; `404 PAYMENT_NOT_FOUND`; `409` concurrency |
| POST | `/api/payments/paymob/webhook?hmac=` | anonymous (HMAC), not in OpenAPI | `PaymentNotificationResult`; `401 PAYMOB_WEBHOOK_SIGNATURE_INVALID`; `400 PAYMOB_WEBHOOK_PAYLOAD_INVALID` / `PAYMENT_NOTIFICATION_MISMATCH`; `404 PAYMENT_NOT_FOUND`; `409` concurrency, `PAYMENT_TRANSACTION_ALREADY_RECORDED` or `PAYMENT_NOT_SETTLED` |

Enum values travel as PascalCase strings. The web page `/student/subscription` shows the subscribe header, the current plan, the Free / Base / Ask a Teacher plan cards and the paged payment log (hidden when empty). Each Base plan card has one subscribe button per configured period ("Subscribe monthly", "Subscribe for a term", "Subscribe yearly"); the Ask a Teacher card has one subscribe button, disabled with a visible hint while the student has no Base. An active plan shows its Active badge and no button until its renewal window opens (`canRenew`), then one Renew button per period ("Renew monthly", "Renew for a term", "Renew yearly"; "Renew" for Ask a Teacher). The Free card has none. Every subscribe or renew button is disabled while a checkout is starting. Each Active or PastDue line of the current-plan card has a Danger "Cancel" button that opens a confirm dialog ("You keep access until {date}"); confirming cancels, updates the card from the response and shows a toast. An Active plan past its period end (`inGracePeriod`) reads "period ended, available until {entitledUntil}".

## For later stories

- **Gates** call `StudentEntitlementLoader.LoadAsync(subscriptionRepository, studentId, options, now, cancellationToken)`. It is the one definition of entitlement and limits. **#87** (done): the Free tier gates above. **#91** (done) enforces the Avatar quota through the same loader, and its counters are served by `GET /api/avatar/status` ([avatar.md](avatar.md)); `UsageResult.dailyAvatarMessageLimit` still exposes the limit. **#94** (done): Ask a Teacher counters in `UsageResult` and the create gate (`AskTeacherGate`).
- **#100** (done): checkout, the Pending payment, the fake gateway and the result page (see Checkout).
- **#101** (done): the webhook, renewal, the lapse sweep and the student cancel (see above).
- **#102** (done): `PaymentStatus.Refunded`, admin refunds, signed refund and void callbacks, and the admin payment log with its review queue (see Refunds and Admin payment log). Automatic detection of overlapping subscriptions (the known limit under Webhook) is a follow-up issue.
- **#106** grants complimentary plans with `Subscription.Start(..., paymobReference: null, ...)`.
