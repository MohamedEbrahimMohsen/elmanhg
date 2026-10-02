# Implementation r2 — Configurable SLA calendar with exam periods (#254)

## Review findings addressed
| # | What I changed | file:line |
|---|---|---|
| 1 (blocking) | PRD §2.2 target reworded: "≥ 95% replied within the SLA (24 counted hours over the reply calendar, §12.3)". This now matches §12.3 and `TeacherThreadRepository.cs:47`. | `docs/PRD.md:35` |
| NB M11 | Added `Handle_OneDayWeekendOnThursday_ReturnsSaturdayDeadlineAndSkipFlag`: weekend `Friday`, Thursday 12:00Z, expects `(24, 2026-10-03T12:00Z, true)`. The `Handle` helper gained an optional `weekendDays` argument. The mutant `> now + 2 × ReplySla` was applied and this test failed (4 run, 1 failed). The mutant was then reverted. | `api/Elmanhg.Tests/Application/Features/TeacherThreads/GetTeacherReplyDeadline/GetTeacherReplyDeadlineHandlerTests.cs:39` |
| NB DueAt | Added `DueAt_UndefinedKind_Throws`, which passes `(TeacherThreadSlaEventKind)99` and expects `ArgumentOutOfRangeException("kind")`. | `api/Elmanhg.Tests/Domain/TeacherThreads/TeacherThreadSlaPolicyTests.cs:25` |
| NB worker log | A reschedule failure now logs at `Warning` when it is a `ConflictCoreException` (lost xmin race) and at `Error` otherwise. Added test `Sweep_RescheduleLosesConcurrencyRace_LogsWarning`. `docs/ask-teacher.md` (SLA, Recompute) is updated to match. | `api/Elmanhg.Api/Workers/TeacherThreadSlaWorker.cs:78`, `api/Elmanhg.Tests/Api/Workers/TeacherThreadSlaWorkerTests.cs:183` |
| NB deployment | Three `SlaCalendar__*` rows added to the Ask a Teacher env table. | `docs/deployment.md:252-254` |

The other non-blockers (RescheduleSla no-op write, the #222 status cell, the ExamPeriodsSection timeout) were not requested and are left as they were.

## Integration of origin/main (#255, PR #266)
Steps run: `git stash -u`, `git fetch origin main`, `git merge origin/main` (fast-forward to 7a85215b), `git stash pop`. That left 14 conflicts. After resolving them I ran `git reset` to unstage (nothing is committed) and dropped the stash.

| File | Resolution |
|---|---|
| `ProcessTeacherThreadSlaHandler.cs` | Both features kept. The #255 constructor is used (out-of-app repo, user repo, channels, `IRuntimeSettings`). #254's `DueSlaStages(now)` reads the stored calendar-aware stage times. Events are keyed by `WindowStartedAt`. `runtimeSettings.GetValuesAsync` now runs only for the out-of-app claim (just before `ClaimAsync`), because stage times no longer need it. The out-of-app reminder is driven by `missing`, which holds the stages newly recorded at their calendar time. Its message and marker use the stored `SlaDueAt`, which equals `SlaSchedule().DueAt(Breach)`. |
| `ProcessTeacherThreadSlaHandlerTests.cs` | #255 constructor with `new FakeRuntimeSettings().Set(OutOfAppReminderRuntimeSettings.Enabled, false)`. |
| `ProcessTeacherThreadSlaOutOfAppReminderTests.cs` (auto-merged) | Updated to the new `TeacherThreadSlaEvent.Record(..., SlaWindowStartedAt, SlaDueAt, ...)` signature. Added 2 wiring tests. (a) With a Cairo weekend calendar, the reminder goes out at the calendar SecondReminder time (2026-10-04T08:00Z) and carries the calendar deadline 10-04T12:00Z. (b) Nothing goes out at the wall-clock SecondReminder time. |
| `Application/DependencyInjection.cs`, `FakeRuntimeSettings.cs`, `RuntimeSettingValuesTests.cs` | Both kept, OutOfApp before SlaCalendar. |
| `RuntimeSettingRegistryTests.cs` | Count set to the combined 20 (11 + 3 + 3 + 3). Renamed to `Definitions_DefaultOptions_TwentyOrderedByGroup`. |
| `AppDbContextTests.cs` | Migration list in timestamp order: `…_AddTeacherThreadOutOfAppReminders` (161421) then `…_AddSlaCalendar` (161642). |
| `20261002161642_AddSlaCalendar.Designer.cs` | `BuildTargetModel` replaced with the merged snapshot body, so it now includes `TeacherThreadOutOfAppReminder`. The snapshot auto-merged. `dotnet ef migrations has-pending-model-changes` reports "No changes have been made to the model since the last migration." |
| `appsettings.example.json`, `ApiFactory.cs` | Both kept. |
| `docs/PRD.md` | §10.6 v1 settings list both; §12.1 item 3 merges the calendar clock and the out-of-app reminder ("at its calendar time"); §15 lists both entities. |
| `docs/ask-teacher.md` | Options paragraph merged. In the #255 text, "the worker still never writes `TeacherThreads`" was stale against #254 (the reschedule step writes threads). It now says that recording or sending the marker never writes `TeacherThreads`. The text also states the reminder fires at the stored calendar time and shows the stored `SlaDueAt`. |
| `docs/configuration.md` | Both setting-table blocks kept. |
| web `configuration/i18n/{ar,en}.json` | `choices` merged (channels/stages + days/zones). All web JSON checked: 0 duplicate keys. |
| `ConfigurationPage.test.tsx` | All three tests kept (out-of-app choices, reminder providers, weekend days). |
| OpenAPI / Orval | Regenerated with `dotnet build` (v1.json now has both `/api/teachers/{teacherId}/phone-number` and the exam-period and reply-deadline routes). `npm run gen:api` changes only #254's generated files. |
| Postman | Auto-merged cleanly. I checked it by hand: the JSON is valid, there are no duplicate request names, the #255 "Set teacher phone number" and the #254 exam-period and reply-deadline requests are all present, and both `invitePassword` and `examPeriodId` variables are kept. |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| #254 plan: remove `IRuntimeSettings` from `ProcessTeacherThreadSlaHandler` | #255 needs runtime values for the out-of-app claim | Kept `IRuntimeSettings` (from #255) and load the values only right before `ClaimAsync` |
| Reviewer suggestion: Warning only for `ConflictCoreException` | — | Done as suggested, in a single `logger.Log(level, …)` line. The file is 110 lines (it was 109). |

## Build & test
- `dotnet build -c Release` (api/): 0 errors. The only warnings are 7 pre-existing core-library nullable warnings.
- `dotnet ef migrations has-pending-model-changes`: "No changes have been made to the model since the last migration."
- M11 mutant run (`--filter-class *GetTeacherReplyDeadlineHandlerTests`): total 4, failed 1 (the new test). Reverted afterwards.
- `dotnet test -c Release` (api/, no appsettings.json present): total 5020, failed 0, succeeded 5020, skipped 0.
- `npm run typecheck`: clean. `npm run lint`: clean.
- `npx vitest run`: Test Files 278 passed (278), Tests 1583 passed (1583), with no timeouts this run.
- `npx prettier --check --end-of-line auto .`: "All matched files use Prettier code style!"
- `npm run build` then `npm run perf:budget`: every entry ok (the closest is quiz at 253/255 KB).

## Notes for review
- #255's marker is once per thread (`IsRecordedAsync(thread.Id)`), not once per window. A rescheduled or follow-up window never re-sends out of the app, which matches #255's documented intent.
- The marker's `SlaDueAt` is historical. A later reschedule does not rewrite it.
