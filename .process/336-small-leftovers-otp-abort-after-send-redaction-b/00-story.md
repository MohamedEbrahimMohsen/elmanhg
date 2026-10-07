# [E21.S14] Small leftovers: OTP abort after send, redaction boundary, test sizes

Issue: #336

Close out the small leftovers in #334.

**Dev instruction (2026-10-07):** "go ahead with #334 too".

**OTP**
- A client that disconnects after the provider accepted the message gets a paid send without spending cooldown or quota. The rollback runs because the request was cancelled. Only the per-IP rate limit bounds this. Consider keeping the claim when the provider has already accepted the send.

**Logging**
- `\b` differs between .NET and RE2. Arabic letters right next to a spaced phone number (for example `رقم010 1234 5678`) are redacted by the OTel collector but not by `LogRedactor`. Numbers without separators are redacted by both.

**Tests**
- The malformed-zip rows in `ClosedXmlSpreadsheetReaderTests` also pass with the old narrow catch. Add a row that throws a non-`InvalidDataException` type, if one can be built.
- There is no host-level test that the spreadsheet options validator runs at startup.
- Files over the ~100-line guide: `OtpTests.cs` (274), `GenerateOTPHandlerTests.cs` (184), `PublicMediaPipelineTests.cs` (153), `DashboardCachingTests.cs` (133), `OtpConcurrencyEndpointTests.cs` (118), `ReviewSessionTests.cs` (122), `OTP.cs` (149, production), `ClosedXmlSpreadsheetReader.cs` (104, production).
- Rename `RepositoryAuditStamping*Tests`: stamping now lives in `AuditStampingInterceptor`.


**Approach notes**
- OTP abort: when the provider has accepted the message, keep the claim (cooldown and quota spent) even if the request is cancelled afterwards. Roll back only when delivery actually failed. Use a cancellation token the client cannot cancel for the claim bookkeeping after a successful send. Add a test that cancels the request after the provider accepts.
- Redaction boundary: make `LogRedactor` and the collector agree. For example, use an explicit ASCII digit boundary (`(?<![0-9])` is not available in RE2), or replace `\b` with a non-digit character class plus a start-of-text alternative that works the same in .NET NonBacktracking and RE2. Add a test row for Arabic text next to the number, and keep the GUID and score-list rows.
- File-size items: split by concern into focused test classes and partial production files, without losing any test name. Treat this as best effort: a file only slightly over the guide may stay if splitting it would hurt readability. Say which ones stay and why.

### Sub-tasks
- [ ] OTP: keep the claim after the provider accepts, with a cancellation test
- [ ] Redaction: the same boundary in .NET and RE2, with an Arabic-adjacent test
- [ ] Spreadsheet: a malformed-zip row of a non-InvalidDataException type if one can be built; a host-level startup test for the options validator
- [ ] File splits and the RepositoryAuditStamping*Tests rename
- [ ] Docs

**Dev decision (2026-10-06):** keep `Core.Notifications` and keep `CoreDbContext` as it is (no decoupling of its OTP/audit/notification tables, no new hook inside it; extensions the app opts into are fine). Add every other core change from the core-libraries analysis.

**Rules for every E21 story**
- Core stays app-agnostic: no Elmanhg names, error codes, metric names or Egypt-only defaults inside `api/core-libraries`; anything app-specific becomes a parameter or option.
- Behaviour of the app must not change unless the story says so; existing tests keep passing, and moved code keeps its tests (next to core coverage in `Elmanhg.Tests/Core/*`).
- No new NuGet packages unless the story names them; new core projects go into `Elmanhg.slnx` under `/core-libraries/`.
- Docs-sync: update `docs/constitution.md` (stack/core list) and `.claude/skills/dotnet-feature/SKILL.md` wherever they describe the core area touched.

Epic: E21 Core libraries hardening and promotion

