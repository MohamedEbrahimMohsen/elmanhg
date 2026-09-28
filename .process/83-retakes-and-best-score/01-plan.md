# Plan — [E6.S4] Retakes and best score (#83)

## Goal
A student can already retake an exam, because starting after a submit opens a new sitting. After this story they can also see every counted sitting of that exam, newest first, with the best score shown and every best sitting marked «الأفضل». The list appears on the unit exam-start screen and on the result screen of both unit and multi-unit exams. The progress history marks each exam sitting that holds its scope's best score. PRD §7.4 ("Retakes: unlimited. Best score is the displayed unit-exam score; all attempts are kept and visible in history") and §17 rule 9.

## Scope
**In:**
- Domain: one definition of "a sitting that counts toward a best score" (`ExamBestScoreSpecification`). It is reused by the existing progress best-score query (generalised from unit-only to every exam scope) and by a new per-scope attempts query.
- API:
  - `GET /api/exams/{sessionId}/attempts` returns the sittings of that session's scope, for a unit or multi-unit exam.
  - `GET /api/exams/units/{unitId}/attempts` returns the unit's sittings.
  - `SessionHistoryItemResult.isBestScore` added.
- Web:
  - An attempts card on `/student/exam-result/{sessionId}` (both kinds) and on `/student/exam-start/{unitId}`.
  - A «الأفضل» badge in the progress history.
  - Invalidation of the attempts queries.
- Docs: `docs/exams.md`, `docs/progress.md`, `docs/sessions.md`, `docs/claude-design-prompt.md` §4.
- Postman: the two new requests.
- OpenAPI and Orval are regenerated.

**Out:**
- Best score or attempts on the multi-unit builder (`/student/multi-exam`). Design prompt §4 lists neither for that route.
- Multi-unit best scores on the progress subject cards. PRD §7.6 lists only unit exam best scores.
- Paging of the attempts list (Decision 6).
- Retake limits and cooldowns. PRD: unlimited.

**Deferred:** none. Everything runs offline and needs no provider.

## Decisions
| # | Question | Decision | Why |
|---|---|---|---|
| 1 | Which sittings count toward best score and attempts? | Kind ≠ Quiz, `!IsTestMode`, `SubmittedAt != null`, `ScorePercent != null`, not soft-deleted (global query filter), for the signed-in student, with the same `Kind` and `ScopeKey`. Auto-submitted sittings count. | Matches the existing `GetBestUnitExamScoresAsync` filter and docs/progress.md. Test mode has "no history effect" (docs/exams.md). |
| 2 | Scope identity for multi-unit exams | Same `ScopeKey`: the same unit set and the same size. A different size, or a different unit set, is a different exam. | `MultiUnitExamScope.ToKey()` already defines retake identity (docs/exams.md: "a retake of the same units and size has the same key"). |
| 3 | Do multi-unit sittings feed a unit's best? | No. This is unchanged. | docs/progress.md and docs/exams.md already say so. |
| 4 | Avoiding duplicate best-score queries | `ExamBestScoreSpecification` (Domain) is the only filter. `GetBestUnitExamScoresAsync` becomes `GetBestExamScoresAsync`, grouping all exam scopes by `ScopeKey`. The attempts list computes the best as the max of its own rows, which pass through the same specification. `UnitExamBestScore` is renamed `ExamBestScore`. | Follows the `ServableQuestionSpecification` pattern: one definition, no drift. |
| 5 | Is a `ScopeKey` alone unique across kinds? | Yes. Keys are prefixed by kind (`lesson:`, `unit:`, `units:`), so best scores are keyed by `ScopeKey` only. | Existing key formats. |
| 6 | Paging of attempts | None. The list returns every counted sitting of one scope. | PRD: "all attempts are kept and visible". One student and one scope give a small list. |
| 7 | Order | `SubmittedAt` descending, then `Id` descending. | The prototype's `examHistory` sorts newest first. |
| 8 | Ties for best | Every sitting whose `ScorePercent` equals the best (exact decimal) gets `isBest = true`, in the attempts list and in the history. | Prototype: `x.scorePct === best` gets the badge. It is consistent in both places. |
| 9 | Open sittings | Not listed and never best. | Prototype `examHistory` filters `submittedAt`. An open exam has no score. |
| 10 | Admin (test-mode) callers | Both endpoints work for an Admin (policy `Assessments.Take`) and return an empty list with a null best, so the web shows no card. | Decision 1. The Admin keeps the same screens. |
| 11 | Unknown or deleted unit on the unit endpoint | 404 `UNIT_NOT_FOUND`. | Same as `GET /api/exams/units/{unitId}`. |
| 12 | Session endpoint for another student's session, or for a quiz id | 404 `SESSION_NOT_FOUND`. | Same as `GET /api/exams/{sessionId}`. |
| 13 | Response shape | `ExamAttemptsResult { bestScorePercent?, attempts[] { sessionId, submittedAt, scorePercent, isBest } }`. There is no pass flag and no start time. | Only these fields are rendered (prototype: date, score, badge, «عرض»). |
| 14 | Web: where the card sits | Result page: after the weakest objectives, before «مراجعة الأسئلة». Exam-start: after the start actions. | prototype.md step 4 (the result shows all attempts). Prototype `vExamStart` shows the history after the start button. Design prompt §4 has exam-start with "best score, attempts list". |
| 15 | Web: card content | Heading «محاولاتك السابقة» and the line «أفضل درجة: {score} / 100». The table has التاريخ, الدرجة (with the «الأفضل» chip) and an action column. The row being viewed on the result page shows «هذه المحاولة» with no link; the other rows link «عرض» to `/student/exam-result/{id}`. Scores are rounded to whole numbers, as elsewhere. No card is shown when the list is empty. | Prototype `examHistory`. "This attempt" avoids a self-link. |
| 16 | Web: card loading and error | The card has its own skeleton (`ContentListSkeleton`) and its own error with retry (`ContentErrorState`). It never blocks the rest of the page. | Same per-section pattern as the progress page. |
| 17 | Freshness | `invalidateExamViews` also marks every key that starts with `/api/exams/` and ends with `/attempts` as stale. The unit key is already covered by the `/api/exams/units/` prefix. | A cached older result page must show a new retake. |
| 18 | Unhandled attempts requests in existing web tests | `web/src/test/msw/server.ts` gets default handlers that return an empty attempts result for both endpoints. No existing web test file is edited for this. | Matches the react-testing skeleton (`setupServer(...defaults)`). `resetHandlers()` keeps the initial handlers. |
| 19 | Migration or index | None. The query filters on `StudentId` (indexed `(StudentId, StartedAt)`) and then `Kind`/`ScopeKey` over a single student's rows. | Small per-student cardinality, and no schema change. |
| 20 | New error codes | None. `SESSION_ID_REQUIRED`, `SESSION_NOT_FOUND`, `UNIT_ID_REQUIRED`, `UNIT_NOT_FOUND` and `USER_NOT_AUTHENTICATED` are reused. | Every branch maps to an existing code. |

