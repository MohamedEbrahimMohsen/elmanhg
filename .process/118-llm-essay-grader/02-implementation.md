# Implementation — [E14.S2] LLM essay grader (#118)

Branch `feature/118-llm-essay-grader` (origin/main already merged). Nothing committed.

## Files created

### ai/
| Path | Lines | Purpose |
|---|---|---|
| `ai/src/elmanhg_ai/prompts/delimiters.py` | 30 | P1: `delimiter_pattern`, `strip_tags` (repeat until stable), `strip_fields` (JsonValue walk); shared by chat and essay grading |
| `ai/src/elmanhg_ai/api/essay_grades/__init__.py` | 0 | P2 |
| `ai/src/elmanhg_ai/api/essay_grades/schemas.py` | 62 | P3: `RubricLevelIn`, `RubricCriterionIn`, `EssayGradeIn`, `CriterionGradeOut`, `EssayGradeOut` |
| `ai/src/elmanhg_ai/pipelines/essay_grading_output.py` | 83 | P4: `parse_model_grade` (schema → criteria → points; rubric order), `_reject` logs reason only |
| `ai/src/elmanhg_ai/pipelines/essay_grading.py` | 160 | P5: prompts loader, `_limit_errors`, `run` (strip per field, structured output, model/timeout override, `essay_grading.completed`) |
| `ai/src/elmanhg_ai/api/essay_grades/router.py` | 50 | P6: `POST /v1/essay-grades`, operationId `grading_create_essay_grade` |
| `ai/src/elmanhg_ai/prompts/essay_grade_system.v1.md` | 13 | P7: verbatim prompt v1 |
| `ai/src/elmanhg_ai/prompts/essay_grade_turn.v1.md` | 7 | P8 |
| `ai/src/elmanhg_ai/prompts/essay_grade_output.v1.json` | 22 | P9: verbatim output schema v1 |
| `ai/src/elmanhg_ai/eval/essay_grading.py` | 128 | P10: `EssayEvalCase`, `CaseScore`, `EssayEvalReport` (D25 threshold), `load_cases`, `score_case`, `run` |
| `ai/src/elmanhg_ai/eval/datasets/essay_grading.v1.jsonl` | 26 | P11: 26 cases, 6 subjects, 6 `safety` (all six required kinds), full/partial/zero/off-topic/short/dialect/english |
| `ai/tests/fixtures/anthropic/message_structured_grade.json` | 1 | P12 |
| `ai/tests/unit/test_delimiters.py` | 41 | tests 93–97 |
| `ai/tests/unit/test_essay_grade_schemas.py` | 83 | tests 98–105 |
| `ai/tests/unit/test_essay_grading_output.py` | 117 | tests 106–114 |
| `ai/tests/unit/test_essay_grading_pipeline.py` | 235 | tests 115–127 |
| `ai/tests/unit/test_essay_grading_eval.py` | 176 | tests 144–156 |
| `ai/tests/integration/test_essay_grades_endpoint.py` | 109 | tests 157–161 |
| `ai/tests/eval/test_eval_essay_grading.py` | 39 | test 164 (skips without Anthropic settings) |

