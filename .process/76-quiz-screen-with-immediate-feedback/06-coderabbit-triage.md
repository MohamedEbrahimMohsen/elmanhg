TRIAGE: 1 to implement, 0 rejected, 0 dev-decisions

# CodeRabbit triage — PR #169 (story #76)

## RC1 — "End practice" not disabled while the answer check is running
**Verdict:** IMPLEMENT (valid, Minor, confirmed in code; matches our round-1 non-blocking note, `03-review.md:17`).

**Verified in code:**
- `web/src/features/quiz/components/QuizQuestionActions.tsx:33`: "Check" is `disabled={isChecking}`.
- `web/src/features/quiz/components/QuizQuestionActions.tsx:36`: "End practice" is `disabled={isPending}` only (`isPending` comes from `useFinishQuiz`, line 23).
- `isChecking` is already a prop: `QuizQuestionCard.tsx:78` passes `quiz.isChecking`, which is `mutation.isPending` of the submit-answer mutation (`useQuizAnswer.ts:62`). `QuizRunner.tsx` does not need changes.
- Race: with an answer POST in flight, clicking "End practice" fires `POST /finish` (`useFinishQuiz.ts:33`). One of the two can fail with 409 `SESSION_MODIFIED_CONCURRENTLY` or 400 `SESSION_ALREADY_SUBMITTED`. The failing `onError` then shows an error toast (`useQuizAnswer.ts:34-38` / `useFinishQuiz.ts:23-27`). The server keeps the data consistent, but the student sees a spurious error, and the answer may be lost if the finish wins. That is a real UX defect, and the fix costs one token.

**Classification:** correctness/UX race on the client. It is not a style issue and not a product decision. No doc divergence: this is a pending state and does not change behaviour described in `docs/claude-design-prompt.md`.

**Minimal change** (one line, no prop or signature changes):
`QuizQuestionActions.tsx:36`: `disabled={isPending}` becomes `disabled={isPending || isChecking}`.
(Optional, same root cause, reverse direction: `QuizQuestionActions.tsx:33` could become `disabled={isChecking || isPending}`, so that Check cannot fire while a finish is pending. It is not required for RC1. Include it only if it is covered by the same test style.)

**Test** (add to `web/src/features/quiz/pages/QuizPage.finish.test.tsx`, inside `describe('QuizPage finish')`):
- Name: `disables End practice while the answer is being checked`.
- Arrange: `server.use(getGetSessionMockHandler(quizSession([quizItem(1), quizItem(2)])))`, plus a pending answer handler `http.post('*/api/sessions/:sessionId/answers', async () => { await delay('infinite'); })` (import `delay` from `msw`); `renderApp(\`/student/quiz/${quizSessionId}\`, { session: testSessions.student })`.
- Act: `user.click(await screen.findByRole('radio', { name: '3' }))`, then `user.click(screen.getByRole('button', { name: 'Check' }))`.
- Assert: `expect(await screen.findByRole('button', { name: 'Checking…' })).toBeDisabled()` and `expect(screen.getByRole('button', { name: 'End practice' })).toBeDisabled()`.
- Constrains: this test fails against the current code, because "End practice" stays enabled while the check is pending. It passes only with `|| isChecking`.

**Size limits** (`.claude/skills/react-feature/SKILL.md:16`, no file over 200 lines):
- Do NOT add the test to `QuizPage.test.tsx`: it is already 271 lines, over the limit (pre-existing, not introduced by this fix).
- Put it in `QuizPage.finish.test.tsx`: 37 lines now, about 52 after the change, so it stays well under 200. The subject ("End practice") matches the file's aspect.
- If the radio label '3' does not exist on `quizItem(1)` in this file's fixture, reuse the same fixture and label that `QuizPage.test.tsx:146` uses. Do not add a new helper.
