# Plan — E21.S14 Small leftovers: OTP abort after send, redaction boundary, test sizes

Story: GitHub 336 (closes the leftovers listed in GitHub 334). Worktree: `D:/Personal/elmanhg-wt/336`, base `main` at `f7a31e84`.
Evidence: `.process/331-fixes-from-the-independent-review-of-the-merged/03-review.md` (lines 16, 68-69), `02-implementation.md` line 108.

## Goal
After this ships, a client that disconnects once the OTP provider has accepted the message no longer gets a free paid send: the cooldown and the daily resend quota stay spent, and only a delivery failure reported while the request is still live hands the claim back. The API's `LogRedactor` and the OTel collector run one phone pattern with the same meaning, so Arabic text glued to a spaced number (`رقم010 1234 5678`) is masked in both places. A workbook whose XML or cell references are corrupt returns 400 `SPREADSHEET_UNREADABLE` instead of 500. Nothing changes for users who send valid input.

## Scope
**In:**
1. OTP: keep the claim when the request is cancelled; roll back only on a failure while the request is live; tests that cancel after the provider accepts.
2. Redaction: one phone pattern, built without `\b`, in `LogRedactor` and in the four collector phone statements (each applied twice); Arabic-adjacent, adjacent-number, line-break, timestamp and long-digit test rows; a parity test that reads `deploy/observability/otel-collector/config.yaml`.
3. Spreadsheet: malformed-workbook rows whose inner exception is not `InvalidDataException` (`XmlException`, `FormatException`, `InvalidOperationException`); widen the shared catch filter so the first two map to the configured code; a core host-start test and an app host-start test for `SpreadsheetOptionsValidator`.
4. File splits by concern for all 8 listed files, plus `ClosedXmlSpreadsheetReaderTests.cs` and `OtpDeliveryClaimTests.cs` because this story adds tests to them. Rename `RepositoryAuditStamping*Tests` to `AuditStampingInterceptor*Tests`.
5. Docs: `docs/otp-delivery.md`, `docs/observability.md` §12.

