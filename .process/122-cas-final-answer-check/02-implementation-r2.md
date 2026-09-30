# Implementation r2 — [E15.S2] CAS final answer check + MathSteps type (#122, #224)

## Blocking findings addressed

| # | What I changed | Where |
|---|---|---|
| 1 | Added an awaiting-review flag to the grade contract. `GradeFeedback.AwaitsReview` (`[JsonIgnore]`, so the stored `Attempts.Grade` JSON does not change) is the single rule. `QuestionGrade.AwaitsReview` and the new `Attempt.AwaitsReview` both delegate to it. `AttemptResult` gains `bool AwaitsReview`, which also flows into `ExamItemResult.Attempt`. I regenerated OpenAPI and Orval. | `api/Elmanhg.Domain/Questions/Grading/GradeFeedback.cs:7-8`, `QuestionGrade.cs:9`, `api/Elmanhg.Domain/Sessions/Attempt.cs:26`, `api/Elmanhg.Application/Sessions/Shared/AttemptResult.cs:5`, `SessionResultGenerator.cs:26`, `api/openapi/v1.json`, `web/src/shared/api/generated/**` |
| 1 | Web: `FeedbackPanel` has an `InReview` verdict. It uses the warning token and the Clock icon, the same as #118's essay «قيد المراجعة» box. It shows «قيد المراجعة» / "Under review" and «الدرجة معلّقة حتى يراجعها معلمك.» in place of the verdict and score, and it hides the correct answer and the explanation. The quiz feedback, the quiz result review and the exam result review all use `FeedbackPanel`. | `web/src/features/quiz/components/FeedbackPanel.tsx:36-50,57-58,71-77` |
| 1 | Scores: `QuizResultSummary` and `ExamResultSummary` show a warning line «N إجابة قيد المراجعة، والنتيجة مبدئية حتى يراجعها معلمك.». When an exam is below the pass mark and has an answer under review, it shows a «قيد المراجعة» badge in place of the fail badge. | `web/src/features/quiz/components/QuizResultSummary.tsx`, `web/src/features/exam/components/ExamResultSummary.tsx`; strings in `quiz/i18n/{ar,en}.json` (`feedback.inReview`, `feedback.inReviewHint`, `result.inReview`) and `exam/i18n/{ar,en}.json` (`result.inReview`, `result.provisional`) |
| 1 | Tests: API: S1 asserts `AwaitsReview == false` and S3 asserts `(0, true)`. New `SubmitExamHandlerTests.Handle_SavedMathStepsAnswerUnchecked_ReturnsItemAwaitingReview`. Web: QuizPage.mathSteps "shows an unchecked answer as under review without a verdict, score or correct answer". FeedbackPanel "shows an answer awaiting review…". QuizResultPage "marks the score provisional…" and "does not mention review…". ExamResultPage "shows an unchecked math answer as under review instead of failing the exam". | `api/Elmanhg.Tests/.../SubmitAnswerHandlerTests.cs`, `.../SubmitExamHandlerTests.cs`, `web/src/features/quiz/pages/QuizPage.mathSteps.test.tsx`, `quiz/components/FeedbackPanel.test.tsx`, `quiz/pages/QuizResultPage.test.tsx`, `exam/pages/ExamResultPage.test.tsx`, `web/src/test/quizFixtures.ts` |
| 2 | `docs/backlog.json` E17: the description, the story description and a new task, "MathSteps attempts graded unchecked (feedback kind mathUnchecked, awaitsReview) are listed and can be scored by the teacher". PRD §8.3 has a new bullet with the same scope. PRD §6 (math with steps) now describes the «قيد المراجعة» presentation. `docs/math-cas.md` points to §8.3 and E17 and describes the UI. `sessions.md` (AttemptResult shape), `exams.md`, `claude-design-prompt.md` §4 and `prototype.md` are updated to match. | `docs/backlog.json:722-731`, `docs/PRD.md` §6 and §8.3, `docs/math-cas.md`, `docs/sessions.md:186`, `docs/exams.md:53`, `docs/claude-design-prompt.md:135`, `docs/prototype.md:68` |
| 3a | Parse-time magnitude bound. `nodes.magnitude` gives an upper bound on the decimal digits of an unevaluated tree: a number counts its digits, a symbol counts 1, a sum or product adds its parts, and a power multiplies its base by the exponent. `nodes.power` raises `MathParseError("power too large")` above the new setting `cas_max_magnitude`. The setting defaults to 10000 (range 10 to 1,000,000) and is carried in `CasLimits.max_magnitude`. `((9^{999})^{999})^{999}` is now `unreadable` in milliseconds. `9^{999}` is still accepted. | `ai/src/elmanhg_ai/cas/nodes.py:55-60,78-88`, `ai/src/elmanhg_ai/cas/models.py`, `ai/src/elmanhg_ai/settings.py:77` |
| 3b | A timeout kills only the offending task. Each slot is now its own single-process pool (`CasWorker`, in the new `cas/worker.py`). On timeout, the slot is discarded, restarted and warmed in a background task, and it rejoins the idle queue only when it is ready. Other slots are untouched. | `ai/src/elmanhg_ai/cas/pool.py`, `ai/src/elmanhg_ai/cas/worker.py` |
| 3c | A check takes a free slot from an `asyncio.Queue` of `cas_workers` workers before `apply_async`, and the timeout clock starts only then, so queue wait is not charged. The queue acts as the semaphore and also says which worker is free. `warm()` returns worker pids (used by the tests). The lifespan now calls `aclose()`, which waits for any recycle before it closes. | `ai/src/elmanhg_ai/cas/pool.py:42-80`, `ai/src/elmanhg_ai/main.py:86` |
| 3d | Tests. Parser: `test_parse_compounded_power_raises[tower-of-three, tower-of-two, product-base, symbolic-tower]`, `test_parse_single_large_power_accepted`, and `test_evaluate_compounded_power_is_unreadable_without_evaluation` (the reproducer, under 1 s). Settings: the default and a `tiny-magnitude` out-of-range case. Pool: `test_cas_pool_hostile_timeout_spares_concurrent_benign_check` runs 2 workers. The real tower is sent with an unbounded magnitude limit and a 2 s timeout, and a benign check is sent 1 s later. The test asserts that the benign check returns `equivalent`, the hostile one returns `unchecked`, and exactly one worker pid changed. `test_cas_pool_queue_wait_is_not_charged_to_timeout` runs 1 worker: a benign check queued behind the hostile one still returns `equivalent`. PT5b now uses a CPU-bound hostile task and asserts that the worker pid was replaced. | `ai/tests/unit/test_cas_parser.py`, `test_cas_settings.py`, `test_cas_pool.py` |

