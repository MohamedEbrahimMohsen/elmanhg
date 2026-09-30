# Implementation — [E12.S1] Append-only training records (#109)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| api/Elmanhg.Domain/Sessions/AttemptsRecorded.cs | 5 | event |
| api/Elmanhg.Domain/Avatar/AvatarExchangeRecorded.cs | 5 | event |
| api/Elmanhg.Domain/TeacherThreads/TeacherThreadClosed.cs | 5 | event |
| api/Elmanhg.Domain/TeacherThreads/TeacherThreadRatedAfterClose.cs | 5 | event |
| api/Elmanhg.Domain/Questions/QuestionPlacement.cs | 3 | placement record |
| api/Elmanhg.Domain/TrainingData/AttemptTrainingRecord.cs | 56 | entity + `From` |
| api/Elmanhg.Domain/TrainingData/AvatarTrainingRecord.cs | 66 | entity + `From` |
| api/Elmanhg.Domain/TrainingData/TeacherThreadTrainingRecord.cs | 67 | entity + `From` + `ReadMessages` |
| api/Elmanhg.Domain/TrainingData/TeacherThreadTrainingTrigger.cs | 3 | enum |
| api/Elmanhg.Domain/TrainingData/TeacherThreadTrainingAuthor.cs | 3 | enum |
| api/Elmanhg.Domain/TrainingData/TeacherThreadTrainingMessage.cs | 5 | message record |
| api/Elmanhg.Domain/TrainingData/I{Attempt,Avatar,TeacherThread}TrainingRecordRepository.cs | 5 each | ports |
| api/Elmanhg.Application/Shared/TrainingData/IStudentIdHasher.cs | 6 | port |
| api/Elmanhg.Application/Events/TrainingRecords/AttemptTrainingRecordHandler.cs | 27 | handler |
| api/Elmanhg.Application/Events/TrainingRecords/AvatarTrainingRecordHandler.cs | 15 | handler |
| api/Elmanhg.Application/Events/TrainingRecords/TeacherThreadTrainingRecordHandler.cs | 25 | handler (2 events) |
| api/Elmanhg.Infrastructure/TrainingData/TrainingDataOptions.cs | 11 | options |
| api/Elmanhg.Infrastructure/TrainingData/TrainingDataOptionsValidator.cs | 30 | startup validator |
| api/Elmanhg.Infrastructure/TrainingData/HmacStudentIdHasher.cs | 13 | HMAC-SHA256 hasher |
| api/Elmanhg.Infrastructure/TrainingData/TrainingDataServiceCollectionExtensions.cs | 16 | DI |
| api/Elmanhg.Infrastructure/TrainingData/{Attempt,Avatar,TeacherThread}TrainingRecordRepository.cs | 7 each | repositories |
| api/Elmanhg.Infrastructure/Data/Context/AppDbContext.TrainingData.cs | 62 | partial: table consts, DbSets, EF mapping |
| api/Elmanhg.Infrastructure/Migrations/20260930053010_AddTrainingRecords.cs (+ .Designer.cs) | 187 | 3 tables, indexes, 6 triggers; `Down` drops triggers first |
| docs/training-data.md | 96 | purpose, hash, tables, write rules, append-only, not-stored, checklist, consumers |
| api/Elmanhg.Tests/Domain/TrainingData/{AttemptTrainingRecordTests,AvatarTrainingRecordTests,TeacherThreadTrainingRecordTests}.cs | 60 / 49 / 78 | plan rows 1–11 |
| api/Elmanhg.Tests/Domain/Sessions/AttemptsRecordedEventTests.cs | 64 | rows 12–15 |
| api/Elmanhg.Tests/Domain/Avatar/AvatarExchangeRecordedEventTests.cs | 21 | row 16 |
| api/Elmanhg.Tests/Domain/TeacherThreads/TeacherThreadTrainingEventsTests.cs | 56 | rows 17–20 |
| api/Elmanhg.Tests/Application/Features/Events/{Attempt,Avatar,TeacherThread}TrainingRecordHandlerTests.cs | 97 / 37 / 55 | rows 21–27 |
| api/Elmanhg.Tests/Infrastructure/TrainingData/HmacStudentIdHasherTests.cs | 57 | rows 28–32 |
| api/Elmanhg.Tests/Infrastructure/TrainingData/TrainingDataOptionsValidatorTests.cs | 68 | rows 33–37 |
| api/Elmanhg.Tests/Integration/TrainingData/TrainingDataTestData.cs | 91 | helpers (`ExpectedHash`, readers, `SeedRecordIdAsync`) |
| api/Elmanhg.Tests/Integration/TrainingData/AttemptTrainingRecordTests.cs | 110 | rows 38–41 |
| api/Elmanhg.Tests/Integration/TrainingData/AvatarTrainingRecordTests.cs | 65 | rows 42–44 |
| api/Elmanhg.Tests/Integration/TrainingData/TeacherThreadTrainingRecordTests.cs | 97 | rows 45–48 |
| api/Elmanhg.Tests/Integration/Persistence/TrainingRecordAppendOnlyTests.cs | 79 | rows 49–51 |

