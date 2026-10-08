VERDICT: CHANGES_REQUESTED

# Review — E21.S15 Last gaps: Arabic-Indic digit redaction, missing spreadsheet parts, per-context parity

## Blocking

### 1. The parts pre-check unescapes resolved part names, so it refuses valid packages whose part names are percent-encoded. ClosedXML reads those packages.
**Where:** `api/core-libraries/Core.Spreadsheets/SpreadsheetPackageParts.cs:34` (`ResolvePartName`), called at `:20` and `:28`
**Rule:** plan Goal and Definition of done ("the three valid variants read the sheet"; valid workbooks must keep reading). Review brief item (2): correct OPC resolution, and valid real-world xlsx files still pass. Plan D9 introduced the bug and the implementer copied it verbatim.
**Problem:** OPC part names are URIs, and the ZIP item name is the percent-encoded part name without the leading `/`. ECMA-376 Part 2 requires non-ASCII characters and spaces to be percent-encoded. System.IO.Packaging, which ClosedXML uses, looks entries up by that escaped form. `ResolvePartName` calls `Uri.UnescapeDataString` on `AbsolutePath`, so it looks for `xl/worksheets/my sheet.xml` while the archive holds `xl/worksheets/my%20sheet.xml`. `Require` then throws `InvalidOperationException` and the upload gets a 400.
**Failure:** I took a ClosedXML workbook and renamed `xl/worksheets/sheet1.xml` to `xl/worksheets/my%20sheet.xml`, with the workbook relationship target and the content-type override changed to match. `new XLWorkbook(stream)` reads it: `Mcq:2, Two:1`. `ClosedXmlSpreadsheetReader.Read` now throws `BadRequestCoreException` with inner `The package part 'xl/worksheets/my sheet.xml' is missing.` The same happens with `xl/worksheets/%D9%88.xml` (the Arabic letter waw): ClosedXML reads it, and the pre-check refuses it as missing under its unescaped name. Before this change both files returned 200. After it, both return 400 `SPREADSHEET_UNREADABLE`.
**Fix:** Remove `Uri.UnescapeDataString` and compare the escaped `AbsolutePath` (`new Uri(new Uri(PackageRoot, sourcePartName), target).AbsolutePath.TrimStart('/')`). I checked this: it gives `xl/worksheets/my%20sheet.xml` for both the target `my%20sheet.xml` and the target `my sheet.xml`, and `%D9%88.xml` for both the escaped and the literal Arabic target. That is the form System.IO.Packaging uses. Every existing row still resolves the same way (relative, `../`, upper-case). Add a `Read_ValidPackageVariant_ReadsWorkbook` row, for example `sheet-part-name-percent-encoded`, that renames the sheet entry and its relationship target to `xl/worksheets/my%20sheet.xml`. Update plan D9 to match.

## Non-blocking
- `api/Elmanhg.Tests/Application/Features/TrainingExports/Shared/TrainingDataScrubberTests.cs` is now 119 lines (it was 109). It was already over the ~100-line guide, and the plan placed the theory there.
- `api/core-libraries/Core.Spreadsheets/OpcPartReader.cs:43`: the sheet relationship id is taken as the first namespaced attribute whose local name is `id`. That holds for transitional and strict files and for every producer I checked (ClosedXML, openpyxl). An extension attribute named `id` in another namespace that comes before `r:id` would be picked up instead. Matching the two relationship namespaces explicitly would be stricter. No failing input exists today.
- `api/core-libraries/Core.Spreadsheets/OpcPartReader.cs:16`: `TargetMode` is compared case-insensitively, while OPC defines it as case-sensitive `External`. This only makes the check more lenient.

