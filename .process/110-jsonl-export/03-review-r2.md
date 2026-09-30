VERDICT: APPROVED

# Review r2 — [E12.S2] JSONL export (+ #229 essay-grade training records)

## Blocking
None.

Round-1 blocking #1 (orphaned export files) is resolved:
- **Claim before upload.** `RunTrainingExportHandler.cs:24-27`: `BeginRun(key, now, RunLease)` writes `FileKey` and `NextAttemptAt = now + lease`, then `SaveChangesAsync` runs, all before any read or upload. `TrainingExport.Lifecycle.cs:8-15` is guarded by `EnsurePending`.
- **30-minute lease.** `TrainingExportsOptions.cs:23-24,48` has `[Range(1,1440)]` with a default of 30. `GetDueIdsAsync` and `IsDueAt` both filter on `NextAttemptAt <= now`, so a replica that loads the export after the claim sees it as not due.
- **Retry reuses the key.** `RunTrainingExportHandler.cs:23-31`: `previousKey ?? NewKey`, then the claim save, then `DeleteAsync(previousKey)`, then an upload to the same key. A key is always on the row before any byte exists.
- **Lost claim is skipped.** `TrainingExportWorker.cs:77-81` catches only `TRAINING_EXPORT_MODIFIED_CONCURRENTLY`, logs at Information, and does not send `FailTrainingExportCommand`. `AppDbContext.cs:126` maps the `xmin` conflict to that code.
- **Final-failure sweep.** `FailAttempt` keeps `FileKey`. `TrainingExportRepository.cs:27` also lists `Failed && FileKey != null`. `ExpireTrainingExportHandler.cs:14-26` deletes the file first and then `DiscardFile` clears the key; the status stays `Failed`. Postgres sorts the null `ExpiresAt` of Failed rows last, and the deferral list prevents starvation.
- The orphan bullet is gone from `docs/training-data.md` § Export, Known limits.

## Non-blocking
- `api/Elmanhg.Tests/Application/Features/TrainingExports/RunTrainingExport/RunTrainingExportHandlerTests.cs:142-153`: on its own, `Handle_SaveFailsAfterUpload_UploadedKeyWasRecordedBeforeUploading` would also pass if the key were set only in `Complete`, because `Complete` runs before the failing second save. The property is still pinned by `Handle_StorageFails_PropagatesAndLeavesPendingWithClaimedKey` (:127-139), which checks that `FileKey` is set after a single save and a failed upload. One option is to capture `export.FileKey` inside the first save callback.
- `api/Elmanhg.Application/TrainingExports/RunTrainingExport/RunTrainingExportHandler.cs:26-27`: the second `SaveChangesAsync` departs from the one-save-per-handler rule. It is declared in the r2 Deviations, explained by the invariant comment at :26, and is required by the fix. Accepted.
- The residual lease-overrun edge case (a run longer than 30 minutes) is disclosed in the r2 notes. The stale run's final save fails on `xmin`, the worker treats that as a skip, and the file stays under a key the row still names. No orphan results.
- The round-1 non-blocking items the implementer left open are unchanged: no-op Expire audit noise, deferral reset, Cairo file-name dates, source coverage in the run handler, and `revokeObjectURL`.

## Verified
- **CI parity:** there is no `api/Elmanhg.Api/appsettings.json`. I re-ran `dotnet test api/ -c Release`: **3921/3921 passed**, 0 failed, 0 skipped. This matches the r2 claim (3904 before, plus 17 new).
- **Lease config documentation:**
  - `appsettings.example.json:39`: `"RunLeaseMinutes": 30`.
  - `deploy/api.env.example:106`: `# TrainingExports__RunLeaseMinutes=30`.
  - `docs/deployment.md:213`: a table row with the range 1–1440.
  - Code default: `TrainingExportsOptions.cs:24`, 30.
  - `ApiFactory` needs no entry. It overrides only the kill switches (`SweepEnabled` and `RetentionSweepEnabled` are both off), and the lease default is safe for tests, following the existing pattern where only settings that must differ are overridden.
- **Timestamp exemption narrowed:** `TrainingDataScrubber.cs:50-54`. A whole-value GUID is still exempt anywhere. An ISO value is exempt only when its key ends in `At` (keys are propagated through arrays at :42), it matches the strict full-string regex, and `DateTimeOffset.TryParse` accepts it. Tests cover ISO-shaped student `text` being scrubbed, `sentAt:"2990-10-12T34:56:78"` being scrubbed, a prefixed `askedAt` being scrubbed, and GUIDs in arrays being kept. The JSON-number decision is documented in `docs/training-data.md` § Export, PII scrubbing.
- **nosniff:** `TrainingExportsController.cs:48` sets `XContentTypeOptions = "nosniff"`, and the endpoint integration test asserts it. The WHY comment in `TrainingExportJson` is now true.
- **Whitespace in `api/core-libraries`:** there are no modified or untracked files under `api/core-libraries` (`git ls-files -m` and `-o` both return 0). No changed file anywhere is whitespace-only (`git diff --stat` and `git diff -w --stat` are identical: 53 files, +1047/−37). The CRLF/LF warnings on `deploy/api.env.example` and others are working-copy line endings that git normalises on commit. They do not show up in the diff.
- **Docs sync:** `docs/training-data.md` § Export (steps 2–3, Retention, PII scrubbing) and `docs/audit-log.md` (Expire row for the Failed-file case) agree with the code. No divergence.
- **Postman:** no contract change in r2; round 1 already verified the collection.
- **New tests exist with the claimed names:** `TrainingExportRunTests` (6), `TrainingExportClaimTests` (2), three new run-handler tests, `Handle_FailedExportWithLeftoverFile_DeletesFileAndClearsKey`, `Expire_FailedExportWithLeftoverFile_IsListedAndDeletesFile`, and `Sweep_ClaimLostToAnotherRun_SkipsWithoutRecordingFailure`.

## Test quality
- `RunTrainingExportHandlerTests`: strong. The ordered event log (`save`, `delete:key`, `upload:key`, `save`) pins claim-before-upload, delete-before-upload on retry, key reuse, and "no storage access when the claim is lost". One weak spot is noted above.
- `TrainingExportRunTests`: exact tuples for key, lease, the microsecond-truncated `UpdationDate` and the due-window boundary, plus the guard codes for `BeginRun` and `DiscardFile`.
- `TrainingExportClaimTests.BeginRun_TwoRunsLoadedTogether_OnlyTheFirstClaimSaves`: exercises the real Postgres `xmin` conflict deterministically. `Run_TwoConcurrentRuns_WriteExactlyOneFile` checks on disk that exactly one file exists and that it matches `FileKey`.
- Expire unit and integration tests: the file is deleted from disk, the status stays Failed, and the key is cleared. The integration test also proves `GetExpiredIdsAsync` lists the row.
- Worker test: `DidNotReceive` on `FailTrainingExportCommand`, and the next item still runs.
- No vacuous tests.
