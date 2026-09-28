VERDICT: APPROVED

# CodeRabbit verify — Quiz screen with immediate feedback (PR #169, story #76)

## Blocking
None.

## Verified
- RC1 fix: `web/src/features/quiz/components/QuizQuestionActions.tsx:36` is `disabled={isPending || isChecking}`. `isChecking` was already a prop (line 9), so no signature changes were needed.
- New test `disables End practice while the answer is being checked` (`web/src/features/quiz/pages/QuizPage.finish.test.tsx:38-53`) matches the triage spec exactly.
  - It is deterministic. The answer POST handler (`*/api/sessions/:sessionId/answers`, which matches the generated URL at `sessions.ts:291`) never resolves because of `delay('infinite')`, so `isChecking` stays true. `findByRole('Checking…')` waits for that state, and after that the `getByRole` check for End practice is synchronous. There are no timers or races.
  - It fails without the fix. No finish request fires in this test, so `isPending` from `useFinishQuiz` stays false. With the old `disabled={isPending}`, End practice would be enabled and line 52 would fail. That matches the implementer's reported failure at line 52.
- Scope: `git diff --stat` shows two changed web files, the component and its test. The other changes are `PROGRESS.md` and the `.process/` files, which belong to the orchestrator. No other production changes.
- The test file is 54 lines, under the 200-line limit. No new visual values were added, so there are no design-token concerns.
- Docs sync: this change only adds a pending/disabled state. No behaviour change, so no doc divergence.

## Build & test (re-run by reviewer)
- `npm --prefix web run typecheck`: clean (`tsc -b`).
- `npm --prefix web run lint`: clean (`--max-warnings=0`).
- `npm --prefix web run format:check`: all files use Prettier code style.
- `npm --prefix web test -- --run --coverage`: 78 test files and 490 tests, all passed. Coverage: 94.83% statements, 82.01% branches, 90.5% functions, 95.02% lines. This matches the claims in 07-coderabbit-rework.md.

## Non-blocking
- `QuizQuestionActions.tsx:33`: the optional reverse guard (making Check `disabled={isChecking || isPending}`) was not applied. The triage marked it optional, and the implementer's report says so openly.
