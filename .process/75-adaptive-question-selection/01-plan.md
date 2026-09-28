# Plan — Adaptive question selection (#75, E5.S2)

## Goal
When a student starts a new lesson quiz, the questions are no longer a random draw. The quiz serves unseen questions first. It then serves questions the student last got wrong (oldest first), then questions answered correctly exactly once, then the rest, weighted toward the least recently seen (PRD §7.2). The pool is still servable questions only. A question never repeats within a session, and a small pool gives a shorter quiz. Selection runs as one grouped attempt-summary query per start, and a seeded random source makes it deterministic in tests.

## Scope
**In:**
- A pure domain selector (`QuestionSelector`) with an injected `System.Random`.
- A bucket classification on a `QuestionAttemptSummary` record.
- One grouped attempt-summary repository query.
- A servable-ids candidate query that replaces `GetRandomServableInLessonAsync`.
- A `StartQuizSessionHandler` rewire.
- `MasteryOptions.CorrectThreshold` (0.8).
- Unit tests, persistence tests and endpoint tests.
- A `docs/sessions.md` update.

**Out:**
- No API or route change, so `api/openapi/v1.json`, the Orval client, Postman and `web/` are all untouched.
- The mastery table and mastery % (#77).
- Exam selection (E6, PRD §7.4).
- The web quiz screen (#76).
- No migration, because the existing index covers the query (see D13).

**Deferred:** none.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| D1 | What is a "correct" attempt? | `NormalisedScore >= MasteryOptions.CorrectThreshold` (default `0.8m`). Anything below the threshold is **wrong**. A partial credit of 0.5 is wrong; 0.8 is correct. | PRD §7.2 bucket 2 says "normalised score < mastery threshold", and PRD §7.3 sets the threshold at 0.8. `Attempt.Outcome` (the 1.0/0 display outcome) is **not** used. |
| D2 | Where does the threshold live? | New `MasteryOptions` (`Mastery:CorrectThreshold`, `[Range(0.01, 1.0)]`, default `0.8m`, `ValidateOnStart`). #77 reuses it. | skill §8.1: tunables go in Options. It is a mastery concept, so it does not belong in `SessionsOptions`. |
| D3 | Precise buckets for a candidate | **Unseen (1):** no attempt row. **LastWrong (2):** the latest attempt is wrong, i.e. `LastCorrectAt != LastAttemptedAt` (this includes never correct). **CorrectOnce (3):** the latest attempt is correct and `CorrectCount == 1`. **Rest (4):** the latest attempt is correct and `CorrectCount >= 2`. | This is the literal PRD §7.2 wording, and the buckets are disjoint and exhaustive. Example: history C,W,C is Rest, W,C is CorrectOnce, C,C,W is LastWrong. |
| D4 | Order within each bucket | Unseen and CorrectOnce: uniform shuffle. LastWrong: `LastAttemptedAt` ascending (oldest first), with ties broken randomly (shuffle, then a stable sort). Rest: weighted random without replacement. The weights come from rank by `LastAttemptedAt` ascending: the oldest gets weight `n`, the newest gets weight `1`. Sampling uses Efraimidis–Spirakis: `key = Math.Log(1d - random.NextDouble()) / weight`, ordered by key descending. | PRD §7.2 says "random within a bucket", with the explicit bucket-2 "oldest first" and bucket-4 "weighted toward least-recently seen". The specific clause wins over the general one. |
| D5 | Do attempts on an older question version count? | Yes. The summary is keyed by `QuestionId` only, and `QuestionVersion` is ignored. | Mastery (PRD §7.3) is per question. A typo fix should not reset history. The index has no version column. |
| D6 | Do test-mode (admin) attempts count? | Yes. The query filters by `StudentId == caller`, and `IsTestMode` is ignored. | Students never produce test-mode attempts, so this only affects admins. Admins previewing the quiz then see real adaptive behaviour. The #77/E12 exclusion of test mode is about aggregates, not selection. No join to `Sessions` is needed, so the query stays on one index. |
| D7 | Do attempts in unfinished sessions count? | Yes. Every non-deleted attempt counts. | An attempt is graded and final when saved (`docs/sessions.md`). Selection runs only when there is no open session for this lesson. |
| D8 | Item order in the new session | `Position` follows priority order: position 1 is the first unseen question, and so on. There is no final shuffle. | Finishing mid-quiz is allowed (`docs/sessions.md`), so the most purposeful questions come first. `Session.StartQuiz` already assigns positions in input order. |
| D9 | Random source | `QuestionSelector.Select(..., Random random)`. The handler takes a `Random` from DI, and `AddApplication` registers `services.AddSingleton(Random.Shared)`. Tests pass `new Random(seed)`. | `Core.Utilities.IGenerator` only produces strings (Nanoid), with no numeric or shuffle API, so §8.2 does not apply. This is not a secret, so §8.10 does not apply. `Random.Shared` is thread-safe. |
| D10 | Input-order independence | The selector sorts distinct candidate ids ascending before any randomness. | The result depends only on the candidate set, the summaries and the seed. Postgres row order cannot leak in. |
| D11 | No repeats within a session | Three layers: the selector's `Distinct()`, the existing `Session.StartQuiz` guard (`SESSION_QUESTION_DUPLICATE`), and the unique index `IX_SessionItems_SessionId_QuestionId`. Items are fixed at start. | Resume returns the open session untouched, never re-selects, and ignores `questionCount` (unchanged #74 behaviour). A concurrent double start is still resolved by `IX_Sessions_InProgressScope` (409, and a retry resumes). |
| D12 | Small pool | The selector returns `min(count, distinctCandidates)`. Zero candidates short-circuit in the handler: no summary query, no load, and `Session.StartQuiz([])` throws the existing 400 `SESSION_NO_SERVABLE_QUESTIONS`. | This keeps #74 behaviour and error codes. |
| D13 | Summary query shape and plan | One `GROUP BY "QuestionId"` over `Attempts WHERE StudentId = @s AND QuestionId = ANY(@ids)` (plus the global `NOT IsDeleted` filter), with conditional aggregates: `count(*)`, `count(correct)`, `max(CreatedAt)`, and `max(CASE WHEN correct THEN CreatedAt END)`. "Last attempt correct" is derived as `LastCorrectAt == LastAttemptedAt`, so no per-group subquery is needed. Verified with `EXPLAIN ANALYZE` on pgvector/pg17 (1.2M rows): `GroupAggregate` over an `Index Scan using IX_Attempts_StudentId_QuestionId_CreatedAt`, with `Index Cond: StudentId = … AND QuestionId = ANY(…)`, no Sort, 0.13 ms. **No new index or migration.** | The brief asks for one grouped query per start. The existing index already orders by `(StudentId, QuestionId)`. |
| D14 | Timestamp tie between a wrong and a correct attempt on one question | The attempt counts as correct (`LastCorrectAt == LastAttemptedAt`). | This needs two attempts by one student on one question in the same microsecond, from two different sessions, which is practically unreachable. Documented only. |
| D15 | Candidate loading | The new `IQuestionRepository.GetServableIdsInLessonAsync` projects **ids only** through `WhereServable`, the single home of the servable rule. Only the selected questions are then loaded, through the base `FindAsync(x => selectedIds.Contains(x.Id), …, asNoTracking: true)`, and re-ordered to selection order. `GetRandomServableInLessonAsync` is deleted: it is dead code and it contradicts PRD §7.2. | Loading every servable question's jsonb content to pick 10 would be wasteful. Raw SQL would duplicate the servable rule. |
| D16 | A selected question disappears or changes between queries | Soft-deleted (missing from the load): skipped, so the quiz is shorter. Retired or unapproved in between: the existing `Session.StartQuiz` guard throws 400 `SESSION_QUESTION_NOT_SERVABLE` (unchanged #74 semantics). | This needs no new code paths beyond the `Where(byId.ContainsKey)` filter. |
| D17 | Where the selector lives | `Elmanhg.Domain/Sessions/Selection/` as a `static class`, mirroring the static `QuestionGrader` and `ServableQuestionSpecification`. | It is pure logic with no I/O and needs no DI registration. |
| D18 | Morabh reuse | None. Grep of `/home/user/apis` for shuffle/weighted/Random finds only `Core.Utilities/Generator/Generator.cs`, which is already vendored and unsuitable (D9). Every piece is new, with no Morabh equivalent. | |

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Domain/Questions/IQuestionRepository.cs` | Remove `GetRandomServableInLessonAsync`. Add `Task<List<Guid>> GetServableIdsInLessonAsync(Guid lessonId, CancellationToken cancellationToken);` |
| `api/Elmanhg.Infrastructure/Questions/QuestionRepository.cs` | Remove `GetRandomServableInLessonAsync`. Add `GetServableIdsInLessonAsync`: `_dbSet.WhereServable(_context.Set<Lesson>()).Where(x => x.LessonId == lessonId).Select(x => x.Id).ToListAsync(cancellationToken).ConfigureAwait(false)` (one operator per line). |
| `api/Elmanhg.Domain/Sessions/ISessionRepository.cs` | Becomes `public interface ISessionRepository : IRepository<Session> { Task<List<QuestionAttemptSummary>> GetAttemptSummariesAsync(Guid studentId, IReadOnlyCollection<Guid> questionIds, decimal correctThreshold, CancellationToken cancellationToken); }`, adding `using Elmanhg.Domain.Sessions.Selection;` |
| `api/Elmanhg.Infrastructure/Sessions/SessionRepository.cs` | Implement `GetAttemptSummariesAsync` (exact body below). |
| `api/Elmanhg.Application/Sessions/StartQuizSession/StartQuizSessionHandler.cs` | New constructor and selection steps (below). |
| `api/Elmanhg.Application/DependencyInjection.cs` | Add `services.AddOptions<MasteryOptions>().BindConfiguration(MasteryOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();` and `services.AddSingleton(Random.Shared);` |
| `api/Elmanhg.Api/appsettings.example.json` | Add the line `"Mastery": { "CorrectThreshold": 0.8 },` right after the `"Sessions"` line. |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | Add `["Mastery:CorrectThreshold"] = "0.8",` after `["Sessions:AnswerMaxLength"] = "4000",` |
| `api/Elmanhg.Tests/Application/Features/Sessions/StartQuizSession/StartQuizSessionHandlerTests.cs` | Rewrite for the new dependencies (intentional behaviour change, skill §8.11). The rows are in the test plan. |
| `api/Elmanhg.Tests/Integration/Sessions/StartQuizSessionEndpointTests.cs` | Add 2 tests (test plan). Existing tests stay as they are. |
| `api/Elmanhg.Tests/Integration/Sessions/SessionTestData.cs` | Add `public static Task<HttpResponseMessage> FinishAsync(HttpClient client, Guid sessionId) => client.PostAsync($"{Route}/{sessionId}/finish", null, CancellationToken);` |
| `api/Elmanhg.Tests/Integration/Persistence/ServableQuestionSpecificationPersistenceTests.cs` | Add 1 test (test plan). |
| `docs/sessions.md` | Lifecycle step 1: replace "drawn at random … Adaptive selection (#75) replaces the random draw without changing this model." with "chosen by adaptive selection (see Selection)". Add a `## Selection` section after `## Lifecycle` that states D1, D3–D8, D11, D12, D14 and D16 in prose and a table. Options table: add the row `Mastery:CorrectThreshold` · `0.8` · "Normalised score at or above which an attempt counts as correct (PRD §7.3); used by selection and, later, mastery." Test mode section: add "Test-mode attempts still count toward that admin's own question selection." |

`FinishSessionEndpointTests` keeps its private `FinishAsync` (no edit). The new helper lives in `SessionTestData`, and only the new tests use it.

## Files to create
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `api/Elmanhg.Domain/Sessions/Selection/QuestionSelectionBucket.cs` | enum | `namespace Elmanhg.Domain.Sessions.Selection; public enum QuestionSelectionBucket { Unseen = 1, LastWrong = 2, CorrectOnce = 3, Rest = 4 }`. No extensions class. |
| 2 | `api/Elmanhg.Domain/Sessions/Selection/QuestionAttemptSummary.cs` | sealed record | `public sealed record QuestionAttemptSummary(Guid QuestionId, int AttemptCount, int CorrectCount, DateTimeOffset LastAttemptedAt, DateTimeOffset? LastCorrectAt)` with members `public bool IsLastAttemptCorrect => LastCorrectAt == LastAttemptedAt;` and `public QuestionSelectionBucket Bucket => !IsLastAttemptCorrect ? QuestionSelectionBucket.LastWrong : CorrectCount == 1 ? QuestionSelectionBucket.CorrectOnce : QuestionSelectionBucket.Rest;`. The one WHY comment allowed, above `IsLastAttemptCorrect`: `// The latest attempt is correct exactly when the latest correct attempt is the latest attempt; this keeps the summary a single GROUP BY.` |
| 3 | `api/Elmanhg.Domain/Sessions/Selection/QuestionSelector.cs` | static class | See "Domain behaviour". Signature: `public static List<Guid> Select(IReadOnlyCollection<Guid> candidateIds, IReadOnlyCollection<QuestionAttemptSummary> summaries, int count, Random random)`. |
| 4 | `api/Elmanhg.Application/Shared/Options/MasteryOptions.cs` | sealed class | `namespace Elmanhg.Application.Shared.Options; public sealed class MasteryOptions { public const string SectionName = "Mastery"; [Range(0.01, 1.0)] public decimal CorrectThreshold { get; set; } = 0.8m; }` |
| 5 | `api/Elmanhg.Tests/Domain/Sessions/Selection/QuestionSelectorTests.cs` | tests | Rows S1–S14. |
| 6 | `api/Elmanhg.Tests/Domain/Sessions/Selection/QuestionAttemptSummaryTests.cs` | tests | Rows B1–B4. |
| 7 | `api/Elmanhg.Tests/Application/Features/Sessions/MasteryOptionsTests.cs` | tests | Rows O1–O2. Copy the `BuildProvider` helper shape from `SessionsOptionsTests`. |
| 8 | `api/Elmanhg.Tests/Integration/Persistence/AttemptSummaryPersistenceTests.cs` | tests | Rows P1–P4. `public sealed class AttemptSummaryPersistenceTests(ApiFactory factory)`. |

### SessionRepository.GetAttemptSummariesAsync (exact)
```csharp
public async Task<List<QuestionAttemptSummary>> GetAttemptSummariesAsync(Guid studentId, IReadOnlyCollection<Guid> questionIds, decimal correctThreshold, CancellationToken cancellationToken)
{
    return await _context.Set<Attempt>()
        .Where(x => x.StudentId == studentId && questionIds.Contains(x.QuestionId))
        .GroupBy(x => x.QuestionId)
        .Select(x => new QuestionAttemptSummary(x.Key, x.Count(), x.Count(attempt => attempt.NormalisedScore >= correctThreshold), x.Max(attempt => attempt.CreatedAt), x.Max(attempt => attempt.NormalisedScore >= correctThreshold ? attempt.CreatedAt : (DateTimeOffset?)null)))
        .AsNoTracking()
        .ToListAsync(cancellationToken)
        .ConfigureAwait(false);
}
```
Do not add `.Where(!IsDeleted)`; the global filter applies (skill §8.5). If EF fails to translate the constructor projection, project to an anonymous type first and map with `.Select(...)` after `ToListAsync`. Do not change the SQL shape.

### StartQuizSessionHandler (new)
Constructor, on one line:
`public sealed class StartQuizSessionHandler(ISessionRepository sessionRepository, ILessonRepository lessonRepository, IQuestionRepository questionRepository, IOptions<SessionsOptions> sessionsOptions, IOptions<MasteryOptions> masteryOptions, Random random, ICurrentUserService currentUserService, ILocalizer localizer) : IRequestHandler<StartQuizSessionCommand, SessionResult>`

`Handle` steps:
1. The auth check, lesson load and Published check, and open-session lookup and `Resume()` are unchanged.
2. When no open session exists: `var questions = await SelectQuestionsAsync(userId, lesson.Id, request.QuestionCount ?? sessionsOptions.Value.DefaultQuizSize, cancellationToken).ConfigureAwait(false);` Then call `Session.StartQuiz(userId, lesson, questions, isAdmin)` and `AddAsync` (unchanged).
3. `SaveChangesAsync`, `GetRevisionsAsync` and `SessionResultGenerator.Generate` are unchanged.

`private async Task<List<Question>> SelectQuestionsAsync(Guid studentId, Guid lessonId, int count, CancellationToken cancellationToken)`:
1. `var candidateIds = await questionRepository.GetServableIdsInLessonAsync(lessonId, cancellationToken).ConfigureAwait(false);`
2. `if (candidateIds.Count == 0) { return []; }`
3. `var summaries = await sessionRepository.GetAttemptSummariesAsync(studentId, candidateIds, masteryOptions.Value.CorrectThreshold, cancellationToken).ConfigureAwait(false);`
4. `var selectedIds = QuestionSelector.Select(candidateIds, summaries, count, random);`
5. `var loaded = await questionRepository.FindAsync(x => selectedIds.Contains(x.Id), cancellationToken, asNoTracking: true).ConfigureAwait(false);`
6. `var questionsById = loaded.ToDictionary(x => x.Id);` then `return selectedIds.Where(questionsById.ContainsKey).Select(x => questionsById[x]).ToList();` (one operator per line).

## Error codes
None new. Reused and unchanged: `SESSION_NO_SERVABLE_QUESTIONS` (400, the empty pool), `SESSION_QUESTION_NOT_SERVABLE` (400, a race guard), `SESSION_QUESTION_DUPLICATE` (400, a guard). No resource strings change.

## Domain behaviour
`QuestionSelector.Select` does the following, in this order. The order fixes how the seeded `Random` is consumed.
1. `ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);`
2. `var candidates = candidateIds.Distinct().Order().ToList();` (sorted by Guid ascending, D10).
3. `var summariesByQuestion = summaries.ToDictionary(x => x.QuestionId);` Summaries whose id is not a candidate are ignored.
4. `var unseen = candidates.Where(x => !summariesByQuestion.ContainsKey(x)).ToArray();` and `var seen = candidates.Where(summariesByQuestion.ContainsKey).Select(x => summariesByQuestion[x]).ToList();`
5. `random.Shuffle(unseen);`
6. LastWrong: take the array of `seen` with `Bucket == LastWrong`, `random.Shuffle` it, then stable `OrderBy(x => x.LastAttemptedAt)`, and select `QuestionId`.
7. CorrectOnce: take the array of `QuestionId` with `Bucket == CorrectOnce`, then `random.Shuffle` it.
8. Rest: take the array of `seen` with `Bucket == Rest`, `random.Shuffle` it, then stable `OrderBy(x => x.LastAttemptedAt)` gives the ranked list (index `i`, size `n`, weight `n - i`). Then `.Select((summary, index) => (summary.QuestionId, Key: Math.Log(1d - random.NextDouble()) / (n - index)))`, `.OrderByDescending(x => x.Key)` and `.Select(x => x.QuestionId)`. Materialise the keys (`ToList()`) before ordering, so each element draws exactly one `NextDouble` in rank order.
9. `return unseen.Concat(lastWrong).Concat(correctOnce).Concat(rest).Take(count).ToList();`

Split private static helpers (`ShuffledOldestFirst(IEnumerable<QuestionAttemptSummary>, Random)` and `WeightedLeastRecentFirst(IReadOnlyList<QuestionAttemptSummary>, Random)`) so the file stays under 80 lines. The file has no comments except one WHY line above the weighting: `// Efraimidis–Spirakis weighted sampling without replacement; weight = rank from newest (1) to oldest (n), PRD §7.2 bucket 4.`

The entity methods are unchanged: `Session.StartQuiz` keeps all three guards and assigns positions in input order. No `UpdationDate` changes.

## API surface
Unchanged: `POST /api/sessions/quiz` · `DefaultCodes.AssessmentsTake` · `StartQuizSessionRequest { lessonId, questionCount? }` · `SessionResult`. Only the choice and order of the served items differ. No OpenAPI, Orval or Postman regeneration is needed. The implementer confirms that `dotnet build` leaves `api/openapi/v1.json` unchanged.

## Test plan
Conventions: xUnit v3 + FluentAssertions + NSubstitute; `TestContext.Current.CancellationToken`. Domain tests use no doubles. Summaries are built directly with `var t0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)` plus `AddMinutes(k)`. Question ids come from `Guid.NewGuid()` in the arrange step, with `Random` seeded by a `private const int Seed = 42`.

Summary helpers in the selector tests (private static):
- `Wrong(Guid id, DateTimeOffset at) => new(id, 1, 0, at, null)`
- `CorrectOnce(Guid id, DateTimeOffset at) => new(id, 1, 1, at, at)`
- `Rest(Guid id, DateTimeOffset at) => new(id, 2, 2, at, at)`

| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| B1 | `QuestionAttemptSummaryTests` | `Bucket_NeverCorrect_IsLastWrong` | `new(id, 2, 0, t0, null).Bucket == LastWrong`; `IsLastAttemptCorrect` is false |
| B2 | `QuestionAttemptSummaryTests` | `Bucket_CorrectBeforeLatestWrong_IsLastWrong` | `new(id, 2, 1, t0.AddMinutes(5), t0)` gives `LastWrong` |
| B3 | `QuestionAttemptSummaryTests` | `Bucket_LatestCorrectAndCorrectOnce_IsCorrectOnce` | `new(id, 2, 1, t0, t0)` gives `CorrectOnce` |
| B4 | `QuestionAttemptSummaryTests` | `Bucket_LatestCorrectAndCorrectTwice_IsRest` | `new(id, 3, 2, t0, t0)` gives `Rest` |
| S1 | `QuestionSelectorTests` | `Select_AllBuckets_OrdersUnseenThenLastWrongThenCorrectOnceThenRest` | 2 unseen, 2 wrong, 2 correct-once and 2 rest; count 8. Result `[0..1]` is the unseen set, `[2..3]` the wrong set, `[4..5]` the correct-once set and `[6..7]` the rest set (each compared with `BeEquivalentTo`) |
| S2 | `QuestionSelectorTests` | `Select_UnseenFillCount_ExcludesSeenQuestions` | 3 unseen and 3 wrong; count 3. Result is equivalent to the 3 unseen |
| S3 | `QuestionSelectorTests` | `Select_PoolSmallerThanCount_ReturnsWholePool` | 3 candidates (1 unseen, 1 wrong, 1 rest); count 10. The result has 3 items, all unique, equivalent to the candidates |
| S4 | `QuestionSelectorTests` | `Select_PoolLargerThanCount_ReturnsCountUniqueCandidates` | 12 unseen; count 5. 5 items, `OnlyHaveUniqueItems`, and a subset of the candidates |
| S5 | `QuestionSelectorTests` | `Select_DuplicateCandidateIds_ReturnsEachOnce` | candidates `[a, a, b]`; count 5. Result equivalent to `[a, b]` with 2 items |
| S6 | `QuestionSelectorTests` | `Select_LastWrong_OrdersOldestFirst` | 3 wrong at t0+2, t0+0, t0+1 (unseen none); count 3. Result equals the ids ordered by time ascending (`Equal`, in order) |
| S7 | `QuestionSelectorTests` | `Select_CorrectOnceBeforeRest_EvenWhenRestIsOlder` | 1 correct-once at t0+10 and 1 rest at t0; count 2. Result is `[correctOnce, rest]` |
| S8 | `QuestionSelectorTests` | `Select_SameSeed_ReturnsSameSequence` | 10 mixed candidates; two calls with `new Random(Seed)` return `Equal` lists |
| S9 | `QuestionSelectorTests` | `Select_CandidateInputOrderReversed_ReturnsSameSequence` | Same candidates in reversed input order and the same seed give `Equal` results |
| S10 | `QuestionSelectorTests` | `Select_UnseenBucket_IsShuffledAcrossSeeds` | 5 unseen; count 5. Over seeds 1..20, more than one distinct first element occurs, and every result is a permutation of the 5 |
| S11 | `QuestionSelectorTests` | `Select_CorrectOnceBucket_IsShuffledAcrossSeeds` | 5 correct-once with distinct times; count 5. Over seeds 1..20, more than one distinct first element |
| S12 | `QuestionSelectorTests` | `Select_RestBucket_FavoursLeastRecentlySeen` | 5 rest at t0+0..t0+4; count 1; seeds 0..999. The oldest is picked more than twice as often as the newest (the expected ratio is 5:1), and every one of the 5 is picked at least once |
| S13 | `QuestionSelectorTests` | `Select_SummaryForNonCandidate_IsIgnored` | candidates `[a]`, a summary for `b` (wrong); count 5. Result is `[a]` |
| S14 | `QuestionSelectorTests` | `Select_CountZero_ThrowsArgumentOutOfRange` | `act.Should().Throw<ArgumentOutOfRangeException>()` |
| O1 | `MasteryOptionsTests` | `AddApplication_DefaultMasteryOptions_CorrectThresholdIsPointEight` | `CorrectThreshold == 0.8m` |
| O2 | `MasteryOptionsTests` | `AddApplication_CorrectThresholdAboveOne_ThrowsOptionsValidationException` | `Mastery:CorrectThreshold = "1.5"` makes `.Value` throw `OptionsValidationException` |
| H1 | `StartQuizSessionHandlerTests` | `Handle_NoOpenSession_StartsQuizFromWholeSmallPool` | pool of 2; count 5. `AddAsync` receives a session with 2 items and the student id; the result has 2 items, `Kind` Quiz, `Type` Mcq and a null `CorrectAnswer`; `SaveChangesAsync` `Received(1)` |
| H2 | `StartQuizSessionHandlerTests` | `Handle_NoQuestionCount_UsesDefaultQuizSize` | pool of 12, `null` count. The result has 10 items |
| H3 | `StartQuizSessionHandlerTests` | `Handle_PoolLargerThanCount_ServesRequestedCountWithoutRepeats` | pool of 8; count 5. 5 items, unique `QuestionId`s |
| H4 | `StartQuizSessionHandlerTests` | `Handle_AttemptHistory_OrdersItemsByBucket` | pool of 4. Summaries: q1 none, q2 wrong, q3 correct-once, q4 rest. Result items' `QuestionId` in position order equal `[q1, q2, q3, q4]`. `GetAttemptSummariesAsync` received `(studentId, Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 4), 0.8m, …)`. This proves the threshold is threaded; it sits next to the ordering assertion, not alone |
| H5 | `StartQuizSessionHandlerTests` | `Handle_SelectedQuestionMissingOnLoad_ServesTheRest` | Candidate ids are 3, but `FindAsync` returns only 2 of them. The session has 2 items |
| H6 | `StartQuizSessionHandlerTests` | `Handle_OpenSessionForLesson_ResumesIt` | Same assertions as today, plus `GetServableIdsInLessonAsync` and `GetAttemptSummariesAsync` `DidNotReceive` (no re-selection on resume) |
| H7 | `StartQuizSessionHandlerTests` | `Handle_OpenSessionOfOtherStudent_StartsNewSession` | Unchanged assertions |
| H8 | `StartQuizSessionHandlerTests` | `Handle_AdminCaller_StartsTestModeSession` | Unchanged |
| H9 | `StartQuizSessionHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | Unchanged |
| H10 | `StartQuizSessionHandlerTests` | `Handle_LessonMissing_ThrowsLessonNotFound` | Unchanged |
| H11 | `StartQuizSessionHandlerTests` | `Handle_LessonDraft_ThrowsLessonNotFound` | Unchanged |
| H12 | `StartQuizSessionHandlerTests` | `Handle_NoServableQuestions_ThrowsSessionNoServableQuestions` | `GetServableIdsInLessonAsync` returns `[]`. It throws `BusinessRuleViolationCoreException` `SESSION_NO_SERVABLE_QUESTIONS`; `GetAttemptSummariesAsync` `DidNotReceive`; `AddAsync` and `SaveChangesAsync` `DidNotReceive` |
| P1 | `AttemptSummaryPersistenceTests` | `GetAttemptSummariesAsync_MixedHistory_AggregatesPerQuestion` | Seed a lesson with 3 questions and a student. Session 1 (all 3): q1 grade 0, q2 grade 1, q3 grade 0.5; `Submit`. Session 2 (q1, q2): q1 grade 1, q2 grade 0; `Submit`. Query with threshold 0.8 through `ISessionRepository` from a fresh scope. q1 is `(2, 1)` with `IsLastAttemptCorrect` true and bucket CorrectOnce. q2 is `(2, 1)` with `LastCorrectAt < LastAttemptedAt` and bucket LastWrong. q3 is `(1, 0)` with `LastCorrectAt` null and bucket LastWrong (partial counts as wrong) |
| P2 | `AttemptSummaryPersistenceTests` | `GetAttemptSummariesAsync_OtherStudentAndUnrequestedQuestion_AreExcluded` | Another student answers q1, and the student answers q2. Query for `[q1]` only returns an empty list |
| P3 | `AttemptSummaryPersistenceTests` | `GetAttemptSummariesAsync_TestModeSession_Counts` | Session with `isTestMode: true`, q1 answered and submitted. The summary for q1 has `AttemptCount == 1` |
| P4 | `AttemptSummaryPersistenceTests` | `GetAttemptSummariesAsync_OlderQuestionVersion_Counts` | q1 answered at version 1 and submitted; `SessionTestData.EditQuestionContentAsync` bumps it to version 2. The summary for q1 still has `AttemptCount == 1` |
| P5 | `ServableQuestionSpecificationPersistenceTests` | `GetServableIdsInLessonAsync_MixedStates_ReturnsOnlyServableInLesson` | Using the existing `SeedAsync()`, for `PublishedLessonId`, the result `Equal(seed.ServableQuestionId)` |
| E1 | `StartQuizSessionEndpointTests` | `Post_AfterFinishedQuiz_ServesUnseenThenLastWrongOldestFirst` | Seed 6 questions and a student. Start (5). Answer the served items in position order: the 1st with "b" (correct), the 2nd–5th with "a" (wrong). Finish (200). Start again (5): the items in position order are `[theOneUnseen, wrong2, wrong3, wrong4, wrong5]` (`Equal`); the correct-once question is absent; `OnlyHaveUniqueItems`; the new session id differs from the first |
| E2 | `StartQuizSessionEndpointTests` | `Post_AfterFinishedQuiz_SmallPoolServesWholePoolOnce` | Seed 2 questions. Start (5), answer both wrong, finish. Start (5) returns 2 items, unique, equivalent to both ids |

Handler test stubs (in the constructor):
- `_questionRepository.GetServableIdsInLessonAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(call => _pool.Select(x => x.Id).ToList())`
- `_sessionRepository.GetAttemptSummariesAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<decimal>(), Arg.Any<CancellationToken>()).Returns(_summaries)`
- `_questionRepository.FindAsync(Arg.Any<Expression<Func<Question, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Question>, IQueryable<Question>>?>(), Arg.Any<Func<IQueryable<Question>, IOrderedQueryable<Question>>?>(), Arg.Any<bool>()).Returns(call => _pool.Where(call.Arg<Expression<Func<Question, bool>>>().Compile()).ToList())`

The handler is built with `Options.Create(new SessionsOptions())`, `Options.Create(new MasteryOptions())` and `new Random(Seed)`. `_pool` is a `List<Question>` from `_builder.BuildQuestions(n)`, and its revisions are added to `_revisions`. H5 overrides the `FindAsync` stub to drop one question.

## Definition of done
- [ ] The `QuestionSelector`, `QuestionAttemptSummary` and `QuestionSelectionBucket` files exist exactly as specified in `Elmanhg.Domain/Sessions/Selection/`, with no I/O and no DI.
- [ ] The bucket definitions match D3. The correct threshold comes from `MasteryOptions.CorrectThreshold` (0.8, validated at start) and is not hard-coded anywhere.
- [ ] Within-bucket order matches D4: shuffle, oldest-first, shuffle, weighted least-recent. Candidates are sorted before randomness (D10).
- [ ] The only random source is the injected `Random`: `Random.Shared` is registered in `AddApplication`, and there is no `new Random()` in production code.
- [ ] `GetAttemptSummariesAsync` is a single GROUP BY query with no `IsTestMode` or `QuestionVersion` filter and no manual `IsDeleted`.
- [ ] `GetRandomServableInLessonAsync` is removed from the interface, the implementation and the tests. `GetServableIdsInLessonAsync` uses `WhereServable`.
- [ ] The handler follows the `SelectQuestionsAsync` steps exactly, including the empty-pool short-circuit and the missing-question skip. The resume path does no selection queries.
- [ ] No migration, no new error code, and no controller, OpenAPI, Orval, Postman or `web/` change. `api/openapi/v1.json` shows no diff after build.
- [ ] `appsettings.example.json` and `ApiFactory` contain `Mastery:CorrectThreshold`.
- [ ] `docs/sessions.md` has the Selection section (D1, D3–D8, D11, D12, D14, D16), the updated lifecycle step 1, the Options row and the Test mode line. No other doc diverges; PRD §7.2 is implemented as written.
- [ ] All tests B1–B4, S1–S14, O1–O2, H1–H12, P1–P5 and E1–E2 exist with these names and pass. No existing test is weakened or skipped.
- [ ] `dotnet test api/ -c Release` is green with `api/Elmanhg.Api/appsettings.json` moved aside (CI parity), then restored.
- [ ] Style: file-scoped namespaces, one-line class declarations, `ConfigureAwait(false)` everywhere, one LINQ operator per line, no comments beyond the two WHY lines named above, and every file under about 100 lines.
