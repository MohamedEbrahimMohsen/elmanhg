# Implementation — [E10.S3] Webhook-driven entitlement (#101)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| api/Elmanhg.Domain/Subscriptions/PaymentReviewReason.cs | 3 | `AskTeacherWithoutBase` flag |
| api/Elmanhg.Domain/Subscriptions/SubscriptionLapseSpecification.cs | 15 | `DueAt(now, grace, excludedIds)`, the SQL twin of `Lapse` |
| api/Elmanhg.Application/Shared/Payments/PaymentNotification.cs | 3 | Provider-neutral notification record |
| api/Elmanhg.Application/Shared/Payments/IPaymentNotificationReader.cs | 6 | Port: parse and verify a notification |
| api/Elmanhg.Application/Subscriptions/Shared/PaymentNotificationOutcome.cs | 3 | Succeeded / MarkedFailed / Duplicate / Ignored / OutOfOrder |
| api/Elmanhg.Application/Subscriptions/Shared/PaymentNotificationResult.cs | 8 | Webhook 200 body, `IAuditableResult` |
| api/Elmanhg.Application/Subscriptions/ProcessPaymentNotification/ProcessPaymentNotificationCommand.cs | 12 | Audited `Payment.ProcessNotification` |
| api/Elmanhg.Application/Subscriptions/ProcessPaymentNotification/ProcessPaymentNotificationHandler.cs | 74 | #H1 steps |
| api/Elmanhg.Application/Subscriptions/CancelSubscription/CancelSubscriptionCommand.cs | 12 | Audited `Subscription.Cancel` |
| api/Elmanhg.Application/Subscriptions/CancelSubscription/CancelSubscriptionHandler.cs | 36 | Owner-scoped cancel, returns fresh entitlement |
| api/Elmanhg.Application/Subscriptions/CancelSubscription/CancelSubscriptionValidator.cs | 13 | `SUBSCRIPTION_ID_REQUIRED` |
| api/Elmanhg.Application/Subscriptions/GetLapsedSubscriptionIds/GetLapsedSubscriptionIdsQuery.cs | 5 | Sweep listing query |
| api/Elmanhg.Application/Subscriptions/GetLapsedSubscriptionIds/GetLapsedSubscriptionIdsHandler.cs | 18 | Paged ids ordered by `CurrentPeriodEnd` |
| api/Elmanhg.Application/Subscriptions/LapseSubscription/LapseSubscriptionCommand.cs | 11 | Audited `Subscription.Lapse` |
| api/Elmanhg.Application/Subscriptions/LapseSubscription/LapseSubscriptionHandler.cs | 20 | `Subscription.Lapse`, save only on change |
| api/Elmanhg.Application/Subscriptions/LapseSubscription/LapseSubscriptionValidator.cs | 13 | `SUBSCRIPTION_ID_REQUIRED` |
| api/Elmanhg.Infrastructure/Payments/Paymob/PaymobHmac.cs | 48 | HMAC-SHA512 over the 20 documented fields, constant-time compare |
| api/Elmanhg.Infrastructure/Payments/Paymob/PaymobNotificationReader.cs | 96 | Parse, verify (fail closed), map; never logs |
| api/Elmanhg.Infrastructure/Migrations/20260929022041_AddPaymentWebhookState.cs (+ .Designer.cs, snapshot) | 96 | PeriodMonths (+ backfill SQL), ProviderOrderId, ReviewReason, xmin x2, 2 indexes |
| api/Elmanhg.Api/Controllers/Payments/PaymobWebhooksController.cs | 24 | Anonymous, 64 KiB, hidden from OpenAPI |
| api/Elmanhg.Api/Workers/SubscriptionLapseWorker.cs | 74 | Mirror of `ExpiredExamSubmissionWorker` |
| api/Elmanhg.Tests/Fixtures/Paymob/{transaction-succeeded,transaction-declined,transaction-pending,transaction-refunded,token}.json | 1 each | Recorded payloads (fake PII) |
| api/Elmanhg.Tests/Fixtures/Paymob/PaymobPayloads.cs | 31 | Read / ForPayment / Sign |
| api/Elmanhg.Tests/Domain/Subscriptions/SubscriptionLapseSpecificationTests.cs | 47 | #17–18 |
| api/Elmanhg.Tests/Application/Features/Subscriptions/ProcessPaymentNotification/ProcessPaymentNotificationHandlerTests.cs | 238 | #37–50 |
| api/Elmanhg.Tests/Application/Features/Subscriptions/CancelSubscription/CancelSubscriptionHandlerTests.cs | 103 | #60–64 |
| api/Elmanhg.Tests/Application/Features/Subscriptions/CancelSubscription/CancelSubscriptionValidatorTests.cs | 22 | #65 |
| api/Elmanhg.Tests/Application/Features/Subscriptions/LapseSubscription/LapseSubscriptionHandlerTests.cs | 72 | #66–69 |
| api/Elmanhg.Tests/Application/Features/Subscriptions/LapseSubscription/LapseSubscriptionValidatorTests.cs | 22 | #70 |
| api/Elmanhg.Tests/Infrastructure/Payments/PaymobHmacTests.cs | 61 | #73–77 (known-answer digest computed with the plan's Python script) |
| api/Elmanhg.Tests/Infrastructure/Payments/PaymobNotificationReaderTests.cs | 102 | #78–85 |
| api/Elmanhg.Tests/Api/Workers/SubscriptionLapseWorkerTests.cs | 121 | #90–93 |
| api/Elmanhg.Tests/Integration/Subscriptions/PaymobWebhookEndpointTests.cs | 252 | #98–111 |
| api/Elmanhg.Tests/Integration/Subscriptions/CancelSubscriptionEndpointTests.cs | 95 | #112–116 |
| api/Elmanhg.Tests/Integration/Subscriptions/SubscriptionLapseSweepTests.cs | 73 | #117–119 |
| web/src/features/subscription/components/CancelSubscriptionDialog.tsx | 46 | Danger confirm dialog |
| web/src/features/subscription/hooks/useSubscriptionCancellation.ts | 34 | Wraps `useCancelSubscription`, sets entitlement cache, toasts |
| web/src/features/subscription/pages/SubscriptionPage.manage.test.tsx | 160 | W1–W10 |