### api/
| Path | Lines | Purpose |
|---|---|---|
| `api/Elmanhg.Domain/Questions/Grading/EssayCriterionAward.cs` | 3 | D1 |
| `api/Elmanhg.Domain/Questions/Grading/EssayGrader.cs` | 27 | D2: #117 formula, throws on rubric mismatch |
| `api/Elmanhg.Domain/EssayGrading/EssayGradeStatus.cs` | 3 | D3 |
| `api/Elmanhg.Domain/EssayGrading/EssayReviewReason.cs` | 3 | D4 |
| `api/Elmanhg.Domain/EssayGrading/EssayCriterionScore.cs` | 3 | D5 |
| `api/Elmanhg.Domain/EssayGrading/EssayAssessment.cs` | 3 | D6 |
| `api/Elmanhg.Domain/EssayGrading/EssayGrade.cs` | 73 | D7: aggregate, `Request`, `IsDueAt`, `ReadAnswerText`, `ReadCriteria` |
| `api/Elmanhg.Domain/EssayGrading/EssayGrade.Grading.cs` | 61 | D8: `Complete`, `FailAttempt`, `EnsurePending` |
| `api/Elmanhg.Domain/EssayGrading/IEssayGradeRepository.cs` | 8 | D9 |
| `api/Elmanhg.Application/Shared/AiService/IAiEssayGradingClient.cs` + `AiEssayGradingRequest`, `AiRubricCriterion`, `AiRubricLevel`, `AiEssayGradingResult`, `AiEssayCriterionScore` | 3–6 each | A1–A6 |
| `api/Elmanhg.Application/Shared/Options/EssayGradingOptions.cs` | 28 | A7 |
| `api/Elmanhg.Application/Questions/Shared/RichTextContent.cs` | 28 | A8 (RC2) |
| `api/Elmanhg.Application/EssayGrading/Shared/EssayCriterionResult.cs`, `EssayGradeResult.cs`, `EssayGradeDetailResult.cs`, `EssayGradingContext.cs` | 3 each | A9–A11, A13 |
| `api/Elmanhg.Application/EssayGrading/Shared/EssayGradeResultGenerator.cs` | 31 | A12: hides the AI score unless `Graded` |
| `api/Elmanhg.Application/EssayGrading/Shared/EssayGradingContextLoader.cs` | 36 | A14 |
| `api/Elmanhg.Application/EssayGrading/Shared/EssayGradingRequestFactory.cs` | 38 | A15 |
| `api/Elmanhg.Application/EssayGrading/Shared/EssayAssessments.cs` | 34 | A16 |
| `api/Elmanhg.Application/EssayGrading/GradeEssay/GradeEssayCommand.cs`, `GradeEssayHandler.cs` | 5 / 39 | A17–A18 |
| `api/Elmanhg.Application/EssayGrading/FailEssayGrade/FailEssayGradeCommand.cs`, `FailEssayGradeHandler.cs` | 5 / 24 | A19 |
| `api/Elmanhg.Application/EssayGrading/GetDueEssayGradeIds/GetDueEssayGradeIdsQuery.cs`, `…Handler.cs` | 5 / 15 | A21 |
| `api/Elmanhg.Application/EssayGrading/GetEssayGrade/GetEssayGradeQuery.cs`, `GetEssayGradeValidator.cs`, `GetEssayGradeHandler.cs` | 6 / 14 / 24 | A22–A24 |
| `api/Elmanhg.Infrastructure/EssayGrading/EssayGradeRepository.cs` | 22 | I1 |
| `api/Elmanhg.Infrastructure/AiService/FakeAiEssayGradingClient.cs` | 30 | I2 |
| `api/Elmanhg.Infrastructure/AiService/AiEssayGradingReplyRules.cs` | 40 | I3 |
| `api/Elmanhg.Infrastructure/AiService/HttpAiEssayGradingClient.cs` | 77 | I4 |
| `api/Elmanhg.Infrastructure/Migrations/20260930051854_AddEssayGrades.cs` (+ `.Designer.cs`) | generated | CreateTable + 5 indexes only (reviewed) |
| `api/Elmanhg.Api/Workers/EssayGradingWorker.cs` | 98 | W1 |
| `api/Elmanhg.Tests/Builders/EssayGradeBuilder.cs` | 49 | T1 |
| `api/Elmanhg.Tests/Integration/EssayGrading/EssayGradingTestData.cs` | 52 | T2 |
| `api/Elmanhg.Tests/Domain/Questions/Grading/EssayGraderTests.cs` | 64 | tests 1–6 |
| `api/Elmanhg.Tests/Domain/Questions/QuestionRevisionEssayTests.cs` | 18 | test 8 |
| `api/Elmanhg.Tests/Domain/EssayGrading/EssayGradeTests.cs` | 142 | tests 9–19 |
| `api/Elmanhg.Tests/Application/Features/Questions/Shared/RichTextContentTests.cs` | 32 | tests 20–22 |
| `api/Elmanhg.Tests/Application/Features/EssayGrading/GradeEssay/GradeEssayHandlerTests.cs` | ~160 | tests 39–46 |
| `api/Elmanhg.Tests/Application/Features/EssayGrading/FailEssayGrade/FailEssayGradeHandlerTests.cs` | 79 | tests 47–50 |
| `api/Elmanhg.Tests/Application/Features/EssayGrading/GetDueEssayGradeIds/GetDueEssayGradeIdsHandlerTests.cs` | 28 | test 51 |
| `api/Elmanhg.Tests/Application/Features/EssayGrading/GetEssayGrade/GetEssayGradeHandlerTests.cs` | 99 | tests 52–56 |
| `api/Elmanhg.Tests/Application/Features/EssayGrading/GetEssayGrade/GetEssayGradeValidatorTests.cs` | 29 | tests 57–59 |
| `api/Elmanhg.Tests/Application/Features/EssayGrading/Shared/EssayGradingRequestFactoryTests.cs` | 55 | tests 60–63 |
| `api/Elmanhg.Tests/Application/Features/EssayGrading/Shared/EssayAssessmentsTests.cs` | 35 | tests 64–65 |
| `api/Elmanhg.Tests/Application/Features/EssayGrading/Shared/EssayGradingContextLoaderTests.cs` | 54 | tests 66–67 |
| `api/Elmanhg.Tests/Infrastructure/AiService/FakeAiEssayGradingClientTests.cs` | 42 | tests 68–69 |
| `api/Elmanhg.Tests/Infrastructure/AiService/HttpAiEssayGradingClientTests.cs` | 85 | tests 70–73 |
| `api/Elmanhg.Tests/Api/Workers/EssayGradingWorkerTests.cs` | 143 | tests 76–81 |
| `api/Elmanhg.Tests/Integration/EssayGrading/EssayGradeEndpointTests.cs` | 88 | tests 82–86 |

