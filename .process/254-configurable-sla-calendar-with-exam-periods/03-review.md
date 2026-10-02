VERDICT: CHANGES_REQUESTED

# Review — Configurable SLA calendar with exam periods (#254, E18.S2)

## Blocking

### 1. PRD success metric still defines SLA compliance as "replied within 24h"
**Where:** `docs/PRD.md:35` (§2.2 Success metrics) vs `api/Elmanhg.Infrastructure/TeacherThreads/TeacherThreadRepository.cs:43-47` and `docs/PRD.md:450` (§12.3)
**Rule:** `.claude/rules/docs-sync.md`, divergence (business rule / metric definition); Review order #8
**Problem:** The change redefines SLA compliance. `RepliedWithinSla` is now "the reply's window has no Breach event", counted over the reply calendar. PRD §12.3 and `docs/dashboard.md` say this. PRD §2.2 still sets the v1 target as "≥ 95% replied within 24h", a wall-clock measure. So the PRD gives two different answers to "what counts as compliant".
**Failure:** A question sent Thursday 15:00 Cairo is answered Sunday 14:00, which is 71 wall-clock hours later. `slaComplianceRate` counts it as compliant: no breach, because the deadline is Sunday 15:00. PRD §2.2 says that reply missed the target.
**Fix:** Reword the §2.2 target to match, e.g. "≥ 95% replied within the SLA (24 counted hours over the reply calendar, §12.3)".

## Non-blocking
- **Weak mutant M11** (`api/Elmanhg.Application/TeacherThreads/GetTeacherReplyDeadline/GetTeacherReplyDeadlineHandler.cs:15`). A test is worth adding, but this does not gate. The only skip-on row (61) moves the deadline by 48 h, so it cannot tell `> now + ReplySla` apart from `> now + 2 x ReplySla`. The difference shows in a supported configuration: a one-day weekend (`weekendDays = [Friday]`) moves every crossing by exactly 24 h. Under the mutant, the weekend note would then be hidden. Suggested test: weekend `[Friday]`, Thursday 12:00Z, then `(24, 2026-10-03T12:00Z, true)`. The flag only drives UI copy, so the impact is cosmetic.
- `api/Elmanhg.Domain/TeacherThreads/TeacherThreadSlaSchedule.cs:10`: no test covers the discard arm of `DueAt`. Defined enum values cannot reach it (it is the exhaustiveness guard), so this is not gating. A one-line `(TeacherThreadSlaEventKind)99` test would close it.
- `api/Elmanhg.Api/Workers/TeacherThreadSlaWorker.cs:75-78`: a reschedule batch that loses an xmin race logs at Error. Examples are another instance, or a teacher claim between load and save. Decision 2 treats that race as expected (skill 8.7 spirit). The plan mandated Error, so this does not gate. Consider Warning for `ConflictCoreException`. The file is 109 lines, slightly over the ~100 guideline.
- `api/Elmanhg.Domain/TeacherThreads/TeacherThread.Sla.cs:22-23`: `RescheduleSla` writes and bumps xmin even when the recomputed times are identical. Example: an exam period added a year ahead changes the fingerprint, but no deadline moves. Skipping the `UpdationDate` stamp and the write when only the fingerprint changes would shrink the 409 window that Decision 2 accepts.
- `docs/deployment.md` (env tables near lines 240 and 256) has no `SlaCalendar__*` rows, although `deploy/api.env.example` has them. This is incompleteness of a doc outside the ownership map, not divergence.
- `docs/implementation-report.md:109` still lists #222 as open, awaiting the dev. Line 177 and PRD 12.3 now record the clock decision. This is the GitHub issue state, not a contradiction about product behaviour.
- `web/src/features/configuration/components/ExamPeriodsSection.test.tsx:50`: in the full vitest run, the first test timed out on `findByRole` under load. It passes alone (10/10). Consider the same `findBy` timeout the neighbouring suites use.

## Verified
- **`dotnet test -c Release` in `api/`** (no `appsettings.json` present): `total: 4936, failed: 0, succeeded: 4936`. This matches the report.
- **Web checks:**
  - `npx tsc -b` is clean and `npx eslint . --max-warnings=0` is clean.
  - The full `npx vitest run` gave 2 failures out of 1571, both `findBy` timeouts under load:
    - `ExamPeriodsSection.test.tsx` "shows the periods under the reply calendar card after loading" (new file): rerun alone, 10/10 pass.
    - `ExamPage.dragDrop.test.tsx` "autosaves placements" (untouched): rerun alone, 2/2 pass.
  - Net result: green.
- **Calendar arithmetic** (`api/Elmanhg.Domain/SlaCalendars/SlaCalendar.cs`), checked by hand against the 2026 Egypt rules:
  - Weekends are skipped and exam periods count them (`Counts` uses `Any`, which gives the union, so overlapping and touching periods are fine).
  - A window starting on Friday begins at Sunday 00:00 Cairo, which is Saturday 21:00Z.
  - The Cairo-vs-UTC boundary uses the calendar zone (row 5 vs row 6).
  - Spring forward: `StartOfDay(2026-04-24)` = 04-23T22:00Z, the transition instant. Row 10: 4 h Thursday + 20 h Sunday = 04-26T17:00Z.
  - Fall back: Thursday 10-29 runs from 21:00Z the day before to 22:00Z, 25 real hours. Row 11 gives 13 h + 11 h = 11-01T09:00Z.
  - Iteration is bounded by `MaxDaysScanned` (3660). Exam periods only add counted days, so no holiday can lengthen a non-counted run beyond the weekend. A seven-day weekend with skip on is refused at startup (`SlaCalendarOptionsValidator.cs:22`) and at runtime (`SlaCalendarRuntimeSettings.cs:23`, integration row 110).
  - Results are UTC and keep microsecond precision.