**Out:** `Core.Notifications` and `CoreDbContext` stay as they are (dev decision 2026-10-06). No migration. No new NuGet package. `OtpChannelRouter`, `GenerateOTPHandler`, the endpoints and the HTTP clients stay unchanged. Arabic-Indic digits (`٠١٠…`) are not redacted by either engine today, and this story does not change that (see D9).
**Deferred:** none. Everything runs offline: unit tests, Testcontainers Postgres for the two `SpreadsheetCompositionTests`, and a file read of the collector YAML.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| D1 | Which send outcomes roll the claim back? | `OtpDeliveryClaim.DeliverAsync` releases **only** when the sender throws **and** `cancellationToken.IsCancellationRequested` is `false` when the filter runs: `catch (Exception) when (!cancellationToken.IsCancellationRequested)`. The full table is below this one. | The request token is the only signal that the client went away. From that point the provider may already have accepted the message (2xx headers arrived, then the body read was cancelled, or the send was in flight), so keeping the claim is the cost-safe choice. All genuine provider failures arrive as exceptions while the request is still live: `EnsureDelivered` → `ServiceUnavailableCoreException`, Polly timeouts and an open circuit are mapped by `IsTransientFailure` because the request token is not cancelled, and `ChannelUnavailable` is thrown by `Resolve`. This also matches the filter `OtpChannelRouter` already uses for its delivered/failed metric. |
| D2 | A token the client cannot cancel for the bookkeeping? | Nothing is written after a successful send: the claim is saved **before** the send (`AddIfAbsentAsync` / `ReissueAsync`). The only post-send write is the release, which already uses `CancellationToken.None`. That stays, with an updated WHY comment. No new token plumbing. | The story's concern is met: once the provider has accepted, no client cancellation can undo anything. |
| D3 | Where does the logic live? | Only in `OtpDeliveryClaim` (core). `GenerateOTPHandler` gets no `try`/`catch` and no change at all. | Constitution: no `try`/`catch` in handlers. The claim type exists for exactly this. |
| D4 | What happens to the existing test `DeliverAsync_SendCancelled_ReleasesWithoutTheCancelledToken`? | It is **replaced** by `DeliverAsync_SendCancelledByTheRequest_KeepsTheClaimAndRethrows` in the new `OtpDeliveryClaimCancellationTests` (same arrange, inverted assertions). This is the only test name that is not kept. | The behaviour changes on purpose (SKILL §8.11 allows updating a test when the behaviour changes intentionally). The implementer states this in the report so the test-integrity guard reads it as intended. |
| D5 | Redaction pattern without lookbehind. | `PhonePattern` (below) uses explicit ASCII boundaries that are **captured and written back**: the leading boundary `(^|[^0-9A-Za-z_])` is group 1, the trailing boundary `([^0-9A-Za-z_]|$)` is group 2, and the replacement is `${1}[redacted-phone]${2}`. The unseparated branch keeps its old shape: no leading boundary, trailing boundary only. | This reproduces RE2's ASCII `\b` exactly, with no lookaround and no `\b`. .NET's `\b` counts Arabic letters as word characters and RE2's does not, which was the bug. Leaving the unseparated branch without a leading boundary keeps today's behaviour ("numbers without separators are redacted by both"). |
| D6 | The trailing boundary consumes one character, so in `A B` (one separator) the second spaced number loses its leading boundary. | Apply the phone rule **twice**: `LogRedactor` lists it twice in its `TextRedactor`, and the collector runs each phone statement twice, back to back. Two passes always suffice: a number can be missed only when the number right before it was redacted, the missed number's trailing character is never consumed, and the marker ends in `]`, which cannot end a phone match. | One pass would leak `call 010 1234 5678 011 1234 5678` (verified: one pass leaves `011 1234 5678`). |
| D7 | `^`/`$` or `\A`/`\z`? | `^` and `$` (no multiline option on either side). | Verified with `dotnet run` (.NET 10.0.401): `RegexOptions.NonBacktracking` **drops both captures** when `\z` meets a final `\n` (`"call 010 1234 5678\n"` → `"call[redacted-phone]"`), while `^`/`$` give the same results as the backtracking engine. .NET's `$` can also match before a final `\n`, but the class alternative is tried first and `\n` belongs to the class, so the meaning equals Go's `$` (end of text). |
| D8 | How was parity verified? | One case file of 33 inputs and expected outputs (every existing `LogRedactorTests` row plus all new rows), run in .NET NonBacktracking, .NET backtracking, Python `re`, and real RE2 (`google-re2` 1.1, the same syntax and leftmost-first semantics as Go `regexp`, which OTTL uses). All four agree on all 33. The old pattern was run in RE2 and Python `re` (Unicode `\b`, a stand-in for .NET) to confirm the bug (`رقم010 1234 5678`, `010 1234 5678رقم`, `01012345678رقم`). | The story asks for a verified pattern, not one checked by reasoning alone. |
| D9 | Known behaviour this story keeps. | (a) The unseparated branch has no leading boundary, so the last 10 digits of a longer run can be masked when they form a mobile number (`1791234567890` → `179[redacted-phone]`). Both engines did this before and still do. No test row pins it. (b) Arabic-Indic digits are not matched (both engines use `[0-9]`). | Changing either one is a behaviour change outside "make them agree". Both are listed in the report as possible follow-ups. |
| D10 | How is drift between the two copies prevented? | `LogRedactor.PhonePattern` and `LogRedactor.PhoneReplacementPattern` become `public const string`. The new `LogRedactorCollectorParityTests` reads the collector YAML and checks for exact text: the pattern escaped for OTTL and the collector (each `\` doubled for the OTTL string literal, each `$` doubled for collector config expansion) appears 8 times, the escaped replacement appears 8 times, the shared email pattern appears 4 times, and no `\\b` remains. | "Both copies must match exactly" becomes a failing test, not a code-review note. The YAML items are plain scalars, so the only escaping comes from OTTL string literals (`\\` → `\`) and confmap (`$$` → `$`). The existing email statements already follow this rule. |
| D11 | Can a malformed-zip row raise something other than `InvalidDataException`? | **Not from the guard.** A byte-level fuzz of a 6 KB workbook (every offset; widths 1, 2 and 4; 9 values) made `ZipArchive` throw only `InvalidDataException`. **Yes, from the package parts** (a valid zip, so the guard passes): XML that is not valid → `XmlException`; a cell reference that is not A1 → `FormatException`; `xl/workbook.xml` missing → `InvalidOperationException`. All three come from the `XLWorkbook` constructor (verified with stack traces). Today `XmlException` and plain `FormatException` escape the reader's filter, which returns **500**. | These are the "row of a non-InvalidDataException type" the story asks for, and they expose a real gap. |
| D12 | Fix the gap. | Move the filter into one predicate, `SpreadsheetPackageGuard.IsUnreadablePackage(Exception)`: `InvalidDataException or FormatException or XmlException or OpenXmlPackageException or ArgumentException or InvalidOperationException`. Both `catch` sites use it. `FileFormatException` is dropped because it derives from `FormatException`, which also lets both files drop `using System.IO.Packaging;`. | `docs/question-import.md` already promises `SPREADSHEET_UNREADABLE` for "a zip archive that is not a readable workbook", so this brings the code in line with the doc. One predicate keeps the two filters from drifting apart. |
| D13 | ClosedXML throws `NullReferenceException` when `[Content_Types].xml` or `_rels/.rels` is missing, and on some deflate-level corruptions. | Not caught. It stays a 500 and is logged. | Catching `NullReferenceException` would hide our own bugs. The fuzz shows it can also come from corrupted entry data, so a "required parts" check would only be a partial fix. Listed in the report. |
| D14 | Host-level validator test. | Two levels: core (`Host.CreateEmptyApplicationBuilder` + `AddCoreSpreadsheets`, then `StartAsync`) and app (`ApiFactory.WithWebHostBuilder` + `ConfigureTestServices` → `PostConfigure<SpreadsheetOptions>` sets the cap to 0, then `CreateClient()` throws). | The app does not bind `SpreadsheetOptions` from configuration (delegate only), so `PostConfigure` is the only way to break it. This follows the precedent `TelemetryRegistrationTests.Start_NonHttpOtlpEndpoint_FailsOptionsValidation`. |
| D15 | File splits: base classes or duplication? | Follow the existing `*TestBase` precedent (`AuditStampingTestBase`, `RefreshTokenRotatorTestBase`) where setup is more than ~10 lines (`GenerateOTPHandlerTestBase`, `PublicMediaPipelineTestBase`). Where the shared state is one or two fields (`Now`, `AllSheets`, `_reader`, `Lifetime`), each class repeats it. Shared byte helpers go into static helper classes. | Mirror, don't modernize. |
| D16 | Which listed files stay over ~100 lines? | None. Every listed file is split (table in "Existing code touched"). `ClosedXmlSpreadsheetReader.cs` (104) is only just over, but this story edits it anyway, so it is split into package opening and sheet reading. | The story says "best effort, say which stay": none stay. |
| D17 | Class names after a split. | The original class name stays on the main concern, and new classes are named `<Sut><Concern>Tests`. Every `[Fact]`/`[Theory]` method name is kept, except D4. | Keeps `git log -S` and the test-name check simple. |
| D18 | Audit stamping rename. | `git mv` `RepositoryAuditStampingTests.cs` → `AuditStampingInterceptorTests.cs` and `RepositoryAuditStampingModifiedTests.cs` → `AuditStampingInterceptorModifiedTests.cs`. Rename the classes to match. Method bodies stay unchanged. | The stamping lives in `AuditStampingInterceptor` since story 329. No doc names these classes (grep checked). |
| D19 | `constitution.md` / `SKILL.md`. | No change. | Neither describes the OTP claim release, the spreadsheet catch list or the redaction boundary. `Core.Spreadsheets` "(ClosedXML reader/writer with an unpacked-size pre-check)" and the `TextRedactor` lines are still true. |

**D1 rollback table (`OtpDeliveryClaim.DeliverAsync`)**

| Sender outcome | Request token when the filter runs | Claim | Rethrown |
|---|---|---|---|
| returns a channel (the provider accepted), even if the token is cancelled during or after the send | any | kept | — (returns the channel) |
| `ServiceUnavailableCoreException` (channel unavailable; `Rejected`; `Unreachable`, including Polly timeout or open circuit) | live | **released** (new row: `Delete`; reissue: `RestoreReissue`), then saved with `CancellationToken.None` | yes |
| any other exception (`OperationCanceledException` not caused by the request, `InvalidOperationException`, …) | live | **released** | yes |
| any exception (normally `OperationCanceledException`/`TaskCanceledException` from the request token; also a failure that races with the client's cancellation) | cancelled | **kept** (cooldown, count and new code stay) | yes |
| the release save itself fails | live | unchanged behaviour: the release error propagates and the row keeps the claimed state | release error |

## Existing code touched
| File | Change |
|------|--------|
| `api/core-libraries/Core.OTP/GenerateOTP/OtpDeliveryClaim.cs` | Catch filter + two WHY comments (Domain behaviour §1). |
| `api/core-libraries/Core.OTP/Entities/OTP.cs` | Keep: usings `Core.DDD.Entities` only; the class header `public partial class Otp : Entity, IVersioned`; all properties; private ctor; `Create`. Move `ReissueWindow` (with its WHY comment) and `Reissue` to `OTP.Reissue.cs`. Move `Verify`, `MarkUsed` and `HashesMatch` to the new `OTP.Verification.cs`. Bodies are moved verbatim. Expected length is about 60 lines. |
| `api/core-libraries/Core.OTP/Entities/OTP.Reissue.cs` | Add `using Core.Errors; using Core.OTP.Exceptions;`, then the moved `ReissueWindow` field and `Reissue` method placed above `RestoreReissue`. About 60 lines. |
| `api/core-libraries/Core.Spreadsheets/ClosedXmlSpreadsheetReader.cs` | `public sealed partial class ClosedXmlSpreadsheetReader(IOptions<SpreadsheetOptions> spreadsheetOptions) : ISpreadsheetReader`, holding only `Read`. The catch becomes `catch (Exception exception) when (SpreadsheetPackageGuard.IsUnreadablePackage(exception))`. Remove `using System.IO.Packaging;` and `using DocumentFormat.OpenXml.Packaging;`. Move `HeaderRow` (with its comment), `ReadSheet`, `ReadCells` and `CellText` to `ClosedXmlSpreadsheetReader.Sheets.cs`. |
| `api/core-libraries/Core.Spreadsheets/SpreadsheetPackageGuard.cs` | Add `public static bool IsUnreadablePackage(Exception exception) => exception is InvalidDataException or FormatException or XmlException or OpenXmlPackageException or ArgumentException or InvalidOperationException;`. The catch in `EnsureWithinUncompressedCap` becomes `when (IsUnreadablePackage(exception))`. Add `using System.Xml;` and remove `using System.IO.Packaging;`. |
| `api/Elmanhg.Application/Shared/Observability/LogRedactor.cs` | New constants, the regex and two rules (Domain behaviour §2). |
| `deploy/observability/otel-collector/config.yaml` | Replace the four phone statements with eight (Domain behaviour §3). Update the comment above `transform/redact`. |
| `api/Elmanhg.Tests/Core/Otp/OtpDeliveryClaimTests.cs` | Delete `DeliverAsync_SendCancelled_ReleasesWithoutTheCancelledToken` (D4). Add 2 tests (Test plan). |
| `api/Elmanhg.Tests/Core/Otp/OtpTests.cs` | Keep `Create_*` ×2 and `Verify_*` ×4, the `Now` field and the usings it still needs. Move the rest (Test plan T-map). |
| `api/Elmanhg.Tests/Core/Otp/GenerateOTPHandlerTests.cs` | Becomes `public sealed class GenerateOTPHandlerTests : GenerateOTPHandlerTestBase` with 5 tests. Fields and ctor move to the base. |
| `api/Elmanhg.Tests/Core/Storage/PublicMediaPipelineTests.cs` | `public sealed class PublicMediaPipelineTests : PublicMediaPipelineTestBase` with the 6 Local tests. Fields, `Dispose` and helpers move to the base. |
| `api/Elmanhg.Tests/Application/Features/Dashboard/Shared/DashboardCachingTests.cs` | Keep the 6 `AddApplication_*` tests and `BuildProvider`. Move `DashboardCardQueries` and the 3 query-shape tests. |
| `api/Elmanhg.Tests/Integration/Auth/OtpConcurrencyEndpointTests.cs` | Keep the 3 parallel tests and `SendInParallelAsync`. Use `OtpEndpointTestData` for the routes, `PostAsync`, `SeedAsync` and `ReadOtpAsync`. Move `SendOtp_ResendDeliveryFails_KeepsThePreviousCodeAndLimits`. |
| `api/Elmanhg.Tests/Domain/ReviewSessions/ReviewSessionTests.cs` | Keep `Start_*` and `RecordOpening_*` ×4. Move `HasOpened_*` and `EnsureOpened_*` ×3. |
| `api/Elmanhg.Tests/Core/Spreadsheets/ClosedXmlSpreadsheetReaderTests.cs` | Keep `Read_Workbook_ReturnsSheetsHeadersAndRowsWithExcelRowNumbers`, `Read_BlankRow_IsSkipped`, `Read_NumberAndBooleanCells_UseInvariantText`, `AllSheets` and `_reader`. Move the rest (T-map). |
| `api/Elmanhg.Tests/Integration/Composition/SpreadsheetCompositionTests.cs` | Add 2 tests (Test plan). |
| `api/Elmanhg.Tests/Core/Persistence/RepositoryAuditStampingTests.cs` | `git mv` → `AuditStampingInterceptorTests.cs`; class `AuditStampingInterceptorTests`. |
| `api/Elmanhg.Tests/Core/Persistence/RepositoryAuditStampingModifiedTests.cs` | `git mv` → `AuditStampingInterceptorModifiedTests.cs`; class `AuditStampingInterceptorModifiedTests`. |
| `docs/otp-delivery.md` | Line 16 (Docs). |
| `docs/observability.md` | §12, items 2 and 3 of "Three redaction layers" (Docs). |

## Files to create
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `api/core-libraries/Core.OTP/Entities/OTP.Verification.cs` | partial class | `namespace Core.OTP.Entities;` `public partial class Otp` with `Verify(string codeHash, DateTimeOffset now) : string?`, `MarkUsed(DateTimeOffset now) : void` and `private static bool HashesMatch(string expected, string actual)`, moved verbatim from `OTP.cs`. Usings: `Core.Errors`, `Core.OTP.Exceptions`, `System.Security.Cryptography`, `System.Text`. New, no Morabh equivalent (Morabh `Core/Core.OTP/Entities/OTP.cs` is a single file). |
| 2 | `api/core-libraries/Core.Spreadsheets/ClosedXmlSpreadsheetReader.Sheets.cs` | partial class | `namespace Core.Spreadsheets;` `public sealed partial class ClosedXmlSpreadsheetReader` with `private const int HeaderRow = 1;` (its comment moves with it), `ReadSheet`, `ReadCells` and `CellText`, moved verbatim. Usings: `ClosedXML.Excel`, `System.Globalization`. New. |
| 3 | `api/Elmanhg.Tests/Core/Otp/OtpDeliveryClaimCancellationTests.cs` | test class | `public sealed class OtpDeliveryClaimCancellationTests`: `private const string Code = "123456";`, `Now`, `_otpSender`, `_otpRepository` (same as `OtpDeliveryClaimTests`). 3 tests (Test plan). |
| 4 | `api/Elmanhg.Tests/Core/Otp/OtpMarkUsedTests.cs` | test class | Moved `MarkUsed_*` ×4 + `Now`. |
| 5 | `api/Elmanhg.Tests/Core/Otp/OtpReissueTests.cs` | test class | Moved: `Reissue_BelowMax_IssuesNewCode`, `Reissue_BelowMax_ReturnsThePreviousState`, `Reissue_DayAfterWindowStartDespiteRecentReissue_ResetsCount`, `Reissue_WithinWindow_KeepsWindowStart`, `Reissue_BelowMax_NextAllowedIsCooldownFromResend` + `Now`. |
| 6 | `api/Elmanhg.Tests/Core/Otp/OtpReissueLimitTests.cs` | test class | Moved: `Reissue_BeforeCooldown_ThrowsRateLimitExceeded`, `Reissue_PastMaxReissueCount_ThrowsRateLimitExceeded`, `Reissue_MaxReissueCountZero_ThrowsOnFirstReissue`, `Reissue_LastAllowedResend_BlocksFromThatResend`, `Reissue_BlockedAfterLastAllowedResend_ThrowsCooldownUntilBlockEnds` + `Now`. |
| 7 | `api/Elmanhg.Tests/Core/Otp/OtpRestoreReissueTests.cs` | test class | Moved: `RestoreReissue_AfterReissue_RestoresThePreviousCodeAndLimits`, `RestoreReissue_AfterWindowReset_RestoresTheOldWindowAndCount`, `Snapshot` helper (with the `OtpEntity` alias) + `Now`. |
| 8 | `api/Elmanhg.Tests/Core/Otp/GenerateOTPHandlerTestBase.cs` | abstract class | `public abstract class GenerateOTPHandlerTestBase` with `protected const string PhoneNumber = "01012345678";`, `protected const string GeneratedCode = "123456";`, `protected static readonly DateTimeOffset Now = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);`, `protected readonly` `_otpRepository`, `_generator`, `_otpHasher`, `_otpSender`, `_timeProvider`, `_handler`, and `protected GenerateOTPHandlerTestBase()` with today's constructor body, verbatim. |
| 9 | `api/Elmanhg.Tests/Core/Otp/GenerateOTPHandlerInsertRaceTests.cs` | test class | `: GenerateOTPHandlerTestBase`. Moved: `Handle_LostInsertRaceWithinCooldown_ThrowsCooldownAndSendsNothing`, `Handle_LostInsertRacePastCooldown_ReissuesTheWinnersRowAndSends`, `Handle_LostInsertRaceAndWinnerGone_ThrowsOtpModifiedConcurrently`. |
| 10 | `api/Elmanhg.Tests/Core/Otp/GenerateOTPHandlerDeliveryTests.cs` | test class | `: GenerateOTPHandlerTestBase`. Moved: `Handle_DeliveryUnavailable_ThrowsAndDeletesTheClaimedRow`, `Handle_ExistingOtp_SavesTheClaimBeforeSending`, `Handle_ExistingOtpDeliveryFails_RestoresThePreviousCodeAndRethrows`. New: 2 tests (Test plan). Alias `AppErrorCodes = Elmanhg.Application.Exceptions.ErrorCodes`. |
| 11 | `api/Elmanhg.Tests/Core/Storage/PublicMediaPipelineTestBase.cs` | abstract class | `public abstract class PublicMediaPipelineTestBase : IDisposable` with `protected static readonly byte[] Bytes`, `protected readonly string _contentRoot`, `protected readonly IFileStorage _fileStorage`, `public void Dispose()`, `protected static FileStorageOptions LocalOptions(string localRootPath = "media")`, `protected static FileStorageOptions S3Options()`, `protected void WriteLocalFile(string folder, string name)` and `protected async Task<HttpContext> SendAsync(FileStorageOptions options, string path)`. Bodies verbatim. |
| 12 | `api/Elmanhg.Tests/Core/Storage/PublicMediaPipelineS3Tests.cs` | test class | `: PublicMediaPipelineTestBase`. Moved: `UseCorePublicMedia_S3_PrivateFolder_Returns404WithoutReading`, `UseCorePublicMedia_S3_PublicKey_StreamsFromStorage`. |
| 13 | `api/Elmanhg.Tests/Application/Features/Dashboard/Shared/DashboardCardQueryCachingTests.cs` | test class | Moved: `DashboardCardQueries` (TheoryData), `DashboardCardQuery_IsCacheableWithoutOwnTtl`, `DashboardCardQuery_UsesDashboardCacheProfile`, `GetMyTeacherStatsQuery_IsNotCacheable`. |
| 14 | `api/Elmanhg.Tests/Integration/Auth/OtpEndpointTestData.cs` | static helper | `public static class OtpEndpointTestData` with `public const string SendRoute = "/api/auth/otp/send";`, `public const string VerifyRoute = "/api/auth/otp/verify";`, `public static async Task<(HttpStatusCode Status, string? Code)> PostAsync(HttpClient client, string route, object body, CancellationToken cancellationToken)`, `public static async Task SeedAsync(ApiFactory factory, Otp otp, CancellationToken cancellationToken)` and `public static async Task<Otp> ReadOtpAsync(ApiFactory factory, string phone, CancellationToken cancellationToken)`. Bodies are today's private helpers with the token as a parameter and `.ConfigureAwait(false)` on every await. |
| 15 | `api/Elmanhg.Tests/Integration/Auth/OtpResendFailureEndpointTests.cs` | test class | `public sealed class OtpResendFailureEndpointTests(ApiFactory factory)`. Moved: `SendOtp_ResendDeliveryFails_KeepsThePreviousCodeAndLimits`, body unchanged except helper calls through `OtpEndpointTestData`. |
| 16 | `api/Elmanhg.Tests/Domain/ReviewSessions/ReviewSessionOpeningCheckTests.cs` | test class | `Lifetime`, `_builder` and `_teacherId` as in `ReviewSessionTests`. Moved: `HasOpened_AfterContentEdit_ReturnsFalse`, `EnsureOpened_Opened_DoesNotThrow`, `EnsureOpened_NotOpened_ThrowsWithQuestionIdContext`, `EnsureOpened_ExpiredSession_ThrowsReviewSessionExpired`. |
| 17 | `api/Elmanhg.Tests/Core/Spreadsheets/SpreadsheetPackageCorruption.cs` | static helper | `public static class SpreadsheetPackageCorruption` with `public static byte[] Corrupt(byte[] content, string corruption)` (moved verbatim from the reader tests), `public static byte[] CorruptPart(byte[] workbook, string corruption)` (a switch: `"workbook-part-not-xml"` → `RewriteEntry(workbook, "xl/workbook.xml", _ => "<workbook")`; `"content-types-not-xml"` → `RewriteEntry(workbook, "[Content_Types].xml", _ => "<Types")`; `"cell-reference-not-a1"` → `RewriteEntry(workbook, "xl/worksheets/sheet1.xml", x => x.Replace("r=\"A2\"", "r=\"!!\"", StringComparison.Ordinal))`; `"workbook-part-missing"` → `RewriteEntry(workbook, "xl/workbook.xml", _ => null)`; `_` → `throw new ArgumentOutOfRangeException(nameof(corruption))`), and `public static byte[] RewriteEntry(byte[] workbook, string entryName, Func<string, string?> rewrite)`. `RewriteEntry` opens `workbook` as a read `ZipArchive` and writes a new `ZipArchive` (`Create`, `leaveOpen: true`) into a `MemoryStream`. It copies every entry other than `entryName` byte for byte (`entry.Open().CopyTo(target.CreateEntry(entry.FullName).Open())`). For `entryName` it reads the text, writes `rewrite(text)` as UTF-8 without a BOM, or skips the entry when the result is `null`. It returns `output.ToArray()` after the target archive is disposed. |
| 18 | `api/Elmanhg.Tests/Core/Spreadsheets/ClosedXmlSpreadsheetReaderMalformedTests.cs` | test class | `AllSheets` and a `_reader` with `UnreadableErrorCode = "PROBE_UNREADABLE"`. Moved: `Read_NotASpreadsheet_ThrowsConfiguredUnreadableCode`, `Read_MalformedZip_ThrowsConfiguredUnreadableCode` (6 rows, now calling `SpreadsheetPackageCorruption.Corrupt`), `Read_NotASpreadsheetWithDefaultOptions_ThrowsSpreadsheetUnreadable`. New: `Read_MalformedWorkbookPart_ThrowsConfiguredUnreadableCode` (Test plan). |
| 19 | `api/Elmanhg.Tests/Core/Spreadsheets/ClosedXmlSpreadsheetReaderLimitsTests.cs` | test class | Moved: `Read_FarCellAtLastColumnAndRow_FinishesFastAndReadsOnlyUpToTheHeader`, `Read_FarCellInTheHeaderRow_CapsTheColumnsRead`, `Read_MoreRowsThanTheCap_StopsOneRowPastTheCapAcrossSheets`, `FarCellWorkbook` + `AllSheets` and `_reader`. |
| 20 | `api/Elmanhg.Tests/Core/Spreadsheets/ClosedXmlSpreadsheetReaderPackageSizeTests.cs` | test class | Moved: `Read_UncompressedSizeOverCap_ThrowsConfiguredUnreadableCode`, `Read_UncompressedSizeWithinCap_ReadsWorkbook`, `Read_NonSeekableStream_ReadsWorkbook`, `Read_NonSeekableStreamOverCompressedCap_ThrowsConfiguredUnreadableCode`, `OneMegabyteCapReader`, `IncompressibleWorkbook` + `AllSheets` and `_reader`. |
| 21 | `api/Elmanhg.Tests/Core/Spreadsheets/SpreadsheetRegistrationTests.cs` | test class | 2 host-start tests (Test plan). Usings: `Core.Spreadsheets`, `FluentAssertions`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Options`. |
| 22 | `api/Elmanhg.Tests/Application/Features/Shared/Observability/LogRedactorBoundaryTests.cs` | test class | 4 theories (Test plan). |
| 23 | `api/Elmanhg.Tests/Application/Features/Shared/Observability/LogRedactorCollectorParityTests.cs` | test class | 3 facts (Test plan) + `private static string ReadCollectorConfig()`: walk up from `AppContext.BaseDirectory` (same loop as `StorageProjectReferencesTests.FindProject`) until `deploy/observability/otel-collector/config.yaml` exists, then `File.ReadAllText`. Throw `FileNotFoundException` if it is never found. Plus `private static string Escaped(string value) => value.Replace(@"\", @"\\", StringComparison.Ordinal).Replace("$", "$$", StringComparison.Ordinal);` and `private static int Occurrences(string text, string value) => text.Split(value).Length - 1;`. |

