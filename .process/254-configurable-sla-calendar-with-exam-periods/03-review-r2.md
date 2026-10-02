VERDICT: APPROVED

# Review r2 — Configurable SLA calendar with exam periods (#254, E18.S2)

## Blocking
None.

## Non-blocking
- origin/main has moved again since this merge: it is now at 97f73d51 (#265, payment safety switches), and the branch is still based on 7a85215b (#255). `git diff origin/main` therefore shows #265 reverted, for example in `docs/deployment.md:35` and `:171`. That is not this branch's change; `git diff HEAD` shows only #254. Before the PR, merge origin/main again. #265 adds one runtime setting to `FeatureFlagRuntimeSettings`, so `RuntimeSettingRegistryTests.cs:21` (`HaveCount(20)`) becomes 21 and the test name changes with it. #265 also touches `ErrorCodes.cs`, `Messages.*.resx`, `RuntimeSettingValuesTests.cs`, `api/openapi/v1.json`, the Postman collection, `web/src/test/msw/server.ts`, `generated/model/index.ts`, PRD, `configuration.md`, `deployment.md` and `deploy/api.env.example`. Expect conflicts there.
- `docs/deployment.md:254`: the exam-period caps row links to `ask-teacher.md`, but the caps are described in `configuration.md` §5. This is cosmetic.
- Carried over from r1 and not requested: the `RescheduleSla` no-op write (`TeacherThread.Sla.cs:22-29`), the #222 status cell, and the `ExamPeriodsSection` `findBy` timeout.

## Verified
- **Round-1 finding 1 is resolved.** `docs/PRD.md:35` now reads "≥ 95% replied within the SLA (24 counted hours over the reply calendar, §12.3)". This agrees with §12.3 ("SLA compliance counts replies whose window had no breach") and with `TeacherThreadRepository.cs` (the breach EXISTS on the window).
- **Out-of-app reminder (#255) integration**, in `ProcessTeacherThreadSlaHandler.cs:24-46` and `OutOfAppTeacherReminder.cs`:
  - It fires at the calendar stage. `missing` comes from `DueSlaStages(now)`, which reads the stored calendar-counted `FirstReminderDueAt`, `SecondReminderDueAt` and `SlaDueAt`. `ClaimAsync` fires only when the configured stage is in `missing`.
  - It fires exactly once. The guards are `IsRecordedAsync(thread.Id)` plus the unique `ThreadId` index on the marker.
  - It shows the calendar deadline. The marker and `TeacherThreadReminderMessage` both use the stored `thread.SlaDueAt`, which is the calendar deadline.
  - The claim is in the same save as the events. The marker `AddAsync` (line 45) comes before the single `SaveChangesAsync` (line 46). Sending happens after the commit (line 75). `Handle_ConfiguredStageNewlyDue_RecordsMarkerInTheSameSave` still asserts this order.
  - The re-keyed events cannot cause a resend:
    - Events are found by `WindowStartedAt == thread.SlaWindowStartedAt` (line 30). A reschedule never moves the window start.
    - The out-of-app marker is per thread, so neither a reschedule nor a re-keyed window can re-send out of the app.
    - The migration backfills event windows to the latest student message at or before `OccurredAt`. That matches the backfilled `SlaWindowStartedAt` for the current window, so already-recorded stages stay recorded.
  - The new tests check the calendar times by hand (Thursday 2026-10-01 12:00Z, Cairo, weekend Friday and Saturday, 20 h second reminder, 24 h deadline):
    - `Handle_CalendarSkipsWeekend_SendsAtCalendarStageWithCalendarDeadline` expects the send at 10-04T08:00Z with deadline 10-04T12:00Z. Hand check: 9 h on Thursday, then Sunday starts at Saturday 21:00Z, giving 08:00Z and 12:00Z.
    - `Handle_CalendarSkipsWeekendAtWallClockStage_DoesNotSend`: at the wall-clock stage (+21 h, Friday) nothing is due under the calendar.
    - Both would fail if the code used wall-clock stage times.
  - `IRuntimeSettings` is kept, as disclosed. The values are now loaded only for the claim (line 44), before the save, so a failure there saves nothing.
- **Worker** (`TeacherThreadSlaWorker.cs:78`): it logs Warning for `ConflictCoreException` and Error otherwise. `AppDbContext.cs:121,187` really does turn xmin conflicts into `ConflictCoreException`. Both log levels are tested (`TeacherThreadSlaWorkerTests.cs:165,183`).
- **Migration order:**
  - `20261002161421_AddTeacherThreadOutOfAppReminders` sorts before `20261002161642_AddSlaCalendar`.
  - The order in `AppDbContextTests` matches.
  - The `AddSlaCalendar.Designer.cs` `BuildTargetModel` body is byte-identical to the `AppDbContextModelSnapshot.BuildModel` body (diffed), and it includes `TeacherThreadOutOfAppReminder`.
- **Registry count:** 17 on HEAD plus 3 `SlaCalendar` settings gives 20 (`RuntimeSettingRegistryTests.cs:17-21`). The `SlaCalendar` group sits between `AskTeacher` and `PlanLimits`, and the definitions stay in ascending group order.
- **M11 and DueAt tests:**
  - `Handle_OneDayWeekendOnThursday_ReturnsSaturdayDeadlineAndSkipFlag` (`GetTeacherReplyDeadlineHandlerTests.cs:39`) expects 10-03T12:00Z. Hand check: Thursday 9 h, Friday skipped, Saturday starts 10-02T21:00Z, plus 15 h gives 10-03T12:00Z, a 24 h shift. It kills the `2 x ReplySla` mutant.
  - `DueAt_UndefinedKind_Throws` covers the discard arm.
- **Docs-sync:**
  - PRD §2.2, §10.6 (both #255 and #254 settings), §12.1 item 3 ("at its calendar time"), §12.3, §15 (`TeacherThread` columns, `TeacherThreadSlaEvent.window_started_at`, `TeacherThreadOutOfAppReminder`, `ExamPeriod`), §16 and §17.11 all agree with the code.
  - `ask-teacher.md`: the #255 "worker never writes `TeacherThreads`" claim is corrected to "recording/sending never writes; only the reschedule step does". The out-of-app bullet states the calendar time and the stored `SlaDueAt`.
  - `configuration.md` §1, §2, §5 and §8 are updated.
  - `deployment.md:252-254` has rows that match `SlaCalendarOptions` (defaults, ranges 1–500 and 1–366).
  - No divergence found.
- **Postman:** five new requests (reply deadline, exam-period CRUD). The diff against HEAD is additions only. The #255 "Set teacher phone number" request is kept. The duplicate names ("List teachers", "Assign subject") were already there before this change.
- **Builds and tests, run by me:**
  - `dotnet test -c Release` in `api/`, with no `appsettings.json` present: total 5020, failed 0, succeeded 5020. This matches the report.
  - `npm run typecheck`: clean.
  - `npm run lint` (`--max-warnings=0`): clean.
  - `npx vitest run`: 278 files and 1583 tests passed, with no timeouts.

## Test quality
- `ProcessTeacherThreadSlaOutOfAppReminderTests` (+2) runs the real domain schedule with no stubbed times, so it constrains the calendar-stage wiring. The existing in-order test constrains claim-before-save-before-send.
- `GetTeacherReplyDeadlineHandlerTests` now constrains both sides of the `SkipsUncountedDays` threshold.
- `TeacherThreadSlaWorkerTests` constrains the log level per exception type.
- No vacuous tests found among the r2 additions.
