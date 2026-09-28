# Implementation — [E3.S3] Bulk question import from spreadsheet (#66)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| api/Elmanhg.Domain/Questions/QuestionImportBatch.cs | 24 | D1 idempotency entity (`Create`, `Matches`) |
| api/Elmanhg.Domain/Questions/IQuestionImportBatchRepository.cs | 5 | D2 |
| api/Elmanhg.Domain/Questions/Question.Import.cs | 14 | D3 `Question.CreateImported` |
| api/Elmanhg.Application/Shared/Spreadsheets/ISpreadsheetReader.cs | 6 | S1 port |
| api/Elmanhg.Application/Shared/Spreadsheets/ISpreadsheetWriter.cs | 6 | S2 port |
| api/Elmanhg.Application/Shared/Spreadsheets/SpreadsheetWorkbook.cs | 7 | S3 records |
| api/Elmanhg.Application/Shared/Spreadsheets/SpreadsheetSheetDefinition.cs | 5 | S4 records |
| api/Elmanhg.Application/Questions/Shared/Import/QuestionImportFile.cs | 20 | I1 |
| api/Elmanhg.Application/Questions/Shared/Import/QuestionImportFileValidation.cs | 21 | I2 |
| api/Elmanhg.Application/Questions/Shared/Import/QuestionImportColumns.cs | 107 | I3 column contract |
| api/Elmanhg.Application/Questions/Shared/Import/QuestionImportRowError.cs | 3 | I4 |
| api/Elmanhg.Application/Questions/Shared/Import/QuestionImportParse.cs | 5 | I5 |
| api/Elmanhg.Application/Questions/Shared/Import/QuestionImportCells.cs | 84 | I6 typed cell reads |
| api/Elmanhg.Application/Questions/Shared/Import/QuestionImportText.cs | 41 | I7 text → HTML + inline math |
| api/Elmanhg.Application/Questions/Shared/Import/QuestionImportHeaders.cs | 37 | I8 |
| api/Elmanhg.Application/Questions/Shared/Import/QuestionImportBodies.cs | 56 | I9 per-type body/spec |
| api/Elmanhg.Application/Questions/Shared/Import/QuestionImportRowMapper.cs | 49 | I10 |
| api/Elmanhg.Application/Questions/Shared/Import/QuestionImportParser.cs | 62 | I11 (reuses `IValidator<QuestionFields>`) |
| api/Elmanhg.Application/Questions/Shared/Import/QuestionImportHelpKeys.cs | 25 | I12 |
| api/Elmanhg.Application/Questions/Shared/Import/QuestionImportTemplate.cs | 59 | I13 |
| api/Elmanhg.Application/Questions/GetQuestionImportTemplate/GetQuestionImportTemplateQuery.cs | 5 | U1 |
| api/Elmanhg.Application/Questions/GetQuestionImportTemplate/QuestionImportTemplateResult.cs | 3 | U2 |
| api/Elmanhg.Application/Questions/GetQuestionImportTemplate/GetQuestionImportTemplateHandler.cs | 18 | U3 |
| api/Elmanhg.Application/Questions/PreviewQuestionImport/PreviewQuestionImportQuery.cs | 6 | U4 |
| api/Elmanhg.Application/Questions/PreviewQuestionImport/PreviewQuestionImportValidator.cs | 17 | U5 |
| api/Elmanhg.Application/Questions/PreviewQuestionImport/QuestionImportPreviewResult.cs | 8 | U6 |
| api/Elmanhg.Application/Questions/PreviewQuestionImport/PreviewQuestionImportHandler.cs | 33 | U7 |
| api/Elmanhg.Application/Questions/ImportQuestions/ImportQuestionsCommand.cs | 12 | U8 (audited `Question.Import`) |
| api/Elmanhg.Application/Questions/ImportQuestions/ImportQuestionsValidator.cs | 18 | U9 |
| api/Elmanhg.Application/Questions/ImportQuestions/ImportQuestionsResult.cs | 3 | U10 |
| api/Elmanhg.Application/Questions/ImportQuestions/ImportQuestionsHandler.cs | 65 | U11 |
| api/Elmanhg.Application/Questions/ImportQuestions/QuestionImportReplay.cs | 18 | **addendum** — shared "stored batch → replay or 409" rule |
| api/Elmanhg.Application/Questions/ImportQuestions/ImportQuestionsReplayBehaviour.cs | 30 | **addendum** — closed pipeline behaviour resolving the concurrent-confirm race |
| api/Elmanhg.Infrastructure/Questions/QuestionImportBatchRepository.cs | 7 | F1 |
| api/Elmanhg.Infrastructure/Spreadsheets/ClosedXmlSpreadsheetReader.cs | 67 | F2 |
| api/Elmanhg.Infrastructure/Spreadsheets/ClosedXmlSpreadsheetWriter.cs | 52 | F3 |
| api/Elmanhg.Infrastructure/Migrations/20260928055532_AddQuestionImportBatches.cs (+ .Designer.cs) | 84 | migration: create table, nullable `Questions.ImportBatchId` + index + Restrict FK, no drops |
| api/Elmanhg.Api/Controllers/Questions/QuestionImportsController.cs | 45 | A1 three endpoints, `ContentManage` |
| docs/question-import.md | 128 | X1 |
| api/Elmanhg.Tests/Builders/QuestionWorkbookBuilder.cs | 39 | test builder |
| api/Elmanhg.Tests/Domain/Questions/QuestionImportBatchTests.cs | 42 | tests 1–4 |
| api/Elmanhg.Tests/Domain/Questions/QuestionImportTests.cs | 23 | test 5 |
| api/Elmanhg.Tests/Application/Features/Questions/Shared/Import/QuestionImportTextTests.cs | 43 | 6–11 |
| api/Elmanhg.Tests/Application/Features/Questions/Shared/Import/QuestionImportCellsTests.cs | 107 | 12–22 |
| api/Elmanhg.Tests/Application/Features/Questions/Shared/Import/QuestionImportColumnsTests.cs | 42 | 23–26 |
| api/Elmanhg.Tests/Application/Features/Questions/Shared/Import/QuestionImportParserTests.cs | 194 | 27–44 |
| api/Elmanhg.Tests/Application/Features/Questions/Shared/Import/QuestionImportTemplateTests.cs | 54 | 45–48 |
| api/Elmanhg.Tests/Application/Features/Questions/PreviewQuestionImport/PreviewQuestionImportValidatorTests.cs | 47 | 49–53 |
| api/Elmanhg.Tests/Application/Features/Questions/ImportQuestions/ImportQuestionsValidatorTests.cs | 33 | 54–56 |
| api/Elmanhg.Tests/Application/Features/Questions/PreviewQuestionImport/PreviewQuestionImportHandlerTests.cs | 63 | 57–59 |
| api/Elmanhg.Tests/Application/Features/Questions/ImportQuestions/ImportQuestionsHandlerTests.cs | 154 | 60–67 |
| api/Elmanhg.Tests/Application/Features/Questions/ImportQuestions/ImportQuestionsReplayBehaviourTests.cs | 61 | **addendum** — 3 behaviour branches |
| api/Elmanhg.Tests/Application/Features/Questions/GetQuestionImportTemplate/GetQuestionImportTemplateHandlerTests.cs | 30 | 68 |
| api/Elmanhg.Tests/Infrastructure/Spreadsheets/ClosedXmlSpreadsheetReaderTests.cs | 65 | 69–72 |
| api/Elmanhg.Tests/Infrastructure/Spreadsheets/ClosedXmlSpreadsheetWriterTests.cs | 46 | 73–75 |
| api/Elmanhg.Tests/Integration/Content/QuestionImportEndpointTests.cs | 331 | 76–88 + **addendum** `PostImport_ConcurrentConfirmOfSameBatch_ReplaysInsteadOfFailing` |
| web/src/routes/admin/question.import.$lessonId.tsx | 11 | W1 |
| web/src/features/questions/schemas/questionImportSchema.ts | 12 | W2 |
| web/src/features/questions/api/downloadBlob.ts | 10 | W3 |
| web/src/features/questions/hooks/useQuestionImportTemplate.ts | 27 | W4 |
| web/src/features/questions/hooks/useQuestionImport.ts | 62 | W5 |
| web/src/features/questions/components/QuestionImportHeader.tsx | 35 | W6 |
| web/src/features/questions/components/QuestionImportTemplateCard.tsx | 20 | W7 |
| web/src/features/questions/components/QuestionImportForm.tsx | 83 | W8 |
| web/src/features/questions/components/QuestionImportErrorTable.tsx | 49 | W9 |
| web/src/features/questions/components/QuestionImportReport.tsx | 60 | W10 |
| web/src/features/questions/components/QuestionImportSuccess.tsx | 30 | W11 |
| web/src/features/questions/pages/QuestionImportPage.tsx | 72 | W12 |
| web/src/features/questions/pages/QuestionImportPage.test.tsx | 253 | W13, tests 94–105 |
| web/src/features/questions/schemas/questionImportSchema.test.ts | 24 | W14, tests 89–92 |
| web/src/shared/api/generated/question-imports/**, zod/question-imports/**, model/{importQuestionsBody,importQuestionsResult,previewQuestionImportBody,questionImportPreviewResult,questionImportRowError,questionImportTypeCount,stream}.ts | gen | Orval regeneration |

## Files modified
| Path | Change |
|---|---|
| api/Directory.Packages.props | `ClosedXML` 0.105.1 pinned (transitives: OpenXml 3.1.1, ClosedXML.Parser 2.0.0, ExcelNumberFormat 1.1.0, RBush.Signed 4.0.0, SixLabors.Fonts 1.0.0) |
| api/Elmanhg.Infrastructure/Elmanhg.Infrastructure.csproj, api/Elmanhg.Tests/Elmanhg.Tests.csproj | `<PackageReference Include="ClosedXML" />` |
| api/Elmanhg.Domain/Questions/Question.cs | `Guid? ImportBatchId` after `RejectionReason` |
| api/Elmanhg.Application/Exceptions/ErrorCodes.cs | `// QUESTION IMPORTS` (12) and `// SPREADSHEETS` (1) |
| api/Elmanhg.Application/Shared/Options/ContentOptions.cs | `QuestionImportMaxRows`, `QuestionImportMaxFileSizeInMb` (`[Range(1, int.MaxValue)]`) |
| api/Elmanhg.Application/DependencyInjection.cs | **addendum, not in plan** — registers `IPipelineBehavior<ImportQuestionsCommand, ImportQuestionsResult>` → `ImportQuestionsReplayBehaviour` (after SubjectScope, so innermost; audit stays outermost) |
| api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs | DbSet, `ConfigureQuestionImportBatches`, `Sha256HexLength` const, Question→batch Restrict FK, soft-delete filter; **addendum**: `SaveChangesAsync` override translating `DbUpdateException`/`PostgresException{UniqueViolation, TableName: QuestionImportBatches}` into `ConflictCoreException(QUESTION_IMPORT_BATCH_CONFLICT, inner)` |
| api/Elmanhg.Infrastructure/DependencyInjection.cs | batch repository (scoped), ClosedXML reader/writer (singletons) |
| api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs | updated |
| api/Elmanhg.Api/Resources/Messages.en.resx, Messages.ar.resx | 13 error codes + 20 help keys (intro + 19) |
| api/Elmanhg.Api/appsettings.example.json | `QuestionImportMaxRows: 500`, `QuestionImportMaxFileSizeInMb: 5` |
| api/openapi/v1.json | regenerated by build |
| api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs | the two Content keys |
| api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs | appended `tenth => _AddQuestionImportBatches` |
| api/Elmanhg.Tests/Integration/OpenApi/OpenApiEndpointTests.cs | `Get_OpenApiDocument_DescribesImportTemplateAsBinary` |
| postman/elmanhg.postman_collection.json | `importBatchId` variable; three requests after "Resubmit question" (template GET, preview POST with pre-request guid script, import POST); Questions folder description extended |
| docs/audit-log.md | `ImportQuestions` row; `QuestionImportBatch` in audited entities |
| docs/claude-design-prompt.md | lesson editor + lesson-filter bar mention the import link; `#/admin/question/import/:lessonId` screen spec |
| docs/question-schemas.md | "Changing a schema" sentence about the import |
| web/src/shared/lib/http.ts / http.test.ts | Blob for non-JSON `Content-Type`; test 93 |
| web/src/shared/i18n/en.json, ar.json | 13 `errors` keys |
| web/src/features/questions/i18n/en.json, ar.json | `import` block, `list.importInLesson` |
| web/src/features/content/i18n/en.json, ar.json | `lessonEditor.importQuestions` |
| web/src/features/questions/index.ts | export `QuestionImportPage` |
| web/src/features/questions/pages/QuestionListPage.tsx / .test.tsx | import link in lesson-filter bar; test 106 |
| web/src/features/content/pages/LessonEditorPage.tsx / .test.tsx | import link between the two buttons; test 107 |
| web/src/routeTree.gen.ts | regenerated by `vite build` |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Decision 16 / risk: concurrent double submit of one batch id is "not mapped", surfaces as 500 | Orchestrator addendum (00-acceptance.md) puts it in scope: replay for same hash, 409 for different hash | Infrastructure (`AppDbContext.SaveChangesAsync`) translates the unique violation on `QuestionImportBatches` into `ConflictCoreException(QUESTION_IMPORT_BATCH_CONFLICT)`; new closed MediatR behaviour `ImportQuestionsReplayBehaviour` catches that conflict, re-reads the committed batch (no tracking) and returns the replay or rethrows 409 via new `QuestionImportReplay.Resolve`, which the handler also uses for its pre-check (instead of the inline code in U11 step 3). The handler itself still has no `try/catch` and one `SaveChangesAsync`. Added `Application/DependencyInjection.cs` registration, 3 behaviour unit tests and 1 deterministic integration test (holds the batch row in an uncommitted Npgsql transaction, waits until the API's insert is blocked on it via `pg_stat_activity`, commits, asserts 200 `replayed: true`). Verified non-vacuous: with the behaviour unregistered the test gets 409. `docs/question-import.md` Idempotency describes this instead of the "500" note. |
| I2 `ValidateQuestionImportFile<T>(this IRuleBuilder<T, IFormFile?> rule, ...)` starting with `rule.Cascade(...)` | FluentValidation's `Cascade` exists only on `IRuleBuilderInitial<T, TProperty>`; the plan's signature does not compile | Parameter type is `IRuleBuilderInitial<T, IFormFile?>` (what `RuleFor` returns); body as planned |
| I3 `AllowedValues` literal lists `easy,medium,hard` etc. | — | Same values, derived from `Enum.GetNames<...>()` lower-cased (difficulty, answer kind, tolerance mode) and `bool.TrueString/FalseString` lower-cased, so the tokens cannot drift from the enums the parser accepts |
| OpenAPI test: `...content[xlsx].schema` has `type`/`format` | MS OpenAPI 10 emits `{"$ref": "#/components/schemas/Stream"}` whose component is `{"type":"string","format":"binary"}` | Test follows the `$ref` to the component, then asserts `string`/`binary` |
| W5 `confirm()` errors surface via mutation `onError` toast | `confirm` is called as `void confirm()`; a rejected `mutateAsync` would be an unhandled rejection | `confirm` wraps `mutateAsync` in `try/catch` and shows the same `common:errors.<code>` toast in the catch; `onSuccess` (invalidate + toast) stays on the mutation |
| Test 106 path `/admin/questions?lessonId=l1` | The list test file already uses a UUID `lessonId` constant | Used that constant (same assertion shape) |

## Build & test
All run in this container (Docker up), `api/Elmanhg.Api/appsettings.json` does not exist here, so this matches CI.

- `dotnet build api/ -c Release` → `Build succeeded.` `0 Warning(s)` (incremental; no warnings from Elmanhg projects on the full build either — only the known core-libraries CS86xx).
- `git status --porcelain api/openapi` → ` M api/openapi/v1.json` (regenerated, ready to commit; the diff adds the three operations, `Stream`, preview/import result schemas and the `QuestionImports` tag).
- `dotnet test api/ -c Release --no-build` → `total: 875  failed: 0  succeeded: 875  skipped: 0`.
- `dotnet list api/ package --vulnerable --include-transitive` → every project "has no vulnerable packages"; SixLabors.Fonts resolves to 1.0.0.
- `dotnet tool restore && dotnet ef migrations has-pending-model-changes ... --configuration Release --no-build` (env `ConnectionStrings__DbConnectionString=Host=localhost;Database=elmanhg_design;Username=design`) → `No changes have been made to the model since the last migration.`
- `dotnet format api/Elmanhg.slnx --verify-no-changes` → exit 2, all 108 findings inside `api/core-libraries` (known whitespace noise per PROGRESS.md Gotchas); none in Elmanhg projects.
- Guard grep (§9, including untracked files, plus `AdjustToContents` / bare `catch (Exception)`) → prints nothing.
- web: `npm run gen:tokens` (no tokens.css drift), `npm run gen:api` (re-run produces no further diff; the new generated files are part of this change), `npm run typecheck` → clean, `npm run lint` → clean, `npm run format:check` → "All matched files use Prettier code style!", CI literal/direction greps → clean, `npm test -- --run --coverage` → `Test Files 60 passed (60)`, `Tests 344 passed (344)` (All files 94.65 % lines), `npm run build` → `✓ built` (the >500 kB chunk warning is pre-existing), `npm audit --audit-level=high` → 0 vulnerabilities, `git status --porcelain src/routeTree.gen.ts` → modified (regenerated, to commit).
- `ai/` not touched; pytest not run.

## Notes for review
- `ImportQuestionsReplayBehaviour` catches every `ConflictCoreException` with code `QUESTION_IMPORT_BATCH_CONFLICT`, including the handler's own pre-check 409; in that case it re-reads the batch and `Resolve` rethrows the same 409 (one extra file read, identical outcome). I chose that over filtering on `InnerException` to avoid a hidden coupling.
- The race path writes the audit row as `Success` with no diff (the failed save recorded nothing), matching "a replay writes a row with no diff".
- The Unique-violation translation in `AppDbContext` is table-scoped (`TableName == "QuestionImportBatches"`); other unique violations still behave as before.
- The integration race test polls `pg_stat_activity` (`wait_event_type = 'Lock' AND query LIKE '%QuestionImportBatches%'`) every 50 ms up to 10 s — an Eventually-style poll, not a fixed sleep.
- `QuestionImportColumns.cs` is 107 lines (constant block + contract switch); I kept it in one file because every member is the column contract.
- `QuestionImportParserTests.ImportOptions()` / `Sheet(...)`, `PreviewQuestionImportValidatorTests.Upload(...)` and `ImportQuestionsHandlerTests.Upload()/ContentHash` are `internal static` helpers shared by sibling test classes (no extra helper file).
- Test 36 uses an empty stem alongside the invalid difficulty so "exactly one error" also proves the validator was skipped.
- `PROGRESS.md` left untouched (orchestrator edit). Nothing committed.
