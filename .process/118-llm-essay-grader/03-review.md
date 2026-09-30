VERDICT: CHANGES_REQUESTED

# Review — [E14.S2] LLM essay grader (#118)

## Blocking

### 1. Four new `throw`/`raise` paths have no test, so mutating them leaves the suite green
**Where:**
- `api/Elmanhg.Application/EssayGrading/GradeEssay/GradeEssayHandler.cs:29`: `?? throw new NotFoundCoreException(ErrorCodes.QuestionNotFound)` when the revision exists but the question row does not, for example a soft-deleted question whose revisions are still returned. Test 43 covers only the missing *revision* at line 28.
- `ai/src/elmanhg_ai/prompts/loader.py:34-35`: `raise TypeError(...)` when the output schema file is not a JSON object. `test_prompt_loader.py` covers only the known-version and unknown-version cases (tests 137 and 138).
- `ai/src/elmanhg_ai/eval/essay_grading.py:34-37`: both `EssayEvalCase` validator raises (reference keys differ from the criterion ids; reference points out of range). Test 146 checks only that the shipped dataset is valid. No test feeds it a bad case.
- `api/Elmanhg.Application/EssayGrading/Shared/EssayGradingRequestFactory.cs:12`: `?? throw new InvalidOperationException("Essay grading spec is not readable.")` for a spec that deserialises to null (`"null"`).

**Rule:** reviewer order #6 ("every `throw` in the new code needs a test"); dotnet-testing (every throwing path asserts the exception type, the code and `SaveChangesAsync` `DidNotReceive()`); python-testing (every error branch).
**Problem:** Each guard can be deleted or inverted without any test failing. The eval-case validator protects the D25 agreement metric. The loader guard protects the schema passed to Anthropic `output_config`.
**Failure:**
- Delete line 29 of `GradeEssayHandler`: all 3589 api tests still pass, and a soft-deleted question produces a `NullReferenceException` on `question.LessonId` instead of `QUESTION_NOT_FOUND`.
- Delete lines 34-37 of `eval/essay_grading.py`: a dataset line with a mistyped criterion id, or a reference above the criterion maximum, loads silently and skews `mean_total_error`. No test fails.
- Delete lines 34-35 of `loader.py`: a `[]` schema file loads without error. No test fails.

**Fix:**
- Add `Handle_QuestionMissing_ThrowsQuestionNotFound`: revisions stubbed, question repository returns null. Assert the code and that save and the client `DidNotReceive()`.
- Add `test_load_output_schema_non_object_raises_type_error`: monkeypatch or a temp resource.
- Add two parametrised `EssayEvalCase.model_validate` cases (a wrong key, and a value above the maximum) that expect `ValidationError`.
- Add `Create_NullSpec_ThrowsInvalidOperationException`.

## Non-blocking
- `api/Elmanhg.Application/EssayGrading/Shared/EssayGradingRequestFactory.cs:16-19` together with `ai/src/elmanhg_ai/api/essay_grades/schemas.py:341`: `ExtractBlocks` drops a model answer whose only content is an image with an empty alt. If every model answer is dropped, the ai service returns 400, the draft grade shows 503 ("try again in a moment"), and a student essay uses up all 4 attempts and ends in `InReview/GradingFailed`. The web editor requires alt text (`imageInsertSchema.description` min 1), so only a direct API caller can reach this. Consider making `EssayQuestionRules` require extractable text, or letting the ai accept an empty `modelAnswers`.
- `api/Elmanhg.Infrastructure/AiService/HttpAiEssayGradingClient.cs:157-161`: an ai 400 (a deterministic payload rejection) maps to the same retryable `ESSAY_GRADING_UNAVAILABLE` as a transient 5xx. The worker retries a payload that can never succeed. Consider recording a distinct `LastErrorCode` for 4xx.
- `ai/src/elmanhg_ai/pipelines/essay_grading.py:160`: `estimate_cost_usd` uses the global chat token prices. If `ELMANHG_AI_ESSAY_GRADING_MODEL` differs in price from the chat model, `cost_usd` is wrong. Consider per-model pricing later.
- Postman "Get essay grade" uses `{{sessionQuestionId}}`, not the plan `{{questionId}}`. This is correct (it is the variable set by "Start quiz") and it is disclosed in the notes, but it is missing from the Deviations table.

## Verified
- **Tests re-run with CI parity.** `appsettings.json` was moved aside and then restored; I confirmed the restore.
  - `dotnet test api/ -c Release`: 3589 passed, 0 failed.
  - ai `pytest -m "not eval"`: 268 passed. `-m eval`: 3 skipped (the essay eval skips without keys).
  - ai `ruff check`, `ruff format --check` and `mypy src` are clean.
  - web `vitest run`: 193 files / 1094 tests passed. `typecheck` and `lint` are clean.
