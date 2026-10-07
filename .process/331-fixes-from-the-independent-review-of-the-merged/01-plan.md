# Plan — E21.S13 Fixes from the independent review of the merged E21 PRs

Story: GitHub 331. Evidence: `.process/328-wave-1-follow-ups-otp-cooldown-and-columns-core/03-merged-pr-review.md` (main) and `03-review.md` "Non-blocking".
Base: **main after PR 332 is merged** (story 329: `AuditStampingInterceptor`; `Repository<T>(DbContext context)` and `OtpRepository<…>(TContext context)` take only the context; `Repository.SaveChangesAsync` is a plain `_context.SaveChangesAsync`). Run `git fetch origin && git merge origin/main` before the first edit. If PR 332 is not merged yet, stop and report `BLOCKED: PR 332 not merged`.

## Goal
After this ships, parallel requests can no longer get past the OTP limits: every answered verify counts one attempt, parallel resends send exactly one paid message and respect the 60 s cooldown and the daily quota, two simultaneous first sends store one row, and a losing request gets a clear 409 `OTP_MODIFIED_CONCURRENTLY`. Local media serves only the restricted content types, a trailing slash in `FileStorage:LocalRootPath` works, authentication failures (e.g. a database outage in the active-user check) return the standard error body inside request logging, formatted phone numbers are redacted from logs, and an oversized-when-unpacked spreadsheet is refused before ClosedXML loads it.

## Scope
**In:** M1, m1, m2, m3, m4, m5, m6, the two #328 non-blocking notes (null-provider recursion guard; `Caching__DefaultSeconds` line in `deploy/api.env.example`), docs, Postman, web i18n strings for the new code.
**Out:** `CoreDbContext.cs` (unchanged, dev decision 2026-10-06); `Core.Notifications` (kept); the same claim-before-side-effect race in Avatar messages (not in this story).
**Deferred:** none. Everything runs offline (Testcontainers Postgres; fake OTP channels).

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| D1 | How is `Otp` made optimistically concurrent without touching `CoreDbContext`? | `Otp` implements `Core.DDD.Entities.IVersioned` (`public uint Version { get; private set; }`). `AppDbContext.OnModelCreating` already calls `modelBuilder.ApplyRowVersionConvention()` (opt-in core extension), which maps `Version` to `xmin`. No `.IsRowVersion()` anywhere. | Skill delta 1 forbids per-entity `.IsRowVersion()`; the convention is the existing opt-in core extension the app calls. `xmin` is a system column: the generated `AddColumn("xmin")` is skipped by Npgsql (same as `AddSessionVersion`), so the schema does not change. |
| D2 | M1: what does a losing verify/resend do — 409 or bounded retry? | **409 `OTP_MODIFIED_CONCURRENTLY`, no retry.** The app maps it once in `AppDbContext.Conflicts`: `.MapConcurrency<Otp>(OtpErrorCodes.OtpModifiedConcurrently)`. `VerifyOTPHandler` is unchanged. | Strict: the loser's write never lands, so the counter only moves by committed writes, and the loser learns nothing about its guess (no match/no-match leak). Predictable: one documented code, same as every other `*_MODIFIED_CONCURRENTLY` in the app. A retry would need a handler `try/catch` (constitution: none in handlers) and a context reload, for no gain. |
| D3 | Where is the new error code defined? | `Core.OTP.Exceptions.ErrorCodes.OtpModifiedConcurrently = "OTP_MODIFIED_CONCURRENTLY"` (core). The app references it in its `ConflictMap` and localises it in `Messages.*.resx`. | The code belongs to the OTP feature and is app-agnostic (core already owns `OTP_REISSUE_COOLDOWN` etc.); core also throws it directly (D5). |
| D4 | Resend: the paid send currently happens before the save, so a loser has already sent. | **Claim before send.** `GenerateOTPHandler` saves the reissued/created row first (row-version guarded), then sends. If the send throws (any exception, including cancellation), the claim is handed back: a created row is deleted, a reissued row gets its exact previous state restored (`Otp.RestoreReissue`), saved with `CancellationToken.None`, and the original exception is rethrown. | Without claim-first, a parallel loser still sends a paid message. Release-on-failure keeps the documented behaviour ("if delivery fails, nothing is stored, the cooldown does not start", and the previous code stays valid) so a provider outage cannot burn a user's daily quota. |
| D5 | m1 insert race (two first sends both see no row). | Unique filtered index `IX_Otps_PhoneNumber` on `PhoneNumber` `WHERE "IsDeleted" = false`, configured in `AppDbContext` (new partial). New `IOtpRepository.AddIfAbsentAsync(Otp, CancellationToken) : Task<bool>`: adds and saves; on `DbUpdateException` it detaches the new row and returns `false` when a live row for the recipient now exists, otherwise rethrows. On `false` the handler **re-reads** the winner's row and applies `Reissue` to it (so the loser normally gets 429 `OTP_REISSUE_COOLDOWN`; with a 0 s cooldown it re-sends within the limits). If the re-read finds no row (the winner's delivery failed and released it), core throws `ConflictCoreException(OtpModifiedConcurrently)`. The recipient index is **not** mapped in `ConflictMap`. | Re-read gives the truthful answer (a code was just sent; wait N s) and keeps the limits. Detection is provider-agnostic (re-query, not `PostgresException`), so `Core.EntityFrameworkCore` stays Npgsql-free. Precedent for a saving insert-if-absent: `IIssuedRefreshTokenRepository.AddIfAbsentAsync`. Leaving the index unmapped lets the raw `DbUpdateException` reach the repository. |
| D6 | Where does the release `try/catch` live? | In a new core type `OtpDeliveryClaim` (`Core.OTP/GenerateOTP`), not in the handler. One `catch (Exception)` that releases and `throw;`. | Constitution §"Errors": no `try`/`catch` in handlers. This catch does not map or swallow errors; it compensates a side effect and rethrows. |
| D7 | Migration and existing duplicates. | New migration `AddOtpVersionAndRecipientIndex`: generated `AddColumn xmin` (no-op on Postgres), then `migrationBuilder.Sql(DeduplicateRecipientsSql)` **before** the generated `CreateIndex`. The SQL hard-deletes every live row except the newest per `PhoneNumber` (`ORDER BY "CreatedAt" DESC, "Id" DESC`). `Down` = generated (drop index, drop xmin); deleted duplicates are not restored. | Before this change two concurrent first sends could store two rows, so prod may hold duplicates and `CREATE UNIQUE INDEX` would fail. `CreatedAt` is the time of the latest code. OTP rows are transient secrets with no foreign keys, so hard delete is safe and minimises data. |
| D8 | Side effect on sign-in/registration consumers. | Accepted and documented: two parallel `MarkUsed` saves of one verified code (login/register/accept-invite) now give the loser 409 `OTP_MODIFIED_CONCURRENTLY` instead of both succeeding. | It closes a double-use race; same mechanism, no code change in those handlers. |
| D9 | m4: where does `UseAuthentication()` go? | Explicit `app.UseAuthentication();` immediately after `app.UseMiddleware<CoreExceptionMiddleware>();`, before `UseHttpsRedirection`, `UseMediaStorage`, `UseAuthorization`. | Inside request logging (X-Trace-Id, log line) and the exception middleware (standard body); before `TeacherThreadMediaMiddleware`, which reads the signed-in user through `CanViewTeacherThreadMediaQuery`. Calling it explicitly stops `WebApplication` from auto-inserting it at the pipeline start. |
| D10 | m2: what does Local serve for non-media extensions? | `UseStaticFiles` gets `ContentTypeProvider = StorageContentTypes.ContentTypeProvider`, `ServeUnknownFileTypes = true`, `DefaultContentType = StorageContentTypes.Fallback`. `.svg`/`.html` → `application/octet-stream` + `nosniff`. | Same as the S3 path (`PublicMediaMiddleware` serves any public key with `StorageContentTypes.FromKey`). |
| D11 | m3: where to normalise? | `FileStorageOptions.ResolveLocalRoot` returns `Path.TrimEndingDirectorySeparator(Path.GetFullPath(LocalRootPath, contentRootPath))`. | One method feeds both `LocalDiskFileStorage` and `UseCorePublicMedia`. |
| D12 | m5 pattern. | `@"(?:\+?20[ -]?)?(?:0[ -]?)?1[ -]?[0125](?:[ -]?[0-9]){8}\b"` with `RegexOptions.NonBacktracking`; same pattern (RE2-compatible) replaces the four phone patterns in `deploy/observability/otel-collector/config.yaml`. | Single optional space/hyphen between digits; the collector comment says "Same patterns as LogRedactor", so both move together. Training-data scrubbing already handles separators. |
| D13 | m6 cap and error code. | New `SpreadsheetOptions.MaxUncompressedSizeInMb` (int, default 100). Before `new XLWorkbook`, open the package with `System.IO.Compression.ZipArchive` (read-only, `leaveOpen: true`) and sum `entry.Length` incrementally; over the cap → `BadRequestCoreException(UnreadableErrorCode)`; not a zip (`InvalidDataException`) → same. Rewind to the start position. Non-seekable input is buffered into a `MemoryStream` first. App keeps the core default. | No new error code (story: keep existing limits and codes); `SPREADSHEET_UNREADABLE` already covers "not a readable workbook". A 5 MB import that expands past 100 MB is not a real question sheet. BCL only, no new package. Incremental sum avoids `long` overflow from forged Zip64 sizes. |
| D14 | Null-provider recursion guard. | `Resolve`: `var provider = Options(sp).Provider ?? throw new InvalidOperationException("FileStorage:Provider is not set.");` then `GetKeyedService<IFileStorage>(provider)` with the non-null enum. | Removes the stack-overflow path if `[Required]` is ever dropped. |
| D15 | `Caching__DefaultSeconds`. | Add a commented block after the Dashboard block in `deploy/api.env.example`. | The file lists optional settings (Dashboard, TrainingExports…). |
| D16 | Web. | Add `OTP_MODIFIED_CONCURRENTLY` to `web/src/shared/i18n/en.json` and `ar.json` (`errors` block, next to the other `OTP_*` keys). No component change. | Pages render `common:errors.${code}`; without a key the user sees the generic error. |

