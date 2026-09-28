# Plan — [E5.S4] Mastery calculation and headline counter (#77)

## Goal
After this ships, a signed-in student lands on `/student` and sees real, live numbers instead of a placeholder: "متبقّي لك X سؤال من Y" (servable total minus the questions they have mastered), how many questions they have seen and mastered, their day streak, a suggested next lesson with a "درّب الآن" link to practice, and one card per subject with a mastery bar. Every quiz answer now updates a materialised `QuestionMastery` row (a question counts as mastered when its two most recent attempts both score at least 0.8). The API also exposes per-subject mastery, broken down by unit and lesson and weighted by question count, for the browsing (#85) and progress (#78) stories.

## Scope
**In:**
- `QuestionMastery` aggregate, table, migration, and a backfill of existing attempts.
- Mastery is updated inside `SubmitAnswerHandler` on every new attempt outside test mode.
- Lesson, unit and subject mastery rollups, weighted by question count. They count servable questions only and are computed live.
- Headline counter (servable total, mastered, remaining, seen), streak, and the next recommended lesson.
- `GET /api/mastery/overview` and `GET /api/mastery/subjects/{subjectId}`.
- Retirement and unpublishing are reflected at read time. Integration tests prove it (see D6).
- Web student home page (`/student`): all its widgets, plus mastery invalidation after each quiz answer.
- Docs: new `docs/mastery.md`; updates to PRD §15, `docs/sessions.md`, `docs/audit-log.md` and `docs/backlog.json`.
- Postman, OpenAPI and the Orval client.

**Out (owned by later stories, per `PROGRESS.md` run order):**
- Subject, unit and lesson browsing pages and links from the subject cards (#85).
- Progress page, weak spots and history (#78).
- Plan line and free-tier lesson locking on Home (#99, #87).
- Admin view of a student's progress (#106).
- Exam attempts feeding mastery (E6 reuses the same `SubmitAnswer` path automatically).

**Deferred:** none. Nothing in this story needs credentials or an external service.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| D1 | Where is `QuestionMastery` updated? | Explicitly in `SubmitAnswerHandler`, in the same `SaveChangesAsync` as the attempt. No domain event. | `CoreDbContext.SaveChangesAsync` publishes events while it lazily enumerates `ChangeTracker`, so adding an entity from a handler is unsafe. One save keeps the attempt and its mastery atomic. |
| D2 | How is PRD §15 `last_two_json` stored? | Typed columns: `LatestAttemptId`, `LatestNormalisedScore`, `LatestAttemptedAt`, `PreviousAttemptId?`, `PreviousNormalisedScore?`, `PreviousAttemptedAt?`, plus `IsMastered`. `UpdationDate` is `updated_at`. PRD §15 is updated. | Queryable and type-safe, with no jsonb parsing for a fixed two-slot shape. Docs-sync requires the PRD change. |
| D3 | Do test-mode (admin) attempts count? | No. `session.IsTestMode` means no mastery write, and they are excluded from streak and backfill. | `docs/sessions.md` ("Test mode") already promises that mastery excludes test-mode sessions. |
| D4 | Replays, and attempts arriving out of order | Only a newly created attempt updates mastery (the handler checks `FindAttempt` before `RecordAttempt`). `QuestionMastery.Record` is idempotent on attempt id and orders by `AttemptedAt`: a newer attempt shifts latest to previous; an older one replaces previous if it is newer than it; otherwise nothing changes. | "Two most recent" is defined by attempt time, not commit order. |
| D5 | Which questions count in mastered, seen and percentages? | Only questions that are servable now (`ServableQuestionSpecification`), joined at read time: mastered ∩ servable, seen ∩ servable. | PRD §17 rule 1 ("derived, never stored") and the prototype `headline()`/`mastery()`, which filter with `servableWhere`. This keeps remaining = total − mastered ≥ 0. |
| D6 | Sub-task "Recalculation job when questions are retired or lessons unpublished" | No job and no stored aggregate. A `QuestionMastery` row depends only on attempts, which retirement and unpublishing do not change. Every aggregate joins the servable rule at read time, so a retire, unpublish or archive (and a later re-publish) shows on the next read. Integration tests `Get_MasteredQuestionRetired_…` and `Get_LessonUnpublished_…` prove it. The backlog task text is updated to say so. | A job would recompute nothing. Storing servability would break PRD §17 rule 1. |
| D7 | Where does the headline's servable total come from? | The sum of the same per-lesson grouped query that yields mastered and seen. It does not use the 60 s cached `questions:servable-count`. | This keeps total, mastered and remaining from one snapshot (never negative) and meets PRD §7.3 "both live". |
| D8 | Percentage format and weighting | `int` 0–100, `floor(mastered × 100 / servable)`, and 0 when servable = 0. Unit and subject use the pooled Σmastered / Σservable over their lessons. | "Weighted by question count" (PRD §7.3). Rounding down never shows 100 % before everything is mastered. |
| D9 | "Seen" | A servable question with a `QuestionMastery` row (at least one non-test attempt). | PRD §7.3: "Attempted at least once". The row is created on the first attempt. |
| D10 | Streak | The number of consecutive calendar days in `Progress:StreakTimeZone` (default `Africa/Cairo`) with at least one attempt in a non-test `Quiz` session. It counts back from today if today is active, otherwise from yesterday, and looks back at most `Progress:StreakMaxDays` (365). "Today" comes from an injected `TimeProvider` (`TimeProvider.System` registered). | PRD §7.6 and the prototype `streak()`. Egyptian students need Cairo days, not UTC. Tunables go in Options (skill §8.1). |
| D11 | Next recommended lesson | Among Published lessons with ≥ 1 servable question and mastered < servable: the lowest mastered/servable ratio. Ties go to the first in curriculum order (subject order, unit order, lesson order, then lesson id). Null when none qualifies. Free-tier locks are ignored until #87. | Prototype `vStudentHome`. |
| D12 | API shape and policy | `GET /api/mastery/overview` and `GET /api/mastery/subjects/{subjectId}`, both `DefaultCodes.ProgressViewOwn` (Students only, PRD §16 "View own progress"). Admins and teachers get 403. | The policy already exists in `PermissionMatrixPolicies`. Admin viewing is #106. |
| D13 | Concurrent updates of one mastery row | `QuestionMastery.Version` is an xmin row version, with unique index `IX_QuestionMasteries_StudentId_QuestionId`. Both a concurrency conflict and a unique violation map to 409 `SESSION_MODIFIED_CONCURRENTLY` in `AppDbContext.SaveChangesAsync`. No new error code. | Same retry semantics as a session conflict, and the quiz UI already handles that code. |
| D14 | Attempts that already exist (#74–#76) | The migration backfills from `Attempts` joined to non-test `Sessions` with a hard-coded threshold of 0.8 (PRD §7.3). The SQL is a `public const string BackfillSql` on the migration class with `ON CONFLICT … DO NOTHING`. A later `Mastery:CorrectThreshold` change applies to new attempts only (documented). | Migrations cannot read config. Home numbers stay correct for existing dev data. |
| D15 | Home widgets owned by other stories | No plan line (E10). Subject cards are `<article>`s, not links, until #85 adds `/student/subject/:id`. | Incompleteness, not divergence (docs-sync rule). |
| D16 | Web feature placement, and the home page title | New feature `web/src/features/mastery/`. The page h1 is the greeting "Hello, {name}" / "أهلًا {name}" (prototype `<h2>أهلًا …`). The two `AppShell.test.tsx` assertions that looked for heading "Home" change to the greeting, and its `beforeEach` adds the mastery MSW mocks. | Prototype fidelity. The placeholder is intentionally replaced. |
| D17 | Mastery bar markup | Native `<progress max=100>` styled with token utilities and `::-webkit-progress-*` / `::-moz-progress-bar` variants, with no fill animation. | The design system forbids inline `style` widths. Native progress gives role, value and accessible name for free. |
| D18 | Button label | "درّب الآن" / "Train now". | `docs/claude-design-prompt.md` §4 wording, which wins over the prototype's "تدرّب الآن". |
| D19 | Subject detail contents | Every unit of the subject (order, then creation date) and the Published lessons of each (order, then creation date). A lesson without servable questions shows 0/0/0 %. | Students see Published lessons only (PRD §5.2, `PROGRESS.md`). |
| D20 | Auditing | `QuestionMastery` is not `IAuditedEntity`. `docs/audit-log.md` "Not audited" gains a line. | It is derived from the append-only attempt log. |
| D21 | Morabh reuse | Searched `D:\Personal\Projects\Projects\Morabh\repos\apis` for streak, mastery, `TimeZoneInfo` and `AtTimeZone`: no match. Every piece below is **new — no Morabh equivalent**. Internal patterns reused: `SessionRepositoryStub` (tests), `MasteryOptionsTests` (options tests), the `AppDbContext` conflict mapping, and the `SessionPersistenceTests` `SqlQuery` style. | Skill delta §5. |

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Domain/Sessions/ISessionRepository.cs` | Add `Task<List<DateOnly>> GetQuizActivityDaysAsync(Guid studentId, string timeZone, DateTimeOffset since, CancellationToken cancellationToken);` |
| `api/Elmanhg.Infrastructure/Sessions/SessionRepository.cs` | Implement it (SQL below). |
| `api/Elmanhg.Application/Sessions/SubmitAnswer/SubmitAnswerHandler.cs` | New constructor and mastery step (see Files #10a). |
| `api/Elmanhg.Application/DependencyInjection.cs` | `services.AddOptions<ProgressOptions>().BindConfiguration(ProgressOptions.SectionName).ValidateDataAnnotations().Validate(x => TimeZoneInfo.TryFindSystemTimeZoneById(x.StreakTimeZone, out _), "Progress:StreakTimeZone must be a known IANA time zone id.").ValidateOnStart();` and `services.AddSingleton(TimeProvider.System);` after the `MasteryOptions` line. |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | Add `public const string QuestionMasteryPerStudentIndex = "IX_QuestionMasteries_StudentId_QuestionId";`, `public DbSet<QuestionMastery> QuestionMasteries { get; set; }` and `ConfigureQuestionMastery(modelBuilder)` (called after `ConfigureSessions`). Add `modelBuilder.Entity<QuestionMastery>().HasQueryFilter(x => !x.IsDeleted);` to the global filter method. In `SaveChangesAsync`: change the concurrency catch filter to `exception.Entries.Any(x => x.Entity is Session or QuestionMastery)`, and add `catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: QuestionMasteryPerStudentIndex })` that throws `new ConflictCoreException(ErrorCodes.SessionModifiedConcurrently, innerException: exception)`. |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | `services.AddScoped<IQuestionMasteryRepository, QuestionMasteryRepository>();` after `ISessionRepository`. |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated by `dotnet ef migrations add AddQuestionMastery`. |
| `api/Elmanhg.Api/appsettings.example.json` | Add `"Progress": { "StreakTimeZone": "Africa/Cairo", "StreakMaxDays": 365 },` after `"Mastery"`. |
| `api/openapi/v1.json` | Regenerated by `dotnet build`. |
| `api/Elmanhg.Tests/Application/Features/Sessions/SubmitAnswer/SubmitAnswerHandlerTests.cs` | Constructor wiring for the new dependencies, plus 4 new tests (Test plan T24–T27). Existing tests are unchanged apart from the constructor call. |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | Append `sixteenth => sixteenth.Should().EndWith("_AddQuestionMastery")` to the migration list (accepted pattern). |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | Add `["Progress:StreakTimeZone"] = "Africa/Cairo", ["Progress:StreakMaxDays"] = "365",` after the `Mastery:CorrectThreshold` entry. |
| `postman/elmanhg.postman_collection.json` | New folder "Mastery" right after "Sessions" (see API surface). |
| `web/src/routes/student/index.tsx` | `component: StudentHomePage` imported from `@/features/mastery`, replacing `PlaceholderPage`. |
| `web/src/app/i18n.ts` | Import `masteryLocales` from `@/features/mastery/locales`, add `mastery: masteryLocales.ar/en` to resources and `'mastery'` to `ns`. |
| `web/src/features/quiz/hooks/useQuizAnswer.ts` | In `onSuccess`, after `setQueryData`, add `void invalidateMastery(queryClient);` (import from `@/features/mastery`). |
| `web/src/features/shell/components/AppShell.test.tsx` | Modify: `beforeEach` becomes `server.use(...getValidationQueueMock(), ...getMasteryMock());`. In "redirects a student who opens an admin page to the student home" and "has no axe violations", replace `{ name: 'Home' }` with `{ name: 'Hello, أحمد' }`. No other change. |
| `web/src/shared/api/generated/**` | Regenerated by `npm --prefix web run gen:api` (new `mastery/` folder, models, zod). |
| `docs/PRD.md` §15 | Replace the `QuestionMastery(...)` line with `QuestionMastery(student_id, question_id, mastered bool, latest_attempt_id, latest_normalised_score, latest_attempted_at, previous_attempt_id?, previous_normalised_score?, previous_attempted_at?, updated_at)  -- materialised from the two most recent attempts (docs/mastery.md)`. |
| `docs/sessions.md` | Lifecycle step 2: append "A new attempt in a non-test session also updates the student's `QuestionMastery` row in the same save (`docs/mastery.md`); a replayed answer does not." Options row `Mastery:CorrectThreshold`: change "used by selection and, later, mastery" to "used by selection and mastery (`docs/mastery.md`)". Error table `SESSION_MODIFIED_CONCURRENTLY`: append "or a concurrent answer updated the same question's mastery row". |
| `docs/audit-log.md` "Not audited" | Add: "- **Question mastery** (`QuestionMastery`): derived from the append-only attempt log (`docs/mastery.md`); not an `IAuditedEntity`." |
| `docs/backlog.json` (E5, "Mastery calculation and headline counter") | Replace the task string `"Recalculation job when questions are retired or lessons unpublished"` with `"Mastery follows retirement and unpublishing live: aggregates join the servable rule at read time, so no recalculation job is needed"`. |

## Files to create

### API — Domain (`api/Elmanhg.Domain/Mastery/`, namespace `Elmanhg.Domain.Mastery`)
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `MasteryAttempt.cs` | `sealed record` | `public sealed record MasteryAttempt(Guid AttemptId, decimal NormalisedScore, DateTimeOffset AttemptedAt) { public static MasteryAttempt From(Attempt attempt) => new(attempt.Id, attempt.NormalisedScore, attempt.CreatedAt); }` |
| 2 | `QuestionMastery.cs` | `public class QuestionMastery : AuditEntity` | Properties (all `{ get; private set; }`): `Guid StudentId`, `Guid QuestionId`, `bool IsMastered`, `Guid LatestAttemptId`, `decimal LatestNormalisedScore`, `DateTimeOffset LatestAttemptedAt`, `Guid? PreviousAttemptId`, `decimal? PreviousNormalisedScore`, `DateTimeOffset? PreviousAttemptedAt`, `uint Version`. `private QuestionMastery(Guid id, Guid? createdBy) : base(id, createdBy) { }`. `public static QuestionMastery Start(Guid studentId, Guid questionId, MasteryAttempt attempt)`. `public void Record(MasteryAttempt attempt, decimal correctThreshold)`. Bodies under Domain behaviour. |
| 3 | `IQuestionMasteryRepository.cs` | interface | `public interface IQuestionMasteryRepository : IRepository<QuestionMastery> { Task<List<LessonMasteryCount>> GetLessonCountsAsync(Guid studentId, Guid? subjectId, CancellationToken cancellationToken); }` |
| 4 | `LessonMasteryCount.cs` | `sealed record` | `public sealed record LessonMasteryCount(Guid SubjectId, int SubjectOrder, Guid UnitId, int UnitOrder, Guid LessonId, int LessonOrder, int ServableCount, int MasteredCount, int SeenCount)`. |
| 5 | `MasteryTotals.cs` | `sealed record` | `public sealed record MasteryTotals(int ServableCount, int MasteredCount, int SeenCount)` with `public int RemainingCount => ServableCount - MasteredCount;`, `public int MasteryPercent => ServableCount == 0 ? 0 : MasteredCount * 100 / ServableCount;` and `public static MasteryTotals Of(IEnumerable<LessonMasteryCount> lessons)`, which materialises once (`var list = lessons.ToList();`) and returns `new(list.Sum(x => x.ServableCount), list.Sum(x => x.MasteredCount), list.Sum(x => x.SeenCount))`. |
| 6 | `NextLessonRecommendation.cs` | `static class` | `public static LessonMasteryCount? Pick(IEnumerable<LessonMasteryCount> lessons)` returns `lessons.Where(x => x.ServableCount > 0 && x.MasteredCount < x.ServableCount).OrderBy(x => (decimal)x.MasteredCount / x.ServableCount).ThenBy(x => x.SubjectOrder).ThenBy(x => x.UnitOrder).ThenBy(x => x.LessonOrder).ThenBy(x => x.LessonId).FirstOrDefault()` (one LINQ operator per line). Class comment: `// PRD §7.3 / prototype vStudentHome: lowest mastery first, curriculum order breaks ties.` |
| 7 | `StudyStreak.cs` | `static class` | `public static int Count(IReadOnlyCollection<DateOnly> activeDays, DateOnly today)`: `var days = activeDays.ToHashSet(); var cursor = days.Contains(today) ? today : today.AddDays(-1); var count = 0; while (days.Contains(cursor)) { count++; cursor = cursor.AddDays(-1); } return count;` |

### API — Application
| # | Path | Type | Contract |
|---|------|------|----------|
| 8 | `api/Elmanhg.Application/Shared/Options/ProgressOptions.cs` | `sealed class` | Namespace `Elmanhg.Application.Shared.Options`. `public const string SectionName = "Progress";` `[Required] public string StreakTimeZone { get; set; } = "Africa/Cairo";` `[Range(1, 3650)] public int StreakMaxDays { get; set; } = 365;` |
| 9 | `api/Elmanhg.Application/Mastery/GetMasteryOverview/GetMasteryOverviewQuery.cs` | `sealed record` | `public sealed record GetMasteryOverviewQuery : IRequest<MasteryOverviewResult>;` (no validator: no input). |
| 10 | `api/Elmanhg.Application/Mastery/GetMasteryOverview/GetMasteryOverviewHandler.cs` | `sealed class` | `public sealed class GetMasteryOverviewHandler(IQuestionMasteryRepository questionMasteryRepository, ISessionRepository sessionRepository, ISubjectRepository subjectRepository, ILessonRepository lessonRepository, IOptions<ProgressOptions> progressOptions, TimeProvider timeProvider, ICurrentUserService currentUserService) : IRequestHandler<GetMasteryOverviewQuery, MasteryOverviewResult>`. `Handle` steps: (1) if `currentUserService.UserId` is null or default, throw `UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated)`; (2) `lessons = await questionMasteryRepository.GetLessonCountsAsync(userId, null, cancellationToken)`; (3) `subjects = await subjectRepository.GetAllAsync(cancellationToken, orderBy: query => query.OrderBy(x => x.Order).ThenBy(x => x.CreationDate), asNoTracking: true) ?? []`; (4) `var options = progressOptions.Value; var zone = TimeZoneInfo.FindSystemTimeZoneById(options.StreakTimeZone); var now = timeProvider.GetUtcNow(); var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);` (5) `activeDays = await sessionRepository.GetQuizActivityDaysAsync(userId, options.StreakTimeZone, now.AddDays(-options.StreakMaxDays), cancellationToken)`; (6) `next = NextLessonRecommendation.Pick(lessons)`; `nextLesson = next is null ? null : await lessonRepository.GetByIdAsync(next.LessonId, cancellationToken, asNoTracking: true)`; (7) `return MasteryOverviewResultGenerator.Generate(lessons, subjects, StudyStreak.Count(activeDays, today), next, nextLesson);`. Every await has `.ConfigureAwait(false)`. |
| 10a | (modify) `SubmitAnswerHandler.cs` | — | New constructor: `SubmitAnswerHandler(ISessionRepository sessionRepository, IQuestionRepository questionRepository, IQuestionMasteryRepository questionMasteryRepository, IOptions<MasteryOptions> masteryOptions, ICurrentUserService currentUserService, ILocalizer localizer)`. Replace the `session.RecordAttempt(...)` statement with: `var isNewAttempt = session.FindAttempt(item.QuestionId) is null; var attempt = session.RecordAttempt(item, …same args…); if (isNewAttempt && !session.IsTestMode) { await RecordMasteryAsync(userId, attempt, cancellationToken).ConfigureAwait(false); }`, followed by the existing single `SaveChangesAsync`. Private `async Task RecordMasteryAsync(Guid studentId, Attempt attempt, CancellationToken cancellationToken)`: `var masteryAttempt = MasteryAttempt.From(attempt); var mastery = await questionMasteryRepository.FirstOrDefaultAsync(x => x.StudentId == studentId && x.QuestionId == attempt.QuestionId, cancellationToken).ConfigureAwait(false); if (mastery is null) { await questionMasteryRepository.AddAsync(QuestionMastery.Start(studentId, attempt.QuestionId, masteryAttempt), cancellationToken).ConfigureAwait(false); return; } mastery.Record(masteryAttempt, masteryOptions.Value.CorrectThreshold);` (tracked load, not `asNoTracking`). |
| 11 | `api/Elmanhg.Application/Mastery/GetSubjectMastery/GetSubjectMasteryQuery.cs` | `sealed record` | `public sealed record GetSubjectMasteryQuery(Guid SubjectId) : IRequest<SubjectMasteryDetailResult>;` |
| 12 | `.../GetSubjectMastery/GetSubjectMasteryValidator.cs` | `sealed class` | `RuleFor(x => x.SubjectId).ValidateRequired(ErrorCodes.SubjectIdRequired);` (Core.Validation, existing code). |
| 13 | `.../GetSubjectMastery/GetSubjectMasteryHandler.cs` | `sealed class` | `public sealed class GetSubjectMasteryHandler(IQuestionMasteryRepository questionMasteryRepository, ISubjectRepository subjectRepository, ICurriculumUnitRepository unitRepository, ILessonRepository lessonRepository, ICurrentUserService currentUserService) : IRequestHandler<GetSubjectMasteryQuery, SubjectMasteryDetailResult>`. Steps: (1) user guard → `UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated)`; (2) `subject = await subjectRepository.GetByIdAsync(request.SubjectId, cancellationToken, asNoTracking: true)`, null → `NotFoundCoreException(ErrorCodes.SubjectNotFound)`; (3) `units = await unitRepository.FindAsync(x => x.SubjectId == subject.Id, cancellationToken, orderBy: query => query.OrderBy(x => x.Order).ThenBy(x => x.CreationDate), asNoTracking: true)`; (4) `var unitIds = units.Select(x => x.Id).ToList(); lessons = await lessonRepository.FindAsync(x => unitIds.Contains(x.UnitId) && x.State == LessonState.Published, cancellationToken, orderBy: query => query.OrderBy(x => x.Order).ThenBy(x => x.CreationDate), asNoTracking: true)`; (5) `counts = await questionMasteryRepository.GetLessonCountsAsync(userId, subject.Id, cancellationToken)`; (6) `return SubjectMasteryResultGenerator.Generate(subject, units, lessons, counts);` |
| 14 | `api/Elmanhg.Application/Mastery/Shared/MasteryOverviewResult.cs` | `sealed record` (client) | `public sealed record MasteryOverviewResult(MasteryHeadlineResult Headline, int StreakDays, NextLessonResult? NextLesson, List<SubjectMasteryResult> Subjects);` |
| 15 | `.../Shared/MasteryHeadlineResult.cs` | `sealed record` (client) | `(int ServableTotal, int MasteredCount, int RemainingCount, int SeenCount)` |
| 16 | `.../Shared/NextLessonResult.cs` | `sealed record` (client) | `(Guid LessonId, string LessonName, Guid SubjectId, string SubjectName, int MasteryPercent)` |
| 17 | `.../Shared/SubjectMasteryResult.cs` | `sealed record` (client) | `(Guid SubjectId, string Name, int ServableCount, int MasteredCount, int SeenCount, int MasteryPercent)` |
| 18 | `.../Shared/SubjectMasteryDetailResult.cs` | `sealed record` (client) | `(Guid SubjectId, string Name, int ServableCount, int MasteredCount, int SeenCount, int MasteryPercent, List<UnitMasteryResult> Units)` |
| 19 | `.../Shared/UnitMasteryResult.cs` | `sealed record` (client) | `(Guid UnitId, string Name, int ServableCount, int MasteredCount, int SeenCount, int MasteryPercent, List<LessonMasteryResult> Lessons)` |
| 20 | `.../Shared/LessonMasteryResult.cs` | `sealed record` (client) | `(Guid LessonId, string Name, int ServableCount, int MasteredCount, int SeenCount, int MasteryPercent)` |
| 21 | `.../Shared/MasteryOverviewResultGenerator.cs` | `static class` | `public static MasteryOverviewResult Generate(IReadOnlyCollection<LessonMasteryCount> lessons, IReadOnlyList<Subject> subjects, int streakDays, LessonMasteryCount? next, Lesson? nextLesson)`. Headline: `var totals = MasteryTotals.Of(lessons); new MasteryHeadlineResult(totals.ServableCount, totals.MasteredCount, totals.RemainingCount, totals.SeenCount)`. Subjects: for every subject in the given order, `MasteryTotals.Of(lessons.Where(x => x.SubjectId == subject.Id))` becomes `SubjectMasteryResult(subject.Id, subject.Name, t.ServableCount, t.MasteredCount, t.SeenCount, t.MasteryPercent)`. NextLesson: null when `next`, `nextLesson` or the subject with `next.SubjectId` is missing; otherwise `new NextLessonResult(nextLesson.Id, nextLesson.Name, subject.Id, subject.Name, MasteryTotals(next.ServableCount, next.MasteredCount, next.SeenCount).MasteryPercent)`. Names are plain `string` (no `LocalizedText` in these entities). |
| 22 | `.../Shared/SubjectMasteryResultGenerator.cs` | `static class` | `public static SubjectMasteryDetailResult Generate(Subject subject, IReadOnlyList<CurriculumUnit> units, IReadOnlyList<Lesson> lessons, IReadOnlyCollection<LessonMasteryCount> counts)`. Build `countsByLesson = counts.ToDictionary(x => x.LessonId)`. A lesson's totals are `countsByLesson.TryGetValue(lesson.Id, out var c) ? new MasteryTotals(c.ServableCount, c.MasteredCount, c.SeenCount) : new MasteryTotals(0, 0, 0)`. A unit's lessons are `lessons.Where(x => x.UnitId == unit.Id)` in the given order. Unit totals are `MasteryTotals.Of(counts.Where(x => x.UnitId == unit.Id))`. Subject totals are `MasteryTotals.Of(counts)`. |

### API — Infrastructure, migration, controller
| # | Path | Type | Contract |
|---|------|------|----------|
| 23 | `api/Elmanhg.Infrastructure/Mastery/QuestionMasteryRepository.cs` | `public class QuestionMasteryRepository(AppDbContext context) : Repository<QuestionMastery>(context), IQuestionMasteryRepository` | `GetLessonCountsAsync`: `var servable = _context.Set<Question>().WhereServable(_context.Set<Lesson>());` then the query-syntax chain `from question in servable join lesson in _context.Set<Lesson>() on question.LessonId equals lesson.Id join unit in _context.Set<CurriculumUnit>() on lesson.UnitId equals unit.Id join subject in _context.Set<Subject>() on unit.SubjectId equals subject.Id where subjectId == null \|\| unit.SubjectId == subjectId from mastery in _dbSet.Where(x => x.StudentId == studentId && x.QuestionId == question.Id).DefaultIfEmpty() select new { unit.SubjectId, SubjectOrder = subject.Order, lesson.UnitId, UnitOrder = unit.Order, LessonId = lesson.Id, LessonOrder = lesson.Order, Seen = mastery != null ? 1 : 0, Mastered = mastery != null && mastery.IsMastered ? 1 : 0 }`, then `.GroupBy(x => new { x.SubjectId, x.SubjectOrder, x.UnitId, x.UnitOrder, x.LessonId, x.LessonOrder }).Select(x => new LessonMasteryCount(x.Key.SubjectId, x.Key.SubjectOrder, x.Key.UnitId, x.Key.UnitOrder, x.Key.LessonId, x.Key.LessonOrder, x.Count(), x.Sum(row => row.Mastered), x.Sum(row => row.Seen))).AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false)`. |
| 23a | (modify) `SessionRepository.GetQuizActivityDaysAsync` | — | `var kind = nameof(SessionKind.Quiz); return await _context.Database.SqlQuery<DateOnly>($"""SELECT DISTINCT (a."CreatedAt" AT TIME ZONE {timeZone})::date AS "Value" FROM "Attempts" AS a INNER JOIN "Sessions" AS s ON s."Id" = a."SessionId" WHERE a."StudentId" = {studentId} AND a."CreatedAt" >= {since} AND a."IsDeleted" = false AND s."IsDeleted" = false AND s."IsTestMode" = false AND s."Kind" = {kind}""").ToListAsync(cancellationToken).ConfigureAwait(false);` It is interpolated `FormattableString`, so it is parameterised. Never `SqlQueryRaw`. |
| 24 | `api/Elmanhg.Infrastructure/Migrations/<timestamp>_AddQuestionMastery.cs` | EF migration | Generate with `dotnet ef migrations add AddQuestionMastery -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`. Review it: `CreateTable("QuestionMasteries")` with FKs to `AspNetUsers`/`Users` and `Questions` (restrict), and the unique index `IX_QuestionMasteries_StudentId_QuestionId`. No `Drop*`. Add `public const string BackfillSql = """…""";` (below) and `migrationBuilder.Sql(BackfillSql);` as the last statement of `Up`. `Down`: the generated `DropTable` only. |
| 25 | `…/<timestamp>_AddQuestionMastery.Designer.cs` | generated | Do not edit. |
| 26 | `api/Elmanhg.Api/Controllers/Mastery/MasteryController.cs` | controller | Namespace `Elmanhg.Api.Controllers.Mastery`. `[ApiController] [Route("api/mastery")] [Authorize] public class MasteryController(IMediator mediator) : ControllerBase`. Actions under API surface. |

`ConfigureQuestionMastery` (in `AppDbContext`):
```csharp
modelBuilder.Entity<QuestionMastery>(builder =>
{
    builder.Property(x => x.LatestNormalisedScore).HasPrecision(5, 4);
    builder.Property(x => x.PreviousNormalisedScore).HasPrecision(5, 4);
    builder.Property(x => x.Version).IsRowVersion();
    builder.HasOne<User>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
    builder.HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict);
    builder.HasIndex(x => new { x.StudentId, x.QuestionId }).IsUnique().HasDatabaseName(QuestionMasteryPerStudentIndex);
});
```

`BackfillSql` (the column list must match the generated `CreateTable`; the comment above the const reads `// PRD §7.3 threshold 0.8; migrations cannot read Mastery:CorrectThreshold.`):
```sql
WITH ranked AS (
    SELECT a."Id", a."StudentId", a."QuestionId", a."NormalisedScore", a."CreatedAt",
           row_number() OVER (PARTITION BY a."StudentId", a."QuestionId" ORDER BY a."CreatedAt" DESC, a."Id" DESC) AS "Rank"
    FROM "Attempts" AS a
    INNER JOIN "Sessions" AS s ON s."Id" = a."SessionId"
    WHERE s."IsTestMode" = false AND s."IsDeleted" = false AND a."IsDeleted" = false
)
INSERT INTO "QuestionMasteries" ("Id", "StudentId", "QuestionId", "IsMastered", "LatestAttemptId", "LatestNormalisedScore", "LatestAttemptedAt", "PreviousAttemptId", "PreviousNormalisedScore", "PreviousAttemptedAt", "CreatedBy", "CreationDate", "UpdatedBy", "UpdationDate", "IsDeleted", "DeletedAt")
SELECT gen_random_uuid(), latest."StudentId", latest."QuestionId",
       previous."Id" IS NOT NULL AND latest."NormalisedScore" >= 0.8 AND previous."NormalisedScore" >= 0.8,
       latest."Id", latest."NormalisedScore", latest."CreatedAt",
       previous."Id", previous."NormalisedScore", previous."CreatedAt",
       latest."StudentId", latest."CreatedAt", latest."StudentId", latest."CreatedAt", false, NULL
FROM ranked AS latest
LEFT JOIN ranked AS previous ON previous."StudentId" = latest."StudentId" AND previous."QuestionId" = latest."QuestionId" AND previous."Rank" = 2
WHERE latest."Rank" = 1
ON CONFLICT ("StudentId", "QuestionId") DO NOTHING;
```

### API — Tests (`api/Elmanhg.Tests/`)
| # | Path | Type | Contract |
|---|------|------|----------|
| 27 | `Domain/Mastery/QuestionMasteryTests.cs` | tests | T1–T11 |
| 28 | `Domain/Mastery/MasteryAttemptTests.cs` | tests | T12 |
| 29 | `Domain/Mastery/MasteryTotalsTests.cs` | tests | T13–T16 |
| 30 | `Domain/Mastery/NextLessonRecommendationTests.cs` | tests | T17–T19 |
| 31 | `Domain/Mastery/StudyStreakTests.cs` | tests | T20–T23 |
| 32 | `Application/Features/Mastery/QuestionMasteryRepositoryStub.cs` | static helper | `public static void StubFind(IQuestionMasteryRepository repository, params QuestionMastery[] rows)`: stubs `FirstOrDefaultAsync(Arg.Any<Expression<Func<QuestionMastery,bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<…include…>(), Arg.Any<…orderBy…>(), Arg.Any<bool>())` to return `rows.FirstOrDefault(predicate.Compile())`. This mirrors `SessionRepositoryStub`. |
| 33 | `Application/Features/Mastery/ProgressOptionsTests.cs` | tests | T28–T29 (mirror `MasteryOptionsTests.BuildProvider`) |
| 34 | `Application/Features/Mastery/GetMasteryOverview/GetMasteryOverviewHandlerTests.cs` | tests | T30–T35. `TimeProvider` is `Substitute.For<TimeProvider>()` with `GetUtcNow()` stubbed. Subjects, units and lessons are built with `Subject.Create`, `CurriculumUnit.Create`, `Lesson.Create` + `Publish`. |
| 35 | `Application/Features/Mastery/GetSubjectMastery/GetSubjectMasteryHandlerTests.cs` | tests | T36–T39. `unitRepository.FindAsync` and `lessonRepository.FindAsync` are stubbed to return `list.Where(predicate.Compile()).ToList()`, so the Published filter is exercised. |
| 36 | `Application/Features/Mastery/GetSubjectMastery/GetSubjectMasteryValidatorTests.cs` | tests | T40–T41 |
| 37 | `Integration/Mastery/MasteryTestData.cs` | static helper | `Task<Guid> PracticeAsync(HttpClient client, Guid lessonId, IReadOnlyDictionary<Guid, string> optionByQuestion)` starts a quiz (`SessionTestData.StartQuizAsync`), answers every item whose `questionId` is in the dictionary (`SessionTestData.AnswerAsync`, asserting 200), finishes, and returns the session id. Also `Task<JsonElement> GetOverviewAsync(HttpClient client)` (asserts 200), `JsonElement SubjectCard(JsonElement overview, Guid subjectId)`, `Task<List<QuestionMastery>> ReadMasteriesAsync(ApiFactory factory, Guid studentId)`, `Task RetireAsync(ApiFactory factory, Guid questionId)` (load tracked, `Retire(Guid.NewGuid())`, save) and `Task UnpublishAsync(ApiFactory factory, Guid lessonId)`. In the seeded MCQ questions `"b"` is correct and `"a"` is wrong. |
| 38 | `Integration/Mastery/MasteryOverviewEndpointTests.cs` | tests | T42–T50 |
| 39 | `Integration/Mastery/SubjectMasteryEndpointTests.cs` | tests | T51–T55 |
| 40 | `Integration/Persistence/QuestionMasteryPersistenceTests.cs` | tests | T56–T59 |

### Web (`web/src/`)
| # | Path | Type | Contract |
|---|------|------|----------|
| 41 | `features/mastery/index.ts` | barrel | `export { StudentHomePage } from './pages/StudentHomePage'; export { invalidateMastery } from './api/invalidateMastery';` |
| 42 | `features/mastery/locales.ts` | locales | `import ar from './i18n/ar.json'; import en from './i18n/en.json'; export const masteryLocales = { ar, en };` |
| 43 | `features/mastery/i18n/en.json` | strings | See the i18n table. |
| 44 | `features/mastery/i18n/ar.json` | strings | See the i18n table. |
| 45 | `features/mastery/api/invalidateMastery.ts` | util | `export const masteryQueryPrefix = '/api/mastery';` `export function invalidateMastery(queryClient: QueryClient): Promise<void> { return queryClient.invalidateQueries({ predicate: (query) => { const [first] = query.queryKey; return typeof first === 'string' && first.startsWith(masteryQueryPrefix); } }); }` |
| 46 | `features/mastery/api/invalidateMastery.test.ts` | test | W9 |
| 47 | `features/mastery/components/MasteryBar.tsx` | component | `export interface MasteryBarProps { percent: number; label: string }`. Renders `<progress value={Math.min(100, Math.max(0, percent))} max={100} aria-label={label} className="h-1.5 w-full appearance-none overflow-hidden rounded-full bg-soft [&::-webkit-progress-bar]:bg-soft [&::-webkit-progress-value]:bg-accent [&::-moz-progress-bar]:bg-accent" />` |
| 48 | `features/mastery/components/HeadlineCounterCard.tsx` | component | `export interface HeadlineCounterCardProps { headline: MasteryHeadlineResult; streakDays: number }`. Renders `<section aria-label={t('headline.label')} className="flex flex-col gap-1 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">` containing `<p className="font-display text-display font-bold lg:text-display-desktop">{t('headline.remaining', { remaining, total })}</p>` and `<p className="text-caption text-text-muted">{t('headline.meta', { seen, mastered, streak })}</p>`, with every number wrapped in `Number(...)`. |
| 49 | `features/mastery/components/NextLessonCard.tsx` | component | `export interface NextLessonCardProps { lesson: NextLessonResult }`. Card classes as in #48 plus `gap-2`: caption `t('nextLesson.label')`, `<p className="text-ui font-semibold">{lesson.lessonName}</p>`, caption `lesson.subjectName`, caption `t('nextLesson.mastery', { percent })`, `<MasteryBar percent label={t('nextLesson.barLabel', { name: lesson.lessonName })} />`, then `<Button asChild><Link to="/student/lesson/$lessonId/practice" params={{ lessonId: lesson.lessonId }}>{t('nextLesson.train')}</Link></Button>` (the one primary action). |
| 50 | `features/mastery/components/SubjectMasteryCard.tsx` | component | `export interface SubjectMasteryCardProps { subject: SubjectMasteryResult }`. `<article aria-labelledby={id} className="flex flex-col gap-2 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">` containing `<h3 id={id} className="font-display text-h2 font-bold lg:text-h2-desktop">{subject.name}</h3>`, `<MasteryBar percent label={t('subjects.barLabel', { name })} />`, `<p className="text-caption text-text-muted">{t('subjects.mastery', { percent })} · {t('subjects.available', { count })}</p>`. `id` comes from `useId()`. Not a link (D15). |
| 51 | `features/mastery/pages/StudentHomePage.tsx` | page | `export function StudentHomePage()`. It reads `useSession()` for `displayName` and `useGetMasteryOverview()` (Orval). Always renders `<section className="flex flex-col gap-4"><h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('home.greeting', { name: session?.displayName ?? '' })}</h1>…`. Then: `isError` → `<ContentErrorState title={t('home.errorTitle')} error={error} onRetry={() => { void refetch(); }} />`; `isPending` → `<ContentListSkeleton label={t('home.loading')} />`; otherwise `HeadlineCounterCard`, `NextLessonCard` when `data.nextLesson` is not null, `<h2 className="font-display text-h2 font-bold lg:text-h2-desktop">{t('subjects.title')}</h2>`, and either `<p className="text-ui text-text-muted">{t('subjects.empty')}</p>` or `<ul className="grid grid-cols-1 gap-3 md:grid-cols-2 lg:grid-cols-3">` with `<li key={subject.subjectId}><SubjectMasteryCard subject={subject} /></li>`. Under 120 lines. |
| 52 | `features/mastery/pages/StudentHomePage.test.tsx` | test | W1–W8 |
| 53 | `test/masteryFixtures.ts` | fixtures | `export const physicsId = '66666666-6666-4666-8666-666666666666'; export const chemistryId = '77777777-7777-4777-8777-777777777777'; export const nextLessonId = '88888888-8888-4888-8888-888888888888';` `export function masteryOverview(overrides?: Partial<MasteryOverviewResult>): MasteryOverviewResult` returns `{ headline: { servableTotal: 60, masteredCount: 20, remainingCount: 40, seenCount: 30 }, streakDays: 3, nextLesson: { lessonId: nextLessonId, lessonName: "Newton's laws", subjectId: physicsId, subjectName: 'Physics', masteryPercent: 10 }, subjects: [{ subjectId: physicsId, name: 'Physics', servableCount: 50, masteredCount: 20, seenCount: 30, masteryPercent: 40 }, { subjectId: chemistryId, name: 'Chemistry', servableCount: 10, masteredCount: 0, seenCount: 0, masteryPercent: 0 }], ...overrides }`. |

### Docs
| # | Path | Contract |
|---|------|----------|
| 54 | `docs/mastery.md` | Sections, in order: **Definitions** (PRD §7.3 table, restated with the 0.8 threshold from `Mastery:CorrectThreshold`). **Model** (a `QuestionMastery` column table with the PRD §15 mapping from D2). **Update rule** (D1, D3, D4; the ordering algorithm; "Loses mastery on a wrong attempt"; a threshold change applies to new attempts only, D14). **Aggregates** (servable only, D5; floor percent, D8; weighting). **Headline counter** (D7, D9; remaining = total − mastered; seen). **Streak** (D10). **Next recommended lesson** (D11). **Retirement and unpublishing** (D6: live, no job; re-publishing restores counts, because rows are kept). **Backfill** (D14). **Concurrency** (D13). **API** (both endpoints, response shapes, 401/403/404/422). **Options** (`Mastery:CorrectThreshold`, `Progress:StreakTimeZone`, `Progress:StreakMaxDays`). **Access** (`Progress.ViewOwn`, Students only). **Student home (web)** (widgets, "درّب الآن" to `/student/lesson/{id}/practice`, subject cards not links until #85, mastery refreshed after each answer). |

### i18n strings (namespace `mastery`)
| Key | en | ar |
|---|---|---|
| `home.greeting` | `Hello, {name}` | `أهلًا {name}` |
| `home.loading` | `Loading your progress…` | `جارٍ تحميل تقدّمك…` |
| `home.errorTitle` | `Could not load your progress.` | `تعذّر تحميل تقدّمك.` |
| `headline.label` | `Your question counter` | `عدّاد أسئلتك` |
| `headline.remaining` | `{remaining, number} of {total, number} questions left for you` | `متبقّي لك {remaining, number} سؤال من {total, number}` |
| `headline.meta` | `Seen {seen, number} · Mastered {mastered, number} · Streak: {streak, plural, one {# day} other {# days}}` | `شاهدت {seen, number} سؤالًا · أتقنت {mastered, number} · سلسلة الأيام: {streak, number} يوم` |
| `nextLesson.label` | `Suggested next lesson` | `الدرس التالي المقترح` |
| `nextLesson.mastery` | `Mastery {percent, number}%` | `إتقان {percent, number}٪` |
| `nextLesson.barLabel` | `{name} mastery` | `إتقان {name}` |
| `nextLesson.train` | `Train now` | `درّب الآن` |
| `subjects.title` | `Your subjects` | `موادك` |
| `subjects.mastery` | `{percent, number}% mastered` | `إتقان {percent, number}٪` |
| `subjects.available` | `{count, plural, one {# question available} other {# questions available}}` | `{count, number} سؤال متاح` |
| `subjects.barLabel` | `{name} mastery` | `إتقان {name}` |
| `subjects.empty` | `No subjects yet.` | `لا توجد مواد بعد.` |

## Error codes
No new constants and no new resx keys. Reused:
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `ErrorCodes.UserNotAuthenticated` | `USER_NOT_AUTHENTICATED` | both new handlers | `UnauthorizedCoreException` | 401 |
| `ErrorCodes.SubjectIdRequired` | `SUBJECT_ID_REQUIRED` | `GetSubjectMasteryValidator` | pipeline 422 | 422 |
| `ErrorCodes.SubjectNotFound` | `SUBJECT_NOT_FOUND` | `GetSubjectMasteryHandler` | `NotFoundCoreException` | 404 |
| `ErrorCodes.SessionModifiedConcurrently` | `SESSION_MODIFIED_CONCURRENTLY` | `AppDbContext.SaveChangesAsync` (xmin conflict or unique violation on `QuestionMasteries`) | `ConflictCoreException` | 409 |

## Domain behaviour
```csharp
public static QuestionMastery Start(Guid studentId, Guid questionId, MasteryAttempt attempt)
{
    return new QuestionMastery(Guid.NewGuid(), studentId)
    {
        StudentId = studentId,
        QuestionId = questionId,
        IsMastered = false,
        LatestAttemptId = attempt.AttemptId,
        LatestNormalisedScore = attempt.NormalisedScore,
        LatestAttemptedAt = attempt.AttemptedAt,
    };
}

public void Record(MasteryAttempt attempt, decimal correctThreshold)
{
    if (attempt.AttemptId == LatestAttemptId || attempt.AttemptId == PreviousAttemptId)
    {
        return;
    }

    if (attempt.AttemptedAt >= LatestAttemptedAt)
    {
        PreviousAttemptId = LatestAttemptId;
        PreviousNormalisedScore = LatestNormalisedScore;
        PreviousAttemptedAt = LatestAttemptedAt;
        LatestAttemptId = attempt.AttemptId;
        LatestNormalisedScore = attempt.NormalisedScore;
        LatestAttemptedAt = attempt.AttemptedAt;
    }
    else if (PreviousAttemptedAt is null || attempt.AttemptedAt > PreviousAttemptedAt)
    {
        PreviousAttemptId = attempt.AttemptId;
        PreviousNormalisedScore = attempt.NormalisedScore;
        PreviousAttemptedAt = attempt.AttemptedAt;
    }
    else
    {
        return;
    }

    IsMastered = PreviousNormalisedScore is not null && LatestNormalisedScore >= correctThreshold && PreviousNormalisedScore >= correctThreshold;
    UpdatedBy = StudentId;
    UpdationDate = DateTimeOffset.UtcNow;
}
```
- A comment above `Record` reads `// PRD §7.3: mastered = the two most recent attempts, by attempt time, both at or above the threshold.`
- There are no `BusinessRuleViolation*` guards: every input is valid, and a wrong attempt is a normal transition that clears `IsMastered`.
- `Version` is never set in code, because it is xmin.

## API surface
| Method | Route | Policy | Request | Response |
|---|---|---|---|---|
| GET | `/api/mastery/overview` | `DefaultCodes.ProgressViewOwn` | — | 200 `MasteryOverviewResult` |
| GET | `/api/mastery/subjects/{subjectId:guid}` | `DefaultCodes.ProgressViewOwn` | route `subjectId` | 200 `SubjectMasteryDetailResult`, 404 `SUBJECT_NOT_FOUND` |

- Actions: `[HttpGet("overview", Name = "GetMasteryOverview")] [ProducesResponseType<MasteryOverviewResult>(StatusCodes.Status200OK)] public async Task<ActionResult> GetMasteryOverview(CancellationToken cancellationToken)` sends `new GetMasteryOverviewQuery()`. `[HttpGet("subjects/{subjectId:guid}", Name = "GetSubjectMastery")] [ProducesResponseType<SubjectMasteryDetailResult>(StatusCodes.Status200OK)] public async Task<ActionResult> GetSubjectMastery([FromRoute] Guid subjectId, CancellationToken cancellationToken)` sends `new GetSubjectMasteryQuery(subjectId)`.
- Postman folder "Mastery" has the description "Sign in as a Student. Answer questions in the Sessions folder first to see mastery." It holds two requests with the same shape as "Get session" (a status-200 test script and no header): "Get mastery overview" `{{baseUrl}}/api/mastery/overview` and "Get subject mastery" `{{baseUrl}}/api/mastery/subjects/{{subjectId}}`.

## Test plan

### API — Domain (no doubles)
`MasteryAttempt` values are built directly with an explicit `AttemptedAt` (`T0 = 2026-09-01T10:00Z`, then `T0.AddMinutes(n)`).
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| T1 | QuestionMasteryTests | `Start_FirstAttempt_KeepsLatestAndIsNotMastered` | Student and question ids, latest id/score/time set; previous fields null; `IsMastered` false, even when the score is 1 |
| T2 | QuestionMasteryTests | `Record_TwoCorrectAttempts_IsMastered` | Start(1.0 @T0), Record(0.9 @T1) → mastered; latest = the T1 attempt, previous = the T0 attempt |
| T3 | QuestionMasteryTests | `Record_BothAtThreshold_IsMastered` | 0.8 and 0.8 → true |
| T4 | QuestionMasteryTests | `Record_OneBelowThreshold_IsNotMastered` | 1.0 then 0.79 → false |
| T5 | QuestionMasteryTests | `Record_WrongAfterMastered_LosesMastery` | 1, 1, 0 → false |
| T6 | QuestionMasteryTests | `Record_CorrectWrongCorrect_NeedsOneMoreCorrect` | 1, 0, 1 → false; then 1 → true |
| T7 | QuestionMasteryTests | `Record_SameAttemptAgain_ChangesNothing` | Recording the latest attempt again leaves the previous fields, `IsMastered` and `UpdationDate` unchanged |
| T8 | QuestionMasteryTests | `Record_OlderThanLatest_BecomesPrevious` | Start(1 @T2), Record(1 @T1) → previous = the T1 attempt, latest still the T2 attempt, mastered |
| T9 | QuestionMasteryTests | `Record_OlderThanBothSlots_IsIgnored` | Slots @T2 and @T3, Record(0 @T1) → slots unchanged, still mastered |
| T10 | QuestionMasteryTests | `Record_NewAttempt_StampsUpdatedByAndUpdationDate` | `UpdatedBy == StudentId`; `UpdationDate` ≥ a timestamp taken before the call |
| T11 | QuestionMasteryTests | `Record_ThresholdArgument_IsHonoured` | 0.85 and 0.85 with threshold 0.9 → not mastered |
| T12 | MasteryAttemptTests | `From_Attempt_CopiesIdScoreAndTime` | An attempt from `SessionBuilder` + `RecordAttempt(…, Grade(0.5m), 0)` maps its id, 0.5 and `CreatedAt` |
| T13 | MasteryTotalsTests | `Of_Lessons_PoolsCountsWeightedByQuestionCount` | Lessons (10 servable, 5 mastered) and (30, 0) → 40/5, percent 12 (not the 25 % mean) |
| T14 | MasteryTotalsTests | `MasteryPercent_NoServable_IsZero` | (0, 0, 0) → 0 |
| T15 | MasteryTotalsTests | `MasteryPercent_Fraction_RoundsDown` | (3, 2, 3) → 66; (3, 3, 3) → 100 |
| T16 | MasteryTotalsTests | `RemainingCount_ServableMinusMastered` | (60, 20, 30) → 40 |
| T17 | NextLessonRecommendationTests | `Pick_Lessons_ReturnsLowestMastery` | 50 % and 10 % lessons → the 10 % one |
| T18 | NextLessonRecommendationTests | `Pick_EqualMastery_ReturnsFirstInCurriculumOrder` | Two 0 % lessons: subject order 2/unit 1 and subject order 1/unit 3 → the subject-order-1 lesson; same subject and unit → the lower `LessonOrder` |
| T19 | NextLessonRecommendationTests | `Pick_OnlyMasteredOrEmptyLessons_ReturnsNull` | (5, 5) and (0, 0) → null |
| T20 | StudyStreakTests | `Count_NoActivity_ReturnsZero` | empty → 0 |
| T21 | StudyStreakTests | `Count_ActiveToday_CountsConsecutiveDaysBack` | today, −1, −2, −4 → 3 |
| T22 | StudyStreakTests | `Count_InactiveTodayActiveYesterday_CountsFromYesterday` | −1, −2 → 2 |
| T23 | StudyStreakTests | `Count_LastActiveTwoDaysAgo_ReturnsZero` | −2, −3 → 0 |

### API — Application (NSubstitute at ports)
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| T24 | SubmitAnswerHandlerTests | `Handle_FirstAnswer_StartsQuestionMastery` | Mastery stub empty → `AddAsync` `Received(1)` with `Arg.Is<QuestionMastery>(m => m.StudentId == student && m.QuestionId == QuestionId && m.LatestAttemptId == result.Attempt!.Id && !m.IsMastered)`; `SaveChangesAsync` `Received(1)` |
| T25 | SubmitAnswerHandlerTests | `Handle_SecondCorrectAnswer_MarksExistingMasteryMastered` | Stubbed row `Start(student, QuestionId, new MasteryAttempt(Guid.NewGuid(), 1m, UtcNow.AddDays(-1)))` → after a correct answer `row.IsMastered` true and `row.LatestAttemptId == result.Attempt!.Id`; `AddAsync` `DidNotReceive`; save `Received(1)` |
| T26 | SubmitAnswerHandlerTests | `Handle_ReplayedAnswer_DoesNotRecordMasteryAgain` | Two identical calls → `AddAsync` `Received(1)` in total, and the same attempt id is returned |
| T27 | SubmitAnswerHandlerTests | `Handle_TestModeSession_DoesNotRecordMastery` | Restub with a `StartQuiz(..., isTestMode: true)` session → an attempt is recorded, `AddAsync` `DidNotReceive`, save `Received(1)` |
| T28 | ProgressOptionsTests | `AddApplication_DefaultProgressOptions_UseCairoAndOneYear` | `StreakTimeZone == "Africa/Cairo"`, `StreakMaxDays == 365` |
| T29 | ProgressOptionsTests | `AddApplication_UnknownTimeZone_ThrowsOptionsValidationException` | `Progress:StreakTimeZone = "Mars/Olympus"` → `OptionsValidationException` |
| T30 | GetMasteryOverviewHandlerTests | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | `UnauthorizedCoreException` + `USER_NOT_AUTHENTICATED` |
| T31 | GetMasteryOverviewHandlerTests | `Handle_LessonCounts_ReturnsHeadlineFromServableMasteredAndSeen` | Counts (10, 4, 6) and (5, 1, 2) → headline (15, 5, 10, 8) |
| T32 | GetMasteryOverviewHandlerTests | `Handle_Subjects_ReturnsEverySubjectInOrderWithWeightedMastery` | Subject A (lessons 10/5 and 30/0 → 12 %), subject B with no counts → 0/0/0 %, in the order returned by `GetAllAsync` |
| T33 | GetMasteryOverviewHandlerTests | `Handle_LessonToImprove_ReturnsNextLessonWithNames` | `NextLesson` = lesson id, lesson name, subject id and name, percent 10 |
| T34 | GetMasteryOverviewHandlerTests | `Handle_EverythingMastered_ReturnsNoNextLesson` | `NextLesson` null; `lessonRepository.GetByIdAsync` `DidNotReceive` |
| T35 | GetMasteryOverviewHandlerTests | `Handle_ActivityDays_CountsStreakInConfiguredTimeZone` | `GetUtcNow` = 2026-09-27T22:30Z (Cairo 2026-09-28 01:30); active days {2026-09-28} → `StreakDays == 1`. A UTC calendar would give 0. |
| T36 | GetSubjectMasteryHandlerTests | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | 401 exception + code |
| T37 | GetSubjectMasteryHandlerTests | `Handle_UnknownSubject_ThrowsSubjectNotFound` | `NotFoundCoreException` + `SUBJECT_NOT_FOUND` |
| T38 | GetSubjectMasteryHandlerTests | `Handle_Subject_ReturnsUnitsAndPublishedLessonsWithWeightedMastery` | Unit with a Published lesson (4/2 → 50 %), a Published lesson (4/0 → 0 %) and a Draft lesson (absent) → unit 8/2 → 25 %, subject 25 % |
| T39 | GetSubjectMasteryHandlerTests | `Handle_LessonWithoutServableQuestions_ReturnsZeroCounts` | A Published lesson with no count row → 0/0/0 and 0 % |
| T40 | GetSubjectMasteryValidatorTests | `Validate_SubjectId_Passes` | valid |
| T41 | GetSubjectMasteryValidatorTests | `Validate_EmptySubjectId_FailsWithSubjectIdRequired` | error code `SUBJECT_ID_REQUIRED` |

### API — Integration (Testcontainers, real HTTP)
Every test seeds its own subject, student and questions. Headline totals are global in the shared database, so tests assert `remainingCount == servableTotal − masteredCount` and exact per-student values (mastered, seen, streak) and per-subject cards. They never assert an exact global total.
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| T42 | MasteryOverviewEndpointTests | `Get_Anonymous_Returns401` | 401 |
| T43 | MasteryOverviewEndpointTests | `Get_Teacher_Returns403` | 403 |
| T44 | MasteryOverviewEndpointTests | `Get_Admin_Returns403` | 403 |
| T45 | MasteryOverviewEndpointTests | `Get_NewStudent_ReturnsNothingMasteredAndNoStreak` | 200; mastered 0, seen 0, remaining == total, streak 0; the seeded subject card shows servable 2, mastered 0, percent 0 |
| T46 | MasteryOverviewEndpointTests | `Get_AfterTwoCorrectAttempts_CountsQuestionMasteredSeenAndStreak` | Lesson with q1 and q2. Practice 1: q1 "b", q2 "a". Practice 2: q1 "b". Result: mastered 1, seen 2, remaining == total − 1, streak 1; subject card 2/1/50 %. DB row for q1 has `IsMastered` true and q2 false. |
| T47 | MasteryOverviewEndpointTests | `Get_WrongAttemptAfterMastery_LosesMastery` | After T46's flow, practice 3 with q1 "a" → mastered 0; q1 row `IsMastered` false |
| T48 | MasteryOverviewEndpointTests | `Get_MasteredQuestionRetired_DropsItFromMasteredAndServable` | After mastering q1, `RetireAsync(q1)` → mastered 0, seen 1; subject card servable 1, mastered 0; the q1 row still exists with `IsMastered` true |
| T49 | MasteryOverviewEndpointTests | `Get_LessonUnpublished_DropsItsQuestionsFromHeadlineAndSubject` | After mastering q1, `UnpublishAsync(lesson)` → mastered 0, seen 0; subject card 0/0/0 % |
| T50 | MasteryOverviewEndpointTests | `Post_AdminTestModeAnswer_WritesNoQuestionMastery` | Admin practises and answers "b" → `ReadMasteriesAsync(admin)` empty |
| T51 | SubjectMasteryEndpointTests | `Get_Anonymous_Returns401` | 401 |
| T52 | SubjectMasteryEndpointTests | `Get_Teacher_Returns403` | 403 |
| T53 | SubjectMasteryEndpointTests | `Get_UnknownSubject_Returns404` | 404, problem `code` = `SUBJECT_NOT_FOUND` |
| T54 | SubjectMasteryEndpointTests | `Get_SubjectWithProgress_ReturnsUnitAndLessonPercentages` | Unit with Published lesson A (2 q; q1 mastered via two practices), Published lesson B (2 q, untouched) and Draft lesson C → lessons [A 2/1/50 %, B 2/0/0 %] in order, C absent; unit 4/1/25 %; subject 4/1/25 %; A seen 1 |
| T55 | SubjectMasteryEndpointTests | `Get_SubjectWithoutLessons_ReturnsZeroTotals` | Subject with one empty unit → 200; subject 0/0/0 %, one unit with an empty `lessons` array |
| T56 | QuestionMasteryPersistenceTests | `Migrate_QuestionMasteries_HasUniqueStudentQuestionIndex` | `pg_indexes.indexdef` for `IX_QuestionMasteries_StudentId_QuestionId` contains `UNIQUE` and `"StudentId", "QuestionId"` |
| T57 | QuestionMasteryPersistenceTests | `Save_DuplicateStudentQuestion_ThrowsSessionModifiedConcurrently` | Two scopes each `Add(QuestionMastery.Start(same student, same question, …))`. The second save throws `ConflictCoreException` with code `SESSION_MODIFIED_CONCURRENTLY`. |
| T58 | QuestionMasteryPersistenceTests | `Save_ConcurrentRecordOnSameRow_ThrowsSessionModifiedConcurrently` | Two scopes load the same row and both `Record` a new attempt. The first save succeeds; the second throws the same conflict. |
| T59 | QuestionMasteryPersistenceTests | `Backfill_ExistingAttempts_RebuildsMasteryExcludingTestMode` | Student: q1 "b" in two practices, q2 "a" once. Admin: q1 "b" in test mode. `ExecuteSql($"DELETE FROM \"QuestionMasteries\" WHERE \"StudentId\" = {student}")`, then `ExecuteSqlRawAsync(AddQuestionMastery.BackfillSql)`. Result: the student's q1 row is mastered and has latest and previous attempt ids equal to the two attempt ids; q2 row is not mastered, latest score 0, previous null; the admin has no rows. |
| — | AppDbContextTests | `Migrate_FreshDatabase_LeavesNoPendingMigrations` (modified) | The list ends with `_AddQuestionMastery` |

### Web (Vitest + RTL + MSW; `renderApp('/student', { session: testSessions.student, lng })`, handlers from `getGetMasteryOverviewMockHandler(masteryOverview(...))`)
| # | Test file | `it(...)` | Asserts |
|---|-----------|-----------|---------|
| W1 | StudentHomePage.test.tsx | `shows a loading state then the headline counter with seen, mastered and streak` | `status` "Loading your progress…", then "40 of 60 questions left for you" and "Seen 30 · Mastered 20 · Streak: 3 days"; heading "Hello, أحمد" |
| W2 | StudentHomePage.test.tsx | `links the suggested lesson to its practice page` | "Newton's laws" visible; link "Train now" has `href` `/student/lesson/88888888-8888-4888-8888-888888888888/practice` |
| W3 | StudentHomePage.test.tsx | `hides the suggested lesson when there is none` | `nextLesson: null` → no "Suggested next lesson" text and no "Train now" link |
| W4 | StudentHomePage.test.tsx | `shows every subject with its mastery bar and available questions` | `article` "Physics" has `progressbar` "Physics mastery" with `value` "40" and text "40% mastered · 50 questions available"; `article` "Chemistry" has `value` "0" |
| W5 | StudentHomePage.test.tsx | `shows the empty state when there are no subjects` | `subjects: []` → "No subjects yet." |
| W6 | StudentHomePage.test.tsx | `shows the error state and retries` | First `GET */api/mastery/overview` returns 500 `{ code: 'UNHANDLED_EXCEPTION' }` (`once: true`) → `alert` contains "Could not load your progress."; clicking "Retry" shows "40 of 60 questions left for you" |
| W7 | StudentHomePage.test.tsx | `renders right-to-left with the Arabic counter in Arabic` | `lng: 'ar'` → `document.documentElement` `dir="rtl"`; text "متبقّي لك ٤٠ سؤال من ٦٠"; link "درّب الآن" |
| W8 | StudentHomePage.test.tsx | `has no axe violations` | After the headline renders, `(await axe(container)).violations` equals `[]` |
| W9 | invalidateMastery.test.ts | `marks mastery queries stale and leaves other queries fresh` | `createTestQueryClient()`; `setQueryData(['/api/mastery/overview'], …)`, `setQueryData(['/api/mastery/subjects/x'], …)`, `setQueryData(['/api/sessions/s'], …)`; after `await invalidateMastery(client)`, `getQueryState(...).isInvalidated` is true, true, false |
| — | AppShell.test.tsx (modified) | existing tests | They pass with `getMasteryMock()` handlers and the greeting heading (see Existing code touched) |

Mutation-check T25, T27, T35, T48, W2 and W9: break the production line, confirm the test fails, then restore it.

## Definition of done
- [ ] `QuestionMastery` entity, migration `AddQuestionMastery` (table, restrict FKs, unique index, backfill SQL) and xmin `Version` exist exactly as specified. The migration has no `Drop*` in `Up`.
- [ ] A new, non-test-mode attempt creates or updates the student's `QuestionMastery` in the same `SaveChangesAsync`. Replays and test-mode attempts do not.
- [ ] Mastered means the two most recent attempts, by attempt time, are both ≥ `Mastery:CorrectThreshold`. A wrong attempt clears it.
- [ ] Mastered, seen and all percentages count servable questions only, joined at read time. Nothing servability-dependent is stored.
- [ ] Retiring a question or unpublishing a lesson changes the overview and subject numbers on the next read (T48, T49). `docs/backlog.json` records the no-job decision.
- [ ] Lesson, unit and subject percentages are pooled Σmastered / Σservable, floored integers, and 0 when empty.
- [ ] Headline: `remainingCount = servableTotal − masteredCount` from one query. `seenCount` is present.
- [ ] Streak uses `Progress:StreakTimeZone` days and non-test quiz attempts, anchored on today or yesterday. `ProgressOptions` is validated on start, and the key is present in `appsettings.example.json` and `ApiFactory`.
- [ ] Both endpoints use `DefaultCodes.ProgressViewOwn`. Teacher and admin get 403, anonymous 401, an unknown subject 404 `SUBJECT_NOT_FOUND`.
- [ ] Concurrent mastery conflicts return 409 `SESSION_MODIFIED_CONCURRENTLY`. No new error codes and no resx changes.
- [ ] `SessionRepository.GetQuizActivityDaysAsync` uses interpolated `SqlQuery` (parameterised). No `*Raw` with interpolation.
- [ ] `api/openapi/v1.json`, `web/src/shared/api/generated/**` and the Postman "Mastery" folder are regenerated or updated, with no drift.
- [ ] `/student` renders the greeting, headline card, suggested lesson with "درّب الآن", subject cards with a mastery bar, and loading, error+retry and empty states. Tokens only, logical properties only, `en` and `ar` strings both present.
- [ ] Answering a quiz question invalidates `/api/mastery*` queries.
- [ ] Every test T1–T59 and W1–W9 exists with these names and passes. The migration list includes `_AddQuestionMastery`. `AppShell.test.tsx` is changed only as listed.
- [ ] `dotnet test api/ -c Release` is green with `appsettings.json` moved aside (CI parity). `dotnet build` adds no new warnings.
- [ ] `npm --prefix web run typecheck`, `lint`, `test` and `gen:api` pass, with no diff.
- [ ] `docs/mastery.md` is created. `docs/PRD.md` §15, `docs/sessions.md`, `docs/audit-log.md` and `docs/backlog.json` are updated as listed. No other doc diverges.
- [ ] Guard grep is clean (`DateTime.Now/UtcNow`, `.Result`, `.Wait()`, `new HttpClient(`, `FromSqlRaw`, `async void`).
