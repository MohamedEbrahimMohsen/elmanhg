VERDICT: APPROVED

# CodeRabbit verify: #74 Attempt log and session model (PR #165)

Scope: the uncommitted RC1 to RC3 fixes on `feature/74-attempt-log-and-session-model`, checked against `06-coderabbit-triage.md` and `07-coderabbit-rework.md`. I read every changed file end to end.

## Blocking
None.

## Non-blocking
- `api/Elmanhg.Tests/Application/Features/Sessions/SessionsOptionsTests.cs:29`: the Default-outside-range case asserts only the exception type, not the failure message. It still constrains the code, because `[Range(1,100)]` accepts 30, so only the new cross-field `.Validate` can make it throw. Asserting the message, as line 19 does, would be stricter.

## Verified
- **RC1 token.** `Session.Version` (`api/Elmanhg.Domain/Sessions/Session.cs:20`) is mapped with `.IsRowVersion()` (`api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs:201`). The snapshot (`AppDbContextModelSnapshot.cs:953-957`) records it as an `xmin`/`xid` concurrency token with `ValueGeneratedOnAddOrUpdate`. `Submit` (`Session.Submission.cs:19`), `Resume` (`Session.cs:66`) and `RecordAttempt` (`Session.Answering.cs:36`) all call `Touch`, so every session-mutating save issues a guarded UPDATE.
- **RC1 409 scope.** The new catch (`AppDbContext.cs:51-54`) is filtered on `exception.Entries.Any(x => x.Entity is Session)`, so it maps only Session conflicts.
  - A `DbUpdateConcurrencyException` on any other entity fails this filter.
  - It cannot match the later catches either, because each of those requires `InnerException is PostgresException`. It therefore propagates unchanged; nothing else is swallowed.
  - The catch comes before the `DbUpdateException` catches, which is the correct order for the subtype.
  - `ErrorCodes.SessionModifiedConcurrently` (`ErrorCodes.cs:121`) has en and ar resx entries (`Messages.en.resx:252`, `Messages.ar.resx:252`).
- **RC1 migration.** `20260928123459_AddSessionVersion.cs` contains the Npgsql-standard `AddColumn<uint>("xmin", type "xid", rowVersion)`, for which Npgsql emits no DDL.
  - `has-pending-model-changes` (Release, env conn string) reports "No changes have been made to the model since the last migration."
  - `AppDbContextTests.cs:23` expects `_AddSessionVersion` as the 15th migration, and the test passes.
- **RC1 tests are deterministic.** `SessionPersistenceTests.cs:62,81` use two DI scopes and sequential awaited saves, with no timers or parallelism.
  - Test 1 asserts 409 and zero attempts, which proves the whole save rolled back.
  - Test 2 asserts 409, one attempt, and `SubmittedAt` null.
  - Without the token, the loser's save would succeed and neither `ThrowAsync<ConflictCoreException>` could pass.
- **Mutation check (asked for, only partly achievable).**
  - I removed the `.IsRowVersion()` line as instructed, rebuilt, and ran `SessionPersistenceTests`: 6 of 6 failed.
  - They failed at the `ApiFactory` fixture (`PendingModelChangesWarning` at `ApiFactory.cs:39`), before any test body ran. Deleting the line also changes the model, so this literal mutation cannot isolate the race assertions.
  - I tried a sharper mutation that keeps the `xmin` mapping but drops only the token. It needs the pending-model warning suppressed, and that build was denied by the permission classifier, so I did not pursue it.
  - I restored `AppDbContext.cs` from a byte-for-byte backup and confirmed it with `diff` (no output) and an unchanged `git diff --stat`.
  - The claim that the tests fail without the token therefore rests on reading the code above, plus the triage's own scratch-worktree reproduction. My run did not show it directly.
- **RC2.** `Session.Answering.cs:16-19` is now the first statement of `RecordAttempt`.
  - `SessionItem` inherits `Core.DDD.Entities.Entity` without an `Equals` override, so `Contains` uses reference identity, and an item taken from another session cannot pass.
  - `RecordAttempt_ItemFromAnotherSession_ThrowsAndAddsNoAttempt` (`SessionAnsweringTests.cs:32`) asserts the throw, the message and an empty `Attempts`. It would fail without the guard, because the foreign item would otherwise be recorded.
- **RC3.** `DependencyInjection.cs:25-27` adds a cross-field `.Validate(Min <= Default <= Max)` before `ValidateOnStart`.
  - `SessionsOptionsTests` covers Min>Max (with the exact message), Default outside the range, and the defaults resolving to (5, 10, 20).
  - It uses the real `AddApplication()`, so it constrains the production registration.
- **Docs.** `docs/sessions.md` agrees with the code: the `Version`/`xmin` row (:23), the Idempotency bullet (:73), the boot-fail sentence (:128) and the error-table row (:159). `docs/question-schemas.md:143` ("no concurrency token yet") is about Question approval, not Sessions, so it does not diverge.
- **Postman.** No endpoint or contract changed; only a new error code was added. No change is needed.
- **Re-runs, all observed on the restored tree:**
  - `dotnet build api/ -c Release --no-incremental`: 0 errors. The 9 warnings are all in vendored `core-libraries`, which this change does not touch.
  - OpenAPI drift: `git status --porcelain api/openapi web/src` is empty.
  - `dotnet test api/ -c Release --no-build`: 1395 total, 1395 passed, 0 failed. This matches the rework report's 1395.
  - `dotnet ef migrations has-pending-model-changes`: none pending.
  - `npm --prefix web run typecheck`: clean.

## Test quality
- `SessionPersistenceTests` (the new race tests) constrain the token and the Session-scoped mapping: the error code, the rollback and the unchanged `SubmittedAt`.
- `SessionAnsweringTests.RecordAttempt_ItemFromAnotherSession_ThrowsAndAddsNoAttempt` constrains the guard.
- `SessionsOptionsTests` constrains the cross-field validation through the real DI registration.
- No vacuous tests were found.
