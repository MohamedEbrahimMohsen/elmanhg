VERDICT: CHANGES_REQUESTED

# Review — [E3.S3] Bulk question import from spreadsheet (#66), round 1

## Blocking

### 1. The spreadsheet reader walks the whole used bounding box before any cap applies, so a 6 KB upload keeps a request thread busy with no bound
**Where:** `api/Elmanhg.Infrastructure/Spreadsheets/ClosedXmlSpreadsheetReader.cs:36-50`. The cap is only checked afterwards, at `api/Elmanhg.Application/Questions/Shared/Import/QuestionImportParser.cs:20-29`.
**Rule:** skill §10 "Uploads" (every row BLOCKING). Plan Decision 18 relies on the caps to bound the work ("the file cap also bounds zip-expansion memory"). The orchestrator asked for zip-bomb and row-count limits on an untrusted spreadsheet.
**Problem:** `ReadSheet` takes `LastColumnUsed()` × `LastRowUsed()` and calls `worksheet.Cell(r, c)` for every coordinate in that rectangle. It does this for every worksheet, including sheets the parser later ignores (`Instructions` or any other name). Only after every sheet is fully materialised does the parser apply `QuestionImportMaxRows` (500). The 5 MB file-size check limits the compressed bytes. It does not limit the sheet dimensions, and it does not limit the number of dense rows kept in memory.
**Failure:** I reproduced this against the built `Elmanhg.Infrastructure.dll` in a scratch console app. The workbook has sheet `Mcq`, `A1 = "stem"` and one cell `XFD1048576 = "x"`, and the file is 6,278 bytes. It passes the extension and size validators. `ClosedXmlSpreadsheetReader.Read` was still running after 60 s: about 1.7×10^10 cell visits, and the ×16,384-column case never finished. The same file with the far cell at `A1048576` returns 1 row in 443 ms. So the column × row product is the problem. The same file on a sheet named `Junk` has the same cost, even though that sheet is ignored. One admin request (or a stolen admin token) can pin a thread and CPU for hours. Preview and confirm are both affected.
**Fix:** bound the work inside the reader, before materialising anything:
- Iterate only used rows (`worksheet.RowsUsed()`), and only up to the header's column count (`worksheet.Row(1).LastCellUsed()`), not `LastColumnUsed()`.
- Skip sheets whose name is not a type sheet. For example, pass a sheet-name predicate, or let the parser choose the sheets first.
- Stop as soon as the non-blank data-row count exceeds `QuestionImportMaxRows`, and throw `QUESTION_IMPORT_TOO_MANY_ROWS`. For example, pass the cap into `ISpreadsheetReader.Read`.
- Add a reader test with a far cell (`XFD1048576`) that asserts it finishes quickly with a bounded result or `QUESTION_IMPORT_TOO_MANY_ROWS`.

## Non-blocking
- `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs:39`: the unique-violation translation is correctly scoped by table (`TableName == "QuestionImportBatches"`, SqlState 23505), so other unique violations still propagate unchanged. No test pins that negative case, though. A persistence test that forces a unique violation on another table and asserts it is not a `ConflictCoreException` would guard the scope.
- `api/Elmanhg.Application/Questions/Shared/Import/QuestionImportFileValidation.cs:13-19`: there is no content-type or magic-byte check. This follows plan Decision 19, and a non-zip file fails in `new XLWorkbook` with 400 `SPREADSHEET_UNREADABLE` (integration test 82). It is acceptable, but a cheap `PK\x03\x04` prefix check before ClosedXML would reject junk earlier.
- `api/Elmanhg.Infrastructure/Spreadsheets/ClosedXmlSpreadsheetReader.cs:53-66`: formula cells without a cached value read as `""`, and ClosedXML does not evaluate them. I probed this with `SUMPRODUCT(ROW(A1:A1000000)*1)`: 6 ms and an empty cell. This is safe, but a formula-only cell is silently treated as blank instead of reported. It is worth a sentence in `docs/question-import.md`.
- `api/Elmanhg.Application/Questions/Shared/Import/QuestionImportColumns.cs` (107 lines) and `api/Elmanhg.Tests/Integration/Content/QuestionImportEndpointTests.cs` (331 lines) are over the ~100-line guideline. The report discloses the first.
- `docs/claude-design-prompt.md` §4 opens with "Every route below exists in `prototype/app.js`", but `#/admin/question/import/:lessonId` does not. A short "(not in the prototype)" note would keep that statement true.
- `api/Elmanhg.Tests/Application/Features/Questions/GetQuestionImportTemplate/GetQuestionImportTemplateHandlerTests.cs:26`: `Content.Should().BeSameAs(bytes)` only echoes the substitute. The `HaveCount(6)` and name/type assertions carry the test, and `QuestionImportTemplateTests` covers the content.