Assumed product answers (no human input was needed): Decisions 2, 6, 8 and 14.

Morabh reuse: searched `D:\Personal\Projects\Projects\Morabh\repos\apis` for best score, retakes and attempt history, and found nothing. Every piece below is **new, with no Morabh equivalent**. It follows the in-repo patterns: `ServableQuestionSpecification` for the specification, and the `GetUnitExamOverview` and `GetExamSession` slices for the queries.

## Existing code touched
| File | Change |
|---|---|
| `api/Elmanhg.Domain/Sessions/UnitExamBestScore.cs` | **Delete**, replaced by `ExamBestScore.cs`. |
| `api/Elmanhg.Domain/Sessions/ISessionRepository.cs` | Replace `Task<List<UnitExamBestScore>> GetBestUnitExamScoresAsync(Guid studentId, CancellationToken cancellationToken);` with `Task<List<ExamBestScore>> GetBestExamScoresAsync(Guid studentId, CancellationToken cancellationToken);`. Add `Task<List<ExamAttemptSummary>> GetExamAttemptsAsync(Guid studentId, SessionKind kind, string scopeKey, CancellationToken cancellationToken);`. |
| `api/Elmanhg.Infrastructure/Sessions/SessionRepository.cs` | Implement both methods (bodies below) and remove `GetBestUnitExamScoresAsync`. |
| `api/Elmanhg.Application/Progress/GetSubjectProgress/GetSubjectProgressHandler.cs` | `var bests = await sessionRepository.GetBestExamScoresAsync(userId, cancellationToken).ConfigureAwait(false);` |
| `api/Elmanhg.Application/Progress/Shared/SubjectProgressResultGenerator.cs` | Parameter type `IReadOnlyCollection<UnitExamBestScore>` becomes `IReadOnlyCollection<ExamBestScore>`. The body is unchanged: lookup by `new UnitExamScope(unit.Id).ToKey()` already ignores `units:` keys. |
| `api/Elmanhg.Application/Progress/Shared/SessionHistoryItemResult.cs` | Append a parameter: `…, decimal? ScorePercent, bool IsBestScore)`. |
| `api/Elmanhg.Application/Progress/Shared/SessionHistoryResultGenerator.cs` | New signature `Generate(Session session, IReadOnlyList<Lesson> lessons, IReadOnlyList<CurriculumUnit> units, IReadOnlyDictionary<string, decimal> bestByScopeKey)`. Last argument: `ExamBestScoreSpecification.IsSatisfiedBy(session) && bestByScopeKey.TryGetValue(session.ScopeKey, out var best) && best == session.ScorePercent`. |
| `api/Elmanhg.Application/Progress/GetSessionHistory/GetSessionHistoryHandler.cs` | After loading lessons and units: `var bestByScopeKey = page.Items.Any(ExamBestScoreSpecification.IsSatisfiedBy) ? (await sessionRepository.GetBestExamScoresAsync(userId, cancellationToken).ConfigureAwait(false)).ToDictionary(x => x.ScopeKey, x => x.BestScorePercent) : new Dictionary<string, decimal>();`. Pass it to `Generate`. |
| `api/Elmanhg.Api/Controllers/Exams/ExamsController.cs` | Two actions (see API surface). Add usings for `Elmanhg.Application.Exams.GetExamAttempts` and `Elmanhg.Application.Exams.GetUnitExamAttempts`. |
| `api/openapi/v1.json` | Regenerated by `dotnet build`. |
| `postman/elmanhg.postman_collection.json` | In folder "Exams", insert after "Submit exam": "Get exam attempts" (`GET {{baseUrl}}/api/exams/{{examSessionId}}/attempts`, test status 200 plus `pm.expect(pm.response.json().attempts.length).to.be.above(0)`). Then "Get unit exam attempts" (`GET {{baseUrl}}/api/exams/units/{{unitId}}/attempts`, test status 200). Both use the same event/request JSON shape as "Get exam session". |
| `web/src/shared/api/generated/**` | Regenerated with `npm --prefix web run gen:api`. Commit every emitted file: `exams/exams.ts`, `exams/exams.msw.ts`, `model/examAttemptResult.ts`, `model/examAttemptsResult.ts`, `model/index.ts`, `model/sessionHistoryItemResult.ts`. Never hand-edit. |
| `web/src/features/exam/api/invalidateExamViews.ts` | Add `export const examAttemptsQuerySuffix = '/attempts';`. The predicate also returns true when `first.startsWith('/api/exams/') && first.endsWith(examAttemptsQuerySuffix)`. |
| `web/src/features/exam/pages/ExamResultPage.tsx` | Top of component, before the early returns: `const attempts = useGetExamAttempts(sessionId);`. Render `<ExamAttemptsSection query={attempts} currentSessionId={sessionId} />` right after `<ExamWeakestObjectives …/>` and before the `result.review` heading. |
| `web/src/features/exam/pages/ExamStartPage.tsx` | Top of component: `const attempts = useGetUnitExamAttempts(unitId);`. Render `<ExamAttemptsSection query={attempts} />` after `<ExamStartActions …/>`. |
| `web/src/features/exam/i18n/ar.json`, `en.json` | New `attempts` object (strings below). |
| `web/src/features/progress/components/SessionHistoryRow.tsx` | In the score cell, when `item.submittedAt && item.isBestScore`, render after the score text `<span className="ms-2 rounded-full bg-accent-soft px-2.5 py-0.5 text-micro font-semibold text-accent">{t('history.best')}</span>`. |
| `web/src/features/progress/i18n/ar.json`, `en.json` | Add `history.best`: ar «الأفضل», en "Best". |
| `web/src/test/progressFixtures.ts` | Add `isBestScore: false` to `openQuizItem`, `finishedQuizItem` and `examItem`. The type now requires it. |
| `web/src/test/examFixtures.ts` | Add `earlierSittingId = '78787878-7878-4787-8787-787878787878'`. Add `export function examAttempts(overrides?: Partial<ExamAttemptsResult>): ExamAttemptsResult` returning `{ bestScorePercent: 90, attempts: [{ sessionId: examSessionId, submittedAt: '2026-09-28T10:12:30Z', scorePercent: 80, isBest: false }, { sessionId: earlierSittingId, submittedAt: '2026-09-27T09:00:00Z', scorePercent: 90, isBest: true }], ...overrides }`. Add `export function noExamAttempts(): ExamAttemptsResult` returning `{ bestScorePercent: null, attempts: [] }`. |
| `web/src/test/msw/server.ts` | `export const server = setupServer(getGetExamAttemptsMockHandler(noExamAttempts()), getGetUnitExamAttemptsMockHandler(noExamAttempts()));` |
| `docs/exams.md`, `docs/progress.md`, `docs/sessions.md`, `docs/claude-design-prompt.md` | See Docs. |

