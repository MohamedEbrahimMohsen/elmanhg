# Implementation — E21.S13 Fixes from the independent review of the merged E21 PRs

Base: PR 332 was polled every 3 minutes. It merged at 09:18 (local), then `git fetch origin && git merge --ff-only origin/main` fast-forwarded the branch 662b1f50 → 54a7bdac. Nothing is committed or pushed.

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/core-libraries/Core.OTP/Entities/OtpReissueState.cs` | 3 | Record holding the 10 fields a reissue changes |
| `api/core-libraries/Core.OTP/Entities/OTP.Reissue.cs` | 20 | `RestoreReissue(previous)` + private `CaptureReissueState()` |
| `api/core-libraries/Core.OTP/GenerateOTP/OtpDeliveryClaim.cs` | 39 | Send after the claim is saved. If the send fails, release the claim (delete the new row or restore the reissued one), save with `CancellationToken.None`, then `throw;` |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.Otps.cs` | 15 | `OtpRecipientIndex = "IX_Otps_PhoneNumber"`, unique, filtered `"IsDeleted" = false` |
| `api/Elmanhg.Infrastructure/Migrations/20261007062215_AddOtpVersionAndRecipientIndex.cs` (+ `.Designer.cs`) | 47 | Generated `AddColumn xmin` (skipped on Postgres) → `Sql(DeduplicateRecipientsSql)` → `CreateIndex`. `Down` is the generated one |
| `api/core-libraries/Core.Spreadsheets/SpreadsheetPackageGuard.cs` | 35 | Adds up the declared zip entry sizes before `XLWorkbook`. Over the cap or not a zip → `BadRequestCoreException(UnreadableErrorCode)`. Rewinds the stream afterwards |
| `api/Elmanhg.Tests/Core/Otp/OtpDeliveryClaimTests.cs` | 81 | U4–U7 |
| `api/Elmanhg.Tests/Integration/Auth/OtpConcurrencyEndpointTests.cs` | 118 | I1–I4 |
| `api/Elmanhg.Tests/Integration/Persistence/OtpConcurrencyPersistenceTests.cs` | 74 | I5–I6 |
| `api/Elmanhg.Tests/Integration/Persistence/OtpConflictMapTests.cs` | 40 | I10–I11 |
| `api/Elmanhg.Tests/Integration/Persistence/OtpRecipientDeduplicationMigrationTests.cs` | 66 | I13 (raw connection, transaction, rollback) |
| `api/Elmanhg.Tests/Integration/Hosting/AuthenticationOrderTests.cs` | 53 | I15–I16 |
| `api/Elmanhg.Tests/Integration/Infrastructure/ThrowingCheckUserActiveHandler.cs` | 9 | Test double for I15 |
| `api/Elmanhg.Tests/Core/Spreadsheets/NonSeekableReadStream.cs` | 42 | Test helper for U32 |

