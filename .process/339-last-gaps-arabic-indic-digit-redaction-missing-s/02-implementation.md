# Implementation — E21.S15 Last gaps: Arabic-Indic digit redaction, missing spreadsheet parts, per-context parity

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/core-libraries/Core.Spreadsheets/SpreadsheetPackageParts.cs` | 41 | `internal static class SpreadsheetPackageParts.EnsurePresent(ZipArchive)`: the required-parts pre-check (D8), verbatim from the plan |
| `api/core-libraries/Core.Spreadsheets/OpcPartReader.cs` | 53 | Streaming `XmlReader` (`DtdProcessing.Prohibit`) for `.rels` parts and the workbook's `sheet` r:ids, verbatim from the plan |
| `api/core-libraries/Core.Spreadsheets/OpcRelationship.cs` | 3 | `internal sealed record OpcRelationship(string Id, string Type, string Target, bool IsExternal)` |
| `api/Elmanhg.Tests/Application/Features/Shared/Observability/LogRedactorArabicIndicDigitTests.cs` | 76 | Test plan rows 1–6 (verification rows 34–66) |
| `api/Elmanhg.Tests/Core/Spreadsheets/ClosedXmlSpreadsheetReaderPackagePartsTests.cs` | 52 | Test plan rows 9–10 |

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.Application/Shared/Observability/LogRedactor.cs` | Six private constants (`Digit`, `NotWordCharacter`, `Zero`, `One`, `Two`, `OperatorDigit`) written as C# `\uXXXX` escapes in regular strings; `PhonePattern` is now the plan's `$@"..."` const interpolation; comments updated per plan. The source file is still pure ASCII. `RegexOptions.NonBacktracking` unchanged. |
| `deploy/observability/otel-collector/config.yaml` | The 8 phone statements rewritten by the plan's script (digits built from `\u` escapes in Python, no RTL text pasted; asserted 8 old copies); one comment line added after the "$ doubled (collector config)" sentence. LF kept. Diff: 8 removed, 9 added lines. |
| `api/core-libraries/Core.Spreadsheets/SpreadsheetPackageGuard.cs` | `EnsureWithinUncompressedCap` → `EnsureReadablePackage`; `SpreadsheetPackageParts.EnsurePresent(archive);` after the size loop inside the `using var archive` scope; class comment replaced with the plan text. `IsUnreadablePackage` unchanged. |
| `api/core-libraries/Core.Spreadsheets/ClosedXmlSpreadsheetReader.cs` | Call renamed to `EnsureReadablePackage` (same arguments). |
| `api/Elmanhg.Tests/Core/Spreadsheets/SpreadsheetPackageCorruption.cs` | `using System.Text.RegularExpressions;` + the 12 new `CorruptPart` arms from the plan; existing arms unchanged. |
| `api/Elmanhg.Tests/Application/Features/Shared/Observability/LogRedactorCollectorParityTests.cs` | `using System.Globalization;`; new theory `CollectorConfig_PhoneStatements_RunTheLogRedactorPatternTwiceInContext` (4 rows) + helpers `ContextStatements`, `Indent`. The three existing tests and helpers are untouched. |
| `api/Elmanhg.Tests/Application/Features/TrainingExports/Shared/TrainingDataScrubberTests.cs` | New theory `ScrubText_MobileNumberInAnyDigitScript_Replaced` (4 rows). `TrainingDataScrubber.cs` unchanged. |
| `api/Elmanhg.Tests/Integration/Content/QuestionImportEndpointTests.cs` | `using Elmanhg.Tests.Core.Spreadsheets;` + fact `PostPreview_WorkbookWithoutPackageRelationships_Returns400SpreadsheetUnreadable`. |
| `docs/observability.md` | §12 items 2 and 3 per plan. |
| `docs/question-import.md` | `SPREADSHEET_UNREADABLE` error row; new Limits bullet after the unpacked-size bullet. |
| `docs/security.md` | "Spreadsheets" bullet: appended the missing-parts sentence. |
| `docs/constitution.md` | `Core.Spreadsheets (ClosedXML reader/writer with an unpacked-size and required-parts pre-check)`. |
| `.claude/skills/dotnet-feature/SKILL.md` | §5 `ISpreadsheetReader` parenthetical per plan. |

