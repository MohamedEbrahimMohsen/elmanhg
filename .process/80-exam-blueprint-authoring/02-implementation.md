# Implementation — [E6.S1] Exam blueprint authoring (#80)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| api/Elmanhg.Domain/ExamBlueprints/ExamTypeCount.cs | 5 | D1 record |
| api/Elmanhg.Domain/ExamBlueprints/ExamDifficultyMix.cs | 3 | D2 record |
| api/Elmanhg.Domain/ExamBlueprints/ExamBlueprintShape.cs | 3 | D3 record |
| api/Elmanhg.Domain/ExamBlueprints/ExamTypeShortfall.cs | 8 | D4 record with `Missing` |
| api/Elmanhg.Domain/ExamBlueprints/ExamBlueprintShortfall.cs | 15 | D5 pure `Find` |
| api/Elmanhg.Domain/ExamBlueprints/ExamBlueprintJson.cs | 22 | D6 canonical jsonb (de)serialisation |
| api/Elmanhg.Domain/ExamBlueprints/ExamBlueprint.cs | 83 | D7 entity: CreateForSubject/CreateForUnit/Update/Delete |
| api/Elmanhg.Domain/ExamBlueprints/IExamBlueprintRepository.cs | 5 | D8 |
| api/Elmanhg.Domain/Questions/ServableQuestionCount.cs | 3 | D9 |
| api/Elmanhg.Application/Shared/Options/ExamBlueprintsOptions.cs | 14 | A1 options (100 / 300) |
| api/Elmanhg.Application/ExamBlueprints/Shared/ExamBlueprintInput.cs | 9 | A2 input records |
| api/Elmanhg.Application/ExamBlueprints/Shared/ExamBlueprintInputValidator.cs | 50 | A3 |
| api/Elmanhg.Application/ExamBlueprints/Shared/ExamBlueprintShapeGenerator.cs | 15 | A4 |
| api/Elmanhg.Application/ExamBlueprints/Shared/ExamBlueprintResult.cs | 13 | A5 (IAuditableResult) |
| api/Elmanhg.Application/ExamBlueprints/Shared/SubjectExamBlueprintsResult.cs | 5 | A6 |
| api/Elmanhg.Application/ExamBlueprints/Shared/ServableTypeCounts.cs | 28 | A7 |
| api/Elmanhg.Application/ExamBlueprints/Shared/ExamBlueprintResultGenerator.cs | 33 | A8 |
| api/Elmanhg.Application/ExamBlueprints/GetSubjectExamBlueprints/{Query,Validator,Handler}.cs | 6/13/27 | A9–A11 |
| api/Elmanhg.Application/ExamBlueprints/SaveSubjectExamBlueprint/{Command,Validator,Handler}.cs | 12/20/44 | A12–A14 |
| api/Elmanhg.Application/ExamBlueprints/SaveUnitExamBlueprint/{Command,Validator,Handler}.cs | 12/20/44 | A15–A17 |
| api/Elmanhg.Application/ExamBlueprints/DeleteExamBlueprint/{Command,Validator,Handler}.cs | 11/13/28 | A18–A20 |
| api/Elmanhg.Infrastructure/ExamBlueprints/ExamBlueprintRepository.cs | 7 | I1 |
| api/Elmanhg.Infrastructure/Migrations/20260928172138_AddExamBlueprints.cs (+ .Designer.cs) | 72 (+1744) | I3: CreateTable + 2 FKs (Restrict) + 2 partial unique indexes; no Drop/Rename |
| api/Elmanhg.Api/Controllers/ExamBlueprints/ExamBlueprintsController.cs | 53 | P1, 4 actions, `Blueprints.Manage` |
| api/Elmanhg.Tests/Builders/ExamBlueprintBuilder.cs | 28 | T3 |
| api/Elmanhg.Tests/Domain/ExamBlueprints/ExamBlueprintTests.cs | 137 | T1 (tests 1–10) |
| api/Elmanhg.Tests/Domain/ExamBlueprints/ExamBlueprintShortfallTests.cs | 53 | T2 (11–15) |
| api/Elmanhg.Tests/Application/Features/ExamBlueprints/Shared/ExamBlueprintInputValidatorTests.cs | 142 | T4 (16–30) |
| api/Elmanhg.Tests/Application/Features/ExamBlueprints/GetSubjectExamBlueprints/*Tests.cs | 78/26 | T5 (31, 35–37) |
| api/Elmanhg.Tests/Application/Features/ExamBlueprints/SaveSubjectExamBlueprint/*Tests.cs | 119/47 | T6 (32, 38–43) |
| api/Elmanhg.Tests/Application/Features/ExamBlueprints/SaveUnitExamBlueprint/*Tests.cs | 99/39 | T7 (33, 44–48) |
| api/Elmanhg.Tests/Application/Features/ExamBlueprints/DeleteExamBlueprint/*Tests.cs | 78/26 | T8 (34, 49–52) |
| api/Elmanhg.Tests/Integration/ExamBlueprints/ExamBlueprintsEndpointTests.cs | 298 | T9 (53–69) |
| api/Elmanhg.Tests/Integration/ExamBlueprints/ExamBlueprintTestData.cs | 30 | T10 |
| api/Elmanhg.Tests/Integration/Persistence/ExamBlueprintPersistenceTests.cs | 91 | T11 (70–72) |
| web/src/features/blueprints/schemas/blueprintsSearchSchema.ts | 7 | W1 |
| web/src/features/blueprints/schemas/examBlueprintSchema.ts | 69 | W2 |
| web/src/features/blueprints/api/blueprintValues.ts | 87 | W3 |
| web/src/features/blueprints/hooks/useBlueprintsSearch.ts | 15 | W4 |
| web/src/features/blueprints/hooks/useBlueprintSave.ts | 36 | W5 |
| web/src/features/blueprints/hooks/useBlueprintDelete.ts | 29 | W6 |
| web/src/features/blueprints/components/ShortfallNotice.tsx | 29 | W7 |
| web/src/features/blueprints/components/TypeCountsTable.tsx | 94 | W8 |
| web/src/features/blueprints/components/DifficultyMixFields.tsx | 32 | W9 |
| web/src/features/blueprints/components/BlueprintEditor.tsx | 90 | W10 |
| web/src/features/blueprints/components/UnitBlueprintSection.tsx | 106 | W11 |
| web/src/features/blueprints/components/SubjectBlueprints.tsx | 59 | W12 |
| web/src/features/blueprints/components/SubjectPicker.tsx | 35 | W13 |
| web/src/features/blueprints/components/BlueprintsEmptyState.tsx | 15 | W14 |
| web/src/features/blueprints/pages/ExamBlueprintsPage.tsx | 48 | W15 |
| web/src/features/blueprints/i18n/{ar,en}.json | 55/55 | W16 |
| web/src/features/blueprints/locales.ts | 4 | W17 |
| web/src/features/blueprints/index.ts | 3 | W18 |
| web/src/test/blueprintFixtures.ts | 51 | W19 |
| web/src/features/blueprints/{pages/ExamBlueprintsPage,components/BlueprintEditor,components/UnitBlueprintSection}.test.tsx, api/blueprintValues.test.ts, schemas/examBlueprintSchema.test.ts, schemas/blueprintsSearchSchema.test.ts | 99/114/104/65/86/14 | W20 (tests 74–103) |
| docs/exam-blueprints.md | 127 | X1 |

## Files modified
| Path | Change |
|---|---|
| api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs | `// EXAM BLUEPRINTS`: Shortfall, DefaultNotDeletable |
| api/Elmanhg.Application/Exceptions/ErrorCodes.cs | `// EXAM BLUEPRINTS`: 10 codes |
| api/Elmanhg.Domain/Questions/IQuestionRepository.cs | `CountServableByUnitAndTypeAsync` |
| api/Elmanhg.Infrastructure/Questions/QuestionRepository.cs | implemented as the plan's query (WhereServable + Join Lesson + GroupBy) |
| api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs | 2 index consts, DbSet, `ConfigureExamBlueprints`, 409 catch, global filter |
| api/Elmanhg.Infrastructure/DependencyInjection.cs | repository registration |
| api/Elmanhg.Application/DependencyInjection.cs | `ExamBlueprintsOptions` bound + ValidateOnStart |
| api/Elmanhg.Api/appsettings.example.json | `ExamBlueprints` section after `Progress` |
| api/Elmanhg.Api/Resources/Messages.{ar,en}.resx | 12 entries each |
| api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs | 2 in-memory settings |
| api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs | 17th migration `_AddExamBlueprints` (test 73) |
| api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs | regenerated |
| api/openapi/v1.json | regenerated by `dotnet build` (+502 lines) |
| postman/elmanhg.postman_collection.json | folder `ExamBlueprints` (6 requests, plan order) before `AuditLogs`; variables `blueprintSubjectId`, `blueprintUnitId` |
| web/src/routes/admin/blueprints.tsx | placeholder replaced by `validateSearch` + `ExamBlueprintsPage` |
| web/src/app/i18n.ts | `blueprints` namespace |
| web/src/features/questions/index.ts | exports `questionTypes` |
| web/src/shared/i18n/{ar,en}.json | `errors.*` for the 12 codes |
| web/src/shared/api/generated/** | regenerated by `npm run gen:api` (exam-blueprints client, msw, zod, 8 model types) |
| docs/PRD.md | §10.2 paragraph appended; §15 Subject/Unit lose blueprint ids, ExamBlueprint row replaced |
| docs/audit-log.md | 3 command rows; `ExamBlueprint` in audited entities |
| docs/claude-design-prompt.md | §4 `#/admin/blueprints` bullet replaced |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| A3: `RuleFor(x => x.DifficultyMix).ChildRules(...)` | Does not compile: `ExamDifficultyMixInput?` makes the child lambdas nullable (CS8602 as error). | `RuleFor(x => x.DifficultyMix!).ChildRules(...)`, still guarded by `.When(x => x.DifficultyMix is not null)`. Behaviour unchanged. |
| A3: only the `PassMarkMax` constant carries a WHY comment | `PercentMax` is also an invariant constant | Gave `PercentMax` a one-line WHY comment too (skill §1 "invariants → named constants with a WHY comment"). |
| Resx ar string for `EXAM_BLUEPRINT_SHORTFALL`: `غير كافٍ` | Skill §7.5: resx Arabic has no tashkeel | Wrote `غير كاف` (the web string in the plan already has no tanween). The blueprints i18n `page.intro` keeps the plan's text verbatim. |
| W2: `blueprintMaxQuestionCount`, `blueprintMaxTimeLimitMinutes`, `passMarkMax` | The mix rule needs a 100 bound too | Added a private `percentMax = 100` in the schema file (not exported). |
| W10 props list includes `serverErrorFields` | The same row says it is a constant in the file | Implemented as a module constant, not a prop. |
| W8/W10 live values | `form.watch()` is flagged by the React Compiler lint and may not re-render memoised children | Used `useWatch({ control, name: 'counts' })` (+ `getValues()`) in `BlueprintEditor` and `TypeCountsTable`. |
| T10 `SeedServableMcqAsync` return type unspecified | Test 53 needs the lesson id to add a pending question | Returns `Task<Guid>` (the Published lesson id). |
| W8 `sm:` grid breakpoints (not in plan, my first draft) | The design tokens define only `md` and `lg` | Used `md:grid-cols-*`. |

No file outside *Files to create* / *Existing code touched* was added or edited (besides generated output the plan lists).

## Build & test
- `dotnet build api/Elmanhg.slnx --no-incremental`: 0 errors; the only warnings are the pre-existing CS8618 ones in `api/core-libraries` (grep for warnings outside core-libraries is empty).
- `dotnet format api/Elmanhg.slnx --verify-no-changes --exclude api/core-libraries`: exit 0.
- `dotnet test api/ -c Release` with `api/Elmanhg.Api/appsettings.json` moved aside (restored after): **Passed, total 1618, failed 0, succeeded 1618, skipped 0**. The new tests are 80 (15 domain, 34 validator, 18 handler, 17 endpoint, 3 persistence; the `[Theory]` counts twice) plus the edited `AppDbContextTests` row.
- `npm --prefix web run gen:api`: regenerated; running it again gives no further change.
- `npm --prefix web run typecheck`: clean. `npm --prefix web run lint`: clean (`--max-warnings=0`).
- `npm --prefix web test -- --run`: **Test Files 90 passed (90), Tests 560 passed (560)**. 31 of them are new.
- `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` in `web/`: "All matched files use Prettier code style!"
- `npm --prefix web run build`: exit 0. The `blueprints` chunk is 22.8 kB; the questions barrel import does not pull in TipTap.
- Mutation checks, each applied and then reverted:
  - API, 5 mutations at once: `Find` using `>=`; the unit handler using the subject pool; the duplicate rule weakened; the default-delete guard disabled; the 409 catch dropping the unit index. 14 tests failed, and every mutation was caught by its dedicated test: `Find_EnoughForEveryType_ReturnsEmpty`, `Handle_OtherUnitsQuestions_DoNotCount`, `Validate_DuplicateType_FailsTypeDuplicate`, `Delete_SubjectDefault_*`, and `SaveChanges_SecondBlueprintForUnit_ThrowsModifiedConcurrently`.
  - Web, 5 mutations: `findShortfall` using `>=`; the client save guard disabled; `onSettled` invalidation removed; the mix-sum rule removed; the delete call removed. 5 tests failed, one per mutation.

## Notes for review
- The integration tests read enum values over the wire as PascalCase (`"Mcq"`). Domain jsonb stays camelCase (`"mcq"`), per Decision 10.
- `ExamBlueprintInputValidator` assumes no `null` elements inside `typeCounts`. A JSON body like `[null]` would reach `Sum` and throw an NRE, which would surface as a 500. The plan's rules have the same gap; I did not change them.
- The local dev `api/Elmanhg.Api/appsettings.json` was not given the new `ExamBlueprints` section. The code defaults (100/300) satisfy `ValidateOnStart`, so `dotnet run` still starts.
- Test 89 checks the available cells inside the default editor region. That region is a `<section aria-label>`, so the tests find it with `getByRole('region', { name })`.
- `docs/prototype.md` already says "Blueprint editor refuses to save when there is a shortfall", so it needed no edit.
