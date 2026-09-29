# Implementation — [E7.S3] Free tier limits (#87)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/Elmanhg.Domain/Lessons/LessonPosition.cs` | 6 | Projection record + `Of(Lesson)` |
| `api/Elmanhg.Domain/Lessons/LessonAccess.cs` | 26 | Pure rule: first N published lessons per unit (Order, CreationDate, Id) |
| `api/Elmanhg.Application/Subscriptions/Shared/FreeTierGate.cs` | 55 | The only thrower of the 3 new codes; lesson lock, daily quota, exam gate, today count |
| `api/Elmanhg.Application/Subscriptions/Shared/UsageResult.cs` | 5 | Usage result |
| `api/Elmanhg.Application/Subscriptions/GetMyUsage/GetMyUsageQuery.cs` | 6 | Query |
| `api/Elmanhg.Application/Subscriptions/GetMyUsage/GetMyUsageHandler.cs` | 29 | Handler |
| `api/Elmanhg.Tests/Application/Features/Subscriptions/SubscriptionRepositoryStub.cs` | 18 | Shared stub helper (`Stub`, `EntitledBase`) |
| `api/Elmanhg.Tests/Domain/Lessons/LessonAccessTests.cs` | 88 | A1–A7 |
| `api/Elmanhg.Tests/Application/Features/Subscriptions/Shared/FreeTierGateTests.cs` | 118 | A8–A16 |
| `api/Elmanhg.Tests/Application/Features/Subscriptions/GetMyUsage/GetMyUsageHandlerTests.cs` | 77 | A17–A20 |
| `api/Elmanhg.Tests/Application/Features/Sessions/StartQuizSession/StartQuizSessionFreeTierTests.cs` | 131 | A21–A26 |
| `api/Elmanhg.Tests/Application/Features/Sessions/SubmitAnswer/SubmitAnswerFreeTierTests.cs` | 125 | A27, A28, A30–A33 |
| `api/Elmanhg.Tests/Application/Features/Exams/StartUnitExam/StartUnitExamFreeTierTests.cs` | ~108 | A34–A36 |
| `api/Elmanhg.Tests/Application/Features/Exams/StartMultiUnitExam/StartMultiUnitExamFreeTierTests.cs` | ~115 | A37–A39 |
| `api/Elmanhg.Tests/Application/Features/Browse/GetStudentUnit/GetStudentUnitFreeTierTests.cs` | ~80 | A40–A41 |
| `api/Elmanhg.Tests/Application/Features/Browse/GetStudentLesson/GetStudentLessonFreeTierTests.cs` | ~100 | A42–A44 |
| `api/Elmanhg.Tests/Application/Features/Mastery/GetMasteryOverview/GetMasteryOverviewFreeTierTests.cs` | 82 | A45–A46 |
| `api/Elmanhg.Tests/Integration/Subscriptions/FreeTierQuizEndpointTests.cs` | 112 | A47–A51 |
| `api/Elmanhg.Tests/Integration/Subscriptions/FreeTierExamEndpointTests.cs` | 57 | A52–A54 |
| `api/Elmanhg.Tests/Integration/Subscriptions/UsageEndpointTests.cs` | 72 | A55–A58 |
| `api/Elmanhg.Tests/Integration/Browse/FreeTierBrowseEndpointTests.cs` | 64 | A59–A61 |
| `web/src/features/subscription/api/paywall.ts` | 11 | W1 code → reason |
| `web/src/features/subscription/api/invalidateEntitlementViews.ts` | 18 | W2 |
| `web/src/features/subscription/components/PaywallDialog.tsx` | 50 | W3 |
| `web/src/features/subscription/components/DailyQuizCounter.tsx` | 24 | W4 |
| `web/src/features/subscription/components/PlanSummaryLine.tsx` | 33 | W5 |
| `web/src/features/browse/components/LockedLessonNotice.tsx` | 21 | W6 |
| `web/src/features/subscription/api/paywall.test.ts` | | W-1, W-2 |
| `web/src/features/subscription/api/invalidateEntitlementViews.test.ts` | | W-3 |
| `web/src/features/subscription/components/PaywallDialog.test.tsx` | | W-4–W-7 |
| `web/src/features/mastery/pages/StudentHomePage.plan.test.tsx` | | W-8, W-9 |
| `web/src/features/quiz/pages/PracticePage.freeTier.test.tsx` | | W-10–W-14 |
| `web/src/features/quiz/pages/QuizPage.freeTier.test.tsx` | | W-15–W-18 |
| `web/src/features/browse/pages/UnitPage.freeTier.test.tsx` | | W-19, W-20 |
| `web/src/features/browse/pages/LessonPage.freeTier.test.tsx` | | W-21, W-22 |
| `web/src/features/exam/pages/ExamStartPage.freeTier.test.tsx` | | W-23 |
| `web/src/features/exam/pages/MultiExamBuilderPage.freeTier.test.tsx` | | W-24 |
| `web/src/features/quiz/pages/QuizResultPage.freeTier.test.tsx` | | W-25 |
| `web/src/shared/api/generated/model/usageResult.ts` | | Orval output |

