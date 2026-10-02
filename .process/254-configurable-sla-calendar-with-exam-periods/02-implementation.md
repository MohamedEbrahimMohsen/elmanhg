# Implementation — Configurable SLA calendar with exam periods (#254, E18.S2)

Branch `feature/254-configurable-sla-calendar-with-exam-periods` at a91848e (contains #253/#263). Nothing committed.

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/Elmanhg.Domain/SlaCalendars/SlaDateRange.cs` | 6 | D1 inclusive date range |
| `api/Elmanhg.Domain/SlaCalendars/SlaCalendar.cs` | 51 | D2 counted-time maths (DST-safe `StartOfDay`, `AddCountedTime`) |
| `api/Elmanhg.Domain/SlaCalendars/ExamPeriod.cs` | 38 | D3 aggregate (`IAuditedEntity`, soft delete, xmin) |
| `api/Elmanhg.Domain/SlaCalendars/IExamPeriodRepository.cs` | 5 | D4 |
| `api/Elmanhg.Domain/TeacherThreads/TeacherThreadSlaSchedule.cs` | 12 | D5 record + `DueAt(kind)` (#255 hook) |
| `api/Elmanhg.Domain/TeacherThreads/TeacherThreadSlaPolicy.cs` | 37 | D6 policy, `ScheduleFrom`, SHA-256 fingerprint |
| `api/Elmanhg.Application/Shared/Options/SlaCalendarOptions.cs` | 31 | A1 |
| `api/Elmanhg.Application/Shared/Options/SlaCalendarOptionsValidator.cs` | 37 | A2 startup validation |
| `api/Elmanhg.Application/Shared/RuntimeSettings/Definitions/SlaCalendarRuntimeSettings.cs` | 25 | A3 three keys + weekend constraint |
| `api/Elmanhg.Application/TeacherThreads/Shared/TeacherThreadSlaPolicyLoader.cs` | 23 | A4 |
| `api/Elmanhg.Application/TeacherThreads/RescheduleTeacherThreadSlas/RescheduleTeacherThreadSlasCommand.cs` | 5 | A5 |
| `api/Elmanhg.Application/TeacherThreads/RescheduleTeacherThreadSlas/RescheduleTeacherThreadSlasHandler.cs` | 33 | A6 |
| `api/Elmanhg.Application/TeacherThreads/GetTeacherReplyDeadline/GetTeacherReplyDeadlineQuery.cs` | 6 | A7 |
| `api/Elmanhg.Application/TeacherThreads/GetTeacherReplyDeadline/GetTeacherReplyDeadlineHandler.cs` | 17 | A8 |
| `api/Elmanhg.Application/TeacherThreads/Shared/TeacherReplyDeadlineResult.cs` | 3 | A9 |
| `api/Elmanhg.Application/SlaCalendars/Shared/ExamPeriodResult.cs` | 8 | A10 |
| `api/Elmanhg.Application/SlaCalendars/Shared/ExamPeriodResultGenerator.cs` | 8 | A11 |
| `api/Elmanhg.Application/SlaCalendars/GetExamPeriods/GetExamPeriodsQuery.cs` / `GetExamPeriodsHandler.cs` | 6 / 16 | A12, A13 |
| `api/Elmanhg.Application/SlaCalendars/CreateExamPeriod/CreateExamPeriodCommand.cs` / `Validator.cs` / `Handler.cs` | 12 / 21 / 26 | A14–A16 |
| `api/Elmanhg.Application/SlaCalendars/UpdateExamPeriod/UpdateExamPeriodCommand.cs` / `Validator.cs` / `Handler.cs` | 12 / 22 / 27 | A17–A19 |
| `api/Elmanhg.Application/SlaCalendars/DeleteExamPeriod/DeleteExamPeriodCommand.cs` / `Validator.cs` / `Handler.cs` | 11 / 13 / 24 | A20–A22 |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.SlaCalendars.cs` | 20 | I1 |
| `api/Elmanhg.Infrastructure/SlaCalendars/ExamPeriodRepository.cs` | 7 | I2 |
| `api/Elmanhg.Infrastructure/Migrations/20261002161642_AddSlaCalendar.cs` + `.Designer.cs` | 130 + 3866 | I3, backfill SQL verbatim, run before the index swap |
| `api/Elmanhg.Tests/Builders/TeacherThreadSlaPolicies.cs` | 13 | T1 |
| `api/Elmanhg.Tests/Domain/SlaCalendars/SlaCalendarTests.cs` | 137 | T2 rows 1–18 |
| `api/Elmanhg.Tests/Domain/SlaCalendars/ExamPeriodTests.cs` | 72 | T3 rows 19–24 |
| `api/Elmanhg.Tests/Domain/TeacherThreads/TeacherThreadSlaPolicyTests.cs` | 66 | T4 rows 25–30 |
| `api/Elmanhg.Tests/Application/Shared/Options/SlaCalendarOptionsValidatorTests.cs` | 57 | T6 rows 45–50 |
| `api/Elmanhg.Tests/Application/Shared/RuntimeSettings/SlaCalendarRuntimeSettingsTests.cs` | 60 | T7 rows 51–54 |
| `api/Elmanhg.Tests/Application/Features/TeacherThreads/Shared/TeacherThreadSlaPolicyLoaderTests.cs` | 50 | T8 rows 55–57 |
| `api/Elmanhg.Tests/Application/Features/TeacherThreads/RescheduleTeacherThreadSlas/RescheduleTeacherThreadSlasHandlerTests.cs` | 71 | T9 rows 58–60 |
| `api/Elmanhg.Tests/Application/Features/TeacherThreads/GetTeacherReplyDeadline/GetTeacherReplyDeadlineHandlerTests.cs` | 50 | T10 rows 61–63 |
| `api/Elmanhg.Tests/Application/Features/SlaCalendars/**` (7 files) | 22–69 each | T11–T17 rows 64–84 |
| `api/Elmanhg.Tests/Integration/SlaCalendars/SlaCalendarTestData.cs` | 47 | T18 |
| `api/Elmanhg.Tests/Integration/SlaCalendars/ExamPeriodsEndpointTests.cs` | 166 | T19 rows 101–110 |
| `api/Elmanhg.Tests/Integration/TeacherThreads/TeacherThreadSlaCalendarTests.cs` | 135 | T20 rows 111–115 |
| `api/Elmanhg.Tests/Integration/TeacherThreads/TeacherReplyDeadlineEndpointTests.cs` | 51 | T21 rows 116–118 |
| `api/Elmanhg.Tests/Integration/Persistence/ExamPeriodPersistenceTests.cs` | 60 | T22 rows 119–120 |
| `web/src/features/configuration/schemas/examPeriodSchema.ts` / `.test.ts` | 14 / 47 | W1, W8 (row 122) |
| `web/src/features/configuration/hooks/useExamPeriods.ts` | 5 | W2 |
| `web/src/features/configuration/hooks/useExamPeriodMutations.ts` | 40 | W3 |
| `web/src/features/configuration/components/ExamPeriodsSection.tsx` | 94 | W4 |
| `web/src/features/configuration/components/ExamPeriodRow.tsx` | 50 | W5 |
| `web/src/features/configuration/components/ExamPeriodDialog.tsx` | 70 | W6 |
| `web/src/features/configuration/components/DeleteExamPeriodDialog.tsx` | 49 | W7 |
| `web/src/features/configuration/components/ExamPeriodsSection.test.tsx` | 206 | W9 rows 123–132 |
| `web/src/shared/api/generated/model/{createExamPeriodCommand,examPeriodResult,teacherReplyDeadlineResult,updateExamPeriodRequest}.ts` | — | Orval output (`gen:api`) |

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.Domain/TeacherThreads/TeacherThread.cs`, `.FollowUps.cs`, `.Sla.cs` | four stored schedule props + fingerprint; `Submit`/`FollowUp` take `TeacherThreadSlaPolicy`; `.Sla.cs` rewritten (`SlaSchedule()`, `DueSlaStages(now)`, `RescheduleSla`, `ApplySlaSchedule`) |
| `api/Elmanhg.Domain/TeacherThreads/TeacherThreadSlaEvent.cs`, `ITeacherThreadRepository.cs` | `WindowStartedAt`; new `Record`, `GetSlaDueIdsAsync`, `GetReplyStatsAsync` signatures |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs`, `DependencyInjection.cs`, `RuntimeSettingGroup.cs`, `AskTeacherRuntimeSettings.cs` | 10 codes; options + validator + definitions registration; `SlaCalendar` group after `AskTeacher`; reply-hours description |
| `CreateTeacherThreadHandler`, `FollowUpTeacherThreadHandler`, `GetDueSlaThreadIdsHandler`, `ProcessTeacherThreadSlaHandler`, `GetTeacherInboxRemindersHandler`, `GetAskTeacherMetricsHandler`, `GetMyTeacherStatsHandler` | as planned (policy loader; no settings in the sweep handlers; window key; 5-arg reply stats). `missing` list and its position after `SaveChangesAsync` unchanged |
| `api/Elmanhg.Infrastructure/TeacherThreads/TeacherThreadRepository.cs` | stored-stage due query, window-keyed reminders, breach-based compliance SQL (parameterised `SqlQuery`) |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs`, `DependencyInjection.cs`, `Migrations/AppDbContextModelSnapshot.cs` | fingerprint column, unique `(ThreadId, Kind, WindowStartedAt)`, `ConfigureSlaCalendars`, global filter, `ExamPeriod` 409 catch after `RuntimeSettingOverride`; repo registration; snapshot |
| `api/Elmanhg.Api/Controllers/Configuration/ConfigurationController.cs`, `Requests.cs`, `Controllers/TeacherThreads/TeacherThreadsController.cs` | 4 exam-period actions (`ConfigurationManage`), `UpdateExamPeriodRequest`, `GetTeacherReplyDeadline` (`AskTeacherSubmit`) |
| `api/Elmanhg.Api/Workers/TeacherThreadSlaWorker.cs` | `RescheduleAsync` before listing (scope per batch, loop while full, Error log on failure) |
| `api/Elmanhg.Api/Resources/Messages.{ar,en}.resx` | 10 keys |
| `api/Elmanhg.Api/appsettings.example.json`, `deploy/api.env.example` | `SlaCalendar` section / commented env lines |
| `api/openapi/v1.json` | regenerated by `dotnet build` |
| `postman/elmanhg.postman_collection.json` | Configuration: Create, Get, Update, Delete exam period (+ `examPeriodId` variable); AskTeacher: Get reply deadline |
| Existing tests (rows 42–44, 85–94, 98–99) | `TeacherThreadBuilder`, `FakeRuntimeSettings`, `ApiFactory`, `TeacherThreadTests`, `TeacherThreadFollowUpTests`, `TeacherThreadSlaEventTests`, `ProcessTeacherThreadSlaHandlerTests` (+2 tests), `GetDueSlaThreadIdsHandlerTests`, `CreateTeacherThreadHandlerTests` (+1), `FollowUpTeacherThreadHandlerTests` (+1), `GetTeacherInboxRemindersHandlerTests`, `GetAskTeacherMetricsHandlerTests` (`Handle_PassesConfiguredReplySla` deleted per row 90), `GetMyTeacherStatsHandlerTests`, `TrainingExportLineGeneratorTests`, `RuntimeSettingRegistryTests` (renamed to `…SeventeenOrderedByGroup`), `GetRuntimeSettingsHandlerTests`, `TeacherThreadSlaWorkerTests` (+3), `TeacherThreadSlaSweepTests`, `TeacherInboxRemindersEndpointTests`, `DashboardTestData`, `AskTeacherMetricsEndpointTests` (+1) |
| `web/src/features/configuration/pages/ConfigurationPage.tsx`, `.test.tsx` (+row 133), `i18n/{en,ar}.json`, `i18n/configurationErrors.{en,ar}.json` | `Fragment` + `ExamPeriodsSection` under `SlaCalendar`; copy; 10 error strings (text insertions, existing compact objects untouched) |
| `web/src/features/askTeacher/pages/AskTeacherNewPage.tsx`, `.test.tsx`, `components/AskTeacherForm.tsx`, `i18n/{en,ar}.json` | reply-deadline query replaces the plan catalogue; deadline + weekend note; rows 134–135 |
| `web/src/test/{configurationFixtures,askTeacherFixtures}.ts`, `web/src/test/msw/server.ts` | fixtures + default deadline handler |
| `web/src/shared/api/generated/**` | `npm run gen:api` |
| Docs | `PRD.md` (§10.6, §12.1, new §12.3, §15, §16, §17 rule 11), `ask-teacher.md`, `configuration.md`, `dashboard.md`, `audit-log.md`, `claude-design-prompt.md`, `prototype.md`, `implementation-report.md` |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| T5 `Domain/TeacherThreads/TeacherThreadSlaTests.cs` is a new file | It already exists (#97) with 8 tests on the old `DueSlaStages(now, sla, first, second)` signature, one of them already named `DueSlaStages_AtSecondReminder_ReturnsFirstAndSecond` (row 34) | Kept every existing test (only the private `Stages` helper now calls `DueSlaStages(now)`), added the 10 plan methods of rows 31–41 that were missing. `…_BeforeFirstReminder_ReturnsNone`/`…_ReturnsEmpty`, `…_AtDueTime_ReturnsAllStages`/`…_AtDeadline_ReturnsAllThree`, `…_AnsweredThread_ReturnsNone`/`…_ReturnsEmpty` now overlap (old: wall clock; new: Cairo policy), deleting tests is not allowed |
| Row 121: `AppDbContextTests` passes unchanged | The repo convention (PROGRESS.md) and the task brief require each migration in its hard-coded list | Added `fortySecond => …"_AddSlaCalendar"` |
| Test plan lists no change to `RuntimeSettingValuesTests` | `Get_EveryRegisteredKey_DeserialisesToItsType` asserts one `Get` per registered key and failed with 17 keys | Added the three `SlaCalendarRuntimeSettings` keys to its list (behaviour change, not weakening) |
| Row 3 asserts only `2026-10-02T09:00Z + 12h` | Mutation `LocalDate` → UTC date survived every planned test (the Friday case at noon cannot tell the zones apart) | Added a second assertion in the same method: `2026-10-01T22:00Z` (Fri 01:00 Cairo, still Thursday in UTC) `+ 12h → 2026-10-04T09:00Z`; it kills that mutant |
| Docs table does not list `docs/implementation-report.md` | Its #222 row says the clock "never pauses", which now diverges (docs-sync rule) | Appended to that cell that the clock part is decided and built in #254; refund/reassignment stay "no" |
| A3 constraint `Count < 7`; A2 rule (3) "count ≤ 6" | Magic number | `Count < Enum.GetNames<DayOfWeek>().Length` in both (same semantics) |
| Postman order "Get, Create, Update, Delete" | Folder requests run top to bottom (PROGRESS.md) | Order Create, Get, Update, Delete; Create stores `examPeriodId` |
| `AddCountedTime` etc. | as planned | `DashboardWindow.StartOfDay` formula duplicated in Domain with the planned comment |

## Build & test
- `dotnet build api/` — 0 errors; only the 9 pre-existing core-libraries warnings.
- First `dotnet test -c Release` in `api/` (CI parity: no `appsettings.json` in the worktree; Docker/Testcontainers): `total: 4936, failed: 0, succeeded: 4936`.
- Final run after every edit (incl. the extra row-3 assertion), `dotnet test -c Release` in `api/`, no `appsettings.json`: `Test run summary: Passed! total: 4936, failed: 0, succeeded: 4936`.
- Backfill SQL checked against real Postgres via a throw-away Testcontainers test (seeded an answered + followed-up thread and a single-message thread, reset the new columns, ran the two `UPDATE`s verbatim, asserted window starts T0+2h / T0, reminders = `SlaDueAt`, fingerprint `''`, events mapped to the window before their `OccurredAt`): passed; file deleted afterwards.
- `dotnet format Elmanhg.slnx --verify-no-changes`: only CRLF/LF whitespace findings from the Windows checkout (also in untouched core-libraries) and one pre-existing finding in the untouched `Tests/Builders/SubscriptionBuilder.cs`; nothing else.
- `npm run gen:api` re-run after the final API build: no further diff in `web/src/shared/api/generated`.
- Guard grep (`DateTime.Now|UtcNow|.Result|.Wait()|new HttpClient(|FromSqlRaw|async void`) on the `.cs` diff: no output.
- Web: `npm run gen:api` (regenerated), `npx tsc -b` clean, `npx eslint . --max-warnings=0` clean, `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` clean, `npx vitest run` → `Test Files 277 passed, Tests 1571 passed`, `npm run build` ok, `npm run perf:budget` all chunks ok (entry 208/210, admin chunks within budget).
- `python -m pytest ai/` not run: the story does not touch `ai/`.

Mutation check (each mutant applied alone, unit suite run, file restored):
| # | Mutant | Result |
|---|---|---|
| M1 | `StartOfDay` uses the base offset (no DST) | killed: 3 calendar tests (Cairo boundary, spring-forward weekend, starts-on-Friday) |
| M2 | `Counts` ignores exam periods | killed: 5 (3 domain, follow-up handler, reply-deadline handler) |
| M3 | `remaining < available` instead of `<=` | killed: `AddCountedTime_EndsExactlyAtMidnightBeforeWeekend_ReturnsThatMidnight` |
| M4 | `LocalDate` uses the UTC date | survived the planned rows; killed after the extra row-3 assertion (`AddCountedTime_StartsOnFriday_ClockStartsSundayMidnight`) |
| M5 | fingerprint without ordering weekend days | killed: `Fingerprint_WeekendOrderAndPeriodOrder_DoNotMatter` |
| M6 | fingerprint without the time zone | killed: `Fingerprint_TimeZoneChanged_Changes` |
| M7 | `RescheduleSla` without the `Open` check | killed: `RescheduleSla_AnsweredThread_ReturnsFalse` |
| M8 | reschedule counts from `rescheduledAt` instead of the window start | killed: `RescheduleSla_NewFingerprint_RecomputesFromWindowStartAndStamps` |
| M9 | process handler keys recorded events by `SlaDueAt` | killed: `Handle_StageRecordedForEarlierWindow_RecordsAgainForCurrentWindow` |
| M10 | reschedule filter without `Status == Open` | killed: `Handle_PassesBatchSizeAndCurrentFingerprintFilter` |
| M11 | skip flag compares with `now + 2 × ReplySla` | survived (test data skips 48 h, so both forms agree; the `>=`/`<` boundary is covered by the skip-off row 62) |
| M12 | weekend constraint ignores the skip flag | killed: `Constraint_SkipOffAllSevenDays_Holds` |
| M13 | max-days check off by one | killed: `Validate_OverMaxDays_FailsTooLong` |
| M14 | loader drops exam periods | killed: 3 |
| M15 | worker reschedules only one batch | killed: `Sweep_FullRescheduleBatch_SendsAgainUntilShort` |
| M16 | inbox reminders keyed by `SlaDueAt` | killed: `Handle_Teacher_PassesAssignedSubjectsAndReturnsLatestReminderKind` (followed-up case) |
| W1 | schema without the range refine | killed: 1 schema test |
| W2 | weekend note shown whenever a deadline exists | killed: `hides the weekend note when no day is skipped` |
| W3 | `ExamPeriodsSection` not rendered | killed: 10 |
| W4 | mutations do not invalidate the list | killed: 2 (add, delete) |
| W5 | `EXAM_PERIOD_NAME_TOO_LONG` not mapped to the name field | killed: `shows the server error under the name field` |

Integration-only paths (repository SQL: stored-stage due query, breach-based compliance, window-keyed reminder list) are covered by rows 98–100 and 111–115, which ran green against Postgres; they were not mutated (each mutant would need a full Testcontainers run).

## Notes for review
- #255 hook points kept as planned: `TeacherThread.SlaSchedule()`, `TeacherThreadSlaSchedule.DueAt(kind)`, and `missing` in `ProcessTeacherThreadSlaHandler` right after `SaveChangesAsync`.
- `ExamPeriodResult.UpdatedAt` is `DateTimeOffset?` per the plan, but `AuditEntity.UpdationDate` is non-nullable, so it is never null in practice (the web fixture uses `null`, which the type allows).
- `FindPaginatedAsync` also runs a `COUNT` query, so the steady-state reschedule cost is two indexed `Status='Open'` queries, not one.
- Exam-period persistence tests use 2001 dates so a leftover row cannot shift a deadline in parallel tests; calendar integration tests sit in the non-parallel `RuntimeSettings` collection and clear overrides and exam periods before and after.
- `RescheduleAllAsync` in the integration tests reschedules every stale open thread in the shared test DB (other tests' threads too); this is safe because the collection runs alone.
- Row 100 uses a 2-hour-old thread with a recorded breach, so it fails under the old wall-clock compliance (`wait ≤ 24h`) and passes only with the breach rule.
- `AskTeacherNewPage.test.tsx` `openNew` now registers the deadline handler itself: the `*/api/teacher-threads/:threadId` mock registered in `openNew` otherwise shadowed `reply-deadline`.
- `ExamPeriodsSection` keys `ExamPeriodDialog` by the edited id so the form defaults reset between create and edit.
- `TeacherThreadSlaWorker.cs` is now 109 lines (plan-mandated `RescheduleAsync`), slightly over the ~100 guideline.
- Existing MSW default list: `planCatalogue` handler in `openNew` left in place although the page no longer calls it.
