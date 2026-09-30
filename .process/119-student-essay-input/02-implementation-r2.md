# Implementation (rework r2) — [E14.S3] Student essay input (#119)

## Blocking findings addressed

| # | What I changed | File:line |
|---|---|---|
| 1 | `EssayGradeResultGenerator.Generate` now returns the `Pending` shape (no score, no criteria) while `grade.IsAwaitingApplication`, so `GET essay-grade` reads `Graded` only after Apply has written the attempt and the score. The existing Graded test now calls `MarkApplied`. Added the test `Handle_GradedNotYetApplied_ReportsPendingWithoutScore`. The integration helper `EssayGradingTestData.GradeAsync` already sends Apply, so `GetEssayGrade_AfterGrading_*` needed no change. I updated the GET paragraph in `docs/essay-grading.md`. | `api/Elmanhg.Application/EssayGrading/Shared/EssayGradeResultGenerator.cs:10-13`; `api/Elmanhg.Tests/Application/Features/EssayGrading/GetEssayGrade/GetEssayGradeHandlerTests.cs:38,46-56`; `docs/essay-grading.md:77` |
| 2 | I deleted the duplicate `essayQuestionId` variable, so only the original at `:19` remains. I added the "Approve essay question" request (QuestionValidation, after "Approve question", body `{version: 1}`), so the essay is servable before Sessions runs. Start quiz now sets `sessionQuestionId` from the first item whose type is not `Essay`. "Submit essay answer" and "Get essay grade" each have a pre-request `pm.execution.skipRequest()` for when `essayQuestionId` is empty. Their tests now expect a strict 200. The submit test also checks `attempt === null` and a non-null `pendingAnswer`. | `postman/elmanhg.postman_collection.json:1650-1678,1780-1781,1857-1896,1921-1935` |
| 3 | The `AnswerSavedAt` row now reads "Exams: when the draft was last saved. Quizzes: when a written essay was submitted." | `docs/sessions.md:37` |
| (d) ruling | `QuizEssayCard` is now loaded with `React.lazy` in `QuizRunner` and rendered inside `Suspense` with a `ContentListSkeleton` fallback, the same pattern as `AvatarDock`. The editor, draft status, grade status and outcome therefore leave the quiz route chunk. | `web/src/features/quiz/components/QuizRunner.tsx:1,11,29-40` |

perf:budget quiz chunk: **before 254/255 KB, after 251/255 KB** (brotli). The other chunks did not change: entry 202/210, landing 210/220, lesson 231/240.

## Files created
None.

## Files modified
| Path | Change |
|---|---|
| api/Elmanhg.Application/EssayGrading/Shared/EssayGradeResultGenerator.cs | Pending shape while the grade is awaiting application |
| api/Elmanhg.Tests/Application/Features/EssayGrading/GetEssayGrade/GetEssayGradeHandlerTests.cs | `MarkApplied` in the Graded test; new test for Graded but not applied → Pending |
| postman/elmanhg.postman_collection.json | Duplicate variable removed; Approve essay question; non-essay `sessionQuestionId`; skip guards; strict 200 tests |
| docs/sessions.md | `AnswerSavedAt` row |
| docs/essay-grading.md | GET paragraph: `Graded` only once applied |
| web/src/features/quiz/components/QuizRunner.tsx | Lazy `QuizEssayCard` + `Suspense` |

## Deviations
None. Finding #1's "update the #118 tests that seed Graded grades": the only read-path tests are `GetEssayGradeHandlerTests`, which I updated, and `EssayGradeEndpointTests`, which already apply through `GradeAsync`. The other Graded seeds go through Apply or domain paths and do not read the result generator.

## Build & test
- `dotnet test api/ -c Release` with `api/Elmanhg.Api/appsettings.json` moved to the scratchpad and then restored (the file is back in place): `Test run summary: Passed! total: 3843 failed: 0 succeeded: 3843 skipped: 0`
- `npm --prefix web run build` (includes `tsc -b` typecheck): `built`, `Precompressed 200 files`
- `npm --prefix web run lint`: eslint `--max-warnings=0` clean. `prettier --check` on QuizRunner.tsx: clean.
- `npm --prefix web test -- --run`: `Test Files 207 passed (207)  Tests 1191 passed (1191)`
- `npm --prefix web run perf:budget`: `entry 202/210 ok, landing 210/220 ok, lesson 231/240 ok, quiz 251/255 ok` (before the change: quiz 254/255)
- `postman/elmanhg.postman_collection.json` parses as valid JSON. I did not run it with Newman against a live API.
- No contract change, so `api/openapi/v1.json` and the Orval output did not change in this round.

## Notes for review
- "Approve essay question" relies on the essay still being at version 1 when approval runs. No request between "Create essay question" and approval edits it.
- Start quiz still overwrites `essayQuestionId` with the essay it served, or with an empty string. This is deliberate: the two essay requests then run only when the quiz actually served the essay (for example, the free tier or a question count can still leave it out).
- The Suspense fallback reuses `ContentListSkeleton` with `session.loading`. No new i18n keys.
- I added no web test for the lazy boundary. The existing essay-card and runner tests pass through Suspense.
