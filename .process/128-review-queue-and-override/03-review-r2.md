VERDICT: APPROVED

# Review r2: [E17.S1] Review queue and override (#128)

## Blocking

None. Blocking #1 from `03-review.md` is resolved.

## Non-blocking
- `web/src/features/askTeacher/hooks/useStudentRealtime.ts:12-15`: `readsSession` invalidates every query whose first key segment contains the session id. This is broader than before, because it also covers `/api/sessions/{id}` sub-resources that were not listed. It is safe because the ids are UUIDs. It is also cheap: one refetch of the active queries.
- `api/Elmanhg.Tests/Application/Features/GradeReviews/Get*ReviewHandlerTests.cs`: the new test-mode case stubs only a test-mode session. The other Get tests leave `CountAsync` unstubbed, where it returns 0. So in the Get handlers the `IsTestMode` half of the guard predicate is only constrained by the Review handler tests (`Seed` stubs non-test sessions) and by the integration test. That coverage is adequate.
- Opening the review note is now lazy (`TeacherReviewNote.tsx:3`, `fallback={null}`), so the note appears one chunk load after the grade renders. This deviation is disclosed and acceptable.

## Verified
- **B1 bundle budget:** I ran `npm run perf:budget` on this worktree (brotli, `ceil(bytes/1024)`). The results are entry 208/210, landing 216/220, lesson 237/240, quiz 252/255 and teacher-home 255/265, all ok, exit 0. By bytes and KB:
  - **Quiz:** 258,021 B against a budget of 261,120 B, so the headroom is 3,099 B (at least 2 KB). In KB it is 252, equal to main.
  - **Lesson:** 237, equal to main's 237.
  - **Entry:** +381 B over main. The report justifies this as the minimum eager cost: the route tree with the 3 teacher routes, the nav label and the student toast string. I accept that.
- **B1.1:** `gradeReview` is removed from `web/src/app/i18n.ts` (resources and ns). `features/gradeReview/locales.ts` uses `addResourceBundle`, and it is registered at module scope in both pages (`GradeReviewQueuePage.tsx:13`, `GradeReviewDetailPage.tsx:17`). Every `useTranslation('gradeReview')` consumer is inside the feature and is reached only through those pages.
- **B1.2:** `useStudentRealtime.ts` no longer imports from `generated/exams` or `generated/sessions`. The Orval keys (`/api/sessions/${id}...`, `/api/exams/${id}`) all contain the session id, so the predicate still covers session, essay grade, math grade and exam session. `StudentRealtimeListener.test.tsx` still passes on the refetched note.
- **NB1:** `GradeReviewSessionGuard.cs` returns 404 `GRADE_REVIEW_NOT_FOUND` for an existing test-mode session. It is called in all 4 handlers right after the grade lookup and before any mutation (`GetEssayGradeReviewHandler.cs:18`, `GetMathStepGradeReviewHandler.cs:18`, `ReviewEssayGradeHandler.cs:31`, `ReviewMathStepGradeHandler.cs:31`).
  - Unit tests cover all 4 handlers, and the review tests assert no attempt, `SaveChangesAsync` `DidNotReceive()`, and no notify.
  - The integration test `GetAndPost_TestModeGrades_Return404` covers all 4 endpoints and asserts that no attempt is written.
  - The guard lets a missing session pass. This deviation is disclosed, and it is consistent with the existing `Handle_SessionMissing_MarksAppliedWithoutAttempt`.
  - The guard follows house style: `sealed` is not applicable because the class is static, the namespace is file-scoped, and `ConfigureAwait(false)` is used.
- **NB2:** `GradeReviewQueuePage.tsx:19-20` resolves `assigned` from the subject list, falling back to the first subject, and the queue query uses only `assigned?.subjectId`. The new test asserts that no request carries the unassigned id and that every request carries the fallback id.
- **Nav icon swap:** `navConfig.ts:91` uses `ClipboardList`, which is already imported for admin. No doc names this icon. `ClipboardCheck` stays in `GradeReviewEmptyState` (lazy) and `landing/ValueProps`.
- **Docs:** `docs/grade-review.md` matches the new 404 behaviour and the subject fallback. The r2 changes add no endpoints and change no contracts, so Postman needs no change.
- **Merge 67c9bbd against `origin/main` (948b949):**
  - Every one of the 211 files changed only by main since the merge base `d299676` is byte-identical to `origin/main`, both in the merge commit and in the working tree. That covers the #115 rate limiting, `ActiveUserTokenValidation`, Kestrel hardening, the placeholder-secret guard, CSP and Caddy, the workflows, and all #126 DragDrop files.
  - I checked the 16 files changed by both sides. Every line removed relative to main is one of this story's intended edits:
    - the `EssayGradeId` unique index swap in the snapshot;
    - the migration list in `AppDbContextTests`, which ends `_AddIssuedRefreshTokens`, `_AddGradeReviews`;
    - PRD §training-data;
    - the nav "more than 3" rule in both design docs;
    - the `prototype.md` teacher step;
    - the `sessions.md` `GradedBy` row, where main's drag-and-drop wording is kept.
  - The `IssuedRefreshToken` entity is present in the merged snapshot.
- **Build and tests, re-run by me:**
  - **API:** `api/Elmanhg.Api/appsettings.json` is absent (CI parity). `dotnet test api/Elmanhg.slnx -c Release` gave total 4727 and failed 0.
  - **Web:**
    - `tsc -b --noEmit` exit 0;
    - `eslint . --max-warnings=0` exit 0;
    - `prettier --check` clean;
    - `vitest run` 272 files and 1523 tests passed;
    - `npm run build` exit 0;
    - `perf:budget` exit 0.
  - All of these match `02-implementation-r2.md`.

## Test quality
The new tests constrain the change:
- The test-mode 404 cases would fail if the guard were removed or placed after the mutation.
- The queue fallback test would fail if the raw URL id were used.
- `teacherReviewLocales.test.ts` would fail if the strings went back into the eager quiz bundle.

No vacuous tests.