Nothing else is created. No production type is added (two partial files only), no new exception type, no option, no error code.

## Error codes
None new. Existing codes are reused unchanged: `SPREADSHEET_UNREADABLE` (app; via `SpreadsheetOptions.UnreadableErrorCode`, 400) now also covers `XmlException` and `FormatException` from the package parts. The OTP codes (`OTP_DELIVERY_FAILED`, `OTP_CHANNEL_UNAVAILABLE`, `OTP_REISSUE_COOLDOWN`) are unchanged. No resource strings change.

## Domain behaviour

### 1. `OtpDeliveryClaim` (whole file after the change)
```csharp
using Core.OTP.Delivery;
using Core.OTP.Entities;
using Core.OTP.Repositories;

namespace Core.OTP.GenerateOTP;

// The row is saved before the paid send, so a parallel request loses on the row version and sends nothing. Only a failure while the request is still live hands the claim back: once the request is cancelled the provider may already have accepted the message, so the cooldown and the resend count stay spent.
public sealed class OtpDeliveryClaim(Otp otp, OtpReissueState? previous)
{
    public Otp Otp => otp;

    public async Task<OtpChannel> DeliverAsync(IOtpSender otpSender, IOtpRepository otpRepository, string code, CancellationToken cancellationToken)
    {
        try
        {
            return await otpSender.SendAsync(otp.RecipientType, otp.Recipient, code, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            await ReleaseAsync(otpRepository).ConfigureAwait(false);
            throw;
        }
    }

    // ReleaseAsync body unchanged; its comment becomes:
    // Not the request token: a release that has started must finish even if the client goes away meanwhile.
}
```
`GenerateOTPHandler`, `OtpChannelRouter`, `Otp.Reissue` and `RestoreReissue` behave exactly as before. No `UpdationDate` applies (`Otp` is an `Entity`, not audited).

