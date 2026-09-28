VERDICT: APPROVED

# Review — [E3.S3] Bulk question import from spreadsheet (#66), round 2

## Blocking
None. Round-1 Blocking #1 is resolved.

## Non-blocking
- `api/Elmanhg.Infrastructure/Spreadsheets/ClosedXmlSpreadsheetReader.cs:51-56`: rows that are blank after trimming, and rows whose only cells lie past the header width, are dropped by the `Where` before `Take`. They do not count toward the row cap, so the reader walks all of them. The walk is still linear in the used rows. `RowsUsed` is sparse, and each row reads at most `headers.Count` (256 or fewer) columns, so there is no rows x columns rectangle. Measured through the built DLLs (fsi probe in the scratchpad, 200,000 rows, about 0.97 MB file):
  - Whitespace-only rows: the load takes 2.7 s and the reader takes 3.1 s in total.
  - Rows with a single cell at column 16000: the load takes 24.9 s and the reader takes 31.6 s in total, so the walk adds about 35 us per row.
  - Extrapolating to the 5 MB cap (about 1M rows), one request would take roughly 2-3 minutes. ClosedXML's own `new XLWorkbook` load dominates that time, not the walk.
  - The cost is bounded by the file-size cap and the endpoint is admin-only. If this matters later, a streaming (SAX) row-count pre-check or a lower size cap would bound the load itself.
- `ClosedXmlSpreadsheetReader.cs:54`: a data row whose only content is in columns without a header now reads as blank and is skipped. Before this change it was reported as missing fields. A sheet whose header row is blank still gets its column-missing errors from `QuestionImportHeaders.Map`, but if that is the only sheet the result is now `QUESTION_IMPORT_EMPTY`. Both paths still reject the file.
- The round-1 non-blocking items that were not addressed still apply. None of them gates.

## Verified
- The reader bounds its own work:
  - It filters sheets through `limits.IncludeSheet` (`:33`).
  - It visits only `RowsUsed()` (`:51`).
  - It reads the header up to `MaxColumns` and data rows up to the header width, through `Range(...).CellsUsed()` (`:50,54,68`).
  - It stops at `MaxDataRows + 1` rows across all sheets (`:31-41`, `:56`).
  - The parser passes the type-sheet predicate, 256 and `QuestionImportMaxRows` (`QuestionImportParser.cs:13,18`), and still throws `TOO_MANY_ROWS` at `:29-32`.
- The new `SpreadsheetReadLimits` record is in the existing `SpreadsheetWorkbook.cs:9`. The interface change is disclosed as a deviation. `docs/question-import.md:110` documents the new bound and agrees with the code.
- The three new reader tests exist with the names given in the report. They constrain the fix: they check the far-cell timeout, sheet exclusion, the header-width cap, and the stop one row past the cap across sheets.
- CI re-run:
  - `dotnet build api/ -c Release`: 0 warnings, 0 errors.
  - `api/openapi/v1.json` differs from HEAD only by the feature's own change. The rework does not change the contract.
  - `dotnet test api/ -c Release`: 878/878 passed.
  - `ef migrations has-pending-model-changes`: "No changes have been made to the model since the last migration."
  - `gen:api`: produced the same generated file set as before.
  - `typecheck`, `lint` and `format:check`: clean.
  - `vitest`: 60 files and 344 tests passed.
  - `build`: succeeded.
- Taken as given from the earlier stalled round-2 pass:
  - The far-cell case finishes in about 0.4 s on Mcq, 4 ms on an ignored sheet and 12 ms in the header row.
  - The 256-column cap is acceptable.

## Test quality
`ClosedXmlSpreadsheetReaderTests` now constrains the bound. The implementer reports that the far-cell test times out against the old dense walk, and the test's structure supports that claim. No test covers blank or header-width-only rows at volume. That is acceptable given the linear cost above.
