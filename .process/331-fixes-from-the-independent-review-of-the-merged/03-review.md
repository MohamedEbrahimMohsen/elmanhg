VERDICT: CHANGES_REQUESTED

# Review — E21.S13 Fixes from the independent review of the merged E21 PRs (round 1)

## Blocking

### 1. The widened phone pattern redacts parts of GUIDs (ids) and space-separated number lists, at about 5 times the old rate
**Where:** `api/Elmanhg.Application/Shared/Observability/LogRedactor.cs:13`; `deploy/observability/otel-collector/config.yaml:90`, `:92`, `:99`, `:103`; test gap at `api/Elmanhg.Tests/Application/Features/Shared/Observability/LogRedactorTests.cs:46`
**Rule:** plan D12 / story m5 ("widen it"). The caller's brief asks that the pattern not over-redact ids or timestamps. docs/observability.md §12 says the collector redacts phone numbers and emails, not identifiers.
**Problem:** The pattern has no start anchor, and `[ -]?` now lets a match cross the hyphens inside a GUID. A match can start mid-token after any hex letter, and its 10 digits can span GUID groups as long as it ends at a `-`. The collector applies the pattern to every log body, every flattened log attribute, and every span and span-event attribute. So any entity id with this shape (UserId, lesson id, the path in a request log, an exception message) is changed in Loki and Tempo. The same id is changed in the same way every time, so that entity cannot be found in logs. The same happens to a space- or hyphen-separated list of numbers.
**Failure:** `3f2a1b10-1234-5678-9abc-def012345678` becomes `3f2a1b[redacted-phone]-9abc-def012345678`. `scores 10 12 15 20 11` becomes `scores [redacted-phone]`. I tested 200,000 random UUIDv4s: the new pattern matches 0.17% of them, the old one 0.034%. That is about 1 id in 590, against 1 in 3,000 before. U29 (`Redact_ShortDigitGroups_ReturnsUnchanged`) checks only a date and `12 34 56`, so it does not pin this.
**Fix:** Keep the old unanchored contiguous form, and allow separators only when the match starts at a word boundary or at `+`. For example `(\+?20)?0?1[0125][0-9]{8}\b|(?:\+|\b)(?:20[ -]?)?(?:0[ -]?)?1[ -]?[0125](?:[ -]?[0-9]){8}\b`. This passes every U28 row and the existing rows, and leaves the GUID rate at the old level (start-anchored part alone: about 0.001%). It is RE2- and NonBacktracking-compatible. Apply the same pattern to the four collector lines. Add U29 rows for a GUID with a `b10-1234-5678-` segment and for `10 12 15 20 11`.

## Non-blocking
- `api/core-libraries/Core.OTP/GenerateOTP/OtpDeliveryClaim.cs:20-22`: if `ReleaseAsync` throws (database down, or a 409 on the restore save), that exception replaces the delivery exception. The row then keeps the claimed state: the cooldown has started, the count has gone up and the previous code is invalid. The state is consistent, not partial, but docs/otp-delivery.md §1 does not mention it. Worth one sentence there, or logging the original exception.
- `api/core-libraries/Core.OTP/GenerateOTP/OtpDeliveryClaim.cs:13-23` and U7: releasing on `OperationCanceledException` from the request token keeps a behaviour that existed before this change. A client that aborts after the provider has accepted the message gets a paid send without using cooldown or quota. Only the per-IP `OtpRequests` limit bounds this. The behaviour is intended and documented, but consider not releasing when the provider call may already have gone out (a follow-up story).
- `api/core-libraries/Core.OTP/GenerateOTP/GenerateOTPHandler.cs:58,66`: the claim save before the side effect is a deliberate exception to constitution "One SaveChangesAsync per handler, at the end". `RunTrainingExport` already does the same, and this change does not add a docs divergence.
- `api/core-libraries/Core.EntityFrameworkCore/Repositories/OtpRepository.cs:27-45`: a new `catch (DbUpdateException)` in a repository. The plan sanctions it (D5): it keeps core Npgsql-free, does not swallow (rethrows unless a winner row exists), and does not map to a 409. The IssuedRefreshToken precedent uses `ON CONFLICT DO NOTHING` instead. Acceptable as written.
- `api/core-libraries/Core.Spreadsheets/SpreadsheetPackageGuard.cs:28`: catches only `InvalidDataException`, while the reader's existing filter also catches ArgumentException, InvalidOperationException and FileFormatException. A malformed zip that makes `ZipArchive` throw something else would now surface as a 500 instead of SPREADSHEET_UNREADABLE. I know of no such input from ZipArchive's read path, so a fuzz row would be enough.
- `api/core-libraries/Core.Spreadsheets/SpreadsheetOptions.cs:9`: `MaxUncompressedSizeInMb` has no positive-value validation. 0 refuses every workbook.
- U22 `UseCorePublicMedia_LocalRootWithTrailingSlash_ServesPublicFile` passes without the fix, as the report already says. U24/U25 pin m3.

