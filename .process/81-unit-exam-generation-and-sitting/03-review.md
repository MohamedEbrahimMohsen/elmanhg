VERDICT: APPROVED

# Review — [E6.S2] Unit exam generation and sitting (#81)

## Blocking
None.

## Non-blocking
- `api/Elmanhg.Api/Workers/ExpiredExamSubmissionWorker.cs:42,56`: the filter `exception is not OperationCanceledException` lets any OCE that is not a shutdown escape the loop. With the default `BackgroundServiceExceptionBehavior.StopHost`, that would stop the API. `when (!stoppingToken.IsCancellationRequested)` is safer. In practice only `stoppingToken` cancels today, so this is not a live bug.
- `api/Elmanhg.Application/Exams/GetExpiredExamSessionIds/GetExpiredExamSessionIdsHandler.cs:14`: an exam whose auto-submit keeps failing (for example, a missing revision throws `InvalidOperationException`) stays at the head of the Deadline-ordered batch. `AutoSubmitBatchSize` (50) such exams would starve every later expired exam.
- `api/Elmanhg.Application/Exams/StartUnitExam/StartUnitExamHandler.cs:42`: when the student has an *expired* open exam in another unit, start returns 409 until the worker sweeps it (up to about 60 s plus grace). Submitting it inline would be friendlier. The plan specifies the current behaviour, and the start screen's "Continue that exam" link auto-submits it on open.
- `web/src/features/exam/hooks/useSubmitExam.ts:24-28` and `useStartExam.ts:17-23`: neither invalidates the unit-overview query or the progress history query. With the 30 s default `staleTime` (`web/src/app/queryClient.ts:3`), this can happen: a student loads the start page while an exam is open, submits within 30 s, then clicks «إعادة الامتحان». The page still shows «استكمل الامتحان», which bounces to the result page. Invalidate `getGetUnitExamOverviewQueryKey(unitId)` and the history on submit and start.
- A manual submit racing the worker is safe: xmin on `Session` and `IX_Attempts_SessionId_QuestionId` roll back the loser. The student who loses sees a 409 toast (`SESSION_MODIFIED_CONCURRENTLY` or `SESSION_QUESTION_ALREADY_ANSWERED`) even though the exam is submitted. `useSubmitExam.ts:29-35` refetches, and the page then navigates to the result. This is cosmetic.
- `docs/sessions.md` § Model, `LastActivityAt` row: it says "set at start, on resume, on each attempt and on finish". Exam saves also set it (`api/Elmanhg.Domain/Sessions/Session.Exam.cs:75`). One clause to add.
- `web/src/features/exam/components/ExamHeader.tsx:32`: the countdown uses `text-h3` (16 px). The design prompt §2 and `.claude/design-system.md` ExamTimer say 18 px, but no 18 px text token exists, so this follows the plan's choice.
- No test covers `ExpiredExamSubmissionWorker` itself: the loop, isolation between sessions, and the `AutoSubmitEnabled` switch. The handlers it sends are tested directly (A44–A47, I27–I29). A small `FakeTimeProvider` test would lock in the "one failing id does not stop the sweep" contract.

## Verified
- **Builds and tests (run by me).**
  - `dotnet test api/ -c Release` with `appsettings.json` moved aside (restored afterwards): 1781/1781 passed, no warnings in the log.
  - Web `typecheck` and `lint` (`--max-warnings=0`) are clean.
  - `test -- --run`: 97 files, 606 tests passed.
  - Prettier check with `--end-of-line auto`: clean.
  - The implementation report's numbers match.
- **Every "Files to create" item exists.** Nothing extra beyond the reported test helpers. Every "Existing code touched" change is present, including the `ToMicroseconds` extraction with the WHY comment moved and `CalculateScorePercent()`.
- **Deadline and grace.**
  - `IsPastDeadline` is `now > Deadline + grace` (`Session.Exam.cs:53`).
  - A save checks it against the microsecond-truncated time, after `EnsureNotSubmitted`.
  - The sweep cutoff `Deadline < now - grace` (`GetExpiredExamSessionIdsHandler.cs:13-14`) matches.
  - Untimed exams have `Deadline` null and are never listed or expired.
  - Submit is always allowed.
