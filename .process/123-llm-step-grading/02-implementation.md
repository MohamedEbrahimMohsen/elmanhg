# Implementation — [E15.S3] LLM step grading (v2) + #237 CAS follow-ups

Worktree `D:/Personal/elmanhg-wt/123`, branch `feature/123-llm-step-grading`. `origin/main` (#239, 28528a6) was merged first with no conflicts. Nothing is committed.

## Files created
| Path | Lines | Purpose |
|---|---|---|
| ai/src/elmanhg_ai/api/math_step_grades/__init__.py | 0 | A1 package |
| ai/src/elmanhg_ai/api/math_step_grades/schemas.py | 35 | A2 `MathStepGradeIn` / `StepGradeOut` / `MathStepGradeOut` |
| ai/src/elmanhg_ai/api/math_step_grades/router.py | 47 | A3 `POST /v1/math-step-grades` (`grading_create_math_step_grade`) |
| ai/src/elmanhg_ai/pipelines/math_step_grading_output.py | 75 | A4 reply parser (`MAX_STEP_POINTS`, schema/steps/points rejects) |
| ai/src/elmanhg_ai/pipelines/math_step_grading.py | 189 | A5 pipeline (limits, delimiter stripping, model call, logging) |
| ai/src/elmanhg_ai/prompts/math_step_grade_system.v1.md | 13 | A6 static system prompt |
| ai/src/elmanhg_ai/prompts/math_step_grade_turn.v1.md | 7 | A7 turn template |
| ai/src/elmanhg_ai/prompts/math_step_grade_output.v1.json | 22 | A8 output schema |
| ai/src/elmanhg_ai/eval/math_step_grading.py | 126 | A9 eval runner/scorer |
| ai/src/elmanhg_ai/eval/datasets/math_step_grading.v1.jsonl | 26 | A10 dataset (20 graded + 6 safety cases) |
| ai/tests/unit/test_math_step_grade_schemas.py | 56 | ai tests 1–6 |
| ai/tests/unit/test_math_step_grading_output.py | 80 | ai tests 7–14 |
| ai/tests/unit/test_math_step_grading_pipeline.py | 242 | ai tests 15–25 |
| ai/tests/unit/test_math_step_grading_eval.py | 187 | ai tests 26–39 |
| ai/tests/integration/test_math_step_grades_endpoint.py | 112 | ai tests 40–44 |
| ai/tests/eval/test_eval_math_step_grading.py | 39 | ai test 45 (skips without key) |
| ai/tests/integration/test_cas_warm_lifespan.py | 40 | ai tests 53–54 |
| api/Elmanhg.Domain/MathStepGrading/MathStepGrade.cs | 83 | D-1 aggregate |
| api/Elmanhg.Domain/MathStepGrading/MathStepGrade.Grading.cs | 103 | D-2 RecordVerdict/Complete/FailAttempt/ToQuestionGrade/MarkApplied |
| api/Elmanhg.Domain/MathStepGrading/MathStepAssessment.cs | 3 | D-3 |
| api/Elmanhg.Domain/MathStepGrading/MathStepScore.cs | 3 | D-4 |
| api/Elmanhg.Domain/MathStepGrading/MathStepGradeStatus.cs | 3 | D-5 |
| api/Elmanhg.Domain/MathStepGrading/MathStepReviewReason.cs | 3 | D-6 |
| api/Elmanhg.Domain/MathStepGrading/IMathStepGradeRepository.cs | 8 | D-7 |
| api/Elmanhg.Domain/Questions/Grading/MathStepAward.cs | 3 | D-8 |
| api/Elmanhg.Application/Shared/AiService/{IAiMathStepGradingClient, AiMathStepGradingRequest, AiMathStepGradingResult, AiMathStepScore, IMathCheckRateLimiter}.cs | 3–6 each | P-1..P-5 |
| api/Elmanhg.Application/Shared/Options/MathStepGradingOptions.cs | 34 | P-6 |
| api/Elmanhg.Application/Questions/Shared/Grading/AnswerDecision.cs | 10 | P-7 |
| api/Elmanhg.Application/MathStepGrading/CheckMathStepAnswer/{Command,Handler}.cs | 5 / 35 | P-8, P-9 |
| api/Elmanhg.Application/MathStepGrading/GradeMathSteps/{Command,Handler}.cs | 5 / 53 | P-10, P-11 |
| api/Elmanhg.Application/MathStepGrading/ApplyMathStepGrade/{Command,Handler}.cs | 5 / 27 | P-12, P-13 |
| api/Elmanhg.Application/MathStepGrading/FailMathStepGrade/{Command,Handler}.cs | 5 / 23 | P-14, P-15 |
| api/Elmanhg.Application/MathStepGrading/GetDueMathStepGradeIds/{Query,Handler}.cs | 5 / 14 | P-16 |
| api/Elmanhg.Application/MathStepGrading/GetMathStepGrade/{Query,Validator,Handler}.cs | 6 / 14 / 23 | P-17..P-19 |
| api/Elmanhg.Application/MathStepGrading/Shared/MathStepGradingRequestFactory.cs | 30 | P-20 |
| api/Elmanhg.Application/MathStepGrading/Shared/MathStepAssessments.cs | 33 | P-21 |
| api/Elmanhg.Application/MathStepGrading/Shared/MathStepAttemptRecorder.cs | 28 | P-22 |
| api/Elmanhg.Application/MathStepGrading/Shared/{MathStepScoreResult, MathStepGradeResult, MathStepGradeDetailResult}.cs | 3 each | P-23..P-25 |
| api/Elmanhg.Application/MathStepGrading/Shared/MathStepGradeResultGenerator.cs | 35 | P-26 |
| api/Elmanhg.Application/Sessions/SubmitAnswer/QuizMathStepsSubmission.cs | 24 | P-27 |
| api/Elmanhg.Application/Sessions/SubmitAnswer/QuizAttemptRecorder.cs | 20 | P-28 |
| api/Elmanhg.Application/Exams/Shared/ExamDeferredGrading.cs | 46 | P-29 |
| api/Elmanhg.Application/Questions/GradeQuestionDraft/MathStepsDraftGrading.cs | 41 | P-30 |
| api/Elmanhg.Infrastructure/AiService/HttpAiMathStepGradingClient.cs | 76 | I-1 |
| api/Elmanhg.Infrastructure/AiService/AiMathStepGradingReplyRules.cs | 38 | I-2 |
| api/Elmanhg.Infrastructure/AiService/FakeAiMathStepGradingClient.cs | 31 | I-3 |
| api/Elmanhg.Infrastructure/AiService/MathCheckRateLimiter.cs | 26 | I-4 |
| api/Elmanhg.Infrastructure/MathStepGrading/MathStepGradeRepository.cs | 23 | I-5 |
| api/Elmanhg.Infrastructure/Data/Context/AppDbContext.MathStepGrading.cs | 44 | I-6 |
| api/Elmanhg.Infrastructure/Migrations/20260930190914_AddMathStepGrades.cs (+ .Designer.cs) | 125 / 3629 | I-7 (CreateTable + 6 indexes only; Down drops the table) |
| api/Elmanhg.Api/Workers/MathStepGradingWorker.cs | 103 | W-1 |
| api/Elmanhg.Tests/Builders/MathStepGradeBuilder.cs | 74 | T-1 |
| api/Elmanhg.Tests/Domain/MathStepGrading/MathStepGradeTests.cs | 248 | .NET 1–22 |
| api/Elmanhg.Tests/Domain/Questions/Grading/MathStepsGraderStepsTests.cs | 112 | .NET 23–35 |
| api/Elmanhg.Tests/Domain/Sessions/SessionReplayTests.cs | 92 | .NET 36–42 |
| api/Elmanhg.Tests/Application/Features/MathStepGrading/CheckMathStepAnswer/CheckMathStepAnswerHandlerTests.cs | 122 | .NET 61–66 |
| api/Elmanhg.Tests/Application/Features/MathStepGrading/GradeMathSteps/GradeMathStepsHandlerTests.cs | 205 | .NET 67–76 |
| api/Elmanhg.Tests/Application/Features/MathStepGrading/ApplyMathStepGrade/ApplyMathStepGradeHandlerTests.cs | 134 | .NET 77–82 |
| api/Elmanhg.Tests/Application/Features/MathStepGrading/FailMathStepGrade/FailMathStepGradeHandlerTests.cs | 69 | .NET 83–85 |
| api/Elmanhg.Tests/Application/Features/MathStepGrading/GetDueMathStepGradeIds/GetDueMathStepGradeIdsHandlerTests.cs | 28 | .NET 86 |
| api/Elmanhg.Tests/Application/Features/MathStepGrading/GetMathStepGrade/GetMathStepGradeHandlerTests.cs | 112 | .NET 87–92 |
| api/Elmanhg.Tests/Application/Features/MathStepGrading/GetMathStepGrade/GetMathStepGradeValidatorTests.cs | 28 | .NET 93 |
| api/Elmanhg.Tests/Application/Features/MathStepGrading/Shared/MathStepGradingRequestFactoryTests.cs | 50 | .NET 94–97 |
| api/Elmanhg.Tests/Application/Features/MathStepGrading/Shared/MathStepAssessmentsTests.cs | 35 | .NET 98–100 |
| api/Elmanhg.Tests/Application/Features/Questions/GradeQuestionDraft/GradeQuestionDraftMathStepsTests.cs | 89 | .NET 102–106 |
| api/Elmanhg.Tests/Infrastructure/AiService/HttpAiMathStepGradingClientTests.cs | 80 | .NET 117–120 |
| api/Elmanhg.Tests/Infrastructure/AiService/AiMathStepGradingReplyRulesTests.cs | 44 | .NET 121 |
| api/Elmanhg.Tests/Infrastructure/AiService/FakeAiMathStepGradingClientTests.cs | 40 | .NET 122–123 |
| api/Elmanhg.Tests/Infrastructure/AiService/MathCheckRateLimiterTests.cs | 38 | .NET 124–126 |
| api/Elmanhg.Tests/Api/Workers/MathStepGradingWorkerTests.cs | 139 | .NET 127–131 |
| api/Elmanhg.Tests/Integration/MathStepGrading/MathStepGradingTestData.cs | 95 | .NET 132 seeding helper |
| api/Elmanhg.Tests/Integration/MathStepGrading/MathStepGradeEndpointTests.cs | 81 | .NET 132–136 |
| api/Elmanhg.Tests/Integration/Sessions/MathStepGradingAnswerEndpointTests.cs | 71 | .NET 137–139 |
| api/Elmanhg.Tests/Integration/Exams/MathStepGradingExamEndpointTests.cs | 34 | .NET 140 |
| web/src/features/questions/components/MathSolutionField.tsx | 67 | F-1 |
| web/src/features/questions/components/MathStepScoreList.tsx | 42 | F-2 |
| web/src/features/questions/components/MathStepGradeDetails.tsx | 40 | F-3 |
| web/src/features/quiz/api/mathStepsItem.ts | 25 | F-4 |
| web/src/features/quiz/hooks/useMathStepGrade.ts | 22 | F-5 |
| web/src/features/quiz/components/MathStepGradeStatus.tsx | 79 | F-6 |
| web/src/features/quiz/components/MathStepGradeOutcome.tsx | 77 | F-7 |
| web/src/features/quiz/components/MathStepsReviewItem.tsx | 39 | F-8 |
| web/src/test/mathStepGradeFixtures.ts | 55 | F-9 |
| web/src/features/quiz/api/mathStepsItem.test.ts | 48 | web 1 |
| web/src/features/quiz/components/MathStepGradeStatus.test.tsx | 164 | web 2–11 |
| web/src/features/quiz/pages/QuizPage.mathStepGrading.test.tsx | 91 | web 12–14 |
| web/src/features/quiz/pages/QuizResultPage.mathStepGrading.test.tsx | 35 | web 15 |
| web/src/features/exam/pages/ExamResultPage.mathStepGrading.test.tsx | 45 | web 16–17 |
| web/src/shared/api/generated/model/{mathStepGradeResult, mathStepGradeDetailResult, mathStepScoreResult}.ts | 17–34 | Orval output |
| docs/math-step-grading.md | 150 | the new contract doc |

## Files modified
| Path | Change |
|---|---|
| ai/src/elmanhg_ai/settings.py | 9 `math_step_grading_*` settings, `cas_max_expansion_terms`, `cas_warm_on_start` |
| ai/src/elmanhg_ai/cas/models.py | `CasLimits.max_expansion_terms` |
| ai/src/elmanhg_ai/cas/nodes.py | `expansion_terms` / `check_expansion` (D14); `power()` checks it |
| ai/src/elmanhg_ai/cas/parser.py | each element side checked with `check_expansion` |
| ai/src/elmanhg_ai/cas/pool.py | catch-all `except Exception` → log `math_check.worker_failed`, recycle, `unchecked` (D15) |
| ai/src/elmanhg_ai/main.py | loads step prompts, warms the CAS pool when enabled, logs model/version, includes router |
| ai/src/elmanhg_ai/api/deps.py | `MathStepGradingPromptsDep` |
| ai/openapi/v1.json | regenerated |
| ai/tests/conftest.py | `cas_warm_on_start=False` |
| ai/tests/unit/test_cas_{parser,equivalence,lexer}.py | `max_expansion_terms=5000`; parser gets tests 47–50 |
| ai/tests/unit/test_cas_pool.py | test 46 |
| ai/tests/unit/test_cas_settings.py | defaults assert + `tiny-expansion` row (51) |
| ai/tests/unit/test_settings.py | test 52 |
| api/Elmanhg.Domain/Questions/Schemas/MathStepsSchemas.cs | `ModelSolution`, `StepsWeight` |
| api/Elmanhg.Domain/Questions/Grading/MathStepsGrader.cs | `MaxStepPoints`, `PercentScale`, `NeedsStepGrading`, `Combine` |
| api/Elmanhg.Domain/Questions/Grading/{QuestionGrader, GradeFeedbackKind, GradeFeedback}.cs, Questions/QuestionRevision.cs | `GradeMathStepsCombined`, `MathStepTally`, `GradeMathSteps(verdict, awards)` |
| api/Elmanhg.Domain/Sessions/Session.{Answering,Essays,ExamSubmission}.cs | `IsReplay`; `SubmitForAiGrading` / `RecordAiGradedAttempt` (essay methods delegate); `deferredQuestionIds` rename |
| api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs, api/Elmanhg.Application/Exceptions/ErrorCodes.cs | new codes |
| api/Elmanhg.Application/Questions/Shared/Grading/{AnswerGrader, GradeFeedbackKeys, GradeFeedbackText}.cs | `DecideAsync` (both overloads) + `CheckFinalAnswerAsync`; tally key/arm |
| api/Elmanhg.Application/Questions/Shared/{MathStepsQuestionRules, MathStepsAnswerRules}.cs | new rules + canonical omission; `HasFinalAnswer` |
| api/Elmanhg.Application/Shared/Options/ContentOptions.cs, DependencyInjection.cs | model-solution caps; `MathStepGradingOptions` registration |
| api/Elmanhg.Application/Sessions/SubmitAnswer/SubmitAnswerHandler.cs | new ctor, `AnswerAsync` (replay → limiter → decide → record or defer) |
| api/Elmanhg.Application/Exams/Shared/ExamSubmission.cs, SubmitExam/SubmitExamHandler.cs, AutoSubmitExam/AutoSubmitExamHandler.cs | deferred MathSteps at submit via `ExamDeferredGrading` |
| api/Elmanhg.Application/Exams/StartUnitExam/StartUnitExamHandler.cs, StartMultiUnitExam/StartMultiUnitExamHandler.cs | ctor + `ExamSubmission.SubmitAsync` argument (see Deviations) |
| api/Elmanhg.Application/Questions/GradeQuestionDraft/{GradeQuestionDraftHandler, QuestionGradeResult}.cs | MathSteps branch via `MathStepsDraftGrading`; `MathSteps` detail |
| api/Elmanhg.Infrastructure/AiService/{AiServiceOptions, AiServiceServiceCollectionExtensions}.cs, DependencyInjection.cs, Data/Context/AppDbContext.cs | timeout, typed client, fake, limiter singleton, repo, config + query filter |
| api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs | regenerated |
| api/Elmanhg.Api/Controllers/Sessions/SessionsController.cs, Program.cs | `GET …/math-step-grade`; hosted worker |
| api/Elmanhg.Api/Resources/Messages.{ar,en}.resx | 7 new keys; `GRADE_FEEDBACK_MATH_FINAL_ONLY` text (D21) |
| api/Elmanhg.Api/appsettings.example.json | Content caps, AiService timeout, `MathStepGrading` section |
| api/openapi/v1.json | regenerated by `dotnet build` |
| api/Elmanhg.Tests/Builders/QuestionBuilder.cs | `MathStepsGradedSpecJson`, `MathStepsGraded()`, `MathStepsGradedFields()`, `MathStepsContent(spec)` default param |
| api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs | `MathStepGrading:SweepEnabled=false`, `CheckPermitLimit=1000` |
| api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs | `_AddMathStepGrades` |
| api/Elmanhg.Tests/Application/Features/Questions/Shared/Grading/AnswerGraderTests.cs | renamed to `DecideAsync_*` + tests 48–50 |
| api/Elmanhg.Tests/Application/Features/Sessions/SubmitAnswer/{SubmitAnswerHandlerTests, SubmitAnswerFreeTierTests}.cs | ctor; replaced unchecked test; tests 51–56 |
| api/Elmanhg.Tests/Application/Features/Exams/SubmitExam/SubmitExamHandlerTests.cs, AutoSubmitExam/AutoSubmitExamHandlerTests.cs | ctor; replaced tests; test 59 |
| api/Elmanhg.Tests/Application/Features/Exams/StartUnitExam/*, StartMultiUnitExam/* (4 files) | ctor only (see Deviations) |
| api/Elmanhg.Tests/Application/Features/Questions/GradeQuestionDraft/GradeQuestionDraftHandlerTests.cs | ctor only |
| api/Elmanhg.Tests/Application/Features/Questions/Shared/{MathStepsQuestionRulesTests, MathGradeFeedbackTextTests}.cs | tests 107–116 |
| api/Elmanhg.Tests/Integration/Content/MathStepsQuestionEndpointTests.cs | tests 141–142; D21 expected text |
| web/src/features/questions/** (schemas ×3, api ×4, components ×5, index.ts, i18n ×2) | model solution + steps weight editor, rules, read/write, error mapping, validation view, preview details |
| web/src/features/questions/{schemas/mathStepsRules.test.ts, api/mathStepsValues.test.ts, pages/NewMathStepsQuestion.test.tsx, pages/ValidationMathStepsQuestion.test.tsx} | web tests 18–23 |
| web/src/features/quiz/{hooks/useQuizAnswer.ts, components/QuizQuestionCard.tsx, components/ProvisionalScoreNotes.tsx, pages/QuizResultPage.tsx, index.ts, i18n ×2} | pending/applied math UI, result review, note |
| web/src/features/exam/components/{ExamReviewItem, ExamResultSummary}.tsx | pending math review item, answered count, provisional badge |
| web/src/shared/i18n/{ar,en}.json | 5 error codes |
| web/src/shared/api/generated/** | `npm run gen:api` |
| postman/elmanhg.postman_collection.json | Create math question body, Grade math draft body + `mathSteps` test, new "Get math step grade" |
| deploy/ai.env.example, deploy/api.env.example, .env.example | commented keys; stale «unchecked» comments fixed |
| docs/PRD.md, math-cas.md, question-schemas.md, sessions.md, exams.md, ai-service.md, training-data.md, claude-design-prompt.md, prototype.md, deployment.md, backlog.json | docs-sync per the plan table |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Only `SubmitExamHandler` and `AutoSubmitExamHandler` call `ExamSubmission.SubmitAsync` | `StartUnitExamHandler` and `StartMultiUnitExamHandler` also call it (they submit an expired open exam) | Added `IMathStepGradeRepository mathStepGradeRepository` right after `IEssayGradeRepository` to both ctors and passed it through; updated the ctor call in their 4 test files (ctor only, no assertions changed) |
| `MathStepsFields` TextField for `mathStepsWeight` with `inputMode="numeric"` | Shared `TextField` has no `inputMode` prop and is not in the touched list | Rendered it without `inputMode` (same as `mathTolerance`) |
| `QuizResultPage` test: implied "answered" count includes pending math | `QuizResultSummary` (not in the plan's list) counts only attempts and written essays | Left `QuizResultSummary` unchanged; the test asserts only what the plan lists. The exam summary (in the list) does count pending math |
| Test #141 expects `201` | `POST /api/questions` returns `200` in this repo | Asserted 200, as the sibling test does |
| Test #134 implied a problem+json content type | The API's 404 body is `application/json` (the essay sibling doesn't assert it either) | Asserted status + `code` only |
| Existing test `Post_GradeDraftMathSteps_ReturnsCorrectWithFeedback` | It asserts the old D21 text | Updated its expected string to «صُحّحت الإجابة النهائية فقط.» (intentional behaviour change D21, §8.11-compliant) |
| Plan's mutation list: removing the Unchecked defer branch fails #51 **and #78** | #78 builds the grade through the domain directly, so it does not depend on `AnswerGrader` | Mutation fails #51, #48 (DecideAsync) and #58; #78 is independent by construction (see Build & test) |
| `test_settings_cas_defaults` in `test_settings.py` | It lives in `test_cas_settings.py` | Edited it there |

## Build & test
- **ai** (`ai/`, `python -m uv …`): `uv sync --locked` → "Checked 56 packages"; `ruff format --check .` → "136 files already formatted"; `ruff check .` → "All checks passed!"; `mypy src` → "Success: no issues found in 74 source files"; `pytest -m "not eval"` → **448 passed, 4 deselected**; `pytest -m eval tests/eval/test_eval_math_step_grading.py` → 1 skipped (no key, as designed). OpenAPI re-export is stable.
- **api** (CI parity: `api/Elmanhg.Api/appsettings.json` absent in this worktree, nothing to move): `dotnet test api/ -c Release` → **Passed! total 4365, failed 0**. `dotnet format api/Elmanhg.slnx --verify-no-changes --exclude core-libraries` → only `Elmanhg.Tests/Builders/SubscriptionBuilder.cs` (untouched, pre-existing CRLF whitespace noise).
- **web**: `npm run typecheck` exit 0; `npm run lint` exit 0; `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` → "All matched files use Prettier code style!"; `npx vitest run` → **247 files, 1385 tests passed**; `npm run build` exit 0; `npm run perf:budget` → all ok (quiz 249/255 KB).
- **Migration**: `dotnet ef migrations add AddMathStepGrades` (design-time with the example settings and a dummy connection string, file removed after) → CreateTable + 6 CreateIndex; Down = DropTable.
- **Mutation checks** (break, run, restore):
  - ai: catch-all `except Exception` → `except ZeroDivisionError`: test 46 **failed**. Parse-level + power-level `check_expansion` removed: tests 47/48 **failed**; test 50 (evaluate) hangs in SymPy (the blow-up itself), killed.
  - .NET: `AnswerGrader` unchecked branch changed to grade at once: `Handle_MathCheckUnchecked_DefersToMathStepGradeWithoutAttempt`, `DecideAsync_UncheckedVerdict_DefersWithoutVerdict`, `Handle_SavedMathStepsAnswerUnchecked_RequestsMathStepGradeWithoutAttempt` **failed**. `IsReplay` guard removed from `SubmitAnswerHandler`: `Handle_StepGradedReplay_CallsNeitherCheckNorLimiterAgain` **failed**. All files restored (diff checked).

- **Final .NET re-run** after the using-sort and mutation restores: `dotnet test api/ -c Release` → **Passed! total 4365, failed 0, skipped 0** (1m 06s).

## Notes for review
- `SubmitAnswerHandler.cs` is 119 lines (was ~111): the plan puts `AnswerAsync` in the handler. `MathStepGrade.Grading.cs` and `MathStepGradingWorker.cs` are 103 lines (the worker mirrors the 100-line essay worker).
- `GradeMathStepsHandler` computes the final-only grade first and overwrites it when step grading runs; semantics match the plan, the extra `Combine` call is cheap.
- `MathStepGrade.RecordVerdict` throws one `InvalidOperationException` for both `Unchecked` and already-checked (plan tests 6/7 both expect it).
- `ai/src/elmanhg_ai/api/health.py` readiness still checks only chat/essay prompts (not in the touched list); `math_step_grading_prompts` is loaded in the same lifespan step, so it cannot be missing when ready.
- CI "Container becomes ready" now waits for the CAS warm-up (default on) before `/health/ready`; locally the warm lifespan test passes, but the Docker image smoke step was not run here.
- Existing web tests that render a MathSteps attempt now issue `GET …/math-step-grade` without a handler (MSW `onUnhandledRequest: 'error'`); they still pass (the status renders an error box they don't assert on). A default 404 handler in `src/test/msw/server.ts` would silence it but that file is not in the plan.
- The model solution becomes part of the grading spec, which is revealed after answering like every type (`correctAnswer`); pending answers still hide it (D23).
- Follow-ups for the orchestrator: grader-calibration table for math step grades (plan Out), `QuizResultSummary` counting pending math as answered, the live eval score at go-live.
