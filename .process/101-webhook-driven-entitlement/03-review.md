VERDICT: CHANGES_REQUESTED

# Review — [E10.S3] Webhook-driven entitlement (#101)

## Blocking

### 1. docs/paymob.md §8 says malformed JSON returns `PAYMOB_WEBHOOK_PAYLOAD_INVALID`; the endpoint returns the MVC default uncoded 400
**Where:** `api/Elmanhg.Api/Controllers/Payments/PaymobWebhooksController.cs:19` (`[FromBody] JsonElement payload`) vs `docs/paymob.md` §8 "Transaction webhook" → **Status codes** ("400 `PAYMOB_WEBHOOK_PAYLOAD_INVALID` (malformed JSON, no `obj`, …)").
**Rule:** `.claude/rules/docs-sync.md` (divergence: a status-code/error-code contract); Review order #8.
**Problem:** `[ApiController]` model binding rejects unparseable JSON before the mediator runs. The repo has no `InvalidModelStateResponseFactory`, so the response is a default `ValidationProblemDetails` with no `code`. The `JsonException` branch of the reader (`PaymobNotificationReader.cs:58-61`) cannot be reached over HTTP. It is only covered by the unit test. The doc and the code give two different answers to "what does the webhook return for a malformed body?". The implementer flagged the behaviour in `02-implementation.md` → "Notes for review", but left the doc unchanged.
**Failure:** `POST /api/payments/paymob/webhook?hmac=x` with body `{"type":` returns 400 with no `code` member (and writes no audit row). The doc promises `code: "PAYMOB_WEBHOOK_PAYLOAD_INVALID"`.
**Fix:** The smallest fix is to reword the §8 status line: malformed JSON (or an empty or non-JSON body) is rejected by the framework with a plain 400 or 415 before verification. `PAYMOB_WEBHOOK_PAYLOAD_INVALID` covers well-formed JSON with a bad shape. The alternative is to read the raw body as a string (for example `Request.Body` via `StreamReader`) and pass it to the reader, so the coded branch becomes live. Either way, the doc and the code must agree.
**Judgement on whether it matters:** Functionally, no. Nothing changes state, nothing leaks, and Paymob never sends malformed JSON. The body is also consistent with every other `[FromBody]` endpoint in the repo. The only defect is the doc divergence. The missing `MALFORMED_REQUEST` code, which the momenta contract §3 asks for, is a repo-wide gap and not this story (see Non-blocking).

## Non-blocking
- `api/Elmanhg.Application/Subscriptions/ProcessPaymentNotification/ProcessPaymentNotificationHandler.cs:29`: the `payment.Currency != notification.Currency` term of the binding has no test in the handler or endpoint suites. Removing it breaks nothing. Add `Handle_CurrencyDiffers_ThrowsMismatch`.
- `api/Elmanhg.Api/Controllers/Payments/PaymobWebhooksController.cs:17`: the endpoint is anonymous and has no rate limit, as the plan defers this to #115. Every unsigned POST still costs a JSON parse, an HMAC and one audit-row insert (a Failure row). Unauthenticated callers can therefore amplify writes into `AuditLogs`. Make sure #115 covers this endpoint, for example with the existing `AddAuthRateLimiting` partitioned by IP.
- `api/Elmanhg.Application/Subscriptions/CancelSubscription/CancelSubscriptionHandler.cs:27` + `web/src/features/subscription/components/CancelSubscriptionDialog.tsx:23`: cancelling an Active-in-grace or PastDue plan sets `EntitledUntil = CurrentPeriodEnd`, which is already past. Access therefore ends immediately and the student drops to Free. Meanwhile the dialog says "You keep access until {date}", with that past date. This is consistent with Decision 17 ("keeps paid time"), but the wording misleads in grace. Consider hiding Cancel while `inGracePeriod`, or changing the copy.
- `api/Elmanhg.Domain/Subscriptions/Subscription.Lifecycle.cs:18`: `CurrentPeriodEnd.AddMonths(n)` clamps the day of the month, so a Jan 31 start renews monthly as Feb 28, Mar 28, and so on. The student permanently loses up to 3 days. This was already true before this story, but chained renewals now make it visible. Decide whether to anchor on the original day.
- `api/Elmanhg.Domain/Subscriptions/Payment.cs:60-62`: `MarkSucceeded` from Failed overwrites `RawWebhook` and `PaymobTransactionId`, so the evidence of the declined attempt is lost. This is fine for v1. #102 may want an attempts log.
- `docs/PRD.md:472`: the data-model summary line for `Payment` omits `period_months`, `provider_order_id` and `review_reason`. It defers to `docs/subscriptions.md`, which is updated. Consider syncing the summary.
- The momenta contract §3 asks for `MALFORMED_REQUEST` on unparseable JSON. The repo has no `InvalidModelStateResponseFactory` anywhere, so every `[FromBody]` endpoint returns an uncoded 400. This predates the story and belongs in a follow-up issue.
- `PaymobNotificationReader` trusts the unsigned `is_refund`/`is_void`. Adding them only turns a replayed signed body into `Ignored`, which is harmless. Noted for the #102 refund work.

