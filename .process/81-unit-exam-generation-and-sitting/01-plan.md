# Plan — [E6.S2] Unit exam generation and sitting (#81)

## Goal
A student (or an admin, in test mode) opens a unit's exam start screen and sees the blueprint summary. They start an exam built from the unit's blueprint (or the subject default). The generator draws the blueprint's type counts, follows the difficulty mix, prefers questions the student has not mastered, and draws across every lesson in the unit. On the exam screen all questions are on one page with a server-anchored countdown. Every answer is auto-saved as a draft, with no feedback, and a refresh resumes with the answers intact. The server rejects saves after the deadline plus a grace period and auto-submits expired exams. Submitting grades every saved answer against the served version and stores append-only attempts. Those attempts update mastery. The result screen shows a score out of 100, pass or fail, a per-lesson breakdown, the weakest objectives and a review of every question.

## Scope
**In:**
- **Domain:**
  - Session exam lifecycle: start, saved answers, deadline, submit.
  - Exam question selector and difficulty apportioning.
  - Per-lesson and weakest-objective breakdown.
  - Blueprint resolution and a start-time servability check.
- **Application:** 7 use cases (overview, start or resume, get, save answer, submit, list expired, auto-submit), plus the shared result, loader and submission helpers.
- **Infrastructure:** one migration and two new `Sessions` indexes; `IX_Sessions_OneOpenExam` gets a conflict mapping in `SaveChangesAsync`. Also one candidate query.
- **Api:** `ExamsController` and a `BackgroundService` that auto-submits expired exams.
- **Web:** feature `exam` with 3 routes (start, sitting, result). Progress history links exams. The progress unit table links to the exam start.
- **Docs, Postman and config:** docs, Postman, `appsettings.example.json`, `ApiFactory`, OpenAPI and Orval regeneration.

**Out:**
- **Other stories:**
  - Multi-unit exams: #82.
  - Best score and the attempts list on the start and result screens: #83.
  - The Avatar refusing to answer during an exam: E8 #91.
  - The free-tier paywall on exams: E7 #87.
  - The unit page entry point: #85. #81 adds the progress-page link instead.
- **Per-question time in exams:** not measured. Exam attempts store `TimeTakenMilliseconds = 0`, and the exam's time is `SubmittedAt − StartedAt` (Decision 12).

**Deferred:**
- **Lesson-open gate (PRD §7.4 bullet 1, §19 Q4).** PRD: the exam is "available once the student has opened every lesson (configurable; default: no gate)". Nothing in the repo records that a student opened a lesson; student browsing ships in #85. The default (no gate) is what ships here. Follow-up issue: "Configurable unit-exam gate on opened lessons (needs lesson-open tracking from #85)".

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | Do exam attempts count toward mastery? | **Yes.** At submission, each new attempt in a non-test exam updates `QuestionMastery` exactly as a quiz answer does (`Start` or `Record`). Test-mode exams never write mastery. The day streak stays quiz-only. | PRD §7.3 defines an attempt as an answer "inside any quiz or exam" and mastery as the two most recent attempts. PRD §7.6 defines the streak on quiz attempts. |
| 2 | Where does a draft answer live before submission? | New nullable columns on `SessionItem`: `SavedAnswer` (jsonb, canonical form) and `AnswerSavedAt`. No `Attempt` row exists until submission. | `Attempts` is append-only (DB trigger), with one row per session and question. Drafts must be overwritable. A new table would duplicate the item key. |
| 3 | What is graded at submission? | Every item with a `SavedAnswer` gets one `Attempt`, graded through `QuestionRevision.Grade` at the item's served version. Unanswered items get no attempt and count 0. | Reuses the #74 grading path. PRD: "one student answer" per attempt. The score formula already counts missing attempts as 0. |
| 4 | Score | `ScorePercent = round(Σ attempt.Score / Σ item.MaxScore × 100, 2)`, the existing formula, extracted to `CalculateScorePercent()`. `IsPassed = ScorePercent >= PassMark`. | PRD §7.4: score out of 100. Same formula as quizzes. |
| 5 | Deadline enforcement | The session stores `TimeLimitMinutes`, `PassMark` and `Deadline = StartedAt + TimeLimitMinutes` at start (a blueprint edit never changes a started exam). A save is rejected with 400 `EXAM_TIME_EXPIRED` when `now > Deadline + Exams:DeadlineGraceSeconds` (30 s). Submit is always allowed and grades what was saved. | The grace period absorbs the client's last in-flight save at 0:00. Submitting late cannot add answers, because saves are already closed. |
| 6 | Auto-submit | Three paths. (a) The client submits when its countdown hits 0 or a save returns `EXAM_TIME_EXPIRED`. (b) `POST /api/exams/units/{unitId}` on an expired open exam of that unit submits it and returns it. (c) `ExpiredExamSubmissionWorker` sweeps every `Exams:AutoSubmitIntervalSeconds` (60) for open exams whose `Deadline + grace` has passed and sends `AutoSubmitExamCommand` per session. `GET` never mutates. | "Server-side deadline enforcement and auto-submit" must hold even if the student never returns. One command per session isolates a failing session. The worker follows the Morabh timer → mediator pattern. |
| 7 | Untimed exams | `TimeLimitMinutes` null means `Deadline` null: never expires, no countdown, stays open until submitted. | Blueprint time limit is optional (PRD §10.2). |
| 8 | Several exams at once | At most **one open exam per student** (any kind), enforced by the unique partial index `IX_Sessions_OneOpenExam`. Starting another unit's exam while one is open returns 409 `EXAM_ALREADY_IN_PROGRESS`. The overview exposes `inProgressExam`. | Prototype `startExam`: "لديك امتحان جارٍ بالفعل." and `activeExam(sid)` is student-wide. It also keeps PRD rule 10 (Avatar vs in-progress exam) well defined. |
| 9 | Resume | `POST /api/exams/units/{unitId}` with an open exam for that unit resumes it (`Resume()`), with no new draw. `GET /api/exams/{id}` returns saved answers. | Mirrors the quiz start-or-resume. PRD §14: a refresh resumes. |
| 10 | Blueprint resolution | Pure domain `ExamBlueprintResolution.ForUnit`: the unit's own blueprint, else the subject default, else null. Null returns 400 `UNIT_EXAM_NO_BLUEPRINT`. | `docs/exam-blueprints.md` "Resolution for exams" step 1. |
| 11 | Shortfall at start | `ExamBlueprint.EnsureServable(servableByType)` re-runs `ExamBlueprintShortfall.Find` against the unit's live servable pool. A shortfall returns 400 `EXAM_SHORTFALL` with context `types` (`"Mcq 1/2"`). | Step 2 of the same doc. A separate code, because `EXAM_BLUEPRINT_SHORTFALL` says "not saved". |
| 12 | Exam time | Exam attempts get `TimeTakenMilliseconds = 0` (named constant with a WHY comment). The result exposes `elapsedMilliseconds = (SubmittedAt ?? now) − StartedAt`. | All questions share one page, so per-question time is not observable. The wall time is what the timer measured. |
| 13 | Selection algorithm | Per blueprint type, in `QuestionType` order:<br>1. Sort the pool by id.<br>2. With a difficulty mix, apportion the count across Easy/Medium/Hard by largest remainder (ties Easy < Medium < Hard). For each difficulty, take up to its target from that difficulty's preferred list.<br>3. Fill the rest of the count from the remaining candidates of the type, again preferred first.<br>4. Preferred list = the shuffled not-mastered candidates, then the shuffled mastered ones.<br>5. Item order: grouped by type in `QuestionType` order; within a type, Easy → Medium → Hard, random within a difficulty. | PRD §7.4: fixed counts per type; difficulty is "a target followed as far as the pool allows" (`docs/exam-blueprints.md`); "prefer not mastered, then random". Deterministic under a seeded `Random`, like `QuestionSelector`. |
| 14 | "Mastered" for selection | `QuestionMastery.IsMastered == true` for the student. | #77's single definition. |
| 15 | Breakdown weighting | Per lesson and per objective: `Σscore / ΣmaxScore × 100` (2 decimals). "Correct" count uses `Mastery:CorrectThreshold`. Lesson and objective come from the live `Question.LessonId`/`ObjectiveId`; objective text and order come from live `LessonObjective`s. A soft-deleted objective is ignored; an item whose question or lesson cannot be loaded is left out of the breakdown but still counts in the score. | Consistent with the session score. The revision snapshot carries no lesson or objective. |
| 16 | Weakest objectives | Objectives with `ScorePercent < 100`. Ascending by `ScorePercent`, then lesson order, objective order, id. Take `Exams:WeakestObjectiveCount` (3). An empty list means "no weak objectives". | Prototype `vExamResult`: `p < 1`, sorted, `slice(0,3)`, with the message «لا توجد أهداف ضعيفة. أحسنت!». |
| 17 | What is revealed | While open: items carry `savedAnswer` only; `attempt`, `correctAnswer` and `explanation` are null. After submit: `correctAnswer` and `explanation` for every item, and `attempt` for answered ones. | PRD §7.4 "no per-question feedback until submission". Reuses `SessionResultGenerator.GenerateItem` (reveal = attempt ∨ submitted). |
| 18 | Quiz endpoints on exam sessions | `POST /api/sessions/{id}/answers` and `/finish` add `Kind == Quiz` to their lookup, so an exam id returns 404 `SESSION_NOT_FOUND`. Domain guards: `RecordAttempt` and `Submit()` on an exam, and `SaveExamAnswer`/`SubmitExam` on a quiz, throw `InvalidOperationException`. `GET /api/sessions/{id}` is unchanged (it leaks nothing for an open exam). | Otherwise `Finish` would submit an exam with 0 and drop its saved answers. |
| 19 | API home | New `ExamsController` at `api/exams`. The sitting routes (`{sessionId}`…) are kind-agnostic, so #82 adds only `POST api/exams/multi-unit`. | Keeps `SessionsController` quiz-only. |
| 20 | Shared submission logic | Static `ExamSubmission.SubmitAsync` (Application/Exams/Shared), used by Start (expired resume), SubmitExam and AutoSubmitExam. | Three handlers need identical grading and mastery. A static helper beside the result generators, not a service. |
| 21 | Empty or cleared answers | The web saves every change, including a cleared multi-select, fill or short answer. The server stores any readable shape, and a cleared answer grades 0. The client's "unanswered" count uses `isAnswerEmpty` on local state. | No new "clear" endpoint. The score is identical. |
| 22 | Result screen actions | «إعادة الامتحان» links to `/student/exam-start/{unitId}`. «درّب الآن» per lesson links to `/student/lesson/{lessonId}/practice`. «تقدّمي» links to `/student/progress`. | Prototype `vExamResult`. Retake is free by construction (a new start after submit); the best-score list is #83. |
| 23 | Countdown | Anchored to the server clock: `offset = Date.parse(serverNow) − dataUpdatedAt`, `remaining = deadline − (Date.now() + offset)`, ticked each second. It turns `text-danger`, with a one-time `role="status"` notice, at ≤ 2 minutes. At 0: "Time is up, submitting…" and auto-submit. | Design prompt §2.4: "turning `--bad` in the last two minutes". The prototype's 1 minute is superseded by the design prompt. |
| 24 | Auto-save | Per question, debounced 800 ms (`examAutoSaveDelayMilliseconds`). Saves for the same question are chained so the newest always lands last. `flush()` sends all pending saves before submit. Header status: «محفوظ تلقائيًا» / «جارٍ الحفظ…» / «تم الحفظ {time}» / error. | Prototype `examSave` + «محفوظ تلقائيًا». Avoids a request per keystroke. |
| 25 | Admin | An admin can start an exam: `IsTestMode = true`. No mastery, no history, and it still counts as the admin's one open exam. | Same rule as quizzes (`docs/sessions.md` Test mode). |
| 26 | Entry point before #85 | The progress page unit table gains a column linking «امتحان الوحدة» to `/student/exam-start/{unitId}`. History rows link exams: submitted → «عرض» `/student/exam-result/{id}`, open → «متابعة» `/student/exam/{id}`. | `docs/progress.md`: "Exams have no link until the exam result pages (E6, #81)". Without the table link, the flow would be reachable only by typing the URL. |