## Deviations
None of substance. Two wording details:

| Plan said | Reality | What I did |
|---|---|---|
| `LogRedactor` code block shows the constants with literal Arabic-Indic digits (and the comment reads ".NET only knows ٠ and RE2 only knows \x{0660}"), while D1 says the C# source uses C# string escapes. | The rendered markdown shows the characters; D1 is the instruction. | Wrote `٠`-style escapes in regular (non-verbatim) strings and `٠` in the comment, so the source stays ASCII. The compiled value equals the YAML copy (parity tests green; harness prints `yaml pattern == LogRedactor.PhonePattern: True`). |
| observability.md item 2: add "so an ASCII number written right against an Arabic-Indic digit is not masked". | The sentence already has "so GUIDs, hex ids and longer digit runs are left alone"; a second "so" read badly. | Wrote "...are left alone and an ASCII number written right against an Arabic-Indic digit is not masked, while Arabic letters...". Same meaning. |

## Build & test
- `dotnet build api/ -c Release`: `0 Error(s)`, 9 warnings, all already there (Core.Notifications / Core.OTP CS8618, Core.Validation CS8602). None come from touched files.
- `dotnet test api/ -c Release --no-build` (full suite, Docker/Testcontainers): `Test run summary: Passed! total: 5742 failed: 0 succeeded: 5742 skipped: 0`.
- Focused run before that (`-- --filter-class "*LogRedactor*" --filter-class "*TrainingDataScrubberTests" --filter-class "*ClosedXmlSpreadsheetReader*"`): 127/127 passed.
- OpenAPI diff (as CI does): `git status --porcelain api/openapi` → empty after the build.
- EF check (as CI does): `dotnet ef migrations has-pending-model-changes --project api/Elmanhg.Infrastructure --startup-project api/Elmanhg.Api --configuration Release --no-build` → `No changes have been made to the model since the last migration.`
- 4-engine regex re-check over the final strings. The patterns were read back from the final `config.yaml` (OTTL/`$$` unescaped), and the 66 cases with expected values were parsed from the plan's verification table:
  - .NET 10 `RegexOptions.NonBacktracking` (scratchpad console against the built `Elmanhg.Application.dll`): `yaml pattern == LogRedactor.PhonePattern: True`, `dotnet-nonbacktracking 66 / 66; LogRedactor.Redact 66 / 66`
  - Python `re` (host): `re 66 / 66`
  - `google-re2` (`python:3.13.15-slim` container): `re2 66 / 66`
  - Go `regexp` (`golang:1.24-alpine` container, `ReplaceAllString` with `${1}[redacted-phone]${2}`): `go 66 / 66`
  - I did not re-run the collector container itself (the plan says the planner did that and the implementer does not repeat it).
- Not run: `web/` and `ai/` (not touched).

## Hard gates
- C# constant and YAML copies are byte-identical: the existing parity test (8 occurrences) and the new per-context theory (exact pair in log body, log attributes, span, spanevent) pass.
- `CoreDbContext` and `Core.Notifications` are unchanged; no migration; no csproj or `Directory.Packages.props` change (no new NuGet); OpenAPI unchanged; Postman collection unchanged (no endpoint added or changed). No existing test was renamed or edited. No `#<number>` in any added code or comment line.
- Line endings: the `.cs` and `.md` files keep CRLF in the working tree (index is LF via autocrlf); `config.yaml` stays LF (`deploy/** eol=lf`).

