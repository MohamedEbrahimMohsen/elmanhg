# Implementation — [E14.S3] Student essay input (#119)

Branch `feature/119-student-essay-input`. I merged `origin/main` first (#109 training records, `bad2c0a`) and there were no conflicts. Nothing is committed. `PROGRESS.md` holds the orchestrator's edits, which I left untouched.

## Files created
| Path | Lines | Purpose |
|---|---|---|
| api/Elmanhg.Domain/Sessions/EssaySubmission.cs | 6 | Result record of `SubmitEssay` (plan #1) |
| api/Elmanhg.Domain/Sessions/Session.Essays.cs | 67 | `FindPendingEssayAnswer`, `SubmitEssay`, `RecordEssayAttempt`. The last one stamps `UpdationDate` (xmin) and raises `AttemptsRecorded` (plan #2) |
| api/Elmanhg.Application/EssayGrading/Shared/EssayAttemptRecorder.cs | 28 | Writes the attempt and mastery from a Graded grade. It does not save (plan #3) |
| api/Elmanhg.Infrastructure/Migrations/20260930071822_AddEssayGradeTimeTaken.cs (+ Designer) | 50 | `TimeTakenMilliseconds`, plus `AppliedAt` and a filtered index (gate 2; see Deviations) |
| api/Elmanhg.Application/EssayGrading/ApplyEssayGrade/ApplyEssayGradeCommand.cs | 5 | **Not in plan** (gate 2) |
| api/Elmanhg.Application/EssayGrading/ApplyEssayGrade/ApplyEssayGradeHandler.cs | 27 | **Not in plan** (gate 2). Recorder, `MarkApplied` and one save |
| api/Elmanhg.Application/Sessions/SubmitAnswer/QuizEssaySubmission.cs | 25 | **Not in plan**. The quiz essay branch, moved out so `SubmitAnswerHandler` stays near 100 lines |
| api/Elmanhg.Application/Mastery/Shared/QuestionMasteryRecorder.cs | 20 | **Not in plan**. Mastery start/record shared by `SubmitAnswerHandler` and `EssayAttemptRecorder` |
| api/Elmanhg.Tests/Domain/Sessions/SessionEssayTests.cs | 171 | T7–T18 |
| api/Elmanhg.Tests/Integration/Sessions/EssayAnswerEndpointTests.cs | 149 | T35–T40, plus 2 extra tests (gate 1, training row) |
| api/Elmanhg.Tests/Integration/Exams/ExamEssayEndpointTests.cs | 74 | T41–T43 |
| api/Elmanhg.Tests/Application/Features/EssayGrading/ApplyEssayGrade/ApplyEssayGradeHandlerTests.cs | 120 | T28–T31 (moved here, see Deviations), plus `Handle_AlreadyApplied_DoesNothing` |
| web/src/shared/lib/localDraft.ts / .test.ts | 70 / 52 | Shared storage primitives, X1–X5 |
| web/src/features/quiz/api/essayDraftStore.ts / .test.ts | 33 / 27 | X6–X7 |
| web/src/features/quiz/api/essayItem.ts / .test.ts | 22 / 26 | X8–X10 |
| web/src/features/quiz/hooks/useEssayDraft.ts | 84 | Local draft hook |
| web/src/features/quiz/hooks/useQuizSubmit.ts | 61 | Submit mutation extracted from `useQuizAnswer` |
| web/src/features/quiz/hooks/useQuizEssay.ts | 65 | Essay card state |
| web/src/features/quiz/components/QuizQuestionHeading.tsx | 36 | Shared card heading |
| web/src/features/quiz/components/QuizEssayCard.tsx | 111 | Quiz essay card |
| web/src/features/quiz/components/EssayDraftStatus.tsx | 16 | |
| web/src/features/quiz/components/EssayAnswerText.tsx | 21 | Read-only essay |
| web/src/features/quiz/components/EssayReviewItem.tsx | 47 | Result review of a written essay (quiz and exam) |
| web/src/features/quiz/pages/QuizPage.essay.test.tsx | 150 | X17–X26 |
| web/src/features/quiz/pages/QuizResultPage.essay.test.tsx | 78 | X27–X29 |
| web/src/features/questions/components/EssayAnswerInput.test.tsx | 70 | X12–X16 |
| web/src/features/exam/pages/ExamPage.essay.test.tsx | 33 | X31 |
| web/src/features/exam/pages/ExamResultPage.essay.test.tsx | 48 | X32–X34 |

## Files modified
| Path | Change |
|---|---|
| Domain: `ServableQuestionSpecification`, `QuestionGrader`, `EssayGrader` | Essays are servable. Blank essay → `GradeBlank` → Unanswered |
| Domain: `EssayGrade.cs`, `EssayGrade.Grading.cs` | Added `TimeTakenMilliseconds` (the `Request` parameter), `AppliedAt`, `IsAwaitingApplication`, `ToQuestionGrade()` and `MarkApplied()` |
| Domain: `Attempt.cs`, `Session.cs`, `Session.Answering.cs`, `Session.ExamSubmission.cs` | `gradedBy` parameter. `CurrentPosition` skips pending essays. Answering an item with a pending essay → 409. Added the `SubmitExam(grades, essayQuestionIds, now)` overload |
| App: `QuestionAnswerRules` | Added `TryReadWrittenEssay`, `IsEssayTooLong` and **`IsRawAnswerTooLong`** (gate 1) |
| App: `SessionsOptions` | `AnswerMaxLength` **stays 4000**. Added `EssayAnswerMaxLength` = 121000 and `RequestAnswerMaxLength` (the larger of the two) |
| App: `SubmitAnswerValidator`, `SaveExamAnswerValidator` | The request ceiling is `RequestAnswerMaxLength`. The handlers enforce the cap per type |
| App: `SubmitAnswerHandler` | New dependencies: `IEssayGradeRepository`, `ContentOptions`, `SessionsOptions`. Per-type raw cap, essay length check, essay branch, free-tier skip for a pending essay, and one save |
| App: `SaveExamAnswerHandler` | Per-type raw cap and essay length check |
| App: `ExamSubmission`, `SubmitExamHandler`, `AutoSubmitExamHandler` | Written essays → `EssayGrade.Request` (time 0, `RequestedAt` = `SubmittedAt`) instead of an attempt |
| App: `StartUnitExamHandler`, `StartMultiUnitExamHandler` | **Not in plan.** They call `ExamSubmission.SubmitAsync` (expired-exam submit on start), so each gained `IEssayGradeRepository` |
| App: `SessionItemResult`, `SessionResultGenerator` | `PendingAnswer`. The answer is revealed when an essay is pending |
| Api: `Workers/EssayGradingWorker.cs` | **Not in plan** (gate 2). Sends `GradeEssayCommand` and then `ApplyEssayGradeCommand` |
| Infra: `EssayGradeRepository.GetDueIdsAsync`, `AppDbContext` | Due = Pending and due, OR Graded with `AppliedAt` null (listed first). Added a filtered index. `AppDbContextModelSnapshot` regenerated |
| `appsettings.example.json`, `ApiFactory` | `Sessions:EssayAnswerMaxLength` 121000 (`AnswerMaxLength` stays 4000) |
| `api/openapi/v1.json`, `web/src/shared/api/generated/**` | Regenerated (`pendingAnswer`) |
| Tests (modify) | D1–D6 as planned. Also `GetSubjectExamBlueprintsHandlerTests` (5→6 served types), `SubmitAnswerValidatorTests` and `SaveExamAnswerValidatorTests` (set `EssayAnswerMaxLength` next to `AnswerMaxLength`), `QuizAnswerMetricsBehaviourTests` (new positional argument), and constructor updates in `StartUnitExam*`/`StartMultiUnitExam*` tests. New tests were added to `QuestionGraderTests`, `EssayGradeTests`, `QuestionAnswerRulesTests`, `SubmitAnswerHandlerTests`, `SubmitAnswerFreeTierTests`, `SaveExamAnswerHandlerTests`, `SubmitExamHandlerTests`, `AutoSubmitExamHandlerTests`, `GradeEssayHandlerTests` (gate test) and `EssayGradingWorkerTests` (2) |
| Builders/test data | `EssayGradeBuilder` (`ForSession`, `WithTimeTaken`, `WithMaxScore`), `SessionBuilder.BuildWithEssay`, `ExamSessionBuilder.BuildWithEssay`. `EssayGradingTestData` gained the seeders and `ReadGradeForAsync`, and `GradeAsync` now also applies |
| web questions | `EssayAnswerInput` (W-EssayAnswerInput), `essayValues.isOverWordLimit`, `questionOptions` (Essay served, `essayAnswerMaxLength`, `essayCharactersLeftShownAt`), `index.ts`, i18n |
| web quiz | `quizItem` (Essay, `maxWords`, plus a `QuizItemContent` type), `quizSession`, `QuizRunner`, `QuizQuestionCard`, `QuizQuestionActions`, `EssayGradeStatus`, `useEssayGrade`, `useQuizAnswer`, `QuizResultSummary`, `QuizResultPage`, `index.ts`, i18n. **Not in plan:** `QuizReviewItem.tsx` and `FeedbackPanel.tsx` (type-only: `item: QuizItemContent`) |
| web exam / blueprints / mathSteps / test fixtures | As planned (`ExamReviewItem`, `ExamResultSummary`, i18n, `blueprintValues`, `examBlueprintSchema`, `mathDraftStore` delegating, `quizFixtures`, `examFixtures`) |
| web tests (modify) | W1–W3 as planned. X11 added to `quizSession.test.ts`, X30 to `EssayGradeStatus.test.tsx` |
| postman | Start quiz sets `essayQuestionId`. New "Submit essay answer" request before Finish. "Get essay grade" uses `essayQuestionId`, and its stale "(#119)" test text is fixed |
| docs | essay-grading, sessions, exams, question-schemas, exam-blueprints, mastery, content-retrieval, PRD (§5.3, §6, §17), claude-design-prompt, prototype, **training-data** (not in plan: essay attempts are a new `AttemptsRecorded` source) |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| D9: `Sessions:AnswerMaxLength` 4000 → 45000 | Gate 1 overrides: 4000 stays for non-essay types, and only Essay gets a larger cap, enforced per type | New `Sessions:EssayAnswerMaxLength`. The validators check the ceiling (the larger of the two), and the handlers check the raw length per type (`IsRawAnswerTooLong` → 422 `ATTEMPT_ANSWER_TOO_LONG`). Two validator tests got `EssayAnswerMaxLength` set so their small cap still applies |
| Essay raw cap 45000 | `HttpClient`/System.Text.Json send Arabic as `\uXXXX` (6 characters each), so a 10 000-character exam essay was rejected (T41 failed) | Default 121000 (20 000 × 6 + envelope), `[Range(1, 200000)]` |
| D4/D5: write the attempt in `GradeEssayHandler` in the same save as `grade.Complete`, and on conflict let `FailEssayGradeCommand` retry, which costs an AI call | Gate 2: a concurrency retry must reuse the stored grade and never call the AI again | Two steps. `GradeEssayCommand` is unchanged (AI → Complete → save). The new `ApplyEssayGradeCommand` runs `EssayAttemptRecorder` + `MarkApplied` in one save. The worker sends both. Due grades include Graded ones with `AppliedAt` null, so a lost race is re-applied on the next sweep without the AI. `GradeEssayHandler` therefore gained no dependencies, and T28–T31 live in `ApplyEssayGradeHandlerTests` with the planned method names |
| Migration contains only `TimeTakenMilliseconds` | Gate 2 needs a persisted "applied" marker | The same migration (name kept for the D5 list) also adds nullable `AppliedAt` and the index `IX_EssayGrades_GradedAt` (filter `Status = 'Graded' AND AppliedAt IS NULL`) |
| `RecordEssayAttempt` body (plan code) | Without an event, essay attempts would never reach the #109 training table | It raises `AttemptsRecorded(this, [attempt])`. Re-application returns null (attempt exists), so there is no double write. An integration test proves one training row after two applies. No EssayGrade events were added (#110) |
| Existing code touched: only listed files | `StartUnitExamHandler`/`StartMultiUnitExamHandler` also call `ExamSubmission.SubmitAsync`. `ExamItemResult` lacks `pendingAnswer`, so `QuizReviewItem`/`FeedbackPanel` typing broke. `GetSubjectExamBlueprintsHandlerTests` asserts 5 served types | Updated them (constructor, a type-only `QuizItemContent`, 5→6). All are additive |
| `SubmitAnswerHandler` essay branch inline | The file was 99 lines, and the inline branch plus a second save call would exceed the size rule and break "one save" | Extracted `QuizEssaySubmission` and `QuestionMasteryRecorder`. The handler has a single `SaveChangesAsync` (107 lines) |
| `purgeLocalDrafts<T>(…, isAnswer: … answer is T)` | ESLint `no-unnecessary-type-parameters` fails | Non-generic, with `isAnswer: (answer: unknown) => boolean` |
| D19 Postman: no change | The "Get essay grade" test text said "until essays are served (#119)", and there was no way to exercise essay submission | Added the "Submit essay answer" request and the `essayQuestionId` variable |

## Build & test
- `dotnet build api/Elmanhg.Api`: 0 errors. `api/openapi/v1.json` regenerated.
- CI parity: `dotnet test -c Release` with `api/Elmanhg.Api/appsettings.json` moved aside and then restored → **total 3842, failed 0, succeeded 3842**.
- `dotnet format --verify-no-changes`: the only finding is `SubscriptionBuilder.cs` whitespace (pre-existing CRLF noise, not touched here).
- The guard grep (`DateTime.Now`/`.Result`/…) on the diff printed nothing.
- web:
  - `npx tsc -b --noEmit` clean.
  - `npm run lint` clean.
  - `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` → "All matched files use Prettier code style!".
  - `npx vitest run` → **207 files, 1191 tests passed**. One earlier full run had two timing flakes, `ExamPage.autosave` "saves only the latest answer…" and `QuizResultPage.freeTier` (neither file touched). Both passed alone and on the next full run.
  - `npm run build` OK.
  - `npm run perf:budget` → entry 202/210, landing 210/220, lesson 231/240, **quiz 254/255 KB** (passes, no budget raised).
  - `npm run gen:api` gives no further drift.
- ai/: not touched, not run.
- Mutation checks. Each mutation was made on purpose, confirmed to fail, and restored:
  - API: removing the pending-essay 409 in `RecordAttempt` (T14 fails); removing the `UpdationDate` stamp (T15 fails); making the raw cap ignore type (2 tests fail); removing the worker's Apply send (2 worker tests fail).
  - Web: removing the `onGraded` call (X28, X30 fail); dropping `maxLength` (X15); dropping `pendingAnswer` in merge (X11); disabling the exam essay branch (X32, X34); dropping `draft.discard` (X21 caught it only after I strengthened X21 to wait for the saved draft first).

## Notes for review
- There is a millisecond window between the Grade save and the Apply save in which `GET essay-grade` says Graded while the attempt is not yet written. `onGraded` refetches the session then. The chance is roughly ms / 2 s poll. On an Apply conflict the gap lasts until the next sweep. I did not remap the status to Pending until applied, because that would change the #118 `GetEssayGrade` contract and its tests. That is a candidate for a deferred issue.
- A permanently failing Apply (not a transient conflict) would retry on every sweep without a cap, logging a warning each time. The only realistic failure is the xmin race.
- Rows from before the migration that are `Graded` have a null `AppliedAt`, so the next sweep applies them. Before #119 their essays were never session items, so the recorder writes nothing and only stamps `AppliedAt`.
- `SessionBuilder.BuildWithEssay` / `ExamSessionBuilder.BuildWithEssay` call `Questions.Essay()`, which leaves that builder in essay mode. Tests use a fresh builder for a second session.
- Exam essays in test-mode sessions still get AI grading and an attempt, with no mastery (Decision 18).
- The quiz chunk is at 254/255 KB br, so the headroom is 1 KB.
- `IsAwaitingApplication` is a computed property placed after `IsDueAt` in `EssayGrade.cs`.
