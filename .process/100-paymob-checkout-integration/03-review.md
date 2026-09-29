VERDICT: CHANGES_REQUESTED

# Review — Paymob checkout integration (#100, E10.S2)

## Blocking

### 1. The Paymob response parser throws on a numeric `id` it never uses
**Where:** `api/Elmanhg.Infrastructure/Payments/Paymob/PaymobIntentionResponse.cs:5` (read at `api/Elmanhg.Infrastructure/Payments/Paymob/PaymobPaymentGateway.cs:59`)
**Rule:** orchestrator focus "robustness of the real adapter parsing"; plan D2 (only `client_secret` is read, `id` is unused and its type is unverified; `docs/paymob.md` §5 lists it as unverified)
**Problem:** `Id` is typed `string?`. System.Text.Json does not read a JSON number into a `string`, so a numeric `id` throws `JsonException`. The adapter catches that and returns 503. The field is unused, and its type is on the plan's own "unverified" list.
**Failure:** Paymob returns `{"id": 1234567, "client_secret": "egy_csk_test_..."}` with 201. Deserialisation throws `JsonException` at `$.id`, and every real checkout returns 503 `PAYMENT_GATEWAY_UNAVAILABLE`, although a valid `client_secret` arrived.
**Fix:** Remove `Id` from `PaymobIntentionResponse`, or type it `JsonElement?`. Add a `PaymobPaymentGatewayTests` case where the body has a numeric `id`, and assert the unified-checkout URL comes back. Update the last bullet of `docs/paymob.md` §5 to match.

### 2. `CompleteFakePaymentHandler` has an untested `throw`
**Where:** `api/Elmanhg.Application/Subscriptions/CompleteFakePayment/CompleteFakePaymentHandler.cs:33`
**Rule:** reviewer role "Coverage gaps": every `throw` in new code needs a test, and throwing paths need `SaveChangesAsync` `DidNotReceive()`
**Problem:** The path `PriceFor(payment.Plan, payment.Period)?.Months ?? throw new BadRequestCoreException(CHECKOUT_PERIOD_UNAVAILABLE)` is covered by no unit or integration test. `CompleteFakePaymentHandlerTests` only builds Base Monthly payments, and Base Monthly is configured.
**Failure:** Change the line to `?? 1` (or any default). All 2162 tests still pass, yet a Pending payment for a period removed from config would settle with a made-up duration.
**Fix:** Add `Handle_PeriodNoLongerConfigured_ThrowsCheckoutPeriodUnavailable`. Seed a Pending Base Yearly payment when only Monthly is configured, then assert `BadRequestCoreException` `CHECKOUT_PERIOD_UNAVAILABLE`, `SaveChangesAsync` `DidNotReceive()`, `subscriptionRepository.AddAsync` `DidNotReceive()`, and that the payment is still Pending.

### 3. No test checks that result-page polling stops (D12)
**Where:** `web/src/features/subscription/hooks/useCheckoutResult.ts:9-13`; tests `web/src/features/subscription/pages/CheckoutResultPage.test.tsx:76-98` (T95, T96)
**Rule:** plan D12 (poll only while Pending and under 60 s since the page opened); reviewer role Tests ("would each test fail if the code were wrong?"); orchestrator focus "polling stops correctly"
**Problem:** The code is correct. The tests do not constrain it. `timedOut` is computed on its own from `dataUpdatedAt - openedAt`, so T96 shows the "still confirming" state whether or not polling stopped. No test counts requests.
**Failure:** Change `refetchInterval` to `current.state.data?.status === "Pending" ? checkoutPollIntervalMs : false`, which drops the timeout clause. Every web test still passes, but a page left open on a payment that never confirms polls `GET /payments/{id}` every 2 s forever. That is exactly the state every real checkout ends in until #101 ships.
**Fix:** In T96, count calls to the `GetMyPayment` handler. After `waitOutConfirmationWindow()`, record the count, advance several more intervals, and assert the count did not change. Optionally, in T95, assert no further GET after the Succeeded response.

## Non-blocking
- **Idempotency (judged not needed now).** `POST /api/subscriptions/checkout` moves no money. A duplicate call leaves an extra Pending row, which is never listed and never paid unless the student completes it. The repo has no `Idempotency-Key` convention on its API surface, and the web disables every subscribe button while a checkout is pending. D18 and `docs/subscriptions.md` "For later stories" hand the double-paid case to #101. One real gap to carry into #101: `CompleteFakePaymentHandler.cs:37` / `PaymentSettlement.cs:15` do not re-check `EnsureCanPurchase`. Completing a second, stale Pending Base payment after the first succeeded therefore creates a second Active Base subscription. This is dev-only today with the fake, but the #101 webhook must guard it.
- `api/Elmanhg.Infrastructure/Payments/FakePaymentGateway.cs:11,15`: the lock keys on the environment name Production only. A host named Staging with `Provider=Fake` hands out plans freely. This is intended per D4 and documented in `docs/paymob.md` section 6. Consider noting it in the go-live steps.
- `web/src/features/subscription/components/PlanCardGrid.tsx:81`: no test clicks the Ask a Teacher subscribe button. If it sent Base instead of AskTeacher, no test would catch it (the implementer disclosed this). A one-line assertion in T79 would close it.
- `web/src/features/subscription/hooks/useCheckoutResult.ts`: when polling observes Succeeded, the entitlement query is not invalidated (the fake path does invalidate it in `useFakePaymentCompletion.ts:26`). This is harmless after a full-page return from Paymob, because the cache is empty. It is worth doing for symmetry.
- `docs/PRD.md` section 11.2 and section 17 rule 12 say entitlement changes "only via verified Paymob webhooks". The non-Production fake completion is a simulated webhook, documented in `docs/subscriptions.md` Checkout and `docs/paymob.md` section 6, so this is not a divergence. A half-sentence in PRD section 11.2 would stop a future reader from asking.
- `web/src/features/subscription/hooks/useFakePaymentCompletion.ts:28`: the explicit `navigate` does the same job as the `<Navigate>` in `FakeCheckoutPage.tsx:32`. The mutation survivor was disclosed. It is fine to keep.

