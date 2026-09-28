# Plan — [E3.S3] Bulk question import from spreadsheet (#66)

## Goal
An admin opens a lesson, chooses "Import questions" and reaches `#/admin/question/import/:lessonId`. From there they download an Excel template. The template has an Instructions sheet (in the request language) and one sheet per v1 type: Mcq, Multi, TrueFalse, Fill and Short. They fill in rows and upload the file for a dry run. The server parses every row, validates it with the same per-type rules as the question editor (#65), and returns a report: rows found, rows ready per type, and every problem by sheet, row and column. It saves nothing. When the report is clean, the admin confirms and every row is created as a Pending question in that lesson, in one transaction. The confirm carries a client-generated import batch id, so a retried or double-submitted confirm of the same file creates nothing new and returns the original outcome (PRD §10.1).

## Scope
**In:**
- **Domain:**
  - `QuestionImportBatch` entity (idempotency record).
  - `Question.ImportBatchId` and `Question.CreateImported(...)`.
- **Application:**
  - `ISpreadsheetReader` / `ISpreadsheetWriter` ports.
  - The shared import parser: columns, cells, headers, row mapper, per-type bodies, text-to-HTML.
  - Queries `GetQuestionImportTemplate` and `PreviewQuestionImport`.
  - Command `ImportQuestions`, which is audited.
  - `ContentOptions` caps.
  - Error codes and help-text keys.
- **Infrastructure:**
  - ClosedXML adapters.
  - `QuestionImportBatchRepository`.
  - `AppDbContext` mapping.
  - Migration `AddQuestionImportBatches`.
- **API:**
  - `QuestionImportsController`, with three endpoints.
  - resx strings in ar and en.
  - `appsettings.example.json`.
  - Regenerated `api/openapi/v1.json`.
  - Postman.
- **Web:**
  - The import page and route.
  - Links from the lesson editor and from the lesson-filtered question list.
  - Blob support in the `http` mutator.
  - Regenerated Orval client.
  - i18n strings.
- **Docs:**
  - New `docs/question-import.md`.
  - `docs/audit-log.md`, `docs/claude-design-prompt.md` §4 and `docs/question-schemas.md`.

**Out:**
- CSV and `.xls`.
- One file spanning several lessons.
- Images and block math in cells.
- Arabic column names or Arabic enum tokens.
- Importing essay and math-steps (v2) types.
- Filtering the question list by batch.
- Undoing an import.

**Deferred:** none. Nothing here needs credentials or an online service; ClosedXML runs offline.

## Morabh reuse
| Piece | Source | Use |
|---|---|---|
| Bulk-create command shape: one audited command, N entities, one `SaveChangesAsync` | `/home/user/apis/Morabh.Application/Brands/Branches/AddBranches/AddBranchesCommand.cs`, `AddBranchesHandler.cs` | Shape of `ImportQuestionsCommand`/handler; audit action named `Question.Import`, in the same style as `Branch.CreateBulk` |
| File validation extensions | `api/core-libraries/Core.Validation/Extensions/FileValidationExtensions.cs` (vendored from Morabh `Core.Validation`) | `ValidateAllowedExtensions`, `ValidateMaxFileSize` |
| Spreadsheet read/write, template generation, idempotency batch, dry-run report | none | new — no Morabh equivalent (Morabh has no ClosedXML/EPPlus/CSV code and no idempotency key; searched `/home/user/apis` for ClosedXML, EPPlus, ExcelDataReader, NPOI, MiniExcel, CsvHelper, OpenXml, xlsx, Idempotency, Import, Bulk) |

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | Spreadsheet library | **ClosedXML 0.105.1** (MIT; published 2026-07-25). It brings DocumentFormat.OpenXml 3.1.1 (MIT), ClosedXML.Parser 2.0.0 (MIT), ExcelNumberFormat 1.1.0 (MIT), RBush.Signed 4.0.0 (MIT) and SixLabors.Fonts 1.0.0 (Apache-2.0; NuGet resolves the lowest version in `[1.0.0,3.0.0)`, which is not the split-licence 2.x). A scratch project ran `dotnet list package --vulnerable --include-transitive` and it came back clean. | Most-used maintained .NET xlsx library. It reads and writes, and supports right-to-left sheets and list data-validation (dropdowns) for the template. MiniExcel has no data validation. Raw OpenXml is 5× the code. |
| 2 | File format | `.xlsx` only | Excel opens UTF-8 CSV as ANSI, which garbles Arabic. One workbook can hold a sheet per type. |
| 3 | "Template per v1 type" | One workbook: an `Instructions` sheet plus five sheets named exactly `Mcq`, `Multi`, `TrueFalse`, `Fill`, `Short` (the `QuestionType` names). The admin uploads the same workbook. | One download, one upload. Each type still has its own sheet and columns. |
| 4 | Where rows land | An import targets **one lesson**, the `lessonId` form field. The page is reached from that lesson. | Matches `#/admin/question/new/:lessonId`. Objectives can then be referenced by their number in the lesson instead of by GUID. |
| 5 | Header row | Row 1 holds fixed English snake_case keys (the list is in "Column contract"). Headers are matched trimmed and case-insensitive, in any column order. An empty header cell makes its column ignored. An unknown header is an error on row 1 (`QUESTION_IMPORT_COLUMN_UNKNOWN`), and a repeated header is an error on row 1 (`QUESTION_IMPORT_COLUMN_DUPLICATE`). A missing column reads as empty. | Strict on typos, so data is never silently dropped. Tolerant of column order and of a template generated with a different options cap. |
| 6 | Sheets and rows | A sheet whose name is not a type name is ignored (this includes `Instructions`). Rows with every cell blank are skipped. `Row` in the report is the Excel row number (the header is row 1). | This is what the admin sees in Excel. |
| 7 | Enum and boolean tokens | Difficulty `easy/medium/hard`, answer kind `numeric/text` and tolerance mode `absolute/percent` are matched case-insensitively against enum names; numeric text is rejected. A boolean is `true/yes/1` or `false/no/0`, case-insensitive. The template adds dropdowns with exactly these values. | Deterministic, and the same as the schema wire names. |
| 8 | Lists | Values separated by `\|`, trimmed, empty values dropped. Used by `correct` (Multi), `blank_N`, `accepted_answers` and `tags`. | Accepted answers can contain commas (`3,5`). |
| 9 | Rich-text fields from plain cells | `stem`, `explanation` and each `option_x` go through `QuestionImportText.ToHtml`: <ul><li>normalise `\r\n` to `\n`</li><li>each non-blank line becomes `<p>…</p>`, HTML-encoded</li><li>`$latex$` (no newline inside) becomes `<span data-type="inline-math" data-latex="…"></span>`</li></ul> The existing `IRichTextSanitizer` still runs at save, through `QuestionContentFactory`. | Produces the same markup the TipTap editor stores (`docs/rich-text.md`), so math questions can be imported. Block math and images are added later in the editor. |
| 10 | Option and blank ids | Options are `option_a`…, and the id is the letter, the same ids the editor generates (`a`, `b`, …). The column count is `min(QuestionOptionsMaxCount, 26)`. Blanks are `blank_1`…`blank_{QuestionBlanksMaxCount}` with ids `"1"`, `"2"`…. Empty option and blank columns are skipped, so gaps are allowed. | Imported questions look exactly like editor-made ones. |
| 11 | Defaults for empty cells | <ul><li>`max_score` → 1</li><li>`partial_credit` → false</li><li>`unify_letter_variants` → true</li><li>numeric `tolerance` → 0</li><li>`tolerance_mode` → absolute</li><li>`difficulty` has no default: the existing `QUESTION_DIFFICULTY_REQUIRED` applies</li></ul> | These are the editor defaults (`emptyQuestionValues`), except difficulty. A silent Medium would skew adaptive selection across a 100k bank. |
| 12 | Row validation | The row mapper only turns cells into a `QuestionFields`. Cell-level parse failures use the new `QUESTION_IMPORT_CELL_INVALID` and `QUESTION_IMPORT_OBJECTIVE_INVALID`. Everything else is checked by the existing **`QuestionFieldsValidator`**, injected as `IValidator<QuestionFields>` (already registered by `AddValidatorsFromAssembly`). Each failure's `ErrorCode` is reported, de-duplicated per row. A row with cell errors skips the validator. | The story says to reuse the #65 validation rather than duplicate it. A row failure is data in a report, not a 422, so the shared parser calls the validator itself. This is the one deliberate exception to "validation only via the pipeline". |
| 13 | Dry run vs commit | `PreviewQuestionImportQuery` is a query: nothing is saved and it is not audited. `ImportQuestionsCommand` is `IAuditableCommand`. Both call the same `QuestionImportParser`. | A dry run must not write audit rows or data. |
| 14 | Commit when rows are invalid | All-or-nothing. Any row or header error throws `BusinessRuleViolationCoreException(QUESTION_IMPORT_HAS_ERRORS, context {count})` (400). Nothing is created and no batch is stored. | A partial import leaves the admin not knowing which rows landed. The UI only enables confirm after a clean dry run. |
| 15 | Idempotency | The client generates `batchId` (UUID) when a file is chosen and reuses it while the same `File` object stays selected. The server stores `QuestionImportBatch { Id = batchId, LessonId, FileHash (SHA-256 hex of the uploaded bytes), QuestionCount }` in the same `SaveChangesAsync` as the questions. <ul><li>Batch exists with the same lesson and hash: 200 `{ replayed: true, createdCount: stored count }`, no parsing and no writes.</li><li>Batch exists with another lesson or hash: 409 `QUESTION_IMPORT_BATCH_CONFLICT`.</li><li>A commit that failed stores nothing, so the same id can be retried after a fix.</li></ul> | Implements "idempotency via import batch id". The hash keeps a reused id from importing a different file. |
| 16 | Concurrent double submit with one batch id | Not mapped. The second insert hits the primary key, which surfaces as an unhandled 500; retrying replays. The UI disables confirm while the request is in flight. | `CoreExceptionMiddleware` has no unique-violation mapping, and a handler `try/catch` is prohibited. A true race needs two in-flight submits, which the UI prevents. |
| 17 | Traceability | `Question.ImportBatchId` (`Guid?`, FK to `QuestionImportBatches`, Restrict) is set by `Question.CreateImported`. | Audit and a later "questions from this batch" query can find them. `Create`'s signature stays unchanged. |
| 18 | Limits | `Content:QuestionImportMaxRows` = 500 and `Content:QuestionImportMaxFileSizeInMb` = 5, in `ContentOptions` (`[Range(1, int.MaxValue)]`, no code default, mirroring the existing Content caps). | The audit diff lists every created question, so 500 rows keeps one audit row about 1–2 MB. The file cap also bounds zip-expansion memory; the endpoint is admin-only. |
| 19 | Upload checks | Extension `.xlsx`, case-insensitive, and the size cap, in the validator (422). The content type is **not** checked, because browsers send `application/octet-stream` for xlsx on machines without Office. A file that does not parse → 400 `SPREADSHEET_UNREADABLE`, thrown by the reader. | Parsing is the real type check. |
| 20 | Spreadsheet ports | `ISpreadsheetReader` / `ISpreadsheetWriter` in `Application/Shared/Spreadsheets`, implemented in `Infrastructure/Spreadsheets` with ClosedXML and registered as singletons. No Fake and no config switch. | Same precedent as `IRichTextSanitizer`/`RichTextSanitizer` (HtmlSanitizer). A local library is not an external provider. |
| 21 | Template language | Column keys, sheet names and tokens are English. The Instructions sheet's description column is localised through `ILocalizer.GetMessage(key)` with new `QUESTION_IMPORT_HELP_*` resx keys, so the `Accept-Language` the web already sends decides the language. All sheets are right-to-left. | Documentation inside the file, in the admin's language, with no hard-coded message strings in C#. |
| 22 | Row-error localisation | The report carries codes only: `{ sheet, row, column?, code }`. The web translates with `common:errors.<CODE>` and `{ column }`, as it already does for every error code. | Mirrors the existing client-side error i18n. No `ILocalizer` in the parser. |
| 23 | Template response and web download | The controller returns `File(bytes, contentType, fileName)` and declares `[ProducesResponseType(typeof(Stream), 200, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]`. Microsoft.AspNetCore.OpenApi 10 maps `Stream` to `string/binary`; it does not map `FileContentResult`. `web/src/shared/lib/http.ts` returns `response.blob()` when the response `Content-Type` is present and not JSON. The page turns the Blob into an object URL and clicks a temporary `<a download>`. | Bearer auth lives in memory, so a plain link cannot download. The Orval-generated function is typed `Promise<Blob>`. |
| 24 | Routes | `GET api/question-imports/template`, `POST api/question-imports/preview` and `POST api/question-imports`. The posts are multipart with `lessonId`, `batchId` (import only) and `file`, bound as parameters like `UploadLessonImage`. All use policy `DefaultCodes.ContentManage`. | Flat, kebab-case plural resource. The binding mirrors the existing upload endpoint. |
| 25 | Lesson state | Not checked; import works for draft, published and archived lessons. | Mirrors `CreateQuestionHandler`. |
| 26 | UI | New route `/admin/question/import/$lessonId`. Links go on the lesson editor (next to "New question") and on the question list's lesson-filter bar. No nav item. There is no prototype screen, so this plan defines it, and `docs/claude-design-prompt.md` §4 is updated to match. | Mirrors the `question/new/$lessonId` entry points. |
| 27 | Audit | `AuditAction = "Question.Import"`, `AuditResourceType = "QuestionImportBatch"`, `AuditResourceId = BatchId` (from the command). `QuestionImportBatch` is `IAuditedEntity`, so the diff lists the batch and every created question. | The existing auditing pipeline, unchanged. |

## Column contract (the template, parser and docs all use this)
Order per sheet (`QuestionImportColumns.For`). The common tail is: `explanation, difficulty, max_score, objective, tags`.

| Sheet | Columns in order | Required (Instructions "yes") |
|---|---|---|
| Mcq | `stem`, `option_a`…`option_{n}`, `correct`, tail | stem, option_a, option_b, correct, difficulty |
| Multi | `stem`, `option_a`…`option_{n}`, `correct`, `partial_credit`, tail | stem, option_a, option_b, correct, difficulty |
| TrueFalse | `stem`, `correct_answer`, tail | stem, correct_answer, difficulty |
| Fill | `stem`, `blank_1`…`blank_{m}`, `unify_letter_variants`, tail | stem, blank_1, difficulty |
| Short | `stem`, `answer_kind`, `value`, `tolerance`, `tolerance_mode`, `accepted_answers`, `unify_letter_variants`, tail | stem, answer_kind, difficulty |

`n = min(QuestionOptionsMaxCount, 26)` and `m = QuestionBlanksMaxCount`.

Dropdown values (`AllowedValues`):

| Column | Values |
|---|---|
| `difficulty` | `easy,medium,hard` |
| `answer_kind` | `numeric,text` |
| `tolerance_mode` | `absolute,percent` |
| `partial_credit`, `correct_answer`, `unify_letter_variants` | `true,false` |
| all others | none |

Mapping to `QuestionFields(Type, Stem, Body, GradingSpec, Explanation, Difficulty, ObjectiveId, Tags, MaxScore)`.

Common fields:

| Field | Source |
|---|---|
| `Stem` | `ToHtml(Text(stem))` |
| `Explanation` | `ToHtml(Text(explanation))` |
| `Difficulty` | `Enum<QuestionDifficulty>(difficulty)` |
| `MaxScore` | `Integer(max_score) ?? 1` |
| `Tags` | `List(tags)` |
| `ObjectiveId` | `objective` is `Integer`: <ul><li>null → null</li><li>`1..lesson.Objectives.Count` → the `Id` of `lesson.Objectives.OrderBy(Order).ElementAt(n - 1)`</li><li>otherwise → error `QUESTION_IMPORT_OBJECTIVE_INVALID` on column `objective`</li></ul> |

Body and grading spec per type, built as `JsonSerializer.SerializeToElement(record, QuestionJson.SerializerOptions)`:

| Type | Body | GradingSpec |
|---|---|---|
| Mcq | `ChoiceBody(options)`: each option id with a non-null `Text(option_id)` gives `ChoiceOption(id, ToHtml(text))` | `McqGradingSpec(correct.Count == 1 ? correct[0] : null)`, where `correct = List(correct)` lower-cased |
| Multi | same as Mcq | `MultiGradingSpec(correct, Boolean(partial_credit) ?? false)` |
| TrueFalse | `TrueFalseBody()` | `TrueFalseGradingSpec(Boolean(correct_answer))` |
| Fill | `FillBody(ids.Select(id => new FillBlank(id)))`, where `ids` are the blank ids whose `List(blank_id)` is not empty | `FillGradingSpec(ids.Select(id => new FillBlankAnswers(id, List(blank_id))), Boolean(unify_letter_variants) ?? true)` |
| Short | `ShortBody(kind)`, where `kind = Enum<ShortAnswerKind>(answer_kind)` | <ul><li>Numeric: `ShortGradingSpec(Decimal(value), Decimal(tolerance) ?? 0, Enum<ToleranceMode>(tolerance_mode) ?? Absolute, null, null)`</li><li>Text: `ShortGradingSpec(null, null, null, List(accepted_answers), Boolean(unify_letter_variants) ?? true)`</li><li>kind null: `ShortGradingSpec(null, null, null, null, null)`, and the validator reports `QUESTION_ANSWER_KIND_REQUIRED`</li></ul> |

## Existing code touched
| File | Change |
|------|--------|
| `api/Directory.Packages.props` | Add `<PackageVersion Include="ClosedXML" Version="0.105.1" />` in the main group. |
| `api/Elmanhg.Infrastructure/Elmanhg.Infrastructure.csproj` | Add `<PackageReference Include="ClosedXML" />`. |
| `api/Elmanhg.Tests/Elmanhg.Tests.csproj` | Add `<PackageReference Include="ClosedXML" />`. Tests build workbooks directly. |
| `api/Elmanhg.Domain/Questions/Question.cs` | Add `public Guid? ImportBatchId { get; private set; }` after `RejectionReason`. |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | Add a `// QUESTION IMPORTS` group (12 constants) and a `// SPREADSHEETS` group (1 constant). See Error codes. |
| `api/Elmanhg.Application/Shared/Options/ContentOptions.cs` | Add `[Range(1, int.MaxValue)] public int QuestionImportMaxRows { get; set; }` and `[Range(1, int.MaxValue)] public int QuestionImportMaxFileSizeInMb { get; set; }`. |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | <ul><li>`public DbSet<QuestionImportBatch> QuestionImportBatches { get; set; }`</li><li>Call `ConfigureQuestionImportBatches(modelBuilder)` after `ConfigureQuestions`.</li><li>In `ConfigureQuestions`: `builder.HasOne<QuestionImportBatch>().WithMany().HasForeignKey(x => x.ImportBatchId).OnDelete(DeleteBehavior.Restrict);`</li><li>New private method: `modelBuilder.Entity<QuestionImportBatch>(builder => { builder.Property(x => x.Id).ValueGeneratedNever(); builder.Property(x => x.FileHash).IsRequired().HasMaxLength(Sha256HexLength); builder.HasOne<Lesson>().WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Restrict); });`</li><li>`private const int Sha256HexLength = 64;` with comment `// A SHA-256 digest is 32 bytes, 64 lower-case hex characters; a schema invariant.`</li><li>`modelBuilder.Entity<QuestionImportBatch>().HasQueryFilter(x => !x.IsDeleted);` in `ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries`.</li></ul> |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | `services.AddScoped<IQuestionImportBatchRepository, QuestionImportBatchRepository>();` `services.AddSingleton<ISpreadsheetReader, ClosedXmlSpreadsheetReader>();` `services.AddSingleton<ISpreadsheetWriter, ClosedXmlSpreadsheetWriter>();` |
| `api/Elmanhg.Infrastructure/Migrations/` | New migration `AddQuestionImportBatches` (`dotnet ef migrations add AddQuestionImportBatches -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`). Expected content: <ul><li>create table `QuestionImportBatches`</li><li>add nullable column `Questions.ImportBatchId` with an index and a Restrict FK</li><li>no drops</li></ul> The snapshot is updated as well. |
| `api/Elmanhg.Api/Resources/Messages.en.resx`, `Messages.ar.resx` | 13 error-code keys and 19 help keys (texts below). |
| `api/Elmanhg.Api/appsettings.example.json` | In `"Content"`, append `"QuestionImportMaxRows": 500, "QuestionImportMaxFileSizeInMb": 5`. |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | Add `["Content:QuestionImportMaxRows"] = "500"` and `["Content:QuestionImportMaxFileSizeInMb"] = "5"`. |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | Append `tenth => tenth.Should().EndWith("_AddQuestionImportBatches")` to `Migrate_FreshDatabase_LeavesNoPendingMigrations` (the accepted pattern per PROGRESS.md). |
| `api/Elmanhg.Tests/Integration/OpenApi/OpenApiEndpointTests.cs` | Add a test (see the Test plan). |
| `api/openapi/v1.json` | Regenerated by `dotnet build`. |
| `postman/elmanhg.postman_collection.json` | Add collection variable `importBatchId`. In folder `Questions`, append after "Resubmit question", in this order: <ol><li>"Download question import template": GET `{{baseUrl}}/api/question-imports/template`, test status 200.</li><li>"Preview question import": POST `{{baseUrl}}/api/question-imports/preview`, formdata `lessonId={{lessonId}}` and `file` (type file, src ""). Pre-request script `pm.collectionVariables.set("importBatchId", pm.variables.replaceIn("{{$guid}}"));`. Test status 200.</li><li>"Import questions": POST `{{baseUrl}}/api/question-imports`, formdata `lessonId={{lessonId}}`, `batchId={{importBatchId}}` and `file`. Test status 200.</li></ol> |
| `docs/audit-log.md` | <ul><li>Row `\| ImportQuestions \| Question.Import \| QuestionImportBatch \| command (the diff lists the new QuestionImportBatch and every created Question; a replay writes a row with no diff) \|`.</li><li>Add `QuestionImportBatch` to "Audited entities".</li></ul> |
| `docs/claude-design-prompt.md` | <ul><li>Line 151 (`#/admin/lesson/:id`): "links to a new question, to the lesson's questions and to the question import".</li><li>Line 152: append the screen spec from "Web screen spec" below, beginning "`#/admin/question/import/:lessonId` …".</li><li>The lesson-filter line mentions the import link too.</li></ul> |
| `docs/question-schemas.md` | In "Changing a schema", add the sentence: "The spreadsheet import (`docs/question-import.md`) builds the same shapes from columns; a schema change updates its columns, parser and template in the same change." |
| `web/src/shared/lib/http.ts` | In `parse<T>`, after the `!response.ok` check: `const contentType = response.headers.get('Content-Type') ?? ''; if (contentType !== '' && !contentType.includes('json')) { return (await response.blob()) as T; }` |
| `web/src/shared/lib/http.test.ts` | Add one test (see the Test plan). |
| `web/src/shared/i18n/en.json`, `ar.json` | 13 new keys under `errors` (texts below). |
| `web/src/features/questions/index.ts` | `export { QuestionImportPage } from './pages/QuestionImportPage';` |
| `web/src/features/questions/i18n/en.json`, `ar.json` | Add the `import` block and `list.importInLesson` (texts below). |
| `web/src/features/questions/pages/QuestionListPage.tsx` | Inside the `search.lessonId` bar, after the "new in lesson" button: `<Button asChild variant="ghost" size="sm"><Link to="/admin/question/import/$lessonId" params={{ lessonId: search.lessonId }}>{t('list.importInLesson')}</Link></Button>` |
| `web/src/features/questions/pages/QuestionListPage.test.tsx` | Add one test. |
| `web/src/features/content/pages/LessonEditorPage.tsx` | Between the two existing link buttons: `<Button asChild variant="secondary" size="sm"><Link to="/admin/question/import/$lessonId" params={{ lessonId: data.id }}>{t('lessonEditor.importQuestions')}</Link></Button>` |
| `web/src/features/content/pages/LessonEditorPage.test.tsx` | Add one test. |
| `web/src/features/content/i18n/en.json`, `ar.json` | `lessonEditor.importQuestions`: "Import questions" / "استيراد أسئلة". |
| `web/src/routeTree.gen.ts` | Regenerated by the router plugin (`npx vite build` or the dev server). |
| `web/src/shared/api/generated/**` | Regenerated: `npm --prefix web run gen:api`. |

## Files to create

### Domain
| # | Path | Type | Contract |
|---|------|------|----------|
| D1 | `api/Elmanhg.Domain/Questions/QuestionImportBatch.cs` | entity | `namespace Elmanhg.Domain.Questions; public class QuestionImportBatch : AuditEntity, IAuditedEntity`. <ul><li>Properties: `Guid LessonId`, `string FileHash = string.Empty`, `int QuestionCount`, all `{ get; private set; }`.</li><li>Constructor: `private QuestionImportBatch(Guid id, Guid? createdBy) : base(id, createdBy) { }`.</li><li>`public static QuestionImportBatch Create(Guid batchId, Guid lessonId, string fileHash, int questionCount, Guid createdBy)` returns `new QuestionImportBatch(batchId, createdBy) { LessonId = lessonId, FileHash = fileHash, QuestionCount = questionCount }`.</li><li>`public bool Matches(Guid lessonId, string fileHash) => LessonId == lessonId && FileHash == fileHash;`</li></ul> |
| D2 | `api/Elmanhg.Domain/Questions/IQuestionImportBatchRepository.cs` | repo interface | `public interface IQuestionImportBatchRepository : IRepository<QuestionImportBatch> { }` |
| D3 | `api/Elmanhg.Domain/Questions/Question.Import.cs` | partial | `public partial class Question { public static Question CreateImported(Lesson lesson, CurriculumUnit unit, QuestionType type, QuestionContent content, QuestionMetadata metadata, Guid importBatchId, Guid createdBy) { var question = Create(lesson, unit, type, content, metadata, createdBy); question.ImportBatchId = importBatchId; return question; } }` |

### Application: spreadsheet ports (`Elmanhg.Application.Shared.Spreadsheets`)
| # | Path | Type | Contract |
|---|------|------|----------|
| S1 | `api/Elmanhg.Application/Shared/Spreadsheets/ISpreadsheetReader.cs` | port | `SpreadsheetWorkbook Read(Stream content);` A file that is not a readable xlsx throws `BadRequestCoreException(ErrorCodes.SpreadsheetUnreadable)`. |
| S2 | `api/Elmanhg.Application/Shared/Spreadsheets/ISpreadsheetWriter.cs` | port | `byte[] Write(IReadOnlyList<SpreadsheetSheetDefinition> sheets);` |
| S3 | `api/Elmanhg.Application/Shared/Spreadsheets/SpreadsheetWorkbook.cs` | records | <ul><li>`public sealed record SpreadsheetWorkbook(IReadOnlyList<SpreadsheetSheet> Sheets);`</li><li>`public sealed record SpreadsheetSheet(string Name, IReadOnlyList<string> Headers, IReadOnlyList<SpreadsheetRow> Rows);`</li><li>`public sealed record SpreadsheetRow(int Number, IReadOnlyList<string> Cells);`</li></ul> `Cells[i]` aligns with `Headers[i]`. Values are trimmed; a blank cell is `""`. |
| S4 | `api/Elmanhg.Application/Shared/Spreadsheets/SpreadsheetSheetDefinition.cs` | records | <ul><li>`public sealed record SpreadsheetSheetDefinition(string Name, IReadOnlyList<SpreadsheetColumn> Columns, IReadOnlyList<IReadOnlyList<string>> Rows);`</li><li>`public sealed record SpreadsheetColumn(string Header, IReadOnlyList<string> AllowedValues, int Width);`</li></ul> |

### Application: shared import (`Elmanhg.Application.Questions.Shared.Import`)
| # | Path | Type | Contract |
|---|------|------|----------|
| I1 | `.../Questions/Shared/Import/QuestionImportFile.cs` | static | <ul><li>`public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";`</li><li>`public const string TemplateFileName = "elmanhg-question-import-template.xlsx";`</li><li>`public static readonly IReadOnlyList<string> Extensions = [".xlsx"];`</li><li>`public static async Task<byte[]> ReadAsync(IFormFile file, CancellationToken cancellationToken)`: `await using var stream = new MemoryStream(); await file.CopyToAsync(stream, cancellationToken).ConfigureAwait(false); return stream.ToArray();`</li><li>`public static string Hash(byte[] content) => Convert.ToHexStringLower(SHA256.HashData(content));`</li></ul> |
| I2 | `.../Questions/Shared/Import/QuestionImportFileValidation.cs` | static ext | `public static IRuleBuilderOptions<T, IFormFile?> ValidateQuestionImportFile<T>(this IRuleBuilder<T, IFormFile?> rule, ContentOptions options)`. It returns `rule.Cascade(CascadeMode.Stop).NotNull().WithErrorCode(ErrorCodes.QuestionImportFileRequired).ValidateRequired(ErrorCodes.QuestionImportFileRequired).ValidateAllowedExtensions(QuestionImportFile.Extensions, ErrorCodes.QuestionImportFileTypeInvalid).ValidateMaxFileSize(options.QuestionImportMaxFileSizeInMb, ErrorCodes.QuestionImportFileTooLarge);`. This mirrors `UploadLessonImageValidator`. |
| I3 | `.../Questions/Shared/Import/QuestionImportColumns.cs` | static | <ul><li>Constants (values as in the Column contract): `Stem`, `Explanation`, `Difficulty`, `MaxScore`, `Objective`, `Tags`, `Correct`, `PartialCredit`, `CorrectAnswer`, `UnifyLetterVariants`, `AnswerKind`, `Value`, `Tolerance`, `ToleranceMode`, `AcceptedAnswers`.</li><li>`private const int MaxLetterOptions = 26;` with comment `// Option ids are single lower-case letters, the ids the question editor generates.`</li><li>`public static string Option(string id) => $"option_{id}";`</li><li>`public static string Blank(string id) => $"blank_{id}";`</li><li>`public static IReadOnlyList<string> OptionIds(ContentOptions options)`: `a`… for `min(QuestionOptionsMaxCount, 26)`.</li><li>`public static IReadOnlyList<string> BlankIds(ContentOptions options)`: `"1"`…`"{QuestionBlanksMaxCount}"`, invariant culture.</li><li>`public static IReadOnlyList<string> For(QuestionType type, ContentOptions options)`: a switch expression producing the Column contract order.</li><li>`public static bool IsRequired(QuestionType type, string column)`: per the contract.</li><li>`public static IReadOnlyList<string> AllowedValues(string column)`: per the contract; `[]` otherwise.</li><li>`public static QuestionType? TypeForSheet(string sheetName)`: exact enum name, trimmed, `OrdinalIgnoreCase`, over `Enum.GetValues<QuestionType>()`; otherwise null. Never `Enum.TryParse`, because `"1"` must not match.</li></ul> |
| I4 | `.../Questions/Shared/Import/QuestionImportRowError.cs` | record | `public sealed record QuestionImportRowError(string Sheet, int Row, string? Column, string Code);` (admin-facing) |
| I5 | `.../Questions/Shared/Import/QuestionImportParse.cs` | records | <ul><li>`public sealed record QuestionImportRow(string Sheet, int Row, QuestionFields Fields);`</li><li>`public sealed record QuestionImportParse(IReadOnlyList<QuestionImportRow> Rows, IReadOnlyList<QuestionImportRowError> Errors, int TotalRows);`</li></ul> |
| I6 | `.../Questions/Shared/Import/QuestionImportCells.cs` | sealed class | `public sealed class QuestionImportCells(string sheet, SpreadsheetRow row, IReadOnlyDictionary<string, int> columns)`. <ul><li>`public List<QuestionImportRowError> Errors { get; } = [];`</li><li>`public string? Text(string column)`: null when the column is missing, the index is past `row.Cells`, or the value is whitespace; otherwise the trimmed value.</li><li>`public List<string> List(string column)`: `Text` split on `'\|'`, trimmed, empties dropped; `[]` when null.</li><li>`public bool? Boolean(string column)`: null when empty; `true/yes/1` → true and `false/no/0` → false (case-insensitive); otherwise `AddError(column, ErrorCodes.QuestionImportCellInvalid)` and null.</li><li>`public decimal? Decimal(string column)`: `decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, …)`, else a cell error.</li><li>`public int? Integer(string column)`: `int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, …)`, else a cell error.</li><li>`public TEnum? Enum<TEnum>(string column) where TEnum : struct, System.Enum`: a case-insensitive match against `System.Enum.GetNames<TEnum>()`, else a cell error.</li><li>`public void AddError(string column, string code) => Errors.Add(new QuestionImportRowError(sheet, row.Number, column, code));`</li></ul> |
| I7 | `.../Questions/Shared/Import/QuestionImportText.cs` | static | <ul><li>`private static readonly Regex InlineMath = new(@"\$([^$\n]+)\$", RegexOptions.NonBacktracking);` with comment `// $…$ in a cell is the admin's inline-math shorthand; storage keeps LaTeX in data-latex only (docs/rich-text.md).`</li><li>`public static string ToHtml(string? text)`: <ol><li>null or whitespace → `string.Empty`</li><li>otherwise replace `\r\n` with `\n` and split on `\n`</li><li>for each line with non-whitespace content: `<p>` + `ConvertLine(line.Trim())` + `</p>`</li><li>concatenate</li></ol></li><li>`private static string ConvertLine(string line)`: iterate `InlineMath.Matches(line)`. Append `WebUtility.HtmlEncode(gap)` for the text between matches and `<span data-type="inline-math" data-latex="{WebUtility.HtmlEncode(match.Groups[1].Value.Trim())}"></span>` for each match. Uses `StringBuilder`.</li></ul> |
| I8 | `.../Questions/Shared/Import/QuestionImportHeaders.cs` | static | `public static Dictionary<string, int> Map(SpreadsheetSheet sheet, QuestionType type, ContentOptions options, List<QuestionImportRowError> errors)`: <ol><li>`known = QuestionImportColumns.For(type, options)`, as a `HashSet` with `OrdinalIgnoreCase`</li><li>result dictionary with `StringComparer.OrdinalIgnoreCase`</li><li>for each `i`: `header = sheet.Headers[i].Trim()`; skip `""`</li><li>not known → `errors.Add(new(sheet.Name, 1, header, ErrorCodes.QuestionImportColumnUnknown))`</li><li>already mapped → the same with `QuestionImportColumnDuplicate`</li><li>else `result[header] = i`</li></ol> |
| I9 | `.../Questions/Shared/Import/QuestionImportBodies.cs` | static | Each method returns `(JsonElement Body, JsonElement GradingSpec)`, with the bodies from the Column contract, serialised through `private static JsonElement ToJson<T>(T value) => JsonSerializer.SerializeToElement(value, QuestionJson.SerializerOptions);`: <ul><li>`ReadChoice(QuestionImportCells cells, bool multiple, ContentOptions options)`</li><li>`ReadTrueFalse(QuestionImportCells cells)`</li><li>`ReadFill(QuestionImportCells cells, ContentOptions options)`</li><li>`ReadShort(QuestionImportCells cells)`</li></ul> Option text uses `QuestionImportText.ToHtml`. `correct` values are `ToLowerInvariant()`. |
| I10 | `.../Questions/Shared/Import/QuestionImportRowMapper.cs` | static | <ul><li>`private const int DefaultMaxScore = 1;` with comment `// Matches the question editor's default points.`</li><li>`public static QuestionFields Map(QuestionType type, QuestionImportCells cells, Lesson lesson, ContentOptions options)`: <ol><li>`var (body, spec) = type switch { Mcq => ReadChoice(cells, false, options), Multi => ReadChoice(cells, true, options), TrueFalse => ReadTrueFalse(cells), Fill => ReadFill(cells, options), Short => ReadShort(cells), _ => throw new InvalidOperationException("Unsupported question type.") };`</li><li>`objectiveId = ResolveObjective(cells, lesson)`</li><li>`return new QuestionFields(type, QuestionImportText.ToHtml(cells.Text(Stem)), body, spec, QuestionImportText.ToHtml(cells.Text(Explanation)), cells.Enum<QuestionDifficulty>(Difficulty), objectiveId, cells.List(Tags), cells.Integer(MaxScore) ?? DefaultMaxScore);`</li></ol></li><li>`private static Guid? ResolveObjective(QuestionImportCells cells, Lesson lesson)`: follows the Column contract.</li></ul> |
| I11 | `.../Questions/Shared/Import/QuestionImportParser.cs` | static | `public static async Task<QuestionImportParse> ParseAsync(byte[] content, Lesson lesson, ISpreadsheetReader reader, IValidator<QuestionFields> validator, ContentOptions options, CancellationToken cancellationToken)`: <ol><li>`using var stream = new MemoryStream(content, writable: false); var workbook = reader.Read(stream);`</li><li>`sheets` = the workbook sheets with a non-null `TypeForSheet`, in workbook order.</li><li>`total = sheets.Sum(x => x.Sheet.Rows.Count)`.</li><li>`total == 0` → `throw new BadRequestCoreException(ErrorCodes.QuestionImportEmpty)`.</li><li>`total > options.QuestionImportMaxRows` → `throw new BadRequestCoreException(ErrorCodes.QuestionImportTooManyRows, context: new Dictionary<string, object> { ["max"] = options.QuestionImportMaxRows })`.</li><li>For each sheet: `columns = QuestionImportHeaders.Map(...)`. For each row: build `cells`, then `fields = QuestionImportRowMapper.Map(...)`.<ul><li>If `cells.Errors.Count > 0`: add the errors and `continue`.</li><li>Otherwise `validation = await validator.ValidateAsync(fields, cancellationToken).ConfigureAwait(false)`. If invalid: add `validation.Errors.Select(x => x.ErrorCode).Distinct()` as `new QuestionImportRowError(sheet.Name, row.Number, null, code)` and `continue`.</li><li>Otherwise add `new QuestionImportRow(sheet.Name, row.Number, fields)`.</li></ul></li><li>Return `new QuestionImportParse(rows, errors, total)`.</li></ol> |
| I12 | `.../Questions/Shared/Import/QuestionImportHelpKeys.cs` | static | Constants: `Intro = "QUESTION_IMPORT_HELP_INTRO"`, `Stem`, `StemFill`, `Explanation`, `Difficulty`, `MaxScore`, `Objective`, `Tags`, `Option`, `CorrectMcq`, `CorrectMulti`, `PartialCredit`, `CorrectAnswer`, `Blank`, `UnifyLetterVariants`, `AnswerKind`, `Value`, `Tolerance`, `ToleranceMode`, `AcceptedAnswers`. Each value is `"QUESTION_IMPORT_HELP_" + SCREAMING_SNAKE of the name`, for example `CorrectMcq = "QUESTION_IMPORT_HELP_CORRECT_MCQ"`. |
| I13 | `.../Questions/Shared/Import/QuestionImportTemplate.cs` | static | <ul><li>`public const string InstructionsSheetName = "Instructions";`</li><li>Width constants `TypeColumnWidth = 24`, `InstructionsNarrowWidth = 20`, `InstructionsDescriptionWidth = 90`, with comment `// Excel column widths in characters; presentation only.`</li><li>`public static IReadOnlyList<SpreadsheetSheetDefinition> Build(ContentOptions options, ILocalizer localizer)`: <ol><li>`types = [Mcq, Multi, TrueFalse, Fill, Short]`</li><li>The Instructions definition: columns `sheet`, `column`, `required`, `description`, no allowed values. Rows: first `["", "", "", localizer.GetMessage(Intro)]`, then for each type and each `column` in `For(type, options)`: `[type.ToString(), column, IsRequired(type, column) ? "yes" : "", localizer.GetMessage(HelpKey(type, column))]`.</li><li>For each type: `new SpreadsheetSheetDefinition(type.ToString(), For(type, options).Select(c => new SpreadsheetColumn(c, AllowedValues(c), TypeColumnWidth)).ToList(), [])`.</li><li>Return `[instructions, ..typeSheets]`.</li></ol></li><li>`private static string HelpKey(QuestionType type, string column)`: switch. `stem` gives `StemFill` for Fill, else `Stem`. `option_*` gives `Option`. `correct` gives `CorrectMulti` for Multi, else `CorrectMcq`. `blank_*` gives `Blank`. Every other column maps to its constant.</li></ul> |

### Application: use cases
| # | Path | Type | Contract |
|---|------|------|----------|
| U1 | `api/Elmanhg.Application/Questions/GetQuestionImportTemplate/GetQuestionImportTemplateQuery.cs` | query | `public sealed record GetQuestionImportTemplateQuery : IRequest<QuestionImportTemplateResult>;` |
| U2 | `.../GetQuestionImportTemplate/QuestionImportTemplateResult.cs` | result | `public sealed record QuestionImportTemplateResult(byte[] Content, string FileName, string ContentType);` Admin-facing and not JSON: the controller returns `File(...)`. |
| U3 | `.../GetQuestionImportTemplate/GetQuestionImportTemplateHandler.cs` | handler | `public sealed class GetQuestionImportTemplateHandler(ISpreadsheetWriter spreadsheetWriter, ILocalizer localizer, IOptions<ContentOptions> contentOptions) : IRequestHandler<GetQuestionImportTemplateQuery, QuestionImportTemplateResult>`. `Handle`: <ol><li>`sheets = QuestionImportTemplate.Build(contentOptions.Value, localizer)`</li><li>`content = spreadsheetWriter.Write(sheets)`</li><li>`return Task.FromResult(new QuestionImportTemplateResult(content, QuestionImportFile.TemplateFileName, QuestionImportFile.ContentType));`</li></ol> Non-async method, no await. |
| U4 | `.../Questions/PreviewQuestionImport/PreviewQuestionImportQuery.cs` | query | `public sealed record PreviewQuestionImportQuery(Guid LessonId, IFormFile? File) : IRequest<QuestionImportPreviewResult>;` |
| U5 | `.../PreviewQuestionImport/PreviewQuestionImportValidator.cs` | validator | Constructor `(IOptions<ContentOptions> contentOptions)`. <ul><li>`RuleFor(x => x.LessonId).ValidateRequired(ErrorCodes.LessonIdRequired);`</li><li>`RuleFor(x => x.File).ValidateQuestionImportFile(contentOptions.Value);`</li></ul> |
| U6 | `.../PreviewQuestionImport/QuestionImportPreviewResult.cs` | results | <ul><li>`public sealed record QuestionImportPreviewResult(int TotalRows, int ValidRows, List<QuestionImportTypeCount> Types, List<QuestionImportRowError> Errors);`</li><li>`public sealed record QuestionImportTypeCount(QuestionType Type, int Count);`</li></ul> Admin-facing; no `LocalizedText` involved. |
| U7 | `.../PreviewQuestionImport/PreviewQuestionImportHandler.cs` | handler | `public sealed class PreviewQuestionImportHandler(ILessonRepository lessonRepository, ISpreadsheetReader spreadsheetReader, IValidator<QuestionFields> questionValidator, IOptions<ContentOptions> contentOptions) : IRequestHandler<PreviewQuestionImportQuery, QuestionImportPreviewResult>`. `Handle`: <ol><li>`lesson = await lessonRepository.GetWithObjectivesAsync(request.LessonId, asNoTracking: true, cancellationToken)`; null → `NotFoundCoreException(ErrorCodes.LessonNotFound)`</li><li>`content = await QuestionImportFile.ReadAsync(request.File!, cancellationToken)`</li><li>`parse = await QuestionImportParser.ParseAsync(content, lesson, spreadsheetReader, questionValidator, contentOptions.Value, cancellationToken)`</li><li>Return `new QuestionImportPreviewResult(parse.TotalRows, parse.Rows.Count, parse.Rows.GroupBy(x => x.Fields.Type.GetValueOrDefault()).OrderBy(x => x.Key).Select(x => new QuestionImportTypeCount(x.Key, x.Count())).ToList(), parse.Errors.ToList())`</li></ol> No save and no user guard, mirroring `GetQuestionHandler`. |
| U8 | `.../Questions/ImportQuestions/ImportQuestionsCommand.cs` | command | `public sealed record ImportQuestionsCommand(Guid LessonId, Guid BatchId, IFormFile? File) : IRequest<ImportQuestionsResult>, IAuditableCommand { public string AuditAction => "Question.Import"; public string AuditResourceType => "QuestionImportBatch"; public Guid? AuditResourceId => BatchId; }` |
| U9 | `.../ImportQuestions/ImportQuestionsValidator.cs` | validator | Constructor `(IOptions<ContentOptions> contentOptions)`. <ul><li>`RuleFor(x => x.LessonId).ValidateRequired(ErrorCodes.LessonIdRequired);`</li><li>`RuleFor(x => x.BatchId).ValidateRequired(ErrorCodes.QuestionImportBatchIdRequired);`</li><li>`RuleFor(x => x.File).ValidateQuestionImportFile(contentOptions.Value);`</li></ul> |
| U10 | `.../ImportQuestions/ImportQuestionsResult.cs` | result | `public sealed record ImportQuestionsResult(Guid BatchId, int CreatedCount, bool Replayed);` (admin-facing) |
| U11 | `.../ImportQuestions/ImportQuestionsHandler.cs` | handler | `public sealed class ImportQuestionsHandler(ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, IQuestionRepository questionRepository, IQuestionImportBatchRepository importBatchRepository, ISpreadsheetReader spreadsheetReader, IValidator<QuestionFields> questionValidator, IRichTextSanitizer richTextSanitizer, IOptions<ContentOptions> contentOptions, ICurrentUserService currentUserService) : IRequestHandler<ImportQuestionsCommand, ImportQuestionsResult>`. `Handle`: <ol><li>`UserId` null or default → `UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated)`.</li><li>`content = await QuestionImportFile.ReadAsync(request.File!, …)` and `hash = QuestionImportFile.Hash(content)`.</li><li>`existing = await importBatchRepository.GetByIdAsync(request.BatchId, cancellationToken, asNoTracking: true)`. If not null: `!existing.Matches(request.LessonId, hash)` → `ConflictCoreException(ErrorCodes.QuestionImportBatchConflict)`; else `return new ImportQuestionsResult(existing.Id, existing.QuestionCount, Replayed: true)`.</li><li>`lesson = GetWithObjectivesAsync(request.LessonId, asNoTracking: true, …)`; null → `NotFoundCoreException(ErrorCodes.LessonNotFound)`.</li><li>`unit = await unitRepository.GetByIdAsync(lesson.UnitId, cancellationToken, asNoTracking: true)`; null → `NotFoundCoreException(ErrorCodes.UnitNotFound)`.</li><li>`parse = await QuestionImportParser.ParseAsync(...)`.</li><li>`parse.Errors.Count > 0` → `throw new BusinessRuleViolationCoreException(ErrorCodes.QuestionImportHasErrors, context: new Dictionary<string, object> { ["count"] = parse.Errors.Count })`.</li><li>`userId = currentUserService.UserId.Value` and `batch = QuestionImportBatch.Create(request.BatchId, lesson.Id, hash, parse.Rows.Count, userId)`.</li><li>`questions = parse.Rows.Select(x => Question.CreateImported(lesson, unit, x.Fields.Type.GetValueOrDefault(), QuestionContentFactory.CreateContent(x.Fields, richTextSanitizer), QuestionContentFactory.CreateMetadata(x.Fields), batch.Id, userId)).ToList()`.</li><li>`await importBatchRepository.AddAsync(batch, …)`, `await questionRepository.AddRangeAsync(questions, …)`, then `await questionRepository.SaveChangesAsync(…)` **once**.</li><li>`return new ImportQuestionsResult(batch.Id, questions.Count, Replayed: false)`.</li></ol> |

### Infrastructure
| # | Path | Type | Contract |
|---|------|------|----------|
| F1 | `api/Elmanhg.Infrastructure/Questions/QuestionImportBatchRepository.cs` | repo | `public class QuestionImportBatchRepository(AppDbContext context) : Repository<QuestionImportBatch>(context), IQuestionImportBatchRepository { }` |
| F2 | `api/Elmanhg.Infrastructure/Spreadsheets/ClosedXmlSpreadsheetReader.cs` | adapter | `public sealed class ClosedXmlSpreadsheetReader : ISpreadsheetReader`. `Read(Stream content)`: <ul><li>`XLWorkbook workbook; try { workbook = new XLWorkbook(content); } catch (Exception exception) when (exception is InvalidDataException or FileFormatException or OpenXmlPackageException or ArgumentException or InvalidOperationException) { throw new BadRequestCoreException(ErrorCodes.SpreadsheetUnreadable, innerException: exception); }` If the "not a spreadsheet" tests show ClosedXML throwing another concrete type, add **that** type to the filter. Never catch bare `Exception`.</li><li>`using (workbook)` for each `worksheet` in `workbook.Worksheets`: <ul><li>`lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0` and `lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 0`</li><li>`headers` = row 1 cells `1..lastColumn`, via `CellText`</li><li>`rows` = for `r` in `2..lastRow`: cells `1..lastColumn` via `CellText`; skip the row when all are `""`; else `new SpreadsheetRow(r, cells)`</li><li>add `new SpreadsheetSheet(worksheet.Name, headers, rows)`</li></ul></li><li>`private static string CellText(IXLCell cell)` is a switch on `cell.DataType`: <ul><li>`Blank` → `""`</li><li>`Boolean` → `"true"`/`"false"`</li><li>`Number` → `cell.GetDouble().ToString(CultureInfo.InvariantCulture)`</li><li>`DateTime` → `cell.GetDateTime().ToString("O", CultureInfo.InvariantCulture)`</li><li>`TimeSpan` → `cell.GetTimeSpan().ToString("c", CultureInfo.InvariantCulture)`</li><li>`Error` → `""`</li><li>`_` → `cell.GetString()`</li></ul> then `.Trim()`.</li></ul> |
| F3 | `api/Elmanhg.Infrastructure/Spreadsheets/ClosedXmlSpreadsheetWriter.cs` | adapter | `public sealed class ClosedXmlSpreadsheetWriter : ISpreadsheetWriter`. `Write(sheets)`: <ul><li>`using var workbook = new XLWorkbook();`</li><li>For each definition, add `sheet = workbook.Worksheets.Add(definition.Name)` and set `sheet.RightToLeft = true`.</li><li>For each column `c` at index `i`, 1-based: `sheet.Cell(1, i).Value = c.Header`, `sheet.Cell(1, i).Style.Font.Bold = true`, `sheet.Column(i).Width = c.Width`. If `c.AllowedValues.Count > 0`: `sheet.Range(2, i, XLHelper.MaxRowNumber, i).CreateDataValidation().List($"\"{string.Join(',', c.AllowedValues)}\"", true);`</li><li>Rows are written from row 2 as text: `sheet.Cell(r, i).Value = value`.</li><li>`sheet.SheetView.FreezeRows(1)`.</li><li>`using var stream = new MemoryStream(); workbook.SaveAs(stream); return stream.ToArray();`</li><li>Never call `AdjustToContents` (it needs font measurement in containers).</li></ul> |

### API
| # | Path | Type | Contract |
|---|------|------|----------|
| A1 | `api/Elmanhg.Api/Controllers/Questions/QuestionImportsController.cs` | controller | `[ApiController] [Route("api/question-imports")] [Authorize] public class QuestionImportsController(IMediator mediator) : ControllerBase`. <ol><li>`[HttpGet("template", Name = "GetQuestionImportTemplate")] [Authorize(Policy = DefaultCodes.ContentManage)] [ProducesResponseType(typeof(Stream), StatusCodes.Status200OK, QuestionImportFile.ContentType)] public async Task<ActionResult> GetQuestionImportTemplate(CancellationToken cancellationToken)` returns `File(result.Content, result.ContentType, result.FileName)`.</li><li>`[HttpPost("preview", Name = "PreviewQuestionImport")] [Authorize(Policy = DefaultCodes.ContentManage)] [Consumes("multipart/form-data")] [ProducesResponseType<QuestionImportPreviewResult>(StatusCodes.Status200OK)] public async Task<ActionResult> PreviewQuestionImport([FromForm] Guid lessonId, IFormFile? file, CancellationToken cancellationToken)` returns `Ok(await mediator.Send(new PreviewQuestionImportQuery(lessonId, file), cancellationToken))`.</li><li>`[HttpPost(Name = "ImportQuestions")] [Authorize(Policy = DefaultCodes.ContentManage)] [Consumes("multipart/form-data")] [ProducesResponseType<ImportQuestionsResult>(StatusCodes.Status200OK)] public async Task<ActionResult> ImportQuestions([FromForm] Guid lessonId, [FromForm] Guid batchId, IFormFile? file, CancellationToken cancellationToken)` returns `Ok(...)`.</li></ol> After `dotnet build`, verify that `v1.json` describes the template response as `{"type":"string","format":"binary"}` under the xlsx media type. |

### Docs
| # | Path | Contract |
|---|------|----------|
| X1 | `docs/question-import.md` | Sections: <ul><li>**Flow**: download, check (dry run), confirm. Every row becomes a Pending question in the chosen lesson.</li><li>**Endpoints**: the three routes, policy `Content.Manage`, form fields.</li><li>**Workbook**: sheets, row 1 keys, unknown and duplicate headers, ignored sheets and blank rows, Excel row numbers.</li><li>**Columns**: the Column contract tables verbatim, with defaults.</li><li>**Text and math**: lines become paragraphs, `$…$` becomes inline math, no images or block math.</li><li>**Lists and tokens**: `\|`, booleans, enums.</li><li>**Validation**: the same rules and codes as `docs/question-schemas.md`, plus the import codes table (Error codes below).</li><li>**Limits**: `Content:QuestionImportMaxRows`, `Content:QuestionImportMaxFileSizeInMb`, `.xlsx` only.</li><li>**All-or-nothing commit**.</li><li>**Idempotency**: batch id, SHA-256, replay, 409, a failed commit stores nothing, the concurrent double-submit note.</li><li>**Audit**: `Question.Import`.</li></ul> |

### Web
| # | Path | Contract |
|---|------|----------|
| W1 | `web/src/routes/admin/question.import.$lessonId.tsx` | `createFileRoute('/admin/question/import/$lessonId')({ component: QuestionImportRoute })`, where `QuestionImportRoute` reads `Route.useParams().lessonId` and renders `<QuestionImportPage lessonId={lessonId} />`. This mirrors `question.new.$lessonId.tsx`. |
| W2 | `web/src/features/questions/schemas/questionImportSchema.ts` | <ul><li>`export const acceptedSpreadsheetTypes = '.xlsx';` with comment `// Mirrors QuestionImportFile on the API; the server is authoritative.`</li><li>`export const questionImportSchema = z.object({ file: z.instanceof(File, { error: 'validation.fileRequired' }).refine((file) => file.name.toLowerCase().endsWith('.xlsx'), { error: 'errors.QUESTION_IMPORT_FILE_TYPE_INVALID' }) });`</li><li>`export type QuestionImportValues = z.infer<typeof questionImportSchema>;`</li></ul> |
| W3 | `web/src/features/questions/api/downloadBlob.ts` | `export const questionImportTemplateFileName = 'elmanhg-question-import-template.xlsx';` and `export function downloadBlob(blob: Blob, fileName: string): void`: create an object URL, create an `a` element, set `href` and `download`, `click()`, then `URL.revokeObjectURL`. |
| W4 | `web/src/features/questions/hooks/useQuestionImportTemplate.ts` | `export function useQuestionImportTemplate(): { download: () => void; isPending: boolean }`: `useMutation({ mutationFn: () => getQuestionImportTemplate(), onSuccess: (blob) => downloadBlob(blob, questionImportTemplateFileName), onError: (error) => toast.error(t([common:errors.<code>, common:errors.UNHANDLED_EXCEPTION])) })`. |
| W5 | `web/src/features/questions/hooks/useQuestionImport.ts` | `export function useQuestionImport(lessonId: string)` returns `{ preview: QuestionImportPreviewResult \| undefined; imported: ImportQuestionsResult \| undefined; check: (file: File) => Promise<void>; confirm: () => Promise<void>; isConfirming: boolean; reset: () => void }`. <ul><li>Local UI state is `useState<{ file: File; batchId: string } \| null>`.</li><li>`check(file)`: `batchId = selection?.file === file ? selection.batchId : crypto.randomUUID()`; set the selection; `importMutation.reset()`; `await previewMutation.mutateAsync({ data: { lessonId, file } })`. It lets `ApiError` propagate so the Form maps it.</li><li>`confirm()`: requires a selection; `importMutation.mutateAsync({ data: { lessonId, batchId, file } })`.<ul><li>onSuccess: invalidate `getGetQuestionsQueryKey()` and `getGetLessonsQueryKey()`, then `toast(t('import.toast.imported'))`.</li><li>onError: `toast.error` with the common error by code.</li></ul></li><li>`preview` and `imported` read from the mutations' `data`, never copied into state.</li><li>`reset()` resets both mutations and the selection.</li></ul> |
| W6 | `web/src/features/questions/components/QuestionImportHeader.tsx` | `QuestionImportHeaderProps { lesson: LessonDetailResult }`. <ul><li>A breadcrumb `nav` (`aria-label={t('import.breadcrumb')}`) with the same classes as `QuestionEditorHeader`: link "Questions" to `/admin/questions`, the lesson name linking to `/admin/lesson/$lessonId`, then `aria-current="page"` `t('import.title')`.</li><li>`h1` `t('import.title')`.</li><li>`p.text-caption.text-text-muted` `t('import.intro')`.</li></ul> |
| W7 | `web/src/features/questions/components/QuestionImportTemplateCard.tsx` | No props. It uses `useQuestionImportTemplate`. A `section` card (`rounded-lg border border-border bg-surface p-4 shadow-1 flex flex-col gap-2`) holding: <ul><li>`h2` `t('import.template.title')`</li><li>a `p` `t('import.template.description')`</li><li>`<Button variant="secondary" onClick={download} disabled={isPending} aria-busy={isPending}>{t('import.template.download')}</Button>`</li></ul> |
| W8 | `web/src/features/questions/components/QuestionImportForm.tsx` | `QuestionImportFormProps { onCheck: (file: File) => Promise<void>; onFileChange: () => void }`. A section card with `h2` `t('import.form.title')` and the RHF+zod `Form`, mirroring `ImageInsertForm`: <ul><li>`useController` for `file`.</li><li>The label is `t('import.form.file')`, on an `input type="file" accept={acceptedSpreadsheetTypes}` with `aria-invalid`/`aria-describedby`. Its onChange calls `onChange(files?.[0])` and then `onFileChange()`.</li><li>Hint `p` `t('import.form.hint')`.</li><li>The error `p` renders `t([message, 'errors.UNHANDLED_EXCEPTION'], { ns: 'common' })`.</li><li>`FormRootError`, then `SubmitButton` `t('import.form.check')`.</li><li>`serverErrorFields = { QUESTION_IMPORT_FILE_REQUIRED, QUESTION_IMPORT_FILE_TYPE_INVALID, QUESTION_IMPORT_FILE_TOO_LARGE, SPREADSHEET_UNREADABLE, QUESTION_IMPORT_EMPTY, QUESTION_IMPORT_TOO_MANY_ROWS }`, each mapped to `'file'`.</li><li>onSubmit: `await onCheck(values.file)`.</li></ul> |
| W9 | `web/src/features/questions/components/QuestionImportErrorTable.tsx` | `QuestionImportErrorTableProps { errors: QuestionImportRowError[] }`. The table wrapper and `th` classes are the same as `QuestionTable`, with an sr-only caption `t('import.report.errorsCaption')`. <ul><li>Headers: `import.report.sheet`, `.row`, `.column` and `.problem`.</li><li>Cells: `sheet`; `formatNumber(row, lng, 'latin')`; `column ?? t('import.report.noColumn')`; and `t([`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION'], { column: column ?? '' })`.</li><li>The key is `` `${sheet}-${row}-${column ?? ''}-${code}` ``.</li></ul> |
| W10 | `web/src/features/questions/components/QuestionImportReport.tsx` | `QuestionImportReportProps { preview: QuestionImportPreviewResult; onConfirm: () => void; isConfirming: boolean }`. A `section` card with `aria-labelledby` pointing at its `h2` `t('import.report.title')`. <ul><li>`p` `t('import.report.summary', { total, valid, errors: errors.length })`.</li><li>`h3` `t('import.report.types')` and a `ul` with one `li` per type: `` `${t(`types.${type}`)}: ${formatNumber(count, lng, 'latin')}` ``.</li><li>If `errors.length > 0`: `<QuestionImportErrorTable>` and a `p` in `border-warning bg-warning-soft` (the notice classes from `QuestionEditorHeader`) with `t('import.report.fixAndRetry')`. No confirm button.</li><li>Else: a `p` `t('import.report.pendingNote')` and `<Button onClick={onConfirm} disabled={isConfirming} aria-busy={isConfirming}>{t('import.report.confirm', { count: validRows })}</Button>`.</li></ul> |
| W11 | `web/src/features/questions/components/QuestionImportSuccess.tsx` | `QuestionImportSuccessProps { lessonId: string; result: ImportQuestionsResult; onAnother: () => void }`. A `div role="status"` in `rounded-md border border-success bg-success-soft px-3.5 py-3` holding: <ul><li>`p` `t('import.success.title', { count: createdCount })`</li><li>`Button asChild variant="secondary" size="sm"` wrapping a `Link` to `/admin/questions` with `search={{ lessonId }}`: `t('import.success.viewQuestions')`</li><li>`Button variant="ghost" size="sm" onClick={onAnother}`: `t('import.success.another')`</li></ul> |
| W12 | `web/src/features/questions/pages/QuestionImportPage.tsx` | `QuestionImportPageProps { lessonId: string }`. <ul><li>`useGetLesson(lessonId)`.</li><li>Pending: `<ContentListSkeleton label={t('import.loading')} />`.</li><li>Error: `<ContentErrorState title={t('import.lessonErrorTitle')} error={error} onRetry={() => void refetch()} />`.</li><li>Success: `section.flex.flex-col.gap-4` containing `QuestionImportHeader`, `QuestionImportTemplateCard` and `QuestionImportForm key={formKey}` (`formKey` is a `useState` counter; `onFileChange={reset}`).</li><li>Then `imported ? <QuestionImportSuccess onAnother={() => { reset(); setFormKey((k) => k + 1); }} /> : preview ? <QuestionImportReport preview={preview} onConfirm={() => void confirm()} isConfirming={isConfirming} /> : null`.</li><li>Under 120 lines.</li></ul> |
| W13 | `web/src/features/questions/pages/QuestionImportPage.test.tsx` | Tests (see the Test plan). |
| W14 | `web/src/features/questions/schemas/questionImportSchema.test.ts` | Tests (see the Test plan). |

## Error codes
| Constant (Application `ErrorCodes`) | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `QuestionImportFileRequired` | `QUESTION_IMPORT_FILE_REQUIRED` | `ValidateQuestionImportFile` | validation | 422 |
| `QuestionImportFileTypeInvalid` | `QUESTION_IMPORT_FILE_TYPE_INVALID` | `ValidateQuestionImportFile` | validation | 422 |
| `QuestionImportFileTooLarge` | `QUESTION_IMPORT_FILE_TOO_LARGE` | `ValidateQuestionImportFile` | validation | 422 |
| `QuestionImportBatchIdRequired` | `QUESTION_IMPORT_BATCH_ID_REQUIRED` | `ImportQuestionsValidator` | validation | 422 |
| `QuestionImportEmpty` | `QUESTION_IMPORT_EMPTY` | `QuestionImportParser` | `BadRequestCoreException` | 400 |
| `QuestionImportTooManyRows` | `QUESTION_IMPORT_TOO_MANY_ROWS` | `QuestionImportParser` (context `max`) | `BadRequestCoreException` | 400 |
| `QuestionImportHasErrors` | `QUESTION_IMPORT_HAS_ERRORS` | `ImportQuestionsHandler` (context `count`) | `BusinessRuleViolationCoreException` | 400 |
| `QuestionImportBatchConflict` | `QUESTION_IMPORT_BATCH_CONFLICT` | `ImportQuestionsHandler` | `ConflictCoreException` | 409 |
| `QuestionImportColumnUnknown` | `QUESTION_IMPORT_COLUMN_UNKNOWN` | `QuestionImportHeaders` | report row error | 200 (preview) |
| `QuestionImportColumnDuplicate` | `QUESTION_IMPORT_COLUMN_DUPLICATE` | `QuestionImportHeaders` | report row error | 200 (preview) |
| `QuestionImportCellInvalid` | `QUESTION_IMPORT_CELL_INVALID` | `QuestionImportCells` | report row error | 200 (preview) |
| `QuestionImportObjectiveInvalid` | `QUESTION_IMPORT_OBJECTIVE_INVALID` | `QuestionImportRowMapper` | report row error | 200 (preview) |
| `SpreadsheetUnreadable` (group `// SPREADSHEETS`) | `SPREADSHEET_UNREADABLE` | `ClosedXmlSpreadsheetReader` | `BadRequestCoreException` | 400 |

Existing codes reused: `LESSON_ID_REQUIRED`, `LESSON_NOT_FOUND`, `UNIT_NOT_FOUND` and `USER_NOT_AUTHENTICATED`, plus every `QUESTION_*` code from `QuestionFieldsValidator`, which appears as report row errors.

resx (`Messages.en.resx` / `Messages.ar.resx`):

| Key | en | ar |
|---|---|---|
| QUESTION_IMPORT_FILE_REQUIRED | Choose a spreadsheet file. | اختر ملف جدول بيانات. |
| QUESTION_IMPORT_FILE_TYPE_INVALID | Upload an Excel file (.xlsx). | ارفع ملف اكسل (.xlsx). |
| QUESTION_IMPORT_FILE_TOO_LARGE | The file is too large. | حجم الملف كبير جدا. |
| QUESTION_IMPORT_BATCH_ID_REQUIRED | The import batch id is missing. | معرف دفعة الاستيراد مفقود. |
| QUESTION_IMPORT_EMPTY | The file has no questions. Fill at least one row under the column names. | الملف لا يحتوي على اسئلة. املأ صفا واحدا على الاقل تحت اسماء الاعمدة. |
| QUESTION_IMPORT_TOO_MANY_ROWS | The file has more than {max} questions. Split it into smaller files. | الملف يحتوي على اكثر من {max} سؤال. قسمه الى ملفات اصغر. |
| QUESTION_IMPORT_HAS_ERRORS | The file has {count} problems. Check it again and fix them before importing. | الملف يحتوي على {count} مشكلة. افحصه مرة اخرى وصحح المشاكل قبل الاستيراد. |
| QUESTION_IMPORT_BATCH_CONFLICT | This import was already used for a different file. Check the file again to start a new import. | تم استخدام عملية الاستيراد هذه لملف مختلف. افحص الملف مرة اخرى لبدء استيراد جديد. |
| QUESTION_IMPORT_COLUMN_UNKNOWN | Unknown column "{column}". Use the column names from the template. | العمود "{column}" غير معروف. استخدم اسماء الاعمدة من القالب. |
| QUESTION_IMPORT_COLUMN_DUPLICATE | The column "{column}" appears more than once. | العمود "{column}" مكرر. |
| QUESTION_IMPORT_CELL_INVALID | The value in column "{column}" is not valid. | القيمة في العمود "{column}" غير صحيحة. |
| QUESTION_IMPORT_OBJECTIVE_INVALID | The objective number is not one of this lesson's objectives. | رقم الهدف ليس من اهداف هذا الدرس. |
| SPREADSHEET_UNREADABLE | The file could not be read as an Excel spreadsheet. | تعذر قراءة الملف كجدول اكسل. |
| QUESTION_IMPORT_HELP_INTRO | One sheet per question type (Mcq, Multi, TrueFalse, Fill, Short). Keep the column names in row 1; each row below is one question and is imported as pending review. Separate list values with \|. Write math between $ signs, for example $F = ma$. Leave unused columns and sheets empty. | ورقة لكل نوع سؤال (Mcq و Multi و TrueFalse و Fill و Short). لا تغير اسماء الاعمدة في الصف الاول، وكل صف بعده سؤال واحد يستورد قيد المراجعة. افصل بين عناصر القائمة بالرمز \|. اكتب المعادلات بين علامتي $ مثل $F = ma$. اترك الاعمدة والاوراق غير المستخدمة فارغة. |
| QUESTION_IMPORT_HELP_STEM | Question text. Each line becomes a paragraph. | نص السؤال. كل سطر يصبح فقرة. |
| QUESTION_IMPORT_HELP_STEM_FILL | Question text with each blank written as [[1]], [[2]] and so on, matching the blank columns. | نص السؤال مع كتابة كل فراغ بالشكل [[1]] و [[2]] بما يطابق اعمدة الفراغات. |
| QUESTION_IMPORT_HELP_EXPLANATION | Optional explanation shown after answering. | شرح اختياري يظهر بعد الاجابة. |
| QUESTION_IMPORT_HELP_DIFFICULTY | easy, medium or hard. | easy او medium او hard (سهل او متوسط او صعب). |
| QUESTION_IMPORT_HELP_MAX_SCORE | Points, a whole number from 1. Empty means 1. | الدرجة، عدد صحيح من 1. الخانة الفارغة تعني 1. |
| QUESTION_IMPORT_HELP_OBJECTIVE | Optional objective number in the lesson (1 is the first objective). | رقم الهدف في الدرس، اختياري (1 هو الهدف الاول). |
| QUESTION_IMPORT_HELP_TAGS | Optional tags separated by \|. | وسوم اختيارية مفصولة بالرمز \|. |
| QUESTION_IMPORT_HELP_OPTION | Option text. Fill at least two options. | نص الاختيار. املأ اختيارين على الاقل. |
| QUESTION_IMPORT_HELP_CORRECT_MCQ | The letter of the correct option, for example b. | حرف الاختيار الصحيح، مثل b. |
| QUESTION_IMPORT_HELP_CORRECT_MULTI | The letters of every correct option separated by \|, for example a\|c. | حروف كل الاختيارات الصحيحة مفصولة بالرمز \|، مثل a\|c. |
| QUESTION_IMPORT_HELP_PARTIAL_CREDIT | true to give partial credit; false or empty for all or nothing. | true لمنح درجة جزئية، و false او فارغ للدرجة كاملة او صفر. |
| QUESTION_IMPORT_HELP_CORRECT_ANSWER | true or false. | true (صح) او false (خطأ). |
| QUESTION_IMPORT_HELP_BLANK | Accepted answers for the blank with the same number, separated by \|. | الاجابات المقبولة للفراغ الذي يحمل نفس الرقم، مفصولة بالرمز \|. |
| QUESTION_IMPORT_HELP_UNIFY_LETTER_VARIANTS | true or empty to treat أ إ آ ا, ة ه and ى ي as the same letter; false to keep them different. | true او فارغ لاعتبار أ إ آ ا و ة ه و ى ي حرفا واحدا، و false للتفريق بينها. |
| QUESTION_IMPORT_HELP_ANSWER_KIND | numeric for a number answer, text for a word answer. | numeric لاجابة رقمية، و text لاجابة نصية. |
| QUESTION_IMPORT_HELP_VALUE | Numeric answers: the correct number. | للاجابة الرقمية: الرقم الصحيح. |
| QUESTION_IMPORT_HELP_TOLERANCE | Numeric answers: allowed difference, 0 or more. Empty means 0. | للاجابة الرقمية: الفرق المسموح، 0 او اكثر. الخانة الفارغة تعني 0. |
| QUESTION_IMPORT_HELP_TOLERANCE_MODE | Numeric answers: absolute (plus or minus the tolerance) or percent (plus or minus that percent of the value). Empty means absolute. | للاجابة الرقمية: absolute (زائد او ناقص الفرق) او percent (زائد او ناقص نسبة مئوية من القيمة). الخانة الفارغة تعني absolute. |
| QUESTION_IMPORT_HELP_ACCEPTED_ANSWERS | Text answers: accepted answers separated by \|. | للاجابة النصية: الاجابات المقبولة مفصولة بالرمز \|. |

(`\|` in this table is a literal `|` in the resx value.)

Web `common` `errors` (`web/src/shared/i18n/en.json` / `ar.json`). The client has no `max`/`count` context, so only `{column}` is interpolated:

| Key | en | ar |
|---|---|---|
| QUESTION_IMPORT_FILE_REQUIRED | Choose a spreadsheet file. | اختر ملف جدول بيانات. |
| QUESTION_IMPORT_FILE_TYPE_INVALID | Upload an Excel file (.xlsx). | ارفع ملف إكسل (.xlsx). |
| QUESTION_IMPORT_FILE_TOO_LARGE | The file is too large. | حجم الملف كبير جدًا. |
| QUESTION_IMPORT_BATCH_ID_REQUIRED | The import batch id is missing. Check the file again. | معرّف دفعة الاستيراد مفقود. افحص الملف مرة أخرى. |
| QUESTION_IMPORT_EMPTY | The file has no questions. Fill at least one row under the column names. | الملف لا يحتوي على أسئلة. املأ صفًا واحدًا على الأقل تحت أسماء الأعمدة. |
| QUESTION_IMPORT_TOO_MANY_ROWS | The file has too many questions. Split it into smaller files. | الملف يحتوي على أسئلة كثيرة جدًا. قسّمه إلى ملفات أصغر. |
| QUESTION_IMPORT_HAS_ERRORS | The file has problems. Check it again and fix them before importing. | الملف به مشكلات. افحصه مرة أخرى وصحّحها قبل الاستيراد. |
| QUESTION_IMPORT_BATCH_CONFLICT | This import was already used for a different file. Check the file again. | عملية الاستيراد هذه استُخدمت لملف مختلف. افحص الملف مرة أخرى. |
| QUESTION_IMPORT_COLUMN_UNKNOWN | Unknown column "{column}". Use the column names from the template. | العمود "{column}" غير معروف. استخدم أسماء الأعمدة من القالب. |
| QUESTION_IMPORT_COLUMN_DUPLICATE | The column "{column}" appears more than once. | العمود "{column}" مكرر. |
| QUESTION_IMPORT_CELL_INVALID | The value in column "{column}" is not valid. | القيمة في العمود "{column}" غير صحيحة. |
| QUESTION_IMPORT_OBJECTIVE_INVALID | The objective number is not one of this lesson's objectives. | رقم الهدف ليس من أهداف هذا الدرس. |
| SPREADSHEET_UNREADABLE | The file could not be read as an Excel spreadsheet. | تعذّرت قراءة الملف كجدول إكسل. |

Web `questions` i18n. The en block is:

```json
"import": {
  "title": "Import questions", "breadcrumb": "Breadcrumb", "loading": "Loading lesson", "lessonErrorTitle": "Could not load the lesson",
  "intro": "Add many questions to this lesson at once from a spreadsheet. Every imported question starts as pending review.",
  "template": { "title": "1. Download the template", "description": "The template has one sheet per question type and an Instructions sheet that explains every column.", "download": "Download template" },
  "form": { "title": "2. Check your file", "file": "Spreadsheet file (.xlsx)", "hint": "Nothing is saved until you confirm the import.", "check": "Check file" },
  "report": { "title": "Check result", "summary": "{total, plural, one {# row} other {# rows}} found · {valid, plural, one {# ready} other {# ready}} · {errors, plural, one {# problem} other {# problems}}", "types": "Ready by type", "errorsCaption": "Problems to fix", "sheet": "Sheet", "row": "Row", "column": "Column", "problem": "Problem", "noColumn": "—", "fixAndRetry": "Fix these problems in your file, then check it again. Nothing was imported.", "pendingNote": "Imported questions stay pending review until a teacher approves them.", "confirm": "{count, plural, one {Import # question} other {Import # questions}}" },
  "success": { "title": "{count, plural, one {# question imported} other {# questions imported}} as pending review.", "viewQuestions": "View the lesson's questions", "another": "Import another file" },
  "toast": { "imported": "Questions imported." }
}
```

Also `list.importInLesson`: "Import questions into this lesson".

The ar block has the same keys:

```json
"import": {
  "title": "استيراد أسئلة", "breadcrumb": "مسار التنقل", "loading": "جارٍ تحميل الدرس", "lessonErrorTitle": "تعذّر تحميل الدرس",
  "intro": "أضف أسئلة كثيرة إلى هذا الدرس مرة واحدة من جدول بيانات. كل سؤال مستورد يبدأ قيد المراجعة.",
  "template": { "title": "١. نزّل القالب", "description": "يحتوي القالب على ورقة لكل نوع سؤال وورقة تعليمات تشرح كل عمود.", "download": "تنزيل القالب" },
  "form": { "title": "٢. افحص ملفك", "file": "ملف جدول البيانات (.xlsx)", "hint": "لن يُحفظ شيء حتى تؤكد الاستيراد.", "check": "افحص الملف" },
  "report": { "title": "نتيجة الفحص", "summary": "{total, plural, zero {لا صفوف} one {صف واحد} two {صفان} few {# صفوف} many {# صفًا} other {# صف}} · {valid, plural, zero {لا شيء جاهز} one {سؤال جاهز} two {سؤالان جاهزان} few {# أسئلة جاهزة} many {# سؤالًا جاهزًا} other {# سؤال جاهز}} · {errors, plural, zero {لا مشكلات} one {مشكلة واحدة} two {مشكلتان} few {# مشكلات} many {# مشكلة} other {# مشكلة}}", "types": "الجاهز حسب النوع", "errorsCaption": "مشكلات يجب تصحيحها", "sheet": "الورقة", "row": "الصف", "column": "العمود", "problem": "المشكلة", "noColumn": "—", "fixAndRetry": "صحّح هذه المشكلات في ملفك ثم افحصه مرة أخرى. لم يُستورد أي شيء.", "pendingNote": "تبقى الأسئلة المستوردة قيد المراجعة حتى يعتمدها معلّم.", "confirm": "{count, plural, one {استيراد سؤال واحد} two {استيراد سؤالين} few {استيراد # أسئلة} many {استيراد # سؤالًا} other {استيراد # سؤال}}" },
  "success": { "title": "{count, plural, one {تم استيراد سؤال واحد} two {تم استيراد سؤالين} few {تم استيراد # أسئلة} many {تم استيراد # سؤالًا} other {تم استيراد # سؤال}} قيد المراجعة.", "viewQuestions": "عرض أسئلة الدرس", "another": "استيراد ملف آخر" },
  "toast": { "imported": "تم استيراد الأسئلة." }
}
```

Also `list.importInLesson`: "استيراد أسئلة إلى هذا الدرس".

## Domain behaviour
- `QuestionImportBatch.Create`: no guards. The row-count and empty checks happen in the parser before creation. It is a pure factory, and `CreatedBy`/`CreationDate` come from `AuditEntity` and `CoreDbContext`. There is no mutating method, so no `UpdationDate`.
- `QuestionImportBatch.Matches(lessonId, fileHash)` is a pure predicate.
- `Question.CreateImported` delegates to `Create`. It inherits `EnsureObjectiveInLesson` → `BusinessRuleViolationCoreException(QUESTION_OBJECTIVE_NOT_IN_LESSON)`, Version 1, Pending status and the revision snapshot. It then sets `ImportBatchId`. `UpdationDate` is not set because this is creation.

## API surface
| Method | Route | Policy | Input | Response |
|---|---|---|---|---|
| GET | `/api/question-imports/template` | `DefaultCodes.ContentManage` | — | 200 `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` file `elmanhg-question-import-template.xlsx` |
| POST | `/api/question-imports/preview` | `DefaultCodes.ContentManage` | multipart: `lessonId` (Guid), `file` | 200 `QuestionImportPreviewResult` · 422 file/lesson id · 404 `LESSON_NOT_FOUND` · 400 `SPREADSHEET_UNREADABLE` / `QUESTION_IMPORT_EMPTY` / `QUESTION_IMPORT_TOO_MANY_ROWS` |
| POST | `/api/question-imports` | `DefaultCodes.ContentManage` | multipart: `lessonId`, `batchId` (Guid), `file` | 200 `ImportQuestionsResult` · 422 · 404 `LESSON_NOT_FOUND`/`UNIT_NOT_FOUND` · 400 `QUESTION_IMPORT_HAS_ERRORS` and the parse 400s · 409 `QUESTION_IMPORT_BATCH_CONFLICT` |

## Web screen spec (`#/admin/question/import/:lessonId`; copy into `docs/claude-design-prompt.md` §4)
The screen is a breadcrumb (Questions › lesson › Import questions) with the title "استيراد أسئلة" and an intro line, followed by:

1. **Template card**: explains one sheet per type plus an Instructions sheet, with a "تنزيل القالب" button.
2. **Check card**: a labelled `.xlsx` file input with the hint "nothing is saved until you confirm" and a "افحص الملف" button. File-level errors show inline under the input.
3. **Report**: rows found, ready and problems; ready counts per type.
   - When there are problems: a table of sheet, row, column and problem, and a warning notice to fix and re-check. No import button.
   - When clean: a pending-review note and "استيراد N سؤال".
4. **Success** (`role=status`, success tint): "تم استيراد N سؤال قيد المراجعة", a link to the lesson's questions, and "استيراد ملف آخر".

Choosing another file clears the report. Loading uses a skeleton; a lesson that fails to load shows the error state with retry.

## Test plan

### .NET: domain (`api/Elmanhg.Tests/Domain/Questions/`)
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 1 | `QuestionImportBatchTests` | `Create_ValidArguments_SetsBatchIdLessonHashAndCount` | `Id == batchId`, `LessonId`, `FileHash`, `QuestionCount`, `CreatedBy` |
| 2 | `QuestionImportBatchTests` | `Matches_SameLessonAndHash_ReturnsTrue` | true |
| 3 | `QuestionImportBatchTests` | `Matches_OtherHash_ReturnsFalse` | false |
| 4 | `QuestionImportBatchTests` | `Matches_OtherLesson_ReturnsFalse` | false |
| 5 | `QuestionImportTests` | `CreateImported_ValidArguments_IsPendingVersionOneWithBatchId` | `ValidationStatus == Pending`, `Version == 1`, `ImportBatchId == batchId`, `Revisions.Count == 1` (uses `QuestionBuilder` lesson/unit and `McqContent()`) |

### .NET: application shared import (`api/Elmanhg.Tests/Application/Features/Questions/Shared/Import/`)
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 6 | `QuestionImportTextTests` | `ToHtml_Null_ReturnsEmpty` | `""` |
| 7 | `QuestionImportTextTests` | `ToHtml_PlainText_EncodesAndWrapsInParagraph` | `"a < b & c"` → `"<p>a &lt; b &amp; c</p>"` |
| 8 | `QuestionImportTextTests` | `ToHtml_MultipleLines_OneParagraphPerNonBlankLine` | `"one\r\n\r\ntwo"` → `"<p>one</p><p>two</p>"` |
| 9 | `QuestionImportTextTests` | `ToHtml_DollarLatex_BecomesInlineMathSpan` | `"F = $m a$"` → `<p>F = <span data-type="inline-math" data-latex="m a"></span></p>` |
| 10 | `QuestionImportTextTests` | `ToHtml_LatexWithQuote_IsAttributeEncoded` | `$a"b$` → `data-latex="a&quot;b"` |
| 11 | `QuestionImportTextTests` | `ToHtml_FillPlaceholder_IsKept` | `"v = [[1]] m/s"` → `"<p>v = [[1]] m/s</p>"` |
| 12 | `QuestionImportCellsTests` | `Text_MissingColumn_ReturnsNull` | null |
| 13 | `QuestionImportCellsTests` | `Text_Whitespace_ReturnsNull` | null |
| 14 | `QuestionImportCellsTests` | `List_PipeSeparated_TrimsAndDropsEmpty` | `" a \| \| b "` → `["a","b"]` |
| 15 | `QuestionImportCellsTests` | `Boolean_TrueToken_ReturnsTrue` (Theory `true`,`TRUE`,`yes`,`1`) | true, no errors |
| 16 | `QuestionImportCellsTests` | `Boolean_FalseToken_ReturnsFalse` (Theory `false`,`No`,`0`) | false |
| 17 | `QuestionImportCellsTests` | `Boolean_Invalid_AddsCellInvalidWithColumnAndRow` | null; error `(sheet, row.Number, "partial_credit", QUESTION_IMPORT_CELL_INVALID)` |
| 18 | `QuestionImportCellsTests` | `Decimal_InvariantNumber_Parses` | `"9.8"` → 9.8m |
| 19 | `QuestionImportCellsTests` | `Decimal_Invalid_AddsCellInvalid` | `"9,8kg"` → null and an error |
| 20 | `QuestionImportCellsTests` | `Integer_Invalid_AddsCellInvalid` | `"1.5"` → null and an error |
| 21 | `QuestionImportCellsTests` | `Enum_NameCaseInsensitive_Parses` | `"HARD"` → `QuestionDifficulty.Hard` |
| 22 | `QuestionImportCellsTests` | `Enum_NumericText_AddsCellInvalid` | `"1"` → null and an error |
| 23 | `QuestionImportColumnsTests` | `For_Mcq_ListsStemOptionsCorrectThenCommonTail` | options cap 4: exact list `stem, option_a..option_d, correct, explanation, difficulty, max_score, objective, tags` |
| 24 | `QuestionImportColumnsTests` | `For_Fill_ListsBlankColumnsUpToCap` | blanks cap 3 → contains `blank_1..blank_3`, `unify_letter_variants` |
| 25 | `QuestionImportColumnsTests` | `TypeForSheet_CaseInsensitiveName_ReturnsType` | `" truefalse "` → TrueFalse |
| 26 | `QuestionImportColumnsTests` | `TypeForSheet_UnknownOrNumeric_ReturnsNull` (Theory `Instructions`, `1`) | null |
| 27 | `QuestionImportParserTests` | `ParseAsync_ValidMcqRow_ReturnsFieldsWithLetterOptionsAndCorrectId` | Body options `[a:"<p>3</p>", b:"<p>4</p>"]`, spec `correctOptionId == "b"`, `Stem == "<p>2 + 2 = ?</p>"`, no errors |
| 28 | `QuestionImportParserTests` | `ParseAsync_ValidMultiRow_ReturnsCorrectIdsAndPartialCredit` | `correctOptionIds == ["a","c"]`, `partialCredit == true` |
| 29 | `QuestionImportParserTests` | `ParseAsync_ValidTrueFalseRow_ReturnsCorrectAnswer` | spec `correctAnswer == false` |
| 30 | `QuestionImportParserTests` | `ParseAsync_ValidFillRow_ReturnsBlanksFromFilledColumns` | `blank_1="20\|٢٠"`, `blank_2` empty → body one blank `"1"`, accepted `["20","٢٠"]`, `unifyLetterVariants == true` |
| 31 | `QuestionImportParserTests` | `ParseAsync_NumericShortRowWithoutTolerance_DefaultsToZeroAbsolute` | `tolerance == 0`, `toleranceMode == "absolute"`, `value == 9.8` |
| 32 | `QuestionImportParserTests` | `ParseAsync_TextShortRow_ReturnsAcceptedAnswers` | `acceptedAnswers == ["ماء"]` |
| 33 | `QuestionImportParserTests` | `ParseAsync_BlankMaxScore_DefaultsToOne` | `MaxScore == 1` |
| 34 | `QuestionImportParserTests` | `ParseAsync_ObjectiveNumber_ResolvesLessonObjectiveId` | `objective = "1"` → `ObjectiveId == builder.ObjectiveId` |
| 35 | `QuestionImportParserTests` | `ParseAsync_ObjectiveOutOfRange_ReportsObjectiveInvalid` | `"2"` → error `(Mcq, 2, "objective", QUESTION_IMPORT_OBJECTIVE_INVALID)`, `Rows` empty |
| 36 | `QuestionImportParserTests` | `ParseAsync_InvalidCell_ReportsCellInvalidAndSkipsValidator` | difficulty `"very hard"` → exactly one error, `QUESTION_IMPORT_CELL_INVALID` on `difficulty` |
| 37 | `QuestionImportParserTests` | `ParseAsync_RuleViolation_ReportsValidatorCodeWithSheetAndRow` | mcq `correct = "z"` → `(Mcq, 2, null, QUESTION_CORRECT_OPTION_INVALID)` |
| 38 | `QuestionImportParserTests` | `ParseAsync_MissingStem_ReportsStemRequired` | error code `QUESTION_STEM_REQUIRED` |
| 39 | `QuestionImportParserTests` | `ParseAsync_UnknownHeader_ReportsColumnUnknownOnRowOne` | `(Mcq, 1, "stm", QUESTION_IMPORT_COLUMN_UNKNOWN)` |
| 40 | `QuestionImportParserTests` | `ParseAsync_DuplicateHeader_ReportsColumnDuplicate` | `(Mcq, 1, "stem", QUESTION_IMPORT_COLUMN_DUPLICATE)` |
| 41 | `QuestionImportParserTests` | `ParseAsync_UnrecognisedSheet_IsIgnored` | an `Instructions` sheet with rows plus one TrueFalse row → `TotalRows == 1` |
| 42 | `QuestionImportParserTests` | `ParseAsync_NoDataRows_ThrowsQuestionImportEmpty` | `BadRequestCoreException`, code `QUESTION_IMPORT_EMPTY` |
| 43 | `QuestionImportParserTests` | `ParseAsync_TooManyRows_ThrowsQuestionImportTooManyRows` | cap 1 and 2 rows → `BadRequestCoreException`, code, `Context["max"] == 1` |
| 44 | `QuestionImportParserTests` | `ParseAsync_SameCodeTwiceInRow_ReportedOnce` | two tags longer than the cap → one `QUESTION_TAG_TOO_LONG` for that row |
| 45 | `QuestionImportTemplateTests` | `Build_Default_HasInstructionsThenOneSheetPerType` | names `[Instructions, Mcq, Multi, TrueFalse, Fill, Short]` |
| 46 | `QuestionImportTemplateTests` | `Build_ShortSheet_HeadersMatchImportColumns` | headers equal `QuestionImportColumns.For(Short, options)` |
| 47 | `QuestionImportTemplateTests` | `Build_DifficultyColumn_OffersEasyMediumHard` | `AllowedValues == [easy, medium, hard]` |
| 48 | `QuestionImportTemplateTests` | `Build_Instructions_DescribesColumnsWithLocalizedHelp` | the localizer substitute returns `"L:" + key`; the row for (Fill, stem) has description `"L:QUESTION_IMPORT_HELP_STEM_FILL"` and required `"yes"`; the first row is `"L:QUESTION_IMPORT_HELP_INTRO"` |

Parser tests use `QuestionBuilder` (its lesson has one objective) and the real `QuestionFieldsValidator` with full `ContentOptions` (same initializer as `QuestionFieldsValidatorTests`, plus `QuestionImportMaxRows = 500`, `QuestionImportMaxFileSizeInMb = 5`). They use an NSubstitute `ISpreadsheetReader` that returns a hand-built `SpreadsheetWorkbook`, and pass `TestContext.Current.CancellationToken`.

### .NET: application use cases (`api/Elmanhg.Tests/Application/Features/Questions/<UseCase>/`)
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 49 | `PreviewQuestionImportValidatorTests` | `Validate_Valid_HasNoErrors` | valid (`FormFile` "q.xlsx", 10 bytes) |
| 50 | `PreviewQuestionImportValidatorTests` | `Validate_EmptyLessonId_HasLessonIdRequired` | code |
| 51 | `PreviewQuestionImportValidatorTests` | `Validate_NullFile_HasFileRequired` | `QUESTION_IMPORT_FILE_REQUIRED` |
| 52 | `PreviewQuestionImportValidatorTests` | `Validate_CsvFile_HasFileTypeInvalid` | `QUESTION_IMPORT_FILE_TYPE_INVALID` |
| 53 | `PreviewQuestionImportValidatorTests` | `Validate_OversizedFile_HasFileTooLarge` | length 5 MB + 1 → `QUESTION_IMPORT_FILE_TOO_LARGE` |
| 54 | `ImportQuestionsValidatorTests` | `Validate_Valid_HasNoErrors` | valid |
| 55 | `ImportQuestionsValidatorTests` | `Validate_EmptyBatchId_HasBatchIdRequired` | `QUESTION_IMPORT_BATCH_ID_REQUIRED` |
| 56 | `ImportQuestionsValidatorTests` | `Validate_NullFile_HasFileRequired` | code |
| 57 | `PreviewQuestionImportHandlerTests` | `Handle_UnknownLesson_ThrowsLessonNotFound` | `NotFoundCoreException` + `LESSON_NOT_FOUND`; reader `DidNotReceive().Read` |
| 58 | `PreviewQuestionImportHandlerTests` | `Handle_ValidWorkbook_ReturnsCountsPerTypeAndNoErrors` | one Mcq and two TrueFalse rows → `TotalRows 3`, `ValidRows 3`, `Types == [(Mcq,1),(TrueFalse,2)]`, `Errors` empty |
| 59 | `PreviewQuestionImportHandlerTests` | `Handle_RowErrors_ReturnsErrorsAndValidCount` | one valid and one invalid row → `ValidRows 1`, one error with sheet and row |
| 60 | `ImportQuestionsHandlerTests` | `Handle_NoUser_ThrowsUnauthorized` | `UnauthorizedCoreException` + `USER_NOT_AUTHENTICATED`; `SaveChangesAsync` `DidNotReceive` |
| 61 | `ImportQuestionsHandlerTests` | `Handle_ValidWorkbook_AddsBatchAndPendingQuestionsAndSavesOnce` | batch added with `Id == BatchId`, `QuestionCount 2`, `FileHash == SHA-256 hex of the file bytes`. `AddRangeAsync` gets 2 questions, all Pending, `ImportBatchId == BatchId`, `Stem == "clean:<p>…</p>"` (sanitizer substitute). Result `(BatchId, 2, false)`. `SaveChangesAsync` `Received(1)`. |
| 62 | `ImportQuestionsHandlerTests` | `Handle_ExistingBatchSameFile_ReturnsReplayWithoutSaving` | result `(BatchId, stored count, true)`; reader `DidNotReceive().Read`; `SaveChangesAsync` `DidNotReceive` |
| 63 | `ImportQuestionsHandlerTests` | `Handle_ExistingBatchDifferentFile_ThrowsBatchConflict` | `ConflictCoreException` + `QUESTION_IMPORT_BATCH_CONFLICT`; `DidNotReceive` save |
| 64 | `ImportQuestionsHandlerTests` | `Handle_ExistingBatchOtherLesson_ThrowsBatchConflict` | same |
| 65 | `ImportQuestionsHandlerTests` | `Handle_UnknownLesson_ThrowsLessonNotFound` | code; `DidNotReceive` save |
| 66 | `ImportQuestionsHandlerTests` | `Handle_UnknownUnit_ThrowsUnitNotFound` | code; `DidNotReceive` save |
| 67 | `ImportQuestionsHandlerTests` | `Handle_RowErrors_ThrowsHasErrorsWithoutSaving` | `BusinessRuleViolationCoreException` + `QUESTION_IMPORT_HAS_ERRORS`, `Context["count"] == 1`; `AddRangeAsync` and save `DidNotReceive` |
| 68 | `GetQuestionImportTemplateHandlerTests` | `Handle_Default_ReturnsWriterBytesWithXlsxNameAndType` | the writer substitute receives 6 definitions; the result `Content` is the writer's bytes, with `FileName` and `ContentType` constants |

### .NET: infrastructure (`api/Elmanhg.Tests/Infrastructure/Spreadsheets/`)
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 69 | `ClosedXmlSpreadsheetReaderTests` | `Read_Workbook_ReturnsSheetsHeadersAndRowsWithExcelRowNumbers` | built with `QuestionWorkbookBuilder`: sheet name, headers, rows numbered 2 and 3 |
| 70 | `ClosedXmlSpreadsheetReaderTests` | `Read_BlankRow_IsSkipped` | a blank row 3 between rows 2 and 4 → row numbers `[2, 4]` |
| 71 | `ClosedXmlSpreadsheetReaderTests` | `Read_NumberAndBooleanCells_UseInvariantText` | a numeric 9.8 cell → `"9.8"`, 2 → `"2"`, boolean TRUE → `"true"` |
| 72 | `ClosedXmlSpreadsheetReaderTests` | `Read_NotASpreadsheet_ThrowsSpreadsheetUnreadable` | bytes of `"hello"` → `BadRequestCoreException` + `SPREADSHEET_UNREADABLE` |
| 73 | `ClosedXmlSpreadsheetWriterTests` | `Write_Definitions_RoundTripsThroughReader` | the writer output read by `ClosedXmlSpreadsheetReader` returns the same sheet names, headers and row values |
| 74 | `ClosedXmlSpreadsheetWriterTests` | `Write_ColumnWithAllowedValues_AddsListValidation` | reopened with `XLWorkbook`: the sheet has a data validation whose value (`MinValue`) contains `easy,medium,hard` |
| 75 | `ClosedXmlSpreadsheetWriterTests` | `Write_Definition_IsRightToLeftWithFrozenHeader` | `RightToLeft == true`, `SheetView.SplitRow == 1` |

New builder `api/Elmanhg.Tests/Builders/QuestionWorkbookBuilder.cs`: `public QuestionWorkbookBuilder Sheet(string name, string[] headers, params string[][] rows)` and `public byte[] Build()`, which writes text cells with ClosedXML, headers in row 1 and rows from row 2. Tests that need numeric or boolean cells set them through ClosedXML directly.

### .NET: integration (`api/Elmanhg.Tests/Integration/Content/QuestionImportEndpointTests.cs`, `(ApiFactory factory)`, same helpers as `QuestionResubmitEndpointTests`)
| # | Test method | Asserts |
|---|-------------|---------|
| 76 | `GetTemplate_Admin_ReturnsWorkbookWithInstructionsAndTypeSheets` | 200; content type is the xlsx media type; reopened with `XLWorkbook`, the sheet names are the 6 expected and Mcq row 1 contains `stem`, `option_a`, `correct` |
| 77 | `GetTemplate_Teacher_Returns403` | 403 |
| 78 | `PostPreview_ValidWorkbook_ReturnsCountsAndSavesNothing` | 200: `totalRows 2`, `validRows 2`, errors empty; the database has 0 questions for the lesson |
| 79 | `PostPreview_InvalidRow_ReturnsRowError` | 200; `errors[0]` is `{ sheet:"Mcq", row:2, code:"QUESTION_CORRECT_OPTION_INVALID" }` |
| 80 | `PostPreview_UnknownLesson_Returns404LessonNotFound` | 404 + code |
| 81 | `PostPreview_CsvFile_Returns422FileTypeInvalid` | 422, code contains `QUESTION_IMPORT_FILE_TYPE_INVALID` |
| 82 | `PostPreview_CorruptFile_Returns400SpreadsheetUnreadable` | 400 + `SPREADSHEET_UNREADABLE` |
| 83 | `PostImport_ValidWorkbook_CreatesPendingQuestionsWithBatchAndAudits` | 200 `{ createdCount: 2, replayed: false }`. The database has 2 questions: Pending, Version 1, `ImportBatchId == batchId`, one revision each, and a `QuestionImportBatches` row with count 2. The `Question.Import` audit row with resource `batchId` is Success. |
| 84 | `PostImport_SameBatchSameFileTwice_ReplaysWithoutDuplicates` | second call 200 `replayed: true`, `createdCount: 2`; the database still has 2 questions |
| 85 | `PostImport_SameBatchDifferentFile_Returns409BatchConflict` | 409 + code; the question count is unchanged |
| 86 | `PostImport_RowErrors_Returns400HasErrorsAndCreatesNothing` | 400 + `QUESTION_IMPORT_HAS_ERRORS`; 0 questions and no batch row; the audit row is Failure |
| 87 | `PostImport_Anonymous_Returns401` | 401 |
| 88 | `PostImport_Teacher_Returns403` | 403; 0 questions |

Also `OpenApiEndpointTests.Get_OpenApiDocument_DescribesImportTemplateAsBinary`: `paths./api/question-imports/template.get.responses.200.content["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"].schema` has `type == "string"` and `format == "binary"`.

Existing test modified: `AppDbContextTests.Migrate_FreshDatabase_LeavesNoPendingMigrations` gets one appended element, `_AddQuestionImportBatches`.

### Web
| # | File | `it(...)` | Asserts |
|---|------|-----------|---------|
| 89 | `schemas/questionImportSchema.test.ts` | `accepts an .xlsx file` | success |
| 90 | same | `accepts an upper-case .XLSX extension` | success |
| 91 | same | `rejects a missing file with the required key` | issue message `validation.fileRequired` |
| 92 | same | `rejects a .csv file with the file type key` | issue message `errors.QUESTION_IMPORT_FILE_TYPE_INVALID` |
| 93 | `shared/lib/http.test.ts` | `returns a Blob for a non-JSON response` | the MSW response has body bytes and `Content-Type` xlsx → `http()` resolves to a `Blob` whose `size` equals the byte count |
| 94 | `pages/QuestionImportPage.test.tsx` | `shows a loading state then the lesson in the breadcrumb` | `role=status` loading label, then a link with the lesson name and the heading "Import questions" |
| 95 | same | `shows an error with retry when the lesson fails to load` | error title; after Retry and MSW reset, the heading appears |
| 96 | same | `shows the required error when checking without a file` | click "Check file" → "Choose a file." |
| 97 | same | `shows the check result with counts per type when the file is valid` | upload `q.xlsx` via `user.upload` → click Check → "3 rows found · 3 ready · 0 problems", "Multiple choice: 2", and an "Import 3 questions" button |
| 98 | same | `lists row problems and offers no import when the file has errors` | a row in table "Problems to fix" containing `Mcq`, `2`, and the text of `QUESTION_CORRECT_OPTION_INVALID`; no "Import" button; the fix notice is visible |
| 99 | same | `shows a file error inline when the server cannot read the file` | preview → 400 `{code:"SPREADSHEET_UNREADABLE"}` → "The file could not be read as an Excel spreadsheet." next to the file input |
| 100 | same | `imports after a clean check and shows the success panel` | click Import → `role=status` "3 questions imported as pending review." and a link "View the lesson's questions" to `/admin/questions?lessonId=…` |
| 101 | same | `reuses the batch id when the import is retried after a failure` | first import → 500 and a toast with the unhandled error; second click → success. Both captured form `batchId` values are equal and non-empty. (The retry-safety boundary *is* the behaviour.) |
| 102 | same | `clears the check result when another file is chosen` | after a report, uploading another file removes the "Check result" heading |
| 103 | same | `downloads the template when the button is clicked` | MSW GET template returns an xlsx Blob. With `URL.createObjectURL` set to `vi.fn(() => 'blob:t')`, `URL.revokeObjectURL` to `vi.fn()` and `HTMLAnchorElement.prototype.click` spied (a no-op that records `this.download`), clicking "Download template" makes `createObjectURL` receive a `Blob` and the clicked anchor's `download == 'elmanhg-question-import-template.xlsx'` |
| 104 | same | `renders right-to-left in Arabic` | `lng: 'ar'` → heading "استيراد أسئلة", `document.documentElement` `dir="rtl"` |
| 105 | same | `has no axe violations` | after the heading, `axe(container)` has no violations |
| 106 | `pages/QuestionListPage.test.tsx` | `links to the question import for the filtered lesson` | `/admin/questions?lessonId=l1` → link "Import questions into this lesson" with href `/admin/question/import/l1` |
| 107 | `content/pages/LessonEditorPage.test.tsx` | `links to the question import page` | link "Import questions" with href `/admin/question/import/l1` |

Page tests use `renderApp('/admin/question/import/<id>', { session: testSessions.admin })`, `getGetLessonMockHandler(lesson)` and the Orval MSW handlers `getPreviewQuestionImportMockHandler` and `getImportQuestionsMockHandler`, with bodies overridden. They create `File` objects as `new File([bytes], 'q.xlsx')`.

## Definition of done
- [ ] Every Files-to-create entry exists at its exact path with its stated type name and signature; no other new production files.
- [ ] `ClosedXML` 0.105.1 is pinned in `Directory.Packages.props`; `dotnet list package --vulnerable --include-transitive` is clean; SixLabors.Fonts resolves to 1.0.x.
- [ ] Row validation calls the existing `QuestionFieldsValidator` through `IValidator<QuestionFields>`; no per-type rule is re-implemented in the import code.
- [ ] Preview saves nothing and writes no audit row. Import is all-or-nothing, with exactly one `SaveChangesAsync`.
- [ ] Replay with the same batch id, lesson and file returns `replayed: true` and creates nothing. A reused id with a different file or lesson returns 409.
- [ ] Imported questions are Pending, Version 1, have one revision and carry `ImportBatchId`.
- [ ] Migration `AddQuestionImportBatches` contains no drops, the snapshot is updated, and `AppDbContextTests` lists it tenth.
- [ ] All 13 new error codes exist in both resx files and in both web `common` locale files; all 19 help keys (plus the intro key) exist in both resx files.
- [ ] `appsettings.example.json` and `ApiFactory` both carry the two new `Content` keys.
- [ ] Every new endpoint has `[Authorize(Policy = DefaultCodes.ContentManage)]`; `EndpointAuthorizationTests` passes.
- [ ] `api/openapi/v1.json` is regenerated; the template response is `string/binary`; the Orval client is regenerated with no drift (`npm --prefix web run gen:api && git diff --exit-code web/src/shared/api/generated`).
- [ ] Postman: the three requests are in `Questions`, in the stated order, and `importBatchId` is a collection variable.
- [ ] Web: the page has loading, error with retry, report with and without problems, and success states; every string is in en and ar; logical properties only; no raw colours or arbitrary values; the axe test passes.
- [ ] `docs/question-import.md` is created; `docs/audit-log.md`, `docs/claude-design-prompt.md` §4 and `docs/question-schemas.md` are updated as stated.
- [ ] All 107 enumerated tests plus the OpenAPI test exist with those names and pass. No existing test is edited except the `AppDbContextTests` list append.
- [ ] `dotnet build` has zero new warnings; `dotnet test api/ -c Release` is green with `appsettings.json` moved aside; `dotnet format --verify-no-changes` exits 0.
- [ ] `npm --prefix web run typecheck`, `lint`, `format:check` and `vitest run --coverage` pass, and `npx vite build` succeeds, which regenerates `routeTree.gen.ts`.
- [ ] The guard grep from dotnet-feature §9 prints nothing. There is no `catch (Exception)` without a type filter, and no `AdjustToContents`.
