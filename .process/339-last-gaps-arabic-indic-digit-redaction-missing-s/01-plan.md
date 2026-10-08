# Plan — E21.S15 Last gaps: Arabic-Indic digit redaction, missing spreadsheet parts, per-context parity

## Goal
After this ships, an Egyptian mobile number written in Arabic-Indic (`٠١٠…`), Extended Arabic-Indic (`۰۱۰…`) or mixed digits is masked as `[redacted-phone]` by `LogRedactor` and by the OTel collector, with identical results in .NET NonBacktracking and RE2/Go. An `.xlsx` with missing core OPC parts (`[Content_Types].xml`, `_rels/.rels`, the workbook relationship, the workbook relationships part, a sheet relationship or a sheet part) gets 400 `SPREADSHEET_UNREADABLE` instead of a 500 from ClosedXML's `NullReferenceException`. The collector parity test checks the exact phone statement pair in each of the four contexts (log body, log attributes, span attributes, span-event attributes).

## Scope
**In:** `LogRedactor.PhonePattern` (digit and boundary classes) and its 8 copies in `deploy/observability/otel-collector/config.yaml`; a required-parts pre-check in `Core.Spreadsheets` run by `SpreadsheetPackageGuard` before ClosedXML; a per-context parity theory; tests; docs (`docs/observability.md` §12, `docs/question-import.md`, `docs/security.md`, `docs/constitution.md`, `.claude/skills/dotnet-feature/SKILL.md`).
**Out:** `TrainingDataScrubber` code (only a consistency test is added). `Core.Notifications`, `CoreDbContext`. No migration, no NuGet package, no new exception type, no API change. Catching `NullReferenceException` (never). The `filelog` alias deprecation warning seen in collector 0.161.0 (unrelated). `docs/backlog.json` (the orchestrator adds the E21.S15 entry after merge, like E21.S11–S14).
**Deferred:** none.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| D1 | Escape syntax for U+0660–U+0669 / U+06F0–U+06F9 that .NET and RE2/Go both accept. | None exists: .NET accepts `٠` and rejects `\x{0660}`; RE2/Go accept `\x{0660}` and reject `\u`. Use the **literal characters** in the regex value. The C# source writes them as C# string escapes in regular (non-verbatim) `const string`s (`"[0-9٠-٩۰-۹]"`), so the compiled `PhonePattern` value holds the literal characters; the YAML holds the same literal UTF-8 characters. The value is byte-identical in both copies, so `LogRedactorCollectorParityTests.Escaped()` (doubles `\` and `$`) keeps working unchanged and no normalisation is needed. | One string, four engines, no per-engine spelling. Verified (see "Engine verification"). |
| D2 | Which literal digits in the pattern accept other scripts? | Every one. `0`→`[0٠۰]`, `1`→`[1١۱]`, `2`→`[2٢۲]`, operator `[0125]`→`[0125٠١٢٥۰۱۲۵]`, `[0-9]`→`[0-9٠-٩۰-۹]`; the `00`/`20` prefixes too. Mixing scripts inside one number is allowed (each digit independently). | Story: "or mixed with ASCII digits"; `+٢٠`/`٠٠٢٠` prefixes must be masked. |
| D3 | Boundary classes. | `[^0-9A-Za-z_]` → `[^0-9٠-٩۰-۹A-Za-z_]` on both sides; `^`/`$` unchanged; `+` stays ASCII `\+`. | Story: boundaries treat these digits as digits. Arabic letters (U+0621–U+064A) are still outside the class, so `رقم٠١٠…` and `…٥٦٧٨رقم` still match (rows 50–54). |
| D4 | Behaviour change for ASCII numbers touching an Arabic-Indic digit (`٩01012345678`, `01012345678٩`). | No longer masked (rows 60–61). | Direct consequence of D3, which the story requires; same rule that keeps `x01012345678` and longer ASCII runs intact. |
| D5 | `TrainingDataScrubber` consistency. | Code unchanged. Its `Digit` already holds the same three ranges; add one test asserting every Arabic-Indic / mixed number `LogRedactor` now masks is also masked by the scrubber. | Story: "check it stays consistent; do not change its behaviour". |
| D6 | Where the part check lives. | New `internal static class SpreadsheetPackageParts` (`EnsurePresent(ZipArchive)`), called inside `SpreadsheetPackageGuard`'s existing `ZipArchive` pass, right after the size loop. `EnsureWithinUncompressedCap` is renamed `EnsureReadablePackage` (only caller: `ClosedXmlSpreadsheetReader.Read`). | One archive open; every entry read is already bounded by the uncompressed-size cap checked a few lines earlier; files stay < 100 lines. |
| D7 | Parsing technique. | `System.IO.Compression.ZipArchive` (already used) + streaming `System.Xml.XmlReader` (`DtdProcessing.Prohibit`). Only three parts are parsed: `_rels/.rels`, the workbook part (only its `sheet` elements at depth 2) and the workbook relationships part. No `System.IO.Packaging`, no OpenXML SDK load. | Instruction: no whole-workbook load, BCL only, bounded by the size guard. |
| D8 | What is checked (in order). | 1 `[Content_Types].xml` entry exists. 2 `_rels/.rels` exists. 3 it has an internal (`TargetMode` ≠ `External`) relationship of type officeDocument, transitional `http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument` or strict `http://purl.oclc.org/ooxml/officeDocument/relationships/officeDocument`. 4 its target part exists. 5 the workbook's relationships part (`<dir>/_rels/<file>.rels`) exists. 6 every `sheet` element of the workbook part has a relationship id attribute (local name `id`, non-empty namespace) that names an internal relationship in that part. 7 each such relationship's target part exists. | Probe results (ClosedXML 0.105.1): missing 1, 2, 3 or an external 3 → `NullReferenceException` (the 500s); missing 4 / sheet part → `InvalidOperationException`; missing 5 / sheet relationship → `ArgumentOutOfRangeException`; missing sheet `r:id` → ClosedXML silently reads an **empty** sheet. All now fail in the pre-check. ClosedXML accepts the strict officeDocument type, so the check does too (test row). |
| D9 | Part-name resolution. | `Uri.UnescapeDataString(new Uri(new Uri(PackageRoot, sourcePartName), target).AbsolutePath).TrimStart('/')` with `PackageRoot = http://package/`; source `""` for package relationships. Entry lookup is `ToLookup(x => x.FullName, StringComparer.OrdinalIgnoreCase)` (OPC part names are case-insensitive; duplicates do not throw). A bad target throws `UriFormatException` (a `FormatException`) → already unreadable. No special case for absolute `http://` targets. | ClosedXML writes `/xl/workbook.xml`; Excel writes `xl/workbook.xml` and `worksheets/sheet1.xml`; both resolve (test rows). Probe: upper-case target reads in ClosedXML, so lookup is case-insensitive. **Superseded by rework r1:** no unescape; the escaped `AbsolutePath` is compared (see 02-implementation.md, Rework r1). |
| D10 | Which exception the pre-check throws. | `InvalidOperationException` with a message naming the missing part. It is already in `IsUnreadablePackage`, so the existing `catch` in the guard wraps it as `BadRequestCoreException(unreadableErrorCode, innerException: exception)`. | No new exception type; the inner message tells the log which part is missing; the existing `workbook-part-missing` row (inner `InvalidOperationException`) keeps passing. |
| D11 | Per-context parity without a YAML library. | Text block extraction: the lines after the single `- context: <name>` line until the first non-blank line indented ≤ that line; statements are lines starting with `- `. For each of the four (context, target) pairs, the statements containing `[redacted-phone]` and starting with the target's prefix must equal exactly `[expected, expected]`. The existing file-wide test stays untouched. | No new NuGet; story asks for "the exact pair in each of the four contexts"; existing tests are never edited. |
| D12 | How the implementer writes the literal digits into YAML. | Run the script in "Collector config" below (escapes only, no copy-paste of RTL text); the parity tests prove the result. | Avoids bidi copy-paste errors. |
| D13 | Integration coverage of the 400. | One endpoint test: preview of a workbook without `_rels/.rels` → 400 `SPREADSHEET_UNREADABLE` (was 500). | Story names the HTTP outcome. |

