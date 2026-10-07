VERDICT: APPROVED

# Review — [E21.S12] Wave-2/3 follow-ups: audit stamping rule, tighter tests, docs

## Blocking
None.

## Non-blocking
- `api/core-libraries/Core.EntityFrameworkCore/Auditing/AuditStampingInterceptor.cs:43` — the `&&` operator has no space before the `!` that follows it.
- `api/core-libraries/Core.EntityFrameworkCore/Auditing/AuditStampingInterceptor.cs:43-51` — no test covers a hard `Deleted` entry. Every Modified, Added and Unchanged branch is tested, and soft deletes are `Modified`, so this only affects values that are never persisted.
- `api/Elmanhg.Domain/Sessions/Session.cs:74-78` — plan Decision 8 calls this "the Resume path", but `Touch` is shared by six callers (`Session.Answering.cs:66`, `Session.Essays.cs:38`, `Session.Exam.cs:48`, `Session.ExamSubmission.cs:41`, `Session.Submission.cs:25`, `Session.cs:71`). The removal is correct under the rule for all of them: on HTTP paths the actor is stamped, and on worker paths such as essay apply the previous value is kept. The wording in the plan understates how far the change reaches. No code change is needed.
- `api/Elmanhg.Tests/Core/Persistence/RepositoryAuditStampingTests.cs:6` and `RepositoryAuditStampingModifiedTests.cs:6` — the class names still say "Repository", but the subject under test is now the interceptor. The repository only passes the save through. A later rename to `AuditStampingInterceptor*Tests` would read better.
- `api/Elmanhg.Tests/Core/Identity/RefreshTokenRotatorTestBase.cs` — the `_addIfAbsent` field is a mutable protected field. This is what the plan specified (Decision 13), so it is noted only.
- `RepositoryAuditStampingModifiedTests.cs` is 102 lines and `ReviewSessionTests.cs` grows to 122 lines. Both are disclosed and close to the size guide.
- The after-dispatch proof (rows 6 and 7) runs on a probe context that copies the "dispatch, then base" order of `CoreDbContext` instead of using `CoreDbContext` itself (plan Decision 11). If `CoreDbContext.SaveChangesAsync` were ever reordered, these tests would not catch it. This is acceptable while `CoreDbContext` is frozen by dev decision.

## Verified
- **Ordering.** `CoreDbContext.SaveChangesAsync` (`Context/CoreDbContext.cs:109-116`, unchanged) publishes domain events, then reads audit changes, then calls `base.SaveChangesAsync`. EF runs `SavingChangesAsync` interceptors inside that base call, so stamping happens after dispatch. `AuditChangeReader` ignores all four audit fields (`AuditChangeReader.cs:16`), so stamping after the read does not change the diff.
- **Tests that prove ordering.** `SaveChangesAsync_RowAddedByDomainEventHandler_...` and `..._RowChangedByDomainEventHandler_...` would fail if stamping ran before dispatch. The added row would be missing from the tracker, and the changed row would still be `Unchanged` and keep `(creator, Earlier)`.
- **The rule, as implemented** (`AuditStampingInterceptor.cs:36-51`):
  - Added rows: `CreationDate = now` and `CreatedBy ??= actor`.
  - Modified or Deleted rows: `UpdationDate` is set only when it is not `IsModified`, so a value the aggregate set wins.
  - `UpdatedBy = actor` on Added, Modified or Deleted rows only when there is an actor. It is never set to null.
  - Operator precedence is correct: the actor check AND (State is Added or Modified or Deleted).
  - Values are written through `entry.Property(...).CurrentValue`, as Decision 7 requires.
- **No double stamping.** The repository loop is removed (`Repository.cs:232`, which now only saves). `ContainSingle` is asserted on both the core probe (`AuditStampingRegistrationTests`) and the real `AppDbContext` (`AppDbContextAuditStampingTests.AppDbContext_ResolvedFromApp_HasAuditStampingInterceptor`).
- **Registration in every host.** `api/` has one host, `Program.cs`. `AddCoreAuditStamping` for `AppDbContext` is called at `Program.cs:87`.
  - The workers (`SweepWorker` hosted services), the `--MigrateAndExit` command (`Program.cs:117-121`), the load-test seed (`Program.cs:133-138`) and `ApiFactory` all resolve `AppDbContext` from that container.
  - `ApiFactory` does not replace the `DbContext` registration.
  - No `AddDbContextPool`, `AddDbContextFactory` or other `DbContext` exists. Options are scoped (`Core.Identity/DependencyInjection.cs:45`), so the scoped interceptor is resolved per scope.
  - No `Elmanhg.Jobs` project exists yet.
