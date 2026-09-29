# Subscriptions, entitlement and payments

This is the contract for plans, subscriptions, entitlement and payments (PRD §11, story #99). Paymob checkout (#100), webhooks and the downgrade sweep (#101) and refunds (#102) build on it.

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

Placeholder prices ship in `appsettings.example.json` (and the test host): Base 19900 / 69900 / 179900 (199 / 699 / 1,799 EGP for 1 / 4 / 12 months) and Ask a Teacher 9900 (99 EGP a month). Final prices are still open (PRD §19 Q1); copy the section into your local `appsettings.json` and change the numbers.

## Money

Money is an integer number of minor units (piastres for EGP) plus an ISO 4217 code: `Money(AmountMinor, Currency)` in the domain, `{ "amountMinor": 19900, "currency": "EGP" }` on the wire (schema `Money`). No `decimal` or float is used for money. The web formats it with `formatMoney` (`shared/lib/format.ts`).

A `Payment` stores a snapshot of the amount it charged (`AmountMinor`, `Currency` columns). The price paid is never recomputed from configuration.

## Subscription lifecycle

A `Subscription` belongs to one student and one plan (`Base` or `AskTeacher`) with a billing period (`Monthly`, `Termly`, `Yearly`). States are `Active`, `PastDue`, `Cancelled` and `Expired`. Trialing is not used in v1.

| Transition | From | To | Rejected with |
|---|---|---|---|
| `Start(periodMonths, startsAt, …)` | — | Active, period `startsAt` → `startsAt + months` | `SUBSCRIPTION_PERIOD_INVALID` when months < 1 |
| `Renew(periodMonths, reference?)` | Active, PastDue | Active; the new period starts at the previous period end | `SUBSCRIPTION_ENDED` from Cancelled or Expired; `SUBSCRIPTION_PERIOD_INVALID` when months < 1 |
| `MarkPastDue()` | Active | PastDue | `SUBSCRIPTION_NOT_ACTIVE` from any other state |
| `Cancel(cancelledAt)` | Active, PastDue | Cancelled | `SUBSCRIPTION_ENDED` from Cancelled or Expired |
| `Expire(expiredAt)` | Active, PastDue, Cancelled | Expired | `SUBSCRIPTION_ALREADY_EXPIRED` from Expired |

`Renew` keeps the previous Paymob reference when none is given. Every rejected transition is a `400` business-rule error.

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

A `Payment` records one checkout: student, plan, period, amount snapshot, status and completion time. States are `Pending`, `Succeeded` and `Failed`; `MarkSucceeded` and `MarkFailed` only accept a Pending payment (`PAYMENT_NOT_PENDING`). `Payment.Create` rejects a non-positive amount or a currency that is not three upper-case letters (`PAYMENT_AMOUNT_INVALID`).

- `Payment.Id` is the Paymob `merchant_order_id`; no separate order column is needed.
- `SubscriptionId` is set on success. A first purchase has no subscription yet while the payment is Pending.
- `PaymobTransactionId` has a unique index filtered to non-null values (`IX_Payments_PaymobTransactionId`). It is the idempotency key #101 uses for webhook processing.
- The raw webhook body is kept as jsonb (`RawWebhook`).
- The student payment log lists **completed** payments only (Succeeded and Failed), newest first. A Pending payment is an abandoned or in-flight checkout and is not shown.

## API

| Method | Route | Policy | Response |
|---|---|---|---|
| GET | `/api/plans` | anonymous | `PlanCatalogueResult`: `free` limits, `base` Avatar limit and prices ordered by months, `askTeacher` quota, SLA and its single monthly price |
| GET | `/api/subscriptions/entitlement` | `Subscription.Manage` (Student) | `EntitlementResult`: `tier` (`Free`/`Base`), `hasAskTeacher`, `canTakeExams`, the limits (`null` = unlimited) and the entitled `subscriptions` with `entitledUntil` |
| GET | `/api/subscriptions/payments?pageNumber&pageSize` | `Subscription.Manage` (Student) | `PageData<PaymentResult>`; `422 PAYMENT_HISTORY_PAGE_NUMBER_INVALID` / `PAYMENT_HISTORY_PAGE_SIZE_INVALID` |

Enum values travel as PascalCase strings. The web page `/student/subscription` shows the subscribe header, the current plan, the Free / Base / Ask a Teacher plan cards and the paged payment log (hidden when empty). It has no subscribe or cancel buttons yet.

## For later stories

- **Gates (#87 free tier, #94 Ask a Teacher quota)** call `StudentEntitlementLoader.LoadAsync(subscriptionRepository, studentId, options, now, cancellationToken)`. It is the one definition of entitlement and limits.
- **#100** creates Pending payments at checkout and adds the subscribe actions to the page.
- **#101** adds the webhook transitions, the renewal and downgrade sweep, the student cancel, the `xmin` concurrency token on `Subscription` and the mapping of a duplicate transaction id.
- **#102** adds `PaymentStatus.Refunded` and the admin payments page.
- **#106** grants complimentary plans with `Subscription.Start(..., paymobReference: null, ...)`.
