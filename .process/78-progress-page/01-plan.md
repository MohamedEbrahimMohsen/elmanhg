# Plan — [E5.S5] Progress page (#78)

## Goal
A signed-in student opens `/student/progress` ("تقدّمي") and sees four things. First, the headline counter and day streak. Second, one card per subject with its mastery bar and a unit table: unit mastery % and best unit-exam score. Third, their weak spots: the lowest-mastery lessons and objectives, each with a "درّب الآن" shortcut into that lesson's practice page. Fourth, a paged history of all their quiz and exam sessions, filterable by all, quizzes or exams, with "عرض" and "متابعة" links. Today the route shows a placeholder.

## Scope
**In:**
- Three new read endpoints under `api/progress`: subjects with units and best exam scores, weak spots, and session history with a kind filter and paging.
- Two supporting repository queries (objective counts, best unit-exam scores).
- `UnitExamScope`, the unit-exam scope contract E6 will write.
- Progress options, error codes and resx strings.
- OpenAPI, Orval and Postman regenerated.
- A new `web/src/features/progress` feature for the page, which reuses `GET /api/mastery/overview` for headline and streak.
- Progress queries are invalidated after each answer.
- Docs: the new `docs/progress.md`, plus updates to `docs/mastery.md`, `docs/sessions.md` and `docs/claude-design-prompt.md` §4.

