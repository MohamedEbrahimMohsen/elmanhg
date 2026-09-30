VERDICT: CHANGES_REQUESTED

# Review — [E15.S2] CAS final answer check + MathSteps type (#122, #224)

## Blocking

### 1. An `unchecked` math answer is shown to the student as a wrong answer ("Incorrect", red, 0/x, correct answer revealed), not as pending review
**Where:** `api/Elmanhg.Domain/Questions/Grading/QuestionGrade.cs:9,13` (an unchecked grade gets `Outcome = Incorrect`); `web/src/features/quiz/components/FeedbackPanel.tsx:37-46,57-75` (any non-Correct/Partial outcome renders the danger "incorrect" verdict with the score, then reveals the correct answer); no field in `AttemptResult` tells the client that the grade awaits review (`grep -ri unchecked web/src` finds nothing).
**Rule:** gate condition 2 in `00-acceptance.md` ("mark its CAS result unchecked/pending … retried by a sweep **or shown as under review**"); PRD §6.1 ("queued for teacher review before the score is final. The student sees «قيد المراجعة» in the meantime"); `.claude/design-system.md` `color.warning` = "partial credit, pending review".
**Problem:** The deviation from plan D14 is reasonable interim behaviour on the server. The attempt is stored, the feedback kind `mathUnchecked` is persisted, and mastery skips it. But the student UI treats the grade as final and wrong. The only hint is a muted caption at `FeedbackPanel.tsx:69`. The correct answer is revealed, and "Ask the avatar" frames it as a mistake.
**Failure:** The ai service is down (or see #3). A student submits `x = 2` for `2x + 3 = 7` and sees the red «إجابة خاطئة», "0 / 2" and the correct answer `x = 2`. The quiz result counts the question as wrong.
**Fix:** Expose a pending flag on the attempt and exam item result (e.g. `awaitsReview: bool`, derived from `QuestionGrade.AwaitsReview` / the stored feedback kind). Render it with the warning token and «قيد المراجعة» in place of the verdict and score. Hide the correct-answer reveal for that item. Add a web test (QuizPage.mathSteps) and an API test asserting the flag.

### 2. "Your teacher will review it" has no owner: the E17/#128 scope that is supposed to resolve `unchecked` attempts still covers only low-confidence AI grades
**Where:** Code and docs that promise review: `api/Elmanhg.Api/Resources/Messages.en.resx:421` / `Messages.ar.resx:421`, `docs/math-cas.md:94`, `docs/sessions.md:100`, `docs/exams.md:53`, `docs/PRD.md:166`. Docs that define the review scope: `docs/backlog.json:720-733` (E17 "Low-confidence AI grades reviewed…", tasks mention only a confidence threshold) and `docs/PRD.md` §8.3 ("Low-confidence AI grades for the teacher-owned subjects").
**Rule:** `.claude/rules/docs-sync.md`: a scope change (E17 now has to include CAS-unchecked attempts) must update `docs/backlog.json` and PRD in the same change. Divergence is blocking.
**Problem:** The two sets of docs answer "which attempts reach the teacher review queue?" differently. The implementer flagged the gap only in "Notes for review" and did not record it anywhere #128 will read. Attempts are append-only and there is no sweep, so without #128 the provisional 0 is permanent.
**Failure:** #128 is planned from backlog/PRD §8.3. It builds a confidence-threshold queue over essay grades, and every `mathUnchecked` attempt keeps 0 forever, despite the message the student received.
**Fix:** Add a task to E17 in `docs/backlog.json` and a bullet to PRD §8.3: "MathSteps attempts graded `unchecked` (feedback kind `mathUnchecked`) are listed and can be scored by the teacher". Update the GitHub issue #128 to match.

### 3. CAS pool DoS: one 23-character answer burns a worker for the full timeout, then `terminate()` kills every concurrent check. Queue wait counts against the timeout.
**Where:** `ai/src/elmanhg_ai/cas/pool.py:50-58` (the timeout clock starts at `apply_async`, so queue time is charged) and `pool.py:89-94` (`_discard` terminates the whole pool, including the in-flight tasks of other requests). `ai/src/elmanhg_ai/cas/nodes.py:68-73` with `parser.py:142-149` (`check_exponent` bounds each literal exponent, but not the magnitude compounded through parenthesised bases).
**Rule:** plan D11/D12 (limits + hard timeout so a hostile answer cannot hurt grading); python skill non-negotiables on untrusted input; orchestrator focus (a) "no DoS via huge factorials/powers/nested expressions".
**Problem:** `((9^{999})^{999})^{999}` passes every parse limit (each exponent is 999, 23 chars, depth 3). `doit()` then computes a ~400 MB integer. The timeout and `RLIMIT_AS` stop it, and I verified both are enforced on Linux: worker `RLIMIT_AS = 1 GiB`, `unchecked` at 5.0 s. But the recovery is pool-wide.
**Failure:** Reproduced in the built `ai/Dockerfile` image (Linux, forkserver, default settings):
- A legit `\frac{x^9-1}{x-1}+\sin^2x+\cos^2x` check started 4.9 s after a hostile one returned `unchecked` (5.0 s).
- With two hostile checks in flight, a legit `x+1` vs `1+x` sent 0.5 s later returned `unchecked` (5.0 s).

Under gate 2 these become recorded 0s shown as wrong answers (#1). Any student can therefore repeatedly zero the math answers of other students during an exam window, at the cost of one quiz answer each time.
**Fix:**
- (a) Reject compounded numeric powers at parse time, e.g. track an estimated `log10|value|` for numeric subtrees and raise `MathParseError` when `|exp| × log10|base|` passes a setting (so the input is `unreadable` in milliseconds).
- (b) Confine a timeout to the offending task: one single-worker pool per slot, or kill only that worker process.
- (c) Take a slot with an `asyncio.Semaphore(cas_workers)` before `apply_async`, so queue time is not charged to the timeout.
- (d) Add tests for (a) and for "a concurrent benign check still returns its verdict while a hostile one times out". The current PT5b only times out a cold start (`cas_timeout_seconds=0.001`), so it never exercises a CPU-bound hostile task.

## Non-blocking
- `api/Elmanhg.Application/Sessions/SubmitAnswer/SubmitAnswerHandler.cs:68-73`: if the first attempt of a student is `unchecked`, a later, checked retry is not a "new attempt", so mastery for that question is never recorded.
- `web/src/features/quiz/hooks/useQuizAnswer.ts:40-47`: `mathOwner` is a new object on every render, so the clear effect runs on every render once the attempt is recorded. `clearMathDraft` is idempotent, so this is harmless; memoise it or key on the ids.
- `web/src/features/questions/components/MathAnswersField.tsx:221`: the name cast claims the path uses `number`, but the value uses `index`. Type the template with the index.
- `ai/src/elmanhg_ai/cas/pool.py:70-74`: the pool is created lazily and recreated after every timeout, so each discard pays a cold start. Consider warming it in the lifespan.
- `deploy/docker-compose.prod.yml:105-125`: the `ai` service has no `mem_limit`. `cas_workers` (2) × 1 GiB `RLIMIT_AS` plus the main process should fit the host; document or set a limit.
- `ai/src/elmanhg_ai/api/math_checks/schemas.py:307`: `tolerance` has no upper bound. It is trusted (.NET-validated) input, so this is only hardening.

## Verified
- **Builds and suites, re-run by me:**
  - api: `dotnet test api/ -c Release`, with no `appsettings.json` present (CI parity), gave 3797/3797 passed.
  - ai: `uv sync --locked`, `ruff format --check` (120 files), `ruff check` and `mypy src` (67 files) were all clean. `pytest -m "not eval"`: 375 passed.
  - web: `typecheck`, `lint` and `prettier --check --end-of-line auto` were clean. `vitest`: 208 files, 1182 passed. `build` succeeded. `perf:budget`: quiz 253/255 KB, lesson 232/240, landing 211/220, entry 203/210, all within budget.
- **The SymPy path has no string evaluation.**
  - `cas/*.py` builds only Integer/Rational/Symbol/Add/Mul/Pow/functions with `evaluate=False`, and never calls sympify, parse_expr, parse_latex, the S string form, eval, exec or lambdify.
  - The lexer is a whitelist: unknown commands and characters raise. Numbers are built from digits.
  - PT4 guards the sources.
  - All parsing and SymPy work runs inside the worker (`check.evaluate`). The parent only checks character counts.
- **Limits.** The token, depth, digit, element and per-literal exponent limits are settings and are enforced (PT1h/i, PT2h/i).
- **Worker isolation on Linux.**
  - forkserver is selected.
  - `RLIMIT_AS` is set in the worker: I read (1073741824, 1073741824) from inside the worker.
  - The hard timeout returns `unchecked` at 5.0 s. The pool is recreated and serves the next request (see #3 for the collateral).
- **Gate 1 holds.** `AnswerGrader.GradeAsync` (`AnswerGrader.cs:19-22`) returns the deterministic grade for every non-MathSteps type before touching the client (AG1: DidNotReceive). Exam autosave never calls the client. A blank final answer never calls the client (AG2).
- **Gate 2 on the server holds.** `HttpAiMathCheckClient` maps transport, timeout, non-2xx and bad replies to `Unchecked` (H3–H5 renamed). The attempt and the exam submission are saved, and `AwaitsReview` excludes them from mastery (S3 and E4 assert that mastery is DidNotReceive and that SaveChangesAsync is Received(1)). Only the student-facing presentation and the ownership of the review fail (#1, #2).
- **Deviations are declared honestly and match the code:**
  - QuestionGrade.AwaitsReview.
  - StartUnit/StartMultiUnit ctor changes and their tests.
  - OpenApiEndpointTests and blueprint lines 47/51 changed 5→6.
  - The quiz draft is cleared in an effect.
  - React.lazy for the math input.
  - MaybeEncodingError removed.
  - `_parse` catches SYMPY_FAILURES.
  - Q1 expects 200; Q3 sends Accept-Language ar.
- **Contract and plan items are present:**
  - `QuestionType.MathSteps`, `ServedTypes` (6 types), the schemas, rules and caps (SessionsOptions defaults plus ApiFactory plus appsettings.example.json).
  - Four new error codes plus MATH_CHECK_UNAVAILABLE, and 9 resx entries in each language.
  - The Fake/Http provider switch, and the typed client with a 15 s attempt and total timeout and no POST retry.
  - #224: `useMathStepsDraft` sends a restored draft to onChange once (WT9a/b, WT7b, WT8b).
- **No migration is needed:** the type is a string column and the content is jsonb. There is no snapshot change.
- **Generated artifacts:** `api/openapi/v1.json` was not rewritten by my build (no drift). The Orval zod and model files carry MathSteps. The ai OpenAPI drift test passes.
- **Postman:** "Create math question" (POST /api/questions, collection auth, the plan body, stores mathQuestionId) and "Grade math draft" (POST /api/questions/grade-draft, flat body like the existing Mcq draft) are placed as planned. No endpoint was added or removed.
- **Docs listed in the plan are updated and agree with the code on the outage behaviour:** PRD §6, question-schemas, math-cas (new), math-input, ai-service, sessions, exams, exam-blueprints, question-import, design-prompt §4, prototype and deployment. The exception is the E17 scope (#2).
- **Every test named in the plan exists:** D1–D9, A1–A16, M1–M9, AG1–AG5, F1, G1–G2, S1–S2, E1–E3, V1–V2, H1–H2, K1–K4, R1–R2, Q1–Q4, I1–I3, X1–X2, PT1a–PT9d and the 49-row PT3 table. S3, E4 and H3–H5 were renamed per the declared deviation.

## Test quality
- MathStepsGraderTests, MathStepsQuestionRulesTests, MathStepsAnswerRulesTests and AnswerGraderTests each pin one branch, and a wrong arm or rule fails them. AG3 asserts the exact request fields.
- SubmitAnswerHandlerTests (S1–S3) and AutoSubmitExamHandlerTests (E4) assert canonical answers, Received(1) and DidNotReceive on mastery. Removing the AwaitsReview filter would fail S3 and E4.
- HttpAiMathCheckClientTests asserts the wire body, including that null tolerance is omitted, and the Unchecked mapping.
- test_cas_equivalence.py (the PT3 table) genuinely constrains the CAS: the mutation checks the implementer ran were caught, and rows 29, 35 and 38 would fail on a naive simplify equality.
- test_cas_pool.py is weak. The 0.001 s timeout in PT5b only proves that a cold start times out. Nothing proves that a CPU-bound task is killed, that RLIMIT_AS applies, or that concurrent checks survive a timeout (see #3).
- No web test covers how an `unchecked` attempt is presented (see #1).