### 2. `LogRedactor` (whole class body after the change)
```csharp
public const string EmailReplacement = "[redacted-email]";
public const string PhoneReplacement = "[redacted-phone]";

// Egyptian mobile numbers, local (01x) or international (+20 / 20). Separators are allowed only after +, the text start or a character that is not an ASCII letter, digit or underscore, in 4-4 or 1-3-4 groups, so GUIDs and number lists stay intact.
// No \b: .NET counts Arabic letters as word characters and RE2 does not. The ASCII boundaries are captured and written back, and the OTel collector runs this exact string (deploy/observability/otel-collector/config.yaml). ^ and $, not \A and \z: NonBacktracking drops the captures when \z meets a final newline.
public const string PhonePattern = @"(?:(?:\+?20)?0?1[0125][0-9]{8}|(?:\+|(^|[^0-9A-Za-z_]))(?:20[ -]?)?(?:0[ -]?)?1[ -]?[0125](?:[ -]?[0-9]{4}[ -]?[0-9]{4}|[0-9][ -]?[0-9]{3}[ -]?[0-9]{4}))([^0-9A-Za-z_]|$)";

public const string PhoneReplacementPattern = "${1}" + PhoneReplacement + "${2}";

private static readonly Regex PhoneRegex = new(PhonePattern, RegexOptions.NonBacktracking);

// Each match consumes the character after the number, so a number right behind another one is only found by the second pass.
private static readonly TextRedactor Redactor = new([new(RedactionPatterns.EmailAddress, EmailReplacement), new(PhoneRegex, PhoneReplacementPattern), new(PhoneRegex, PhoneReplacementPattern)]);

[return: NotNullIfNotNull(nameof(value))]
public static string? Redact(string? value) => Redactor.Redact(value);
```
`Core.Logging` (`TextRedactor`, `RedactionPatterns`) is unchanged: the phone pattern is app-specific and stays in the app.

