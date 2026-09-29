VERDICT: CHANGES_REQUESTED

# Review — Refunds and payment log (#102, E10.S4)

## Blocking

### 1. No test covers concurrent refunds with the same key, although Decision 6 relies on that behaviour
**Where:** `api/Elmanhg.Tests/Integration/Payments/RefundPaymentEndpointTests.cs` (no such test). The contract under test is `api/Elmanhg.Application/Payments/RefundPayment/RefundPaymentHandler.cs:24-34`.
**Rule:** plan Decision 6 ("concurrent same-key calls race on `xmin` (409 `PAYMENT_MODIFIED_CONCURRENTLY`)"), `docs/subscriptions.md` → Refunds → Idempotency, the orchestrator brief ("test concurrent double-submits"), and review order #5/#6.
**Problem:** The only idempotency test (`Post_SameIdempotencyKeyTwice_ReplaysSameResult`) sends the two requests one after the other. The case this story cares about most is two requests with the same key in flight at once, both past `IsRefundReplay` and both calling the gateway. It is documented but never exercised. Nothing proves the loser gets 409 instead of 500, or that the subscription is shortened only once, or that only one refund is recorded.
**Failure:** Two kinds of regression would leave the whole suite green: (a) the refund is saved in two `SaveChangesAsync` calls, or the subscription is loaded untracked and updated separately, so it is shortened twice; (b) the `xmin` or unique-index mapping for `Payment` changes, so the loser returns 500. Today, two parallel `POST /api/payments/{id}/refund` with the same `Idempotency-Key` have no asserted outcome.
**Fix:** Add an integration test that fires two same-key refunds with `Task.WhenAll`, using separate `HttpClient`s against one seeded payment. Assert:
- the status pair is {200, 200} or {200, 409 `PAYMENT_MODIFIED_CONCURRENTLY`/`PAYMENT_TRANSACTION_ALREADY_RECORDED`};
- the stored payment is Refunded with one `RefundTransactionId`;
- `CurrentPeriodEnd` moved back exactly `PeriodMonths` once, or the subscription is Expired at a single `ExpiredAt`.

Seed a renewed (2-month) subscription so that shortening it twice is visible as a date.

### 2. The new throw paths in `ReverseAsync` are not tested
**Where:** `api/Elmanhg.Application/Subscriptions/ProcessPaymentNotification/ProcessPaymentNotificationHandler.Reversal.cs:24` (`?? throw new NotFoundCoreException(PaymentNotFound)`) and `:25` (the `AmountMinor <= 0` arm and the provider-order arm of `IsBound`).
**Rule:** review order #6 (every `throw` in new code needs a test), testing convention (every throwing path: type, code, `SaveChangesAsync` `DidNotReceive()`), and plan Decision 8 (amount ≤ 0 and provider-order mismatch → 400).
**Problem:** `ProcessPaymentNotificationReversalTests` covers the "above amount" and "currency" mismatches only. Nothing tests:
- an unmatched reversal (404, which makes Paymob retry);
- a zero or negative amount;
- a reversal whose `ProviderOrderId` differs.
**Failure:** Suppose line 24 changes to return `Ignored`, or line 25 loses `<= 0`. The suite stays green. In the first case, a refund callback that arrives before its payment is visible (or for an unknown payment) gets 200 and is never retried, so the reversal is silently lost. In the second, a signed 0-amount child flags a payment for review instead of being rejected.
**Fix:** Add `Handle_ReversalForUnknownPayment_ThrowsPaymentNotFound`, `Handle_ReversalAmountNotPositive_ThrowsMismatch` (0) and `Handle_ReversalProviderOrderDiffers_ThrowsMismatch` (payment with `LinkProviderOrder`, notification with another order id). Each asserts the exception type and code, and `SaveChangesAsync` `DidNotReceive()`.

## Non-blocking
- **The ordering question** (`api/Elmanhg.Infrastructure/Payments/Paymob/PaymobPaymentGateway.Refund.cs:76-86`). The current order ("missing id → 503" before "declined → 400") matches plan item #20 and `docs/paymob.md` §9, and it is money-safe: neither branch saves anything, and a real refund that comes back without an id is recovered by the signed callback. I accept it. The better order separates the two cases:
  - `success == false` means Paymob did nothing, so it should be 400 `PAYMENT_REFUND_DECLINED` whether or not an id came back.
  - `success == true` with no id stays 503.

  If you change it, update the §9 error table in the same change.