## Files modified
| Path | Change |
|---|---|
| api/core-libraries/Core.EntityFrameworkCore/Context/CoreDbContext.cs | entities and events materialised with `.ToList()` before publishing; `Publish(domainEvent, cancellationToken).ConfigureAwait(false)`; WHY comment as planned |
| api/Elmanhg.Domain/Sessions/Session.Answering.cs | `RecordAttempt` raises `AttemptsRecorded(this, [attempt])` (new attempts only) |
| api/Elmanhg.Domain/Sessions/Session.ExamSubmission.cs | `SubmitExam` raises `AttemptsRecorded` when attempts exist |
| api/Elmanhg.Domain/Avatar/AvatarConversation.cs | `RecordExchange` uses locals and raises `AvatarExchangeRecorded` last |
| api/Elmanhg.Domain/TeacherThreads/TeacherThread.Replies.cs | final reply raises `TeacherThreadClosed` |
| api/Elmanhg.Domain/TeacherThreads/TeacherThread.FollowUps.cs | `Rate` body exactly as planned (`wasClosed` switch between the two events) |
| api/Elmanhg.Domain/Questions/IQuestionRepository.cs | `GetPlacementsAsync` |
| api/Elmanhg.Infrastructure/Questions/QuestionRepository.cs | `GetPlacementsAsync` join (IgnoreQueryFilters on both, no-tracking) |
| api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs | `partial`; `ConfigureTrainingData`; 3 soft-delete filters in the global method |
| api/Elmanhg.Infrastructure/DependencyInjection.cs | `AddTrainingData()`; 3 repository registrations |
| api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs | regenerated |
| api/Elmanhg.Api/appsettings.example.json | `"TrainingData": { "StudentIdHashKey": "" }` after `Avatar` |
| api/Elmanhg.Api/Program.cs | **not in plan** — see Deviations |
| api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs | `TestStudentIdHashKey` const + `UseSetting` |
| api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs | `thirtyThird => …_AddTrainingRecords` |
| deploy/api.env.example | Training data block after Identity |
| docs/PRD.md | §13 paragraph; §15 three entity lines |
| docs/avatar.md, docs/sessions.md, docs/ask-teacher.md | wording replacements exactly as planned |
| docs/deployment.md | §3 secrets row, §3 rotation row, §4 API identity row |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| (Untested assumption) `dotnet build` regenerates `api/openapi/v1.json` with the validator unchanged; `Program.cs` is not touched. | **The assumption is false.** The build-time document host (`GetDocument.Insider`) calls `Host.StartAsync`, so `ValidateOnStart` runs. It runs as **Production** and loads `appsettings.example.json`, where the key is empty. `dotnet build` failed with `OptionsValidationException: TrainingData:StudentIdHashKey is required in Staging and Production.` | Inside the existing `#region BUILD-TIME OPENAPI` of `Program.cs`, only that host now gets a throwaway key: `builder.Configuration.AddInMemoryCollection([new("TrainingData:StudentIdHashKey", Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32)))])`, with one WHY comment. The validator is unchanged and strict in every real host. The example file keeps `""`, so there is no committed known key. The key is random for each build, never persisted and never logged. After the change `dotnet build` succeeds and `api/openapi/v1.json` has no diff. |
| Test 40: "attempt exists and 0 rows for it" | as planned | I also assert 0 rows for the admin's hash. This is an extra assertion, not an extra test. |
| `TrainingDataTestData` helper list | Test 39 needs a per-student read, and test 38 needs the lesson's subject and unit ids | I added `ReadAttemptRecordsOfStudentAsync(factory, hash)` and `ReadLessonScopeAsync(factory, lessonId)` to the planned helper file. No new file. |

