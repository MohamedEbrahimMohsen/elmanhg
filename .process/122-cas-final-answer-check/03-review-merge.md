VERDICT: CHANGES_REQUESTED

# Review (merge): #122 MathSteps + CAS on top of #119, #110 and #105

## Blocking

### 1. The new per-type MathSteps raw cap has no test, so a regression to 4000 would pass
**Where:** `api/Elmanhg.Application/Questions/Shared/QuestionAnswerRules.cs:56` (`QuestionType.MathSteps => options.MathStepsAnswerMaxLength`); tests at `api/Elmanhg.Tests/Application/Features/Questions/Shared/QuestionAnswerRulesTests.cs:83`
**Rule:** reviewer order #5/#6 (every new rule needs a test that fails when the rule is wrong); merge deviation #1 in `02-implementation-merge.md`
**Problem:** The merge replaces #122's `AnswerMaxLength = 24000` with a new `MathStepsAnswerMaxLength` switch arm. This is the main semantic decision of the merge, but no test checks it. `IsRawAnswerTooLong_EssayUsesEssayCapAndOtherTypesKeepAnswerCap` only covers Essay and Short. The MathSteps handler and integration tests only send small answers or answers that trip the step-count and final-answer caps (`ExceedsLimits`). The validators accept anything up to `RequestAnswerMaxLength` (121000).
**Failure:** Delete line 56, or map MathSteps to `AnswerMaxLength`. A valid MathSteps answer of 20 steps × 300 LaTeX characters (about 6–12 KB of raw JSON, well inside the `Math*` caps) now gets 422 `ATTEMPT_ANSWER_TOO_LONG` on quiz submit and on exam save, and all 4057 tests still pass.
**Fix:** Add a MathSteps case to `QuestionAnswerRulesTests`, for example `IsRawAnswerTooLong_MathStepsUsesMathStepsCap`, with `AnswerMaxLength = 20, MathStepsAnswerMaxLength = 40` and an answer between 20 and 40 raw characters: MathSteps → false, Short → true. Optionally also add a handler test that a MathSteps answer over 4000 raw characters but inside the `Math*` caps is accepted.

## Non-blocking
- `api/Elmanhg.Application/Exams/Shared/ExamSubmission.cs:34-50`: no test submits a written essay and an unchecked MathSteps answer in the same exam. The report claims "one exam submit handles both pending kinds". The code is correct: essays are excluded from `grades` and from `SubmitExam`, so `grades[x.QuestionId]` cannot miss. A combined test would still pin it.
- `api/Elmanhg.Tests/Application/Features/Exams/SubmitExam/SubmitExamHandlerTests.cs:195`: the manual-submit unchecked test does not assert that mastery is untouched. `AutoSubmitExamHandlerTests.Handle_MathCheckUnchecked_SubmitsWithoutMastery` covers the same `ExamSubmission` path, so this is not a gap.
- `web/src/features/subscription/components/LazyPaywallDialog.tsx:4`: if the chunk import fails, the error goes to the route error boundary. That is the same risk as the existing `QuizEssayCard`, `AvatarPanel` and `MathStepsAnswerInput` lazy boundaries, and there is no retry helper in the repo. There is also no dedicated unit test. `QuizPage.freeTier.test.tsx:31` does cover the dialog opening from the quiz (`findByRole('dialog', { name: 'Free limit reached' })`).
- `docs/performance.md` §3 still says quiz 249 KB measured, and §4 does not list the lazy paywall and math-input boundaries. The "measured" column was already stale on main (254), and the budget (255) is correct, so this is refresh material, not a divergence.

## LazyPaywallDialog assessment
- It opens correctly. When `reason` becomes non-null, the eager `PaywallDialog` is mounted with `open={true}`, and Radix mounts `DialogContent` with its FocusScope. Focus trap, initial focus, Escape and outside-click (`onOpenChange` → `onClose`) all behave as before. The accessible name comes from `DialogContent title`, and the quiz free-tier test finds it by role and name.
- Focus on close is unchanged. The quiz never had a `DialogTrigger`, so Radix's `onCloseAutoFocus` had no trigger to return to, both before and after the change.
- No regression elsewhere: `QuizEssayCard`, `PracticeStart` and exam start still use the eager `PaywallDialog`. `PaywallDialog.test.tsx` is unchanged.
- The lost close animation (the content now unmounts immediately) is acceptable. It is cosmetic, only in the quiz card, and it buys back about 10 KB br (quiz 256 → 245 of 255).

