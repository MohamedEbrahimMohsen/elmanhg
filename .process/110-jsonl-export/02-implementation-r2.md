# Implementation r2 — [E12.S2] JSONL export (rework of 03-review.md)

## Blocking findings addressed
| # | What I changed | Where |
|---|---|---|
| 1 | Before it uploads anything, the run now claims the export. `TrainingExport.BeginRun(fileKey, startedAt, lease)` records the key and moves `NextAttemptAt` out by `RunLeaseMinutes`, and `SaveChangesAsync` follows. That save is the `xmin` claim. A retry reuses the recorded key and calls `DeleteAsync` on it before it uploads. A final failure keeps `FileKey`. The retention sweep now also lists `Failed` exports that still have a `FileKey`: it deletes the file and `DiscardFile` clears the key. The worker treats a lost claim (`TRAINING_EXPORT_MODIFIED_CONCURRENTLY`) as a skip, not a failed attempt, so the losing run cannot fail the winner's export. The orphan bullet is removed from Known limits. | `Elmanhg.Domain/TrainingExports/TrainingExport.Lifecycle.cs` (BeginRun, DiscardFile), `TrainingExport.cs` (HasFileToDeleteAt), `Elmanhg.Application/TrainingExports/RunTrainingExport/RunTrainingExportHandler.cs:22-33`, `ExpireTrainingExport/ExpireTrainingExportHandler.cs`, `Elmanhg.Infrastructure/TrainingExports/TrainingExportRepository.cs:27`, `Elmanhg.Api/Workers/TrainingExportWorker.cs` (new catch), `Shared/Options/TrainingExportsOptions.cs` (RunLeaseMinutes 30, RunLease) |

## Non-blocking findings addressed
- **nosniff:** `TrainingExportsController` now sets `Response.Headers.XContentTypeOptions = "nosniff"`, so the WHY comment in `TrainingExportJson` is true. The integration FullFlow test asserts the header.
- **Scrubber exemption narrowed:**
  - A whole-value GUID (`D` format) is still kept anywhere. Option ids inside arrays need this.
  - An ISO timestamp is kept only when both hold:
    - its key ends in `At` (the parent key is passed down through arrays);
    - the value matches the strict full-string shape and `DateTimeOffset.TryParse` succeeds, which rejects out-of-range values such as `34:56:78`.
  - Student `text` and `answer` strings never get the timestamp exemption.
- **JSON numbers:** documented in `docs/training-data.md`. Every student-typed answer is a string, and `QUESTION_ANSWER_INVALID` rejects a number where a string is expected. The numbers in the export are written by the server. A future numeric answer shape must extend the scrubber first.