- `PaymobPaymentGateway.Refund.cs:82`: a `pending: true` response is reported to the admin as "Paymob declined the refund". The refund may in fact be in progress and finish through the callback. The wording invites a second manual refund in the Paymob dashboard (Paymob caps the total, so this is not a double refund, but the message is misleading). Consider a separate code or message later.
- `PaymobPaymentGateway.Refund.cs:46-50`: a 401 or 403 from Paymob, for example a wrong `SecretKey`, is reported as a business "declined" (400, Warning). A configuration fault then reads as a merchant decision. Consider mapping 401 and 403 to 503 at Error level.
- `RefundPaymentHandler.cs:32-34`: after Paymob confirms the refund, the subscription load and the save still use the request cancellation token. If the admin closes the tab at that moment, the local record is lost until the signed callback arrives. The design accepts this (Decision 4 and 8 safety net). Passing `CancellationToken.None` after the irreversible call would close the gap.
- `ProcessPaymentNotificationHandler.Reversal.cs:43-47`: partial reversals are not idempotent. Nothing records the child id. A retried or replayed signed partial callback calls `FlagForReview` again, which clears an admin "Keep payment" resolution and reopens the review. On a payment already flagged `AskTeacherWithoutBase`, it also overwrites the reason. No money or entitlement effect. Consider a follow-up that records partial child ids.
- Two partial refunds in Paymob that add up to the full amount leave the payment Succeeded with access. The product has no action that revokes access without a Paymob call. Out of scope per the plan ("a human decides"), but worth listing in the follow-up.
- Edge case: suppose a parent charge callback arrives late with `is_refunded: true` while the payment is still Pending (the webhook was down, then a dashboard refund happened). It is classified `Other` and ignored, and every child reversal then gets 409 `PAYMENT_NOT_SETTLED`, so Paymob retries forever. The money is correct (no access, refunded), but the payment stays Pending. Worth adding to the §5 verify list.
- `ResolvePaymentReviewEndpointTests`: no integration test asserts the `Payment.ResolveReview` audit row. The command is `IAuditableCommand` (`ResolvePaymentReviewCommand.cs:7`), so this is covered by construction. The Teacher 403 on `review-resolution` is covered only by the permission-matrix row.
- Repo-wide, not new: `pageNumber` has no upper bound (`GetPaymentLogValidator.cs:15`). `int.MaxValue` overflows `(pageNumber - 1) * pageSize` in `Repository.FindPaginatedAsync`, which gives a negative OFFSET and a 500. It is the same in every paged validator.
- Momenta §8 deviations are deliberate and documented (plan Decision 6, `docs/subscriptions.md` → Refunds):
  - there is no idempotency table, and the key is scoped per payment;
  - a missing key returns 422, not 400;
  - there is no `Idempotent-Replayed` header, no in-progress 409, and no body-hash check.

  They are acceptable given the single state transition, and Paymob caps refunds at the captured amount.

## Verified
- **Build and tests, run myself:**
  - `dotnet test -c Release` in `api/` with `appsettings.json` moved aside: Passed, 2411/2411; the file was restored afterwards.
  - web `npm run typecheck`: clean.
  - web `npm run lint`: clean.
  - `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto`: all files pass.
  - `npm test -- --run`: 119 files, 736 tests passed.
- **Access:** `Payments.Manage` is Admin-only (`PermissionMatrixPolicies.cs:29`, `DefaultCodes.cs:22`), and all three actions carry it (`PaymentsController.cs:22,31,40`). Tests confirm 403 for Teacher on the log and on refund, and 403 for Student on the log and on resolve. The matrix rows were added. The web route sits under the `/admin` requireRole guard, and the nav item is capability-gated.
- **Signed-only classification:** `KindOf` (`PaymobNotificationReader.cs:78-84`) reads only `has_parent_transaction`, `is_capture`, `is_refunded` and `is_voided`. All four are in `PaymobHmac.TransactionFields`. `is_refund` and `is_void` are never looked up; they appear only in the WHY comment the plan prescribed. The tamper test and the unsigned-flag tests (unit and integration) pass.
- **Entitlement effect:**
  - `RevokePaidPeriod` matches the plan code exactly.
  - Revocation runs on the subscription of the payment itself, in the same save as `MarkRefunded`.
  - A refunded first purchase expires at once with no grace (unit test plus integration test: tier Free).
  - A renewal returns the subscription to the previous end.
  - A Cancelled subscription with time left stays Cancelled.
  - An Expired subscription is untouched.
  - A later success for a Refunded payment is `OutOfOrder`, and `MarkSucceeded` refuses Refunded.