Existing **tests** modified (listed, so allowed by test integrity):
- `api/Elmanhg.Tests/Application/Features/Progress/GetSubjectProgress/GetSubjectProgressHandlerTests.cs`: rename `UnitExamBestScore` to `ExamBestScore` and `GetBestUnitExamScoresAsync` to `GetBestExamScoresAsync` (lines 83 and 94; the assertions are unchanged), and add test #22.
- `api/Elmanhg.Tests/Application/Features/Progress/GetSessionHistory/GetSessionHistoryHandlerTests.cs`: add tests #20 and #21. Nothing existing changes.
- `web/src/features/exam/api/invalidateExamViews.test.ts`: add test #W8.

## Files to create
| # | Path | Type | Contract |
|---|---|---|---|
| 1 | `api/Elmanhg.Domain/Sessions/ExamBestScore.cs` | record | `namespace Elmanhg.Domain.Sessions; public sealed record ExamBestScore(string ScopeKey, decimal BestScorePercent);` |
| 2 | `api/Elmanhg.Domain/Sessions/ExamAttemptSummary.cs` | record | `public sealed record ExamAttemptSummary(Guid SessionId, DateTimeOffset SubmittedAt, decimal ScorePercent);` |
| 3 | `api/Elmanhg.Domain/Sessions/ExamBestScoreSpecification.cs` | static class | WHY comment: `// PRD §17 rule 9: the single definition of an exam sitting that counts toward a best score and the attempts list.` Members: `public static readonly Expression<Func<Session, bool>> Condition = x => x.Kind != SessionKind.Quiz && !x.IsTestMode && x.SubmittedAt != null && x.ScorePercent != null;` · `private static readonly Func<Session, bool> IsCounted = Condition.Compile();` · `public static bool IsSatisfiedBy(Session session) => IsCounted(session);` · `public static IQueryable<Session> WhereCountsTowardBestScore(this IQueryable<Session> sessions, Guid studentId) => sessions.Where(x => x.StudentId == studentId).Where(Condition);` |
| 4 | `api/Elmanhg.Application/Exams/Shared/ExamAttemptsResult.cs` | records | `namespace Elmanhg.Application.Exams.Shared;` `public sealed record ExamAttemptsResult(decimal? BestScorePercent, List<ExamAttemptResult> Attempts);` and `public sealed record ExamAttemptResult(Guid SessionId, DateTimeOffset SubmittedAt, decimal ScorePercent, bool IsBest);`. Client-facing, but carries no text, so there is no `.Localized()`. |
| 5 | `api/Elmanhg.Application/Exams/Shared/ExamAttemptsResultGenerator.cs` | static class | `public static ExamAttemptsResult Generate(IReadOnlyList<ExamAttemptSummary> attempts)`: `decimal? best = attempts.Count == 0 ? null : attempts.Max(x => x.ScorePercent);` Return `new ExamAttemptsResult(best, attempts.Select(x => new ExamAttemptResult(x.SessionId, x.SubmittedAt, x.ScorePercent, x.ScorePercent == best)).ToList())`. The input order is kept. |
| 6 | `api/Elmanhg.Application/Exams/GetExamAttempts/GetExamAttemptsQuery.cs` | query | `public sealed record GetExamAttemptsQuery(Guid SessionId) : IRequest<ExamAttemptsResult>;` |
| 7 | `api/Elmanhg.Application/Exams/GetExamAttempts/GetExamAttemptsValidator.cs` | validator | `RuleFor(x => x.SessionId).ValidateRequired(ErrorCodes.SessionIdRequired);` (`Core.Validation.Extensions`) |
| 8 | `api/Elmanhg.Application/Exams/GetExamAttempts/GetExamAttemptsHandler.cs` | handler | `public sealed class GetExamAttemptsHandler(ISessionRepository sessionRepository, ICurrentUserService currentUserService) : IRequestHandler<GetExamAttemptsQuery, ExamAttemptsResult>`. Steps: (1) if `UserId` is null or default, throw `UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated)`. (2) `var session = await sessionRepository.FirstOrDefaultAsync(x => x.Id == request.SessionId && x.StudentId == userId && x.Kind != SessionKind.Quiz, cancellationToken, asNoTracking: true)`; if null, throw `NotFoundCoreException(ErrorCodes.SessionNotFound)`. (3) `var attempts = await sessionRepository.GetExamAttemptsAsync(userId, session.Kind, session.ScopeKey, cancellationToken)`. (4) `return ExamAttemptsResultGenerator.Generate(attempts);`. Every await uses `.ConfigureAwait(false)`. |
| 9 | `api/Elmanhg.Application/Exams/GetUnitExamAttempts/GetUnitExamAttemptsQuery.cs` | query | `public sealed record GetUnitExamAttemptsQuery(Guid UnitId) : IRequest<ExamAttemptsResult>;` |
| 10 | `api/Elmanhg.Application/Exams/GetUnitExamAttempts/GetUnitExamAttemptsValidator.cs` | validator | `RuleFor(x => x.UnitId).ValidateRequired(ErrorCodes.UnitIdRequired);` |
| 11 | `api/Elmanhg.Application/Exams/GetUnitExamAttempts/GetUnitExamAttemptsHandler.cs` | handler | `public sealed class GetUnitExamAttemptsHandler(ICurriculumUnitRepository unitRepository, ISessionRepository sessionRepository, ICurrentUserService currentUserService) : IRequestHandler<GetUnitExamAttemptsQuery, ExamAttemptsResult>`. Steps: (1) the same auth guard. (2) `var unit = await unitRepository.GetByIdAsync(request.UnitId, cancellationToken, asNoTracking: true)`; if null, throw `NotFoundCoreException(ErrorCodes.UnitNotFound)`. (3) `var attempts = await sessionRepository.GetExamAttemptsAsync(userId, SessionKind.UnitExam, new UnitExamScope(unit.Id).ToKey(), cancellationToken)`. (4) `return ExamAttemptsResultGenerator.Generate(attempts);`. |
| 12 | `web/src/features/exam/components/ExamAttemptsTable.tsx` | component | `export interface ExamAttemptsTableProps { attempts: ExamAttemptsResult; currentSessionId?: string }`. Returns `null` when `attempts.attempts.length === 0`. Otherwise a card `div.flex.flex-col.gap-3.rounded-lg.border.border-border.bg-surface.p-4.shadow-1.lg:p-5` (same as `ExamUnitBreakdown`) with: `<h2>` `t('attempts.title')` (same classes as `ExamUnitBreakdown` h2); `<p className="text-ui font-semibold">` `t('attempts.best', { score: Math.round(Number(bestScorePercent)) })`; then `<div className="overflow-x-auto"><table className="w-full border-collapse">` with `<caption className="sr-only">{t('attempts.caption')}</caption>`, headers `date`, `score`, `actions` (`attempts.*`, same `th` classes as `ExamUnitBreakdown`), and one row per attempt in the given order. Date cell: `formatDate(new Date(submittedAt), i18n.language, 'arabic-indic', { dateStyle: 'medium', timeStyle: 'short' })`. Score cell: `t('attempts.scoreValue', { score: Math.round(Number(scorePercent)) })`, plus, when `isBest`, `<span className="ms-2 rounded-full bg-accent-soft px-2.5 py-0.5 text-micro font-semibold text-accent">{t('attempts.bestBadge')}</span>`. Action cell: when `sessionId === currentSessionId`, `<span className="text-text-muted">{t('attempts.current')}</span>`; otherwise `<Link to="/student/exam-result/$sessionId" params={{ sessionId }}>` `t('attempts.view')` with the `SessionHistoryRow` link classes. Cell class `px-2.5 py-2.25 text-caption`. |
| 13 | `web/src/features/exam/components/ExamAttemptsSection.tsx` | component | `export interface ExamAttemptsSectionProps { query: UseQueryResult<ExamAttemptsResult>; currentSessionId?: string }` (`UseQueryResult` from `@tanstack/react-query`). If `query.isError`: `<ContentErrorState title={t('attempts.errorTitle')} error={query.error} onRetry={() => { void query.refetch(); }} />`. If `query.isPending`: `<ContentListSkeleton label={t('attempts.loading')} />`. Otherwise `<ExamAttemptsTable attempts={query.data} currentSessionId={currentSessionId} />`. |
| 14 | `api/Elmanhg.Tests/Integration/Exams/ExamAttemptsTestData.cs` | test helper | `public static class ExamAttemptsTestData`. `public static async Task<Guid> InsertExamSittingAsync(ApiFactory factory, Guid studentId, SessionKind kind, string scopeJson, string scopeKey, decimal? scorePercent, bool submitted, bool isTestMode, DateTimeOffset startedAt)`: the same raw `INSERT INTO "Sessions"` as `ProgressTestData.InsertUnitExamSessionAsync`, plus `"PassMark"` = 50 and `"Kind"` = `kind.ToString()`, with `submittedAt = startedAt.AddMinutes(30)` when submitted. `public static Task<Guid> InsertUnitSittingAsync(ApiFactory factory, Guid studentId, Guid unitId, decimal? score, bool submitted = true, bool isTestMode = false, DateTimeOffset startedAt = default)` uses `UnitExamScope`. `public static Task<Guid> InsertMultiSittingAsync(ApiFactory factory, Guid studentId, Guid subjectId, IReadOnlyList<Guid> unitIds, int size, decimal? score, DateTimeOffset startedAt)` uses `MultiUnitExamScope`. `public static async Task<JsonElement> GetAttemptsAsync(HttpClient client, string relativePath)` asserts 200 and returns the body. Note: `IX_Sessions_OneOpenExam` allows at most one open exam per student, so insert at most one non-submitted sitting per student. |
| 15 | `api/Elmanhg.Tests/Domain/Sessions/ExamBestScoreSpecificationTests.cs` | tests | Tests #1–#6 |
| 16 | `api/Elmanhg.Tests/Application/Features/Exams/Shared/ExamAttemptsResultGeneratorTests.cs` | tests | Tests #7–#9 |
| 17 | `api/Elmanhg.Tests/Application/Features/Exams/GetExamAttempts/GetExamAttemptsHandlerTests.cs` | tests | Tests #10–#14 |
| 18 | `api/Elmanhg.Tests/Application/Features/Exams/GetExamAttempts/GetExamAttemptsValidatorTests.cs` | tests | Tests #15–#16 |
| 19 | `api/Elmanhg.Tests/Application/Features/Exams/GetUnitExamAttempts/GetUnitExamAttemptsHandlerTests.cs` | tests | Tests #17–#19 |
| 20 | `api/Elmanhg.Tests/Application/Features/Exams/GetUnitExamAttempts/GetUnitExamAttemptsValidatorTests.cs` | tests | Tests #23–#24 |
| 21 | `api/Elmanhg.Tests/Integration/Exams/ExamAttemptsEndpointTests.cs` | tests | Tests #25–#35 |
| 22 | `api/Elmanhg.Tests/Integration/Progress/SessionHistoryBestScoreEndpointTests.cs` | tests | Test #36 |
| 23 | `web/src/features/exam/pages/ExamResultPage.attempts.test.tsx` | tests | Tests #W1–#W5 |
| 24 | `web/src/features/exam/pages/ExamStartPage.attempts.test.tsx` | tests | Tests #W6–#W7 |
| 25 | `web/src/features/progress/pages/ProgressPage.best.test.tsx` | tests | Test #W9 |