## Notes for review
- `TrainingDataScrubberTests.cs` grows to 119 lines (it was 109, already over ~100). The plan puts the theory there.
- `QuestionImportEndpointTests.cs` was already 344 lines; one fact was added, as the plan says.
- The existing `Read_MalformedWorkbookPart_ThrowsConfiguredUnreadableCode` row `workbook-part-missing` still expects `InvalidOperationException`. That exception now comes from the pre-check, not ClosedXML. `content-types-not-xml` still reaches ClosedXML: the pre-check only requires the entry to exist and does not parse it, so the inner exception is still `XmlException`.
- The test strings with Arabic text were generated from `\u` escapes and checked to appear verbatim in the plan before they were written. The C# test files hold the literal characters, like the sibling `LogRedactorBoundaryTests`.
- Tooling note: the Bash tool turned `\\` into `\` inside heredocs. The one place this hit (`sheet1\\.xml` in `SpreadsheetPackageCorruption`) was fixed with the Edit tool. A broken first attempt at the parity-test edit was reverted with `git checkout` and redone with Edit. The final files were checked.

## Rework r1

| Finding | What I changed | File:line |
|---|---|---|
| Blocking 1 | `ResolvePartName` no longer calls `Uri.UnescapeDataString`; it returns the escaped `new Uri(new Uri(PackageRoot, sourcePartName), target).AbsolutePath.TrimStart('/')`, the form System.IO.Packaging uses for zip item names. Entry lookup stays `OrdinalIgnoreCase`, so `%d9%88` vs `%D9%88` hex case also matches. One comment states the invariant. | `api/core-libraries/Core.Spreadsheets/SpreadsheetPackageParts.cs:34-35` |
| Blocking 1 (tests) | Four new `Read_ValidPackageVariant_ReadsWorkbook` rows: `sheet-part-name-percent-encoded` (entry `xl/worksheets/my%20sheet.xml`, target `worksheets/my%20sheet.xml`), `sheet-part-name-percent-encoded-literal-target` (same entry, target `worksheets/my sheet.xml`), `sheet-part-name-arabic-percent-encoded` (entry `xl/worksheets/%D9%88.xml`, escaped target), `sheet-part-name-arabic-percent-encoded-literal-target` (same entry, literal U+0648 target). Each renames the entry, rewrites the workbook relationship target, and rewrites the content-type override `PartName`. | `api/Elmanhg.Tests/Core/Spreadsheets/ClosedXmlSpreadsheetReaderPackagePartsTests.cs:41-44`; `api/Elmanhg.Tests/Core/Spreadsheets/SpreadsheetPackageCorruption.cs:50-62` |
| Blocking 1 (helper) | `RewriteEntry` gains an optional `renameTo` parameter (no other callers), and the private `RenameSheetPart` composes the three rewrites. | `SpreadsheetPackageCorruption.cs:64,83` |
| Non-blocking (r:id) | `RelationshipId` matches `id` only in the transitional (`http://schemas.openxmlformats.org/officeDocument/2006/relationships`) or the strict (`http://purl.oclc.org/ooxml/officeDocument/relationships`) namespace, compared ordinally. The comment above the method was updated to match. | `api/core-libraries/Core.Spreadsheets/OpcPartReader.cs:8,25,45` |
| Non-blocking (TargetMode) | `TargetMode` is compared to `External` with `StringComparison.Ordinal`. | `OpcPartReader.cs:18` |

Plan D9 now has a short "superseded by rework r1" note in `01-plan.md`. D9 now reads: part names are resolved to the escaped absolute path and matched case-insensitively against zip item names. They are not unescaped.

**Resolution check.** The relative (`xl/workbook.xml`, `worksheets/...`), absolute (`/xl/...`) and upper-case (`/XL/Workbook.xml`) rows still pass. `./` and `../` contain no escapable characters, so `AbsolutePath` gives the same result for them as before. Space and Arabic targets resolve to the escaped entry whether they are written literally or escaped. All four new rows pass.

**The new tests fail on the old code.** I temporarily put `Uri.UnescapeDataString` back, and three of the new rows failed (the fourth was added after this check):
- `The package part 'xl/worksheets/my sheet.xml' is missing.` (both space rows)
- `The package part 'xl/worksheets/و.xml' is missing.` (escaped Arabic)

I restored the fix afterwards and confirmed `Unescape` no longer appears in the file.

**Build & test (r1):**
- `dotnet build -c Release` (api/): 0 Warning(s), 0 Error(s).
- `dotnet test --project Elmanhg.Tests -c Release --no-build -- --filter-class "*Spreadsheet*" --filter-class "*QuestionImport*"`: total 142, failed 0, succeeded 142, skipped 0.
- I did not re-run the full suite in r1.

**Docs-sync:** no doc describes unescaping or part-name encoding, so no doc changed.