## Verified
- **Build and tests (run by the reviewer):**
  - `dotnet test api/ -c Release` with `appsettings.json` moved aside: total 2293, failed 0. The file was restored.
  - Web `typecheck`: clean.
  - Web `lint`: clean.
  - Web `test -- --run`: 113 files, 706 tests passed.
  - `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto`: clean.
- **HMAC:**
  - The field list and order in `PaymobHmac.cs:10` match Decision 6.
  - Value formatting matches: string value, `true`/`false`, raw number text, empty otherwise.
  - The digest is `HMACSHA512` lower-case hex.
  - The comparison is `CryptographicOperations.FixedTimeEquals` over UTF-8 bytes, with the provided signature trimmed and lower-cased.
  - It fails closed on a blank secret or signature (`PaymobHmac.cs:20`).
  - `HmacSecret` is required at startup with Paymob (`PaymentsOptionsValidator.cs:26`).
  - There is no logger in the reader or the handler, and nothing logs the secret or the body.
- **Known-answer digest:** recomputed independently with the Python script from the plan. The result is `7c2317...04a0cd`, identical to `PaymobHmacTests.ExpectedDigest`.
- **Tampering:**
  - Amount, currency and (when set) `ProviderOrderId` are bound to the signed `amount_cents`, `currency` and `order.id` (`ProcessPaymentNotificationHandler.cs:29`).
  - The unsigned `merchant_order_id` is only a lookup key.
  - A replayed signed transaction redirected at another payment is stopped in one of three ways: the Duplicate check (step 3), the binding, or the unique `IX_Payments_PaymobTransactionId` index, which maps to 409 (`AppDbContext.cs:83`).
- **Idempotency and races:**
  - The Duplicate check runs before the tracked load.
  - `xmin` is on both Payment and Subscription (`AppDbContext.cs:314,329`), and the concurrency catches map to 409.
  - Both code paths use a single `SaveChangesAsync`.
  - The same transaction delivered twice concurrently cannot extend twice, because the loser fails on the Payment `xmin`. The persistence tests cover both tokens against real Postgres.
- **Out-of-order:**
  - A decline only applies to a Pending payment; otherwise the outcome is `OutOfOrder`.
  - A success for a Succeeded payment is `OutOfOrder`.
  - `MarkSucceeded` accepts Failed (Failed to Succeeded).
  - All of this is covered by handler and endpoint tests.
- **Audit exclusion:**
  - `Core:AuditExcluded` is checked in `AuditChangeReader.IsAudited` (line 56) and set on `Payment.RawWebhook` (`AppDbContext.cs:328`).
  - `AuditBehaviour` never serialises the command, so `Payload` stays out of the audit row.
  - The endpoint test asserts the diff contains neither `rawWebhook` nor the fixture email.
- **Renew:**
  - The new period starts at the old `CurrentPeriodEnd`, including inside grace.
  - It switches `Period`, clears `CancelledAt`, and is rejected when the subscription is not entitled at `renewedAt` (the strict `>` matches the entitlement spec).
  - `IsRenewableAt` is inclusive at end minus window.