Repository bodies (`SessionRepository`):
```csharp
public async Task<List<ExamBestScore>> GetBestExamScoresAsync(Guid studentId, CancellationToken cancellationToken)
{
    return await _dbSet
        .WhereCountsTowardBestScore(studentId)
        .GroupBy(x => x.ScopeKey)
        .Select(x => new ExamBestScore(x.Key, x.Max(session => session.ScorePercent) ?? 0m))
        .AsNoTracking()
        .ToListAsync(cancellationToken)
        .ConfigureAwait(false);
}

public async Task<List<ExamAttemptSummary>> GetExamAttemptsAsync(Guid studentId, SessionKind kind, string scopeKey, CancellationToken cancellationToken)
{
    return await _dbSet
        .WhereCountsTowardBestScore(studentId)
        .Where(x => x.Kind == kind && x.ScopeKey == scopeKey)
        .OrderByDescending(x => x.SubmittedAt)
        .ThenByDescending(x => x.Id)
        .Select(x => new ExamAttemptSummary(x.Id, x.SubmittedAt ?? DateTimeOffset.MinValue, x.ScorePercent ?? 0m))
        .AsNoTracking()
        .ToListAsync(cancellationToken)
        .ConfigureAwait(false);
}
```
A new repository method is justified because the base `FindAsync(Expression…)` cannot apply the `IQueryable` extension together with a projection.

