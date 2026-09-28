TRIAGE: 3 to implement, 0 rejected, 0 dev-decisions

# CodeRabbit triage — PR #165 (#74 Attempt log and session model)

Branch `feature/74-attempt-log-and-session-model` @ 05c8eeb. All claims were checked against the code. RC1 was reproduced, and its fix proven, against real Postgres (Testcontainers) in a throwaway git worktree under the scratchpad. The worktree has been removed and the repo tree was not touched.

## RC1: finish vs submit race. Major, IMPLEMENT

**Claim verified: yes, reproduced.**
- `SubmitAnswerHandler.cs:24` and `FinishSessionHandler.cs:23` each load the session (Items + Attempts) in their own scope. Each commits with one `SaveChangesAsync` (`:51` / `:31`). That call is the only transaction. There is no lock and no re-read.
- `Session` has no concurrency token. `AppDbContext.cs:189-202` has none, and in the model snapshot `IsConcurrencyToken` appears only on the Identity `ConcurrencyStamp`. So the answer's `UPDATE "Sessions" SET "LastActivityAt", "UpdatedBy", "UpdationDate" WHERE "Id" = @id` succeeds even after the finish committed. The unique index `IX_Attempts_SessionId_QuestionId` does not help, because the two operations write different rows.
- Repro: the session was loaded in two contexts. Context A ran `RecordAttempt(item, B, 1.0)`, context B ran `Submit()`. B saved, then A saved. Both saves succeeded. The DB ended with `SubmittedAt` set, one attempt with score 1 of 1, and `ScorePercent = 0`.
- This is permanent: attempts are append-only (trigger) and `Submit` is a no-op once submitted (`Session.Submission.cs:10-13`). It also contradicts `docs/sessions.md` § Idempotency ("Answering a finished session returns 400 `SESSION_ALREADY_SUBMITTED`").
- Realistic trigger: the student answers the last question and taps "إنهاء التدريب" while the answer POST is still in flight.

**How the plan handles concurrency:**
- Decision 9 covers only concurrent first answers (unique index mapped to 409). Decision 15 covers only concurrent starts (partial unique index mapped to 409). Decision 16 (Finishing) says nothing about concurrency.
- The round-1 review (`03-review.md:28`) noted this race as non-blocking and deferred it to #158. I could not open #158 (no `gh`).
- The skill mandates the mechanism: SKILL.md:14 ("Concurrency tokens use PostgreSQL `xmin` (`.IsRowVersion()` on a `uint Version` property mapped to `xmin`)") and §6.8.
- #64 D21 and `docs/question-schemas.md:143` confirm there is no token or `DbUpdateConcurrencyException` mapping anywhere yet. Grep of `Core.Exceptions` and `Elmanhg.Api` found none.

**Minimal fix (validated in the scratch worktree):**
1. `api/Elmanhg.Domain/Sessions/Session.cs`: add `public uint Version { get; private set; }` after `ScorePercent`.
2. `AppDbContext.ConfigureSessions` (`AppDbContext.cs:196`): add `builder.Property(x => x.Version).IsRowVersion();`. Npgsql maps it to `xmin`.
   - Every session-mutating command already writes the Session row. `RecordAttempt` → `Touch`, `Submit`, and `Resume` all do, so every one of them now carries `WHERE xmin = @original`.
   - Replays and a repeat finish make no changes, so they send no UPDATE and cannot conflict.
3. `AppDbContext.SaveChangesAsync` (`AppDbContext.cs:45-64`): add a first catch.
   - `catch (DbUpdateConcurrencyException exception) when (exception.Entries.Any(x => x.Entity is Session))` → `throw new ConflictCoreException(ErrorCodes.SessionModifiedConcurrently, innerException: exception);`.
   - This follows the existing pattern in this file (the unique violation → 409 mapping), not a handler try/catch.
4. `api/Elmanhg.Application/Exceptions/ErrorCodes.cs:120`: add `SessionModifiedConcurrently = "SESSION_MODIFIED_CONCURRENTLY"`. Add en and ar entries to `Api/Resources/Messages.{en,ar}.resx`.
5. What the losing operation returns: **409 `SESSION_MODIFIED_CONCURRENTLY`**. The whole `SaveChangesAsync` rolls back, including the attempt INSERT (verified: 0 attempts remain). A retry resolves every case:
   - Answer lost to finish: the retry gets 400 `SESSION_ALREADY_SUBMITTED` (the documented behaviour).
   - Finish lost to answer: the retry finishes with the attempt counted.
   - Finish lost to finish: the retry gets the idempotent 200.
   - Answer lost to an answer on a different item, or to a resume: the retry succeeds.
