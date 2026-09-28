# Implementation — [E5.S5] Progress page (#78)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| api/Elmanhg.Domain/Mastery/ObjectiveMasteryCount.cs | 3 | Per-objective mastery count record |
| api/Elmanhg.Domain/Mastery/WeakSpots.cs | 32 | `PickLessons` / `PickObjectives` (Decisions 7–8) |
| api/Elmanhg.Domain/Sessions/UnitExamScope.cs | 13 | `unit:<guid>` key, JSON round trip (for E6) |
| api/Elmanhg.Domain/Sessions/UnitExamBestScore.cs | 3 | Best score per ScopeKey |
| api/Elmanhg.Domain/Sessions/SessionHistoryKind.cs | 3 | `Quiz`, `Exam` filter enum |
| api/Elmanhg.Application/Progress/GetSubjectProgress/GetSubjectProgressQuery.cs | 6 | Query |
| api/Elmanhg.Application/Progress/GetSubjectProgress/GetSubjectProgressHandler.cs | 29 | Subjects + units + counts + best exams |
| api/Elmanhg.Application/Progress/GetWeakSpots/GetWeakSpotsQuery.cs | 6 | Query |
| api/Elmanhg.Application/Progress/GetWeakSpots/GetWeakSpotsHandler.cs | 47 | Weak lessons/objectives, short-circuit when none |
| api/Elmanhg.Application/Progress/GetSessionHistory/GetSessionHistoryQuery.cs | 8 | Paged query |
| api/Elmanhg.Application/Progress/GetSessionHistory/GetSessionHistoryValidator.cs | 21 | Page number / size / kind rules |
| api/Elmanhg.Application/Progress/GetSessionHistory/GetSessionHistoryFilter.cs | 18 | Own, non-test, kind filter expression |
| api/Elmanhg.Application/Progress/GetSessionHistory/GetSessionHistoryHandler.cs | 48 | `FindPaginatedAsync` + one lesson and one unit lookup |
| api/Elmanhg.Application/Progress/Shared/SubjectProgressResult.cs | 3 | Result |
| api/Elmanhg.Application/Progress/Shared/UnitProgressResult.cs | 3 | Result |
| api/Elmanhg.Application/Progress/Shared/SubjectProgressResultGenerator.cs | 34 | Mapping |
| api/Elmanhg.Application/Progress/Shared/WeakSpotsResult.cs | 3 | Result |
| api/Elmanhg.Application/Progress/Shared/WeakLessonResult.cs | 3 | Result |
| api/Elmanhg.Application/Progress/Shared/WeakObjectiveResult.cs | 3 | Result |
| api/Elmanhg.Application/Progress/Shared/WeakSpotsResultGenerator.cs | 48 | Mapping, skips vanished lesson/subject/objective |
| api/Elmanhg.Application/Progress/Shared/SessionHistoryItemResult.cs | 3 | Result |
| api/Elmanhg.Application/Progress/Shared/SessionHistoryResultGenerator.cs | 23 | Switch on Kind, scope names |
| api/Elmanhg.Api/Controllers/Progress/ProgressController.cs | 45 | 3 GET actions, `Progress.ViewOwn` |
| api/Elmanhg.Tests/Domain/Mastery/WeakSpotsTests.cs | 89 | D1–D6 |
| api/Elmanhg.Tests/Domain/Sessions/SessionScopeTests.cs | 45 | D7–D10 |
| api/Elmanhg.Tests/Application/Features/Progress/GetSubjectProgress/GetSubjectProgressHandlerTests.cs | 95 | A1–A4 |
| api/Elmanhg.Tests/Application/Features/Progress/GetWeakSpots/GetWeakSpotsHandlerTests.cs | 128 | A5–A10 |
| api/Elmanhg.Tests/Application/Features/Progress/GetSessionHistory/GetSessionHistoryHandlerTests.cs | 94 | A11, A12, A12b, A12c |
| api/Elmanhg.Tests/Application/Features/Progress/GetSessionHistory/GetSessionHistoryValidatorTests.cs | 53 | A15–A19 |
| api/Elmanhg.Tests/Application/Features/Progress/GetSessionHistory/GetSessionHistoryFilterTests.cs | 51 | A20–A24 |
| api/Elmanhg.Tests/Integration/Progress/ProgressTestData.cs | 73 | Helpers incl. parameterised `ExecuteSqlAsync` UnitExam insert |
| api/Elmanhg.Tests/Integration/Progress/SubjectProgressEndpointTests.cs | 101 | I1–I5 |
| api/Elmanhg.Tests/Integration/Progress/WeakSpotsEndpointTests.cs | 106 | I6–I10 |
| api/Elmanhg.Tests/Integration/Progress/SessionHistoryEndpointTests.cs | 146 | I11–I18 |
| web/src/features/progress/index.ts | 4 | Barrel |
| web/src/features/progress/locales.ts | 4 | Locales |
| web/src/features/progress/i18n/en.json, ar.json | 64 each | Same key set |
| web/src/features/progress/schemas/progressSearchSchema.ts | 8 | `kind`, `page` URL search |
| web/src/features/progress/schemas/progressSearchSchema.test.ts | 21 | W1–W4 |
| web/src/features/progress/api/sessionHistory.ts | 37 | Params, row links, kind labels |
| web/src/features/progress/api/sessionHistory.test.ts | 40 | W5–W10 |
| web/src/features/progress/hooks/useProgressSearch.ts | 21 | URL search navigation |
| web/src/features/progress/hooks/useSessionHistory.ts | 10 | `keepPreviousData`, `staleTime: 0` |
| web/src/features/progress/pages/ProgressPage.tsx | 19 | Page |
| web/src/features/progress/pages/ProgressPage.test.tsx | 162 | W13–W22 |
| web/src/features/progress/pages/ProgressPage.history.test.tsx | 167 | W23–W30 |
| web/src/features/progress/components/ProgressSummary.tsx | 25 | Reused overview headline + streak |
| web/src/features/progress/components/SubjectProgressSection.tsx | 49 | |
| web/src/features/progress/components/SubjectProgressCard.tsx | 29 | |
| web/src/features/progress/components/UnitProgressTable.tsx | 50 | |
| web/src/features/progress/components/WeakSpotsSection.tsx | 59 | |
| web/src/features/progress/components/WeakLessonList.tsx | 39 | |
| web/src/features/progress/components/WeakObjectiveList.tsx | 41 | |
| web/src/features/progress/components/SessionHistorySection.tsx | 56 | |
| web/src/features/progress/components/SessionKindFilter.tsx | 31 | |
| web/src/features/progress/components/SessionHistoryTable.tsx | 39 | |
| web/src/features/progress/components/SessionHistoryRow.tsx | 50 | |
| web/src/features/progress/components/SessionHistoryEmptyState.tsx | 26 | |
| web/src/test/progressFixtures.ts | 144 | Fixtures |
| docs/progress.md | 79 | New doc |