## Verified
- The base contains PR 332 (HEAD 54a7bdac). CoreDbContext.cs and Core.Notifications have no diff (`git diff --quiet`). No `.IsRowVersion()`. `Otp : Entity, IVersioned` with `uint Version`. The snapshot maps it to `xmin` (concurrency token, ValueGeneratedOnAddOrUpdate).
- M1 verify: VerifyOTPHandler (unchanged) saves before it branches on the error code. A loser's stale xmin UPDATE fails, and AppDbContext.SaveChangesAsync turns it into 409 OTP_MODIFIED_CONCURRENTLY through MapConcurrency<Otp>, so the handler returns before revealing match or no-match. The counter moves only on committed writes. I3 pins this: without the token, 10 parallel wrong codes would give 10 OTP_NOT_MATCHED, which is more than the asserted 3 or fewer.
- M1 resend: the claim is saved (ReissueAsync's save, or AddIfAbsentAsync) before DeliverAsync. A loser throws at its save and never reaches SendAsync (no paid send). Reissue release: CaptureReissueState runs before the window reset. RestoreReissue restores all 10 fields. It saves with CancellationToken.None against the xmin read back by the claim save, then `throw;` keeps the original exception. New-row release does a hard Remove plus save. Failed delivery leaves nothing stored (SendOtp_NoPhoneChannelEnabled_Returns503AndPersistsNothing green). A failed resend keeps the previous code working (I4 verifies the old code with 200).
- No try/catch in the handler. The single catch is in OtpDeliveryClaim (core, compensate and rethrow), as D6 says.
- AddIfAbsentAsync: the recipient index is unmapped (I11), so the raw DbUpdateException reaches the repository. It detaches the row and re-queries live rows (soft-delete filter applies). Otherwise it rethrows (I9). The handler re-reads the winner and applies Reissue. A missing winner gives ConflictCoreException(OtpModifiedConcurrently). The generate request has no ambient transaction, so the re-query after the unique violation works (I1 green).
- Migration: DeduplicateRecipientsSql runs before CREATE UNIQUE INDEX with the filter `"IsDeleted" = false`. I checked the script from `dotnet ef migrations script`: the xmin AddColumn is skipped. It keeps the newest live row per PhoneNumber (CreatedAt DESC, Id DESC) and does not touch soft-deleted rows (I13). Down drops the index only, and the xmin drop is a no-op (verified with the generated Down script).
- Index vs lookup: Recipient maps to column PhoneNumber for both phone and email recipients. Emails are trimmed and lower-cased before both lookup and insert (GenerateOTPHandler.cs:18). Phones pass ValidatePhoneNumber (fixed prefixes and length). Case cannot bypass uniqueness, and the index matches the exact-equality FindByRecipientAsync.
- D8 consumers: RegisterWithPhone (via UserManager.CreateAsync) and AcceptInvitation (via AddPasswordAsync) persist the MarkUsed change in the same SaveChanges as the user write. A double-use loser gets 409 and nothing is half-written (I6 pins the mapping).
- The AcceptInvitationEndpointTests change is legitimate. Its seed now deletes the recipient's live rows before it inserts the verified one, which matches production (one row per recipient). The scenario and the 404 INVITATION_NOT_FOUND assertion are unchanged. It is not weakened.
- m2: the Local static files use the restricted map with ServeUnknownFileTypes and an octet-stream default. Previously served media types are unchanged: png/jpg/jpeg/webp/gif/m4a from framework defaults, webm/ogg/mp4 as audio. .jsonl exports live in the private training-exports folder, hidden by PublicMediaFileProvider. The media root holds only uploads. U19 to U21 and U23 pass.
- m3: Path.TrimEndingDirectorySeparator in ResolveLocalRoot. Traversal is still refused (U26). Provider guard: a null provider throws InvalidOperationException (U27).
- m4: UseAuthentication() comes right after CoreExceptionMiddleware and before HTTPS redirection, media and UseAuthorization. No earlier middleware reads HttpContext.User: request logging, localization and forwarded headers don't. I15 checks the 500 JSON body, UNHANDLED_EXCEPTION and X-Trace-Id. I16 and TeacherThreadMediaEndpointTests pass.
- m6: the guard sums the declared central-directory sizes incrementally (no overflow) before XLWorkbook. Over the cap or not a zip gives the configured unreadable code. Lying headers: OpenXML reads through System.IO.Packaging and ZipArchive, whose entry streams stop at the declared size, so an understated header cannot unpack more than it declared. Non-seekable input is buffered, and the stream is rewound. U30 to U32 would fail without the guard or the buffering.
- OTP_MODIFIED_CONCURRENTLY is in Core.OTP ErrorCodes, both resx files and both web i18n files with the plan texts. Web i18n.test.ts passes (10/10). The Postman collection is valid JSON, and SendOtp/VerifyOtp have descriptions (no endpoint changed). The docs table was applied: otp-delivery, security, question-import, observability, rich-text, constitution, SKILL.md and deploy/api.env.example. I found no divergence in PRD or other docs. Core stays app-agnostic: the index name and the conflict mapping are in Infrastructure. No `#<number>` in added comments.
- Every test name in the plan's Test plan exists exactly once.
- Runs: the relevant classes (OTP*, auth order, AcceptInvitation, storage, spreadsheets, redactor, AppDbContext, TeacherThreadMedia, ExceptionMiddlewareOrder, AccessTokenRevocation, Phone/EmailCode auth, QuestionImport): 263/263 green. The concurrency, repository and dedupe classes (12 tests) ran 5 times in a row with 0 failures.

## Test quality
- OtpConcurrencyEndpointTests: these constrain the implementation. I1/I2 fail on more than one send or row, and I3 fails without the row version. Their outcome assertions do not depend on timing.
- OtpConcurrencyPersistenceTests, OtpConflictMapTests, OtpRepositoryTests I7 to I9, OtpRecipientDeduplicationMigrationTests, OtpTableSchemaTests I12: these constrain the implementation.
- OtpDeliveryClaimTests and GenerateOTPHandlerTests U13 to U18: these constrain the implementation. They check the state restored on the real entity, Received.InOrder for save-before-send, and the CancellationToken.None release.
- AuthenticationOrderTests I15: constrains the implementation.
- LogRedactorTests U29: too weak. See Blocking 1.
- PublicMediaPipelineTests U22: vacuous for m3, as already reported.

---

# Round 2 — verify rework r1

VERDICT: CHANGES_REQUESTED

## Blocking

### 2. docs/observability.md still describes the round-1 phone pattern
**Where:** `api/Elmanhg.Application/Shared/Observability/LogRedactor.cs:13` and `deploy/observability/otel-collector/config.yaml:90,92,99,103` vs `docs/observability.md:218` (§12, "Three redaction layers", item 2)
**Rule:** `.claude/rules/docs-sync.md` (divergence: a format/policy contract described differently by code and doc).
**Problem:** This story added the sentence "digits with an optional single space or hyphen between them, local or `+20`". That was true of the round-1 pattern. Rework r1 narrowed it: separators are now allowed only in a match that starts at a word boundary or `+`, and only between the `20` / `0` / `1[0125]` prefix parts and in a 4-4 or 1-3-4 grouping of the last 8 digits. The doc and the code now give different answers to "is this number redacted?".
**Failure:** The doc says `010 123 45678`, `0101 2345 678` and `010 12 34 56 78` are redacted (single spaces between digits, local form). The code and collector leave all three unchanged. I checked this in Python `re` and .NET NonBacktracking.
**Fix:** Reword the parenthesis, e.g. "(local `01x`, `20` or `+20`, contiguous or with single spaces or hyphens in the usual 3-4-4 / 4-3-4 groupings such as `010 1234 5678` or `+20 10 1234 5678`)". Doc-only change, no code change.

## Non-blocking
- `LogRedactor.cs:13`: .NET `\b` is Unicode-aware and RE2's `\b` is ASCII-only. A separated number glued to an Arabic word (`رقم010 1234 5678`) is redacted by the collector but not by `LogRedactor`. Contiguous numbers (`رقم01012345678`) are redacted by both. This is an edge case, and the collector layer still catches it at ingestion.
- Formats neither the old nor the new pattern redacts: `010 123 45678`, `0101 2345 678`, `010 12 34 56 78`, `01 0123 45678`, `+20 (10) 1234 5678`, `010.1234.5678`. The reviewer's round-1 suggestion caught the first four. The 2-2-2-2 grouping is the only one I have seen used in Egypt, and it is rare. None of these is a regression against HEAD.
- `00201012345678` becomes `00[redacted-phone]` (the international-prefix `00` stays, all subscriber digits are redacted). This is the same as the old pattern.
- The `Read_MalformedZip_ThrowsConfiguredUnreadableCode` rows pass even with the narrow catch (the implementer says so too). It is a regression pin, not proof that the wider filter is needed. The wider filter now matches the reader's filter (`ClosedXmlSpreadsheetReader.cs:25`).
- `SpreadsheetOptionsValidator` has no host-level test. `Elmanhg.Infrastructure/DependencyInjection.cs:79` never sets the cap, so production always runs with 100.

## Verified
- **Finding 1 resolved.** I extracted the four collector literals and turned each `\` into `\`. All four are character-for-character identical to the C# pattern, and the YAML parses (PyYAML). The escaping is the same as the pre-change lines. Every construct is RE2-valid: non-capturing groups, alternation, character classes, bounded repeats and `\b`, with no lookaround or backreferences.
- Format coverage. I checked these in Python `re` and .NET `RegexOptions.NonBacktracking`, and both agree.
  - Redacted, as before: `01012345678`, `+201012345678`, `201012345678`, `+2001012345678`, `phone:01112345678.`.
  - Newly redacted (HEAD missed them): `010 1234 5678`, `0101 234 5678`, `010-1234-5678`, `+20 10 1234 5678`, `+20 101 234 5678`, `+20-100-123-4567`, `0100 123 4567`, `+20 0101 234 5678`, `0 10 1234 5678`. `+20 1012345678` is now redacted whole, where HEAD gave `+20 [redacted]`.
  - Caught by round-1 but missed now: only the irregular groupings listed under Non-blocking. Every grouping people commonly type (3-4-4, 4-3-4, +20 2-4-4, +20 3-3-4) is covered. The loss does not matter.
- Over-redaction:
  - Unchanged: `3f2a1b10-1234-5678-9abc-def012345678`, `2026-10-07`, `2026-10-07T12:34:56.789Z`, `1696680000123`, `scores 10 12 15 20 11`, `scores 10 12 15 20 11 13 14`, `pin 12 34 56`.
  - Random UUIDv4 hit rate over 200,000 samples: HEAD 0.041%, new 0.0385% (Python) and 0.0325% (.NET). This is back to the baseline from the contiguous branch, which was already there before this story.
  - The implementer's deviation from my suggested pattern is correct: my suggestion did redact `scores 10 12 15 20 11` (`scores [redacted]`). The 4-4 / 1-3-4 tail fixes that.
- New U29 rows (`LogRedactorTests.cs:46-47`) pin both regressions.
- `SpreadsheetPackageGuard.cs:30` catch filter now equals the reader's filter. It rethrows as `BadRequestCoreException(unreadableErrorCode, innerException)`. The `BadRequestCoreException` thrown for an over-cap entry is not in the filter, so it propagates unchanged. `cap` is computed in `long`, so it cannot overflow.
- Options validator. `SpreadsheetOptionsValidator` is `sealed` and checks `> 0`. `DependencyInjection.cs:10-11` registers it with `ValidateOnStart`. I checked this empirically with a host built via `AddCoreSpreadsheets`: cap 0 throws `OptionsValidationException: MaxUncompressedSizeInMb must be greater than 0.` at `StartAsync`, and cap 100 starts.
- Docs:
  - `docs/otp-delivery.md:16` adds the release-failure sentence. It is accurate (the restore error replaces the delivery error, and the row keeps the claimed cooldown, count and code).
  - `docs/question-import.md:110` adds "must be greater than 0, checked at startup". It agrees with the code.
- Tests: `dotnet build` gave 0 errors. Filters `*LogRedactorTests`, `*Spreadsheet*`, `*OptionsValidatorTests`, `*QuestionImport*`, `*Otp*`: 345/345 passed.

## Test quality (r1 additions)
- LogRedactorTests U28/U29: these now constrain the pattern in both directions (formats caught, GUID and number list left alone).
- SpreadsheetOptionsValidatorTests: constrains the validator, including the exact message.
- Read_MalformedZip rows: regression pins only (see Non-blocking).
