VERDICT: CHANGES_REQUESTED

# Review r2: [E15.S2] CAS final answer check + MathSteps type (#122, #224)

Round-1 findings #1 and #2 are fixed. The core of #3 is fixed: slots are isolated and queue wait is not charged. Two small gaps from the round-2 change remain, numbered from #4.

## Blocking

### 4. The magnitude bound does not cover roots: a nested `\sqrt[...]` tower passes parsing and holds a worker for the full timeout
**Where:** `ai/src/elmanhg_ai/cas/nodes.py:58-60` (`root` builds `Pow(radicand, 1/index)` without the `magnitude` check that `power` applies at `nodes.py:50-55`). The doc that promises the bound is `docs/math-cas.md:115`, the limits table (`ELMANHG_AI_CAS_MAX_MAGNITUDE`: "an upper bound on the decimal digits of **every power** ... before any evaluation").
**Rule:** round-1 #3 fix (a), which asked for a general bound on compounded numeric powers; `.claude/rules/docs-sync.md` divergence (the code and math-cas.md give different answers).
**Problem:** A root with a small fractional index is a power with a large exponent, for example `\sqrt[0.001]{a}` = `a^1000`. It never reaches `power()`, so neither `check_exponent` nor `magnitude` runs on it. Wrapping it in `^` is caught, because `magnitude` walks into the Pow. Nesting roots alone is not caught.
**Failure:** Reproduced in the rebuilt `ai/Dockerfile` image (Linux, forkserver, default settings, over HTTP `POST /v1/math-checks`). `\sqrt[0.001]{\sqrt[0.001]{\sqrt[0.001]{9}}}` (43 characters, which is 9^(10^9)) and the same tower with `{\frac{1}{999}}` as the index both parse and return `200 unchecked` after 5.03 s, where they should return `unreadable` in milliseconds. `((9^{999})^{999})^{999}` now returns `unreadable` in 0.03 s. The harm is bounded by the #3b/#3c fixes: only the attacker's own slot is killed. Still, it is the hostile-numeric case the bound was added to stop, and the doc says the bound stops it.
**Fix:** In `root`, apply the same guard as `power`: `check_exponent` on the computed exponent, then raise `MathParseError("power too large")` when `magnitude(value) > limits.max_magnitude`. Pass `limits` from `parser.py:195`. Add a nested-root case and a fractional-index case to `test_parse_compounded_power_raises`.

### 5. Two docs still say a timeout kills the whole pool
**Where:**
- Code: `ai/src/elmanhg_ai/cas/pool.py:174-184` and `ai/src/elmanhg_ai/cas/worker.py:41-45` now discard only the timed-out slot. `docs/math-cas.md:117` agrees with the code.
- Stale: `docs/ai-service.md:291`, Settings table, `ELMANHG_AI_CAS_TIMEOUT_SECONDS`: "the pool is terminated on a timeout".
- Stale: `docs/deployment.md:271`, `ELMANHG_AI_CAS_TIMEOUT_SECONDS`: "the worker pool is killed and recreated on a timeout".

**Rule:** `.claude/rules/docs-sync.md`, divergence (an architecture/policy change with the owning doc still describing the old behaviour).
**Problem:** An operator reading either table would conclude that one hostile answer fails every in-flight check. That was the round-1 behaviour, and the r2 change removed it.
**Failure:** Ask "what happens to concurrent checks when one times out?". `math-cas.md` says they are untouched. `ai-service.md` and `deployment.md` say the pool is terminated.
**Fix:** In both rows, say that only the offending worker slot is killed and restarted in the background, and that other slots are untouched.

## Non-blocking
- `ai/src/elmanhg_ai/cas/pool.py:148`: `WORKER_FAILURES` is a closed list. Any other exception raised in the worker re-raises through `except BaseException` (line 152) and becomes a 500. This contradicts `docs/ai-service.md:215` ("never returns 5xx for CAS work"). I saw it once: a `SystemError: error return without exception set` from `sympy.simplify` on `(x+y+z+w)^{999}`, in a recycled slot during a multi-scenario run. Five later isolated runs and the HTTP run all returned `unchecked` at 5 s. .NET maps the 500 to Unchecked, so the student outcome is the same. Catching `Exception` there (recycle and return UNCHECKED) would make the doc hold unconditionally.
- A symbolic blow-up is still accepted and costs a slot for the full timeout, for example `(x+y+z+w)^{999}` (the magnitude estimate is 3,996 digits, but expansion is enormous). No parse-time bound can close every such shape; the timeout plus per-slot isolation is the real backstop, and it now works. With `cas_workers=2`, two hostile checks every ~5 s still keep benign checks queued; in my run `x+1` waited 5.4 s and then returned `equivalent`. A sustained attack could push queued checks past the API's 15 s and turn them into `unchecked`. Consider a per-student rate limit on MathSteps submissions, or an alert on `math_check.timeout`.
- Cold start measured 7.5 s for the first check in the container, against the ~2 s stated at `docs/ai-service.md:337`. It still fits under the API's 15 s, but with little margin. Warming the slots in the lifespan (`warm()` already exists) would remove it.
- `api/Elmanhg.Application/Sessions/Shared/SessionResultGenerator.cs:25`: the correct answer and the explanation are still sent for an item awaiting review, and only the UI hides them (`FeedbackPanel.tsx:51-52`). This is acceptable, because quizzes reveal after any attempt, but it is a UI-only hide.
- The round-1 non-blocking items are unchanged, as declared.

