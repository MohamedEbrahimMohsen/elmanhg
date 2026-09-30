VERDICT: APPROVED

# Review r2 — [E12.S1] Append-only training records (#109)

## Blocking
None. All three round-1 blocking findings are fixed.

### Round-1 findings
1. **Deploy example key too short: fixed.** `deploy/api.env.example:10` is now `change-me-openssl-rand-hex-32-placeholder`.
   - It is 41 characters (at least `MinStudentIdHashKeyLength` = 32) and is not `DevelopmentStudentIdHashKey`.
   - Both stacks copy this file verbatim: `smoke-test.sh:20` runs it as Production (`deploy/.env.example:16`) and `load-test.sh:26` runs it as Staging.
   - Reading `TrainingDataOptionsValidator.cs:15-28`, that value passes all three rules in both environments. I did not run the Docker stack.
2. **Stale close/rate returned 500: fixed.**
   - The index name is now a const: `AppDbContext.TrainingData.cs:11` and `:58`. It equals the EF default name, and the snapshot and migration are unchanged.
   - `AppDbContext.cs:155-159` maps a 23505 on that constraint to `ConflictCoreException(TeacherThreadModifiedConcurrently)`. It is the last catch clause, and nothing earlier catches `DbUpdateException` without a filter.
   - A double RatedAfterClose can only come from a race: `TeacherThread.FollowUps.cs:44-46` rejects a second rating, so a legitimate re-rate cannot hit this mapping.
   - The new tests `SaveChanges_StaleRate_…` and `SaveChanges_StaleFinalReply_…` (`TeacherThreadPersistenceTests.cs`) use two scopes against real Postgres. They assert the error code, so they would fail against the round-1 code, where I reproduced the raw `DbUpdateException`.
3. **Environment policy divergence: fixed.**
   - The validator now requires the key everywhere except Development and Testing: `TrainingDataOptionsValidator.cs:13`.
   - `HmacStudentIdHasher.cs:10` falls back to the development key only when the key is empty, so the fallback can now only happen in Development and Testing.
   - `docs/deployment.md` §4 and `docs/training-data.md` §Student hash say the same thing.
   - Validator tests cover Demo for both the empty-key rule and the dev-key rule. The only `ASPNETCORE_ENVIRONMENT` values in the repo are Development (launchSettings), Testing (ApiFactory), Production and Staging (deploy), so no existing host breaks.

## DomainEventDispatcher (new, `api/core-libraries/Core.EntityFrameworkCore/Context/DomainEventDispatcher.cs`)
- **Rounds.** `trackedEntities()` is re-evaluated every round (`:15`). `ChangeTracker.Entries` runs DetectChanges, so entities a handler adds are picked up in the next round.
- **No double publish.** `Drain` copies and then clears each entity's events (`:30-35`) before anything is published, so an event cannot be seen twice.
- **Ordering.** Within a round, events keep entity order and then raise order, the same as the old code. `mediator.Publish` still dispatches on the runtime type, as before.
- **Handler exceptions.** The exception propagates and `base.SaveChangesAsync` never runs, so nothing is committed. The drained events are lost, which the report discloses. I found no caller that retries the same context after a handler failure: the only same-flow catch is `ImportQuestionsReplayBehaviour.cs:17`, which handles a save-time conflict, not a publish failure.
- **Cancellation.** A cancellation token is now passed, and `.ConfigureAwait(false)` is used. The comment at `:10` states an invariant, which is allowed.
- **Cap.** It throws `InvalidOperationException` after `MaxRounds` rounds. See the non-blocking note below on the edge.
- **Suite.** The full suite passes with the dispatcher in place, and today every chain is one level deep (no training handler raises events).

## Non-blocking
- `DomainEventDispatcher.cs:13-27` has an off-by-one at the cap.
  - The loop throws after round 10 without checking whether round 10's handlers raised anything. So a chain that needs exactly 10 rounds and then stops still throws.
  - `docs/training-data.md` ("When rows are written") says "more than MaxRounds (10) rounds throws".
  - No handler in this codebase can reach this (every chain is one level deep).
  - Fix: after the loop, drain once more and throw only if that drain is non-empty. Or reword the doc to "a 10th round throws".
  - `PublishAsync_HandlersKeepRaising_ThrowsAfterMaxRounds` does not tell the two behaviours apart.
- `TeacherThreadPersistenceTests.cs` is 106 lines, slightly over the ~100-line guide. This is disclosed.

## Verified
- `dotnet test -c Release` in `api/` (no `appsettings.json` present, so it matches CI): total 3553, failed 0, succeeded 3553, exit 0. This matches the report (3546 + 7).
- Everything in the r2 report's Rework table is present at the cited locations, including the docs fixes (`ask-teacher.md:141` casing, `training-data.md` index-to-409 note and rounds note).
- The new comments (`AppDbContext.cs:157`, `Program.cs:50`, `DomainEventDispatcher.cs:10`) each state an invariant, which skill §No Comments allows.
- No endpoint changed, so no Postman or OpenAPI change is needed. The only deviations are the two disclosed ones (the dispatcher was extracted to a new Core file; the race is proved at the persistence level), and both are justified.

## Test quality
- `DomainEventDispatcherTests` pins down that each event is published once and cleared, that later rounds pick up events raised on tracked entities and on added ones, and that the round count is capped. The weak spot is the exact-cap edge noted above.
- The two new persistence tests would fail without the new mapping.
- The validator theory rows would fail with the round-1 Staging/Production-only rule.
