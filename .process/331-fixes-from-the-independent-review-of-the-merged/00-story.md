# [E21.S13] Fixes from the independent review of the merged E21 PRs (OTP concurrency, media, auth order)

Issue: #331

Fix the issues found by the independent review of the merged E21 code that CodeRabbit never reviewed (PRs #315, #316, #317 and #318). The review is in `.process/328-wave-1-follow-ups-otp-cooldown-and-columns-core/03-merged-pr-review.md` on the #328 branch.

**Dev instruction (2026-10-07):** "go ahead with the follow-ups in #319 and #327". The independent review was one of the #319 follow-ups.

**Major**
- **M1: parallel requests can get past the OTP limits.** The `Otp` row has no concurrency token. Verify and resend both read, change in memory, then save. So k parallel verify requests only add 1 to the attempt counter, and parallel resends get past the 60 s cooldown and the daily quota, each sending a paid message. Code: `Core.OTP/Entities/OTP.cs`, `VerifyOTPHandler`, `GenerateOTPHandler`.
  - Fix: add optimistic concurrency on the `Otp` entity, with no new hook inside `CoreDbContext`. Use Postgres `xmin` configured from `AppDbContext.OnModelCreating` for the core `Otp` type, or an equivalent opt-in core extension the app calls.
  - A losing request gets a clear error: 409, or a retry-once that re-checks the limits.
  - Add tests that run real parallel requests against Postgres.
  - The schema must not change, apart from any index added for m1.

**Minor**
- **m1:** the OTP lookup by recipient has no index or uniqueness, so two first sends at the same time create two rows. Add a unique index on the recipient (migration) and handle the duplicate-insert race. If existing rows could break uniqueness, keep the latest row per recipient.
- **m2:** with the Local provider, public media uses the framework's default content types instead of the restricted list, so an `.svg` or `.html` key would render inline. Serve only the allowed content types, and serve everything else as `application/octet-stream` with `nosniff`, the same as S3.
- **m3:** a trailing slash in `FileStorage:LocalRootPath` makes every local read and write fail with a 500. Normalise the root.
- **m4:** `UseAuthentication()` is never called, so ASP.NET adds it at the very start of the pipeline. A database failure in `ActiveUserTokenValidation` then becomes a bare 500, without the standard error body or a request log line. Call `UseAuthentication()` explicitly after `CoreExceptionMiddleware`, and correct the `Program.cs` comment. Add an integration test.
- **m5:** the phone redaction pattern in `LogRedactor` misses numbers written with spaces or dashes. Widen it and add tests.
- **m6:** the spreadsheet row and column limits only apply after the whole workbook is unpacked into memory. Reject on file size or entry size before loading, or stream, without changing the existing limits or error codes.
- **Plus the non-blocking notes from the #328 review:**
  - the null-provider recursion guard in `Core.Storage/DependencyInjection.cs`;
  - `Caching__DefaultSeconds` in `deploy/api.env.example` if that file lists the optional settings.

### Sub-tasks
- [ ] M1 OTP concurrency, with parallel-request tests
- [ ] m1 unique recipient index and race handling (migration)
- [ ] m2 and m3 local media content types and root path
- [ ] m4 explicit UseAuthentication order, with a test
- [ ] m5 redaction pattern; m6 spreadsheet pre-check
- [ ] Small notes; docs

**Dev decision (2026-10-06):** keep `Core.Notifications` and keep `CoreDbContext` as it is (no decoupling of its OTP/audit/notification tables, no new hook inside it; extensions the app opts into are fine). Add every other core change from the core-libraries analysis.

**Rules for every E21 story**
- Core stays app-agnostic: no Elmanhg names, error codes, metric names or Egypt-only defaults inside `api/core-libraries`; anything app-specific becomes a parameter or option.
- Behaviour of the app must not change unless the story says so; existing tests keep passing, and moved code keeps its tests (next to core coverage in `Elmanhg.Tests/Core/*`).
- No new NuGet packages unless the story names them; new core projects go into `Elmanhg.slnx` under `/core-libraries/`.
- Docs-sync: update `docs/constitution.md` (stack/core list) and `.claude/skills/dotnet-feature/SKILL.md` wherever they describe the core area touched.

Epic: E21 Core libraries hardening and promotion