## Engine verification (planner ran this; implementer does not repeat it)
Harness: each engine applies the shared email pattern, then the phone pattern twice (`${1}[redacted-phone]${2}`), over every `LogRedactorTests`/`LogRedactorBoundaryTests` string plus the new Arabic-Indic cases. Engines: .NET 10 `RegexOptions.NonBacktracking` (with the C# `const` composed exactly as in this plan — `PhonePattern == harness pattern` → `True`), Python 3.14 `re`, `google-re2` (Python 3.13 container), Go 1.24 `regexp` (container), and the real collector `otel/opentelemetry-collector-contrib:0.161.0` (`filelog` → `transform` with the YAML-escaped statements → `file` exporter) for every single-line input.
Result: old pattern 66/66 engines agree; new pattern 66/66 engines agree, 66/66 equal the expected value; collector 63/63 single-line inputs equal the .NET output.

| # | Input | Old pattern (all engines) | New pattern (all engines) | Collector 0.161.0 |
|---|---|---|---|---|
| 1 | `mail mona@example.com now` | `mail [redacted-email] now` | `mail [redacted-email] now` | same |
| 2 | `/audit-logs?actor=mona%40example.com` | `/audit-logs?actor=[redacted-email]` | `/audit-logs?actor=[redacted-email]` | same |
| 3 | `call 01012345678` | `call [redacted-phone]` | `call [redacted-phone]` | same |
| 4 | `call +201012345678` | `call [redacted-phone]` | `call [redacted-phone]` | same |
| 5 | `call +20 10 1234 5678` | `call [redacted-phone]` | `call [redacted-phone]` | same |
| 6 | `call 010-1234-5678` | `call [redacted-phone]` | `call [redacted-phone]` | same |
| 7 | `call 010 1234 5678` | `call [redacted-phone]` | `call [redacted-phone]` | same |
| 8 | `call +20-100-123-4567` | `call [redacted-phone]` | `call [redacted-phone]` | same |
| 9 | `call 0100 123 4567` | `call [redacted-phone]` | `call [redacted-phone]` | same |
| 10 | `on 2026-10-07` | `on 2026-10-07` | `on 2026-10-07` | same |
| 11 | `pin 12 34 56` | `pin 12 34 56` | `pin 12 34 56` | same |
| 12 | `user 3f2a1b10-1234-5678-9abc-def012345678` | unchanged | unchanged | same |
| 13 | `scores 10 12 15 20 11` | unchanged | unchanged | same |
| 14 | `session 3f2c9a7e-8b4d-4e6f-9a2b-5c7d8e9f0a3b code 482913` | unchanged | unchanged | same |
| 15 | `رقم010 1234 5678` | `رقم[redacted-phone]` | `رقم[redacted-phone]` | same |
| 16 | `رقم01012345678` | `رقم[redacted-phone]` | `رقم[redacted-phone]` | same |
| 17 | `010 1234 5678رقم` | `[redacted-phone]رقم` | `[redacted-phone]رقم` | same |
| 18 | `01012345678رقم` | `[redacted-phone]رقم` | `[redacted-phone]رقم` | same |
| 19 | `اتصل على 010 1234 5678 الآن` | `اتصل على [redacted-phone] الآن` | `اتصل على [redacted-phone] الآن` | same |
| 20 | `call 010 1234 5678 011 1234 5678` | `call [redacted-phone] [redacted-phone]` | same as old | same |
| 21 | `call 01012345678,01112345678` | `call [redacted-phone],[redacted-phone]` | same as old | same |
| 22 | `call 010-1234-5678,011-1234-5678,012-1234-5678` | three markers | three markers | same |
| 23 | `call 010 1234 5678\n` | `call [redacted-phone]\n` | `call [redacted-phone]\n` | n/a (multi-line) |
| 24 | `call 01012345678\r\n` | `call [redacted-phone]\r\n` | `call [redacted-phone]\r\n` | n/a (multi-line) |
| 25 | `tel:+201012345678;` | `tel:[redacted-phone];` | `tel:[redacted-phone];` | same |
| 26 | `00201012345678` | `[redacted-phone]` | `[redacted-phone]` | same |
| 27 | `call 0020 10 1234 5678` | `call [redacted-phone]` | `call [redacted-phone]` | same |
| 28 | `at 2026-10-07T08:00:00.123Z` | unchanged | unchanged | same |
| 29 | `order 1012345678901` | unchanged | unchanged | same |
| 30 | `order 101 2345 67890` | unchanged | unchanged | same |
| 31 | `user 3f2a1b10-1012-3456-9abc-def012345678` | unchanged | unchanged | same |
| 32 | `user 3f2a1b10-1012-3456-9abc-a01012345678` | unchanged | unchanged | same |
| 33 | `x01012345678` | unchanged | unchanged | same |
| 34 | `call ٠١٠١٢٣٤٥٦٧٨` | unchanged | `call [redacted-phone]` | same |
| 35 | `call ۰۱۰۱۲۳۴۵۶۷۸` | unchanged | `call [redacted-phone]` | same |
| 36 | `call ٠١٠ ١٢٣٤ ٥٦٧٨` | unchanged | `call [redacted-phone]` | same |
| 37 | `call ٠١٠-١٢٣٤-٥٦٧٨` | unchanged | `call [redacted-phone]` | same |
| 38 | `call ٠١٠٠ ١٢٣ ٤٥٦٧` | unchanged | `call [redacted-phone]` | same |
| 39 | `call +٢٠١٠١٢٣٤٥٦٧٨` | unchanged | `call [redacted-phone]` | same |
| 40 | `call +٢٠ ١٠ ١٢٣٤ ٥٦٧٨` | unchanged | `call [redacted-phone]` | same |
| 41 | `call +٢٠-١٠٠-١٢٣-٤٥٦٧` | unchanged | `call [redacted-phone]` | same |
| 42 | `call ٠٠٢٠١٠١٢٣٤٥٦٧٨` | unchanged | `call [redacted-phone]` | same |
| 43 | `call ٠٠٢٠ ١٠ ١٢٣٤ ٥٦٧٨` | unchanged | `call [redacted-phone]` | same |
| 44 | `tel:+٢٠١٠١٢٣٤٥٦٧٨;` | unchanged | `tel:[redacted-phone];` | same |
| 45 | `call ۰۱۲۴۴۵۵۶۶۷۷` | unchanged | `call [redacted-phone]` | same |
| 46 | `call 010١٢٣٤٥٦٧٨` | unchanged | `call [redacted-phone]` | same |
| 47 | `call ٠1٠1234٥678` | unchanged | `call [redacted-phone]` | same |
| 48 | `call ۰۱٠١٢٣٤۵۶۷۸` | unchanged | `call [redacted-phone]` | same |
| 49 | `call +20١٠١٢٣٤٥٦٧٨` | unchanged | `call [redacted-phone]` | same |
| 50 | `رقم٠١٠١٢٣٤٥٦٧٨` | unchanged | `رقم[redacted-phone]` | same |
| 51 | `٠١٠١٢٣٤٥٦٧٨رقم` | unchanged | `[redacted-phone]رقم` | same |
| 52 | `رقم٠١٠١٢٣٤٥٦٧٨رقم` | unchanged | `رقم[redacted-phone]رقم` | same |
| 53 | `اتصل على ٠١٠ ١٢٣٤ ٥٦٧٨ الآن` | unchanged | `اتصل على [redacted-phone] الآن` | same |
| 54 | `رقمي٠١١١٢٣٤٥٦٧٨، شكرا` | unchanged | `رقمي[redacted-phone]، شكرا` | same |
| 55 | `٠١٠١٢٣٤٥٦٧٨,٠١١١٢٣٤٥٦٧٨` | unchanged | `[redacted-phone],[redacted-phone]` | same |
| 56 | `٠١٠١٢٣٤٥٦٧٨ ٠١٢١٢٣٤٥٦٧٨` | unchanged | `[redacted-phone] [redacted-phone]` | same |
| 57 | `call ٠١٠١٢٣٤٥٦٧٨\n` | unchanged | `call [redacted-phone]\n` | n/a (multi-line) |
| 58 | `طلب ١٠١٢٣٤٥٦٧٨٩٠١` | unchanged | unchanged | same |
| 59 | `9٠١٠١٢٣٤٥٦٧٨` | unchanged | unchanged | same |
| 60 | `٩01012345678` | `٩[redacted-phone]` | unchanged (D4) | same |
| 61 | `01012345678٩` | `[redacted-phone]٩` | unchanged (D4) | same |
| 62 | `٠١٠١٢٣٤٥٦٧٨9` | unchanged | unchanged | same |
| 63 | `x٠١٠١٢٣٤٥٦٧٨` | unchanged | unchanged | same |
| 64 | `رمز ١٢ ٣٤ ٥٦` | unchanged | unchanged | same |
| 65 | `٢٠٢٦-١٠-٠٧` | unchanged | unchanged | same |
| 66 | `call ٠١٣١٢٣٤٥٦٧٨` | unchanged | unchanged | same |

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Application/Shared/Observability/LogRedactor.cs` | New private digit/boundary constants; `PhonePattern` rebuilt from them; comments updated. Exact text under "LogRedactor". |
| `deploy/observability/otel-collector/config.yaml` | The 8 phone statements get the new pattern (script below); the comment above `transform/redact` gains one sentence (text below). Nothing else changes. |
| `api/core-libraries/Core.Spreadsheets/SpreadsheetPackageGuard.cs` | Rename `EnsureWithinUncompressedCap` → `EnsureReadablePackage`; inside the `try`, after the `foreach` size loop and still inside the `using var archive` scope, add `SpreadsheetPackageParts.EnsurePresent(archive);`. Replace the class comment with: `// ClosedXML unpacks every part into memory before any row or column cap applies; the declared sizes are checked first, then the parts ClosedXML needs (SpreadsheetPackageParts). System.IO.Compression stops an entry at its declared size.` Nothing else changes. |
| `api/core-libraries/Core.Spreadsheets/ClosedXmlSpreadsheetReader.cs` | `SpreadsheetPackageGuard.EnsureWithinUncompressedCap(...)` → `SpreadsheetPackageGuard.EnsureReadablePackage(...)` (same arguments). |
| `api/Elmanhg.Tests/Core/Spreadsheets/SpreadsheetPackageCorruption.cs` | Add `using System.Text.RegularExpressions;` and the new `CorruptPart` arms listed under "Test helpers". Existing arms unchanged. |
| `api/Elmanhg.Tests/Application/Features/Shared/Observability/LogRedactorCollectorParityTests.cs` | Add one theory + two private helpers (below). Existing three tests and helpers unchanged. |
| `api/Elmanhg.Tests/Application/Features/TrainingExports/Shared/TrainingDataScrubberTests.cs` | Add one theory (below). |
| `api/Elmanhg.Tests/Integration/Content/QuestionImportEndpointTests.cs` | Add one fact (below) and `using Elmanhg.Tests.Core.Spreadsheets;`. |
| `docs/observability.md` | §12 items 2 and 3 (text under "Docs"). |
| `docs/question-import.md` | Error table row `SPREADSHEET_UNREADABLE`; new Limits bullet. |
| `docs/security.md` | "Spreadsheets" bullet (line ~140). |
| `docs/constitution.md` | `Core.Spreadsheets (ClosedXML reader/writer with an unpacked-size pre-check)` → `Core.Spreadsheets (ClosedXML reader/writer with an unpacked-size and required-parts pre-check)`. |
| `.claude/skills/dotnet-feature/SKILL.md` | §5 line: `(SpreadsheetOptions.MaxUncompressedSizeInMb caps the unpacked size)` → `(SpreadsheetOptions.MaxUncompressedSizeInMb caps the unpacked size; a package without its content types, package relationships, workbook part or a declared sheet part is refused with UnreadableErrorCode before ClosedXML opens it)`. |

## Files to create
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `api/core-libraries/Core.Spreadsheets/SpreadsheetPackageParts.cs` | `internal static class SpreadsheetPackageParts` (ns `Core.Spreadsheets`) | `public static void EnsurePresent(ZipArchive archive)`; exact body below. New — no Morabh equivalent. |
| 2 | `api/core-libraries/Core.Spreadsheets/OpcPartReader.cs` | `internal static class OpcPartReader` | `public static IReadOnlyList<OpcRelationship> ReadRelationships(ZipArchiveEntry entry)`; `public static IReadOnlyList<string?> ReadSheetRelationshipIds(ZipArchiveEntry entry)`; exact body below. New — no Morabh equivalent. |
| 3 | `api/core-libraries/Core.Spreadsheets/OpcRelationship.cs` | `internal sealed record OpcRelationship(string Id, string Type, string Target, bool IsExternal);` | File holds only `namespace Core.Spreadsheets;` + this record. |
| 4 | `api/Elmanhg.Tests/Application/Features/Shared/Observability/LogRedactorArabicIndicDigitTests.cs` | `public sealed class LogRedactorArabicIndicDigitTests` | Tests below. |
| 5 | `api/Elmanhg.Tests/Core/Spreadsheets/ClosedXmlSpreadsheetReaderPackagePartsTests.cs` | `public sealed class ClosedXmlSpreadsheetReaderPackagePartsTests` | Tests below. Same fields as `ClosedXmlSpreadsheetReaderMalformedTests`: `AllSheets = new(_ => true, 256, 500)` and `_reader` with `UnreadableErrorCode = "PROBE_UNREADABLE"`. |

### LogRedactor (full file after the change)
```csharp
using Core.Logging;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Elmanhg.Application.Shared.Observability;

public static class LogRedactor
{
    public const string EmailReplacement = "[redacted-email]";
    public const string PhoneReplacement = "[redacted-phone]";

    // ASCII, Arabic-Indic and Extended Arabic-Indic digits. Regular strings, not verbatim: the pattern holds the characters themselves, because .NET only knows ٠ and RE2 only knows \x{0660}.
    private const string Digit = "[0-9٠-٩۰-۹]";
    private const string NotWordCharacter = "[^0-9٠-٩۰-۹A-Za-z_]";
    private const string Zero = "[0٠۰]";
    private const string One = "[1١۱]";
    private const string Two = "[2٢۲]";
    private const string OperatorDigit = "[0125٠١٢٥۰۱۲۵]";

    // Egyptian mobile numbers, local (01x) or international (+20 / 0020 / 20), contiguous or with single spaces or hyphens in 4-4 or 1-3-4 groups, in any mix of the three digit scripts. Every number starts after +, the text start or a character that is not an ASCII letter, a digit or an underscore, so GUIDs, hex ids and longer digit runs stay intact.
    // No \b: .NET counts Arabic letters as word characters and RE2 does not. The boundaries are captured and written back, and the OTel collector runs this exact string (deploy/observability/otel-collector/config.yaml). ^ and $, not \A and \z: NonBacktracking drops the captures when \z meets a final newline.
    public const string PhonePattern = $@"(?:\+|(^|{NotWordCharacter})(?:{Zero}{Zero})?)(?:{Two}{Zero}[ -]?)?(?:{Zero}[ -]?)?{One}[ -]?{OperatorDigit}(?:[ -]?{Digit}{{4}}[ -]?{Digit}{{4}}|{Digit}[ -]?{Digit}{{3}}[ -]?{Digit}{{4}})({NotWordCharacter}|$)";

    public const string PhoneReplacementPattern = "${1}" + PhoneReplacement + "${2}";

    private static readonly Regex PhoneRegex = new(PhonePattern, RegexOptions.NonBacktracking);

    // Each match consumes the character after the number, so a number right behind another one is only found by the second pass.
    private static readonly TextRedactor Redactor = new([new(RedactionPatterns.EmailAddress, EmailReplacement), new(PhoneRegex, PhoneReplacementPattern), new(PhoneRegex, PhoneReplacementPattern)]);

    [return: NotNullIfNotNull(nameof(value))]
    public static string? Redact(string? value) => Redactor.Redact(value);
}
```
(Compiled and checked by the planner: `PhonePattern` equals the verified pattern.)

### Collector config
Run once from the worktree root (Python 3, any OS); it asserts 8 old copies and writes the 8 new ones with the digits as literal UTF-8 characters, preserving LF line endings:
```python
import sys
path = "deploy/observability/otel-collector/config.yaml"
B = chr(92)
digit = "[0-9٠-٩۰-۹]"
nonword = "[^0-9٠-٩۰-۹A-Za-z_]"
zero, one, two = "[0٠۰]", "[1١۱]", "[2٢۲]"
operator = "[0125٠١٢٥۰۱۲۵]"
old = "(?:" + B + "+|(^|[^0-9A-Za-z_])(?:00)?)(?:20[ -]?)?(?:0[ -]?)?1[ -]?[0125](?:[ -]?[0-9]{4}[ -]?[0-9]{4}|[0-9][ -]?[0-9]{3}[ -]?[0-9]{4})([^0-9A-Za-z_]|$)"
new = ("(?:" + B + "+|(^|" + nonword + ")(?:" + zero + zero + ")?)(?:" + two + zero + "[ -]?)?(?:" + zero + "[ -]?)?" + one + "[ -]?" + operator
       + "(?:[ -]?" + digit + "{4}[ -]?" + digit + "{4}|" + digit + "[ -]?" + digit + "{3}[ -]?" + digit + "{4})(" + nonword + "|$)")
esc = lambda s: s.replace(B, B + B).replace("$", "$$")
text = open(path, encoding="utf-8").read()
assert text.count(esc(old)) == 8
open(path, "w", encoding="utf-8", newline="").write(text.replace(esc(old), esc(new)))
```
(Tested by the planner on a copy: 8 replacements, 16 diff lines, LF kept.) Then, in the comment block above `transform/redact`, after the sentence ending `$ doubled (collector config); it runs twice because a match consumes the character after the number.` insert: `# Its digit classes hold the Arabic-Indic (U+0660-U+0669) and Extended Arabic-Indic (U+06F0-U+06F9) digits as literal characters: RE2 has no \u escape and .NET has no \x{...} escape.` (ASCII hyphens, keep the `#` indentation of the block).

### SpreadsheetPackageParts.cs
```csharp
using System.IO.Compression;

namespace Core.Spreadsheets;

// ClosedXML throws NullReferenceException when [Content_Types].xml, _rels/.rels or the workbook relationship is missing, and reads a sheet without a relationship as empty, so the parts it needs are checked before it opens the package.
internal static class SpreadsheetPackageParts
{
    private const string ContentTypesPartName = "[Content_Types].xml";
    private const string PackageRelationshipsPartName = "_rels/.rels";
    private static readonly string[] OfficeDocumentTypes = ["http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "http://purl.oclc.org/ooxml/officeDocument/relationships/officeDocument"];
    private static readonly Uri PackageRoot = new("http://package/");

    public static void EnsurePresent(ZipArchive archive)
    {
        var entries = archive.Entries.ToLookup(x => x.FullName, StringComparer.OrdinalIgnoreCase);
        Require(entries, ContentTypesPartName);
        var workbookRelationship = OpcPartReader.ReadRelationships(Require(entries, PackageRelationshipsPartName))
            .FirstOrDefault(x => !x.IsExternal && OfficeDocumentTypes.Contains(x.Type, StringComparer.Ordinal))
            ?? throw new InvalidOperationException("The package has no workbook relationship.");
        var workbookPartName = ResolvePartName(string.Empty, workbookRelationship.Target);
        var sheetRelationshipIds = OpcPartReader.ReadSheetRelationshipIds(Require(entries, workbookPartName));
        var workbookRelationships = OpcPartReader.ReadRelationships(Require(entries, RelationshipsPartName(workbookPartName)))
            .Where(x => !x.IsExternal)
            .ToLookup(x => x.Id, StringComparer.Ordinal);
        foreach (var id in sheetRelationshipIds)
        {
            var relationship = (id is null ? null : workbookRelationships[id].FirstOrDefault()) ?? throw new InvalidOperationException("A sheet in the workbook has no relationship.");
            Require(entries, ResolvePartName(workbookPartName, relationship.Target));
        }
    }

    private static ZipArchiveEntry Require(ILookup<string, ZipArchiveEntry> entries, string partName) => entries[partName].FirstOrDefault() ?? throw new InvalidOperationException($"The package part '{partName}' is missing.");

    private static string ResolvePartName(string sourcePartName, string target) => Uri.UnescapeDataString(new Uri(new Uri(PackageRoot, sourcePartName), target).AbsolutePath).TrimStart('/');

    private static string RelationshipsPartName(string partName)
    {
        var folderLength = partName.LastIndexOf('/') + 1;
        return $"{partName[..folderLength]}_rels/{partName[folderLength..]}.rels";
    }
}
```

### OpcPartReader.cs
```csharp
using System.IO.Compression;
using System.Xml;

namespace Core.Spreadsheets;

internal static class OpcPartReader
{
    public static IReadOnlyList<OpcRelationship> ReadRelationships(ZipArchiveEntry entry)
    {
        List<OpcRelationship> relationships = [];
        using var reader = Open(entry);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Relationship")
            {
                relationships.Add(new OpcRelationship(reader.GetAttribute("Id") ?? string.Empty, reader.GetAttribute("Type") ?? string.Empty, reader.GetAttribute("Target") ?? string.Empty, string.Equals(reader.GetAttribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase)));
            }
        }

        return relationships;
    }

    // workbook > sheets > sheet; the relationship id is the only namespaced attribute named "id" (r:id, transitional or strict).
    public static IReadOnlyList<string?> ReadSheetRelationshipIds(ZipArchiveEntry entry)
    {
        List<string?> ids = [];
        using var reader = Open(entry);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && reader.Depth == 2 && reader.LocalName == "sheet")
            {
                ids.Add(RelationshipId(reader));
            }
        }

        return ids;
    }

    private static string? RelationshipId(XmlReader reader)
    {
        while (reader.MoveToNextAttribute())
        {
            if (reader.LocalName == "id" && reader.NamespaceURI.Length > 0)
            {
                return reader.Value;
            }
        }

        return null;
    }

    private static XmlReader Open(ZipArchiveEntry entry) => XmlReader.Create(entry.Open(), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, CloseInput = true });
}
```
A malformed relationships or workbook part throws `XmlException` → already unreadable (the existing `workbook-part-not-xml` row keeps its `XmlException` inner).

### Test helpers (`SpreadsheetPackageCorruption.CorruptPart` new arms, before the `_ =>` arm)
| Key | Expression |
|---|---|
| `content-types-missing` | `RewriteEntry(workbook, "[Content_Types].xml", _ => null)` |
| `package-relationships-missing` | `RewriteEntry(workbook, "_rels/.rels", _ => null)` |
| `workbook-relationship-missing` | `RewriteEntry(workbook, "_rels/.rels", x => Regex.Replace(x, "<Relationship [^>]*/officeDocument\"[^>]*/>", string.Empty))` |
| `workbook-relationship-external` | `RewriteEntry(workbook, "_rels/.rels", x => x.Replace("Target=\"/xl/workbook.xml\"", "Target=\"/xl/workbook.xml\" TargetMode=\"External\"", StringComparison.Ordinal))` |
| `workbook-relationship-target-missing` | `RewriteEntry(workbook, "_rels/.rels", x => x.Replace("/xl/workbook.xml", "/xl/missing.xml", StringComparison.Ordinal))` |
| `workbook-relationships-missing` | `RewriteEntry(workbook, "xl/_rels/workbook.xml.rels", _ => null)` |
| `sheet-relationship-missing` | `RewriteEntry(workbook, "xl/_rels/workbook.xml.rels", x => Regex.Replace(x, "<Relationship [^>]*/worksheets/sheet1\\.xml\"[^>]*/>", string.Empty))` |
| `sheet-relationship-id-missing` | `RewriteEntry(workbook, "xl/workbook.xml", x => Regex.Replace(x, " r:id=\"[^\"]*\"", string.Empty))` |
| `sheet-part-missing` | `RewriteEntry(workbook, "xl/worksheets/sheet1.xml", _ => null)` |
| `relationship-targets-relative` | `RewriteEntry(RewriteEntry(workbook, "_rels/.rels", x => x.Replace("\"/xl/workbook.xml\"", "\"xl/workbook.xml\"", StringComparison.Ordinal)), "xl/_rels/workbook.xml.rels", x => x.Replace("\"/xl/", "\"", StringComparison.Ordinal))` |
| `relationship-target-upper-case` | `RewriteEntry(workbook, "_rels/.rels", x => x.Replace("/xl/workbook.xml", "/XL/Workbook.xml", StringComparison.Ordinal))` |
| `workbook-relationship-strict` | `RewriteEntry(workbook, "_rels/.rels", x => x.Replace("http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "http://purl.oclc.org/ooxml/officeDocument/relationships/officeDocument", StringComparison.Ordinal))` |

(ClosedXML 0.105.1 writes `Target="/xl/workbook.xml"` and `Target="/xl/worksheets/sheet1.xml"`; checked by the planner.)

## Error codes
None new. All new failures reuse the configured `SpreadsheetOptions.UnreadableErrorCode` (app: `ErrorCodes.SpreadsheetUnreadable` = `SPREADSHEET_UNREADABLE`, `BadRequestCoreException`, 400). No resource strings change.

## Domain behaviour
No domain change.

## API surface
No new endpoint. `POST /api/question-imports/preview` and `POST /api/question-imports` now return 400 `SPREADSHEET_UNREADABLE` (was 500) for packages missing the parts in D8.

## Test plan
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 1 | `LogRedactorArabicIndicDigitTests` | `Redact_ArabicIndicMobileNumber_ReplacesWithMarker(string text)` — `[Theory]` rows (verification rows 34–43 and 45, verbatim): `call ٠١٠١٢٣٤٥٦٧٨`, `call ۰۱۰۱۲۳۴۵۶۷۸`, `call ٠١٠ ١٢٣٤ ٥٦٧٨`, `call ٠١٠-١٢٣٤-٥٦٧٨`, `call ٠١٠٠ ١٢٣ ٤٥٦٧`, `call +٢٠١٠١٢٣٤٥٦٧٨`, `call +٢٠ ١٠ ١٢٣٤ ٥٦٧٨`, `call +٢٠-١٠٠-١٢٣-٤٥٦٧`, `call ٠٠٢٠١٠١٢٣٤٥٦٧٨`, `call ٠٠٢٠ ١٠ ١٢٣٤ ٥٦٧٨`, `call ۰۱۲۴۴۵۵۶۶۷۷` | `LogRedactor.Redact(text)` == `"call [redacted-phone]"` |
| 2 | same | `Redact_MixedDigitScripts_ReplacesWithMarker(string text)` rows: `call 010١٢٣٤٥٦٧٨`, `call ٠1٠1234٥678`, `call ۰۱٠١٢٣٤۵۶۷۸`, `call +20١٠١٢٣٤٥٦٧٨` | == `"call [redacted-phone]"` |
| 3 | same | `Redact_ArabicTextOrPunctuationAdjacentToArabicIndicNumber_ReplacesNumberOnly(string text, string expected)` rows 44, 50, 51, 52, 53, 54, 57 of the verification table (input, new result; row 57 as `"call ٠١٠١٢٣٤٥٦٧٨\n"` → `"call [redacted-phone]\n"`) | == expected |
| 4 | same | `Redact_ArabicIndicNumbersOneSeparatorApart_ReplacesEach(string text, string expected)` rows 55, 56 | == expected |
| 5 | same | `Redact_ArabicIndicNonMobileDigitRuns_ReturnsUnchanged(string text)` rows 58, 63, 64, 65, 66 | == text |
| 6 | same | `Redact_DigitOfAnotherScriptAdjacent_ReturnsUnchanged(string text)` rows 59, 60, 61, 62 | == text |
| 7 | `LogRedactorCollectorParityTests` | `CollectorConfig_PhoneStatements_RunTheLogRedactorPatternTwiceInContext(string context, string statementFormat)` rows: (`"log"`, `"replace_pattern(body, \"{0}\", \"{1}\") where IsString(body)"`), (`"log"`, `"replace_all_patterns(attributes, \"value\", \"{0}\", \"{1}\")"`), (`"span"`, same attributes format), (`"spanevent"`, same attributes format). Body: `var expected = string.Format(CultureInfo.InvariantCulture, statementFormat, Escaped(LogRedactor.PhonePattern), Escaped(LogRedactor.PhoneReplacementPattern)); var prefix = statementFormat[..statementFormat.IndexOf('"', StringComparison.Ordinal)];` then `ContextStatements(ReadCollectorConfig(), context).Where(x => x.StartsWith(prefix, StringComparison.Ordinal) && x.Contains(LogRedactor.PhoneReplacement, StringComparison.Ordinal))` `.Should().Equal(expected, expected)`. Helpers: `private static List<string> ContextStatements(string config, string context)` — split on `'\n'`, `TrimEnd('\r')`; the lines equal (after `Trim()`) to `$"- context: {context}"` `.Should().ContainSingle()`; take following lines while blank or `Indent(line) > Indent(contextLine)`; keep trimmed lines starting with `"- "`, return them without the `"- "`. `private static int Indent(string line) => line.Length - line.TrimStart().Length;` | exact pair per context; any drift, extra or missing phone statement in that context fails |
| 8 | `TrainingDataScrubberTests` | `ScrubText_MobileNumberInAnyDigitScript_Replaced(string number)` rows: `۰۱۰۱۲۳۴۵۶۷۸`, `٠١٠ ١٢٣٤ ٥٦٧٨`, `٠1٠1234٥678`, `+٢٠ ١٠ ١٢٣٤ ٥٦٧٨` | `ScrubText("كلمني " + number)` == `"كلمني [number]"` (scrubber unchanged; consistency with LogRedactor) |
| 9 | `ClosedXmlSpreadsheetReaderPackagePartsTests` | `Read_RequiredPartMissing_ThrowsConfiguredUnreadableCode(string corruption, string missing)` rows: (`content-types-missing`, `The package part '[Content_Types].xml' is missing.`), (`package-relationships-missing`, `The package part '_rels/.rels' is missing.`), (`workbook-relationship-missing`, `The package has no workbook relationship.`), (`workbook-relationship-external`, `The package has no workbook relationship.`), (`workbook-relationship-target-missing`, `The package part 'xl/missing.xml' is missing.`), (`workbook-part-missing`, `The package part 'xl/workbook.xml' is missing.`), (`workbook-relationships-missing`, `The package part 'xl/_rels/workbook.xml.rels' is missing.`), (`sheet-relationship-missing`, `A sheet in the workbook has no relationship.`), (`sheet-relationship-id-missing`, `A sheet in the workbook has no relationship.`), (`sheet-part-missing`, `The package part 'xl/worksheets/sheet1.xml' is missing.`). Input: `SpreadsheetPackageCorruption.CorruptPart(new QuestionWorkbookBuilder().Sheet("Mcq", ["stem"], ["one"]).Build(), corruption)` | `BadRequestCoreException`, `ErrorCode == "PROBE_UNREADABLE"`, `InnerException` `BeOfType<InvalidOperationException>()` with `Message == missing` (proves the pre-check, not ClosedXML, refused it) |
| 10 | same | `Read_ValidPackageVariant_ReadsWorkbook(string variant)` rows: `relationship-targets-relative`, `relationship-target-upper-case`, `workbook-relationship-strict` | `Sheets.Single()`: `Name == "Mcq"`, `Headers` Equal `"stem"`, `Rows.Select(x => x.Number)` Equal `2`, `Rows[0].Cells` Equal `"one"` |
| 11 | `QuestionImportEndpointTests` | `PostPreview_WorkbookWithoutPackageRelationships_Returns400SpreadsheetUnreadable` | mirror `PostPreview_CorruptFile_Returns400SpreadsheetUnreadable` with `Form(lessonId, SpreadsheetPackageCorruption.CorruptPart(ValidWorkbook(), "package-relationships-missing"))`: status 400, `ReadCodeAsync` == `"SPREADSHEET_UNREADABLE"` |

All existing tests (`LogRedactorTests`, `LogRedactorBoundaryTests`, the three existing parity tests, `ClosedXmlSpreadsheetReader*Tests`, `TrainingDataScrubberTests`, `QuestionImportEndpointTests`) pass unchanged.

## Docs
- `docs/observability.md` §12, item 2: replace `(local \`01x\`, \`20\`, \`+20\` or \`0020\`, written contiguously …)` wording so it says the digits may be ASCII, Arabic-Indic (U+0660–U+0669) or Extended Arabic-Indic (U+06F0–U+06F9), mixed freely, and replace `a character that is not an ASCII letter, digit or underscore` with `a character that is not an ASCII letter, a digit of any of those three scripts or an underscore`; add: `so an ASCII number written right against an Arabic-Indic digit is not masked`. Item 3: after `(with \`\\\` and \`$\` doubled for OTTL and the collector config)` add `; its non-ASCII digits are written as literal characters in both copies, because .NET has no \`\x{…}\` escape and RE2 has no \`\u\` escape`; replace `\`LogRedactorCollectorParityTests\` fails if the two copies drift` with `\`LogRedactorCollectorParityTests\` checks the exact pair in each of the four contexts (log body, log attributes, span attributes, span-event attributes) and fails if the copies drift`.
- `docs/question-import.md`: error row → `| \`SPREADSHEET_UNREADABLE\` | a zip archive that is not a readable workbook (including one without \`[Content_Types].xml\`, \`_rels/.rels\`, the workbook part or a sheet part the workbook declares), or whose entries unpack to more than 100 MB | 400 |`. Limits: after the unpacked-size bullet add `- Before the workbook is loaded, the package must have \`[Content_Types].xml\`, \`_rels/.rels\` with a workbook relationship whose part exists, the workbook's relationships part, and a relationship and part for every sheet the workbook declares; only those three small XML parts are parsed (DTDs refused). Otherwise \`SPREADSHEET_UNREADABLE\`.`
- `docs/security.md` "Spreadsheets" bullet: append `A package missing \`[Content_Types].xml\`, \`_rels/.rels\`, its workbook part or a declared sheet part is refused with the same code before ClosedXML opens it.`
- `docs/constitution.md`, `.claude/skills/dotnet-feature/SKILL.md`: as in "Existing code touched".

## Definition of done
- [ ] `LogRedactor.PhonePattern` is composed from the six private constants exactly as in the plan; no verbatim `\u` escapes; `RegexOptions.NonBacktracking` unchanged.
- [ ] The collector config contains the new phone statement 8 times (2 per context) with literal Arabic-Indic / Extended Arabic-Indic digits, and no old copy; nothing else in the file changes except the one added comment line.
- [ ] Existing parity tests pass unchanged; new per-context theory passes for all four rows.
- [ ] `TrainingDataScrubber.cs` unchanged; new scrubber theory passes.
- [ ] `SpreadsheetPackageParts`, `OpcPartReader`, `OpcRelationship` exist in `Core.Spreadsheets` with the given members; no Elmanhg names or codes in them.
- [ ] `SpreadsheetPackageGuard.EnsureReadablePackage` runs the size check and then `SpreadsheetPackageParts.EnsurePresent` on the same archive; `ClosedXmlSpreadsheetReader` calls it; no `catch` of `NullReferenceException` anywhere; `IsUnreadablePackage` unchanged.
- [ ] Every missing-part row returns `BadRequestCoreException(UnreadableErrorCode)` with an `InvalidOperationException` inner carrying the listed message; the three valid variants read the sheet.
- [ ] The endpoint test returns 400 `SPREADSHEET_UNREADABLE` for a workbook without `_rels/.rels`.
- [ ] No new NuGet package, no migration, no change to `Core.Notifications` or `CoreDbContext`, no new exception type, no `#<number>` in code comments.
- [ ] Docs updated as listed: observability §12 items 2–3, question-import error row + Limits bullet, security Spreadsheets bullet, constitution `Core.Spreadsheets`, SKILL.md §5.
- [ ] `dotnet build` (warnings as errors) and the full test suite pass.