Not addressed, intentionally: the audit noise for a no-op Expire, the deferral reset in the retention worker, the Cairo file-name dates, test coverage of the other sources, and `revokeObjectURL`. None of these was in the rework brief.

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/Elmanhg.Tests/Domain/TrainingExports/TrainingExportRunTests.cs` | 80 | BeginRun: records the key, lease and not-due window; rejects a non-Pending export. A final failure keeps the key; `HasFileToDeleteAt`; `DiscardFile` |
| `api/Elmanhg.Tests/Integration/TrainingExports/TrainingExportClaimTests.cs` | 58 | Real Postgres. Two runs loaded together: only the first claim saves, and the second gets 409 `MODIFIED_CONCURRENTLY`. Two concurrent `RunTrainingExportCommand`s: Completed with exactly one file |

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.Domain/TrainingExports/TrainingExport.Lifecycle.cs` | `BeginRun`, `DiscardFile` |
| `api/Elmanhg.Domain/TrainingExports/TrainingExport.cs` | `HasFileToDeleteAt` |
| `api/Elmanhg.Application/TrainingExports/RunTrainingExport/RunTrainingExportHandler.cs` | claim save before upload; a retry deletes the previous file and reuses its key |
| `api/Elmanhg.Application/TrainingExports/ExpireTrainingExport/ExpireTrainingExportHandler.cs` | also handles a Failed export with a file |
| `api/Elmanhg.Infrastructure/TrainingExports/TrainingExportRepository.cs` | `GetExpiredIdsAsync` includes Failed rows that have a `FileKey` |
| `api/Elmanhg.Application/Shared/Options/TrainingExportsOptions.cs` | `RunLeaseMinutes` (1–1440, default 30) and `RunLease` |
| `api/Elmanhg.Api/Workers/TrainingExportWorker.cs` | a lost claim logs Information and skips `FailTrainingExportCommand` |
| `api/Elmanhg.Api/Controllers/TrainingExports/TrainingExportsController.cs` | nosniff header |
| `api/Elmanhg.Application/TrainingExports/Shared/TrainingDataScrubber.cs` | key-aware, range-checked timestamp exemption |
| `api/Elmanhg.Api/appsettings.example.json`, `deploy/api.env.example`, `docs/deployment.md` | `TrainingExports:RunLeaseMinutes` |
| `docs/training-data.md` | Export flow: claim, lease, retry delete, crash retry. Retention covers Failed rows. Scrubbing exemptions and the JSON-number decision. Orphan Known-limit removed |
| `docs/audit-log.md` | the Expire row notes the Failed-file case |
| `api/Elmanhg.Tests/.../RunTrainingExportHandlerTests.cs` | event-order log (save, delete, upload). Existing tests updated for two saves. New tests: `Handle_SaveFailsAfterUpload_UploadedKeyWasRecordedBeforeUploading`, `Handle_RetryAfterEarlierAttempt_DeletesPreviousFileThenUploadsToSameKey`, `Handle_ClaimLostToAnotherRun_ThrowsBeforeTouchingStorage`. `Handle_StorageFails_*` renamed to `..._PropagatesAndLeavesPendingWithClaimedKey` |
| `api/Elmanhg.Tests/.../ExpireTrainingExportHandlerTests.cs` | `Handle_FailedExportWithLeftoverFile_DeletesFileAndClearsKey` |
| `api/Elmanhg.Tests/Api/Workers/TrainingExportWorkerTests.cs` | `Sweep_ClaimLostToAnotherRun_SkipsWithoutRecordingFailure`; `Sweep_RunFails_*` now uses `TRAINING_EXPORT_NOT_FOUND`, because the conflict code is now a skip |
| `api/Elmanhg.Tests/.../TrainingDataScrubberTests.cs` | ISO-shaped student text is scrubbed; a timestamp key with an out-of-range or prefixed value is scrubbed; GUIDs in arrays and whole values are kept |
| `api/Elmanhg.Tests/Integration/TrainingExports/TrainingExportRetentionEndpointTests.cs` | `Expire_FailedExportWithLeftoverFile_IsListedAndDeletesFile` |
| `api/Elmanhg.Tests/Integration/TrainingExports/TrainingExportEndpointTests.cs` | asserts nosniff |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| `SaveChangesAsync` exactly once per handler (skill rule) | The reviewer's fix needs the key persisted before the upload | `RunTrainingExportHandler` saves twice: the claim, then completion. One invariant comment explains why |
| Reviewer: "a retry deletes the earlier attempt's file" (implies a new key per attempt) | If the delete ran after a claim that wrote a *new* key, a failed delete would lose the old key (orphan). If it ran before the claim, it could delete a file another replica had just completed | A retry **reuses** the recorded key: claim, then `DeleteAsync(key)`, then upload to the same key. A key is on the row before any byte is written, so no orphan is possible |
| Reviewer: `xmin` claim alone | A replica that loads the export *after* the claim commits would pass the `xmin` check and run it again | `BeginRun` also leases the export (`NextAttemptAt = now + RunLeaseMinutes`), so later loads see it as not due. A run that crashes is retried once the lease runs out. This adds a new config key |
| Reviewer: "final failure deletes the file" | Deleting inside `FailTrainingExportHandler` would put a storage call on the failure path, so a failed delete would also lose the recorded attempt | A final failure keeps `FileKey`. The retention sweep picks up Failed-with-key exports right away, not at `ExpiresAt` |
| Worker: every run exception leads to `FailTrainingExportCommand` | The losing claimant's Fail would bump `xmin` and make the winner's completion save fail | The worker skips `TRAINING_EXPORT_MODIFIED_CONCURRENTLY` (Information log, counted as a success item) |
| 2 new files were not in the plan | Needed for the requested tests (`TrainingExportTests.cs` is already 98 lines) | Created `TrainingExportRunTests.cs` and `TrainingExportClaimTests.cs` |