### 3. Collector (`deploy/observability/otel-collector/config.yaml`)
Each of the four current phone lines (log `body`, log `attributes`, `span` attributes, `spanevent` attributes) is replaced by **two identical lines**, placed directly after that context's email line. Exact text, copied character for character:
- Log body (×2):
  `          - replace_pattern(body, "(?:(?:\\+?20)?0?1[0125][0-9]{8}|(?:\\+|(^|[^0-9A-Za-z_]))(?:20[ -]?)?(?:0[ -]?)?1[ -]?[0125](?:[ -]?[0-9]{4}[ -]?[0-9]{4}|[0-9][ -]?[0-9]{3}[ -]?[0-9]{4}))([^0-9A-Za-z_]|$$)", "$${1}[redacted-phone]$${2}") where IsString(body)`
- Log attributes, span and spanevent (×2 each, with the indentation of the line being replaced):
  `- replace_all_patterns(attributes, "value", "(?:(?:\\+?20)?0?1[0125][0-9]{8}|(?:\\+|(^|[^0-9A-Za-z_]))(?:20[ -]?)?(?:0[ -]?)?1[ -]?[0125](?:[ -]?[0-9]{4}[ -]?[0-9]{4}|[0-9][ -]?[0-9]{3}[ -]?[0-9]{4}))([^0-9A-Za-z_]|$$)", "$${1}[redacted-phone]$${2}")`

