# Implementation — [E5.S4] Mastery calculation and headline counter (#77)

Two implementer runs. Run 1 did the whole API, OpenAPI, the Orval regen and the web production code, then a tool outage cut it off. Run 2 (this report) did the web tests, the AppShell/Login/SignUp test updates, Postman, docs, the CI-parity run and the web mutation checks. Run 2 changed one production line (a lint fix, see Notes).

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/Elmanhg.Domain/Mastery/MasteryAttempt.cs` | 8 | #1 attempt snapshot record |
| `api/Elmanhg.Domain/Mastery/QuestionMastery.cs` | 65 | #2 aggregate (`Start`, `Record`) |
| `api/Elmanhg.Domain/Mastery/IQuestionMasteryRepository.cs` | 8 | #3 port |
| `api/Elmanhg.Domain/Mastery/LessonMasteryCount.cs` | 3 | #4 per-lesson counts |
| `api/Elmanhg.Domain/Mastery/MasteryTotals.cs` | 14 | #5 pooled totals, floor percent |
| `api/Elmanhg.Domain/Mastery/NextLessonRecommendation.cs` | 17 | #6 next lesson pick |
| `api/Elmanhg.Domain/Mastery/StudyStreak.cs` | 18 | #7 streak count |
| `api/Elmanhg.Application/Shared/Options/ProgressOptions.cs` | 14 | #8 options |
| `api/Elmanhg.Application/Mastery/GetMasteryOverview/GetMasteryOverviewQuery.cs` | 6 | #9 |
| `api/Elmanhg.Application/Mastery/GetMasteryOverview/GetMasteryOverviewHandler.cs` | 38 | #10 |
| `api/Elmanhg.Application/Mastery/GetSubjectMastery/GetSubjectMasteryQuery.cs` | 6 | #11 |
| `api/Elmanhg.Application/Mastery/GetSubjectMastery/GetSubjectMasteryValidator.cs` | 13 | #12 |
| `api/Elmanhg.Application/Mastery/GetSubjectMastery/GetSubjectMasteryHandler.cs` | 35 | #13 |
| `api/Elmanhg.Application/Mastery/Shared/*Result.cs` (7 files) | 3 each | #14–#20 result records |
| `api/Elmanhg.Application/Mastery/Shared/MasteryOverviewResultGenerator.cs` | 32 | #21 |
| `api/Elmanhg.Application/Mastery/Shared/SubjectMasteryResultGenerator.cs` | 35 | #22 |
| `api/Elmanhg.Infrastructure/Mastery/QuestionMasteryRepository.cs` | 31 | #23 grouped servable query |
| `api/Elmanhg.Infrastructure/Migrations/20260928150149_AddQuestionMastery.cs` | 95 | #24 table, FKs, indexes, `BackfillSql` |
| `api/Elmanhg.Infrastructure/Migrations/20260928150149_AddQuestionMastery.Designer.cs` | 1669 | #25 generated |
| `api/Elmanhg.Api/Controllers/Mastery/MasteryController.cs` | 33 | #26 two GET actions |
| `api/Elmanhg.Tests/Domain/Mastery/QuestionMasteryTests.cs` | 155 | T1–T11 |
| `api/Elmanhg.Tests/Domain/Mastery/MasteryAttemptTests.cs` | 19 | T12 |
| `api/Elmanhg.Tests/Domain/Mastery/MasteryTotalsTests.cs` | 45 | T13–T16 |
| `api/Elmanhg.Tests/Domain/Mastery/NextLessonRecommendationTests.cs` | 48 | T17–T19 |
| `api/Elmanhg.Tests/Domain/Mastery/StudyStreakTests.cs` | 43 | T20–T23 |
| `api/Elmanhg.Tests/Application/Features/Mastery/QuestionMasteryRepositoryStub.cs` | 14 | #32 stub helper |
| `api/Elmanhg.Tests/Application/Features/Mastery/ProgressOptionsTests.cs` | 40 | T28–T29 |
| `api/Elmanhg.Tests/Application/Features/Mastery/GetMasteryOverview/GetMasteryOverviewHandlerTests.cs` | 112 | T30–T35 |
| `api/Elmanhg.Tests/Application/Features/Mastery/GetSubjectMastery/GetSubjectMasteryHandlerTests.cs` | 102 | T36–T39 |
| `api/Elmanhg.Tests/Application/Features/Mastery/GetSubjectMastery/GetSubjectMasteryValidatorTests.cs` | 26 | T40–T41 |
| `api/Elmanhg.Tests/Integration/Mastery/MasteryTestData.cs` | 69 | #37 helper |
| `api/Elmanhg.Tests/Integration/Mastery/MasteryOverviewEndpointTests.cs` | 161 | T42–T50 |
| `api/Elmanhg.Tests/Integration/Mastery/SubjectMasteryEndpointTests.cs` | 109 | T51–T55 |
| `api/Elmanhg.Tests/Integration/Persistence/QuestionMasteryPersistenceTests.cs` | 113 | T56–T59 |
| `web/src/features/mastery/index.ts` | 2 | #41 barrel |
| `web/src/features/mastery/locales.ts` | 4 | #42 |
| `web/src/features/mastery/i18n/en.json`, `ar.json` | 25 each | #43–#44 |
| `web/src/features/mastery/api/invalidateMastery.ts` | 12 | #45 |
| `web/src/features/mastery/api/invalidateMastery.test.ts` | 18 | #46 W9 (run 2) |
| `web/src/features/mastery/components/MasteryBar.tsx` | 15 | #47 |
| `web/src/features/mastery/components/HeadlineCounterCard.tsx` | 29 | #48 |
| `web/src/features/mastery/components/NextLessonCard.tsx` | 31 | #49 |
| `web/src/features/mastery/components/SubjectMasteryCard.tsx` | 29 | #50 |
| `web/src/features/mastery/pages/StudentHomePage.tsx` | 61 | #51 |
| `web/src/features/mastery/pages/StudentHomePage.test.tsx` | 97 | #52 W1–W8 (run 2) |
| `web/src/test/masteryFixtures.ts` | 24 | #53 (run 2) |
| `docs/mastery.md` | 123 | #54 (run 2) |
| `web/src/shared/api/generated/mastery/**`, `zod/mastery/**`, `model/{lessonMastery,masteryHeadline,masteryOverview,nextLesson,subjectMasteryDetail,subjectMastery,unitMastery}Result.ts` | generated | Orval regen |

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.Domain/Sessions/ISessionRepository.cs` | `GetQuizActivityDaysAsync` |
| `api/Elmanhg.Infrastructure/Sessions/SessionRepository.cs` | Interpolated `SqlQuery<DateOnly>` implementation |
| `api/Elmanhg.Application/Sessions/SubmitAnswer/SubmitAnswerHandler.cs` | New constructor; mastery step for new, non-test attempts; single save |
| `api/Elmanhg.Application/DependencyInjection.cs` | `ProgressOptions` (validated, on start) + `TimeProvider.System` |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | `QuestionMasteries` set, config, query filter, index const, 409 mapping for xmin and unique violation |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | `IQuestionMasteryRepository` registration |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated |
| `api/Elmanhg.Api/appsettings.example.json` | `Progress` section |
| `api/openapi/v1.json` | Regenerated by build |
| `api/Elmanhg.Tests/Application/Features/Sessions/SubmitAnswer/SubmitAnswerHandlerTests.cs` | Constructor wiring + T24–T27 |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | `_AddQuestionMastery` in the migration list |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | `Progress:*` keys |
| `postman/elmanhg.postman_collection.json` | New "Mastery" folder after "Sessions": "Get mastery overview", "Get subject mastery" (run 2) |
| `web/src/routes/student/index.tsx` | `StudentHomePage` replaces the placeholder |
| `web/src/app/i18n.ts` | `mastery` namespace |
| `web/src/features/quiz/hooks/useQuizAnswer.ts` | `void invalidateMastery(queryClient)` in `onSuccess` |
| `web/src/features/shell/components/AppShell.test.tsx` | `getMasteryMock()` in `beforeEach`; two `'Home'` headings become `'Hello, أحمد'` (run 2) |
| `web/src/features/session/pages/LoginPage.test.tsx` | Deviation 4 (run 2) |
| `web/src/features/session/pages/SignUpPage.test.tsx` | Deviation 4 (run 2) |
| `web/src/shared/api/generated/{index.ts,model/index.ts,zod/index.zod.ts}` | Orval regen |
| `docs/PRD.md` §15 | `QuestionMastery` line with typed columns (run 2) |
| `docs/sessions.md` | Lifecycle step 2, `Mastery:CorrectThreshold` row, `SESSION_MODIFIED_CONCURRENTLY` row (run 2) |
| `docs/audit-log.md` | "Question mastery" under "Not audited" (run 2) |
| `docs/backlog.json` | E5 task text: no recalculation job (run 2) |

`PROGRESS.md` is also modified in the working tree. The orchestrator did that (per-story rhythm step 1), not the implementer.

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| T48: "After mastering q1, retire q1 → mastered 0, seen 1" | The test flow also answers q2 wrongly (`MasterFirstSeeSecondAsync`, the same flow as T46). The "seen 1" is q2, because q1 is no longer servable. | Kept it. It matches the plan's expected numbers and proves that seen is also intersected with servable. |
| `DependencyInjection.cs`: the `ProgressOptions` registration on one line | That line would be about 250 characters. | Split the fluent chain across three lines. Same calls, same order. |
| Migration review: "the unique index `IX_QuestionMasteries_StudentId_QuestionId`" | EF also generates `IX_QuestionMasteries_QuestionId` for the `QuestionId` FK, because the composite unique index leads with `StudentId`. | Kept the generated FK index. It supports the restrict FK check on question deletes. |
| Existing code touched lists only `AppShell.test.tsx` for the "Home" heading | `LoginPage.test.tsx` (4 assertions) and `SignUpPage.test.tsx` (2) also sign a student in and expected heading "Home". The placeholder is gone, and without a mastery handler MSW errors on the unhandled `/api/mastery/overview`. | Added `...getMasteryMock()` to those tests' `server.use` (a new `server.use(...getMasteryMock())` in the signed-in redirect test). Changed the heading to `'Hello, Mona'` (the auth result's display name), or `'Hello, أحمد'` for `testSessions.student`. No other assertion changed. |

## Build & test
All run on the laptop, with Docker 29.6.2 up.
- `dotnet build api/ -c Release`: `0 Error(s)`. The 9 warnings are all in vendored `api/core-libraries` (CS8618 in Core.Notifications). An incremental rebuild of the Elmanhg projects shows `0 Warning(s)`.
- `dotnet test api/ -c Release` with `api/Elmanhg.Api/appsettings.json` moved aside (restored afterwards): `Test run summary: Passed! total: 1484 failed: 0 succeeded: 1484 skipped: 0`.
- `--filter-class "*MasteryOverviewEndpointTests"` alone: `total: 9, succeeded: 9`. This confirms the Testcontainers integration tests execute.
- OpenAPI drift: the `api/openapi/v1.json` md5 is `50dc96a5…` both before and after the build. No drift.
- `npm --prefix web run typecheck` (`tsc -b`): clean.
- `npm --prefix web run lint`: clean, after the one-line fix in Notes.
- `npm --prefix web test -- --run`: `Test Files 80 passed (80) · Tests 499 passed (499)`.
- Orval drift: I hashed all 120 files under `web/src/shared/api/generated` before and after `npm --prefix web run gen:api`. They are identical.
- Mutation checks, run 2:
  - W2: `params={{ lessonId: lesson.subjectId }}` in `NextLessonCard` → "links the suggested lesson to its practice page" failed.
  - W9: predicate changed to `startsWith('/api/')` → "marks mastery queries stale and leaves other queries fresh" failed.
  - Both files were restored and the tests are green again.
- Mutation checks T25, T27, T35 and T48 belong to run 1's scope. Run 2 was asked only for W2 and W9, so it did not re-verify them.
- Guard grep (`DateTime.Now|UtcNow`, `.Result`, `.Wait()`, `new HttpClient(`, `FromSqlRaw`, `SqlQueryRaw`, `async void`) over the new and changed API code: no matches.
- `python -m pytest ai/`: not run; `ai/` is untouched.

## Notes for review
- Run 2 made one production edit. In `HeadlineCounterCard.tsx`, `streak: Number(streakDays)` became `streak: streakDays`. ESLint `no-unnecessary-type-conversion` failed `--max-warnings=0`, because the prop is already `number`; the page converts the Orval value once. The other `Number(...)` wraps are on Orval types (`number | string`) and stay.
- Some test files are over ~100 lines: `QuestionMasteryTests` 155, `MasteryOverviewEndpointTests` 161, `QuestionMasteryPersistenceTests` 113, `GetMasteryOverviewHandlerTests` 112, `SubjectMasteryEndpointTests` 109, `GetSubjectMasteryHandlerTests` 102. Every production file is under 100 lines, apart from the generated Designer.
- The AppShell `beforeEach` uses Orval's faker `getMasteryMock()`, so the overview content is random. The AppShell tests only assert the greeting heading, which does not depend on the payload.
- W7 asserts Arabic-Indic digits ("متبقّي لك ٤٠ سؤال من ٦٠"). It passes with the local Node ICU. I did not check that CI's Node build formats `ar` numbers the same way.
- Local dev: `appsettings.json` has no `Progress` section. The option defaults apply, so startup validation passes. Copy the section from the example file anyway.