**Mutation check:** I made `_replace` close all workers (the old pool-wide behaviour). `test_cas_pool_hostile_timeout_spares_concurrent_benign_check` then fails. I reverted the mutation.

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `ai/src/elmanhg_ai/cas/worker.py` | 63 | One single-process CAS worker slot: submit, warm, discard, memory limit |

## Files modified (this round)
- **ai:**
  - `cas/pool.py`, `cas/nodes.py`, `cas/models.py`, `settings.py`, `main.py`
  - tests: `test_cas_pool.py`, `test_cas_parser.py`, `test_cas_settings.py`, and `test_cas_lexer.py` / `test_cas_equivalence.py` (LIMITS gains `max_magnitude`)
- **api:**
  - production: `GradeFeedback.cs`, `QuestionGrade.cs`, `Attempt.cs`, `AttemptResult.cs`, `SessionResultGenerator.cs`, `openapi/v1.json`
  - tests: `SubmitAnswerHandlerTests.cs`, `SubmitExamHandlerTests.cs`, `QuizAnswerMetricsBehaviourTests.cs`
- **web:**
  - components: `FeedbackPanel.tsx`, `QuizResultSummary.tsx`, `ExamResultSummary.tsx`
  - i18n: quiz and exam i18n (ar/en)
  - generated Orval: `model/attemptResult.ts`, `zod/sessions`, `zod/exams`
  - `test/quizFixtures.ts` and four test files
- **docs:** `backlog.json`, `PRD.md`, `math-cas.md`, `ai-service.md`, `sessions.md`, `exams.md`, `claude-design-prompt.md`, `prototype.md`
- **Postman:** unchanged. No request changed: only a response field was added, and the collection holds no response examples.

## Deviations
| Plan / review said | Reality | What I did |
|---|---|---|
| Review 3c: use an `asyncio.Semaphore(cas_workers)` | With per-slot pools, a semaphore would still need a way to say which slot is free | I used an `asyncio.Queue` of workers, which gives the same bound. The timeout starts only after a worker is taken. |
| Contract: create exactly the listed files | Putting the pool and per-slot workers in one file would pass ~100 lines | I added `cas/worker.py` (63 lines). `limit_worker_memory` and `BYTES_PER_MEGABYTE` moved there from `pool.py`. |
| PT5b name `test_cas_pool_timeout_returns_unchecked_and_discards_pool` | After a timeout the slot is now restarted in the background, so `started` is no longer False right after the check | I kept the name. The test now asserts that the worker pid changed (discarded and replaced), and that `started` is False after `aclose()`. |
| Explanation visibility was not specified | The explanation usually reveals the answer | For an attempt under review, I hide the explanation as well as the correct answer. |
| GitHub issue #128 update | The orchestrator says it already commented there | I did not touch GitHub. |

## Build & test
- **api (CI parity: no `appsettings.json` in the worktree):**
  - `dotnet build api/ -c Release`: Build succeeded, 0 warnings. `openapi/v1.json` was regenerated (adds `awaitsReview`).
  - `dotnet test api/ -c Release`: total 3798, failed 0, succeeded 3798.
- **ai:**
  - `ruff format --check`: 121 files already formatted.
  - `ruff check`: All checks passed!
  - `mypy src`: no issues in 68 source files.
  - `python -m uv run pytest -m "not eval"`: 384 passed, 3 deselected.
- **web:**
  - `gen:api` regenerated.
  - `lint`: clean.
  - `typecheck`: clean.
  - `prettier --check --end-of-line auto`: clean.
  - `vitest run`: 208 files, 1187 passed.
  - `build`: ok.
  - `perf:budget`:

    | Chunk | Size |
    |---|---|
    | entry | 203/210 KB |
    | landing | 211/220 KB |
    | lesson | 232/240 KB |
    | quiz | 253/255 KB |

    All are ok.

## Notes for review
- The hostile pool tests were run on Windows (spawn, no `RLIMIT_AS`). On Linux, the tower with an unbounded magnitude grows toward the 1 GiB `RLIMIT_AS`. The reviewer observed a timeout at 5 s there, so it should still be running at the 2 s test timeout. If it ever hit `MemoryError` first, the concurrency test's pid assertion would fail. That would be a test-timing issue, not a DoS regression.
- A check cancelled mid-flight (client disconnect) recycles its worker in the background, so a cancelled hostile task cannot keep a slot busy.
- Cold start is still charged to the first check on each slot. Warming in the lifespan was left out (non-blocking review item). `warm()` exists if wanted.
- The teacher's "جرّب الإجابة" preview (`GradeResultPanel`, grade-draft) still shows an `unchecked` math draft as a normal verdict with the feedback line. It is admin-only and was not in the finding.
- The non-blocking review items were not addressed. These include the `SubmitAnswerHandler` mastery-on-retry case and the `mathOwner` memo.