## Build & test
- There is no `api/Elmanhg.Api/appsettings.json` in the worktree, so CI parity holds without moving it aside.
- `dotnet build -c Release`: Build succeeded, 0 warnings.
- `dotnet test -c Release --no-build` (whole `api/`): **total 3921, failed 0, succeeded 3921** (was 3904).
- `dotnet format --verify-no-changes`: outside `core-libraries`, the only finding is the pre-existing `SubscriptionBuilder.cs(57)`. The `core-libraries` WHITESPACE findings are pre-existing and untouched.
- `api/openapi/v1.json`: the header change alters no schema, so this adds no drift beyond the branch's existing training-exports additions.
- Web and Postman were not touched (no contract change), so no web checks were run.

## Notes for review
- The first targeted test run failed 111 tests because the ApiFactory fixture threw in DisposeAsync (a container start-up hiccup). The immediate rerun and the full suite were green.
- `Run_TwoConcurrentRuns_WriteExactlyOneFile` has a nondeterministic interleaving. The invariant holds on both paths (the loser either gets a conflict or sees a not-due export), and the deterministic claim race is covered by `BeginRun_TwoRunsLoadedTogether_OnlyTheFirstClaimSaves`.
- Residual edge case: if a run exceeds the 30-minute lease, a second run may take over the same key. The stale run's final save then fails on `xmin`. On S3, an extremely late PUT from the stale run could overwrite the new file with bytes from the same query.

## Rebase onto main after #119 (student essay input), 2026-09-30

Fast-forwarded to origin/main and popped the #110 stash; resolved conflicts keeping both sides:

| File | Resolution |
|---|---|
| `api/Elmanhg.Tests/Domain/EssayGrading/EssayGradeTests.cs` | kept #119's time-taken / ToQuestionGrade / MarkApplied tests and #110's `Complete_ConfidentAssessment_RaisesEssayGradeCompleted`, `FailAttempt_ReachesMax_RaisesNoEvent` |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | migration list in timestamp order: ..., `_AddEssayGradeTimeTaken` (36th), `_AddTrainingExports` (37th) |
| `docs/essay-grading.md` | #119 lifecycle kept; `EssayGradeCompleted` → training row sentence added to the `Complete` bullet |
| `docs/training-data.md` | both sides of "When rows are written"; new decision bullets under EssayGradeTrainingRecords |

**Semantics decision (documented in `docs/training-data.md` → EssayGradeTrainingRecords).** Both rows stay. The #119 attempt row (`AttemptTrainingRecords`, `GradedBy = AI`) is the student-performance example; the `EssayGradeTrainingRecords` row is the grader example and carries what the attempt lacks: per-criterion scores/comments, justification, confidence, Graded/InReview outcome, model, prompt version. `InReview` grades have no attempt yet (#128), so they exist only there. No double counting: each export file holds exactly one source (source in the file name), unique per `AttemptId` / `EssayGradeId`; essay lines in the Attempts file are identified by `gradedBy = AI`. No code change was needed.

**Migration Designer.** `20260930131953_AddTrainingExports.Designer.cs` had been generated before `AddEssayGradeTimeTaken` (#119) and lacked `EssayGrades.AppliedAt`, `TimeTakenMilliseconds` and the `GradedAt` filtered index; its `BuildTargetModel` body was replaced with the merged snapshot's `BuildModel` body. The snapshot auto-merged correctly; `dotnet ef migrations has-pending-model-changes` → "No changes have been made to the model since the last migration."

**Build & test (worktree has no `appsettings.json`, so CI parity holds).**
- `dotnet build api/ -c Release` → 0 errors; `api/openapi/v1.json` unchanged by the rebuild (the diff vs HEAD is #110's own endpoints).
- `npm --prefix web run gen:api` → no change to the generated client.
- `dotnet test api/ -c Release` → total 3978, succeeded 3978, skipped 0.
- web: `typecheck` clean, `lint` clean, `vitest --run` 220 files / 1265 tests passed. `format:check` reports only CRLF differences from the Windows checkout; `prettier --check . --end-of-line auto` is clean.
- The stash entry (`WIP on feature/110-jsonl-export`) was dropped after resolution. Nothing committed.
