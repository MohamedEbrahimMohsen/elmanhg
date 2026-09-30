# Plan — [E11.S1] Dashboard metrics queries (#104)

## Goal
An admin can call nine read-only endpoints under `GET /api/dashboard/*` and get the PRD §10.3 cards: Students (total, new, DAU, MAU), Subscribers (active by plan, churn, MRR), Content (curriculum and question inventory, servable total), Solve rate, Success rate (overall and per subject, unit and lesson), Validation (backlog, median time to decision, per-teacher throughput), Ask a Teacher (open, overdue, breaches, median reply time, SLA compliance), Payments (successful and failed, revenue by day, refunds) and the Sign-up funnel. Each card takes a date range, and every card with a subject dimension also takes a subject. Results are cached for 60 s. DAU and MAU come from a new `UserActivityDays` read model. It gets one row per user per Cairo day, written on login, token refresh, registration and any authenticated request. #105 (UI), #106 and #107 reuse these queries and repository methods.

## Scope
**In:** 9 query slices in `Application/Dashboard/*` plus shared window, filter, cache and rate helpers; the `UserActivityDay` entity, its repository and the `UserActivityBehaviour` (all four sub-tasks); `QuestionDecision.SubmittedAt`, so time-to-decision is exact; new aggregate repository methods on existing repositories; one migration (`AddDashboardMetrics`) with indexes; `DashboardController`; `DashboardOptions`; 2 error codes; tests; OpenAPI and Orval regeneration; Postman; docs (`docs/dashboard.md` new, PRD §10.3/§15, `docs/analytics.md`).
**Out:** all UI (#105), the per-teacher stats endpoint (#107, which reuses `GetDecisionStatsAsync(..., teacherId)` and `GetReplyStatsAsync(..., teacherId)` added here), user administration (#106), and materialised views or scheduled rollups.
**Deferred:** none. Nothing needs external credentials.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | "Read-model or SQL views" | Live aggregate queries in repository methods (LINQ, or parameterised `SqlQuery` where the query needs time-zone day bucketing or `percentile_cont`), backed by new indexes and a 60 s cache. The only stored read model is `UserActivityDays`. No DB views. | Volumes are small (v1). Views would bypass the EF model and the soft-delete filter. The cache is the sub-task's latency answer. |
| 2 | One endpoint or one per card | One query and one endpoint per card, like Morabh `DashboardController` (one route per card). | #105 can load cards independently, #107 reuses slices, and each handler stays under 100 lines. |
| 3 | Route | `api/dashboard/{card}`, policy `DefaultCodes.DashboardsView`, which already exists (Admin). | The policy was pre-seeded by #11. |
| 4 | Date filter shape | `DateOnly? from`, `DateOnly? to`: Cairo local days, both inclusive. Missing `to` → today. Missing `from` → `to − (DefaultRangeDays − 1)`, with `DefaultRangeDays` = 30. Instants are `[StartOfDay(from), StartOfDay(to + 1))`. | Admins think in days. It matches the prototype's 7/14/30-day filter. The Cairo zone is already used for streaks and quotas. |
| 5 | Range validation | Checked on the resolved window: `from > to` → 422 `DASHBOARD_DATE_RANGE_INVALID`. A day count above `MaxRangeDays` (366) → 422 `DASHBOARD_DATE_RANGE_TOO_WIDE`. | Codes are reused from Morabh `ErrorCodes.cs`. |
| 6 | Subject filter | `subjectId` is accepted by Content, Solve rate, Success rate, Validation and Ask a Teacher. An unknown id → 404 `SUBJECT_NOT_FOUND` (existing code). Students, Subscribers, Payments and Funnel do not take `subjectId`: they have no subject dimension. | This departs from PRD "all charts filterable by subject", so PRD §10.3 is updated in this change (docs-sync). |
| 7 | Content card and range | Content is a current snapshot: it takes `subjectId` only, with no `from` or `to` and no validator. | Inventory has no time axis. |
| 8 | "new this week", "active today", "active this month", "churned this month" | These are fixed windows relative to the Cairo today and ignore the range: week = the last `RecentWeekDays` (7) days including today, month = the last `RecentMonthDays` (30) days including today. Range-based siblings are returned alongside (`NewInRange`, `ChurnedInRange`, daily series). | This gives the PRD values literally and the filterable values together. A rolling 30 days matches the prototype's MAU. |
| 9 | Who is "active" (DAU/MAU) | A student (`User.Role == Student`) with a `UserActivityDays` row for the day. Rows are written for every role; the queries filter to students. | Sub-task: "from login and activity events". Keeping teacher and admin rows lets #106 reuse the table. |
| 10 | How activity is recorded | `UserActivityBehaviour<TRequest,TResponse>` (MediatR pipeline). After `next` succeeds, it takes `ICurrentUserService.UserId`, or `AuthResult.User.Id` when the response is an `AuthResult` (login, register, refresh). It computes the Cairo day. If `IMemoryCache` key `user-activity:{userId:N}:{yyyy-MM-dd}` is absent, it calls `AddIfAbsentAsync` and sets the key for 1 day. Anonymous requests, and requests whose handler throws, record nothing. | Login events and activity events come from one hook, with at most one insert per user per day per instance. |
| 11 | Activity insert mechanics | `INSERT … ON CONFLICT ("UserId","Day") DO NOTHING` through `Database.ExecuteSqlAsync` (parameterised), outside change tracking and the handler's `SaveChangesAsync`. | Idempotent under races. It never adds a tracked entity to a query's unit of work. It is non-audited telemetry. This is the one named use of raw DML (skill §6.6). |
| 12 | Solve rate | `Σ attempts in range ÷ Σ daily active students in range` (student-days), rounded to 2 dp; `null` when the denominator is 0. `subjectId` filters the numerator only, because activity has no subject. The result also returns daily `(attempts, activeStudents)`. | PRD: "attempts per active student per day". |
| 13 | Which attempts count (solve and success rate) | Attempts whose session is not `IsTestMode`, whose question is not soft-deleted, and whose `CreatedAt` is in range. All session kinds are included. Correct means `NormalisedScore >= Mastery:CorrectThreshold` (0.8). | Same threshold as sessions and mastery. Admin test mode is not student behaviour. |
| 14 | Success rate groups | `BySubject`, `ByUnit` (`ParentId` = subject) and `ByLesson` (`ParentId` = unit), each only for groups with ≥ 1 attempt, in curriculum order (subject, unit and lesson `Order`, then `Name`). Rate is a 0–1 decimal, 4 dp, or `null`. | PRD "overall and per subject/unit/lesson". |
| 15 | Question status buckets | `QuestionsRetired` counts every question with `RetiredAt != null`. Pending, Approved and Rejected count only non-retired questions. `QuestionsByType` counts all questions, zero-filled for every `QuestionType`. `ServableTotal` uses `ServableQuestionSpecification`, the only definition. | Retired is final (#155) and not a `QuestionValidationStatus` value. |
| 16 | Time to decision | Add `QuestionDecision.SubmittedAt`, a copy of `Question.SubmittedAt` at decision time. The median is `percentile_cont(0.5)` of `DecidedAt − SubmittedAt` over decisions in range, in whole seconds. The migration backfills existing rows from `QuestionRevisions.EditedAt` of the decided version, falling back to `DecidedAt`. | `Question.SubmittedAt` is overwritten on resubmit, so without the column earlier decisions cannot be measured. This also gives #107 exact per-teacher decision time. |
| 17 | Validation backlog | Pending, non-retired questions right now (a snapshot). `Approved` and `Rejected` count decisions in range. `ByTeacher` groups decisions in range by `DecidedBy`, ordered by total descending, then display name. `DailyDecisions` is decisions per day (the #105 throughput chart). | — |
| 18 | Ask a Teacher | Snapshot counts: `OpenThreads` (Status ≠ Closed, as in the prototype), `AwaitingReply` (Open) and `OverdueNow` (Open and `SlaDueAt <= now`, the same rule as `IsOverdueAt`). In range: `SlaBreaches` (`TeacherThreadSlaEvents` of kind Breach, `OccurredAt` in range). A reply is a non-student message with `CreatedAt` in range. Its wait is measured from the latest earlier student message in the thread. `RepliedWithinSla` means wait ≤ `Subscriptions:AskTeacherReplySlaHours`. `SlaComplianceRate` = within ÷ replies (4 dp). `MedianReplySeconds` = `percentile_cont`. | Follow-ups start a new window (docs/ask-teacher.md). PRD §2.2 measures SLA compliance. |
| 19 | Subscribers | Active means `Status ∈ {Active, PastDue}` (still entitled), counted per subscription and zero-filled for both plans. Churned means `Status ∈ {Cancelled, Expired}` with `CancelledAt ?? ExpiredAt` in the window. MRR = Σ over active subscriptions of the latest `Succeeded` payment's `AmountMinor ÷ PeriodMonths`, rounded to a whole minor unit. Subscriptions without such a payment (complimentary) add 0. The currency is `Subscriptions:Currency`. | Uses the actual price paid and normalises termly and yearly plans to monthly. |
| 20 | Payments | `Succeeded` = status Succeeded or Refunded with `CompletedAt` in range; `Failed` = Failed with `CompletedAt` in range; `Revenue` = Σ amount of those succeeded; `Refunds` / `Refunded` = count and Σ amount with `RefundedAt` in range; `NetRevenue` = Revenue − Refunded; `RevenueByDay` = gross per Cairo day of `CompletedAt`, zero-filled. | A refunded payment still counts as a success on the day it was paid. The refund is counted on the day of the refund. |
| 21 | Funnel | As `docs/analytics.md`: distinct `AnonymousId` per step within the range, in enum order. `ConversionFromPrevious` is `null` for the first step and when the previous step is 0. The timing is the median of first `FirstQuizAnswered` − first `LandingViewed` per visitor (both in range, answered ≥ landed). `CompletedJourneys` is the number of such visitors. | The contract was written in #110. |
| 22 | Cache | `DashboardCacheBehaviour` (pipeline, request `is IDashboardQuery`) uses `IMemoryCache`, the key `IDashboardQuery.CacheKey` and a TTL of `Dashboard:CacheSeconds` (60). `0` disables caching (the test factory sets 0). No event invalidation. | The sub-task asks for a short TTL. The `servable-count` precedent (#67) uses `IMemoryCache`. A default-window key has no date, so it may serve up to 60 s of stale data across midnight, which is accepted. |
| 23 | Rounding | Rates are 4 dp and solve rate is 2 dp, both `MidpointRounding.AwayFromZero`. Durations are whole seconds (`long?`). Money is `Money(long AmountMinor, string Currency)`. | Presentation precision is an invariant, not a tunable. |
| 24 | `GeneratedAt` | Every result carries `GeneratedAt` (the handler's `now`). | The UI can show freshness under caching. |
| 25 | Row types for `SqlQuery<T>` | Domain read records materialised by `SqlQuery` are non-positional `sealed record`s with `{ get; init; }` properties. Records built only by LINQ projections are positional. | EF Core materialises unmapped `SqlQuery` types through settable properties. The only existing precedent is scalar `SqlQuery`. |
| 26 | Midnight in the Cairo DST gap | `DashboardWindow.StartOfDay` uses the `AskTeacherGate.MonthStart` formula `wallClock − zone.GetUtcOffset(wallClock − zone.GetUtcOffset(wallClock))` and never calls `ConvertTimeToUtc`. | Egypt springs forward at 00:00, so local midnight is invalid on that day and `ConvertTimeToUtc` would throw. |

**Morabh reuse:**

| Reused piece | Morabh source |
|---|---|
| Per-card dashboard query layout | `Morabh.APIs/Controllers/Dashboard/DashboardController.cs`, `Morabh.Application/Dashboard/*` |
| Error codes and resx strings | `Morabh.Application/Exceptions/ErrorCodes.cs` (`DashboardDateRangeInvalid`, `DashboardDateRangeTooWide`), `Morabh.APIs/Resources/Messages.{ar,en}.resx` |
| Range rules | `Morabh.Application/Dashboard/GetActiveUsers/GetActiveUsersQueryValidator.cs` |
| Window defaulting | `Morabh.Application/Dashboard/Shared/DashboardPeriodExtensions.ResolveWindow` |
| Result shapes | `Morabh.Application/Dashboard/Shared/DashboardSharedResults.cs` (`TimeSeriesPointResult` → `DailyValueResult`, `FunnelStageResult` → `FunnelStepResult`) |

Morabh aggregates in memory (`FindAsync(x => true)`); here every aggregate runs in SQL (skill §6.6). Everything else is new, with no Morabh equivalent: `UserActivityDay` (Morabh uses `User.LastLoginAt` and `AppEvent`), `UserActivityBehaviour`, `DashboardCacheBehaviour`, `DashboardWindow` with a time zone, and every metric definition.

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Domain/Questions/QuestionDecision.cs` | Add `public DateTimeOffset SubmittedAt { get; private set; }`. `Create(...)` gains the parameter `DateTimeOffset submittedAt`, placed before `decidedAt`, and assigns it. |
| `api/Elmanhg.Domain/Questions/Question.Approval.cs` | Both `QuestionDecision.Create` calls pass `SubmittedAt` (the question's current value). |
| `api/Elmanhg.Domain/Questions/IQuestionRepository.cs` | Add `CountInventoryAsync`, `CountServableInSubjectAsync`, `GetDecisionStatsAsync`, `CountDecisionsByTeacherAsync`, `CountDecisionsByDayAsync` (signatures below). |
| `api/Elmanhg.Infrastructure/Questions/QuestionRepository.cs` | Implement those 5 methods. |
| `api/Elmanhg.Domain/Sessions/ISessionRepository.cs` / `api/Elmanhg.Infrastructure/Sessions/SessionRepository.cs` | Add and implement `CountAttemptsByDayAsync`, `GetAttemptOutcomesByLessonAsync`. |
| `api/Elmanhg.Domain/Subscriptions/ISubscriptionRepository.cs` / `api/Elmanhg.Infrastructure/Subscriptions/SubscriptionRepository.cs` | Add and implement `CountActiveByPlanAsync`, `GetMonthlyRecurringRevenueMinorAsync`. The interface gets a body. |
| `api/Elmanhg.Domain/Subscriptions/IPaymentRepository.cs` / `api/Elmanhg.Infrastructure/Subscriptions/PaymentRepository.cs` | Add and implement `GetTotalsAsync`, `GetRevenueByDayAsync`. |
| `api/Elmanhg.Domain/Lessons/ILessonRepository.cs` / `api/Elmanhg.Infrastructure/Lessons/LessonRepository.cs` | Add and implement `CountByStateAsync`. |
| `api/Elmanhg.Domain/TeacherThreads/ITeacherThreadRepository.cs` / `api/Elmanhg.Infrastructure/TeacherThreads/TeacherThreadRepository.cs` | Add and implement `GetReplyStatsAsync`. |
| `api/Elmanhg.Domain/TeacherThreads/ITeacherThreadSlaEventRepository.cs` / `api/Elmanhg.Infrastructure/TeacherThreads/TeacherThreadSlaEventRepository.cs` | Add and implement `CountBreachesAsync`. |
| `api/Elmanhg.Domain/Analytics/IFunnelEventRepository.cs` / `api/Elmanhg.Infrastructure/Analytics/FunnelEventRepository.cs` | Add and implement `CountVisitorsByStepAsync`, `GetLandingToFirstAnswerTimingAsync`. |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | `public DbSet<UserActivityDay> UserActivityDays { get; set; }`; const `UserActivityDayIndex = "IX_UserActivityDays_UserId_Day"`; new `ConfigureUserActivityDays` called from `OnModelCreating` after `ConfigureFunnelEvents`. Indexes: `ConfigureUsers` → `HasIndex(x => new { x.Role, x.CreationDate })`; Attempt → `HasIndex(x => x.CreatedAt)`; Payment → `HasIndex(x => x.CompletedAt)`; QuestionDecision → `HasIndex(x => x.DecidedAt)`; TeacherMessage → `HasIndex(x => x.CreatedAt)`; TeacherThreadSlaEvent → `HasIndex(x => new { x.Kind, x.OccurredAt })`. Global filter line for `UserActivityDay`. |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | `services.AddScoped<IUserActivityDayRepository, UserActivityDayRepository>();` after the `IFunnelEventRepository` line. |
| `api/Elmanhg.Application/DependencyInjection.cs` | `services.AddOptions<DashboardOptions>().BindConfiguration(DashboardOptions.SectionName).ValidateDataAnnotations().Validate(x => TimeZoneInfo.TryFindSystemTimeZoneById(x.TimeZone, out _), "Dashboard:TimeZone must be a known IANA time zone id.").Validate(x => x.DefaultRangeDays <= x.MaxRangeDays, "Dashboard:DefaultRangeDays must not exceed MaxRangeDays.").ValidateOnStart();`. Also `services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UserActivityBehaviour<,>));` and `services.AddTransient(typeof(IPipelineBehavior<,>), typeof(DashboardCacheBehaviour<,>));`, both after the `SubjectScopeBehaviour` line. |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | New group `// DASHBOARD` before `// PLATFORM`, with 2 constants. |
| `api/Elmanhg.Api/Resources/Messages.ar.resx`, `Messages.en.resx` | 2 keys each (see Error codes). |
| `api/Elmanhg.Api/appsettings.example.json` | Add a line after `"Analytics"`: `"Dashboard": { "TimeZone": "Africa/Cairo", "CacheSeconds": 60, "DefaultRangeDays": 30, "MaxRangeDays": 366, "RecentWeekDays": 7, "RecentMonthDays": 30 },` |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | In the in-memory dictionary: `["Dashboard:TimeZone"] = "Africa/Cairo"`, `["Dashboard:CacheSeconds"] = "0"`, `["Dashboard:DefaultRangeDays"] = "30"`, `["Dashboard:MaxRangeDays"] = "366"`, `["Dashboard:RecentWeekDays"] = "7"`, `["Dashboard:RecentMonthDays"] = "30"`, with a one-line comment: parallel tests share the host, so caching would leak results between them. |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | Append `thirtyThird => thirtyThird.Should().EndWith("_AddDashboardMetrics")` to the migration list. |
| `api/Elmanhg.Tests/Domain/Questions/QuestionApprovalTests.cs`, `QuestionRejectionTests.cs` | 1 new test each (Test plan #3 and #4). |
| `api/openapi/v1.json` | Regenerated by `dotnet build`. |
| `web/src/shared/api/generated/**` | Regenerated by `npm --prefix web run gen:api`. Do not hand-edit. |
| `postman/elmanhg.postman_collection.json` | New folder `Dashboard`, inserted directly after `AdminPayments` so the admin token is current. 9 GET requests in controller order, e.g. `{{baseUrl}}/api/dashboard/students`, with disabled query params `from`/`to` (`2026-09-01`/`2026-09-30`) and, where applicable, `subjectId`. Each has the test `status is 200` plus one field check (`students` → `total` is a number; `funnel` → `steps` is an array; others → `generatedAt` exists). |
| `docs/PRD.md` | §10.3: replace "All charts filterable by date range and subject." with "All charts filterable by date range (Cairo days). Content, Solve rate, Success rate, Validation and Ask a Teacher are also filterable by subject; Students, Subscribers, Payments and the Sign-up funnel have no subject dimension, and Content is a current snapshot with no date range. Definitions, caching and endpoints: `docs/dashboard.md`." §15: add the line `UserActivityDay(id, user_id, day, first_seen_at)  -- one row per user per Cairo day; DAU/MAU (docs/dashboard.md)` after the `FunnelEvent` line. |
| `docs/analytics.md` | Intro: the read side is `GET /api/dashboard/funnel` (docs/dashboard.md); #105 draws it. Heading "Funnel definition (for #104)" → "Funnel definition". Add a bullet: steps and timing count only events inside the requested range. |

## Files to create

### Domain
| # | Path | Type | Contract |
|---|------|------|----------|
| D1 | `api/Elmanhg.Domain/Analytics/UserActivityDay.cs` | entity | `namespace Elmanhg.Domain.Analytics; public class UserActivityDay : Entity { Guid UserId; DateOnly Day; DateTimeOffset FirstSeenAt (all private set); private UserActivityDay(Guid id) : base(id) {} public static UserActivityDay Record(Guid userId, DateOnly day, DateTimeOffset seenAt) }`. `Record` returns `new(Guid.NewGuid()) { UserId, Day, FirstSeenAt = seenAt }`. No other members. |
| D2 | `api/Elmanhg.Domain/Analytics/IUserActivityDayRepository.cs` | repo interface | `: IRepository<UserActivityDay>` with `Task AddIfAbsentAsync(UserActivityDay activity, CancellationToken cancellationToken);` `Task<int> CountActiveStudentsAsync(DateOnly fromDay, DateOnly toDay, CancellationToken cancellationToken);` `Task<List<DailyTotal>> CountActiveStudentsByDayAsync(DateOnly fromDay, DateOnly toDay, CancellationToken cancellationToken);` |
| D3 | `api/Elmanhg.Domain/Analytics/FunnelStepCount.cs` | positional record | `public sealed record FunnelStepCount(FunnelEventType Type, int Visitors);` |
| D4 | `api/Elmanhg.Domain/Analytics/FunnelTiming.cs` | SqlQuery row | `public sealed record FunnelTiming { public int Completed { get; init; } public double? MedianSeconds { get; init; } }` |
| D5 | `api/Elmanhg.Domain/SharedKernel/MetricsWindow.cs` | value object | `public sealed record MetricsWindow(DateTimeOffset Start, DateTimeOffset End, string TimeZone);`, End exclusive. |
| D6 | `api/Elmanhg.Domain/SharedKernel/DailyTotal.cs` | SqlQuery row | `public sealed record DailyTotal { public DateOnly Day { get; init; } public long Value { get; init; } }` |
| D7 | `api/Elmanhg.Domain/Subscriptions/PlanCount.cs` | positional record | `public sealed record PlanCount(SubscriptionPlan Plan, int Count);` |
| D8 | `api/Elmanhg.Domain/Subscriptions/PaymentTotals.cs` | SqlQuery row | `{ int Succeeded; int Failed; long GrossMinor; int Refunds; long RefundedMinor }`, all `{ get; init; }`. |
| D9 | `api/Elmanhg.Domain/Subscriptions/SubscriptionChurnSpecification.cs` | static spec | `public static class SubscriptionChurnSpecification { public static Expression<Func<Subscription, bool>> ChurnedBetween(DateTimeOffset start, DateTimeOffset end) => x => (x.Status == SubscriptionStatus.Cancelled \|\| x.Status == SubscriptionStatus.Expired) && (x.CancelledAt ?? x.ExpiredAt) >= start && (x.CancelledAt ?? x.ExpiredAt) < end; }` |
| D10 | `api/Elmanhg.Domain/Lessons/LessonStateCount.cs` | positional record | `(LessonState State, int Count)` |
| D11 | `api/Elmanhg.Domain/Questions/QuestionInventoryCount.cs` | positional record | `(QuestionValidationStatus Status, QuestionType Type, bool IsRetired, int Count)` |
| D12 | `api/Elmanhg.Domain/Questions/QuestionDecisionStats.cs` | SqlQuery row | `{ int Approved; int Rejected; double? MedianSecondsToDecision }`, all `{ get; init; }`. |
| D13 | `api/Elmanhg.Domain/Questions/TeacherDecisionCount.cs` | positional record | `(Guid TeacherId, int Approved, int Rejected)` |
| D14 | `api/Elmanhg.Domain/Sessions/LessonAttemptOutcome.cs` | positional record | `(Guid SubjectId, Guid UnitId, Guid LessonId, int Attempts, int Correct)` |
| D15 | `api/Elmanhg.Domain/TeacherThreads/TeacherReplyStats.cs` | SqlQuery row | `{ int Replies; int RepliedWithinSla; double? MedianReplySeconds }`, all `{ get; init; }`. |

**New repository method signatures** (interfaces listed under Existing code; every `DateTimeOffset start, DateTimeOffset end` pair is half-open `[start, end)`):
```csharp
// IQuestionRepository
Task<List<QuestionInventoryCount>> CountInventoryAsync(Guid? subjectId, CancellationToken cancellationToken);
Task<int> CountServableInSubjectAsync(Guid subjectId, CancellationToken cancellationToken);
Task<QuestionDecisionStats> GetDecisionStatsAsync(DateTimeOffset start, DateTimeOffset end, Guid? subjectId, Guid? teacherId, CancellationToken cancellationToken);
Task<List<TeacherDecisionCount>> CountDecisionsByTeacherAsync(DateTimeOffset start, DateTimeOffset end, Guid? subjectId, CancellationToken cancellationToken);
Task<List<DailyTotal>> CountDecisionsByDayAsync(MetricsWindow window, Guid? subjectId, CancellationToken cancellationToken);
// ISessionRepository
Task<List<DailyTotal>> CountAttemptsByDayAsync(MetricsWindow window, Guid? subjectId, CancellationToken cancellationToken);
Task<List<LessonAttemptOutcome>> GetAttemptOutcomesByLessonAsync(DateTimeOffset start, DateTimeOffset end, Guid? subjectId, decimal correctThreshold, CancellationToken cancellationToken);
// ISubscriptionRepository
Task<List<PlanCount>> CountActiveByPlanAsync(CancellationToken cancellationToken);
Task<long> GetMonthlyRecurringRevenueMinorAsync(CancellationToken cancellationToken);
// IPaymentRepository
Task<PaymentTotals> GetTotalsAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken);
Task<List<DailyTotal>> GetRevenueByDayAsync(MetricsWindow window, CancellationToken cancellationToken);
// ILessonRepository
Task<List<LessonStateCount>> CountByStateAsync(Guid? subjectId, CancellationToken cancellationToken);
// ITeacherThreadRepository
Task<TeacherReplyStats> GetReplyStatsAsync(DateTimeOffset start, DateTimeOffset end, Guid? subjectId, Guid? teacherId, TimeSpan replySla, CancellationToken cancellationToken);
// ITeacherThreadSlaEventRepository
Task<int> CountBreachesAsync(DateTimeOffset start, DateTimeOffset end, Guid? subjectId, CancellationToken cancellationToken);
// IFunnelEventRepository
Task<List<FunnelStepCount>> CountVisitorsByStepAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken);
Task<FunnelTiming> GetLandingToFirstAnswerTimingAsync(DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken);
```

**Implementation rules (Infrastructure):** every method is read-only, and LINQ methods use `.AsNoTracking()`. SQL goes only through `_context.Database.SqlQuery<T>($"""…""")` or `ExecuteSqlAsync($"""…""")` with interpolated parameters; never `*Raw`. Enum values are passed as parameters built with `nameof(...)`, as in `SessionRepository.GetQuizActivityDaysAsync`. An optional id filter is written `({subjectId}::uuid IS NULL OR q."SubjectId" = {subjectId})`. Raw SQL adds `"IsDeleted" = false` for every table it reads; LINQ relies on the global filters. Every `SqlQuery` returning one row ends in `.SingleAsync(cancellationToken)`; series end in `.ToListAsync(cancellationToken)`. Every await has `.ConfigureAwait(false)`.

| Method | Implementation |
|---|---|
| `UserActivityDayRepository.AddIfAbsentAsync` | `ExecuteSqlAsync`: `INSERT INTO "UserActivityDays" ("Id","UserId","Day","FirstSeenAt","IsDeleted") VALUES ({activity.Id},{activity.UserId},{activity.Day},{activity.FirstSeenAt},false) ON CONFLICT ("UserId","Day") DO NOTHING`. |
| `CountActiveStudentsAsync` | LINQ: `_dbSet.Where(day in [from,to]).Join(_context.Set<User>().Where(u => u.Role == UserRole.Student), a => a.UserId, u => u.Id, (a, u) => a.UserId).Distinct().CountAsync`. |
| `CountActiveStudentsByDayAsync` | The same join, `.GroupBy(a => a.Day).Select(g => new DailyTotal { Day = g.Key, Value = g.LongCount() })`. |
| `SessionRepository.CountAttemptsByDayAsync` | SQL: `SELECT (a."CreatedAt" AT TIME ZONE {window.TimeZone})::date AS "Day", COUNT(*)::bigint AS "Value" FROM "Attempts" a JOIN "Sessions" s ON s."Id"=a."SessionId" JOIN "Questions" q ON q."Id"=a."QuestionId" WHERE a/s/q not deleted AND s."IsTestMode"=false AND a."CreatedAt" >= {window.Start} AND a."CreatedAt" < {window.End} AND (subject filter on q) GROUP BY 1`. |
| `GetAttemptOutcomesByLessonAsync` | LINQ method chain: `Attempts` join `_dbSet` (sessions, `!IsTestMode`) join `Questions` join `Lessons`, filtered by `CreatedAt` range and `subjectId == null \|\| q.SubjectId == subjectId`, grouped by `{ q.SubjectId, l.UnitId, q.LessonId }` → `new LessonAttemptOutcome(key.SubjectId, key.UnitId, key.LessonId, g.Count(), g.Count(x => x.NormalisedScore >= correctThreshold))`. |
| `SubscriptionRepository.CountActiveByPlanAsync` | LINQ: status Active or PastDue, `GroupBy(Plan)` → `PlanCount`. |
| `GetMonthlyRecurringRevenueMinorAsync` | `SqlQuery<long>`: `SELECT COALESCE(ROUND(SUM(p."AmountMinor"::numeric / p."PeriodMonths")), 0)::bigint AS "Value" FROM "Subscriptions" s JOIN LATERAL (SELECT x."AmountMinor", x."PeriodMonths" FROM "Payments" x WHERE x."SubscriptionId" = s."Id" AND x."Status" = {succeeded} AND x."IsDeleted" = false ORDER BY x."CompletedAt" DESC LIMIT 1) p ON true WHERE s."IsDeleted" = false AND s."Status" IN ({active}, {pastDue})`. |
| `PaymentRepository.GetTotalsAsync` | One `SqlQuery<PaymentTotals>` row over `"Payments"`. `COUNT(*) FILTER (...)::int` and `COALESCE(SUM("AmountMinor") FILTER (...), 0)::bigint` for the 5 columns, per Decision 20. Outer `WHERE` is not deleted AND (`CompletedAt` in range OR `RefundedAt` in range). |
| `GetRevenueByDayAsync` | SQL grouped by `("CompletedAt" AT TIME ZONE {tz})::date`, `SUM("AmountMinor")::bigint`, status ∈ {Succeeded, Refunded}, `CompletedAt` in window. |
| `LessonRepository.CountByStateAsync` | LINQ: `_dbSet.Join(Units, l => l.UnitId, u => u.Id, (l, u) => new { l.State, u.SubjectId })`, subject filter, `GroupBy(State)` → `LessonStateCount`. |
| `QuestionRepository.CountInventoryAsync` | LINQ: subject filter, `GroupBy(new { ValidationStatus, Type, IsRetired = RetiredAt != null })` → `QuestionInventoryCount`. |
| `CountServableInSubjectAsync` | `_dbSet.WhereServable(_context.Set<Lesson>()).Where(x => x.SubjectId == subjectId).CountAsync`. |
| `GetDecisionStatsAsync` | `SqlQuery<QuestionDecisionStats>`: `COUNT(*) FILTER (WHERE d."Outcome" = {approved})::int`, the same for rejected, and `percentile_cont(0.5) WITHIN GROUP (ORDER BY EXTRACT(EPOCH FROM (d."DecidedAt" - d."SubmittedAt"))::double precision)` over `"QuestionDecisions" d JOIN "Questions" q`. Filters: range on `DecidedAt`, optional subject on q and teacher on `d."DecidedBy"`. |
| `CountDecisionsByTeacherAsync` | LINQ: `QuestionDecisions` in range joined to `_dbSet` (subject-filtered) → `GroupBy(DecidedBy)` → `new TeacherDecisionCount(key, g.Count(Approved), g.Count(Rejected))`. |
| `CountDecisionsByDayAsync` | SQL: day of `DecidedAt AT TIME ZONE`, `COUNT(*)::bigint`, joined to Questions for the subject filter. |
| `TeacherThreadRepository.GetReplyStatsAsync` | `SqlQuery<TeacherReplyStats>`: inner select over `"TeacherMessages" m JOIN "TeacherThreads" t` where `m."SenderId" <> t."StudentId"` and `m."CreatedAt"` is in range, with optional subject on t and teacher on `m."SenderId"`. `JOIN LATERAL (SELECT s."CreatedAt" FROM "TeacherMessages" s WHERE s."ThreadId" = m."ThreadId" AND s."SenderId" = t."StudentId" AND s."CreatedAt" <= m."CreatedAt" AND s."IsDeleted" = false ORDER BY s."CreatedAt" DESC LIMIT 1) q ON true`, with wait `EXTRACT(EPOCH FROM (m."CreatedAt" - q."CreatedAt"))::double precision`. Outer select: `COUNT(*)::int AS "Replies"`, `COUNT(*) FILTER (WHERE wait <= {replySla.TotalSeconds})::int AS "RepliedWithinSla"`, `percentile_cont(0.5) WITHIN GROUP (ORDER BY wait) AS "MedianReplySeconds"`. |
| `TeacherThreadSlaEventRepository.CountBreachesAsync` | LINQ: kind Breach, `OccurredAt` in range, join `TeacherThreads` (subject-filtered), `CountAsync`. |
| `FunnelEventRepository.CountVisitorsByStepAsync` | LINQ: `OccurredAt` in range, `GroupBy(Type)` → `new FunnelStepCount(g.Key, g.Select(x => x.AnonymousId).Distinct().Count())`. |
| `GetLandingToFirstAnswerTimingAsync` | `SqlQuery<FunnelTiming>`: inner `SELECT MIN("OccurredAt") FILTER (WHERE "Type" = {landing}) AS "Landed", MIN(...) FILTER (WHERE "Type" = {answered}) AS "Answered" … GROUP BY "AnonymousId"`, range and not-deleted filtered. Outer: `COUNT(*)::int AS "Completed"`, `percentile_cont(0.5) … (ORDER BY EXTRACT(EPOCH FROM ("Answered" - "Landed"))::double precision) AS "MedianSeconds"` `WHERE "Landed" IS NOT NULL AND "Answered" >= "Landed"`. |

### Infrastructure
| # | Path | Contract |
|---|------|----------|
| I1 | `api/Elmanhg.Infrastructure/Analytics/UserActivityDayRepository.cs` | `public class UserActivityDayRepository(AppDbContext context) : Repository<UserActivityDay>(context), IUserActivityDayRepository` implementing D2. |
| I2 | `api/Elmanhg.Infrastructure/Migrations/<timestamp>_AddDashboardMetrics.cs` (+ `.Designer.cs`, snapshot) | Generate it with `dotnet ef migrations add AddDashboardMetrics -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`, then hand-edit `Up`: (1) `AddColumn<DateTimeOffset>("SubmittedAt", "QuestionDecisions", nullable: true)`; (2) `migrationBuilder.Sql("UPDATE \"QuestionDecisions\" AS d SET \"SubmittedAt\" = r.\"EditedAt\" FROM \"QuestionRevisions\" AS r WHERE r.\"QuestionId\" = d.\"QuestionId\" AND r.\"Version\" = d.\"Version\";")` and `migrationBuilder.Sql("UPDATE \"QuestionDecisions\" SET \"SubmittedAt\" = \"DecidedAt\" WHERE \"SubmittedAt\" IS NULL;")`; (3) `AlterColumn` to `nullable: false`. Then the generated table `UserActivityDays` (Id, UserId FK → AspNetUsers Restrict, Day `date`, FirstSeenAt, IsDeleted, DeletedAt), unique `IX_UserActivityDays_UserId_Day` (no filter), index `Day`, and the 6 new indexes. `Down` reverses all of it. No other `Drop*` or `Rename*`. |

`ConfigureUserActivityDays`: `Id.ValueGeneratedNever()`; `HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(Restrict)`; `HasIndex(x => new { x.UserId, x.Day }, UserActivityDayIndex).IsUnique()`; `HasIndex(x => x.Day)`.

### Application — shared
| # | Path | Contract |
|---|------|----------|
| A1 | `api/Elmanhg.Application/Shared/Options/DashboardOptions.cs` | `public sealed class DashboardOptions { public const string SectionName = "Dashboard"; [Required] string TimeZone = "Africa/Cairo"; [Range(0, 3600)] int CacheSeconds = 60; [Range(1, 3650)] int DefaultRangeDays = 30; [Range(1, 3650)] int MaxRangeDays = 366; [Range(1, 31)] int RecentWeekDays = 7; [Range(1, 366)] int RecentMonthDays = 30; }`, all `{ get; set; }`. |
| A2 | `api/Elmanhg.Application/Shared/Analytics/UserActivityBehaviour.cs` | `public sealed class UserActivityBehaviour<TRequest, TResponse>(ICurrentUserService currentUserService, IUserActivityDayRepository userActivityDayRepository, IMemoryCache memoryCache, TimeProvider timeProvider, IOptions<DashboardOptions> dashboardOptions) : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull`. Private const `TimeSpan SeenKeyLifetime = TimeSpan.FromDays(1)`, with the comment "one insert per user per day per instance; the unique index absorbs the rest". `Handle`: (1) `var response = await next(cancellationToken)`; (2) `userId = currentUserService.UserId is { } id && id != Guid.Empty ? id : (response as AuthResult)?.User.Id`; if null, return response; (3) `now = timeProvider.GetUtcNow()`, `day = DashboardWindow.LocalDay(now, options.TimeZone)`; (4) `key = string.Create(CultureInfo.InvariantCulture, $"user-activity:{userId:N}:{day:yyyy-MM-dd}")`; if `memoryCache.TryGetValue(key, out _)`, return response; (5) `await userActivityDayRepository.AddIfAbsentAsync(UserActivityDay.Record(userId.Value, day, now), cancellationToken)`; (6) `memoryCache.Set(key, true, SeenKeyLifetime)`; return response. |
| A3 | `api/Elmanhg.Application/Dashboard/Shared/IDashboardQuery.cs` | `public interface IDashboardQuery { string CacheKey { get; } }` |
| A4 | `api/Elmanhg.Application/Dashboard/Shared/IDashboardRangeQuery.cs` | `public interface IDashboardRangeQuery : IDashboardQuery { DateOnly? From { get; } DateOnly? To { get; } }` |
| A5 | `api/Elmanhg.Application/Dashboard/Shared/DashboardCacheKey.cs` | `public static class DashboardCacheKey { public static string For(string card, DateOnly? from, DateOnly? to, Guid? subjectId) => string.Create(CultureInfo.InvariantCulture, $"dashboard:{card}:{from:yyyy-MM-dd}:{to:yyyy-MM-dd}:{subjectId:N}"); }` |
| A6 | `api/Elmanhg.Application/Dashboard/Shared/DashboardCacheBehaviour.cs` | `public sealed class DashboardCacheBehaviour<TRequest, TResponse>(IMemoryCache memoryCache, IOptions<DashboardOptions> dashboardOptions) : IPipelineBehavior<TRequest, TResponse> where TRequest : notnull`. If `request is not IDashboardQuery query \|\| options.CacheSeconds == 0`, return `next`. If `memoryCache.TryGetValue(query.CacheKey, out TResponse? cached) && cached is not null`, return `cached`. Otherwise run `next`, `memoryCache.Set(query.CacheKey, result, TimeSpan.FromSeconds(options.CacheSeconds))` and return. |
| A7 | `api/Elmanhg.Application/Dashboard/Shared/DashboardWindow.cs` | `public sealed record DashboardWindow(DateOnly From, DateOnly To, DateOnly Today, DateTimeOffset Now, string TimeZone)`. Members: `DateTimeOffset Start => StartOfDay(From)`; `DateTimeOffset End => StartOfDay(To.AddDays(1))`; `int DayCount => To.DayNumber - From.DayNumber + 1`; `IEnumerable<DateOnly> Days => Enumerable.Range(0, Math.Max(DayCount, 0)).Select(From.AddDays)`; `MetricsWindow ToMetricsWindow() => new(Start, End, TimeZone)`; `DateTimeOffset StartOfDay(DateOnly day)` uses the Decision 26 formula (with `wallClock = new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)`) and returns UTC; `static DateOnly LocalDay(DateTimeOffset now, string timeZone) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, FindSystemTimeZoneById(timeZone)).DateTime)`; `static DashboardWindow Resolve(DateOnly? from, DateOnly? to, DateTimeOffset now, DashboardOptions options)`, where today = `LocalDay`, `to ??= today`, `from ??= to.AddDays(-(options.DefaultRangeDays - 1))`. |
| A8 | `api/Elmanhg.Application/Dashboard/Shared/DashboardFilterRules.cs` | `public static class DashboardFilterRules { public static void AddDashboardFilterRules<T>(this AbstractValidator<T> validator, DashboardOptions options, TimeProvider timeProvider) where T : IDashboardRangeQuery }`. Rule 1: `validator.RuleFor(x => x).Must(x => Resolve(x).From <= Resolve(x).To).WithErrorCode(ErrorCodes.DashboardDateRangeInvalid)`. Rule 2: `.Must(x => Resolve(x).DayCount <= options.MaxRangeDays).WithErrorCode(ErrorCodes.DashboardDateRangeTooWide)`. `Resolve` = `DashboardWindow.Resolve(x.From, x.To, timeProvider.GetUtcNow(), options)`. |
| A9 | `api/Elmanhg.Application/Dashboard/Shared/DashboardSubjectGuard.cs` | `public static async Task EnsureExistsAsync(Guid? subjectId, ISubjectRepository subjectRepository, CancellationToken cancellationToken)`: returns when null; `GetByIdAsync(id, cancellationToken, asNoTracking: true)`; null → `throw new NotFoundCoreException(ErrorCodes.SubjectNotFound)`. |
| A10 | `api/Elmanhg.Application/Dashboard/Shared/DashboardSeries.cs` | `public static List<DailyValueResult> Fill(DashboardWindow window, IEnumerable<DailyTotal> totals)` returns every day in `window.Days`, taking the value from totals or 0. |
| A11 | `api/Elmanhg.Application/Dashboard/Shared/DashboardRates.cs` | Consts `RateDecimals = 4` and `PerStudentDecimals = 2`, with the WHY comment "presentation precision; not a tunable". `public static decimal? Ratio(long numerator, long denominator, int decimals)` returns `null` when denominator = 0, else `Math.Round((decimal)numerator / denominator, decimals, MidpointRounding.AwayFromZero)`. `public static long? Seconds(double? seconds)` returns `null` or `(long)Math.Round(seconds.Value, MidpointRounding.AwayFromZero)`. |
| A12 | `api/Elmanhg.Application/Dashboard/Shared/DailyValueResult.cs` | `public sealed record DailyValueResult(DateOnly Date, long Value);` |

### Application — cards
Every query is a `sealed record` implementing `IRequest<TResult>` and `IDashboardRangeQuery` (Content: `IDashboardQuery`), with `public string CacheKey => DashboardCacheKey.For("<card>", From, To, SubjectId-or-null);`. Every validator is `public sealed class XValidator : AbstractValidator<XQuery> { public XValidator(IOptions<DashboardOptions> dashboardOptions, TimeProvider timeProvider) { this.AddDashboardFilterRules(dashboardOptions.Value, timeProvider); } }`. Every handler starts, where marked, with `await DashboardSubjectGuard.EnsureExistsAsync(...)` and then `var window = DashboardWindow.Resolve(request.From, request.To, timeProvider.GetUtcNow(), dashboardOptions.Value)`. Every result is admin-facing: plain strings and no `.Localized()`, since the repo has no `LocalizedText`. Namespaces are `Elmanhg.Application.Dashboard.<Folder>`.

| # | Folder / files | Query | Handler (ctor deps → steps) | Result |
|---|---|---|---|---|
| C1 | `GetStudentMetrics/` Query, Validator, Handler, `StudentMetricsResult.cs` | `GetStudentMetricsQuery(DateOnly? From, DateOnly? To)`, card `"students"` | `(IUserRepository userRepository, IUserActivityDayRepository userActivityDayRepository, TimeProvider timeProvider, IOptions<DashboardOptions> dashboardOptions)`: window; `total = userRepository.CountAsync(ct, x => x.Role == UserRole.Student)`; `newInRange` = the same with `CreationDate >= window.Start && < window.End`; `newThisWeek` = `CreationDate >= window.StartOfDay(window.Today.AddDays(-(RecentWeekDays - 1)))`; `activeToday = CountActiveStudentsAsync(Today, Today)`; `activeThisMonth = CountActiveStudentsAsync(Today.AddDays(-(RecentMonthDays - 1)), Today)`; `daily = CountActiveStudentsByDayAsync(window.From, window.To)`. | `StudentMetricsResult(DateOnly From, DateOnly To, int Total, int NewInRange, int NewThisWeek, int ActiveToday, int ActiveThisMonth, List<DailyValueResult> DailyActive, DateTimeOffset GeneratedAt)` |
| C2 | `GetSubscriberMetrics/` Query, Validator, Handler, `SubscriberMetricsResult.cs`, `PlanCountResult.cs` | `GetSubscriberMetricsQuery(DateOnly? From, DateOnly? To)`, `"subscribers"` | `(ISubscriptionRepository subscriptionRepository, TimeProvider timeProvider, IOptions<DashboardOptions> dashboardOptions, IOptions<SubscriptionsOptions> subscriptionsOptions)`: window; `byPlan = CountActiveByPlanAsync`; `churnedInRange = CountAsync(ct, SubscriptionChurnSpecification.ChurnedBetween(window.Start, window.End))`; `churnedThisMonth` = `ChurnedBetween(window.StartOfDay(Today.AddDays(-(RecentMonthDays - 1))), window.Now)`; `mrr = GetMonthlyRecurringRevenueMinorAsync`. `ActiveByPlan` = `Enum.GetValues<SubscriptionPlan>()` zero-filled; `ActiveSubscriptions` = sum. | `SubscriberMetricsResult(DateOnly From, DateOnly To, int ActiveSubscriptions, List<PlanCountResult> ActiveByPlan, int ChurnedInRange, int ChurnedThisMonth, Money MonthlyRecurringRevenue, DateTimeOffset GeneratedAt)`; `PlanCountResult(SubscriptionPlan Plan, int Count)` |
| C3 | `GetContentMetrics/` Query, Handler, `ContentMetricsResult.cs`, `QuestionTypeCountResult.cs`, `ContentMetricsResultGenerator.cs` (no validator) | `GetContentMetricsQuery(Guid? SubjectId) : IRequest<ContentMetricsResult>, IDashboardQuery`, key `For("content", null, null, SubjectId)` | `(ISubjectRepository, ICurriculumUnitRepository, ILessonRepository, IQuestionRepository, TimeProvider)`: guard; `subjects = SubjectId is null ? subjectRepository.CountAsync(ct) : 1`; `units = unitRepository.CountAsync(ct, x => request.SubjectId == null \|\| x.SubjectId == request.SubjectId)`; `lessons = CountByStateAsync(SubjectId)`; `inventory = CountInventoryAsync(SubjectId)`; `servable = SubjectId is null ? CountServableAsync : CountServableInSubjectAsync(SubjectId.Value)`; return `ContentMetricsResultGenerator.Generate(request.SubjectId, subjects, units, lessons, inventory, servable, timeProvider.GetUtcNow())`, which does the Decision 15 bucketing and zero-fills types. | `ContentMetricsResult(Guid? SubjectId, int Subjects, int Units, int LessonsDraft, int LessonsPublished, int LessonsArchived, int QuestionsPending, int QuestionsApproved, int QuestionsRejected, int QuestionsRetired, List<QuestionTypeCountResult> QuestionsByType, int ServableTotal, DateTimeOffset GeneratedAt)`; `QuestionTypeCountResult(QuestionType Type, int Count)` |
| C4 | `GetSolveRateMetrics/` Query, Validator, Handler, `SolveRateMetricsResult.cs`, `SolveRateDayResult.cs`, `SolveRateMetricsResultGenerator.cs` | `GetSolveRateMetricsQuery(DateOnly? From, DateOnly? To, Guid? SubjectId)`, `"solve-rate"` | `(ISessionRepository, IUserActivityDayRepository, ISubjectRepository, TimeProvider, IOptions<DashboardOptions>)`: guard; window; `attempts = CountAttemptsByDayAsync(window.ToMetricsWindow(), SubjectId)`; `active = CountActiveStudentsByDayAsync(window.From, window.To)`; generator: per day `(attempts or 0, active or 0)`, totals, `Ratio(Attempts, ActiveStudentDays, PerStudentDecimals)`. | `SolveRateMetricsResult(DateOnly From, DateOnly To, Guid? SubjectId, long Attempts, long ActiveStudentDays, decimal? AttemptsPerActiveStudentPerDay, List<SolveRateDayResult> Daily, DateTimeOffset GeneratedAt)`; `SolveRateDayResult(DateOnly Date, long Attempts, long ActiveStudents)` |
| C5 | `GetSuccessRateMetrics/` Query, Validator, Handler, `SuccessRateMetricsResult.cs`, `SuccessRateGroupResult.cs`, `SuccessRateMetricsResultGenerator.cs` | `GetSuccessRateMetricsQuery(DateOnly? From, DateOnly? To, Guid? SubjectId)`, `"success-rate"` | `(ISessionRepository, ISubjectRepository, ICurriculumUnitRepository, ILessonRepository, TimeProvider, IOptions<DashboardOptions>, IOptions<MasteryOptions>)`: guard; window; `outcomes = GetAttemptOutcomesByLessonAsync(window.Start, window.End, SubjectId, masteryOptions.Value.CorrectThreshold)`; distinct id sets; `subjects/units/lessons = FindAsync(x => ids.Contains(x.Id), ct, asNoTracking: true)` (one call each; skipped when the set is empty); generator per Decision 14 (a missing name → `string.Empty`). | `SuccessRateMetricsResult(DateOnly From, DateOnly To, Guid? SubjectId, int Attempts, int Correct, decimal? Rate, List<SuccessRateGroupResult> BySubject, List<SuccessRateGroupResult> ByUnit, List<SuccessRateGroupResult> ByLesson, DateTimeOffset GeneratedAt)`; `SuccessRateGroupResult(Guid Id, string Name, Guid? ParentId, int Attempts, int Correct, decimal? Rate)` |
| C6 | `GetValidationMetrics/` Query, Validator, Handler, `ValidationMetricsResult.cs`, `TeacherThroughputResult.cs` | `GetValidationMetricsQuery(DateOnly? From, DateOnly? To, Guid? SubjectId)`, `"validation"` | `(IQuestionRepository, IUserRepository, ISubjectRepository, TimeProvider, IOptions<DashboardOptions>)`: guard; window; `backlog = questionRepository.CountAsync(ct, x => x.ValidationStatus == Pending && x.RetiredAt == null && (request.SubjectId == null \|\| x.SubjectId == request.SubjectId))`; `stats = GetDecisionStatsAsync(Start, End, SubjectId, null)`; `byTeacher = CountDecisionsByTeacherAsync(...)`; `teachers = userRepository.FindAsync(x => ids.Contains(x.Id), ct, asNoTracking: true)`, skipped when empty; `daily = CountDecisionsByDayAsync(window.ToMetricsWindow(), SubjectId)`. `ByTeacher` is ordered by `Approved + Rejected` desc, then `DisplayName` ordinal. | `ValidationMetricsResult(DateOnly From, DateOnly To, Guid? SubjectId, int PendingBacklog, int Approved, int Rejected, long? MedianSecondsToDecision, List<TeacherThroughputResult> ByTeacher, List<DailyValueResult> DailyDecisions, DateTimeOffset GeneratedAt)`; `TeacherThroughputResult(Guid TeacherId, string DisplayName, int Approved, int Rejected)` |
| C7 | `GetAskTeacherMetrics/` Query, Validator, Handler, `AskTeacherMetricsResult.cs` | `GetAskTeacherMetricsQuery(DateOnly? From, DateOnly? To, Guid? SubjectId)`, `"ask-teacher"` | `(ITeacherThreadRepository, ITeacherThreadSlaEventRepository, ISubjectRepository, TimeProvider, IOptions<DashboardOptions>, IOptions<SubscriptionsOptions>)`: guard; window; three `CountAsync` calls per Decision 18 (subject predicate as in C6; overdue uses `window.Now`); `breaches = CountBreachesAsync(Start, End, SubjectId)`; `replies = GetReplyStatsAsync(Start, End, SubjectId, null, TimeSpan.FromHours(AskTeacherReplySlaHours))`; rate `Ratio(RepliedWithinSla, Replies, RateDecimals)`; median `Seconds(...)`. | `AskTeacherMetricsResult(DateOnly From, DateOnly To, Guid? SubjectId, int OpenThreads, int AwaitingReply, int OverdueNow, int SlaBreaches, int Replies, int RepliedWithinSla, decimal? SlaComplianceRate, long? MedianReplySeconds, DateTimeOffset GeneratedAt)` |
| C8 | `GetPaymentMetrics/` Query, Validator, Handler, `PaymentMetricsResult.cs` | `GetPaymentMetricsQuery(DateOnly? From, DateOnly? To)`, `"payments"` | `(IPaymentRepository, TimeProvider, IOptions<DashboardOptions>, IOptions<SubscriptionsOptions>)`: window; `totals = GetTotalsAsync(Start, End)`; `daily = GetRevenueByDayAsync(window.ToMetricsWindow())`; build `Money` with `Currency`. `NetRevenue = GrossMinor - RefundedMinor`. | `PaymentMetricsResult(DateOnly From, DateOnly To, int Succeeded, int Failed, int Refunds, Money Revenue, Money Refunded, Money NetRevenue, List<DailyValueResult> RevenueByDay, DateTimeOffset GeneratedAt)` |
| C9 | `GetFunnelMetrics/` Query, Validator, Handler, `FunnelMetricsResult.cs`, `FunnelStepResult.cs`, `FunnelMetricsResultGenerator.cs` | `GetFunnelMetricsQuery(DateOnly? From, DateOnly? To)`, `"funnel"` | `(IFunnelEventRepository, TimeProvider, IOptions<DashboardOptions>)`: window; `steps = CountVisitorsByStepAsync(Start, End)`; `timing = GetLandingToFirstAnswerTimingAsync(Start, End)`; the generator walks `Enum.GetValues<FunnelEventType>()` in order, zero-filling visitors, with conversion `index == 0 ? null : Ratio(visitors, previous, RateDecimals)`. | `FunnelMetricsResult(DateOnly From, DateOnly To, List<FunnelStepResult> Steps, int CompletedJourneys, long? MedianLandingToFirstAnswerSeconds, DateTimeOffset GeneratedAt)`; `FunnelStepResult(FunnelEventType Type, int Visitors, decimal? ConversionFromPrevious)` |

Handlers take no `ICurrentUserService`: the policy is enforced at the controller, and these are pure reads with no per-user data.

### API
| # | Path | Contract |
|---|------|----------|
| P1 | `api/Elmanhg.Api/Controllers/Dashboard/DashboardController.cs` | `[ApiController] [Route("api/dashboard")] [Authorize] public class DashboardController(IMediator mediator) : ControllerBase`. It has 9 thin actions (table below), each with `[Authorize(Policy = DefaultCodes.DashboardsView)]` and `[ProducesResponseType<TResult>(StatusCodes.Status200OK)]`. Parameters are `[FromQuery] DateOnly? from, [FromQuery] DateOnly? to` (plus `[FromQuery] Guid? subjectId` where listed) and `CancellationToken cancellationToken`. Each action sends the query and returns `Ok(result)`. |

### Docs
| # | Path | Content |
|---|------|---------|
| X1 | `docs/dashboard.md` | Sections: **Filters** (Cairo days, inclusive, defaults, 366-day max, which cards take `subjectId`, the 422 and 404 codes); **Cards** (one table per card: every field and its exact definition, from Decisions 8, 12–21); **Activity tracking** (the `UserActivityDays` table, what writes a row, one row per user per day, students-only counting); **Caching** (60 s `IMemoryCache`, no invalidation, `Dashboard:CacheSeconds`, `0` disables); **Endpoints** (the API surface table); **Config** (`Dashboard:*` keys and defaults). |

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `DashboardDateRangeInvalid` | `DASHBOARD_DATE_RANGE_INVALID` | `DashboardFilterRules` (all 8 range validators) | `ValidationBehaviourException` (pipeline) | 422 |
| `DashboardDateRangeTooWide` | `DASHBOARD_DATE_RANGE_TOO_WIDE` | `DashboardFilterRules` | `ValidationBehaviourException` | 422 |
| `SubjectNotFound` (existing) | `SUBJECT_NOT_FOUND` | `DashboardSubjectGuard` | `NotFoundCoreException` | 404 |

| Key | ar | en |
|---|---|---|
| `DASHBOARD_DATE_RANGE_INVALID` | يجب أن يكون تاريخ النهاية في نفس يوم تاريخ البداية أو بعده. | The end date must be on or after the start date. |
| `DASHBOARD_DATE_RANGE_TOO_WIDE` | الفترة الزمنية أطول من الحد المسموح. | The date range is longer than allowed. |

## Domain behaviour
- `UserActivityDay.Record(userId, day, seenAt)` is a pure factory: new `Guid` id, fields assigned, no guards, and no `UpdationDate` (it inherits `Entity`, not `AuditEntity`). There are no mutators; rows are never updated.
- `QuestionDecision.Create(..., Guid decidedBy, DateTimeOffset submittedAt, DateTimeOffset decidedAt)` assigns `SubmittedAt = submittedAt`. `Question.Approve` and `Question.Reject` pass `SubmittedAt` before any mutation; their guards, state changes and `UpdationDate` stamping are unchanged.
- `SubscriptionChurnSpecification.ChurnedBetween` is pure and EF-translatable.
- No `BusinessRuleViolationException` is added.

## API surface
| Method | Route | Policy | Query params | Response |
|---|---|---|---|---|
| GET | `/api/dashboard/students` (Name `GetDashboardStudents`) | `DefaultCodes.DashboardsView` | from, to | `StudentMetricsResult` |
| GET | `/api/dashboard/subscribers` (`GetDashboardSubscribers`) | same | from, to | `SubscriberMetricsResult` |
| GET | `/api/dashboard/content` (`GetDashboardContent`) | same | subjectId | `ContentMetricsResult` |
| GET | `/api/dashboard/solve-rate` (`GetDashboardSolveRate`) | same | from, to, subjectId | `SolveRateMetricsResult` |
| GET | `/api/dashboard/success-rate` (`GetDashboardSuccessRate`) | same | from, to, subjectId | `SuccessRateMetricsResult` |
| GET | `/api/dashboard/validation` (`GetDashboardValidation`) | same | from, to, subjectId | `ValidationMetricsResult` |
| GET | `/api/dashboard/ask-teacher` (`GetDashboardAskTeacher`) | same | from, to, subjectId | `AskTeacherMetricsResult` |
| GET | `/api/dashboard/payments` (`GetDashboardPayments`) | same | from, to | `PaymentMetricsResult` |
| GET | `/api/dashboard/funnel` (`GetDashboardFunnel`) | same | from, to | `FunnelMetricsResult` |

The API uses no request records: every input is a query parameter.

## Test plan
The unit-test clock is `Substitute.For<TimeProvider>()` with `GetUtcNow()` returning `2026-01-15T10:00:00Z` (Cairo 12:00, UTC+2, so Today = 2026-01-15), unless stated otherwise. Options come from `Options.Create(new DashboardOptions())` (defaults). Query handlers call no `SaveChangesAsync`; each success test asserts the result fields.

| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 1 | `Tests/Domain/Analytics/UserActivityDayTests` | `Record_SetsUserDayAndFirstSeenAt` | UserId, Day and FirstSeenAt equal the inputs; Id is not empty. |
| 2 | `Tests/Domain/Subscriptions/SubscriptionChurnSpecificationTests` | `ChurnedBetween_CancelledInWindow_Matches`, `ChurnedBetween_ExpiredWithoutCancelInWindow_Matches`, `ChurnedBetween_CancelledBeforeWindowExpiredInside_DoesNotMatch`, `ChurnedBetween_ActiveSubscription_DoesNotMatch` | Compiled expression over `SubscriptionBuilder` subscriptions, using `Cancel`/`Expire`. |
| 3 | `Tests/Domain/Questions/QuestionApprovalTests` | `Approve_Pending_RecordsSubmittedAtOnDecision` | `Decisions.Single().SubmittedAt == question.SubmittedAt`. |
| 4 | `Tests/Domain/Questions/QuestionRejectionTests` | `Reject_Pending_RecordsSubmittedAtOnDecision` | Same, for Reject. |
| 5 | `Tests/Application/Features/Dashboard/Shared/DashboardWindowTests` | `Resolve_NoDates_EndsTodayAndSpansDefaultDays` | From 2025-12-17, To 2026-01-15, DayCount 30. |
| 6 | 〃 | `Resolve_OnlyFrom_EndsToday` | To = 2026-01-15. |
| 7 | 〃 | `Resolve_OnlyTo_StartsDefaultDaysBeforeTo` | From = To − 29. |
| 8 | 〃 | `Resolve_WinterDay_StartAndEndAreCairoMidnightsInUtc` | From = To = 2026-01-15 → Start 2026-01-14T22:00Z, End 2026-01-15T22:00Z. |
| 9 | 〃 | `LocalDay_LateUtcEvening_IsNextCairoDay` | 2026-01-14T23:00Z → 2026-01-15. |
| 10 | 〃 | `StartOfDay_CairoSpringForwardDay_DoesNotThrow` | `StartOfDay(2026-04-24)` < `StartOfDay(2026-04-25)`, with no exception. |
| 11 | 〃 | `Days_EnumeratesEveryDayInclusive` | 3-day window → 3 consecutive dates. |
| 12 | `Tests/Application/Features/Dashboard/Shared/DashboardFilterRulesTests` (via `GetStudentMetricsValidator`) | `Validate_NoDates_Passes` | Valid. |
| 13 | 〃 | `Validate_FromAfterTo_FailsWithDashboardDateRangeInvalid` | Error code. |
| 14 | 〃 | `Validate_FromAfterTodayWithoutTo_FailsWithDashboardDateRangeInvalid` | Error code. |
| 15 | 〃 | `Validate_RangeOfMaxDays_Passes` | 366-day range is valid. |
| 16 | 〃 | `Validate_RangeLongerThanMax_FailsWithDashboardDateRangeTooWide` | 367 days → code, and not `…INVALID`. |
| 17 | 〃 | `Validate_EveryRangeValidator_FromAfterTo_FailsWithDashboardDateRangeInvalid` | `[Theory]` + `MemberData` over the 8 validators paired with a from > to query each; `validator.Validate(new ValidationContext<object>(query))` has the code. |
| 18 | `Tests/Application/Features/Dashboard/Shared/DashboardCacheBehaviourTests` (real `MemoryCache`; probe records `CachedProbeQuery : IRequest<string>, IDashboardQuery` and `PlainProbeRequest : IRequest<string>` in the test file) | `Handle_ColdCache_CallsNextAndCachesResult` | next called once; the cache holds the value under `CacheKey`. |
| 19 | 〃 | `Handle_WarmCache_ReturnsCachedWithoutCallingNext` | Second call returns the first value; next called once in total. |
| 20 | 〃 | `Handle_DifferentCacheKey_CallsNextAgain` | next called twice. |
| 21 | 〃 | `Handle_CacheSecondsZero_AlwaysCallsNext` | next called twice; cache empty. |
| 22 | 〃 | `Handle_NonDashboardRequest_PassesThroughUncached` | next called twice. |
| 23 | `Tests/Application/Features/Dashboard/Shared/DashboardRatesTests` | `Ratio_ZeroDenominator_ReturnsNull`, `Ratio_RoundsAwayFromZero` (1/8 at 2 dp → 0.13), `Seconds_Null_ReturnsNull`, `Seconds_RoundsToWholeSeconds` (209.5 → 210) | Values. |
| 24 | `Tests/Application/Features/Shared/Analytics/UserActivityBehaviourTests` (substitute repo and user service, real `MemoryCache`) | `Handle_AuthenticatedRequest_RecordsCairoTodayForUser` | `AddIfAbsentAsync` received once with the user id and Day 2026-01-15; response returned. |
| 25 | 〃 | `Handle_SameUserSameDayTwice_RecordsOnce` | Received(1). |
| 26 | 〃 | `Handle_SameUserNextDay_RecordsAgain` | Clock moves +1 day → Received(2), with different days. |
| 27 | 〃 | `Handle_AnonymousNonAuthResponse_RecordsNothing` | DidNotReceive. |
| 28 | 〃 | `Handle_AnonymousAuthResultResponse_RecordsAuthResultUser` | Received with `AuthResult.User.Id`. |
| 29 | 〃 | `Handle_NextThrows_RecordsNothingAndRethrows` | Exception propagates; DidNotReceive. |
| 30 | `Tests/Application/Features/Dashboard/GetStudentMetrics/GetStudentMetricsHandlerTests` | `Handle_ReturnsRepositoryCountsForTheirWindows` | ActiveToday comes from the (2026-01-15, 2026-01-15) call and ActiveThisMonth from (2025-12-17, 2026-01-15); Total, NewInRange and NewThisWeek mapped (distinct substitute returns keyed on the predicate call order); From/To echoed; GeneratedAt = now. |
| 31 | 〃 | `Handle_ZeroFillsDailyActiveForEveryDay` | Range of 3 days with 1 total → 3 entries, 2 of them 0. |
| 32 | `…/GetSubscriberMetrics/GetSubscriberMetricsHandlerTests` | `Handle_ZeroFillsPlansWithoutActiveSubscriptions` | Only Base returned → [Base n, AskTeacher 0]; ActiveSubscriptions = n. |
| 33 | 〃 | `Handle_ReturnsMrrInConfiguredCurrency` | `Money(12345, "EGP")`. |
| 34 | 〃 | `Handle_ReturnsChurnInRangeAndThisMonth` | Two `CountAsync` results mapped to the right fields. |
| 35 | `…/GetContentMetrics/GetContentMetricsHandlerTests` | `Handle_UnknownSubject_ThrowsSubjectNotFound` | `NotFoundCoreException` with `SUBJECT_NOT_FOUND`; the question repo is not called. |
| 36 | 〃 | `Handle_SplitsRetiredOutOfTheirStatus` | Inventory [(Approved, Mcq, retired, 2), (Approved, Mcq, not retired, 3), (Pending, Fill, not retired, 1)] → Approved 3, Retired 2, Pending 1, ByType Mcq 5 and Fill 1, every other type 0. |
| 37 | 〃 | `Handle_NoSubject_UsesGlobalServableCountAndSubjectTotal` | `CountServableAsync` used; Subjects = repo count. |
| 38 | 〃 | `Handle_WithSubject_UsesSubjectServableCountAndOneSubject` | `CountServableInSubjectAsync(id)` used; Subjects = 1. |
| 39 | 〃 | `Handle_MapsLessonStates` | Draft, Published and Archived counts. |
| 40 | `…/GetSolveRateMetrics/GetSolveRateMetricsHandlerTests` | `Handle_UnknownSubject_ThrowsSubjectNotFound` | Code. |
| 41 | 〃 | `Handle_DividesAttemptsByActiveStudentDays` | 50 attempts over 2 days with 3 + 4 active → 7.14. |
| 42 | 〃 | `Handle_NoActiveStudents_ReturnsNullRate` | null. |
| 43 | 〃 | `Handle_ZeroFillsDailyAttemptsAndActive` | Missing days → 0/0. |
| 44 | `…/GetSuccessRateMetrics/GetSuccessRateMetricsHandlerTests` | `Handle_UnknownSubject_ThrowsSubjectNotFound` | Code. |
| 45 | 〃 | `Handle_RollsLessonsUpToUnitsSubjectsAndOverall` | 2 lessons in 1 unit (6/10 and 2/10) → unit 8/20 at 0.4, subject 0.4, overall 0.4; lesson ParentId = unit id; unit ParentId = subject id; names mapped. |
| 46 | 〃 | `Handle_OrdersGroupsByCurriculumOrder` | Lessons returned in Order ascending. |
| 47 | 〃 | `Handle_PassesMasteryCorrectThreshold` | Repo received threshold 0.8. |
| 48 | 〃 | `Handle_NoAttempts_ReturnsNullRateAndEmptyGroups` | Rate null, lists empty, subject/unit/lesson repos not called. |
| 49 | `…/GetValidationMetrics/GetValidationMetricsHandlerTests` | `Handle_UnknownSubject_ThrowsSubjectNotFound` | Code. |
| 50 | 〃 | `Handle_ReturnsBacklogAndRoundedMedian` | Backlog mapped; median 7199.6 → 7200. |
| 51 | 〃 | `Handle_NamesTeachersAndOrdersByThroughput` | 2 teachers → the higher total first, display names from users. |
| 52 | 〃 | `Handle_ZeroFillsDailyDecisions` | Missing days → 0. |
| 53 | `…/GetAskTeacherMetrics/GetAskTeacherMetricsHandlerTests` | `Handle_UnknownSubject_ThrowsSubjectNotFound` | Code. |
| 54 | 〃 | `Handle_ReturnsCountsAndComplianceRate` | 19/20 → 0.95; the counts map to the right fields. |
| 55 | 〃 | `Handle_NoReplies_ReturnsNullComplianceAndMedian` | Both null. |
| 56 | 〃 | `Handle_PassesConfiguredReplySla` | `GetReplyStatsAsync` received `TimeSpan.FromHours(24)`. |
| 57 | `…/GetPaymentMetrics/GetPaymentMetricsHandlerTests` | `Handle_ComputesNetRevenueInConfiguredCurrency` | Gross 89800, refunded 69900 → Net 19900 EGP; counts mapped. |
| 58 | 〃 | `Handle_ZeroFillsRevenueByDay` | Missing days → 0. |
| 59 | `…/GetFunnelMetrics/GetFunnelMetricsHandlerTests` | `Handle_OrdersStepsAndComputesConversionFromPrevious` | 5 steps in enum order; the first conversion is null; 4/8 → 0.5. |
| 60 | 〃 | `Handle_PreviousStepZero_ReturnsNullConversion` | null. |
| 61 | 〃 | `Handle_ZeroFillsMissingSteps` | Absent types have Visitors 0. |
| 62 | 〃 | `Handle_MapsTimingRoundedToSeconds` | Completed and median (209.5 → 210). |
| 63 | `Tests/Integration/Persistence/UserActivityDayPersistenceTests` | `AddIfAbsent_SameUserSameDayTwice_KeepsOneRowWithFirstSeenAt` | 1 row; FirstSeenAt = the first value. |
| 64 | `Tests/Integration/Dashboard/DashboardAccessEndpointTests` | `Get_Anonymous_Returns401` | `[Theory]` over all 9 routes → 401. |
| 65 | 〃 | `Get_NonAdmin_Returns403` | `[Theory]` Student and Teacher on `/students` → 403. |
| 66 | 〃 | `Get_Admin_Returns200ForEveryCard` | `[Theory]` over 9 routes → 200 and `generatedAt` present. |
| 67 | 〃 | `Get_FromAfterTo_Returns422DashboardDateRangeInvalid` | 422 and `code`. |
| 68 | 〃 | `Get_RangeTooWide_Returns422DashboardDateRangeTooWide` | 422 and `code`. |
| 69 | 〃 | `Get_UnknownSubject_Returns404SubjectNotFound` | `/content?subjectId=<new guid>` → 404 and `code`. |
| 70 | `Tests/Integration/Dashboard/UserActivityTrackingTests` | `Login_RecordsActivityForCairoToday` | After an email login through `/api/auth`, DB has exactly 1 `UserActivityDays` row for the user with Day = Cairo today. |
| 71 | 〃 | `AuthenticatedRequests_RecordOneRowPerDay` | Signed-in student makes 2 GET requests → still 1 row. |
| 72 | `Tests/Integration/Dashboard/StudentMetricsEndpointTests` | `Get_SeededActivityDay_CountsStudentButNotTeacher` | Activity rows seeded on 2021-02-03 for a student and a teacher → `?from=2021-02-03&to=2021-02-03` → `dailyActive[0].value == 1`. |
| 73 | 〃 | `Get_StudentCreatedOnSeededDay_CountsInNewInRange` | Student `CreationDate` set to 2021-03-10T10:00Z → `newInRange == 1` for that day. |
| 74 | `Tests/Integration/Dashboard/SubscriberMetricsEndpointTests` | `Get_ActiveTermlySubscription_ContributesMonthlyRevenue` | After seeding an active Termly subscription with a succeeded 69900 payment: both plans present, `monthlyRecurringRevenue.currency == "EGP"`, `amountMinor >= 17475`. |
| 75 | `Tests/Integration/Dashboard/ContentMetricsEndpointTests` | `Get_SubjectFilter_CountsLessonsQuestionsAndServable` | Fresh subject with a published lesson (approved, pending and retired questions) and a draft lesson → subjects 1, units 1, lessonsPublished 1, lessonsDraft 1, approved 1, pending 1, retired 1, servableTotal 1. |
| 76 | `Tests/Integration/Dashboard/LearningMetricsEndpointTests` | `Get_SuccessRate_SubjectFilter_CountsCorrectOverAttempts` | `SeedServableLessonAsync(factory, 2)`; the student answers one "b" and one "a" → attempts 2, correct 1, rate 0.5, byLesson single with the lesson id. |
| 77 | 〃 | `Get_SuccessRate_AdminTestModeAttempts_AreExcluded` | Admin quiz and answer on the same lesson → attempts still come only from the student (2). |
| 78 | 〃 | `Get_SolveRate_SubjectFilter_CountsTodaysAttempts` | Same flow → attempts 2; today's daily attempts 2; `activeStudentDays >= 1`. |
| 79 | `Tests/Integration/Dashboard/ValidationMetricsEndpointTests` | `Get_SubjectFilter_ReturnsBacklogDecisionsAndTeacherThroughput` | Fresh subject, assigned teacher and 3 pending questions; teacher approves one and rejects one through `/api/validation-queue` → pendingBacklog 1, approved 1, rejected 1, byTeacher one row with the teacher's display name (1/1), `medianSecondsToDecision` not null. |
| 80 | `Tests/Integration/Dashboard/AskTeacherMetricsEndpointTests` | `Get_SubjectFilter_CountsOpenOverdueAndBreaches` | 2 threads seeded 30 h ago plus 1 Breach event seeded now → open 2, awaiting 2, overdue 2, breaches 1, replies 0, compliance null. |
| 81 | 〃 | `Get_SubjectFilter_ReplyWithinSla_CountsCompliance` | Thread seeded 1 h ago; assigned teacher claims and replies through `/api/teacher-inbox` → replies 1, repliedWithinSla 1, slaComplianceRate 1.0, medianReplySeconds > 0. |
| 82 | `Tests/Integration/Dashboard/PaymentMetricsEndpointTests` | `Get_SeededDay_SumsRevenueFailuresAndRefunds` | On 2021-05-05 (UTC 10:00): succeeded 19900; failed; succeeded-then-refunded 69900 → succeeded 2, failed 1, refunds 1, revenue 89800, refunded 69900, netRevenue 19900, `revenueByDay` a single entry of 89800. |
| 83 | `Tests/Integration/Dashboard/FunnelMetricsEndpointTests` | `Get_SeededDay_CountsDistinctVisitorsAndMedian` | 2021-06-06: visitor A lands twice then completes all steps with the answer at +120 s; B lands, starts sign-up and answers at +300 s; C lands only → steps [3, 2, 1, 1, 2], completedJourneys 2, median 210. |

Integration helper (new, not a test): `api/Elmanhg.Tests/Integration/Dashboard/DashboardTestData.cs`. It is a static class with `Route = "/api/dashboard"`, `AdminClientAsync(factory)` (delegates to `ScopeTestData`), `GetJsonAsync(client, path)`, `SeedActivityAsync(factory, userId, DateOnly day)`, `SetCreationDateAsync(factory, userId, DateTimeOffset)`, `SeedFunnelEventAsync(factory, Guid anonymousId, FunnelEventType type, DateTimeOffset occurredAt)`, `SeedBreachAsync(factory, TeacherThread thread, DateTimeOffset occurredAt)` and `SeedPaymentAsync(factory, …)`. Seeding goes through domain factories and methods (`Payment.Create`/`MarkSucceeded`/`MarkFailed`/`MarkRefunded`, `FunnelEvent.Record`, `UserActivityDay.Record`, `TeacherThreadSlaEvent.Record`) in a fresh `AppDbContext` scope, and reuses `SessionTestData`, `ValidationTestData`, `TeacherThreadTestData`, `TeacherInboxTestData`, `ContentTestData`, `QuestionTestData`, `SubscriptionTestData` and `PaymentsTestData`. Tests on global cards use a unique past date per test, and no two tests share a date.

## Definition of done
- [ ] All 9 routes exist, each has `[Authorize(Policy = DefaultCodes.DashboardsView)]`, and `PermissionMatrixPolicyTests` passes.
- [ ] Every card field matches its definition in Decisions 8, 12–21 and in `docs/dashboard.md`.
- [ ] Day boundaries use `Dashboard:TimeZone`, and `StartOfDay` never throws in the DST gap.
- [ ] Range validation returns 422 with the two new codes; an unknown subject returns 404 `SUBJECT_NOT_FOUND`; both codes exist in both resx files.
- [ ] `UserActivityBehaviour` records one row per user per Cairo day on login, register, refresh and authenticated requests, and DAU/MAU count students only.
- [ ] The activity insert is `ON CONFLICT DO NOTHING` through parameterised `ExecuteSqlAsync`; no `*Raw` SQL anywhere.
- [ ] `DashboardCacheBehaviour` caches `IDashboardQuery` results for `CacheSeconds`, and 0 disables it. The code default is 60; `ApiFactory` sets 0.
- [ ] `QuestionDecision.SubmittedAt` is set on approve and reject, backfilled in migration `AddDashboardMetrics`, and non-nullable.
- [ ] The migration adds `UserActivityDays` and the 6 indexes, contains no unplanned drop or rename, and is appended to `AppDbContextTests`.
- [ ] Solve and success rate exclude test-mode sessions and soft-deleted questions; correct uses `Mastery:CorrectThreshold`.
- [ ] Servable total goes through `ServableQuestionSpecification` (`WhereServable`) only.
- [ ] All aggregation runs in SQL, with no `FindAsync(x => true)` in memory; reads are `AsNoTracking`; there is no N+1.
- [ ] `DashboardOptions` is validated on start (time zone, `DefaultRangeDays <= MaxRangeDays`) and present in `appsettings.example.json` and `ApiFactory`.
- [ ] Every file listed here exists and there are no others; every file is ≤ ~100 lines with a file-scoped namespace; no `DateTime.Now/UtcNow` and no `.Result`.
- [ ] All 83 test rows are implemented; `dotnet test api/ -c Release` is green with `appsettings.json` moved aside.
- [ ] `api/openapi/v1.json` and `web/src/shared/api/generated` are regenerated with no drift, and Postman has the `Dashboard` folder.
- [ ] Docs are updated: `docs/dashboard.md` (new), PRD §10.3 filter sentence and §15 `UserActivityDay` line, and `docs/analytics.md` read-side note.
- [ ] `dotnet format --verify-no-changes` exits 0, and the build has zero new warnings.
