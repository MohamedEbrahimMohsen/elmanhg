# Implementation — E21.S14 Small leftovers: OTP abort after send, redaction boundary, test sizes

## Files created
| Path | Lines | Purpose |
|---|---|---|
| api/core-libraries/Core.OTP/Entities/OTP.Verification.cs | 60 | `Verify`, `MarkUsed`, `HashesMatch` moved verbatim from OTP.cs |
| api/core-libraries/Core.Spreadsheets/ClosedXmlSpreadsheetReader.Sheets.cs | 57 | `HeaderRow` (with comment), `ReadSheet`, `ReadCells`, `CellText` moved verbatim |
| api/Elmanhg.Tests/Core/Otp/OtpDeliveryClaimCancellationTests.cs | 68 | 3 cancellation tests (rows 3-5; row 3 replaces the deleted test) |
| api/Elmanhg.Tests/Core/Otp/OtpMarkUsedTests.cs | 53 | moved `MarkUsed_*` x4 |
| api/Elmanhg.Tests/Core/Otp/OtpReissueTests.cs | 75 | moved 5 `Reissue_*` tests |
| api/Elmanhg.Tests/Core/Otp/OtpReissueLimitTests.cs | 67 | moved 5 `Reissue_*` limit tests |
| api/Elmanhg.Tests/Core/Otp/OtpRestoreReissueTests.cs | 43 | moved 2 `RestoreReissue_*` + `Snapshot` |
| api/Elmanhg.Tests/Core/Otp/GenerateOTPHandlerTestBase.cs | 37 | shared fields + ctor (verbatim) |
| api/Elmanhg.Tests/Core/Otp/GenerateOTPHandlerInsertRaceTests.cs | 54 | moved 3 insert-race tests |
| api/Elmanhg.Tests/Core/Otp/GenerateOTPHandlerDeliveryTests.cs | 91 | moved 3 delivery tests + new rows 6-7 |
| api/Elmanhg.Tests/Core/Storage/PublicMediaPipelineTestBase.cs | 63 | fields, `Dispose`, helpers (verbatim) |
| api/Elmanhg.Tests/Core/Storage/PublicMediaPipelineS3Tests.cs | 29 | moved 2 S3 tests |
| api/Elmanhg.Tests/Application/Features/Dashboard/Shared/DashboardCardQueryCachingTests.cs | 57 | moved TheoryData + 3 query-shape tests |
| api/Elmanhg.Tests/Integration/Auth/OtpEndpointTestData.cs | 36 | routes + `PostAsync`/`SeedAsync`/`ReadOtpAsync` with token param and `.ConfigureAwait(false)` |
| api/Elmanhg.Tests/Integration/Auth/OtpResendFailureEndpointTests.cs | ~45 | moved `SendOtp_ResendDeliveryFails_KeepsThePreviousCodeAndLimits` |
| api/Elmanhg.Tests/Domain/ReviewSessions/ReviewSessionOpeningCheckTests.cs | 62 | moved `HasOpened_*` + `EnsureOpened_*` x3 |
| api/Elmanhg.Tests/Core/Spreadsheets/SpreadsheetPackageCorruption.cs | 67 | `Corrupt` (moved), `CorruptPart`, `RewriteEntry` |
| api/Elmanhg.Tests/Core/Spreadsheets/ClosedXmlSpreadsheetReaderMalformedTests.cs | 66 | moved 3 tests + new row 15 |
| api/Elmanhg.Tests/Core/Spreadsheets/ClosedXmlSpreadsheetReaderLimitsTests.cs | 75 | moved 3 tests + `FarCellWorkbook` |
| api/Elmanhg.Tests/Core/Spreadsheets/ClosedXmlSpreadsheetReaderPackageSizeTests.cs | 92 | moved 4 tests + `OneMegabyteCapReader`, `IncompressibleWorkbook` |
| api/Elmanhg.Tests/Core/Spreadsheets/SpreadsheetRegistrationTests.cs | 35 | rows 16-17 (host start, core level) |
| api/Elmanhg.Tests/Application/Features/Shared/Observability/LogRedactorBoundaryTests.cs | 47 | rows 8-11 |
| api/Elmanhg.Tests/Application/Features/Shared/Observability/LogRedactorCollectorParityTests.cs | 57 | rows 12-14 + YAML read |

