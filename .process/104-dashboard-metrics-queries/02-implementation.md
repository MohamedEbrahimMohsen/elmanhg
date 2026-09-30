# Implementation — [E11.S1] Dashboard metrics queries (#104)

Worktree `D:/Personal/elmanhg-wt/104`, branch `feature/104-dashboard-metrics-queries`. Nothing committed.

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/Elmanhg.Domain/Analytics/UserActivityDay.cs` | 22 | D1 entity, `Record` factory |
| `api/Elmanhg.Domain/Analytics/IUserActivityDayRepository.cs` | 11 | D2 |
| `api/Elmanhg.Domain/Analytics/FunnelStepCount.cs`, `FunnelTiming.cs` | 3 / 7 | D3, D4 |
| `api/Elmanhg.Domain/SharedKernel/MetricsWindow.cs`, `DailyTotal.cs` | 3 / 7 | D5, D6 |
| `api/Elmanhg.Domain/Subscriptions/PlanCount.cs`, `PaymentTotals.cs`, `SubscriptionChurnSpecification.cs` | 3 / 10 / 8 | D7–D9 |
| `api/Elmanhg.Domain/Lessons/LessonStateCount.cs` | 3 | D10 |
| `api/Elmanhg.Domain/Questions/QuestionInventoryCount.cs`, `QuestionDecisionStats.cs`, `TeacherDecisionCount.cs` | 3 / 8 / 3 | D11–D13 |
| `api/Elmanhg.Domain/Sessions/LessonAttemptOutcome.cs` | 3 | D14 |
| `api/Elmanhg.Domain/TeacherThreads/TeacherReplyStats.cs` | 8 | D15 |
| `api/Elmanhg.Infrastructure/Analytics/UserActivityDayRepository.cs` | 48 | I1 (`ON CONFLICT DO NOTHING` via `ExecuteSqlAsync`) |
| `api/Elmanhg.Infrastructure/Migrations/20260930054113_AddDashboardMetrics.cs` (+ `.Designer.cs`, snapshot) | 131 | I2: nullable `SubmittedAt` → 2 backfill `UPDATE`s → `AlterColumn` NOT NULL; `UserActivityDays`; 6 indexes. `Down` reverses. No other drop/rename |
| `api/Elmanhg.Infrastructure/Questions/QuestionRepository.Metrics.cs` | 74 | **not in plan**, see Deviations #4 |
| `api/Elmanhg.Infrastructure/Sessions/SessionRepository.Metrics.cs` | 40 | **not in plan**, see Deviations #4 |
| `api/Elmanhg.Application/Shared/Options/DashboardOptions.cs` | 26 | A1 |
| `api/Elmanhg.Application/Shared/Analytics/UserActivityBehaviour.cs` | 49 | A2, plus gate condition (a) |
| `api/Elmanhg.Application/Dashboard/Shared/*` (10 files) | 3–36 | A3–A12 |
| `api/Elmanhg.Application/Dashboard/Get{Student,Subscriber,Content,SolveRate,SuccessRate,Validation,AskTeacher,Payment,Funnel}Metrics/*` (45 files) | 3–48 | C1–C9, exactly the files listed in the plan |
| `api/Elmanhg.Api/Controllers/Dashboard/DashboardController.cs` | 102 | P1, 9 actions |
| `docs/dashboard.md` | 141 | X1 |
| Tests: `Domain/Analytics/UserActivityDayTests.cs`, `Domain/Subscriptions/SubscriptionChurnSpecificationTests.cs`, `Application/Features/Dashboard/Shared/{DashboardWindow,DashboardFilterRules,DashboardCacheBehaviour,DashboardRates}Tests.cs`, `Application/Features/Shared/Analytics/UserActivityBehaviourTests.cs`, the 9 `…/Get*Metrics/Get*MetricsHandlerTests.cs`, `Integration/Persistence/UserActivityDayPersistenceTests.cs`, `Integration/Dashboard/{DashboardAccess,UserActivityTracking,StudentMetrics,SubscriberMetrics,ContentMetrics,LearningMetrics,ValidationMetrics,AskTeacherMetrics,PaymentMetrics,FunnelMetrics}EndpointTests.cs` (tracking class is `UserActivityTrackingTests`) | 22–118 | Test plan rows 1–83 |
| `api/Elmanhg.Tests/Integration/Dashboard/DashboardTestData.cs` | 93 | The planned integration helper |
| `web/src/shared/api/generated/dashboard/**`, `…/model/*Metrics*`, `…/model/getDashboard*Params.ts`, etc. | generated | Orval regeneration |

## Files modified
| Path | Change |
|---|---|
| `Domain/Questions/QuestionDecision.cs` | `SubmittedAt` property; `Create(..., Guid decidedBy, DateTimeOffset submittedAt, DateTimeOffset decidedAt)` |
| `Domain/Questions/Question.Approval.cs` | Both `Create` calls pass `SubmittedAt` |
| `Domain/Questions/IQuestionRepository.cs`, `Sessions/ISessionRepository.cs`, `Subscriptions/ISubscriptionRepository.cs`, `Subscriptions/IPaymentRepository.cs`, `Lessons/ILessonRepository.cs`, `TeacherThreads/ITeacherThreadRepository.cs`, `TeacherThreads/ITeacherThreadSlaEventRepository.cs`, `Analytics/IFunnelEventRepository.cs` | The planned signatures, verbatim |
| `Infrastructure/Questions/QuestionRepository.cs`, `Sessions/SessionRepository.cs` | Class made `partial` only (the methods live in the `.Metrics.cs` files) |
| `Infrastructure/Subscriptions/SubscriptionRepository.cs`, `PaymentRepository.cs`, `Lessons/LessonRepository.cs`, `TeacherThreads/TeacherThreadRepository.cs`, `TeacherThreadSlaEventRepository.cs`, `Analytics/FunnelEventRepository.cs` | Planned methods implemented (LINQ `AsNoTracking`, or parameterised `SqlQuery`; no `*Raw`) |
| `Infrastructure/Data/Context/AppDbContext.cs` | `UserActivityDays` DbSet, `UserActivityDayIndex` const, `ConfigureUserActivityDays`, the 6 indexes, soft-delete filter line |
| `Infrastructure/DependencyInjection.cs` | `IUserActivityDayRepository` registration |
| `Application/DependencyInjection.cs` | `DashboardOptions` (ValidateDataAnnotations + time zone + Default ≤ Max, ValidateOnStart); `UserActivityBehaviour` and `DashboardCacheBehaviour` after `SubjectScopeBehaviour` |
| `Application/Exceptions/ErrorCodes.cs` | `// DASHBOARD` group with the 2 codes |
| `Api/Resources/Messages.ar.resx`, `Messages.en.resx` | 2 keys each (plan text) |
| `Api/appsettings.example.json` | `Dashboard` section |
| `Tests/Integration/Infrastructure/ApiFactory.cs` | 6 `Dashboard:*` keys, `CacheSeconds=0` with a one-line comment |
| `Tests/Integration/Persistence/AppDbContextTests.cs` | `thirtyThird … _AddDashboardMetrics` |
| `Tests/Domain/Questions/QuestionApprovalTests.cs`, `QuestionRejectionTests.cs` | +1 test each (rows 3, 4) |
| `api/openapi/v1.json` | Regenerated by the build (9 new paths plus schemas) |
| `web/src/shared/api/generated/{index.ts,model/index.ts,zod/index.zod.ts}` | Regenerated by `npm --prefix web run gen:api` |
| `postman/elmanhg.postman_collection.json` | New `Dashboard` folder after `AdminPayments`: 9 GETs in controller order, disabled `from`/`to` (`2026-09-01`/`2026-09-30`) and `subjectId` (`{{subjectId}}`) where the card takes them; tests `status is 200` plus `total` is a number / `steps` is an array / `generatedAt` exists |
| `docs/PRD.md` | §10.3 filter sentence replaced (plan text); §15 `UserActivityDay` line |
| `docs/analytics.md` | Intro points to `GET /api/dashboard/funnel`; heading loses "(for #104)"; range bullet added |

## Deviations
| # | Plan said | Reality | What I did |
|---|---|---|---|
| 1 | (Gate condition b) Flag the PRD change | PRD §10.3 said "All charts filterable by date range and subject." The implementation drops the subject filter on Students, Subscribers, Payments and Sign-up funnel (they have no subject dimension), and Content takes no date range (it is a snapshot). | **Dev-visible product change.** PRD §10.3 was rewritten in this change (docs-sync) with the plan's sentence, and `docs/dashboard.md` → Filters describes the rule. If the dev wants per-subject Students/Subscribers/Payments, this needs a definition (for example "students interested in the subject") and a follow-up. |
| 2 | A2: ctor `(ICurrentUserService, IUserActivityDayRepository, IMemoryCache, TimeProvider, IOptions<DashboardOptions>)`; steps 5–6 with no error handling | Gate condition (a): a failed activity write must be logged and swallowed and must never fail the request | Added `ILogger<UserActivityBehaviour<TRequest,TResponse>>` as the 6th ctor parameter. Steps 5–6 sit in a `try`/`catch (Exception)` that calls `LogWarning` (same pattern and WHY comment as Core `AuditBehaviour`). The seen-key is set only after a successful insert, so a failed write is retried on the user's next request and a successful one is never repeated (in-memory dedup per user per Cairo day, so repeat requests do not hit the DB). Added one test beyond the plan: `UserActivityBehaviourTests.Handle_RecordFails_ReturnsResponseAndRetriesOnNextRequest`. |
| 3 | A2: `private const TimeSpan SeenKeyLifetime` | C# cannot declare a `TimeSpan` const | `private static readonly TimeSpan`, with the planned WHY comment. |
| 4 | Implement the 5 new methods in `QuestionRepository.cs` and the 2 in `SessionRepository.cs` | `QuestionRepository.cs` was already 101 lines (it would reach about 175) and `SessionRepository.cs` about 125, which breaks the "no file over ~100 lines" non-negotiable | Made both classes `partial` and put the new methods in `QuestionRepository.Metrics.cs` (74) and `SessionRepository.Metrics.cs` (40), next to the originals. That is 2 files not in *Files to create*. |
| 5 | `DashboardTestData.SeedPaymentAsync(factory, …)` (signature unspecified); helper list | Tests 76–78 need the subject of `SeedServableLessonAsync`'s lesson, which that helper does not return | Added `SubjectOfLessonAsync(factory, lessonId)` to `DashboardTestData`. `SeedPaymentAsync(factory, studentId, period, periodMonths, amountMinor, status, completedAt, refundedAt?)` seeds through `Payment.Create`/`MarkSucceeded`/`MarkFailed`/`MarkRefunded`, plus a subscription for succeeded ones (FK). `SetCreationDateAsync` uses `ExecuteUpdateAsync` on `User.CreationDate`, because a normal save would restamp it. |
| 6 | Test 70: DB row Day = Cairo today | Exact equality flakes if the login crosses Cairo midnight | Asserts exactly 1 row whose Day is either the Cairo day just before or just after the login. |

## Build & test
- `dotnet build api/` → succeeded. The only warnings are the 9 pre-existing ones in `core-libraries` (Core.Notifications, Core.OTP, Core.Validation); nothing new.
- **CI parity:** `dotnet test api/ -c Release`. The worktree has no `api/Elmanhg.Api/appsettings.json` (gitignored and absent), so there was nothing to move aside. Result: `Test run summary: Passed! total: 3600 failed: 0 succeeded: 3600 skipped: 0 duration: 48s 218ms`. Testcontainers PostgreSQL ran on Docker.
- Targeted runs during development: new unit tests 100/100, dashboard integration + persistence + AppDbContext + pipeline composition 48/48.
- `dotnet format api/Elmanhg.slnx --verify-no-changes`: no findings in any file this change touches. The only findings are pre-existing whitespace ones in `api/core-libraries/**` and `Elmanhg.Tests/Builders/SubscriptionBuilder.cs(57,27)`, none of them touched here (Windows CRLF checkout).
- Guard grep (`DateTime.Now/UtcNow`, `.Result`, `.Wait()`, `new HttpClient(`, `FromSqlRaw`/`ExecuteSqlRaw`, `async void`) over the `.cs` diff: no hits.
- `npm --prefix web run gen:api` → OK (generated diff is additive only). `npm --prefix web run typecheck` → clean. `npm --prefix web run build` → exit 0.
- `npm --prefix web test -- --run` → 1083/1084. The one failure, `ExamPage.autosave.test.tsx > saves only the latest answer after quick changes`, is a timing test that this change does not touch (only generated files changed on web). The run took 341 s under load from parallel lanes. Rerun alone: `Tests 4 passed (4)`.
- `ai/`: not touched, not run.

### Mutation check (11 hand mutants; each applied, filtered tests run, source restored)
Killed (10): range `<=`→`<` MaxRangeDays; activity seen-key `Set` removed; catch narrowed to `InvalidCastException`; `!IsRetired` dropped from status buckets; `CacheSeconds == 0` bypass removed; `!IsTestMode` join removed (success rate, integration); refunded dropped from the succeeded count (SQL); student-only join removed from DAU (SQL/LINQ); `SenderId = StudentId` dropped from the reply-wait lateral (SQL); lesson `Order` sort removed.
**Survived (1):** `FunnelEventRepository` timing filter `"Landed" IS NOT NULL AND "Answered" >= "Landed"` → `"Answered" IS NOT NULL`. Test 83's seeded data (as specified) has no visitor who answers without landing, or before landing, in range. Closing this would change the plan's expected step counts `[3,2,1,1,2]`, so I left it as specified.

## Notes for review
- **Config docs:** PROGRESS says new config keys also go to `docs/deployment.md` and `deploy/*.env.example`. The plan did not list them and I did not touch them. All `Dashboard:*` keys have safe code defaults (the app runs correctly without them) and are documented in `docs/dashboard.md` → Config. `deploy/api.env.example` only lists secrets and provider switches today.
- **Activity writes on every authenticated request (first per user per day per instance).** The insert runs on the request's `AppDbContext` connection after the handler completes, outside `SaveChangesAsync`. No handler in the repo opens an explicit transaction (checked), so a failed insert cannot poison a transaction.
- `QuestionDecisions` backfill joins `QuestionRevisions` on (QuestionId, Version). A resubmit without a content edit keeps the version, so the backfilled `SubmittedAt` for such decisions is the last edit time, not the resubmission time. This is approximate for historical rows only; new decisions are exact.
- The raw SQL null filters use `({id}::uuid IS NULL OR col = {id})`: two parameters with the same value, which Npgsql types from context. Covered by integration tests both with and without `subjectId`.
- The Postman folder description contains "§" (UTF-8). The collection already mixes escaped and raw text; the JSON parses.
- `DashboardFilterRules` uses `RuleFor(x => x)`, so the 422 body's property name is empty; tests assert on `code` only.
- Test files `UserActivityBehaviourTests.cs` (118 lines) and `GetContentMetricsHandlerTests.cs` / `GetSuccessRateMetricsHandlerTests.cs` (~107) are slightly over 100 lines. The limit is applied to production code here, and several existing test classes are longer.