6. Migration impact: a new migration `AddSessionVersion`. EF generates `AddColumn<uint>("xmin", type "xid", rowVersion)`, but Npgsql emits **no DDL** for the system column. Verified: `migrations script AddSessionsAndAttempts AddSessionVersion` produced only the `__EFMigrationsHistory` insert. The snapshot and Designer change.
   - `AppDbContextTests.cs:23` (`Migrate_FreshDatabase_LeavesNoPendingMigrations`) must add a 15th `_AddSessionVersion` element. It was the only failure in the full suite with the fix applied: 1390/1391. Test 112 `SaveChanges_ConcurrentFirstAnswers_ThrowsSessionQuestionAlreadyAnswered` still passed.
7. Deterministic integration tests. Add them to `SessionPersistenceTests`, reusing its `LoadAsync` two-scope pattern with no timing:
   - `SaveChanges_AnswerAfterConcurrentFinish_ThrowsSessionModifiedConcurrently`: load in scope A and scope B, A.`RecordAttempt`, B.`Submit`, save B, then save A. Assert it throws `ConflictCoreException` with `SESSION_MODIFIED_CONCURRENTLY`, and `ReadAttemptsAsync` is empty.
   - `SaveChanges_FinishAfterConcurrentAnswer_ThrowsSessionModifiedConcurrently`: the same setup, but save A then save B. Assert the same 409, the attempt count is 1, and the session is still open.
   - Both were checked in the scratch worktree against the unmapped `DbUpdateConcurrencyException`, and both pass. Without the token, the first one's save succeeds, so the test fails as it should.
8. Docs (docs-sync, divergence if omitted): `docs/sessions.md` § Idempotency needs two changes.
   - Add a bullet: a concurrent answer/finish/resume on one session is serialised by an `xmin` token; the loser gets 409 `SESSION_MODIFIED_CONCURRENTLY` and rolls back; a retry resolves it.
   - Add the code to the error table (around line 147-154).
   - No Postman change is needed.

## RC2: item must belong to the session. Minor, IMPLEMENT (defence in depth)

**Claim verified: the guard is missing, and the bug is unreachable today.**
- The only production caller gets the item from the session itself: `SubmitAnswerHandler.cs:30` calls `session.GetItem(request.QuestionId)`, which reads `Items` (`Session.Answering.cs:10`), and returns 404 `SESSION_QUESTION_NOT_FOUND` when the item is null (`:31-34`). An API caller therefore cannot pass a foreign item.
- `RecordAttempt` is still `public` (`Session.Answering.cs:14`), and `Attempt.Create` takes `QuestionId`/`QuestionVersion` from the item but `SessionId` from the session (`Attempt.cs:32-35`). A future caller (E6 exams, a worker) could write an attempt that mixes two sessions. The DB would not catch it: `Attempts` has FKs to `Sessions` and `Questions` but no composite FK to `SessionItems(SessionId, QuestionId)`.
- Plan Decision 1 names "item belongs to this session" as one of the invariants that justify the aggregate. The aggregate does not enforce it, so this is a small gap against the plan, not a CodeRabbit nicety.

**Fix:** make this the first statement of `RecordAttempt`: `if (!Items.Contains(item)) { throw new InvalidOperationException("Session item does not belong to this session."); }`.
- It is a programmer-contract violation, not user input, so it follows the Domain precedent in `QuestionGrader.cs:17,27,38` and `QuestionRevision.cs:19`. It needs no error code or resx entry.
- `Items.Contains` also covers `item.SessionId`, because Items only ever holds this session's rows.
- Test in `SessionAnsweringTests`: `RecordAttempt_ItemFromAnotherSession_ThrowsAndAddsNoAttempt`. Build two sessions, pass `other.Items[0]`, and assert the throw and that `Attempts` is empty.

## RC3: cross-field options check. Minor, IMPLEMENT

**Claim verified: yes.**
- `SessionsOptions.cs:9-16` has only per-property `[Range(1,100)]`, and the registration at `DependencyInjection.cs:25` uses `ValidateDataAnnotations().ValidateOnStart()`. So `Min=15, Max=10` boots.
- With `Min > Max`, every explicit `questionCount` fails `ValidateRange` (`StartQuizSessionValidator.cs:16`).
- With `Default` outside `[Min,Max]`, a request with no count silently serves a size the validator would reject, because `StartQuizSessionHandler.cs:42` uses `DefaultQuizSize` unvalidated.
- SKILL §8.12 says a bad cap must fail the boot.

**Fix:** at `DependencyInjection.cs:25`, add `.Validate(x => x.MinQuizSize <= x.DefaultQuizSize && x.DefaultQuizSize <= x.MaxQuizSize, "Sessions:MinQuizSize <= DefaultQuizSize <= MaxQuizSize is required.")` before `.ValidateOnStart()`.
- Test: build a `ServiceCollection` with in-memory config `Min=15, Max=10`, call `AddApplication`, or the same `AddOptions` chain if `AddApplication` pulls too much. Resolving `IOptions<SessionsOptions>.Value` should throw `OptionsValidationException`. Add a second case with valid defaults that does not throw.
- The repo has no `OptionsValidationException` test yet, so this is the first.
