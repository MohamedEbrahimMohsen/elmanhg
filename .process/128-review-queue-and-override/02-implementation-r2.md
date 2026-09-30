# Implementation r2: Review queue and override (#128)

## Step 1: WIP commit and main merge (committed locally, not pushed)

- `3247d26 feat(E17.S1): Review queue and override (wip before main merge)`
- `67c9bbd Merge origin/main into feature/128-review-queue-and-override`. This merged #115 (security hardening) and #126 (DragDrop student canvas). There were three conflicts, and each was resolved by keeping both sides:
  - `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs`: migrations are in timestamp order. `_AddIssuedRefreshTokens` (20260930192717) is 39th, then `_AddGradeReviews` (20260930205802) is 40th.
  - `docs/claude-design-prompt.md` §quiz line: main's drag-and-drop text plus #128's teacher-review note and toast text.
  - `docs/sessions.md` `GradedBy` row: main's "deterministic type (v1 + drag-and-drop)" wording plus #128's `Teacher` meaning.
- Post-merge checks:
  - `dotnet build api/ -c Release` produced 0 errors and no OpenAPI drift.
  - `npm --prefix web run gen:api` produced no Orval drift.
  - Every `web/src/**/i18n/*.json` parses as JSON.
  - `tsc -b` exits 0.

## Step 2: review findings (uncommitted)

| # | Finding | Change | File(s) |
|---|---|---|---|
| B1.1 | `gradeReview` i18n was eager in the entry | Removed from `app/i18n.ts` (`resources` and `ns`). `features/gradeReview/locales.ts` now exports `registerGradeReviewLocales()` (`addResourceBundle`, same as `users/locales.ts`), and both pages call it at module scope. | `web/src/app/i18n.ts`, `web/src/features/gradeReview/locales.ts`, `pages/GradeReviewQueuePage.tsx:11-13`, `pages/GradeReviewDetailPage.tsx:15-17` |
| B1.2 | `useStudentRealtime` imported the generated `exams` module for one key | The generated key imports are gone, both `exams` and `sessions`. One `invalidateQueries({ predicate: readsSession(sessionId) })` now refreshes every query whose first key segment contains the session id: session, essay grade, math grade, exam session and exam result. | `web/src/features/askTeacher/hooks/useStudentRealtime.ts:11-14,33` |
| B1.3 | Student pages must be at or below main | Further cuts listed under "Extra size work" below. Numbers are in the table. | — |
| NB1 | Detail and review endpoints did not exclude test-mode grades | New `GradeReviewSessionGuard.EnsureNotTestModeAsync(sessionId, ISessionRepository, ct)` throws 404 `GRADE_REVIEW_NOT_FOUND` when the grade's session is a test-mode session. It is called in all four handlers before any read or mutation. The two detail handlers now take `ISessionRepository`. | `api/Elmanhg.Application/GradeReviews/Shared/GradeReviewSessionGuard.cs`, `GetEssayGradeReviewHandler.cs:17`, `GetMathStepGradeReviewHandler.cs:17`, `ReviewEssayGradeHandler.cs:31`, `ReviewMathStepGradeHandler.cs:31` |
| NB2 | The queue page fired a 403 request for an unassigned `subjectId` in the URL | The page resolves `assigned = subjects.find(search.subjectId) ?? subjects[0]`. The queue query waits for that value, so it never uses a raw URL id, and an unknown id falls back to the first assigned subject. | `web/src/features/gradeReview/pages/GradeReviewQueuePage.tsx:19-20,68-80` |

### Extra size work (needed to get quiz back to main's 252 KB)

- **Teacher-review note is lazy.** `TeacherReviewNote.tsx` is now a `React.lazy` of the new `TeacherReviewNoteContent.tsx`. Both outcomes wrap it in `<Suspense fallback={null}>`. Its three strings moved out of the eager `quiz` namespace into a lazily registered `quizTeacherReview` namespace: `teacherReviewLocales.ts` and `i18n/teacherReview.{ar,en}.json`, the same pattern as main's `questions/diagramStudentLocales.ts`.
- **Teacher nav icon.** "مراجعة التصحيح" now uses `ClipboardList` instead of `ClipboardCheck`. `ClipboardList` is already in `navConfig` for the admin nav, so no new icon chunk is loaded on every student page. No doc names this icon.
- **`gradeReviewSearchSchema`** inlines `['Essay', 'MathSteps']`, so `gradeReviewOptions` stays out of the entry chunk.

### Tests added or changed

