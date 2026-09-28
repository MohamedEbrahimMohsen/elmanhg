# Implementation — Quiz screen with immediate feedback (#76), rework round 2

## Blocking findings addressed
| # | What I changed | File:line |
|---|---|---|
| 1 | Added a QuizPage test "reloads the session when finishing fails", modelled on test #60 ("reloads the session when the answer conflicts"). The finish POST handler swaps in a finished-session GET handler and returns 400 `SESSION_ALREADY_SUBMITTED`. The test asserts the toast "This session has already ended." and then the "Practice result" heading, which the invalidation and the Decision 12 redirect produce. It is in a **new sibling file** because `QuizPage.test.tsx` is already 271 lines. | `web/src/features/quiz/pages/QuizPage.finish.test.tsx:20-36` |

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `web/src/features/quiz/pages/QuizPage.finish.test.tsx` | 37 | Test for the finish-error path of `useFinishQuiz` |

## Files modified
None. `useFinishQuiz.ts` was mutated temporarily and restored exactly from a backup. Lines 25-26 are verified as the original.

## Mutation check (the test fails on each mutation, then passes after the revert)
- Removed `invalidateQueries`: the test fails with "Unable to find role=heading name 'Practice result'".
- Removed the whole `onError`: the test fails with "Unable to find ... This session has already ended."
- Wrong i18n key (`common:errors.X${code}`): the test fails because the toast text is not found.

## Deviations
None. The new sibling file was explicitly allowed by the orchestrator. It is a file outside the plan's *Files to create*, added to keep files at 200 lines or fewer.

## Build & test (web, run after the revert)
- `npm --prefix web run typecheck`: `tsc -b`, exit 0.
- `npm --prefix web run lint`: `eslint . --max-warnings=0`, exit 0.
- `npm --prefix web run format:check`: "All matched files use Prettier code style!"
- `npm --prefix web test -- --run --coverage`: "Test Files 78 passed (78)" and "Tests 489 passed (489)". All files: 94.83 stmts, 81.91 branches, 90.5 funcs, 95.02 lines. `useFinishQuiz.ts`: 100 stmts / 50 branches / 100 lines, uncovered line 24.

## Notes for review
- The 50% branch on `useFinishQuiz.ts:24` is the non-`ApiError` fallback (network failure → `UNHANDLED_EXCEPTION`). The review lists it as non-blocking; I left it untested to keep this change to finding #1 only.
- The `finished()` helper is duplicated from `QuizPage.test.tsx`, because that file must not be edited in this round.