## Files modified
| Path | Change |
|---|---|
| `Core.OTP/Entities/OTP.cs` | `partial`, `IVersioned`, `uint Version`; `Reissue` captures and returns `OtpReissueState`; one-space whitespace fix (`Otp(Guid id) : base(id)`) flagged by `dotnet format` |
| `Core.OTP/Exceptions/ErrorCodes.cs` | `OtpModifiedConcurrently = "OTP_MODIFIED_CONCURRENTLY"` |
| `Core.OTP/Repositories/IOtpRepository.cs` | `Task<bool> AddIfAbsentAsync(Otp, CancellationToken)` |
| `Core.OTP/GenerateOTP/GenerateOTPHandler.cs` | Claim before delivery: `ClaimAsync` / `ReissueAsync` exactly as in the plan; no try/catch |
| `Core.EntityFrameworkCore/Repositories/OtpRepository.cs` | `AddIfAbsentAsync` (saves; on `DbUpdateException` detaches and re-queries; otherwise `throw;`) |
| `Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | `ConfigureOtps(modelBuilder)` after `ConfigureUsers` |
| `Elmanhg.Infrastructure/Data/Context/AppDbContext.Conflicts.cs` | `.MapConcurrency<Otp>(OtpErrorCodes.OtpModifiedConcurrently)` plus the "deliberately unmapped" comment; the recipient index is not mapped |
| `Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | regenerated |
| `Elmanhg.Api/Program.cs` | explicit `app.UseAuthentication()` right after `CoreExceptionMiddleware`; comments as in the plan |
| `Elmanhg.Api/Resources/Messages.en.resx` / `.ar.resx` | `OTP_MODIFIED_CONCURRENTLY` |
| `Core.Storage/StorageContentTypes.cs` | `ContentTypeProvider` |
| `Core.Storage/DependencyInjection.cs` | Local static files use the media content-type map, `ServeUnknownFileTypes`, and `Fallback`; null-provider guard in `Resolve` |
| `Core.Storage/FileStorageOptions.cs` | `ResolveLocalRoot` trims a trailing separator |
| `Elmanhg.Application/Shared/Observability/LogRedactor.cs` | D12 pattern + comment |
| `Core.Spreadsheets/SpreadsheetOptions.cs` | `MaxUncompressedSizeInMb = 100` |
| `Core.Spreadsheets/ClosedXmlSpreadsheetReader.cs` | buffers non-seekable streams, then runs the guard before `XLWorkbook` |
| `deploy/observability/otel-collector/config.yaml` | all 4 phone patterns replaced with the D12 pattern |
| `deploy/api.env.example` | `Caching__DefaultSeconds` block |
| `web/src/shared/i18n/en.json`, `ar.json` | `OTP_MODIFIED_CONCURRENTLY` (plan texts) |
| `postman/elmanhg.postman_collection.json` | SendOtp description appended; VerifyOtp description added |
| `docs/otp-delivery.md`, `docs/security.md`, `docs/question-import.md`, `docs/observability.md`, `docs/rich-text.md`, `docs/constitution.md`, `.claude/skills/dotnet-feature/SKILL.md` | Docs table applied as written in the plan |
| Tests (modify) | `OtpTests` (U1–U3), `GenerateOTPHandlerTests` (U8–U18; U13 renamed), `PublicMediaPipelineTests` (U19–U22), `StorageContentTypesTests` (U23), `FileStorageOptionsTests` (U24), `LocalDiskFileStorageTests` (U25–U26), `CoreFileStorageDependencyInjectionTests` (U27), `LogRedactorTests` (U28–U29), `ClosedXmlSpreadsheetReaderTests` (U30–U32), `OtpRepositoryTests` (I7–I9), `OtpTableSchemaTests` (I12), `AppDbContextTests` (I14 + pinned migration list), `OtpOutbox` (`SendCountFor`), `AcceptInvitationEndpointTests` (see Deviations) |

`CoreDbContext.cs` and `Core.Notifications` are unchanged. I checked with `git diff --quiet`. No `.IsRowVersion()` was added.

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| `AcceptInvitationEndpointTests` must stay green unchanged; only U9–U13 and I14 change existing tests | `Accept_AfterAcceptance_Returns404` set up its test data by inserting a second live OTP row for an email that already has one. The new unique `IX_Otps_PhoneNumber` index (D5) refuses that row, so the test failed with 23505 before it reached its assertion | Changed only the setup: `SeedVerifiedEmailOtpAsync` now deletes the recipient's existing row (`ExecuteDeleteAsync`) before it inserts the verified one, so there is still one row per recipient, as in production. The scenario and the assertion (404 `INVITATION_NOT_FOUND`) are unchanged. No other test inserts duplicate recipients (searched every `Otps.Add` / raw `INSERT`) |
| `AppDbContextTests`: only I14 changes | `Migrate_FreshDatabase_LeavesNoPendingMigrations` pins the ordered list of migrations | Added `fortyEighth => …EndWith("_AddOtpVersionAndRecipientIndex")`. The caller's brief asked for this ("AppDbContextTests migration list updated if pinned") |
| U24 rows `"media/"`, `"media" + Path.DirectorySeparatorChar` | `[InlineData]` needs constants | Rows `"media/"` and `"media\\"`, with `\\` mapped to `Path.DirectorySeparatorChar` in the test body. On Windows it covers the backslash case; on Linux both rows are `/` |
| I9 "new `Otp` … with `Id = A.Id`" | `Entity.Id` has a public setter | Used `newOtp.Id = stored.Id` (no reflection) |

