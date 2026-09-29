VERDICT: APPROVED

# Review r2 — Refunds and payment log (#102, E10.S4)

## Blocking
None.

## Round-1 findings
- **#1 (concurrent same-key refunds) — fixed.** `api/Elmanhg.Tests/Integration/Payments/RefundPaymentEndpointTests.cs:54` fires two same-key refunds with `Task.WhenAll` from two separate admin clients against a renewed (2-month) subscription, then asserts statuses in {200, 409 with a known code}, the stored payment is Refunded with one refund id and the request key, and the subscription is Active at exactly end minus `PeriodMonths`.
- **#2 (Reversal throw paths) — fixed.** `ProcessPaymentNotificationReversalTests.cs:148,159,172` cover an unknown payment (404 `PAYMENT_NOT_FOUND`), amount 0 and a provider-order mismatch (400 `PAYMENT_NOTIFICATION_MISMATCH`). Each asserts the exception type and code, and `SaveChangesAsync` `DidNotReceive()`. The unknown-payment case goes through `FindPaymentAsync` with a fresh Guid, so it reaches `ProcessPaymentNotificationHandler.Reversal.cs:24`. The order case reaches the `ProviderOrderId` arm of `IsBound` (`ProcessPaymentNotificationHandler.cs:70`).

## Mutation checks (run by me; every file restored and verified byte-identical with `cmp`)
| Mutation | Result |
|---|---|
| Removed the `Payment` and `Subscription` `DbUpdateConcurrencyException` mappings (`AppDbContext.cs:77-84`) | The concurrent test **failed**, in 2 of 2 runs: the loser returned 500. The race is real: both requests pass `IsRefundReplay`, and the loser fails on `xmin`. |
| Removed only the `Subscription` mapping (`AppDbContext.cs:81-84`) | The test passed. So the observed loser code is `PAYMENT_MODIFIED_CONCURRENTLY`, which matches `docs/subscriptions.md` → Refunds → Idempotency. |
| `PaymentRefundSettlement.Apply` revokes twice (the second refund also shortens) | The concurrent test **failed**: it found Expired at the refund time, not Active at end − 1 month. |
| Removed the `IsRefundReplay` short-circuit (`RefundPaymentHandler.cs:24-27`) | The concurrent test passed, because the race means neither request reaches the replay. `Post_SameIdempotencyKeyTwice_ReplaysSameResult` **failed** (200, 400). Between them, the two tests cover the guard. |
| Turned off the `xmin` concurrency token on Payment and Subscription | I could not run this. `PendingModelChangesWarning` stops the host at migrate, so the mutant cannot run. |

## Gateway ordering and docs
`PaymobPaymentGateway.Refund.cs:70-92`:
- `success == false` → 400 `PAYMENT_REFUND_DECLINED`, whether or not an id comes back.
- A missing or unreadable id, or a JSON `null` body, → 503.
- `success` missing/not true, or `pending` true → 400.

The `docs/paymob.md` §9 error table gives the same answer row by row. `docs/subscriptions.md:132` ("`success` not true … → 400") still agrees. `RefundAsync_NotSuccessfulWithoutId_ThrowsRefundDeclined` (`PaymobPaymentGatewayRefundTests.cs:60`) would fail under the old order, where it returns 503. `RefundAsync_MissingId_ThrowsPaymentGatewayUnavailable` still pins the 503 arm.

## Non-blocking
- `RefundPaymentEndpointTests.cs:73`: the test also accepts `SUBSCRIPTION_MODIFIED_CONCURRENTLY`. I never observed that code (see the mutation table), and the docs name only `PAYMENT_MODIFIED_CONCURRENTLY` for this race. The tolerance is harmless. Narrowing it would pin the documented contract.
- With the instant fake gateway, the concurrent test always races, so the "second request arrives after the first commits and replays" branch of the {200, 200} outcome is exercised only by the sequential test.

## Verified
- `dotnet test D:/Personal/elmanhg/api/ -c Release` with `appsettings.json` moved aside: Passed, 2416/2416. I ran it before the mutations and again after restoring them. `appsettings.json` has been restored.
- The r2 report claims (files touched, the new test names, the §9 table update, the 2416 green count) all match the working tree. The rework created no new production files.
- Style in the new tests: sealed class, file-scoped namespace, `ConfigureAwait(false)` in helpers, `DateTimeOffset` only, and exact names from the finding fixes.

## Test quality
- `RefundPaymentEndpointTests` now constrains the concurrent path. It fails on a 500 loser and on double revocation.
- `ProcessPaymentNotificationReversalTests` covers every throw in `ReverseAsync`.
- `PaymobPaymentGatewayRefundTests` pins both sides of the reordered branch.
