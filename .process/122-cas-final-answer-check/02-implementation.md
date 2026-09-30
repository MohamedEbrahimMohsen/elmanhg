# Implementation — [E15.S2] CAS final answer check + math-with-steps question type (#122, #224)

Step 1: `git fetch && git merge origin/main` fast-forwarded to `82f90c5` (#118 and #104), with no conflicts and so no merge commit. The feature work is **not committed**.

## Files created
| Path | Lines | Purpose |
|---|---|---|
| ai/src/elmanhg_ai/cas/__init__.py | 0 | package |
| ai/src/elmanhg_ai/cas/models.py | 81 | P2 enums, `MathParseError`, `Tolerance`, `CasLimits`, `CasRequest`, `CheckOutcome` |
| ai/src/elmanhg_ai/cas/lexer.py | 122 | P3 whitelist tokenizer |
| ai/src/elmanhg_ai/cas/nodes.py | 80 | P4 builders with `evaluate=False`, exponent guard, unreduced-fraction check |
| ai/src/elmanhg_ai/cas/elements.py | 62 | P5 `ParsedElement`, `assignment`, value views |
| ai/src/elmanhg_ai/cas/parser.py | 251 | P6 recursive-descent parser (lists, `\pm`, sets, relations) |
| ai/src/elmanhg_ai/cas/equivalence.py | 143 | P7 equivalence, tolerance, relations, fixed-seed sampling, matching |
| ai/src/elmanhg_ai/cas/forms.py | 60 | P8 form rules |
| ai/src/elmanhg_ai/cas/check.py | 75 | P9 `evaluate` (picklable entry point) |
| ai/src/elmanhg_ai/cas/pool.py | 94 | P10 process pool: hard timeout, terminate and recreate, RLIMIT_AS |
| ai/src/elmanhg_ai/pipelines/math_check.py | 69 | P11 limits, request, logs without the answer text |
| ai/src/elmanhg_ai/api/math_checks/{__init__,schemas,router}.py | 0/29/21 | P12–P14 `POST /v1/math-checks` |
| ai/tests/unit/test_cas_{lexer,parser,equivalence,sources_safe,pool,settings}.py | 76/101/109/21/53/31 | PT1–PT5, PT8 (PT3 = 49-row table) |
| ai/tests/unit/test_math_check_{schemas,pipeline}.py | 53/85 | PT6, PT7 |
| ai/tests/integration/test_math_checks_endpoint.py | 75 | PT9 |
| api/Elmanhg.Domain/Questions/Schemas/MathStepsSchemas.cs | 9 | D1 |
| api/Elmanhg.Domain/Questions/Grading/MathAnswerVerdict.cs | 3 | D2 |
| api/Elmanhg.Domain/Questions/Grading/MathStepsGrader.cs | 15 | D3 |
| api/Elmanhg.Application/Shared/AiService/{IAiMathCheckClient,AiMathCheckRequest,AiMathCheckResult}.cs | 6/5/5 | A1–A3 |
| api/Elmanhg.Application/Questions/Shared/MathStepsQuestionRules.cs | 39 | A4 |
| api/Elmanhg.Application/Questions/Shared/MathStepsAnswerRules.cs | 29 | A5 |
| api/Elmanhg.Application/Questions/Shared/Grading/AnswerGrader.cs | 35 | A6 (only MathSteps calls the client) |
| api/Elmanhg.Infrastructure/AiService/FakeAiMathCheckClient.cs | 54 | I1 |
| api/Elmanhg.Infrastructure/AiService/HttpAiMathCheckClient.cs | 89 | I2 (an outage becomes `Unchecked`; see Deviations) |
| api/Elmanhg.Tests/Domain/Questions/Grading/MathStepsGraderTests.cs | 43 | T1 D1–D6 |
| api/Elmanhg.Tests/Application/Features/Questions/Shared/MathStepsQuestionRulesTests.cs | 122 | T2 A1–A16 |
| api/Elmanhg.Tests/Application/Features/Questions/Shared/MathStepsAnswerRulesTests.cs | 73 | T3 M1–M9 |
| api/Elmanhg.Tests/Application/Features/Questions/Shared/Grading/AnswerGraderTests.cs | 72 | T4 AG1–AG5 |
| api/Elmanhg.Tests/Application/Features/Questions/Shared/MathGradeFeedbackTextTests.cs | 26 | T5 F1 |
| api/Elmanhg.Tests/Infrastructure/AiService/HttpAiMathCheckClientTests.cs | 86 | T6 H1–H5 |
| api/Elmanhg.Tests/Infrastructure/AiService/FakeAiMathCheckClientTests.cs | 58 | T7 K1–K4 |
| api/Elmanhg.Tests/Integration/Content/MathStepsQuestionEndpointTests.cs | 115 | T8 Q1–Q4 |
| api/Elmanhg.Tests/Integration/Sessions/MathStepsAnswerEndpointTests.cs | 72 | T9 I1–I3 |
| api/Elmanhg.Tests/Integration/Exams/MathStepsExamEndpointTests.cs | 78 | T10 X1–X2 |
| web/src/features/questions/api/mathStepsValues.ts | 30 | W1 |
| web/src/features/questions/schemas/mathStepsRules.ts | 35 | W2 |
| web/src/features/questions/components/MathStepsFields.tsx | 38 | W3 |
| web/src/features/questions/components/MathAnswersField.tsx | 68 | W4 |
| web/src/features/questions/components/AnswerInputs.tsx | 64 | W5 (MathSteps input lazy-loaded) |
| web/src/features/questions/components/MathStepsAnswerInput.tsx | 52 | W6 |
| web/src/features/questions/components/MathStepsReadOnly.tsx | 32 | W7 |
| web/src/features/questions/components/MathAnswerRulesView.tsx | 45 | W8 |
| web/src/features/quiz/api/mathDraftOwner.ts | 9 | W9 |
| web tests (WT1–WT9) | 14–129 | `mathStepsValues.test.ts`, `mathStepsRules.test.ts`, `NewMathStepsQuestion.test.tsx`, `ValidationMathStepsQuestion.test.tsx`, `quizItem.mathSteps.test.ts`, `correctAnswer.mathSteps.test.ts`, `QuizPage.mathSteps.test.tsx`, `ExamPage.mathSteps.test.tsx`, `MathStepsAnswer.restoreNotify.test.tsx` |
| docs/math-cas.md | 120 | notation, equivalence, forms, verdicts, outage behaviour, safety, the fake |

## Files modified
| Path | Change |
|---|---|
| ai/pyproject.toml, ai/uv.lock | `sympy==1.14.0` (with mpmath 1.3.0) and the mypy override |
| ai/src/elmanhg_ai/settings.py | the 12 `cas_*` settings |
| ai/src/elmanhg_ai/api/deps.py, main.py | `CasPoolDep`; the pool is created in the lifespan and closed in `finally`; the router is registered |
| ai/openapi/v1.json | regenerated |
| api Domain: QuestionType, ServableQuestionSpecification, GradeFeedbackKind, GradeFeedback, QuestionGrader | as planned |
| api/Elmanhg.Domain/Questions/Grading/QuestionGrade.cs | **not in plan**: `AwaitsReview => Feedback?.Kind == MathUnchecked` (gate) |
| api Application: QuestionSchemaRules, QuestionAnswerRules, GradeFeedbackKeys/Text, ErrorCodes, SessionsOptions, AvatarAnswerText(.Correct) | as planned |
| api/Elmanhg.Application/Sessions/SubmitAnswer/SubmitAnswerHandler.cs | caps, `AnswerGrader`; mastery skipped when `grade.AwaitsReview` |
| api/Elmanhg.Application/Exams/SaveExamAnswer/SaveExamAnswerHandler.cs | caps |
| api/Elmanhg.Application/Exams/Shared/ExamSubmission.cs | async grading through `AnswerGrader`; `AwaitsReview` attempts skip mastery |
| api/.../SubmitExamHandler.cs, AutoSubmitExamHandler.cs | the `IAiMathCheckClient` parameter |
| api/.../StartUnitExamHandler.cs, StartMultiUnitExamHandler.cs | **not in plan**: the `IAiMathCheckClient` parameter (they call `ExamSubmission`) |
| api/.../GradeQuestionDraftHandler.cs | merged with #118: the essay branch is kept; non-essay types go through caps and `AnswerGrader` |
| api/Elmanhg.Infrastructure/AiService/AiServiceOptions.cs, AiServiceServiceCollectionExtensions.cs | `MathCheckTimeoutSeconds`; typed client and provider switch |
| api/Elmanhg.Api/Resources/Messages.{ar,en}.resx | 9 entries |
| api/Elmanhg.Api/appsettings.example.json, Tests ApiFactory | the Sessions math caps, `AnswerMaxLength` 24000, `MathCheckTimeoutSeconds` |
| api/openapi/v1.json | regenerated (`QuestionType` gains `MathSteps`) |
| api/Elmanhg.Tests: QuestionBuilder, QuestionTestData | `MathSteps()`, `MathStepsContent/Fields`, `SeedMathStepsQuestionAsync` |
| api tests (constructor-only) | SubmitAnswerHandlerTests, SubmitAnswerFreeTierTests, SaveExamAnswerHandlerTests, SubmitExamHandlerTests, AutoSubmitExamHandlerTests, GradeQuestionDraftHandlerTests, **plus** StartUnitExam{Handler,FreeTier}Tests and StartMultiUnitExam{Handler,FreeTier}Tests |
| api tests (additions) | G1–G2, S1–S3, E1–E4, V1–V2, D7–D9, R1–R2 |
| api tests (intentional behaviour change) | GetSubjectExamBlueprintsHandlerTests lines 47 **and 51** (5→6); **OpenApiEndpointTests** QuestionType enum list gains `MathSteps` |
| web: the plan's "Existing code touched" list | all applied: questionOptions, editor and content schemas, values, studentQuestion, answerKey, errorFields, TypeSpecificFields, QuestionEditorForm, QuestionView, ValidationQuestionContent, index, i18n, quizItem, correctAnswer, CorrectAnswer, QuizQuestionCard, useQuizAnswer, quiz index, ExamQuestionCard, ExamRunner, useSubmitExam, examSession, blueprints values and schema, blueprintValues.test (2 expectations), mathSteps index, useMathStepsDraft (#224) |
| web/src/shared/api/generated/** | `gen:api` (QuestionType enum in 6 files) |
| postman/elmanhg.postman_collection.json | "Create math question" after the essay request; "Grade math draft" after "Grade question draft" |
| .env.example, deploy/ai.env.example, deploy/api.env.example, docs/deployment.md | CAS and `MathCheckTimeoutSeconds` keys (PROGRESS config convention; deployment.md was not in the plan's doc list) |
| docs | question-schemas, math-input, ai-service, sessions, exams, exam-blueprints, question-import, PRD §6, claude-design-prompt §4, prototype |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| D14: an AI outage returns 503 and nothing is saved (quiz); exam submit returns 503 and the worker retries. S3 `Handle_MathCheckUnavailable_ThrowsAndDoesNotSave`, E4 `Handle_MathCheckUnavailable_LeavesExamUnsubmitted`, H3–H5 `…_ThrowsMathCheckUnavailable` | Gate condition 2 overrides this: never lose an answer and never block a submit. Attempts are append-only (DB trigger), so a sweep cannot regrade an attempt later. | `HttpAiMathCheckClient` maps transport, timeout, non-2xx and unreadable replies to `Unchecked` and logs an Error. The attempt is recorded with a provisional 0 and the `mathUnchecked` feedback kind (which a query can find), is left out of mastery (`QuestionGrade.AwaitsReview`), and is resolved by teacher review (#128, the gate's "or teacher review" option). There is no sweep. Tests were renamed to match: `CheckAsync_Non2xx_ReturnsUnchecked`, `…TransportFailure_ReturnsUnchecked`, `…InvalidReply_ReturnsUnchecked`, `Handle_MathCheckUnchecked_RecordsAttemptForReviewWithoutMastery` (S3) and `Handle_MathCheckUnchecked_SubmitsWithoutMastery` (E4). The fake still throws 503 in Production (a misconfiguration); AG5 still covers propagation. The `GRADE_FEEDBACK_MATH_UNCHECKED` text adds "Your teacher will review it." |
| A7: ctor `(sanitizer, mathCheckClient, sessionsOptions, localizer)`; G-test ctor to match | #118 made the handler async with 8 dependencies and an essay branch | Kept #118's constructor and essay branch, appended `mathCheckClient, sessionsOptions` after `localizer`, and routed the non-essay types through the caps and `AnswerGrader` (D31) |
| Only Submit/AutoSubmit call `ExamSubmission.SubmitAsync` | StartUnitExam and StartMultiUnitExam also call it (auto-submitting an expired exam) | Added the client to both and to their 4 test constructors |
| Only `GetSubjectExamBlueprintsHandlerTests` line 47 changes | Line 51 asserts the same count, and `OpenApiEndpointTests` pins the QuestionType enum | Updated both. This is the intentional behaviour change, not a weakened test |
| Q1: 201. Q2: problem+json | The API returns 200 for create and `application/json` for errors | Asserted 200 and dropped the content-type assertion |
| Q3: Arabic feedback | The test host's default language is English | The test sends `Accept-Language: ar` |
| D23: the quiz clears the draft in `useQuizAnswer.onSuccess` | WT7c showed that TanStack's `onSuccess` can run before React commits the disabled render, so the input's unmount flush rewrites the draft | The draft is cleared in a `useEffect` keyed on the recorded attempt; React runs child unmount cleanups before parent effects. The exam path is as planned (it disables before the request) |
| W5 imports `MathStepsAnswerInput` statically | The quiz page was 257/255 KB | `React.lazy` plus `Suspense` for the MathSteps input: 253/255 KB. WT4 waits with `findByRole` |
| P10 `WORKER_FAILURES` includes `MaybeEncodingError` | typeshed does not declare it (mypy error); our result is always picklable | Removed it. A dead worker ends in the timeout path |
| P9: parse catches only `MathParseError` | SymPy `doit` can raise (for example RecursionError) | `_parse` also catches `SYMPY_FAILURES`, which reads as unreadable or invalid-expected |
| P3/P6 details | `\log_2` needs a `_` token; nested unevaluated `Mul`s broke `factored`; `x^23` needs a multiply by the pushed-back digits | The lexer emits `_` only after `\log`; `term()` builds one flat product with `nodes.reciprocal`; the parser tracks `split_at` so the pushed-back digits multiply implicitly |
| i18n `editor.math.answersHint` gives `\frac{1}{2}` as the example | ICU treats `{1}` as a placeholder | The example is `\sqrt 2` |

## Build & test
- **api** (no `api/Elmanhg.Api/appsettings.json` present, so CI parity; Docker 29.6.2): `dotnet test -c Release` → `total: 3797, failed: 0, succeeded: 3797`. `dotnet build` produced no new warnings. `dotnet format --verify-no-changes` reports only the existing Windows CRLF whitespace noise in `core-libraries` and nothing in the changed files.
- **ai**: `python -m uv sync --locked` passed ("Checked 56 packages"). `ruff format --check`: 120 files already formatted. `ruff check`: all checks passed. `mypy src`: no issues in 67 files. `pytest -q -m "not eval"`: **375 passed**, 3 deselected. The OpenAPI drift test passes.
- **web**: `npm ci` and `gen:api` done. `lint` and `typecheck` pass; `prettier --check --end-of-line auto .` passes. `vitest run`: **208 files, 1182 passed**. `npm run build` passed; `perf:budget`: entry 203/210, landing 211/220, lesson 232/240, quiz 253/255 KB, all ok.
- **Mutation checks** (each caught, then reverted): (1) no sign alternation, so PT3 `absolute-value` fails; (2) no digit-split implicit multiply, so PT2c fails; (3) no product-of-sum check, so PT3 `expanded-expected` fails; (4) no restored-draft `onChange`, so WT9a and WT8b fail; (5) the `onSuccess` draft clear, so WT7c fails (this found the D23 ordering bug).

## Notes for review
- Teacher review of `mathUnchecked` attempts does not exist yet (#128). Until it does, such attempts keep a provisional 0 in the session score. The orchestrator should make sure #128 includes them; this is noted in math-cas.md and sessions.md.
- Exam submit checks each MathSteps item one after another. If the AI service hangs, each check can take up to `MathCheckTimeoutSeconds` (15 s) before it becomes `unchecked`, so a submit with many math items could be slow. It never fails.
- The #119 lane (student essay input) changes the same files (`SubmitAnswerHandler`, `ExamSubmission`, `ServableQuestionSpecification`, `QuestionView`); expect merge conflicts there.
- The `HttpAiMathCheckClient.PostAsync` catch returns `null` after logging an Error. This is deliberate (gate condition 2), not a swallowed exception.
- The CAS pool tests spawn real worker processes; they take about 44 s in total on Windows (spawn) and use a 60 s cold-start timeout.