## Files modified
| Path | Change |
|---|---|
| api/core-libraries/Core.OTP/GenerateOTP/OtpDeliveryClaim.cs | `catch (Exception) when (!cancellationToken.IsCancellationRequested)`; both WHY comments updated as planned; release still saves with `CancellationToken.None` |
| api/core-libraries/Core.OTP/Entities/OTP.cs | 149 → 53 lines; only `using Core.DDD.Entities;`, properties, ctor, `Create` |
| api/core-libraries/Core.OTP/Entities/OTP.Reissue.cs | + usings, `ReissueWindow` (with comment) and `Reissue` above `RestoreReissue` (64 lines) |
| api/core-libraries/Core.Spreadsheets/ClosedXmlSpreadsheetReader.cs | `sealed partial`, `Read` only, catch uses `SpreadsheetPackageGuard.IsUnreadablePackage`; Packaging usings removed (44 lines) |
| api/core-libraries/Core.Spreadsheets/SpreadsheetPackageGuard.cs | `IsUnreadablePackage` (6 types); catch uses it; `System.IO.Packaging` → `System.Xml` |
| api/Elmanhg.Application/Shared/Observability/LogRedactor.cs | `public const PhonePattern` / `PhoneReplacementPattern` exactly as plan §2, `PhoneRegex` NonBacktracking, phone rule listed twice |
| deploy/observability/otel-collector/config.yaml | 4 phone statements → 8 (two identical per context, text exactly as plan §3); comment above `transform/redact` updated |
| api/Elmanhg.Tests/Core/Otp/OtpDeliveryClaimTests.cs | deleted `DeliverAsync_SendCancelled_ReleasesWithoutTheCancelledToken` (D4), added rows 1-2 (97 lines) |
| api/Elmanhg.Tests/Core/Otp/OtpTests.cs | keeps `Create_*` x2, `Verify_*` x4 (76 lines) |
| api/Elmanhg.Tests/Core/Otp/GenerateOTPHandlerTests.cs | `: GenerateOTPHandlerTestBase`, 5 tests (71 lines) |
| api/Elmanhg.Tests/Core/Storage/PublicMediaPipelineTests.cs | `: PublicMediaPipelineTestBase`, 6 Local tests (76 lines) |
| api/Elmanhg.Tests/Application/Features/Dashboard/Shared/DashboardCachingTests.cs | 6 `AddApplication_*` + `BuildProvider` (84 lines) |
| api/Elmanhg.Tests/Integration/Auth/OtpConcurrencyEndpointTests.cs | 3 parallel tests + `SendInParallelAsync`, helpers via `using static ...OtpEndpointTestData` |
| api/Elmanhg.Tests/Domain/ReviewSessions/ReviewSessionTests.cs | `Start_*` + `RecordOpening_*` x4 (74 lines); unused `Elmanhg.Domain.Questions` using dropped |
| api/Elmanhg.Tests/Core/Spreadsheets/ClosedXmlSpreadsheetReaderTests.cs | 3 kept tests + `AllSheets`, `_reader` (57 lines) |
| api/Elmanhg.Tests/Integration/Composition/SpreadsheetCompositionTests.cs | rows 18-19 |
| api/Elmanhg.Tests/Core/Persistence/RepositoryAuditStampingTests.cs → AuditStampingInterceptorTests.cs | `git mv` + class rename only |
| api/Elmanhg.Tests/Core/Persistence/RepositoryAuditStampingModifiedTests.cs → AuditStampingInterceptorModifiedTests.cs | `git mv` + class rename only |
| docs/otp-delivery.md | line 16: claim kept after client cancellation |
| docs/observability.md | §12 items 2 and 3 as planned |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| `RewriteEntry` copies with `entry.Open().CopyTo(target.CreateEntry(entry.FullName).Open())` | Same behaviour, but that one-liner leaves both entry streams undisposed, and the zip writer needs the entry stream disposed to finish the entry | Copied with `using var from/to` and then `CopyTo`. Same bytes, same result |
| `OtpConcurrencyEndpointTests` / `OtpResendFailureEndpointTests` "use `OtpEndpointTestData`" | No call style was given | Used `using static Elmanhg.Tests.Integration.Auth.OtpEndpointTestData;` (the repo already uses this pattern in `CreateQuestionHandlerTests` and others), so call sites change only by the added `factory` and token arguments |

There are no other deviations. The single test name not kept is `DeliverAsync_SendCancelled_ReleasesWithoutTheCancelledToken`. This is intended (D4): the behaviour is reversed on purpose. `OtpDeliveryClaimCancellationTests.DeliverAsync_SendCancelledByTheRequest_KeepsTheClaimAndRethrows` replaces it, with the same arrange and inverted assertions.