- **Performance.** The `ChangeTracker.Entries` call runs one `DetectChanges`. This is the same cost the old repository loop had. Direct context saves (for example the Identity `UserStore`) now pay that one extra pass, which is required for them to be stamped.
- **The 8 owner assignments are removed.** The remaining 33 domain `UpdatedBy =` assignments all come from actor arguments (checked with grep). Callers:
  - `SendAvatarMessageHandler`, `FollowUpTeacherThreadHandler`, `RateTeacherThreadHandler`, `RecordQuestionOpeningHandler`, `SendVoiceReplyHandler` and the `Start*` handlers are HTTP calls by the owner, so the persisted value is unchanged.
  - `QuestionMasteryRecorder` and `ExamSubmission` record the teacher in teacher review, as the story asks, and keep the previous value in workers.
- **Repository constructors.** All 5 core and 33 app repositories now take `(context)` only. No `_currentUser` or `_timeProvider` usage is left. DI resolves, as the integration tests confirm. `RepositoryPagingTests` was updated.
- **Test splits.** Method names in the deleted files and the new classes match exactly for `ConflictMap` (11 + 1 new), `RefreshTokenRotator` (11) and `CachingBehaviour` (7). A whitespace-insensitive diff of the concatenated bodies shows only moves, visibility changes (private to protected) and using lines. No assertion was dropped.
- **Tightened tests are tighter.**
  - `ReceivedCalls().Should().BeEmpty()` on the suspended-user path.
  - The default grace is now pinned from both sides (10 s issues, 11 s revokes) through the rotator resolved from DI.
  - The concurrency-over-unique test registers the unique rule first, so it would catch the wrong precedence.
  - The group-contiguity check compares run-length-collapsed groups with the enum order.
- **`SweepWorker` split.** `SweepWorker.Steps.cs` is a verbatim move, with `scopeFactory`/`logger` replaced by the existing `ScopeFactory`/`Logger` properties. `SweepWorker.cs` is 68 lines. No logic changed.
- **What did not change.** `CoreDbContext.cs`, `Migrations/`, `api/openapi`, `Directory.Packages.props` and `postman/` are untouched. No API surface changed, so no Postman sync is needed.
- **Core stays app-agnostic.** No Elmanhg names appear in the interceptor or in `AddCoreAuditStamping`.
- **No issue numbers.** No issue-number reference appears in added lines.
- **Comments.** The only comment added in production code is the ordering invariant on the interceptor.
- **Deviations.** All 5 listed deviations are accurate and harmless.
- **Build and tests, run by me.** `dotnet build api/Elmanhg.slnx -c Release` gives 0 warnings and 0 errors. `dotnet test api/Elmanhg.slnx -c Release --no-build`, with Docker up so the integration tests ran, gives 5560 passed, 0 failed and 0 skipped. This matches the report.
- **Docs sync.**
  - `docs/audit-log.md` has the owned-values bullet (correct, because `LocalizedText` and `RetrySchedule` are mapped with `OwnsOne`) and the "Audit fields" section. Both match the code.
  - `docs/constitution.md` lines 3, 75, 143 and 144 and `SKILL.md` lines 22, 252, 597, 600, 609, 637, 719 and 1049 now describe the interceptor and the `UpdatedBy` rule.
  - No doc still says the repository stamps (checked with grep across `docs/` and `.claude/`).

## Test quality
- `RepositoryAuditStampingTests` and `RepositoryAuditStampingModifiedTests` constrain the implementation. Each branch of the rule has a case that a wrong implementation fails: the `IsModified` bug, no actor, the value the aggregate set, an Unchanged row with an actor present, after-dispatch Added and Modified rows, and the synchronous path.
- `AuditStampingRegistrationTests` constrains lifetime, wiring and instance identity per scope.
- `AppDbContextAuditStampingTests` constrains app registration and runs the `IsModified` fix and the no-actor rule against PostgreSQL. Row 19 would fail under the old loop, which kept `AdminId`.
- The domain tests reset `UpdatedBy` to null before the call (or use the teacher value from the reply), so each would fail if the owner assignment came back.
- `RefreshTokenRotationRegistrationTests`, `ConflictMapConcurrencyTests` and `RuntimeSettingRegistryTests` constrain the implementation as described above.
- No vacuous tests were found.