- **Lapse and sweep:**
  - `Lapse` and `SubscriptionLapseSpecification.DueAt` agree across a 4-status by 5-time grid that includes exactly end + grace.
  - `ExpiredAt` is end + grace (Active or PastDue) or end (Cancelled).
  - The worker is a line-for-line mirror of `ExpiredExamSubmissionWorker` (diffed). It has a scope per call, the `when (!stoppingToken.IsCancellationRequested)` filters, and `_deferredIds` that clear on a short batch (the #81 fix).
  - The sweep is disabled in `ApiFactory` via `UseSetting`.
- **Webhook endpoint:**
  - It has `[AllowAnonymous]`, `[RequestSizeLimit(65536)]` and `[ApiExplorerSettings(IgnoreApi = true)]`. It is absent from `api/openapi/v1.json`.
  - The 404, 400-mismatch and 200 outcomes are only reachable after a valid signature. Unsigned callers only ever see 401 or a shape 400, so nothing leaks.
- **Fake completion:** It now uses `PaymentSettlement.Succeed/Fail`, the conflict branch and the `PriceFor` call are gone, and the `PAYMENT_NOT_PENDING` guard was added (#S2).
- **Snapshot and migration:**
  - `PeriodMonths` is snapshotted at checkout.
  - The migration backfill SQL is present (`20260929022041_AddPaymentWebhookState.cs:28`).
  - `ProviderOrderId` is linked before the first save.
  - `AppDbContextTests` got the `twentyFirst` entry.
- **Plan inventory:**
  - Every file in Files to create exists, and nothing extra was added.
  - All backend test-plan method names exist (checked by grep).
  - W1 to W10 exist with the exact `it(...)` names.
- **Resources and config:**
  - The 8 error codes are in `ErrorCodes.cs` and in both resx files.
  - The 3 web common error strings are present.
  - Config keys are in `SubscriptionsOptions`, `appsettings.example.json`, `ApiFactory` and `.env.example`.
- **Postman:**
  - `baseSubscriptionId` is captured.
  - "Cancel subscription" is POST `/api/subscriptions/{{baseSubscriptionId}}/cancel` and expects 200 and Cancelled.
  - The new "PaymobWebhook" folder uses `noauth`, `?hmac=0` and the plan body, and expects 401 `PAYMOB_WEBHOOK_SIGNATURE_INVALID`.
  - There are no orphaned requests.
- **Deviations** in `02-implementation.md` are real and benign:
  - the `onCancel` two-argument type;
  - the existing test classes;
  - the extra #53 test;
  - the Postman folder description, which was checked against behaviour.
- **Docs:**
  - `subscriptions.md`, `paymob.md` (apart from Blocking #1), `audit-log.md`, PRD §11.2 and design-prompt §4 match the code.
  - `docs/prototype.md` has nothing that diverges.
- **Issue #187:** the two items for #101 are resolved: RawWebhook audit exclusion and Renew semantics.

## Test quality
- `PaymobHmacTests`: strong. The known answer is computed externally, so a change to field order, boolean case or lower-casing fails it.
- `PaymobNotificationReaderTests`: constrains every mapped field, fail-closed and the body fallback.
- `ProcessPaymentNotificationHandlerTests`: a real in-memory predicate evaluation behind the substitutes, so matching and binding are genuinely exercised. Gap: currency.
- `PaymobWebhookEndpointTests`: end-to-end against Postgres with signed recorded payloads. It asserts DB state, not just status codes. Strong.
- `PaymentPersistenceTests`: the concurrency and unique-index tests use two real contexts. Strong.
- `SubscriptionLapseSpecificationTests`: the grid compares the SQL spec with the domain method. Strong.
- `SubscriptionLapseWorkerTests`, `CancelSubscriptionHandlerTests`, `LapseSubscriptionHandlerTests`, `PaymentSettlementTests`: constrain behaviour and follow the Received/DidNotReceive convention.
- No vacuous test found.
