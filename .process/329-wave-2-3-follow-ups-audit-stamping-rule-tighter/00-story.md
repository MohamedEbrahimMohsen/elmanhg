# [E21.S12] Wave-2/3 follow-ups: audit stamping rule, tighter tests, docs

Issue: #329

Close out the follow-ups in #327 (second and third E21 waves).

**Dev instruction (2026-10-07):** "go ahead with the follow-ups in #319 and #327".

**Audit stamping**
- `Repository.SaveChangesAsync` uses `IsModified`, so it cannot tell "the aggregate did not set `UpdatedBy`" from "the aggregate set it to the value it already had". Example: in the teacher review flow, `QuestionMastery.Record` sets `UpdatedBy = StudentId`, and the repository then overwrites it with the teacher's id. Define one rule (`UpdatedBy` = the acting user when there is one, else what the aggregate set), make the code follow it, and fix any aggregate that misuses `UpdatedBy` as an owner field.
- Domain events are dispatched after the stamping loop, so rows that event handlers add or change (training records) are never actor-stamped. Stamp after dispatch, e.g. with an EF `SaveChangesInterceptor` registered on `AppDbContext` (it runs inside `base.SaveChangesAsync`, after `CoreDbContext` dispatches events). `CoreDbContext` stays unchanged. No migration.

**Tests to tighten**
- `RefreshAccessTokenHandlerTests`: the suspended-user test must assert the token store is never read.
- `RefreshTokenRotationRegistrationTests`: the default-grace test must check the value the registered options actually resolve to.
- `ConflictMapTests`: add a test where a concurrency rule and a unique rule both match; the concurrency code wins.
- `RuntimeSettingRegistryTests` (app): also assert that each group's settings are contiguous.
- Bring `ConflictMapTests`, `RefreshTokenRotatorTests` and `CachingBehaviourTests` near the ~100-line guide by splitting them into focused classes, without losing a test. Bring `SweepWorker.cs` near it by extracting helpers, without changing behaviour.
- (The `CachingBehaviour` registration test is in E21.S11.)

**Docs**
- `docs/audit-log.md`: say that owned values (`RetrySchedule`, `LocalizedText`) are not in the audit diff.

### Sub-tasks
- [ ] UpdatedBy rule and the aggregate fixes, with tests
- [ ] Stamping after domain-event dispatch, through an interceptor (no CoreDbContext change), with tests
- [ ] Tightened tests and file splits
- [ ] Docs

**Dev decision (2026-10-06):** keep `Core.Notifications` and keep `CoreDbContext` as it is (no decoupling of its OTP/audit/notification tables, no new hook inside it; extensions the app opts into are fine). Add every other core change from the core-libraries analysis.

**Rules for every E21 story**
- Core stays app-agnostic: no Elmanhg names, error codes, metric names or Egypt-only defaults inside `api/core-libraries`; anything app-specific becomes a parameter or option.
- Behaviour of the app must not change unless the story says so; existing tests keep passing, and moved code keeps its tests (next to core coverage in `Elmanhg.Tests/Core/*`).
- No new NuGet packages unless the story names them; new core projects go into `Elmanhg.slnx` under `/core-libraries/`.
- Docs-sync: update `docs/constitution.md` (stack/core list) and `.claude/skills/dotnet-feature/SKILL.md` wherever they describe the core area touched.

Epic: E21 Core libraries hardening and promotion

