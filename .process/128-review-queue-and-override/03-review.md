VERDICT: CHANGES_REQUESTED

# Review — [E17.S1] Review queue and override (#128)

## Blocking

### 1. Merging with main puts the quiz page over its 255 KB budget (256/255). The extra weight is not the review note.
**Where:** `web/src/app/i18n.ts:12`, `:60`, `:84`, `:129` (the `gradeReview` namespace is registered eagerly), and `web/src/features/askTeacher/hooks/useStudentRealtime.ts:5` (`getGetExamSessionQueryKey` import).
**Rule:** orchestrator gate ("blocking if merging with main would exceed 255"); `web/scripts/perf/budgets.json` quiz `maxKb: 255`.
**Problem:** I measured with brotli-11, using the repo's own `budgetCli` logic, on four builds: base `d299676`, this worktree, `origin/main` (`948b949`), and `origin/main` with this story's web diff applied on top.

| Build | entry | lesson | quiz |
|---|---|---|---|
| base `d299676` | 207 | 236 | 250 |
| #128 worktree | 209 | 239 | 254 |
| `origin/main` | 207 | 237 | **252** (not ~248) |
| main + #128 | 209 | 240/240 | **256/255 OVER** |

This story adds 4,280 B to the quiz page. Here is where it comes from, per chunk:
- `index.html` entry: +1,834 B. Most of this is the `gradeReview` en and ar JSON (7.4 KB raw), which `app/i18n.ts` bundles into the entry. That namespace is teacher-only, yet every page pays for it.
- New `_exams` chunk: +1,373 B. The student layout (`useStudentRealtime`) now imports `getGetExamSessionQueryKey`, and that pulls the whole generated `exams` module into `student/route`.
- `MathStepGradeStatus`: +283 B. This is `TeacherReviewNote` plus the quiz i18n strings. Lazy-loading the review note would save about 0.3 KB, so on its own it does not fix the overrun.
- The rest is chunk reshuffling: `_mathStepsValue`, `_QuestionView`, `circle-alert` and `clipboard-check` together come to about +0.5 KB.

**Failure:** after `git merge origin/main`, `npm run perf:budget` prints `quiz 256/255 KB OVER` and exits 1, so CI goes red. The lesson page lands exactly on its 240 KB limit.
**Fix:**
1. Register `gradeReview` lazily, following the existing pattern in `features/users/locales.ts` (`registerUsersLocales()` → `addResourceBundle`, called from the page module). Remove it from `app/i18n.ts` `resources` and `ns`.
2. In `useStudentRealtime`, stop importing from `generated/exams/exams`. For example, invalidate the exam session query with a key predicate, or with the literal key `['/api/exams/${sessionId}']` behind a small shared helper.
3. Re-measure after `git merge origin/main`, and record the merged numbers in `02-implementation.md`.

## Non-blocking
- `api/Elmanhg.Application/GradeReviews/GetEssayGradeReview/GetEssayGradeReviewHandler.cs:16` and `ReviewEssayGrade/ReviewEssayGradeHandler.cs:30` (and the math twins) exclude test-mode grades only from the queue and counts, as D1 states. An assigned teacher who has an admin trial grade's id can still open or review it directly. Consider adding the `!IsTestMode` session check here too.
- `web/src/features/gradeReview/pages/GradeReviewQueuePage.tsx:16,66`: when the URL has a `subjectId` that is not in the teacher's list, the page shows «لا توجد مواد مسندة إليك» and still fires a queue request, which returns 403. It could fall back to the first subject instead.
- `api/Elmanhg.Domain/TrainingData/EssayGradeTrainingRecord.cs:75`: `Trigger = Completed` is the enum default, so removing that line is a mutation that survives (the implementer already disclosed this).

## Verified
- **API tests (CI parity):** `api/Elmanhg.Api/appsettings.json` is absent in this worktree. `dotnet test api/Elmanhg.slnx -c Release` → total 4587, failed 0. This matches the claim.
- **Web:** `tsc -b --noEmit` 0, `eslint --max-warnings=0` 0, `vitest run` 262 files / 1470 tests passed, `npm run build` 0, `perf:budget` all ok on this worktree (quiz 254/255, teacher-home 256/265). All match the claims.
- **Subject scoping / IDOR:**
  - All 6 actions have `[Authorize(Policy = DefaultCodes.AiGradesOverride)]` (`GradeReviewsController.cs:22-67`).
  - The queue, detail and review requests implement `ISubjectScopedRequest`.
  - Every handler filters by `x.SubjectId == request.SubjectId`, so another subject's id returns 404 `GRADE_REVIEW_NOT_FOUND`.
  - Detail also requires `ReviewReason != null`.
  - Unit tests evaluate the real predicate, and integration tests 75, 78, 86 and 88 cover the endpoints.