Generated (not hand-written): `web/src/shared/api/generated/progress/*`, 9 new `model/*` files, `zod/progress/*`, index updates.

## Files modified
| Path | Change |
|---|---|
| api/Elmanhg.Domain/Mastery/IQuestionMasteryRepository.cs | `GetObjectiveCountsAsync` |
| api/Elmanhg.Infrastructure/Mastery/QuestionMasteryRepository.cs | Implemented, mirrors `GetLessonCountsAsync` with an objective join |
| api/Elmanhg.Domain/Sessions/ISessionRepository.cs | `GetBestUnitExamScoresAsync` |
| api/Elmanhg.Infrastructure/Sessions/SessionRepository.cs | Implemented as the plan's LINQ |
| api/Elmanhg.Domain/Sessions/QuizScope.cs | `FromJson` |
| api/Elmanhg.Application/Shared/Options/ProgressOptions.cs | 3 keys with `[Range]` |
| api/Elmanhg.Application/Exceptions/ErrorCodes.cs | 3 codes under `// SESSIONS` |
| api/Elmanhg.Api/Resources/Messages.ar.resx, Messages.en.resx | 3 keys each |
| api/Elmanhg.Api/appsettings.example.json | Progress line extended |
| api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs | 3 Progress keys next to `StreakMaxDays` |
| api/Elmanhg.Tests/Application/Features/Mastery/ProgressOptionsTests.cs | Added A13, A14 (existing untouched) |
| api/openapi/v1.json | Regenerated by `dotnet build` |
| postman/elmanhg.postman_collection.json | New `Progress` folder after `Mastery`, 3 GET requests with tests |
| web/orval.config.ts | `GetSessionHistory` zod query override |
| web/src/routes/student/progress.tsx | `validateSearch` + `ProgressPage` |
| web/src/app/i18n.ts | `progress` namespace |
| web/src/features/mastery/index.ts | Exports `HeadlineCounterCard`, `MasteryBar` |
| web/src/features/mastery/api/invalidateMastery.ts | `progressQueryPrefix`, predicate includes `/api/progress` |
| web/src/features/mastery/api/invalidateMastery.test.ts | Added W12 (existing `it` untouched) |
| web/src/shared/i18n/en.json, ar.json | 3 error codes |
| docs/mastery.md | Intro link to progress.md, Access line |
| docs/sessions.md | ScopeKey row (`UnitExamScope`), History line under API |
| docs/claude-design-prompt.md §4 | `#/student/progress` bullet |