- **Fingerprint** (`TeacherThreadSlaPolicy.cs:27-36`): covers reply hours, both reminder hours, the skip flag, sorted weekend days, the zone id, and sorted exam-period ranges. It uses invariant culture and SHA-256 lower hex. Names are excluded, correctly, because they do not affect deadlines.
- **Recompute:**
  - It is bounded: page 1 of `BatchSize`, and rescheduled rows leave the filter (`RescheduleTeacherThreadSlasHandler.cs:15`). The worker loops only while a batch is full, with one scope per batch.
  - The window start never moves (`TeacherThread.Sla.cs:22`).
  - In steady state no rows match, there is no `SaveChangesAsync` and xmin is untouched (row 59).
  - The due query reads the stored columns (`TeacherThreadRepository.cs:15`). The event key is `(ThreadId, Kind, WindowStartedAt)`, a unique index plus the process-handler filter. Row 115 proves no resend: it would fail with the old `SlaDueAt` key, because a second FirstReminder would be recorded.
- **Migration** (`20261002161642_AddSlaCalendar.cs:72-75`):
  - The backfill SQL is verbatim from the plan and runs before the index swap. There are no unplanned drops or renames, and Down reverses Up.
  - Open threads: the window start is the latest student message (equal to the follow-up `at` written by `FollowUp`). Reminders are set to `SlaDueAt` (late, never early), and the empty fingerprint makes the first sweep recompute.
  - Events: the window is the latest student message at or before `OccurredAt`. The old unique key `(ThreadId, Kind, SlaDueAt)` maps one-to-one onto windows, so the new unique index cannot collide.
- **Concurrency:** only the reschedule step writes `TeacherThreads`. The existing xmin mapping gives `409 TEACHER_THREAD_MODIFIED_CONCURRENTLY`, as documented in `docs/ask-teacher.md` SLA section and `docs/configuration.md` 8.
- **Exam periods:**
  - `ConfigurationManage` is on all four actions.
  - Audit actions are `ExamPeriod.Create`, `Update` and `Delete`; `ExamPeriod : IAuditedEntity`.
  - Validation: required name (whitespace rejected by `NotEmpty`), max length from options, end >= start, at most 120 days inclusive, and the DB check constraint.
  - Soft delete sits in the global filter only. The xmin conflict is mapped to 409 (persistence row 120).
  - The ar/en UI covers loading, error with retry, empty, create, edit and delete. Visual classes are design tokens and match `RuntimeSettingGroupCard`.
- **Dashboard:** compliance uses the breach EXISTS on `WindowStartedAt = q.CreatedAt` (`TeacherThreadRepository.cs:47`). Row 100 fails under the old wall-clock rule. The new semantics are documented in PRD 12.3, `docs/dashboard.md` and the `docs/ask-teacher.md` known limits (sweep-lag caveat). Finding 1 covers the one stale PRD line.
- **Contract fidelity:**
  - Every file in Files to create exists, and nothing extra was created.
  - Signatures match.
  - The #255 touch points are unchanged: `missing` after `SaveChangesAsync`, plus `SlaSchedule()` and `DueAt(kind)`.
  - Every deviation is disclosed and verified: the T5 file already existed; `AppDbContextTests` migration list; `RuntimeSettingValuesTests` key list; the extra row-3 assertion (`SlaCalendarTests.cs:28`, kills M4); the `implementation-report.md` #222 cell; `Enum.GetNames<DayOfWeek>().Length`; Postman order.
- **Plan test rows:** spot-checked 50 names, all present.
- **Postman:** the four exam-period requests (Configuration folder, chained `examPeriodId`, plausible bodies within 120 days) and Get reply deadline (AskTeacher folder) match routes, methods and OpenAPI (`api/openapi/v1.json:577, 6236, 6323`).
- **Docs:** PRD 10.6, 12.1, 12.3, 15, 16 and 17.11 are updated. Also updated: `ask-teacher.md`, `configuration.md` (1, 2, 5, 8), `dashboard.md`, `audit-log.md`, `claude-design-prompt.md`, `prototype.md` and `implementation-report.md`. No stale "never pauses" or "worker never writes TeacherThreads" text remains.

## Test quality
- `SlaCalendarTests`, `TeacherThreadSlaPolicyTests`, `TeacherThreadSlaTests` and `ExamPeriodTests` use exact instants with no doubles. They constrain DST, the zone and boundaries (M1, M3, M4, M5 and M6 killed).
- `RescheduleTeacherThreadSlasHandlerTests` compiles and runs the captured filter against a matching thread, a stale open thread and a stale answered thread. This is real, not a mock echo.
- `ProcessTeacherThreadSlaHandlerTests` (+2) constrain the window key (M9).
- The worker tests constrain ordering, batch looping and failure isolation (M15).
- `TeacherThreadSlaCalendarTests` and `AskTeacherMetricsEndpointTests` row 100 exercise the SQL paths against Postgres and would fail under the old semantics.
- Weak spot: `GetTeacherReplyDeadlineHandlerTests` does not constrain the upper side of the `SkipsUncountedDays` threshold (M11, see Non-blocking).
- None of the new test classes only asserts what a substitute was told to return.