### i18n strings (`web/src/features/exam/i18n/*.json`, new key `attempts`)
| Key | ar | en |
|---|---|---|
| `attempts.title` | محاولاتك السابقة | Your previous attempts |
| `attempts.best` | أفضل درجة: {score, number} / 100 | Best score: {score, number} / 100 |
| `attempts.caption` | محاولاتك في هذا الامتحان، الأحدث أولًا | Your attempts at this exam, newest first |
| `attempts.date` | التاريخ | Date |
| `attempts.score` | الدرجة | Score |
| `attempts.actions` | إجراءات | Actions |
| `attempts.scoreValue` | {score, number} / 100 | {score, number} / 100 |
| `attempts.bestBadge` | الأفضل | Best |
| `attempts.view` | عرض | View |
| `attempts.current` | هذه المحاولة | This attempt |
| `attempts.loading` | جارٍ تحميل محاولاتك… | Loading your attempts… |
| `attempts.errorTitle` | تعذّر تحميل محاولاتك. | Could not load your attempts. |

## Error codes
No new constants and no new resource strings. The reused codes:

| Constant | Value | Thrown by | Exception type | HTTP |
|---|---|---|---|---|
| `ErrorCodes.UserNotAuthenticated` | `USER_NOT_AUTHENTICATED` | both handlers | `UnauthorizedCoreException` | 401 |
| `ErrorCodes.SessionIdRequired` | `SESSION_ID_REQUIRED` | `GetExamAttemptsValidator` | validation | 422 |
| `ErrorCodes.SessionNotFound` | `SESSION_NOT_FOUND` | `GetExamAttemptsHandler` (missing, another student's, or a quiz) | `NotFoundCoreException` | 404 |
| `ErrorCodes.UnitIdRequired` | `UNIT_ID_REQUIRED` | `GetUnitExamAttemptsValidator` | validation | 422 |
| `ErrorCodes.UnitNotFound` | `UNIT_NOT_FOUND` | `GetUnitExamAttemptsHandler` | `NotFoundCoreException` | 404 |

## Domain behaviour
- No entity changes and no state transitions. `Session` is read only, so no `UpdationDate` is touched.
- `ExamBestScoreSpecification` (file #3) is the invariant, PRD §17 rule 9. Every best-score or attempts read goes through `WhereCountsTowardBestScore` (SQL) or `IsSatisfiedBy` (in memory). No other code may restate the filter.
- Retakes are already unlimited: `StartUnitExam` and `StartMultiUnitExam` resume only an **open** sitting (`SubmittedAt == null`) and create a new one otherwise. This is unchanged and is proven by test #33.

## API surface
| Method | Route | Policy | Request | Response |
|---|---|---|---|---|
| GET | `/api/exams/{sessionId:guid}/attempts` (Name `GetExamAttempts`) | `DefaultCodes.AssessmentsTake` | route `sessionId`, sent as `new GetExamAttemptsQuery(sessionId)` | 200 `ExamAttemptsResult` |
| GET | `/api/exams/units/{unitId:guid}/attempts` (Name `GetUnitExamAttempts`) | `DefaultCodes.AssessmentsTake` | route `unitId`, sent as `new GetUnitExamAttemptsQuery(unitId)` | 200 `ExamAttemptsResult` |
| GET | `/api/progress/sessions` (existing) | unchanged | unchanged | `SessionHistoryItemResult` gains `isBestScore: bool` |

Both actions follow the shape of `GetExamSession`: `[Authorize(Policy = …)]`, `[ProducesResponseType<ExamAttemptsResult>(StatusCodes.Status200OK)]`, `var result = await mediator.Send(…, cancellationToken); return Ok(result);`. Place `GetUnitExamAttempts` after `StartUnitExam`, and `GetExamAttempts` after `GetExamSession`.

## Docs
| File | Change |
|---|---|
| `docs/exams.md` | (1) Intro: replace "best score and the attempts list are #83." with "retakes, best score and the attempts list are in [Retakes and best score](#retakes-and-best-score) (#83)." (2) New section `## Retakes and best score` before `## API`. It covers: retakes are unlimited (start opens a new sitting once the previous one is submitted); the counted-sitting rule (Decision 1, `ExamBestScoreSpecification`); scope identity (Decision 2); order, no paging, ties, open sittings excluded and test mode excluded (Decisions 6–10); the multi-unit sittings rule (unchanged). (3) Add both routes to the API table and the line `ExamAttemptsResult { bestScorePercent?, attempts[] { sessionId, submittedAt, scorePercent, isBest } }`. (4) Student screens: the exam-start bullet gains «محاولاتك السابقة» with «أفضل درجة: {score} / 100», rows (date, score, «الأفضل» chip, «عرض»), hidden when empty. The exam-result bullet gains the same card after the weakest objectives, with the viewed row marked «هذه المحاولة». |
| `docs/progress.md` | The best-score bullet: "…computed by `ISessionRepository.GetBestExamScoresAsync` over `ExamBestScoreSpecification` (submitted, not test mode, not deleted), grouped by `ScopeKey`…". History: add the bullet "A finished exam whose score equals the best of its scope (same `ScopeKey`) shows «الأفضل» next to its score (`isBestScore`); ties all show it." API: add `isBestScore` to the `SessionHistoryItemResult` line. |
| `docs/sessions.md` | `ScopeKey` row: "already read by progress best scores (#78)" becomes "also the retake identity for best scores and the attempts list (#83, `docs/exams.md`)". |
| `docs/claude-design-prompt.md` | Line 135 (§4): `#/student/exam-result/:id` "…weakest objectives, retake." becomes "…weakest objectives, attempts list with the best score highlighted, retake." |

`docs/PRD.md` and `docs/prototype.md` already agree and are not changed.

## Test plan
### api — Domain (no doubles)
| # | Test class | Test method | Asserts |
|---|---|---|---|
| 1 | `Domain/Sessions/ExamBestScoreSpecificationTests` | `IsSatisfiedBy_SubmittedUnitExam_ReturnsTrue` | `new ExamSessionBuilder().Build()` then `SubmitExam(new Dictionary<Guid, QuestionGrade>(), ExamSessionBuilder.Now.AddMinutes(1))` gives true |
| 2 | 〃 | `IsSatisfiedBy_SubmittedMultiUnitExam_ReturnsTrue` | `new MultiUnitExamBuilder().Build()` submitted the same way gives true |
| 3 | 〃 | `IsSatisfiedBy_OpenExam_ReturnsFalse` | An unsubmitted `ExamSessionBuilder` session gives false |
| 4 | 〃 | `IsSatisfiedBy_TestModeExam_ReturnsFalse` | `Build(isTestMode: true)` submitted gives false |
| 5 | 〃 | `IsSatisfiedBy_SubmittedQuiz_ReturnsFalse` | `SessionBuilder` quiz with `RecordAttempt` and `Submit()` gives false |
| 6 | 〃 | `WhereCountsTowardBestScore_KeepsOnlyTheStudentsCountedSittings` | Over `[counted own, counted of another builder's student, open own].AsQueryable()`, the result is exactly `[counted own]` |

### api — Application (NSubstitute at repositories)
| # | Test class | Test method | Asserts |
|---|---|---|---|
| 7 | `Application/Features/Exams/Shared/ExamAttemptsResultGeneratorTests` | `Generate_NoAttempts_ReturnsNullBestAndEmptyList` | `BestScorePercent` null, `Attempts` empty |
| 8 | 〃 | `Generate_Attempts_KeepsOrderAndFlagsHighest` | Input [70, 80, 60.5] gives best 80, `SessionId`s in input order, `IsBest` [false, true, false], `SubmittedAt` and `ScorePercent` copied |
| 9 | 〃 | `Generate_TiedBest_FlagsEveryTiedAttempt` | [80, 80, 50] gives `IsBest` [true, true, false] |
| 10 | `Application/Features/Exams/GetExamAttempts/GetExamAttemptsHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | `UnauthorizedCoreException`, `ErrorCode == USER_NOT_AUTHENTICATED`; `GetExamAttemptsAsync` `DidNotReceive` |
| 11 | 〃 | `Handle_UnknownSession_ThrowsSessionNotFound` | `SessionRepositoryStub.StubFind` with no sessions gives `NotFoundCoreException` `SESSION_NOT_FOUND`; `GetExamAttemptsAsync` `DidNotReceive` |
| 12 | 〃 | `Handle_QuizSession_ThrowsSessionNotFound` | StubFind with a `SessionBuilder` quiz of the same student gives `SESSION_NOT_FOUND` |
| 13 | 〃 | `Handle_UnitExam_ReturnsAttemptsOfTheSessionScope` | `GetExamAttemptsAsync(studentId, SessionKind.UnitExam, session.ScopeKey, Any)` is stubbed with two summaries. The result has 2 attempts, the stubbed ids in order, the correct best and `IsBest` flags. An argument mismatch would return null and fail. |
| 14 | 〃 | `Handle_MultiUnitExam_UsesMultiUnitKindAndScopeKey` | `MultiUnitExamBuilder` session, stub on `(SessionKind.MultiUnitExam, session.ScopeKey)`, gives 1 attempt with `IsBest` true |
| 15 | `…/GetExamAttempts/GetExamAttemptsValidatorTests` | `Validate_EmptySessionId_ReturnsSessionIdRequired` | Error code `SESSION_ID_REQUIRED` |
| 16 | 〃 | `Validate_SessionId_IsValid` | `IsValid` true |
| 17 | `…/GetUnitExamAttempts/GetUnitExamAttemptsHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | `UnauthorizedCoreException` `USER_NOT_AUTHENTICATED` |
| 18 | 〃 | `Handle_UnknownUnit_ThrowsUnitNotFound` | `GetByIdAsync` returns null, giving `NotFoundCoreException` `UNIT_NOT_FOUND`; `GetExamAttemptsAsync` `DidNotReceive` |
| 19 | 〃 | `Handle_Unit_ReturnsUnitExamAttemptsForUnitKey` | Stub on `(studentId, SessionKind.UnitExam, $"unit:{unit.Id:D}", Any)` returns 2 summaries; the result has 2 attempts and the correct best |
| 20 | `Application/Features/Progress/GetSessionHistory/GetSessionHistoryHandlerTests` (modify: add) | `Handle_SubmittedExams_FlagsSittingsMatchingTheirScopeBest` | Page = [submitted unit exam A (score 0.00), submitted unit exam B from another `ExamSessionBuilder` (0.00)]. `GetBestExamScoresAsync` returns `[ExamBestScore(A.ScopeKey, 0m), ExamBestScore(B.ScopeKey, 50m)]`. `IsBestScore` is [true, false]. |
| 21 | 〃 (modify: add) | `Handle_NoCountedExamOnPage_DoesNotLoadBestScores` | Page = [open quiz, finished quiz] gives both `IsBestScore` false; `GetBestExamScoresAsync` `DidNotReceive` |
| 22 | `Application/Features/Progress/GetSubjectProgress/GetSubjectProgressHandlerTests` (modify: rename + add) | `Handle_MultiUnitBest_IsNotAUnitBest` | Bests = `[ExamBestScore(new MultiUnitExamScope(_physics.Id, [_mechanics.Id, _waves.Id], 20).ToKey(), 95m)]` gives `result[0].Units` best values `[null, null]` |
| 23 | `…/GetUnitExamAttempts/GetUnitExamAttemptsValidatorTests` | `Validate_EmptyUnitId_ReturnsUnitIdRequired` | Error code `UNIT_ID_REQUIRED` |
| 24 | 〃 | `Validate_UnitId_IsValid` | `IsValid` true |

### api — Integration (real PostgreSQL, `ApiFactory`)
`Integration/Exams/ExamAttemptsEndpointTests(ApiFactory factory)`. Helpers: `ExamAttemptsTestData`, `ExamTestData`, `SessionTestData.SignedInStudentAsync`, `ScopeTestData`, `ContentTestData`, `AuthTestClient`.

| # | Test method | Asserts |
|---|---|---|
| 25 | `GetSessionAttempts_Anonymous_Returns401` | 401 |
| 26 | `GetSessionAttempts_Teacher_Returns403` | 403 |
| 27 | `GetSessionAttempts_OtherStudentsSession_Returns404` | 404, code `SESSION_NOT_FOUND` |
| 28 | `GetSessionAttempts_UnitScope_ListsCountedSittingsNewestFirstWithBest` | The student's unit U sittings: 60.5 (day 1), 80 (day 2), 70 (day 3), test-mode 99 (day 4), open (day 5, null score). Also a multi-unit sitting over U + V (90), another student's U sitting (100), and the student's sitting of another unit (100). GET with the day-1 id returns `attempts[].sessionId` = [day 3, day 2, day 1], `scorePercent` [70, 80, 60.5], `isBest` [false, true, false] and `bestScorePercent` 80 |
| 29 | `GetSessionAttempts_MultiUnitScope_ListsSameUnitsAndSizeOnly` | Sittings: [A, B] size 20 (50, day 1); [B, A] size 20 (70, day 2); [A, B] size 40 (90); a unit sitting of A (95). GET with the day-1 id returns ids [day 2, day 1] and best 70 |
| 30 | `GetUnitAttempts_Student_ReturnsOnlyUnitExamSittings` | Seeded subject and unit U: U sittings 40 and 65, plus a multi sitting over U + V (99). GET `units/{U}/attempts` returns 2 attempts and best 65 |
| 31 | `GetUnitAttempts_NoSittings_ReturnsEmptyListAndNullBest` | `attempts` empty and `bestScorePercent` JSON null |
| 32 | `GetUnitAttempts_UnknownUnit_Returns404` | 404, code `UNIT_NOT_FOUND` |
| 33 | `Retake_StartAfterSubmit_OpensNewSittingAndListsBoth` | `SeedExamUnitAsync(factory, 2)`. Start, then submit with no answers. Start again: the id differs from the first. Save `"b"` (correct in `QuestionBuilder.McqContent`) on both questions, then submit. GET `units/{unitId}/attempts` returns the set {second, first}, the second with `scorePercent` 100 and `isBest` true, and `bestScorePercent` 100. GET `/{first}/attempts` returns the same two ids |
| 34 | `GetSessionAttempts_AdminTestModeSitting_ReturnsEmpty` | An Admin client (`ScopeTestData.SeedAdminAsync` + `SignedInClientAsync`) starts and submits on a seeded unit. GET `/{id}/attempts` returns 200, `attempts` empty and best null |
| 35 | `GetUnitAttempts_Anonymous_Returns401` | 401 |

`Integration/Progress/SessionHistoryBestScoreEndpointTests(ApiFactory factory)`:

| # | Test method | Asserts |
|---|---|---|
| 36 | `Get_ExamSittings_FlagsBestPerScope` | Student sittings: unit A 60 and 80, unit B 50, multi [A, B] size 20 at 70, and a test-mode unit A sitting at 99 (not listed). `GET /api/progress/sessions?kind=Exam` returns `isBestScore` true for A-80, B-50 and multi-70, and false for A-60 |

### web (Vitest + Testing Library + MSW; `renderApp`)
| # | File | `it(...)` | Asserts |
|---|---|---|---|
| W1 | `features/exam/pages/ExamResultPage.attempts.test.tsx` (`describe('ExamResultPage attempts')`) | `lists the attempts newest first with the best score` | `getGetExamSessionMockHandler(submittedExam([examItem(1)]))` + `getGetExamAttemptsMockHandler(examAttempts())`. Heading "Your previous attempts", text "Best score: 90 / 100". The table "Your attempts at this exam, newest first" has row 1 containing "80 / 100" and row 2 containing "90 / 100" and "Best"; row 1 has no "Best" |
| W2 | 〃 | `marks the attempt being viewed and links the others to their results` | The `examSessionId` row has "This attempt" and no link. The other row's "View" link has href `/student/exam-result/${earlierSittingId}` |
| W3 | 〃 | `shows no attempts card when there are none` | Default handler (empty). After "80 / 100" appears, `queryByRole('heading', { name: 'Your previous attempts' })` is null |
| W4 | 〃 | `shows an error with retry when the attempts fail to load` | `http.get('*/api/exams/:sessionId/attempts', 500)` gives alert "Could not load your attempts.". Then `server.use(getGetExamAttemptsMockHandler(examAttempts()))`, click "Retry", and the heading "Your previous attempts" appears |
| W5 | 〃 | `renders the attempts in Arabic without axe violations` | `lng: 'ar'` shows heading «محاولاتك السابقة» and «الأفضل», and `expect(await axe(container)).toHaveNoViolations()` |
| W6 | `features/exam/pages/ExamStartPage.attempts.test.tsx` | `lists the unit attempts under the start actions` | `getGetUnitExamOverviewMockHandler(overview())` + `getGetUnitExamAttemptsMockHandler(examAttempts())`. Heading "Your previous attempts" and "Best score: 90 / 100". Both rows have a "View" link (no current session), with hrefs for `examSessionId` and `earlierSittingId` |
| W7 | 〃 | `shows no attempts card before the first sitting` | Default handler. After the "Start exam" button appears, the attempts heading is absent |
| W8 | `features/exam/api/invalidateExamViews.test.ts` (modify: add) | `marks exam attempts stale` | `getGetExamAttemptsQueryKey(examSessionId)` and `getGetUnitExamAttemptsQueryKey(examUnitId)` are invalidated; `getGetExamSessionQueryKey(examSessionId)` is not |
| W9 | `features/progress/pages/ProgressPage.best.test.tsx` | `marks the best exam sitting in the history` | History page = `[{ ...examItem, isBestScore: true }, { ...examItem, id: '<new uuid>', scorePercent: 60, isBestScore: false }]`, plus the mastery, subjects and weak-spots handlers as in `UnitProgressTable.test.tsx`. The row with "90%" contains "Best"; the row with "60%" does not |

Mutation-check W1, W8, W9 and tests #13, #20 and #28: break the production line, confirm the test fails, then restore it.

## Definition of done
- [ ] `ExamBestScoreSpecification` exists in Domain and is the only place the counted-sitting filter is written. `GetBestExamScoresAsync` and `GetExamAttemptsAsync` both call `WhereCountsTowardBestScore`, and the history uses `IsSatisfiedBy`.
- [ ] `UnitExamBestScore` and `GetBestUnitExamScoresAsync` are gone, with no remaining references. Progress unit best scores are unchanged in behaviour (existing tests green).
- [ ] `GET /api/exams/{sessionId}/attempts` and `GET /api/exams/units/{unitId}/attempts` exist with policy `Assessments.Take` and return `ExamAttemptsResult`, newest first, with ties all flagged.
- [ ] Open, test-mode, quiz, other-student and other-scope sittings never appear in an attempts list or set a best.
- [ ] A multi-unit list contains only sittings with the same units and size.
- [ ] `SessionHistoryItemResult.isBestScore` is correct per scope, and no best-score query runs for a page without a counted exam.
- [ ] No new error codes, no migration, no new exception types.
- [ ] Every test in the Test plan exists with the exact name and passes. `dotnet test api/ -c Release` is green with `appsettings.json` moved aside.
- [ ] `api/openapi/v1.json` and `web/src/shared/api/generated/**` are regenerated and committed (no drift).
- [ ] The Postman "Exams" folder has "Get exam attempts" and "Get unit exam attempts" after "Submit exam".
- [ ] The result page (unit and multi) and the exam-start page show the attempts card per Decisions 14–16, with no card when the list is empty and its own loading and error-with-retry states. All Arabic strings match the i18n table.
- [ ] Progress history shows «الأفضل» on best exam sittings.
- [ ] `invalidateExamViews` marks attempts queries stale. The default MSW handlers exist in `test/msw/server.ts`.
- [ ] Only the listed existing tests changed (GetSubjectProgressHandlerTests rename + 1 add, GetSessionHistoryHandlerTests 2 adds, invalidateExamViews.test.ts 1 add).
- [ ] Web: `npm --prefix web run typecheck`, `lint` and `test` pass. `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` is clean. No `ml-/mr-/pl-/pr-/text-left/right`.
- [ ] `docs/exams.md`, `docs/progress.md`, `docs/sessions.md` and `docs/claude-design-prompt.md` are updated per the Docs table. No doc contradicts the code.