No migration (the plan has none), so there is no `AppDbContextTests` entry.

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Decision 14 / I14 `Get_KindOutOfRange_Returns422WithCode`: `?kind=9` binds and the validator returns 422 `SESSION_HISTORY_KIND_INVALID`. | ASP.NET Core's enum model binder rejects undefined numeric values. `?kind=9` returns a framework 400 (`errors.kind`) before MediatR runs. Observed in the test run. | Renamed I14 to `Get_KindOutOfRange_Returns400ModelBindingError`, which asserts 400 and an `errors.kind` entry. I kept the validator rule, error code, resx and A19 as the in-process guard. `docs/progress.md` states the real HTTP behaviour. |
| I10: one objective question, answered wrong. | With only that data, a mutation that zeroes the objective `Mastered` sum in `GetObjectiveCountsAsync` survived. | Kept the plan's assertions. Added a second lesson whose objective question is mastered (answered correctly twice). `ContainSingle` now also proves mastered objectives are excluded. |
| `progressFixtures` exports the listed ids and 3 builders. | The tests also need subject ids, the objective id and single items. | Added extra exports in the same file: `physicsSubjectId`, `chemistrySubjectId`, `wavesUnitId`, `weakObjectiveId`, `openQuizItem`, `finishedQuizItem`, `examItem`. `weakObjectiveLessonId` equals `weakLessonId`, as in the plan's fixture description. No new file. |

## Build & test
- `dotnet build api/Elmanhg.slnx`: Build succeeded, no new warnings. `api/openapi/v1.json` regenerated.
- `dotnet test api/ -c Release` with `api/Elmanhg.Api/appsettings.json` moved aside, then restored: **total 1538, failed 0, succeeded 1538, skipped 0**. New API tests: 54 (10 domain, 26 application including A13–A14, 18 integration). Docker 29.6.2 was running.
- `npm --prefix web run gen:api`: regenerated, and a second run produced no further drift.
- `npm --prefix web run typecheck`: exit 0.
- `npm --prefix web run lint`: exit 0 (`--max-warnings=0`).
- `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` (in web/): "All matched files use Prettier code style!"
- `npm --prefix web test -- --run`: **Test Files 84 passed, Tests 528 passed**. New web tests: 29 (W1–W10, W12, W13–W30).
- Guard grep over the diff (DateTime.Now, .Result, .Wait(), new HttpClient, FromSqlRaw, async void, comments): only hit is the plan's one-line invariant comment in `WeakSpots.cs`.
- Mutation checks (break the code, run the targeted tests, restore):
  - API: 12 mutations. 11 were killed; the 12th, removing the lesson null-check, fails the build through nullable analysis. The first round had 2 survivors: D5's tie-break depended on random GUID order, and the objective `Mastered` sum was untested. I fixed both tests (D5 now uses fixed GUIDs, I10 as above), and both mutations are now killed.
  - Web: 13 of 13 killed (invalidation prefix, link mapping, kind param, schema min, empty-state variant, pagination threshold, score/in-progress switch, no-exam dash, weak-spots empty state, objective practice link, setKind, subjects empty state).

## Notes for review
- **PRD §7.6** says "Per subject: …, streak". Following Decision 2, the page shows the single student-wide streak from #77, in the summary. I did not edit `docs/PRD.md` because it is not in the plan's touch list, but `docs/progress.md` states the rule. The reviewer should decide whether the PRD wording needs a clarifying edit.
- **ApiFactory:** the new keys went into the existing `ConfigureAppConfiguration` dictionary, as the plan says, not `UseSetting`. `ProgressOptions` is read lazily through `IOptions`, like the existing `Progress:StreakMaxDays`, so this works. The CI-parity run confirms it.
- **Shared test database:** the integration tests share one database without reset. The subject tests therefore locate their own subject by id, and the weak-spot and history tests rely on a fresh student per test.
- **Arabic test (W21):** it scopes the "٥٠٪" check to the Physics card, because the history table also has a "Mechanics" exam row.
- **`GetWeakSpotsHandler`** uses `Microsoft.EntityFrameworkCore` for `Include` in Application. The same pattern exists in `BulkApproveQuestionsHandler` and `GetValidationQuestionHandler`.