## Build & test
- `dotnet build api/Elmanhg.slnx -c Release`: **Build succeeded, 0 errors.** The first full compile listed 9 warnings, all CS8602/CS8618 nullable warnings in vendored `Core.Validation`, `Core.Notifications` and the unchanged `Otp` private ctor. `api/core-libraries/Directory.Build.props` exempts these projects from TreatWarningsAsErrors, and all 9 were already there on `main`; this change adds none. The incremental `dotnet build api/ -c Release` reports 0 warnings and 0 errors.
- OpenAPI diff (as CI does it): `git status --porcelain api/openapi` after the build printed nothing, so OpenAPI is unchanged.
- `dotnet ef migrations has-pending-model-changes --project api/Elmanhg.Infrastructure --startup-project api/Elmanhg.Api --configuration Release --no-build`: "No changes have been made to the model since the last migration."
- `dotnet test api/ -c Release --no-build` (Docker up): **Passed! total 5683, failed 0, succeeded 5683, skipped 0.**
- Mutation check (temporary, reverted, then rebuilt and the full suite re-run green as above). I put back the old catch-all in `OtpDeliveryClaim`, the old exception list without `FormatException`/`XmlException`, and a single phone pass. 9 new test cases failed:
  - both handler cancellation tests
  - 3 claim cancellation tests
  - 3 malformed-part rows: `workbook-part-not-xml`, `content-types-not-xml`, `cell-reference-not-a1`
  - 2 adjacent-number rows
- Test-name check against `main`: removed = `{DeliverAsync_SendCancelled_ReleasesWithoutTheCancelledToken}`; added = exactly the 19 names of the new-test table.
- `git grep RepositoryAuditStamping -- api/`: no hits.
- Byte-identical to `main`: `GenerateOTPHandler.cs`, `OtpChannelRouter.cs`, `Core.EntityFrameworkCore` (CoreDbContext), `Core.Notifications`, Infrastructure context and migrations.
- No `#<number>` in any changed code. No new `try`/`catch`.

## Notes for review
- `AuditStampingInterceptorModifiedTests.cs` is 102 lines. It was 102 before; the plan only renames it (D18), so I did not split it.
- `PublicMediaPipelineTestBase.SendAsync` keeps its awaits without `.ConfigureAwait(false)`, because the plan said "bodies verbatim" and the original private helper had none. `OtpEndpointTestData` adds them as the plan asked.
- The base classes expose `protected readonly _field` names, following the `RefreshTokenRotatorTestBase` precedent.
- `Read_MalformedWorkbookPart_ThrowsConfiguredUnreadableCode` asserts the exact inner exception type per row. This depends on ClosedXML's current exception behaviour for each corruption; it is green on the pinned version.
- Possible follow-ups (not blocking):
  - Arabic-Indic digits are not redacted by either engine (D9b).
  - The unseparated branch masks the tail of longer digit runs (`1791234567890` → `179[redacted-phone]`, D9a).
  - ClosedXML's `NullReferenceException` on missing OPC parts (`[Content_Types].xml`, `_rels/.rels`) or some deflate corruptions is still a 500 (D13).
- No Postman change: no endpoint or contract changed.

## CodeRabbit RC1

**Finding (valid).** The contiguous alternative of `LogRedactor.PhonePattern` (`(?:\+?20)?0?1[0125][0-9]{8}`) had no left boundary, so the last 11 digits of a hex id or a longer digit run were masked (`3f2a1b10-1012-3456-9abc-a01012345678` → `…-9abc-a[redacted-phone]`). The collector copy had the same pattern. This also closes follow-up D9a above.

**Change.** The contiguous alternative is dropped, not given its own boundary. Once it gets the same left boundary as the spaced alternative it adds nothing: the spaced alternative `(?:20[ -]?)?(?:0[ -]?)?1[ -]?[0125](?:[ -]?[0-9]{4}[ -]?[0-9]{4}|…)` with every separator left out is exactly `(?:20)?0?1[0125][0-9]{8}`, and the shared prefix `(?:\+|(^|[^0-9A-Za-z_]))` covers the old `\+?`. This keeps the group numbering (`${1}` = left boundary, `${2}` = right boundary), so `PhoneReplacementPattern` and the two-pass rule are unchanged. No lookbehind (RE2-safe).