## Build & test
- `dotnet build api/ -c Release` → `Build succeeded.` A `--no-incremental` rebuild showed no new warnings; the remaining ones (Core.Notifications, Core.Validation, OTP.cs CS8618) were already there.
- OpenAPI check (`git status --porcelain api/openapi` after the Release build) → empty, so the document is unchanged.
- `dotnet ef migrations has-pending-model-changes … --configuration Release --no-build` → `No changes have been made to the model since the last migration.`
- `dotnet ef migrations script --idempotent` → the script has the `DELETE FROM "Otps" … "Rank" > 1` line before `CREATE UNIQUE INDEX "IX_Otps_PhoneNumber" ON "Otps" ("PhoneNumber") WHERE "IsDeleted" = false;`. No xmin DDL is emitted.
- `dotnet test api/ -c Release --no-build` (Docker 29.6.2, Testcontainers) → `Test run summary: Passed! total: 5637 failed: 0 succeeded: 5637 skipped: 0`. This was the second full run; the first run's only failure was the `AcceptInvitationEndpointTests` setup described above.
- Flakiness: the concurrency and auth-order classes plus AcceptInvitation (19 tests) ran 5 times in a row with 0 failures. The parallel tests assert outcomes that hold in any timing: exactly one 200 and one send, every other response is one of the allowed (status, code) pairs, `NOT_MATCHED` ≤ 3, and stored attempts equal the number of non-409 responses. None of them assert on timing.
- Regression probe: with `app.UseAuthentication()` temporarily commented out, `Get_ActiveUserCheckThrows_Returns500StandardErrorBodyInsideRequestLogging` failed. I then restored the line and rebuilt.
- `dotnet format api/Elmanhg.slnx --verify-no-changes --include <changed .cs files>` → exit 0.
- Guard grep (`DateTime.Now|UtcNow`, `.Result`, `.Wait()`, `new HttpClient(`, `FromSqlRaw`, `async void`) on added lines → nothing. No `#<number>` in added comments.
- Web: `npm --prefix web ci`, then `npm --prefix web run typecheck` → exit 0. `npm --prefix web test -- --run src/app/i18n.test.ts src/shared` → `Test Files 19 passed (19)  Tests 111 passed (111)`. `npm --prefix web run build` → succeeded. I did not run the full web suite.

## Deferred
None from the plan. Out of scope, as the plan says: the same claim-before-side-effect race in Avatar messages.

## Notes for review
- File length: `GenerateOTPHandlerTests` is 184 lines, `OtpTests` 274, `PublicMediaPipelineTests` 153, `OtpConcurrencyEndpointTests` 118. Each new test went into the class the plan named. Splitting a class (for example into a partial) would have meant creating files the plan does not list. In production code `OTP.cs` is 149 lines (it was already over 100 before this story; the restore half went into `OTP.Reissue.cs`) and `ClosedXmlSpreadsheetReader.cs` is 104.
- `SpreadsheetPackageGuard` keeps the plan's `entry.Length > cap || total > cap`. The first condition is implied by the second, but I kept it verbatim.
- `OtpDeliveryClaim` releases with `IRepository.Delete` (a hard `_context.Remove`) for a new row. On a reissue release, EF writes the restored values back because the claim save had already accepted the reissued values.
- Under parallel load, a request that loses a resend race gets either 409 `OTP_MODIFIED_CONCURRENTLY` (it lost on xmin) or 429 `OTP_REISSUE_COOLDOWN` (it read after the winner committed), depending on timing. I2 accepts both, as the plan says.
- U22 (trailing slash through `UseCorePublicMedia`) would also pass without the fix, because `PhysicalFileProvider` accepts a trailing separator. The fix itself is pinned by U24/U25, which go through the `StartsWith(root + separator)` check in `LocalDiskFileStorage`.
- I13 runs `DROP INDEX` inside a transaction, which holds an exclusive lock on `Otps` until the rollback. Other parallel OTP tests may wait briefly behind it. The deduplication runs only inside that rolled-back transaction.
- While checking the format I accidentally ran `git add -N .` (intent-to-add). I reset it straight away with `git reset -q`; nothing was staged before, and the working tree did not change.

## Rework r1

