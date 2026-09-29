# Plan — [E7.S3] Free tier limits (#87)

## Goal
A Free student (no entitled Base) can browse the whole tree but read only the first `FreeOpenLessonsPerUnit` Published lessons of each unit, answer at most `FreeDailyQuizQuestions` quiz questions per local day, and cannot start exams. Every limit is enforced by the server (403 with a paywall code) and shown in the UI: locked lesson cards and a locked lesson page, a daily counter on Home, the practice tab and the quiz screen, and a paywall dialog whose «اشترك» opens `/student/subscription`. Base students, Admins and test-mode sessions are never gated; the #74–#86 flows keep working for entitled students.

## Scope
**In:**
- API: `LessonPosition` + `LessonAccess` (Domain), `FreeTierGate` (Application) built on `StudentEntitlementLoader.LoadAsync` (#99), a daily quiz-attempt count query, gates on quiz start, answer submit, unit-exam start and multi-unit-exam start, `IsLocked` on browse results (content withheld when locked), locked lessons excluded from the Home "next recommended lesson", `GET /api/subscriptions/usage`, option `Subscriptions:DailyQuotaTimeZone`.
- Web: paywall dialog, daily counters, locked lesson card, locked lesson page, Home plan line, paywall on quiz start / answer / new practice / unit exam / multi-unit exam, cache refresh after a fake payment.
- Docs, OpenAPI, Orval client, Postman.

**Out:**
- Avatar quota enforcement: there are no Avatar messages yet. `UsageResult.DailyAvatarMessageLimit` exposes the limit; #91 (E8.S3) counts and enforces it through the same `StudentEntitlementLoader`.
- Gating the progress page ("full progress" is listed under Base in PRD §11.1, but the PRD defines no reduced Free view). The progress page stays ungated (D12).
- Gating exam reads (overview, attempts, preview), exam answer save and submit, and lesson openings (D9, D10).

**Deferred:** none. Nothing in this story needs credentials or an external service.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| D1 | What counts toward "10 quiz questions/day"? | New `Attempt` rows in non-test `Quiz` sessions whose `CreatedAt` falls on today's date in `Subscriptions:DailyQuotaTimeZone` (default `Africa/Cairo`). A replayed identical answer creates no row, so it does not count. Exam attempts do not count. | Matches the prototype's `quizUsedToday` (quiz attempts per local day). Egypt is the market. Uses the same SQL pattern as the streak (`AT TIME ZONE`), so there is no DST arithmetic in C#. |
| D2 | Where is the quota enforced? | (a) `SubmitAnswer`, for a new attempt only: used ≥ limit → 403 `QUIZ_DAILY_LIMIT_REACHED`. (b) `StartQuizSession`, **new** session only: used ≥ limit → same 403. Resuming an open session is allowed (answers are still gated). The size of a new session is **not** capped to the remaining quota. | Prototype: start and "تحقّق" are both paywalled when used ≥ limit. Allowing resume lets a student review answered items and continue the next day. |
| D3 | Lesson access rule | Open lessons = the first `OpenLessonsPerUnit` **Published** lessons of each unit, ordered by `Order`, then `CreationDate`, then `Id` (`LessonAccess.OpenLessonIds`). `null` (Base) = all open; `0` = none open. | PRD §11.1 "first lesson of each unit" plus the existing config key. The order matches `LessonSequence` (#85 D6) and the unit page. |
| D4 | Where is the lesson lock enforced? | `GetStudentLesson` (content withheld), `StartQuizSession` (start **and** resume), `SubmitAnswer` (new attempt): 403 `LESSON_LOCKED`. `GetStudentUnit` flags each lesson with `isLocked`. | Content is the paid asset, so it never leaves the server for a locked lesson. The submit check covers a quiz started while subscribed and continued after a lapse. |
| D5 | What does a locked `GET /api/browse/lessons/{id}` return? | 200 with `isLocked: true`. Name, breadcrumb ids and names, mastery counts and previous/next links stay; `explanation` and `summary` are `""`, `videoUrl` is `null`, `objectives` is `[]`. | The prototype renders breadcrumbs plus a "subscribers only" card for a locked lesson; the web needs names to render it. |
| D6 | Status and exception type for the gates | 403 `ForbiddenCoreException` (Core.Errors) with codes `QUIZ_DAILY_LIMIT_REACHED` (context `limit`), `LESSON_LOCKED` and `EXAM_REQUIRES_SUBSCRIPTION`. | "Authenticated but not entitled" is a permission refusal. The web has no global 403 handler, so the codes drive the paywall. No new exception types. |
| D7 | Who is exempt? | Admins (role claim `Admin`, which is the same test that sets `IsTestMode`) on quiz start and exam start; any `IsTestMode` session on answer submit. Teachers cannot reach these endpoints (policy `Assessments.Take`). Everyone else is gated. | "Test mode and admins are never gated." The exemption is an exact role match, so an unknown or missing role fails closed. |
| D8 | Fail closed | The gate always loads entitlement through `StudentEntitlementLoader.LoadAsync` before any write. A failure throws, and nothing is saved. `EntitlementResultGenerator` already maps any non-Base tier to Free limits. | Required by the orchestrator; no try/catch anywhere. |
| D9 | Exams | A **new** unit exam or multi-unit exam needs `CanTakeExams` (Base). Resuming, saving or submitting an exam already started is not gated; the auto-submit worker still runs. The check runs before the #85 lesson-opened gate. | PRD §11.1: exams are a Base feature. Blocking resume would strand an exam started before a lapse; the deadline ends it anyway. |
| D10 | Lesson openings (`POST …/openings`) for a locked lesson | Server unchanged. The web does not post an opening for a locked lesson. | An opening grants nothing (exams need Base anyway); it is reading telemetry, not a gate. |
| D11 | Daily counter data | New `GET /api/subscriptions/usage` (policy `Subscription.Manage`) → `UsageResult`. `GET /entitlement` stays unchanged, so the shared loader keeps one signature. | The counter needs a usage count, which the entitlement contract does not carry. #91 and #94 add their counters here. |
| D12 | Progress page for Free | Not gated. | PRD §11.1 gives no reduced Free view; inventing one is a product decision. Flagged to the orchestrator as an assumed answer. |
| D13 | Home "next recommended lesson" | For Free, candidates are restricted to open lessons (the prototype's `vStudentHome` does the same). The headline totals are unchanged. | Stops Home from sending a Free student straight into a paywall; closes the `docs/mastery.md` "(#87)" note. |
| D14 | Concurrency of the quota | Soft limit: two answers sent in parallel at 9/10 can both pass. There is no lock. | A per-student lock costs more than one extra free question. Documented as a known limit. |
| D15 | Paywall «اشترك» | A link to `/student/subscription` (not a direct checkout, unlike the prototype). «لاحقًا» closes the dialog. | The orchestrator requires a link to the subscribe screen (#99/#100), which already sells every period. |
| D16 | Counter and plan-line loading/error states | `DailyQuizCounter` and `PlanSummaryLine` render nothing while pending or on error (including the 403 an Admin would get). | Secondary lines inside pages that already have full states; the server gate is authoritative. |
| D17 | Existing integration tests | `SessionTestData.SignedInStudentAsync` now seeds an entitled Base subscription (the "entitled student" of #74–#86 flows). A new `SignedInFreeStudentAsync` replaces it in the subscription and payment test files, which need a Free student. | This keeps every existing flow test green without weakening a gate, and makes "entitled student" explicit. Only the helper call changes; no assertion changes. |
| D18 | Where do the gate helpers live? | `FreeTierGate`, a static class in `Application/Subscriptions/Shared`, mirroring `Exams/Shared/ExamLessonGate`. The pure rule (`LessonAccess`) lives in Domain. | No service layer (skill §5.6); the existing gate pattern in this repo. |
| D19 | Morabh reuse | `ForbiddenCoreException` comes from Core.Errors (vendored from Morabh `Core/Core.Errors/Exceptions.cs`). Everything else is new; Morabh has no quota, entitlement or paywall code (grep for quota/entitlement/paywall/DailyLimit: none). | Reuse-first rule. |

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Application/Shared/Options/SubscriptionsOptions.cs` | Add `[Required] public string DailyQuotaTimeZone { get; set; } = "Africa/Cairo";` after `FreeOpenLessonsPerUnit`. |
| `api/Elmanhg.Application/DependencyInjection.cs` | In the `SubscriptionsOptions` chain, add `.Validate(x => TimeZoneInfo.TryFindSystemTimeZoneById(x.DailyQuotaTimeZone, out _), "Subscriptions:DailyQuotaTimeZone must be a known IANA time zone id.")` before `.ValidateOnStart()`. |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | New group `// FREE TIER` after `// SUBSCRIPTIONS`: `QuizDailyLimitReached = "QUIZ_DAILY_LIMIT_REACHED"`, `LessonLocked = "LESSON_LOCKED"`, `ExamRequiresSubscription = "EXAM_REQUIRES_SUBSCRIPTION"`. |
| `api/Elmanhg.Api/Resources/Messages.ar.resx`, `Messages.en.resx` | 3 `<data>` entries (see Error codes). |
| `api/Elmanhg.Domain/Lessons/ILessonRepository.cs` | Add `Task<List<LessonPosition>> GetPublishedPositionsAsync(CancellationToken cancellationToken);` and `Task<List<LessonPosition>> GetPublishedSiblingPositionsAsync(Guid lessonId, CancellationToken cancellationToken);` |
| `api/Elmanhg.Infrastructure/Lessons/LessonRepository.cs` | Implement both methods (see Files #3). |
| `api/Elmanhg.Domain/Sessions/ISessionRepository.cs` | Add `Task<int> CountQuizAttemptsOnDayAsync(Guid studentId, string timeZone, DateOnly day, CancellationToken cancellationToken);` |
| `api/Elmanhg.Infrastructure/Sessions/SessionRepository.cs` | Implement (see Files #4). |
| `api/Elmanhg.Application/Sessions/StartQuizSession/StartQuizSessionHandler.cs` | New ctor and gate steps (see Files #8). |
| `api/Elmanhg.Application/Sessions/SubmitAnswer/SubmitAnswerHandler.cs` | New ctor and gate steps (see Files #9). |
| `api/Elmanhg.Application/Exams/StartUnitExam/StartUnitExamHandler.cs` | New ctor and exam gate (see Files #10). |
| `api/Elmanhg.Application/Exams/StartMultiUnitExam/StartMultiUnitExamHandler.cs` | New ctor and exam gate (see Files #11). |
| `api/Elmanhg.Application/Browse/Shared/StudentUnitResult.cs` | `StudentLessonSummaryResult` gains a last positional member `bool IsLocked`. |
| `api/Elmanhg.Application/Browse/Shared/StudentLessonResult.cs` | `StudentLessonResult` gains a last positional member `bool IsLocked`. |
| `api/Elmanhg.Application/Browse/Shared/StudentUnitResultGenerator.cs` | `Generate(..., IReadOnlyCollection<ExamBestScore> bests, IReadOnlySet<Guid> openLessonIds)`; each lesson `IsLocked = !openLessonIds.Contains(lesson.Id)`. |
| `api/Elmanhg.Application/Browse/Shared/StudentLessonResultGenerator.cs` | `Generate(..., IReadOnlyCollection<LessonMasteryCount> counts, bool isLocked)`; when locked: `Explanation = string.Empty`, `Summary = string.Empty`, `VideoUrl = null`, `Objectives = []`, `IsLocked = true`; everything else as today. |
| `api/Elmanhg.Application/Browse/GetStudentUnit/GetStudentUnitHandler.cs` | New ctor; compute open ids (see Files #12). |
| `api/Elmanhg.Application/Browse/GetStudentLesson/GetStudentLessonHandler.cs` | New ctor; compute `isLocked` (see Files #13). |
| `api/Elmanhg.Application/Mastery/GetMasteryOverview/GetMasteryOverviewHandler.cs` | New ctor; filter next-lesson candidates for Free (see Files #14). |
| `api/Elmanhg.Api/Controllers/Subscriptions/SubscriptionsController.cs` | Add the `GetMyUsage` action (see API surface). |
| `api/Elmanhg.Api/appsettings.example.json` | Add `"DailyQuotaTimeZone": "Africa/Cairo"` to `Subscriptions` after `FreeOpenLessonsPerUnit`. |
| `api/openapi/v1.json` | Regenerated by `dotnet build`. |
| `web/src/shared/api/generated/**` | Regenerated by `npm --prefix web run gen:api`. |
| `postman/elmanhg.postman_collection.json` | New request "Get my usage" (`GET {{baseUrl}}/api/subscriptions/usage`, student bearer) placed right after "Get my entitlement" in the same folder. |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | Add `["Subscriptions:DailyQuotaTimeZone"] = "Africa/Cairo",` after the `FreeOpenLessonsPerUnit` line. |
| `api/Elmanhg.Tests/Integration/Sessions/SessionTestData.cs` | `SignedInStudentAsync` additionally seeds `new SubscriptionBuilder().ForStudent(student.Id).StartingAt(DateTimeOffset.UtcNow.AddDays(-1)).Build()` through `SubscriptionTestData.SeedSubscriptionAsync`. New `public static async Task<(User Student, HttpClient Client)> SignedInFreeStudentAsync(ApiFactory factory)` = today's body (no subscription). |
| `api/Elmanhg.Tests/Integration/Subscriptions/{CancelSubscription,Checkout,Entitlement,FakePaymentCompletion,Payment,PaymentHistory,PaymobRefundWebhook,PaymobWebhook,PlanCatalogue}EndpointTests.cs`, `api/Elmanhg.Tests/Integration/Payments/RefundPaymentEndpointTests.cs` | **modify:** replace every `SignedInStudentAsync(` call with `SignedInFreeStudentAsync(`. No other change. |
| `api/Elmanhg.Tests/Application/Features/Sessions/StartQuizSession/StartQuizSessionHandlerTests.cs` | **modify (wiring only):** add `ISubscriptionRepository` and `TimeProvider` substitutes (`GetUtcNow()` returns `T0`), `SubscriptionRepositoryStub.Stub(_subscriptionRepository, SubscriptionRepositoryStub.EntitledBase(_builder.StudentId, T0))`, and pass the new ctor arguments (`Options.Create(new SubscriptionsOptions())`). No assertion changes. |
| `.../Sessions/SubmitAnswer/SubmitAnswerHandlerTests.cs` | **modify (wiring only):** same pattern; add an `ILessonRepository` substitute. |
| `.../Exams/StartUnitExam/StartUnitExamHandlerTests.cs`, `.../Exams/StartMultiUnitExam/StartMultiUnitExamHandlerTests.cs` | **modify (wiring only):** add `ISubscriptionRepository` stubbed with an entitled Base at the test clock, plus `Options.Create(new SubscriptionsOptions())`. |
| `.../Browse/GetStudentLesson/GetStudentLessonHandlerTests.cs` | **modify (wiring only):** subscription repository stubbed Base, `TimeProvider` substitute, options. |
| `.../Browse/GetStudentUnit/GetStudentUnitHandlerTests.cs` | **modify:** same wiring; line 85's expected records gain the final argument `false`. |
| `.../Mastery/GetMasteryOverview/GetMasteryOverviewHandlerTests.cs` | **modify (wiring only):** subscription repository stubbed Base, options. |
| `.../Subscriptions/SubscriptionsOptionsTests.cs` | **modify:** add one test (Test plan row A29). |
| `web/src/features/subscription/api/entitlement.ts` | `planLabelKey(entitlement: Pick<EntitlementResult, 'tier' \| 'hasAskTeacher'>)`. |
| `web/src/features/subscription/index.ts` | Export `PaywallDialog`, `DailyQuizCounter`, `PlanSummaryLine`, `paywallReason`, `type PaywallReason`. |
| `web/src/features/subscription/hooks/useFakePaymentCompletion.ts` | Replace the `getGetMyEntitlementQueryKey()` invalidation with `await invalidateEntitlementViews(queryClient)`. |
| `web/src/features/subscription/i18n/ar.json`, `en.json` | `usage.*` and `paywall.*` keys (see Web copy). |
| `web/src/features/browse/i18n/ar.json`, `en.json` | `unit.locked`, `unit.unlock`, `lesson.lockedNotice`, `lesson.subscribe`. |
| `web/src/shared/i18n/ar.json`, `en.json` | `errors.QUIZ_DAILY_LIMIT_REACHED`, `errors.LESSON_LOCKED`, `errors.EXAM_REQUIRES_SUBSCRIPTION`. |
| `web/src/features/browse/components/LessonListItem.tsx` | Locked variant (see Files W7). |
| `web/src/features/browse/pages/LessonPage.tsx` | Locked branch; `useLessonOpening(lessonId, data?.isLocked ? undefined : data?.unitId)`. |
| `web/src/features/quiz/hooks/useStartQuiz.ts` | Return `reset: () => void` (`mutation.reset`). |
| `web/src/features/quiz/hooks/useQuizAnswer.ts` | `paywall` state; usage invalidation (see W11). |
| `web/src/features/quiz/components/PracticeStart.tsx` | Counter and paywall (see W12). |
| `web/src/features/quiz/components/NewPracticeButton.tsx` | Paywall (see W13). |
| `web/src/features/quiz/components/QuizRunner.tsx` | `{session.isTestMode ? null : <DailyQuizCounter variant="quiz" />}` right after the `h1`. |
| `web/src/features/quiz/components/QuizQuestionCard.tsx` | Render `<PaywallDialog reason={quiz.paywall} onClose={quiz.closePaywall} />` as the last child of `article`. |
| `web/src/features/exam/hooks/useStartExam.ts`, `useStartMultiExam.ts` | Return `reset: () => void` (`mutation.reset`). |
| `web/src/features/exam/components/ExamStartActions.tsx` | In the final branch, render the error `<p>` only when `errorCode && paywallReason(errorCode) === null`, and add `<PaywallDialog reason={paywallReason(errorCode)} onClose={reset} />`. |
| `web/src/features/exam/components/MultiExamPreview.tsx` | Same pattern with `starter.errorCode` / `starter.reset`. |
| `web/src/features/mastery/pages/StudentHomePage.tsx` | Render `<PlanSummaryLine />` between `HeadlineCounterCard` and `NextLessonCard`. |
| `web/src/test/browseFixtures.ts` | `studentLesson()` gets `isLocked: false`; every lesson in `studentUnit()` gets `isLocked: false`. |
| `web/src/test/subscriptionFixtures.ts` | Add `freeUsage(overrides?: Partial<UsageResult>)` and `baseUsage()` (see W18). |
| `web/src/test/msw/server.ts` | Add the default handler `getGetMyUsageMockHandler(baseUsage())`. |
| `docs/subscriptions.md`, `docs/browsing.md`, `docs/sessions.md`, `docs/exams.md`, `docs/mastery.md`, `docs/claude-design-prompt.md` | See Docs. |

## Files to create

### API
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `api/Elmanhg.Domain/Lessons/LessonPosition.cs` | record | `namespace Elmanhg.Domain.Lessons; public sealed record LessonPosition(Guid Id, Guid UnitId, int Order, DateTimeOffset CreationDate) { public static LessonPosition Of(Lesson lesson) => new(lesson.Id, lesson.UnitId, lesson.Order, lesson.CreationDate); }` |
| 2 | `api/Elmanhg.Domain/Lessons/LessonAccess.cs` | static class | `public static HashSet<Guid> OpenLessonIds(IEnumerable<LessonPosition> publishedLessons, int? openLessonsPerUnit)`: when `null` → every id; otherwise `GroupBy(UnitId)` → each group `OrderBy(Order).ThenBy(CreationDate).ThenBy(Id).Take(openLessonsPerUnit.Value)` → ids. `public static bool IsOpen(Guid lessonId, IEnumerable<LessonPosition> publishedLessons, int? openLessonsPerUnit) => OpenLessonIds(publishedLessons, openLessonsPerUnit).Contains(lessonId);` One-line WHY comment: "PRD §11.1: Free opens the first N published lessons of each unit; curriculum order as LessonSequence." |
| 3 | (in `LessonRepository.cs`) | repo methods | `GetPublishedPositionsAsync`: `_dbSet.Where(x => x.State == LessonState.Published).Select(x => new LessonPosition(x.Id, x.UnitId, x.Order, x.CreationDate)).AsNoTracking().ToListAsync(ct)`. `GetPublishedSiblingPositionsAsync(lessonId)`: the same projection with `Where(x => x.State == LessonState.Published && _dbSet.Any(lesson => lesson.Id == lessonId && lesson.UnitId == x.UnitId))`. |
| 4 | (in `SessionRepository.cs`) | repo method | `CountQuizAttemptsOnDayAsync`: `var kind = nameof(SessionKind.Quiz); var lowerBound = new DateTimeOffset(day.AddDays(-1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);` then `_context.Database.SqlQuery<int>($"""SELECT COUNT(*)::int AS "Value" FROM "Attempts" AS a INNER JOIN "Sessions" AS s ON s."Id" = a."SessionId" WHERE a."StudentId" = {studentId} AND a."CreatedAt" >= {lowerBound} AND a."IsDeleted" = false AND s."IsDeleted" = false AND s."IsTestMode" = false AND s."Kind" = {kind} AND (a."CreatedAt" AT TIME ZONE {timeZone})::date = {day}""").SingleAsync(ct)`. Interpolated `SqlQuery` only (parameterised), formatted over several lines like `GetQuizActivityDaysAsync`. Comment: "Any zone's local midnight is within 14 h of UTC midnight, so the day before is a safe index bound." |
| 5 | `api/Elmanhg.Application/Subscriptions/Shared/FreeTierGate.cs` | static class | `namespace Elmanhg.Application.Subscriptions.Shared;` Members: `public static void EnsureCanTakeExams(EntitlementResult entitlement)`: `!CanTakeExams` → `throw new ForbiddenCoreException(ErrorCodes.ExamRequiresSubscription)`. `public static async Task EnsureLessonOpenAsync(EntitlementResult entitlement, Guid lessonId, ILessonRepository lessonRepository, CancellationToken cancellationToken)`: `OpenLessonsPerUnit is null` → return with no query; else `GetPublishedSiblingPositionsAsync(lessonId)`, and `!LessonAccess.IsOpen(...)` → `ForbiddenCoreException(ErrorCodes.LessonLocked)`. `public static async Task<int> CountQuizQuestionsTodayAsync(Guid studentId, ISessionRepository sessionRepository, SubscriptionsOptions options, DateTimeOffset now, CancellationToken cancellationToken)`: `zone = TimeZoneInfo.FindSystemTimeZoneById(options.DailyQuotaTimeZone)`; `today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime)`; return `CountQuizAttemptsOnDayAsync(studentId, options.DailyQuotaTimeZone, today, ct)`. `public static async Task EnsureQuizQuestionAvailableAsync(EntitlementResult entitlement, Guid studentId, ISessionRepository sessionRepository, SubscriptionsOptions options, DateTimeOffset now, CancellationToken cancellationToken)`: `DailyQuizQuestionLimit is not { } limit` → return with no query; `used >= limit` → `throw new ForbiddenCoreException(ErrorCodes.QuizDailyLimitReached, context: new Dictionary<string, object> { ["limit"] = limit })`. Every await `.ConfigureAwait(false)`. |
| 6 | `api/Elmanhg.Application/Subscriptions/GetMyUsage/GetMyUsageQuery.cs` | query | `public sealed record GetMyUsageQuery : IRequest<UsageResult>;` |
| 7 | `api/Elmanhg.Application/Subscriptions/GetMyUsage/GetMyUsageHandler.cs` | handler | `public sealed class GetMyUsageHandler(ISubscriptionRepository subscriptionRepository, ISessionRepository sessionRepository, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<GetMyUsageQuery, UsageResult>`. Steps: (1) null or default `UserId` → `UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated)`; (2) `now = timeProvider.GetUtcNow()`, `options = subscriptionsOptions.Value`; (3) `entitlement = StudentEntitlementLoader.LoadAsync(subscriptionRepository, userId, options, now, ct)`; (4) `used = FreeTierGate.CountQuizQuestionsTodayAsync(userId, sessionRepository, options, now, ct)`; (5) return `new UsageResult(entitlement.Tier, entitlement.HasAskTeacher, entitlement.DailyQuizQuestionLimit, used, entitlement.DailyQuizQuestionLimit is { } limit ? Math.Max(0, limit - used) : null, entitlement.DailyAvatarMessageLimit)`. No validator (no input). |
| 7b | `api/Elmanhg.Application/Subscriptions/Shared/UsageResult.cs` | result (client-facing, no LocalizedText) | `public sealed record UsageResult(PlanTier Tier, bool HasAskTeacher, int? DailyQuizQuestionLimit, int QuizQuestionsUsedToday, int? QuizQuestionsRemainingToday, int DailyAvatarMessageLimit);` |

**Handler changes (existing files, exact).**

| # | Handler | New constructor (one line) | `Handle` steps (changes in bold) |
|---|---------|---------------------------|--------------------------------|
| 8 | `StartQuizSessionHandler` | `(ISessionRepository sessionRepository, ILessonRepository lessonRepository, IQuestionRepository questionRepository, ISubscriptionRepository subscriptionRepository, IOptions<SessionsOptions> sessionsOptions, IOptions<MasteryOptions> masteryOptions, IOptions<SubscriptionsOptions> subscriptionsOptions, Random random, TimeProvider timeProvider, ICurrentUserService currentUserService, ILocalizer localizer)` | 1 user guard; 2 load lesson, 404 unless Published; **3 `isTestMode = role == nameof(UserRole.Admin)`; 4 `entitlement = isTestMode ? null : await StudentEntitlementLoader.LoadAsync(subscriptionRepository, userId, subscriptionsOptions.Value, now, ct)`; 5 `entitlement is not null` → `FreeTierGate.EnsureLessonOpenAsync(entitlement, lesson.Id, lessonRepository, ct)`**; 6 find the open session → `Resume()`; 7 else **`entitlement is not null` → `FreeTierGate.EnsureQuizQuestionAvailableAsync(entitlement, userId, sessionRepository, subscriptionsOptions.Value, now, ct)`**, select, `Session.StartQuiz(userId, lesson, questions, isTestMode)`, `AddAsync`; 8 Save; 9 result. `now = timeProvider.GetUtcNow()`. |
| 9 | `SubmitAnswerHandler` | `(ISessionRepository sessionRepository, IQuestionRepository questionRepository, IQuestionMasteryRepository questionMasteryRepository, ILessonRepository lessonRepository, ISubscriptionRepository subscriptionRepository, IOptions<MasteryOptions> masteryOptions, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider, ICurrentUserService currentUserService, ILocalizer localizer)` | After `item` is found (404 otherwise) and **before** loading revisions: **`if (!session.IsTestMode && session.FindAttempt(item.QuestionId) is null) { await EnsureFreeTierAsync(userId, session, ct); }`**. New private method `EnsureFreeTierAsync(Guid studentId, Session session, CancellationToken)`: `now = timeProvider.GetUtcNow()`; load entitlement; `FreeTierGate.EnsureLessonOpenAsync(entitlement, QuizScope.FromJson(session.Scope).LessonId, lessonRepository, ct)`; then `FreeTierGate.EnsureQuizQuestionAvailableAsync(entitlement, studentId, sessionRepository, options, now, ct)`. The rest is unchanged. |
| 10 | `StartUnitExamHandler` | existing params with `ISubscriptionRepository subscriptionRepository` inserted after `IQuestionMasteryRepository questionMasteryRepository`, and `IOptions<SubscriptionsOptions> subscriptionsOptions` inserted after `IOptions<MasteryOptions> masteryOptions` | In `StartAsync`, right after `isTestMode` is computed and **before** the `RequireAllLessonsOpened` block: **`if (!isTestMode) { var entitlement = await StudentEntitlementLoader.LoadAsync(subscriptionRepository, userId, subscriptionsOptions.Value, now, ct); FreeTierGate.EnsureCanTakeExams(entitlement); }`**. The resume path is untouched. |
| 11 | `StartMultiUnitExamHandler` | the same two insertions (after `questionMasteryRepository`; after `IOptions<MasteryOptions> masteryOptions`) | Inside `if (session is null)`, right after `isTestMode`, before the lesson-opened gate: the same entitlement check. |
| 12 | `GetStudentUnitHandler` | `(ICurriculumUnitRepository unitRepository, ISubjectRepository subjectRepository, ILessonRepository lessonRepository, IQuestionMasteryRepository questionMasteryRepository, ISessionRepository sessionRepository, ISubscriptionRepository subscriptionRepository, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider, ICurrentUserService currentUserService)` | After loading `lessons`: `entitlement = LoadAsync(...)`; `openLessonIds = LessonAccess.OpenLessonIds(lessons.Select(LessonPosition.Of), entitlement.OpenLessonsPerUnit)`; pass it to the generator. |
| 13 | `GetStudentLessonHandler` | `(ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, ISubjectRepository subjectRepository, IQuestionMasteryRepository questionMasteryRepository, ISubscriptionRepository subscriptionRepository, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider, ICurrentUserService currentUserService)` | After loading the subject's Published `lessons`: entitlement; `isLocked = !LessonAccess.IsOpen(lesson.Id, lessons.Select(LessonPosition.Of), entitlement.OpenLessonsPerUnit)`; pass it to the generator. |
| 14 | `GetMasteryOverviewHandler` | `(IQuestionMasteryRepository questionMasteryRepository, ISessionRepository sessionRepository, ISubjectRepository subjectRepository, ILessonRepository lessonRepository, IUserRepository userRepository, ISubscriptionRepository subscriptionRepository, IOptions<ProgressOptions> progressOptions, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider, ICurrentUserService currentUserService)` | Before `Pick`: `entitlement = LoadAsync(...)`; `candidates = entitlement.OpenLessonsPerUnit is null ? lessons : lessons.Where(x => open.Contains(x.LessonId)).ToList()` where `open = LessonAccess.OpenLessonIds(await lessonRepository.GetPublishedPositionsAsync(ct), entitlement.OpenLessonsPerUnit)` (loaded only for Free); `next = NextLessonRecommendation.Pick(candidates)`. The generator still receives the full `lessons` for the totals. |

Any handler whose `Handle` grows past ~100 lines moves its gate call into a private method in the same file (as in #9). No new classes.

### Web
| # | Path | Type | Contract |
|---|------|------|----------|
| W1 | `web/src/features/subscription/api/paywall.ts` | util | `export type PaywallReason = 'dailyQuiz' \| 'lesson' \| 'exam';` `export function paywallReason(code: string \| null \| undefined): PaywallReason \| null` mapping `QUIZ_DAILY_LIMIT_REACHED→'dailyQuiz'`, `LESSON_LOCKED→'lesson'`, `EXAM_REQUIRES_SUBSCRIPTION→'exam'`, anything else → `null`. |
| W2 | `web/src/features/subscription/api/invalidateEntitlementViews.ts` | util | `export function invalidateEntitlementViews(queryClient: QueryClient): Promise<void>`: `invalidateQueries({ predicate })` where the first key element is a string starting with `/api/subscriptions/entitlement`, `/api/subscriptions/usage`, `/api/browse`, `/api/mastery` or `/api/exams`. |
| W3 | `web/src/features/subscription/components/PaywallDialog.tsx` | component | `export interface PaywallDialogProps { reason: PaywallReason \| null; onClose: () => void; }`. `<Dialog open={reason !== null} onOpenChange={(open) => { if (!open) onClose(); }}>` + `<DialogContent title={t(`paywall.${reason}.title`)}>`. The body `<p className="text-ui text-text">`: for `dailyQuiz`, `t('paywall.dailyQuiz.body', { count: limit })` when `useGetMyUsage({ query: { enabled: reason === 'dailyQuiz' } }).data?.dailyQuizQuestionLimit` is a number, else `t('paywall.dailyQuiz.bodyNoLimit')`; for other reasons `t(`paywall.${reason}.body`)`. Actions row `flex flex-wrap justify-end gap-3`: `<Button variant="secondary" onClick={onClose}>{t('paywall.later')}</Button>` then `<Button asChild variant="primary"><Link to="/student/subscription">{t('paywall.subscribe')}</Link></Button>`. When `reason` is null the title key uses `'dailyQuiz'` so no key is missing while closed. |
| W4 | `web/src/features/subscription/components/DailyQuizCounter.tsx` | component | `export interface DailyQuizCounterProps { variant: 'practice' \| 'quiz' }`. `const { data } = useGetMyUsage();` returns `null` unless `data` exists and `data.dailyQuizQuestionLimit != null`; renders `<p className="text-caption text-text-muted">{t(`usage.${variant}Counter`, { used: Number(data.quizQuestionsUsedToday), limit: Number(data.dailyQuizQuestionLimit) })}</p>`. |
| W5 | `web/src/features/subscription/components/PlanSummaryLine.tsx` | component | `useGetMyUsage()`; `null` while pending or on error. Card `flex flex-wrap items-center gap-3 rounded-lg border border-border bg-surface p-4 shadow-1`: `<span className="text-ui text-text">{t('usage.plan', { plan: t(`current.${planLabelKey(data)}`) })}</span>`; when `data.dailyQuizQuestionLimit != null`: `<span className="text-ui text-text-muted">{t('usage.homeCounter', { used, limit })}</span>` and `<Button asChild size="sm" variant="primary"><Link to="/student/subscription">{t('usage.subscribe')}</Link></Button>`. |
| W6 | `web/src/features/browse/components/LockedLessonNotice.tsx` | component | `div` with `flex flex-col items-start gap-2 rounded-lg border border-warning bg-warning-soft p-4`: `<p className="flex items-start gap-2 text-ui text-text"><Lock aria-hidden className="size-4 shrink-0" />{t('lesson.lockedNotice')}</p>` + `<Button asChild size="sm" variant="primary"><Link to="/student/subscription">{t('lesson.subscribe')}</Link></Button>`. `Lock` from `lucide-react`. |
| W7 | (LessonListItem locked variant) | — | When `lesson.isLocked`: the name is a `<span className="text-ui font-semibold break-words text-text-muted">` (no `Link`), followed by the badge `<span className="inline-flex items-center gap-1 self-start rounded-full bg-soft px-2.5 py-0.5 text-micro font-semibold text-text-muted"><Lock aria-hidden className="size-4" />{t('unit.locked')}</span>`; the `MasteryBar` and meta line as today; then `<Button asChild size="sm" variant="secondary" className="self-start"><Link to="/student/subscription">{t('unit.unlock')}</Link></Button>`. Unlocked: unchanged. |
| W8 | (LessonPage locked branch) | — | When `data.isLocked`: `StudentBreadcrumbs`, `h1`, `MasterySummary`, `<LockedLessonNotice />`, `LessonNavigation`. No `LessonTabs`, no `Outlet`. |
| W11 | (useQuizAnswer) | — | Add `const [paywall, setPaywall] = useState<PaywallReason \| null>(null)`. `onSuccess` also runs `void queryClient.invalidateQueries({ queryKey: getGetMyUsageQueryKey() })`. `onError`: `const reason = paywallReason(code); if (reason) { setPaywall(reason); } else { toast.error(...) }`, then the existing session invalidation plus the usage invalidation. `QuizAnswerState` gains `paywall: PaywallReason \| null; closePaywall: () => void`. |
| W12 | (PracticeStart) | — | `<DailyQuizCounter variant="practice" />` after the `resumeHint` paragraph; the error branch becomes `errorCode === 'SESSION_NO_SERVABLE_QUESTIONS' ? … : errorCode && paywallReason(errorCode) === null ? <p role="alert">… : null`; add `<PaywallDialog reason={paywallReason(errorCode)} onClose={reset} />`. |
| W13 | (NewPracticeButton) | — | Same error condition plus `<PaywallDialog reason={paywallReason(errorCode)} onClose={reset} />`. |
| W18 | (fixtures) | — | `freeUsage(overrides?: Partial<UsageResult>): UsageResult` → `{ tier: 'Free', hasAskTeacher: false, dailyQuizQuestionLimit: 10, quizQuestionsUsedToday: 3, quizQuestionsRemainingToday: 7, dailyAvatarMessageLimit: 5, ...overrides }`; `baseUsage(): UsageResult` → `{ tier: 'Base', hasAskTeacher: false, dailyQuizQuestionLimit: null, quizQuestionsUsedToday: 0, quizQuestionsRemainingToday: null, dailyAvatarMessageLimit: 50 }`. |

Cross-feature imports go through `@/features/subscription` (barrel) only. Orval number fields are read with `Number(...)`, as elsewhere.

### Web copy
| Namespace.key | ar | en |
|---|---|---|
| `subscription:usage.plan` | `باقتك: {plan}` | `Your plan: {plan}` |
| `subscription:usage.homeCounter` | `أسئلة التدريب اليوم: {used, number} / {limit, number}` | `Practice questions today: {used, number} / {limit, number}` |
| `subscription:usage.practiceCounter` | `الباقة المجانية: {used, number} / {limit, number} سؤال اليوم` | `Free plan: {used, number} / {limit, number} questions today` |
| `subscription:usage.quizCounter` | `اليوم: {used, number} / {limit, number}` | `Today: {used, number} / {limit, number}` |
| `subscription:usage.subscribe` | `اشترك` | `Subscribe` |
| `subscription:paywall.dailyQuiz.title` | `انتهى الحد المجاني` | `Free limit reached` |
| `subscription:paywall.dailyQuiz.body` | `وصلت إلى الحد اليومي للباقة المجانية ({count, number} أسئلة تدريب). اشترك في الباقة الأساسية للتدريب بلا حدود.` | `You have reached the free plan's daily limit ({count, number} practice questions). Subscribe to Base for unlimited practice.` |
| `subscription:paywall.dailyQuiz.bodyNoLimit` | `وصلت إلى الحد اليومي للباقة المجانية. اشترك في الباقة الأساسية للتدريب بلا حدود.` | `You have reached the free plan's daily limit. Subscribe to Base for unlimited practice.` |
| `subscription:paywall.lesson.title` / `.exam.title` | `ميزة للمشتركين` | `For subscribers` |
| `subscription:paywall.lesson.body` | `هذا الدرس متاح للمشتركين فقط.` | `This lesson is for subscribers only.` |
| `subscription:paywall.exam.body` | `الامتحانات متاحة في الباقة الأساسية.` | `Exams are included in the Base plan.` |
| `subscription:paywall.subscribe` / `.later` | `اشترك` / `لاحقًا` | `Subscribe` / `Later` |
| `browse:unit.locked` | `مقفل - للمشتركين` | `Locked - subscribers only` |
| `browse:unit.unlock` | `اشترك لفتح الدرس` | `Subscribe to unlock` |
| `browse:lesson.lockedNotice` | `هذا الدرس متاح للمشتركين فقط. الباقة المجانية تتيح الدرس الأول من كل وحدة.` | `This lesson is for subscribers only. The free plan opens the first lesson of each unit.` |
| `browse:lesson.subscribe` | `اشترك` | `Subscribe` |

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `ErrorCodes.QuizDailyLimitReached` | `QUIZ_DAILY_LIMIT_REACHED` | `FreeTierGate.EnsureQuizQuestionAvailableAsync` (quiz start, new session; answer submit, new attempt) | `ForbiddenCoreException`, context `limit` | 403 |
| `ErrorCodes.LessonLocked` | `LESSON_LOCKED` | `FreeTierGate.EnsureLessonOpenAsync` (quiz start or resume; answer submit) | `ForbiddenCoreException` | 403 |
| `ErrorCodes.ExamRequiresSubscription` | `EXAM_REQUIRES_SUBSCRIPTION` | `FreeTierGate.EnsureCanTakeExams` (new unit exam; new multi-unit exam) | `ForbiddenCoreException` | 403 |

resx and web `common:errors`:
- `QUIZ_DAILY_LIMIT_REACHED`: ar `وصلت إلى الحد اليومي للباقة المجانية.` / en `You have reached the free plan's daily limit.`
- `LESSON_LOCKED`: ar `هذا الدرس متاح للمشتركين فقط.` / en `This lesson is for subscribers only.`
- `EXAM_REQUIRES_SUBSCRIPTION`: ar `الامتحانات متاحة في الباقة الأساسية.` / en `Exams are included in the Base plan.`

## Domain behaviour
No entity changes and no migration. `Lesson`, `Session` and `Subscription` are untouched. The only domain code is the pure functions `LessonPosition.Of` and `LessonAccess.OpenLessonIds` / `IsOpen` (Files #1–#2). They throw nothing and have no clock. There is no `UpdationDate` change because no state changes.

## API surface
| Method | Route | Policy | Request | Response |
|---|---|---|---|---|
| GET | `/api/subscriptions/usage` (Name `GetMyUsage`) | `DefaultCodes.SubscriptionManage` | — | 200 `UsageResult`; 401 anonymous; 403 non-student |
| POST | `/api/sessions/quiz` | unchanged | unchanged | + 403 `LESSON_LOCKED`, `QUIZ_DAILY_LIMIT_REACHED` |
| POST | `/api/sessions/{id}/answers` | unchanged | unchanged | + 403 `LESSON_LOCKED`, `QUIZ_DAILY_LIMIT_REACHED` |
| POST | `/api/exams/units/{unitId}` | unchanged | unchanged | + 403 `EXAM_REQUIRES_SUBSCRIPTION` |
| POST | `/api/exams/subjects/{subjectId}/multi-unit` | unchanged | unchanged | + 403 `EXAM_REQUIRES_SUBSCRIPTION` |
| GET | `/api/browse/units/{id}` | unchanged | — | `lessons[].isLocked` |
| GET | `/api/browse/lessons/{id}` | unchanged | — | `isLocked`; content withheld when true |

Controller action: `[HttpGet("usage", Name = "GetMyUsage")] [Authorize(Policy = DefaultCodes.SubscriptionManage)] [ProducesResponseType<UsageResult>(StatusCodes.Status200OK)] public async Task<ActionResult> GetMyUsage(CancellationToken cancellationToken)` → `Ok(await mediator.Send(new GetMyUsageQuery(), cancellationToken))`, placed after `GetMyEntitlement`.

## Docs
| File | Change |
|---|---|
| `docs/subscriptions.md` | Config table: row `DailyQuotaTimeZone` · `Africa/Cairo` · "IANA zone whose calendar day the Free daily quotas reset on." New section **Free tier gates** (D1–D9, D14): what is counted, where each gate runs, the 403 codes, the exemptions, fail-closed behaviour and the known soft limit. API table: `GET /api/subscriptions/usage` → `UsageResult`. "For later stories": #87 done; #91 and #94 add their counters to `UsageResult` and use the loader. |
| `docs/browsing.md` | Result shapes: `StudentLessonSummaryResult.isLocked`, `StudentLessonResult.isLocked`. Replace the **Free tier** section with the implemented rule (D3–D5, D10) and the web behaviour (locked card, locked page, no opening posted). |
| `docs/sessions.md` | Lifecycle steps 1–2: the Free gates (lock on start and resume; quota on a new start and on a new attempt; replay and test mode exempt; 403 codes). |
| `docs/exams.md` | A new exam needs Base (403 `EXAM_REQUIRES_SUBSCRIPTION`); resume, save and submit are ungated; Admin test mode is exempt; order relative to the lesson-open gate. |
| `docs/mastery.md` | Replace "Free-tier locks are not applied yet (#87)." with "For a Free student, locked lessons are not candidates (`docs/browsing.md` → Free tier)." |
| `docs/claude-design-prompt.md` | §4 Home: "plan line (plan name; for Free the daily counter «أسئلة التدريب اليوم: X / 10» and «اشترك»)". §4 unit page: replace "(locked lessons for Free users arrive with #87)" with "locked lessons for Free users show «مقفل - للمشتركين» and «اشترك لفتح الدرس»; a locked lesson page shows only the subscribers-only notice". §4 Free plan line: counters on Home, the practice tab and the quiz screen; the paywall dialog («اشترك» opens `#/student/subscription`, «لاحقًا» closes) on quiz start, answer, exam start and multi-unit exam start. §7 item 4: «اشترك» in the paywall opens the subscription screen, then the fake checkout «نجاح الدفع». |

## Test plan

### API — Domain (`api/Elmanhg.Tests/Domain/Lessons/LessonAccessTests.cs`, class `LessonAccessTests`)
| # | Test method | Asserts |
|---|-------------|---------|
| A1 | `OpenLessonIds_NullLimit_ReturnsEveryLesson` | all ids across 2 units |
| A2 | `OpenLessonIds_LimitOne_ReturnsFirstLessonOfEachUnitByOrder` | positions given out of order; result = the lowest `Order` per unit |
| A3 | `OpenLessonIds_EqualOrder_BreaksTieByCreationDateThenId` | earlier `CreationDate` wins; with equal date the lower `Id` wins |
| A4 | `OpenLessonIds_LimitZero_ReturnsNoLesson` | empty |
| A5 | `OpenLessonIds_LimitAboveUnitSize_ReturnsWholeUnit` | all lessons of a 2-lesson unit with limit 5 |
| A6 | `IsOpen_LessonNotInPublishedList_ReturnsFalse` | false for an id absent from the positions |
| A7 | `Of_Lesson_CopiesIdUnitOrderAndCreationDate` | `LessonPosition.Of(lesson)` fields equal the lesson's |

### API — Application (unit)
Shared helper (new file, not a test): `api/Elmanhg.Tests/Application/Features/Subscriptions/SubscriptionRepositoryStub.cs`, `public static class SubscriptionRepositoryStub { public static void Stub(ISubscriptionRepository repository, params Subscription[] subscriptions); public static Subscription EntitledBase(Guid studentId, DateTimeOffset now); }`. `Stub` configures `FindAsync` to filter by the compiled predicate (the pattern in `GetMyEntitlementHandlerTests`). `EntitledBase` builds an Active Base starting `now.AddDays(-1)` with `SubscriptionBuilder`. An unstubbed Free case = `Stub(repository)` with no subscriptions.

| # | Test class (file) | Test method | Asserts |
|---|-------------------|-------------|---------|
| A8 | `FreeTierGateTests` (`Application/Features/Subscriptions/Shared/FreeTierGateTests.cs`) | `EnsureCanTakeExams_Free_ThrowsExamRequiresSubscription` | `ForbiddenCoreException` + `EXAM_REQUIRES_SUBSCRIPTION` |
| A9 | 〃 | `EnsureCanTakeExams_Base_DoesNotThrow` | no exception |
| A10 | 〃 | `EnsureLessonOpenAsync_FreeFirstLesson_DoesNotThrow` | siblings [L1, L2]; L1 passes |
| A11 | 〃 | `EnsureLessonOpenAsync_FreeSecondLesson_ThrowsLessonLocked` | 403 type + `LESSON_LOCKED` |
| A12 | 〃 | `EnsureLessonOpenAsync_BaseSecondLesson_DoesNotThrow` | no exception for L2 when `OpenLessonsPerUnit` is null |
| A13 | 〃 | `EnsureQuizQuestionAvailableAsync_FreeBelowLimit_DoesNotThrow` | count 9, limit 10 |
| A14 | 〃 | `EnsureQuizQuestionAvailableAsync_FreeAtLimit_ThrowsWithLimitContext` | count 10 → `QUIZ_DAILY_LIMIT_REACHED`, `Context["limit"] == 10` |
| A15 | 〃 | `EnsureQuizQuestionAvailableAsync_BaseAboveFreeLimit_DoesNotThrow` | count stub 500, limit null → no exception |
| A16 | 〃 | `CountQuizQuestionsTodayAsync_LateUtcEvening_UsesCairoLocalDate` | now `2026-10-01T22:30Z` → repository called with `"Africa/Cairo"` and `2026-10-02`; returns the stubbed count |
| A17 | `GetMyUsageHandlerTests` (`Application/Features/Subscriptions/GetMyUsage/`) | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | 401 type + code |
| A18 | 〃 | `Handle_FreeStudent_ReturnsLimitUsedAndRemaining` | `(Free, false, 10, 3, 7, 5)` |
| A19 | 〃 | `Handle_FreeStudentOverLimit_ReturnsZeroRemaining` | used 12 → remaining 0 |
| A20 | 〃 | `Handle_SubscribedStudent_ReturnsUnlimited` | limit null, remaining null, avatar 50, tier Base |
| A21 | `StartQuizSessionFreeTierTests` (`Application/Features/Sessions/StartQuizSession/`) | `Handle_FreeStudentLockedLesson_ThrowsLessonLocked` | 403 + `LESSON_LOCKED`; `AddAsync` and `SaveChangesAsync` DidNotReceive |
| A22 | 〃 | `Handle_FreeStudentAtDailyLimit_ThrowsQuizDailyLimitReached` | count stub 10; 403 + code; Save DidNotReceive |
| A23 | 〃 | `Handle_FreeStudentAtDailyLimitWithOpenSession_ResumesSession` | open session returned (same id); Save Received(1) |
| A24 | 〃 | `Handle_FreeStudentBelowLimitOnOpenLesson_StartsQuiz` | `AddAsync` Received(1); Save Received(1) |
| A25 | 〃 | `Handle_AdminOnLockedLessonAtLimit_StartsTestModeQuiz` | role Admin, no subscription, count 10 → `result.IsTestMode` true; Save Received(1) |
| A26 | 〃 | `Handle_EntitlementLookupFails_ThrowsAndSavesNothing` | subscription `FindAsync` throws `InvalidOperationException` → it propagates; Save DidNotReceive |
| A27 | `SubmitAnswerFreeTierTests` (`Application/Features/Sessions/SubmitAnswer/`) | `Handle_FreeStudentAtDailyLimit_ThrowsQuizDailyLimitReached` | 403 + code; `session.Attempts` empty; Save DidNotReceive |
| A28 | 〃 | `Handle_FreeStudentAtDailyLimitReplayingSameAnswer_ReturnsExistingAttempt` | pre-recorded attempt; same answer → result carries that attempt; no exception |
| A29 | `SubscriptionsOptionsTests` (existing file) | `AddApplication_DailyQuotaTimeZoneUnknown_ThrowsOptionsValidationException` | failure message `Subscriptions:DailyQuotaTimeZone must be a known IANA time zone id.` |
| A30 | `SubmitAnswerFreeTierTests` | `Handle_FreeStudentLockedLesson_ThrowsLessonLocked` | siblings put the session lesson second → 403 + `LESSON_LOCKED`; Save DidNotReceive |
| A31 | 〃 | `Handle_FreeStudentBelowLimit_RecordsAttempt` | one attempt; Save Received(1) |
| A32 | 〃 | `Handle_TestModeSessionAtLimit_RecordsAttempt` | admin session (`IsTestMode`), no subscription, count 10 → attempt recorded; Save Received(1) |
| A33 | 〃 | `Handle_SubscribedStudentAboveFreeLimit_RecordsAttempt` | Base, count 50 → attempt recorded |
| A34 | `StartUnitExamFreeTierTests` (`Application/Features/Exams/StartUnitExam/`) | `Handle_FreeStudentNewExam_ThrowsExamRequiresSubscription` | 403 + code; `AddAsync`/Save DidNotReceive |
| A35 | 〃 | `Handle_FreeStudentOpenExamForUnit_ResumesExam` | open unit-exam session → returned; Save Received(1) |
| A36 | 〃 | `Handle_AdminWithoutSubscription_StartsTestModeExam` | `IsTestMode` true on the added session |
| A37 | `StartMultiUnitExamFreeTierTests` (`Application/Features/Exams/StartMultiUnitExam/`) | `Handle_FreeStudentNewExam_ThrowsExamRequiresSubscription` | 403 + code; Save DidNotReceive |
| A38 | 〃 | `Handle_FreeStudentOpenExamForSelection_ResumesExam` | Save Received(1) |
| A39 | 〃 | `Handle_AdminWithoutSubscription_StartsTestModeExam` | `IsTestMode` true |
| A40 | `GetStudentUnitFreeTierTests` (`Application/Features/Browse/GetStudentUnit/`) | `Handle_FreeStudent_LocksEveryLessonAfterTheFirst` | 3 lessons → `[false, true, true]` |
| A41 | 〃 | `Handle_SubscribedStudent_LocksNoLesson` | all false |
| A42 | `GetStudentLessonFreeTierTests` (`Application/Features/Browse/GetStudentLesson/`) | `Handle_FreeStudentLockedLesson_ReturnsLockedResultWithoutContent` | `IsLocked` true; `Explanation ""`, `Summary ""`, `VideoUrl` null, `Objectives` empty; `Name`, `UnitName`, `ServableCount` kept |
| A43 | 〃 | `Handle_FreeStudentFirstLesson_ReturnsContent` | `IsLocked` false; explanation present |
| A44 | 〃 | `Handle_SubscribedStudentSecondLesson_ReturnsContent` | `IsLocked` false; objectives present |
| A45 | `GetMasteryOverviewFreeTierTests` (`Application/Features/Mastery/GetMasteryOverview/`) | `Handle_FreeStudent_NextLessonSkipsLockedLesson` | lower-mastery lesson is locked → next = the open lesson |
| A46 | 〃 | `Handle_SubscribedStudent_NextLessonIsLowestMasteryLesson` | next = the lower-mastery (second) lesson |

### API — Integration (HTTP + real PostgreSQL)
| # | Test class (file) | Test method | Asserts |
|---|-------------------|-------------|---------|
| A47 | `FreeTierQuizEndpointTests` (`Integration/Subscriptions/`) | `PostAnswer_FreeStudentEleventhQuestion_Returns403AndStoresNoAttempt` | lesson with 11 servable questions; start with 20; 10 answers → 200; the 11th → 403 `QUIZ_DAILY_LIMIT_REACHED`; DB attempts for the session = 10 |
| A48 | 〃 | `PostQuiz_FreeStudentAtLimitAfterFinishing_Returns403` | 10 answers, finish, start again → 403 `QUIZ_DAILY_LIMIT_REACHED` |
| A49 | 〃 | `PostQuiz_FreeStudentSecondLessonOfUnit_Returns403LessonLocked` | unit with lessons order 1 and 2 → start on lesson 2 → 403 `LESSON_LOCKED`; no session row |
| A50 | 〃 | `PostAnswer_SubscribedStudentEleventhQuestion_Returns200` | via `SignedInStudentAsync` (Base) |
| A51 | 〃 | `PostAnswer_AdminTestModeEleventhQuestion_Returns200` | admin, no subscription |
| A52 | `FreeTierExamEndpointTests` (`Integration/Subscriptions/`) | `PostUnitExam_FreeStudent_Returns403AndStoresNoSession` | 403 `EXAM_REQUIRES_SUBSCRIPTION`; no exam session for the student |
| A53 | 〃 | `PostMultiUnitExam_FreeStudent_Returns403` | 403 + code |
| A54 | 〃 | `PostUnitExam_AdminWithoutSubscription_Returns200TestMode` | `isTestMode` true |
| A55 | `UsageEndpointTests` (`Integration/Subscriptions/`) | `Get_FreeStudentAfterTwoAnswers_ReturnsUsedAndRemaining` | `dailyQuizQuestionLimit` 10, used 2, remaining 8, tier `Free` |
| A56 | 〃 | `Get_SubscribedStudent_ReturnsUnlimited` | limit null, remaining null, tier `Base` |
| A57 | 〃 | `Get_Anonymous_Returns401` | 401 |
| A58 | 〃 | `Get_Admin_Returns403` | 403 |
| A59 | `FreeTierBrowseEndpointTests` (`Integration/Browse/`) | `GetUnit_FreeStudent_LocksAllButFirstLesson` | `lessons[*].isLocked` = `[false, true]` |
| A60 | 〃 | `GetLesson_FreeStudentLockedLesson_ReturnsNoContent` | `isLocked` true, `explanation` "", `objectives` [] |
| A61 | 〃 | `GetUnit_SubscribedStudent_LocksNoLesson` | all false |
| A62 | `FreeTierNextLessonEndpointTests` (`Integration/Mastery/`) | `GetOverview_FreeStudentOnlyLockedLessonHasQuestions_ReturnsNoNextLesson` | lesson 1 with no questions, lesson 2 with servable → `nextLesson` null |
| A63 | 〃 | `GetOverview_SubscribedStudentSameContent_ReturnsSecondLesson` | `nextLesson.lessonId` = lesson 2 |

Each integration test seeds its own subject, unit, lessons and questions with `ContentTestData` / `QuestionTestData` / `ExamTestData` / `MultiUnitExamTestData`, and signs in with `SignedInFreeStudentAsync`, `SignedInStudentAsync` or `ScopeTestData.SeedAdminAsync` + `SignedInClientAsync`. Answers use `AnswerAsync(client, sessionId, questionId, "b")`.

### Web (Vitest + RTL + MSW; errors stubbed as `HttpResponse.json({ code }, { status: 403 })`)
| # | File | `it(...)` | Asserts |
|---|------|-----------|---------|
| W-1 | `features/subscription/api/paywall.test.ts` | `maps each paywall code to its reason` | three codes → reasons |
| W-2 | 〃 | `returns null for other codes and for null` | `SESSION_NOT_FOUND`, `null` → null |
| W-3 | `features/subscription/api/invalidateEntitlementViews.test.ts` | `invalidates entitlement, usage, browse, mastery and exam queries only` | seeded cache: those keys `isInvalidated` true; a `/api/progress/...` key false |
| W-4 | `features/subscription/components/PaywallDialog.test.tsx` | `shows the daily limit with the configured count and a subscribe link` | title «Free limit reached», body with "10 practice questions", link `href="/student/subscription"` (MSW `freeUsage()`) |
| W-5 | 〃 | `shows the subscribers-only message for a locked lesson` | «For subscribers» + lesson body |
| W-6 | 〃 | `calls onClose when Later is pressed` | `onClose` called once (a `vi.fn()` prop is the boundary) |
| W-7 | `features/subscription/components/PaywallDialog.test.tsx` | `renders in Arabic` | `lng: 'ar'`; «انتهى الحد المجاني» visible |
| W-8 | `features/mastery/pages/StudentHomePage.plan.test.tsx` | `shows the free plan, today's counter and a subscribe link` | «Your plan: Free», «Practice questions today: 3 / 10», link to `/student/subscription` |
| W-9 | 〃 | `shows the base plan without a counter` | «Your plan: Base»; no «Practice questions today» |
| W-10 | `features/quiz/pages/PracticePage.freeTier.test.tsx` | `shows today's free counter for a free student` | «Free plan: 3 / 10 questions today» |
| W-11 | 〃 | `hides the counter for a subscribed student` | counter absent (default `baseUsage`) |
| W-12 | 〃 | `opens the daily-limit paywall when starting is refused` | start → 403 `QUIZ_DAILY_LIMIT_REACHED` → dialog «Free limit reached»; no inline alert |
| W-13 | 〃 | `opens the subscribers-only paywall when the lesson is locked` | 403 `LESSON_LOCKED` → «For subscribers» |
| W-14 | 〃 | `closes the paywall with Later` | dialog gone after «Later» |
| W-15 | `features/quiz/pages/QuizPage.freeTier.test.tsx` | `shows today's counter in the quiz header for a free student` | «Today: 3 / 10» |
| W-16 | 〃 | `opens the paywall instead of a toast when an answer hits the daily limit` | answer → 403 → dialog visible; no toast text |
| W-17 | 〃 | `updates the counter after an answer` | usage handler returns 4 after the POST → «Today: 4 / 10» |
| W-18 | 〃 | `hides the counter in a test-mode session` | `quizSession(..., { isTestMode: true })` + `freeUsage()` → no «Today:» |
| W-19 | `features/browse/pages/UnitPage.freeTier.test.tsx` | `shows a locked lesson without a link, with the badge and a subscribe link` | no link «Energy»; «Locked - subscribers only»; «Subscribe to unlock» → `/student/subscription` |
| W-20 | 〃 | `keeps open lessons linked` | link «Forces» present |
| W-21 | `features/browse/pages/LessonPage.freeTier.test.tsx` | `shows the subscribers-only notice and no tabs for a locked lesson` | notice text; `tablist`/tab links absent; «Subscribe» link to `/student/subscription` |
| W-22 | 〃 | `renders the locked notice in Arabic` | `lng: 'ar'`, root `dir="rtl"`, Arabic notice |
| W-23 | `features/exam/pages/ExamStartPage.freeTier.test.tsx` | `opens the exam paywall when starting is refused for a free student` | 403 `EXAM_REQUIRES_SUBSCRIPTION` → «Exams are included in the Base plan.»; subscribe link |
| W-24 | `features/exam/pages/MultiExamBuilderPage.freeTier.test.tsx` | `opens the exam paywall when the multi-unit start is refused` | same |
| W-25 | `features/quiz/pages/QuizResultPage.freeTier.test.tsx` | `opens the paywall when a new practice is refused` | «New practice» → 403 `QUIZ_DAILY_LIMIT_REACHED` → dialog |

Existing web tests are not edited. They stay green through the fixture and default-handler changes listed above.

## Definition of done
- [ ] `LessonPosition`, `LessonAccess` exist in `Elmanhg.Domain.Lessons`; A1–A7 green.
- [ ] `FreeTierGate` is the only place that throws the three new codes; every gate loads entitlement through `StudentEntitlementLoader.LoadAsync` (no second entitlement definition).
- [ ] Quota values come only from `SubscriptionsOptions` (`FreeDailyQuizQuestions`, `FreeOpenLessonsPerUnit` via `EntitlementResult`); no numeric literal for a limit in code.
- [ ] `Subscriptions:DailyQuotaTimeZone` exists in the options (default `Africa/Cairo`), is validated at startup, and appears in `appsettings.example.json`, `ApiFactory` and `docs/subscriptions.md`.
- [ ] Quiz start: locked lesson → 403 `LESSON_LOCKED` (start and resume); new session at limit → 403 `QUIZ_DAILY_LIMIT_REACHED`; resume at limit allowed; Admin exempt.
- [ ] Answer submit: new attempt at limit → 403 and no attempt stored; replay not gated; `IsTestMode` exempt; locked lesson → 403.
- [ ] New unit and multi-unit exam → 403 `EXAM_REQUIRES_SUBSCRIPTION` for Free; resume ungated; Admin exempt; checked before `EXAM_LESSONS_NOT_OPENED`.
- [ ] `GET /api/browse/units/{id}` returns `isLocked` per lesson; `GET /api/browse/lessons/{id}` withholds explanation, summary, video and objectives when locked.
- [ ] Home next-lesson candidates exclude locked lessons for Free only.
- [ ] `GET /api/subscriptions/usage` has policy `Subscription.Manage` and returns `UsageResult`; A55–A58 green.
- [ ] Error codes are in `ErrorCodes.cs`, both resx files and both web `common:errors` files.
- [ ] `SignedInStudentAsync` seeds Base; the listed subscription and payment test files use `SignedInFreeStudentAsync`; no other existing test assertion changed; the whole existing suite is green (#74–#86 flows intact).
- [ ] `api/openapi/v1.json`, the Orval client and the Postman collection are regenerated or updated and match the API.
- [ ] Web: paywall dialog on quiz start, answer, new practice, unit exam and multi-unit exam, with «اشترك» linking to `/student/subscription` and «لاحقًا» closing it; counters on Home, practice and quiz (hidden for Base and test mode); locked lesson card and locked lesson page; no lesson opening posted for a locked lesson; fake payment success invalidates entitlement views.
- [ ] Every new user-visible string is in both `ar` and `en`; tokens only; logical properties only; files within the size limits.
- [ ] A1–A63 and W-1–W-25 exist with exactly these names and pass; each new test fails when its production line is removed (mutation check).
- [ ] Docs updated as listed (subscriptions, browsing, sessions, exams, mastery, claude-design-prompt §4 and §7).
- [ ] CI parity: `dotnet test api/ -c Release` green with `appsettings.json` moved aside; web typecheck, lint, `vitest run`, and `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` clean.