## Verified
- CI parity. `api/Elmanhg.Api/appsettings.json` is absent, so this matches CI.
  - `dotnet build api/ -c Release`: 0 warnings, 0 errors. A forced `--no-incremental` rebuild of the Api shows only core-libraries CS86xx warnings.
  - OpenAPI drift: the rebuilt `api/openapi/v1.json` is byte-identical to the working-tree copy.
  - `dotnet test api/ -c Release --no-build`: 875/875 passed.
  - `dotnet list package --vulnerable --include-transitive`: clean for every project.
  - `dotnet ef migrations has-pending-model-changes`: "No changes have been made to the model since the last migration."
- Web:
  - `npm run gen:api` produces no drift: every generated file has the same md5 before and after.
  - `typecheck`, `lint` and `format:check` are clean.
  - `vitest`: 60 files and 344 tests passed.
  - `build` succeeds.
- **Idempotency:**
  - The pre-check comes before any parsing (`ImportQuestionsHandler.cs:29-33`). Same lesson and hash replays; a different lesson or hash returns 409 through `QuestionImportReplay.Resolve`.
  - The race path works. `AppDbContext.SaveChangesAsync` maps only a 23505 on `QuestionImportBatches` to `ConflictCoreException(QUESTION_IMPORT_BATCH_CONFLICT)`. `ImportQuestionsReplayBehaviour` is registered after `SubjectScopeBehaviour`, so it is innermost, after validation, and the audit stays outermost. It re-reads the batch without tracking and replays or returns 409. If no batch is stored, it rethrows the original.
  - The audit repository appends through raw SQL, so the failed save's still-tracked entities are not re-saved.
  - The deterministic integration test holds the row lock in an uncommitted transaction and asserts 200 `replayed: true` with 0 loser questions.
- **Transaction:** the batch, the questions and their revisions are written in one `SaveChangesAsync` (`ImportQuestionsHandler.cs:59-61`), with no explicit transaction and no domain events on `Question`.
  - The race test proves the loser's questions roll back.
  - `PostImport_RowErrors_Returns400HasErrorsAndCreatesNothing` proves that no batch and no questions are stored and that the audit records Failure.
- Huge strings are bounded by the reused `QuestionFieldsValidator`: stem and explanation 20,000, option 2,000, answer 200, tags 10×50. Text is HTML-encoded before the sanitizer runs, and the inline-math regex is `NonBacktracking` (§8.13).
- Every Files-to-create entry exists. The two extra files (`QuestionImportReplay.cs`, `ImportQuestionsReplayBehaviour.cs`) and the `Application/DependencyInjection.cs` registration are the disclosed addendum. The other deviations in the report (the `IRuleBuilderInitial` signature, `AllowedValues` derived from the enums, the OpenAPI `$ref` assertion, the `confirm` try/catch) match the code.
- The migration's `Up` creates `QuestionImportBatches` and a nullable `Questions.ImportBatchId` with an index and a Restrict FK; its drops are only in `Down`. `AppDbContextTests` gets the tenth entry. The soft-delete filter is added in the global method.
- Resx has 13 codes + 20 help keys (33) in both en and ar. The web `common.errors` files have 13 keys in both en and ar. `appsettings.example.json` and `ApiFactory` carry both Content keys.
- Postman matches the plan:
  - The `importBatchId` variable exists.
  - After "Resubmit question", in order: template GET, preview POST (formdata `lessonId`/`file`, with a pre-request guid script), and import POST (`lessonId`/`batchId`/`file`). All inherit auth.
- Docs: `docs/question-import.md` (Limits, All-or-nothing, and an Idempotency section that describes the race as replay/409), `audit-log.md`, `claude-design-prompt.md` §4 and `question-schemas.md` agree with the code. PRD §10.1 ("imported questions enter as Pending") matches.
- Every .NET test name and web `it(...)` title in the plan's Test plan exists. The guard-grep patterns do not appear in the new code, and `ClosedXmlSpreadsheetReader.cs:23` is the only `catch (Exception)`, which has a type filter.
- Web: every class is a design-system token or matches the existing `AuditLogTable` and `FormRootError` classes, with no literals and logical `text-start`.

## Test quality
- `ImportQuestionsHandlerTests` constrains the handler well:
  - It checks the real SHA-256 hash, the batch id propagation, Pending status and `ImportBatchId` on each question, and the sanitizer applied to the stem.
  - It checks `SaveChangesAsync` `Received(1)` on success and `DidNotReceive` on every throw.
  - The replay test also asserts the reader was not called.
- `ImportQuestionsReplayBehaviourTests` covers replay, 409 and rethrow-when-missing, and would fail if the behaviour swallowed the conflict.
- `QuestionImportParserTests` and `PreviewQuestionImportHandlerTests` use the real `QuestionFieldsValidator` and assert exact error tuples, so they constrain the implementation.
- `QuestionImportEndpointTests` includes a non-vacuous race test. The implementer reports it gets 409 when the behaviour is unregistered.
- `ClosedXmlSpreadsheetReaderTests` has no dimension, size or far-cell case. That is why Blocking #1 got through.
- `GetQuestionImportTemplateHandlerTests` is partly an echo test (see Non-blocking).