## Build & test
- `dotnet build api/` (Debug): Build succeeded, 0 warnings. `git status api/openapi` is clean, so v1.json is unchanged.
- Build-time OpenAPI check: **before** the Program.cs fix, `dotnet build Elmanhg.Api` failed with the OptionsValidationException quoted above (environment = Production). **After** the fix: Build succeeded, and there is no v1.json diff.
- `dotnet ef migrations add AddTrainingRecords --project Elmanhg.Infrastructure --startup-project Elmanhg.Api`: Done. The 6 trigger statements were added by hand to Up and Down.
- CI parity: this worktree has no `api/Elmanhg.Api/appsettings.json` (gitignored and absent), so there was nothing to move aside. Command: `dotnet test -c Release` in `api/`. Result: `Test run summary: Passed! total: 3546, failed: 0, succeeded: 3546, skipped: 0`. I ran it twice; the second run came after the mutation checks and docs edits.
- Web, ai and Postman are untouched (no endpoint change), so none of those suites were run.
- **Mutation checks.** I applied each mutant, ran the targeted classes, then restored the file. All 18 mutants were killed:
  1. Dropped `RecordAttempt` raise → 5 failed.
  2. Idempotent path raises → 2 failed.
  3. Test-mode skip disabled → 2 failed (unit + integration).
  4. Exam raise suppressed → 1 failed.
  5. Exam raise when empty → 1 failed.
  6. Unkeyed SHA-256 instead of HMAC → 6 failed.
  7. Staging not requiring the key → 1 failed.
  8. Development key accepted in Production → 1 failed.
  9. 16-char minimum → 2 failed.
  10. Rate always raises Closed → 2 failed.
  11. Final reply raises nothing → 3 failed.
  12. All authors marked Student → 2 failed.
  13. `HasImage` always false → 1 failed.
  14. Text leaks audio URL, image URL and sender id → 4 failed.
  15. Avatar raise dropped → 3 failed.
  16. `CoreDbContext` lazy enumeration restored → 2 integration failures. This shows the Core fix is load-bearing.
  17. Avatar triggers removed from the migration → 3 append-only failures.
  18. Placement guard weakened → 1 failed.

  The classifier refused none of them.

## Notes for review
- **HMAC key never logged.** Validator messages name the setting, never the value. The hasher only keeps bytes. Nothing logs options. The build-time key is random and in memory only.
- **`deploy/api.env.example` placeholder** `change-me-openssl-rand-hex-32` is 29 characters. If an operator forgets to replace it, the validator's "at least 32 characters" rule refuses to start the API. That is intended.
- **The migrate container is not affected.** `--MigrateAndExit` returns before `app.Run`, so `ValidateOnStart` does not run there.
- **Integration fix while writing test 45.** `TeacherThreadBuilder.DefaultSubmittedAt` (2026-10-01) is later than the real clock, so the HTTP reply sorted before the seeded messages. The integration thread tests now seed with `.SubmittedAt(DateTimeOffset.UtcNow.AddDays(-1))`. This is arrange-only; production ordering by `CreatedAt` is as planned.
- **File size.** `Integration/TrainingData/AttemptTrainingRecordTests.cs` is 110 lines, slightly over the ~100 guide. It holds the 4 planned tests plus the two session helpers copied from `SessionPersistenceTests`.
- **Raw SQL in the append-only tests.** They use `DbCommand` with an `@id` parameter, because `ExecuteSqlRaw` with a concatenated table name trips EF1003, and warnings are errors. The table names are compile-time consts.
- **Every new test was confirmed failing under at least one mutant**, except the success cases 36 and 37, which by nature pass under all mutants.
- Nothing was committed.