- **Audit:**
  - Both commands are `IAuditableCommand`, and both aggregates are `IAuditedEntity`.
  - Test 86 asserts that the 403 writes a `Failure` row, and test 79 asserts the `Success` row.
  - No grade-creating command is auditable, so the student's answer never enters an audit diff.
- **Double decision:**
  - `EnsureInReview` returns 409 `GRADE_NOT_IN_REVIEW` (test 81).
  - Two `AppDbContext.cs` mappings return 409 `GRADE_MODIFIED_CONCURRENTLY`: `DbUpdateConcurrencyException` on `EssayGrade`/`MathStepGrade` (`:129`) and the trigger-index unique violation (`:175`).
  - Test 89 is a Theory that covers both paths.
- **Override bounds:** the domain enforces `0 ≤ score ≤ MaxScore`. The validator allows at most 2 decimals, requires a comment on override and forbids a score on accept. Per-criterion or per-step marks are out of scope by plan D6 (the teacher sets a total), so criterion and step bounds do not apply.
- **Attempts are never mutated:**
  - The review is the first attempt, written by the existing recorders with `grade.GradedBy`.
  - `RecordAiGradedAttempt` returns null if an attempt already exists, and recomputes `ScorePercent` on submitted sessions (test 28), which covers the quiz and exam scores.
  - Mastery is skipped in test mode.
  - There is one `SaveChangesAsync`, and the notifier runs after the commit.
- **Training records:**
  - `FromReview` copies every AI field and adds the review fields, with `OccurredAt` equal to the AI `GradedAt`.
  - The export `Where` keeps the `TeacherReviewed` row and drops the `Completed` row only when a `TeacherReviewed` row exists (test 90). There is no double counting, and no AI data is lost.
  - GradingFailed and test-mode reviews write no essay row.
  - The attempt row carries `GradedBy = Teacher`.
- **Realtime:** `Clients.User(studentId)` sends only to the owning student, best-effort (`SignalRGradeReviewNotifier.cs`). The web parses the payload with zod and ignores a malformed one (test 101).
- **Teacher note:**
  - It is rendered as React text with `dir="auto"` (`TeacherReviewNote.tsx:16-19`, `ReviewedCard.tsx:34-37`), never as HTML.
  - It is capped by `GradeReview:CommentMaxLength` (2000) on both the API and the client.
  - It is scrubbed in the export.
- **No student identity** in `GradeReviewItemResult`, `GradeReviewDetailResult` or `GradeReviewNoteResult`, and no teacher identity in the student note.
- **Migration:**
  - It contains only the listed nullable columns.
  - `Trigger` is NOT NULL with default `'Completed'`, a constant default that causes no rewrite, so the append-only trigger does not fire.
  - The only index change is the planned swap to `IX_EssayGradeTrainingRecords_EssayGradeId_Trigger`. `AppDbContextTests` is updated.
- **Postman:** the `GradeReviews` folder has the 7 requests in state order, with inherited collection bearer auth, plausible bodies, and the `reviewEssayGradeId` / `reviewMathStepGradeId` variables.
- **Docs-sync:**
  - These docs agree with the code: PRD §6.1, §8.3, §13 and §15; `grade-review.md`; `essay-grading.md`; `math-step-grading.md`; `math-cas.md`; `training-data.md`; `audit-log.md`; `sessions.md`; `exams.md`; `claude-design-prompt.md`; `prototype.md`; `backlog.json`.
  - The nav rule in `docs/design-system.md` ("more than 3") agrees with `.claude/design-system.md`.
- **Deviations:** every row in the implementation’s Deviations table is accurate and justified. That covers ICU braces, the Orval zod opt-out, the Test 89 Theory, the `ReviewedCard` props and the extra docs.
- **Design tokens:** every visual value in the new web code is a design-system token: `bg-soft`, `accent-soft`, `rounded-pill`, `shadow-1`, `text-ui`/`caption`/`micro`/`h3`. There are no hex colours and no physical-direction utilities.

## Test quality
- **Domain** (`EssayGradeReviewTests`, `MathStepGradeReviewTests`, `GradeReviewScoreTests`): they assert real state transitions and codes, so they constrain the implementation.
- **Handlers** (`ReviewEssayGradeHandlerTests`, `ReviewMathStepGradeHandlerTests`, `Get*ReviewHandlerTests`, `GetGradeReviewQueueHandlerTests`, `GetGradeReviewSubjectsHandlerTests`): the repository substitutes compile and evaluate the handler’s own predicate, so the subject filter and `ReviewReason` filter are genuinely tested. `SaveChangesAsync` is checked with `Received(1)` on success and `DidNotReceive()` on throwing paths.
- **Validators:** every rule has a failing case.
- **Integration** (queue, review, concurrency, export page, hub): they hit the real PostgreSQL behaviour (xmin, unique index, export dedupe, audit rows).
- **Web** (page tests with MSW body assertions, schemas, realtime listener): they constrain the implementation.
- **Vacuous tests:** none found.