## Verified
- **Redaction escapes:** I decoded the six C# constants (`LogRedactor.cs:13-18`, written as C# u-escapes in regular strings) and built `PhonePattern` from them. I loaded `config.yaml` with PyYAML, took the 8 phone statements and undid the collector dollar-doubling and the OTTL backslash escaping. There is exactly 1 distinct pattern, and it equals the C# value code point for code point (219 chars). The non-ASCII code points are exactly U+0660/0661/0662/0665/0669 and U+06F0/06F1/06F2/06F5/06F9. The YAML is valid UTF-8 with LF endings (no CR), and it loads.
- **Engines over the plan's 66 strings** (parsed from the plan table). Each run used the email pattern, then the phone pattern twice:
  - .NET 10 NonBacktracking: new 66/66, old 66/66.
  - The real `LogRedactor.Redact` from the built dll: 66/66. The dll's `PhonePattern` equals the YAML copy.
  - Python `re`: new 66/66, old 66/66.
  - google-re2 (python:3.13-slim): new 66/66, old 66/66.
  - Go 1.24 `regexp`: new 66/66, old 66/66.
  - Real collector `otel-collector-contrib:0.161.0` (filelog, then the repo's `transform/redact` block, then the file exporter): 63/63 single-line inputs match.
  - `otelcol validate` on the real config passes.
- **No ASCII regression:** rows 1-33 (every existing `LogRedactorTests`/`LogRedactorBoundaryTests` string) give the same result under the old and new patterns in all engines. Rows 60-61 change on purpose (D4).
- **Spreadsheet pre-check:** it runs after the size loop and inside the same `try`/`using archive` (`SpreadsheetPackageGuard.cs:45-54`). `IsUnreadablePackage` is unchanged, and there is no `catch` of `NullReferenceException`. `XmlReader` uses `DtdProcessing.Prohibit`. A DOCTYPE with a SYSTEM `file:///` entity in `_rels/.rels` or `xl/workbook.xml` gives 400 with inner `XmlException` ("DTD is prohibited"), so the check is XXE-safe. Outcomes:
  - Bad URI target: 400 (`UriFormatException`).
  - Empty target: 400.
  - Sheet relationship marked External: 400.
  - Non-XML rels: 400.
  - Upper-case zip entry: found case-insensitively.
  - Read correctly: the ClosedXML workbook (seekable and non-seekable), an openpyxl-generated workbook (relative officeDocument target, absolute sheet targets), Excel-style relative targets, `./` and `../` targets, and an Arabic sheet name.
- The test rows match the plan: 10 missing-part rows, 3 valid variants, and the endpoint test.
- **Per-context parity:** I re-implemented `ContextStatements`. On the real config all four rows pass. On a copy with a 3/1/2/2 layout (one log-attributes phone statement moved to the log body), the file-wide count is still 8, yet the log-body and log-attributes rows fail.
- **Contract fidelity:**
  - The five new files exist with the planned members.
  - `OpcRelationship` is a sealed record.
  - The rename was applied, and its only caller was updated.
  - The 12 `CorruptPart` arms match the plan. Existing tests are untouched.
  - `TrainingDataScrubber.cs`, `CoreDbContext` and `Core.Notifications` are unchanged.
  - No csproj or package change, no migration, no new exception type.
  - `api/openapi` is unchanged. The Postman collection needs no change because no endpoint changed.
  - No issue-number reference appears in any added comment.
- **Build and tests:** `dotnet build -c Release` gives 0 errors. Focused run: 216/216. Full suite: 5742/5742.
- **Docs-sync:** these agree with the code:
  - `docs/observability.md` §12 items 2-3
  - `docs/question-import.md` (error row and Limits bullet)
  - `docs/security.md` (Spreadsheets bullet)
  - `docs/constitution.md`
  - `.claude/skills/dotnet-feature/SKILL.md` §5

  No stale copy of the old pattern or of the old method name remains.

## Test quality
- `LogRedactorArabicIndicDigitTests` constrains the implementation: every row would fail with the old pattern or a wrong boundary class.
- The new `LogRedactorCollectorParityTests` theory constrains it: it catches a per-context redistribution.
- `ClosedXmlSpreadsheetReaderPackagePartsTests` constrains it: the exact inner message proves the pre-check made the refusal. It has no percent-encoded valid row, which is why Blocking #1 went unnoticed.
- The new `TrainingDataScrubberTests` theory is a consistency guard. It asserts real output, so it is not vacuous.
- `PostPreview_WorkbookWithoutPackageRelationships_Returns400SpreadsheetUnreadable` constrains the change: before it, this input produced a 500.

## Status after rework r1

Finding 1 (percent-encoded part names) was fixed in rework r1 (see 02-implementation.md, "Rework r1"): `ResolvePartName` keeps the escaped form, four valid-variant rows cover escaped and literal space/Arabic targets, three of them fail with the old decoding, and CI on PR 340 is green. The non-blocking OpcPartReader items were also applied. No open blocking findings.