Morabh reuse: Morabh `Core/Core.OTP/Entities/OTP.cs` and `GenerateOTP/GenerateOTPHandler.cs` are the vendored originals and have the same race; Morabh `Morabh.APIs/Program.cs` never calls `UseAuthentication` either. Everything below is **new — no Morabh equivalent**.

## Existing code touched
| File | Change |
|------|--------|
| `api/core-libraries/Core.OTP/Entities/OTP.cs` | `public partial class Otp : Entity, IVersioned`; add `public uint Version { get; private set; }`; `Reissue` returns `OtpReissueState` (see Domain behaviour). |
| `api/core-libraries/Core.OTP/Exceptions/ErrorCodes.cs` | add `public const string OtpModifiedConcurrently = "OTP_MODIFIED_CONCURRENTLY";` after `OTPAlreadyUsed`. |
| `api/core-libraries/Core.OTP/Repositories/IOtpRepository.cs` | add `Task<bool> AddIfAbsentAsync(Otp otp, CancellationToken cancellationToken);` |
| `api/core-libraries/Core.OTP/GenerateOTP/GenerateOTPHandler.cs` | rewrite `Handle` (claim → deliver), see Files to create #3 contract. |
| `api/core-libraries/Core.EntityFrameworkCore/Repositories/OtpRepository.cs` | implement `AddIfAbsentAsync`. |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | call `ConfigureOtps(modelBuilder);` right after `ConfigureUsers(modelBuilder);` (before `ApplyRowVersionConvention`). |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.Conflicts.cs` | add `using Core.OTP.Entities;` and `using OtpErrorCodes = Core.OTP.Exceptions.ErrorCodes;`; add `.MapConcurrency<Otp>(OtpErrorCodes.OtpModifiedConcurrently)` after `.MapConcurrency<ExamPeriod>(…)`, preceded by the comment `// The OTP recipient index is deliberately unmapped: OtpRepository.AddIfAbsentAsync resolves that race by re-reading the row.` |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | regenerated by `dotnet ef migrations add`. |
| `api/Elmanhg.Api/Program.cs` | pipeline: see API surface. |
| `api/Elmanhg.Api/Resources/Messages.en.resx` / `Messages.ar.resx` | add `OTP_MODIFIED_CONCURRENTLY` after `OTP_REACHED_MAX_REISSUE_COUNT`. |
| `api/core-libraries/Core.Storage/StorageContentTypes.cs` | add `public static IContentTypeProvider ContentTypeProvider => Provider;` (`using Microsoft.AspNetCore.StaticFiles;` already present). |
| `api/core-libraries/Core.Storage/DependencyInjection.cs` | m2 static-file options (D10); D14 guard in `Resolve`. |
| `api/core-libraries/Core.Storage/FileStorageOptions.cs` | D11. |
| `api/Elmanhg.Application/Shared/Observability/LogRedactor.cs` | D12 pattern; comment becomes `// Egyptian mobile numbers, local (01x) or international (+20 / 20), with an optional single space or hyphen between digits.` |
| `api/core-libraries/Core.Spreadsheets/SpreadsheetOptions.cs` | add `public int MaxUncompressedSizeInMb { get; set; } = 100;` |
| `api/core-libraries/Core.Spreadsheets/ClosedXmlSpreadsheetReader.cs` | call the package guard before `new XLWorkbook` (Files to create #6). |
| `deploy/observability/otel-collector/config.yaml` | replace the 4 occurrences of `(\\+?20)?0?1[0125][0-9]{8}\\b` with `(?:\\+?20[ -]?)?(?:0[ -]?)?1[ -]?[0125](?:[ -]?[0-9]){8}\\b`. |
| `deploy/api.env.example` | D15 block (see Docs). |
| `web/src/shared/i18n/en.json`, `web/src/shared/i18n/ar.json` | D16. |
| `postman/elmanhg.postman_collection.json` | SendOtp and VerifyOtp descriptions (see Docs). |
| Tests listed in the Test plan as "modify". |
| Docs listed under Docs. |

## Files to create
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `api/core-libraries/Core.OTP/Entities/OtpReissueState.cs` | record | `namespace Core.OTP.Entities;` `public sealed record OtpReissueState(Guid VerificationId, string CodeHash, bool IsVerified, bool IsUsed, int VerificationAttempts, int ReissueCount, DateTimeOffset ReissueWindowStartedAt, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, DateTimeOffset NextAllowedReissueAt);` |
| 2 | `api/core-libraries/Core.OTP/Entities/OTP.Reissue.cs` | partial class | `namespace Core.OTP.Entities;` `public partial class Otp` with `private OtpReissueState CaptureReissueState()` and `public void RestoreReissue(OtpReissueState previous)` (bodies in Domain behaviour). |
| 3 | `api/core-libraries/Core.OTP/GenerateOTP/OtpDeliveryClaim.cs` | sealed class | `namespace Core.OTP.GenerateOTP;` WHY comment line above the class: `// The row is saved before the paid send, so a parallel request loses on the row version and sends nothing; a failed send hands the claim back.` `public sealed class OtpDeliveryClaim(Otp otp, OtpReissueState? previous)`; `public Otp Otp => otp;` `public async Task<OtpChannel> DeliverAsync(IOtpSender otpSender, IOtpRepository otpRepository, string code, CancellationToken cancellationToken)`: `try { return await otpSender.SendAsync(otp.RecipientType, otp.Recipient, code, cancellationToken).ConfigureAwait(false); } catch (Exception) { await ReleaseAsync(otpRepository).ConfigureAwait(false); throw; }`. `private async Task ReleaseAsync(IOtpRepository otpRepository)`: `if (previous is null) { otpRepository.Delete(otp); } else { otp.RestoreReissue(previous); }` then, under the comment `// Not the request token: a cancelled request must still hand the claim back.`, `await otpRepository.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);` |
| 3b | (modify) `Core.OTP/GenerateOTP/GenerateOTPHandler.cs` | handler | Constructor unchanged: `(IOtpRepository otpRepository, IGenerator generator, IOtpHasher otpHasher, IOptions<OtpOptions> otpOptions, IOtpSender otpSender, TimeProvider timeProvider)`. `Handle`: (1) recipient type/recipient exactly as today; (2) `now = timeProvider.GetUtcNow()`; (3) `code = generator.Generate(size: _otpOptions.OtpLength, allowedCharacters: _otpOptions.AllowedCharacters)`; (4) `codeHash = otpHasher.Hash(code)`; (5) `var claim = await ClaimAsync(recipientType, recipient, codeHash, now, cancellationToken)`; (6) `var channel = await claim.DeliverAsync(otpSender, otpRepository, code, cancellationToken)`; (7) return `GenerateOTPResult` from `claim.Otp` (same 8 fields as today). `private async Task<OtpDeliveryClaim> ClaimAsync(OtpRecipientType recipientType, string recipient, string codeHash, DateTimeOffset now, CancellationToken cancellationToken)`: (a) `existing = FindByRecipientAsync(recipient)`; if not null → `return await ReissueAsync(existing, …)`; (b) `otp = Otp.Create(…)` (same arguments as today); (c) `if (await otpRepository.AddIfAbsentAsync(otp, cancellationToken)) return new OtpDeliveryClaim(otp, previous: null);` (d) comment `// Another request stored this recipient's row first; its row carries the limits this request must pass.` then `var winner = await otpRepository.FindByRecipientAsync(recipient, cancellationToken) ?? throw new ConflictCoreException(ErrorCodes.OtpModifiedConcurrently);` `return await ReissueAsync(winner, codeHash, now, cancellationToken);`. `private async Task<OtpDeliveryClaim> ReissueAsync(Otp otp, string codeHash, DateTimeOffset now, CancellationToken cancellationToken)`: `var previous = otp.Reissue(codeHash, _otpOptions.ExpirationMinutes, now); await otpRepository.SaveChangesAsync(cancellationToken); return new OtpDeliveryClaim(otp, previous);`. No `try`/`catch` in the handler. `.ConfigureAwait(false)` on every await. |
| 3c | (modify) `Core.EntityFrameworkCore/Repositories/OtpRepository.cs` | repository | `public async Task<bool> AddIfAbsentAsync(Otp otp, CancellationToken cancellationToken)`: `await _dbSet.AddAsync(otp, cancellationToken)`; `try { await _context.SaveChangesAsync(cancellationToken); return true; } catch (DbUpdateException) { _context.Entry(otp).State = EntityState.Detached; if (!await _dbSet.AsNoTracking().AnyAsync(x => x.Recipient == otp.Recipient && x.Id != otp.Id, cancellationToken)) { throw; } return false; }`. WHY comment above the method: `// Saves at once: a concurrent first send for the same recipient loses on the unique recipient index and must re-read the winner's row.` |
| 4 | `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.Otps.cs` | partial | `namespace Elmanhg.Infrastructure.Data.Context;` `public partial class AppDbContext { public const string OtpRecipientIndex = "IX_Otps_PhoneNumber"; private static void ConfigureOtps(ModelBuilder modelBuilder) { modelBuilder.Entity<Otp>(builder => builder.HasIndex(x => x.Recipient, OtpRecipientIndex).IsUnique().HasFilter("\"IsDeleted\" = false")); } }` WHY comment above the const: `// One OTP row per recipient; FindByRecipientAsync and the resend limits depend on it.` |
| 5 | `api/Elmanhg.Infrastructure/Migrations/<timestamp>_AddOtpVersionAndRecipientIndex.cs` (+ `.Designer.cs`) | migration | Generate: `dotnet ef migrations add AddOtpVersionAndRecipientIndex -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`. Expected generated ops: `AddColumn<uint>("xmin", "Otps", type: "xid", rowVersion: true, …)` and `CreateIndex("IX_Otps_PhoneNumber", "Otps", "PhoneNumber", unique: true, filter: "\"IsDeleted\" = false")`. Add `public const string DeduplicateRecipientsSql = """DELETE FROM "Otps" WHERE "Id" IN (SELECT "Id" FROM (SELECT "Id", ROW_NUMBER() OVER (PARTITION BY "PhoneNumber" ORDER BY "CreatedAt" DESC, "Id" DESC) AS "Rank" FROM "Otps" WHERE "IsDeleted" = false) AS "Ranked" WHERE "Rank" > 1);""";` (format across lines like `AddAnswerNormalizationRules`) and call `migrationBuilder.Sql(DeduplicateRecipientsSql);` between the `AddColumn` and the `CreateIndex`. No other op (no `DropColumn`/`RenameColumn`). |
| 6 | `api/core-libraries/Core.Spreadsheets/SpreadsheetPackageGuard.cs` | internal static class | `namespace Core.Spreadsheets;` `internal static class SpreadsheetPackageGuard` — `private const long BytesPerMegabyte = 1024 * 1024;` `public static void EnsureWithinUncompressedCap(Stream package, int maxUncompressedSizeInMb, string unreadableErrorCode)`: `var start = package.Position; var cap = maxUncompressedSizeInMb * BytesPerMegabyte; long total = 0;` `try { using var archive = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true); foreach (var entry in archive.Entries) { total += entry.Length; if (entry.Length > cap \|\| total > cap) { throw new BadRequestCoreException(unreadableErrorCode); } } } catch (InvalidDataException exception) { throw new BadRequestCoreException(unreadableErrorCode, innerException: exception); }` then `package.Position = start;`. WHY comment: `// ClosedXML unpacks every part into memory before any row or column cap applies; the declared sizes are checked first. System.IO.Compression stops an entry at its declared size.` |
| 6b | (modify) `ClosedXmlSpreadsheetReader.Read` | reader | First lines: `using var buffered = content.CanSeek ? null : Buffer(content); var package = buffered ?? content; SpreadsheetPackageGuard.EnsureWithinUncompressedCap(package, spreadsheetOptions.Value.MaxUncompressedSizeInMb, spreadsheetOptions.Value.UnreadableErrorCode);` then the existing `try { workbook = new XLWorkbook(package); } …` unchanged. `private static MemoryStream Buffer(Stream content) { var buffer = new MemoryStream(); content.CopyTo(buffer); buffer.Position = 0; return buffer; }` |
| 7 | `api/Elmanhg.Tests/Core/Otp/OtpDeliveryClaimTests.cs` | tests | see Test plan. |
| 8 | `api/Elmanhg.Tests/Integration/Auth/OtpConcurrencyEndpointTests.cs` | tests | see Test plan. |
| 9 | `api/Elmanhg.Tests/Integration/Persistence/OtpConcurrencyPersistenceTests.cs` | tests | see Test plan. |
| 10 | `api/Elmanhg.Tests/Integration/Persistence/OtpConflictMapTests.cs` | tests | see Test plan. |
| 11 | `api/Elmanhg.Tests/Integration/Persistence/OtpRecipientDeduplicationMigrationTests.cs` | tests | see Test plan. |
| 12 | `api/Elmanhg.Tests/Integration/Hosting/AuthenticationOrderTests.cs` | tests | see Test plan. |
| 13 | `api/Elmanhg.Tests/Integration/Infrastructure/ThrowingCheckUserActiveHandler.cs` | test double | `public sealed class ThrowingCheckUserActiveHandler : IRequestHandler<CheckUserActiveQuery, bool> { public Task<bool> Handle(CheckUserActiveQuery request, CancellationToken cancellationToken) => throw new InvalidOperationException("probe database failure"); }` |
| 14 | `api/Elmanhg.Tests/Core/Spreadsheets/NonSeekableReadStream.cs` | test helper | `public sealed class NonSeekableReadStream(byte[] content) : Stream` wrapping a `MemoryStream`: `CanRead => true`, `CanSeek => false`, `CanWrite => false`, `Read` delegates, `Length`/`Position`/`Seek`/`SetLength`/`Write` throw `NotSupportedException`, `Flush` no-op, `Dispose(bool)` disposes the inner stream. |

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `Core.OTP.Exceptions.ErrorCodes.OtpModifiedConcurrently` | `OTP_MODIFIED_CONCURRENTLY` | `AppDbContext.Conflicts` on a stale `Otp` row version (verify, resend, consume); `GenerateOTPHandler.ClaimAsync` when the insert-race re-read finds no row | `ConflictCoreException` | 409 |

Resources (`Messages.*.resx`, and the same text in web i18n):
- en: `Another request changed this code at the same time. Try again.`
- ar: `تم تغيير هذا الرمز بواسطة طلب آخر في نفس الوقت. حاول مرة أخرى.`

No other new code. m6 reuses the app's `SPREADSHEET_UNREADABLE` (via `SpreadsheetOptions.UnreadableErrorCode`).

## Domain behaviour
`OTP.cs`:
```csharp
public OtpReissueState Reissue(string newCodeHash, int expiresInMinutes, DateTimeOffset now)
{
    var previous = CaptureReissueState();
    // existing body unchanged: window reset, cooldown guard (RateLimitExceededCoreException OTPReissueCooldown with days/hours/minutes/seconds),
    // quota guard (OTPReachedMaxReissueCount), then the same assignments as today
    return previous;
}
```
`OTP.Reissue.cs`:
```csharp
private OtpReissueState CaptureReissueState() => new(VerificationId, CodeHash, IsVerified, IsUsed, VerificationAttempts, ReissueCount, ReissueWindowStartedAt, CreatedAt, ExpiresAt, NextAllowedReissueAt);

public void RestoreReissue(OtpReissueState previous)
{
    VerificationId = previous.VerificationId;
    CodeHash = previous.CodeHash;
    IsVerified = previous.IsVerified;
    IsUsed = previous.IsUsed;
    VerificationAttempts = previous.VerificationAttempts;
    ReissueCount = previous.ReissueCount;
    ReissueWindowStartedAt = previous.ReissueWindowStartedAt;
    CreatedAt = previous.CreatedAt;
    ExpiresAt = previous.ExpiresAt;
    NextAllowedReissueAt = previous.NextAllowedReissueAt;
}
```
`Otp` derives from `Entity` (no `UpdationDate`). `Verify` and `MarkUsed` are unchanged; the row version makes their saves serialise.

## API surface
No new endpoint, route, policy or request record. Changed responses:
- `POST /api/auth/otp/send` (`AuthRateLimitPolicies.OtpRequests`) → `GenerateOTPResult`; new 409 `OTP_MODIFIED_CONCURRENTLY` when a parallel request changed the row. A parallel first send now gets 429 `OTP_REISSUE_COOLDOWN`.
- `POST /api/auth/otp/verify` (`Credentials`) → `VerifyOTPResult`; new 409 `OTP_MODIFIED_CONCURRENTLY`.
- `register/phone`, `login/phone`, `login/email-code`, `invitations/accept`: 409 `OTP_MODIFIED_CONCURRENTLY` for the loser of a double use (D8).

`Program.cs` pipeline (only these lines change):
```csharp
app.UseMiddleware<CoreRequestLoggingMiddleware>();

// Inside request logging (it reads the error code) and before authentication and authorization, so failures in auth handlers, the active-user check and media get the standard error body.
app.UseMiddleware<CoreExceptionMiddleware>();

// Called explicitly so it runs inside the exception middleware and request logging instead of at the start of the pipeline; before media, whose private teacher-thread middleware reads the signed-in user.
app.UseAuthentication();

app.UseHttpsRedirection();
app.UseMediaStorage();
app.UseAuthorization();
```
`Core.Storage/DependencyInjection.cs` `UseCorePublicMedia` Local branch: `new StaticFileOptions { FileProvider = …, RequestPath = options.RequestPath, ContentTypeProvider = StorageContentTypes.ContentTypeProvider, ServeUnknownFileTypes = true, DefaultContentType = StorageContentTypes.Fallback, OnPrepareResponse = … }` with the WHY comment `// Same content types as the S3 path: anything outside the media map is served as application/octet-stream, never as an inline-renderable type.`

## Test plan
Docker is required for every `Integration/*` test; if unavailable, report `BLOCKED: docker unavailable`. Use `TestContext.Current.CancellationToken`, AwesomeAssertions/FluentAssertions as already imported in each sibling file.

### Unit — Core.OTP
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| U1 | `OtpTests` (add) | `Reissue_BelowMax_ReturnsThePreviousState` | returned state equals the pre-reissue `VerificationId`, `CodeHash`, `ReissueCount` 0, `NextAllowedReissueAt`, `ExpiresAt`, `CreatedAt`, `ReissueWindowStartedAt` |
| U2 | `OtpTests` (add) | `RestoreReissue_AfterReissue_RestoresThePreviousCodeAndLimits` | after `Reissue` + `RestoreReissue(previous)`: every one of the 10 fields equals its value before `Reissue`; `Verify(oldHash, now)` returns null |
| U3 | `OtpTests` (add) | `RestoreReissue_AfterWindowReset_RestoresTheOldWindowAndCount` | otp with `ReissueCount` 2 and window start a day ago; reissue resets window; restore brings back count 2 and the old window start |
| U4 | `OtpDeliveryClaimTests` | `DeliverAsync_SendSucceeds_ReturnsChannelAndSavesNothing` | returns `OtpChannel.Sms`; `SaveChangesAsync` `DidNotReceive`; `Delete` `DidNotReceive` |
| U5 | `OtpDeliveryClaimTests` | `DeliverAsync_NewRowSendFails_DeletesTheRowAndRethrows` | sender throws `ServiceUnavailableCoreException("PROBE_DELIVERY_FAILED")`; same exception type + code; `Delete(otp)` `Received(1)`; `SaveChangesAsync(CancellationToken.None)` `Received(1)` |
| U6 | `OtpDeliveryClaimTests` | `DeliverAsync_ReissuedRowSendFails_RestoresThePreviousStateAndRethrows` | claim built from `previous = otp.Reissue(...)`; after the throw `otp.VerificationId`, `ReissueCount`, `NextAllowedReissueAt` equal their pre-reissue values; `Delete` `DidNotReceive`; `SaveChangesAsync(CancellationToken.None)` `Received(1)` |
| U7 | `OtpDeliveryClaimTests` | `DeliverAsync_SendCancelled_ReleasesWithoutTheCancelledToken` | sender throws `OperationCanceledException` for a cancelled token; `OperationCanceledException` propagates; `SaveChangesAsync(CancellationToken.None)` `Received(1)` |
| U8 | `GenerateOTPHandlerTests` (modify ctor) | constructor | add `_otpRepository.AddIfAbsentAsync(Arg.Any<OtpEntity>(), Arg.Any<CancellationToken>()).Returns(true);` |
| U9 | `GenerateOTPHandlerTests` (modify) | `Handle_NewPhone_CreatesOtpAndSendsGeneratedCode` | `AddIfAbsentAsync` `Received(1)` with recipient/type match (replaces the `AddAsync` assertion); `SaveChangesAsync` `DidNotReceive` (replaces `Received(1)`); send `Received(1)`; result asserts unchanged |
| U10 | `GenerateOTPHandlerTests` (modify) | `Handle_NewEmail_CreatesEmailOtpWithNormalizedRecipient` | `AddIfAbsentAsync` `Received(1)` with `mona@elmanhg.test`/Email (replaces `AddAsync`); send `Received(1)`; `SaveChangesAsync` `DidNotReceive` |
| U11 | `GenerateOTPHandlerTests` (modify) | `Handle_NewPhone_StampsTimesFromTimeProvider` | result times unchanged; `AddIfAbsentAsync` `Received(1)` (replaces `SaveChangesAsync Received(1)`) |
| U12 | `GenerateOTPHandlerTests` (modify) | `Handle_ExistingOtpPastCooldown_ReissuesWithoutAdding` | `AddIfAbsentAsync` `DidNotReceive` (replaces `AddAsync`); `SaveChangesAsync` `Received(1)`; send `Received(1)` |
| U13 | `GenerateOTPHandlerTests` (rename + modify) | `Handle_DeliveryUnavailable_ThrowsAndDeletesTheClaimedRow` (was `Handle_DeliveryUnavailable_ThrowsAndSavesNothing`) | `ServiceUnavailableCoreException` + `OtpChannelUnavailable`; `AddIfAbsentAsync` `Received(1)`; `Delete` `Received(1)`; `SaveChangesAsync(CancellationToken.None)` `Received(1)` — the claim is now saved before sending (D4); "nothing persisted" stays pinned by the existing integration test `SendOtp_NoPhoneChannelEnabled_Returns503AndPersistsNothing` |
| U14 | `GenerateOTPHandlerTests` (add) | `Handle_ExistingOtp_SavesTheClaimBeforeSending` | `Received.InOrder(() => { _otpRepository.SaveChangesAsync(Arg.Any<CancellationToken>()); _otpSender.SendAsync(...); })` |
| U15 | `GenerateOTPHandlerTests` (add) | `Handle_LostInsertRaceWithinCooldown_ThrowsCooldownAndSendsNothing` | `AddIfAbsentAsync` returns false; `FindByRecipientAsync` returns `null` then a row issued at `Now`; `RateLimitExceededCoreException` + `OTPReissueCooldown`; send `DidNotReceive`; `SaveChangesAsync` `DidNotReceive` |
| U16 | `GenerateOTPHandlerTests` (add) | `Handle_LostInsertRacePastCooldown_ReissuesTheWinnersRowAndSends` | winner issued at `Now.AddMinutes(-2)`; result `ReissueCount` 1 and `VerificationId` == winner's new id; `SaveChangesAsync` `Received(1)`; send `Received(1)` |
| U17 | `GenerateOTPHandlerTests` (add) | `Handle_LostInsertRaceAndWinnerGone_ThrowsOtpModifiedConcurrently` | `AddIfAbsentAsync` false; `FindByRecipientAsync` null twice; `ConflictCoreException` + `ErrorCodes.OtpModifiedConcurrently`; send `DidNotReceive` |
| U18 | `GenerateOTPHandlerTests` (add) | `Handle_ExistingOtpDeliveryFails_RestoresThePreviousCodeAndRethrows` | existing row past cooldown, old `VerificationId` captured; sender throws `ServiceUnavailableCoreException`; rethrown; row `VerificationId`, `ReissueCount` 0, `NextAllowedReissueAt` restored; `SaveChangesAsync` `Received(2)` |

### Unit — storage, redaction, spreadsheets
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| U19 | `PublicMediaPipelineTests` (add) | `UseCorePublicMedia_LocalSvgKey_ServesOctetStreamWithNosniff` | write `lessons/a.svg`; 200; `ContentType` `application/octet-stream`; `nosniff` |
| U20 | `PublicMediaPipelineTests` (add) | `UseCorePublicMedia_LocalHtmlKey_ServesOctetStream` | `lessons/a.html` → 200, `application/octet-stream` |
| U21 | `PublicMediaPipelineTests` (add) | `UseCorePublicMedia_LocalWebmKey_ServesAudioWebm` | `lessons/a.webm` → `audio/webm` (restricted map override, not `video/webm`) |
| U22 | `PublicMediaPipelineTests` (add) | `UseCorePublicMedia_LocalRootWithTrailingSlash_ServesPublicFile` | options `LocalRootPath = "media/"`; `lessons/a.png` → 200 with the bytes |
| U23 | `StorageContentTypesTests` (add) | `ContentTypeProvider_Svg_IsNotMapped` | `ContentTypeProvider.TryGetContentType("a.svg", out _)` is false; `"a.png"` → `image/png` |
| U24 | `FileStorageOptionsTests` (add) | `ResolveLocalRoot_TrailingSeparator_IsTrimmed` (`[Theory]` rows `"media/"`, `"media" + Path.DirectorySeparatorChar`) | equals `Path.GetFullPath(Path.Combine(contentRoot, "media"))` |
| U25 | `LocalDiskFileStorageTests` (add) | `SaveAsync_RootWithTrailingSeparator_WritesFile` | storage with `LocalRootPath = "media/"`; returns `/api/media/lessons/a.png`; file exists under `<root>/media/lessons` |
| U26 | `LocalDiskFileStorageTests` (add) | `OpenReadAsync_RootWithTrailingSeparatorKeyEscapingRoot_ThrowsArgumentException` | key `../x.png` still refused |
| U27 | `CoreFileStorageDependencyInjectionTests` (add) | `AddCoreFileStorage_NullProvider_ThrowsInvalidOperation` | `AddCoreFileStorage()` then `services.AddSingleton<IOptions<FileStorageOptions>>(Options.Create(new FileStorageOptions { Provider = null, LocalRootPath = "media", PublicBaseUrl = "/api/media" }))`; resolving `IFileStorage` throws `InvalidOperationException` with message `*FileStorage:Provider is not set*` (no stack overflow) |
| U28 | `LogRedactorTests` (add) | `Redact_FormattedMobileNumber_ReplacesWithMarker` (`[Theory]` rows `"+20 10 1234 5678"`, `"010-1234-5678"`, `"010 1234 5678"`, `"+20-100-123-4567"`, `"0100 123 4567"`) | `"call " + row` → `"call [redacted-phone]"` |
| U29 | `LogRedactorTests` (add) | `Redact_ShortDigitGroups_ReturnsUnchanged` (`[Theory]` rows `"on 2026-10-07"`, `"pin 12 34 56"`) | unchanged |
| U30 | `ClosedXmlSpreadsheetReaderTests` (add) | `Read_UncompressedSizeOverCap_ThrowsConfiguredUnreadableCode` | reader with `MaxUncompressedSizeInMb = 1`; workbook with 64 cells each `new string('a', 32_000) + i` (distinct, ~2 MB unpacked); `BadRequestCoreException` with `PROBE_UNREADABLE` |
| U31 | `ClosedXmlSpreadsheetReaderTests` (add) | `Read_UncompressedSizeWithinCap_ReadsWorkbook` | same reader cap 1 MB, the small `QuestionWorkbookBuilder` workbook reads one sheet with its rows |
| U32 | `ClosedXmlSpreadsheetReaderTests` (add) | `Read_NonSeekableStream_ReadsWorkbook` | `new NonSeekableReadStream(content)` → headers and rows as in `Read_Workbook_ReturnsSheetsHeadersAndRowsWithExcelRowNumbers` |

### Integration (Testcontainers Postgres)
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| I1 | `OtpConcurrencyEndpointTests` | `SendOtp_ParallelFirstSends_DeliversOneCodeAndStoresOneRow` | 8 parallel `POST /api/auth/otp/send` for a new phone (`Task.WhenAll`, one `HttpClient`); exactly one 200; the other 7 are 429 with code `OTP_REISSUE_COOLDOWN`; `factory.Otp.SendCountFor(phone)` == 1; `Otps` rows for the phone (fresh scope, `AsNoTracking`) == 1 |
| I2 | `OtpConcurrencyEndpointTests` | `SendOtp_ParallelResendsPastCooldown_DeliversOneCodeAndCountsOneResend` | seed a row issued at `UtcNow - 2 min` (arrange only); 8 parallel sends; exactly one 200; others 409 `OTP_MODIFIED_CONCURRENTLY` or 429 `OTP_REISSUE_COOLDOWN`; `SendCountFor` == 1; stored `ReissueCount` == 1 |
| I3 | `OtpConcurrencyEndpointTests` | `VerifyOtp_ParallelWrongCodes_CountsEveryAnsweredAttempt` | send a code; 10 parallel wrong verifies; every response is 400 `OTP_NOT_MATCHED`, 429 `OTP_REACHED_MAX_ATTEMPTS` or 409 `OTP_MODIFIED_CONCURRENTLY`; count of `OTP_NOT_MATCHED` ≤ 3; stored `VerificationAttempts` == number of non-409 responses |
| I4 | `OtpConcurrencyEndpointTests` | `SendOtp_ResendDeliveryFails_KeepsThePreviousCodeAndLimits` | seed a row past cooldown whose hash is `IOtpHasher.Hash("123456")` (resolved from `factory.Services`); send through a host with WhatsApp and SMS disabled → 503 `OTP_CHANNEL_UNAVAILABLE`; stored `ReissueCount` 0, `VerificationId` and `NextAllowedReissueAt` unchanged; `POST verify { code: "123456", verificationId: old }` on the normal client → 200 |
| I5 | `OtpConcurrencyPersistenceTests` | `SaveChanges_StaleOtpVerify_ThrowsOtpModifiedConcurrently` | seed row; load it in two scopes; scope A `Verify(wrong)` + save; scope B `Verify(wrong)` + save → `ConflictCoreException` code `OTP_MODIFIED_CONCURRENTLY`, `StatusCode` 409; stored `VerificationAttempts` == 1 |
| I6 | `OtpConcurrencyPersistenceTests` | `SaveChanges_StaleOtpConsume_ThrowsOtpModifiedConcurrently` | seed a verified row (`OtpBuilder().Verified()`, `IssuedAt(UtcNow)`); two scopes `MarkUsed`; second save throws code `OTP_MODIFIED_CONCURRENTLY` |
| I7 | `OtpRepositoryTests` (add) | `AddIfAbsentAsync_NewRecipient_InsertsRowAndReturnsTrue` | true; row readable from a fresh scope |
| I8 | `OtpRepositoryTests` (add) | `AddIfAbsentAsync_RecipientAlreadyStored_ReturnsFalseAndDetachesTheNewRow` | seed row; second `Otp` for the same phone → false; `context.Entry(newOtp).State` == `Detached`; one row for the phone |
| I9 | `OtpRepositoryTests` (add) | `AddIfAbsentAsync_OtherInsertFailure_Rethrows` | seed row A; new `Otp` for a different phone with `Id = A.Id` → `DbUpdateException` propagates |
| I10 | `OtpConflictMapTests` | `TryTranslate_OtpConcurrency_ReturnsOtpModifiedConcurrently` | `ConcurrencyFailures.For(context, new OtpBuilder().Build())` → translated, code `OTP_MODIFIED_CONCURRENTLY`, 409, inner exception kept |
| I11 | `OtpConflictMapTests` | `TryTranslate_OtpRecipientUniqueViolation_IsNotTranslated` | `DbUpdateException` with `PostgresException(constraintName: AppDbContext.OtpRecipientIndex, sqlState: UniqueViolation)` → false, conflict null |
| I12 | `OtpTableSchemaTests` (add) | `Migrate_FreshDatabase_OtpsHasUniqueFilteredRecipientIndex` | `SELECT indexdef FROM pg_indexes WHERE indexname = 'IX_Otps_PhoneNumber'` contains `UNIQUE`, `("PhoneNumber")` and `WHERE` + `IsDeleted` |
| I13 | `OtpRecipientDeduplicationMigrationTests` | `DeduplicateRecipientsSql_DuplicateRecipients_KeepsTheNewestLiveRow` | raw connection + transaction (pattern of `AttemptQueryPlanTests`): `DROP INDEX "IX_Otps_PhoneNumber"`; insert 3 live rows for phone P (`CreatedAt` t-2h, t, t-1h), 1 soft-deleted row for P, 1 live row for phone Q (raw `INSERT` with every NOT NULL column); run `AddOtpVersionAndRecipientIndex.DeduplicateRecipientsSql`; P has exactly the live row with `CreatedAt` t plus the soft-deleted row; Q's row remains; `ROLLBACK` |
| I14 | `AppDbContextTests` (modify) | `Model_VersionedEntities_MapVersionToXminRowVersion` | add `typeof(Core.OTP.Entities.Otp)` to the expected token types (intended: `Otp` is now versioned) |
| I15 | `AuthenticationOrderTests` | `Get_ActiveUserCheckThrows_Returns500StandardErrorBodyInsideRequestLogging` | sign in a seeded student on `factory` (`ScopeTestData.SeedStudentAsync` + `SignedInClientAsync`), copy its `Authorization` header to a client of `factory.WithWebHostBuilder(... services.RemoveAll<IRequestHandler<CheckUserActiveQuery, bool>>(); services.AddTransient<IRequestHandler<CheckUserActiveQuery, bool>, ThrowingCheckUserActiveHandler>())`; `GET /api/progress/subjects` → 500, content type `application/json`, body `code` == `ExceptionErrorCodes.UnhandledException`, header `X-Trace-Id` present |
| I16 | `AuthenticationOrderTests` | `Get_ValidTokenAfterExplicitAuthentication_ReachesStudentEndpoint` | same signed-in client against `factory` → 200 (auth still works with the explicit position) |

Infrastructure change for tests: `OtpOutbox` gains a `ConcurrentDictionary<string, int> _sendCounts`; `Record` also does `_sendCounts.AddOrUpdate(recipient, 1, (_, count) => count + 1)`; new `public int SendCountFor(string recipient) => _sendCounts.TryGetValue(recipient, out var count) ? count : 0;`.

Regression suites that must stay green unchanged (they pin behaviour this story moves): `OtpEndpointTests` (incl. `SendOtp_NoPhoneChannelEnabled_Returns503AndPersistsNothing`, `SendOtp_WithinCooldown_Returns429`), `VerifyOTPHandlerTests`, `TeacherThreadMediaEndpointTests` (auth still reaches the private media middleware), `ExceptionMiddlewareOrderTests`, `AccessTokenRevocationTests`, `PhoneAuthEndpointTests`, `EmailCodeAuthEndpointTests`, `AcceptInvitationEndpointTests`, `AppDbContextTests.Model_Current_MatchesLatestMigrationSnapshot`, `ClosedXmlSpreadsheetReaderTests` existing rows, `QuestionImportEndpointTests`.

## Docs (docs-sync)
| File | Edit |
|------|------|
| `docs/otp-delivery.md` §1, paragraph "The code is delivered **before**…" | Replace the first two sentences with: "The OTP row is saved **before** the code is delivered: the new code, the cooldown and the resend count are claimed first, so a parallel request loses on the row version and sends nothing. If delivery fails, the claim is handed back: a new row is deleted, and a resent row gets its previous code, cooldown and count back, so the 60 s resend cooldown does not start and the previous code still works." Keep the rest of the paragraph. |
| `docs/otp-delivery.md` §1, **Resend limits** | Append: "One row per recipient is enforced by the unique index `IX_Otps_PhoneNumber` (live rows). When two first sends race, one stores the row and the other re-reads it and applies the resend checks, so it normally gets 429 `OTP_REISSUE_COOLDOWN`. The row carries a PostgreSQL `xmin` row version: a send, verify or sign-in that loses a race on the same row gets 409 `OTP_MODIFIED_CONCURRENTLY` and changes nothing, so every answered verify counts one attempt and parallel resends send one message." |
| `docs/security.md` §5 (file handling list) | Add bullets: "**Local media types:** with the `Local` provider, `/api/media` serves only the `StorageContentTypes` media map; any other extension (for example `.svg`, `.html`) is served as `application/octet-stream` with `nosniff`, the same as `S3`." and "**Spreadsheets:** an `.xlsx` whose zip entries declare more than `SpreadsheetOptions.MaxUncompressedSizeInMb` (100 MB) unpacked is refused with `SPREADSHEET_UNREADABLE` before it is loaded." |
| `docs/security.md` §8 Sessions, access-token bullet | Append: "Authentication runs inside request logging and the exception middleware (`UseAuthentication` right after `CoreExceptionMiddleware`), so a failure in the active-user check returns the standard error body and is logged." Add a bullet: "**OTP races:** the OTP row is row-versioned (`xmin`) and unique per recipient; the attempt limit, the resend cooldown and the daily quota hold under parallel requests, and a losing request gets 409 `OTP_MODIFIED_CONCURRENTLY` ([otp-delivery.md](otp-delivery.md))." |
| `docs/question-import.md` | Error table row `SPREADSHEET_UNREADABLE`: "a zip archive that is not a readable workbook, or whose entries unpack to more than 100 MB". Limits list: add "- The unpacked size of the workbook (sum of the zip entries) is capped at `SpreadsheetOptions.MaxUncompressedSizeInMb` (100 MB, core default) and checked before the workbook is loaded." |
| `docs/observability.md` §12 item 2 | "plus the app's Egyptian mobile pattern (digits with an optional single space or hyphen between them, local or `+20`)"; item 3 unchanged wording ("the same two patterns") stays true because the collector is updated. |
| `docs/rich-text.md` (Storage bullet) | After "with `X-Content-Type-Options: nosniff`" add "and only the media content types; any other extension is `application/octet-stream`". |
| `docs/constitution.md` | Line "Errors: … (inside request logging, before authorization)" → "(inside request logging, before authentication and authorization)". Stack line: `Core.Spreadsheets` (ClosedXML reader/writer) → `Core.Spreadsheets` (ClosedXML reader/writer with an unpacked-size pre-check). |
| `.claude/skills/dotnet-feature/SKILL.md` | §7.4 order → "forwarded headers → `UseCoreLocalization` → `CoreRequestLoggingMiddleware` → `CoreExceptionMiddleware` → `UseAuthentication` → HTTPS redirection → media storage → `UseAuthorization` → rate limiter → endpoints", and "before authorization" → "before authentication and authorization; `UseAuthentication` is called explicitly so it is not auto-inserted at the pipeline start". Delta 5 helper list: after `IOtpRepository.ConsumeAsync` add "`IOtpRepository.AddIfAbsentAsync` (saves at once; a lost first-send race returns `false`); `Otp` is `IVersioned` and the app maps `MapConcurrency<Otp>(Core.OTP ErrorCodes.OtpModifiedConcurrently)` plus the unique live-recipient index"; spreadsheet sentence: add "(`SpreadsheetOptions.MaxUncompressedSizeInMb` caps the unpacked size)". |
| `deploy/api.env.example` | After the Dashboard block: `# Query cache (docs/deployment.md). Lifetime of a cacheable query that sets neither its own lifetime nor a cache profile; 0 disables it.` / `# Caching__DefaultSeconds=60` |
| `postman/elmanhg.postman_collection.json` | SendOtp description append: " 409 OTP_MODIFIED_CONCURRENTLY when a parallel request changed the same code; 429 OTP_REISSUE_COOLDOWN within the cooldown." VerifyOtp: add `"description": "409 OTP_MODIFIED_CONCURRENTLY when a parallel request changed the same code; try again."` |

No doc names `#<number>` in code comments; no new doc files.

## Definition of done
- [ ] Branch contains PR 332 (merged from `origin/main`); `CoreDbContext.cs` has no diff; `Core.Notifications` untouched.
- [ ] `Otp : Entity, IVersioned` with `uint Version`; no `.IsRowVersion()` added anywhere; `Model_VersionedEntities_MapVersionToXminRowVersion` lists `Otp`.
- [ ] `AppDbContext.Conflicts` maps `MapConcurrency<Otp>(OTP_MODIFIED_CONCURRENTLY)` and does **not** map `IX_Otps_PhoneNumber`.
- [ ] `IX_Otps_PhoneNumber` unique, filtered `"IsDeleted" = false`, configured in `AppDbContext.Otps.cs`.
- [ ] Migration `AddOtpVersionAndRecipientIndex` runs `DeduplicateRecipientsSql` before `CreateIndex`; contains no drop/rename of a real column; snapshot matches the model.
- [ ] `GenerateOTPHandler` saves the claim before `IOtpSender.SendAsync`, has no `try`/`catch`, and resolves a lost insert race by re-reading and calling `Reissue`.
- [ ] `OtpDeliveryClaim` deletes a new row or restores the previous state on any send failure, saves with `CancellationToken.None`, and rethrows the original exception.
- [ ] `VerifyOTPHandler` unchanged; parallel verifies never reveal more than `MaxVerificationAttempts` match results (I3).
- [ ] Parallel first sends and parallel resends deliver exactly one message (I1, I2).
- [ ] `OTP_MODIFIED_CONCURRENTLY` in `Core.OTP` `ErrorCodes`, both resx files, and both web i18n files with the exact texts above.
- [ ] Local media: `.svg`/`.html` served as `application/octet-stream` with `nosniff`; media map types unchanged.
- [ ] `ResolveLocalRoot` trims a trailing separator; traversal still refused.
- [ ] `Resolve` throws `InvalidOperationException` for a null provider instead of recursing.
- [ ] `Program.cs` calls `UseAuthentication()` right after `CoreExceptionMiddleware`; the comment is corrected; I15 and I16 pass; `TeacherThreadMediaEndpointTests` pass.
- [ ] `LogRedactor` and the four collector patterns use the D12 pattern; existing `LogRedactorTests` rows still pass.
- [ ] Spreadsheet package guard rejects over-cap and non-zip input with the configured unreadable code before `XLWorkbook`; existing reader tests and import tests pass; no new NuGet package.
- [ ] `deploy/api.env.example` has the `Caching__DefaultSeconds` line.
- [ ] Every test in the Test plan exists with the listed name; only U9–U13 and I14 change existing tests; `dotnet build api/` and `dotnet test api/` green; `npm --prefix web run build` and `npm --prefix web test -- --run` green.
- [ ] Docs table applied: otp-delivery, security, question-import, observability, rich-text, constitution, SKILL.md, Postman.
- [ ] No `#<number>` in code comments; no Elmanhg names, codes or defaults added inside `api/core-libraries` (index name and app error mapping live in `Elmanhg.Infrastructure`).
