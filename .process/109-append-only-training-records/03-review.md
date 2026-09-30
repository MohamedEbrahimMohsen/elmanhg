VERDICT: CHANGES_REQUESTED

# Review — [E12.S1] Append-only training records (#109)

## Blocking

### 1. The deploy example key is 29 characters, so the CI smoke and load-test stacks cannot start the API
**Where:** `deploy/api.env.example:10`; used verbatim by `deploy/smoke-test.sh:20` (Production, from `deploy/.env.example:16`) and `deploy/load-test.sh:21,26` (Staging); both run in `.github/workflows/images.yml:35,38` (`deploy-smoke`)
**Rule:** `docs/deployment.md` §3 ("Otherwise the baked example default applies"); existing placeholder convention (`CoreJwt__Key=change-me-openssl-rand-base64-48` is 32 characters and passes its validator)
**Problem:** `TrainingData__StudentIdHashKey=change-me-openssl-rand-hex-32` is 29 characters. `TrainingDataOptionsValidator.cs:13-16` rejects any non-empty key under 32 characters in every environment, and `ValidateOnStart` makes that fatal. 02-implementation.md "Notes for review" calls this intended, but it missed that both whole-stack scripts copy `api.env.example` and override nothing in it for this key.
**Failure:** `bash deploy/smoke-test.sh` → the api container throws `OptionsValidationException: TrainingData:StudentIdHashKey must be at least 32 characters.` on start → `deploy-smoke` fails on this PR and on main. `deploy/load-test.sh` fails the same way.
**Fix:** Either make the placeholder at least 32 characters (the CoreJwt convention), or set a throwaway key of at least 32 characters with `set_env` in `.smoke/api.env` and `.loadtest/api.env` in both scripts.

### 2. A stale or concurrent close/rate of a teacher thread now returns 500 instead of 409 TEACHER_THREAD_MODIFIED_CONCURRENTLY
**Where:** `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.TrainingData.cs:57` (unique `(ThreadId, Trigger)`); unmapped in `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs:92-155`
**Rule:** plan Decision 14 against the existing contract in `docs/ask-teacher.md` §Rules (xmin races give `409 TEACHER_THREAD_MODIFIED_CONCURRENTLY`, "mapped once in `AppDbContext.SaveChangesAsync`"); every other collidable unique index is mapped there
**Problem:** EF orders independent commands by table name. `TeacherThreadTrainingRecords` sorts before `TeacherThreads` (ordinal comparison, upper-case T before lower-case s), so the duplicate training-row INSERT fails with 23505 before the stale xmin UPDATE can raise `DbUpdateConcurrencyException`. The raw `DbUpdateException` escapes unmapped.
**Failure (reproduced):** I copied the tree to a scratch folder and added a probe mirroring `TeacherThreadPersistenceTests.SaveChanges_StaleTeacherThread_ThrowsTeacherThreadModifiedConcurrently`. Two scopes load the same `Answered` thread; the first `Rate(5)` saves; the second `Rate(4)` save throws `DbUpdateException` wrapping `PostgresException 23505` on `IX_TeacherThreadTrainingRecords_ThreadId_Trigger`, where before this change it threw `ConflictCoreException(TeacherThreadModifiedConcurrently)`. The same happens with a double-submitted final reply (two `Closed` rows) and a double rating of a Closed thread (two `RatedAfterClose` rows). The user sees HTTP 500.
**Fix:** In `AppDbContext.SaveChangesAsync`, map a unique violation on the `(ThreadId, Trigger)` index (by a constraint-name const) to `ConflictCoreException(ErrorCodes.TeacherThreadModifiedConcurrently)`. Add a persistence test for the stale-rate case (the probe above).

### 3. Docs say the key is required everywhere except Development/Testing; the code requires it only in Staging/Production
**Where:** code `api/Elmanhg.Infrastructure/TrainingData/TrainingDataOptionsValidator.cs:11` and `HmacStudentIdHasher.cs:10`; docs `docs/deployment.md` §4 API identity row ("the API refuses to start without it outside Development and Testing") and `docs/training-data.md:15` §Student hash
**Rule:** `.claude/rules/docs-sync.md` (divergence: config policy)
**Problem:** The two sources give different answers to "which hosts refuse an empty key?"
**Failure:** With `ASPNETCORE_ENVIRONMENT=Demo` (or any name other than Staging/Production) and an empty key, the API starts and hashes under the public `DevelopmentStudentIdHashKey`. `docs/deployment.md` §4 says it refuses to start.
**Fix:** Change the doc wording to "required in Staging and Production; every other environment falls back to the development key", or make the validator require the key everywhere except Development and Testing. Code and docs must end up giving the same answer.

