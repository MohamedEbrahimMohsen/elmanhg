VERDICT: APPROVED

# Review — [E15.S3] LLM step grading + #237 follow-ups

## Blocking
None.

## Non-blocking
1. `ai/src/elmanhg_ai/cas/nodes.py:108-128`, `ai/src/elmanhg_ai/settings.py:87`: the 5000-term bound blocks every blow-up I tried at parse time in 0.0 s: `(x+y+z+w)^{999}`, `x=(x+y+z+w)^{999}`, `((x+y+z+w)^{3})^{333}`, `(x+y+z)^{30}(x+y+z)^{30}`, `\sqrt{...}`, `\frac{1}{...}`, a negative exponent, `(x+y+z+w)^{3^{6}}`, and `A-A` of the same power. It still lets through answers that exceed `cas_timeout_seconds` (5 s). In-process timings: `x=(x+y+z)^{90}` (4186 terms) 13.2 s; `x=(x+y+z+w)^{20}` (1771 terms) 3.9 s. Such an answer never returns a 5xx: the worker times out and returns `unchecked`. But with #123 an `unchecked` answer is deferred and re-checked `MaxAttempts` times, so one crafted answer costs 5 CAS timeouts plus worker recycles. The per-student limiter bounds this. Consider a lower default (about 1000), or treating a repeated timeout as `unreadable`. Note that docs/math-cas.md says "SymPy never has to expand it", which only holds for answers above the bound.
2. `api/Elmanhg.Application/Subscriptions/Shared/FreeTierGate.cs:47` -> `SessionRepository.CountQuizAttemptsOnDayAsync` counts attempts only. A pending MathSteps answer therefore does not count toward the free daily quota until it is applied. This covers every answer while the AI service is down, for the whole retry window. Essays (#119) have the same gap. Follow-up issue.
3. `web/src/features/quiz/components/QuizResultSummary.tsx` (acknowledged deviation): the quiz summary does not count pending math as answered, while `web/src/features/exam/components/ExamResultSummary.tsx:20-22` does. Small UI inconsistency; follow-up.
4. `api/Elmanhg.Application/Sessions/Shared/SessionResultGenerator.cs:26`: `reveal` is true for any pending answer, so the API returns `correctAnswer` (now including `modelSolution`) while a math answer is pending. The UI hides it (D23), and the answer is already locked (a different answer returns 409), so nothing can be exploited. Worth aligning with the PRD section 6 wording "the correct answer stays hidden until the grade is applied" when #128 touches this.
5. `ai/src/elmanhg_ai/cas/pool.py:66-72`: `warm()` warms the slots one after another. I measured it and it is harmless: in a real Docker build of this branch, `/health/ready` answered on the 4th poll, about 4 s after `docker run` (warm-up about 2.6 s). The CI loop allows about 30 s.
6. `SubmitAnswerHandler.cs` (119 lines), `MathStepGrade.Grading.cs` and `MathStepGradingWorker.cs` (103 each) are slightly over the ~100-line guide. The report acknowledges this.
7. `api/Elmanhg.Application/Exams/Shared/ExamSubmission.cs:50`: the AwaitsReview filter can no longer match new data. It is harmless dead weight.

## Verified
- **Builds and suites, re-run by me:**
  - api: `dotnet build -c Release` has 0 warnings. `dotnet test api/ -c Release --no-build` gives **4365 passed, 0 failed**. There is no `appsettings.json` in the worktree, which matches CI.
  - api: `dotnet ef migrations has-pending-model-changes` -> "No changes". `dotnet format --verify-no-changes --exclude api/core-libraries` is clean apart from the pre-existing `SubscriptionBuilder.cs`.
  - ai: `ruff format --check`, `ruff check` and `mypy src` are clean. `pytest -m "not eval"` gives **448 passed**.
  - web: `typecheck` and `lint` pass. Prettier with `--end-of-line auto` is clean; the CRLF noise is from the Windows checkout. `vitest run` gives **247 files, 1385 tests passed**.
  - Orval: `gen:api` into the tree produces no drift. The generated folder was restored byte-identical afterwards.
- **Prompt injection (D17):**
  - The system prompt is static. Rule 7 marks everything inside both tags as data, ignores injection attempts and caps confidence at 0.3.
  - Context and work are JSON-dumped after `strip_fields` with `delimiter_pattern("grading_context","student_work")`, iterated until stable, over every string, including the model solution, accepted answers and objectives.
  - The reply is parsed with `extra="forbid"`. The step indexes must be exactly {0..n-1} with no duplicates, and points outside 0..2 are rejected, not clamped (`ai/src/elmanhg_ai/pipelines/math_step_grading_output.py:52-60`).
  - .NET re-validates the reply (`AiMathStepGradingReplyRules.cs`), and `MathStepsGrader.Combine` re-validates the awards and recomputes the score. The model's total is never used.
  - Confidence below the threshold -> `InReview/LowConfidence` (`MathStepGrade.Grading.cs:43,59-60`).
  - The logs carry counts only; the ai test asserts that the student text is absent. .NET logs no answer text.
  - The grader never receives the verdict or any student or session id (test 75).
  - The fake is the default in both services (`AiServiceOptions.Provider = Fake`, `llm_provider = "fake"`). The real adapters are selected by config, and the fake refuses in Production.
- **Combine rule:** `MathStepsGrader.Combine` implements ((100-w)F + wS)/100, with S = sum of points / (2n), exactly as in plan D1. PRD section 6, question-schemas.md and math-step-grading.md state the same formula and the w=50 -> 0.75 example. D4 and D5 (no steps -> 0 with no LLM call; a blank final answer is Unanswered) match `AnswerGrader.cs` `DecideAsync` and the PRD text.
- **Unchecked -> pending, end to end:**
  - `HttpAiMathCheckClient` turns any failure into `Unchecked`, and `DecideAsync` defers it. The answer is saved on the item, a `MathStepGrade` is added with a null verdict, the response has attempt null plus pendingAnswer, and no answer is lost.
  - Exam submit defers pending math via `ExamDeferredGrading` in the same save, so a submit never fails.
  - The worker runs Check -> Grade -> Apply. Apply records the attempt through `RecordAiGradedAttempt` (`AttemptGrader.AI`, one `AttemptsRecorded` event) and records mastery unless the session is in test mode.
  - Re-apply is idempotent. `IsAwaitingApplication` guards it, `RecordAiGradedAttempt` returns null when an attempt already exists, and a null attempt means no mastery and no event, so no double training records.
  - `IsReplay` runs before the limiter and the CAS call (`SubmitAnswerHandler.cs:99-110`), and a different answer returns 409.
  - The web quiz card shows the answer read-only while pending, with the status panel, "Next" enabled and no correct answer. The quiz result and exam result list pending math and show the provisional note or badge.
- **#237 items:**
  - `pool.py:56-59` catches any other exception, logs it, recycles the worker and returns unchecked. Test 46 asserts the recycle and the log.
  - The expansion bound is verified above.
  - The per-student limiter is keyed by `currentUserService.UserId` (not client-controlled), uses a partitioned fixed window with queue 0, and covers quiz answers only, with a final answer and not a replay. A 429 stores nothing (the test asserts `SaveChangesAsync` DidNotReceive and `SavedAnswer` null).
  - The lifespan warms the pool when `cas_warm_on_start` is set. The Docker readiness was measured, not assumed.
- **Worker #81 pattern:** `MathStepGradingWorker` is the `EssayGradingWorker` with only the extra Check command and renamed messages (checked with a mechanical diff). It uses a scope per item, metrics registration and listing-failure handling, and records failures through `FailMathStepGradeCommand` in a new scope. The repository lists Pending-due and Graded-unapplied grades.
- **Migration:** CreateTable, 4 FKs with Restrict, and 6 indexes (unique (SessionId, QuestionId), two filtered). `Down` drops the table. It is non-destructive, and `AppDbContextTests` lists `_AddMathStepGrades`. The soft-delete query filter was added in the global method.
- **Contracts:**
  - OpenAPI: `api/openapi/v1.json` has `math-step-grade`, and `ai/openapi/v1.json` has `/v1/math-step-grades`. The ai OpenAPI test passes.
  - Postman: "Get math step grade" is added with GET, the correct route, inherited student auth, a skip guard and a 200/404 test. The Create math question and Grade math draft bodies carry `modelSolution` and `stepsWeight`, and draft asserts `mathSteps`. The AI endpoints are not in Postman, consistent with the existing convention.
- **Plan contract:**
  - Every A/D/P/I/W/T/F file exists.
  - Every planned .NET, ai and web test name exists, checked by grep. The only misses are the four tests the plan replaced.
  - The deviations are real and correctly reported: the StartUnit/StartMultiUnit ctors, `inputMode`, the 200 vs 201 status, and the D21 text.
- **Docs-sync:** PRD sections 6, 6.1 and 8.3, math-cas.md, sessions.md, exams.md, question-schemas.md, ai-service.md, training-data.md, claude-design-prompt.md section 4, prototype.md, backlog E17, deployment.md, the env examples and the new math-step-grading.md all agree with the code. I found no divergence.

## Test quality
- `MathStepGradeTests`, `MathStepsGraderStepsTests`, `SessionReplayTests`: constrain every domain branch (thresholds, the exponential delay, both review reasons, award validation).
- `SubmitAnswerHandlerTests`: the unchecked, step-graded, replay (`Received(1)` on the check, limiter and add), 429 and blank/non-math limiter tests all constrain the new ordering.
- `CheckMathStepAnswer`, `GradeMathSteps`, `Apply`, `Fail`, `GetMathStepGrade` handler tests: each throw path asserts the code and `DidNotReceive` on save, and #78 exercises the #237 item-4 regression through the domain.
- The integration tests run the real pipeline through the fakes and assert the DB state.
- ai: the pipeline tests assert the actual turn text (forged tags removed) and the absence of student text in the logs. The pool test asserts the recycle and the log. The parser tests pin the blow-up cases.
- web: the status/quiz/exam tests assert user-visible states, including the 404 -> nothing case and a single `onGraded` call.
- No test merely echoes a substitute.
