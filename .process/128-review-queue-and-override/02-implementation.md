# Implementation — [E17.S1] Review queue and override (#128)

Worktree `D:/Personal/elmanhg-wt/128`, branch `feature/128-review-queue-and-override` (at `d299676`, which includes #123). Nothing was committed.

**#123 precondition check.** The plan's names all exist in the merged code, with the same meaning: `MathStepGrade`, `MathStepGradeStatus { Pending, InReview, Graded }`, `MathStepReviewReason { LowConfidence, GradingFailed, FinalAnswerUnchecked }`, `MathStepAttemptRecorder`, `MathStepGradeResult` / `MathStepGradeResultGenerator`, `MathStepScoreResult`, `IMathStepGradeRepository`, `AppDbContext.MathStepGrading.cs`, `MathStepGradeBuilder`, the web `MathStepGradeOutcome` and `mathStepGradeFixtures`, and `MathStepScoreList` / `MathStepsReadOnly`, which are already exported from `@/features/questions`. The one difference: `MathStepAttemptRecorder` calls `session.RecordAiGradedAttempt(...)`, not `RecordEssayAttempt`. That has no effect on the plan, because the only change there is `AttemptGrader.AI` → `grade.GradedBy`.

## Files created
| Path | Lines | Purpose |
|---|---|---|
| api/Elmanhg.Domain/Questions/Grading/GradeReviewDecision.cs | 3 | D-1 enum |
| api/Elmanhg.Domain/Questions/Grading/GradeReviewScore.cs | 9 | D-2 normalise to 4 decimals (with a WHY comment) |
| api/Elmanhg.Domain/EssayGrading/EssayGrade.Review.cs | 69 | D-3 `IAuditedEntity`, review props, `Final*`, `GradedBy`, `Accept` / `Override` / `Resolve` / `EnsureInReview` |
| api/Elmanhg.Domain/EssayGrading/EssayGradeReviewed.cs | 5 | D-4 event |
| api/Elmanhg.Domain/MathStepGrading/MathStepGrade.Review.cs | 68 | D-5 (no event) |
| api/Elmanhg.Domain/TrainingData/EssayGradeTrainingTrigger.cs | 3 | D-6 |
| api/Elmanhg.Domain/TrainingData/EssayGradeTrainingRecord.Review.cs | 52 | `FromReview` partial (the plan's conditional split, because the file would pass 100 lines) |
| api/Elmanhg.Application/Shared/Options/GradeReviewOptions.cs | 14 | P-1 |
| api/Elmanhg.Application/Shared/Realtime/IGradeReviewNotifier.cs | 6 | P-2 |
| api/Elmanhg.Application/GradeReviews/Shared/*.cs (GradeReviewKind, IGradeReviewInput, GradeReviewInputValidator, GradeReviewNoteResult, GradeReviewSubjectResult, GradeReviewItemResult, GradeReviewDetailResult, GradeReviewPlacement, GradeReviewPlacementLoader, GradeReviewRevisionLoader, GradeReviewResultGenerator, GradeReviewDetailGenerator) | 3–40 each | P-3 … P-14 |
| api/Elmanhg.Application/GradeReviews/GetGradeReviewSubjects/{Query,Handler}.cs | 6 / 52 | P-15, P-16 |
| api/Elmanhg.Application/GradeReviews/GetGradeReviewQueue/{Query,Validator,Handler}.cs | 8 / 22 / 59 | P-17 … P-19 |
| api/Elmanhg.Application/GradeReviews/GetEssayGradeReview/{Query,Validator,Handler}.cs | 7 / 14 / 21 | P-20 … P-22 |
| api/Elmanhg.Application/GradeReviews/GetMathStepGradeReview/{Query,Validator,Handler}.cs | 7 / 14 / 21 | P-23 |
| api/Elmanhg.Application/GradeReviews/ReviewEssayGrade/{Command,Validator,Handler}.cs | 14 / 18 / 51 | P-24 … P-26 |
| api/Elmanhg.Application/GradeReviews/ReviewMathStepGrade/{Command,Validator,Handler}.cs | 14 / 18 / 51 | P-27 |
| api/Elmanhg.Application/Events/TrainingRecords/EssayGradeReviewTrainingRecordHandler.cs | 31 | P-28 |
| api/Elmanhg.Infrastructure/Migrations/20260930205802_AddGradeReviews.cs (+ Designer) | 236 | I-1. `Trigger` is hand-edited to `defaultValue: "Completed"`. The only drop is the index swap `IX_…_EssayGradeId` → `IX_…_EssayGradeId_Trigger` |
| api/Elmanhg.Api/Realtime/GradeReviewedMessage.cs | 3 | A-1 |
| api/Elmanhg.Api/Realtime/SignalRGradeReviewNotifier.cs | 20 | A-2 (best-effort, with the WHY comment) |
| api/Elmanhg.Api/Controllers/GradeReviews/GradeReviewsController.cs | 74 | A-3: 6 actions, all `AiGradesOverride` |
| api/Elmanhg.Api/Controllers/GradeReviews/Requests.cs | 5 | A-4 |
| api/Elmanhg.Tests/Domain/EssayGrading/EssayGradeReviewTests.cs | 152 | tests 1–12 |
| api/Elmanhg.Tests/Domain/MathStepGrading/MathStepGradeReviewTests.cs | 110 | tests 13–20 |
| api/Elmanhg.Tests/Domain/Questions/Grading/GradeReviewScoreTests.cs | 13 | test 21 |
| api/Elmanhg.Tests/Application/Features/GradeReviews/** (11 classes) | 25–188 | tests 26–64 |
| api/Elmanhg.Tests/Application/Features/Events/EssayGradeReviewTrainingRecordHandlerTests.cs | 87 | tests 68–70 |
| api/Elmanhg.Tests/Integration/GradeReviews/GradeReviewTestData.cs | 96 | the plan's integration helper |
| api/Elmanhg.Tests/Integration/GradeReviews/{GradeReviewQueue,ReviewEssayGrade,ReviewMathStepGrade}EndpointTests.cs | 129 / 155 / 55 | tests 72–88 |
| api/Elmanhg.Tests/Integration/Persistence/GradeConcurrencyTests.cs | 38 | test 89 |
| api/Elmanhg.Tests/Integration/TrainingData/EssayGradeTrainingExportPageTests.cs | 36 | test 90 |
| web/src/features/gradeReview/** (index, locales, i18n ar/en, api ×3, schemas ×2, hooks ×3, components ×10, pages ×2) | 3–107 | F-1 … F-23 |
| web/src/features/gradeReview/**/*.test.ts(x) (6 files) | 16–189 | tests 93–98 |
| web/src/features/questions/components/GradingKeyView.tsx | 42 | F-24 |
| web/src/features/quiz/components/TeacherReviewNote.tsx | 23 | F-17 |
| web/src/routes/teacher/{grades.tsx, grade.$subjectId.$kind.$gradeId.tsx, more.tsx} | 6–7 | F-25 … F-27 |
| web/src/test/gradeReviewFixtures.ts | 123 | F-28 |
| docs/grade-review.md | 83 | the contract doc |

## Files modified
| Path | Change |
|---|---|
| api Domain: EssayGrade.Grading.cs, MathStepGrade.Grading.cs | `ToQuestionGrade` uses `Final*`. Math feedback is null when the grade is `Overridden` |
| api Domain: IEssayGradeRepository.cs, IMathStepGradeRepository.cs | `GetInReviewPageAsync`, `CountInReviewBySubjectAsync` |
| api Domain: SharedKernel/Exceptions/ErrorCodes.cs | `// GRADE REVIEW`: 3 codes |
| api Domain: TrainingData/EssayGradeTrainingRecord.cs | now `partial`; `Trigger` and review props; `From` sets `Completed` |
| api Application: Exceptions/ErrorCodes.cs | `// GRADE REVIEW`: 12 codes |
| api Application: EssayAttemptRecorder.cs, MathStepAttemptRecorder.cs | `grade.GradedBy` |
| api Application: EssayGradeResult(+Generator).cs, MathStepGradeResult(+Generator).cs | `Review` added; Graded branch uses `Final*`; an Overridden grade hides criteria or steps and the justification; math verdict is null-safe |
| api Application: TrainingExportLines.cs, TrainingExportLineGenerator.cs | 6 new essay export fields; the comment is scrubbed |
| api Application: DependencyInjection.cs | `GradeReviewOptions` with `ValidateOnStart` |
| api Infrastructure: EssayGradeRepository.cs, MathStepGradeRepository.cs | the two queries, as specified (test mode excluded) |
| api Infrastructure: EssayGradeTrainingRecordRepository.cs | export dedupe `Where` |
| api Infrastructure: AppDbContext.cs / .MathStepGrading.cs / .TrainingData.cs | review column mappings; `xmin` → 409 `GRADE_MODIFIED_CONCURRENTLY`; trigger-index unique violation → same 409; `(EssayGradeId, Trigger)` unique index |
| api Infrastructure: Migrations/AppDbContextModelSnapshot.cs | regenerated |
| api Api: RealtimeEvents.cs, RealtimeExtensions.cs, Resources/Messages.{ar,en}.resx (15 keys each), appsettings.example.json | as planned |
| api/openapi/v1.json | regenerated by build; stable on rebuild (same md5) |
| api Tests: EssayGradeBuilder.cs | `ForSubject`, `InReview(...)`, `GradingFailed(...)` |
| api Tests: GetEssayGradeHandlerTests, GetMathStepGradeHandlerTests, EssayGradeTrainingRecordTests, TrainingExportLineGeneratorTests, NotificationsHubTests | add-only (tests 22–25, 65–67, 71, 91) |
| api Tests: AppDbContextTests.cs | `_AddGradeReviews` appended (the accepted pattern) |
| web: shell/navConfig.ts (+ i18n) | `gradeReviews` item, tabs `['queue','gradeReviews','inbox']`, `morePath: '/teacher/more'` |
| web: questions/index.ts | exports `GradingKeyView`, `stemExcerpt`, `formatPendingAge` (`MathStepsReadOnly` and `MathStepScoreList` were already exported) |
| web: quiz EssayGradeOutcome.tsx, MathStepGradeOutcome.tsx (+ i18n) | criteria and justification shown only when present; `TeacherReviewNote` |
| web: shared/realtime/realtimeClient.ts, realtimeEvents.ts; askTeacher/hooks/useStudentRealtime.ts (+ i18n) | `gradeReviewed` handling |
| web: app/i18n.ts | `gradeReview` namespace |
| web: test/essayGradeFixtures.ts, mathStepGradeFixtures.ts | `review: null`, plus accepted and overridden fixtures |
| web tests: navConfig.test.ts, AppShell.test.tsx, EssayGradeStatus.test.tsx, MathStepGradeStatus.test.tsx, StudentRealtimeListener.test.tsx | the listed modifications and additions (tests 99–103) |
| web: orval.config.ts | `GetGradeReviewQueue` zod query generation off (see Deviations) |
| web: shared/api/generated/**, routeTree.gen.ts | regenerated; `gen:api` has no further diff |
| postman/elmanhg.postman_collection.json | folder `GradeReviews` with 7 requests in plan order, teacher bearer (collection auth), and variables `reviewEssayGradeId` / `reviewMathStepGradeId` |
| docs: PRD.md (§6.1 decided note, §8.3, §13, §15), essay-grading.md, math-step-grading.md, math-cas.md, training-data.md, audit-log.md, sessions.md, exams.md, claude-design-prompt.md, prototype.md, design-system.md, backlog.json (E17), deployment.md | docs-sync (see Notes) |
| deploy/api.env.example | `GradeReview__*` keys |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| i18n copy uses `{{name}}` interpolation | The app runs i18next-icu, whose syntax is `{name}` | Wrote every new key with single braces. The strings are unchanged |
| `teacherReview.comment` etc. in the quiz namespace | same ICU issue | `{comment}` |
| F-10: server field error `message: t('errors.<code>', { defaultValue })` | The form renders `t(message)`, and the zod messages are keys | `setError(field, { message: 'errors.<CODE>' })`, and added the 5 `errors.*` keys to the gradeReview en/ar files |
| F-20 `ReviewedCard` props `{ detail }` | Reading `detail.review` would need a non-null `!`, which skill §3 forbids | Props `{ detail, review }`. The page passes `detail.review` after the null check |
| F-22 / F-3 | The list item has to map a `kind` string to a route segment with type safety | Added a small `toKind(value)` helper in `api/gradeReviewOptions.ts` (a listed file) |
| Orval generated files only | The generated zod for `GetGradeReviewQueue` query params does not compile (`.default(1)` on a string format, TS2769). This is the known Orval issue that every other paged endpoint already opts out of | Added `GetGradeReviewQueue: { zod: { generate: { query: false } } }` to `web/orval.config.ts`, a file the plan does not list |
| Test 89 is one `[Fact]` | As a Fact on an AI-scored grade, the trigger-index mapping masked the `xmin` mapping: a mutation that removed the `xmin` catch survived | Same class and method name, now a `[Theory]` with `gradingFailed` true (`xmin` path) and false (trigger-index path). Both mutations are now killed |
| Test 97 `has no axe violations` via `toHaveNoViolations` | The repo's axe helper has no such matcher | `expect((await axe(container)).violations).toEqual([])`, the repo pattern |
| Postman folder name "Grade reviews" | Every folder in the collection is PascalCase | Named it `GradeReviews` |
| Orchestrator config checklist: add the key to ApiFactory | The plan says ApiFactory is not edited (the #125 lane owns it), and the code defaults cover the tests | Not edited. The keys are in `appsettings.example.json`, `docs/deployment.md` and `deploy/api.env.example` |
| Docs table does not list `docs/design-system.md`, `docs/exams.md` or PRD §6.1 | D19 gives the teacher 4 destinations shown as 3 tabs + «المزيد». The nav rule said "more than 4 destinations". `exams.md` said "waits for #128". PRD §6.1 said "may override any AI grade" | Nav rule changed to "more than 3" in docs/design-system.md and in the claude-design-prompt nav paragraph. `.claude/design-system.md` ("a role with more destinations shows 3 + المزيد") already agrees, so it is not touched. Updated the exams.md line. Added a "Decided (#128)" bullet under PRD §6.1 |

## Build & test
- **API (CI parity).** `api/Elmanhg.Api/appsettings.json` does not exist in this worktree, so nothing was moved. `dotnet build -c Release`: 0 errors, 0 new warnings (only the pre-existing core-libraries warnings). `dotnet test -c Release` (Docker/Testcontainers): **total 4587, failed 0, succeeded 4587**.
- **API format.** `dotnet format Elmanhg.slnx --verify-no-changes --exclude core-libraries` reports one issue, in `Elmanhg.Tests/Builders/SubscriptionBuilder.cs(57,27)`. That file is untouched and pre-existing. None of my files are reported.
- **Guard greps** (DateTime.Now/UtcNow, `.Result`, `async void`, FromSqlRaw, physical-direction utilities, hex colours) on the diff: clean.
- **OpenAPI** regenerated. A second build gives the same md5 (`fdcf1d83…`).
- **Orval** `npm run gen:api` regenerated. Re-running it gives no further diff. `routeTree.gen.ts` regenerated by vite.
- **Web.** `npx tsc -b --noEmit` exit 0. `npx eslint . --max-warnings=0` exit 0. `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto`: "All matched files use Prettier code style!". `npx vitest run --coverage`: **262 files, 1470 tests passed**, all files 95.36 % lines and 84.02 % branches. `npm run build` exit 0. `npm run perf:budget`: all ok (quiz 254/255 KB, teacher-home 256/265 KB).
- **ai/**: not touched, so not run.
- **Mutation checks** (a scripted break → run → restore, with the files restored byte for byte):
  - API: 18 + 25 + 36 + 7 + 2 mutations over domain, handlers, validators, generators, repositories, AppDbContext mappings and the hub event name. All are killed except one:
    - `From` → `Trigger = Completed` removal survives, because `Completed` is the enum default. Changing it to `TeacherReviewed` is killed.
    - A few first-attempt mutations did not compile (warnings are errors: unread parameter, unreachable code). I replaced them with equivalent compilable mutations, which were killed.
  - Web: 22 mutations across TeacherReviewNote, the outcome components, realtime, useReviewGrade (409, field mapping), the detail page (navigate, accept disabled, reviewed card), toReviewRequest, both schemas, the kind segments, the queue search and empty state, and navConfig. All 22 are killed.

## Notes for review
- **Security:**
  - Every action has `AiGradesOverride`. Queue, detail and review requests are `ISubjectScopedRequest`, and handlers also filter by `SubjectId`, so another subject's id gives 404 (integration tests 75, 78, 86, 88).
  - No student identity is in any grade-review result.
  - The teacher note renders as React text: no `SafeHtml`, with `dir="auto"`. i18n `escapeValue: false` is the existing setting, and React escapes the text.
  - Audit rows are written for Success and for the 403 Failure (test 86).
  - The `EssayGrade` / `MathStepGrade` audit diff lists only changed columns (status, the review fields, `AppliedAt`, `GradedAt`). The answer never changes, so it is never in the diff.
- **`StudentAnswerCard`** holds two small `zod` parse schemas for the answer JSON at module level. These are not form schemas. Skill §6.8 targets form schemas in `schemas/`, and the plan says "zod-parse" inside the component. Flagging it in case the reviewer reads §6.8 more broadly.
- **ReviewedCard** now has `role="status"` with `aria-labelledby` pointing to its «تمت المراجعة» heading, so tests can find it by name.
- **Detail page:** after a successful save it invalidates the detail and then navigates back to `/teacher/grades` with `{ subjectId, kind }`.
- **Toasts:** Sonner toasts persist across tests in a file, so the override test asserts the navigation and the request body rather than the toast text. The accept test asserts the toast.
- **Integration seeding:** the session is saved before the grade is completed. `EssayGradeCompleted` is dispatched before commit and looks the session up in the database.
- **ReviewGradeRequest.Decision** is non-nullable, as in the plan. A body without `decision` binds as `Accepted`, the enum default. The generated TS type makes `decision` required.
- **Performance:** the quiz chunk is at 254/255 KB after adding `TeacherReviewNote`, which leaves little headroom.
- **Docs-sync:** every doc the plan lists is updated, plus the extras in the last Deviations row.