### web/ and docs
| Path | Lines | Purpose |
|---|---|---|
| `web/src/features/questions/components/EssayCriteriaList.tsx` | 34 | F1 |
| `web/src/features/questions/components/EssayGradeDetails.tsx` | 37 | F2 |
| `web/src/features/quiz/hooks/useEssayGrade.ts` | 11 | F3: polls every 2 s while `Pending` |
| `web/src/features/quiz/components/EssayGradeStatus.tsx` | 73 | F4 |
| `web/src/features/quiz/components/EssayGradeOutcome.tsx` | 70 | F5 |
| `web/src/test/essayGradeFixtures.ts` | 62 | F6 |
| `web/src/features/quiz/components/EssayGradeStatus.test.tsx` | ~135 | F7, tests 169–176 |
| `web/src/shared/api/generated/model/essay*.ts` | generated | Orval |
| `docs/essay-grading.md` | 110 | G1 |

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.Domain/Questions/Schemas/EssaySchemas.cs` | `EssayAnswer(string? Text)` |
| `api/Elmanhg.Domain/Questions/Grading/QuestionGrader.cs` | `GradeEssay(...)` |
| `api/Elmanhg.Domain/Questions/QuestionRevision.cs` | `GradeEssay(awards)` against the snapshot |
| `api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs` | `EssayGradeNotPending` |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | removed `QuestionTypeNotGradable`; added `QuestionEssayAnswerTooLong`, `EssayGradeNotFound`, `EssayGradingUnavailable` |
| `api/Elmanhg.Application/Questions/Shared/EssayQuestionRules.cs` | RC2: `RichTextContent.HasContent` in `Validate`; post-sanitise check in `Normalize` (422) |
| `api/Elmanhg.Application/Questions/Shared/QuestionAnswerRules.cs` | Essay arms in `CanRead` / `Canonicalize` |
| `api/Elmanhg.Application/Questions/GradeQuestionDraft/*` | query gains `LessonId`; validator drops the not-gradable rule, adds essay length rule; handler rewritten (A20); result gains `Essay` |
| `api/Elmanhg.Application/Shared/Options/ContentOptions.cs` | `QuestionEssayAnswerMaxLength` = 20000 |
| `api/Elmanhg.Application/DependencyInjection.cs` | `EssayGradingOptions` validated on start |
| `api/Elmanhg.Infrastructure/AiService/AiServiceOptions.cs` | `EssayGradingTimeoutSeconds` = 100 |
| `api/Elmanhg.Infrastructure/AiService/AiServiceServiceCollectionExtensions.cs` | typed `HttpAiEssayGradingClient` (own 100 s budget, no POST retry), fake, provider switch |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | `IEssayGradeRepository` |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | DbSet, index-name const, `ConfigureEssayGrades` (verbatim), soft-delete filter line |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | generated |
| `api/Elmanhg.Api/Program.cs` | `AddHostedService<EssayGradingWorker>()` |
| `api/Elmanhg.Api/Controllers/Sessions/SessionsController.cs` | `GET {sessionId}/questions/{questionId}/essay-grade` (`AssessmentsTake`) |
| `api/Elmanhg.Api/Controllers/Questions/Requests.cs`, `QuestionsController.cs` | `LessonId` passed through |
| `api/Elmanhg.Api/Resources/Messages.{ar,en}.resx` | removed `QUESTION_TYPE_NOT_GRADABLE`; added the 4 codes |
| `api/Elmanhg.Api/appsettings.example.json` | `Content.QuestionEssayAnswerMaxLength`, `AiService.EssayGradingTimeoutSeconds`, `EssayGrading` section |
| `api/openapi/v1.json` | regenerated by build |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | `EssayGrading:SweepEnabled=false` (UseSetting), `Content:QuestionEssayAnswerMaxLength` |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | `_AddEssayGrades` appended |
| `api/Elmanhg.Tests/…/QuestionGraderTests.cs` | + test 7 |
| `api/Elmanhg.Tests/…/EssayQuestionRulesTests.cs` | + tests 23–25 |
| `api/Elmanhg.Tests/…/QuestionAnswerRulesTests.cs` | + tests 26–28 |
| `api/Elmanhg.Tests/…/GradeQuestionDraftValidatorTests.cs` | deleted test 29; + tests 30–32 |
| `api/Elmanhg.Tests/…/GradeQuestionDraftHandlerTests.cs` | constructor (33); + tests 34–38 |
| `api/Elmanhg.Tests/…/AiServiceServiceCollectionExtensionsTests.cs` | + tests 74–75 |
| `api/Elmanhg.Tests/Integration/Content/EssayQuestionEndpointTests.cs` | deleted test 87; + tests 88–91 |
| `ai/src/elmanhg_ai/settings.py` | 9 `essay_grading_*` settings |
| `ai/src/elmanhg_ai/clients/model.py` | `ModelRequest.model`, `timeout_seconds`, `output_schema` |
| `ai/src/elmanhg_ai/clients/anthropic_model.py` | model override, per-request timeout, `output_config` json_schema |
| `ai/src/elmanhg_ai/clients/metered.py` | span/metrics labelled with `request.model` |
| `ai/src/elmanhg_ai/prompts/loader.py` | `load_output_schema` |
| `ai/src/elmanhg_ai/pipelines/chat.py` | uses `prompts/delimiters.py`; behaviour unchanged, all chat tests untouched and green |
| `ai/src/elmanhg_ai/api/deps.py`, `api/health.py`, `main.py` | prompts dependency, readiness, lifespan, router, `service.started` fields |
| `ai/src/elmanhg_ai/eval/scorers.py` | `score_total_error`, `score_within` |
| `ai/openapi/v1.json` | regenerated |
| `ai/tests/conftest.py` | `essay_payload` fixture |
| `ai/tests/unit/test_anthropic_model.py`, `test_metered_clients.py`, `test_settings.py`, `test_prompt_loader.py`, `test_eval_scorers.py`, `tests/integration/test_openapi_document.py`, `test_health_endpoints.py` | + tests 128–143, 162–163 |
| `web/src/features/questions/components/QuestionPreviewPanel.tsx` | `lessonId` prop; button for every type; essay hint + pending label; `EssayGradeDetails` |
| `web/src/features/questions/components/QuestionEditorForm.tsx` | passes `lesson.id` |
| `web/src/features/questions/hooks/useTestGrade.ts` | `useTestGrade(lessonId)`, sends `lessonId` |
| `web/src/features/questions/index.ts`, `web/src/features/quiz/index.ts` | export `EssayCriteriaList`, `EssayGradeStatus` |
| `web/src/features/questions/i18n/{en,ar}.json`, `quiz/i18n/{en,ar}.json`, `shared/i18n/{en,ar}.json` | keys per plan; `preview.essayNotGradable` and `QUESTION_TYPE_NOT_GRADABLE` removed |
| `web/src/features/questions/pages/NewEssayQuestion.test.tsx` | deleted test 165; + tests 166–168 |
| `web/src/shared/api/generated/**` | `npm run gen:api` |
| `postman/elmanhg.postman_collection.json` | "Grade essay draft" (Questions, after "Create essay question"), "Get essay grade" (last in Sessions) |
| `.env.example`, `deploy/ai.env.example`, `deploy/api.env.example` | commented essay-grading lines |
| `docs/ai-service.md`, `question-schemas.md`, `PRD.md` (§6.1, §18), `sessions.md`, `claude-design-prompt.md` §4, `prototype.md`, `observability.md`, `deployment.md` | G2–G9 |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| `AppDbContextTests`: append `thirtyFirst => … "_AddEssayGrades"` | After the origin/main merge the list already ends at `thirtySecond` (`_AddTeacherThreadSlaAndRatings`) | Appended `thirtyThird => thirtyThird.Should().EndWith("_AddEssayGrades")` |
| `load_output_schema` raises `ValueError` when the file is not a JSON object | ruff `TRY004` (enabled in `ai/pyproject.toml`) rejects `ValueError` after an `isinstance` check, and the DoD requires `ruff check` green | Raises `TypeError` instead; `PromptNotFoundError` for a missing file is unchanged |
| `RichTextContent.HasContent` checks `"<img"` / `"data-latex"` inline | No-magic-values rule | Same logic with two private constants `ImageMarker` / `FormulaMarker` |
| `EssayEvalCase.tags: list[str] = []` | sibling `AvatarEvalCase` uses `Field(default_factory=list)` | Followed the sibling |
| `web/src/shared/i18n`: add the 4 codes "with the same text as the resx" | the web errors table has no `AI_SERVICE_UNAVAILABLE` neighbour | Added them at the end of `errors` (and `QUESTION_ESSAY_ANSWER_TOO_LONG` after `QUESTION_MODEL_ANSWER_TOO_LONG`) |
| `.env.example` lines split ai/api | `.env.example` serves both services locally | Put the 5 commented lines (ai + api) in `.env.example`, the ai two in `deploy/ai.env.example`, the api three in `deploy/api.env.example` |

No plan signature was changed. No extra production or test file beyond the plan (helper stubs live inside the planned test classes).

## Build & test
- `dotnet test api/ -c Release` with `api/Elmanhg.Api/appsettings.json` moved aside and restored: **Passed — total 3589, failed 0** (2m 53s; Testcontainers/Docker). Warnings only in `core-libraries` (pre-existing).
- `dotnet format --verify-no-changes`: the only issues in `Elmanhg.*` were one missing space in `EssayQuestionRules.cs` (fixed afterwards) and a pre-existing one in `Tests/Builders/SubscriptionBuilder.cs` (not touched); `core-libraries` whitespace issues are pre-existing.
- ai: `uv sync --locked` OK; `ruff format --check` (97 files formatted), `ruff check` all passed, `mypy src` no issues (53 files); `pytest -m "not eval"`: **268 passed, 3 deselected**; `pytest -m eval`: 3 skipped (essay eval skips without Anthropic settings).
- web: `npm run typecheck` clean, `npm run lint` clean, `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` clean (after formatting `QuestionPreviewPanel.tsx`), `npx vitest run`: **193 files, 1094 tests passed**; `npm run build` OK. (`npm install` was needed first: `node_modules` lacked `@microsoft/signalr`, already in `package.json`/lock from main; no manifest change.)
- Regenerated: `api/openapi/v1.json` (build), `ai/openapi/v1.json` (`openapi_export`, drift test green), Orval (`gen:api`).
- Mutation checks (production line mutated, targeted tests run, file restored):
  - ai: 10/10 killed (repeat-until-stable loop, duplicate-criterion check, points-range check, essay stripping, per-field context stripping, output_schema pass-through, Anthropic model override, metered model label, safety inflation rule, readiness on essay prompts).
  - web: 5/5 killed (poll interval, InReview branch, retry refetch, `lessonId` in the grade-draft body, essay details render).
  - api: 8/8 killed (EssayGrader points range, RichTextContent image rule, post-sanitise model-answer check, hide-score-unless-Graded, reply-rules total check, worker error-code mapping, served-revision selection, confidence threshold `<` vs `<=`). Files restored and `dotnet format` re-checked afterwards (only the pre-existing `SubscriptionBuilder.cs` issue remains in `Elmanhg.*`).

## Notes for review
- The stripping (security code) was verified by reading as well as by tests: `delimiter_pattern("lesson_context","student_message").pattern` equals the old chat literal byte for byte (test 94), and chat tests are unchanged.
- `GradeEssayHandler` loads the revision via `GetRevisionsAsync([questionId])` and filters by version in memory, as planned; a missing revision throws `QUESTION_NOT_FOUND` so the worker records a failure (eventually `GradingFailed`).
- `EssayGradeResultGenerator` fills `GradedAt` only for `Graded` (InReview hides it too), per A12 wording.
- The Python fake returns 502 for essay grades by design (D13); the .NET fake is what makes offline grading work, and Postman's "Grade essay draft" relies on `AiService:Provider=Fake`.
- Postman "Get essay grade" reuses `{{sessionId}}` / `{{sessionQuestionId}}` and accepts 200 or 404 (no essay is served until #119).
- `docs/essay-grading.md` is new; the eval dataset reference points are author-graded (documented as Deferred 2).
- Test 46 additionally asserts `Subject == "Physics"` to prove the context loaded while the ids were absent.
