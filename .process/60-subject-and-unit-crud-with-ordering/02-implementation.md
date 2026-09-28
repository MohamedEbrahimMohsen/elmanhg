# Implementation — [E2.S1] Subject and unit CRUD with ordering (#60)

## Files created
Hand-written: 89 files (plan #1–#89, one for one). Generated: the migration pair, plus Orval output under `web/src/shared/api/generated/**`.

| Path | Lines | Purpose |
|---|---|---|
| api/Elmanhg.Domain/Units/CurriculumUnit.cs | 61 | #1 entity (Create/Rename/MoveTo/Delete) |
| api/Elmanhg.Domain/Units/ICurriculumUnitRepository.cs | 9 | #2 AnyInSubjectAsync, CountBySubjectAsync |
| api/Elmanhg.Infrastructure/Units/CurriculumUnitRepository.cs | 24 | #3 both queries run in SQL (AnyAsync / GroupBy) |
| api/Elmanhg.Infrastructure/Migrations/20260928000438_AddSubjectOrderAndUnits.cs (+ .Designer.cs) | 66 / 848 | #4–5 AddColumn Order, the hand-added ROW_NUMBER backfill, CreateTable Units (FK Restrict), index (SubjectId, Order) |
| api/Elmanhg.Application/Shared/Options/ContentOptions.cs | 14 | #6 |
| api/Elmanhg.Application/Subjects/Shared/{SubjectResult,SubjectDetailResult,SubjectResultGenerator}.cs | 3/5/22 | #7–9 |
| api/Elmanhg.Application/Units/Shared/{UnitResult,UnitResultGenerator}.cs | 3/11 | #10–11 |
| api/Elmanhg.Application/Subjects/{CreateSubject(4),UpdateSubject(3),ReorderSubject(3),DeleteSubject(3),GetSubject(3),GetSubjects(2)}/* | 6–43 each | #12–29 |
| api/Elmanhg.Application/Units/{CreateUnit(4),UpdateUnit(3),ReorderUnit(3),DeleteUnit(3)}/* | 8–34 each | #30–42 |
| api/Elmanhg.Api/Controllers/Subjects/{Requests,SubjectsController}.cs | 5/73 | #43–44 |
| api/Elmanhg.Api/Controllers/Units/{Requests,UnitsController}.cs | 5/52 | #45–46 |
| api/Elmanhg.Tests/Domain/Units/CurriculumUnitTests.cs | 91 | #47 (plan tests 10–16) |
| api/Elmanhg.Tests/Application/Features/Subjects/*/…HandlerTests.cs (6) | 48–72 | #48–53 (tests 17–35) |
| api/Elmanhg.Tests/Application/Features/Subjects/*/…ValidatorTests.cs (5) | 26–44 | #54–58 (tests 50–54) |
| api/Elmanhg.Tests/Application/Features/Units/*/…HandlerTests.cs (4) | 63–66 | #59–62 (tests 36–49) |
| api/Elmanhg.Tests/Application/Features/Units/*/…ValidatorTests.cs (4) | 34–52 | #63–66 (tests 55–58) |
| api/Elmanhg.Tests/Integration/Content/ContentTestData.cs | 54 | #67 |
| api/Elmanhg.Tests/Integration/Content/SubjectsEndpointTests.cs | 307 | #68 (tests 59–78) |
| api/Elmanhg.Tests/Integration/Content/UnitsEndpointTests.cs | 230 | #69 (tests 79–92) |
| web/src/features/content/index.ts | 6 | #70 |
| web/src/features/content/i18n/{en,ar}.json | 45/45 | #71–72 |
| web/src/features/content/pages/ContentPage.tsx | 64 | #73 |
| web/src/features/content/components/SubjectCard.tsx | 60 | #74 |
| web/src/features/content/components/UnitPanel.tsx | 77 | #75 |
| web/src/features/content/components/ItemActions.tsx | 120 | #76 |
| web/src/features/content/components/NameForm.tsx | 48 | #77 |
| web/src/features/content/components/{ContentListSkeleton,ContentEmptyState,ContentErrorState}.tsx | 20/14/24 | #78–80 |
| web/src/features/content/hooks/{useContentFeedback,useSubjectMutations,useUnitMutations}.ts | 17/54/40 | #81–83 |
| web/src/features/content/api/position.ts (+ .test.ts) | 5/12 | #84–85 |
| web/src/features/content/schemas/nameSchema.ts (+ .test.ts) | 5/20 | #86–87 |
| web/src/features/content/pages/ContentPage.test.tsx | 209 | #88 (tests 96–107) |
| web/src/features/content/components/UnitPanel.test.tsx | 185 | #89 (tests 108–114) |

## Files modified
| Path | Change |
|---|---|
| api/Elmanhg.Domain/Subjects/Subject.cs | `IAuditedEntity`, `Order`, `Create(name, order, createdBy)`, `Rename`, `MoveTo`, `Delete(hasUnits, deletedBy)`, following Domain behaviour |
| api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs | New `// CONTENT` group: `SubjectHasUnits`, `ContentOrderInvalid`. The group did not exist before. |
| api/Elmanhg.Application/Exceptions/ErrorCodes.cs | 3 subject codes plus a new `// UNITS` group with 5 codes |
| api/Elmanhg.Application/DependencyInjection.cs | `AddOptions<ContentOptions>()…ValidateDataAnnotations().ValidateOnStart()` |
| api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs | `DbSet<CurriculumUnit> Units`, `ConfigureUnits`, global soft-delete filter |
| api/Elmanhg.Infrastructure/DependencyInjection.cs | registers `ICurriculumUnitRepository` |
| api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs | regenerated |
| api/Elmanhg.Api/Resources/Messages.{en,ar}.resx | the 10 new keys |
| api/Elmanhg.Api/appsettings.example.json | `"Content": { "SubjectNameMaxLength": 100, "UnitNameMaxLength": 100 }`. Also added to the local gitignored appsettings.json. |
| api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs | the two `Content:*` keys |
| api/Elmanhg.Tests/Domain/Subjects/SubjectTests.cs | the existing test now uses the new signature and asserts Order; plan tests 2–9 added |
| api/Elmanhg.Tests/Domain/Teachers/TeacherSubjectTests.cs, …/AssignTeacherSubjectHandlerTests.cs, …/UnassignTeacherSubjectHandlerTests.cs, …/Integration/Authorization/ScopeTestData.cs | `Subject.Create(…, 1, …)` signature change only |
| api/Elmanhg.Tests/Integration/Persistence/AuditChangeCaptureTests.cs | `NonAuditedEntity` now uses an `Otp`; `using Elmanhg.Domain.Subjects` removed because it became unused |
| api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs | **Not in plan.** See Deviations. |
| api/openapi/v1.json | regenerated by the build |
| postman/elmanhg.postman_collection.json | "Subjects" folder (6 requests) and "Units" folder (4 requests), both using collection-level bearer auth. New `unitId` variable. Create requests store `subjectId` / `unitId`. |
| docs/audit-log.md | 8 command rows; audited entities are now `TeacherSubject`, `Subject`, `CurriculumUnit`; the "E2/E3 join later" line updated so it no longer contradicts the table |
| web/src/routes/admin/content.tsx | `component: ContentPage` |
| web/src/app/i18n.ts | `content` namespace |
| web/src/shared/i18n/{en,ar}.json | 9 `errors.*` codes |
| web/src/shared/lib/http.ts | **Not in plan.** See Deviations. |
| web/src/shared/api/generated/** | regenerated by `npm run gen:api` (subjects/, units/, model, zod) |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Only the 6 existing tests listed may be modified | `AppDbContextTests.Migrate_FreshDatabase_LeavesNoPendingMigrations` lists every applied migration by name. It fails (4 expected, 5 found) as soon as the migration the plan requires exists. | Added one entry to its `SatisfyRespectively` list (`fifth => …_AddSubjectOrderAndUnits`). Nothing was removed or weakened. The behaviour changed on purpose (SKILL §8.11). The reviewer may still treat this as a test-integrity edit because the plan does not list it. |
| Modify only the listed files (the web mutator is not listed) | Every `Ok()` action with no result (update/reorder/delete) returns 200 with an empty body. `shared/lib/http.ts` called `response.json()` on every non-204 success, so each of these mutations rejected with a JSON parse error. The generated MSW handlers reproduce this (`new HttpResponse(null, {status: 200})`). | `parse()` now reads `response.text()` and returns `undefined` when the body is empty, otherwise `JSON.parse`. This covers 204 as well. The existing `http.test.ts` still passes. No new http test, because that would edit an unlisted test file. |
| `NameForm` props have no cancel label | The cancel button still needs a translated label | `NameForm` reads `t('actions.cancel')` from the `content` namespace itself. The props are exactly as the plan lists them. |
| `SubjectCard` prop `position: number` (meaning not stated) | — | Treated as the 1-based position. The card calls `targetPosition(position - 1, direction)`. |

## Build & test
- `dotnet build api/`: Build succeeded, 0 errors. The only warnings are the existing CS8618/CS8602 in vendored `core-libraries` (Core.Notifications, Core.OTP, Core.Validation). No warning comes from Elmanhg projects. The final incremental build showed 0 warnings.
- `dotnet test api/ -c Release` (Docker 29.6.2, Testcontainers): `Test run summary: Passed! total: 372, failed: 0, succeeded: 372, skipped: 0`.
- `dotnet test api/ -c Release` with `api/Elmanhg.Api/appsettings.json` moved out of the repo: `Passed! total: 372, failed: 0, succeeded: 372, skipped: 0`. The file was restored afterwards and still contains the new `Content` section.
- `dotnet format api/Elmanhg.slnx --verify-no-changes`: no findings outside `core-libraries`.
- `dotnet ef migrations add AddSubjectOrderAndUnits -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`: done. Up() has no Drop or Rename. Down() has the generated DropTable/DropColumn.
- `npm run gen:api`: regenerated. Running it again gives no diff.
- `npm --prefix web run build` (tsc -b + vite build): exit 0.
- `npm --prefix web test -- --run`: `Test Files 33 passed (33) · Tests 167 passed (167)`.
- `npx tsc -b --noEmit`: exit 0. `npm run lint` (eslint --max-warnings=0): exit 0.
- `npx prettier --check .` reports the known CRLF-only noise on Windows (147 files, repo-wide). `npx prettier --check --end-of-line auto .` gives "All matched files use Prettier code style!"
- `npx vitest run --coverage`: 1 of 5 runs failed. `UnitPanel › shows the units of an expanded subject` hit the 1 s `findByRole('button', {name: 'Show units'})` timeout while the lazy route loaded cold under coverage instrumentation. The other 4 runs passed, and the passing runs meet the thresholds (`src/features/**`: all files 93.68% lines, 81.05% branches; `features/content/hooks` 100% lines, 75% branches).

## Notes for review
- `ItemActions.tsx` is exactly 120 lines (the component limit). `ContentPage.test.tsx` is 209 lines, over the 200-line file guideline; the sibling `AuditLogPage.test.tsx` is 191.
- `PutPosition_MoveBeforeSibling_ReordersSubjects`: A is seeded with Order 1 and B with Order 2. B is then PUT to A's current Order (1). The shared DB holds many subjects with Order 1 (from `ScopeTestData`), so the "Order equals list index" reading is unreliable. Moving B to position 1 is deterministic, and it gives B < A no matter what else is in the table.
- The Admin/Student subject list is all live subjects (`GetAllAsync`). `SubjectScopeBehaviour` still lets Students and Admins through `GetSubject`.
- `SubjectCard` and `ContentPage` each call `useSubjectMutations()`, so every card owns its own four mutation instances. This keeps each card's pending state independent.
- `settle()` in both mutation hooks awaits invalidation inside `onSuccess`, so `mutateAsync` resolves only after the refetch. The create form resets only after the list is fresh.
- New LF-ending files sit next to CRLF files in the working tree. `core.autocrlf=true` normalises them, so git shows content-only diffs.
- Nothing was committed, pushed or switched.