## Verified
- **#1 is fixed.**
  - `GradeFeedback.AwaitsReview` (`GradeFeedback.cs:7-8`, `[JsonIgnore]`, so the stored JSON is unchanged) is the single source.
  - `QuestionGrade.AwaitsReview` and `Attempt.AwaitsReview` delegate to it.
  - `AttemptResult.AwaitsReview` is populated in `SessionResultGenerator.cs:26`, and OpenAPI and Orval carry it.
  - `FeedbackPanel` shows the InReview verdict with the warning tokens and the Clock icon, replaces the score with the hint, and hides the correct answer and the explanation.
  - The quiz and exam summaries show the provisional line, and the exam shows «قيد المراجعة» instead of the fail badge.
  - The only other outcome consumers are `GradeResultPanel` (the admin preview, declared) and `EssayGradeOutcome`. Neither is fed MathSteps attempts.
  - All visual values are tokens.
  - The API tests (S1, S3 and `Handle_SavedMathStepsAnswerUnchecked_ReturnsItemAwaitingReview`) and the web tests (QuizPage.mathSteps, FeedbackPanel, QuizResultPage, ExamResultPage) assert the flag and the absence of a verdict, score and correct answer.
- **#2 is fixed.** The `docs/backlog.json` E17 description, story and new task, the PRD §8.3 bullet and PRD §6, `math-cas.md:93-95`, sessions, exams, design-prompt and prototype all agree on the review owner and on the «قيد المراجعة» presentation. GitHub #128 was not checked (declared as done by the orchestrator).
- **#3a holds for most shapes.** In the Linux image with default limits, these are rejected at parse time in about 0 s:
  - `((9^{999})^{999})^{999}`, `{9^{9}}^{999}`
  - `9^{9^{9^{9}}}`, `9^{9^{999}}`, `e^{e^{e^{9}}}` (exponent limit)
  - `123456789012345678901234567890^{999}` and `\sin^{999}(9^{999})`
  - `(\sqrt[0.001]{9})^{999}`
  - `9!` (no factorial token)

  These are accepted and cheap (at most 0.13 s): ten `9^{999}` factors multiplied together, nested `\frac`, `\sqrt[0.001]{9^{999}}` and `(9^{999})^{x}`. The gap is roots (#4).
- **#3b/#3c hold (round-1 reproduction re-run, Docker/Linux, 2 slots, 5 s):**
  - Scenario 1: a hostile check, then legit 4.9 s later. The legit `\frac{x^9-1}{x-1}+\sin^2x+\cos^2x` returned `equivalent` in 0.12 s. Round 1 returned `unchecked` here.
  - Scenario 2: two hostile checks in flight, then `x+1` vs `1+x` 0.5 s later. `x+1` returned `equivalent`: it queued 4.5 s, and the queue time was not charged. Round 1 returned `unchecked` at 5.0 s.
  - Over HTTP, I got the same result, with no 5xx.
  - The slot pids change only for the killed slot.
- **The tests do not depend on Windows timing.** I ran `test_cas_pool.py`, `test_cas_parser.py` and `test_cas_settings.py` 3x on Linux (`python:3.13.15-slim`, `uv sync --locked`): 32 passed each time, in about 4.8 s. The full ai suite on Linux gave 384 passed. On Linux, the tower under `RLIMIT_AS` 1 GiB stays running past the 2 s test timeout, so the pid assertion holds. PT5b warms first, so the 0.001 s timeout always hits a running task.
- **Suites re-run by me:**
  - api: CI parity, no appsettings.json. `dotnet test api/ -c Release` gave 3798/3798.
  - ai (Windows): `ruff format --check` covered 121 files, `ruff check` was clean, and `mypy src` reported no issues in 68 files. `python -m uv run pytest -m "not eval"` gave 384 passed and 3 deselected.
  - web:
    - typecheck, lint and prettier were clean.
    - vitest: 208 files, 1187 passed.
    - The build succeeded.
    - perf:budget (all ok): entry 203/210, landing 211/220, lesson 232/240, quiz 253/255.
- **The r2 deviations are declared and match the code:** the Queue instead of a Semaphore, `cas/worker.py`, the PT5b pid assertion, and the hidden explanation.

## Test quality
- `test_cas_pool.py` now constrains the design. The concurrent test fails under a pool-wide discard, and the queue test fails if the clock starts before checkout.
- `test_cas_parser.py::test_parse_compounded_power_raises` covers only towers built with `^` and has no root case. That is why #4 went unnoticed.
