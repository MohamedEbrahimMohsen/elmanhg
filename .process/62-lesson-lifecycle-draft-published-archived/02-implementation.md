# Implementation — [E2.S3] Lesson lifecycle: draft, published, archived (#62)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| api/Elmanhg.Domain/Lessons/Lesson.Lifecycle.cs | 56 | partial `Lesson`: `Publish(Guid publishedBy)`, `Unpublish(Guid updatedBy)`, `Archive(Guid updatedBy)` (guard, mutate, stamp, raise) |
| api/Elmanhg.Domain/Lessons/LessonPublished.cs | 5 | `sealed record LessonPublished(Guid LessonId, Guid UnitId) : DomainEvent` |
| api/Elmanhg.Domain/Lessons/LessonUnpublished.cs | 5 | domain event |
| api/Elmanhg.Domain/Lessons/LessonArchived.cs | 5 | domain event |
| api/Elmanhg.Application/Lessons/PublishLesson/PublishLessonCommand.cs | 11 | audited command `Lesson.Publish` |
| api/Elmanhg.Application/Lessons/PublishLesson/PublishLessonValidator.cs | 13 | `LESSON_ID_REQUIRED` |
| api/Elmanhg.Application/Lessons/PublishLesson/PublishLessonHandler.cs | 28 | user guard → GetByIdAsync → `Publish` → save once |
| api/Elmanhg.Application/Lessons/UnpublishLesson/UnpublishLessonCommand.cs | 11 | audited command `Lesson.Unpublish` |
| api/Elmanhg.Application/Lessons/UnpublishLesson/UnpublishLessonValidator.cs | 13 | `LESSON_ID_REQUIRED` |
| api/Elmanhg.Application/Lessons/UnpublishLesson/UnpublishLessonHandler.cs | 28 | as Publish with `Unpublish` |
| api/Elmanhg.Application/Lessons/ArchiveLesson/ArchiveLessonCommand.cs | 11 | audited command `Lesson.Archive` |
| api/Elmanhg.Application/Lessons/ArchiveLesson/ArchiveLessonValidator.cs | 13 | `LESSON_ID_REQUIRED` |
| api/Elmanhg.Application/Lessons/ArchiveLesson/ArchiveLessonHandler.cs | 28 | as Publish with `Archive` |
| api/Elmanhg.Application/Lessons/ReorderLesson/ReorderLessonCommand.cs | 11 | audited command `Lesson.Reorder` |
| api/Elmanhg.Application/Lessons/ReorderLesson/ReorderLessonValidator.cs | 14 | `LESSON_ID_REQUIRED`, `LESSON_POSITION_INVALID` |
| api/Elmanhg.Application/Lessons/ReorderLesson/ReorderLessonHandler.cs | 34 | copy of `ReorderUnitHandler` keyed on the lesson's `UnitId` |
| api/Elmanhg.Application/Lessons/DeleteLesson/DeleteLessonCommand.cs | 11 | audited command `Lesson.Delete` |
| api/Elmanhg.Application/Lessons/DeleteLesson/DeleteLessonValidator.cs | 13 | `LESSON_ID_REQUIRED` |
| api/Elmanhg.Application/Lessons/DeleteLesson/DeleteLessonHandler.cs | 28 | GetWithObjectivesAsync (tracked) → `Delete` → save once |
| api/Elmanhg.Infrastructure/Migrations/20260928020258_AddLessonPublishedAt.cs | 29 | only `AddColumn<DateTimeOffset>("PublishedAt", "Lessons", timestamptz, nullable)` / `DropColumn` in Down |
| api/Elmanhg.Infrastructure/Migrations/20260928020258_AddLessonPublishedAt.Designer.cs | 971 | generated |
| api/Elmanhg.Tests/Domain/Lessons/LessonLifecycleTests.cs | 210 | A1–A16 |
| api/Elmanhg.Tests/Application/Features/Lessons/PublishLesson/PublishLessonHandlerTests.cs | 68 | A17–A20 |
| api/Elmanhg.Tests/Application/Features/Lessons/UnpublishLesson/UnpublishLessonHandlerTests.cs | 68 | A21–A24 |
| api/Elmanhg.Tests/Application/Features/Lessons/ArchiveLesson/ArchiveLessonHandlerTests.cs | 69 | A25–A28 |
| api/Elmanhg.Tests/Application/Features/Lessons/ReorderLesson/ReorderLessonHandlerTests.cs | 81 | A29–A32 |
| api/Elmanhg.Tests/Application/Features/Lessons/DeleteLesson/DeleteLessonHandlerTests.cs | 70 | A33–A36 |
| api/Elmanhg.Tests/Application/Features/Lessons/{Publish,Unpublish,Archive,Delete}Lesson/*ValidatorTests.cs | 26 each | A37 |
| api/Elmanhg.Tests/Application/Features/Lessons/ReorderLesson/ReorderLessonValidatorTests.cs | 34 | A38–A39 |
| api/Elmanhg.Tests/Integration/Infrastructure/LessonEventLog.cs | 16 | singleton event sink (D18) |
| api/Elmanhg.Tests/Integration/Infrastructure/LessonEventRecorder.cs | 25 | test-assembly `INotificationHandler` for the 3 events |
| api/Elmanhg.Tests/Integration/Content/LessonLifecycleEndpointTests.cs | 209 | I1–I12 |
| api/Elmanhg.Tests/Integration/Content/LessonManagementEndpointTests.cs | 159 | I13–I20 |
| web/src/features/content/api/lessonLifecycle.ts | 11 | `availableLessonActions(state)` |
| web/src/features/content/api/lessonLifecycle.test.ts | 20 | W1–W4 |
| web/src/features/content/api/contentQueries.ts | 14 | `isContentQuery(queryKey, excludedLessonId?)` |
| web/src/features/content/api/contentQueries.test.ts | 22 | W5–W7 |
| web/src/features/content/hooks/useLessonMutations.ts | 65 | publish/unpublish/archive/move/remove + toast + content-query invalidation |
| web/src/features/content/components/LessonActions.tsx | 74 | state-allowed actions with inline confirmation (`role="group"`) |
| web/src/features/content/components/LessonItem.tsx | 61 | tree lesson row: link, badge, move up/down, `LessonActions` |
| web/src/features/content/components/LessonItem.test.tsx | 199 | W8–W17 |
| web/src/features/content/components/LessonActions.test.tsx | 99 | W18–W21 |
| web/src/shared/api/generated/model/lessonPositionRequest.ts | 11 | Orval output (generated, not hand-edited) |

## Files modified
| Path | Change |
|---|---|
| api/Elmanhg.Domain/Lessons/Lesson.cs | `public partial class Lesson : AuditEntity, IAuditedEntity`; `DateTimeOffset? PublishedAt` after `VideoUrl`; `MoveTo(int order, Guid updatedBy)`, `Delete(Guid deletedBy)` |
| api/Elmanhg.Domain/Lessons/ILessonRepository.cs | `CountByUnitAsync(IReadOnlyCollection<Guid> unitIds, bool publishedOnly, CancellationToken)` |
| api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs | 5 domain codes under `// CONTENT` |
| api/Elmanhg.Application/Exceptions/ErrorCodes.cs | `LessonPositionInvalid` after `LessonIdRequired` |
| api/Elmanhg.Application/Subjects/GetSubject/GetSubjectHandler.cs | 4th ctor param `ICurrentUserService`; `publishedOnly = role != Admin` passed to the count |
| api/Elmanhg.Infrastructure/Lessons/LessonRepository.cs | predicate `unitIds.Contains(x.UnitId) && (!publishedOnly \|\| x.State == LessonState.Published)` |
| api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs | regenerated (+3 lines, `PublishedAt`) |
| api/Elmanhg.Api/Controllers/Lessons/LessonsController.cs | 5 actions after `UpdateLesson` (routes, names, policies per API surface) |
| api/Elmanhg.Api/Controllers/Lessons/Requests.cs | `LessonPositionRequest(int Position)` |
| api/Elmanhg.Api/Resources/Messages.en.resx, Messages.ar.resx | 6 keys, texts verbatim from the plan |
| api/openapi/v1.json | regenerated by the API build (+154 lines) |
| api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs | `LessonEvents` property + `services.AddSingleton(LessonEvents)` |
| api/Elmanhg.Tests/Integration/Content/ContentTestData.cs | `SeedLessonInStateAsync(...)` as specified |
| api/Elmanhg.Tests/Application/Features/Subjects/GetSubject/GetSubjectHandlerTests.cs | `_currentUserService` 4th arg; Admin role stub and `false` count stub in the existing test; A40 added |
| api/Elmanhg.Tests/Integration/Content/SubjectsEndpointTests.cs | I21–I22, `StudentClientAsync()` helper (+ `SeedLessonsInEveryStateAsync()` helper, see Deviations) |
| api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs | **not in plan**: seventh expected migration `_AddLessonPublishedAt` (see Deviations) |
| postman/elmanhg.postman_collection.json | "Lessons" folder: Publish/Unpublish/Archive (POST, no body), Reorder (PUT `{"position": 1}`), Delete (DELETE), all `{{lessonId}}`, inheriting the collection Bearer auth like the siblings; description appended verbatim |
| docs/PRD.md | §5.2 Transitions paragraph (verbatim) |
| docs/audit-log.md | 5 rows after `UploadLessonImage` |
| docs/claude-design-prompt.md | §4 admin bullet replaced/appended verbatim |
| web/src/features/content/components/UnitLessons.tsx | `data.map((lesson, index) => <LessonItem …/>)`; removed `Link`/`LessonStateBadge` imports |
| web/src/features/content/pages/LessonEditorPage.tsx | `useNavigate()`; `LessonActions` after the badge, `onDeleted` → `/admin/content` |
| web/src/features/content/i18n/en.json, ar.json | `lessons.{moved,published,unpublished,archived,deleted}` + `lessonActions.*` |
| web/src/shared/i18n/en.json, ar.json | `errors.*` for the 6 codes, same text as resx |
| web/src/shared/api/generated/** | `npm run gen:api` (lessons.ts, lessons.msw.ts, model/index.ts, zod/lessons.zod.ts, new model file); re-running it leaves no further diff |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| No existing test is edited except `GetSubjectHandlerTests`; `AppDbContextTests.cs` is not in *Existing code touched* | `AppDbContextTests.Migrate_FreshDatabase_LeavesNoPendingMigrations` asserts the exact ordered list of applied migrations (6 items). The plan's own new migration makes it fail ("Expected applied to contain exactly 6 items, but it contains 7") | Appended `seventh => seventh.Should().EndWith("_AddLessonPublishedAt")` — an intentional-behaviour update, nothing weakened (same edit each prior migration story made). Flagging it for the test-integrity guard. |
| Integration helpers: `SeedUnitAsync`, `AdminClientAsync`, `TeacherClientAsync(Guid)`, `StudentClientAsync()`, `ReadCodeAsync` | Several tests need "unit + lesson in state X" | Added a private `SeedLessonAsync(LessonState)` helper to both new endpoint classes and `SeedLessonsInEveryStateAsync()` to `SubjectsEndpointTests`. `LessonManagementEndpointTests` has no `StudentClientAsync` because none of I13–I20 uses a student (an unused private method would be dead code). |

## Build & test
- `dotnet build api/ --no-incremental` → `0 Error(s)`, `9 Warning(s)`, all 9 in `api/core-libraries` (pre-existing; zero new warnings).
- `dotnet test api/ -c Release` → `Test run summary: Passed! total: 535 failed: 0 succeeded: 535 skipped: 0`.
- Same command with `api/Elmanhg.Api/appsettings.json` moved to the scratchpad (restored afterwards, verified present) → `Passed! total: 535 failed: 0 succeeded: 535 skipped: 0`.
- `Elmanhg.Tests.exe -list tests` shows the 70 new test cases (A1–A40, I1–I22) in the discovered set.
- `dotnet format api/Elmanhg.slnx --verify-no-changes` → exit 2; all 108 reported lines are in `api/core-libraries` (known local whitespace noise); 0 lines outside it.
- Guard grep (`DateTime.Now/UtcNow`, `.Result`, `.Wait()`, `new HttpClient(`, `FromSqlRaw`, `async void`) over the diff and all new `.cs` files → no matches.
- `dotnet ef migrations add AddLessonPublishedAt -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api` → "Done."; the file contains only the nullable AddColumn / DropColumn.
- `npm --prefix web run build` → `✓ built in 1.92s`, exit 0 (existing >500 kB chunk warning only).
- `npm --prefix web test -- --run --coverage` → `Test Files 46 passed (46)`, `Tests 237 passed (237)`, exit 0; All files: Statements 94.27%, Branches 81.83%, Lines 94.21%.
- `npx tsc -b --noEmit` → exit 0. `npx eslint . --max-warnings=0` → exit 0.
- `npx prettier --check .` → flags CRLF endings on Windows (known local noise). `npx prettier --check --end-of-line auto src/features/content src/shared/i18n` → "All matched files use Prettier code style!".
- `npm run gen:api` → re-run leaves no further diff in `src/shared/api/generated`.

## Notes for review
- `CoreDbContext.SaveChangesAsync` (vendored, unchanged) publishes domain events **before** `base.SaveChangesAsync`, so a later DB failure would still have dispatched them. That doesn't matter today because nothing consumes them, but E3's servable handlers should know about it.
- `LessonsController.cs` is now 118 lines and `Lesson.cs` is 108 lines, both a bit over SKILL's ~100. The plan (D13, API) puts these members in those files.
- `LessonItem.tsx` line 41 (the move-up `onClick`) is uncovered: 80% lines, 100% branches. No plan row clicks "move up". W13 only asserts that it is disabled on the first row.
- W16 checks the Arabic group name `إجراءات Newton's laws` and the Arabic list label `دروس Mechanics`, both from the existing/new locale keys.
- `ReorderLessonHandler` relies on EF identity resolution: the tracked lesson from `GetByIdAsync` is the same instance that `FindAsync` returns, so `RemoveAll` then `Insert` does not duplicate it. The unit test stubs return the same instances.
- `DeleteLesson` loads with the global soft-delete filter, so only live objectives are cascaded.
- Line endings: all files were written with LF (the index is LF; autocrlf renders them as CRLF on checkout).
