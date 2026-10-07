VERDICT: APPROVED

# Review — E21.S14 Small leftovers: OTP abort after send, redaction boundary, test sizes

## Blocking
None.

## Non-blocking
- `api/Elmanhg.Tests/Application/Features/Shared/Observability/LogRedactorCollectorParityTests.cs:17-18` — the parity test counts 8 occurrences across the whole file; it does not prove two per context (a 3/1/2/2 layout would pass). Today the YAML parses as 2/2/2/2 (log body, log attributes, span, spanevent), so this is only a weaker guard than the comment implies.
- `api/Elmanhg.Tests/Core/Otp/OtpDeliveryClaimCancellationTests.cs:36` (`DeliverAsync_RequestCancelledAfterProviderAccepts_ReturnsTheChannelAndKeepsTheClaim`) — the success path never released, before or after this change, so this test also passes on the old catch-all. The implementation report says 3 claim cancellation tests failed under mutation; only rows 3 and 5 can fail, so that is an over-count. Row 4 is still useful as a regression pin.
- `api/core-libraries/Core.Spreadsheets/SpreadsheetPackageGuard.cs:62` — `FormatException` and `XmlException` are broad. This was accepted in plan D12, and the filter only wraps the `XLWorkbook` constructor and the guard zip scan.
- The follow-ups the report lists (Arabic-Indic digits, tail match on long digit runs, ClosedXML `NullReferenceException` on missing OPC parts) are real and out of scope.

## Verified
- **OTP claim (D1-D4).**
  - `OtpDeliveryClaim.cs:18` is `catch (Exception) when (!cancellationToken.IsCancellationRequested)`, and the release still saves with `CancellationToken.None` (`:37`).
  - Polly and transport failures still roll back. `HttpFailureExtensions.IsTransientFailure` maps `HttpRequestException`, `ExecutionRejectedException` (timeout or open circuit) and an `OperationCanceledException` without request cancellation, so none of them set the request token. `OtpChannelRouter.Resolve` throws `ServiceUnavailableCoreException` while the request is still live.
  - Absent from the diff: `GenerateOTPHandler`, `OtpChannelRouter`, `CoreDbContext`/`Core.EntityFrameworkCore`, `Core.Notifications`, and Infrastructure/migrations.
  - Postman is untouched, which is correct: no endpoint or contract changed.
  - The only added `catch` lines are the claim filter and the two spreadsheet sites.
  - The replaced test is a true replacement. The old `DeliverAsync_SendCancelled_ReleasesWithoutTheCancelledToken` (HEAD `OtpDeliveryClaimTests.cs:67-78`) has the same arrange as the new `OtpDeliveryClaimCancellationTests.DeliverAsync_SendCancelledByTheRequest_KeepsTheClaimAndRethrows`, with inverted assertions (Delete and SaveChangesAsync DidNotReceive).
- **Redaction (D5-D10).**
  - I extracted the C# verbatim pattern and parsed `config.yaml` with PyYAML. After confmap unescaping (double dollar to dollar) and OTTL unescaping (double backslash to backslash), all 8 phone statements are byte-equal to `LogRedactor.PhonePattern`.
  - The replacement is `${1}[redacted-phone]${2}` in all 8. The 4 email statements are unchanged.
  - Statement order and structure match the plan, the YAML loads, and no backslash-b remains.
  - Two passes in both places: `LogRedactor.cs:21` lists the rule twice, and each collector context has two identical lines.
  - I ran 35 inputs, each plain and with LF and CRLF endings (105 cases), with two passes, through .NET NonBacktracking, .NET backtracking (dotnet fsi), google-re2 1.1 and Python re (end anchor emulated as end-of-text). All four agree on all 105 cases.
  - The inputs were every LogRedactorTests and LogRedactorBoundaryTests string, plus:
    - Arabic text adjacent on either side, including Arabic before `+201012345678`
    - GUIDs, the score list and timestamps
    - three back-to-back spaced and unspaced numbers
    - `x` or `_` before a spaced number, which is correctly not matched
  - The parity test really compares the two copies: it escapes the C# constants and counts them in the repo file. CI runs `dotnet test api/` from the checkout, so `deploy/` is reachable.
