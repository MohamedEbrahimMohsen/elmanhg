# Implementation (rework r2): Refunds and payment log (#102, E10.S4)

## Blocking findings addressed
| # | What I changed | File:line |
|---|---|---|
| 1 | Added `Post_SameIdempotencyKeyConcurrently_RefundsAndShortensSubscriptionOnce`. It seeds a renewed subscription (Start 1 month, then Renew 1 month, so the end is start + 2 months). The start time is truncated to whole seconds so it survives the Postgres round-trip. It fires two same-key refunds with `Task.WhenAll` from two separately signed-in admin `HttpClient`s. It asserts: at least one 200; every other status is 200 or 409; each 409 code is one of `PAYMENT_MODIFIED_CONCURRENTLY`, `PAYMENT_TRANSACTION_ALREADY_RECORDED` or `SUBSCRIPTION_MODIFIED_CONCURRENTLY`; the stored payment is Refunded with `fake-refund-{id:N}` and the request key; and the subscription is still Active with `CurrentPeriodEnd` moved back exactly one `PeriodMonths` (shortening it twice would expire it). The private helper `SeedRenewedAsync` is in the same file. | `api/Elmanhg.Tests/Integration/Payments/RefundPaymentEndpointTests.cs:54`, `:146` |
| 2 | Added `Handle_ReversalForUnknownPayment_ThrowsPaymentNotFound` (404 `PAYMENT_NOT_FOUND`), `Handle_ReversalAmountNotPositive_ThrowsMismatch` (amount 0; also asserts no review flag) and `Handle_ReversalProviderOrderDiffers_ThrowsMismatch` (payment linked to order `999999999`, notification carries `217503754`). Each test asserts the exception type and code, and that `SaveChangesAsync` was `DidNotReceive()`. `AddSucceeded` gained an optional `providerOrderId` that calls `LinkProviderOrder` before success. | `api/Elmanhg.Tests/Application/Features/Subscriptions/ProcessPaymentNotification/ProcessPaymentNotificationReversalTests.cs:148`, `:159`, `:172` |
| opt | `ReadRefundAsync` now returns 400 `PAYMENT_REFUND_DECLINED` as soon as `success` is `false`, whatever the id. A missing id with `success` not false stays 503. After that, the `success != true` / `pending` check is unchanged. Added the test `RefundAsync_NotSuccessfulWithoutId_ThrowsRefundDeclined`. Updated the error table in `docs/paymob.md` §9. | `api/Elmanhg.Infrastructure/Payments/Paymob/PaymobPaymentGateway.Refund.cs:70`, `api/Elmanhg.Tests/Infrastructure/Payments/PaymobPaymentGatewayRefundTests.cs:60`, `docs/paymob.md:131-133` |

## Files created
None.

## Files modified
| Path | Change |
|---|---|
| api/Elmanhg.Tests/Integration/Payments/RefundPaymentEndpointTests.cs | Concurrent same-key test and `SeedRenewedAsync` helper |
| api/Elmanhg.Tests/Application/Features/Subscriptions/ProcessPaymentNotification/ProcessPaymentNotificationReversalTests.cs | 3 throw-path tests; `AddSucceeded(providerOrderId)` |
| api/Elmanhg.Tests/Infrastructure/Payments/PaymobPaymentGatewayRefundTests.cs | `RefundAsync_NotSuccessfulWithoutId_ThrowsRefundDeclined` |
| api/Elmanhg.Infrastructure/Payments/Paymob/PaymobPaymentGateway.Refund.cs | `success == false` gives declined before the id check |
| docs/paymob.md | §9 error mapping table: 3 rows reflect the new order |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Review finding 1: the loser gets 409 `PAYMENT_MODIFIED_CONCURRENTLY` or `PAYMENT_TRANSACTION_ALREADY_RECORDED` | The handler mutates the payment and the subscription in one save. Both carry `xmin`, and `AppDbContext` maps a subscription concurrency failure to `SUBSCRIPTION_MODIFIED_CONCURRENTLY`. Which code the loser gets depends on which UPDATE fails first. | I also accept `SUBSCRIPTION_MODIFIED_CONCURRENTLY`. All three codes are safe 409s with nothing saved. |

## Mutation check
All four mutations were applied together, and then the three affected test classes were run:
1. `PaymentRefundSettlement` revokes twice.
2. In `ReverseAsync`, a missing payment returns `Ignored` instead of throwing 404.
3. `ReverseAsync` drops `<= 0` and replaces `IsBound` with a currency-only check.
4. The gateway `success == false` check only fires when an id is present.

Result: `total: 32, failed: 5, succeeded: 27`. The 5 failures were exactly the 5 new tests. I restored all files from backups and confirmed the restored contents.

A separate mutation that removed the `Payment`/`Subscription` concurrency and unique-violation mappings in `AppDbContext` (to show the loser would become a 500) was blocked by the auto-mode permission classifier and was not run. `AppDbContext.cs` is byte-identical to its pre-attempt state (diff checked).

## Build & test
- `api/Elmanhg.Api/appsettings.json` moved aside to the scratchpad, then restored.
- Targeted run (3 classes) after restoring the mutations: `Test run summary: Passed! total: 32 failed: 0 succeeded: 32`.
- `dotnet test D:/Personal/elmanhg/api/ -c Release`: `Test run summary: Passed! total: 2416 failed: 0 succeeded: 2416 skipped: 0`.
- `dotnet build D:/Personal/elmanhg/api/ -c Release`: `Build succeeded. 0 Warning(s)`.
- web/ and ai/ were not touched and not run.

## Notes for review
- The concurrent test accepts {200, 200}, because the second request may arrive after the first commits and be served as a replay. With the fast fake gateway, I did not observe whether a real 409 race occurs on every run. The assertions hold in both cases.
- `docs/subscriptions.md:132` ("`success` not true … → 400") still agrees with the new behaviour, so it was left as is.
- `PROGRESS.md` and `scripts/` were not touched. Nothing was committed.