| # | Finding | What I changed | File:line |
|---|---|---|---|
| 1 (blocking) | Phone pattern redacts GUID segments and number lists | Replaced the pattern with the reviewer's two-branch form (contiguous unanchored, or separated only from a word start or `+`), with one change: the 8-digit tail after `1[0125]` must be grouped 4-4 or 1-3-4 (see Deviations). Same `RegexOptions.NonBacktracking`. The same pattern, with `\` escaping, is on all four collector lines | `api/Elmanhg.Application/Shared/Observability/LogRedactor.cs:13`; `deploy/observability/otel-collector/config.yaml:90,92,99,103` |
| 1 (test) | U29 too weak | Added U29 rows `user 3f2a1b10-1234-5678-9abc-def012345678` and `scores 10 12 15 20 11` | `api/Elmanhg.Tests/Application/Features/Shared/Observability/LogRedactorTests.cs:46-47` |
| NB | Guard catches only `InvalidDataException` | The guard now uses the reader's filter: `InvalidDataException or FileFormatException or OpenXmlPackageException or ArgumentException or InvalidOperationException` | `api/core-libraries/Core.Spreadsheets/SpreadsheetPackageGuard.cs:30` |
| NB (test) | No fuzz row | New `Read_MalformedZip_ThrowsConfiguredUnreadableCode` theory with 6 corruptions: truncated, central-directory offset, entry count, disk number, local-header offset and entry-name length | `api/Elmanhg.Tests/Core/Spreadsheets/ClosedXmlSpreadsheetReaderTests.cs:61-76`, helper `Corrupt` |
| NB | `MaxUncompressedSizeInMb` not validated | New `SpreadsheetOptionsValidator` (> 0). `AddCoreSpreadsheets` now uses `AddOptions().Configure(configure).ValidateOnStart()` and registers the validator | `api/core-libraries/Core.Spreadsheets/SpreadsheetOptionsValidator.cs` (new), `api/core-libraries/Core.Spreadsheets/DependencyInjection.cs:10-11` |
| NB (test) | — | New `SpreadsheetOptionsValidatorTests`: `Validate_PositiveUncompressedCap_Succeeds`, `Validate_NonPositiveUncompressedCap_Fails` (0, -1) | `api/Elmanhg.Tests/Core/Spreadsheets/SpreadsheetOptionsValidatorTests.cs` (new) |
| NB | Release failure not documented | One sentence in §1: if handing the claim back fails, that error is returned and the row keeps the claimed state | `docs/otp-delivery.md:16` |
| — | Doc agreement for the new startup check | `question-import.md` now says the cap must be greater than 0 and is checked at startup | `docs/question-import.md:110` |

### Deviations (r1)
| Plan / brief said | Reality | What I did |
|---|---|---|
| Use the reviewer's pattern `(\+?20)?0?1[0125][0-9]{8}\b\|(?:\+\|\b)(?:20[ -]?)?(?:0[ -]?)?1[ -]?[0125](?:[ -]?[0-9]){8}\b` and add a U29 row asserting `scores 10 12 15 20 11` is unchanged | The two requirements conflict. In the reviewer's pattern the second branch starts at the word boundary before `10` and matches `10 12 15 20 11` (1, 0, then 8 digits with single spaces). So the text still becomes `scores [redacted-phone]`. I checked this in Python, which uses the same semantics for this pattern | Kept the reviewer's structure but changed the separated tail from `(?:[ -]?[0-9]){8}` to `(?:[ -]?[0-9]{4}[ -]?[0-9]{4}\|[0-9][ -]?[0-9]{3}[ -]?[0-9]{4})`. This covers every U28 format (`+20 10 1234 5678`, `010-1234-5678`, `010 1234 5678`, `+20-100-123-4567`, `0100 123 4567`) and does not match 2-digit lists. It is still RE2- and NonBacktracking-compatible (alternation only). Over 200,000 random UUIDv4s it matched 0.034%, against 0.033% for the original contiguous pattern |

### Build & test (r1)
- `dotnet build api/` → `0 Warning(s) 0 Error(s)`.
- `dotnet test --project Elmanhg.Tests --no-build --filter-class "*LogRedactorTests" --filter-class "*Spreadsheet*" --filter-class "*OptionsValidatorTests" --filter-class "*QuestionImport*"` → `Passed! total: 220 failed: 0 succeeded: 220`. `*Spreadsheet*` includes `SpreadsheetCompositionTests`, which resolves the options through the host with `ValidateOnStart`.
- `dotnet format … --verify-no-changes --include <the 7 changed .cs files>` → exit 0.
- I did not rerun the full suite. No OTP code changed in r1; the only change was the docs sentence.