**Out:**
- The plan line ("الباقة"): subscriptions come in E10 (#99).
- The admin view of any student's progress (#106).
- Exam result pages, so history rows for exams get no link until E6 (#81).
- Objective-scoped quizzes: an objective's "درّب الآن" opens its lesson's practice page.
- Free-tier locks (#87).

**Deferred:** none. Every sub-task is buildable offline. Unit-exam best scores read `UnitExam` sessions that E6 (#81) will create. The query and tests are built now, and the tests seed exam rows directly.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | Where do headline and streak come from? | Reuse `GET /api/mastery/overview` (`useGetMasteryOverview`). Its streak logic is not duplicated. | Orchestrator instruction: reuse the mastery endpoints. The streak is already defined in `docs/mastery.md`. |
| 2 | Is the streak per subject (PRD §7.6 wording)? | No. It is the student-wide streak, as defined in #77, shown once in the summary card. | The #77 definition (consecutive days with at least one non-test quiz attempt) is global. A per-subject streak has no data model. |
| 3 | One `GET /api/progress` or several endpoints? | Three: `GET /api/progress/subjects`, `GET /api/progress/weak-spots`, `GET /api/progress/sessions`. | One per sub-task. Each page section loads, fails and retries on its own. History is paged separately. |
| 4 | How is a subject's unit mastery computed? | Reuse `IQuestionMasteryRepository.GetLessonCountsAsync(userId, null)` and `MasteryTotals.Of(...)`, grouped by `UnitId` and `SubjectId`. Every unit (order, then creation date) and every subject (order, then creation date) is listed, and a unit without servable questions shows 0/0/0 %. | Same rules as `SubjectMasteryResultGenerator` (#77). One grouped query covers all subjects. |
| 5 | Best exam score source | Submitted (`SubmittedAt != null`), non-test, not soft-deleted `Kind == UnitExam` sessions of the student, grouped by `ScopeKey`, `MAX(ScorePercent)`. A unit matches on `new UnitExamScope(unit.Id).ToKey()` = `unit:{id:D}`. Null when there is none. Multi-unit exams do not count toward a unit's best. | `docs/sessions.md` already says exams use `unit:<guid>`. PRD §7.4: the best score is the displayed one. PRD §7.6 says "unit exam best scores". |
| 6 | `UnitExamScope` now or in E6? | Now, in `Elmanhg.Domain/Sessions/UnitExamScope.cs`, mirroring `QuizScope`. E6 (#81) must start exams with it. | The progress query needs the key format fixed in code, not only in docs. |
| 7 | Weak lesson rule | A lesson qualifies when it has servable questions, `SeenCount > 0` and `MasteredCount < ServableCount`. Order: mastered/servable ascending, then subject order, unit order, lesson order, lesson id. Take `Progress:WeakLessonCount` (default 4, as in the prototype `slice(0,4)`). | Prototype `vProgress` shows only lessons with attempts, lowest first. Fully mastered lessons are not weak. The tie-break matches `NextLessonRecommendation`. |
| 8 | Weak objective rule | Per objective, over the **servable** questions tagged with it (`Question.ObjectiveId`): `mastered / servable`, using the same mastery definition. It qualifies when seen > 0 and not fully mastered. Order: ratio ascending, subject, unit, lesson, objective order, objective id. Take `Progress:WeakObjectiveCount` (default 3, as in the prototype `slice(0,3)`). Questions without an objective and soft-deleted objectives are ignored. | PRD §7.6 says "lowest-mastery … objectives". The prototype uses raw attempt accuracy. Mastery keeps one definition across the product (`docs/mastery.md`). |
| 9 | Where "درّب الآن" goes | `/student/lesson/{lessonId}/practice`, for weak lessons and weak objectives (the objective's lesson). | Matches `NextLessonCard` (#77). No objective-scoped quiz exists. |
| 10 | History filters | `kind` ∈ {`Quiz`, `Exam`} or absent (all). `Exam` = `UnitExam` or `MultiUnitExam`. No date or subject filter. | The prototype filter is `all / quiz / exam`. |
| 11 | History contents | All of the student's non-test sessions, open and finished, newest `StartedAt` first (then `Id` descending). Paged with `PageData<T>` through `FindPaginatedAsync`: `pageSize` defaults to 20, and the maximum is `Progress:HistoryMaxPageSize` (50). | PRD §7.6 "all quiz and exam sessions". A history grows without bound, so it is paged. Mirrors `GetAuditLogsHandler`. |
| 12 | History scope names | Quiz: the lesson name, looked up by `QuizScope.FromJson(Scope).LessonId` across all lesson states (an archived lesson keeps its name). UnitExam: the unit name, through `UnitExamScope.FromJson`. MultiUnitExam or a soft-deleted lesson/unit: `ScopeName = null`, and the web shows "غير متاح". | A student saw the lesson, so its name is history, not content. The "Published only" rule (PRD §5.2) covers content reads. |
| 13 | History row links (web) | Finished Quiz → "عرض" to `/student/quiz-result/{id}`. Open Quiz → "متابعة" to `/student/quiz/{id}`. Exams → no link (E6). | Mirrors the prototype `link`. The resume route exists (#76). |
| 14 | Invalid `kind` | A numeric out-of-range value binds, and the validator's `IsInEnum` returns 422 `SESSION_HISTORY_KIND_INVALID`. An unknown name gets the framework's 400 model-binding error. | Mirrors `GetQuestionsValidator` (`IsInEnum` on nullable enums). |
| 15 | Freshness after a quiz | Extend `invalidateMastery` to also invalidate keys that start with `/api/progress`. `useSessionHistory` sets `staleTime: 0`, so every visit refetches history (starts and finishes add or change rows without an answer). | Keeps one invalidation helper. No quiz-hook changes. |
| 16 | Admins and teachers | All three endpoints use `DefaultCodes.ProgressViewOwn` (Student only), and every query filters `StudentId == current user`. | PRD §16 "View own progress". Same as `MasteryController`. |
| 17 | Options home | Add to the existing `ProgressOptions`: `WeakLessonCount` [Range(1,20)] = 4, `WeakObjectiveCount` [Range(1,20)] = 3, `HistoryMaxPageSize` [Range(1,100)] = 50. | Skill §8.1: caps live in Options. The section already exists. |
| 18 | Seeding UnitExam sessions in tests | `ProgressTestData.InsertUnitExamSessionAsync` inserts rows with parameterised `Database.ExecuteSqlAsync($"...")`. | No domain factory for exams exists until #81, and the builders must not use reflection. The raw-SQL rule applies to production code. Interpolated `ExecuteSqlAsync` is parameterised. |
| 19 | Web table on mobile | Tables sit inside a Card with `overflow-x-auto` (the `AuditLogTable` pattern). | `.claude/design-system.md` Table: "horizontal scroll inside the card on mobile". It wins over the generic skill §17. |
| 20 | Unit-exam score display | `Math.round(Number(score))` + "٪", shown as "—" when null. | Same rounding as `QuizResultSummary`. |

### Morabh reuse
| Piece | Source |
|---|---|
| `PageData<T>`, `IRepository<T>.FindPaginatedAsync` | Morabh `Core/Core.DDD/Models/PageData.cs`, `Core/Core.EntityFrameworkCore/Repositories/Repository.cs` (already vendored in `api/core-libraries`) |
| A paged list scoped to the current user | Pattern of Morabh `Core/Core.Notifications/ListNotifications/ListNotificationsQueryHandler.cs`. The Elmanhg precedent is `GetAuditLogsHandler`. |
| Everything else (weak spots, unit-exam best, progress results, web page) | New. There is no Morabh equivalent. |

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Domain/Mastery/IQuestionMasteryRepository.cs` | Add `Task<List<ObjectiveMasteryCount>> GetObjectiveCountsAsync(Guid studentId, CancellationToken cancellationToken);` |
| `api/Elmanhg.Infrastructure/Mastery/QuestionMasteryRepository.cs` | Implement `GetObjectiveCountsAsync` (see Files #1 contract). |
| `api/Elmanhg.Domain/Sessions/ISessionRepository.cs` | Add `Task<List<UnitExamBestScore>> GetBestUnitExamScoresAsync(Guid studentId, CancellationToken cancellationToken);` |
| `api/Elmanhg.Infrastructure/Sessions/SessionRepository.cs` | Implement: `_dbSet.Where(x => x.StudentId == studentId && x.Kind == SessionKind.UnitExam && !x.IsTestMode && x.SubmittedAt != null && x.ScorePercent != null).GroupBy(x => x.ScopeKey).Select(x => new UnitExamBestScore(x.Key, x.Max(session => session.ScorePercent) ?? 0m)).AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false)` (one operator per line). |
| `api/Elmanhg.Domain/Sessions/QuizScope.cs` | Add `public static QuizScope FromJson(string json) => JsonSerializer.Deserialize<QuizScope>(json, QuestionJson.SerializerOptions) ?? throw new InvalidOperationException("Quiz scope is empty.");` |
| `api/Elmanhg.Application/Shared/Options/ProgressOptions.cs` | Add `[Range(1, 20)] public int WeakLessonCount { get; set; } = 4;`, `[Range(1, 20)] public int WeakObjectiveCount { get; set; } = 3;`, `[Range(1, 100)] public int HistoryMaxPageSize { get; set; } = 50;` |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | Under `// SESSIONS` add three constants (see Error codes). |
| `api/Elmanhg.Api/Resources/Messages.ar.resx`, `Messages.en.resx` | Add three keys (see Error codes). |
| `api/Elmanhg.Api/appsettings.example.json` | The `Progress` line becomes `{ "StreakTimeZone": "Africa/Cairo", "StreakMaxDays": 365, "WeakLessonCount": 4, "WeakObjectiveCount": 3, "HistoryMaxPageSize": 50 }`. |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | Add `["Progress:WeakLessonCount"] = "4"`, `["Progress:WeakObjectiveCount"] = "3"`, `["Progress:HistoryMaxPageSize"] = "50"` to the existing settings dictionary, next to `Progress:StreakMaxDays`. |
| `api/Elmanhg.Tests/Application/Features/Mastery/ProgressOptionsTests.cs` | **Add** two tests (see Test plan #A13–A14). Existing tests are untouched. |
| `api/openapi/v1.json` | Regenerated by `dotnet build`. |
| `postman/elmanhg.postman_collection.json` | New folder `Progress` after `Mastery`, with three student requests: `GET {{baseUrl}}/api/progress/subjects`, `GET {{baseUrl}}/api/progress/weak-spots`, `GET {{baseUrl}}/api/progress/sessions?kind=Quiz&pageNumber=1&pageSize=20`. Same auth setup as the `Mastery` folder. |
| `web/orval.config.ts` | In `apiZod.output.override.operations` add `GetSessionHistory: { zod: { generate: { query: false } } },` (the same as the other paged lists). |
| `web/src/shared/api/generated/**` | Regenerated by `npm --prefix web run gen:api` (new `progress/progress.ts`, `progress/progress.msw.ts`, models, zod). Never hand-edited. |
| `web/src/routes/student/progress.tsx` | `createFileRoute('/student/progress')({ validateSearch: progressSearchSchema, component: ProgressPage })`, imported from `@/features/progress`. |
| `web/src/app/i18n.ts` | Import `progressLocales` from `@/features/progress/locales`. Add `progress: progressLocales.ar` / `.en` to resources and `'progress'` to `ns`. |
| `web/src/features/mastery/index.ts` | Also export `HeadlineCounterCard` and `MasteryBar` (reused by progress through the barrel). |
| `web/src/features/mastery/api/invalidateMastery.ts` | Add `export const progressQueryPrefix = '/api/progress';`. The predicate becomes `typeof first === 'string' && (first.startsWith(masteryQueryPrefix) \|\| first.startsWith(progressQueryPrefix))`. |
| `web/src/features/mastery/api/invalidateMastery.test.ts` | **Add** one `it` (see W12). The existing `it` is untouched. |
| `web/src/shared/i18n/en.json`, `ar.json` | Under `errors` add the three new codes (the same text as resx). |
| `docs/mastery.md` | In the intro, "progress (#78)" links to `docs/progress.md`. Add to "Access": "The progress endpoints (`docs/progress.md`) use the same policy." |
| `docs/sessions.md` | In the Model table `ScopeKey` row: "exams use `unit:<guid>` through `UnitExamScope` (Domain), already read by progress best scores (#78)". Under API, add one line: "History: `GET /api/progress/sessions` (`docs/progress.md`)". |
| `docs/claude-design-prompt.md` §4 | The `#/student/progress` bullet becomes: "per subject mastery with a unit table (mastery, best unit-exam score), weak lessons and weak objectives with "درّب الآن", session history filterable by all / quizzes / exams, paged." |

## Files to create

### API — Domain
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `api/Elmanhg.Domain/Mastery/ObjectiveMasteryCount.cs` | record | `namespace Elmanhg.Domain.Mastery; public sealed record ObjectiveMasteryCount(Guid SubjectId, int SubjectOrder, Guid UnitId, int UnitOrder, Guid LessonId, int LessonOrder, Guid ObjectiveId, int ObjectiveOrder, int ServableCount, int MasteredCount, int SeenCount);` |
| 2 | `api/Elmanhg.Domain/Mastery/WeakSpots.cs` | static class | `public static class WeakSpots`. `public static List<LessonMasteryCount> PickLessons(IEnumerable<LessonMasteryCount> lessons, int count)` filters `SeenCount > 0 && MasteredCount < ServableCount && ServableCount > 0`, orders `(decimal)MasteredCount / ServableCount`, then SubjectOrder, UnitOrder, LessonOrder, LessonId, then `.Take(count).ToList()`. `public static List<ObjectiveMasteryCount> PickObjectives(IEnumerable<ObjectiveMasteryCount> objectives, int count)` uses the same filter and the ratio, then SubjectOrder, UnitOrder, LessonOrder, ObjectiveOrder, ObjectiveId, Take. One-line comment: `// PRD §7.6 / prototype vProgress: attempted but not fully mastered, lowest mastery first.` |
| 3 | `api/Elmanhg.Domain/Sessions/UnitExamScope.cs` | record | `public sealed record UnitExamScope(Guid UnitId) { public string ToKey() => $"unit:{UnitId:D}"; public string ToJson() => JsonSerializer.Serialize(this, QuestionJson.SerializerOptions); public static UnitExamScope FromJson(string json) => JsonSerializer.Deserialize<UnitExamScope>(json, QuestionJson.SerializerOptions) ?? throw new InvalidOperationException("Unit exam scope is empty."); }` |
| 4 | `api/Elmanhg.Domain/Sessions/UnitExamBestScore.cs` | record | `public sealed record UnitExamBestScore(string ScopeKey, decimal BestScorePercent);` |
| 5 | `api/Elmanhg.Domain/Sessions/SessionHistoryKind.cs` | enum | `public enum SessionHistoryKind { Quiz, Exam }` |

`QuestionMasteryRepository.GetObjectiveCountsAsync` (Infrastructure, in the existing file) mirrors `GetLessonCountsAsync`:
`from question in servable join objective in _context.Set<LessonObjective>() on question.ObjectiveId equals (Guid?)objective.Id join lesson … join unit … join subject … from mastery in _dbSet.Where(x => x.StudentId == studentId && x.QuestionId == question.Id).DefaultIfEmpty() select new { …, ObjectiveId = objective.Id, ObjectiveOrder = objective.Order, Seen, Mastered }`
It then groups by (SubjectId, SubjectOrder, UnitId, UnitOrder, LessonId, LessonOrder, ObjectiveId, ObjectiveOrder) and returns `new ObjectiveMasteryCount(..., x.Count(), x.Sum(Mastered), x.Sum(Seen))`, `AsNoTracking`, `ToListAsync`. There is no subject filter parameter.

### API — Application (`Elmanhg.Application/Progress/...`)
| # | Path | Type | Contract |
|---|------|------|----------|
| 6 | `Progress/GetSubjectProgress/GetSubjectProgressQuery.cs` | query | `namespace Elmanhg.Application.Progress.GetSubjectProgress; public sealed record GetSubjectProgressQuery : IRequest<List<SubjectProgressResult>>;` |
| 7 | `Progress/GetSubjectProgress/GetSubjectProgressHandler.cs` | handler | `public sealed class GetSubjectProgressHandler(IQuestionMasteryRepository questionMasteryRepository, ISessionRepository sessionRepository, ISubjectRepository subjectRepository, ICurriculumUnitRepository unitRepository, ICurrentUserService currentUserService) : IRequestHandler<GetSubjectProgressQuery, List<SubjectProgressResult>>`. Steps: (1) if `UserId` is null or default, throw `UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated)`; (2) `counts = GetLessonCountsAsync(userId, null)`; (3) `subjects = subjectRepository.GetAllAsync(ct, orderBy: q => q.OrderBy(x => x.Order).ThenBy(x => x.CreationDate), asNoTracking: true) ?? []`; (4) `units = unitRepository.GetAllAsync(ct, orderBy: same, asNoTracking: true) ?? []`; (5) `bests = sessionRepository.GetBestUnitExamScoresAsync(userId)`; (6) `return SubjectProgressResultGenerator.Generate(subjects, units, counts, bests)`. |
| 8 | `Progress/GetWeakSpots/GetWeakSpotsQuery.cs` | query | `public sealed record GetWeakSpotsQuery : IRequest<WeakSpotsResult>;` |
| 9 | `Progress/GetWeakSpots/GetWeakSpotsHandler.cs` | handler | `public sealed class GetWeakSpotsHandler(IQuestionMasteryRepository questionMasteryRepository, ILessonRepository lessonRepository, ISubjectRepository subjectRepository, IOptions<ProgressOptions> progressOptions, ICurrentUserService currentUserService) : IRequestHandler<GetWeakSpotsQuery, WeakSpotsResult>`. Steps: (1) guard the user (as in #7); (2) `lessonCounts = GetLessonCountsAsync(userId, null)`; (3) `objectiveCounts = GetObjectiveCountsAsync(userId)`; (4) `weakLessons = WeakSpots.PickLessons(lessonCounts, options.WeakLessonCount)`, `weakObjectives = WeakSpots.PickObjectives(objectiveCounts, options.WeakObjectiveCount)`; (5) `lessonIds` = distinct union of both LessonIds; when empty, `return new WeakSpotsResult([], [])` without further reads; (6) `lessons = lessonRepository.FindAsync(x => lessonIds.Contains(x.Id), ct, include: q => q.Include(x => x.Objectives), asNoTracking: true)`; (7) `subjectIds` = distinct SubjectIds of both lists; `subjects = subjectRepository.FindAsync(x => subjectIds.Contains(x.Id), ct, asNoTracking: true)`; (8) `return WeakSpotsResultGenerator.Generate(weakLessons, weakObjectives, lessons, subjects)`. |
| 10 | `Progress/GetSessionHistory/GetSessionHistoryQuery.cs` | query | `public sealed record GetSessionHistoryQuery(SessionHistoryKind? Kind, int PageNumber = 1, int PageSize = 20) : IRequest<PageData<SessionHistoryItemResult>>;` |
| 11 | `Progress/GetSessionHistory/GetSessionHistoryValidator.cs` | validator | `public sealed class GetSessionHistoryValidator : AbstractValidator<GetSessionHistoryQuery>`; ctor `(IOptions<ProgressOptions> progressOptions)`. Rules: `RuleFor(x => x.PageNumber).ValidateMin(1, ErrorCodes.SessionHistoryPageNumberInvalid)`; `RuleFor(x => x.PageSize).ValidateRange(1, options.HistoryMaxPageSize, ErrorCodes.SessionHistoryPageSizeInvalid)`; `RuleFor(x => x.Kind).IsInEnum().WithErrorCode(ErrorCodes.SessionHistoryKindInvalid)`. |
| 12 | `Progress/GetSessionHistory/GetSessionHistoryFilter.cs` | static class | `public static class GetSessionHistoryFilter { public static Expression<Func<Session, bool>> Build(Guid studentId, SessionHistoryKind? kind) }`. Body: `var quizOnly = kind == SessionHistoryKind.Quiz; var examOnly = kind == SessionHistoryKind.Exam; return x => x.StudentId == studentId && !x.IsTestMode && (!quizOnly \|\| x.Kind == SessionKind.Quiz) && (!examOnly \|\| x.Kind != SessionKind.Quiz);` |
| 13 | `Progress/GetSessionHistory/GetSessionHistoryHandler.cs` | handler | `public sealed class GetSessionHistoryHandler(ISessionRepository sessionRepository, ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, ICurrentUserService currentUserService) : IRequestHandler<GetSessionHistoryQuery, PageData<SessionHistoryItemResult>>`. Steps: (1) guard the user; (2) `page = sessionRepository.FindPaginatedAsync(request.PageNumber, request.PageSize, ct, filter: GetSessionHistoryFilter.Build(userId, request.Kind), orderBy: q => q.OrderByDescending(x => x.StartedAt).ThenByDescending(x => x.Id), asNoTracking: true)`; (3) `lessonIds = page.Items.Where(Kind == Quiz).Select(x => QuizScope.FromJson(x.Scope).LessonId).Distinct().ToList()`; `unitIds` the same for `UnitExam` with `UnitExamScope.FromJson`; (4) `lessons` = `lessonIds.Count == 0 ? [] : lessonRepository.FindAsync(x => lessonIds.Contains(x.Id), ct, asNoTracking: true)`; `units` the same with `unitRepository`; (5) return `new PageData<SessionHistoryItemResult> { Items = page.Items.Select(x => SessionHistoryResultGenerator.Generate(x, lessons, units)).ToList(), PageNumber = page.PageNumber, PageSize = page.PageSize, TotalItems = page.TotalItems, TotalPages = page.TotalPages }`. |
| 14 | `Progress/Shared/SubjectProgressResult.cs` | result (client) | `public sealed record SubjectProgressResult(Guid SubjectId, string Name, int ServableCount, int MasteredCount, int SeenCount, int MasteryPercent, List<UnitProgressResult> Units);` Names are plain strings (no `LocalizedText`), so there is no `.Localized()`. |
| 15 | `Progress/Shared/UnitProgressResult.cs` | result (client) | `public sealed record UnitProgressResult(Guid UnitId, string Name, int ServableCount, int MasteredCount, int SeenCount, int MasteryPercent, decimal? BestExamScorePercent);` |
| 16 | `Progress/Shared/SubjectProgressResultGenerator.cs` | static | `public static List<SubjectProgressResult> Generate(IReadOnlyList<Subject> subjects, IReadOnlyList<CurriculumUnit> units, IReadOnlyCollection<LessonMasteryCount> counts, IReadOnlyCollection<UnitExamBestScore> bests)`. It builds `bestByKey = bests.ToDictionary(x => x.ScopeKey, x => x.BestScorePercent)`. For each subject (input order), `totals = MasteryTotals.Of(counts.Where(x => x.SubjectId == subject.Id))`, and its units are `units.Where(x => x.SubjectId == subject.Id)` (input order). Each unit gets `MasteryTotals.Of(counts.Where(x => x.UnitId == unit.Id))` and `BestExamScorePercent = bestByKey.TryGetValue(new UnitExamScope(unit.Id).ToKey(), out var best) ? best : null`. Private helpers: `GenerateSubject`, `GenerateUnit`. |
| 17 | `Progress/Shared/WeakSpotsResult.cs` | result (client) | `public sealed record WeakSpotsResult(List<WeakLessonResult> Lessons, List<WeakObjectiveResult> Objectives);` |
| 18 | `Progress/Shared/WeakLessonResult.cs` | result (client) | `public sealed record WeakLessonResult(Guid LessonId, string LessonName, Guid SubjectId, string SubjectName, int ServableCount, int MasteredCount, int SeenCount, int MasteryPercent);` |
| 19 | `Progress/Shared/WeakObjectiveResult.cs` | result (client) | `public sealed record WeakObjectiveResult(Guid ObjectiveId, string Text, Guid LessonId, string LessonName, Guid SubjectId, string SubjectName, int ServableCount, int MasteredCount, int SeenCount, int MasteryPercent);` |
| 20 | `Progress/Shared/WeakSpotsResultGenerator.cs` | static | `public static WeakSpotsResult Generate(IReadOnlyList<LessonMasteryCount> weakLessons, IReadOnlyList<ObjectiveMasteryCount> weakObjectives, IReadOnlyList<Lesson> lessons, IReadOnlyList<Subject> subjects)`. It keeps pick order. An entry whose lesson, subject or (for objectives) `lesson.Objectives` entry with that id is missing is **skipped**, which handles a race with delete. Percent = `new MasteryTotals(servable, mastered, seen).MasteryPercent`. |
| 21 | `Progress/Shared/SessionHistoryItemResult.cs` | result (client) | `public sealed record SessionHistoryItemResult(Guid Id, string Kind, Guid? LessonId, Guid? UnitId, string? ScopeName, DateTimeOffset StartedAt, DateTimeOffset? SubmittedAt, decimal? ScorePercent);` |
| 22 | `Progress/Shared/SessionHistoryResultGenerator.cs` | static | `public static SessionHistoryItemResult Generate(Session session, IReadOnlyList<Lesson> lessons, IReadOnlyList<CurriculumUnit> units)`. It uses a switch expression on `session.Kind`: `Quiz` → `lessonId = QuizScope.FromJson(session.Scope).LessonId`, name = `lessons.FirstOrDefault(x => x.Id == lessonId)?.Name`; `UnitExam` → `unitId` through `UnitExamScope.FromJson`, name from `units`; `_` → all null. `Kind = session.Kind.ToString()`. |

### API — controller
| # | Path | Type | Contract |
|---|------|------|----------|
| 23 | `api/Elmanhg.Api/Controllers/Progress/ProgressController.cs` | controller | `namespace Elmanhg.Api.Controllers.Progress; [ApiController] [Route("api/progress")] [Authorize] public class ProgressController(IMediator mediator) : ControllerBase` with three actions (see API surface). Each has `[Authorize(Policy = DefaultCodes.ProgressViewOwn)]` and `[ProducesResponseType<T>(StatusCodes.Status200OK)]`, and returns `Ok(result)`. No `Requests.cs` (query params bind directly). |

### API — tests
| # | Path |
|---|------|
| 24 | `api/Elmanhg.Tests/Domain/Mastery/WeakSpotsTests.cs` |
| 25 | `api/Elmanhg.Tests/Domain/Sessions/SessionScopeTests.cs` |
| 26 | `api/Elmanhg.Tests/Application/Features/Progress/GetSubjectProgress/GetSubjectProgressHandlerTests.cs` |
| 27 | `api/Elmanhg.Tests/Application/Features/Progress/GetWeakSpots/GetWeakSpotsHandlerTests.cs` |
| 28 | `api/Elmanhg.Tests/Application/Features/Progress/GetSessionHistory/GetSessionHistoryHandlerTests.cs` |
| 29 | `api/Elmanhg.Tests/Application/Features/Progress/GetSessionHistory/GetSessionHistoryValidatorTests.cs` |
| 30 | `api/Elmanhg.Tests/Application/Features/Progress/GetSessionHistory/GetSessionHistoryFilterTests.cs` |
| 31 | `api/Elmanhg.Tests/Integration/Progress/ProgressTestData.cs`: `public static class ProgressTestData` with `public const string Route = "/api/progress";` and these members. `Task<JsonElement> GetJsonAsync(HttpClient client, string relativePath)` asserts 200 and returns the body. `Task<(Guid LessonId, Guid ObjectiveId)> SeedPublishedLessonWithObjectiveAsync(ApiFactory factory, Guid unitId, string name, int order, string objectiveText, CancellationToken cancellationToken)` does `Lesson.Create`, then `Update(name, "", "", null, [new LessonObjectiveContent(null, objectiveText)], creator)`, `Publish(creator)`, `ClearDomainEvents()` and save. `Task<Guid> SeedObjectiveQuestionAsync(ApiFactory factory, Guid lessonId, Guid objectiveId, CancellationToken cancellationToken)` is a copy of `QuestionTestData.SeedQuestionAsync(approved: true)` with `new QuestionMetadata(QuestionDifficulty.Medium, objectiveId, [])`. `Task<Guid> InsertUnitExamSessionAsync(ApiFactory factory, Guid studentId, Guid unitId, decimal? scorePercent, bool submitted, bool isTestMode, DateTimeOffset startedAt)` runs `context.Database.ExecuteSqlAsync($"INSERT INTO \"Sessions\" (\"Id\",\"CreatedBy\",\"CreationDate\",\"IsDeleted\",\"IsTestMode\",\"Kind\",\"LastActivityAt\",\"Scope\",\"ScopeKey\",\"ScorePercent\",\"StartedAt\",\"StudentId\",\"SubmittedAt\",\"UpdationDate\") VALUES ({id},{studentId},{startedAt},false,{isTestMode},'UnitExam',{startedAt},CAST({scope.ToJson()} AS jsonb),{scope.ToKey()},{scorePercent},{startedAt},{studentId},{submittedAtOrNull},{startedAt})")` with `scope = new UnitExamScope(unitId)`, and returns the id. |
| 32 | `api/Elmanhg.Tests/Integration/Progress/SubjectProgressEndpointTests.cs` |
| 33 | `api/Elmanhg.Tests/Integration/Progress/WeakSpotsEndpointTests.cs` |
| 34 | `api/Elmanhg.Tests/Integration/Progress/SessionHistoryEndpointTests.cs` |

Integration classes use the primary constructor `(ApiFactory factory)`, like `MasteryOverviewEndpointTests`, and reuse `SessionTestData.SignedInStudentAsync`, `MasteryTestData.PracticeAsync`, `ContentTestData.Seed*`, `QuestionTestData.SeedQuestionAsync`, `ScopeTestData.SeedTeacherAsync/SignedInClientAsync` and `AuthTestClient.Create`. MCQ answer `"b"` is correct and `"a"` is wrong.

### Web — `web/src/features/progress/`
| # | Path | Contract |
|---|------|----------|
| 35 | `index.ts` | `export { ProgressPage } from './pages/ProgressPage'; export { progressSearchSchema } from './schemas/progressSearchSchema'; export type { ProgressSearch } from './schemas/progressSearchSchema'; export { progressLocales } from './locales';` |
| 36 | `locales.ts` | `import ar/en from './i18n/*.json'; export const progressLocales = { ar, en };` |
| 37–38 | `i18n/en.json`, `i18n/ar.json` | Keys and values in "Web strings" below. Both files have the same key set. |
| 39 | `schemas/progressSearchSchema.ts` | `export const progressSearchSchema = z.object({ kind: z.enum(['Quiz', 'Exam']).optional().catch(undefined), page: z.coerce.number().int().min(1).optional().catch(undefined) }); export type ProgressSearch = z.infer<typeof progressSearchSchema>;` |
| 40 | `schemas/progressSearchSchema.test.ts` | W1–W4 |
| 41 | `api/sessionHistory.ts` | `export const historyPageSize = 20;`. `export function toSessionHistoryParams(search: ProgressSearch): GetSessionHistoryParams` returns `{ pageNumber: search.page ?? 1, pageSize: historyPageSize, ...(search.kind ? { kind: search.kind } : {}) }` (use the generated params type name). `export type SessionLink = { to: '/student/quiz-result/$sessionId' \| '/student/quiz/$sessionId'; labelKey: 'history.view' \| 'history.continue' } \| null;` `export function sessionLink(item: SessionHistoryItemResult): SessionLink` gives Quiz with `submittedAt` → result/view, Quiz open → quiz/continue, anything else → null. `export function sessionKindLabelKey(kind: string): string` maps `'Quiz'` → `'history.kindQuiz'`, `'UnitExam'` → `'history.kindUnitExam'` and anything else → `'history.kindMultiUnitExam'`. |
| 42 | `api/sessionHistory.test.ts` | W5–W9 |
| 43 | `hooks/useProgressSearch.ts` | `getRouteApi('/student/progress')`. Returns `{ search, setKind(kind: 'Quiz' \| 'Exam' \| undefined), setPage(page: number), clearFilter() }`. `setKind` navigates `search: kind ? { kind, page: 1 } : { page: 1 }`. `setPage` merges the previous search. `clearFilter` navigates `search: {}`. |
| 44 | `hooks/useSessionHistory.ts` | `useGetSessionHistory(toSessionHistoryParams(search), { query: { placeholderData: keepPreviousData, staleTime: 0 } })`. |
| 45 | `pages/ProgressPage.tsx` | `<section className="flex flex-col gap-4">`: `<h1>` `t('page.title')` (h1 classes as `StudentHomePage`), then `<ProgressSummary/>`, `<SubjectProgressSection/>`, `<WeakSpotsSection/>`, `<SessionHistorySection/>`. |
| 46 | `pages/ProgressPage.test.tsx` | W13–W22 |
| 47 | `pages/ProgressPage.history.test.tsx` | W23–W30 |
| 48 | `components/ProgressSummary.tsx` | `useGetMasteryOverview()`. Pending → `ContentListSkeleton label={t('summary.loading')}`. Error → `ContentErrorState title={t('summary.errorTitle')} onRetry={refetch}`. Success → `<HeadlineCounterCard headline streakDays>` (from the `@/features/mastery` barrel). |
| 49 | `components/SubjectProgressSection.tsx` | `<section aria-labelledby>` with an h2 `t('subjects.title')`. `useGetSubjectProgress()` shows the skeleton, error and retry, and when `[]` the text `t('subjects.empty')`. Otherwise a `<ul className="grid grid-cols-1 gap-3 lg:grid-cols-2">` with one `<li><SubjectProgressCard/></li>` per subject. |
| 50 | `components/SubjectProgressCard.tsx` | `<article aria-labelledby={id}>` Card (`rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5`). It holds an h3 with the subject name, `MasteryBar percent label={t('subjects.barLabel',{name})}`, the caption `t('subjects.mastery',{percent})` and `<UnitProgressTable units subjectName/>`. |
| 51 | `components/UnitProgressTable.tsx` | `overflow-x-auto` wrapper with `<table>` and `<caption className="sr-only">{t('subjects.unitsCaption',{name})}</caption>`. Headers (`scope="col"`, caption-600 muted, `px-2.5 py-2.25 text-start`): `subjects.unit`, `subjects.unitMastery`, `subjects.bestExam`. One row per unit: name; `t('subjects.unitMasteryValue',{percent})`; `bestExamScorePercent == null ? t('subjects.noExam') : t('subjects.bestExamValue',{score: Math.round(Number(value))})`. Row hairline `border-t border-border`. |
| 52 | `components/WeakSpotsSection.tsx` | `<section aria-labelledby>` with an h2 `t('weakSpots.title')`. `useGetWeakSpots()` shows the skeleton, error and retry. When both lists are empty it shows `<p>{t('weakSpots.empty')}</p>`. Otherwise `<WeakLessonList>` when lessons are non-empty (h3 `weakSpots.lessons`) and `<WeakObjectiveList>` when objectives are non-empty (h3 `weakSpots.objectives`). |
| 53 | `components/WeakLessonList.tsx` | `<ul className="flex flex-col gap-2">`. Each `<li>` is a list item (`rounded-md bg-surface border border-border px-3.5 py-3`) with the lesson name (`text-ui font-semibold`), the caption `subjectName`, the caption `t('weakSpots.mastery',{percent})`, `MasteryBar label={t('weakSpots.barLabel',{name})}` and `<Button asChild size="sm" variant="secondary"><Link to="/student/lesson/$lessonId/practice" params={{lessonId}}>{t('weakSpots.train')}</Link></Button>`. |
| 54 | `components/WeakObjectiveList.tsx` | The same item layout: the objective `text` (semibold), the caption `t('weakSpots.objectiveLesson',{lesson: lessonName, subject: subjectName})`, the mastery caption and bar, and "درّب الآن" linking to the objective's `lessonId` practice. |
| 55 | `components/SessionHistorySection.tsx` | `<section aria-labelledby>` with an h2 `t('history.title')`, then `<SessionKindFilter value={search.kind} onChange={setKind}/>`. Content: pending → skeleton `history.loading`; error → error and retry `history.errorTitle`; `items.length === 0` → `<SessionHistoryEmptyState variant={search.kind ? 'no-results' : 'no-data'} onClear={clearFilter}/>`; else `<SessionHistoryTable items/>` plus `Pagination` (from `@/shared/components/Pagination`) when `totalPages > 1`, with `page={Number(data.pageNumber)}` and `totalPages={Number(data.totalPages)}`. |
| 56 | `components/SessionKindFilter.tsx` | `Label` plus a native `<select>` (the classes of `ResourceTypeField`), with `useId`. Options: `''` → `history.all`, `'Quiz'` → `history.quizzes`, `'Exam'` → `history.exams`. `onChange(event.target.value === 'Quiz' \|\| event.target.value === 'Exam' ? event.target.value : undefined)`. Props `{ value: 'Quiz' \| 'Exam' \| undefined; onChange: (kind: 'Quiz' \| 'Exam' \| undefined) => void }`. |
| 57 | `components/SessionHistoryTable.tsx` | Card `overflow-x-auto` with a table and `caption.sr-only` `history.caption`. Headers: `history.date`, `history.kind`, `history.scope`, `history.score`, `history.actions`. Rows are `<SessionHistoryRow key={item.id}/>`. |
| 58 | `components/SessionHistoryRow.tsx` | Date: `formatDate(new Date(item.startedAt), i18n.language, 'arabic-indic', { dateStyle: 'medium', timeStyle: 'short' })`. Kind: `t(sessionKindLabelKey(item.kind))`. Scope: `item.scopeName ?? t('history.unknownScope')`. Score: when `submittedAt` is set, `t('history.scoreValue',{score: Math.round(Number(item.scorePercent ?? 0))})`; otherwise a pending badge `<span className="rounded-full bg-soft px-2.5 py-0.5 text-micro font-semibold text-text-muted">{t('history.inProgress')}</span>`. Action: `sessionLink(item)` → `<Link to={link.to} params={{ sessionId: item.id }} className="text-accent …focus ring">{t(link.labelKey)}</Link>`, or empty. |
| 59 | `components/SessionHistoryEmptyState.tsx` | Mirrors `AuditLogEmptyState`: a `History` Lucide icon, `t(variant==='no-data' ? 'history.empty.noData' : 'history.empty.noResults')`, and for no-results `<Button variant="primary" onClick={onClear}>{t('history.empty.clear')}</Button>`. |
| 60 | `web/src/test/progressFixtures.ts` | `export const algebraUnitId`, `mechanicsUnitId`, `weakLessonId`, `weakObjectiveLessonId`, `finishedQuizId`, `openQuizId`, `examSessionId` (fixed v4-shaped uuids). `subjectProgress(overrides?)`: Physics (40 %) with units Mechanics (50 %, best 87.5) and Waves (0 %, best null), plus Chemistry (0 %, one unit). `weakSpots(overrides?)`: lessons [Ohm's law 20 % / Physics], objectives [State Ohm's law 0 %, lesson Ohm's law]. `sessionHistoryPage(items?, overrides?)` is a PageData with default items: an open quiz "Ohm's law" (newest), a finished quiz "Newton's laws" scored 72.4, and a UnitExam "Mechanics" scored 90. `pageNumber 1`, `pageSize 20`, `totalItems 3`, `totalPages 1`. |

### Docs
| # | Path | Content |
|---|------|---------|
| 61 | `docs/progress.md` | Sections: the page (`/student/progress`: summary from `/api/mastery/overview`, subjects, weak spots, history); Subject progress (Decisions 4–5, UnitExam best rule, `unit:<guid>` via `UnitExamScope`); Weak spots (Decisions 7–9 rules and ordering, option keys); History (Decisions 10–13, ordering, paging, scope names, links); API table (three routes, responses as in API surface); Error codes table (three codes, 422); Options table (`Progress:WeakLessonCount` 4, `Progress:WeakObjectiveCount` 3, `Progress:HistoryMaxPageSize` 50); Access (`Progress.ViewOwn`, own data only, test-mode excluded); Freshness (Decision 15). |

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `SessionHistoryPageNumberInvalid` | `SESSION_HISTORY_PAGE_NUMBER_INVALID` | `GetSessionHistoryValidator` | validation pipeline | 422 |
| `SessionHistoryPageSizeInvalid` | `SESSION_HISTORY_PAGE_SIZE_INVALID` | `GetSessionHistoryValidator` | validation pipeline | 422 |
| `SessionHistoryKindInvalid` | `SESSION_HISTORY_KIND_INVALID` | `GetSessionHistoryValidator` | validation pipeline | 422 |
| (reused) `UserNotAuthenticated` | `USER_NOT_AUTHENTICATED` | all three handlers | `UnauthorizedCoreException` | 401 |

| Key | ar (resx, no tashkeel) | en |
|---|---|---|
| `SESSION_HISTORY_PAGE_NUMBER_INVALID` | `رقم الصفحة يجب ان يكون 1 او اكثر.` | `The page number must be 1 or more.` |
| `SESSION_HISTORY_PAGE_SIZE_INVALID` | `حجم الصفحة خارج النطاق المسموح.` | `The page size is out of the allowed range.` |
| `SESSION_HISTORY_KIND_INVALID` | `نوع الجلسة غير صالح.` | `The session type filter is not valid.` |

The web `shared/i18n` gets the same three keys. For ar use `رقم الصفحة يجب أن يكون ١ أو أكثر.`, `حجم الصفحة خارج النطاق المسموح.` and `نوع الجلسة غير صالح.`.

## Domain behaviour
No entity state changes. There are no commands, no `SaveChangesAsync`, no auditing and no migration (the new queries use existing tables and indexes: `IX_Sessions_StudentId_StartedAt`, `IX_QuestionMasteries_StudentId_QuestionId`). The new domain members are pure:
- `WeakSpots.PickLessons` / `PickObjectives`: see Files #2. There are no exceptions, and a `count` below 1 is prevented by Options validation.
- `QuizScope.FromJson` / `UnitExamScope.FromJson`: deserialise with `QuestionJson.SerializerOptions`. JSON `null` → `InvalidOperationException` (an invariant, like `SessionResultGenerator.FindRevision`). A round trip of `ToJson()` returns an equal record.
- `UnitExamScope.ToKey()` = `$"unit:{UnitId:D}"` (lowercase D-format guid, the same as `QuizScope`).

## API surface
| Method | Route | Name (operationId) | Policy | Request | Response |
|---|---|---|---|---|---|
| GET | `/api/progress/subjects` | `GetSubjectProgress` | `DefaultCodes.ProgressViewOwn` | — | 200 `List<SubjectProgressResult>` |
| GET | `/api/progress/weak-spots` | `GetWeakSpots` | `DefaultCodes.ProgressViewOwn` | — | 200 `WeakSpotsResult` |
| GET | `/api/progress/sessions` | `GetSessionHistory` | `DefaultCodes.ProgressViewOwn` | `[FromQuery] SessionHistoryKind? kind, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20` → `new GetSessionHistoryQuery(kind, pageNumber, pageSize)` | 200 `PageData<SessionHistoryItemResult>` |

Every route returns 401 when anonymous and 403 for a Teacher or Admin.

## Web strings (`features/progress/i18n`)
| Key | en | ar |
|---|---|---|
| page.title | My progress | تقدّمي |
| summary.loading | Loading your summary… | جارٍ تحميل ملخّصك… |
| summary.errorTitle | Could not load your summary. | تعذّر تحميل ملخّصك. |
| subjects.title | Subjects | المواد |
| subjects.loading | Loading subject progress… | جارٍ تحميل تقدّم المواد… |
| subjects.errorTitle | Could not load subject progress. | تعذّر تحميل تقدّم المواد. |
| subjects.empty | No subjects yet. | لا توجد مواد بعد. |
| subjects.mastery | {percent, number}% mastered | إتقان {percent, number}٪ |
| subjects.barLabel | {name} mastery | إتقان {name} |
| subjects.unitsCaption | {name} units | وحدات {name} |
| subjects.unit | Unit | الوحدة |
| subjects.unitMastery | Mastery | الإتقان |
| subjects.bestExam | Best exam | أفضل امتحان |
| subjects.unitMasteryValue | {percent, number}% | {percent, number}٪ |
| subjects.bestExamValue | {score, number}% | {score, number}٪ |
| subjects.noExam | — | — |
| weakSpots.title | Weak spots | نقاط الضعف |
| weakSpots.loading | Loading weak spots… | جارٍ تحميل نقاط الضعف… |
| weakSpots.errorTitle | Could not load weak spots. | تعذّر تحميل نقاط الضعف. |
| weakSpots.empty | No data yet. Practise a lesson to find your weak spots. | لا توجد بيانات بعد. تدرّب على درس لتظهر نقاط ضعفك. |
| weakSpots.lessons | Weakest lessons | أضعف الدروس |
| weakSpots.objectives | Weakest objectives | أضعف الأهداف |
| weakSpots.mastery | Mastery {percent, number}% | إتقان {percent, number}٪ |
| weakSpots.barLabel | {name} mastery | إتقان {name} |
| weakSpots.objectiveLesson | {lesson} · {subject} | {lesson} · {subject} |
| weakSpots.train | Train now | درّب الآن |
| history.title | Session history | سجل الجلسات |
| history.loading | Loading your sessions… | جارٍ تحميل جلساتك… |
| history.errorTitle | Could not load your sessions. | تعذّر تحميل جلساتك. |
| history.filterLabel | Type | النوع |
| history.all | All | الكل |
| history.quizzes | Quizzes | تدريبات |
| history.exams | Exams | امتحانات |
| history.caption | Your sessions, newest first | جلساتك، الأحدث أولًا |
| history.date | Date | التاريخ |
| history.kind | Type | النوع |
| history.scope | Scope | النطاق |
| history.score | Score | الدرجة |
| history.actions | Actions | إجراءات |
| history.kindQuiz | Quiz | تدريب |
| history.kindUnitExam | Unit exam | امتحان وحدة |
| history.kindMultiUnitExam | Multi-unit exam | متعدد الوحدات |
| history.inProgress | In progress | جارٍ |
| history.scoreValue | {score, number}% | {score, number}٪ |
| history.unknownScope | Unavailable | غير متاح |
| history.view | View | عرض |
| history.continue | Continue | متابعة |
| history.empty.noData | No sessions yet. | لا توجد جلسات بعد. |
| history.empty.noResults | No sessions of this type. | لا توجد جلسات من هذا النوع. |
| history.empty.clear | Show all | عرض الكل |

## Test plan

### API — domain (no doubles)
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| D1 | `WeakSpotsTests` | `PickLessons_UnseenOrFullyMastered_AreExcluded` | Of four lessons (unseen, fully mastered, 0 servable, partial), only the partial one is returned. |
| D2 | `WeakSpotsTests` | `PickLessons_Candidates_OrdersByLowestRatioThenCurriculumOrder` | 1/4 comes before 1/2, and two equal ratios follow subject, then unit, then lesson order. |
| D3 | `WeakSpotsTests` | `PickLessons_MoreThanCount_TakesCount` | Five candidates with count 4 → 4 returned, the lowest four. |
| D4 | `WeakSpotsTests` | `PickObjectives_UnseenOrFullyMastered_AreExcluded` | As D1, for objectives. |
| D5 | `WeakSpotsTests` | `PickObjectives_SameLessonAndRatio_OrdersByObjectiveOrder` | Two objectives in one lesson with equal ratio → objective order 1 first. |
| D6 | `WeakSpotsTests` | `PickObjectives_MoreThanCount_TakesCount` | Count 3 → 3 returned. |
| D7 | `SessionScopeTests` | `QuizScope_FromJson_RoundTripsLessonId` | `QuizScope.FromJson(new QuizScope(id).ToJson()).LessonId == id`. |
| D8 | `SessionScopeTests` | `UnitExamScope_ToKey_UsesUnitPrefixAndLowercaseGuid` | `"unit:" + id.ToString("D")`. |
| D9 | `SessionScopeTests` | `UnitExamScope_FromJson_RoundTripsUnitId` | Round-trip equality. |
| D10 | `SessionScopeTests` | `UnitExamScope_FromJsonNull_ThrowsInvalidOperation` | `"null"` → `InvalidOperationException`. |

### API — application (NSubstitute at repositories)
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| A1 | `GetSubjectProgressHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | `UnauthorizedCoreException` + `ErrorCodes.UserNotAuthenticated`, and `GetLessonCountsAsync` DidNotReceive. |
| A2 | `GetSubjectProgressHandlerTests` | `Handle_Subjects_ReturnsUnitsInOrderWithWeightedMastery` | Two subjects, each with units in order. Unit and subject counts and floor percent (e.g. 3/7 → 42) pool the lessons. |
| A3 | `GetSubjectProgressHandlerTests` | `Handle_UnitWithoutServableQuestions_ReturnsZeroCounts` | A unit with no counts → 0/0/0/0 %, and it is still listed. |
| A4 | `GetSubjectProgressHandlerTests` | `Handle_UnitExamBest_ReturnsBestOnlyForMatchingUnit` | `bests = [("unit:{A}", 87.5)]` → unit A `87.5m`, unit B `null`. |
| A5 | `GetWeakSpotsHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | Type + code. |
| A6 | `GetWeakSpotsHandlerTests` | `Handle_NothingAttempted_ReturnsEmptyListsWithoutLoadingLessons` | Both lists are empty, and `lessonRepository.FindAsync` DidNotReceive (the short-circuit is the behaviour). |
| A7 | `GetWeakSpotsHandlerTests` | `Handle_WeakLessons_ReturnsNamesSubjectAndPercentInPickOrder` | Lesson names and subject names are resolved, percent is floored, and the order is ascending mastery. |
| A8 | `GetWeakSpotsHandlerTests` | `Handle_WeakObjective_ReturnsObjectiveTextWithLesson` | `Text`, `LessonId`, `LessonName`, `SubjectName`, `MasteryPercent`. Uses `QuestionBuilder().Lesson` (it has one objective). |
| A9 | `GetWeakSpotsHandlerTests` | `Handle_LessonNoLongerLoaded_SkipsEntry` | The lesson repository returns [] → the weak lesson is omitted and there is no exception. |
| A10 | `GetWeakSpotsHandlerTests` | `Handle_ConfiguredCounts_LimitsBothLists` | Options `WeakLessonCount = 1`, `WeakObjectiveCount = 1` with 3 candidates each → 1 and 1. |
| A11 | `GetSessionHistoryHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | Type + code, and `FindPaginatedAsync` DidNotReceive. |
| A12 | `GetSessionHistoryHandlerTests` | `Handle_QuizSessions_ReturnsLessonNameScoreAndPaging` | A page of 2 sessions from `SessionBuilder` (one submitted via `Submit()`) → items keep order, `Kind == "Quiz"`, `LessonId` and `ScopeName` come from the lesson, `SubmittedAt`/`ScorePercent` are passed through, and `PageNumber`/`TotalItems`/`TotalPages` are copied. |
| A12b | `GetSessionHistoryHandlerTests` | `Handle_LessonDeleted_ReturnsNullScopeName` | The lesson repository returns [] → `ScopeName` is null and `LessonId` is still set. |
| A12c | `GetSessionHistoryHandlerTests` | `Handle_EmptyPage_DoesNotLoadLessonsOrUnits` | An empty page returns `Items` empty, and neither repository's `FindAsync` is called. |
| A13 | `ProgressOptionsTests` (add) | `AddApplication_DefaultProgressOptions_UseWeakSpotAndHistoryDefaults` | `(4, 3, 50)`. |
| A14 | `ProgressOptionsTests` (add) | `AddApplication_WeakLessonCountZero_ThrowsOptionsValidationException` | `Progress:WeakLessonCount = "0"` → throws. |
| A15 | `GetSessionHistoryValidatorTests` | `Validate_Defaults_Passes` | `new GetSessionHistoryQuery(null)` is valid. |
| A16 | `GetSessionHistoryValidatorTests` | `Validate_PageNumberZero_FailsWithPageNumberInvalid` | Error code. |
| A17 | `GetSessionHistoryValidatorTests` | `Validate_PageSizeZero_FailsWithPageSizeInvalid` | Error code. |
| A18 | `GetSessionHistoryValidatorTests` | `Validate_PageSizeAboveMax_FailsWithPageSizeInvalid` | Max 50, size 51 → code. |
| A19 | `GetSessionHistoryValidatorTests` | `Validate_KindOutOfRange_FailsWithKindInvalid` | `(SessionHistoryKind)9` → code. |
| A20 | `GetSessionHistoryFilterTests` | `Build_NoKind_MatchesOwnNonTestQuiz` | The compiled predicate is true for the student's quiz. |
| A21 | `GetSessionHistoryFilterTests` | `Build_KindQuiz_MatchesQuiz` | True. |
| A22 | `GetSessionHistoryFilterTests` | `Build_KindExam_DoesNotMatchQuiz` | False. |
| A23 | `GetSessionHistoryFilterTests` | `Build_OtherStudent_DoesNotMatch` | False. |
| A24 | `GetSessionHistoryFilterTests` | `Build_TestModeSession_DoesNotMatch` | `SessionBuilder.Build(isTestMode: true)` → false. |

### API — integration (Testcontainers, through HTTP)
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| I1 | `SubjectProgressEndpointTests` | `Get_Anonymous_Returns401` | 401. |
| I2 | `SubjectProgressEndpointTests` | `Get_Teacher_Returns403` | 403. |
| I3 | `SubjectProgressEndpointTests` | `Get_Admin_Returns403` | 403. |
| I4 | `SubjectProgressEndpointTests` | `Get_AfterPractice_ReturnsSubjectAndUnitMastery` | Seed a subject, a unit Mechanics with a published lesson and 2 approved questions, and a unit Waves with none. After q1 is correct twice and q2 wrong: the subject is 2/1/2 at 50 %, Mechanics is 50 % with `bestExamScorePercent` null, and Waves is 0/0/0 %. Units appear in order. |
| I5 | `SubjectProgressEndpointTests` | `Get_UnitExamSessions_ReturnsBestSubmittedNonTestScore` | Insert, for the student on Mechanics: submitted 60.5, submitted 80, open with 95, submitted test-mode 99. Insert another student's submitted 100. → `80.0`. |
| I6 | `WeakSpotsEndpointTests` | `Get_Anonymous_Returns401` | 401. |
| I7 | `WeakSpotsEndpointTests` | `Get_Teacher_Returns403` | 403. |
| I8 | `WeakSpotsEndpointTests` | `Get_NewStudent_ReturnsEmptyLists` | 200, `lessons: []` and `objectives: []`. |
| I9 | `WeakSpotsEndpointTests` | `Get_PracticedLessons_ReturnsLowestMasteryFirstAndSkipsMasteredAndUnseen` | Lessons: A (2 questions, 1 mastered), B (1 question answered wrong), C (1 question mastered) and D (unseen). → `[B, A]` with names, subjectName and percents 0 and 50. |
| I10 | `WeakSpotsEndpointTests` | `Get_ObjectiveQuestionAnsweredWrong_ReturnsObjectiveWithLesson` | Seed a published lesson with an objective and an objective question, then answer "a". → `objectives[0]` has the objectiveId, the text, the lessonId and 0 %. |
| I11 | `SessionHistoryEndpointTests` | `Get_Anonymous_Returns401` | 401. |
| I12 | `SessionHistoryEndpointTests` | `Get_Teacher_Returns403` | 403. |
| I13 | `SessionHistoryEndpointTests` | `Get_PageSizeAboveMax_Returns422WithCode` | `?pageSize=51` → 422, and the problem `code` is `SESSION_HISTORY_PAGE_SIZE_INVALID`. |
| I14 | `SessionHistoryEndpointTests` | `Get_KindOutOfRange_Returns422WithCode` | `?kind=9` → 422 `SESSION_HISTORY_KIND_INVALID`. |
| I15 | `SessionHistoryEndpointTests` | `Get_Sessions_ReturnsNewestFirstWithLessonNamesAndOpenState` | Finish a quiz on lesson A, then start (not finish) one on lesson B. → items `[B open (submittedAt null, scorePercent null), A (scopeName A, submittedAt set, scorePercent set)]`, and `totalItems` is 2. |
| I16 | `SessionHistoryEndpointTests` | `Get_KindExam_ReturnsOnlyExamsWithUnitName` | One quiz plus one inserted submitted UnitExam. `?kind=Exam` → 1 item, `kind` "UnitExam", `unitId`, `scopeName` = the unit name. `?kind=Quiz` → 1 quiz. |
| I17 | `SessionHistoryEndpointTests` | `Get_OtherStudentsSessions_AreNotReturned` | Student 2 sees `totalItems` 0 after student 1 practised. |
| I18 | `SessionHistoryEndpointTests` | `Get_SecondPage_ReturnsRemainingItem` | 2 sessions, `?pageSize=1&pageNumber=2` → 1 item (the older one), `totalPages` 2. |

### Web (Vitest + Testing Library + MSW; `renderApp('/student/progress…', { session: testSessions.student })`)
| # | File | Test (`it`) | Asserts |
|---|------|-------------|---------|
| W1 | `progressSearchSchema.test.ts` | accepts Quiz and Exam kinds | Parsed values are kept. |
| W2 | 〃 | drops an unknown kind | `{kind:'Essay'}` → `kind` undefined. |
| W3 | 〃 | coerces a numeric page string | `'2'` → 2. |
| W4 | 〃 | drops a page below 1 | `'0'` → undefined. |
| W5 | `sessionHistory.test.ts` | maps search to params with defaults | `{}` → `{pageNumber:1,pageSize:20}`, with no `kind` key. |
| W6 | 〃 | passes kind and page through | `{kind:'Exam',page:3}` → includes `kind:'Exam', pageNumber:3`. |
| W7 | 〃 | links a finished quiz to its result | `to` is the quiz-result route, `labelKey` `history.view`. |
| W8 | 〃 | links an open quiz to continue | The quiz route, `history.continue`. |
| W9 | 〃 | gives no link for exams | `UnitExam` → null. |
| W10 | 〃 | labels every session kind | Quiz, UnitExam and MultiUnitExam keys. |
| W12 | `invalidateMastery.test.ts` (add) | marks progress queries stale too | `['/api/progress/weak-spots']` is invalidated and `['/api/sessions/s']` is not. |
| W13 | `ProgressPage.test.tsx` | shows loading then the headline summary | A `status` named "Loading your summary…", then "40 of 60 questions left for you" and the streak meta. |
| W14 | 〃 | shows each subject with its units, mastery and best exam | `article` Physics: progressbar `value` 40, row Mechanics "50%" and "88%", row Waves "0%" and "—". |
| W15 | 〃 | shows the subjects empty state | `subjectProgress` [] → "No subjects yet." |
| W16 | 〃 | lists weak lessons with a Train now link to practice | Inside the list item "Ohm's law": the link "Train now" has `href` `/student/lesson/{weakLessonId}/practice`. |
| W17 | 〃 | lists weak objectives with their lesson and a Train now link | "State Ohm's law", "Ohm's law · Physics", and the link href. |
| W18 | 〃 | shows the weak-spots empty state | Both lists empty → "No data yet. Practise…". |
| W19 | 〃 | shows the subjects error and retries | The first `/api/progress/subjects` returns 500 (`once`) → `alert` "Could not load subject progress.", then Retry → the Physics article appears. |
| W20 | 〃 | shows the weak-spots error and retries | The same for `/api/progress/weak-spots`. |
| W21 | 〃 | renders right to left in Arabic | `lng:'ar'`: `dir="rtl"`, h1 "تقدّمي", a "درّب الآن" link, and "٥٠٪" in the Mechanics row. |
| W22 | 〃 | has no axe violations | After data loads, `axe(container).violations` is `[]`. |
| W23 | `ProgressPage.history.test.tsx` | shows sessions newest first with type, scope and score | Rows in order: Ohm's law "In progress"; Newton's laws "72%"; Mechanics "Unit exam" "90%". |
| W24 | 〃 | links finished quizzes to results and open quizzes to continue | In the Newton row the link "View" has href `/student/quiz-result/{finishedQuizId}`. In the Ohm row, "Continue" goes to `/student/quiz/{openQuizId}`. The exam row has no link. |
| W25 | 〃 | filters to exams through the URL | Choose "Exams" in the select labelled "Type". An MSW handler returns only the exam row when `kind=Exam`. Only the Mechanics row remains, and `router.state.location.search` has `kind: 'Exam'`. |
| W26 | 〃 | opens with the filter from the URL | `renderApp('/student/progress?kind=Quiz')`: the select shows "Quizzes" and the request carries `kind=Quiz` (the handler returns quiz rows only). |
| W27 | 〃 | shows the no-data empty state | Empty page with no filter → "No sessions yet.", and no "Show all" button. |
| W28 | 〃 | shows no-results with a way back to all | Empty page with `?kind=Exam` → "No sessions of this type." Clicking "Show all" makes the select show "All" and brings the rows back. |
| W29 | 〃 | pages through the history | `totalPages 2`. Click "Next" → a page-2 row appears (the handler branches on `pageNumber=2`). |
| W30 | 〃 | shows the history error and retries | 500 once → `alert` "Could not load your sessions." → Retry → the rows appear. |

The MSW defaults come from the Orval-generated `getGetSubjectProgressMockHandler`, `getGetWeakSpotsMockHandler`, `getGetSessionHistoryMockHandler` and `getGetMasteryOverviewMockHandler`, fed with `progressFixtures` / `masteryOverview()`.

## Definition of done
- [ ] Every file in "Files to create" exists at the exact path. No other new files, apart from generated Orval output.
- [ ] `GET /api/progress/subjects`, `/weak-spots` and `/sessions` exist with `DefaultCodes.ProgressViewOwn`, and return 401 anonymous and 403 for a Teacher or Admin.
- [ ] Unit best exam = MAX `ScorePercent` over submitted, non-test `UnitExam` sessions keyed `unit:{id:D}` via `UnitExamScope`, and null when there are none.
- [ ] Weak lessons and objectives follow Decisions 7–8 (seen > 0, not fully mastered, lowest ratio first, curriculum tie-break), limited by the `Progress:*Count` options.
- [ ] History is the student's non-test sessions only, newest first. The `kind` filter maps Quiz and Exam, paging uses `PageData`, and invalid page, size or kind return the three 422 codes.
- [ ] The history handler resolves lesson and unit names with one `FindAsync` each (no N+1) and skips the lookup when the page has none.
- [ ] Error codes exist in `ErrorCodes`, both resx files and both web `shared/i18n` files.
- [ ] `ProgressOptions` has the three new keys with `[Range]`, mirrored in `appsettings.example.json` and `ApiFactory`.
- [ ] `api/openapi/v1.json` and `web/src/shared/api/generated` are regenerated, and `git diff` is clean after regen. `orval.config.ts` has the `GetSessionHistory` zod override.
- [ ] The Postman `Progress` folder has the three requests.
- [ ] `/student/progress` renders the summary (the reused overview), subjects with the unit table, weak lessons and objectives with "درّب الآن" → practice, and history with a filter in the URL, pagination, links and every loading/empty/error-and-retry state.
- [ ] `invalidateMastery` also invalidates `/api/progress*`, and the history hook uses `staleTime: 0`.
- [ ] Every new string exists in `en` and `ar`. There are no literal colours or sizes and no physical-direction utilities, and axe is clean.
- [ ] Every test D1–D10, A1–A24, I1–I18 and W1–W30 exists and passes. No existing test is edited except the additive A13–A14 and W12.
- [ ] `dotnet test api/ -c Release` passes with `appsettings.json` moved aside. The web typecheck, lint, `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` and `vitest run --coverage` all exit 0.
- [ ] `docs/progress.md` is created. `docs/mastery.md`, `docs/sessions.md` and `docs/claude-design-prompt.md` §4 are updated as listed, with no divergence left.