- **Drift.** I regenerated Orval from `api/openapi/v1.json` into a scratch copy: byte-identical to `web/src/shared/api/generated`. The ai OpenAPI drift test passes.
- **Postman.** It has "Grade essay draft" (POST, Questions folder, `lessonId` plus `answer.text`, asserts `essay.criteria`) and "Get essay grade" (GET, last in Sessions, accepts 200 or 404). There is no stale `QUESTION_TYPE_NOT_GRADABLE` usage.
- **Prompt-injection resistance.**
  - The system prompt matches the plan verbatim (checked programmatically) and contains no untrusted text.
  - The context JSON sits in `<grading_context>` and the essay in `<student_essay>`, in a single user turn.
  - `strip_tags` repeats until stable and runs on every string field (`strip_fields`) and on the essay.
  - `render` substitutes in one regex pass, so a placeholder inside the essay is not expanded.
  - The output schema matches the plan verbatim. `parse_model_grade` rejects schema errors, duplicate, missing or unknown ids, and out-of-range points and confidence.
  - The ai sums the totals itself. .NET re-validates the reply (`AiEssayGradingReplyRules`) and recomputes the score via `QuestionGrader.GradeEssay` against the served revision (test 39 proves v1 is used after an edit to v2).
  - Injection means confidence <= 0.3, which is below 0.7, so the grade goes to `InReview`.
  - The justification renders as React text, not HTML.
- **No secret or essay logging.** `essay_grading.completed` and `essay_grading.output_invalid` log only metadata. The .NET client logs only status and exception, without the body or the token. The Anthropic key is not touched.
- **Provider selection.** The Fake is the default and throws `ESSAY_GRADING_UNAVAILABLE` in Production. The Http adapter is selected by `AiService:Provider`. The typed client uses a 100 s attempt and total timeout, `DisableForUnsafeHttpMethods` (no POST retry), and `client.Timeout` infinite. The ai side uses 45 s per call with 1 SDK retry.
- **Failure handling.**
  - The worker catches per item, logs a Warning and sends `FailEssayGradeCommand(code)`.
  - Backoff is 30/60/120 s; after 4 attempts the grade is `InReview/GradingFailed`.
  - The student sees the under-review state ("Under review") with no score. `EssayGradeResultGenerator` hides everything unless `Graded`.
  - The draft grade returns 503 with no retry.
- **Idempotency and concurrency.** `IsDueAt` gates the handler. There is an xmin row version. `FailEssayGrade` does nothing when the grade is not Pending. `PeriodicTimer` runs sweeps sequentially.
- **Migration.** `20260930051854_AddEssayGrades` has only CreateTable, 4 FKs (Restrict) and 5 indexes, including the unique `(SessionId, QuestionId)` and the partial `NextAttemptAt` index filtered to Pending. The Down migration drops the table. The migration is appended to `AppDbContextTests`, and the pending-migrations test passes.
- **#117 fold-ins.**
  - RC2: `RichTextContent.HasContent` in `Validate` plus the post-sanitise check in `Normalize` (tests 23-25, 90, 91).
  - RC3/RC4: `preview.essayNotGradable` and `QUESTION_TYPE_NOT_GRADABLE` are removed from code, resx and web. The new en/ar hint describes AI grading.
- **Contract fidelity.**
  - Every file in *Files to create* exists, with no extras.
  - The signatures match D1-D9, A1-A24, I1-I4, W1, P1-P13 and F1-F7.
  - Every test-plan name exists. The only absent names are the plan deliberate deletions (29, 87, 165).
  - The disclosed deviations were checked and are correct: TypeError for ruff TRY004, `thirtyThird`, `Field(default_factory=list)`, and the env-file split.
- **Skill compliance.**
  - The guard grep finds nothing in production code.
  - Namespaces are file-scoped. Handlers and records are sealed. `ConfigureAwait(false)` is used throughout and `DateTimeOffset` everywhere.
  - Options are `ValidateOnStart`. The soft-delete filter line was added. The Tag regex is NonBacktracking.
  - The only comments are the plan-mandated WHY comments.
  - `dotnet format --verify-no-changes` on the new files is clean.
- **Web.**
  - Only design tokens are used (`px-3.5 py-3` and `size-6.5` mirror `FeedbackPanel`).
  - The en and ar keys are complete.
  - `useEssayGrade` polls every 2 s only while the status is `Pending`.
- **Docs.** They agree with the code: PRD §6.1 and §18 (polling replaces SignalR for grades), `question-schemas`, `sessions`, `claude-design-prompt` §4, `prototype`, `ai-service`, `observability`, `deployment` and the new `essay-grading.md`. No divergence found.

## Test quality
- `GradeEssayHandlerTests`: strong. It holds a real `QuestionBuilder` aggregate and edits it to v2, so it genuinely proves grading uses the served revision. Save is `Received`/`DidNotReceive` on each path. Test 46 serialises the captured request. The gap is the question-missing throw (Blocking #1).
- `EssayGradeTests`, `EssayGraderTests`, `FailEssayGradeHandlerTests` and `GetEssayGradeHandlerTests` constrain the implementation: exact backoff times, the threshold boundary (0.7 gives Graded) and hidden scores.
- `HttpAiEssayGradingClientTests`: theory rows cover each reply-rule branch, so they would fail on a loosened rule.
- `EssayGradingWorkerTests`: assert the error-code mapping and that the loop continues after a failure.
- ai `test_essay_grading_pipeline.py` and `test_delimiters.py`: assert on the exact rendered turn (the tag count equals the template count), per-field stripping, and the byte-equal chat regex. These would catch a regression in the injection hygiene.
- `test_essay_grading_eval.py`: scoring and threshold are well covered. The validator error paths are not (Blocking #1).
- `EssayGradeStatus.test.tsx` and `NewEssayQuestion.test.tsx`: assert the visible text, the polling transition, the retry and the request body (`lessonId`, `answer.text`). No test only asserts a mock return value.