## Files modified
| Path | Change |
|---|---|
| `SubscriptionsOptions.cs`, `DependencyInjection.cs` | `DailyQuotaTimeZone` (default `Africa/Cairo`) + startup validation |
| `ErrorCodes.cs`, `Messages.ar.resx`, `Messages.en.resx` | `QUIZ_DAILY_LIMIT_REACHED`, `LESSON_LOCKED`, `EXAM_REQUIRES_SUBSCRIPTION` |
| `ILessonRepository.cs`, `LessonRepository.cs` | `GetPublishedPositionsAsync`, `GetPublishedSiblingPositionsAsync` |
| `ISessionRepository.cs`, `SessionRepository.cs` | `CountQuizAttemptsOnDayAsync` (parameterised `SqlQuery`, `AT TIME ZONE`) |
| `StartQuizSessionHandler.cs`, `SubmitAnswerHandler.cs`, `StartUnitExamHandler.cs`, `StartMultiUnitExamHandler.cs` | New ctors and gate steps exactly as plan #8–#11 |
| `StudentUnitResult.cs`, `StudentLessonResult.cs`, both generators, `GetStudentUnitHandler.cs`, `GetStudentLessonHandler.cs` | `IsLocked`; content withheld when locked |
| `GetMasteryOverviewHandler.cs` | Free next-lesson candidates restricted to open lessons (private `OpenLessonsAsync`) |
| `SubscriptionsController.cs` | `GET /api/subscriptions/usage` (`GetMyUsage`, `Subscription.Manage`) |
| `appsettings.example.json` (+ local gitignored `appsettings.json`), `ApiFactory.cs` | `DailyQuotaTimeZone` |
| `SessionTestData.cs` | `SignedInStudentAsync` seeds an entitled Base; new `SignedInFreeStudentAsync` |
| 9 `Integration/Subscriptions/*EndpointTests.cs` + `RefundPaymentEndpointTests.cs` | `SignedInStudentAsync(` → `SignedInFreeStudentAsync(` only |
| 7 existing unit test files (StartQuiz, SubmitAnswer, StartUnitExam, StartMultiUnitExam, GetStudentLesson, GetStudentUnit, GetMasteryOverview) | Wiring only (substitutes, Base stub, options, a clock constant where the file had none); `GetStudentUnitHandlerTests` expected records gain `false` |
| `SubscriptionsOptionsTests.cs` | + A29 |
| `api/openapi/v1.json`, `web/src/shared/api/generated/**` | Regenerated |
| `postman/elmanhg.postman_collection.json` | "Get my usage" after "Get my entitlement"; Sessions/Exams/Browse/Subscriptions folder descriptions name the Free-tier 403s |
| Web: `entitlement.ts`, `index.ts`, `useFakePaymentCompletion.ts`, i18n (subscription, browse, common), `LessonListItem.tsx`, `LessonPage.tsx`, `useStartQuiz.ts`, `useQuizAnswer.ts`, `PracticeStart.tsx`, `NewPracticeButton.tsx`, `QuizRunner.tsx`, `QuizQuestionCard.tsx`, `useStartExam.ts`, `useStartMultiExam.ts`, `ExamStartActions.tsx`, `MultiExamPreview.tsx`, `StudentHomePage.tsx`, `browseFixtures.ts`, `subscriptionFixtures.ts`, `msw/server.ts` | As the plan lists |
| `docs/subscriptions.md`, `browsing.md`, `sessions.md`, `exams.md`, `mastery.md`, `claude-design-prompt.md` | As the plan's Docs table |

