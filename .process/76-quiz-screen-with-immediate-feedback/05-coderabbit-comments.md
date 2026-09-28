# CodeRabbit comments — PR #169

Collected 2026-09-28 14:19. Review 5339976735 ("Actionable comments posted: 1"). Verbatim essentials.

## RC1 — `web/src/features/quiz/components/QuizQuestionActions.tsx:36` (🟡 Minor, Stability)
**Disable "End practice" while the answer check is running.** The "End practice" button uses only `isPending` from `useFinishQuiz` for `disabled`. Line 33 disables "Check" while `isChecking` is true, but "End practice" stays enabled. A click during the check races the pending submit; the server can reject one with 409 `SESSION_MODIFIED_CONCURRENTLY` or 400 `SESSION_ALREADY_SUBMITTED`, the student sees an error toast, and the answer may not be recorded.
Proposed: `disabled={isPending || isChecking}`.