## Files modified
| Path | Change |
|---|---|
| api/core-libraries/Core.EntityFrameworkCore/Auditing/AuditChangeReader.cs | `ExcludedAnnotation = "Core:AuditExcluded"`; `IsAudited` skips annotated properties |
| api/Elmanhg.Domain/Subscriptions/Subscription.cs | `Version` (xmin), `IsRenewableAt` |
| api/Elmanhg.Domain/Subscriptions/Subscription.Lifecycle.cs | New `Renew(period, months, ref, renewedAt, grace)`; `Lapse(now, grace)` |
| api/Elmanhg.Domain/Subscriptions/Payment.cs | `PeriodMonths`, `ProviderOrderId`, `ReviewReason`, `Version`; `Create(..., periodMonths, amount)`; `MarkSucceeded` accepts Failed; `LinkProviderOrder`, `FlagForReview` |
| api/Elmanhg.Domain/Subscriptions/StudentEntitlement.cs | `Held`, `PurchaseConflict/EnsureCanPurchase(plan, now, renewalWindow)` |
| api/Elmanhg.Application/Exceptions/ErrorCodes.cs | 8 codes |
| api/Elmanhg.Application/Shared/Options/SubscriptionsOptions.cs | `RenewalWindowDays`, `LapseSweep*`, `RenewalWindow` |
| api/Elmanhg.Application/Shared/Payments/PaymentCheckout.cs | `ProviderOrderId = null` |
| api/Elmanhg.Application/Subscriptions/Shared/PaymentSettlement.cs | `Succeed` / `Fail` (#S1) |
| api/Elmanhg.Application/Subscriptions/Shared/SubscriptionResult.cs | `InGracePeriod`, `CanRenew` |
| api/Elmanhg.Application/Subscriptions/Shared/EntitlementResultGenerator.cs, StudentEntitlementLoader.cs | `now` threaded; flags computed |
| api/Elmanhg.Application/Subscriptions/StartCheckout/StartCheckoutHandler.cs | Renewal window, months snapshot, `LinkProviderOrder` |
| api/Elmanhg.Application/Subscriptions/CompleteFakePayment/CompleteFakePaymentHandler.cs | #S2 rewrite (no `PriceFor`, no conflict branch) |
| api/Elmanhg.Infrastructure/Payments/Paymob/{PaymobOptions,PaymobIntentionResponse,PaymobPaymentGateway}.cs | `HmacSecret`; `intention_order_id` → `ProviderOrderId` |
| api/Elmanhg.Infrastructure/Payments/{PaymentsOptionsValidator,PaymentsServiceCollectionExtensions}.cs | HmacSecret required with Paymob; reader registered always |
| api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs | 2 index constants, xmin on both, new columns, `RawWebhook` annotation, 3 catch clauses |
| api/Elmanhg.Api/Controllers/Subscriptions/SubscriptionsController.cs | `CancelSubscription` action |
| api/Elmanhg.Api/Program.cs | `AddHostedService<SubscriptionLapseWorker>()` |
| api/Elmanhg.Api/Resources/Messages.{ar,en}.resx | 8 keys each |
| api/Elmanhg.Api/appsettings.example.json (+ local appsettings.json, untracked) | 4 Subscriptions keys, `Paymob.HmacSecret` |
| .env.example | `# Payments__Paymob__HmacSecret=` |
| api/openapi/v1.json | Regenerated (cancel endpoint, 2 result fields; webhook absent) |
| api/Elmanhg.Tests/Elmanhg.Tests.csproj | Copy `Fixtures\Paymob\*.json` |
| api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs | `TestPaymobHmacSecret`, `LapseSweepEnabled=false` (UseSetting), 4 keys |
| api/Elmanhg.Tests/Integration/Subscriptions/SubscriptionTestData.cs | `MonthsFor`, `WebhookRoute`, months on every `Payment.Create` |
| api/Elmanhg.Tests/Infrastructure/Payments/PaymentsTestSettings.cs | `HmacSecret` |
| api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs | `twentyFirst => _AddPaymentWebhookState` |
| Existing test files (Test plan "modify" rows + mechanical Arrange edits) | SubscriptionTests, PaymentTests, StudentEntitlementTests, PaymentSettlementTests, CompleteFakePaymentHandlerTests, StartCheckoutHandlerTests, GetMyEntitlementHandlerTests, GetMyPayment(s)HandlerTests, SubscriptionsOptionsTests, PaymentsOptionsValidatorTests, PaymentsServiceCollectionExtensionsTests, PaymobPaymentGatewayTests, AuditChangeCaptureTests, PaymentPersistenceTests, CheckoutEndpointTests, EntitlementEndpointTests, FakePaymentCompletionEndpointTests |
| postman/elmanhg.postman_collection.json | `baseSubscriptionId` variable + set in "Get my entitlement after payment"; "Cancel subscription"; new "PaymobWebhook" folder with "Unsigned webhook is rejected" |
| web/src/shared/api/generated/** | Orval regenerated |
| web/src/features/subscription/components/{PlanCardGrid,BaseCheckoutActions,AskTeacherCheckoutAction,CurrentPlanCard,SubscriptionStatusLine}.tsx | Renew modes, cancel button + dialog, grace wording |
| web/src/features/subscription/pages/SubscriptionPage.tsx | Wires `useSubscriptionCancellation` |
| web/src/features/subscription/i18n/{en,ar}.json, web/src/shared/i18n/{en,ar}.json | New keys |
| web/src/test/subscriptionFixtures.ts | `canRenew`, `inGracePeriod` (default false) |
| docs/subscriptions.md, docs/paymob.md, docs/audit-log.md, docs/PRD.md §11.2, docs/claude-design-prompt.md §4 | Per the plan's Docs section |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| `CurrentPlanCard` prop `onCancel: (subscriptionId: string) => void` | The same plan calls it as `onCancel(cancelling.id, () => setCancelling(null))` and the hook's `cancel` takes `(subscriptionId, onDone)`; the one-parameter type does not compile with that call | Typed it `(subscriptionId: string, onDone: () => void) => void`, matching the hook |
| Test classes marked "(new)": `CheckoutEndpointTests`, `EntitlementEndpointTests`, `SubscriptionsOptionsTests`, `PaymobPaymentGatewayTests`, `PaymentsServiceCollectionExtensionsTests`, `AuditChangeCaptureTests` | These classes already exist | Added the listed methods to the existing classes |
| #53 `Handle_FailedPaymentSucceeded_ThrowsPaymentNotPending` (new) | Existing `Handle_PaymentAlreadyCompleted_ThrowsPaymentNotPending` already covers the same case | Added #53 as specified (it also asserts status/transaction unchanged and no Add) and left the existing test untouched |
| Postman: only the listed requests | The Subscriptions folder description still said a re-run just returns `CHECKOUT_PLAN_ALREADY_ACTIVE` | Also updated that folder description (renewal window, cancel, re-run codes) |

## Build & test
- `dotnet build` (api): Build succeeded, no new warnings (only the pre-existing core-libraries CS8618/CS8602).
- `dotnet test api/` (Debug): `failed: 0, succeeded: 2293`.
- CI parity: `appsettings.json` moved aside, `dotnet test -c Release` → `failed: 0, succeeded: 2293`; file restored.
- `npm --prefix web run gen:api`: regenerated 4 generated files.
- `npm --prefix web run typecheck`: clean. `npm --prefix web run lint`: clean (`--max-warnings=0`).
- `npm --prefix web test -- --run`: `Test Files 113 passed (113)`, `Tests 706 passed (706)`.
- `npm --prefix web run build`: `✓ built in 4.98s`.
- `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` (web): clean after `prettier --write` on the two new files.
- `dotnet format --verify-no-changes`: only a pre-existing whitespace finding in `Tests/Builders/SubscriptionBuilder.cs` (file not touched) plus core-libraries noise.
- Mutation checks (each broke the named tests, then restored):
  - HMAC compare forced true → `IsValid_TamperedAmount_IsFalse`, `Read_BadSignature_ThrowsSignatureInvalid`, `Post_InvalidSignature_Returns401AndChangesNothing`.
  - Field order swapped / `true` → `True` / no lower-casing → `Compute_RecordedTransaction_MatchesKnownDigest`, `IsValid_UpperCaseSignature_IsTrue`.
  - Blank-secret guard removed → `IsValid_BlankSecret_IsFalse`, `Read_EmptyHmacSecret_ThrowsSignatureInvalid`.
  - Body-signature fallback removed → `Read_SignatureOnlyInBody_IsAccepted`.
  - Amount binding removed → handler + endpoint mismatch tests.
  - Duplicate check / Succeeded guard removed → `Handle_TransactionAlreadyRecorded_ReturnsDuplicate`, `Handle_SecondSuccessForSucceededPayment_ReturnsOutOfOrder`, `Post_SameTransactionTwice_SecondIsDuplicate`.
  - Audit annotation check removed → `SaveChangesAsync_AuditExcludedProperty_IsLeftOutOfTheDiff`, `Post_SignedSuccess_AuditDiffExcludesRawWebhook`.
  - Payment concurrency catch disabled → `SaveChanges_StalePayment_ThrowsPaymentModifiedConcurrently`.
  - PastDue spec term wrong → grid rows `PastDue, 0h / 24h`.
  - Worker deferral removed → `Sweep_FailedIds_AreSkippedUntilTheBacklogEnds`.
  - `CancelledAt = null` removed → `Renew_CancelledBeforeEnd_…`, `Succeed_CancelledBaseHeld_ResumesHeld`.
  - `CanRenew` forced false → both GetMyEntitlement flag tests + `Get_BaseInsideRenewalWindow_ReportsCanRenew`.
  - Web: grace flag off, cancel button on Cancelled, renew mode off → W1, W2, W3, W7, W8.

## Notes for review
- **Malformed JSON on the webhook.** The plan's controller binds `[FromBody] JsonElement`, so MVC rejects malformed JSON with its default 400 ProblemDetails (no `code`) before the reader runs. `PAYMOB_WEBHOOK_PAYLOAD_INVALID` over HTTP therefore covers only well-formed JSON with a bad shape; the reader's `JsonException` branch is exercised by the unit test. Nothing changes state either way.
- `PaymobPaymentGateway` uses `intention.ClientSecret!` after `ReadIntentionAsync` has already rejected a blank secret (the helper now returns the whole response, per the plan).
- `ProcessPaymentNotificationHandler` folds #H1 steps 1 and 2 into one `if` (null, pending or refund/void → `Ignored`). The behaviour is the same.
- The `SUBSCRIPTION_ENDED` web string reuses the existing resx text verbatim, as the plan asks. The Arabic resx text has no hamza (`او`, `الغاؤه`). The same goes for the new `اشعار` strings.
- `SubscriptionLapseSweepTests` seeds subscriptions ending about 20 years ago, so they sort into the first batch of 100 in the shared test database (the exam sweep tests do the same).
- The integration host uses real time, not a fake clock. Where a test compares a date read back from the database, it either reads both sides from the database or uses a start truncated to whole seconds (Postgres stores microseconds).
- The secret and raw payloads are never logged. Neither the reader nor the handler has a logger. The worker logs only ids.
- `appsettings.json` (local, gitignored) got the new keys. `HmacSecret` is empty there, which is fine with `Provider=Fake`.