## Morabh reuse
| Piece | Source |
|---|---|
| Timer job that sends a mediator request each tick | Pattern from `D:\Personal\Projects\Projects\Morabh\repos\apis\Morabh.Jobs\Functions\InstallmentOverdueReminderFunction.cs`, re-expressed as a `BackgroundService` + `PeriodicTimer` (no Azure Functions; `Core.Azure` is not vendored). |
| Exceptions, validation extensions, `IRepository<T>`, `PageData<T>` | Already vendored in `api/core-libraries` (Core.Errors, Core.Validation, Core.DDD). |
| Exam generation, sitting, breakdown, web screens | New — no Morabh equivalent (Morabh is BNPL). |

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Domain/Sessions/Session.cs` | Add `public int? TimeLimitMinutes { get; private set; }`, `public int? PassMark { get; private set; }`, `public DateTimeOffset? Deadline { get; private set; }` and `public bool IsExam => Kind != SessionKind.Quiz;`. Add `private static DateTimeOffset ToMicroseconds(DateTimeOffset value) => value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMicrosecond));`, move the existing WHY comment onto it, and make `UtcNowToMicroseconds()` return `ToMicroseconds(DateTimeOffset.UtcNow)`. |
| `api/Elmanhg.Domain/Sessions/Session.Answering.cs` | `RecordAttempt`: first statement `if (IsExam) { throw new InvalidOperationException("Exam answers are saved with SaveExamAnswer."); }`. |
| `api/Elmanhg.Domain/Sessions/Session.Submission.cs` | `Submit()`: first statement `if (IsExam) { throw new InvalidOperationException("Exams are submitted with SubmitExam."); }`. Extract `private decimal CalculateScorePercent()` (the existing rounding expression) and use it. |
| `api/Elmanhg.Domain/Sessions/SessionItem.cs` | Add `public string? SavedAnswer { get; private set; }`, `public DateTimeOffset? AnswerSavedAt { get; private set; }` and `internal void SaveAnswer(string answer, DateTimeOffset savedAt) { SavedAnswer = answer; AnswerSavedAt = savedAt; }`. |
| `api/Elmanhg.Domain/ExamBlueprints/ExamBlueprint.cs` | Add `public void EnsureServable(IReadOnlyDictionary<QuestionType, int> servable)` (see Domain behaviour). Extract `private static string Describe(IEnumerable<ExamTypeShortfall> shortfalls) => string.Join(", ", shortfalls.Select(x => $"{x.Type} {x.Available}/{x.Required}"));` and use it in `EnsureNoShortfall` too. |
| `api/Elmanhg.Domain/Questions/IQuestionRepository.cs` | Add `Task<List<ExamCandidate>> GetServableExamCandidatesAsync(IReadOnlyCollection<Guid> unitIds, CancellationToken cancellationToken);` |
| `api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs` | New group `// EXAMS`: `ExamTimeExpired = "EXAM_TIME_EXPIRED"`, `ExamShortfall = "EXAM_SHORTFALL"`. |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | New group `// EXAMS`: `ExamAlreadyInProgress = "EXAM_ALREADY_IN_PROGRESS"`, `UnitExamNoBlueprint = "UNIT_EXAM_NO_BLUEPRINT"`. |
| `api/Elmanhg.Application/DependencyInjection.cs` | `services.AddOptions<ExamsOptions>().BindConfiguration(ExamsOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();` |
| `api/Elmanhg.Application/Sessions/SubmitAnswer/SubmitAnswerHandler.cs` | Lookup predicate becomes `x => x.Id == request.SessionId && x.StudentId == userId && x.Kind == SessionKind.Quiz`. |
| `api/Elmanhg.Application/Sessions/FinishSession/FinishSessionHandler.cs` | Same `&& x.Kind == SessionKind.Quiz`. |
| `api/Elmanhg.Infrastructure/Questions/QuestionRepository.cs` | Implement `GetServableExamCandidatesAsync`: `_dbSet.WhereServable(_context.Set<Lesson>()).Join(_context.Set<Lesson>(), question => question.LessonId, lesson => lesson.Id, (question, lesson) => new { question, lesson.UnitId }).Where(x => unitIds.Contains(x.UnitId)).OrderBy(x => x.question.Id).Select(x => new ExamCandidate(x.question.Id, x.question.LessonId, x.question.Type, x.question.Difficulty)).AsNoTracking().ToListAsync(cancellationToken)`. |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | See "Persistence" below. |
| `api/Elmanhg.Infrastructure/Migrations/*` | `dotnet ef migrations add AddExamSittings -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api` (migration, Designer and snapshot). Review it: only AddColumn and CreateIndex, no drops. |
| `api/Elmanhg.Api/Program.cs` | After `builder.Services.AddInfrastructure();` add `builder.Services.AddHostedService<ExpiredExamSubmissionWorker>();` |
| `api/Elmanhg.Api/Resources/Messages.en.resx`, `Messages.ar.resx` | 4 new keys (see Error codes). |
| `api/Elmanhg.Api/appsettings.example.json` | `"Exams": { "DeadlineGraceSeconds": 30, "AutoSubmitEnabled": true, "AutoSubmitIntervalSeconds": 60, "AutoSubmitBatchSize": 50, "WeakestObjectiveCount": 3 }` |
| `api/openapi/v1.json` | Regenerated by `dotnet build`. |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | Add `builder.UseSetting("Exams:AutoSubmitEnabled", "false");` with the comment `// The sweep would race tests that expire sessions on purpose; AutoSubmitExam is exercised directly through the mediator.` Add in-memory keys `Exams:DeadlineGraceSeconds=30`, `Exams:AutoSubmitIntervalSeconds=60`, `Exams:AutoSubmitBatchSize=50`, `Exams:WeakestObjectiveCount=3`. |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | Append `eighteenth => eighteenth.Should().EndWith("_AddExamSittings")` to the migration list (accepted pattern). |
| `api/Elmanhg.Tests/Application/Features/Sessions/SubmitAnswer/SubmitAnswerHandlerTests.cs` | Add 1 test (Test plan #D1). |
| `api/Elmanhg.Tests/Application/Features/Sessions/FinishSession/FinishSessionHandlerTests.cs` | Add 1 test (Test plan #D2). |
| `postman/elmanhg.postman_collection.json` | New folder `Exams` right after `Sessions`, requests in this order: `Get unit exam overview`, `Start unit exam`, `Get exam session`, `Save exam answer`, `Submit exam`. Variables `{{unitId}}`, `{{examSessionId}}` (set by the Start test script from `id`), `{{examQuestionId}}` (set from `items[0].questionId`). |
| `web/src/features/quiz/index.ts` | Also export `toQuizQuestion`, `fromAnswerPayload`, `isAnswerEmpty` (from `./api/quizItem`), `choiceReview`, `describeCorrectAnswer` (from `./api/correctAnswer`), `CorrectAnswer` and `QuizReviewItem` (components). |
| `web/src/app/i18n.ts` | Register namespace `exam` (`examLocales.ar`/`.en`), and add `'exam'` to `ns`. |
| `web/src/shared/i18n/en.json`, `ar.json` | `errors`: the 4 new codes (see Error codes; no placeholders on the web). |
| `web/src/features/progress/api/sessionHistory.ts` | `SessionLink.to` adds `'/student/exam-result/$sessionId' \| '/student/exam/$sessionId'`. Non-quiz kinds: submitted → `{ to: '/student/exam-result/$sessionId', labelKey: 'history.view' }`, open → `{ to: '/student/exam/$sessionId', labelKey: 'history.continue' }`. |
| `web/src/features/progress/api/sessionHistory.test.ts` | Delete `it('gives no link for exams')`. Add W-P1 and W-P2. |
| `web/src/features/progress/pages/ProgressPage.history.test.tsx` | In `links finished quizzes to results and open quizzes to continue`, replace `expect(within(exam).queryByRole('link')).toBeNull();` with `expect(within(exam).getByRole('link', { name: 'View' })).toHaveAttribute('href', \`/student/exam-result/${examSessionId}\`);` (import `examSessionId` from `@/test/progressFixtures` if it is not already exported, and export it there). |
| `web/src/features/progress/components/UnitProgressTable.tsx` | Add `'exam'` to `headerKeys`. The new last cell is `<Link to="/student/exam-start/$unitId" params={{ unitId: unit.unitId }} className=…same link classes as SessionHistoryRow…>{t('subjects.startExam')}</Link>`. |
| `web/src/features/progress/i18n/en.json`, `ar.json` | `subjects.exam`: "Exam" / «الامتحان»; `subjects.startExam`: "Unit exam" / «امتحان الوحدة». |
| `web/src/shared/api/generated/**` | Regenerated with `npm --prefix web run gen:api`. |
| `web/src/routeTree.gen.ts` | Regenerated by the router plugin (`npx vite build` or `npm run dev`). |
| `docs/PRD.md` | See Docs. |
| `docs/sessions.md`, `docs/exam-blueprints.md`, `docs/mastery.md`, `docs/progress.md`, `docs/audit-log.md` | See Docs. |

### Persistence (AppDbContext)
- Constants:
  - `public const string OneOpenExamIndex = "IX_Sessions_OneOpenExam";`
  - `public const string OpenExamDeadlineIndex = "IX_Sessions_OpenExamDeadline";`
- `ConfigureSessions` → `Session`:
  - `builder.HasIndex(x => x.StudentId, OneOpenExamIndex).IsUnique().HasFilter("\"Kind\" <> 'Quiz' AND \"SubmittedAt\" IS NULL AND \"IsDeleted\" = false");`
  - `builder.HasIndex(x => x.Deadline, OpenExamDeadlineIndex).HasFilter("\"SubmittedAt\" IS NULL AND \"Deadline\" IS NOT NULL AND \"IsDeleted\" = false");`
- `SessionItem`: `builder.Property(x => x.SavedAnswer).HasColumnType("jsonb");`
- `SaveChangesAsync`: new catch before the blueprint one: `catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: OneOpenExamIndex }) { throw new ConflictCoreException(ErrorCodes.ExamAlreadyInProgress, innerException: exception); }`
- The migration adds:
  - `Sessions`: `TimeLimitMinutes integer NULL`, `PassMark integer NULL`, `Deadline timestamptz NULL`.
  - `SessionItems`: `SavedAnswer jsonb NULL`, `AnswerSavedAt timestamptz NULL`.
  - The 2 indexes.

## Files to create

### Domain (`api/Elmanhg.Domain`)
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `Sessions/Session.Exam.cs` | `public partial class Session` (ns `Elmanhg.Domain.Sessions`) | `public static Session StartUnitExam(Guid studentId, CurriculumUnit unit, ExamBlueprint blueprint, IReadOnlyList<Question> questions, IReadOnlyCollection<Lesson> lessons, bool isTestMode, DateTimeOffset now)`; `public bool IsPastDeadline(DateTimeOffset now, TimeSpan grace) => Deadline is not null && now > Deadline.Value + grace;`; `public void SaveExamAnswer(SessionItem item, string answer, TimeSpan grace, DateTimeOffset now)`. Bodies are in Domain behaviour. |
| 2 | `Sessions/Session.ExamSubmission.cs` | `public partial class Session` | `// Exam questions share one page; per-question time is not observable, the exam's time is SubmittedAt − StartedAt.` `private const int ExamAttemptTimeTakenMilliseconds = 0;` `public List<Attempt> SubmitExam(IReadOnlyDictionary<Guid, QuestionGrade> grades, DateTimeOffset now)` |
| 3 | `Sessions/Exams/ExamCandidate.cs` | `public sealed record ExamCandidate(Guid QuestionId, Guid LessonId, QuestionType Type, QuestionDifficulty Difficulty);` (ns `Elmanhg.Domain.Sessions.Exams`) | — |
| 4 | `Sessions/Exams/ExamDifficultyTargets.cs` | `public static class ExamDifficultyTargets` | `public static Dictionary<QuestionDifficulty, int> Apportion(int count, ExamDifficultyMix mix)`:<br>1. `exact = count × percent` per difficulty; floor `= exact / 100`, remainder `= exact % 100`.<br>2. Leftover `= count − Σfloor`, given +1 each to difficulties ordered by remainder descending, then `QuestionDifficulty` ascending.<br>3. Always returns all 3 keys. |
| 5 | `Sessions/Exams/ExamQuestionSelector.cs` | `public static class ExamQuestionSelector` | `public static List<Guid> Select(IReadOnlyCollection<ExamCandidate> candidates, IReadOnlySet<Guid> masteredQuestionIds, IReadOnlyList<ExamTypeCount> typeCounts, ExamDifficultyMix? difficultyMix, Random random)`. Steps:<br>1. `pool = candidates.DistinctBy(x => x.QuestionId).OrderBy(x => x.QuestionId)`.<br>2. For each `typeCount` with `Count > 0`, ordered by `Type`: `ofType = pool` where `Type`. `picked = []`.<br>3. If the mix is not null: for `d` in Easy, Medium, Hard, `picked.AddRange(Preferred(ofType.Where(x => x.Difficulty == d && !picked.Contains(x))).Take(targets[d]))`.<br>4. Then `picked.AddRange(Preferred(ofType.Where(x => !picked.Contains(x))).Take(typeCount.Count − picked.Count))`.<br>5. Append `picked.OrderBy(x => x.Difficulty).Select(x => x.QuestionId)` (stable sort).<br>`private static IEnumerable<ExamCandidate> Preferred(IEnumerable<ExamCandidate> list, IReadOnlySet<Guid> mastered, Random random)`: arrays `notMastered`, `masteredOnes`, each `random.Shuffle`d, concatenated. A short type yields what exists (no throw). |
| 6 | `Sessions/Exams/ExamItemPlacement.cs` | `public sealed record ExamItemPlacement(Guid QuestionId, Guid LessonId, int LessonOrder, Guid? ObjectiveId, int ObjectiveOrder);` | — |
| 7 | `Sessions/Exams/ExamShare.cs` | `public sealed record ExamShare(Guid Id, Guid LessonId, int LessonOrder, int ObjectiveOrder, int QuestionCount, int CorrectCount, decimal Score, int MaxScore)` | `// Matches the session score precision.` `private const int PercentDecimals = 2;` `public decimal ScorePercent => MaxScore == 0 ? 0m : Math.Round(Score * 100m / MaxScore, PercentDecimals, MidpointRounding.AwayFromZero);` |
| 8 | `Sessions/Exams/ExamBreakdown.cs` | `public static class ExamBreakdown` | **`public static List<ExamShare> ByLesson(Session session, IReadOnlyCollection<ExamItemPlacement> placements, decimal correctThreshold)`:**<br>1. Returns `[]` unless `session.IsSubmitted`.<br>2. Rows are items joined to placements by `QuestionId`; items without a placement are skipped.<br>3. Group by `LessonId` into `ExamShare(LessonId, LessonId, LessonOrder, 0, count, correct, Σ(attempt?.Score ?? 0), ΣMaxScore)`; correct means `attempt is not null && attempt.NormalisedScore >= correctThreshold`.<br>4. Order by `LessonOrder`, then `Id`.<br>**`public static List<ExamShare> WeakestObjectives(Session session, IReadOnlyCollection<ExamItemPlacement> placements, decimal correctThreshold, int count)`:**<br>1. `[]` unless submitted.<br>2. Only rows with `ObjectiveId` not null; group by `ObjectiveId` into `ExamShare(ObjectiveId, LessonId, LessonOrder, ObjectiveOrder, …)`.<br>3. Keep `Score < MaxScore`; order by `ScorePercent`, `LessonOrder`, `ObjectiveOrder`, `Id`; `Take(count)`. |
| 9 | `ExamBlueprints/ExamBlueprintResolution.cs` | `public static class ExamBlueprintResolution` (ns `Elmanhg.Domain.ExamBlueprints`) | `public static ExamBlueprint? ForUnit(IEnumerable<ExamBlueprint> blueprints, CurriculumUnit unit)`: `list.FirstOrDefault(x => x.UnitId == unit.Id) ?? list.FirstOrDefault(x => x.IsSubjectDefault && x.SubjectId == unit.SubjectId)`. |

### Application (`api/Elmanhg.Application`)
All handlers: `sealed class`, guard `ICurrentUserService.UserId` first where listed (`UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated)`), `.ConfigureAwait(false)` everywhere, and `now = timeProvider.GetUtcNow()` read once. `grace = examsOptions.Value.DeadlineGrace`. `threshold = masteryOptions.Value.CorrectThreshold`. The "exam lookup" predicate is `x => x.Id == request.SessionId && x.StudentId == userId && x.Kind != SessionKind.Quiz`, with `include: query => query.Include(x => x.Items).Include(x => x.Attempts).AsSplitQuery()`.

| # | Path | Type | Contract |
|---|------|------|----------|
| 10 | `Shared/Options/ExamsOptions.cs` | `public sealed class ExamsOptions` | `SectionName = "Exams"`. Properties:<br>`[Range(0, 600)] int DeadlineGraceSeconds = 30`<br>`bool AutoSubmitEnabled = true`<br>`[Range(5, 3600)] int AutoSubmitIntervalSeconds = 60`<br>`[Range(1, 1000)] int AutoSubmitBatchSize = 50`<br>`[Range(1, 20)] int WeakestObjectiveCount = 3`<br>`public TimeSpan DeadlineGrace => TimeSpan.FromSeconds(DeadlineGraceSeconds);` |
| 11 | `Exams/Shared/ExamSessionResult.cs` | records (ns `Elmanhg.Application.Exams.Shared`) | `public sealed record ExamSessionResult(Guid Id, string Kind, bool IsTestMode, string? SubjectName, List<ExamUnitResult> Units, DateTimeOffset StartedAt, int? TimeLimitMinutes, DateTimeOffset? Deadline, DateTimeOffset ServerNow, int PassMark, DateTimeOffset? SubmittedAt, decimal? ScorePercent, bool? IsPassed, long ElapsedMilliseconds, List<ExamItemResult> Items, List<ExamLessonResult> Lessons, List<ExamObjectiveResult> WeakestObjectives);` `public sealed record ExamUnitResult(Guid UnitId, string? Name);` Client-facing (names are plain strings; no `LocalizedText` exists). |
| 12 | `Exams/Shared/ExamItemResult.cs` | record | `public sealed record ExamItemResult(int Position, Guid QuestionId, int QuestionVersion, string Type, string Stem, JsonElement Body, int MaxScore, JsonElement? SavedAnswer, DateTimeOffset? AnswerSavedAt, AttemptResult? Attempt, JsonElement? CorrectAnswer, string? Explanation);` (`AttemptResult` from `Sessions.Shared`) |
| 13 | `Exams/Shared/ExamBreakdownResults.cs` | records | `public sealed record ExamLessonResult(Guid LessonId, string? Name, int QuestionCount, int CorrectCount, decimal Score, int MaxScore, decimal ScorePercent);` `public sealed record ExamObjectiveResult(Guid ObjectiveId, string Text, Guid LessonId, string? LessonName, int QuestionCount, decimal ScorePercent);` |
| 14 | `Exams/Shared/ExamAnswerSavedResult.cs` | record | `public sealed record ExamAnswerSavedResult(Guid QuestionId, DateTimeOffset AnswerSavedAt);` |
| 15 | `Exams/Shared/UnitExamOverviewResult.cs` | records | `public sealed record UnitExamOverviewResult(Guid UnitId, string UnitName, Guid SubjectId, string SubjectName, ExamBlueprintSummaryResult? Blueprint, bool IsAvailable, InProgressExamResult? InProgressExam);` `public sealed record ExamBlueprintSummaryResult(bool IsSubjectDefault, int QuestionCount, List<ExamTypeAvailabilityResult> TypeCounts, ExamDifficultyMixResult? DifficultyMix, int? TimeLimitMinutes, int PassMark);` `public sealed record ExamTypeAvailabilityResult(QuestionType Type, int Required, int Available);` `public sealed record InProgressExamResult(Guid SessionId, bool IsThisUnit);` (`ExamDifficultyMixResult` reused from `ExamBlueprints.Shared`) |
| 16 | `Exams/Shared/UnitExamOverviewResultGenerator.cs` | static | `public static UnitExamOverviewResult Generate(CurriculumUnit unit, Subject subject, ExamBlueprint? blueprint, IReadOnlyDictionary<QuestionType, int> available, Session? openExam)`:<br>• `TypeCounts` = `blueprint.GetTypeCounts()` mapped to `(Type, Count, available.GetValueOrDefault(Type))`.<br>• `IsAvailable = blueprint is not null && ExamBlueprintShortfall.Find(blueprint.GetTypeCounts(), available).Count == 0`.<br>• `InProgressExam = openExam is null ? null : new(openExam.Id, openExam.ScopeKey == new UnitExamScope(unit.Id).ToKey())`. |
| 17 | `Exams/Shared/ExamSessionResultGenerator.cs` | static | `public static ExamSessionResult Generate(Session session, IReadOnlyCollection<QuestionRevision> revisions, string? subjectName, List<ExamUnitResult> units, List<ExamLessonResult> lessons, List<ExamObjectiveResult> weakestObjectives, DateTimeOffset now, ILocalizer localizer)`: items ordered by `Position`, each `GenerateItem`.<br>• `ElapsedMilliseconds = Math.Max(0, (long)((session.SubmittedAt ?? now) − session.StartedAt).TotalMilliseconds)`.<br>• `PassMark = session.PassMark.GetValueOrDefault()`.<br>• `IsPassed = session.ScorePercent is null ? null : session.ScorePercent >= session.PassMark`.<br>• `Kind = session.Kind.ToString()`.<br>`public static ExamItemResult GenerateItem(Session session, SessionItem item, QuestionRevision revision, ILocalizer localizer)`:<br>• `var shown = SessionResultGenerator.GenerateItem(session, item, revision, localizer);` then copy its fields.<br>• `SavedAnswer = item.SavedAnswer is null ? null : JsonDocument.Parse(...).RootElement.Clone()` (use a `using` document).<br>• `AnswerSavedAt = item.AnswerSavedAt`.<br>Revision lookup is the same as `SessionResultGenerator.FindRevision` (`QuestionId` + `Version`; missing throws `InvalidOperationException`). |
| 18 | `Exams/Shared/ExamBreakdownResultGenerator.cs` | static | `public static List<ExamLessonResult> Lessons(IEnumerable<ExamShare> shares, IReadOnlyCollection<Lesson> lessons)`: name = the lesson's `Name` or null.<br>`public static List<ExamObjectiveResult> Objectives(IEnumerable<ExamShare> shares, IReadOnlyCollection<Lesson> lessons)`: text from `lesson.Objectives` by `Id`; a share whose objective is not found is skipped. |
| 19 | `Exams/Shared/ExamSessionResultLoader.cs` | static | `public static async Task<ExamSessionResult> LoadAsync(Session session, IReadOnlyCollection<QuestionRevision> revisions, IQuestionRepository questionRepository, ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, ISubjectRepository subjectRepository, decimal correctThreshold, int weakestObjectiveCount, DateTimeOffset now, ILocalizer localizer, CancellationToken cancellationToken)`:<br>1. `unitId = UnitExamScope.FromJson(session.Scope).UnitId`.<br>2. `unit = unitRepository.GetByIdAsync(unitId, asNoTracking: true)`; `subject = unit is null ? null : subjectRepository.GetByIdAsync(unit.SubjectId, asNoTracking: true)`.<br>3. `units = [new ExamUnitResult(unitId, unit?.Name)]`.<br>4. If not submitted: `Generate(…, lessons: [], weakestObjectives: [], …)`.<br>5. Otherwise:<br>&nbsp;&nbsp;a. `questions = questionRepository.FindAsync(x => ids.Contains(x.Id), asNoTracking: true)`.<br>&nbsp;&nbsp;b. `lessons = lessonRepository.FindAsync(x => lessonIds.Contains(x.Id), include: q => q.Include(x => x.Objectives), asNoTracking: true)`.<br>&nbsp;&nbsp;c. `placements` = one per question whose lesson loaded: `new ExamItemPlacement(q.Id, lesson.Id, lesson.Order, objective?.Id, objective?.Order ?? 0)`, where `objective = lesson.Objectives.FirstOrDefault(o => o.Id == q.ObjectiveId)`.<br>&nbsp;&nbsp;d. `Generate` with `ExamBreakdownResultGenerator.Lessons(ExamBreakdown.ByLesson(…))` and `.Objectives(ExamBreakdown.WeakestObjectives(…, weakestObjectiveCount))`. |
| 20 | `Exams/Shared/ExamSubmission.cs` | static | `public static async Task SubmitAsync(Session session, IReadOnlyCollection<QuestionRevision> revisions, IQuestionMasteryRepository questionMasteryRepository, decimal correctThreshold, DateTimeOffset now, CancellationToken cancellationToken)`:<br>1. Return if `session.IsSubmitted`.<br>2. `grades` = for each item with `SavedAnswer is { } answer`: the revision (`QuestionId` + `QuestionVersion`, missing throws `InvalidOperationException`) `.Grade(document.RootElement)`, where `using var document = JsonDocument.Parse(answer)`. Keyed by `QuestionId`.<br>3. `attempts = session.SubmitExam(grades, now)`.<br>4. Return if `session.IsTestMode || attempts.Count == 0`.<br>5. `masteries = questionMasteryRepository.FindAsync(x => x.StudentId == session.StudentId && ids.Contains(x.QuestionId), cancellationToken)` (tracked).<br>6. For each attempt: `MasteryAttempt.From(attempt)`; existing → `Record(masteryAttempt, correctThreshold)`, else collect `QuestionMastery.Start(session.StudentId, attempt.QuestionId, masteryAttempt)`.<br>7. `AddRangeAsync(new, …)` once when any. |
| 21 | `Exams/GetUnitExamOverview/GetUnitExamOverviewQuery.cs` | `public sealed record GetUnitExamOverviewQuery(Guid UnitId) : IRequest<UnitExamOverviewResult>;` | — |
| 22 | `Exams/GetUnitExamOverview/GetUnitExamOverviewValidator.cs` | validator | `RuleFor(x => x.UnitId).ValidateRequired(ErrorCodes.UnitIdRequired);` |
| 23 | `Exams/GetUnitExamOverview/GetUnitExamOverviewHandler.cs` | handler | Constructor: `(ICurriculumUnitRepository unitRepository, ISubjectRepository subjectRepository, IExamBlueprintRepository examBlueprintRepository, IQuestionRepository questionRepository, ISessionRepository sessionRepository, ICurrentUserService currentUserService)`. Handle:<br>1. User guard.<br>2. `unit = GetByIdAsync(asNoTracking)`; null → `NotFoundCoreException(UnitNotFound)`.<br>3. `subject = GetByIdAsync(unit.SubjectId, asNoTracking)`; null → `NotFoundCoreException(UnitNotFound)`.<br>4. `blueprints = examBlueprintRepository.FindAsync(x => x.SubjectId == unit.SubjectId && (x.UnitId == unit.Id \|\| x.UnitId == null), asNoTracking: true)`; `blueprint = ExamBlueprintResolution.ForUnit(blueprints, unit)`.<br>5. `available = ServableTypeCounts.ForUnit(await questionRepository.CountServableByUnitAndTypeAsync(unit.SubjectId, …), unit.Id)`.<br>6. `openExam = sessionRepository.FirstOrDefaultAsync(x => x.StudentId == userId && x.Kind != SessionKind.Quiz && x.SubmittedAt == null, asNoTracking: true)`.<br>7. `return UnitExamOverviewResultGenerator.Generate(…)`. |
| 24 | `Exams/StartUnitExam/StartUnitExamCommand.cs` | `public sealed record StartUnitExamCommand(Guid UnitId) : IRequest<ExamSessionResult>;` | — |
| 25 | `Exams/StartUnitExam/StartUnitExamValidator.cs` | validator | `UnitId` → `ValidateRequired(ErrorCodes.UnitIdRequired)` |
| 26 | `Exams/StartUnitExam/StartUnitExamHandler.cs` | handler | Constructor: `(ISessionRepository sessionRepository, ICurriculumUnitRepository unitRepository, ISubjectRepository subjectRepository, ILessonRepository lessonRepository, IQuestionRepository questionRepository, IExamBlueprintRepository examBlueprintRepository, IQuestionMasteryRepository questionMasteryRepository, IOptions<ExamsOptions> examsOptions, IOptions<MasteryOptions> masteryOptions, Random random, TimeProvider timeProvider, ICurrentUserService currentUserService, ILocalizer localizer)`. Handle:<br>1. User guard; `now`.<br>2. `unit` (`asNoTracking`); null → `NotFoundCoreException(UnitNotFound)`.<br>3. `open = sessionRepository.FirstOrDefaultAsync(x => x.StudentId == userId && x.Kind != SessionKind.Quiz && x.SubmittedAt == null, include items+attempts split)` (tracked).<br>4. If `open` is not null:<br>&nbsp;&nbsp;a. `open.ScopeKey != new UnitExamScope(unit.Id).ToKey()` → `ConflictCoreException(ErrorCodes.ExamAlreadyInProgress)`.<br>&nbsp;&nbsp;b. `revisions = GetRevisionsAsync(item ids)`.<br>&nbsp;&nbsp;c. `open.IsPastDeadline(now, grace)` ? `await ExamSubmission.SubmitAsync(open, revisions, questionMasteryRepository, threshold, now, ct)` : `open.Resume()`.<br>&nbsp;&nbsp;d. `SaveChangesAsync`; return `LoadAsync(open, revisions, …)`.<br>5. Resolve the blueprint as in the overview handler; null → `BadRequestCoreException(ErrorCodes.UnitExamNoBlueprint)`.<br>6. `candidates = questionRepository.GetServableExamCandidatesAsync([unit.Id])`.<br>7. `blueprint.EnsureServable(candidates.GroupBy(x => x.Type).ToDictionary(x => x.Key, x => x.Count()))`.<br>8. `mastered = (await questionMasteryRepository.FindAsync(x => x.StudentId == userId && x.IsMastered && candidateIds.Contains(x.QuestionId), ct, asNoTracking: true)).Select(x => x.QuestionId).ToHashSet()`.<br>9. `selectedIds = ExamQuestionSelector.Select(candidates, mastered, blueprint.GetTypeCounts(), blueprint.GetDifficultyMix(), random)`.<br>10. `questions = FindAsync(x => selectedIds.Contains(x.Id), asNoTracking)`, re-ordered by `selectedIds`, skipping missing.<br>11. `lessons = lessonRepository.FindAsync(x => lessonIds.Contains(x.Id), asNoTracking: true)`.<br>12. `session = Session.StartUnitExam(userId, unit, blueprint, questions, lessons, currentUserService.GetClaim(ClaimTypes.Role) == nameof(UserRole.Admin), now)`.<br>13. `AddAsync`, then `SaveChangesAsync` (once).<br>14. `revisions = GetRevisionsAsync(item ids)`; return `LoadAsync`.<br>Keep steps 6–10 in `private async Task<List<Question>> DrawAsync(...)`. |
| 27 | `Exams/GetExamSession/GetExamSessionQuery.cs` | `public sealed record GetExamSessionQuery(Guid SessionId) : IRequest<ExamSessionResult>;` | — |
| 28 | `Exams/GetExamSession/GetExamSessionValidator.cs` | validator | `SessionId` → `ValidateRequired(ErrorCodes.SessionIdRequired)` |
| 29 | `Exams/GetExamSession/GetExamSessionHandler.cs` | handler | Constructor: `(ISessionRepository, IQuestionRepository, ILessonRepository, ICurriculumUnitRepository, ISubjectRepository, IOptions<ExamsOptions>, IOptions<MasteryOptions>, TimeProvider, ICurrentUserService, ILocalizer)`. Handle:<br>1. User guard.<br>2. Exam lookup with `asNoTracking: true`; null → `NotFoundCoreException(SessionNotFound)`.<br>3. `revisions`.<br>4. `LoadAsync(…, now)`.<br>No mutation. |
| 30 | `Exams/SaveExamAnswer/SaveExamAnswerCommand.cs` | `public sealed record SaveExamAnswerCommand(Guid SessionId, Guid QuestionId, JsonElement Answer) : IRequest<ExamAnswerSavedResult>;` | — |
| 31 | `Exams/SaveExamAnswer/SaveExamAnswerValidator.cs` | validator (`IOptions<SessionsOptions>`) | • `SessionId` → `ValidateRequired(SessionIdRequired)`<br>• `QuestionId` → `ValidateRequired(QuestionIdRequired)`<br>• `Answer.Must(x => x.ValueKind == JsonValueKind.Object).WithErrorCode(QuestionAnswerInvalid)`<br>• `Answer.Must(x => x.GetRawText().Length <= options.AnswerMaxLength).WithErrorCode(AttemptAnswerTooLong)`, `.When(object)`<br>Mirrors `SubmitAnswerValidator`. |
| 32 | `Exams/SaveExamAnswer/SaveExamAnswerHandler.cs` | handler | Constructor: `(ISessionRepository sessionRepository, IQuestionRepository questionRepository, IOptions<ExamsOptions> examsOptions, TimeProvider timeProvider, ICurrentUserService currentUserService)`. Handle:<br>1. User guard.<br>2. Exam lookup, tracked, `include: query => query.Include(x => x.Items)`; null → 404 `SessionNotFound`.<br>3. `item = session.GetItem(QuestionId)`; null → `NotFoundCoreException(SessionQuestionNotFound)`.<br>4. `revision = (await GetRevisionsAsync([item.QuestionId])).FirstOrDefault(x => x.Version == item.QuestionVersion)`; null → `NotFoundCoreException(QuestionNotFound)`.<br>5. `type = revision.ReadSnapshot().Type`; `!QuestionAnswerRules.CanRead(type, answer)` → `ApplicationValidationCoreException(QuestionAnswerInvalid)`.<br>6. `session.SaveExamAnswer(item, QuestionAnswerRules.Canonicalize(type, answer), grace, now)`.<br>7. `SaveChangesAsync`.<br>8. `return new ExamAnswerSavedResult(item.QuestionId, item.AnswerSavedAt.GetValueOrDefault())`. |
| 33 | `Exams/SubmitExam/SubmitExamCommand.cs` | `public sealed record SubmitExamCommand(Guid SessionId) : IRequest<ExamSessionResult>;` | — |
| 34 | `Exams/SubmitExam/SubmitExamValidator.cs` | validator | `SessionId` → `ValidateRequired(SessionIdRequired)` |
| 35 | `Exams/SubmitExam/SubmitExamHandler.cs` | handler | Constructor: `(ISessionRepository, IQuestionRepository, IQuestionMasteryRepository, ILessonRepository, ICurriculumUnitRepository, ISubjectRepository, IOptions<ExamsOptions>, IOptions<MasteryOptions>, TimeProvider, ICurrentUserService, ILocalizer)`. Handle:<br>1. User guard.<br>2. Exam lookup, tracked; null → 404 `SessionNotFound`.<br>3. `revisions`.<br>4. `ExamSubmission.SubmitAsync(…)` (a no-op when already submitted).<br>5. `SaveChangesAsync` (once, always, mirroring Finish).<br>6. `LoadAsync`. |
| 36 | `Exams/GetExpiredExamSessionIds/GetExpiredExamSessionIdsQuery.cs` | `public sealed record GetExpiredExamSessionIdsQuery : IRequest<List<Guid>>;` | — |
| 37 | `Exams/GetExpiredExamSessionIds/GetExpiredExamSessionIdsHandler.cs` | handler | Constructor: `(ISessionRepository sessionRepository, IOptions<ExamsOptions> examsOptions, TimeProvider timeProvider)`.<br>`cutoff = now − grace`.<br>`page = FindPaginatedAsync(1, options.AutoSubmitBatchSize, ct, filter: x => x.Kind != SessionKind.Quiz && x.SubmittedAt == null && x.Deadline != null && x.Deadline < cutoff, orderBy: q => q.OrderBy(x => x.Deadline), asNoTracking: true)`.<br>Return `page.Items.Select(x => x.Id).ToList()`. No user guard (system caller). |
| 38 | `Exams/AutoSubmitExam/AutoSubmitExamCommand.cs` | `public sealed record AutoSubmitExamCommand(Guid SessionId) : IRequest;` | — |
| 39 | `Exams/AutoSubmitExam/AutoSubmitExamValidator.cs` | validator | `SessionId` → `ValidateRequired(SessionIdRequired)` |
| 40 | `Exams/AutoSubmitExam/AutoSubmitExamHandler.cs` | handler | Constructor: `(ISessionRepository, IQuestionRepository, IQuestionMasteryRepository, IOptions<ExamsOptions>, IOptions<MasteryOptions>, TimeProvider)`. Handle:<br>1. `session = FirstOrDefaultAsync(x => x.Id == request.SessionId && x.Kind != SessionKind.Quiz && x.SubmittedAt == null, include items+attempts split)` (tracked).<br>2. Return (no save) if null or `!session.IsPastDeadline(now, grace)`.<br>3. `revisions`; `ExamSubmission.SubmitAsync`; `SaveChangesAsync`. |

### Api (`api/Elmanhg.Api`)
| # | Path | Type | Contract |
|---|------|------|----------|
| 41 | `Controllers/Exams/ExamsController.cs` | `[ApiController] [Route("api/exams")] [Authorize] public class ExamsController(IMediator mediator) : ControllerBase` | 5 actions (see API surface). Each has `[Authorize(Policy = DefaultCodes.AssessmentsTake)]` and `[ProducesResponseType<T>(StatusCodes.Status200OK)]`. Thin: map, send, `Ok(result)`. |
| 42 | `Controllers/Exams/Requests.cs` | `public sealed record SaveExamAnswerRequest(JsonElement Answer);` | — |
| 43 | `Workers/ExpiredExamSubmissionWorker.cs` | `public sealed class ExpiredExamSubmissionWorker(IServiceScopeFactory scopeFactory, IOptions<ExamsOptions> examsOptions, TimeProvider timeProvider, ILogger<ExpiredExamSubmissionWorker> logger) : BackgroundService` (ns `Elmanhg.Api.Workers`) | **`ExecuteAsync(stoppingToken)`:** return if `!options.AutoSubmitEnabled`; `using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.AutoSubmitIntervalSeconds), timeProvider);` `while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false)) { await SweepAsync(stoppingToken).ConfigureAwait(false); }`.<br>**`SweepAsync`:**<br>1. New scope; `ISender.Send(new GetExpiredExamSessionIdsQuery())`, inside `try … catch (Exception exception) when (exception is not OperationCanceledException)` → `logger.LogError(exception, "Listing expired exams failed.")` and return.<br>2. For each id: a **new scope per id**, `Send(new AutoSubmitExamCommand(id))`, in the same `try/catch` → `logger.LogWarning(exception, "Auto-submit of exam {SessionId} failed.", id)`.<br>This is the only `IServiceScopeFactory` use (skill §8.9). |

### Web (`web/src`)
| # | Path | Type | Contract |
|---|------|------|----------|
| 44 | `features/exam/index.ts` | barrel | `export { ExamStartPage } from './pages/ExamStartPage'; export { ExamPage } from './pages/ExamPage'; export { ExamResultPage } from './pages/ExamResultPage'; export { examLocales } from './locales';` |
| 45 | `features/exam/locales.ts` | `export const examLocales = { ar, en };` | — |
| 46 | `features/exam/i18n/en.json`, `features/exam/i18n/ar.json` | locale | Keys in "Exam i18n keys" below. |
| 47 | `features/exam/api/examSession.ts` | pure helpers | `export const examAutoSaveDelayMilliseconds = 800;` `// design prompt §2.4: the countdown turns red in the last two minutes` `export const urgentCountdownMilliseconds = 120_000;`<br>`export function sortedExamItems(session: ExamSessionResult): ExamItemResult[]`<br>`export function remainingMilliseconds(deadline: string, clockOffsetMilliseconds: number, nowMilliseconds: number): number` = `Math.max(0, Date.parse(deadline) − (nowMilliseconds + clockOffsetMilliseconds))`<br>`export function splitCountdown(milliseconds: number): { minutes: number; seconds: number }` (total seconds = `Math.ceil(ms / 1000)`)<br>`export function isCountdownUrgent(milliseconds: number): boolean` = `milliseconds <= urgentCountdownMilliseconds`<br>`export function unitIdOf(session: ExamSessionResult): string \| null` = `session.units[0]?.unitId ?? null` |
| 48 | `features/exam/hooks/useStartExam.ts` | hook | `useStartExam(unitId: string): { start: () => void; isPending: boolean; errorCode: string \| null }`. Uses generated `useStartUnitExam`. `onSuccess`: `queryClient.setQueryData(getGetExamSessionQueryKey(result.id), result)`, then navigate to `/student/exam-result/$sessionId` if `result.submittedAt`, else `/student/exam/$sessionId`. `errorCode` computed as in `useStartQuiz`. |
| 49 | `features/exam/hooks/useExamAnswers.ts` | hook | `export type ExamSaveStatus = 'idle' \| 'saving' \| 'saved' \| 'error';` `useExamAnswers(session: ExamSessionResult, onTimeOver: () => void): { answerOf(questionId: string): QuestionAnswer; change(item: ExamItemResult, answer: QuestionAnswer): void; flush(): Promise<void>; status: ExamSaveStatus; lastSavedAt: Date \| null; unansweredCount: number }`.<br>**Initial state:** `Record<questionId, QuestionAnswer>` from `item.savedAnswer ? fromAnswerPayload(toQuizQuestion(item), item.savedAnswer) : emptyAnswer()` (a `useState` initializer).<br>**`change`:** set state, and keep a ref copy of the latest answers. Clear this question's pending timer, then `setTimeout(send, examAutoSaveDelayMilliseconds)`; pending timers live in `useRef<Map<string, ReturnType<typeof setTimeout>>>`.<br>**`send(questionId)`:** chain on `inFlight.get(questionId) ?? Promise.resolve()`; call `mutateAsync({ sessionId, questionId, data: { answer: toAnswerPayload(question, latest) } })` from generated `useSaveExamAnswer`. Status `'saving'`, then on success `'saved'` with `lastSavedAt = new Date(result.answerSavedAt)`. On failure with code `EXAM_TIME_EXPIRED` or `SESSION_ALREADY_SUBMITTED`: call `onTimeOver()`. Any other failure: status `'error'` and `toast.error(t([common:errors.<code>, common:errors.UNHANDLED_EXCEPTION]))`. The rejection is handled inside the chain so the chain never rejects.<br>**`flush`:** for every pending timer, clear it and `send` now; then `await Promise.all([...inFlight.values()])`.<br>**Unmount:** `useEffect` cleanup clears all timers.<br>**`unansweredCount`:** items where `isAnswerEmpty(toQuizQuestion(item), answerOf(id))`. |
| 50 | `features/exam/hooks/useExamCountdown.ts` | hook | `useExamCountdown(deadline: string \| null, serverNow: string, receivedAt: number, onExpire: () => void): number \| null`. Returns null when `deadline` is null. `offset = Date.parse(serverNow) − receivedAt`. A `useEffect` runs `setInterval(tick, 1000)` and ticks once immediately. `tick` sets `remainingMilliseconds(deadline, offset, Date.now())`; at 0 it calls `onExpire` once (ref flag) and clears the interval. |
| 51 | `features/exam/hooks/useSubmitExam.ts` | hook | `useSubmitExam(sessionId: string, flush: () => Promise<void>): { submit: () => void; isPending: boolean }`. `submit`: return if a ref flag is set; set it; `await flush()`; `mutation.mutate({ sessionId })` via generated `useSubmitExam`.<br>`onSuccess`: `setQueryData(getGetExamSessionQueryKey(sessionId), result)`, `void invalidateMastery(queryClient)`, navigate to `/student/exam-result/$sessionId`.<br>`onError`: toast the code, reset the flag, `invalidateQueries({ queryKey: getGetExamSessionQueryKey(sessionId) })`.<br>`isPending` = flag set or `mutation.isPending`. |
| 52 | `features/exam/components/ExamBlueprintSummary.tsx` | component | Props: `{ blueprint: ExamBlueprintSummaryResult }`. A card with:<br>• The «النموذج الافتراضي للمادة» badge when `isSubjectDefault`.<br>• A table with caption (sr-only) and columns type (`t('questions:types.<Type>')`), required, available. The available cell is `text-danger` when available < required.<br>• A time line: `start.timeLimit` with `{minutes}`, or `start.noTimeLimit`.<br>• Pass mark: `start.passMark`.<br>• The caption note `start.note`. |
| 53 | `features/exam/components/ExamStartActions.tsx` | component | Props: `{ overview: UnitExamOverviewResult }`. Uses `useStartExam`. Branches, in order:<br>1. `inProgressExam?.isThisUnit` → `<Button asChild variant="primary"><Link to="/student/exam/$sessionId">{t('start.continue')}</Link></Button>`.<br>2. `inProgressExam` (another unit) → a warning card (`border-warning bg-warning-soft`) with `start.otherInProgress` and a Link `start.openOther`.<br>3. `blueprint && !isAvailable` → a warning card `start.shortfall`.<br>4. `blueprint && isAvailable` → primary Button `start.start` (disabled while pending) and `role="alert"` for `errorCode` (`common:errors.<code>`).<br>5. Otherwise nothing. |
| 54 | `features/exam/components/ExamHeader.tsx` | component | Props: `{ title: string; remainingMilliseconds: number \| null; status: ExamSaveStatus; lastSavedAt: Date \| null }`. A sticky card (`sticky top-0 z-10 rounded-md border border-border bg-surface shadow-1`) with:<br>• An `h1` title.<br>• When `remaining !== null`: `<p role="timer" aria-live="off" className={cn('font-display text-h3 font-bold', urgent && 'text-danger')}>{t('exam.timeLeft', { minutes: formatNumber(m, lng), seconds: formatNumber(s, lng, 'arabic-indic', { minimumIntegerDigits: 2 }) })}</p>`. When urgent, also render once `<p role="status" className="sr-only">{t('exam.timeUrgent')}</p>`.<br>• Save status text in `aria-live="polite"`: idle → `exam.autoSaved`, saving → `exam.saving`, saved → `exam.savedAt` with `{time}` (`formatDate(lastSavedAt, lng, 'arabic-indic', { timeStyle: 'short' })`), error → `exam.saveFailed` (`text-danger`). |
| 55 | `features/exam/components/ExamQuestionCard.tsx` | component | Props: `{ item: ExamItemResult; total: number; answer: QuestionAnswer; onChange: (answer: QuestionAnswer) => void; disabled: boolean }`. An `article` labelled by an `h2` `exam.counter` `{position,total}`, a type badge, a caption `exam.marks` `{count: maxScore}`, and `<QuestionView question={toQuizQuestion(item)} answer={answer} onAnswerChange={onChange} disabled={disabled} />`. |
| 56 | `features/exam/components/ExamSubmitDialog.tsx` | component | Props: `{ open: boolean; unansweredCount: number; isPending: boolean; onOpenChange(open: boolean): void; onConfirm(): void }`. Uses `Dialog`/`DialogContent title={t('exam.confirmTitle')}`. Body `exam.confirmUnanswered` `{count}` only when count > 0. Buttons: secondary `exam.cancel` and primary `exam.confirmSubmit` (disabled while pending). |
| 57 | `features/exam/components/ExamRunner.tsx` | component | Props: `{ session: ExamSessionResult; receivedAt: number }`. Wires `useExamAnswers(session, submitter.submit)`, `useSubmitExam(session.id, answers.flush)` and `useExamCountdown(session.deadline, session.serverNow, receivedAt, submitter.submit)`. Renders:<br>• `ExamHeader` titled `exam.title` `{unit: session.units[0]?.name ?? ''}`.<br>• When remaining === 0: `<p role="status">{t('exam.timeUp')}</p>`.<br>• A `gap-3` list of `ExamQuestionCard`s, disabled while submitting.<br>• A bar that is sticky at the bottom on mobile (`sticky bottom-0 … lg:static`) with a primary `exam.submit` Button that opens `ExamSubmitDialog`; confirm calls `submitter.submit()`.<br>To stay under 120 lines, `useExamAnswers` must be created before `useSubmitExam`. Break the circular callback with a ref: `const submitRef = useRef(() => {})`; pass `() => submitRef.current()` to `useExamAnswers`, and set `submitRef.current = submitter.submit` in render. |
| 58 | `features/exam/components/ExamResultSummary.tsx` | component | Props: `{ session: ExamSessionResult }`. A card showing `result.score` `{score: Math.round(scorePercent)}` in display size. The badge is `result.passed` (`bg-success text-surface`) when `isPassed`, else `result.failed` `{passMark}` (`bg-danger text-surface`). Captions: `result.answered` `{answered: items with attempt, total}` and `result.time` from `splitDuration(elapsedMilliseconds)` (the quiz barrel does not export `splitDuration`, so implement the same split inline in `examSession.ts` as `splitDuration` — add it to file #47). |
| 59 | `features/exam/components/ExamLessonBreakdown.tsx` | component | Props: `{ lessons: ExamLessonResult[] }`. `h2` `result.byLesson`; a table (lesson name or `result.unknownLesson`, `result.percent` `{percent: Math.round(scorePercent)}`, and a Link `result.train` to `/student/lesson/$lessonId/practice`). Not rendered when empty. |
| 60 | `features/exam/components/ExamWeakestObjectives.tsx` | component | Props: `{ objectives: ExamObjectiveResult[] }`. `h2` `result.weakest`; a `ul` of `text — lessonName · percent`, or the `p` `result.noWeak` when empty. |
| 61 | `features/exam/components/ExamReviewItem.tsx` | component | Props: `{ item: ExamItemResult }`. If `item.attempt` → `<QuizReviewItem item={item} attempt={item.attempt} />`. Otherwise an `article` with:<br>• `h3` `result.reviewItem` `{position}`.<br>• `QuestionView` disabled with `emptyAnswer()` and `review={choiceReview(question, item.correctAnswer)}`.<br>• The `p` `result.unanswered`.<br>• `describeCorrectAnswer` → `CorrectAnswer` under the `result.correctAnswer` label.<br>• The explanation via `RichTextViewer` when present. |
| 62 | `features/exam/pages/ExamStartPage.tsx` | page | Props: `{ unitId: string }`. `useGetUnitExamOverview(unitId)`. States:<br>• Error → `ContentErrorState title=start.errorTitle` with retry.<br>• Pending → `ContentListSkeleton label=start.loading`.<br>• Data → `h1` `start.title` `{unit}`, then either `ExamBlueprintSummary`, or a card `start.noBlueprint` when `blueprint` is null, then `ExamStartActions`. |
| 63 | `features/exam/pages/ExamPage.tsx` | page | Props: `{ sessionId: string }`. `useGetExamSession(sessionId, { query: { staleTime: Infinity } })`.<br>• Error → `ContentErrorState` (`exam.errorTitle`).<br>• Pending → skeleton (`exam.loading`).<br>• `submittedAt` → `<Navigate to="/student/exam-result/$sessionId" replace />`.<br>• Else `<ExamRunner session={data} receivedAt={dataUpdatedAt} />`. |
| 64 | `features/exam/pages/ExamResultPage.tsx` | page | Props: `{ sessionId: string }`. The same query.<br>• Error → `result.errorTitle`.<br>• Pending → skeleton `result.loading`.<br>• Open (`submittedAt` null) → Navigate to `/student/exam/$sessionId`.<br>• Else: `h1` `result.title` `{unit}`, `ExamResultSummary`, `ExamLessonBreakdown`, `ExamWeakestObjectives`, `h2` `result.review` with an `ExamReviewItem` per sorted item, and actions: primary `Button asChild` Link `result.retake` to `/student/exam-start/$unitId` (only when `unitIdOf` is not null) and secondary Link `result.progress` to `/student/progress`. |
| 65 | `routes/student/exam-start.$unitId.tsx` | route | `createFileRoute('/student/exam-start/$unitId')({ component })`, rendering `<ExamStartPage unitId={unitId} />`. No logic. |
| 66 | `routes/student/exam.$sessionId.tsx` | route | `'/student/exam/$sessionId'` → `<ExamPage sessionId={sessionId} />` |
| 67 | `routes/student/exam-result.$sessionId.tsx` | route | `'/student/exam-result/$sessionId'` → `<ExamResultPage sessionId={sessionId} />` |
| 68 | `test/examFixtures.ts` | fixtures | `examSessionId`, `examUnitId`, `examLessonId`.<br>`examItem(position, overrides?) : ExamItemResult` (an Mcq like `quizItem`, `savedAnswer: null`, `answerSavedAt: null`).<br>`openExam(items, overrides?) : ExamSessionResult` with:<br>• `units: [{ unitId: examUnitId, name: 'Mechanics' }]`, `subjectName: 'Physics'`<br>• `startedAt: '2026-09-28T10:00:00Z'`, `timeLimitMinutes: 30`, `deadline: '2026-09-28T10:30:00Z'`, `serverNow: '2026-09-28T10:00:01Z'`<br>• `passMark: 50`, `submittedAt: null`, `scorePercent: null`, `isPassed: null`, `elapsedMilliseconds: 0`, `lessons: []`, `weakestObjectives: []`<br>`submittedExam(items, overrides?)` and `overview(overrides?) : UnitExamOverviewResult`. |

### Tests and docs files to create
| # | Path |
|---|------|
| 69 | `api/Elmanhg.Tests/Builders/ExamSessionBuilder.cs`: `public sealed class ExamSessionBuilder`. `public static readonly DateTimeOffset Now = new(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);` The constructor creates a `QuestionBuilder` and publishes its lesson. Members:<br>• `QuestionBuilder Questions`, `Guid StudentId`<br>• `List<Question> BuildQuestions(int count)` (approved)<br>• `ExamBlueprint Blueprint(int mcqCount, int? timeLimitMinutes = 30, int passMark = 50)` (`CreateForUnit(Questions.Unit, …, ExamBlueprintBuilder.Plenty(), …)`)<br>• `Session Build(int count = 2, int? timeLimitMinutes = 30, bool isTestMode = false)` → `Session.StartUnitExam(StudentId, Questions.Unit, Blueprint(count, timeLimitMinutes), BuildQuestions(count), [Questions.Lesson], isTestMode, Now)` |
| 70 | `api/Elmanhg.Tests/Integration/Exams/ExamTestData.cs`: `public const string Route = "/api/exams";`<br>• `SeedExamUnitAsync(factory, int mcqCount, int? timeLimitMinutes = 30, int passMark = 50)` → `(Guid SubjectId, Guid UnitId, Guid LessonId, List<Guid> QuestionIds)`: seeds a subject, unit and published lesson with approved Mcq questions (reuse `ContentTestData`/`QuestionTestData`), plus a unit blueprint (`ExamBlueprint.CreateForUnit` saved through a fresh `AppDbContext` scope).<br>• `StartAsync(client, unitId)` → `JsonElement` (asserts 200).<br>• `SaveAsync(client, sessionId, questionId, string optionId)` → `HttpResponseMessage`.<br>• `SubmitAsync(client, sessionId)`.<br>• `ExpireAsync(factory, sessionId, TimeSpan ago)`: `context.Database.ExecuteSqlAsync($"UPDATE \"Sessions\" SET \"Deadline\" = {DateTimeOffset.UtcNow - ago} WHERE \"Id\" = {sessionId}")`. |
| 71–? | Test classes listed in the Test plan (one file per class, at the paths given there). |
| — | `docs/exams.md` (new; see Docs). |

### Exam i18n keys (`features/exam/i18n`; en / ar)
| Key | en | ar |
|---|---|---|
| `start.title` | Exam: {unit} | امتحان: {unit} |
| `start.loading` / `start.errorTitle` | Loading the exam… / Could not load the exam. | جارٍ تحميل الامتحان… / تعذّر تحميل الامتحان. |
| `start.defaultBlueprint` | Subject default blueprint | النموذج الافتراضي للمادة |
| `start.caption` / `start.type` / `start.required` / `start.available` | Exam questions by type / Type / Count / Available | أسئلة الامتحان حسب النوع / النوع / العدد / المتاح |
| `start.timeLimit` / `start.noTimeLimit` | Time: {minutes, number} min / Time: open | الزمن: {minutes, number} دقيقة / الزمن: مفتوح |
| `start.passMark` | Pass mark: {passMark, number} | درجة النجاح: {passMark, number} |
| `start.note` | Correct answers are shown only after you submit. Your answers are saved automatically. | لا تظهر الإجابات الصحيحة إلا بعد التسليم. تُحفظ إجاباتك تلقائيًا. |
| `start.shortfall` | The exam cannot be created: not enough questions are available. | لا يمكن إنشاء الامتحان: عدد الأسئلة المتاحة غير كافٍ. |
| `start.noBlueprint` | This unit has no exam yet. | لا يوجد امتحان لهذه الوحدة بعد. |
| `start.start` / `start.continue` | Start exam / Continue exam | ابدأ الامتحان / استكمل الامتحان |
| `start.otherInProgress` / `start.openOther` | You have an exam in progress. / Continue that exam | لديك امتحان جارٍ. / استكمل الامتحان |
| `exam.title` / `exam.loading` / `exam.errorTitle` | Exam: {unit} / Loading the exam… / Could not load the exam. | امتحان: {unit} / جارٍ تحميل الامتحان… / تعذّر تحميل الامتحان. |
| `exam.timeLeft` / `exam.timeUrgent` / `exam.timeUp` | Time left {minutes}:{seconds} / Less than two minutes left. / Time is up. Submitting your exam… | الوقت المتبقي {minutes}:{seconds} / تبقّى أقل من دقيقتين. / انتهى الوقت. جارٍ تسليم امتحانك… |
| `exam.autoSaved` / `exam.saving` / `exam.savedAt` / `exam.saveFailed` | Saved automatically / Saving… / Saved {time} / Not saved. Change your answer to try again. | محفوظ تلقائيًا / جارٍ الحفظ… / تم الحفظ {time} / لم يتم الحفظ. عدّل إجابتك للمحاولة مجددًا. |
| `exam.counter` / `exam.marks` | Question {position, number} of {total, number} / {count, plural, one {# mark} other {# marks}} | سؤال {position, number} من {total, number} / {count, plural, zero {# درجة} one {درجة واحدة} two {درجتان} few {# درجات} many {# درجة} other {# درجة}} |
| `exam.submit` / `exam.confirmTitle` / `exam.confirmUnanswered` / `exam.confirmSubmit` / `exam.cancel` | Submit exam / Submit the exam? / {count, plural, one {# question has no answer.} other {# questions have no answer.}} / Submit / Cancel | تسليم الامتحان / تسليم الامتحان؟ / {count, plural, one {سؤال واحد بدون إجابة.} two {سؤالان بدون إجابة.} few {# أسئلة بدون إجابة.} other {# سؤال بدون إجابة.}} / تسليم / إلغاء |
| `result.title` / `result.loading` / `result.errorTitle` | Result: {unit} / Loading the result… / Could not load the result. | نتيجة: {unit} / جارٍ تحميل النتيجة… / تعذّر تحميل النتيجة. |
| `result.score` / `result.passed` / `result.failed` | {score, number} / 100 / Passed / Below the pass mark ({passMark, number}) | {score, number} / 100 / ناجح / لم تبلغ درجة النجاح ({passMark, number}) |
| `result.answered` / `result.time` | Answered {answered, number} of {total, number} / Time: {minutes, number} min {seconds, number} s | أجبت عن {answered, number} من {total, number} / الوقت: {minutes, number} دقيقة {seconds, number} ثانية |
| `result.byLesson` / `result.lesson` / `result.percentHeader` / `result.percent` / `result.train` / `result.unknownLesson` | By lesson / Lesson / Score / {percent, number}% / Train now / Unavailable | حسب الدرس / الدرس / النسبة / {percent, number}٪ / درّب الآن / غير متاح |
| `result.weakest` / `result.noWeak` | Weakest objectives / No weak objectives. Well done! | أضعف الأهداف / لا توجد أهداف ضعيفة. أحسنت! |
| `result.review` / `result.reviewItem` / `result.unanswered` / `result.correctAnswer` | Question review / Question {position, number} / You did not answer this question. / The correct answer | مراجعة الأسئلة / سؤال {position, number} / لم تُجب عن هذا السؤال. / الإجابة الصحيحة |
| `result.retake` / `result.progress` | Retake exam / My progress | إعادة الامتحان / تقدّمي |

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `Domain.ErrorCodes.ExamTimeExpired` | `EXAM_TIME_EXPIRED` | `Session.SaveExamAnswer` | `BusinessRuleViolationCoreException` | 400 |
| `Domain.ErrorCodes.ExamShortfall` | `EXAM_SHORTFALL` (context `types`) | `ExamBlueprint.EnsureServable` | `BusinessRuleViolationCoreException` | 400 |
| `Application.ErrorCodes.ExamAlreadyInProgress` | `EXAM_ALREADY_IN_PROGRESS` | `StartUnitExamHandler`; `AppDbContext.SaveChangesAsync` (`IX_Sessions_OneOpenExam`) | `ConflictCoreException` | 409 |
| `Application.ErrorCodes.UnitExamNoBlueprint` | `UNIT_EXAM_NO_BLUEPRINT` | `StartUnitExamHandler` | `BadRequestCoreException` | 400 |
| Reused | `UNIT_ID_REQUIRED` 422, `UNIT_NOT_FOUND` 404, `SESSION_ID_REQUIRED` 422, `SESSION_NOT_FOUND` 404, `SESSION_QUESTION_NOT_FOUND` 404, `QUESTION_ID_REQUIRED` 422, `QUESTION_NOT_FOUND` 404, `QUESTION_ANSWER_INVALID` 422, `ATTEMPT_ANSWER_TOO_LONG` 422, `SESSION_ALREADY_SUBMITTED` 400, `SESSION_NO_SERVABLE_QUESTIONS` 400, `SESSION_QUESTION_NOT_SERVABLE` 400, `SESSION_QUESTION_DUPLICATE` 400, `SESSION_ALREADY_IN_PROGRESS` 409, `SESSION_MODIFIED_CONCURRENTLY` 409, `USER_NOT_AUTHENTICATED` 401 | | | |

Resx (`Messages.en.resx` / `Messages.ar.resx`, Arabic without tashkeel):
- `EXAM_TIME_EXPIRED`: "The exam time is over. Your saved answers will be submitted." / «انتهى وقت الامتحان. سيتم تسليم إجاباتك المحفوظة.»
- `EXAM_SHORTFALL`: "The exam cannot be created: not enough questions are available: {types}" / «لا يمكن انشاء الامتحان: عدد الاسئلة المتاحة غير كاف: {types}»
- `EXAM_ALREADY_IN_PROGRESS`: "You already have an exam in progress. Finish it first." / «لديك امتحان جار بالفعل. اكمله اولا.»
- `UNIT_EXAM_NO_BLUEPRINT`: "This unit has no exam yet." / «لا يوجد امتحان لهذه الوحدة بعد.»

Web `common:errors` (en / ar): the same texts, with `EXAM_SHORTFALL` ending at "available." / «غير كافٍ.» (no `{types}`).

## Domain behaviour
**`Session.StartUnitExam(studentId, unit, blueprint, questions, lessons, isTestMode, now)`**
1. `if (blueprint.UnitId != unit.Id && !(blueprint.IsSubjectDefault && blueprint.SubjectId == unit.SubjectId)) { throw new InvalidOperationException("The blueprint does not apply to this unit."); }`
2. `questions.Count == 0` → `BusinessRuleViolationCoreException(ErrorCodes.SessionNoServableQuestions)`.
3. Any question whose lesson (`lessons.FirstOrDefault(l => l.Id == q.LessonId)`) is null, has `UnitId != unit.Id`, or fails `ServableQuestionSpecification.IsSatisfiedBy(q, lesson)` → `SessionQuestionNotServable`.
4. Duplicate ids → `SessionQuestionDuplicate`.
5. `scope = new UnitExamScope(unit.Id)`, `started = ToMicroseconds(now)`.
6. `new Session(Guid.NewGuid(), studentId)` with: `StudentId`, `Kind = SessionKind.UnitExam`, `Scope = scope.ToJson()`, `ScopeKey = scope.ToKey()`, `IsTestMode`, `StartedAt = LastActivityAt = started`, `TimeLimitMinutes = blueprint.TimeLimitMinutes`, `PassMark = blueprint.PassMark`, `Deadline = blueprint.TimeLimitMinutes is null ? null : started.AddMinutes(blueprint.TimeLimitMinutes.Value)`.
7. Items via `SessionItem.Create(session.Id, index + 1, question)` in the given order.

**`Session.SaveExamAnswer(item, answer, grace, now)`**. Guard, then mutate, then stamp:
1. `!Items.Contains(item)` → `InvalidOperationException("Session item does not belong to this session.")`.
2. `!IsExam` → `InvalidOperationException("Quiz answers are recorded with RecordAttempt.")`.
3. `EnsureNotSubmitted()` → `SESSION_ALREADY_SUBMITTED`.
4. `at = ToMicroseconds(now)`; `IsPastDeadline(at, grace)` → `BusinessRuleViolationCoreException(ErrorCodes.ExamTimeExpired)`.
5. `item.SaveAnswer(answer, at)`; `Touch(at)` (sets `LastActivityAt`, `UpdatedBy`, `UpdationDate`).

**`Session.SubmitExam(grades, now)`**
1. `!IsExam` → `InvalidOperationException("Quizzes are finished with Submit.")`.
2. `IsSubmitted` → `return [];` (idempotent; the score is unchanged).
3. `at = ToMicroseconds(now)`.
4. `foreach` item by `Position` with `SavedAnswer is { } answer`: take the grade with `grades.TryGetValue(item.QuestionId, …)`; a missing grade throws `InvalidOperationException("A saved exam answer has no grade.")`. `Attempt.Create(this, item, answer, grade, ExamAttemptTimeTakenMilliseconds, at)`, added to `Attempts` and to the returned list.
5. `ScorePercent = CalculateScorePercent()`, `SubmittedAt = at`, `Touch(at)`.
6. Return the created attempts.

**`ExamBlueprint.EnsureServable(servable)`**: `shortfalls = ExamBlueprintShortfall.Find(GetTypeCounts(), servable)`. If any → `BusinessRuleViolationCoreException(ErrorCodes.ExamShortfall, context: new Dictionary<string, object> { ["types"] = Describe(shortfalls) })`. No mutation, no `UpdationDate`.

**Guards on existing methods**: `RecordAttempt` and `Submit()` throw `InvalidOperationException` when `IsExam` (Existing code touched).

## API surface
| Method | Route | Name (Orval op) | Policy | Request | Response |
|---|---|---|---|---|---|
| GET | `/api/exams/units/{unitId:guid}` | `GetUnitExamOverview` | `DefaultCodes.AssessmentsTake` | — | 200 `UnitExamOverviewResult` |
| POST | `/api/exams/units/{unitId:guid}` | `StartUnitExam` | `AssessmentsTake` | — (`new StartUnitExamCommand(unitId)`) | 200 `ExamSessionResult` (new, resumed, or auto-submitted) |
| GET | `/api/exams/{sessionId:guid}` | `GetExamSession` | `AssessmentsTake` | — | 200 `ExamSessionResult` |
| PUT | `/api/exams/{sessionId:guid}/answers/{questionId:guid}` | `SaveExamAnswer` | `AssessmentsTake` | `SaveExamAnswerRequest { answer }` | 200 `ExamAnswerSavedResult` |
| POST | `/api/exams/{sessionId:guid}/submit` | `SubmitExam` | `AssessmentsTake` | — | 200 `ExamSessionResult` |

Another student's session and quiz ids both return 404 `SESSION_NOT_FOUND`. Teachers get 403 and anonymous callers 401.

## Docs
| Doc | Change |
|---|---|
| `docs/exams.md` (new) | The contract:<br>• Resolution and start: resume, one open exam, expired-open submit, shortfall and no-blueprint.<br>• The selection algorithm (Decision 13) with an example.<br>• Sitting: saved answers, what is revealed, deadline + grace, auto-save.<br>• Submission: grading, score, mastery, attempt time 0, elapsed time.<br>• Auto-submit: 3 paths, the worker, and the options.<br>• Breakdown and weakest objectives (Decisions 15–16).<br>• API table, the result shapes, the error-code table, and the `Exams:*` options table.<br>• Web screens: the 3 routes and their states. |
| `docs/sessions.md` | Session model rows:<br>• Replace "— `time_limit_min?` Added by E6" with `TimeLimitMinutes`, `PassMark` and `Deadline` rows.<br>• `Kind`: "`Quiz` via `/api/sessions`, `UnitExam` via `/api/exams` (`docs/exams.md`)".<br>SessionItem: add `SavedAnswer` and `AnswerSavedAt`, and change "Items … never change afterwards" to "The served questions never change; an exam item's saved answer does until submission".<br>Indexes: add `IX_Sessions_OneOpenExam` and `IX_Sessions_OpenExamDeadline`.<br>Lifecycle: note that answer and finish return 404 for exam sessions. |
| `docs/exam-blueprints.md` | Intro: "Exam generation (#81) … read them" stays. "Resolution for exams": drop "(not coded in #80)", name `ExamBlueprintResolution.ForUnit` and `ExamBlueprint.EnsureServable` → `EXAM_SHORTFALL`, and link `docs/exams.md`. |
| `docs/PRD.md` §7.4 | Add bullets:<br>• "A student has at most one exam in progress."<br>• "Answers are auto-saved as drafts; with a time limit, saves close at the deadline (plus a short grace) and the exam is submitted automatically."<br>• "Unanswered questions score 0."<br>• "Submitted exam answers are attempts and count toward mastery (§7.3)." |
| `docs/PRD.md` §15 | `Session(… time_limit_min?, pass_mark?, deadline?)`, `SessionItem(… max_score, saved_answer_json?, answer_saved_at?)` with the comment `-- served questions fixed at start; exam drafts until submission`. |
| `docs/mastery.md` | "Exam attempts (non-test) update mastery when the exam is submitted; test-mode exams never do." The streak line stays quiz-only (state it explicitly). Seen includes exam attempts. |
| `docs/progress.md` | Links: "A finished exam links «عرض» to `/student/exam-result/{id}`; an open exam links «متابعة» to `/student/exam/{id}`." Subjects: the unit table has a «امتحان الوحدة» link to `/student/exam-start/{unitId}`. Drop "Exams have no link until … (#81)" and "Unit exams are created in E6 (#81), which must start exams with `UnitExamScope`" becomes "Unit exams (#81) use `UnitExamScope`". |
| `docs/audit-log.md` | "Quiz and exam activity" list adds `StartUnitExam`, `SaveExamAnswer`, `SubmitExam`, `AutoSubmitExam`. |

`docs/claude-design-prompt.md` and `docs/prototype.md` stay unchanged: the best score and attempts list on the exam screens are #83 (incompleteness, not divergence).

## Test plan
FluentAssertions (pinned), NSubstitute, xUnit v3, `TestContext.Current.CancellationToken`. Handlers take a `TimeProvider` substitute returning a fixed `GetUtcNow()` (as `GetMasteryOverviewHandlerTests` does). Session lookups use `SessionRepositoryStub.StubFind` (compiled predicate). Success tests assert the result plus `SaveChangesAsync` `Received(1)`. Throwing tests assert the type, the code, and `SaveChangesAsync` `DidNotReceive()`.

### Domain
| # | Test class (path under `api/Elmanhg.Tests/Domain/`) | Test method | Asserts |
|---|---|---|---|
| 1 | `Sessions/SessionExamStartTests` | `StartUnitExam_ServableQuestions_CreatesUnitExamWithBlueprintSettings` | Kind `UnitExam`; Scope JSON has `unitId`; ScopeKey `unit:<id>`; TimeLimitMinutes 30; PassMark 50; Deadline = StartedAt + 30 min; items positions 1..n in the given order; `IsExam` |
| 2 | 〃 | `StartUnitExam_UntimedBlueprint_HasNoDeadline` | `Deadline` null, `TimeLimitMinutes` null |
| 3 | 〃 | `StartUnitExam_SubjectDefaultBlueprint_IsAccepted` | A session is created with the default's pass mark |
| 4 | 〃 | `StartUnitExam_BlueprintOfOtherUnit_ThrowsInvalidOperation` | `InvalidOperationException` |
| 5 | 〃 | `StartUnitExam_NoQuestions_ThrowsNoServableQuestions` | code `SESSION_NO_SERVABLE_QUESTIONS` |
| 6 | 〃 | `StartUnitExam_QuestionInUnpublishedLesson_ThrowsNotServable` | `SESSION_QUESTION_NOT_SERVABLE` |
| 7 | 〃 | `StartUnitExam_LessonOfOtherUnit_ThrowsNotServable` | `SESSION_QUESTION_NOT_SERVABLE` |
| 8 | 〃 | `StartUnitExam_DuplicateQuestion_ThrowsDuplicate` | `SESSION_QUESTION_DUPLICATE` |
| 9 | 〃 | `StartUnitExam_Now_TruncatesToMicroseconds` | `StartedAt.Ticks % TicksPerMicrosecond == 0` for a `now` with sub-microsecond ticks |
| 10 | 〃 | `IsPastDeadline_RelativeToDeadlineAndGrace_ReturnsExpected` (Theory: −1 s → false; +10 s with 30 s grace → false; +31 s → true) | bool |
| 11 | 〃 | `IsPastDeadline_Untimed_ReturnsFalse` | false far in the future |
| 12 | `Sessions/SessionExamAnswerTests` | `SaveExamAnswer_OpenExam_StoresAnswerAndTouches` | `SavedAnswer`, `AnswerSavedAt`, `LastActivityAt` and `UpdationDate` = at; `Attempts` empty |
| 13 | 〃 | `SaveExamAnswer_SecondAnswer_Overwrites` | the latest answer and time are kept |
| 14 | 〃 | `SaveExamAnswer_WithinGrace_Saves` | saved at Deadline + 10 s (grace 30 s) |
| 15 | 〃 | `SaveExamAnswer_PastDeadline_ThrowsExamTimeExpired` | code `EXAM_TIME_EXPIRED`; `SavedAnswer` still null |
| 16 | 〃 | `SaveExamAnswer_Submitted_ThrowsAlreadySubmitted` | `SESSION_ALREADY_SUBMITTED` |
| 17 | 〃 | `SaveExamAnswer_QuizSession_ThrowsInvalidOperation` | `InvalidOperationException` |
| 18 | 〃 | `SaveExamAnswer_ForeignItem_ThrowsInvalidOperation` | `InvalidOperationException` |
| 19 | 〃 | `RecordAttempt_ExamSession_ThrowsInvalidOperation` | `InvalidOperationException` |
| 20 | `Sessions/SessionExamSubmissionTests` | `SubmitExam_SavedAnswers_CreatesAttemptsOnlyForAnsweredItems` | 1 of 2 items saved → 1 attempt: answer, served version, `TimeTakenMilliseconds` 0, `CreatedAt` = at |
| 21 | 〃 | `SubmitExam_ComputesScorePercentOverAllItems` | 1 correct of 2 → 50.00 |
| 22 | 〃 | `SubmitExam_SetsSubmittedAtAndTouches` | `SubmittedAt`, `LastActivityAt` = at |
| 23 | 〃 | `SubmitExam_AlreadySubmitted_ReturnsEmptyAndKeepsScore` | second call returns empty; score and attempt count unchanged |
| 24 | 〃 | `SubmitExam_MissingGrade_ThrowsInvalidOperation` | `InvalidOperationException` |
| 25 | 〃 | `SubmitExam_QuizSession_ThrowsInvalidOperation` | `InvalidOperationException` |
| 26 | 〃 | `Submit_ExamSession_ThrowsInvalidOperation` | `InvalidOperationException` |
| 27 | `Sessions/Exams/ExamQuestionSelectorTests` | `Select_TypeCounts_PicksExactCountPerType` | 2 Mcq + 1 Fill from a larger pool |
| 28 | 〃 | `Select_NotMasteredAvailable_PrefersNotMastered` | every pick is not mastered while enough exist |
| 29 | 〃 | `Select_NotEnoughNotMastered_FillsWithMastered` | all not-mastered picked, the rest mastered |
| 30 | 〃 | `Select_DifficultyMix_MeetsTargetsWhenPoolAllows` | 10 with 30/50/20 → 3/5/2 |
| 31 | 〃 | `Select_DifficultyShort_FillsFromOtherDifficulties` | no Hard in the pool → count still met |
| 32 | 〃 | `Select_OrdersByTypeThenDifficulty` | Mcq before Fill; Easy before Hard within a type |
| 33 | 〃 | `Select_SameSeed_ReturnsSameOrder` | two `new Random(7)` runs are equal; a candidate order permutation gives the same result |
| 34 | 〃 | `Select_PoolSmallerThanCount_ReturnsAvailable` | returns all of the type, no throw |
| 35 | 〃 | `Select_DuplicateCandidates_NeverRepeats` | distinct ids |
| 36 | `Sessions/Exams/ExamDifficultyTargetsTests` | `Apportion_Mix_ReturnsLargestRemainderTargets` (Theory: 10,30/50/20→3/5/2; 3,30/50/20→1/1/1; 1,0/100/0→0/1/0; 7,34/33/33→3/2/2) | the dictionary |
| 37 | 〃 | `Apportion_AnyCount_SumsToCount` (Theory 1, 5, 17, 100) | sum = count |
| 38 | `Sessions/Exams/ExamBreakdownTests` | `ByLesson_SubmittedExam_SumsScoresPerLesson` | per-lesson Score, MaxScore, ScorePercent |
| 39 | 〃 | `ByLesson_UnansweredItems_CountZero` | unanswered adds MaxScore only |
| 40 | 〃 | `ByLesson_CorrectCount_UsesThreshold` | a 0.5 attempt is not correct at 0.8 |
| 41 | 〃 | `ByLesson_OrdersByLessonOrder` | order by `LessonOrder` |
| 42 | 〃 | `ByLesson_OpenExam_ReturnsEmpty` | `[]` |
| 43 | 〃 | `ByLesson_ItemWithoutPlacement_IsSkipped` | the item is absent from all shares |
| 44 | 〃 | `WeakestObjectives_ExcludesFullScoreAndSortsAscending` | a 100% objective is excluded; ascending order |
| 45 | 〃 | `WeakestObjectives_TiesBrokenByLessonThenObjectiveOrder` | order |
| 46 | 〃 | `WeakestObjectives_TakesCount` | 4 weak, count 3 → 3 |
| 47 | 〃 | `WeakestObjectives_IgnoresItemsWithoutObjective` | no share for null objective |
| 48 | `ExamBlueprints/ExamBlueprintResolutionTests` | `ForUnit_UnitBlueprintExists_ReturnsIt` | the unit's blueprint, even when a default exists |
| 49 | 〃 | `ForUnit_NoUnitBlueprint_ReturnsSubjectDefault` | the default |
| 50 | 〃 | `ForUnit_NoBlueprints_ReturnsNull` | null |
| 51 | 〃 | `ForUnit_OtherUnitsBlueprint_IsIgnored` | null (or the default when present) |
| 52 | `ExamBlueprints/ExamBlueprintServabilityTests` | `EnsureServable_EnoughQuestions_DoesNotThrow` | no exception |
| 53 | 〃 | `EnsureServable_Short_ThrowsExamShortfallWithTypes` | code `EXAM_SHORTFALL`; `Context["types"]` = `"Mcq 1/2"` |

### Application (`api/Elmanhg.Tests/Application/Features/`)
| # | Test class | Test method | Asserts |
|---|---|---|---|
| A1 | `Exams/GetUnitExamOverview/GetUnitExamOverviewHandlerTests` | `Handle_NoUser_ThrowsUnauthorized` | 401 code |
| A2 | 〃 | `Handle_UnknownUnit_ThrowsNotFound` | `UNIT_NOT_FOUND` |
| A3 | 〃 | `Handle_UnitBlueprint_ReturnsItWithAvailability` | `IsSubjectDefault` false; type rows required/available; `IsAvailable` true |
| A4 | 〃 | `Handle_OnlyDefault_ReturnsDefault` | `IsSubjectDefault` true |
| A5 | 〃 | `Handle_NoBlueprint_ReturnsUnavailable` | `Blueprint` null, `IsAvailable` false |
| A6 | 〃 | `Handle_ShortPool_ReturnsUnavailable` | `IsAvailable` false; available < required |
| A7 | 〃 | `Handle_OpenExamOtherUnit_ReturnsInProgressElsewhere` | `InProgressExam.IsThisUnit` false |
| A8 | 〃 | `Handle_OpenExamThisUnit_ReturnsInProgressHere` | `IsThisUnit` true |
| A9 | `Exams/GetUnitExamOverview/GetUnitExamOverviewValidatorTests` | `Validate_ValidId_Passes` / `Validate_EmptyUnitId_FailsWithUnitIdRequired` | codes |
| A10 | `Exams/StartUnitExam/StartUnitExamHandlerTests` | `Handle_NoUser_ThrowsUnauthorized` | 401; no save |
| A11 | 〃 | `Handle_UnknownUnit_ThrowsNotFound` | `UNIT_NOT_FOUND`; no save |
| A12 | 〃 | `Handle_OpenExamOtherUnit_ThrowsExamAlreadyInProgress` | `ConflictCoreException` `EXAM_ALREADY_IN_PROGRESS`; no save |
| A13 | 〃 | `Handle_OpenExamThisUnit_ResumesWithoutNewSession` | same id returned; `AddAsync` DidNotReceive; save once |
| A14 | 〃 | `Handle_ExpiredOpenExamThisUnit_SubmitsAndReturnsIt` | `SubmittedAt` set; attempts for saved answers; save once |
| A15 | 〃 | `Handle_NoBlueprint_ThrowsBadRequest` | `UNIT_EXAM_NO_BLUEPRINT`; no save |
| A16 | 〃 | `Handle_Shortfall_ThrowsExamShortfall` | `BusinessRuleViolationCoreException` `EXAM_SHORTFALL`; no save |
| A17 | 〃 | `Handle_ServableUnit_StartsExamFromBlueprint` | `AddAsync` received a session with n items, deadline, pass mark; result items carry no `CorrectAnswer`; save once |
| A18 | 〃 | `Handle_MasteredQuestions_PrefersNotMastered` | pool 3, 1 mastered, blueprint 2 → mastered id not served |
| A19 | 〃 | `Handle_Admin_StartsTestModeExam` | `IsTestMode` true |
| A20 | `Exams/StartUnitExam/StartUnitExamValidatorTests` | `Validate_ValidId_Passes` / `Validate_EmptyUnitId_FailsWithUnitIdRequired` | codes |
| A21 | `Exams/GetExamSession/GetExamSessionHandlerTests` | `Handle_NoUser_ThrowsUnauthorized` | 401 |
| A22 | 〃 | `Handle_UnknownSession_ThrowsNotFound` | `SESSION_NOT_FOUND` |
| A23 | 〃 | `Handle_QuizSession_ThrowsNotFound` | quiz stubbed → `SESSION_NOT_FOUND` |
| A24 | 〃 | `Handle_OpenExam_ReturnsSavedAnswersWithoutKeys` | `SavedAnswer` set; `Attempt`/`CorrectAnswer`/`Explanation` null; `ServerNow` = the clock; lessons empty |
| A25 | 〃 | `Handle_SubmittedExam_ReturnsBreakdown` | `IsPassed`; one lesson row; `CorrectAnswer` revealed |
| A26 | `Exams/GetExamSession/GetExamSessionValidatorTests` | `Validate_ValidId_Passes` / `Validate_EmptySessionId_FailsWithSessionIdRequired` | codes |
| A27 | `Exams/SaveExamAnswer/SaveExamAnswerHandlerTests` | `Handle_NoUser_ThrowsUnauthorized` | 401; no save |
| A28 | 〃 | `Handle_UnknownSession_ThrowsNotFound` | `SESSION_NOT_FOUND`; no save |
| A29 | 〃 | `Handle_QuestionNotInExam_ThrowsNotFound` | `SESSION_QUESTION_NOT_FOUND`; no save |
| A30 | 〃 | `Handle_RevisionMissing_ThrowsNotFound` | `QUESTION_NOT_FOUND`; no save |
| A31 | 〃 | `Handle_WrongShape_ThrowsValidation` | `QUESTION_ANSWER_INVALID`; no save |
| A32 | 〃 | `Handle_ValidAnswer_SavesCanonicalAnswer` | `SavedAnswer` = `SessionBuilder.AnswerB` for input with an extra property; result `AnswerSavedAt`; save once |
| A33 | 〃 | `Handle_PastDeadline_ThrowsExamTimeExpired` | `EXAM_TIME_EXPIRED`; no save |
| A34 | 〃 | `Handle_Submitted_ThrowsAlreadySubmitted` | `SESSION_ALREADY_SUBMITTED`; no save |
| A35 | `Exams/SaveExamAnswer/SaveExamAnswerValidatorTests` | `Validate_ValidCommand_Passes`, `Validate_EmptySessionId_FailsWithSessionIdRequired`, `Validate_EmptyQuestionId_FailsWithQuestionIdRequired`, `Validate_AnswerNotObject_FailsWithQuestionAnswerInvalid`, `Validate_AnswerTooLong_FailsWithAttemptAnswerTooLong` | codes |
| A36 | `Exams/SubmitExam/SubmitExamHandlerTests` | `Handle_NoUser_ThrowsUnauthorized` | 401; no save |
| A37 | 〃 | `Handle_UnknownSession_ThrowsNotFound` | `SESSION_NOT_FOUND`; no save |
| A38 | 〃 | `Handle_SavedAnswers_GradesAndScores` | score, attempts, `IsPassed`; save once |
| A39 | 〃 | `Handle_NewQuestion_StartsMastery` | `questionMasteryRepository.AddRangeAsync` received a list with a `QuestionMastery` for the question |
| A40 | 〃 | `Handle_ExistingMastery_RecordsAttempt` | existing row's `LatestAttemptId` = the new attempt id |
| A41 | 〃 | `Handle_TestMode_DoesNotTouchMastery` | `FindAsync` and `AddRangeAsync` DidNotReceive on the mastery repo |
| A42 | 〃 | `Handle_AlreadySubmitted_ReturnsSameResultWithoutNewAttempts` | attempt count unchanged |
| A43 | `Exams/SubmitExam/SubmitExamValidatorTests` | `Validate_ValidId_Passes` / `Validate_EmptySessionId_FailsWithSessionIdRequired` | codes |
| A44 | `Exams/GetExpiredExamSessionIds/GetExpiredExamSessionIdsHandlerTests` | `Handle_Sessions_ReturnsOnlyExpiredOpenExamsPastGrace` | stub `FindPaginatedAsync` to apply the compiled filter to: expired exam (in), within-grace exam, submitted exam, untimed exam, quiz (out); page size = `AutoSubmitBatchSize` |
| A45 | `Exams/AutoSubmitExam/AutoSubmitExamHandlerTests` | `Handle_ExpiredExam_SubmitsAndSaves` | `SubmittedAt` set; save once |
| A46 | 〃 | `Handle_WithinGrace_LeavesOpen` | not submitted; save DidNotReceive |
| A47 | 〃 | `Handle_UnknownSession_DoesNothing` | save DidNotReceive |
| A48 | `Exams/AutoSubmitExam/AutoSubmitExamValidatorTests` | `Validate_ValidId_Passes` / `Validate_EmptySessionId_FailsWithSessionIdRequired` | codes |
| A49 | `Exams/ExamsOptionsTests` | `AddApplication_DefaultExamsOptions_ResolvesWithoutThrowing` | defaults 30/true/60/50/3 |
| A50 | 〃 | `AddApplication_GraceOutOfRange_ThrowsOptionsValidationException` | `Exams:DeadlineGraceSeconds=601` throws |
| D1 | `Sessions/SubmitAnswer/SubmitAnswerHandlerTests` (modify: add) | `Handle_ExamSession_ThrowsNotFound` | stub an exam session with the same id → `SESSION_NOT_FOUND`; no save |
| D2 | `Sessions/FinishSession/FinishSessionHandlerTests` (modify: add) | `Handle_ExamSession_ThrowsNotFound` | same |

### Integration (`api/Elmanhg.Tests/Integration/`)
| # | Test class | Test method | Asserts |
|---|---|---|---|
| I1 | `Exams/UnitExamOverviewEndpointTests` | `Get_UnitWithBlueprint_ReturnsSummaryAndAvailability` | 200; `isAvailable` true; `typeCounts[0]` `{type:"Mcq",required,available}` |
| I2 | 〃 | `Get_ShortPool_ReturnsUnavailable` | `isAvailable` false |
| I3 | 〃 | `Get_UnknownUnit_Returns404` | 404 `UNIT_NOT_FOUND` |
| I4 | 〃 | `Get_Teacher_Returns403` / `Get_Anonymous_Returns401` | status |
| I5 | `Exams/StartUnitExamEndpointTests` | `Post_ServableUnit_StartsExamWithBlueprintShape` | 200; `kind` `UnitExam`; items count; `deadline` − `startedAt` = limit; no `correctAnswer`; DB row with `PassMark` and `Deadline` |
| I6 | 〃 | `Post_OpenExamSameUnit_ResumesSameSession` | same id; 1 session row |
| I7 | 〃 | `Post_OpenExamOtherUnit_Returns409` | 409 `EXAM_ALREADY_IN_PROGRESS` |
| I8 | 〃 | `Post_NoBlueprint_Returns400` | 400 `UNIT_EXAM_NO_BLUEPRINT` |
| I9 | 〃 | `Post_Shortfall_Returns400` | 400 `EXAM_SHORTFALL` (retire a question after seeding) |
| I10 | 〃 | `Post_ExpiredOpenExam_SubmitsAndReturnsIt` | `submittedAt` not null; DB attempts for saved answers |
| I11 | 〃 | `Post_Admin_StartsTestModeExam` | `isTestMode` true |
| I12 | 〃 | `Post_Teacher_Returns403` / `Post_Anonymous_Returns401` | status |
| I13 | `Exams/GetExamSessionEndpointTests` | `Get_OpenExam_ReturnsSavedAnswersWithoutKeys` | after a save: `savedAnswer` echoed; `correctAnswer` null |
| I14 | 〃 | `Get_OtherStudentExam_Returns404` / `Get_QuizSession_Returns404` | 404 `SESSION_NOT_FOUND` |
| I15 | `Exams/SaveExamAnswerEndpointTests` | `Put_ValidAnswer_SavesWithoutAttempt` | 200 `answerSavedAt`; DB `SessionItems.SavedAnswer` set; 0 `Attempts` rows |
| I16 | 〃 | `Put_ChangedAnswer_OverwritesSavedAnswer` | DB holds the second answer |
| I17 | 〃 | `Put_AfterDeadline_Returns400` | `ExpireAsync` 1 h → 400 `EXAM_TIME_EXPIRED` |
| I18 | 〃 | `Put_SubmittedExam_Returns400` | 400 `SESSION_ALREADY_SUBMITTED` |
| I19 | 〃 | `Put_WrongShape_Returns422` | 422 `QUESTION_ANSWER_INVALID` |
| I20 | 〃 | `Put_QuestionNotInExam_Returns404` / `Put_OtherStudent_Returns404` / `Put_Anonymous_Returns401` | status and code |
| I21 | `Exams/SubmitExamEndpointTests` | `Post_Submit_GradesSavedAnswersAndReturnsBreakdown` | `scorePercent`; `isPassed`; `lessons[0]`; `correctAnswer` on every item; DB attempts only for answered items |
| I22 | 〃 | `Post_SubmitTwice_ReturnsSameResult` | same score; attempt count unchanged |
| I23 | 〃 | `Post_Submit_UpdatesMastery` | a `QuestionMasteries` row for the answered question |
| I24 | 〃 | `Post_SubmitAfterDeadline_GradesSavedAnswers` | 200 after `ExpireAsync` |
| I25 | 〃 | `Post_TestModeExam_DoesNotWriteMastery` | no `QuestionMasteries` row |
| I26 | 〃 | `Post_OtherStudent_Returns404` / `Post_Anonymous_Returns401` | status |
| I27 | `Exams/ExpiredExamSubmissionTests` | `AutoSubmit_ExpiredExam_SubmitsAndGrades` | via `ISender` in a fresh scope; DB `SubmittedAt`, attempts |
| I28 | 〃 | `AutoSubmit_WithinGrace_LeavesOpen` | Deadline now − 10 s → still open |
| I29 | 〃 | `GetExpiredExamSessionIds_ReturnsExpiredOpenExam` | the expired id is in the list; a fresh open exam is not |
| I30 | `Persistence/ExamSessionPersistenceTests` | `SavedAnswer_RoundTrips_AsJsonb` | `information_schema` `data_type` = `jsonb`; the value round-trips |
| I31 | 〃 | `SecondOpenExam_ForStudent_ThrowsExamAlreadyInProgress` | two open exams for different units saved directly → `ConflictCoreException` `EXAM_ALREADY_IN_PROGRESS` |
| I32 | 〃 | `GetServableExamCandidatesAsync_ReturnsOnlyServableQuestionsOfTheUnits` | excludes retired, pending, draft-lesson and other-unit questions; carries type, difficulty and lesson |
| I33 | `Persistence/AppDbContextTests` (modify) | `Migrate_FreshDatabase_LeavesNoPendingMigrations` | the new list entry `_AddExamSittings` |

### Web (Vitest + Testing Library + MSW, `renderApp` with `testSessions.student`)
| # | Test file | `it(...)` | Asserts |
|---|---|---|---|
| W1 | `features/exam/api/examSession.test.ts` | sorts items by position | order |
| W2 | 〃 | subtracts the clock offset and never goes below zero | values |
| W3 | 〃 | splits the countdown rounding up to whole seconds | 61 001 ms → 1:02 |
| W4 | 〃 | marks the last two minutes as urgent | 120 000 true, 120 001 false |
| W5 | 〃 | returns the first unit id or null | both |
| W6 | `features/exam/pages/ExamStartPage.test.tsx` | shows the blueprint summary after loading | loading label, then the type row "Multiple choice" 10/12, "Time: 45 min", "Pass mark: 50", note |
| W7 | 〃 | shows an open time when the blueprint has no time limit | "Time: open" |
| W8 | 〃 | marks the subject default blueprint | badge text |
| W9 | 〃 | shows the shortfall and hides Start when questions are short | message; no Start button |
| W10 | 〃 | says the unit has no exam when there is no blueprint | text |
| W11 | 〃 | starts the exam and opens the exam screen | click Start → exam heading "Exam: Mechanics" |
| W12 | 〃 | shows the server error when starting fails | 409 → "You already have an exam in progress. Finish it first." |
| W13 | 〃 | offers Continue when this unit's exam is in progress | link href `/student/exam/<id>` |
| W14 | 〃 | warns and links to the other exam when another exam is in progress | warning text; link; no Start |
| W15 | 〃 | shows retry on a load error and recovers | retry → summary |
| W16 | 〃 | renders right to left in Arabic | `dir="rtl"`, Arabic title |
| W17 | 〃 | has no axe violations | axe |
| W18 | `features/exam/pages/ExamPage.test.tsx` | shows every question with its saved answer restored | 2 cards; saved radio checked |
| W19 | 〃 | redirects a submitted exam to its result | result heading |
| W20 | 〃 | shows retry when the exam fails to load | retry button |
| W21 | 〃 | shows the countdown from the server clock | fake timers → "Time left 29:59" |
| W22 | 〃 | announces the last two minutes | deadline 90 s away → status "Less than two minutes left." |
| W23 | 〃 | hides the countdown for an untimed exam | no `timer` role |
| W24 | 〃 | renders right to left in Arabic | `dir="rtl"` |
| W25 | `features/exam/pages/ExamPage.autosave.test.tsx` | saves a chosen option after the auto-save delay | the PUT body `{answer:{optionId:'b'}}`; "Saved" status |
| W26 | 〃 | saves only the latest answer after quick changes | exactly 1 PUT, with the last option |
| W27 | 〃 | shows a save error when saving fails | 500 → "Not saved. Change your answer to try again." |
| W28 | 〃 | submits the exam when a save reports the time is over | PUT 400 `EXAM_TIME_EXPIRED` → POST submit → result heading |
| W29 | `features/exam/pages/ExamPage.submit.test.tsx` | asks for confirmation with the unanswered count | dialog "Submit the exam?" + "1 question has no answer." |
| W30 | 〃 | keeps the exam open when the confirmation is cancelled | dialog closed; no POST |
| W31 | 〃 | saves pending answers before submitting | recorded order PUT then POST |
| W32 | 〃 | opens the result after submitting | result heading "Result: Mechanics" |
| W33 | 〃 | submits automatically when the time runs out | fake timers past the deadline → "Time is up…" then result |
| W34 | `features/exam/pages/ExamResultPage.test.tsx` | shows the score and a pass badge | "80 / 100", "Passed" |
| W35 | 〃 | shows the fail badge with the pass mark | "Below the pass mark (50)" |
| W36 | 〃 | lists the per-lesson breakdown with a practice link | row; link href `/student/lesson/<id>/practice` |
| W37 | 〃 | lists the weakest objectives | objective text |
| W38 | 〃 | says there are no weak objectives when none are left | "No weak objectives. Well done!" |
| W39 | 〃 | reviews an unanswered question with the correct answer | "You did not answer this question." + correct option |
| W40 | 〃 | links Retake to the unit's exam start | href `/student/exam-start/<unitId>` |
| W41 | 〃 | redirects an open exam to the exam screen | exam heading |
| W42 | 〃 | shows retry on a load error | retry button |
| W43 | 〃 | renders right to left in Arabic | `dir="rtl"`, «ناجح» |
| W44 | 〃 | has no axe violations | axe |
| W-P1 | `features/progress/api/sessionHistory.test.ts` (add) | links a finished exam to its result | `{ to: '/student/exam-result/$sessionId', labelKey: 'history.view' }` |
| W-P2 | 〃 (add) | links an open exam to continue | `{ to: '/student/exam/$sessionId', labelKey: 'history.continue' }` |
| W-P3 | 〃 (delete) | `gives no link for exams` | removed: behaviour intentionally changed |
| W-P4 | `features/progress/pages/ProgressPage.history.test.tsx` (modify) | links finished quizzes to results and open quizzes to continue | exam row now has View → `/student/exam-result/<id>` |
| W-P5 | `features/progress/components/UnitProgressTable.test.tsx` (new) | links each unit to its exam start | link "Unit exam" href `/student/exam-start/<unitId>` |

Mutation check (per PROGRESS.md): break `IsPastDeadline`, the `Kind == Quiz` filter and the not-mastered preference, confirm tests 10/A33, D1 and 28 fail, then restore.

## Definition of done
- [ ] Every file in "Files to create" exists at the given path; no other new files. Every "Existing code touched" change is made.
- [ ] `Session.StartUnitExam`, `SaveExamAnswer`, `SubmitExam` and `IsPastDeadline` behave exactly as in "Domain behaviour". Mutators set `UpdationDate` through `Touch`.
- [ ] `RecordAttempt`/`Submit()` reject exams. The quiz answer and finish endpoints return 404 for exam ids.
- [ ] Selection follows Decision 13: exact type counts, the difficulty mix as a target, not-mastered first, deterministic with a seeded `Random`.
- [ ] Start re-checks the shortfall (`EXAM_SHORTFALL`), resolves unit → default (`UNIT_EXAM_NO_BLUEPRINT`), resumes the same unit's open exam, submits an expired one, and rejects another open exam (409).
- [ ] While open, no response carries `correctAnswer`, `explanation` or `attempt`. After submit, all are revealed.
- [ ] A save after `Deadline + DeadlineGraceSeconds` returns 400 `EXAM_TIME_EXPIRED`; submit after the deadline grades the saved answers.
- [ ] `ExpiredExamSubmissionWorker` is registered; it is off when `Exams:AutoSubmitEnabled=false` (set in `ApiFactory`) and on by default in code and in `appsettings.example.json`.
- [ ] Submitting writes one attempt per saved answer, the score out of 100, `IsPassed`, and mastery for non-test exams only.
- [ ] The result has a per-lesson breakdown and at most `WeakestObjectiveCount` weakest objectives, all below 100%.
- [ ] Migration `AddExamSittings` adds only 5 nullable columns and 2 indexes. `AppDbContextTests` lists it. `Model_Current_MatchesLatestMigrationSnapshot` passes.
- [ ] All 4 new error codes are in both resx files and both web `common:errors` files.
- [ ] Every new endpoint has `[Authorize(Policy = DefaultCodes.AssessmentsTake)]`; the endpoint authorization test passes.
- [ ] `api/openapi/v1.json` and `web/src/shared/api/generated` are regenerated with no drift; `routeTree.gen.ts` is regenerated.
- [ ] The Postman `Exams` folder has 5 requests in state order.
- [ ] Web: the 3 routes render the prototype content:
  - The start screen: summary, time, pass mark, note, shortfall, and in-progress states.
  - The exam screen: sticky header with countdown (red and announced ≤ 2 min), auto-save status, all questions, sticky submit bar, confirm with the unanswered count, auto-submit at 0.
  - The result screen: score, pass or fail, lessons with «درّب الآن», weakest objectives, review, retake, progress.
  - Each screen has loading, error-with-retry and RTL states. No hard-coded strings; both locales complete; tokens only; logical properties only.
- [ ] Progress history links exams; the unit table links «امتحان الوحدة».
- [ ] Docs updated per the Docs table (PRD §7.4 and §15, sessions, exams (new), exam-blueprints, mastery, progress, audit-log). No divergence remains.
- [ ] Every test in the Test plan exists with the given name and passes. The only existing tests changed are D1, D2, I33 and W-P3/W-P4.
- [ ] `dotnet build` with zero new warnings. `dotnet test api/ -c Release` is green with `appsettings.json` moved aside.
- [ ] Web: `npx tsc -b --noEmit`, `npx eslint . --max-warnings=0`, `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` and `npx vitest run --coverage` all exit 0.
- [ ] The guard grep is clean; no `DateTime.Now`/`UtcNow`. Handlers read time from `TimeProvider`.
- [ ] Deferred item (lesson-open gate) reported for a follow-up issue.