Today the log body statement comes before the attributes email statement, so the log order becomes: `flatten`, body email, body phone ×2, attributes email, attributes phone ×2. The comment above `transform/redact` becomes:
`# Same patterns as LogRedactor in the API: email addresses (literal or URL-encoded @) and Egyptian mobile numbers (docs/observability.md, PII rules). The phone pattern is LogRedactor.PhonePattern with \ doubled (OTTL string) and $ doubled (collector config); it runs twice because a match consumes the character after the number. LogRedactorCollectorParityTests checks the copy. flatten first: replace_all_patterns only reaches top-level values, and Caddy nests the URI under request.`
(Keep the existing line wrapping style: `#` lines of about 120 characters.)

### 4. `SpreadsheetPackageGuard.IsUnreadablePackage`
See "Existing code touched". With it, a valid zip whose part XML is broken (`XmlException`) or whose cell reference is malformed (`FormatException`) → `BadRequestCoreException(UnreadableErrorCode, innerException: exception)` → 400 `SPREADSHEET_UNREADABLE`. `NullReferenceException` is still not caught (D13).

## API surface
No route, policy, request or response changes. `POST /api/auth/otp/send` and `/resend` keep their contracts. The only observable difference is that a client-cancelled send leaves the claim in place, so the next send within 60 s gets 429 `OTP_REISSUE_COOLDOWN`. The question-import endpoints return 400 `SPREADSHEET_UNREADABLE` instead of 500 for corrupt part XML or cell references.

## Test plan
Conventions: xUnit v3, FluentAssertions 7, NSubstitute, `TestContext.Current.CancellationToken` everywhere except where a test owns a `CancellationTokenSource`. The sender stub that cancels "after acceptance" is written exactly like this:
`_otpSender.SendAsync(Arg.Any<OtpRecipientType>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ => { cancellation.Cancel(); return Task.FromException<OtpChannel>(new OperationCanceledException(cancellation.Token)); });`
(This models the provider returning 2xx while the request token is cancelled during the response read. The "accepts and returns" variant returns `Task.FromResult(OtpChannel.Sms)` after `Cancel()`.)