- **Spreadsheet (D11-D14).**
  - `IsUnreadablePackage` covers InvalidDataException, FormatException, XmlException, OpenXmlPackageException, ArgumentException and InvalidOperationException. NullReferenceException is not in the list.
  - Both catch sites use it (`ClosedXmlSpreadsheetReader.cs:20`, `SpreadsheetPackageGuard.cs:54`), and `System.IO.Packaging` is gone from both files.
  - Both throw `BadRequestCoreException(UnreadableErrorCode)`, which becomes 400 `SPREADSHEET_UNREADABLE` in the app.
  - The validator tests are real. `AddCoreSpreadsheets` calls `.ValidateOnStart()` (`DependencyInjection.cs:10`), and the core test resolves nothing before `StartAsync`. The app tests use `PostConfigure` and check that `CreateClient()` throws with the specific message.
- **Splits (D15-D18).**
  - Test-name multiset (public test methods): 5107 at HEAD, 5125 in the working tree. Removed: exactly `DeliverAsync_SendCancelled_ReleasesWithoutTheCancelledToken`. Added: exactly the 19 planned names.
  - Attribute counts: Fact +13 (14 added, 1 removed), Theory +5, InlineData +19 (15 boundary rows and 4 malformed-part rows). MemberData is unchanged, so no row was dropped in a move.
  - Every removed test line reappears in a new file, apart from the expected helper-signature, visibility and class-name changes.
  - The production partials are pure moves. `OTP.Reissue.cs` (ReissueWindow, Reissue), `OTP.Verification.cs` and `ClosedXmlSpreadsheetReader.Sheets.cs` match the removed hunks line for line.
  - All touched files are 100 lines or fewer, except `AuditStampingInterceptorModifiedTests.cs`: 102 lines, rename only, and disclosed in the report.
  - `RepositoryAuditStamping` no longer appears under `api/`.
- **Docs sync.** `docs/otp-delivery.md:16` and `docs/observability.md` section 12 items 2-3 match the code. `docs/question-import.md:94` already promised `SPREADSHEET_UNREADABLE`, and the code now agrees. No divergence.
- **Build and tests.**
  - No new comment refers to a number with `#`.
  - `dotnet build api/Elmanhg.Tests -c Release`: 0 warnings, 0 errors.
  - I ran the affected namespaces with Docker up: Core.Otp, Core.Spreadsheets, Core.Storage, Core.Persistence, Observability, Dashboard.Shared, Domain.ReviewSessions, Integration.Auth and Integration.Composition. 393 passed, 0 failed.
  - I did not re-run the full suite.
- **Deviations.** Both accepted: the RewriteEntry stream disposal is needed for a correct zip, and `using static` has repo precedent.

## Test quality
- OtpDeliveryClaimTests: the new live-request rows assert Received(1) release with CancellationToken.None, so they constrain the filter.
- OtpDeliveryClaimCancellationTests: rows 3 and 5 fail on the old catch-all. Row 4 does not (non-blocking above).
- GenerateOTPHandlerDeliveryTests: the two new rows constrain end-to-end behaviour through the real claim.
- LogRedactorBoundaryTests: these fail on the old pattern (Arabic rows) and on a single pass (adjacent rows).
- LogRedactorCollectorParityTests: these catch real drift; the per-context count check is weak.
- ClosedXmlSpreadsheetReaderMalformedTests.Read_MalformedWorkbookPart: asserts the exact inner exception type per row, so it constrains the widened filter.
- SpreadsheetRegistrationTests and SpreadsheetCompositionTests: these fail if ValidateOnStart or the validator is removed.
