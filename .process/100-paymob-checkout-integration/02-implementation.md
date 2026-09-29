# Implementation — Paymob checkout integration (#100, E10.S2)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/Elmanhg.Application/Shared/Payments/IPaymentGateway.cs` | 8 | F1 gateway port |
| `api/Elmanhg.Application/Shared/Payments/PaymentCheckoutRequest.cs` | 6 | F2 |
| `api/Elmanhg.Application/Shared/Payments/PaymentCustomer.cs` | 3 | F3 |
| `api/Elmanhg.Application/Shared/Payments/PaymentCheckout.cs` | 3 | F4 |
| `api/Elmanhg.Application/Subscriptions/Shared/CheckoutResult.cs` | 9 | F5 (+ explicit `IAuditableResult`, see Deviations) |
| `api/Elmanhg.Application/Subscriptions/Shared/PaymentSettlement.cs` | 19 | F6 shared settle routine for #101 |
| `api/Elmanhg.Application/Subscriptions/StartCheckout/StartCheckoutCommand.cs` | 17 | F7 (`Payment.StartCheckout` audit; audit props `[JsonIgnore]`) |
| `api/Elmanhg.Application/Subscriptions/StartCheckout/StartCheckoutValidator.cs` | 28 | F8 |
| `api/Elmanhg.Application/Subscriptions/StartCheckout/StartCheckoutHandler.cs` | 43 | F9 |
| `api/Elmanhg.Application/Subscriptions/GetMyPayment/GetMyPaymentQuery.cs` | 6 | F10 |
| `api/Elmanhg.Application/Subscriptions/GetMyPayment/GetMyPaymentHandler.cs` | 23 | F11 owner-scoped read |
| `api/Elmanhg.Application/Subscriptions/CompleteFakePayment/CompleteFakePaymentCommand.cs` | 12 | F12 (`Payment.CompleteFake` audit) |
| `api/Elmanhg.Application/Subscriptions/CompleteFakePayment/CompleteFakePaymentHandler.cs` | 46 | F13h |
| `api/Elmanhg.Infrastructure/Payments/PaymentsOptions.cs` | 23 | F13 |
| `api/Elmanhg.Infrastructure/Payments/PaymentProvider.cs` | 3 | F14 |
| `api/Elmanhg.Infrastructure/Payments/Paymob/PaymobOptions.cs` | 13 | F15 |
| `api/Elmanhg.Infrastructure/Payments/PaymentsOptionsValidator.cs` | 51 | F16 |
| `api/Elmanhg.Infrastructure/Payments/FakePaymentGateway.cs` | 22 | F17 (Production lock) |
| `api/Elmanhg.Infrastructure/Payments/Paymob/PaymobIntentionRequest.cs` | 25 | F18 (+ `PaymobItem`) |
| `api/Elmanhg.Infrastructure/Payments/Paymob/PaymobBillingData.cs` | 27 | F19 |
| `api/Elmanhg.Infrastructure/Payments/Paymob/PaymobIntentionResponse.cs` | 5 | F20 |
| `api/Elmanhg.Infrastructure/Payments/Paymob/PaymobPaymentGateway.cs` | 75 | F21 real adapter (Token auth, User-Agent, no body/key logging) |
| `api/Elmanhg.Infrastructure/Payments/PaymentsServiceCollectionExtensions.cs` | 35 | F22 (typed client, resilience, POST retries disabled, `ValidateOnStart`) |
| `api/Elmanhg.Api/Controllers/Subscriptions/Requests.cs` | 3 | F23 |
| `api/Elmanhg.Tests/Application/Features/Subscriptions/StartCheckout/StartCheckoutValidatorTests.cs` | 83 | T10–T16 |
| `api/Elmanhg.Tests/Application/Features/Subscriptions/StartCheckout/StartCheckoutHandlerTests.cs` | 149 | T17–T25 |
| `api/Elmanhg.Tests/Application/Features/Subscriptions/GetMyPayment/GetMyPaymentHandlerTests.cs` | 65 | T26–T28 |
| `api/Elmanhg.Tests/Application/Features/Subscriptions/Shared/PaymentSettlementTests.cs` | 45 | T29–T31 |
| `api/Elmanhg.Tests/Application/Features/Subscriptions/CompleteFakePayment/CompleteFakePaymentHandlerTests.cs` | 121 | T32–T37 |
| `api/Elmanhg.Tests/Infrastructure/Payments/PaymobPaymentGatewayTests.cs` | 134 | T38–T47 |
| `api/Elmanhg.Tests/Infrastructure/Payments/FakePaymentGatewayTests.cs` | 58 | T48–T51 |
| `api/Elmanhg.Tests/Infrastructure/Payments/PaymentsOptionsValidatorTests.cs` | 106 | T52–T58 |
| `api/Elmanhg.Tests/Infrastructure/Payments/PaymentsServiceCollectionExtensionsTests.cs` | 83 | T59–T62 |
| `api/Elmanhg.Tests/Infrastructure/Payments/PaymentsTestSettings.cs` | 45 | helper (mirrors `OtpDeliveryTestSettings`) |
| `api/Elmanhg.Tests/Integration/Subscriptions/CheckoutEndpointTests.cs` | 96 | T63–T68 |
| `api/Elmanhg.Tests/Integration/Subscriptions/PaymentEndpointTests.cs` | 54 | T69–T71 |
| `api/Elmanhg.Tests/Integration/Subscriptions/FakePaymentCompletionEndpointTests.cs` | 102 | T72–T76 |
| `web/src/shared/lib/redirect.ts` | 7 | W1 |
| `web/src/shared/lib/redirect.test.ts` | 16 | T101–T103 |
| `web/src/features/subscription/api/checkoutPolling.ts` | 2 | W2 |
| `web/src/features/subscription/hooks/useCheckout.ts` | 39 | W3 |
| `web/src/features/subscription/hooks/useFakePaymentCompletion.ts` | 43 | W4 |
| `web/src/features/subscription/hooks/useCheckoutResult.ts` | 21 | W5 |
| `web/src/features/subscription/components/BaseCheckoutActions.tsx` | 27 | W6 |
| `web/src/features/subscription/components/AskTeacherCheckoutAction.tsx` | 33 | W7 |
| `web/src/features/subscription/components/FakeCheckoutCard.tsx` | 60 | W8 |
| `web/src/features/subscription/components/CheckoutStatusCard.tsx` | 65 | W9 |
| `web/src/features/subscription/pages/FakeCheckoutPage.tsx` | 35 | W10 |
| `web/src/features/subscription/pages/CheckoutResultPage.tsx` | 38 | W11 |
| `web/src/routes/student/fake-checkout.$paymentId.tsx` | 11 | W12 |
| `web/src/routes/student/checkout-result.$paymentId.tsx` | 11 | W13 |
| `web/src/features/subscription/pages/SubscriptionPage.checkout.test.tsx` | 131 | T77–T83 |
| `web/src/features/subscription/pages/FakeCheckoutPage.test.tsx` | 156 | T84–T92 |
| `web/src/features/subscription/pages/CheckoutResultPage.test.tsx` | 140 | T93–T100 |
| `web/src/shared/api/generated/model/{checkoutResult,completeFakePaymentRequest,startCheckoutCommand}.ts` | — | Orval output |
| `docs/paymob.md` | 81 | New: provider switch, keys, validation, Intention API flow, verify-before-go-live list, fake + Production lock, go-live steps |

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.Domain/Subscriptions/StudentEntitlement.cs` | `EnsureCanPurchase(SubscriptionPlan)` exactly as planned |
| `api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs` | `CheckoutPlanAlreadyActive`, `CheckoutRequiresBase` |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | 8 application codes |
| `api/Elmanhg.Application/Shared/Options/SubscriptionsOptions.cs` | `PriceFor(plan, period)` switch expression |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | `services.AddPayments();` after `AddOtpDelivery()` |
| `api/Elmanhg.Api/Controllers/Subscriptions/SubscriptionsController.cs` | 3 actions (`StartCheckout`, `GetMyPayment`, `CompleteFakePayment`), all `DefaultCodes.SubscriptionManage` |
| `api/Elmanhg.Api/Resources/Messages.ar.resx`, `Messages.en.resx` | 10 keys |
| `api/Elmanhg.Api/appsettings.example.json` | `Payments` section (Fake, blank Paymob keys) |
| `api/Elmanhg.Api/appsettings.json` (LOCAL, gitignored) | Same `Payments` section with Fake values |
| `.env.example` | Commented `Payments__*` block (placeholders only) |
| `api/openapi/v1.json` | Regenerated by `dotnet build` |
| `postman/elmanhg.postman_collection.json` | 4 requests after "Get my payments" in state order, `checkoutPaymentId` variable, folder description |
| `api/Elmanhg.Tests/Domain/Subscriptions/StudentEntitlementTests.cs` | T1–T5 (add only) |
| `api/Elmanhg.Tests/Application/Features/Subscriptions/SubscriptionsOptionsTests.cs` | T6–T9 (add only) |
| `api/Elmanhg.Tests/Infrastructure/OtpDelivery/StubHttpMessageHandler.cs` | `ResponseBody` + JSON `StringContent` (additive) |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | 4 `Payments:*` keys |
| `api/Elmanhg.Tests/Integration/Subscriptions/SubscriptionTestData.cs` | `FakeCheckoutPath` const + `SeedPendingPaymentAsync(factory, studentId, plan, period, amountMinor, ct)` overload |
| `web/src/features/subscription/components/PlanCard.tsx` | optional `actions` prop |
| `web/src/features/subscription/components/PlanCardGrid.tsx` | `onCheckout`, `isCheckoutPending`; Base/AskTeacher actions |
| `web/src/features/subscription/pages/SubscriptionPage.tsx` | `useCheckout`, redirecting status line |
| `web/src/features/subscription/index.ts` | exports `FakeCheckoutPage`, `CheckoutResultPage` |
| `web/src/features/subscription/i18n/{ar,en}.json` | `period`, `checkout`, `fakeCheckout`, `checkoutResult` blocks |
| `web/src/shared/i18n/{ar,en}.json` | 10 new error codes + `PAYMENT_NOT_PENDING` |
| `web/src/test/subscriptionFixtures.ts` | `checkoutPaymentId`, `checkoutResult()` |
| `web/src/shared/api/generated/**` | Regenerated by `npm run gen:api` |
| `web/src/routeTree.gen.ts` | Regenerated by the router plugin (`vite build`) |
| `docs/subscriptions.md` | New "Checkout" section, 3 API rows, subscribe-button rules, "For later stories" (#100 done, #101 settle + double-payment guard) |
| `docs/PRD.md` §11.2 | Checkout sentence as planned |
| `docs/claude-design-prompt.md` §4 | `#/student/subscription` line replaced as planned |
| `docs/audit-log.md` | Not in plan, see Deviations |
| `README.md` | Not in plan, see Deviations |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| F5 `public sealed record CheckoutResult(Guid PaymentId, string RedirectUrl, Money Amount);` | Plan D16 makes `StartCheckoutCommand` auditable; `docs/audit-log.md` says create-style commands expose the new id through `IAuditableResult` on the result. Without it the audit row has no resource id. | Added `: IAuditableResult` with the explicit `Guid? IAuditableResult.AuditResourceId => PaymentId;` (same as `ExamBlueprintResult`). The JSON and the Orval model are unchanged. |
| F7 `StartCheckoutCommand ... : IRequest<CheckoutResult>, IAuditableCommand` bound `[FromBody]` | `IAuditableCommand` needs `AuditAction`/`AuditResourceType`/`AuditResourceId` getters. On a body-bound command they leaked into the OpenAPI schema and the Orval `StartCheckoutCommand` model as `auditAction`, etc. No other body-bound command is auditable. | Put `[JsonIgnore]` on the three audit getters. The schema is back to `{ plan, period }`. |
| "Existing code touched" did not list `docs/audit-log.md` or `README.md` | docs-sync: `docs/audit-log.md` lists every audited command and entity, and two audited commands were added. `README.md` indexes the provider docs (`otp-delivery.md`). | Added `StartCheckout` / `CompleteFakePayment` rows, and `Subscription` and `Payment` to the audited-entity list (they were already `IAuditedEntity` from #99 but missing from the list). Added a `docs/paymob.md` row and a one-line fake-payments note to `README.md`. |
| Web `common:errors.PAYMENT_NOT_PENDING` = "This payment was already processed." and "same strings as the resx" | The existing resx string is "This payment has already been processed." | Used the resx string so web and API agree. |
| T90 test file layout: the "Files to create" table does not list test files | The test plan names them | Created them exactly as named in the test plan. `PaymentSettlementTests` lives in `Tests/Application/Features/Subscriptions/Shared/` because the plan gives no folder. |

## Build & test
All run on Windows, Docker 29.6.2 up (Testcontainers).

- `dotnet build api/` (Debug): **Build succeeded**, 0 warnings (TreatWarningsAsErrors is on). It regenerated `api/openapi/v1.json` (+212 lines: 3 paths, `CheckoutResult`, `StartCheckoutCommand { plan, period }`, `CompleteFakePaymentRequest`).
- `dotnet test api/ -c Release` with `api/Elmanhg.Api/appsettings.json` moved aside, then restored: **Passed! total: 2162, failed: 0, succeeded: 2162, skipped: 0.** The 76 planned test methods (T1–T76) are all present; the T54 and T57 theories have 4 rows each.
- `dotnet format Elmanhg.slnx --verify-no-changes --exclude core-libraries`: one WHITESPACE finding, in `Tests/Builders/SubscriptionBuilder.cs(57,27)`. That file predates this story and I did not touch it. Nothing in the files I changed.
- Guard grep (`DateTime.Now|UtcNow`, `.Result`, `.Wait()`, `new HttpClient(`, `FromSqlRaw`, `async void`) over added `.cs` lines: one hit, `new HttpClient(_handler)` in `PaymobPaymentGatewayTests`. It is a test and uses the same stub-handler pattern as `MetaWhatsAppOtpChannelTests`. Production code has none.
- `npm --prefix web run gen:api`: regenerated; a second run shows no further drift.
- `npx vite build` / `npm run build`: built; `routeTree.gen.ts` regenerated with the two routes.
- `npm run typecheck`: clean. `npm run lint` (`--max-warnings=0`): clean.
- `npm test -- --run`: **Test Files 112 passed, Tests 695 passed** (27 new: T77–T103).
- `npx vitest run --coverage`: 695/695 on the recorded run. An earlier coverage run had 1 failure that did not reproduce; the #148 `RichTextEditor` flake is the likely cause, but the log was not kept.
- `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` (in `web/`): "All matched files use Prettier code style!"

**Mutation checks** (break the line, run the owning tests, restore; scripted, all restored and verified):
- api, 22 mutation points (30 runs, including 8 re-runs in a form that compiles), all killed. StudentEntitlement: 3 guards. `PriceFor`: AskTeacher `when`. Validator: 2 rules. StartCheckoutHandler: add-before-gateway, customer phone. Ownership predicate in GetMyPayment and CompleteFakePayment. CompleteFakePayment: simulated-completion guard. PaymentSettlement: reference. FakePaymentGateway: 2 Production guards. Paymob: notification_url omission, last-name NA, client-secret check, User-Agent. Options validator: NotificationUrl https, positive ids. DI: `DisableForUnsafeHttpMethods`, provider switch. `TreatWarningsAsErrors` would turn an `if (false)` mutation into a build failure that only looks like a kill, so those mutations, the nullable client-secret one and the two ownership ones were re-run in a form that compiles. All 8 re-runs were killed too.
- web, 18 mutations, 17 killed. The survivor is the explicit `navigate(...)` in `useFakePaymentCompletion.onSuccess`. When it is removed, `setQueryData` still flips the cached payment to non-Pending, and `FakeCheckoutPage` redirects through its own `<Navigate>`. The user-visible result is the same, so no test can tell the two apart. I kept the call because the plan specifies it.

## Notes for review
- **Paymob field names are unverified** (D2). `docs/paymob.md` §5 lists what must be checked against a test merchant: `special_reference` → `merchant_order_id`, `"NA"` email, phone format, query-parameter names, and the response `id` type. `PaymobIntentionResponse.Id` is a `string?` as planned. If Paymob returns a numeric `id`, deserialisation throws `JsonException` and every checkout would get 503. Consider `JsonElement?` if the live check shows a number. This is part of the plan's deferred live-verification issue.
- `ApiFactory` gets the `Payments:*` keys through the in-memory dictionary, as planned, not through `UseSetting`. That is safe: the provider and options are read when services resolve, not while `Program` registers them.
- The Base card's buttons follow the catalogue order (`base.prices`, ordered by months). Ask a Teacher always checks out `Monthly`, because PRD §11.1 sells it monthly only.
- `CheckoutStatusCard` renders the pending state inside `role="status" aria-live="polite"`. Every state has one `<h1>`, and axe is clean on the fake-checkout and pending result pages.
- `PaymentSettlement` sits in `Application/Subscriptions/Shared` (F6) and takes no repository. #101's webhook handler must add the returned subscription and save once, as `CompleteFakePaymentHandler` does.
- Test files over ~100 lines (`StartCheckoutHandlerTests` 149, `FakeCheckoutPage.test.tsx` 156, and others): the ~100-line cap is a production-code rule, and existing test files are the same size.
- The modified C# files I edited through a script now use LF in the working copy (`core.autocrlf=true`, so git stores no line-ending change). `dotnet format` does not flag them.
- Web coverage: `redirect.ts` line 6 (`window.location.assign`) is not covered, because T82 mocks it by design (D15). `PlanCardGrid` line 81 (the Ask a Teacher `onCheckout` callback) is not clicked by any test. T79 only asserts that the button is enabled.
- The PROGRESS.md change in the tree is the orchestrator's; I did not modify it.