## Verified
- `dotnet test api/ -c Release` with `api/Elmanhg.Api/appsettings.json` moved aside, then restored: **Passed, 2162/2162**.
- Web `npm run typecheck`: clean. `npm run lint` (`--max-warnings=0`): clean. `npm test -- --run`: **112 files, 695 tests passed**. `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto`: all files pass.
- Every F1-F23 and W1-W13 file exists with the planned signatures. No extra production files. The 5 declared deviations (the `IAuditableResult` on `CheckoutResult`, `[JsonIgnore]` on the audit getters, the audit-log.md and README rows, the `PAYMENT_NOT_PENDING` string, the settlement test folder) are real and justified. OpenAPI shows `StartCheckoutCommand` as `{ plan, period }` only. The `api/openapi/v1.json` diff (+212 lines) matches the report.
- Every test T1-T103 exists under its planned name.
- Security: the fake's `StartCheckoutAsync` throws 503 in Production and `SupportsSimulatedCompletion` is false there. `CompleteFakePaymentHandler` checks that flag before any lookup (404 `FAKE_CHECKOUT_UNAVAILABLE`), and the Paymob adapter always returns false. `GetMyPayment` and `CompleteFakePayment` both filter on `x.StudentId == userId`, and integration tests T71/T76 show the other student's payment returns 404 and stays Pending. `StartCheckout` takes no payment id. The Paymob logs carry only the payment id and the HTTP status. No keys, body or billing data are logged, and none reach responses except the intended publicKey and clientSecret in the redirect URL. The audit diff holds only the Payment and Subscription rows (the fake raw payload is `{source, success}`).
- D9: the gateway is called before `AddAsync` and `SaveChangesAsync`. T25 asserts neither runs on a gateway failure.
- D17: `Retry.DisableForUnsafeHttpMethods()` is in place, and T62 asserts a single attempt on 500. Timeouts come from options, and the options are validated at startup (`ValidateDataAnnotations` + `PaymentsOptionsValidator` + `ValidateOnStart`).
- Style: file-scoped namespaces, `sealed`, `DateTimeOffset` via `TimeProvider`, `.ConfigureAwait(false)` on every handler and adapter await, no try/catch in handlers (the adapter catch mirrors the OTP adapters), and only WHY comments.
- Web: tokens only (`shadow-1`, `bg-soft`, `text-h2`/`text-h2-desktop`, `text-ui`, `text-caption` all map to `app.css` / `.claude/design-system.md`). No `window.location` in features (it is isolated in `shared/lib/redirect.ts`). Every string exists in both ar and en.
- Polling: `refetchInterval` returns false once the status is not Pending or 60 s have passed, and TanStack Query clears the interval on unmount. There is no manual `setInterval` and no leak.
- Postman: 4 requests follow "Get my payments" in state order, with the correct URLs, methods, inherited bearer auth, bodies, the `checkoutPaymentId` variable and the folder description.
- Docs: `docs/subscriptions.md` (Checkout section, API rows, button rules, later-stories), the new `docs/paymob.md`, PRD section 11.2, `docs/claude-design-prompt.md` section 4, `docs/audit-log.md` and README all agree with the code. No divergence found.

## Test quality
- `StudentEntitlementTests` (T1-T5), `SubscriptionsOptionsTests` (T6-T9), `StartCheckoutValidatorTests`, `PaymentSettlementTests`, `GetMyPaymentHandlerTests` (the predicate is compiled over an in-memory list, so ownership is really checked), `PaymentsOptionsValidatorTests` and `FakePaymentGatewayTests` all constrain the code.
- `StartCheckoutHandlerTests`: these constrain the code. They capture the added payment and the gateway request, and assert `Received(1)`/`DidNotReceive` on save. None of them just asserts a stub's return value (T19 checks the redirect passthrough and the request that was sent).
- `CompleteFakePaymentHandlerTests`: good, apart from the missing period-unavailable path (Blocking #2).
- `PaymobPaymentGatewayTests`: they assert the wire JSON and headers directly, which is good. There is no case for a response shape that differs from the assumed one (Blocking #1).
- `PaymentsServiceCollectionExtensionsTests`: T62 really constrains the retry policy.
- Integration (`CheckoutEndpointTests`, `PaymentEndpointTests`, `FakePaymentCompletionEndpointTests`): these read the DB through a fresh context and constrain end-to-end behaviour.
- `CheckoutResultPage.test.tsx`: T95 and T96 do not constrain *stopping* the polls (Blocking #3). The rest are fine.
- `SubscriptionPage.checkout.test.tsx` and `FakeCheckoutPage.test.tsx`: these constrain the code, except that the Ask a Teacher click path is never exercised (non-blocking).
