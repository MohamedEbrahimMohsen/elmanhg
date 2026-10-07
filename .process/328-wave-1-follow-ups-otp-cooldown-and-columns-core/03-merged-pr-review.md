MERGED-PR REVIEW: 0 critical, 1 major, 6 minor

# Independent review of merged PRs 315, 316, 317, 318 (current code on this branch)

Scope: correctness and security of the code merged by PR 315 (E21.S7 Core.Storage, 7cf514af), PR 316 (E21.S10 Core.Hosting/Observability/Cache/Spreadsheets, 13cef235), PR 317 (E21.S1 core bug fixes, d8e8f743) and PR 318 (E21.S5 Core.Http/Core.Messaging, 315a6a9a). Every line reference is to the current file in the worktree D:/Personal/elmanhg-wt/328.

Routing (plan "Review step"): no critical finding, so nothing blocks story 328. The major and minor findings below go into one review-debt issue, quoted as written. None of them is in a file this story changes, except m2 (Core.Storage/DependencyInjection.cs), and m2 is minor.

## Findings

### M1 (Major, PR 317 OTP limits; the race is older than the PR and it did not fix it). The OTP limits can be bypassed with parallel requests: lost updates
- Where: api/core-libraries/Core.OTP/Entities/OTP.cs:97 (Verify increments in memory), :67-82 and :92 (Reissue checks and sets); api/core-libraries/Core.OTP/VerifyOTP/VerifyOTPHandler.cs:14-22; api/core-libraries/Core.OTP/GenerateOTP/GenerateOTPHandler.cs:19-41; api/core-libraries/Core.EntityFrameworkCore/Context/CoreDbContext.cs:34-47 (no concurrency token on Otp).
- Evidence: both limits work as read, mutate in memory, then save, with no row version. With k parallel POST /api/auth/otp/verify requests for one VerificationId, each request reads VerificationAttempts = n, checks n+1 > MaxVerificationAttempts (3), compares its guess, and writes n+1. All k guesses are checked, but the counter moves up by only 1. So the 3-attempt cap does not hold under concurrency. The only remaining limit is the Credentials rate limit (10/min per IP, api/Elmanhg.Api/RateLimiting/AppRateLimiting.cs:30), which an attacker with several IPs avoids. The same race lets parallel resends pass the 60 s cooldown and the daily quota, and each resend sends a paid SMS or WhatsApp message (bounded only by OtpRequests, 5 per 10 min per IP).
- Fix: make Otp optimistically concurrent (for example, an opt-in xmin concurrency token from the app, or through ApplyRowVersionConvention), and map DbUpdateConcurrencyException on verify and resend to the existing limit errors. Or increment atomically in SQL (UPDATE ... SET "VerificationAttempts" = "VerificationAttempts" + 1 ... RETURNING). This touches the CoreDbContext mapping, which the 2026-10-06 dev decision keeps as it is, so it needs its own story or a dev decision. It is too large for 328.

### m1 (Minor, PR 317). FindByRecipientAsync runs on an unindexed, non-unique column
- Where: api/core-libraries/Core.EntityFrameworkCore/Repositories/OtpRepository.cs:19; CoreDbContext.cs:40-42.
- Evidence: there is no index on PhoneNumber (Recipient), so every OTP send scans the whole table. Two concurrent first sends for a new recipient both see null and insert two rows. After that, FirstOrDefaultAsync (no ordering) returns either row, so the resend counters are split across two rows.
- Fix: a unique index on PhoneNumber, with a unique-violation retry or mapping.

### m2 (Minor, PR 315). Local public media does not use the restricted content-type map
- Where: api/core-libraries/Core.Storage/DependencyInjection.cs:35-40.
- Evidence: with Provider=Local, UseStaticFiles uses its default FileExtensionContentTypeProvider. With S3 (and in LocalDiskFileStorage.OpenReadAsync), StorageContentTypes maps only safe media types, and everything else (.svg, .html) is served as application/octet-stream. So in Local mode, a public key ending in .svg or .html would be served as an inline-renderable image/svg+xml or text/html. nosniff does not stop that, because the type is declared. It cannot be exploited today, because the app generates every key with an extension taken from a validated format, but the two providers behave differently and this defence is missing.
- Fix: set StaticFileOptions.ContentTypeProvider to the same restricted provider that StorageContentTypes builds (expose it).

### m3 (Minor, PR 315). A trailing separator on FileStorage:LocalRootPath breaks every local read and write
- Where: api/core-libraries/Core.Storage/Local/LocalDiskFileStorage.cs:45-47.
- Evidence: Path.GetFullPath("media/", contentRoot) keeps the trailing separator, so root + DirectorySeparatorChar ends in a doubled separator, and the prefix check rejects every key with ArgumentException, which surfaces as a 500. It fails closed, so it is not a security issue. The validator does not catch it.
- Fix: Path.TrimEndingDirectorySeparator(root) before the check (or in ResolveLocalRoot).

