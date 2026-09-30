# Implementation r2 — [E12.S1] Append-only training records (#109)

## Rework findings

| # | What I changed | File:line |
|---|---|---|
| 1 | Replaced the 29-character placeholder with `change-me-openssl-rand-hex-32-placeholder` (41 characters). It is clearly marked, is not the development key, and passes the validator in Production (smoke-test.sh) and Staging (load-test.sh). Both scripts copy this file verbatim and never override the key. | `deploy/api.env.example:10` |
| 2 | Added the const `TeacherThreadTrainingTriggerIndex = "IX_TeacherThreadTrainingRecords_ThreadId_Trigger"`, applied via `.HasDatabaseName(...)`. The name equals the EF default, so there is no migration or snapshot change. A 23505 on this index now maps to `ConflictCoreException(ErrorCodes.TeacherThreadModifiedConcurrently)` in `SaveChangesAsync`, using the existing pattern. | `AppDbContext.TrainingData.cs:11,58`; `AppDbContext.cs:154-158` |
| 2 (test) | Added `SaveChanges_StaleRate_ThrowsTeacherThreadModifiedConcurrently` and `SaveChanges_StaleFinalReply_ThrowsTeacherThreadModifiedConcurrently`, plus the helpers `SeedAnsweredThreadAsync` and `SaveStaleCopyAsync`. Mutation check: with the mapping disabled, both tests fail with the raw `DbUpdateException`. With it restored, both pass. | `api/Elmanhg.Tests/Integration/Persistence/TeacherThreadPersistenceTests.cs` |
| 3 | The code now matches the doc. `requiresKey = !IsDevelopment() && !IsEnvironment("Testing")`, and the messages say "outside Development and Testing". The build-time `GetDocument.Insider` bypass in Program.cs is unchanged. | `api/Elmanhg.Infrastructure/TrainingData/TrainingDataOptionsValidator.cs:8,13,22,27` |
| 3 (tests) | Added a `Demo` case to `Validate_EmptyKeyInRequiredEnvironment_Fails`. `Validate_DevelopmentKeyInProduction_Fails` is now a Theory covering Production and Demo. | `api/Elmanhg.Tests/Infrastructure/TrainingData/TrainingDataOptionsValidatorTests.cs` |
| 3 (docs) | deployment.md §4 row now reads "yes (every environment except Development and Testing)". The training-data.md §Student hash bullet says the same. | `docs/deployment.md:114`, `docs/training-data.md:13` |
| NB CoreDbContext | New `Core.EntityFrameworkCore.Context.DomainEventDispatcher.PublishAsync`, which publishes in rounds. Each round snapshots the tracked entities, drains their events (copy then clear), and publishes them. Events raised by handlers, whether on tracked entities or on entities the handlers add, go out in the next round. After `MaxRounds` (10) it throws `InvalidOperationException` instead of saving. `CoreDbContext.SaveChangesAsync` calls it. | `api/core-libraries/Core.EntityFrameworkCore/Context/DomainEventDispatcher.cs` (new, 36 lines); `CoreDbContext.cs:111` |
| NB (tests) | `DomainEventDispatcherTests` has 3 tests: each event is published once and cleared; events raised by a handler (on the same entity and on an added entity) are published in later rounds; a handler that never stops raising throws after `MaxRounds`. | `api/Elmanhg.Tests/Core/Persistence/DomainEventDispatcherTests.cs` (new) |
| NB docs | Fixed the lower-case sentence in `docs/ask-teacher.md:141`. `docs/training-data.md:42` now documents the index-to-409 mapping, and `:48` documents the publishing rounds and the cap. | docs |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| (not in the plan) The non-blocking CoreDbContext note | A private loop inside the generic `CoreDbContext` can only be tested against a live DbContext | Extracted the loop into a public static `DomainEventDispatcher` in the Core library (a new file) and unit-tested it with an NSubstitute `IMediator` |
| Finding 2 asked for "an integration test proving a double rate/final reply returns 409 not 500" | An HTTP-level race cannot be forced deterministically | Proved it at the persistence level instead: two scopes, stale copy, real Postgres. It asserts `ConflictCoreException(TeacherThreadModifiedConcurrently)`, which the existing middleware maps to 409 |

## Build & test
- `api/Elmanhg.Api/appsettings.json` is absent in this worktree, so there was nothing to move aside and the run matches CI.
- `dotnet test -c Release` (in `api/`): `Test run summary: Passed! total: 3553 failed: 0 succeeded: 3553 skipped: 0`. That is 3546 + 7 new.
- `dotnet build` (in `api/`): `0 Warning(s) 0 Error(s)`. `git status` shows no changes under `api/openapi`, `postman`, `web` or `ai`, so there is no drift.
- I did not run `deploy/smoke-test.sh` or `load-test.sh` (a full image build of the stack). The fix follows by construction: the key is 41 characters, is not the dev key, and the `Validate_32CharacterKeyInProduction_Succeeds` path covers it.

## Notes for review
- Draining happens before publishing. If a handler throws, the drained events are lost, but that save fails anyway. Before this change they stayed queued for a retry of the same context.
- `TeacherThreadPersistenceTests.cs` is now 106 lines, slightly over the ~100 guide.
- The catch clause has a one-line comment documenting the non-obvious ordering invariant: EF inserts the training row before the xmin UPDATE.