- **Auto-submit worker.**
  - It runs on a `PeriodicTimer` over the injected `TimeProvider`.
  - It creates a new async scope for the listing and one per session id.
  - Failures are logged: `Error` for the listing, `Warning` per session.
  - It is off in integration tests through `UseSetting("Exams:AutoSubmitEnabled", "false")` (`ApiFactory.cs:47-48`), and on by default in code and in `appsettings.example.json`.
  - `AutoSubmitExamHandler` re-checks open and expired on a tracked load before submitting. Together with xmin, this makes it idempotent against manual submit and against a late save.
- **Exam vs quiz isolation.**
  - `SubmitAnswerHandler` and `FinishSessionHandler` filter `Kind == Quiz` (D1 and D2 prove the 404).
  - Every exam handler filters `Kind != Quiz`.
  - Domain guards are in place for `RecordAttempt`, `Submit()`, `SaveExamAnswer` and `SubmitExam`.
  - `GET /api/sessions/{id}` reveals nothing for an open exam, because reveal requires an attempt or a submitted session.
- **Grading uses the served revision.** `ExamSubmission.FindRevision` matches `QuestionId` and `QuestionVersion`, and a missing revision throws (`ExamSubmission.cs:53-56`). The stored answer is the canonical form.
- **Mastery on submit.**
  - The masteries are loaded tracked.
  - A new question gets `QuestionMastery.Start`, collected into one `AddRangeAsync`. An existing row gets `Record`.
  - Test mode and zero attempts skip it entirely, in the same save as the submission.
  - A42 and I22 show a second submit writes nothing.
  - The streak stays quiz-only.
- **Selection and breakdown.** The selector follows Decision 13. The breakdown follows Decisions 15–16: soft-deleted objectives are filtered through the global query filter on `LessonObjective`.
- **Persistence.** The migration has 5 `AddColumn` and 2 `CreateIndex` in Up, with drops only in Down. `IX_Sessions_OneOpenExam` maps to 409 `EXAM_ALREADY_IN_PROGRESS`. `SavedAnswer` is jsonb.
- **Error codes.** All 4 codes are in both resx files and in both web `common:errors` files.
- **Postman.** The `Exams` folder sits right after `Sessions`, with 5 requests in state order. The URLs and methods are correct, auth is inherited as bearer, and the Start test script sets `examSessionId` and `examQuestionId`.
- **Docs sync.** `docs/exams.md` is new. `PRD.md` §7.4 and §15, `sessions.md`, `exam-blueprints.md`, `mastery.md`, `progress.md` and `audit-log.md` all agree with the code. `claude-design-prompt.md` and `prototype.md` list best score and attempts (#83), which is incompleteness only.
- **Web.**
  - All three routes have loading, error-with-retry and RTL states.
  - The countdown is anchored to `serverNow`: it turns `text-danger` and gets an sr-only status at 2 min or less, and auto-submits at 0.
  - Auto-save is debounced per question and chained. `flush` runs before submit.
  - `EXAM_TIME_EXPIRED` and `SESSION_ALREADY_SUBMITTED` trigger the submit.
  - Only tokens are used: `p-4.5` and `py-2.25` are the existing card and table padding pattern across features. No physical left/right classes, and no hard-coded strings.
- **Tests exist under the planned names.** A script checked every backticked test method name in the plan against `api/Elmanhg.Tests`, with no misses. W1–W44 and W-P1–W-P5 are present.
- **Deviations.** The six listed deviations are accurate and harmless. Validating grades before mutating in `SubmitExam` is better than the plan's version.

## Test quality
- **Domain tests:** real aggregates, with assertions on state, codes and ordering. They constrain the implementation.
- **Handler tests (Start, Submit, SaveExamAnswer, AutoSubmit):**
  - Repositories are stubbed with compiled predicates, so filter mistakes surface.
  - Assertions cover domain state (attempts, `SubmittedAt`, `LatestAttemptId`, `IsMastered`), `AddAsync` and `AddRangeAsync` arguments, and `SaveChangesAsync` `Received(1)` or `DidNotReceive()`.
  - None only echoes a substitute.
- **`GetExpiredExamSessionIdsHandlerTests`:** applies the handler's own filter to five kinds of session, and checks the page size and `asNoTracking`.
- **Integration:** these run against real PostgreSQL. They assert the DB rows (attempts, jsonb `SavedAnswer`, `QuestionMasteries`), the unique-index 409, and 404 or 401 on every endpoint.
- **Web:** assertions are on the MSW request bodies and order (PUT before POST), on fake-timer countdown text, and on navigation.
- **Gap:** no worker-level test (see Non-blocking).
