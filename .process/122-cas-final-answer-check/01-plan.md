# Plan — [E15.S2] CAS final answer check + math-with-steps question type (#122, #224)

## Goal
An admin can author a **math-with-steps** question: stem, accepted final answers in LaTeX, a required form (any equivalent / simplified / factored / expanded / exact) and an optional numeric tolerance. The admin sees the student input in the live preview, and «جرّب الإجابة» grades the final answer with the real CAS check. A teacher validates the question with its answer rules shown. Approved math questions are served in quizzes and exams (and count in blueprints and mastery). Students answer with the #121 `MathStepsAnswer` component (steps + final answer, drafts on the device). The final answer is graded by a new SymPy equivalence endpoint in the `ai/` service, which reads common notations through a restricted parser with size limits and a hard timeout. The steps are stored with the attempt, for #123 to grade. A restored draft now reaches the parent (#224), and the draft is cleared after a successful submit.

## Scope
**In:**
- **ai/**
  - `POST /v1/math-checks`: a restricted LaTeX/Unicode tokenizer and recursive-descent parser that builds SymPy objects directly (never `sympify`/`parse_expr`/`parse_latex`/`eval`).
  - Equivalence, with lists, `\pm`, relations and assignments.
  - Per-request tolerance and form rules.
  - A process pool with a hard timeout and a memory cap.
  - Settings, logging, OpenAPI, and unit and integration tests, including a table of equivalent forms.
- **api/**
  - `QuestionType.MathSteps`, its schemas, authoring rules and answer rules (shape and caps).
  - Servable and served everywhere (quiz, exam, blueprint, mastery).
  - `IAiMathCheckClient` with Fake and Http adapters.
  - `AnswerGrader`, used by quiz answer, exam submit, auto-submit and grade-draft.
  - Grade feedback lines, avatar answer text, error codes, options, OpenAPI and Postman.
- **web/**
  - Editor fields, and a preview that uses the math input.
  - `MathStepsAnswer` mounted in quiz and exam, with read-only review.
  - The validation page shows the answer rules.
  - Correct-answer reveal.
  - Draft clearing on a successful submit.
  - The #224 restore→`onChange` fix.
  - Blueprint rows, i18n and the Orval client.
- **Docs:** `question-schemas.md`, new `math-cas.md`, `math-input.md`, `ai-service.md`, `sessions.md`, `exams.md`, `exam-blueprints.md`, `question-import.md`, `PRD.md` §6, `claude-design-prompt.md` §4, `prototype.md`.

**Out:**
- Step grading, the model-solution authoring (`MathStepsInput` for admins) and combining step and final scores. These are #123.
- Teacher review of math grades. This is E17 (#128).
- Spreadsheet import of math questions (PRD §10.1: v1 deterministic types only).
- The other #224 notes: the Playwright keypad test, T54 timing and the 16 px font.

**Deferred:** none. SymPy runs offline, and the .NET Fake/Http switch already exists (`AiService:Provider`).

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | Type name and label | `QuestionType.MathSteps`, appended after `Essay`. Web label "Math with steps" / «رياضيات بالخطوات», marked v2 in the type select like Essay. | The prototype (`TYPE.math_steps`). Appending keeps the existing enum values. |
| 2 | Student answer shape | `{"steps": string[], "finalAnswer": string}` (docs/math-input.md §2). `CanRead`: a JSON object whose `steps` (optional) holds only strings and whose `finalAnswer` (optional) is a string. `Canonicalize`: trim every step and drop the blank ones, trim `finalAnswer`, and write missing values as `[]` / `""`. | This matches `toMathStepsPayload`. It is lenient like the other types (a missing field means no answer). |
| 3 | Answer caps | New `SessionsOptions`: `MathStepsMaxCount` 20, `MathStepMaxLength` 500, `MathFinalAnswerMaxLength` 200 (they mirror the #121 web constants). Checked in the handlers after `CanRead` through `QuestionAnswerRules.ExceedsLimits`. A violation is 422 `ATTEMPT_ANSWER_TOO_LONG` (reused). `Sessions:AnswerMaxLength` default goes from 4000 to **24000**. | Skill §8.1: caps live in Options. LaTeX backslashes double in JSON, so 20 × 500 × 2 + 200 × 2 + overhead ≈ 20.6k. |
| 4 | Question content | Body `{}` (`MathStepsBody`). Grading spec `{"acceptedAnswers": [...], "form": "...", "tolerance"?: n, "toleranceMode"?: "absolute"\|"percent"}`. The accepted answers reuse `Content:QuestionAcceptedAnswersMaxCount` (20) and `Content:QuestionAnswerMaxLength` (200) through `QuestionSchemaReader.AreValidAcceptedAnswers`, with the new code `QUESTION_MATH_ANSWERS_INVALID` so the web can map the error to the math field. | Nothing is student-visible besides the stem. The caps reuse existing config. |
| 5 | Forms | `MathAnswerForm { Equivalent, Simplified, Factored, Expanded, Exact }`, default `Equivalent` (written explicitly by `Normalize`). A form check runs only after equivalence, on the **answer**. `Exact`: no decimal literal. `Expanded`: `expand(value) == value` and the raw tree has no product that contains a sum with symbols. `Factored`: every factor of the raw top-level product (or the raw value itself) is irreducible, meaning `factor_list` gives one non-constant factor of multiplicity 1 and a content of ±1. `Simplified`: no unreduced integer fraction (gcd > 1, or a denominator of ±1), and `count_ops(raw answer values) ≤ count_ops(raw expected values)`. | Each form has a deterministic definition an implementer can write, and the #122 story asks for "form rules". `x + x` vs `2x` passes Simplified (a documented limitation in math-cas.md). |
| 6 | Tolerance | Optional. `tolerance ≥ 0` and `toleranceMode` (the existing `ToleranceMode` enum) come as a pair. Only with `form = Equivalent` (otherwise `QUESTION_MATH_TOLERANCE_FORM_CONFLICT`). It applies when both compared values are constants: exact rational arithmetic when both are rationals, otherwise 30-digit floats. The bound is inclusive: `absolute` = ±tolerance, `percent` = ±\|expected\| × tolerance / 100. | This mirrors Short numeric (PRD §6). Mixing an approximation with a form rule is contradictory. |
| 7 | Score until #123 | Final answer only. An `equivalent` verdict scores 1; everything else scores 0. A blank final answer is Unanswered (0), even if steps were written. The steps are stored in the attempt answer. | PRD §6: "Final answer: CAS; Steps: LLM" (#123 combines them). The prototype grades the final answer only. |
| 8 | Verdicts and feedback | ai verdicts are `equivalent`, `notEquivalent`, `wrongForm`, `unreadable` (the answer failed to parse or passed a size limit) and `unchecked` (timeout, worker failure, or no accepted answer parses). Feedback: `equivalent`/`notEquivalent` → `MathFinalAnswerOnly`; `wrongForm` → `MathWrongForm`; `unreadable` → `MathUnreadable`; `unchecked` → `MathUnchecked`; blank → `Unanswered`. | The student learns why an answer scored 0 without being shown the answer. `unchecked` is a separate verdict, so an ops problem is never blamed on the student's notation. |
| 9 | Where the CAS runs | `ai/`, as a pure SymPy function behind `POST /v1/math-checks` (tag `grading`, operationId `grading_create_math_check`). No LLM is involved, so there is no prompt file, eval marker, token count or cost. A parametrised unit table of more than 40 equivalent and non-equivalent forms replaces the eval (python skill §4 requires evals for model pipelines only). | PRD §19/§22: "SymPy CAS checks" live in the AI/grading service. #118 uses the same `grading` tag. |
| 10 | Parsing untrusted LaTeX | Our own tokenizer and recursive-descent parser build `sympy.Integer/Rational/Symbol/Add/Mul/Pow/sin/...` with `evaluate=False`. No SymPy string entry point is used (`sympify`, `parse_expr` and `S("...")` call `eval`; `parse_latex` needs antlr/lark). Numbers are built from digits (`Integer(int(...))`, `Rational(p, 10**k)`). A guard test scans `cas/*.py` for forbidden names. | This is the security requirement: the whole grammar is a whitelist. |
| 11 | Resource limits | Settings (`ELMANHG_AI_CAS_*`) bound the answer (500 chars), each expected answer (500), expected count (20), elements (10), tokens (300), nesting depth (30), number digits (30) and \|numeric exponent\| (1000, checked bottom-up while parsing, so power towers stop early). Evaluation runs in a `multiprocessing` pool (`forkserver` where available, else `spawn`) with `RLIMIT_AS` = 1024 MB on POSIX, `maxtasksperchild` 200, and a hard `cas_timeout_seconds` = 5. On timeout the pool is terminated and recreated lazily. | Threads cannot interrupt CPU-bound SymPy. Terminating the pool is the only hard stop. |
| 12 | Worker failure or timeout | The endpoint returns 200 `unchecked` and never a 5xx. | A hostile final answer must not make exam submission fail forever: the auto-submit worker would retry endlessly. |
| 13 | .NET call sites | A static `AnswerGrader` (Application/Questions/Shared/Grading) is used by `SubmitAnswerHandler` (quiz, synchronous), `ExamSubmission` (manual submit and auto-submit) and `GradeQuestionDraftHandler`. Exams check at **submit**, not on every autosave. | There is one place for the "which grader" branch, with no service layer. Autosave is debounced and frequent, while submit grades once. |
| 14 | ai service down (Http provider) | `HttpAiMathCheckClient` throws `ServiceUnavailableCoreException(MATH_CHECK_UNAVAILABLE)` (503). Quiz: nothing is saved and the student retries. Exam: submit returns 503 and the student can retry; the auto-submit worker logs and retries on the next sweep. | The attempt log is append-only, so a guessed grade must never be written. |
| 15 | Accepted answers are not CAS-parsed at authoring | Create and update stay synchronous and offline. At grading, unparseable accepted answers are skipped (`invalidExpected`), and the .NET client logs a Warning. If none parse, the verdict is `unchecked`. The admin preview shows `MathUnchecked` feedback for such a question. | This avoids making question authoring depend on the ai service. The teacher validates with the rules view. |
| 16 | .NET Fake | `FakeAiMathCheckClient` (the default provider) maps Arabic-Indic digits, removes `\left`, `\right` and all whitespace, then compares strings with Ordinal equality against each accepted answer: `Equivalent` with `MatchedIndex`, else `NotEquivalent`. It refuses in Production with 503 `MATH_CHECK_UNAVAILABLE`. | Tests and CI need no Python. This is the same pattern as the other fakes. |
| 17 | Servable | MathSteps is **not** excluded; `ServedTypes` becomes six types (the five v1 types plus `MathSteps`), so it appears in quizzes, exams, blueprint counts, mastery and avatar retrieval. `QuestionImportColumns.Types` is unchanged (still five). | The story makes the type reachable. Import is v1-deterministic only (PRD §10.1). |
| 18 | Equation semantics | `assignment(e)` is an `=` whose side is a bare symbol (left wins). Expected **expression**: the answer is an expression or an assignment, and its value is compared. Expected **assignment** `s = v`: the answer is `v` alone, or an assignment of the same `s`; `2x = 4` for `x = 2` is **not** equivalent. Expected **general relation**: the answer must be a relation with the same normalised operator (`>`→`<` and `≥`→`≤` by swapping sides), and `simplify((La−Ra)/(Le−Re))` must be a nonzero number (`=`/`≠`) or a positive number (`<`/`≤`). | A final answer must isolate the unknown, while general relations accept any rearrangement. |
| 19 | Lists, `\pm`, sets | Top-level commas separate an unordered multiset of elements (at most 10). Arabic `،` is a comma. A single outer `\{…\}` is unwrapped. Exactly one `\pm` in an element expands it into `+` and `−`; more than one is unreadable. A comma inside brackets is unreadable. | Roots such as "x = 2, x = −3" and "x = ±3" are common final answers. The decimal comma is not supported, but the web already maps `٫`. |
| 20 | Notation details | `\text{…}` (no nested braces) is removed. `e` is Euler's number. `\log` without a base is base 10, `\log_{b}`/`\log_b` sets the base, and `\ln` is natural. `\sin x` takes one primary as its argument; `\sin^2 x` squares it. `x^23` is `x²·3` (TeX takes one token). Implicit multiplication happens before a name, a command primary, or `(`, `[` or `{`, but never before a number. Symbols are ASCII letters with an optional `_d` / `_{abc}` subscript, plus 8 Greek letters. | These are TeX semantics, covering the #121 keypad keys and their Unicode glyphs. |
| 21 | Numeric fallback | When `simplify(a−b) ≠ 0`, the parser samples points with a fixed seed of 1729. Signs alternate +,−; magnitudes are `randint(50,300)/100`. There are up to 24 tries, and it needs 5 real agreeing points (1 when there are no symbols), with a relative gap ≤ 1e-12 × max(1,\|b\|). Any real disagreeing point means not equivalent. | Grading is repeatable. Alternating signs catch `√(x²)` ≠ `x`. |
| 22 | Web mounting | `QuestionView` gets an optional `mathDraftOwner`. Enabled with an owner renders `MathStepsAnswer` (quiz and exam). Enabled without an owner renders a local `MathStepsInput` (admin preview). Disabled renders a read-only list (review, answered, validation, submitting). The owner comes from `useSession()?.userId` plus the session and question. | This reuses the #121 components as they are, and the admin preview needs no drafts. |
| 23 | Draft clearing order | Quiz: `disabled` is true while checking, so `MathStepsAnswer` unmounts first and its unmount flush writes any pending draft. `useQuizAnswer.onSuccess` then calls `clearMathDraft`. Exam: `isPending` disables every card before the mutation, and `useSubmitExam.onSuccess` calls `onSubmitted`, which clears every math item's draft, before navigating. A failed submit remounts the input, which restores the draft. | Clearing always happens after the last write. The earlier alternative, clearing inside `onSuccess` while the input was still mounted, could be re-written by a pending timer. |
| 24 | #224 restored draft | `useMathStepsDraft` calls `onChange(initial.value)` once, in an effect, when the draft was restored (a ref guard prevents a second call under StrictMode). | The quiz "تحقّق" and the exam autosave then use the restored answer. |
| 25 | Empty answer (web) | `isAnswerEmpty(MathSteps)` = the final answer, trimmed, is empty. | This matches the server's Unanswered rule. The quiz shows «answer required» and the exam counts the question as unanswered. |
| 26 | Correct-answer reveal | `describeCorrectAnswer` → `{kind:'math', latex: acceptedAnswers[0]}`, rendered with `MathPreview` (KaTeX). | The grading spec is already revealed after answering (docs/sessions.md). |
| 27 | Avatar text | Student answer: the steps, then `الإجابة النهائية: <final>`, joined with `\n`. Correct answer: the first accepted answer. | The avatar's "explain my mistake" then works for math. |
| 28 | Morabh | Morabh has no CAS, LaTeX or math code (`grep -rli "latex\|sympy\|equivalen" --include=*.cs` found nothing). Every piece is **new, with no Morabh equivalent**. In-repo patterns reused: `HttpAiTranscriptionClient`, `FakeAiTranscriptionClient`, `EssayQuestionRules`, `pipelines/transcribe.py` and the transcription router. | The reuse-first check was done. |
| 29 | New Python dependency | `sympy==1.14.0` (BSD-3-Clause, uploaded 2025-04-27, requires `mpmath<1.4,>=1.1`, which resolves to `mpmath==1.3.0`, BSD). It is inside `exclude-newer = 2026-09-22`. SymPy ships no `py.typed`, so there is a mypy override `ignore_missing_imports` for `sympy.*` and `mpmath.*`. | The PRD names SymPy. Both licences are on the allow-list. |
| 30 | Timeouts nest | ai `cas_timeout_seconds` 5, plus a spawn cold start of about 2 s. The .NET `AiService:MathCheckTimeoutSeconds` is 15 (attempt = total). The POST is not retried. | The API always outlives the service. |
| 31 | Merging with #118 | Keep additions in separate new files where possible, and append to shared lists or files at their ends: resx, shared i18n errors, ErrorCodes (after `QuestionIdsDuplicate` and `AiServiceUnavailable`), `settings.py` (after the last field), `main.py`/`deps.py` (after the transcription lines), and `QuestionAnswerRules` (new arms after `Short`). `GradeQuestionDraftHandler` will conflict (both make it async): the merge keeps #118's essay branch and routes every other type through `AnswerGrader.GradeAsync`. | Lane coordination (orchestrator note). |

## Existing code touched
| File | Change |
|------|--------|
| `ai/pyproject.toml` | Add `"sympy==1.14.0",` to `dependencies` (alphabetical, after `structlog`). Add `[[tool.mypy.overrides]]` with `module = ["sympy", "sympy.*", "mpmath", "mpmath.*"]` and `ignore_missing_imports = true`. |
| `ai/uv.lock` | `uv lock` (adds sympy 1.14.0 and mpmath 1.3.0). |
| `ai/src/elmanhg_ai/settings.py` | After `metric_export_interval_seconds`, add the 12 `cas_*` fields in P16. |
| `ai/src/elmanhg_ai/api/deps.py` | `def cas_pool_from_app(request: Request) -> CasPool`, which reads `request.app.state.cas_pool` and raises `ServiceNotReadyError` when missing. `CasPoolDep = Annotated[CasPool, Depends(cas_pool_from_app)]`. |
| `ai/src/elmanhg_ai/main.py` | Lifespan, after the transcription client: `cas_pool = CasPool(settings)` and `app.state.cas_pool = cas_pool`. In `finally`, before `telemetry.shutdown()`: `await asyncio.to_thread(cas_pool.close)`. `create_app`: `app.include_router(math_checks_router.router)` after the transcriptions router. |
| `ai/openapi/v1.json` | `uv run python -m elmanhg_ai.openapi_export`. |
| `api/Elmanhg.Domain/Questions/QuestionType.cs` | `public enum QuestionType { Mcq, Multi, TrueFalse, Fill, Short, Essay, MathSteps }` |
| `api/Elmanhg.Domain/Questions/ServableQuestionSpecification.cs` | `ServedTypes` gets `QuestionType.MathSteps` appended. `QuestionCondition` is unchanged. |
| `api/Elmanhg.Domain/Questions/Grading/GradeFeedbackKind.cs` | Append `MathFinalAnswerOnly, MathWrongForm, MathUnreadable, MathUnchecked`. |
| `api/Elmanhg.Domain/Questions/Grading/GradeFeedback.cs` | Add 4 static properties, like `Unanswered`: `MathFinalAnswerOnly`, `MathWrongForm`, `MathUnreadable`, `MathUnchecked`, each `new(GradeFeedbackKind.X, 0, 0, 0)`. |
| `api/Elmanhg.Domain/Questions/Grading/QuestionGrader.cs` | Add `public static QuestionGrade GradeMathSteps(int maxScore, MathAnswerVerdict? verdict) => QuestionGrade.FromNormalised(MathStepsGrader.Grade(verdict), maxScore);`. `Grade(MathSteps, …)` still throws (default arm). |
| `api/Elmanhg.Application/Questions/Shared/QuestionSchemaRules.cs` | Add a `Validate` arm `QuestionType.MathSteps => MathStepsQuestionRules.Validate(fields.Body, fields.GradingSpec, options),` and a `Normalize` arm `QuestionType.MathSteps => MathStepsQuestionRules.Normalize(fields.Body, fields.GradingSpec),`. |
| `api/Elmanhg.Application/Questions/Shared/QuestionAnswerRules.cs` | `CanRead` arm `QuestionType.MathSteps => MathStepsAnswerRules.CanRead(answer),`. `Canonicalize` arm `QuestionType.MathSteps => MathStepsAnswerRules.Canonicalize(answer),`. New `public static bool ExceedsLimits(QuestionType type, JsonElement answer, SessionsOptions options) => type == QuestionType.MathSteps && MathStepsAnswerRules.ExceedsLimits(answer, options);` |
| `api/Elmanhg.Application/Questions/Shared/Grading/GradeFeedbackKeys.cs` | Add `MathFinalAnswerOnly = "GRADE_FEEDBACK_MATH_FINAL_ONLY"`, `MathWrongForm = "GRADE_FEEDBACK_MATH_WRONG_FORM"`, `MathUnreadable = "GRADE_FEEDBACK_MATH_UNREADABLE"` and `MathUnchecked = "GRADE_FEEDBACK_MATH_UNCHECKED"`. |
| `api/Elmanhg.Application/Questions/Shared/Grading/GradeFeedbackText.cs` | Four arms before `_`: `GradeFeedbackKind.MathFinalAnswerOnly => localizer.GetMessage(GradeFeedbackKeys.MathFinalAnswerOnly)`, and the same for the other three. |
| `api/Elmanhg.Application/Questions/GradeQuestionDraft/GradeQuestionDraftHandler.cs` | Rewritten; see A7. |
| `api/Elmanhg.Application/Sessions/SubmitAnswer/SubmitAnswerHandler.cs` | Append ctor params `IAiMathCheckClient mathCheckClient, IOptions<SessionsOptions> sessionsOptions`. After the `CanRead` throw: `if (QuestionAnswerRules.ExceedsLimits(type, request.Answer, sessionsOptions.Value)) { throw new ApplicationValidationCoreException(ErrorCodes.AttemptAnswerTooLong); }` then `var grade = await AnswerGrader.GradeAsync(revision, request.Answer, mathCheckClient, cancellationToken).ConfigureAwait(false);`. `RecordAttempt(..., grade, ...)` replaces `revision.Grade(request.Answer)`. |
| `api/Elmanhg.Application/Exams/SaveExamAnswer/SaveExamAnswerHandler.cs` | Append ctor param `IOptions<SessionsOptions> sessionsOptions`. After the `CanRead` throw, add the same `ExceedsLimits` throw (`AttemptAnswerTooLong`). |
| `api/Elmanhg.Application/Exams/Shared/ExamSubmission.cs` | Signature: `SubmitAsync(Session session, IReadOnlyCollection<QuestionRevision> revisions, IQuestionMasteryRepository questionMasteryRepository, IAiMathCheckClient mathCheckClient, decimal correctThreshold, DateTimeOffset now, CancellationToken cancellationToken)`. The grades loop becomes `Dictionary<Guid, QuestionGrade> grades = []; foreach (var item in session.Items.Where(x => x.SavedAnswer is not null)) { grades[item.QuestionId] = await GradeAsync(FindRevision(revisions, item), item.SavedAnswer!, mathCheckClient, cancellationToken).ConfigureAwait(false); }`. `private static async Task<QuestionGrade> GradeAsync(QuestionRevision revision, string answer, IAiMathCheckClient mathCheckClient, CancellationToken cancellationToken) { using var document = JsonDocument.Parse(answer); return await AnswerGrader.GradeAsync(revision, document.RootElement, mathCheckClient, cancellationToken).ConfigureAwait(false); }` |
| `api/Elmanhg.Application/Exams/SubmitExam/SubmitExamHandler.cs` | Append ctor param `IAiMathCheckClient mathCheckClient`, and pass it to `ExamSubmission.SubmitAsync`. |
| `api/Elmanhg.Application/Exams/AutoSubmitExam/AutoSubmitExamHandler.cs` | Append ctor param `IAiMathCheckClient mathCheckClient`, and pass it to `ExamSubmission.SubmitAsync`. |
| `api/Elmanhg.Application/Avatar/Shared/AvatarAnswerText.cs` | Constants `private const string StepSeparator = "\n";` and `private const string FinalAnswerLabel = "الإجابة النهائية: ";`. Arm `QuestionType.MathSteps => MathStepsText(Read<MathStepsAnswer>(answerJson)),`. `private static string? MathStepsText(MathStepsAnswer? answer)`: null → null. Otherwise take the non-blank trimmed steps, then append `FinalAnswerLabel + final` when the final answer is not blank, and return `NullIfBlank(string.Join(StepSeparator, parts))`. |
| `api/Elmanhg.Application/Avatar/Shared/AvatarAnswerText.Correct.cs` | Arm `QuestionType.MathSteps => NullIfBlank(Read<MathStepsGradingSpec>(snapshot.GradingSpec)?.AcceptedAnswers?.FirstOrDefault()),` |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | After `QuestionIdsDuplicate`: `QuestionMathAnswersInvalid = "QUESTION_MATH_ANSWERS_INVALID"`, `QuestionMathFormInvalid = "QUESTION_MATH_FORM_INVALID"`, `QuestionMathToleranceInvalid = "QUESTION_MATH_TOLERANCE_INVALID"`, `QuestionMathToleranceFormConflict = "QUESTION_MATH_TOLERANCE_FORM_CONFLICT"`. After `AiServiceUnavailable`: `MathCheckUnavailable = "MATH_CHECK_UNAVAILABLE"`. |
| `api/Elmanhg.Application/Shared/Options/SessionsOptions.cs` | `AnswerMaxLength` default `24000`. Add `[Range(1, 100)] public int MathStepsMaxCount { get; set; } = 20;`, `[Range(1, 10000)] public int MathStepMaxLength { get; set; } = 500;` and `[Range(1, 10000)] public int MathFinalAnswerMaxLength { get; set; } = 200;`. |
| `api/Elmanhg.Infrastructure/AiService/AiServiceOptions.cs` | `[Range(1, 120)] public int MathCheckTimeoutSeconds { get; set; } = 15;` |
| `api/Elmanhg.Infrastructure/AiService/AiServiceServiceCollectionExtensions.cs` | Add a block after the transcription block: `AddHttpClient<HttpAiMathCheckClient>(...)` with BaseAddress and `client.Timeout = Timeout.InfiniteTimeSpan`, then `.AddStandardResilienceHandler().Configure(...)`: attempt timeout = total timeout = `MathCheckTimeoutSeconds`, `CircuitBreaker.SamplingDuration = timeout * 2`, and `Retry.DisableForUnsafeHttpMethods()`. Add `services.AddScoped<FakeAiMathCheckClient>();` and `services.AddScoped<IAiMathCheckClient>(sp => Options(sp).Provider switch { Fake => Fake…, Http => Http…, _ => throw … })`, the same as transcription. |
| `api/Elmanhg.Api/Resources/Messages.ar.resx`, `Messages.en.resx` | Append (at the end) the 9 entries in Error codes / Feedback strings. |
| `api/Elmanhg.Api/appsettings.example.json` | `"Sessions"`: `"AnswerMaxLength": 24000, "MathStepsMaxCount": 20, "MathStepMaxLength": 500, "MathFinalAnswerMaxLength": 200`. `"AiService"`: `"MathCheckTimeoutSeconds": 15`. |
| `api/openapi/v1.json` | Regenerated by `dotnet build` (`QuestionType` gains `MathSteps`). |
| `api/Elmanhg.Tests/Builders/QuestionBuilder.cs` | Field `private bool _mathSteps;`, `public QuestionBuilder MathSteps() { _mathSteps = true; return this; }`. `Build()`: the type is `_mathSteps ? QuestionType.MathSteps : _essay ? QuestionType.Essay : QuestionType.Mcq`, and the content follows the same order. `public const string MathStepsSpecJson = """{"acceptedAnswers":["x = 2"],"form":"equivalent"}""";`. `public static QuestionContent MathStepsContent() => new("<p>Solve 2x + 3 = 7.</p>", "{}", MathStepsSpecJson, "<p>Subtract 3, divide by 2.</p>", 2);`. `public static QuestionFields MathStepsFields()` has the same content, with `QuestionDifficulty.Medium`, no objective, no tags and maxScore 2. |
| `api/Elmanhg.Tests/Integration/Content/QuestionTestData.cs` | `public static async Task<Guid> SeedMathStepsQuestionAsync(ApiFactory factory, Guid lessonId, CancellationToken cancellationToken)`: a copy of `SeedQuestionAsync(approved: true)` with `QuestionType.MathSteps` and `QuestionBuilder.MathStepsContent()`. |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | In the settings dictionary: `["Sessions:MathStepsMaxCount"] = "20"`, `["Sessions:MathStepMaxLength"] = "500"`, `["Sessions:MathFinalAnswerMaxLength"] = "200"`, `["AiService:MathCheckTimeoutSeconds"] = "15"`. |
| `api/Elmanhg.Tests/Application/Features/Questions/GradeQuestionDraft/GradeQuestionDraftHandlerTests.cs` | ctor becomes `new GradeQuestionDraftHandler(_richTextSanitizer, _mathCheckClient, Options.Create(new SessionsOptions()), _localizer)`, where `_mathCheckClient = Substitute.For<IAiMathCheckClient>()`. Add tests G1–G2. |
| `api/Elmanhg.Tests/Application/Features/Sessions/SubmitAnswer/SubmitAnswerHandlerTests.cs`, `SubmitAnswerFreeTierTests.cs` | ctor gains `…, Substitute.For<ILocalizer>(), _mathCheckClient, Options.Create(new SessionsOptions()))` (a field in HandlerTests, inline `Substitute.For<IAiMathCheckClient>()` in FreeTier). HandlerTests adds S1–S3. |
| `api/Elmanhg.Tests/Application/Features/Exams/SaveExamAnswer/SaveExamAnswerHandlerTests.cs` | ctor gains `Options.Create(new SessionsOptions())`. Add E1–E2. |
| `api/Elmanhg.Tests/Application/Features/Exams/SubmitExam/SubmitExamHandlerTests.cs` | ctor gains `_mathCheckClient`. Add E3. |
| `api/Elmanhg.Tests/Application/Features/Exams/AutoSubmitExam/AutoSubmitExamHandlerTests.cs` | ctor gains `_mathCheckClient`. Add E4. |
| `api/Elmanhg.Tests/Application/Features/ExamBlueprints/GetSubjectExamBlueprints/GetSubjectExamBlueprintsHandlerTests.cs` | Line 47: `HaveCount(5)` → `HaveCount(6)` (an intentional behaviour change: MathSteps is served). |
| `api/Elmanhg.Tests/Domain/Questions/ServableQuestionSpecificationTests.cs` | Add D7. |
| `api/Elmanhg.Tests/Domain/Questions/Grading/QuestionGraderTests.cs` | Add D8–D9. |
| `api/Elmanhg.Tests/Application/Features/Avatar/Shared/AvatarAnswerTextTests.cs` | Add V1–V2. |
| `api/Elmanhg.Tests/Infrastructure/AiService/AiServiceServiceCollectionExtensionsTests.cs` | Add R1–R2. |
| `postman/elmanhg.postman_collection.json` | **Questions** folder: after "Create essay question", add "Create math question". It copies "Create question" (method, URL, auth, headers) with the I-body below, and its test stores `mathQuestionId` from `id` and asserts 201. After "Grade question draft", add "Grade math draft": POST `/api/questions/grade-draft` with the same fields plus `"answer":{"steps":["2x = 4"],"finalAnswer":"x=2"}`; its test asserts 200 and that `outcome` is a string. |
| `web/src/features/mathSteps/hooks/useMathStepsDraft.ts` | After the `latest`/`timer` refs: `const announced = useRef(false); useEffect(() => { if (announced.current \|\| initial.status !== 'restored') { return; } announced.current = true; onChange?.(initial.value); }, [initial, onChange]);` |
| `web/src/features/mathSteps/index.ts` | Add `export { MathPreview } from './components/MathPreview';` |
| `web/src/features/questions/api/questionOptions.ts` | Append `'MathSteps'` to `questionTypes` and to `servedQuestionTypes`. Add `export const mathAnswerForms = ['equivalent','simplified','factored','expanded','exact'] as const;`, `// mirrors Content:QuestionAcceptedAnswersMaxCount` `export const mathAnswersMax = 20;` and `// mirrors Content:QuestionAnswerMaxLength` `export const mathAnswerMaxLength = 200;`. |
| `web/src/features/questions/schemas/questionEditorSchema.ts` | Add the fields `mathAnswers: z.array(z.object({ latex: z.string() }))`, `mathForm: z.enum(mathAnswerForms)`, `mathTolerance: z.string()` and `mathToleranceMode: z.enum(['absolute','percent'])`. In `superRefine`: `if (values.type === 'MathSteps') { addMathStepsIssues(values, issue); }` |
| `web/src/features/questions/schemas/questionContentSchemas.ts` | `export const mathStepsSpecSchema = z.object({ acceptedAnswers: z.array(z.string()), form: z.enum(mathAnswerForms).optional(), tolerance: z.number().optional(), toleranceMode: z.enum(['absolute','percent']).optional() });` |
| `web/src/features/questions/api/questionValues.ts` | `emptyQuestionValues` adds `mathAnswers: [{ latex: '' }], mathForm: 'equivalent', mathTolerance: '', mathToleranceMode: 'absolute'`. `readQuestionContent` gets `case 'MathSteps': return readMathSteps(spec);`. `toContent` gets `case 'MathSteps': return toMathStepsContent(values);`. |
| `web/src/features/questions/api/studentQuestion.ts` | `QuestionAnswer` gains `math: MathStepsPayload` (from `@/features/mathSteps`), and `emptyAnswer()` adds `math: { steps: [], finalAnswer: '' }`. `toAnswerPayload` gets `case 'MathSteps': return { steps: answer.math.steps, finalAnswer: answer.math.finalAnswer };` |
| `web/src/features/questions/api/answerKey.ts` | `case 'MathSteps': return { ...answer, math: { steps: [], finalAnswer: values.mathAnswers[0]?.latex ?? '' } };` |
| `web/src/features/questions/api/questionErrorFields.ts` | `QUESTION_MATH_ANSWERS_INVALID: 'mathAnswers'`, `QUESTION_MATH_FORM_INVALID: 'mathForm'`, `QUESTION_MATH_TOLERANCE_INVALID: 'mathTolerance'`, `QUESTION_MATH_TOLERANCE_FORM_CONFLICT: 'mathTolerance'`. |
| `web/src/features/questions/components/TypeSpecificFields.tsx` | `case 'MathSteps': return <MathStepsFields />;` |
| `web/src/features/questions/components/QuestionEditorForm.tsx` | Type option label: `value === 'Essay' \|\| value === 'MathSteps' ? t('editor.fields.typeV2', { type: t(\`types.${value}\`) }) : t(\`types.${value}\`)` |
| `web/src/features/questions/components/QuestionView.tsx` | Props gain `mathDraftOwner?: MathDraftOwner \| undefined`. The `otherInputs`/`inputs` consts are replaced by `<AnswerInputs question={question} answer={answer} onAnswerChange={onAnswerChange} disabled={disabled} review={review} mathDraftOwner={mathDraftOwner} />`. The stem block is unchanged. |
| `web/src/features/questions/components/ValidationQuestionContent.tsx` | After the Essay block: `{values.type === 'MathSteps' ? <MathAnswerRulesView answers={values.mathAnswers} form={values.mathForm} tolerance={values.mathTolerance} toleranceMode={values.mathToleranceMode} /> : null}` |
| `web/src/features/questions/index.ts` | Add `mathStepsSpecSchema` to the content-schemas export. |
| `web/src/features/questions/i18n/en.json`, `ar.json` | The keys in "i18n keys". |
| `web/src/features/quiz/api/quizItem.ts` | `quizQuestionTypes` gets `'MathSteps'` appended. `answerPayloadSchema` adds `steps: z.array(z.string()).optional(), finalAnswer: z.string().nullish()`. `fromAnswerPayload` gets `case 'MathSteps': return { ...answer, math: { steps: steps ?? [], finalAnswer: finalAnswer ?? '' } };`. `isAnswerEmpty` gets `case 'MathSteps': return answer.math.finalAnswer.trim() === '';`. `toQuizQuestion` is unchanged (the body is not read). |
| `web/src/features/quiz/api/correctAnswer.ts` | `CorrectAnswerView` gains `\| { kind: 'math'; latex: string }`. `describeCorrectAnswer` gets `case 'MathSteps': { const spec = mathStepsSpecSchema.safeParse(correctAnswer); return spec.success && spec.data.acceptedAnswers[0] ? { kind: 'math', latex: spec.data.acceptedAnswers[0] } : null; }`. `choiceReview` adds `case 'MathSteps':` to the group that returns `undefined`. |
| `web/src/features/quiz/components/CorrectAnswer.tsx` | `case 'math': return <MathPreview latex={view.latex} label={t('feedback.mathAnswer')} />;` (import from `@/features/mathSteps`). |
| `web/src/features/quiz/components/QuizQuestionCard.tsx` | `const draftOwner = mathDraftOwnerFor(useSession()?.userId, sessionId, item.questionId);`, then `useQuizAnswer(sessionId, item, question, draftOwner)` and `<QuestionView … mathDraftOwner={draftOwner} />`. |
| `web/src/features/quiz/hooks/useQuizAnswer.ts` | Signature `useQuizAnswer(sessionId: string, item: SessionItemResult, question: StudentQuestion, draftOwner?: MathDraftOwner)`. In `onSuccess`, after `trackFunnelEvent`: `if (question.type === 'MathSteps' && draftOwner) { clearMathDraft(draftOwner); }` |
| `web/src/features/quiz/index.ts` | `export { mathDraftOwnerFor } from './api/mathDraftOwner';` |
| `web/src/features/quiz/i18n/en.json`, `ar.json` | `feedback.mathAnswer`: "Final answer" / «الإجابة النهائية». |
| `web/src/features/exam/components/ExamQuestionCard.tsx` | New prop `sessionId: string`. `const draftOwner = mathDraftOwnerFor(useSession()?.userId, sessionId, item.questionId);`, passed as `mathDraftOwner`. |
| `web/src/features/exam/components/ExamRunner.tsx` | `const studentId = useSession()?.userId;`. `useSubmitExam(session.id, answers.flush, () => { clearExamMathDrafts(session, studentId); })`. `<ExamQuestionCard sessionId={session.id} …/>`. |
| `web/src/features/exam/hooks/useSubmitExam.ts` | Signature `(sessionId: string, flush: () => Promise<void>, onSubmitted?: () => void)`. The first line of `onSuccess` is `onSubmitted?.();`. |
| `web/src/features/exam/api/examSession.ts` | `export function clearExamMathDrafts(session: ExamSessionResult, studentId: string \| undefined): void`: return when `!studentId`; otherwise `session.items.filter((item) => item.type === 'MathSteps').forEach((item) => { clearMathDraft({ studentId, sessionId: session.id, questionId: item.questionId }); })`. |
| `web/src/features/blueprints/api/blueprintValues.ts` | `emptyBlueprintValues.counts` and `toFormValues` counts add `MathSteps` (`'0'` / `count('MathSteps')`). |
| `web/src/features/blueprints/schemas/examBlueprintSchema.ts` | `counts` adds `MathSteps: z.string()`. |
| `web/src/features/blueprints/api/blueprintValues.test.ts` | Modify: the `toFormValues` expectation adds `MathSteps: '0'`; "counts only the served question types" adds `'MathSteps'` at the end. |
| `web/src/shared/i18n/en.json`, `ar.json` | Under `errors`, appended: the 5 codes (same text as the resx). |
| `web/src/shared/api/generated/**` | `npm --prefix web run gen:api`. |
| `.env.example`, `deploy/ai.env.example` | Commented lines under `# #122 CAS final answer check`: `# ELMANHG_AI_CAS_TIMEOUT_SECONDS=5`, `# ELMANHG_AI_CAS_WORKERS=2`, `# ELMANHG_AI_CAS_WORKER_MEMORY_MB=1024`. |
| `deploy/api.env.example` | `# AiService__MathCheckTimeoutSeconds=15` |
| Docs | See "Docs". |

## Files to create

### ai/ (package `elmanhg_ai`)
| # | Path | Type | Contract |
|---|------|------|----------|
| P1 | `ai/src/elmanhg_ai/cas/__init__.py` | empty | — |
| P2 | `ai/src/elmanhg_ai/cas/models.py` | types | `class AnswerForm(StrEnum)`: `EQUIVALENT="equivalent"`, `SIMPLIFIED="simplified"`, `FACTORED="factored"`, `EXPANDED="expanded"`, `EXACT="exact"`. `class ToleranceMode(StrEnum)`: `ABSOLUTE="absolute"`, `PERCENT="percent"`. `class Verdict(StrEnum)`: `EQUIVALENT="equivalent"`, `NOT_EQUIVALENT="notEquivalent"`, `WRONG_FORM="wrongForm"`, `UNREADABLE="unreadable"`, `UNCHECKED="unchecked"`. `class RelationOperator(StrEnum)`: `EQ="="`, `NE="!="`, `LT="<"`, `LE="<="`, `GT=">"`, `GE=">="`. `class MathParseError(Exception)` (internal; never reaches HTTP). Frozen slotted dataclasses: `Tolerance(value: Decimal, mode: ToleranceMode)`; `CasLimits(max_tokens: int, max_depth: int, max_elements: int, max_number_digits: int, max_exponent: int)` with `@classmethod from_settings(cls, settings: Settings) -> Self`; `CasRequest(answer: str, expected: tuple[str, ...], form: AnswerForm, tolerance: Tolerance \| None, limits: CasLimits)`; `CheckOutcome(verdict: Verdict, matched_index: int \| None, invalid_expected: tuple[int, ...])`. |
| P3 | `ai/src/elmanhg_ai/cas/lexer.py` | tokenizer | `TokenKind = Literal["number", "name", "command", "symbol"]`. `@dataclass(frozen=True, slots=True) class Token: kind: TokenKind; text: str`. `def tokenize(text: str, limits: CasLimits) -> list[Token]`, in order: 1. Translate Arabic-Indic digits (U+0660–0669, U+06F0–06F9) to 0–9, `٫` (U+066B) to `.` and `،` (U+060C) to `,` (a `str.maketrans` table built with `chr(0x0660 + d)`; no literal `\u` escapes in tool arguments). 2. `TEXT_GROUP = re.compile(r"\\text\s*\{[^{}]*\}")` → replaced by `" "`. 3. Scan: skip whitespace and `~`; `\,` `\;` `\:` `\!` `\ ` are skipped; `\{`/`\}` → symbol `"\\{"`/`"\\}"`; `\` + letters → command name, where `SKIPPED = {"left","right","quad","qquad","displaystyle"}` are dropped, `ALIASES = {"dfrac":"frac","tfrac":"frac","leq":"le","geq":"ge","neq":"ne"}`, and `\lt`/`\gt` become symbols `<`/`>`; `COMMANDS = frozenset({"frac","sqrt","pi","times","cdot","div","pm","le","ge","ne","sin","cos","tan","cot","sec","csc","log","ln","alpha","beta","gamma","theta","lambda","mu","phi","omega"})`, and any other command raises `MathParseError`. Unicode `UNICODE = {"×":"times","÷":"div","·":"cdot","⋅":"cdot","≤":"le","≥":"ge","≠":"ne","±":"pm","π":"pi","√":"sqrt"}` → command; `−` and `–` → symbol `-`. Numbers match `\d+(\.\d+)?\|\.\d+`; a trailing `.` raises; more than `max_number_digits` digits raises. Names are one ASCII letter, optionally followed by `_` and one alnum char, or by `_{[A-Za-z0-9]{1,5}}` → text `"x_1"`; any other `_` raises. Symbols are `+ - * / ^ = < > ( ) [ ] { } ,`. Any other character raises. 4. More than `max_tokens` tokens raises. |
| P4 | `ai/src/elmanhg_ai/cas/nodes.py` | builders | Every builder uses `evaluate=False`. `def number(text: str) -> sympy.Rational` builds `Integer(int(digits))` or `Rational(int(whole+fraction), 10**len(fraction))`. `negate(value)`, `add(terms)`, `multiply(factors)` (one item → itself), `divide(numerator, denominator) = Mul(numerator, Pow(denominator, Integer(-1), evaluate=False), evaluate=False)`, `power(base, exponent, limits)` (runs `check_exponent` first), `root(radicand, index: sympy.Expr \| None)` (`Pow(radicand, Rational(1,2))` or `Pow(radicand, Pow(index, -1))`), `apply_function(name: str, argument)`, where `FUNCTIONS: Final = {"sin": sympy.sin, "cos": sympy.cos, "tan": sympy.tan, "cot": sympy.cot, "sec": sympy.sec, "csc": sympy.csc, "ln": sympy.log}` is called with `evaluate=False`. `logarithm(argument, base) = divide(log(argument, evaluate=False), log(base, evaluate=False))`. `def check_exponent(exponent, limits) -> None`: when `exponent.is_number`, `value = abs(sympy.N(exponent, 15))`, and raise `MathParseError` if `not value.is_finite or value > limits.max_exponent`. `def is_unreduced_fraction(numerator, denominator) -> bool`: true when both are `sympy.Integer`, the denominator is non-zero, and `gcd(\|n\|,\|d\|) > 1 or \|d\| == 1`. |
| P5 | `ai/src/elmanhg_ai/cas/elements.py` | element | `@dataclass(frozen=True, slots=True) class ParsedElement`: `operator: RelationOperator \| None`, `raw_left`, `raw_right` (`None` for an expression), `left`, `right` (evaluated: `raw.doit()`), `has_decimal: bool`, `has_unreduced_fraction: bool`. `@classmethod build(cls, operator, raw_left, raw_right, has_decimal, has_unreduced_fraction) -> Self` computes `left`/`right`. `def assignment(element) -> tuple[sympy.Symbol, sympy.Expr, sympy.Expr] \| None`: returns `(symbol, value, raw_value)` when the operator is `EQ` and the evaluated left (else right) `is_Symbol`, otherwise `None`. `def value_view(element) -> tuple[sympy.Expr, ...]` and `raw_value_view(element)`: an expression gives `(left,)`, an assignment gives `(value,)`, any other relation gives `(left, right)`. |
| P6 | `ai/src/elmanhg_ai/cas/parser.py` | parser | `def parse_answer(text: str, limits: CasLimits) -> tuple[ParsedElement, ...]`: tokenize; strip one outer `\{`…`\}` pair when it wraps everything; split on top-level `,` (a comma inside `()[]{}` raises); raise on more than `max_elements` elements or an empty element; each element with exactly one `pm` command becomes two token lists (`+` and `-`), and more than one raises; then parse each list with `_ElementParser`. `class _ElementParser` holds `tokens`, `position`, `depth`, `limits`, `has_decimal` and `has_unreduced_fraction`. `element()` = `expr` [relop `expr`], then the end is required (a second relop raises). Relops are `=`, `<`, `>`, `le`, `ge` and `ne`. `expr()` increments `depth` (raises above `max_depth`), takes an optional leading `+`/`-`, then terms joined by `+`/`-` (subtraction is `add([a, negate(b)])`). `term()` = `factor` followed by `*`/`times`/`cdot` → multiply, `/`/`div` → divide (setting `has_unreduced_fraction` via `is_unreduced_fraction`), or an implicit factor when the next token is a name, a primary-starting command (`frac`, `sqrt`, `pi`, a function or a Greek letter), or `(`/`[`/`{` (never a number). `factor()` = `-` factor → negate, `+` factor → factor, else a `primary` with an optional `^` power argument; a second `^` raises. The power argument is `{expr}`, one digit (a multi-digit number is split and the rest is pushed back), a name, or `pi`/Greek. `primary()` covers: a number (sets `has_decimal` on `.`), a name (`"e"` → `sympy.E`, else `sympy.Symbol(name)`), `pi` → `sympy.pi`, Greek → `Symbol(name)`, `( expr )`, `[ expr ]`, `{ expr }`, `frac` group group (a group is `{expr}` or one digit/name token; sets the unreduced flag), `sqrt` with an optional `[expr]` index then a group, functions with an optional `^`power, then an argument (`(expr)`, `{expr}` or a single `primary`), and `log` with an optional `_` base (a group or a single token), default `Integer(10)`. Anything else raises `MathParseError`. No file-level mutable state. |
| P7 | `ai/src/elmanhg_ai/cas/equivalence.py` | compare | Constants, each with a WHY comment: `SAMPLE_SEED: Final = 1729` (repeatable grading), `SAMPLE_POINTS: Final = 5`, `SAMPLE_ATTEMPTS: Final = 24`, `DIGITS: Final = 30`, `RELATIVE_GAP: Final = sympy.Rational(1, 10**12)`. `def expressions_equivalent(a, b, tolerance: Tolerance \| None) -> bool`: 1. When `tolerance` is set and both have no free symbols, `within_tolerance` decides; if either value is not real it falls through. 2. `a - b == 0` → True. 3. `sympy.simplify(a - b) == 0` → True. 4. `numerically_equal(a, b)`. `def within_tolerance(a, b, tolerance) -> bool \| None`: the allowed bound is `Rational(Fraction(tolerance.value))` for absolute, or `abs(b) * that / 100` for percent. Exact comparison when `a.is_Rational and b.is_Rational`; otherwise `evalf(DIGITS)`. Returns `None` when a value is not real and finite. `def numerically_equal(a, b) -> bool`: symbols sorted by `str`; `rng = random.Random(SAMPLE_SEED)`; attempt `i` uses sign `+1` when `i` is even, else `-1`, times `Rational(rng.randint(50, 300), 100)`. It skips points where either value is not real and finite, returns False on any gap above `RELATIVE_GAP * max(1, \|b\|)`, and returns True after `SAMPLE_POINTS` agreeing points (1 when there are no symbols). `def relations_match(answer, expected) -> bool` implements D18's ratio rule after normalising `GT`/`GE` by swapping sides; when `expected.left - expected.right` simplifies to 0, it returns whether the answer's difference also simplifies to 0. `def elements_match(answer, expected, tolerance) -> bool` implements D18 exactly. `def match_answers(answer: Sequence[ParsedElement], expected: Sequence[ParsedElement], tolerance) -> list[tuple[ParsedElement, ParsedElement]] \| None`: unequal lengths → None; otherwise a greedy pairing that, for each expected element in order, takes the first unused matching answer element; if one has no match → None. `# S311: fixed-seed sampling, not security` noqa on the `random` use. |
| P8 | `ai/src/elmanhg_ai/cas/forms.py` | forms | `def satisfies_form(pairs: Sequence[tuple[ParsedElement, ParsedElement]], form: AnswerForm) -> bool` returns `all(_pair_ok(a, e, form) for a, e in pairs)`. `EQUIVALENT` → True; `EXACT` → `not a.has_decimal`; `EXPANDED` → for every `(v, raw)` in `zip(value_view(a), raw_value_view(a))`: `sympy.expand(v) == v and not _has_product_of_sum(raw)`; `FACTORED` → every raw value satisfies `_is_factored(raw)`; `SIMPLIFIED` → `not a.has_unreduced_fraction and sum(count_ops(x) for x in raw_value_view(a)) <= sum(count_ops(x) for x in raw_value_view(e))`. `_has_product_of_sum(raw)` checks every `Mul` node in `sympy.preorder_traversal(raw)` for an `Add` argument with free symbols. `_is_factored(raw)`: the factors are `raw.args` when `raw.is_Mul`, else `(raw,)`. For each factor, take the base when it is a `Pow` with an Integer exponent; skip factors with no free symbols; `coefficient, parts = sympy.factor_list(sympy.expand(base.doit()))`; the factor is OK when `sum(m for _, m in parts) == 1 and abs(coefficient) == 1`. `PolificationFailed`/`PolynomialError` (from `sympy.polys.polyerrors`) count as OK. |
| P9 | `ai/src/elmanhg_ai/cas/check.py` | entry | `SYMPY_FAILURES: Final = (ArithmeticError, ValueError, TypeError, NotImplementedError, RecursionError)`. `def evaluate(request: CasRequest) -> CheckOutcome` (module-level, so it pickles), in order: 1. Parse each expected answer; a `MathParseError` adds its index to `invalid`. 2. No valid expected answer → `CheckOutcome(UNCHECKED, None, invalid)`. 3. Parse the answer; `MathParseError` → `CheckOutcome(UNREADABLE, None, invalid)`. 4. For each valid `(index, expected)` in order: `pairs = _safe_match(answer, expected, tolerance)` (a `SYMPY_FAILURES` exception → None). `None` → continue. `_safe_form` → True means `EQUIVALENT` with `matched_index=index`; otherwise set `wrong_form = True`. 5. Return `WRONG_FORM` if `wrong_form`, else `NOT_EQUIVALENT`. A `SYMPY_FAILURES` exception inside the form check counts as a failed form. |
| P10 | `ai/src/elmanhg_ai/cas/pool.py` | pool | `class CasChecker(Protocol): async def check(self, request: CasRequest) -> CheckOutcome: ...`. `WORKER_FAILURES: Final = (MemoryError, RecursionError, ArithmeticError, ValueError, TypeError, NotImplementedError, multiprocessing.pool.MaybeEncodingError)`. `def limit_worker_memory(megabytes: int) -> None`: when `sys.platform != "win32"`, `resource.setrlimit(resource.RLIMIT_AS, (bytes, bytes))`. `class CasPool` with `__init__(self, settings: Settings)` (holds a `threading.Lock` and `_pool: Pool \| None = None`), `started` property (`_pool is not None`), and `async def check(self, request) -> CheckOutcome`, in order: 1. `pool = await asyncio.to_thread(self._current)`. 2. `pending = pool.apply_async(evaluate, (request,))`. 3. `return await asyncio.to_thread(pending.get, settings.cas_timeout_seconds)`. 4. On `multiprocessing.TimeoutError`: `logger.warning("math_check.timeout", timeout_seconds=…)`, then `await asyncio.to_thread(self._discard, pool)` and return unchecked. 5. On `WORKER_FAILURES`: `logger.warning("math_check.worker_failed", error_type=type(error).__name__)` and return unchecked. Unchecked is `CheckOutcome(Verdict.UNCHECKED, None, ())`. `_current()` creates the pool under the lock when missing: `context = multiprocessing.get_context(method)`, where `method = "forkserver"` if it is in `get_all_start_methods()`, else `"spawn"`; for forkserver, `context.set_forkserver_preload(["elmanhg_ai.cas.check"])`; then `context.Pool(processes=cas_workers, initializer=limit_worker_memory, initargs=(cas_worker_memory_mb,), maxtasksperchild=cas_max_tasks_per_worker)`. `_discard(pool)`: under the lock, if `self._pool is pool`, set `None`; then `pool.terminate(); pool.join()`. `close()`: swap to `None` under the lock, then terminate and join; it is idempotent. |
| P11 | `ai/src/elmanhg_ai/pipelines/math_check.py` | pipeline | `PIPELINE_NAME: Final = "math_check"`. `@dataclass(frozen=True, slots=True) class MathCheckResult(verdict: Verdict, matched_index: int \| None, invalid_expected: tuple[int, ...])`. `def _limit_errors(payload: MathCheckIn, settings) -> list[FieldError]`: `answer` gets `TOO_LONG` over `cas_max_answer_chars`; `expected` gets `TOO_MANY_ITEMS` over `cas_max_expected`; `expected[i]` gets `TOO_LONG` over `cas_max_expected_chars`. `async def run(payload: MathCheckIn, *, checker: CasChecker, settings: Settings) -> MathCheckResult`: 1. Limit errors → `ValidationFailedError`. 2. Build the `CasRequest` (the tolerance only when `payload.tolerance is not None`; `CasLimits.from_settings`). 3. Time `await checker.check(request)`. 4. `logger.info("math_check.completed", pipeline=PIPELINE_NAME, verdict=…, form=…, expected_count=…, invalid_expected=len(…), answer_chars=len(payload.answer), latency_ms=…)`; the answer and expected text are **never** logged. 5. Return the result. |
| P12 | `ai/src/elmanhg_ai/api/math_checks/__init__.py` | empty | — |
| P13 | `ai/src/elmanhg_ai/api/math_checks/schemas.py` | Pydantic | `class MathCheckIn(ApiInModel)`: `answer: str = Field(min_length=1)`, `expected: list[Annotated[str, Field(min_length=1)]] = Field(min_length=1)`, `form: AnswerForm = AnswerForm.EQUIVALENT`, `tolerance: Decimal \| None = Field(default=None, ge=0)`, `tolerance_mode: ToleranceMode \| None = None`. A `@model_validator(mode="after")` raises `ValueError("tolerance and toleranceMode go together")` when exactly one is set, and `ValueError("tolerance needs the equivalent form")` when a tolerance is set with any other form. `class MathCheckOut(ApiOutModel)`: `verdict: Verdict`, `matched_index: int \| None`, `invalid_expected: list[int]`. |
| P14 | `ai/src/elmanhg_ai/api/math_checks/router.py` | router | `router = APIRouter(prefix="/v1", tags=["grading"], dependencies=[Depends(require_service_token)])`. `@router.post("/math-checks", responses={400: {"model": Problem}, 401: {"model": Problem}})` `async def create_math_check(payload: MathCheckIn, settings: SettingsDep, pool: CasPoolDep) -> MathCheckOut` calls `math_check.run(payload, checker=pool, settings=settings)` and maps the result. |
| P15 | `api/deps.py`, `main.py` | — | See Existing code touched. |
| P16 | settings (in `settings.py`) | — | `cas_timeout_seconds: float = Field(default=5.0, gt=0, le=60)`; `cas_workers: int = Field(default=2, ge=1, le=16)`; `cas_worker_memory_mb: int = Field(default=1024, ge=256, le=8192)`; `cas_max_tasks_per_worker: int = Field(default=200, ge=1, le=100000)`; `cas_max_answer_chars: int = Field(default=500, ge=1, le=5000)`; `cas_max_expected: int = Field(default=20, ge=1, le=100)`; `cas_max_expected_chars: int = Field(default=500, ge=1, le=5000)`; `cas_max_elements: int = Field(default=10, ge=1, le=50)`; `cas_max_tokens: int = Field(default=300, ge=1, le=5000)`; `cas_max_depth: int = Field(default=30, ge=1, le=200)`; `cas_max_number_digits: int = Field(default=30, ge=1, le=1000)`; `cas_max_exponent: int = Field(default=1000, ge=1, le=100000)`. |

The ai test files are listed in the Test plan (PT1–PT9). Each Python file stays under 300 lines. `parser.py` may need `nodes.py` to hold the builders, and it must not grow past the limit.

### api/ — Domain
| # | Path | Type | Contract |
|---|------|------|----------|
| D1 | `api/Elmanhg.Domain/Questions/Schemas/MathStepsSchemas.cs` | records | `namespace Elmanhg.Domain.Questions.Schemas;` `public enum MathAnswerForm { Equivalent, Simplified, Factored, Expanded, Exact }` `public sealed record MathStepsBody;` `public sealed record MathStepsGradingSpec(List<string>? AcceptedAnswers, MathAnswerForm? Form, decimal? Tolerance, ToleranceMode? ToleranceMode);` `public sealed record MathStepsAnswer(List<string?>? Steps, string? FinalAnswer);` |
| D2 | `api/Elmanhg.Domain/Questions/Grading/MathAnswerVerdict.cs` | enum | `public enum MathAnswerVerdict { Equivalent, NotEquivalent, WrongForm, Unreadable, Unchecked }` |
| D3 | `api/Elmanhg.Domain/Questions/Grading/MathStepsGrader.cs` | static | `public static NormalisedGrade Grade(MathAnswerVerdict? verdict) => verdict switch { null => NormalisedGrade.Unanswered, MathAnswerVerdict.Equivalent => new(1m, GradeFeedback.MathFinalAnswerOnly), MathAnswerVerdict.NotEquivalent => new(0m, GradeFeedback.MathFinalAnswerOnly), MathAnswerVerdict.WrongForm => new(0m, GradeFeedback.MathWrongForm), MathAnswerVerdict.Unreadable => new(0m, GradeFeedback.MathUnreadable), MathAnswerVerdict.Unchecked => new(0m, GradeFeedback.MathUnchecked), _ => throw new InvalidOperationException("Unsupported math verdict."), };` |

### api/ — Application
| # | Path | Type | Contract |
|---|------|------|----------|
| A1 | `api/Elmanhg.Application/Shared/AiService/IAiMathCheckClient.cs` | port | `Task<AiMathCheckResult> CheckAsync(AiMathCheckRequest request, CancellationToken cancellationToken);` |
| A2 | `api/Elmanhg.Application/Shared/AiService/AiMathCheckRequest.cs` | record | `public sealed record AiMathCheckRequest(string Answer, IReadOnlyList<string> Expected, MathAnswerForm Form, decimal? Tolerance, ToleranceMode? ToleranceMode);` |
| A3 | `api/Elmanhg.Application/Shared/AiService/AiMathCheckResult.cs` | record | `public sealed record AiMathCheckResult(MathAnswerVerdict Verdict, int? MatchedIndex, IReadOnlyList<int> InvalidExpected);` |
| A4 | `api/Elmanhg.Application/Questions/Shared/MathStepsQuestionRules.cs` | static | **`public static List<string> Validate(JsonElement body, JsonElement gradingSpec, ContentOptions options)`**, in order: 1. `TryRead<MathStepsBody>` / `TryRead<MathStepsGradingSpec>`; `AddIf(!bodyRead, QuestionBodyInvalid)` and `AddIf(!specRead, QuestionGradingSpecInvalid)`; return if either failed. 2. `AddIf(!QuestionSchemaReader.AreValidAcceptedAnswers(spec.AcceptedAnswers, options), QuestionMathAnswersInvalid)`. 3. `AddIf(spec.Form is { } form && !Enum.IsDefined(form), QuestionMathFormInvalid)`. 4. When `spec.Tolerance is not null \|\| spec.ToleranceMode is not null`: `AddIf(spec.Tolerance is null or < 0 \|\| spec.ToleranceMode is null \|\| !Enum.IsDefined(spec.ToleranceMode.Value), QuestionMathToleranceInvalid)` and `AddIf((spec.Form ?? MathAnswerForm.Equivalent) != MathAnswerForm.Equivalent, QuestionMathToleranceFormConflict)`. **`public static (string Body, string GradingSpec) Normalize(JsonElement body, JsonElement gradingSpec)`**: `spec = Read<MathStepsGradingSpec>(gradingSpec)`; returns `(Serialize(new MathStepsBody()), Serialize(new MathStepsGradingSpec(QuestionSchemaReader.TrimAnswers(spec.AcceptedAnswers), spec.Form ?? MathAnswerForm.Equivalent, spec.Tolerance, spec.Tolerance is null ? null : spec.ToleranceMode)))`. |
| A5 | `api/Elmanhg.Application/Questions/Shared/MathStepsAnswerRules.cs` | static | `public static bool CanRead(JsonElement answer) => QuestionSchemaReader.TryRead<MathStepsAnswer>(answer, out var math) && (math.Steps is null \|\| math.Steps.All(x => x is not null));` `public static string Canonicalize(JsonElement answer)`: `math = Read<MathStepsAnswer>(answer)`; `steps = (math.Steps ?? [])` → `.Select(x => x!.Trim())` → `.Where(x => x.Length > 0)` → `.ToList<string?>()`; returns `Serialize(new MathStepsAnswer(steps, (math.FinalAnswer ?? string.Empty).Trim()))`. `public static bool ExceedsLimits(JsonElement answer, SessionsOptions options)`: `steps = math.Steps ?? []`; true if `steps.Count > options.MathStepsMaxCount \|\| steps.Any(x => x is not null && x.Length > options.MathStepMaxLength) \|\| (math.FinalAnswer?.Length ?? 0) > options.MathFinalAnswerMaxLength`. |
| A6 | `api/Elmanhg.Application/Questions/Shared/Grading/AnswerGrader.cs` | static | `public static Task<QuestionGrade> GradeAsync(QuestionRevision revision, JsonElement answer, IAiMathCheckClient mathCheckClient, CancellationToken cancellationToken)` reads the snapshot and calls the overload with `(snapshot.Type, snapshot.GradingSpec?.ToJsonString() ?? "{}", snapshot.MaxScore, …)`. `public static async Task<QuestionGrade> GradeAsync(QuestionType type, string gradingSpec, int maxScore, JsonElement answer, IAiMathCheckClient mathCheckClient, CancellationToken cancellationToken)`: 1. If `type != MathSteps`, return `QuestionGrader.Grade(type, gradingSpec, maxScore, answer)`. 2. `finalAnswer = (QuestionSchemaReader.Read<MathStepsAnswer>(answer).FinalAnswer ?? string.Empty).Trim()`; if it is empty, return `QuestionGrader.GradeMathSteps(maxScore, null)` (no client call). 3. `spec = JsonSerializer.Deserialize<MathStepsGradingSpec>(gradingSpec, QuestionJson.SerializerOptions) ?? throw new InvalidOperationException("Question grading spec is not readable.")`. 4. `result = await mathCheckClient.CheckAsync(new AiMathCheckRequest(finalAnswer, spec.AcceptedAnswers ?? [], spec.Form ?? MathAnswerForm.Equivalent, spec.Tolerance, spec.ToleranceMode), cancellationToken).ConfigureAwait(false)`. 5. Return `QuestionGrader.GradeMathSteps(maxScore, result.Verdict)`. |
| A7 | `api/Elmanhg.Application/Questions/GradeQuestionDraft/GradeQuestionDraftHandler.cs` (rewrite) | handler | `GradeQuestionDraftHandler(IRichTextSanitizer richTextSanitizer, IAiMathCheckClient mathCheckClient, IOptions<SessionsOptions> sessionsOptions, ILocalizer localizer)`. `public async Task<QuestionGradeResult> Handle(...)`: 1. `content = QuestionContentFactory.CreateContent(request.Question, richTextSanitizer)`; `type = request.Question.Type.GetValueOrDefault()`. 2. `if (QuestionAnswerRules.ExceedsLimits(type, request.Answer, sessionsOptions.Value)) { throw new ApplicationValidationCoreException(ErrorCodes.AttemptAnswerTooLong); }` 3. `grade = await AnswerGrader.GradeAsync(type, content.GradingSpec, content.MaxScore, request.Answer, mathCheckClient, cancellationToken).ConfigureAwait(false)`. 4. Return `new QuestionGradeResult(grade.Score, grade.NormalisedScore, grade.Outcome.ToString(), content.MaxScore, GradeFeedbackText.Localize(grade.Feedback, localizer))`. |

### api/ — Infrastructure
| # | Path | Type | Contract |
|---|------|------|----------|
| I1 | `api/Elmanhg.Infrastructure/AiService/FakeAiMathCheckClient.cs` | adapter | `(IHostEnvironment hostEnvironment) : IAiMathCheckClient`. In Production it throws `ServiceUnavailableCoreException(ErrorCodes.MathCheckUnavailable)`. `Normalize(string latex)`: remove `\left` and `\right` (Ordinal `Replace`), then a `StringBuilder` pass that drops `char.IsWhiteSpace` and maps `'٠'..'٩'` / `'۰'..'۹'` to `'0'..'9'` (range arithmetic from the char constants `(char)0x0660` and `(char)0x06F0`, named `ArabicIndicZero`/`ExtendedArabicIndicZero`). `index` = the first expected answer whose normalised form equals the normalised answer (Ordinal). It returns `new AiMathCheckResult(index >= 0 ? Equivalent : NotEquivalent, index >= 0 ? index : null, [])`. |
| I2 | `api/Elmanhg.Infrastructure/AiService/HttpAiMathCheckClient.cs` | adapter | A copy of `HttpAiTranscriptionClient`: `(HttpClient httpClient, IOptions<AiServiceOptions> aiServiceOptions, ILogger<HttpAiMathCheckClient> logger) : IAiMathCheckClient`, with `MathChecksPath = "v1/math-checks"` and the same `SerializerOptions`. The reply reads into `private sealed record MathCheckReply(MathAnswerVerdict? Verdict, int? MatchedIndex, List<int>? InvalidExpected);`. `CheckAsync`: post; if the reply is null, `Verdict is null`, `!Enum.IsDefined(Verdict.Value)` or `InvalidExpected is null`, it logs Error "AI service returned an invalid math check reply." and throws 503 `MathCheckUnavailable`. Transport, rejected, non-2xx and `JsonException` errors log Error (without the answer) and throw 503 `MathCheckUnavailable` (with the inner exception when there is one). When `Verdict == Unchecked \|\| InvalidExpected.Count > 0`, it logs `LogWarning("Math check verdict {Verdict} with {InvalidCount} unreadable accepted answers.", …)`. It returns `new AiMathCheckResult(reply.Verdict.Value, reply.MatchedIndex, reply.InvalidExpected)`. |

### api/ — Tests (new files)
| # | Path | Notes |
|---|------|-------|
| T1 | `api/Elmanhg.Tests/Domain/Questions/Grading/MathStepsGraderTests.cs` | D1–D6 |
| T2 | `api/Elmanhg.Tests/Application/Features/Questions/Shared/MathStepsQuestionRulesTests.cs` | `_options` = the `ShortQuestionRulesTests` initialiser. A1–A16 |
| T3 | `api/Elmanhg.Tests/Application/Features/Questions/Shared/MathStepsAnswerRulesTests.cs` | Tests `QuestionAnswerRules` for MathSteps: M1–M9 |
| T4 | `api/Elmanhg.Tests/Application/Features/Questions/Shared/Grading/AnswerGraderTests.cs` | `Substitute.For<IAiMathCheckClient>()`. AG1–AG5 |
| T5 | `api/Elmanhg.Tests/Application/Features/Questions/Shared/MathGradeFeedbackTextTests.cs` | F1 |
| T6 | `api/Elmanhg.Tests/Infrastructure/AiService/HttpAiMathCheckClientTests.cs` | The same harness as `HttpAiTranscriptionClientTests` (`StubHttpMessageHandler`, `AiServiceTestSettings`). H1–H5 |
| T7 | `api/Elmanhg.Tests/Infrastructure/AiService/FakeAiMathCheckClientTests.cs` | K1–K4 |
| T8 | `api/Elmanhg.Tests/Integration/Content/MathStepsQuestionEndpointTests.cs` | Declared like `EssayQuestionEndpointTests`. Q1–Q4 |
| T9 | `api/Elmanhg.Tests/Integration/Sessions/MathStepsAnswerEndpointTests.cs` | Seed: `SessionTestData.SeedServableLessonAsync(factory, 4)` plus `QuestionTestData.SeedMathStepsQuestionAsync`, then start a quiz and pick the item with `type == "MathSteps"`. I1–I3 |
| T10 | `api/Elmanhg.Tests/Integration/Exams/MathStepsExamEndpointTests.cs` | Seed as `ExamTestData.SeedExamUnitAsync(factory, 1)`, then add a math question and **replace** the blueprint with a new one of `[Mcq 1, MathSteps 1]` (a local helper in the test class, using `ExamBlueprint.CreateForUnit`). X1–X2 |

### web/
| # | Path | Type | Contract |
|---|------|------|----------|
| W1 | `web/src/features/questions/api/mathStepsValues.ts` | module | `readMathSteps(spec: JsonElement): Partial<QuestionValues>`: `{}` when `mathStepsSpecSchema` fails; otherwise `mathAnswers` (`latex` per answer; `[{latex:''}]` when empty), `mathForm` (`?? 'equivalent'`), `mathTolerance` (`String(n)` or `''`) and `mathToleranceMode` (`?? 'absolute'`). `toMathStepsContent(values: QuestionValues): Pick<UpdateQuestionRequest,'body'\|'gradingSpec'>` → `{ body: {}, gradingSpec: { acceptedAnswers: values.mathAnswers.map((a) => a.latex.trim()), form: values.mathForm, ...(values.mathTolerance.trim() === '' ? {} : { tolerance: Number(values.mathTolerance), toleranceMode: values.mathToleranceMode }) } }`. |
| W2 | `web/src/features/questions/schemas/mathStepsRules.ts` | module | `export interface MathStepsRuleInput { mathAnswers: { latex: string }[]; mathForm: string; mathTolerance: string }`. `export function addMathStepsIssues(values, issue)`, with the `errorKey` helper as in `essayRules.ts`: `mathAnswers.length === 0` → `['mathAnswers']` `mathAnswersCount`; each blank trimmed latex → `['mathAnswers', i, 'latex']` `'validation.required'`; each trimmed length over `mathAnswerMaxLength` → `['mathAnswers', i, 'latex']` `mathAnswerLength`; when the trimmed `mathTolerance` is not `''`: not a finite number ≥ 0 → `['mathTolerance']` `tolerance` (the existing key), and `mathForm !== 'equivalent'` → `['mathTolerance']` `mathToleranceForm`. |
| W3 | `web/src/features/questions/components/MathStepsFields.tsx` | component | `export function MathStepsFields()` renders, in a `flex flex-col gap-3` column: `<MathAnswersField />`; `SelectField<QuestionValues> name="mathForm"` (label `editor.math.form`, options from `mathAnswerForms` with `t(\`editor.math.forms.${v}\`)`); a `grid gap-3 md:grid-cols-2` with `TextField name="mathTolerance"` (label `editor.math.tolerance`, description `editor.math.toleranceHint`, `dir="ltr"`) and `SelectField name="mathToleranceMode"` (label `editor.math.toleranceMode`, options `editor.short.absolute` / `editor.short.percent`); then a caption `<p className="text-caption text-text-muted">{t('editor.math.stepsNote')}</p>`. |
| W4 | `web/src/features/questions/components/MathAnswersField.tsx` | component | Like `ModelAnswersField`: `useFieldArray<QuestionValues,'mathAnswers'>`, `useWatch` of `mathAnswers`, and a `useFormState` root error. It renders `<fieldset>` with `legend` `editor.math.answersLegend`, a caption hint `editor.math.answersHint`, and per item a `flex flex-col gap-2` block. Each block has a `flex items-start gap-2` row with `TextField name={\`mathAnswers.${i}.latex\`}` (label `editor.math.answer {number}`, `dir="ltr"`) and a ghost sm remove button (`Trash2`, aria-label `editor.math.removeAnswer {number}`, disabled when there is 1), followed by `<MathPreview latex={watched[i]?.latex ?? ''} label={t('editor.math.answerPreview', { number })} />`. Then the root error `<p className="text-caption text-danger">`, and a secondary sm add button (`Plus`, `editor.math.addAnswer`, disabled at `mathAnswersMax`, appends `{ latex: '' }`). |
| W5 | `web/src/features/questions/components/AnswerInputs.tsx` | component | `AnswerInputsProps` = `QuestionViewProps` without `stem`: `{ question, answer, onAnswerChange, disabled?, review?, mathDraftOwner? }`. A `switch (question.type)`: `Mcq`/`Multi`/`TrueFalse` → `ChoiceAnswerInputs` (with `review`); `Fill`/`Short` → `TextAnswerInputs`; `Essay` → `EssayAnswerInput`; `MathSteps` → `MathStepsAnswerInput answer onAnswerChange disabled draftOwner={mathDraftOwner}`. |
| W6 | `web/src/features/questions/components/MathStepsAnswerInput.tsx` | component | Props `{ answer: QuestionAnswer; onAnswerChange: (a: QuestionAnswer) => void; disabled?: boolean \| undefined; draftOwner?: MathDraftOwner \| undefined }`. `change = (value: MathStepsValue) => { onAnswerChange({ ...answer, math: toMathStepsPayload(value) }); }`. `disabled` → `<MathStepsReadOnly solution={answer.math} />`; `draftOwner` → `<MathStepsAnswer owner={draftOwner} initialValue={fromMathStepsPayload(answer.math)} onChange={change} />`; otherwise `<LocalMathStepsInput initial={answer.math} onChange={change} />`, a file-private component: `const [value, setValue] = useState(() => fromMathStepsPayload(initial))`, rendering `<MathStepsInput value={value} onChange={(next) => { setValue(next); onChange(next); }} />`. |
| W7 | `web/src/features/questions/components/MathStepsReadOnly.tsx` | component | Props `{ solution: MathStepsPayload }`. `<div role="group" aria-label={t('view.mathReadOnly')} className="flex flex-col gap-3">`. With no steps, a caption `view.mathNoSteps`; otherwise `<ol className="flex flex-col gap-2">`, where each `<li key={\`step-${index}\`}>` has `<p className="text-caption font-semibold text-text-muted">{t('view.mathStep', { number })}</p>` and `<MathPreview latex={step} label={t('view.mathStep', { number })} />`. Then the `view.mathFinal` caption and `<MathPreview latex={solution.finalAnswer} label={t('view.mathFinal')} />`. |
| W8 | `web/src/features/questions/components/MathAnswerRulesView.tsx` | component | Props `{ answers: QuestionValues['mathAnswers']; form: QuestionValues['mathForm']; tolerance: string; toleranceMode: QuestionValues['mathToleranceMode'] }`. `<section aria-label={t('validation.detail.mathRules')} className={card classes as in ValidationQuestionContent}>` with `h2` `mathRules`, `h3` `mathAccepted`, and `<ol>` of `<li key={\`answer-${number}\`}><MathPreview latex label={t('editor.math.answer', { number })} /></li>`. Then `<p>` `t('validation.detail.mathForm', { form: t(\`editor.math.forms.${form}\`) })`, and, when the trimmed `tolerance !== ''`, `<p dir="ltr">` `t(toleranceMode === 'percent' ? 'validation.detail.mathTolerancePercent' : 'validation.detail.mathTolerance', { tolerance })`. |
| W9 | `web/src/features/quiz/api/mathDraftOwner.ts` | module | `export function mathDraftOwnerFor(studentId: string \| undefined, sessionId: string, questionId: string): MathDraftOwner \| undefined { return studentId ? { studentId, sessionId, questionId } : undefined; }` |
| WT1–WT9 | tests | — | See the Test plan. |

### i18n keys
**questions** (en / ar):
- `types.MathSteps`: Math with steps / رياضيات بالخطوات
- `editor.math.answersLegend`: Accepted final answers / الإجابات النهائية المقبولة
- `editor.math.answersHint`: Write each answer in LaTeX, such as x = 2 or \\frac{1}{2}. Any mathematically equivalent answer is accepted. / اكتب كل إجابة بصيغة LaTeX، مثل x = 2 أو \\frac{1}{2}. تُقبل أي إجابة مكافئة رياضيًا.
- `editor.math.answer`: Accepted answer {number} / الإجابة المقبولة {number}
- `editor.math.answerPreview`: Preview of accepted answer {number} / معاينة الإجابة المقبولة {number}
- `editor.math.addAnswer`: Add answer / أضف إجابة
- `editor.math.removeAnswer`: Remove accepted answer {number} / احذف الإجابة المقبولة {number}
- `editor.math.form`: Required form / الصورة المطلوبة
- `editor.math.forms.equivalent`: Any equivalent form / أي صورة مكافئة
- `editor.math.forms.simplified`: Simplest form / أبسط صورة
- `editor.math.forms.factored`: Factored / محللة إلى عوامل
- `editor.math.forms.expanded`: Expanded / مفكوكة
- `editor.math.forms.exact`: Exact value, no decimals / قيمة مضبوطة بدون كسور عشرية
- `editor.math.tolerance`: Numeric tolerance (optional) / هامش الخطأ العددي (اختياري)
- `editor.math.toleranceHint`: Accepts a numeric answer this close. Works only with "Any equivalent form". / يقبل إجابة عددية قريبة بهذا القدر. يعمل مع «أي صورة مكافئة» فقط.
- `editor.math.toleranceMode`: Tolerance type / نوع الهامش
- `editor.math.stepsNote`: The student's steps are saved with the answer and graded later. / تُحفظ خطوات الطالب مع إجابته، وتُصحَّح لاحقًا.
- `editor.errors.mathAnswersCount`: Add at least one accepted answer. / أضف إجابة مقبولة واحدة على الأقل.
- `editor.errors.mathAnswerLength`: An answer can be at most 200 characters. / يجب ألا تزيد الإجابة على 200 حرف.
- `editor.errors.mathToleranceForm`: A tolerance works only with "Any equivalent form". / هامش الخطأ يعمل مع «أي صورة مكافئة» فقط.
- `view.mathReadOnly`: Your solution / حلّك
- `view.mathStep`: Step {number} / الخطوة {number}
- `view.mathFinal`: Final answer / الإجابة النهائية
- `view.mathNoSteps`: No steps were written. / لم تُكتب خطوات.
- `validation.detail.mathRules`: Final answer check / تصحيح الإجابة النهائية
- `validation.detail.mathAccepted`: Accepted answers / الإجابات المقبولة
- `validation.detail.mathForm`: Required form: {form} / الصورة المطلوبة: {form}
- `validation.detail.mathTolerance`: Tolerance: ±{tolerance} / هامش الخطأ: ±{tolerance}
- `validation.detail.mathTolerancePercent`: Tolerance: ±{tolerance}% / هامش الخطأ: ±{tolerance}%

**quiz**: `feedback.mathAnswer`: Final answer / الإجابة النهائية.

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `QuestionMathAnswersInvalid` | `QUESTION_MATH_ANSWERS_INVALID` | `MathStepsQuestionRules` (via `QuestionFieldsValidator`) | ValidationException | 422 |
| `QuestionMathFormInvalid` | `QUESTION_MATH_FORM_INVALID` | `MathStepsQuestionRules` | ValidationException | 422 |
| `QuestionMathToleranceInvalid` | `QUESTION_MATH_TOLERANCE_INVALID` | `MathStepsQuestionRules` | ValidationException | 422 |
| `QuestionMathToleranceFormConflict` | `QUESTION_MATH_TOLERANCE_FORM_CONFLICT` | `MathStepsQuestionRules` | ValidationException | 422 |
| `MathCheckUnavailable` | `MATH_CHECK_UNAVAILABLE` | `HttpAiMathCheckClient`, `FakeAiMathCheckClient` (Production) | `ServiceUnavailableCoreException` | 503 |
| (reused) `AttemptAnswerTooLong` | `ATTEMPT_ANSWER_TOO_LONG` | Submit answer, save exam answer, grade-draft (`ExceedsLimits`) | `ApplicationValidationCoreException` | 422 |
| (reused) `QuestionAnswerInvalid`, `QuestionBodyInvalid`, `QuestionGradingSpecInvalid` | — | existing paths | — | 422 |

Resource strings (en / ar; the resx Arabic uses plain letters for errors, as its neighbours do; feedback lines follow the `GRADE_FEEDBACK_*` style). The same error text goes into `web/src/shared/i18n`:
- `QUESTION_MATH_ANSWERS_INVALID`: Add one or more accepted final answers, none empty or too long, up to the allowed maximum. / اضف اجابة نهائية مقبولة واحدة او اكثر، غير فارغة وغير طويلة جدا، بما لا يتجاوز الحد المسموح.
- `QUESTION_MATH_FORM_INVALID`: Choose a valid required form. / اختر صورة مطلوبة صحيحة.
- `QUESTION_MATH_TOLERANCE_INVALID`: Enter a tolerance of zero or more together with absolute or percent, or leave both empty. / اكتب هامش خطا صفرا او اكثر مع قيمة مطلقة او نسبة مئوية، او اتركهما فارغين.
- `QUESTION_MATH_TOLERANCE_FORM_CONFLICT`: A tolerance works only with the any-equivalent-form rule. / هامش الخطا يعمل مع قاعدة اي صورة مكافئة فقط.
- `MATH_CHECK_UNAVAILABLE`: The final answer cannot be checked right now. Try again in a moment. / تعذر تصحيح الاجابة النهائية الان. حاول مرة اخرى بعد قليل.
- `GRADE_FEEDBACK_MATH_FINAL_ONLY`: Only the final answer was graded; the steps are graded later. / صُحّحت الإجابة النهائية فقط، وتُصحَّح الخطوات لاحقًا.
- `GRADE_FEEDBACK_MATH_WRONG_FORM`: Write the final answer in the form the question asks for. / اكتب الإجابة النهائية بالصورة التي يطلبها السؤال.
- `GRADE_FEEDBACK_MATH_UNREADABLE`: The final answer could not be read. Write it with math symbols, for example x = 2. / تعذّرت قراءة الإجابة النهائية. اكتبها بالرموز الرياضية، مثل x = 2.
- `GRADE_FEEDBACK_MATH_UNCHECKED`: The final answer could not be checked automatically. / تعذّر التحقق من الإجابة النهائية آليًا.

## Domain behaviour
- No entity changes and no migration: `Questions.Type` is a string column with no check constraint, and specs and answers are jsonb. `Question`, `QuestionRevision`, `Session` and `Attempt` are unchanged. Create, edit, resubmit, approve, reject, retire, versioning, audit and `UpdationDate` behave for MathSteps as for any type.
- `MathStepsGrader.Grade` is the full body in D3, a pure function with no exceptions except the unreachable default arm.
- `QuestionGrader.GradeMathSteps(maxScore, verdict)` = `QuestionGrade.FromNormalised(MathStepsGrader.Grade(verdict), maxScore)`, so scoring and rounding are the #65 rules.
- `QuestionGrader.Grade(QuestionType.MathSteps, …)` still throws `InvalidOperationException("Unsupported question type.")`. Every production call site goes through `AnswerGrader`.
- `ServableQuestionSpecification.ServedTypes` = `[Mcq, Multi, TrueFalse, Fill, Short, MathSteps]`. The existing test `ServedTypes_EveryTypeExceptEssay` stays true.
- Feedback persistence: `Attempt.Grade` stores `GradeFeedback` as JSON with a camelCase `kind`, so the 4 new kinds round-trip without a migration.

## API surface
**.NET:** no new routes or policies. Changed behaviour:
- `POST/PUT /api/questions`, `PUT /api/questions/{id}/resubmit` (`ContentManage`) accept `type: "MathSteps"` with the body and spec above. The errors are the 422 codes above.
- `POST /api/questions/grade-draft` (`ContentManage`) grades MathSteps through `IAiMathCheckClient`: 200 `QuestionGradeResult` (feedback localised), 422 `ATTEMPT_ANSWER_TOO_LONG`, 503 `MATH_CHECK_UNAVAILABLE`.
- `POST /api/sessions/{id}/answers` (student) accepts the math answer: 422 `QUESTION_ANSWER_INVALID` / `ATTEMPT_ANSWER_TOO_LONG`, 503 `MATH_CHECK_UNAVAILABLE` (no attempt saved).
- `PUT /api/exams/{id}/answers/{questionId}` stores the canonical math answer: 422 as above.
- `POST /api/exams/{id}/submit` grades math items through the CAS: 503 `MATH_CHECK_UNAVAILABLE` (nothing saved).
- `GET /api/exam-blueprints/subjects/{id}`: `servable` arrays list 6 types.
- OpenAPI: `QuestionType` gains `MathSteps`.

Math request body (Postman and Q1):
```json
{"lessonId":"…","type":"MathSteps","stem":"<p>Solve 2x + 3 = 7.</p>","body":{"x":1},
 "gradingSpec":{"acceptedAnswers":["  x = 2 ","2"],"form":"equivalent","tolerance":0.01,"toleranceMode":"absolute"},
 "explanation":"<p>Subtract 3, divide by 2.</p>","difficulty":"Medium","tags":[],"maxScore":2}
```
Canonical: body `{}`, spec `{"acceptedAnswers":["x = 2","2"],"form":"equivalent","tolerance":0.01,"toleranceMode":"absolute"}`.

**ai:** `POST /v1/math-checks` (Bearer service token, operationId `grading_create_math_check`).
Request `{"answer":"x=\\frac{4}{2}","expected":["x = 2"],"form":"equivalent","tolerance":null,"toleranceMode":null}`. `200 {"verdict":"equivalent","matchedIndex":0,"invalidExpected":[]}`. `400 VALIDATION_FAILED` (schema or limits) and `401 UNAUTHENTICATED`, both problem+json. The endpoint never returns 5xx for CAS work (D12).

## Docs
- **`docs/question-schemas.md`**:
  - Types: add `MathSteps` (v2), "steps + final answer; the final answer is checked by the CAS (#122), the steps by #123".
  - Per-type shapes: a **MathSteps** block with body `{}` and the canonical spec above; the forms (D5), tolerance (D6) and accepted-answer rules; a pointer to `math-cas.md` for notation.
  - Rules table: 4 rows (codes and caps).
  - Servable: `ServedTypes` lists six types.
  - Answer shapes: **MathSteps** `{"steps":["2x = 4"],"finalAnswer":"x = 2"}` with the D2/D3 caps (`Sessions:Math*`, 422 `ATTEMPT_ANSWER_TOO_LONG`).
  - Grading: a MathSteps bullet (D7, D8, D14 and D15) and the four feedback lines, noting that the wrong-form line tells the student to change the form.
  - Changing a schema: "MathSteps is not importable (PRD §10.1)."
- **`docs/math-cas.md`** (new): Purpose, the contract pointer to ai-service.md, the Notation table (D20 and P3 lists), lists / `\pm` / sets (D19), relations and assignments (D18), the equivalence algorithm (P7 and D21), tolerance (D6), forms and their limitation (D5), verdicts (D8), the Safety section (D10–D12: no string evaluation, whitelist grammar, limits table with the `ELMANHG_AI_CAS_*` settings, process pool, timeout, memory cap), and the .NET fake (D16).
- **`docs/math-input.md`**:
  - §1: the component is mounted in quiz, exam and the admin preview, and the type exists (#122).
  - §2: the limits are now server-enforced (`Sessions:MathStepsMaxCount`, `MathStepMaxLength`, `MathFinalAnswerMaxLength`).
  - §6: a restored draft is sent to `onChange` once on mount; quiz and exam clear drafts after a successful submit (D23).
  - §7: replace the checklist with "Integration (done in #122)", listing where each step lives.
- **`docs/ai-service.md`**:
  - Role: add "checks math final answers with SymPy".
  - A new section `### POST /v1/math-checks`: request and response, fields, limits table, and "never 5xx for CAS work".
  - Configuration: the 12 `ELMANHG_AI_CAS_*` rows and `AiService:MathCheckTimeoutSeconds`, plus a nesting sentence (D30).
  - Errors: the .NET side maps math-check failures to `MATH_CHECK_UNAVAILABLE`.
  - Fakes: `FakeAiMathCheckClient` (D16); the Python side has no fake because SymPy runs locally.
- **`docs/sessions.md`**:
  - "Grading against the served version": math goes through `AnswerGrader` → CAS; a 503 saves nothing.
  - Options: the 3 new keys plus `AnswerMaxLength` 24000.
  - Error codes: `MATH_CHECK_UNAVAILABLE`.
- **`docs/exams.md`** Submission / Auto-submit: math items are checked at submit. A 503 leaves the exam unsubmitted; the worker retries.
- **`docs/exam-blueprints.md`** line 74: "(the five v1 types; zeros included)" → "(the six served types: the five v1 types and MathSteps; zeros included)".
- **`docs/question-import.md`** line 25: add "`MathSteps` (v2) is authored in the editor only."
- **`docs/PRD.md`** §6, after the table: "Math with steps: until step grading (E15.S3) the score is the final answer's CAS verdict (1 or 0); each question sets a required form and an optional numeric tolerance (docs/question-schemas.md, docs/math-cas.md)."
- **`docs/claude-design-prompt.md`** §4:
  - Line 135 (quiz) and line 136 (exam): "a math-with-steps question shows the step editor with keypad, LaTeX preview and on-device draft (docs/math-input.md)".
  - Line 145 (teacher): "a math question also shows its accepted answers, required form and tolerance".
  - Line 153 (editor): "math with steps (v2) has one or more accepted final answers in LaTeX with preview, a required form and an optional tolerance, and its preview uses the student step editor with «جرّب الإجابة» running the CAS check".
- **`docs/prototype.md`**: after line 69, add "The built app grades the math final answer with a SymPy CAS check (equivalent forms, tolerance, form rules; #122); steps are stored for step grading (#123)."

## Test plan
.NET tests use FluentAssertions (the repo standard), `TestContext.Current.CancellationToken` and `QuestionBuilder.Json`. Python tests use `test_<unit>_<scenario>_<expected>`. Web tests use `renderApp`/`renderWithProviders`, MSW and `userEvent.setup()`.

### .NET — Domain
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| D1 | `MathStepsGraderTests` | `Grade_NullVerdict_ReturnsUnanswered` | Equals `NormalisedGrade.Unanswered`. |
| D2 | same | `Grade_Equivalent_ReturnsFullWithFinalOnlyFeedback` | `(1m, GradeFeedback.MathFinalAnswerOnly)`. |
| D3 | same | `Grade_NotEquivalent_ReturnsZeroWithFinalOnlyFeedback` | `(0m, MathFinalAnswerOnly)`. |
| D4 | same | `Grade_WrongForm_ReturnsZeroWithWrongFormFeedback` | `(0m, MathWrongForm)`. |
| D5 | same | `Grade_Unreadable_ReturnsZeroWithUnreadableFeedback` | `(0m, MathUnreadable)`. |
| D6 | same | `Grade_Unchecked_ReturnsZeroWithUncheckedFeedback` | `(0m, MathUnchecked)`. |
| D7 | `ServableQuestionSpecificationTests` (add) | `IsSatisfiedBy_ApprovedMathStepsInPublishedLesson_ReturnsTrue` | The builder `.MathSteps().Approved()` with a published lesson → `true`. |
| D8 | `QuestionGraderTests` (add) | `GradeMathSteps_Equivalent_ScalesToMaxScore` | `GradeMathSteps(4, Equivalent)` → score 4, normalised 1, `Correct`, feedback `MathFinalAnswerOnly`. |
| D9 | same (add) | `Grade_MathSteps_ThrowsInvalidOperationException` | `Grade(MathSteps, MathStepsSpecJson, 2, Json("{}"))` throws. |

### .NET — Application
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| A1 | `MathStepsQuestionRulesTests` | `Validate_ValidSpec_ReturnsNoErrors` | Body `{}` with `MathStepsSpecJson` → empty. |
| A2 | same | `Validate_BodyNotObject_ReturnsQuestionBodyInvalid` | Body `[]`. |
| A3 | same | `Validate_SpecNotObject_ReturnsQuestionGradingSpecInvalid` | Spec `"x"`. |
| A4 | same | `Validate_NoAcceptedAnswers_ReturnsQuestionMathAnswersInvalid` | `"acceptedAnswers":[]`. |
| A5 | same | `Validate_BlankAcceptedAnswer_ReturnsQuestionMathAnswersInvalid` | `["  "]`. |
| A6 | same | `Validate_AcceptedAnswerTooLong_ReturnsQuestionMathAnswersInvalid` | `_options.QuestionAnswerMaxLength = 3`, `["x = 2"]`. |
| A7 | same | `Validate_TooManyAcceptedAnswers_ReturnsQuestionMathAnswersInvalid` | `QuestionAcceptedAnswersMaxCount = 1`, 2 answers. |
| A8 | same | `Validate_UndefinedFormNumber_ReturnsQuestionMathFormInvalid` | `"form":7`. |
| A9 | same | `Validate_UnknownFormName_ReturnsQuestionGradingSpecInvalid` | `"form":"fancy"`. |
| A10 | same | `Validate_NegativeTolerance_ReturnsQuestionMathToleranceInvalid` | `-0.1` with `absolute`. |
| A11 | same | `Validate_ToleranceWithoutMode_ReturnsQuestionMathToleranceInvalid` | `"tolerance":0.1`. |
| A12 | same | `Validate_ModeWithoutTolerance_ReturnsQuestionMathToleranceInvalid` | `"toleranceMode":"percent"`. |
| A13 | same | `Validate_ToleranceWithFactoredForm_ReturnsQuestionMathToleranceFormConflict` | `form` factored, tolerance 0.1 absolute. Contains the code, not `…ToleranceInvalid`. |
| A14 | same | `Validate_ToleranceWithoutForm_ReturnsNoErrors` | Tolerance 1 percent, no `form` → empty. |
| A15 | same | `Normalize_Spec_TrimsAnswersDefaultsFormAndDropsUnknownFields` | Input body `{"x":1}`, spec `{"acceptedAnswers":["  x = 2 "],"extra":1}` → body `"{}"`, spec `JsonNode.DeepEquals` `{"acceptedAnswers":["x = 2"],"form":"equivalent"}`. |
| A16 | same | `Normalize_WithTolerance_KeepsToleranceAndMode` | → `…"tolerance":0.01,"toleranceMode":"absolute"`. |
| M1 | `MathStepsAnswerRulesTests` | `CanRead_ValidShape_ReturnsTrue` | `{"steps":["a"],"finalAnswer":"x"}` → true. |
| M2 | same | `CanRead_MissingFields_ReturnsTrue` | `{}` → true. |
| M3 | same | `CanRead_NumberFinalAnswer_ReturnsFalse` | `{"finalAnswer":5}` → false. |
| M4 | same | `CanRead_NullStep_ReturnsFalse` | `{"steps":[null]}` → false. |
| M5 | same | `Canonicalize_TrimsAndDropsBlankSteps` | `{"steps":[" 2x = 4 ","  "],"finalAnswer":" x = 2 "}` → `{"steps":["2x = 4"],"finalAnswer":"x = 2"}`. |
| M6 | same | `ExceedsLimits_TooManySteps_ReturnsTrue` | 21 steps, default `SessionsOptions`. |
| M7 | same | `ExceedsLimits_StepTooLong_ReturnsTrue` | One step of 501 chars. |
| M8 | same | `ExceedsLimits_FinalAnswerTooLong_ReturnsTrue` | Final answer of 201 chars. |
| M9 | same | `ExceedsLimits_WithinLimitsOrOtherType_ReturnsFalse` | A 20-step valid answer → false. `ExceedsLimits(Short, {"text":…})` → false. |
| AG1 | `AnswerGraderTests` | `GradeAsync_NonMathType_UsesDeterministicGraderWithoutClient` | Mcq correct → `Correct`. `CheckAsync` `DidNotReceive()`. |
| AG2 | same | `GradeAsync_BlankFinalAnswer_ReturnsUnansweredWithoutClient` | `{"steps":["x"],"finalAnswer":"  "}` → normalised 0, feedback `Unanswered`. Client `DidNotReceive()`. |
| AG3 | same | `GradeAsync_MathAnswer_SendsTrimmedFinalAnswerAndRules` | Spec with tolerance 0.01 absolute; client returns `Equivalent`. `Received(1)` with `Answer == "x = 2"`, `Expected` equal `["x = 2"]`, `Form == Equivalent`, `Tolerance == 0.01m`, `ToleranceMode == Absolute`. The result is `Correct`, score 2. |
| AG4 | same | `GradeAsync_WrongFormVerdict_ReturnsZeroWithWrongFormFeedback` | `WrongForm` → normalised 0, `MathWrongForm`. |
| AG5 | same | `GradeAsync_ClientUnavailable_Propagates` | The client throws `ServiceUnavailableCoreException(MathCheckUnavailable)` → the same exception and code. |
| F1 | `MathGradeFeedbackTextTests` | `Localize_MathKinds_UseTheirKeys` | `[Theory]` over the 4 kinds and keys: the localiser stub returns the key, and the result equals the key. |
| G1 | `GradeQuestionDraftHandlerTests` (add) | `Handle_MathStepsEquivalent_ReturnsCorrectWithFeedback` | The client returns `Equivalent`, and the localiser returns `"final"` for the key. Result: `Correct`, score 2, feedback `"final"`. |
| G2 | same (add) | `Handle_MathStepsTooManySteps_ThrowsAttemptAnswerTooLong` | 21 steps → `ApplicationValidationCoreException` `AttemptAnswerTooLong`. Client `DidNotReceive()`. |
| S1 | `SubmitAnswerHandlerTests` (add) | `Handle_MathStepsAnswer_RecordsGradeFromMathCheck` | The served revision is MathSteps (QuestionBuilder `.MathSteps()`); the client returns `Equivalent`. The result attempt is `Correct`, the attempt answer is canonical (steps trimmed), and `SaveChangesAsync` `Received(1)`. |
| S2 | same (add) | `Handle_MathStepsAnswerOverLimits_ThrowsAttemptAnswerTooLong` | 21 steps → code; `SaveChangesAsync` `DidNotReceive()`. Client `DidNotReceive()`. |
| S3 | same (add) | `Handle_MathCheckUnavailable_ThrowsAndDoesNotSave` | The client throws 503 → `ServiceUnavailableCoreException` `MathCheckUnavailable`; `SaveChangesAsync` `DidNotReceive()`. |
| E1 | `SaveExamAnswerHandlerTests` (add) | `Handle_MathStepsAnswerOverLimits_ThrowsAttemptAnswerTooLong` | The code; `SaveChangesAsync` `DidNotReceive()`. |
| E2 | same (add) | `Handle_MathStepsAnswer_SavesCanonicalAnswer` | The item's `SavedAnswer` equals the canonical JSON; `SaveChangesAsync` `Received(1)`. |
| E3 | `SubmitExamHandlerTests` (add) | `Handle_SavedMathStepsAnswer_GradesWithMathCheck` | The client returns `Equivalent` for the math item. Its attempt is `Correct`; the client is `Received(1)`. |
| E4 | `AutoSubmitExamHandlerTests` (add) | `Handle_MathCheckUnavailable_LeavesExamUnsubmitted` | The client throws 503 → the exception propagates, `SubmittedAt` stays null, and `SaveChangesAsync` `DidNotReceive()`. |
| V1 | `AvatarAnswerTextTests` (add) | `StudentAnswer_MathSteps_ListsStepsAndFinalAnswer` | `{"steps":["2x = 4"],"finalAnswer":"x = 2"}` → `"2x = 4\nالإجابة النهائية: x = 2"`. |
| V2 | same (add) | `CorrectAnswer_MathSteps_ReturnsFirstAcceptedAnswer` | → `"x = 2"`. |
| — | `GetSubjectExamBlueprintsHandlerTests` (modify) | existing test at line 47 | `HaveCount(6)`. |

### .NET — Infrastructure
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| H1 | `HttpAiMathCheckClientTests` | `CheckAsync_Success_PostsCamelCaseBodyWithBearerAndMapsReply` | POST `http://ai.test/v1/math-checks` with Bearer. The body has `answer`, `expected`, `form:"factored"`, and **omits** `tolerance`/`toleranceMode` when they are null. Reply `{"verdict":"notEquivalent","matchedIndex":null,"invalidExpected":[1]}` → `AiMathCheckResult(NotEquivalent, null, [1])`. |
| H2 | same | `CheckAsync_WithTolerance_SendsToleranceAndMode` | The body has `tolerance:0.01` and `toleranceMode:"absolute"`. |
| H3 | same | `CheckAsync_Non2xx_ThrowsMathCheckUnavailable` | 503 stub → the code. |
| H4 | same | `CheckAsync_TransportFailure_ThrowsMathCheckUnavailable` | `HttpRequestException` is the inner exception. |
| H5 | same | `CheckAsync_InvalidReply_ThrowsMathCheckUnavailable` | `[Theory]` over `"not json"`, `"{}"`, `{"verdict":"maybe","invalidExpected":[]}`, `{"verdict":99,"invalidExpected":[]}` and `{"verdict":"equivalent"}`. |
| K1 | `FakeAiMathCheckClientTests` | `CheckAsync_WhitespaceDifferent_ReturnsEquivalentWithIndex` | Expected `["x = 3", "x = 2"]`, answer `"x=2"` → `Equivalent`, index 1. |
| K2 | same | `CheckAsync_DifferentAnswer_ReturnsNotEquivalent` | → `NotEquivalent`, null. |
| K3 | same | `CheckAsync_ArabicDigitsAndLeftRight_Normalised` | `"x=\left(٢\right)"` vs `"x = (2)"` → `Equivalent`. (Build the Arabic digit with `(char)0x0662`.) |
| K4 | same | `CheckAsync_Production_ThrowsMathCheckUnavailable` | The environment is Production → 503 code. |
| R1 | `AiServiceServiceCollectionExtensionsTests` (add) | `AddAiService_Fake_ResolvesFakeMathCheckClient` | Resolves `FakeAiMathCheckClient`. |
| R2 | same (add) | `AddAiService_Http_ResolvesHttpMathCheckClient` | Resolves `HttpAiMathCheckClient`. |

### .NET — Integration (Testcontainers; `AiService:Provider=Fake`)
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| Q1 | `MathStepsQuestionEndpointTests` | `Post_MathStepsQuestion_StoresCanonicalSpec` | The admin POSTs the API-surface body → 201. The DB `Body` is `{}` and `GradingSpec` deep-equals the canonical spec. |
| Q2 | same | `Post_ToleranceWithFactoredForm_Returns422ToleranceFormConflict` | problem+json `code` = `QUESTION_MATH_TOLERANCE_FORM_CONFLICT`. Nothing is stored. |
| Q3 | same | `Post_GradeDraftMathSteps_ReturnsCorrectWithFeedback` | `answer {"steps":["2x = 4"],"finalAnswer":"x=2"}` → 200, `outcome` `Correct`, and `feedback` is the Arabic `GRADE_FEEDBACK_MATH_FINAL_ONLY` text. |
| Q4 | same | `Post_GradeDraftMathSteps_AsStudent_Returns403` | Student token → 403. |
| I1 | `MathStepsAnswerEndpointTests` | `Post_EquivalentFinalAnswer_RecordsCorrectAttempt` | 200. `attempt.outcome` is `Correct` and `attempt.answer.steps` is `["2x = 4"]`. The DB attempt `Score` is 2. |
| I2 | same | `Post_TooManySteps_Returns422AttemptAnswerTooLong` | 21 steps → 422 with that code. No attempt row. |
| I3 | same | `Post_FinalAnswerNotString_Returns422QuestionAnswerInvalid` | `{"finalAnswer":5}` → 422 `QUESTION_ANSWER_INVALID`. |
| X1 | `MathStepsExamEndpointTests` | `Put_MathStepsAnswer_StoresCanonicalAnswer` | PUT `{"answer":{"steps":[" 2x = 4 "],"finalAnswer":" x=2 "}}` → 200. The DB item `SavedAnswer` deep-equals `{"steps":["2x = 4"],"finalAnswer":"x=2"}`. |
| X2 | same | `Submit_SavedMathStepsAnswer_GradesFinalAnswer` | Save the math answer, then submit → 200. The math item's attempt is `Correct` and the DB attempt count is 1 or more for the math question. |

### ai (pytest)
| # | File | Test | Asserts |
|---|------|------|---------|
| PT1a | `tests/unit/test_cas_lexer.py` | `test_tokenize_arabic_digits_returns_latin_numbers` | `"٢٫٥"` → `[Token("number","2.5")]`. |
| PT1b | same | `test_tokenize_unicode_operators_returns_commands` | `"×÷≤≥≠±π√−"` → the command/symbol kinds and texts in order. |
| PT1c | same | `test_tokenize_text_group_is_removed` | `"2\\text{ سم}"` → `[number 2]`. |
| PT1d | same | `test_tokenize_left_right_and_spacing_are_skipped` | `"\\left( x \\, \\right)"` → `(`, name x, `)`. |
| PT1e | same | `test_tokenize_subscript_name_returns_single_name` | `"x_1+x_{12}"` → names `x_1`, `x_12`. |
| PT1f | same | `test_tokenize_unknown_command_raises` | `"\\input{a}"` raises `MathParseError`. |
| PT1g | same | `test_tokenize_unexpected_character_raises` | Parametrised: `"__import__"`, `"x;"`, `"٢ سم"`. |
| PT1h | same | `test_tokenize_too_many_tokens_raises` | 301 tokens with default limits. |
| PT1i | same | `test_tokenize_long_number_raises` | 31 digits. |
| PT2a | `tests/unit/test_cas_parser.py` | `test_parse_implicit_multiplication_builds_product` | `"2x(x+1)"` → evaluated `2*x*(x+1)` expanded equals `2x²+2x`. |
| PT2b | same | `test_parse_frac_and_nth_root_evaluate` | `"\\frac{6}{4}"` → 3/2 with `has_unreduced_fraction`; `"\\sqrt[3]{27}"` → 3. |
| PT2c | same | `test_parse_tex_single_token_exponent_splits_digits` | `"x^23"` → `3*x**2`. |
| PT2d | same | `test_parse_relation_and_assignment_detected` | `"2 = x"` → `assignment` returns `(x, 2, …)`; `"x > 3"` has operator `GT`. |
| PT2e | same | `test_parse_chained_relation_raises` | `"x = 1 = 2"`. |
| PT2f | same | `test_parse_list_and_plus_minus_expand` | `"x = \\pm 3"` → 2 elements; `"\\{2, -3\\}"` → 2 elements. |
| PT2g | same | `test_parse_invalid_structures_raise` | Parametrised with ids: `"(2, 3)"`, `"\\pm1\\pm2"`, `"x +"`, `"\\frac{1}{"`, `"x^2^3"`, `""`. |
| PT2h | same | `test_parse_depth_limit_raises` | 31 nested parentheses. |
| PT2i | same | `test_parse_power_tower_raises` | `"2^{2^{2^{10}}}"`. |
| PT2j | same | `test_parse_constants_and_logarithms` | `"e"` → `E`; `"\\log 100"` → 2; `"\\log_2 8"` → 3; `"\\ln e"` → 1; `"\\sin^2 x"` → `sin(x)**2`. |
| PT2k | same | `test_parse_decimal_sets_flag_and_is_exact` | `"0.1"` → `Rational(1,10)`, `has_decimal` True. |
| PT3 | `tests/unit/test_cas_equivalence.py` | `test_evaluate_cases_return_expected_verdict` | `@pytest.mark.parametrize` with readable ids over the table below. It calls `evaluate(CasRequest(...))` with default `CasLimits`, and asserts `verdict`, `matched_index` and `invalid_expected`. |
| PT4 | `tests/unit/test_cas_sources_safe.py` | `test_cas_sources_never_evaluate_strings` | For every `cas/*.py`, the source contains none of `sympify`, `parse_expr`, `parse_latex`, `eval(`, `exec(`, `S(\"`, `lambdify`. |
| PT5a | `tests/unit/test_cas_pool.py` | `test_cas_pool_check_returns_worker_outcome` | `CasPool(settings with cas_workers=1)`: `"x+x"` vs `["2x"]` → `EQUIVALENT`; `close()` afterwards. |
| PT5b | same | `test_cas_pool_timeout_returns_unchecked_and_discards_pool` | `cas_timeout_seconds=0.001` → `UNCHECKED` and `pool.started is False`. |
| PT5c | same | `test_cas_pool_close_is_idempotent` | `close()` twice raises nothing, and `started is False`. |
| PT6a | `tests/unit/test_math_check_schemas.py` | `test_math_check_in_valid_payload_parses` | camelCase aliases; form defaults to `equivalent`. |
| PT6b | same | `test_math_check_in_invalid_payload_reports_loc` | Parametrised: empty `answer` (loc `answer`), empty `expected`, blank expected item, negative tolerance, extra field, tolerance without mode, mode without tolerance, tolerance with `factored`. |
| PT7a | `tests/unit/test_math_check_pipeline.py` | `test_run_limits_exceeded_raise_validation_failed` | Parametrised: answer 501 chars → `answer`/`TOO_LONG`; 21 expected → `expected`/`TOO_MANY_ITEMS`; expected item 501 → `expected[0]`/`TOO_LONG`. |
| PT7b | same | `test_run_passes_request_to_checker_and_returns_result` | The stub checker records the `CasRequest`: tolerance mapped (`Decimal("0.01")`, `ABSOLUTE`), limits from settings, and the result mirrors the stub. |
| PT7c | same | `test_run_logs_verdict_without_answer_text` | `log_capture`: the `math_check.completed` event has `verdict`, `answer_chars` and `latency_ms`, and no value in the event contains the answer text. |
| PT8a | `tests/unit/test_cas_settings.py` | `test_settings_cas_defaults` | The 12 defaults. |
| PT8b | same | `test_settings_cas_out_of_range_rejected` | Parametrised: `cas_timeout_seconds=0`, `cas_workers=0`, `cas_worker_memory_mb=100`. |
| PT9a | `tests/integration/test_math_checks_endpoint.py` | `test_math_checks_equivalent_answer_returns_verdict` | 200 `{"verdict":"equivalent","matchedIndex":0,"invalidExpected":[]}` for `"x=\\frac{4}{2}"` vs `["x = 2"]`. |
| PT9b | same | `test_math_checks_unreadable_answer_returns_200_unreadable` | `"x +"` → 200 `unreadable`. |
| PT9c | same | `test_math_checks_missing_token_returns_401_problem` | 401 problem+json `UNAUTHENTICATED`. |
| PT9d | same | `test_math_checks_invalid_body_returns_400_problem` | Tolerance without mode → 400 `VALIDATION_FAILED`, problem+json. |

**PT3 table** (answer | expected | form | tolerance | → verdict, matched, invalid). The default form is `equivalent`, with no tolerance; matched is 0 and invalid is `()` unless stated:
1. `2x+3` \| `3+2x` → equivalent
2. `0.5` \| `\frac{1}{2}` → equivalent
3. `\frac{x^2-1}{x-1}` \| `x+1` → equivalent
4. `2\sqrt{2}` \| `\sqrt{8}` → equivalent
5. `(x+1)^2` \| `x^2+2x+1` → equivalent
6. `x = 2` \| `2 = x` → equivalent
7. `2` \| `x = 2` → equivalent
8. `\sin^2 x + \cos^2 x` \| `1` → equivalent
9. `2, -3` \| `-3, 2` → equivalent
10. `x = \pm 3` \| `x = 3, x = -3` → equivalent
11. `\log_{2} 8` \| `3` → equivalent
12. `x > 3` \| `3 < x` → equivalent
13. `2x + 4 = 0` \| `x + 2 = 0` → equivalent
14. `٢x` \| `2x` → equivalent
15. `x \times y` \| `xy` → equivalent
16. `\frac{6}{4}` \| `\frac{3}{2}` → equivalent
17. `\pi r^2` \| `r^2\pi` → equivalent
18. `\sqrt[3]{27}` \| `3` → equivalent
19. `2 \text{ سم}` \| `2` → equivalent
20. `\left(x+1\right)(x-1)` \| `x^2-1` → equivalent
21. `e^{\ln 5}` \| `5` → equivalent
22. `\{2, -3\}` \| `2, -3` → equivalent
23. `x = 3` \| `x = 2` → notEquivalent
24. `2x = 4` \| `x = 2` → notEquivalent
25. `y = 2` \| `x = 2` → notEquivalent
26. `x \ge 3` \| `x > 3` → notEquivalent
27. `2, 3` \| `2, -3` → notEquivalent
28. `2` \| `2, 3` → notEquivalent
29. `\sqrt{x^2}` \| `x` → notEquivalent
30. `1.41` \| `\sqrt{2}` → notEquivalent
31. `1.41` \| `\sqrt{2}` \| tolerance 0.01 absolute → equivalent
32. `1.3` \| `\sqrt{2}` \| 0.01 absolute → notEquivalent
33. `9.9` \| `10` \| 1 percent → equivalent (inclusive bound)
34. `x = 1.414` \| `x = \sqrt{2}` \| 0.001 absolute → equivalent
35. `x^2 + 2x + 1` \| `(x+1)^2` \| factored → wrongForm
36. `(x+1)(x+1)` \| `(x+1)^2` \| factored → equivalent
37. `x(x+1)` \| `x^2+x` \| factored → equivalent
38. `2(x+1)` \| `2x+2` \| expanded → wrongForm
39. `0.5` \| `\frac{1}{2}` \| exact → wrongForm
40. `\frac{6}{4}` \| `\frac{3}{2}` \| simplified → wrongForm
41. `2+3` \| `5` \| simplified → wrongForm
42. `\frac{3}{2}` \| `\frac{3}{2}` \| simplified → equivalent
43. `x +` \| `2` → unreadable, matched None
44. `\input{/etc/passwd}` \| `2` → unreadable
45. `__import__('os')` \| `2` → unreadable
46. `2^{2^{2^{10}}}` \| `2` → unreadable
47. a 31-digit number \| `2` → unreadable
48. `2` \| [`x +`] → unchecked, matched None, invalid `(0,)`
49. `2` \| [`x +`, `2`] → equivalent, matched 1, invalid `(0,)`

### web (Vitest)
| # | File | `it(...)` | Asserts |
|---|------|-----------|---------|
| WT1a | `features/questions/api/mathStepsValues.test.ts` | `reads a stored spec into editor values` | Spec with tolerance 0.01 absolute and form factored → the matching values. |
| WT1b | same | `writes values to a request, omitting an empty tolerance` | `toMathStepsContent` output: body `{}`, trimmed answers; with `mathTolerance ''` there are no tolerance keys. |
| WT1c | same | `returns no values for an unreadable spec` | `readMathSteps({})` → `{}`. |
| WT2a | `features/questions/schemas/mathStepsRules.test.ts` | `accepts a valid math question` | `questionEditorSchema.safeParse` succeeds. |
| WT2b | same | `requires at least one non-blank answer` | The `mathAnswers` / `mathAnswers.0.latex` paths and messages. |
| WT2c | same | `rejects an answer over 200 characters` | `mathAnswerLength`. |
| WT2d | same | `rejects a negative or non-numeric tolerance` | Path `mathTolerance`, message `…tolerance`. |
| WT2e | same | `rejects a tolerance with a form other than any equivalent` | `mathToleranceForm`. |
| WT3a | `features/questions/pages/NewMathStepsQuestion.test.tsx` | `creates a math question with answers, form and tolerance` | The admin selects "Math with steps (v2)", fills the stem, answer `x = 2`, form and tolerance 0.01, and saves. The captured POST body has `type 'MathSteps'`, `body {}` and the spec with `acceptedAnswers ['x = 2']`, `form 'equivalent'`, `tolerance 0.01` and `toleranceMode 'absolute'`. |
| WT3b | same | `grades the preview answer through grade-draft` | The admin types a final answer in the preview's final-answer textbox and presses "Try the answer". The captured request `answer` equals `{steps: [], finalAnswer: 'x=2'}`. The result panel shows the mocked feedback text. |
| WT3c | same | `shows the server tolerance-form conflict under the tolerance field` | The mock returns 422 `QUESTION_MATH_TOLERANCE_FORM_CONFLICT`. The inline error is under "Numeric tolerance (optional)". |
| WT4 | `features/questions/pages/ValidationMathStepsQuestion.test.tsx` | `shows accepted answers, form and tolerance to the teacher` | The section "Final answer check" lists 2 answer previews, "Required form: Factored" and no tolerance line; the answer-key preview shows the read-only final answer. |
| WT5a | `features/quiz/api/quizItem.mathSteps.test.ts` | `reads a math answer payload` | `fromAnswerPayload` → `math` equals the payload; a garbage payload → `emptyAnswer()`. |
| WT5b | same | `treats a blank final answer as empty` | Steps with a blank final → `true`; final `x=2` → `false`. |
| WT5c | same | `accepts a MathSteps item` | `toQuizQuestion(quizItem(1,{type:'MathSteps', body:{}}))` has type `MathSteps`. |
| WT6 | `features/quiz/api/correctAnswer.mathSteps.test.ts` | `describes the first accepted answer as math` | `{kind:'math', latex:'x = 2'}`; `choiceReview` → `undefined`. |
| WT7a | `features/quiz/pages/QuizPage.mathSteps.test.tsx` | `sends steps and final answer on check` | A math item: the student types into "Step 1" and "Final answer", then presses Check. The captured body `answer` equals `{steps:['2x = 4'], finalAnswer:'x = 2'}`. |
| WT7b | same | `sends a restored draft without any edit` | Pre-seed `localStorage` key `elmanhg.mathDraft.s1.<sessionId>.<questionId>`; press Check → the body has the draft's answer (#224). |
| WT7c | same | `clears the draft after a successful check` | After the submit resolves, the `localStorage` key is `null`, and the read-only "Your solution" group is shown with the correct-answer preview. |
| WT7d | same | `keeps the draft when the check fails` | The submit mock returns 503 `MATH_CHECK_UNAVAILABLE` → a toast with its text; the key still exists and the input is editable again. |
| WT8a | `features/exam/pages/ExamPage.mathSteps.test.tsx` | `autosaves the math answer` | Typing the final answer → the PUT body `answer.finalAnswer` is `x = 2`. |
| WT8b | same | `autosaves a restored draft on open` | A pre-seeded draft → a PUT with the draft answer, with no typing (#224). |
| WT8c | same | `clears math drafts after submitting` | Confirm submit → after navigation, the draft key is `null`. |
| WT9a | `features/mathSteps/components/MathStepsAnswer.restoreNotify.test.tsx` | `reports a restored draft to onChange once` | With a stored draft, `onChange` is called once with the draft value (asserted on the rendered parent echo, e.g. a sibling `<output>` showing `finalAnswer`). |
| WT9b | same | `does not call onChange on mount without a draft` | The parent echo stays empty. |
| — | `features/blueprints/api/blueprintValues.test.ts` (modify) | two existing tests | `MathSteps: '0'` added; the type list ends with `'MathSteps'`. |

## Definition of done
- [ ] `QuestionType.MathSteps` exists. Create, update and resubmit validate and normalise the spec as in A4 (4 new codes, resx en/ar, web shared i18n).
- [ ] MathSteps is servable, and `ServedTypes` has 6 entries. The import still ignores it.
- [ ] Quiz answer, exam submit, auto-submit and grade-draft grade MathSteps only through `AnswerGrader` → `IAiMathCheckClient`. A blank final answer never calls the client.
- [ ] A 503 `MATH_CHECK_UNAVAILABLE` saves nothing: no attempt, and no exam submission.
- [ ] The math answer caps come from `SessionsOptions`, and `AnswerMaxLength` is 24000 (code, example, ApiFactory).
- [ ] `FakeAiMathCheckClient` is the default, and `HttpAiMathCheckClient` posts to `v1/math-checks` with the Bearer token and its own timeout (15 s, no POST retry).
- [ ] The ai `POST /v1/math-checks` exists, service-token protected, with problem+json 400/401, and never returns 5xx for CAS work.
- [ ] No SymPy string entry point (`sympify`/`parse_expr`/`parse_latex`/`eval`/`exec`/`lambdify`) appears in `ai/src/elmanhg_ai/cas`, and PT4 enforces it.
- [ ] Input size, token, depth, digit, exponent and element limits are settings. The worker pool has a hard timeout (terminate + lazy recreate) and `RLIMIT_AS` on POSIX.
- [ ] Every PT3 row passes, including every form and tolerance rule.
- [ ] `sympy==1.14.0` is added with `uv.lock` updated and a mypy override. `uv sync --locked`, `ruff format --check`, `ruff check`, `mypy src` and `pytest -m "not eval"` all exit 0.
- [ ] The web editor authors accepted answers (with preview), form and tolerance. The preview uses the math input, and «جرّب الإجابة» sends `{steps, finalAnswer}`.
- [ ] Quiz and exam mount `MathStepsAnswer` with the owner `{studentId, sessionId, questionId}`. Answered, review and submitting states render read-only.
- [ ] The draft is cleared after a successful quiz check and after an exam submit, and kept on failure.
- [ ] A restored draft reaches the parent through `onChange` (#224) and is sent or autosaved without edits.
- [ ] The teacher validation page shows the accepted answers, form and tolerance. The correct answer reveals as a KaTeX preview.
- [ ] Blueprint rows include MathSteps (web and API).
- [ ] The avatar receives the math student and correct answer text.
- [ ] `api/openapi/v1.json`, `ai/openapi/v1.json` and the Orval client are regenerated with no drift. Postman has "Create math question" and "Grade math draft".
- [ ] `dotnet test api/ -c Release` is green with `appsettings.json` moved aside. The web `typecheck`, `lint`, `prettier --check … --end-of-line auto` and `test` all pass.
- [ ] `npm run build && npm run perf:budget` stays within the quiz page budget (math-input.md §7).
- [ ] The docs listed in "Docs" are updated, and `docs/math-cas.md` is created. There is no divergence between the code and PRD §6, question-schemas, sessions, exams, exam-blueprints, math-input, ai-service or design-prompt §4.
- [ ] Every test in the Test plan exists with the named method and assertion. The only existing tests modified are the constructor updates listed, `GetSubjectExamBlueprintsHandlerTests` (5→6) and the two `blueprintValues.test.ts` expectations.