- **API unit tests:**
  - New `Handle_TestModeSession_ThrowsGradeReviewNotFound` in `GetEssayGradeReviewHandlerTests`, `GetMathStepGradeReviewHandlerTests` and `ReviewMathStepGradeHandlerTests`.
  - In `ReviewEssayGradeHandlerTests`, `Handle_TestModeSession_RecordsAttemptWithoutMastery` is replaced by `Handle_TestModeSession_ThrowsGradeReviewNotFound`. The old behaviour can no longer be reached.
  - New `SessionRepositoryStub.StubCount`.
- **API integration:** `ReviewMathStepGradeEndpointTests.GetAndPost_TestModeGrades_Return404` checks that detail and review for both kinds return 404 and write no attempt.
- **Web:**
  - `GradeReviewQueuePage` "falls back to the first subject when the URL names an unassigned one" asserts that no request carries the unassigned id.
  - `teacherReviewLocales.test.ts` covers registration, and checks the strings are not in the eager quiz bundle.
  - The note assertions in `EssayGradeStatus`, `MathStepGradeStatus` and `StudentRealtimeListener` tests now use `findByRole('note')` because the note is lazy.

### Docs

`docs/grade-review.md` now says that direct detail or review of a test-mode grade returns 404, and that a missing or unassigned `subjectId` opens the first assigned subject.

## Deviations

| Plan / review said | Reality | What I did |
|---|---|---|
| Fix only the `gradeReview` i18n and the exams import | Those two fixes left quiz at 258 531 B (253 KB), above main's 252 | I added the lazy teacher note, the nav icon swap and the inlined kinds (described above). This created 4 new web files: `TeacherReviewNoteContent.tsx`, `teacherReviewLocales.ts` and its test, and `i18n/teacherReview.{ar,en}.json`. |
| The review suggested an exact key or key predicate for the exam query | A `startsWith('/api/sessions/…' \| '/api/exams/…')` predicate cost about 70 B more | The predicate is `String(queryKey[0]).includes(sessionId)`. Session ids are UUIDs, so only that session's queries match. |
| The guard mirrors the queue's "session exists and is not test mode" | The existing `Handle_SessionMissing_MarksAppliedWithoutAttempt` expects a review to succeed with no session row | The guard rejects only an **existing test-mode** session. A missing session still passes, as before. |
| New file `GradeReviewSessionGuard.cs` | It was not in the original plan | This is a small shared helper, so the four handlers do not repeat the check. |

## Build & test

- `dotnet build api/ -c Release`: 0 errors. `api/Elmanhg.Api/appsettings.json` is absent in this worktree, which matches CI.
- `dotnet test api/ -c Release --no-build`: **total 4727, failed 0, succeeded 4727**.
- Web (`web/`) checks:
  - `tsc -b --noEmit`: exit 0.
  - `eslint . --max-warnings=0`: exit 0.
  - `prettier --check --end-of-line auto .`: clean.
  - `vitest run`: **272 files, 1523 tests passed**.
  - `npm run build`: exit 0.
  - `npm run perf:budget`: all ok (exit 0).

### perf:budget (brotli-11, KB is ceil, bytes in brackets)

| Page | `origin/main` 948b949 | merged, before fixes | after fixes | budget |
|---|---|---|---|---|
| entry | 207 (211 639) | 209 (213 389) | **208** (212 020) | 210 |
| landing | 216 (220 244) | 217 (222 140) | **216** (220 771) | 220 |
| lesson | 237 (241 683) | 240 (245 062) | **237** (242 321) | 240 |
| quiz | 252 (257 100) | **256 OVER** (261 206) | **252** (258 021), 3 KB headroom | 255 |
| teacher-home | 254 (259 898) | 257 (262 183) | 255 (260 593) | 265 |

- In perf:budget KB, lesson and quiz now equal main, lesson stays below 240, and quiz has 3 KB headroom.
- In raw bytes they are still about 0.6–0.9 KB above main, and entry is 208 against main's 207 (+381 B).
- That remainder is the minimum eager cost of the feature:
  - the TanStack route tree plus the three new teacher route stubs (`/teacher/grades`, `/teacher/grade/…`, `/teacher/more`): about 1.1 KB raw;
  - the `gradeReviews` nav label;
  - the student toast string «راجع معلمك إحدى إجاباتك، وأصبحت درجتها نهائية.».
- These must be in the entry. Moving the toast string lazily would push quiz over 252.
- Main's numbers were measured by building `git archive origin/main web` in the session scratchpad with the same brotli logic.

## Notes for review

- The note now appears one lazy chunk after the grade renders, with no layout placeholder (`fallback={null}`).
- `quizTeacherReview` is a new i18n namespace. The keys are `accepted`, `overridden` and `comment`, with the same text as before.
- Main added a `diagramStudent` lazy namespace the same way, so this follows an existing pattern.
