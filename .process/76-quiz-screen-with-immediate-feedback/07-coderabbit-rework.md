# CodeRabbit rework — Quiz screen with immediate feedback (PR #169, story #76)

## Findings addressed
| # | What I changed | File:line |
|---|---|---|
| RC1 | "End practice" is now `disabled={isPending \|\| isChecking}`. While the answer POST is in flight, the student can no longer fire `/finish`. | `web/src/features/quiz/components/QuizQuestionActions.tsx:36` |
| RC1 (test) | Added `disables End practice while the answer is being checked`: it holds the answer POST pending with `delay('infinite')` and asserts that both "Checking…" and "End practice" are disabled. It also adds `delay` to the `msw` import. | `web/src/features/quiz/pages/QuizPage.finish.test.tsx:38-53` |

## Files created
None.

## Files modified
| Path | Change |
|---|---|
| `web/src/features/quiz/components/QuizQuestionActions.tsx` | Adds `\|\| isChecking` to the End practice `disabled` prop |
| `web/src/features/quiz/pages/QuizPage.finish.test.tsx` | Adds one test and the `delay` import (54 lines total) |

## Deviations
None. I did not apply the optional reverse guard on "Check" (`isChecking || isPending`). The triage marked it not required, and it had no test.

## Fail-without-fix check
I ran the new test against the unfixed component first. It failed at the intended assertion, `QuizPage.finish.test.tsx:52`, with "Received element is not disabled" (the End practice button). I then applied the fix, and both tests in the file passed.

## Build & test
- `npm --prefix web run typecheck`: `tsc -b` exited clean.
- `npm --prefix web run lint`: `eslint . --max-warnings=0` exited clean.
- `npm --prefix web run format:check`: "All matched files use Prettier code style!"
- `npm --prefix web test -- --run --coverage`: Test Files 78 passed (78), Tests 490 passed (490). All files: 94.83 stmts / 82.01 branch / 90.5 funcs / 95.02 lines.

## Notes for review
- No doc divergence: the change only adds a pending state.
- I did not touch the orchestrator's uncommitted `PROGRESS.md` or `.process` edits. Nothing is committed.
