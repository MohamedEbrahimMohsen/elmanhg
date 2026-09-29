# Implementation — Refunds and payment log (#102, E10.S4)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| api/Elmanhg.Domain/Subscriptions/Payment.Refund.cs | 64 | Refund and review-resolution state, `NeedsReview`, `IsRefundable`, `IsRefundReplay`, `EnsureRefundable`, `MarkRefunded`, `ResolveReview` |
| api/Elmanhg.Domain/Subscriptions/Subscription.Refund.cs | 29 | `RevokePaidPeriod(periodMonths, revokedAt)` |
| api/Elmanhg.Application/Shared/Payments/PaymentRefundRequest.cs | 5 | Gateway refund request |
| api/Elmanhg.Application/Shared/Payments/PaymentRefund.cs | 3 | Gateway refund result |
| api/Elmanhg.Application/Shared/Payments/PaymentNotificationKind.cs | 3 | `Charge / Reversal / Other` |
| api/Elmanhg.Application/Subscriptions/Shared/PaymentRefundSettlement.cs | 12 | Mark refunded + revoke paid period (shared by admin refund and webhook) |
| api/Elmanhg.Application/Subscriptions/ProcessPaymentNotification/ProcessPaymentNotificationHandler.Reversal.cs | 55 | `ReverseAsync` (Decision 8) |
| api/Elmanhg.Application/Payments/Shared/AdminPaymentResult.cs | 6 | Admin result record |
| api/Elmanhg.Application/Payments/Shared/AdminPaymentResultGenerator.cs | 9 | Mapper |
| api/Elmanhg.Application/Payments/GetPaymentLog/GetPaymentLogQuery.cs | 8 | Query |
| api/Elmanhg.Application/Payments/GetPaymentLog/GetPaymentLogValidator.cs | 26 | Validator |
| api/Elmanhg.Application/Payments/GetPaymentLog/GetPaymentLogFilter.cs | 27 | Filter expression |
| api/Elmanhg.Application/Payments/GetPaymentLog/GetPaymentLogHandler.cs | 33 | Paged log + batch user load |
| api/Elmanhg.Application/Payments/RefundPayment/RefundPaymentCommand.cs | 12 | Auditable `Payment.Refund` |
| api/Elmanhg.Application/Payments/RefundPayment/RefundPaymentValidator.cs | 24 | Validator |
| api/Elmanhg.Application/Payments/RefundPayment/RefundPaymentHandler.cs | 44 | Handler |
| api/Elmanhg.Application/Payments/ResolvePaymentReview/ResolvePaymentReviewCommand.cs | 12 | Auditable `Payment.ResolveReview` |
| api/Elmanhg.Application/Payments/ResolvePaymentReview/ResolvePaymentReviewValidator.cs | 13 | Validator |
| api/Elmanhg.Application/Payments/ResolvePaymentReview/ResolvePaymentReviewHandler.cs | 27 | Handler |
| api/Elmanhg.Infrastructure/Payments/Paymob/PaymobPaymentGateway.Refund.cs | 90 | Real refund adapter (no retries, explicit User-Agent) |
| api/Elmanhg.Infrastructure/Payments/Paymob/PaymobRefundRequest.cs | 5 | Wire request |
| api/Elmanhg.Infrastructure/Payments/Paymob/PaymobRefundResponse.cs | 6 | Wire response |
| api/Elmanhg.Infrastructure/Migrations/20260929034314_AddPaymentRefunds.cs (+ .Designer.cs) | 120 / 1996 | Additive migration: 7 nullable columns, 3 indexes |
| api/Elmanhg.Api/Controllers/Payments/PaymentsController.cs | 47 | 3 endpoints, `Payments.Manage` |
| api/Elmanhg.Api/Controllers/Payments/Requests.cs | 3 | `RefundPaymentRequest` |
| api/Elmanhg.Tests/Fixtures/Paymob/transaction-refund.json | 1 | Refund child callback fixture |
| api/Elmanhg.Tests/Integration/Payments/PaymentsTestData.cs | 49 | Test helper |
| api/Elmanhg.Tests/Application/Features/Payments/RefundPayment/RefundPaymentHandlerTests.cs | 160 | 8 tests |
| api/Elmanhg.Tests/Application/Features/Payments/RefundPayment/RefundPaymentValidatorTests.cs | 63 | 6 methods (8 cases) |
| api/Elmanhg.Tests/Application/Features/Payments/ResolvePaymentReview/ResolvePaymentReviewHandlerTests.cs | 89 | 4 tests |
| api/Elmanhg.Tests/Application/Features/Payments/ResolvePaymentReview/ResolvePaymentReviewValidatorTests.cs | 26 | 2 tests |
| api/Elmanhg.Tests/Application/Features/Payments/GetPaymentLog/GetPaymentLogHandlerTests.cs | 61 | 2 tests |
| api/Elmanhg.Tests/Application/Features/Payments/GetPaymentLog/GetPaymentLogFilterTests.cs | 129 | 7 methods (10 cases) |
| api/Elmanhg.Tests/Application/Features/Payments/GetPaymentLog/GetPaymentLogValidatorTests.cs | 74 | 7 methods (8 cases) |
| api/Elmanhg.Tests/Application/Features/Payments/Shared/AdminPaymentResultGeneratorTests.cs | 31 | 1 test |
| api/Elmanhg.Tests/Application/Features/Subscriptions/Shared/PaymentRefundSettlementTests.cs | 43 | 2 tests |
| api/Elmanhg.Tests/Application/Features/Subscriptions/ProcessPaymentNotification/ProcessPaymentNotificationReversalTests.cs | 181 | 9 tests |
| api/Elmanhg.Tests/Infrastructure/Payments/PaymobPaymentGatewayRefundTests.cs | 134 | 11 tests |
| api/Elmanhg.Tests/Integration/Payments/PaymentLogEndpointTests.cs | 103 | 6 methods (7 cases) |
| api/Elmanhg.Tests/Integration/Payments/RefundPaymentEndpointTests.cs | 135 | 7 tests |
| api/Elmanhg.Tests/Integration/Payments/ResolvePaymentReviewEndpointTests.cs | 56 | 3 tests |
| api/Elmanhg.Tests/Integration/Subscriptions/PaymobRefundWebhookEndpointTests.cs | 120 | 5 tests |
| web/src/routes/admin/payments.tsx | 7 | Route |
| web/src/features/payments/index.ts | 2 | Barrel |
| web/src/features/payments/locales.ts | 4 | Locales |
| web/src/features/payments/i18n/en.json, ar.json | 91 / 91 | UI strings (plan table) |
| web/src/features/payments/api/paymentLogParams.ts | 49 | Search → params, active filters, page mapping |
| web/src/features/payments/schemas/paymentLogSearchSchema.ts | 19 | URL search schema |
| web/src/features/payments/schemas/paymentLogFiltersSchema.ts | 22 | Filter form schema |
| web/src/features/payments/schemas/refundPaymentSchema.ts | 14 | Refund reason schema |
| web/src/features/payments/hooks/usePaymentLogSearch.ts | 52 | URL state |
| web/src/features/payments/hooks/usePaymentLog.ts | 10 | Log query |
| web/src/features/payments/hooks/usePaymentReviewCount.ts | 8 | Review count query |
| web/src/features/payments/hooks/useRefundPayment.ts | 31 | Refund mutation (Idempotency-Key header) |
| web/src/features/payments/hooks/useResolvePaymentReview.ts | 37 | Keep-payment mutation |
| web/src/features/payments/components/PaymentLogTabs.tsx | 47 | Pill sub-tabs with count |
| web/src/features/payments/components/PaymentLogSelectField.tsx | 40 | Labelled select |
| web/src/features/payments/components/PaymentLogFilters.tsx | 67 | Filter form |
| web/src/features/payments/components/PaymentLogTable.tsx | 42 | Table |
| web/src/features/payments/components/PaymentLogRow.tsx | 102 | Row |
| web/src/features/payments/components/PaymentStatusBadge.tsx | 24 | Status pill |
| web/src/features/payments/components/PaymentLogEmptyState.tsx | 32 | Three empty variants |
| web/src/features/payments/components/PaymentLogTableSkeleton.tsx | 20 | Skeleton |
| web/src/features/payments/components/RefundPaymentDialog.tsx | 68 | Refund form dialog |
| web/src/features/payments/components/ResolveReviewDialog.tsx | 36 | Keep dialog |
| web/src/features/payments/pages/PaymentLogPage.tsx | 115 | Page |
| web/src/test/paymentFixtures.ts | 38 | Fixtures |
| web/src/shared/form/TextAreaField.tsx | 53 | Moved from questions (content unchanged) |
| web/src/features/payments/api/paymentLogParams.test.ts | 42 | 2 tests |
| web/src/features/payments/schemas/paymentLogSearchSchema.test.ts | 52 | 2 tests |
| web/src/features/payments/schemas/paymentLogFiltersSchema.test.ts | 36 | 3 tests |
| web/src/features/payments/schemas/refundPaymentSchema.test.ts | 22 | 3 tests |
| web/src/features/payments/pages/PaymentLogPage.test.tsx | 229 | 12 tests |
| web/src/features/payments/pages/PaymentLogPage.actions.test.tsx | 144 | 5 tests |