### Notes for review (r1)
- Probe: with the guard's catch temporarily set back to `InvalidDataException` only, all 6 `Read_MalformedZip` rows still passed. As the reviewer expected, `ZipArchive` raises `InvalidDataException` for these corruptions. So the theory is a regression and fuzz pin, not proof that the wider filter is needed. I then restored the guard and rebuilt.
- The collector config has no automated test. The YAML lines are the C# pattern with each `\` doubled, the same escaping as before.

## CodeRabbit + CI fix

### Files modified
| Path | Change |
|---|---|
| `web/package-lock.json` | `npm audit fix` (no `--force`): `node_modules/source-map-js` 1.2.1 -> 1.2.2 (GHSA-68fv-2mgg-jv7q). The diff is only that entry's version, resolved and integrity lines. No `overrides` were needed. |
| `api/core-libraries/Core.Spreadsheets/SpreadsheetOptions.cs` | New `MaxCompressedSizeInMb` (default 100). |
| `api/core-libraries/Core.Spreadsheets/SpreadsheetOptionsValidator.cs` | Also checks that `MaxCompressedSizeInMb` is greater than 0. Each failure is reported on its own. |
| `api/core-libraries/Core.Spreadsheets/SpreadsheetPackageGuard.cs` | New `BufferWithinCompressedCap`: copies in 80 KB chunks and throws `BadRequestCoreException(UnreadableErrorCode)` as soon as the copy would pass the cap. This is the same code the uncompressed-size guard throws. |
| `api/core-libraries/Core.Spreadsheets/ClosedXmlSpreadsheetReader.cs` | A non-seekable input now goes through `BufferWithinCompressedCap` instead of an unbounded `CopyTo`. The private `Buffer` was removed. Seekable inputs work as before. |
| `api/Elmanhg.Tests/Core/Spreadsheets/ClosedXmlSpreadsheetReaderTests.cs` | `Read_NonSeekableStreamOverCompressedCap_ThrowsConfiguredUnreadableCode`: a workbook of random base64 cells, over 1 MB compressed, sent through `NonSeekableReadStream` with a 1 MB cap, throws `PROBE_UNREADABLE`. The same bytes from a seekable `MemoryStream` read fine, which shows the new cap is what rejects it. |
| `api/Elmanhg.Tests/Core/Spreadsheets/SpreadsheetOptionsValidatorTests.cs` | `Validate_NonPositiveCompressedCap_Fails` (0, -1). |
| `docs/question-import.md`, `docs/security.md` | Document `MaxCompressedSizeInMb` (Limits section, and the Spreadsheets bullet in security). |

### Default chosen
The question-import validator caps uploads at `Content:QuestionImportMaxFileSizeInMb` = 5 MB, checked before the reader runs. The core default is 100 MB, the same as `MaxUncompressedSizeInMb`. That is above the app's upload limit and leaves room if that limit is raised later, so current callers behave the same. The app does not override the option in `Elmanhg.Infrastructure/DependencyInjection.cs`.

### Build & test
- `npm --prefix web audit --audit-level=high`: `found 0 vulnerabilities`, exit 0.
- `npm --prefix web run typecheck`: exit 0.
- `npm --prefix web run build`: exit 0.
- `npm --prefix web test -- --run`: `Test Files 310 passed (310)`, `Tests 1849 passed (1849)`.
- `dotnet build api/`: `0 Error(s)`, 9 warnings that already existed (for example CS8618 in Core.Notifications).
- `dotnet test --project api/Elmanhg.Tests --filter-class "Elmanhg.Tests.Core.Spreadsheets.*" --filter-class "*QuestionImport*" --filter-class "*ImportQuestions*" --filter-class "*SpreadsheetComposition*"`: `Passed! total: 130, failed: 0`.
- The full API suite was not rerun.

### Notes for review
- The compressed cap applies only to non-seekable inputs, because only those are copied into memory. Seekable streams are already bounded by the caller (the upload size validator).
- `SpreadsheetPackageGuard` gained a one-line comment explaining why the copy is bounded. It matches the comment style already in that file.