New pattern (C#, verbatim):

```
(?:\+|(^|[^0-9A-Za-z_])(?:00)?)(?:20[ -]?)?(?:0[ -]?)?1[ -]?[0125](?:[ -]?[0-9]{4}[ -]?[0-9]{4}|[0-9][ -]?[0-9]{3}[ -]?[0-9]{4})([^0-9A-Za-z_]|$)
```

**International `00` prefix (coordinator follow-up).** The first version of this fix dropped `00201012345678` (the old pattern gave `00[redacted-phone]`, the boundary version left it whole) and still left `0020 ` visible in `call 0020 10 1234 5678` (`call 0020 [redacted-phone]`). The boundary branch now accepts an optional `00` after the boundary: `(^|[^0-9A-Za-z_])(?:00)?`. The `00` sits outside capture 1, so it is replaced along with the number and the numbering is unchanged. `00201012345678` → `[redacted-phone]`, `call 0020 10 1234 5678` → `call [redacted-phone]`. `x00201012345678` stays unchanged. Side effect: `0001012345678` (`00` + local number) is now masked whole.

| Path | Change |
|---|---|
| `api/Elmanhg.Application/Shared/Observability/LogRedactor.cs` | `PhonePattern` as above; first comment line updated (every number, not only a spaced one, needs the left boundary; `0020` listed). |
| `deploy/observability/otel-collector/config.yaml` | All 8 phone statements replaced with the new pattern (`\` and `$` doubled as before). Replacement strings untouched. |
| `api/Elmanhg.Tests/Application/Features/Shared/Observability/LogRedactorBoundaryTests.cs` | Two rows added to `Redact_TimestampsAndLongerDigitRuns_ReturnsUnchanged`: `user 3f2a1b10-1012-3456-9abc-a01012345678`, `x01012345678`. Two rows added to `Redact_NumberFollowedByPunctuation_KeepsTheFollowingCharacter`: `("00201012345678", "[redacted-phone]")`, `("call 0020 10 1234 5678", "call [redacted-phone]")`. |
| `docs/observability.md` §12 | Boundary sentence now says every number, contiguous or spaced, needs the left boundary; `0020` added to the prefix list. |

**Verification.** 50 strings (every `LogRedactorTests` / `LogRedactorBoundaryTests` input with its expected output, plus the new cases, with `\n` / `\r\n` tails), two passes with `${1}[redacted-phone]${2}`, old vs new pattern, in four engines: .NET 10.0.401 `RegexOptions.NonBacktracking` and backtracking, Python 3.14 `re`, `google-re2` (pip, scratch venv), Go 1.24 `regexp` (the collector's engine, run in a `golang:1.24-alpine` container).

| Engine | Old pattern | Boundary only (first version) | Final (boundary + `00`) |
|---|---|---|---|
| .NET NonBacktracking | 36/50 | 46/50 | 50/50 |
| .NET backtracking | 36/50 | 46/50 | 50/50 |
| Python `re` | 36/50 | 46/50 | 50/50 |
| google-re2 | 36/50 | 46/50 | 50/50 |
| Go `regexp` | 36/50 | 46/50 | 50/50 |

The first version fails only the four `00` rows below. The old pattern fails those four, `x00201012345678`, and the 9 rows in the next table. Failures are identical in every engine.

| Input | Expected (final) | Old output | First-version output |
|---|---|---|---|
| `00201012345678` (also with `
`) | `[redacted-phone]` | `00[redacted-phone]` | unchanged |
| `call 0020 10 1234 5678` (also with `
`) | `call [redacted-phone]` | `call 0020 [redacted-phone]` | `call 0020 [redacted-phone]` |
| `x00201012345678` | unchanged | `x00[redacted-phone]` | unchanged |

The 9 boundary rows the old pattern fails and both new versions pass:

| Input | Expected (new) | Old output |
|---|---|---|
| `user 3f2a1b10-1012-3456-9abc-a01012345678` | unchanged | `user 3f2a1b10-1012-3456-9abc-a[redacted-phone]` |
| same + `\n` | unchanged | `…-9abc-a[redacted-phone]\n` |
| `x01012345678` | unchanged | `x[redacted-phone]` |
| `x01012345678\n` | unchanged | `x[redacted-phone]\n` |
| `x01012345678\r\n` | unchanged | `x[redacted-phone]\r\n` |
| `_01012345678` | unchanged | `_[redacted-phone]` |
| `ts 1759801012345678` | unchanged | `ts 17598[redacted-phone]` |
| `ts 1759801012345678\n` | unchanged | `ts 17598[redacted-phone]\n` |
| `id 1791234567890` | unchanged | `id 179[redacted-phone]` |

Rows that both patterns get right and that pin the requested behaviour: `رقم01012345678` → `رقم[redacted-phone]` (also with `\n` and `\r\n`), `+201012345678` → `[redacted-phone]` (also with `\n` and `\r\n`), `a+201012345678` → `a[redacted-phone]`, `tel:+201012345678;` → `tel:[redacted-phone];`, `ts 1759823456789012` unchanged (also with `\r\n`; this epoch-style value has no `1[0125]` + 8-digit tail, so the old pattern left it alone too; `1759801012345678` is the one that shows the fix). Every existing test string keeps its expected output.

**Tests run.**

- `dotnet test --project api/Elmanhg.Tests -- --filter-class "*LogRedactor*"`: Passed, total 37, failed 0 (includes `LogRedactorCollectorParityTests`, all 3 green).
- `dotnet test --project api/Elmanhg.Tests -- --filter-class "*ReportClientError*"`: Passed, total 12, failed 0.
- `dotnet build api/`: 0 Warning(s), 0 Error(s).
- The full suite was not re-run for this fix.

**Behaviour note.** `x01012345678`, `_01012345678` and a number glued to the end of a hex id or a longer digit run are no longer masked. A number glued to a following ASCII letter (`01012345678x`) was already not masked; the rule is now symmetric.
