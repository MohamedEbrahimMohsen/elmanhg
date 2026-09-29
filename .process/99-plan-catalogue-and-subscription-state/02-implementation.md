# Implementation — [E10.S1] Plan catalogue and subscription state (#99)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| api/Elmanhg.Domain/SharedKernel/Money.cs | 3 | `Money(long AmountMinor, string Currency)` |
| api/Elmanhg.Domain/Subscriptions/SubscriptionPlan.cs, BillingPeriod.cs, PlanTier.cs, PaymentStatus.cs | 3 each | enums |
| api/Elmanhg.Domain/Subscriptions/SubscriptionStatus.cs | 8 | enum + `HasEnded()` extension |
| api/Elmanhg.Domain/Subscriptions/Subscription.cs | 55 | aggregate: `Start`, `EntitledUntil`, `IsEntitledAt`, `EnsurePeriod` |
| api/Elmanhg.Domain/Subscriptions/Subscription.Lifecycle.cs | 57 | `Renew`, `MarkPastDue`, `Cancel`, `Expire` |
| api/Elmanhg.Domain/Subscriptions/SubscriptionEntitlementSpecification.cs | 15 | SQL definition of "entitled" |
| api/Elmanhg.Domain/Subscriptions/StudentEntitlement.cs | 26 | `Resolve`, Tier, HasAskTeacher |
| api/Elmanhg.Domain/Subscriptions/Payment.cs | 71 | `Create`, `MarkSucceeded`, `MarkFailed`, `EnsurePending` |
| api/Elmanhg.Domain/Subscriptions/ISubscriptionRepository.cs, IPaymentRepository.cs | 5 each | repository ports |
| api/Elmanhg.Application/Shared/Options/SubscriptionsOptions.cs | 46 | `Subscriptions` section |
| api/Elmanhg.Application/Shared/Options/PlanPriceOptions.cs | 8 | per-period price |
| api/Elmanhg.Application/Subscriptions/GetPlanCatalogue/GetPlanCatalogueQuery.cs, …Handler.cs | 6 / 14 | anonymous catalogue |
| api/Elmanhg.Application/Subscriptions/GetMyEntitlement/GetMyEntitlementQuery.cs, …Handler.cs | 6 / 23 | student entitlement |
| api/Elmanhg.Application/Subscriptions/GetMyPayments/GetMyPaymentsQuery.cs, …Validator.cs, …Handler.cs | 7 / 18 / 34 | paged completed payments |
| api/Elmanhg.Application/Subscriptions/Shared/*Result.cs (PlanCatalogue, FreePlan, BasePlan, AskTeacherPlan, PlanPrice, Entitlement, Subscription, Payment) | 3–6 each | results |
| api/Elmanhg.Application/Subscriptions/Shared/PlanCatalogueResultGenerator.cs | 19 | catalogue mapping |
| api/Elmanhg.Application/Subscriptions/Shared/EntitlementResultGenerator.cs | 22 | limits per tier |
| api/Elmanhg.Application/Subscriptions/Shared/StudentEntitlementLoader.cs | 14 | the entry point for #87/#94 gates |
| api/Elmanhg.Application/Subscriptions/Shared/PaymentResultGenerator.cs | 8 | payment mapping |
| api/Elmanhg.Infrastructure/Subscriptions/SubscriptionRepository.cs, PaymentRepository.cs | 7 each | repositories |
| api/Elmanhg.Infrastructure/Migrations/20260928235403_AddSubscriptionsAndPayments.cs (+ .Designer.cs) | 118 / 1936 | 2 × CreateTable + 4 indexes + FKs (Down drops) |
| api/Elmanhg.Api/Controllers/Subscriptions/PlansController.cs | 22 | `GET /api/plans` [AllowAnonymous] |
| api/Elmanhg.Api/Controllers/Subscriptions/SubscriptionsController.cs | 34 | `GET /api/subscriptions/entitlement`, `/payments` |
| api/Elmanhg.Tests/Builders/SubscriptionBuilder.cs | 62 | builder via domain methods only |
| api/Elmanhg.Tests/Domain/Subscriptions/SubscriptionTests.cs | 173 | D1–D14 |
| api/Elmanhg.Tests/Domain/Subscriptions/SubscriptionEntitlementSpecificationTests.cs | 44 | D15–D16 |
| api/Elmanhg.Tests/Domain/Subscriptions/StudentEntitlementTests.cs | 70 | D17–D22 |
| api/Elmanhg.Tests/Domain/Subscriptions/PaymentTests.cs | 107 | D23–D29 |
| api/Elmanhg.Tests/Application/Features/Subscriptions/SubscriptionsOptionsTests.cs | 96 | A1–A5 |
| api/Elmanhg.Tests/Application/Features/Subscriptions/GetPlanCatalogue/GetPlanCatalogueHandlerTests.cs | 43 | A6–A7 |
| api/Elmanhg.Tests/Application/Features/Subscriptions/GetMyEntitlement/GetMyEntitlementHandlerTests.cs | 114 | A8–A14 |
| api/Elmanhg.Tests/Application/Features/Subscriptions/GetMyPayments/GetMyPaymentsHandlerTests.cs | 85 | A15–A17 |
| api/Elmanhg.Tests/Application/Features/Subscriptions/GetMyPayments/GetMyPaymentsValidatorTests.cs | 44 | A18–A21 |
| api/Elmanhg.Tests/Integration/Subscriptions/SubscriptionTestData.cs | 53 | seeding helpers |
| api/Elmanhg.Tests/Integration/Subscriptions/PlanCatalogueEndpointTests.cs | 42 | I1–I2 |
| api/Elmanhg.Tests/Integration/Subscriptions/EntitlementEndpointTests.cs | 97 | I3–I8 |
| api/Elmanhg.Tests/Integration/Subscriptions/PaymentHistoryEndpointTests.cs | 69 | I9–I12 |
| api/Elmanhg.Tests/Integration/Subscriptions/PaymentPersistenceTests.cs | 54 | I13–I14 |
| web/src/features/subscription/index.ts, locales.ts | 2 / 4 | barrel, locales |
| web/src/features/subscription/i18n/en.json, ar.json | 67 each | strings (plan #58/#59 verbatim) |
| web/src/features/subscription/pages/SubscriptionPage.tsx | 47 | page |
| web/src/features/subscription/components/SubscribeHeader.tsx | 16 | aurora header, live servable count |
| web/src/features/subscription/components/CurrentPlanCard.tsx | 31 | current plan |
| web/src/features/subscription/components/SubscriptionStatusLine.tsx | 27 | status line per subscription |
| web/src/features/subscription/components/PlanCard.tsx | 42 | one plan card |
| web/src/features/subscription/components/PlanCardGrid.tsx | 57 | Free / Base / Ask a Teacher |
| web/src/features/subscription/components/PaymentHistorySection.tsx | 55 | paged log, hidden when empty |
| web/src/features/subscription/components/PaymentHistoryTable.tsx | 61 | table |
| web/src/features/subscription/hooks/useSubscriptionSearch.ts | 15 | URL `paymentsPage` |
| web/src/features/subscription/hooks/usePaymentHistory.ts | 12 | `useGetMyPayments`, page size 10 |
| web/src/features/subscription/schemas/subscriptionSearchSchema.ts | 7 | Zod search |
| web/src/features/subscription/api/entitlement.ts | 10 | `planLabelKey` |
| web/src/features/subscription/schemas/subscriptionSearchSchema.test.ts | 13 | W4–W5 |
| web/src/features/subscription/api/entitlement.test.ts | 17 | W6–W8 |
| web/src/features/subscription/pages/SubscriptionPage.test.tsx | 138 | W9–W16 |
| web/src/features/subscription/pages/SubscriptionPage.payments.test.tsx | 103 | W17–W20 |
| web/src/test/subscriptionFixtures.ts | 97 | typed fixtures |
| docs/subscriptions.md | 87 | contract doc (all listed sections) |

## Files modified
| Path | Change |
|---|---|
| api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs | `// SUBSCRIPTIONS` group, 6 domain codes |
| api/Elmanhg.Application/Exceptions/ErrorCodes.cs | `// SUBSCRIPTIONS` group, 2 validator codes |
| api/Elmanhg.Api/Resources/Messages.ar.resx, Messages.en.resx | 8 keys each (plan strings) |
| api/Elmanhg.Application/DependencyInjection.cs | `SubscriptionsOptions` registration with the BasePrices `Validate` lambda, `ValidateOnStart` |
| api/Elmanhg.Infrastructure/DependencyInjection.cs | 2 repositories + usings |
| api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs | constants, DbSets, `ConfigureSubscriptions`, 2 soft-delete filters; no new catch clause |
| api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs | regenerated |
| api/Elmanhg.Api/appsettings.example.json | `Subscriptions` section (placeholder prices) |
| api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs | 16 `Subscriptions:*` keys incl. required prices |
| api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs | `twentieth => …_AddSubscriptionsAndPayments` |
| api/openapi/v1.json | regenerated by `dotnet build` |
| postman/elmanhg.postman_collection.json | new `Subscriptions` folder after `Progress` (3 requests; catalogue is `noauth`) |
| web/src/routes/student/subscription.tsx | placeholder replaced with `SubscriptionPage` + `validateSearch` |
| web/src/app/i18n.ts | `subscription` namespace |
| web/src/shared/lib/format.ts | `formatMoney` |
| web/src/shared/lib/format.test.ts | W1–W3 added (import line extended; no existing test changed) |
| web/src/shared/api/generated/** | regenerated (`plans/`, `subscriptions/`, 16 model files, zod) |
| web/orval.config.ts | `GetMyPayments: { zod: { generate: { query: false } } }` — see Deviations |
| docs/PRD.md | §11.1 paragraph, §11.2 States line, §15 two lines, §19 items 1 and 5 |
| docs/backlog.json | E10.S1 gains the page task; E10.S2 task renamed |
| api/Elmanhg.Api/appsettings.json (local, gitignored) | `Subscriptions` section with placeholder prices, after the tests passed |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Modify only the listed files; `web/orval.config.ts` is not listed | Orval's zod generator emits `zod.stringFormat(...).default(<number>)` for the `pageNumber`/`pageSize` query params, which fails `tsc -b` (TS2769). The repo already works around this for every paged GET (`GetAuditLogs`, `GetQuestions`, `GetValidationQueue`, `GetSessionHistory`). | Added `GetMyPayments: { zod: { generate: { query: false } } }` to `web/orval.config.ts`, the same fix as its siblings, then regenerated. |
| I11 asserts `totalCount` 2 | `PageData` serialises `totalItems` (there is no `totalCount`), as `SessionHistoryEndpointTests` asserts | Asserted `totalItems` == 2. |
| W19 clicks "Next" | The shared `Pagination` button is named "Next page" | Clicked "Next page"; also asserted the URL gains `paymentsPage: 2`. |
| `SubscriptionTestData` lists three helpers | The persistence test (I13) needs an unsaved Failed payment with a chosen transaction id | Added a public `NewPayment(studentId, amountMinor)` factory and a `Currency` const to `SubscriptionTestData`; no new file. |
| `SubscriptionBuilder.Build()` "reaches the status through domain methods" | A switch statement would break the "switch expressions only" rule | Used a switch expression returning an `Action` over the same domain methods. |

## Build & test
- `dotnet build api/Elmanhg.slnx` → `0 Warning(s) 0 Error(s)`; `api/openapi/v1.json` regenerated.
- `dotnet ef migrations add AddSubscriptionsAndPayments -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api` → Done; Up contains only `CreateTable` ×2, `CreateIndex` ×4 (incl. filtered unique `IX_Payments_PaymobTransactionId`) and FKs.
- CI parity: moved `api/Elmanhg.Api/appsettings.json` aside, `dotnet test api/ -c Release` → `total: 2080, failed: 0, succeeded: 2080, skipped: 0`; file restored. (Subscriptions namespace alone: 90 test cases incl. theory rows.)
- `npm --prefix web run gen:api` → OK, re-run produces no further diff.
- `npm --prefix web run typecheck` → clean. `npm --prefix web run lint` → clean (`--max-warnings=0`).
- `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` → "All matched files use Prettier code style!"
- `npm --prefix web test -- --run` → `Test Files 108 passed (108)`, `Tests 668 passed (668)`.
- Mutation checks (each restored afterwards): spec Cancelled `> now` → `> lapsedBefore` failed D15 (Cancelled, +1d); dropping `Status != Pending` from the payments filter failed A16 and I11; status line always using `entitledUntil` failed W10; removing the empty-list `return null` failed W17.
- `docker info` → 29.6.2 (Testcontainers ran). `ai/` untouched, so pytest not run.

## Notes for review
- Money: `long AmountMinor` + `Currency` everywhere; `formatMoney` divides by 100 only for display. No decimal/float for money.
- Morabh: nothing copied. The `Payment.MarkSucceeded/MarkFailed` → `PAYMENT_NOT_PENDING` guard mirrors the shape of `Morabh.Domain/Orders/PaymentAttempt.cs` (`Approve`/`Invalidate` → `PAYMENT_ATTEMPT_NOT_PENDING`), as the plan cites.
- `/api/plans` is `[AllowAnonymous]`; `EndpointAuthorizationTests` accepts that. The other two actions use `DefaultCodes.SubscriptionManage` (Student only).
- `RegularExpression("^[A-Z]{3}$")` on `Currency` validates configuration only, not user input.
- `bg-aurora` is used only in `SubscribeHeader`. Payment status badges use `bg-success`/`bg-danger` with `text-surface`; axe runs with colour-contrast disabled repo-wide (`src/test/axe.ts`).
- The dev must copy the `Subscriptions` section from `appsettings.example.json` into any other local `appsettings.json`, or startup fails validation (by design: prices have no code default). Done for this machine.
- Integration seeds use `DateTimeOffset.UtcNow` in Arrange only; `Payment.CreationDate` (set by `AuditEntity`) drives newest-first ordering.
