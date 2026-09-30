# Implementation (merge) — #122 on top of #119, #110 and #105

The reviewed #122 work was stashed, the branch was fast-forwarded to `origin/main` (`64ff902`: #119 student essay input, #110 JSONL export, #105 dashboard UI), and `git stash pop` left 46 conflicted files. All of them are resolved, everything is staged, and **nothing is committed**. The stash entry (`stash@{0}`) was dropped after the checks below passed.

## Semantic resolutions

| Area | #119 (main) | #122 (stash) | Resolution |
|---|---|---|---|
| `ServableQuestionSpecification.ServedTypes` | + `Essay` | + `MathSteps` | All seven types. `ServedTypes_EveryType` keeps #119's name and also covers MathSteps. #122's `IsSatisfiedBy_ApprovedMathStepsInPublishedLesson_ReturnsTrue` is kept. `GetSubjectExamBlueprintsHandlerTests` counts go from 6 to 7 |
| `SessionsOptions` answer caps | `AnswerMaxLength` 4000, `EssayAnswerMaxLength` 121000, `RequestAnswerMaxLength` = the larger | `AnswerMaxLength` raised to 24000 for LaTeX steps, plus 3 `Math*` caps | The cap is per type. `AnswerMaxLength` **stays 4000** for the five v1 types. `EssayAnswerMaxLength` is 121000. New `MathStepsAnswerMaxLength` (24000, `[Range(1, 100000)]`) holds #122's sizing. `MathStepsMaxCount`/`MathStepMaxLength`/`MathFinalAnswerMaxLength` are kept. `RequestAnswerMaxLength` = the largest of the three raw caps. Updated in `appsettings.example.json`, `ApiFactory` (`UseSetting` dictionary) and the two validator tests, which now also set `MathStepsAnswerMaxLength` so their small cap still applies |
| `QuestionAnswerRules` | `TryReadWrittenEssay`, `IsEssayTooLong`, `IsRawAnswerTooLong` (essay or not) | `ExceedsLimits` (MathSteps caps) | All four are kept. `IsRawAnswerTooLong` picks the cap through a private switch expression `RawAnswerMaxLength(type, options)`: Essay, MathSteps, or the default |
| `SubmitAnswerHandler` | Essay branch through `QuizEssaySubmission`, and #119's 13-parameter constructor | `AnswerGrader` (the CAS for MathSteps), and mastery skipped when `grade.AwaitsReview` | Keeps #119's constructor with `IAiMathCheckClient mathCheckClient` appended. Check order: `CanRead`, then `IsRawAnswerTooLong \|\| ExceedsLimits` (422 `ATTEMPT_ANSWER_TOO_LONG`), then `IsEssayTooLong`, then either the essay branch or `AnswerGrader.GradeAsync` → `RecordAttemptAsync`. `RecordAttemptAsync` returns without mastery when `grade.AwaitsReview`. There is still exactly one `SaveChangesAsync` (110 lines, #119 was 107) |
| `SaveExamAnswerHandler` | Per-type raw cap and essay length check | `ExceedsLimits` | #119's constructor is unchanged. The checks are the same as the quiz handler's |
| `ExamSubmission.SubmitAsync` | Written essays → `EssayGrade.Request` and no attempt; other items graded synchronously | Every item graded asynchronously through `AnswerGrader`; `AwaitsReview` attempts skip mastery | New signature `(session, revisions, questionRepository, questionMasteryRepository, essayGradeRepository, mathCheckClient, threshold, now, ct)`. Essays are split out first. The remaining saved answers are graded one by one through `AnswerGrader` (MathSteps → CAS; an outage → `unchecked`). `session.SubmitExam(grades, essayIds, now)` records attempts. `AwaitsReview` attempts are then filtered out of mastery, and the essay grades are requested in the same unit of work. **One exam submit handles both pending kinds** (99 lines) |
| `SubmitExam`/`AutoSubmitExam`/`StartUnitExam`/`StartMultiUnitExam` handlers | + `IEssayGradeRepository` | + `IAiMathCheckClient` (last) | Both. #119's constructor order with `IAiMathCheckClient mathCheckClient` appended last. The 8 test constructors are updated to match |
| `SessionResultGenerator` / `AttemptResult` | `pendingAnswer` on `SessionItemResult`; reveal on a pending essay | `awaitsReview` on `AttemptResult` | Both fields. The reveal rule is #119's (`attempt`, `pendingAnswer` or submitted) |
| resx / `web/src/shared/i18n` | TRAINING_EXPORT_* (#110) | QUESTION_MATH_* and MATH_CHECK_UNAVAILABLE | Union (371 entries in each resx) |
| Handler tests | Essay tests | Math tests | Kept every test from both sides, each with its own `[Fact]`: SubmitAnswerHandlerTests, SaveExamAnswerHandlerTests, SubmitExamHandlerTests |
| Migrations | #119 `20260930071822_AddEssayGradeTimeTaken` | none | #122 adds no migration, so the `AppDbContextTests` list stays main's (timestamp order unchanged) |
| OpenAPI / Orval | `pendingAnswer` | `MathSteps`, `awaitsReview` | `dotnet build -c Release` regenerated `api/openapi/v1.json` (it holds both). `npm run gen:api` produced no further drift in `web/src/shared/api/generated` |
| Postman | "Submit essay answer", `essayQuestionId` | "Create math question", "Grade math draft" | Merged cleanly. All four requests are present, and the JSON parses |
| Web served types | `'Essay'` in `servedQuestionTypes`, `quizQuestionTypes`, blueprint counts/schema | `'MathSteps'` in the same lists | Both, in enum order. `QuizItemContent` (#119) is kept |
| `useQuizAnswer` | Submit moved out to `useQuizSubmit` | Inline mutation plus a `useEffect` that clears the math draft once the attempt is recorded (the D23 ordering fix) | #119's `useQuizSubmit` and #122's `draftOwner` parameter and draft-clearing effect. Nothing is duplicated |
| "Under review / pending" UI | Muted `result.essaysPending` line in the quiz and exam summaries; `answered` counts written essays | Warning `result.inReview` line (plural) and exam `result.provisional` badge instead of "failed" | **Shared component** `quiz/components/ProvisionalScoreNotes.tsx` renders both notes: the warning line when attempts have `awaitsReview`, and the essay note when `hasPendingEssay`. The two can show together. It also uses the new helper `quiz/api/pendingGrades.ts` `countAwaitingReview`. Both summaries use it, with #119's `answered` rule. The exam namespace's `inReview`/`essaysPending` strings were identical to the quiz ones and are removed; the exam uses the quiz strings through the shared component. The exam badge stays as #122 had it: provisional only for math answers under review, while #119's pending-essay note keeps the failed badge, as before. Per-question states are unchanged: `EssayGradeStatus` (essay) and `FeedbackPanel` `InReview` (math) keep their own hint text, because the reasons differ (AI grading vs. teacher review of an unchecked CAS verdict) |
| Quiz page budget | 254/255 | 253/255 | The first merged build measured **256/255 (981 bytes over)**. Both lazy boundaries were already in place: `QuizEssayCard` (`React.lazy` in `QuizRunner`) and `MathStepsAnswerInput` (`React.lazy` in `AnswerInputs`). I tried lazy-loading `EssayAnswerInput` too. It saved only about 200 bytes net, because rolldown split zod into more chunks, so I reverted it. Fix: new `subscription/components/LazyPaywallDialog.tsx`, exported from the feature index. `QuizQuestionCard` now renders the paywall dialog, and so Radix Dialog (about 10 KB br), only when a paywall reason exists. Result: **quiz 245/255** |
| Docs | essay-grading, exams, sessions, question-schemas, exam-blueprints, claude-design-prompt §4 | math-cas, sessions, exams, question-schemas, exam-blueprints, deployment, claude-design-prompt | Conflicts merged as a union and restated for the merged behaviour: seven served types; per-type raw caps including `Sessions:MathStepsAnswerMaxLength` (sessions.md config table, validator paragraph and error table; exams.md; essay-grading.md; deployment.md env table); exam submit with both pending kinds (exams.md); the quiz and exam result notes (claude-design-prompt §4). Removed #122's now-false line "Essay … Not servable until #119" |

## Files created by the merge
| Path | Lines | Purpose |
|---|---|---|
| web/src/features/quiz/components/ProvisionalScoreNotes.tsx | 23 | Shared under-review and essay-pending notes for the quiz and exam results |
| web/src/features/quiz/components/ProvisionalScoreNotes.test.tsx | 34 | Both notes together, and none when every grade is final |
| web/src/features/quiz/api/pendingGrades.ts | 5 | `countAwaitingReview` |
| web/src/features/subscription/components/LazyPaywallDialog.tsx | 19 | Loads the paywall dialog only when it opens (quiz budget) |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| #122 plan: `Sessions:AnswerMaxLength` 24000 | #119's gate 1 requires 4000 for non-essay types and per-type enforcement | New `Sessions:MathStepsAnswerMaxLength` 24000. `AnswerMaxLength` stays 4000 |
| Quiz budget: keep the math input and the essay card lazy | Merged, the page was 981 bytes over even with both lazy | `LazyPaywallDialog` in `QuizQuestionCard`. This touches the subscription feature, which is outside both stories. No budget was raised |
| — | Exam `inReview`/`essaysPending` strings duplicated the quiz ones | Removed them from `exam/i18n/{ar,en}.json`. `ProvisionalScoreNotes` uses the quiz namespace |

## Build & test
- `api/Elmanhg.Api/appsettings.json` was absent, so this is CI parity. `dotnet build api/ -c Release`: Build succeeded, 9 warnings, all pre-existing in `core-libraries`. `dotnet test api/ -c Release --no-build`: **total 4057, failed 0, succeeded 4057** (Docker 29.6.2).
- ai: `uv sync --locked` ok. `ruff format --check`: 121 files already formatted. `ruff check`: All checks passed. `mypy src`: no issues in 68 source files. `pytest -q -m "not eval"`: **389 passed**, 3 deselected.
- web: `gen:api` (no drift), `tsc -b --noEmit` exit 0, `npm run lint` clean, `prettier --check --end-of-line auto .` "All matched files use Prettier code style!". `vitest run` before the paywall change: **229 files, 1295 tests passed**. After it: the quiz folder passed (21 files, 146 tests), and the final full run passed with **230 files, 1297 tests**. `npm run build` ok. `npm run perf:budget`: entry 204/210, landing 213/220, lesson 234/240, **quiz 245/255**, admin-dashboard 222/230, all ok.

## Notes for review
- `LazyPaywallDialog` returns null when there is no reason. The original dialog is always mounted with `open={reason !== null}`, so the close animation no longer plays in the quiz card. Other callers (`QuizEssayCard`, `PracticeStart`, exam start) still use the eager `PaywallDialog`.
- The working copies of the resolved files were written with LF line endings. Git's `autocrlf` normalises them, which is why it prints "LF will be replaced by CRLF" warnings. The index content is unaffected.
- Items still owed to #128 (teacher review): unchecked math attempts keep a provisional 0, and an essay grade in review waits. Both are documented.

---

# Rework (merge review) — 03-review-merge.md

| # | What I changed | file:line |
|---|---|---|
| 1 | Added `IsRawAnswerTooLong_MathStepsUsesMathStepsCap` (`AnswerMaxLength = 20`, `MathStepsAnswerMaxLength = 40`, a 38-char MathSteps answer: MathSteps → false, Short → true) and the theory `IsRawAnswerTooLong_MathStepsAtConfiguredCaps_ComparesAgainstMathStepsCap` at the shipped caps 4000/24000: raw 4001 → false and 23999 → false (between the answer cap and the MathSteps cap), 24001 → true (over `Sessions:MathStepsAnswerMaxLength`). The helper `MathStepsAnswerOfRawLength` builds a MathSteps-shaped answer of an exact raw length and asserts it | `api/Elmanhg.Tests/Application/Features/Questions/Shared/QuestionAnswerRulesTests.cs:96`, `:111`, `:119` |

**Mutation check** (`dotnet test --project api/Elmanhg.Tests -c Release -- --filter-class "*QuestionAnswerRulesTests"`, source restored after each; `git diff` of `QuestionAnswerRules.cs` against the index is empty):
- Map the arm to `options.AnswerMaxLength`: **3 failed** / 16 passed (`MathStepsUsesMathStepsCap`, theory 4001, theory 23999).
- Delete the arm (`QuestionAnswerRules.cs:56`): **3 failed** / 16 passed (same three).
- Map the arm to `options.EssayAnswerMaxLength` (over-cap check): **1 failed** / 18 passed (theory 24001).

**Non-blocking items taken**
- `SubmitExamHandlerTests.Handle_WrittenEssayAndUncheckedMathSteps_HandlesBothPendingKindsInOneSubmit` (`api/Elmanhg.Tests/Application/Features/Exams/SubmitExam/SubmitExamHandlerTests.cs:213`): one exam with an unanswered MCQ pair, a written essay and a MathSteps answer whose CAS returns `Unchecked`. Asserts one `EssayGrade` requested for the essay, the only attempt is the math one, its result has `awaitsReview`, no mastery rows are added (the unchecked attempt is filtered out) and exactly one `SaveChangesAsync`. Used the existing unit fixture (no integration fixture needed).
- `docs/performance.md` §3: measured column refreshed from this run's `perf:budget` (entry 204, landing 213, lesson 234, quiz 245, admin-dashboard 222; budgets unchanged). §4: new bullet "Lazy quiz extras" for `MathStepsAnswerInput`, `QuizEssayCard` and `LazyPaywallDialog`. The §8 "Bundle sizes after this change" line is a historical record of #114 and was left as is.
- Not taken: mastery assertion on the manual-submit unchecked test (reviewer: covered by `AutoSubmitExamHandlerTests`), and a `LazyPaywallDialog` unit test (reviewer: covered by `QuizPage.freeTier.test.tsx`).

## Deviations (rework)
None.

## Build & test (rework)
- No `api/Elmanhg.Api/appsettings.json` (CI parity). `dotnet build api/ -c Release`: 0 Error(s). `dotnet test api/ -c Release` (final run, after the mutants were reverted and rebuilt): **Passed! total 4062, failed 0, succeeded 4062** (4057 + 5 new cases).
- `npm --prefix web run build`: ok. `npm --prefix web run perf:budget`: entry 204/210, landing 213/220, lesson 234/240, quiz 245/255, admin-dashboard 222/230, all ok. No web source changed, so vitest was not re-run.
- Nothing committed.
