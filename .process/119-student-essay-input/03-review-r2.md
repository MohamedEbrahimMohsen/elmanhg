VERDICT: APPROVED

# Review r2 — [E14.S3] Student essay input (#119)

## Blocking
None.

## Round-1 findings
- **#1 fixed.** `api/Elmanhg.Application/EssayGrading/Shared/EssayGradeResultGenerator.cs:10-13` returns the Pending shape (no score, gradedAt, outcome or criteria) while `grade.IsAwaitingApplication` (`EssayGrade.cs:70`: `Graded && AppliedAt is null`). `Generate` is only called from `GetEssayGradeHandler.cs:21`, so no admin or other read path is affected. The new test `Handle_GradedNotYetApplied_ReportsPendingWithoutScore` (`GetEssayGradeHandlerTests.cs:46-56`) fails if the guard is removed, because Status would be "Graded" and Score 2.5. The Graded test now calls `MarkApplied` (`:38`). `docs/essay-grading.md` § Student API matches the code.
- **#2 fixed as specified.** There is one `essayQuestionId` variable. "Approve essay question" (`{version: 1}`) is added. Start quiz picks `sessionQuestionId` from the first non-Essay item. Both essay requests have `skipRequest()` guards, and their tests are strict 200 tests that assert `attempt === null` and `pendingAnswer`.
- **#3 fixed.** `docs/sessions.md:37` now reads "Exams: … Quizzes: when a written essay was submitted."
- **(d) lazy load is correct.** `QuizRunner.tsx:11` wraps `React.lazy`. Only the Essay branch sits inside `Suspense` (`:29-40`), so MCQ and other items render exactly as before with no fallback and no flash. The first essay shows `ContentListSkeleton` once while its chunk loads, the same pattern as `AvatarDock`. `key={nav.position}` is still on the card, so state reset per item is unchanged. The build emits separate `QuizEssayCard-*.js` and `EssayGradeStatus-*.js` chunks. `EssayGradeStatus` stays exported from `index.ts` for the result pages, which is expected.

## Non-blocking
- `postman/elmanhg.postman_collection.json`, "Approve question" vs. "Approve essay question": "Get validation queue" overwrites `questionId` with `items[0].id`, and "Retire question" has already retired the MCQ. On a fresh dataset the queue head is therefore probably the essay. "Approve question" would then approve it, and "Approve essay question" would get `QUESTION_NOT_PENDING`. In the same situation the quiz serves only the essay, so "Submit answer" posts an empty `sessionQuestionId` (400). Round 1 assumed `questionId` was still the MCQ at approval time; that assumption was wrong. This is not gated, because the collection cannot run top to bottom anyway: "Unassign subject" (#14) and "Delete subject" (#20) run before the units, lessons and validation steps that depend on them, so it relies on a seeded environment. A cheap hardening: skip "Approve essay question" when `essayQuestionId === questionId`, and skip "Submit answer" when `sessionQuestionId` is empty.
- Newman was not run. It is not installed, this repo has no Postgres (the only local DB belongs to another project and was not touched), and login needs a live OTP. The notes above come from reading the collection.
- Round-1 non-blocking items that were not addressed are still open (sweep head-of-line ordering, the untested `QUESTION_NOT_FOUND`/`EnsureOwnItem` guards, the startup check on the options cap). None of them gate.

## Verified
- API, CI parity (`api/Elmanhg.Api/appsettings.json` moved aside, then restored and confirmed present): `dotnet test api/ -c Release` gives **3843/3843 passed**, matching the claim (+1 over round 1).
- Web: `typecheck` 0, `lint` 0, prettier clean on QuizRunner.tsx, vitest **207 files / 1191 tests passed**, `build` OK (200 files precompressed), `perf:budget` entry 202/210, landing 210/220, lesson 231/240, **quiz 251/255** (was 254). All match `02-implementation-r2.md`.
- "Deviations: None" holds. The diff touches only the six files listed. The #118 Graded seeds on the read path go through `GradeAsync` → Apply.
- Design tokens only in QuizRunner. There are no new literals, and the fallback reuses the existing `session.loading` i18n key.

## Test quality
- `GetEssayGradeHandlerTests` now constrains the Applied/NotApplied split in both directions.
- No test covers the lazy boundary itself, but the existing essay-card and runner tests render through Suspense, so a broken lazy import would fail them. This is acceptable.
