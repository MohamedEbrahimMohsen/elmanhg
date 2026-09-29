# Implementation — [E7.S1] Subject, unit and lesson browsing (#85)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| api/Elmanhg.Domain/Lessons/LessonOpening.cs | 29 | Entity, `Record` guard (Published only) |
| api/Elmanhg.Domain/Lessons/ILessonOpeningRepository.cs | 8 | `IsOpenedAsync` |
| api/Elmanhg.Domain/Lessons/LessonSequence.cs | 39 | `Order` / `Neighbours` + `LessonNeighbours` record (D6) |
| api/Elmanhg.Infrastructure/Lessons/LessonOpeningRepository.cs | 14 | Repository |
| api/Elmanhg.Infrastructure/Migrations/20260929050559_AddLessonOpenings.cs (+ .Designer.cs) | 61 / 2042 | Creates `LessonOpenings`, FKs Restrict, unique `IX_LessonOpenings_StudentId_LessonId`; no Drop/Rename in Up |
| api/Elmanhg.Application/Browse/Shared/{StudentSubjectResult,StudentUnitResult,StudentLessonResult,UnitBestScore,StudentSubjectResultGenerator,StudentUnitResultGenerator,StudentLessonResultGenerator}.cs | 5–26 each | Results and generators |
| api/Elmanhg.Application/Browse/GetStudentSubject/{Query,Validator,Handler}.cs | 6 / 13 / 37 | Subject read |
| api/Elmanhg.Application/Browse/GetStudentUnit/{Query,Validator,Handler}.cs | 6 / 13 / 41 | Unit read |
| api/Elmanhg.Application/Browse/GetStudentLesson/{Query,Validator,Handler}.cs | 6 / 13 / 47 | Lesson read |
| api/Elmanhg.Application/Browse/RecordLessonOpening/{Command,Validator,Handler}.cs | 5 / 13 / 35 | Idempotent opening record |
| api/Elmanhg.Application/Exams/Shared/ExamLessonGate.cs | 24 | `CountUnopenedAsync` / `EnsureOpenedAsync` |
| api/Elmanhg.Api/Controllers/Browse/BrowseController.cs | 53 | 4 actions, `ProgressViewOwn` |
| api/Elmanhg.Tests/Domain/Lessons/LessonOpeningTests.cs, LessonSequenceTests.cs | 51 / 100 | Domain tests (#1–10) |
| api/Elmanhg.Tests/Application/Features/Browse/**/{4 handler + 4 validator tests} | 26–166 | #11–37 |
| api/Elmanhg.Tests/Application/Features/Exams/Shared/ExamLessonGateTests.cs | 87 | #38–41 |
| api/Elmanhg.Tests/Integration/Browse/BrowseTestData.cs + 4 endpoint test classes | 34–76 | #51–63 |
| api/Elmanhg.Tests/Integration/Exams/ExamLessonGateEndpointTests.cs | 108 | #64–68 |
| api/Elmanhg.Tests/Integration/Persistence/LessonOpeningPersistenceTests.cs | 48 | #69 |
| web/src/features/browse/{index.ts, locales.ts, i18n/en.json, i18n/ar.json} | 7 / 4 / 52 / 52 | Barrel, locales |
| web/src/features/browse/api/{lessonTabs.ts, richText.ts, richText.test.ts} | 6 / 9 / 19 | Tabs, `hasRichText` (#71–73) |
| web/src/features/browse/hooks/useLessonOpening.ts | 26 | Write-on-view, ref guard, overview invalidation |
| web/src/features/browse/components/{StudentBreadcrumbs, MasterySummary, UnitListItem, LessonListItem, UnitExamCard, LessonTabs, LessonNavigation, LessonRichText}.tsx | 15–45 | Components |
| web/src/features/browse/pages/{SubjectPage, UnitPage, LessonPage, LessonExplanationTab, LessonObjectivesTab, LessonSummaryTab}.tsx | 17–59 | Pages |
| web/src/features/browse/pages/{SubjectPage,UnitPage,LessonPage}.test.tsx | 115 / 107 / 199 | #74–105 |
| web/src/routes/student/{subject.$subjectId, unit.$unitId, lesson.$lessonId, lesson.$lessonId.index, lesson.$lessonId.objectives, lesson.$lessonId.summary}.tsx | 11 each | Routes |
| web/src/test/browseFixtures.ts | 93 | Fixtures |
| docs/browsing.md | 61 | New doc |

## Files modified
| Path | Change |
|---|---|
| api/Elmanhg.Application/Exceptions/ErrorCodes.cs | `LessonAlreadyOpened`, `ExamLessonsNotOpened` |
| api/Elmanhg.Application/Shared/Options/ExamsOptions.cs | `RequireAllLessonsOpened` (default false) |
| api/Elmanhg.Application/Exams/Shared/UnitExamOverviewResult{,Generator}.cs | trailing `UnopenedLessonCount` |
| api/Elmanhg.Application/Exams/GetUnitExamOverview/GetUnitExamOverviewHandler.cs | new deps, gate count (0 when off / admin) |
| api/Elmanhg.Application/Exams/StartUnitExam/StartUnitExamHandler.cs | `ILessonOpeningRepository`, gate first in `StartAsync`, `isTestMode` reused |
| api/Elmanhg.Application/Exams/StartMultiUnitExam/StartMultiUnitExamHandler.cs | `ILessonOpeningRepository`, `isTestMode` first + gate inside `if (session is null)` |
| api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs | index const, DbSet, 409 mapping, `ConfigureLessonOpenings`, global filter |
| api/Elmanhg.Infrastructure/DependencyInjection.cs | repository registration |
| api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs | regenerated |
| api/Elmanhg.Api/Resources/Messages.{en,ar}.resx | 2 keys |
| api/Elmanhg.Api/appsettings.example.json (+ local gitignored appsettings.json) | `Exams.RequireAllLessonsOpened: false` (the local file had no `Exams` section; the example's full line was added) |
| api/openapi/v1.json | regenerated |
| api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs | `UseSetting("Exams:RequireAllLessonsOpened", "false")` |
| api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs | `_AddLessonOpenings` appended |
| api/Elmanhg.Tests/Application/Features/Exams/{GetUnitExamOverview,StartUnitExam,StartMultiUnitExam}/*HandlerTests.cs | constructor deps + `_examsOptions` field + tests #42–50 |
| postman/elmanhg.postman_collection.json | `Browse` folder after `Mastery`, 4 requests in order, Bearer inherited from the collection like `Mastery` |
| web/src/app/i18n.ts | `browse` namespace |
| web/src/features/content/index.ts | exports `toSafeVideoUrl` |
| web/src/features/mastery/components/SubjectMasteryCard.tsx, api/invalidateMastery.ts | subject link; `browseQueryPrefix` |
| web/src/features/progress/components/SubjectProgressCard.tsx, UnitProgressTable.tsx | subject and unit links |
| web/src/features/exam/pages/ExamStartPage.tsx, components/ExamStartActions.tsx, i18n/{en,ar}.json | breadcrumbs; unopened-lessons warning; 3 strings |
| web/src/features/quiz/pages/PracticePage.tsx, QuizResultPage.tsx, i18n/{en,ar}.json | h1 removed; back-to-lesson link; `practice.title` removed, `result.backToLesson` added |
| web/src/shared/i18n/{en,ar}.json | 2 error strings |
| web/src/styles/app.css | `.rich-text { overflow-wrap: anywhere }` |
| web/src/test/msw/server.ts, test/examFixtures.ts | default browse handlers; `unopenedLessonCount: 0` |
| web/src/routeTree.gen.ts, web/src/shared/api/generated/** | regenerated |
| Test additions: StudentHomePage, ProgressPage, UnitProgressTable, ExamStartPage (2), QuizResultPage, invalidateMastery tests | #106–112 (additions only) |
| docs/exams.md, mastery.md, progress.md, claude-design-prompt.md §4, prototype.md | as specified; no "until #85" left in /docs |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| `LessonSequence.Neighbours`: `.FirstOrDefault(x => x.Id == lessonId, (Guid.Empty, -1)).position` | Does not compile (CS1061): the unnamed default tuple makes the inferred element type `(Guid, int)` with no `Id`/`position` names | Named the default: `(Id: Guid.Empty, position: -1)`. Behaviour identical. |
| `LessonNavigation`: "Directional icons `ChevronRight`/`ChevronLeft` … `rtl:rotate-180`" (order not pinned) | Read literally as previous = ChevronRight, the arrows point the wrong way in both directions once `rtl:rotate-180` applies | Previous = `ChevronLeft` (before the text), next / back-to-unit = `ChevronRight` (after), with `rtl:rotate-180`, the same as `shared/components/Pagination.tsx` |
| Test #77 / #81: find the link "Multi-unit exam" / «امتحان متعدد الوحدات» | The student shell nav already has a link with that exact name (no search), so `getByRole` found the wrong one | Those assertions are scoped `within(screen.getByRole('main'))` |
| Test #94 asserts the Objectives tab is current | A mutation check (`exact: false`) showed the planned assertion alone did not catch a wrong active-tab rule | Added one assertion to that planned test: the Explanation tab has no `aria-current` |

## Build & test
- `dotnet build api/`: Build succeeded, 0 errors. The only warnings are in vendored `core-libraries` (existing).
- `dotnet test api/ -c Release` with `api/Elmanhg.Api/appsettings.json` moved aside (then restored): `total: 2489, failed: 0, succeeded: 2489, skipped: 0`.
- `dotnet format api/Elmanhg.slnx --verify-no-changes --exclude api/core-libraries`: one WHITESPACE finding in `Elmanhg.Tests/Builders/SubscriptionBuilder.cs`, an existing file this story does not touch (CRLF noise, per PROGRESS Gotchas). No finding in any file I changed.
- `npm --prefix web run gen:api`: re-run after the final build; the generated folder is identical.
- `npm run build` (`tsc -b && vite build`): exit 0. `routeTree.gen.ts` regenerated.
- `npm run typecheck`: clean. `npm run lint`: clean (`before:content-['/']` accepted).
- `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto`: "All matched files use Prettier code style!"
- `npx vitest run`: `Test Files 123 passed (123)`, `Tests 778 passed (778)` (736 existing + 42 new). `PracticePage.test.tsx` is unchanged and green.
- Mutation checks (production line broken, test confirmed red, code restored):
  - api: gate count, draft filters in unit and lesson reads, idempotency check, 409 constraint mapping, `lessonCount`, `Record` guard, unit order, next neighbour, admin exemption (start and overview), gate on resume, next-link mapping. 30 test failures across 2 batches, covering every new test class.
  - web: every link and branch added in mastery, progress, exam and quiz; `invalidateMastery` prefix; the `unitId` guard in `useLessonOpening`; `hasRichText`; back-to-unit; `aria-current`; objectives text; the unit-exam card id; exact tab matching.

## Notes for review
- The `ExamLessonGateEndpointTests` gated factory (`WithWebHostBuilder` + `PostConfigure<ExamsOptions>`) shares the ApiFactory database. The student JWT from the base factory is copied onto `gated.CreateClient()`. Each test disposes the gated factory (`await using`).
- `LessonExplanationTab` / `LessonObjectivesTab` / `LessonSummaryTab` read the lesson through `useGetStudentLesson` (the same cache entry as the layout), so they make no extra request.
- `GetStudentLessonHandlerTests` (166 lines) and `GetStudentUnitHandlerTests` (120 lines) are over ~100 lines. They are tests, and several existing handler tests are longer. Every production file is ≤ 59 lines.
- The local `appsettings.json` had no `Exams` section at all (it relied on code defaults). I added the example's full `Exams` line so the new key is visible locally. The file is gitignored.
- Per D12, the opening mutation has no success or error toast, although react-feature §4 asks for toasts on mutations. The plan chose this deliberately (write-on-view with no UI).