## Verified
- The behaviour of both sides is kept:
  - The quiz handler order (`SubmitAnswerHandler.cs:60-83`) is CanRead → raw/`Math*` caps → essay text cap → essay branch or `AnswerGrader`.
  - Mastery is skipped on `grade.AwaitsReview` (`:103`), and there is still exactly one `SaveChangesAsync` (`:85`).
  - In `ExamSubmission.cs:26-50`, written essays → `EssayGrade.Request` with no attempt. Everything else goes through `AnswerGrader` (only MathSteps calls `IAiMathCheckClient`: `AnswerGrader.cs:118`). `AwaitsReview` attempts are filtered out of mastery (`:40-42`).
  - EssayGrading application, domain and infrastructure code, `QuizEssayCard`, `QuizRunner` (lazy essay card) and `useQuizSubmit` are unchanged from main, so two-step apply and GET essay-grade Pending until applied are untouched.
  - Comparing the staged tree with `origin/main`, no test (api, web or ai) was removed.
- Caps:
  - `AnswerMaxLength` stays 4000, `EssayAnswerMaxLength` is 121000 and `MathStepsAnswerMaxLength` is 24000 (`SessionsOptions.cs`).
  - `RequestAnswerMaxLength` is the maximum of the three.
  - They agree across `appsettings.example.json`, `ApiFactory`, `docs/sessions.md` (config table, validator paragraph, error table), `docs/exams.md:44,178`, `docs/essay-grading.md:118` and `docs/deployment.md:211`.
- `ProvisionalScoreNotes`:
  - Both notes can render together (its test covers both notes together, and none).
  - The summaries keep #119's `answered` rule.
  - The exam badge shows provisional only when `awaitsReview` is set.
  - i18n keys match between ar and en in the quiz, exam, questions and shared namespaces. The removed exam keys are no longer referenced.
  - The `essaysPending` tests in `QuizResultPage.essay.test.tsx` and `ExamResultPage.essay.test.tsx` still pass.
  - Tokens (`border-warning`, `bg-warning-soft`, `text-caption`, `text-micro`) exist in `.claude/design-system.md` and `web/src/styles/app.css`.
- Docs: `sessions.md`, `exams.md` (§ Submit, with both pending kinds in one submit), `math-cas.md` §§ unchecked/awaitsReview, `essay-grading.md`, `question-schemas.md` § Servable (seven types), `exam-blueprints.md` and `claude-design-prompt.md` §4 (both result notes) agree with the code. "Not servable until #119" is gone.
- Postman: the JSON parses. "Create/Grade math" and "Create/Approve/Submit/Get essay" are present.
- Generated client: `awaitsReview` (`attemptResult.ts`), `pendingAnswer` (`sessionItemResult.ts`) and `QuestionType` with Essay and MathSteps are present.
- Builds, re-run by me in `D:/Personal/elmanhg-wt/122`:
  - api (CI parity, no `appsettings.json`): `dotnet build -c Release` has 0 errors, and `dotnet test -c Release --no-build` has **4057/4057 passed**.
  - ai: `uv sync --locked` ok, ruff format (121 files) ok, ruff check ok, mypy (68 files) ok, and `pytest -m "not eval"` gives **389 passed**, 3 deselected.
  - web: `tsc -b` exit 0, lint exit 0, vitest **230 files, 1297 tests passed**, and build exit 0.
  - `perf:budget`: entry 204/210, landing 213/220, lesson 234/240, **quiz 245/255** and admin-dashboard 222/230, all ok.
  - This matches `02-implementation-merge.md`.

## Test quality
- `SubmitAnswerHandlerTests`, `SaveExamAnswerHandlerTests`, `SubmitExamHandlerTests`, `AutoSubmitExamHandlerTests` and `AnswerGraderTests` constrain the merged behaviour. They cover unchecked → no mastery, only MathSteps calling the client, essays requested with no attempt, and a blank essay recorded as unanswered.
- `QuestionAnswerRulesTests` does not constrain the MathSteps raw-cap arm (finding #1).