### New tests
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 1 | `OtpDeliveryClaimTests` | `DeliverAsync_OperationCanceledWhileRequestLive_ReleasesAndRethrows` | `SendThrows(new OperationCanceledException())` with a new-row claim and the live `TestContext` token → `ThrowAsync<OperationCanceledException>()`; `_otpRepository.Received(1).Delete(otp)`; `SaveChangesAsync(CancellationToken.None)` `Received(1)`. |
| 2 | `OtpDeliveryClaimTests` | `DeliverAsync_UnexpectedExceptionWhileRequestLive_ReleasesAndRethrows` | `SendThrows(new InvalidOperationException("probe"))`, reissued claim (`previous = otp.Reissue("next-hash", 5, Now.AddMinutes(2))`) → `ThrowAsync<InvalidOperationException>()`; `otp.ReissueCount` is 0 and `otp.VerificationId` equals the original; `Delete` `DidNotReceive`; `SaveChangesAsync(CancellationToken.None)` `Received(1)`. |
| 3 | `OtpDeliveryClaimCancellationTests` | `DeliverAsync_SendCancelledByTheRequest_KeepsTheClaimAndRethrows` (replaces the deleted test, D4) | Token cancelled before the call, `SendThrows(new OperationCanceledException(cancellation.Token))`, new-row claim → `ThrowAsync<OperationCanceledException>()`; `Delete` `DidNotReceive`; `SaveChangesAsync(Arg.Any<CancellationToken>())` `DidNotReceive`. |
| 4 | `OtpDeliveryClaimCancellationTests` | `DeliverAsync_RequestCancelledAfterProviderAccepts_ReturnsTheChannelAndKeepsTheClaim` | The stub cancels, then returns `OtpChannel.Sms` → the result is `Sms`; `cancellation.IsCancellationRequested` is true; `Delete` `DidNotReceive`; `SaveChangesAsync(Arg.Any)` `DidNotReceive`. |
| 5 | `OtpDeliveryClaimCancellationTests` | `DeliverAsync_RequestCancelledAfterProviderAcceptsThenSendThrows_KeepsTheReissuedClaim` | Reissued claim (`otp.Reissue("next-hash", 5, Now.AddMinutes(2))`), the stub cancels then throws → `ThrowAsync<OperationCanceledException>()`; `otp.ReissueCount` is 1; `otp.CodeHash` is `"next-hash"`; `otp.VerificationId` is not the original; `SaveChangesAsync(Arg.Any)` `DidNotReceive`. |
| 6 | `GenerateOTPHandlerDeliveryTests` | `Handle_RequestCancelledAfterProviderAccepts_KeepsTheNewRowAndRethrows` | New phone, the stub cancels then throws, handler called with `cancellation.Token` → `ThrowAsync<OperationCanceledException>()`; `AddIfAbsentAsync` `Received(1)`; `Delete` `DidNotReceive`; `SaveChangesAsync(Arg.Any)` `DidNotReceive`. |
| 7 | `GenerateOTPHandlerDeliveryTests` | `Handle_RequestCancelledAfterResendAccepted_KeepsTheReissuedRowAndRethrows` | Existing row issued at `Now.AddMinutes(-2)`, the stub cancels then throws → `ThrowAsync<OperationCanceledException>()`; `existing.ReissueCount` is 1; `existing.VerificationId` is not the original; `existing.NextAllowedReissueAt` is `Now.AddSeconds(60)`; `SaveChangesAsync(Arg.Any)` `Received(1)` (the claim only). |
| 8 | `LogRedactorBoundaryTests` | `Redact_ArabicTextAdjacentToNumber_ReplacesNumberOnly(string text, string expected)` | Rows: `("رقم010 1234 5678", "رقم[redacted-phone]")`, `("رقم01012345678", "رقم[redacted-phone]")`, `("010 1234 5678رقم", "[redacted-phone]رقم")`, `("01012345678رقم", "[redacted-phone]رقم")`, `("اتصل على 010 1234 5678 الآن", "اتصل على [redacted-phone] الآن")`. `LogRedactor.Redact(text).Should().Be(expected)`. |
| 9 | `LogRedactorBoundaryTests` | `Redact_NumbersOneSeparatorApart_ReplacesEach(string text, string expected)` | Rows: `("call 010 1234 5678 011 1234 5678", "call [redacted-phone] [redacted-phone]")`, `("call 01012345678,01112345678", "call [redacted-phone],[redacted-phone]")`, `("call 010-1234-5678,011-1234-5678,012-1234-5678", "call [redacted-phone],[redacted-phone],[redacted-phone]")`. |
| 10 | `LogRedactorBoundaryTests` | `Redact_NumberFollowedByPunctuation_KeepsTheFollowingCharacter(string text, string expected)` | Rows: `("call 010 1234 5678\n", "call [redacted-phone]\n")`, `("call 01012345678\r\n", "call [redacted-phone]\r\n")`, `("tel:+201012345678;", "tel:[redacted-phone];")`. |
| 11 | `LogRedactorBoundaryTests` | `Redact_TimestampsAndLongerDigitRuns_ReturnsUnchanged(string text)` | Rows: `"at 2026-10-07T08:00:00.123Z"`, `"order 1012345678901"`, `"order 101 2345 67890"`, `"user 3f2a1b10-1012-3456-9abc-def012345678"`. `Redact(text).Should().Be(text)`. |
| 12 | `LogRedactorCollectorParityTests` | `CollectorConfig_PhoneStatements_RunTheLogRedactorPatternTwiceInEveryContext` | `Occurrences(config, Escaped(LogRedactor.PhonePattern))` is 8 and `Occurrences(config, Escaped(LogRedactor.PhoneReplacementPattern))` is 8. |
| 13 | `LogRedactorCollectorParityTests` | `CollectorConfig_EmailStatements_UseTheSharedEmailPattern` | `Occurrences(config, Escaped(RedactionPatterns.EmailAddress.ToString()))` is 4. |
| 14 | `LogRedactorCollectorParityTests` | `CollectorConfig_PhoneStatements_HaveNoWordBoundary` | `config.Should().NotContain(@"\\b")`. |
| 15 | `ClosedXmlSpreadsheetReaderMalformedTests` | `Read_MalformedWorkbookPart_ThrowsConfiguredUnreadableCode(string corruption, Type innerExceptionType)` | Rows: `("workbook-part-not-xml", typeof(XmlException))`, `("content-types-not-xml", typeof(XmlException))`, `("cell-reference-not-a1", typeof(FormatException))`, `("workbook-part-missing", typeof(InvalidOperationException))`. Input: `SpreadsheetPackageCorruption.CorruptPart(new QuestionWorkbookBuilder().Sheet("Mcq", ["stem"], ["one"]).Build(), corruption)`. Assert `BadRequestCoreException` whose `ErrorCode` is `"PROBE_UNREADABLE"` and whose `InnerException` is exactly of type `innerExceptionType` (`.BeOfType(innerExceptionType)`). The first three rows fail on today's filter (500 path), and every row fails with an `InvalidDataException`-only catch. |
| 16 | `SpreadsheetRegistrationTests` | `AddCoreSpreadsheets_ZeroUncompressedCap_FailsHostStart` | `Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings())`; `builder.Services.AddCoreSpreadsheets(options => options.MaxUncompressedSizeInMb = 0)`; `using var host = builder.Build();` → `host.StartAsync(token)` throws `OptionsValidationException` with message `*MaxUncompressedSizeInMb must be greater than 0.*`. No `IOptions` is resolved before `StartAsync`. |
| 17 | `SpreadsheetRegistrationTests` | `AddCoreSpreadsheets_DefaultCaps_HostStarts` | Same builder with `_ => { }` → `await host.StartAsync(token)` completes; `host.Services.GetRequiredService<IOptions<SpreadsheetOptions>>().Value.MaxUncompressedSizeInMb` is 100; `await host.StopAsync(token)`. |
| 18 | `SpreadsheetCompositionTests` | `Start_ZeroUncompressedCap_FailsOptionsValidation` | `await using var misconfigured = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.PostConfigure<SpreadsheetOptions>(options => options.MaxUncompressedSizeInMb = 0)));` → `misconfigured.CreateClient()` throws `OptionsValidationException` with message `*MaxUncompressedSizeInMb must be greater than 0.*`. |
| 19 | `SpreadsheetCompositionTests` | `Start_ZeroCompressedCap_FailsOptionsValidation` | Same with `MaxCompressedSizeInMb = 0` → message `*MaxCompressedSizeInMb must be greater than 0.*`. |

### Deleted (intentional, D4)
| Test class | Test method | Why |
|---|---|---|
| `OtpDeliveryClaimTests` | `DeliverAsync_SendCancelled_ReleasesWithoutTheCancelledToken` | Its behaviour is reversed by this story. Replaced by row 3. |

