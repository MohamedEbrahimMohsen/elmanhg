VERDICT: APPROVED

# Review — Quiz screen with immediate feedback (#76, E5.S3), round 2

## Blocking
None. Round-1 Blocking #1 is resolved.

## Non-blocking
- `web/src/features/quiz/pages/QuizPage.finish.test.tsx:12-17`: the `finished()` helper duplicates the one in `QuizPage.test.tsx`. Move it into `web/src/test/quizFixtures.ts` when that file is next touched.
- `web/src/features/quiz/pages/QuizPage.finish.test.tsx`: this is the first `<Name>.<aspect>.test.tsx` file in `web/src`. It still sits beside the code, as `react-testing.md` asks, and the orchestrator allowed it. If more splits follow, they should use the same naming.
- `web/src/features/quiz/hooks/useFinishQuiz.ts:24`: the non-`ApiError` fallback is still uncovered (50% branches). The same applies to `useQuizAnswer.ts:35` and `useStartQuiz.ts:24`, as in round 1.
- The round-1 non-blocking items still apply unchanged. The rework was scoped to #1 only.

## Verified
- **Finding #1 fix:** `QuizPage.finish.test.tsx:20-36` clicks "End practice". The finish POST returns 400 `SESSION_ALREADY_SUBMITTED` and swaps in a finished-session GET handler. The test asserts the toast "This session has already ended." and then the "Practice result" heading. That path runs through `onError`, then `invalidateQueries`, then the Decision 12 redirect. The test runs in the `dom` project and passes.
- **Mutation (reviewer, on a scratch copy of `web/` in the session scratchpad; the working tree was never edited):**
  - (a) Deleting `invalidateQueries` (`useFinishQuiz.ts:26`) fails the test: "Unable to find role=heading name 'Practice result'".
  - (b) Deleting the whole `onError` (lines 23-27) fails the test: "Unable to find ... This session has already ended."
  - (c) Changing the key to `common:errors.X${code}` fails the test: the toast text is not found.
  - The unmutated baseline passes. The implementer's three mutation claims hold.
- **Production files unchanged:**
  - After the round-1 review, only two files under `web/src`, `docs` or `api` changed: `useFinishQuiz.ts` (the mutate-and-restore) and the new test file.
  - `useFinishQuiz.ts` is 37 lines, matching the round-1 report. It matches the plan contract (row 7) and mirrors the `onError` in `useQuizAnswer.ts:34-38`. Its `onError` is still at lines 23-27, the location round 1 cited.
  - `QuizPage.test.tsx` is untouched.
  - Its coverage now reads 100 stmts / 50 branches, with only line 24 uncovered. Before, lines 24-26 were uncovered.
- **No stray files:** `git status --untracked-files=all --ignored` shows only the round-1 file set, the new test and the `.process` docs. There are no backup, `.orig` or scratch files. `web/coverage` and `web/dist` are ignored build outputs.
- **CI re-run by the reviewer:**
  - `typecheck`: exit 0.
  - `lint`: exit 0.
  - `format:check`: "All matched files use Prettier code style!"
  - `test -- --run --coverage`: 78 files and 489 tests passed. Coverage is 94.83 stmts / 81.91 branches / 90.5 funcs / 95.02 lines, which matches the r2 report exactly.
  - `build`: built in 6.72 s.
- **Rest of the r2 report:** "Files modified: None" holds. The single new file is disclosed as a deviation that the orchestrator allowed. No `api/`, `postman/` or docs change means no Postman or docs-sync impact.

## Test quality
- `QuizPage.finish.test.tsx`: constraining. It kills all three mutants of the finish error path: no invalidation, no `onError`, and a wrong i18n key.
- The other test classes are unchanged from round 1 and were judged constraining there.