### m4 (Minor, PR 317 middleware order). Authentication runs outside CoreExceptionMiddleware and request logging
- Where: api/Elmanhg.Api/Program.cs:162-179; api/Elmanhg.Api/Authorization/ActiveUserTokenValidation.cs:34-35.
- Evidence: Program.cs never calls UseAuthentication(), so WebApplication inserts it automatically at the very start of the pipeline, before forwarded headers, localization, request logging and the exception middleware. OnTokenValidated calls the database through MediatR (CheckUserActiveQuery). A failure there, such as a database outage or a timeout, is rethrown by JwtBearerHandler and becomes a bare 500: no problem+json code, no request log line, no localization. The comment at Program.cs:170 claims auth handler failures get the standard error body. That holds for authorization only.
- Fix: call app.UseAuthentication() explicitly right after UseMiddleware<CoreExceptionMiddleware>() (before UseMediaStorage), and pin the order next to ExceptionMiddlewareOrderTests.

### m5 (Minor, PR 316). Phone redaction misses formatted numbers
- Where: api/Elmanhg.Application/Shared/Observability/LogRedactor.cs:34.
- Evidence: (\+?20)?0?1[0125][0-9]{8}\b matches only digits with no separators. "+20 10 1234 5678", "010-1234-5678" and "010 1234 5678" pass through unredacted into logs and into training-data scrubbing.
- Fix: allow optional single spaces or hyphens between digit groups, and add test rows for those formats.

### m6 (Minor, PR 316, moved code kept as it was). Spreadsheet row and column caps apply only after the whole workbook is in memory
- Where: api/core-libraries/Core.Spreadsheets/ClosedXmlSpreadsheetReader.cs:20.
- Evidence: new XLWorkbook(content) expands every sheet before MaxDataRows and MaxColumns apply. A 10 MB upload (the Kestrel cap) can decompress to gigabytes of XML. Only authenticated staff can import, so the impact is limited to memory pressure on the API.
- Fix: check the uncompressed size of the package entries (ZipArchive entry Length) against a cap before opening it with ClosedXML.

## Checked and found correct (no finding)
- **Storage path traversal:** LocalDiskFileStorage.ResolvePath canonicalises the path and checks the root prefix, so rooted keys and .. are refused. PublicMediaMiddleware.IsPublicKey accepts only [A-Za-z0-9._/-] with no empty, . or .. segments.
- **Private-folder bypass:** PublicMediaFileProvider.IsPrivate compares the canonical physical path case-insensitively, ~ short names are refused, and leading or doubled slashes are trimmed by PhysicalFileProvider before the check. In S3 mode the first segment is compared case-insensitively after trimming trailing dots. TeacherThreadMediaMiddleware matches its path case-sensitively, but any spelling it misses falls through to the core check, which refuses it.
- **Media headers:** nosniff is set on every media path. Public media gets "public, max-age=31536000, immutable" and private media gets "private, no-store".
- **Forwarded headers:** only X-Forwarded-For and X-Forwarded-Proto are honoured, never X-Forwarded-Host. Only loopback and the configured CIDRs are trusted, the CIDRs are validated at startup, and the forward limit is the default 1. Compose sets the Docker subnet.
- **Rate-limit partitioning:** each endpoint policy has its own partitions, because ASP.NET keys each partition by policy name. PerUser prefixes user: or ip:, so the two cannot collide. The IP comes from RemoteIpAddress after forwarded headers, so it cannot be spoofed from outside. Rejection throws, and the exception middleware runs before UseRateLimiter.
- **Placeholder-secret guard:** it skips only the Development environment. It checks every key in ConfigurationSecrets.Keys, the connection-string password, and CoreOtp:Secret as a required key.
- **Kestrel:** the server header is off and the request body is capped at 10 MB.
- **OTP compare and window:** the code is an HMAC-SHA256 with a secret key, compared with CryptographicOperations.FixedTimeEquals. The 24 h window starts at the first code and resends do not move it. Story 328 fixes the block cooldown.
- **Paging math:** the offset is computed in long. A page past the end or past the int range returns an empty page without calling Skip. ValidatePaging refuses a page number below 1 and an offset beyond the int range.
- **Notifications endpoint auth:** the send, subscribe and template groups require an admin policy, and an empty policy name throws. The device and user groups require an authenticated user. MarkNotificationAsRead checks ownership. None of these is mapped by the app today.
- **HTTP resilience and per-caller isolation:** each consumer gets its own named client and therefore its own retry and circuit-breaker pipeline. The SMS client name HttpSmsOtpChannel is restored by 328. POSTs without an idempotency key (Meta, SMS, Paymob) never retry. Resend keeps one Idempotency-Key across retries (pinned by 328's ResendRetryTests). Every OTP provider base URL must be https.
- **Secrets in logs:** OTP delivery failures log only the channel and the HTTP status. SendJsonAsync logs the method, the relative path and the status, never the body or the headers. The OTLP headers go only to the exporter.
