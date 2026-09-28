# Implementation — [E3.S3] Bulk question import from spreadsheet (#66), rework round 2

## Blocking findings addressed
| # | What I changed | File:line |
|---|---|---|
| 1 | The reader now bounds its own work before it builds anything. (a) Only sheets accepted by `limits.IncludeSheet` are opened; the parser accepts type sheets only, so `Instructions`, `Junk` and similar sheets are never read. (b) Only used rows are visited (`worksheet.RowsUsed()`); the old code visited every row index up to `LastRowUsed()`. (c) Only used cells are read, inside `Range(row, 1, row, maxColumns)`. The header is capped at `limits.MaxColumns`, and data rows are capped at the header's width. (d) Reading stops once `MaxDataRows + 1` non-blank data rows have been collected across all sheets (a lazy `Take` inside each sheet, then a break across sheets). The parser's existing `total > QuestionImportMaxRows` check then throws `QUESTION_IMPORT_TOO_MANY_ROWS` as before. | `api/Elmanhg.Infrastructure/Spreadsheets/ClosedXmlSpreadsheetReader.cs:16-75` |
| 1 | Reader contract: `ISpreadsheetReader.Read(Stream content, SpreadsheetReadLimits limits)`, plus the new `SpreadsheetReadLimits(Func<string, bool> IncludeSheet, int MaxColumns, int MaxDataRows)` record. | `api/Elmanhg.Application/Shared/Spreadsheets/ISpreadsheetReader.cs:5`, `.../SpreadsheetWorkbook.cs:9` |
| 1 | The parser passes the limits: type-sheet predicate, `MaxSheetColumns = 256` (a named invariant with a WHY comment) and `options.QuestionImportMaxRows`. | `api/Elmanhg.Application/Questions/Shared/Import/QuestionImportParser.cs:12-13,19` |
| 1 | Regression tests. `Read_FarCellAtLastColumnAndRow_FinishesFastAndReadsOnlyUpToTheHeader` uses `[Fact(Timeout = 10000)]`. It builds sheets `Mcq` and `Junk`, each with `stem`/`one` and a cell at `XFD1048576`; the file is under 20 KB. It asserts that only `Mcq` is returned, with headers `["stem"]` and one row `["one"]`. `Read_FarCellInTheHeaderRow_CapsTheColumnsRead` puts a far cell in the header row and a data row, sets the column cap to 3, and asserts `["stem"]`/`["one"]`. `Read_MoreRowsThanTheCap_StopsOneRowPastTheCapAcrossSheets` uses a cap of 2 across 3 sheets and asserts 3 rows from only the first 2 sheets. | `api/Elmanhg.Tests/Infrastructure/Spreadsheets/ClosedXmlSpreadsheetReaderTests.cs:69-127` |
| 1 | Mechanical follow-ups for the new signature. The existing reader and writer tests now pass limits. The NSubstitute mocks now use `Read(Arg.Any<Stream>(), Arg.Any<SpreadsheetReadLimits>())`. | `ClosedXmlSpreadsheetReaderTests.cs`, `ClosedXmlSpreadsheetWriterTests.cs:17`, `QuestionImportParserTests.cs:191`, `PreviewQuestionImportHandlerTests.cs:38,44,56`, `ImportQuestionsHandlerTests.cs:43,87,136` |
| 1 | Docs sync: the new bound (256 columns, non-type sheets are not read, reading stops one row past the cap) is added to Limits. | `docs/question-import.md:110` |

I checked that the guard really fails against the old algorithm. I temporarily restored the old dense row × column walk in the reader, and the far-cell test failed with "Test execution timed out after 10000 milliseconds". Then I restored the fix. With the fix, the whole reader test class runs in about 7 s, and most of that is test-host startup.

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| `ISpreadsheetReader.Read(Stream content)` (plan / round 1) | The work cannot be bounded unless the reader knows the sheet filter, the column cap and the row cap. | I added the `SpreadsheetReadLimits` parameter. The record lives in the existing `SpreadsheetWorkbook.cs` next to the other spreadsheet records, so no new file was created. |
| — | The column cap is a new invariant, `MaxSheetColumns = 256`. It is not in Options: it is not a product tunable, it only bounds a crafted sheet, and the largest real set is about 34 columns. | It is a named constant with a WHY comment in `QuestionImportParser`, and it is documented under Limits. Header cells past column 256 are now ignored rather than reported as `QUESTION_IMPORT_COLUMN_UNKNOWN`. |

## Build & test
Env: `ConnectionStrings__DbConnectionString="Host=localhost;Database=elmanhg_design;Username=design"`.
- `dotnet build api/ -c Release`: 0 Warning(s), 0 Error(s).
- OpenAPI drift: the rebuilt `api/openapi/v1.json` is byte-identical to the copy from before the build (`cmp`). The rework does not change the API contract, so Postman is unchanged.
- `dotnet test -c Release --no-build`: Passed. total 878, failed 0 (875 before, plus 3 new tests).
- `dotnet ef migrations has-pending-model-changes ... --configuration Release --no-build`: "No changes have been made to the model since the last migration."
- web:
  - `gen:api` gives the same generated set as round 1.
  - `typecheck`, `lint` and `format:check` are clean ("All matched files use Prettier code style!").
  - `test --run`: 60 files and 344 tests passed.
  - `build`: "built in 4.58s" (only the existing chunk-size notice).

## Notes for review
- Data-row `Cells` lists now end at the last used cell within the header width, instead of padding out to the sheet's last column. `QuestionImportCells` already treats `index >= Cells.Count` as empty, so this does not change behaviour.
- Remaining cost is roughly proportional to the used cells in included sheets, and ClosedXML's load is bounded by the 5 MB compressed cap. The rows × columns rectangle no longer matters.