- **Idempotency, sequential:**
  - The replay (same key, already Refunded) returns before the gateway call and before any save.
  - A different key returns `PAYMENT_ALREADY_REFUNDED`.
  - A missing or empty key returns 422.
  - The web dialog generates one UUID per dialog mount and reuses it on retries.
- **Reversals:** each outcome in Decision 8 is implemented in the order the plan gives. A replayed full refund cannot be re-aimed at another payment: its id is already recorded, so it returns `Duplicate`.
- **Payment log:**
  - The filter is an EF expression over captured locals, so it is parameterised.
  - `from`/`to` are converted to UTC; `from` is inclusive and `to` exclusive; the validator requires From < To.
  - Page size is capped at `AdminPaymentLogMaxPageSize`.
  - Order is `CreationDate` desc, then `Id` desc.
  - Students are batch-loaded with a single query (no N+1).
  - The reference is trimmed and length-capped.
- **Logging and PII:** the adapter logs only `PaymentId` and the HTTP status. No bodies, keys or student data are logged. Declines are logged at Warning and outages at Error. `RawWebhook` stays excluded from audit.
- **Audit:** both commands implement `IAuditableCommand` (`Payment.Refund`, `Payment.ResolveReview`). The integration test asserts the refund row and that its diff contains `refundReason`.
- **Migration:** `20260929034314_AddPaymentRefunds` is additive: 7 nullable columns and 3 indexes; drops occur only in `Down`. It is listed in `AppDbContextTests`. The unique-violation catch covers `IX_Payments_RefundTransactionId`, and a test confirms it.
- **Contract fidelity:**
  - Every file in the plan "Files to create" list exists, and nothing extra was created.
  - The signatures match the plan.
  - 15 resx keys in each language, plus 15 web `errors.*` keys.
  - The config keys appear in the options (with ranges), `appsettings.example.json` and `ApiFactory`.
  - OpenAPI declares `Idempotency-Key` on `RefundPayment`, and Orval runs with `headers: true`.
- **Deviations:** all five claims are true and harmless. I diffed `TextAreaField` against HEAD: the only difference is line endings.
- **Skill compliance:**
  - File-scoped namespaces.
  - Handlers, validators, records and results are sealed.
  - ConfigureAwait(false) on every await outside controllers.
  - DateTimeOffset only.
  - No try/catch in handlers.
  - A single `SaveChangesAsync` per handler.
  - `asNoTracking` on reads.
  - The guard grep over the diff is clean.
  - Controllers are unsealed, which mirrors every existing controller.
- **Web:** every visual value is an existing token or an established pattern, with no literal colours or sizes. The page has the three empty states, error with retry, skeleton, RTL and axe tests.
- **Postman:** the AdminPayments folder has 6 requests in state order, all with correct methods and URLs. Auth is inherited from the collection-level bearer. The refund requests send `Idempotency-Key: {{$guid}}`, and "Refund without key" omits it.
- **Docs:** checked PRD §11.2, §15, §16 and §17 rule 12; subscriptions.md; paymob.md §1, §5, §8 and §9; audit-log.md; claude-design-prompt.md §4 and §5 rule 11; and prototype.md. All of them agree with the code. No divergence found.

## Test quality
- `RefundPaymentHandlerTests`: strong. It uses a real domain payment and subscription, checks the exact gateway request, and asserts the resulting state. The replay test would fail if the short-circuit were removed.
- `ProcessPaymentNotificationReversalTests`: strong on the paths it covers. It is missing the throw paths in finding 2.
- `PaymobPaymentGatewayRefundTests`: strong. The stub handler checks the URI, auth, user agent and body shape, and every error mapping is covered.
- `PaymobNotificationReaderTests`: strong. The tamper and unsigned-flag tests prove the tests really are signature-based.
- `GetPaymentLogFilterTests` compile the real expression, including the date boundaries. The validator tests fail one rule per case.
- `GetPaymentLogHandlerTests`: adequate. It does not assert the paging arguments or the order passed to the repository; integration test 89 covers the order.
- Domain `PaymentTests` and `SubscriptionTests`: strong, with every branch covered.
- Integration refund, log, resolve and webhook tests assert both HTTP and DB state, plus the entitlement tier. The gap is concurrency (finding 1).
- Web `PaymentLogPage*.test.tsx`: constraining. They assert the request query, the body, the UUID header, the refetched row state and in-dialog errors.