## Non-blocking
- `api/core-libraries/Core.EntityFrameworkCore/Context/CoreDbContext.cs:111-126` — events from the snapshot are published once, in the same order, and there is no double publish. The fix is load-bearing, as mutant 16 shows. Two semantic shifts remain, and neither is hit today (no handler raises events and no training entity raises any):
  - An event raised during a handler on a snapshot entity is now silently cleared. Before this change it threw "collection modified".
  - Events on entities added by a handler are neither published nor cleared until the next save of that context.
  A later story that chains events would lose them without a signal.
- `docs/ask-teacher.md:141` — a sentence starts lower-case ("before sending. the training record keeps").
- `api/Elmanhg.Tests/Integration/TrainingData/AttemptTrainingRecordTests.cs` is 110 lines, slightly over the ~100 guide (disclosed).

## Verified
- Tests: I ran `dotnet test -c Release` in `api/` myself. There is no `appsettings.json` in this worktree, so nothing had to be moved aside. Result: 3546 total, 0 failed, 3546 succeeded, exit code 0. This matches the claim.
- `dotnet build api/Elmanhg.Api` leaves `api/openapi/v1.json` unchanged, and `postman/`, `web/` and `ai/` are untouched. No endpoint changed, so no Postman change is needed.
- Build-time key bypass (`api/Elmanhg.Api/Program.cs:48-52`): it is gated by the entry-assembly name equal to `GetDocument.Insider`, the guard the region already uses. A real host has the entry assembly `Elmanhg.Api`, the `--MigrateAndExit` path included, so the bypass cannot fire there. The key is random per run, only in memory, and never logged. The deviation is disclosed and justified.
- No key or PII leaks: validator messages name the setting and never the value; the hasher keeps only bytes; training entities are not `IAuditedEntity`, so nothing reaches `AuditLogs`. No student id, teacher id, session id, audio URL or image URL is stored (`TeacherThreadTrainingRecord.cs:44-47` stores only `HasImage` and the author enum).
- Append-only: the migration (`20260930053010_AddTrainingRecords.cs:155-175`) creates 6 triggers on `reject_append_only_mutation()`. `Down` drops them before the tables and does not drop the function. `TrainingRecordAppendOnlyTests` checks UPDATE, DELETE and TRUNCATE on all three tables for P0001, and checks that the row is unchanged.
- No foreign keys, the unique indexes are as planned, and `OccurredAt` and `StudentHash` are indexed.
- Events are raised exactly where the plan says:
  - `RecordAttempt` does not raise on the idempotent path.
  - `SubmitExam` raises only when attempts exist.
  - `Rate` uses the `wasClosed` switch.
  - `Answer` raises only on the final reply.
- Every close/rate call site loads `Messages` (`RateTeacherThreadHandler.cs:21`, `ReplyToTeacherThreadHandler.cs:25`, `SendVoiceReplyHandler.cs:25`). The auto-submit worker uses one scope per session (`ExpiredExamSubmissionWorker.cs:80`).
- All files in Files to create exist, all 52 test rows exist with the planned names, and the extra helpers in `TrainingDataTestData` are disclosed.

## Test quality
- Domain, handler, hasher and validator tests constrain the implementation:
  - The handler tests capture the actual `AddRangeAsync`/`AddAsync` arguments and assert fields derived from the input, not stub echoes. Each also checks `SaveChangesAsync` `DidNotReceive()`.
  - The hasher test compares against unkeyed SHA-256.
  - The integration tests compute the expected HMAC independently.
- Gap (finding 2): no test covers a stale or concurrent teacher-thread close/rate against the new unique index. `SaveChanges_ConcurrentFirstAnswers_KeepsOneRecord` covers attempts only; there the collision lands on the already-mapped `AttemptPerQuestionIndex`.