Generated (not hand-written): `web/src/shared/api/generated/payments/**`, `zod/payments/**`, `model/{adminPaymentResult,getPaymentLogParams,pageDataOfAdminPaymentResult,paymentReviewReason,refundPaymentHeaders,refundPaymentRequest}.ts`.

## Files modified
| Path | Change |
|---|---|
| api/Elmanhg.Domain/Subscriptions/Payment.cs | `partial`; `MarkSucceeded` refuses Refunded; `FlagForReview` clears the resolution |
| api/Elmanhg.Domain/Subscriptions/PaymentStatus.cs, PaymentReviewReason.cs | `Refunded`; `PartialRefundAtProvider` |
| api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs | 3 domain codes |
| api/Elmanhg.Domain/SharedKernel/DefaultCodes.cs | `PaymentsManage` |
| api/Elmanhg.Application/Exceptions/ErrorCodes.cs | 12 application codes |
| api/Elmanhg.Application/Shared/Options/SubscriptionsOptions.cs | 3 keys with defaults and ranges |
| api/Elmanhg.Application/Shared/Payments/IPaymentGateway.cs | `RefundAsync` |
| api/Elmanhg.Application/Shared/Payments/PaymentNotification.cs | `bool IsRefundOrVoid` → `PaymentNotificationKind Kind` |
| api/Elmanhg.Application/Subscriptions/Shared/PaymentNotificationOutcome.cs | `Refunded`, `FlaggedForReview` |
| api/Elmanhg.Application/Subscriptions/ProcessPaymentNotification/ProcessPaymentNotificationHandler.cs | `partial`; `Other` ignored; `Reversal` → `ReverseAsync`; `IsBound`; Refunded → OutOfOrder |
| api/Elmanhg.Infrastructure/Payments/FakePaymentGateway.cs | `RefundAsync` (production lock) |
| api/Elmanhg.Infrastructure/Payments/Paymob/PaymobPaymentGateway.cs | `partial` only |
| api/Elmanhg.Infrastructure/Payments/Paymob/PaymobNotificationReader.cs | Signed-only `KindOf`, `OptionalFlag`; `RefundOrVoidProperties` removed |
| api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs | 2 index constants, `RefundTransactionId` length, 3 indexes, 2 ignores, unique-violation catch covers the refund index |
| api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs | Regenerated |
| api/Elmanhg.Api/Authorization/PermissionMatrixPolicies.cs | `Payments.Manage` → Admin |
| api/Elmanhg.Api/Resources/Messages.en.resx, Messages.ar.resx | 15 keys each |
| api/Elmanhg.Api/appsettings.example.json (+ local gitignored appsettings.json) | 3 `Subscriptions` keys |
| api/openapi/v1.json | Regenerated by `dotnet build` |
| api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs | 3 keys via `UseSetting` dictionary |
| api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs | `_AddPaymentRefunds` appended |
| api/Elmanhg.Tests/Integration/Authorization/PermissionMatrixPolicyTests.cs | 3 rows |
| api/Elmanhg.Tests/Domain/Subscriptions/PaymentTests.cs | +10 test methods (tests 1–10) |
| api/Elmanhg.Tests/Domain/Subscriptions/SubscriptionTests.cs | +6 tests (11–16) |
| api/Elmanhg.Tests/Application/Features/Subscriptions/ProcessPaymentNotification/ProcessPaymentNotificationHandlerTests.cs | 4 positional `false` → `PaymentNotificationKind.Charge`; `Notify(kind)`; `Handle_RefundOrVoid_ReturnsIgnored` → `Handle_OtherKind_ReturnsIgnored`; +2 tests (57, 58) |
| api/Elmanhg.Tests/Infrastructure/Payments/PaymobNotificationReaderTests.cs | test 68 expects `Charge`; `Read_RefundedTransaction_FlagsRefundOrVoid` → `Read_RefundedParentTransaction_IsOther`; +4 tests |
| api/Elmanhg.Tests/Infrastructure/Payments/FakePaymentGatewayTests.cs | +2 tests |
| api/Elmanhg.Tests/Integration/Subscriptions/PaymentPersistenceTests.cs | +1 test |
| api/Elmanhg.Tests/Integration/Subscriptions/PaymentHistoryEndpointTests.cs | +1 test |
| api/Elmanhg.Tests/Fixtures/Paymob/PaymobPayloads.cs | `RefundChild` constant |
| web/orval.config.ts | `output.headers: true`; `GetPaymentLog` zod `query: false` |
| web/src/shared/api/generated/** | Regenerated (`npm run gen:api`); no drift on a second run |
| web/src/routeTree.gen.ts | Regenerated (`npx vite build`) |
| web/src/app/i18n.ts | `payments` namespace |
| web/src/features/session/permissions.ts, permissions.test.ts | `paymentsManage` (admin); +1 test |
| web/src/features/shell/navConfig.ts, i18n/en.json, ar.json | Admin nav item "Payments" / "المدفوعات" |
| web/src/features/shell/pages/MorePage.test.tsx | Expected list includes `Payments` |
| web/src/shared/i18n/en.json, ar.json | 15 `errors.*` |
| web/src/features/subscription/components/PaymentHistoryTable.tsx | `statusClasses` record (Refunded neutral) |
| web/src/features/subscription/components/CheckoutStatusCard.tsx | Refunded branch |
| web/src/features/subscription/i18n/en.json, ar.json | `payments.Refunded`, `checkoutResult.refunded*` |
| web/src/features/subscription/pages/SubscriptionPage.payments.test.tsx, CheckoutResultPage.test.tsx | +1 test each |
| web/src/features/questions/components/FillBlanksField.tsx, ShortAnswerFields.tsx, ValidationDecisionPanel.tsx | Import `TextAreaField` from `@/shared/form/TextAreaField` |
| web/src/features/questions/components/TextAreaField.tsx | Deleted (moved) |
| postman/elmanhg.postman_collection.json | New `AdminPayments` folder after `AuditLogs` (6 requests, in state order) |
| docs/PRD.md | §11.2 (Paymob-verified events bullet + log/refund bullet), §15 Payment line, §16 row, §17 rule 12 |
| docs/subscriptions.md | Config keys, Payments (Refunded, refund and review fields), new Refunds and Admin payment log sections, Webhook rows + signed-only classification, API rows, known limit, "For later stories" |
| docs/paymob.md | Intro, §1, §5 verify list, §8 classification/reversals/status codes, new §9 Refunds |
| docs/audit-log.md | `Payment.Refund`, `Payment.ResolveReview` rows; ProcessNotification note mentions reversals |
| docs/claude-design-prompt.md | §4 `#/admin/payments` bullet; §5 rule 11 "or an admin refund" |
| docs/prototype.md | Admin walkthrough line |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Controller signature lists `bool needsReview = false` between `plan` and `studentId` | C# requires optional parameters after required ones; the nullable ones have no default | Moved `needsReview`, `pageNumber`, `pageSize` (all defaulted) to the end. Query-string names and the OpenAPI contract are unchanged. |
| #56 `useRefundPayment` exposes `refund(paymentId, reason, idempotencyKey, { onSuccess, onError })`; #66 dialog calls `applyServerErrors` in `onError` | The shared `Form` already maps a thrown submit error through `serverErrorFields` → `applyServerErrors` (repo pattern) | `refund(paymentId, reason, idempotencyKey)` returns the `mutateAsync` promise; the dialog awaits it inside `Form onSubmit` with `serverErrorFields = { PAYMENT_REFUND_REASON_REQUIRED/TOO_LONG: 'reason' }`; other codes (e.g. `PAYMENT_REFUND_DECLINED`) show in `FormRootError` inside the dialog. Same observable behaviour; test 136 covers it. |
| #62 Plan cell `plan.X · period.Y` | Building one string from two `t()` calls is §6.17 DON'T (concatenation) | Plan on one line, period as a muted caption under it. |
| DoD: "`is_refund`/`is_void` do not appear in `PaymobNotificationReader`" | The plan's own file contract for the reader prescribes the WHY comment "…is_refund/is_void are unsigned." | Kept the prescribed comment; the flags are never read (no property constant, no lookup). |
| #53 `applyFilters` "drops empty values" | Form values for status/plan are plain strings; the route search is typed with enums | `applyFilters` runs the values through `paymentLogSearchSchema.parse` (invalid → dropped) before navigating, so the URL stays typed. |

## Build & test
- `dotnet build` (api): **Build succeeded**, only the pre-existing core-libraries nullable warnings. `api/openapi/v1.json` regenerated.
- `dotnet ef migrations add AddPaymentRefunds`: generated additive migration (7 nullable columns, `IX_Payments_CreationDate`, `IX_Payments_OpenReview` filtered, `IX_Payments_RefundTransactionId` unique filtered; no drops).
- `dotnet test` (Debug): `total: 2411, failed: 0, succeeded: 2411, skipped: 0`.
- CI parity: `api/Elmanhg.Api/appsettings.json` moved aside, `dotnet test -c Release`: **`Test run summary: Passed! total: 2411 failed: 0 succeeded: 2411 skipped: 0`**; file restored afterwards.
- `npm --prefix web run gen:api`: regenerated; a second run produced no diff.
- `npm run typecheck`: clean. `npm run lint` (`eslint . --max-warnings=0`): clean.
- `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto`: "All matched files use Prettier code style!"
- `npm test -- --run`: **Test Files 119 passed (119), Tests 736 passed (736)**.
- `npm run build`: built.
- Mutation checks (each broken on purpose, suite run, then restored and re-verified):
  - api round 1: reader `(true,false)` → `Charge`; refund replay short-circuit disabled; `RevokePaidPeriod` no longer expires → 12 tests failed (reader, handler, webhook integration, domain).
  - api round 2: needs-review filter ignores resolution; 429 treated as decline; partial reversal branch disabled → 5 tests failed.
  - web: `Idempotency-Key` header dropped; refund button always shown; review-view empty state collapsed to no-data → 3 tests failed.
- `ai/` not touched; pytest not run.

## Notes for review
- **Test counts.** New api test methods: 16 domain, 44 application/unit, 11 refund adapter, 2 fake gateway, 5 reader (1 replaced), 22 integration (+3 policy matrix rows, +1 migration entry). Web: 27 new tests in `features/payments`, +1 permissions, +2 subscription, MorePage modified.
- `PaymobNotificationReader.cs` is 109 lines (was 96); it is one cohesive reader. `ProcessPaymentNotificationHandler.cs` is 81.
- `ReverseAsync` uses a switch expression that yields a nullable outcome and throws for Pending; Succeeded falls through (`_ => null`).
- The refund adapter checks "missing id → 503" before "success/pending → declined", in the plan's order. A 200 `{ success:false }` without an id therefore maps to 503, not 400. Worth confirming against a live account (§5 list).
- The admin integration tests share one database with other tests, so every log assertion filters by the seeded `studentId`, or by a unique Paymob reference.
- `PaymentLogPage.test.tsx` is 229 lines; test files over 200 lines already exist (e.g. `QuestionEditorPage.test.tsx` 321).
- The review-count tab's accessible name is "Needs review2" in jsdom (no whitespace between the text and the badge span), so the test matches `/^Needs review\s*2$/`.
- Refund invalidates every `GetPaymentLog` query (prefix key), so the tab count and the list both refetch.
- Postman refund requests use `{{checkoutPaymentId}}` from the Subscriptions folder and need an admin `accessToken`. Running AdminPayments right after Subscriptions needs an admin sign-in between them, as the description says.
- Not committed (per instructions). `git mv` of TextAreaField was unstaged again, so the working tree shows it as a delete plus an untracked file.
