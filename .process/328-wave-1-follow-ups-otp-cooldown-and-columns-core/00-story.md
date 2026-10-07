# [E21.S11] Wave-1 follow-ups: OTP cooldown and columns, Core.Storage.S3, cache TTL, messaging tests

Issue: #328

Close out the follow-ups in #319 (first E21 wave).

**Dev instruction (2026-10-07):** "go ahead with the follow-ups in #319 and #327".

**Core.OTP**
- `Otp.Verify`/`MarkUsed` clock: already moved to an injected `now` by E21.S2 (#305). Verify and tick it; no code change expected.
- The cooldown after the last allowed resend is timed from the previous cooldown (`NextAllowedReissueAt.AddHours(...)`), not from that resend. Time it from the resend (`now`), with a test.
- Drop the unused `RequestIP` and `UserAgent` columns from the OTP table. They were never assigned (always empty), and nothing reads them since the lookup by recipient, so no data is lost. It needs a migration that drops only those two columns. `CoreDbContext` keeps its structure; only the two properties and their mapping go.

**Core.Storage**
- Split the S3 provider into a new `Core.Storage.S3` project, so `Core.Storage` (the abstraction, local provider and media serving) has no `AWSSDK.S3` dependency and the Application layer stops pulling it in. Infrastructure references `Core.Storage.S3`. Behaviour and config are unchanged.
- `PublicMediaFileProviderTests`: make the upper-case private-folder case prove the private-folder check itself, independent of whether the file system is case-sensitive.

**Core.Cache and Core.Hosting**
- Give the cache its own default TTL setting, e.g. `Caching:DefaultSeconds`. The dashboard queries set their own `Ttl` from `Dashboard:CacheSeconds`, so dashboard behaviour, the 60 s default and "0 turns it off" are unchanged. Document it in `docs/configuration.md` / `docs/deployment.md` if they list settings.
- Add a pipeline-composition test that `CachingBehaviour<,>` is registered in the app's MediatR pipeline.

**Core.Http and Core.Messaging**
- Restore the SMS client's named HttpClient name `HttpSmsOtpChannel` as it was before E21.S5 (or make the name explicit and documented).
- Add tests that retries stay on for the invitation and reminder Resend clients.

**Process**
- CodeRabbit did not review PRs #315, #316, #317 and #318. Run an independent correctness and security review of the merged code from those four PRs as part of this story's review. Add a pipeline note: split moves of more than 100 files into smaller PRs so CodeRabbit can review them.

### Sub-tasks
- [ ] OTP: clock check, cooldown timing, drop the RequestIP and UserAgent columns (migration), tests
- [ ] Core.Storage.S3 split; private-folder test made deterministic
- [ ] Cache default TTL setting; CachingBehaviour registration test
- [ ] SMS client name; Resend retry tests
- [ ] Independent review of the merged #315 to #318 code; pipeline note on large PRs
- [ ] Docs

**Dev decision (2026-10-06):** keep `Core.Notifications` and keep `CoreDbContext` as it is (no decoupling of its OTP/audit/notification tables, no new hook inside it; extensions the app opts into are fine). Add every other core change from the core-libraries analysis.

**Rules for every E21 story**
- Core stays app-agnostic: no Elmanhg names, error codes, metric names or Egypt-only defaults inside `api/core-libraries`; anything app-specific becomes a parameter or option.
- Behaviour of the app must not change unless the story says so; existing tests keep passing, and moved code keeps its tests (next to core coverage in `Elmanhg.Tests/Core/*`).
- No new NuGet packages unless the story names them; new core projects go into `Elmanhg.slnx` under `/core-libraries/`.
- Docs-sync: update `docs/constitution.md` (stack/core list) and `.claude/skills/dotnet-feature/SKILL.md` wherever they describe the core area touched.

Epic: E21 Core libraries hardening and promotion