### T-map: moved tests (bodies unchanged except helper call sites)
| From | To | Methods |
|---|---|---|
| `OtpTests` | `OtpTests` (stays) | `Create_EmailRecipient_KeepsRecipientAndType`, `Create_GivenNow_StampsTimesFromNow`, `Verify_MatchingHash_MarksVerified`, `Verify_DifferentHashSameLength_ReturnsNotMatched`, `Verify_DifferentLengthHash_ReturnsNotMatched`, `Verify_ExpiredAtGivenTime_ReturnsExpired` |
| `OtpTests` | `OtpMarkUsedTests` | `MarkUsed_NotVerified_ThrowsBadRequest`, `MarkUsed_AlreadyUsed_ThrowsBadRequest`, `MarkUsed_Verified_SetsIsUsed`, `MarkUsed_ExpiredAtGivenTime_ThrowsExpired` |
| `OtpTests` | `OtpReissueTests` / `OtpReissueLimitTests` / `OtpRestoreReissueTests` | as listed in Files to create 5–7 |
| `GenerateOTPHandlerTests` | `GenerateOTPHandlerTests` (stays) | `Handle_NewPhone_CreatesOtpAndSendsGeneratedCode`, `Handle_NewEmail_CreatesEmailOtpWithNormalizedRecipient`, `Handle_ExistingOtpInCooldown_ThrowsRateLimitAndSendsNothing`, `Handle_NewPhone_StampsTimesFromTimeProvider`, `Handle_ExistingOtpPastCooldown_ReissuesWithoutAdding` |
| `GenerateOTPHandlerTests` | `GenerateOTPHandlerInsertRaceTests` / `GenerateOTPHandlerDeliveryTests` | as listed in Files to create 9–10 |
| `PublicMediaPipelineTests` | `PublicMediaPipelineTests` (stays) | the 6 `UseCorePublicMedia_Local*` tests |
| `PublicMediaPipelineTests` | `PublicMediaPipelineS3Tests` | the 2 `UseCorePublicMedia_S3_*` tests |
| `DashboardCachingTests` | `DashboardCachingTests` (stays) | the 6 `AddApplication_*` tests |
| `DashboardCachingTests` | `DashboardCardQueryCachingTests` | `DashboardCardQuery_IsCacheableWithoutOwnTtl`, `DashboardCardQuery_UsesDashboardCacheProfile`, `GetMyTeacherStatsQuery_IsNotCacheable` |
| `OtpConcurrencyEndpointTests` | `OtpConcurrencyEndpointTests` (stays) | `SendOtp_ParallelFirstSends_DeliversOneCodeAndStoresOneRow`, `SendOtp_ParallelResendsPastCooldown_DeliversOneCodeAndCountsOneResend`, `VerifyOtp_ParallelWrongCodes_CountsEveryAnsweredAttempt` |
| `OtpConcurrencyEndpointTests` | `OtpResendFailureEndpointTests` | `SendOtp_ResendDeliveryFails_KeepsThePreviousCodeAndLimits` |
| `ReviewSessionTests` | `ReviewSessionTests` (stays) | `Start_SetsTeacherCreatorAndExpiry`, `RecordOpening_ActiveSession_AddsOpeningAtCurrentVersion`, `RecordOpening_ActiveSession_LeavesUpdatedByToTheSave`, `RecordOpening_SameVersionTwice_AddsOnce`, `RecordOpening_ExpiredSession_ThrowsReviewSessionExpired` |
| `ReviewSessionTests` | `ReviewSessionOpeningCheckTests` | as listed in Files to create 16 |
| `ClosedXmlSpreadsheetReaderTests` | `…MalformedTests` / `…LimitsTests` / `…PackageSizeTests` | as listed in Files to create 18–20 |
| `RepositoryAuditStampingTests` | `AuditStampingInterceptorTests` | all methods, renamed class only |
| `RepositoryAuditStampingModifiedTests` | `AuditStampingInterceptorModifiedTests` | all methods, renamed class only |

Existing `LogRedactorTests` stays as it is (every row is still green under the new pattern; verified, D8).

## Docs
| Doc | Section | New text (replace the stated sentence) |
|---|---|---|
| `docs/otp-delivery.md` | line 16, after "…so the 60 s resend cooldown does not start and the previous code still works." | Insert: "Only a failure reported while the request is still live hands the claim back. If the client cancels the request (disconnects) during or after the provider call, the claim stays: the provider may already have accepted the message, so the cooldown and the resend count are spent and the new code stands." |
| `docs/observability.md` | §12, item 2 | Replace "…such as `010 1234 5678` or `+20 10 1234 5678`); linear-time regexes)" with "…such as `010 1234 5678` or `+20 10 1234 5678`). A spaced number must start after `+`, the start of the text or a character that is not an ASCII letter, digit or underscore, and every number must end before such a character or the end of the text, so Arabic letters right next to a number do not stop the match. The phone rule runs twice so back-to-back numbers are both masked; linear-time regexes)". |
| `docs/observability.md` | §12, item 3 | Append: "The phone pattern is the exact `LogRedactor.PhonePattern` string (with `\` and `$` doubled for OTTL and the collector config), run twice per context; `LogRedactorCollectorParityTests` fails if the two copies drift." |

No other doc changes: `docs/question-import.md` already says "not a readable workbook → 400 `SPREADSHEET_UNREADABLE`" (the code now matches it), and `constitution.md` and `SKILL.md` need no change (D19). `docs/backlog.json` is left to the orchestrator.

## Definition of done
- [ ] `OtpDeliveryClaim.DeliverAsync` uses `catch (Exception) when (!cancellationToken.IsCancellationRequested)`; the release still saves with `CancellationToken.None`; both WHY comments updated.
- [ ] `GenerateOTPHandler.cs`, `OtpChannelRouter.cs`, `CoreDbContext` and `Core.Notifications` are byte-identical to `main`; no migration added; no package added.
- [ ] `LogRedactor.PhonePattern` equals the string in "Domain behaviour §2" character for character; `RegexOptions.NonBacktracking`; the phone rule is listed twice; `PhoneReplacementPattern` is `"${1}[redacted-phone]${2}"`.
- [ ] The collector config has 8 phone statements in exactly the text of §3, 4 email statements unchanged, and no `\\b`.
- [ ] `SpreadsheetPackageGuard.IsUnreadablePackage` exists with the 6 types; both catch sites use it; `System.IO.Packaging` is no longer imported by either file.
- [ ] `OTP.cs`, `OTP.Reissue.cs`, `OTP.Verification.cs`, `ClosedXmlSpreadsheetReader.cs` and `ClosedXmlSpreadsheetReader.Sheets.cs` are each 100 lines or fewer, with moved bodies unchanged.
- [ ] Every test file created or changed by this story is 100 lines or fewer (`wc -l`), except where it was already over and was not split (none expected).
- [ ] Test-name check: the sorted set of `[Fact]`/`[Theory]` method names in `api/Elmanhg.Tests` on `main` minus the set after the change is exactly `{DeliverAsync_SendCancelled_ReleasesWithoutTheCancelledToken}`, and the added names are exactly rows 1–19 of the new-test table.
- [ ] `RepositoryAuditStamping` appears nowhere in `api/` (`git grep`).
- [ ] All 19 new tests exist with the listed names and assertions; theory rows exactly as listed.
- [ ] `dotnet build api/Elmanhg.slnx -c Release` passes with zero warnings (TreatWarningsAsErrors).
- [ ] `dotnet test --project api/Elmanhg.Tests` passes in full (Docker available). Report the totals.
- [ ] No code comment contains `#` followed by a number; no `// TODO`; no new `try`/`catch` outside `OtpDeliveryClaim` and the two existing spreadsheet sites.
- [ ] `docs/otp-delivery.md` and `docs/observability.md` updated as in Docs; no other doc touched.
- [ ] The report lists, as possible follow-ups (not blocking): Arabic-Indic digits not redacted (D9b); the tail match on long digit runs (D9a); the ClosedXML `NullReferenceException` on missing OPC parts (D13).