No migration (plan: no entity change), so no `AppDbContextTests` entry.

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| A62/A63 `FreeTierNextLessonEndpointTests` (`GetOverview_FreeStudentOnlyLockedLessonHasQuestions_ReturnsNoNextLesson`, `GetOverview_SubscribedStudentSameContent_ReturnsSecondLesson`) | Integration tests share one PostgreSQL database for the whole run (assembly fixture, no Respawn/reset). `GetLessonCountsAsync(studentId, null)` + `NextLessonRecommendation.Pick` rank **every** published lesson in the DB; a fresh student has ratio 0 on all of them, so every other test's order-1 lessons tie and win on SubjectOrder/UnitOrder/LessonOrder/LessonId. `nextLesson` null (A62) or = lesson 2 (A63) cannot be asserted deterministically; any assertion that is deterministic would not fail when the filter is removed (vacuous). | Did not create `api/Elmanhg.Tests/Integration/Mastery/FreeTierNextLessonEndpointTests.cs`. The behaviour is covered by the unit tests A45/A46 (mutation-checked). |
| `GetMasteryOverviewHandler` step: inline `candidates = ... lessons.Where(...)` | Same logic | Put the Free-only position load + filter in a private `OpenLessonsAsync` in the same file (plan allows private methods, no new class). |

## Build & test
- `dotnet build` (api/): Build succeeded, 0 warnings outside `core-libraries`. `api/openapi/v1.json` regenerated.
- `dotnet test -c Release` in `api/` with `Elmanhg.Api/appsettings.json` moved aside (restored afterwards): **Passed! total 2597, failed 0, succeeded 2597** (Docker/Testcontainers up). 61 new API tests (A1–A61 except A29 in an existing class; A29 added) — all green.
- `npm --prefix web run gen:api`: regenerated (usage endpoint, `isLocked`).
- `npm --prefix web run typecheck`: clean. `npm --prefix web run lint`: clean.
- `npm --prefix web test -- --run`: **138 files, 834 tests passed** (25 new: W-1–W-25).
- `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` in `web/`: all files pass.
- Mutation checks (production line broken, test observed failing, restored): API — quota in SubmitAnswer (A27, A47), lock in StartQuiz (A21, A49), quota in StartQuiz (A22, A48), Admin exemption in StartQuiz (A25), unit/multi exam gate (A34, A52 / A37, A53), content withholding (A42, A60), unit `isLocked` (A40, A59), mastery candidate filter (A45), time-zone validation (A29), `Math.Max` (A19), Cairo date conversion (A16), CreationDate tie-break (A3), replay exemption (A28). Web — paywall map (W-1), exam prefix (W-3), dialog count body (W-4), home plan line/counter (W-8, W-9), practice counter (W-10), practice/result/exam paywalls (W-13, W-23, W-24, W-25), quiz paywall + usage invalidation (W-16, W-17), test-mode counter hide (W-18, strengthened by seeding the usage cache so it fails when the check is removed), locked card/page (W-19, W-21, W-22). Positive-path tests (Base/Admin passes) were not individually mutated.

## Notes for review
- `StartUnitExamHandler.cs` is 118 lines (112 before this story); the gate is 5 lines inside the existing private `StartAsync`, as the plan specifies.
- `SubmitAnswerHandler` runs the Free gate after the item lookup and before the revision/answer-shape checks, per plan #9; a malformed first answer from a Free student at the limit therefore gets 403, not 422.
- D12 (progress page ungated) is an assumed product answer, as the plan flags.
- `docs/prototype.md` §8 still describes the prototype's own flow (paywall «اشترك» opens the fake checkout directly); it documents the prototype, not the app, so it was left unchanged.
- Integration quota tests depend on "today in Africa/Cairo" at run time; a run straddling Cairo midnight could split the counts (tiny window).
- Several i18n JSON / .cs files were rewritten with LF endings by the edit scripts; git normalises them (autocrlf) and prettier `--end-of-line auto` passes.
