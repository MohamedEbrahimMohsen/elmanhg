# Plan — Configurable SLA calendar with exam periods (#254, E18.S2)

> Built on the runtime settings store from #253. PR #263 is now merged (`origin/main` a91848e, identical to the reviewed branch). Every path below is the post-#263 tree: rebase this branch on `origin/main` before implementing.

## Goal
An admin opens the Configuration page and sets how the Ask a Teacher reply clock counts time: skip weekends on/off (default on), which days are the weekend (default Friday + Saturday), the time zone that decides where a day starts (default Africa/Cairo), and a list of exam periods (name, first day, last day) during which every day counts. Reply deadlines and both reminder times are counted over that calendar; every open question picks up a calendar, reply-time or reminder-hour change at the next SLA sweep. Students see the calendar-aware deadline before they send a question and on the thread; teachers see it in the inbox countdown; reminders, breach alerts and the SLA-compliance metric all use it.

## Scope
**In:**
- Three runtime settings (new group `SlaCalendar`) through the #253 store; startup-validated defaults from a new `SlaCalendarOptions`.
- `ExamPeriod` aggregate + table, audited admin CRUD under `/api/configuration/exam-periods`.
- Pure domain calendar maths (`SlaCalendar`) counting real elapsed hours on counted days, DST-safe.
- Stored, recomputed stage times on `TeacherThread` (window start, first/second reminder, deadline, schedule fingerprint); sweep reschedules stale open threads before listing due ones; due query, events, inbox reminders, dashboard compliance re-keyed to the window start.
- Student endpoint `GET /api/teacher-threads/reply-deadline`; ask form shows the deadline; inbox and thread views already read the stored `slaDueAt`.
- Admin UI: `SlaCalendar` group card (generic editors from #253) + `ExamPeriodsSection` under it.
- Tests across weekend, exam-period, time-zone and DST boundaries.
- Docs: PRD §10.6, §12.1, new §12.3, §15, §16, §17 rule 11; `docs/configuration.md`; `docs/ask-teacher.md`; `docs/dashboard.md`; `docs/audit-log.md`; `docs/claude-design-prompt.md`; `docs/prototype.md`; `appsettings.example.json`; `deploy/api.env.example`; Postman.

**Out:**
- Marketing copy on the landing value prop and the plan card ("within N hours") stays hours-only (Decision 17).
- Public holidays other than exam periods; per-subject calendars.
- Refund/credit on breach, unclaim/reassign on breach (#222 b, c: explicitly no).

**Deferred:** none. Everything runs offline.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | Stored computed timestamps vs in-memory over a candidate set | **Stored, recomputed.** `TeacherThread` stores `SlaWindowStartedAt` (stable), `FirstReminderDueAt`, `SecondReminderDueAt`, `SlaDueAt` and `SlaScheduleFingerprint` (SHA-256 of every input: reply hours, both reminder hours, skip flag, weekend days, time zone id, exam-period date ranges). Each sweep first reschedules `Open` threads whose fingerprint differs from the current policy, then runs the due query on the stored columns. | Correctness: one source of truth; every existing SQL reader of `SlaDueAt` (inbox order, `overdueNow` count, `isOverdue`, reminders list) stays right without change; the fingerprint is per row, so a thread created on a stale settings cache is caught on the next sweep and several instances converge. Scale: the steady-state cost is one indexed `Status='Open'` scan returning zero rows; the due query stays fully in SQL and batched; recompute work only happens after an admin change and is bounded by open threads, batched by `SlaSweepBatchSize`. In-memory evaluation would need the policy in ~12 handlers, a lower-bound prefilter that rescans every weekend-paused thread every minute, and in-memory counting for the dashboard. |
| 2 | Cost of writing `TeacherThreads` from the sweep (#97 said the worker never bumps `xmin`) | Accepted: the worker writes threads only when the fingerprint changed (admin change, or the first sweep after deploy). A teacher claim/reply racing that write gets the existing `409 TEACHER_THREAD_MODIFIED_CONCURRENTLY` and retries. Documented in `docs/ask-teacher.md`. | Rare (a few admin changes a year, ms-long batches) versus the cost of the in-memory design. |
| 3 | What names an SLA window for event de-duplication | `WindowStartedAt` (= submission or follow-up message time, never recomputed). New column on `TeacherThreadSlaEvents`; unique index becomes `(ThreadId, Kind, WindowStartedAt)`. `SlaDueAt` stays on the event as the deadline at the time it fired. | `SlaDueAt` now moves on recompute; keying on it would resend reminders. |
| 4 | Backfill | Migration SQL: thread `SlaWindowStartedAt` = latest student message `CreatedAt` (falls back to `SubmittedAt`); `FirstReminderDueAt`/`SecondReminderDueAt` = `SlaDueAt`; fingerprint `''` (stale → the first sweep recomputes every open thread before listing). Event `WindowStartedAt` = latest student message with `CreatedAt <= OccurredAt` (falls back to `SlaDueAt`). | `Submit`/`FollowUp` write the message `CreatedAt` and window start from the same `at`; an event always fires after its window's start and before the next window can open (needs an answer first). Reminder columns of non-open threads are never read. |
| 5 | What "counting hours" means | Real elapsed time (UTC) that falls on counted days. A day is a calendar date in the configured time zone, from its local midnight to the next local midnight, computed in UTC. A day counts when skip is off, or its weekday is not a weekend day, or it lies inside any exam period (inclusive). | DST-safe by construction: Cairo's 23-hour spring day and 25-hour autumn day count 23 / 25 real hours; a "24-hour" SLA is always 24 real hours of counted time. |
| 6 | Local midnight that does not exist (Cairo springs forward at 00:00) | `StartOfDay` uses the same formula as `DashboardWindow.StartOfDay`: `offset = zone.GetUtcOffset(wallClock - zone.GetUtcOffset(wallClock))`, result `(wallClock - offset).ToUniversalTime()`; it yields the transition instant. Duplicated in the domain (the domain cannot reference Application). | Proven in the dashboard; tested here on both 2026 Cairo transitions. |
| 7 | A window starting on a non-counted day | The clock starts at the next counted day's local midnight. | Follows from Decision 5. |
| 8 | Time zone setting type | `Choice` over `SlaCalendar:AllowedTimeZones` (comma-separated IANA ids, default `Africa/Cairo,Asia/Riyadh,Asia/Dubai,Asia/Kuwait,UTC`); every id must resolve at startup. | #253 has no free-text type; a curated list cannot hold a typo that breaks the sweep. |
| 9 | Weekend days setting | `ChoiceList` over `Sunday`…`Saturday` (`DayOfWeek` names, enum order), default from `SlaCalendar:WeekendDays` = `Friday,Saturday`. Constraint `SLA_CALENDAR_WEEKEND_DAYS_INVALID`: when skip is on, at most 6 days. Empty list allowed (= nothing skipped). | A 7-day weekend would make every deadline infinite outside exam periods. Comma strings avoid the configuration binder's array-append behaviour. |
| 10 | Group placement | New `RuntimeSettingGroup.SlaCalendar` immediately after `AskTeacher`. Keys `slaCalendar.skipWeekends`, `slaCalendar.weekendDays`, `slaCalendar.timeZone`. | `docs/configuration.md` §5 already names this group and the `<ExamPeriodsSection />` under its card. |
| 11 | Exam period dates | `DateOnly` start and end, both inclusive, interpreted in the calendar time zone. Overlapping periods allowed (union). Length ≤ `SlaCalendar:ExamPeriodMaxDays` (120). Name plain `string` (admin-only, not bilingual), ≤ `ExamPeriodNameMaxLength` (100). | Thanaweya Amma exams last weeks; overlap is harmless for the maths. |
| 12 | Exam period lifecycle | Soft delete (`Delete()` → `SoftDelete()`), xmin `Version` concurrency → `409 EXAM_PERIOD_MODIFIED_CONCURRENTLY`. Audited: `ExamPeriod.Create` (result), `ExamPeriod.Update`, `ExamPeriod.Delete` (command); `ExamPeriod : IAuditedEntity`. | Mirrors `RuntimeSettingOverride` and `ExamBlueprint`. |
| 13 | Exam periods list | Not paginated, ordered `StartDate` desc then `Id`. | A handful per year. |
| 14 | Does a reply-hours change apply to open threads? | Yes: reply and reminder hours are in the fingerprint. | Replaces the #253 known limit (`docs/configuration.md` §8) as that doc announces. |
| 15 | Breach already recorded when a recompute moves the deadline later | The event stays; the thread shows its new deadline. Reminders already sent are never resent (event key = window). A recompute that moves a stage into the past fires it at the next sweep. | Events are append-only. |
| 16 | SLA compliance metric (`repliedWithinSla`) | A reply counts as within SLA when its window has no `Breach` event. `GetReplyStatsAsync` drops its `replySla` parameter. | Wall-clock `wait <= N h` would contradict the calendar (a Sunday reply to a Thursday question is on time). Window history is only kept in events. A reply that lands less than one sweep interval after the deadline counts as within (documented). |
| 17 | Student copy | The ask form reads `GET /api/teacher-threads/reply-deadline` (deadline if sent now) and shows hours + that date, plus a weekend note when the deadline is further than N real hours. Landing and plan-card copy unchanged. | The story names the form copy and the inbox countdown; marketing copy is a plan property. |
| 18 | Reply-deadline endpoint auth | Policy `AskTeacherSubmit`; no current-user guard, no entitlement check: the handler reads no user data. | Read-only, non-personal. |
| 19 | Test defaults | `ApiFactory` sets `SlaCalendar:SkipWeekends=false`; `FakeRuntimeSettings` defaults to `new SlaCalendarOptions { SkipWeekends = false }`; `TeacherThreadBuilder` uses a wall-clock policy. Calendar integration tests live in the non-parallel `RuntimeSettings` collection and clear overrides and exam periods. | Existing tests use the real clock and assert `+24h`; they must not depend on the weekday they run on. |
| 20 | #255 coordination | No change to the stage list. Stage times are exposed as `TeacherThread.SlaSchedule()` / `TeacherThreadSlaSchedule.DueAt(kind)`; the hook is the `missing` list in `ProcessTeacherThreadSlaHandler` after `SaveChangesAsync` (see Touch points for #255). | Keeps the sweep diff small. |

## Touch points for #255
| # | Where | What #255 uses |
|---|-------|----------------|
| 1 | `ProcessTeacherThreadSlaHandler.Handle`, right after `await teacherThreadSlaEventRepository.SaveChangesAsync(...)` | `missing` — the stages recorded **by this call**, exactly once per window (unique `(ThreadId, Kind, WindowStartedAt)`). Add `if (missing.Contains(TeacherThreadSlaEventKind.<Stage>)) { … out-of-app reminder … }` there. |
| 2 | `TeacherThread.SlaSchedule()` → `TeacherThreadSlaSchedule` | `WindowStartedAt`, `FirstReminderDueAt`, `SecondReminderDueAt`, `SlaDueAt`, `DueAt(kind)` for message text ("reply by …"). |
| 3 | Only if #255 needs a stage at its own time | add a `TeacherThreadSlaEventKind` member, a stored `…DueAt` column set in `TeacherThreadSlaPolicy.ScheduleFrom` + `TeacherThread.ApplySlaSchedule`, a `DueAt` switch arm, a `DueSlaStages` check, and one OR-clause in `TeacherThreadRepository.GetSlaDueIdsAsync`. Nothing else in the sweep. |

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Domain/TeacherThreads/TeacherThread.cs` | Add props `public DateTimeOffset SlaWindowStartedAt { get; private set; }`, `FirstReminderDueAt`, `SecondReminderDueAt`, `public string SlaScheduleFingerprint { get; private set; } = string.Empty;`. `Submit(Guid studentId, TeacherThreadContext context, string text, string? imageUrl, DateTimeOffset submittedAt, TeacherThreadSlaPolicy slaPolicy)`: replace `SlaDueAt = at + replySla` with `thread.ApplySlaSchedule(slaPolicy.ScheduleFrom(at));` after the initializer. |
| `api/Elmanhg.Domain/TeacherThreads/TeacherThread.FollowUps.cs` | `FollowUp(string text, DateTimeOffset askedAt, TeacherThreadSlaPolicy slaPolicy)`: replace `SlaDueAt = at + replySla;` with `ApplySlaSchedule(slaPolicy.ScheduleFrom(at));`. |
| `api/Elmanhg.Domain/TeacherThreads/TeacherThread.Sla.cs` | Rewrite — see Domain behaviour. |
| `api/Elmanhg.Domain/TeacherThreads/TeacherThreadSlaEvent.cs` | Add `public DateTimeOffset WindowStartedAt { get; private set; }`; `Record(Guid threadId, TeacherThreadSlaEventKind kind, DateTimeOffset windowStartedAt, DateTimeOffset slaDueAt, Guid? teacherId, DateTimeOffset occurredAt)` sets it. |
| `api/Elmanhg.Domain/TeacherThreads/ITeacherThreadRepository.cs` | `Task<List<Guid>> GetSlaDueIdsAsync(DateTimeOffset now, IReadOnlyCollection<Guid> excludedIds, int limit, CancellationToken cancellationToken);` and `Task<TeacherReplyStats> GetReplyStatsAsync(DateTimeOffset start, DateTimeOffset end, Guid? subjectId, Guid? teacherId, CancellationToken cancellationToken);` |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | Add `// SLA CALENDAR` group (see Error codes). |
| `api/Elmanhg.Application/DependencyInjection.cs` | `services.AddOptions<SlaCalendarOptions>().BindConfiguration(SlaCalendarOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();` `services.AddSingleton<IValidateOptions<SlaCalendarOptions>, SlaCalendarOptionsValidator>();` and `services.AddSingleton<IRuntimeSettingDefinitions, SlaCalendarRuntimeSettings>();` directly after the `AskTeacherRuntimeSettings` line. |
| `api/Elmanhg.Application/Shared/RuntimeSettings/RuntimeSettingGroup.cs` | `Features, AskTeacher, SlaCalendar, PlanLimits, Grading, Uploads`. |
| `api/Elmanhg.Application/Shared/RuntimeSettings/Definitions/AskTeacherRuntimeSettings.cs` | `ReplySlaHours` description → ar `"المدة التي يجب أن يرد فيها المعلّم، محسوبة على تقويم مهلة الرد. يطبق التغيير على الأسئلة المفتوحة عند المراجعة التالية."`, en `"How long a teacher has to reply, counted over the reply calendar. A change applies to open questions at the next SLA sweep."`. Nothing else. |
| `api/Elmanhg.Application/TeacherThreads/CreateTeacherThread/CreateTeacherThreadHandler.cs` | Add ctor dep `IExamPeriodRepository examPeriodRepository` (last). Replace the `Submit(..., TimeSpan.FromHours(...))` argument with `var slaPolicy = await TeacherThreadSlaPolicyLoader.LoadAsync(runtimeSettings, examPeriodRepository, cancellationToken).ConfigureAwait(false);` (after image save) and `TeacherThread.Submit(userId, context, request.Text ?? string.Empty, imageUrl, now, slaPolicy)`. Drop the `AskTeacherRuntimeSettings` using if unused. |
| `api/Elmanhg.Application/TeacherThreads/FollowUpTeacherThread/FollowUpTeacherThreadHandler.cs` | Ctor `(ITeacherThreadRepository teacherThreadRepository, IExamPeriodRepository examPeriodRepository, IRuntimeSettings runtimeSettings, TimeProvider timeProvider, ICurrentUserService currentUserService)`. Load policy after the thread lookup; `thread.FollowUp(request.Text ?? string.Empty, now, slaPolicy)`. |
| `api/Elmanhg.Application/TeacherThreads/GetDueSlaThreadIds/GetDueSlaThreadIdsHandler.cs` | Ctor `(ITeacherThreadRepository teacherThreadRepository, IOptions<AskTeacherOptions> askTeacherOptions, TimeProvider timeProvider)`; body `return await teacherThreadRepository.GetSlaDueIdsAsync(timeProvider.GetUtcNow(), request.ExcludedIds, askTeacherOptions.Value.SlaSweepBatchSize, cancellationToken).ConfigureAwait(false);` |
| `api/Elmanhg.Application/TeacherThreads/ProcessTeacherThreadSla/ProcessTeacherThreadSlaHandler.cs` | Remove `IRuntimeSettings runtimeSettings` ctor dep and the `values` line. `var due = thread.DueSlaStages(now);`. Recorded filter `x => x.ThreadId == thread.Id && x.WindowStartedAt == thread.SlaWindowStartedAt`. `TeacherThreadSlaEvent.Record(thread.Id, kind, thread.SlaWindowStartedAt, thread.SlaDueAt, thread.TeacherId, now)`. Everything else unchanged (this is #255's hook). |
| `api/Elmanhg.Application/TeacherInbox/GetTeacherInboxReminders/GetTeacherInboxRemindersHandler.cs` | `LatestReminder`: `.Where(x => x.ThreadId == thread.Id && x.WindowStartedAt == thread.SlaWindowStartedAt)`. |
| `api/Elmanhg.Application/Dashboard/GetAskTeacherMetrics/GetAskTeacherMetricsHandler.cs` | Remove `IRuntimeSettings runtimeSettings` ctor dep; `GetReplyStatsAsync(window.Start, window.End, request.SubjectId, null, cancellationToken)`. Remove unused usings. |
| `api/Elmanhg.Application/Dashboard/GetMyTeacherStats/GetMyTeacherStatsHandler.cs` | Remove `IRuntimeSettings runtimeSettings` ctor dep; `GetReplyStatsAsync(window.Start, window.End, null, teacherId, cancellationToken)`. |
| `api/Elmanhg.Infrastructure/TeacherThreads/TeacherThreadRepository.cs` | `GetSlaDueIdsAsync`: predicate `x.Status == Open && !excludedIds.Contains(x.Id) && ((x.FirstReminderDueAt <= now && !events.Any(e => e.ThreadId == x.Id && e.WindowStartedAt == x.SlaWindowStartedAt && e.Kind == FirstReminder)) \|\| (x.SecondReminderDueAt <= now && !…SecondReminder) \|\| (x.SlaDueAt <= now && !…Breach))`, same order/limit. `GetRemindedOpenThreadsAsync`: `e.WindowStartedAt == x.SlaWindowStartedAt` instead of `e.SlaDueAt == x.SlaDueAt`. `GetReplyStatsAsync`: drop `replySla`; inner select adds `EXISTS (SELECT 1 FROM "TeacherThreadSlaEvents" AS e WHERE e."ThreadId" = m."ThreadId" AND e."WindowStartedAt" = q."CreatedAt" AND e."Kind" = 'Breach' AND e."IsDeleted" = false) AS "Breached"`; outer `COUNT(*) FILTER (WHERE NOT r."Breached")::int AS "RepliedWithinSla"`. Parameterised `SqlQuery($"…")` only. |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | `ConfigureTeacherThreads`: `builder.Property(x => x.SlaScheduleFingerprint).IsRequired().HasMaxLength(Sha256HexLength);`. `ConfigureTeacherThreadSlaEvents`: replace unique index with `builder.HasIndex(x => new { x.ThreadId, x.Kind, x.WindowStartedAt }).IsUnique();`. `OnModelCreating`: `ConfigureSlaCalendars(modelBuilder);` after `ConfigureRuntimeSettings`. Global filter: `modelBuilder.Entity<ExamPeriod>().HasQueryFilter(x => !x.IsDeleted);`. `SaveChangesAsync`: new catch `when (exception.Entries.Any(x => x.Entity is ExamPeriod))` → `ConflictCoreException(ErrorCodes.ExamPeriodModifiedConcurrently, innerException: exception)` placed after the `RuntimeSettingOverride` catch. |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | `services.AddScoped<IExamPeriodRepository, ExamPeriodRepository>();` |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated by the migration. |
| `api/Elmanhg.Api/Controllers/Configuration/ConfigurationController.cs` | Four actions (API surface). |
| `api/Elmanhg.Api/Controllers/Configuration/Requests.cs` | `public sealed record UpdateExamPeriodRequest(string? Name, DateOnly? StartDate, DateOnly? EndDate);` |
| `api/Elmanhg.Api/Controllers/TeacherThreads/TeacherThreadsController.cs` | `GetTeacherReplyDeadline` action (API surface), placed after `GetTeacherThreadContext`. |
| `api/Elmanhg.Api/Workers/TeacherThreadSlaWorker.cs` | See Worker behaviour. |
| `api/Elmanhg.Api/Resources/Messages.ar.resx`, `Messages.en.resx` | Ten keys (Error codes). |
| `api/Elmanhg.Api/appsettings.example.json` | After `"AskTeacher"`: `"SlaCalendar": { "SkipWeekends": true, "WeekendDays": "Friday,Saturday", "TimeZone": "Africa/Cairo", "AllowedTimeZones": "Africa/Cairo,Asia/Riyadh,Asia/Dubai,Asia/Kuwait,UTC", "ExamPeriodNameMaxLength": 100, "ExamPeriodMaxDays": 120 },` |
| `api/openapi/v1.json` | Regenerated by build. |
| `deploy/api.env.example` | After the `AskTeacher__ReminderListMaxCount` line: commented `# SlaCalendar__SkipWeekends=true`, `# SlaCalendar__WeekendDays=Friday,Saturday`, `# SlaCalendar__TimeZone=Africa/Cairo`, `# SlaCalendar__AllowedTimeZones=Africa/Cairo,Asia/Riyadh,Asia/Dubai,Asia/Kuwait,UTC`, `# SlaCalendar__ExamPeriodNameMaxLength=100`, `# SlaCalendar__ExamPeriodMaxDays=120`. |
| `postman/elmanhg.postman_collection.json` | In folder `Configuration`: `Get exam periods`, `Create exam period`, `Update exam period`, `Delete exam period`; in the teacher-threads folder: `Get reply deadline`. |
| `api/Elmanhg.Tests/Builders/TeacherThreadBuilder.cs` | Field `private TeacherThreadSlaPolicy _slaPolicy = TeacherThreadSlaPolicies.WallClock();`, method `public TeacherThreadBuilder WithSlaPolicy(TeacherThreadSlaPolicy slaPolicy)`; `Submit(..., _submittedAt, _slaPolicy)`; `FollowUp(..., _submittedAt.AddHours(2), _slaPolicy)`. |
| `api/Elmanhg.Tests/Fixtures/RuntimeSettings/FakeRuntimeSettings.cs` | Ctor gains last optional `SlaCalendarOptions? slaCalendar = null`; registry adds `new SlaCalendarRuntimeSettings(Options.Create(slaCalendar ?? new SlaCalendarOptions { SkipWeekends = false }))` after `AskTeacherRuntimeSettings`. Comment: `// Unit tests assert wall-clock deadlines; calendar behaviour is tested with explicit options.` |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | `builder.UseSetting("SlaCalendar:SkipWeekends", "false");` with comment `// Tests run on any weekday and assert wall-clock deadlines; calendar tests turn skipping on through a runtime override.` |
| Existing tests | See Test plan rows marked *modify* / *delete*. |
| `web/src/features/configuration/pages/ConfigurationPage.tsx` | In the groups map, render `<Fragment key={group.group}><RuntimeSettingGroupCard group={group} />{group.group === 'SlaCalendar' ? <ExamPeriodsSection /> : null}</Fragment>`. |
| `web/src/features/configuration/i18n/en.json`, `ar.json` | `groups.SlaCalendar`, `choices.*`, `examPeriods.*` (Web copy). |
| `web/src/features/configuration/i18n/configurationErrors.en.json`, `.ar.json` | Ten error keys. |
| `web/src/features/askTeacher/pages/AskTeacherNewPage.tsx` | Replace `useGetPlanCatalogue` with `useGetTeacherReplyDeadline()` (generated, `teacher-threads/teacher-threads`); pass `replyDeadline={deadline.data}` to the form. |
| `web/src/features/askTeacher/components/AskTeacherForm.tsx` | Prop `replyDeadline: TeacherReplyDeadlineResult \| undefined` replaces `replySlaHours`. When defined: `<p className="text-caption text-text-muted">{t('form.slaNote', { hours, date })}</p>` where `hours = formatNumber(Number(replyDeadline.replySlaHours), lng)` and `date = formatDate(new Date(replyDeadline.slaDueAt), lng, 'arabic-indic', { dateStyle: 'full', timeStyle: 'short' })`; then, when `replyDeadline.skipsUncountedDays`, `<p className="text-caption text-text-muted">{t('form.slaCalendarNote')}</p>`. |
| `web/src/features/askTeacher/i18n/en.json`, `ar.json` | `form.slaNote` changed, `form.slaCalendarNote` added (Web copy). |
| `web/src/test/configurationFixtures.ts` | Add `slaCalendarSetting(overrides)` (key `slaCalendar.skipWeekends`, group `SlaCalendar`, type `Boolean`, value/default `true`, labels) and `examPeriod(overrides): ExamPeriodResult` (`id` fixed uuid, `name: 'Final exams'`, `startDate: '2026-06-01'`, `endDate: '2026-07-15'`, `createdAt`, `updatedAt: null`). |
| `web/src/test/askTeacherFixtures.ts` | Add `teacherReplyDeadline(overrides): TeacherReplyDeadlineResult` (`replySlaHours: 24`, `slaDueAt: '2026-10-04T12:00:00Z'`, `skipsUncountedDays: true`). |
| `web/src/test/msw/server.ts` | Add default `getGetTeacherReplyDeadlineMockHandler(teacherReplyDeadline())`. |
| `web/src/shared/api/generated/**` | Regenerated with `npm run gen:api` (never hand-edited). |
| Docs | See Docs. |

## Files to create

### Domain — `api/Elmanhg.Domain`
| # | Path | Type | Contract |
|---|------|------|----------|
| D1 | `SlaCalendars/SlaDateRange.cs` | `public sealed record SlaDateRange(DateOnly Start, DateOnly End)` ns `Elmanhg.Domain.SlaCalendars` | `public bool Contains(DateOnly day) => day >= Start && day <= End;` |
| D2 | `SlaCalendars/SlaCalendar.cs` | `public sealed class SlaCalendar(bool skipWeekends, IReadOnlyCollection<DayOfWeek> weekendDays, TimeZoneInfo timeZone, IReadOnlyList<SlaDateRange> examPeriods)` | Props `SkipWeekends`, `WeekendDays`, `TimeZone`, `ExamPeriods` (get-only, from ctor). Const `MaxDaysScanned = 3660` with WHY comment ("A calendar with no counted day would never end a window; the runtime constraint keeps one weekday counted, so this bound is a guard, not a limit."). Methods: `public bool Counts(DateOnly day)`; `public DateOnly LocalDate(DateTimeOffset instant) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, TimeZone).DateTime);`; `public DateTimeOffset StartOfDay(DateOnly day)` (Decision 6 formula + its comment "Egypt springs forward at local midnight, so that midnight does not exist; the offset just before it gives the transition instant."); `public DateTimeOffset AddCountedTime(DateTimeOffset start, TimeSpan duration)` — body in Domain behaviour. |
| D3 | `SlaCalendars/ExamPeriod.cs` | `public class ExamPeriod : AuditEntity, IAuditedEntity` | Props `string Name`, `DateOnly StartDate`, `DateOnly EndDate`, `uint Version` (all `private set`). `private ExamPeriod(Guid id, Guid? createdBy) : base(id, createdBy) { }`. `public static ExamPeriod Create(string name, DateOnly startDate, DateOnly endDate, Guid createdBy)`; `public void Update(string name, DateOnly startDate, DateOnly endDate, Guid updatedBy)`; `public void Delete(Guid deletedBy)`; `public SlaDateRange ToDateRange() => new(StartDate, EndDate);`. |
| D4 | `SlaCalendars/IExamPeriodRepository.cs` | `public interface IExamPeriodRepository : IRepository<ExamPeriod> { }` | Base covers every use. |
| D5 | `TeacherThreads/TeacherThreadSlaSchedule.cs` | `public sealed record TeacherThreadSlaSchedule(DateTimeOffset WindowStartedAt, DateTimeOffset FirstReminderDueAt, DateTimeOffset SecondReminderDueAt, DateTimeOffset SlaDueAt, string Fingerprint)` | `public DateTimeOffset DueAt(TeacherThreadSlaEventKind kind) => kind switch { FirstReminder => FirstReminderDueAt, SecondReminder => SecondReminderDueAt, Breach => SlaDueAt, _ => throw new ArgumentOutOfRangeException(nameof(kind)) };` |
| D6 | `TeacherThreads/TeacherThreadSlaPolicy.cs` | `public sealed class TeacherThreadSlaPolicy` | Ctor `public TeacherThreadSlaPolicy(SlaCalendar calendar, TimeSpan replySla, TimeSpan firstReminderAfter, TimeSpan secondReminderAfter)` sets props and `Fingerprint = ComputeFingerprint();`. Props `SlaCalendar Calendar`, `TimeSpan ReplySla`, `TimeSpan FirstReminderAfter`, `TimeSpan SecondReminderAfter`, `string Fingerprint`. `public TeacherThreadSlaSchedule ScheduleFrom(DateTimeOffset windowStartedAt) => new(windowStartedAt, Calendar.AddCountedTime(windowStartedAt, FirstReminderAfter), Calendar.AddCountedTime(windowStartedAt, SecondReminderAfter), Calendar.AddCountedTime(windowStartedAt, ReplySla), Fingerprint);`. `private string ComputeFingerprint()`: canonical string, `CultureInfo.InvariantCulture`: `$"{ReplySla.Ticks}\|{FirstReminderAfter.Ticks}\|{SecondReminderAfter.Ticks}\|{Calendar.SkipWeekends}\|{string.Join(',', Calendar.WeekendDays.Select(x => (int)x).Order())}\|{Calendar.TimeZone.Id}\|{string.Join(';', Calendar.ExamPeriods.OrderBy(x => x.Start).ThenBy(x => x.End).Select(x => $"{x.Start:yyyy-MM-dd}..{x.End:yyyy-MM-dd}"))}"` → `Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))`. |

### Application — `api/Elmanhg.Application`
| # | Path | Type | Contract |
|---|------|------|----------|
| A1 | `Shared/Options/SlaCalendarOptions.cs` | `public sealed class SlaCalendarOptions` ns `Elmanhg.Application.Shared.Options` | `public const string SectionName = "SlaCalendar";` `public bool SkipWeekends { get; set; } = true;` `[Required] public string WeekendDays { get; set; } = "Friday,Saturday";` `[Required] public string TimeZone { get; set; } = "Africa/Cairo";` `[Required] public string AllowedTimeZones { get; set; } = "Africa/Cairo,Asia/Riyadh,Asia/Dubai,Asia/Kuwait,UTC";` `[Range(1, 500)] public int ExamPeriodNameMaxLength { get; set; } = 100;` `[Range(1, 366)] public int ExamPeriodMaxDays { get; set; } = 120;` `public List<string> WeekendDayNames() => Split(WeekendDays);` `public List<string> AllowedTimeZoneIds() => Split(AllowedTimeZones);` `private static List<string> Split(string value) => value.Split(',', StringSplitOptions.TrimEntries \| StringSplitOptions.RemoveEmptyEntries).ToList();` |
| A2 | `Shared/Options/SlaCalendarOptionsValidator.cs` | `public sealed class SlaCalendarOptionsValidator : IValidateOptions<SlaCalendarOptions>` | `Validate(string? name, SlaCalendarOptions options)` collects failures, returns `Fail(failures)` or `Success`: (1) every weekend name ∈ `Enum.GetNames<DayOfWeek>()` (ordinal) → `"SlaCalendar:WeekendDays must list day names (Sunday … Saturday)."`; (2) no duplicates → `"SlaCalendar:WeekendDays must not repeat a day."`; (3) count ≤ 6 → `"SlaCalendar:WeekendDays must leave at least one day that counts."`; (4) every allowed id resolves via `TimeZoneInfo.TryFindSystemTimeZoneById` → `$"SlaCalendar:AllowedTimeZones has an unknown time zone id {id}."`; (5) `AllowedTimeZoneIds()` contains `TimeZone` → `"SlaCalendar:TimeZone must be one of SlaCalendar:AllowedTimeZones."`. |
| A3 | `Shared/RuntimeSettings/Definitions/SlaCalendarRuntimeSettings.cs` | `public sealed class SlaCalendarRuntimeSettings(IOptions<SlaCalendarOptions> slaCalendarOptions) : IRuntimeSettingDefinitions` | Keys: `public static readonly RuntimeSettingKey<bool> SkipWeekends = new("slaCalendar.skipWeekends");` `RuntimeSettingKey<List<string>> WeekendDays = new("slaCalendar.weekendDays");` `RuntimeSettingKey<string> TimeZone = new("slaCalendar.timeZone");`. Definitions (group `SlaCalendar`): `ForBoolean(SkipWeekends, …, options.SkipWeekends, label ar "استبعاد أيام العطلة" / en "Skip weekends", desc ar "عند التفعيل لا تحتسب أيام العطلة خارج فترات الامتحانات من مهلة الرد والتذكيرات." / en "When on, weekend days outside exam periods do not count toward the reply time and the reminders.")`; `ForChoiceList(WeekendDays, …, options.WeekendDayNames(), Enum.GetNames<DayOfWeek>(), ar "أيام العطلة" / en "Weekend days", ar "الأيام التي لا تحتسب عند استبعاد العطلة. يجب أن يحتسب يوم واحد على الأقل." / en "Days that do not count when weekends are skipped. At least one day of the week must count.")`; `ForChoice(TimeZone, …, options.TimeZone, options.AllowedTimeZoneIds(), ar "المنطقة الزمنية لحدود الأيام" / en "Time zone for day boundaries", ar "تحدد متى يبدأ اليوم وينتهي عند احتساب العطلات وفترات الامتحانات." / en "Decides when a day starts and ends for weekends and exam periods.")`. Constraints: `new(ErrorCodes.SlaCalendarWeekendDaysInvalid, values => !values.Get(SkipWeekends) \|\| values.Get(WeekendDays).Count < 7)`. |
| A4 | `TeacherThreads/Shared/TeacherThreadSlaPolicyLoader.cs` | `public static class TeacherThreadSlaPolicyLoader` | `public static async Task<TeacherThreadSlaPolicy> LoadAsync(IRuntimeSettings runtimeSettings, IExamPeriodRepository examPeriodRepository, CancellationToken cancellationToken)`: 1 `values = await runtimeSettings.GetValuesAsync(…)`; 2 `periods = await examPeriodRepository.GetAllAsync(cancellationToken, asNoTracking: true).ConfigureAwait(false) ?? []`; 3 `calendar = new SlaCalendar(values.Get(SlaCalendarRuntimeSettings.SkipWeekends), values.Get(SlaCalendarRuntimeSettings.WeekendDays).Select(Enum.Parse<DayOfWeek>).ToList(), TimeZoneInfo.FindSystemTimeZoneById(values.Get(SlaCalendarRuntimeSettings.TimeZone)), periods.Select(x => x.ToDateRange()).ToList())`; 4 return `new TeacherThreadSlaPolicy(calendar, TimeSpan.FromHours(values.Get(AskTeacherRuntimeSettings.ReplySlaHours)), TimeSpan.FromHours(values.Get(AskTeacherRuntimeSettings.FirstReminderAfterHours)), TimeSpan.FromHours(values.Get(AskTeacherRuntimeSettings.SecondReminderAfterHours)))`. |
| A5 | `TeacherThreads/RescheduleTeacherThreadSlas/RescheduleTeacherThreadSlasCommand.cs` | `public sealed record RescheduleTeacherThreadSlasCommand(int BatchSize) : IRequest<int>;` | Not auditable (system job). Returns the number rescheduled. |
| A6 | `TeacherThreads/RescheduleTeacherThreadSlas/RescheduleTeacherThreadSlasHandler.cs` | `public sealed class RescheduleTeacherThreadSlasHandler(ITeacherThreadRepository teacherThreadRepository, IExamPeriodRepository examPeriodRepository, IRuntimeSettings runtimeSettings, TimeProvider timeProvider) : IRequestHandler<RescheduleTeacherThreadSlasCommand, int>` | 1 `policy = await TeacherThreadSlaPolicyLoader.LoadAsync(…)`; 2 `var fingerprint = policy.Fingerprint;` 3 `page = await teacherThreadRepository.FindPaginatedAsync(1, request.BatchSize, cancellationToken, filter: x => x.Status == TeacherThreadStatus.Open && x.SlaScheduleFingerprint != fingerprint, orderBy: query => query.OrderBy(x => x.SlaWindowStartedAt).ThenBy(x => x.Id)).ConfigureAwait(false)` (tracked); 4 `now = timeProvider.GetUtcNow()`; 5 `var rescheduled = 0; foreach (var thread in page.Items) { if (thread.RescheduleSla(policy, now)) { rescheduled++; } }`; 6 if `rescheduled > 0` → `SaveChangesAsync` once; 7 return `rescheduled`. |
| A7 | `TeacherThreads/GetTeacherReplyDeadline/GetTeacherReplyDeadlineQuery.cs` | `public sealed record GetTeacherReplyDeadlineQuery : IRequest<TeacherReplyDeadlineResult>;` | — |
| A8 | `TeacherThreads/GetTeacherReplyDeadline/GetTeacherReplyDeadlineHandler.cs` | `public sealed class GetTeacherReplyDeadlineHandler(IExamPeriodRepository examPeriodRepository, IRuntimeSettings runtimeSettings, TimeProvider timeProvider) : IRequestHandler<GetTeacherReplyDeadlineQuery, TeacherReplyDeadlineResult>` | 1 load policy; 2 `now = timeProvider.GetUtcNow()`; 3 `schedule = policy.ScheduleFrom(now)`; 4 return `new TeacherReplyDeadlineResult((int)policy.ReplySla.TotalHours, schedule.SlaDueAt, schedule.SlaDueAt > now + policy.ReplySla)`. |
| A9 | `TeacherThreads/Shared/TeacherReplyDeadlineResult.cs` | `public sealed record TeacherReplyDeadlineResult(int ReplySlaHours, DateTimeOffset SlaDueAt, bool SkipsUncountedDays);` | Client-facing; no localized text. |
| A10 | `SlaCalendars/Shared/ExamPeriodResult.cs` | `public sealed record ExamPeriodResult(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt) : IAuditableResult { Guid? IAuditableResult.AuditResourceId => Id; }` | Admin-facing. |
| A11 | `SlaCalendars/Shared/ExamPeriodResultGenerator.cs` | `public static class ExamPeriodResultGenerator` | `public static ExamPeriodResult Generate(ExamPeriod examPeriod) => new(examPeriod.Id, examPeriod.Name, examPeriod.StartDate, examPeriod.EndDate, examPeriod.CreationDate, examPeriod.UpdationDate);` |
| A12 | `SlaCalendars/GetExamPeriods/GetExamPeriodsQuery.cs` | `public sealed record GetExamPeriodsQuery : IRequest<List<ExamPeriodResult>>;` | — |
| A13 | `SlaCalendars/GetExamPeriods/GetExamPeriodsHandler.cs` | `(IExamPeriodRepository examPeriodRepository)` | `GetAllAsync(cancellationToken, orderBy: query => query.OrderByDescending(x => x.StartDate).ThenBy(x => x.Id), asNoTracking: true)`; map with A11; `?? []`. |
| A14 | `SlaCalendars/CreateExamPeriod/CreateExamPeriodCommand.cs` | `public sealed record CreateExamPeriodCommand(string? Name, DateOnly? StartDate, DateOnly? EndDate) : IRequest<ExamPeriodResult>, IAuditableCommand` | `AuditAction => "ExamPeriod.Create"`, `AuditResourceType => "ExamPeriod"`, `AuditResourceId => null`. |
| A15 | `SlaCalendars/CreateExamPeriod/CreateExamPeriodValidator.cs` | `(IOptions<SlaCalendarOptions> slaCalendarOptions)` | `Name`: `.ValidateRequired(ErrorCodes.ExamPeriodNameRequired).ValidateMaxLength(options.ExamPeriodNameMaxLength, ErrorCodes.ExamPeriodNameTooLong)`; `StartDate`: `.ValidateRequired(ErrorCodes.ExamPeriodStartDateRequired)`; `EndDate`: `.ValidateRequired(ErrorCodes.ExamPeriodEndDateRequired)`; `RuleFor(x => x).Must(x => x.StartDate is null \|\| x.EndDate is null \|\| x.EndDate >= x.StartDate).WithErrorCode(ErrorCodes.ExamPeriodDateRangeInvalid)`; `RuleFor(x => x).Must(x => x.StartDate is null \|\| x.EndDate is null \|\| x.EndDate < x.StartDate \|\| x.EndDate.Value.DayNumber - x.StartDate.Value.DayNumber + 1 <= options.ExamPeriodMaxDays).WithErrorCode(ErrorCodes.ExamPeriodTooLong)`. |
| A16 | `SlaCalendars/CreateExamPeriod/CreateExamPeriodHandler.cs` | `(IExamPeriodRepository examPeriodRepository, ICurrentUserService currentUserService)` | 1 user guard → `UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated)`; 2 `ExamPeriod.Create(request.Name!.Trim(), request.StartDate!.Value, request.EndDate!.Value, userId)`; 3 `AddAsync`; 4 `SaveChangesAsync`; 5 return A11. |
| A17 | `SlaCalendars/UpdateExamPeriod/UpdateExamPeriodCommand.cs` | `public sealed record UpdateExamPeriodCommand(Guid ExamPeriodId, string? Name, DateOnly? StartDate, DateOnly? EndDate) : IRequest<ExamPeriodResult>, IAuditableCommand` | `"ExamPeriod.Update"`, `"ExamPeriod"`, `AuditResourceId => ExamPeriodId`. |
| A18 | `SlaCalendars/UpdateExamPeriod/UpdateExamPeriodValidator.cs` | `(IOptions<SlaCalendarOptions>)` | `ExamPeriodId`: `.ValidateRequired(ErrorCodes.ExamPeriodIdRequired)` + the five A15 rules on the same fields. |
| A19 | `SlaCalendars/UpdateExamPeriod/UpdateExamPeriodHandler.cs` | `(IExamPeriodRepository examPeriodRepository, ICurrentUserService currentUserService)` | 1 user guard; 2 `GetByIdAsync(request.ExamPeriodId, …)` (tracked) ?? `NotFoundCoreException(ErrorCodes.ExamPeriodNotFound)`; 3 `examPeriod.Update(request.Name!.Trim(), …, userId)`; 4 `SaveChangesAsync`; 5 return A11. |
| A20 | `SlaCalendars/DeleteExamPeriod/DeleteExamPeriodCommand.cs` | `public sealed record DeleteExamPeriodCommand(Guid ExamPeriodId) : IRequest, IAuditableCommand` | `"ExamPeriod.Delete"`, `"ExamPeriod"`, `AuditResourceId => ExamPeriodId`. |
| A21 | `SlaCalendars/DeleteExamPeriod/DeleteExamPeriodValidator.cs` | — | `ExamPeriodId`: `.ValidateRequired(ErrorCodes.ExamPeriodIdRequired)`. |
| A22 | `SlaCalendars/DeleteExamPeriod/DeleteExamPeriodHandler.cs` | `(IExamPeriodRepository examPeriodRepository, ICurrentUserService currentUserService)` | 1 user guard; 2 `GetByIdAsync` ?? `NotFoundCoreException(ErrorCodes.ExamPeriodNotFound)`; 3 `examPeriod.Delete(userId)`; 4 `SaveChangesAsync`. |

### Infrastructure — `api/Elmanhg.Infrastructure`
| # | Path | Contract |
|---|------|----------|
| I1 | `Data/Context/AppDbContext.SlaCalendars.cs` | `public partial class AppDbContext { public DbSet<ExamPeriod> ExamPeriods { get; set; } private static void ConfigureSlaCalendars(ModelBuilder modelBuilder) { modelBuilder.Entity<ExamPeriod>(builder => { builder.Property(x => x.Name).IsRequired(); builder.Property(x => x.Version).IsRowVersion(); builder.HasIndex(x => new { x.StartDate, x.EndDate }); builder.ToTable(x => x.HasCheckConstraint("CK_ExamPeriods_DateRange", "\"EndDate\" >= \"StartDate\"")); }); } }` |
| I2 | `SlaCalendars/ExamPeriodRepository.cs` | `public class ExamPeriodRepository(AppDbContext context) : Repository<ExamPeriod>(context), IExamPeriodRepository { }` |
| I3 | `Migrations/<timestamp>_AddSlaCalendar.cs` + `.Designer.cs` | `dotnet ef migrations add AddSlaCalendar -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`. Expected ops: create `ExamPeriods`; add `TeacherThreads.SlaWindowStartedAt`, `FirstReminderDueAt`, `SecondReminderDueAt` (timestamptz NOT NULL, scaffolded default), `SlaScheduleFingerprint` (varchar(64) NOT NULL default `''`); add `TeacherThreadSlaEvents.WindowStartedAt` (timestamptz NOT NULL); then **hand-add** `migrationBuilder.Sql(...)` with the two backfills below **before** the index swap; drop unique `IX_TeacherThreadSlaEvents_ThreadId_Kind_SlaDueAt`, create unique `IX_TeacherThreadSlaEvents_ThreadId_Kind_WindowStartedAt`. No other `Drop*`/`Rename*`. Down reverses. |

Backfill SQL (verbatim):
```sql
UPDATE "TeacherThreads" AS t SET "SlaWindowStartedAt" = COALESCE((SELECT MAX(m."CreatedAt") FROM "TeacherMessages" AS m WHERE m."ThreadId" = t."Id" AND m."SenderId" = t."StudentId"), t."SubmittedAt"), "FirstReminderDueAt" = t."SlaDueAt", "SecondReminderDueAt" = t."SlaDueAt", "SlaScheduleFingerprint" = '';
UPDATE "TeacherThreadSlaEvents" AS e SET "WindowStartedAt" = COALESCE((SELECT MAX(m."CreatedAt") FROM "TeacherMessages" AS m INNER JOIN "TeacherThreads" AS t ON t."Id" = m."ThreadId" WHERE m."ThreadId" = e."ThreadId" AND m."SenderId" = t."StudentId" AND m."CreatedAt" <= e."OccurredAt"), e."SlaDueAt");
```

### API — `api/Elmanhg.Api` (no new files; see Existing code touched).

### Tests — `api/Elmanhg.Tests` (new files)
| # | Path |
|---|------|
| T1 | `Builders/TeacherThreadSlaPolicies.cs` — `public static class TeacherThreadSlaPolicies { public static TimeZoneInfo Cairo => TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo"); public static TeacherThreadSlaPolicy WallClock(int replySlaHours = 24, int firstReminderAfterHours = 12, int secondReminderAfterHours = 20) => new(new SlaCalendar(false, [], TimeZoneInfo.Utc, []), …); public static TeacherThreadSlaPolicy CairoWeekends(params SlaDateRange[] examPeriods) => new(new SlaCalendar(true, [DayOfWeek.Friday, DayOfWeek.Saturday], Cairo, examPeriods), TimeSpan.FromHours(24), TimeSpan.FromHours(12), TimeSpan.FromHours(20)); }` |
| T2 | `Domain/SlaCalendars/SlaCalendarTests.cs` |
| T3 | `Domain/SlaCalendars/ExamPeriodTests.cs` |
| T4 | `Domain/TeacherThreads/TeacherThreadSlaPolicyTests.cs` |
| T5 | `Domain/TeacherThreads/TeacherThreadSlaTests.cs` |
| T6 | `Application/Shared/Options/SlaCalendarOptionsValidatorTests.cs` |
| T7 | `Application/Shared/RuntimeSettings/SlaCalendarRuntimeSettingsTests.cs` |
| T8 | `Application/Features/TeacherThreads/Shared/TeacherThreadSlaPolicyLoaderTests.cs` |
| T9 | `Application/Features/TeacherThreads/RescheduleTeacherThreadSlas/RescheduleTeacherThreadSlasHandlerTests.cs` |
| T10 | `Application/Features/TeacherThreads/GetTeacherReplyDeadline/GetTeacherReplyDeadlineHandlerTests.cs` |
| T11 | `Application/Features/SlaCalendars/GetExamPeriods/GetExamPeriodsHandlerTests.cs` |
| T12 | `Application/Features/SlaCalendars/CreateExamPeriod/CreateExamPeriodHandlerTests.cs` |
| T13 | `Application/Features/SlaCalendars/CreateExamPeriod/CreateExamPeriodValidatorTests.cs` |
| T14 | `Application/Features/SlaCalendars/UpdateExamPeriod/UpdateExamPeriodHandlerTests.cs` |
| T15 | `Application/Features/SlaCalendars/UpdateExamPeriod/UpdateExamPeriodValidatorTests.cs` |
| T16 | `Application/Features/SlaCalendars/DeleteExamPeriod/DeleteExamPeriodHandlerTests.cs` |
| T17 | `Application/Features/SlaCalendars/DeleteExamPeriod/DeleteExamPeriodValidatorTests.cs` |
| T18 | `Integration/SlaCalendars/SlaCalendarTestData.cs` — `ExamPeriodsRoute = "/api/configuration/exam-periods"`, `ExamPeriodRoute(Guid id)`, `ClearExamPeriodsAsync(ApiFactory)` (`ExamPeriods.IgnoreQueryFilters().ExecuteDeleteAsync` — test cleanup only), `SetOverrideAsync(ApiFactory, string key, string json)` (adds/updates the override row, removes `RuntimeSettingsCache.Key`). |
| T19 | `Integration/SlaCalendars/ExamPeriodsEndpointTests.cs` — `[Collection(RuntimeSettingsCollection.Name)]`, `IAsyncLifetime` clearing overrides + exam periods before and after. |
| T20 | `Integration/TeacherThreads/TeacherThreadSlaCalendarTests.cs` — same collection and cleanup. |
| T21 | `Integration/TeacherThreads/TeacherReplyDeadlineEndpointTests.cs` |
| T22 | `Integration/Persistence/ExamPeriodPersistenceTests.cs` |

### Web — `web/src` (new files)
| # | Path | Contract |
|---|------|----------|
| W1 | `features/configuration/schemas/examPeriodSchema.ts` | `export const examPeriodSchema = z.object({ name: z.string().trim().min(1, { error: 'configuration:examPeriods.validation.nameRequired' }), startDate: z.iso.date({ error: 'configuration:examPeriods.validation.startRequired' }), endDate: z.iso.date({ error: 'configuration:examPeriods.validation.endRequired' }) }).refine((x) => x.endDate >= x.startDate, { path: ['endDate'], error: 'configuration:examPeriods.validation.rangeInvalid' });` (namespaced keys, as `refundPaymentSchema`)` `export type ExamPeriodValues = z.infer<typeof examPeriodSchema>;` |
| W2 | `features/configuration/hooks/useExamPeriods.ts` | `export function useExamPeriods() { return useGetExamPeriods(); }` (generated hook). |
| W3 | `features/configuration/hooks/useExamPeriodMutations.ts` | `export interface ExamPeriodMutations { create(values: ExamPeriodValues): Promise<ExamPeriodResult>; update(id: string, values: ExamPeriodValues): Promise<ExamPeriodResult>; remove(id: string): Promise<void>; isPending: boolean; }` — generated `useCreateExamPeriod`/`useUpdateExamPeriod`/`useDeleteExamPeriod`; `onSuccess` invalidates `getGetExamPeriodsQueryKey()` and toasts `examPeriods.toast.created\|updated\|deleted`; `onError` toasts `examPeriods.toast.failed`. |
| W4 | `features/configuration/components/ExamPeriodsSection.tsx` | Card (same classes as `RuntimeSettingGroupCard`): `<h2>` `examPeriods.title`, caption `examPeriods.description`, `Button` `examPeriods.add` opening `ExamPeriodDialog` (create). Body: pending → `<ConfigurationSkeleton label={t('examPeriods.loading')} />`; error → `role="alert"` block like `ConfigurationPage` (title `examPeriods.errorTitle`, code message, Retry); empty → `<p>` `examPeriods.empty`; else `<ul className="flex flex-col divide-y divide-border">` of `ExamPeriodRow`. Holds `editing: ExamPeriodResult \| null`, `creating: boolean`, `deleting: ExamPeriodResult \| null` UI state. |
| W5 | `features/configuration/components/ExamPeriodRow.tsx` | `<li>`: name (`text-ui font-semibold`), range `t('examPeriods.range', { start, end })` with `formatDate(new Date(`${date}T00:00:00`), lng, 'latin', { dateStyle: 'medium' })`, buttons `secondary sm` «edit» (`aria-label={t('examPeriods.editLabel', { name })}`) and `danger sm` «delete» (`aria-label={t('examPeriods.deleteLabel', { name })}`). Props `{ examPeriod, onEdit, onDelete }`. |
| W6 | `features/configuration/components/ExamPeriodDialog.tsx` | Props `{ open: boolean; examPeriod: ExamPeriodResult \| null; onOpenChange(open: boolean): void; mutations: ExamPeriodMutations }`. `Dialog` + `DialogContent title={t(examPeriod ? 'examPeriods.form.editTitle' : 'examPeriods.form.createTitle')}`; `Form` with `zodResolver(examPeriodSchema)`, defaults from `examPeriod` or empty strings; `TextField` name, `TextField type="date"` startDate/endDate; `FormRootError`; Cancel (`secondary`) + Save (`type="submit"`, disabled while pending). `serverErrorFields = { EXAM_PERIOD_NAME_REQUIRED: 'name', EXAM_PERIOD_NAME_TOO_LONG: 'name', EXAM_PERIOD_START_DATE_REQUIRED: 'startDate', EXAM_PERIOD_END_DATE_REQUIRED: 'endDate', EXAM_PERIOD_DATE_RANGE_INVALID: 'endDate', EXAM_PERIOD_TOO_LONG: 'endDate' }`. Closes on success. |
| W7 | `features/configuration/components/DeleteExamPeriodDialog.tsx` | Props `{ examPeriod: ExamPeriodResult \| null; onOpenChange; mutations }`. Title `examPeriods.deleteDialog.title`, body `examPeriods.deleteDialog.body` `{ name }`, Cancel / Delete (`danger`, disabled while pending). |
| W8 | `features/configuration/schemas/examPeriodSchema.test.ts` | Test plan. |
| W9 | `features/configuration/components/ExamPeriodsSection.test.tsx` | Test plan (renders `ConfigurationPage` with an `SlaCalendar` group via `renderApp` at `/admin/configuration` as admin). |

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `SlaCalendarWeekendDaysInvalid` | `SLA_CALENDAR_WEEKEND_DAYS_INVALID` | `SlaCalendarRuntimeSettings` constraint via `RuntimeSettingRegistry.EnsureConstraintsHold` (update and reset) | `BusinessRuleViolationCoreException` | 400 |
| `ExamPeriodIdRequired` | `EXAM_PERIOD_ID_REQUIRED` | Update/Delete validators | validation | 422 |
| `ExamPeriodNameRequired` | `EXAM_PERIOD_NAME_REQUIRED` | Create/Update validators | validation | 422 |
| `ExamPeriodNameTooLong` | `EXAM_PERIOD_NAME_TOO_LONG` | Create/Update validators | validation | 422 |
| `ExamPeriodStartDateRequired` | `EXAM_PERIOD_START_DATE_REQUIRED` | Create/Update validators | validation | 422 |
| `ExamPeriodEndDateRequired` | `EXAM_PERIOD_END_DATE_REQUIRED` | Create/Update validators | validation | 422 |
| `ExamPeriodDateRangeInvalid` | `EXAM_PERIOD_DATE_RANGE_INVALID` | Create/Update validators | validation | 422 |
| `ExamPeriodTooLong` | `EXAM_PERIOD_TOO_LONG` | Create/Update validators | validation | 422 |
| `ExamPeriodNotFound` | `EXAM_PERIOD_NOT_FOUND` | Update/Delete handlers | `NotFoundCoreException` | 404 |
| `ExamPeriodModifiedConcurrently` | `EXAM_PERIOD_MODIFIED_CONCURRENTLY` | `AppDbContext.SaveChangesAsync` | `ConflictCoreException` | 409 |

Resource strings (Arabic without tashkeel; same text in `web/src/features/configuration/i18n/configurationErrors.{ar,en}.json`):
| Key | ar | en |
|-----|----|----|
| SLA_CALENDAR_WEEKEND_DAYS_INVALID | يجب أن يبقى يوم واحد على الأقل من الأسبوع محتسبا. | At least one day of the week must count. |
| EXAM_PERIOD_ID_REQUIRED | معرف فترة الامتحانات مطلوب. | The exam period id is required. |
| EXAM_PERIOD_NAME_REQUIRED | اسم فترة الامتحانات مطلوب. | Enter a name for the exam period. |
| EXAM_PERIOD_NAME_TOO_LONG | اسم فترة الامتحانات أطول من المسموح. | The exam period name is too long. |
| EXAM_PERIOD_START_DATE_REQUIRED | اختر أول يوم في الفترة. | Choose the first day of the period. |
| EXAM_PERIOD_END_DATE_REQUIRED | اختر آخر يوم في الفترة. | Choose the last day of the period. |
| EXAM_PERIOD_DATE_RANGE_INVALID | يجب أن يكون آخر يوم في نفس يوم البداية أو بعده. | The last day must be on or after the first day. |
| EXAM_PERIOD_TOO_LONG | فترة الامتحانات أطول من المسموح. | The exam period is longer than allowed. |
| EXAM_PERIOD_NOT_FOUND | فترة الامتحانات غير موجودة. | The exam period was not found. |
| EXAM_PERIOD_MODIFIED_CONCURRENTLY | عدل شخص آخر هذه الفترة في نفس الوقت. حدث الصفحة وحاول مرة أخرى. | Someone else changed this exam period at the same time. Refresh and try again. |

## Domain behaviour

`SlaCalendar`:
```csharp
public bool Counts(DateOnly day) => !SkipWeekends || !WeekendDays.Contains(day.DayOfWeek) || ExamPeriods.Any(x => x.Contains(day));

public DateTimeOffset AddCountedTime(DateTimeOffset start, TimeSpan duration)
{
    ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);
    var cursor = start.ToUniversalTime();
    var remaining = duration;
    var day = LocalDate(cursor);
    for (var scanned = 0; scanned < MaxDaysScanned; scanned++)
    {
        var dayEnd = StartOfDay(day.AddDays(1));
        if (Counts(day))
        {
            var available = dayEnd - cursor;
            if (remaining <= available)
            {
                return cursor + remaining;
            }

            remaining -= available;
        }

        cursor = dayEnd;
        day = day.AddDays(1);
    }

    throw new InvalidOperationException("The SLA calendar has no day that counts.");
}
```
Results are UTC (`TimeSpan.Zero` offset) — Npgsql only writes UTC to `timestamptz`.

`ExamPeriod`:
- `Create`: `ArgumentOutOfRangeException.ThrowIfLessThan(endDate, startDate);` then `new ExamPeriod(Guid.NewGuid(), createdBy) { Name = name, StartDate = startDate, EndDate = endDate }`.
- `Update`: same guard; set the three fields; `UpdatedBy = updatedBy; UpdationDate = DateTimeOffset.UtcNow;`.
- `Delete`: `SoftDelete(); UpdatedBy = deletedBy; UpdationDate = DateTimeOffset.UtcNow;`.

`TeacherThread.Sla.cs` (whole file):
```csharp
public TeacherThreadSlaSchedule SlaSchedule() => new(SlaWindowStartedAt, FirstReminderDueAt, SecondReminderDueAt, SlaDueAt, SlaScheduleFingerprint);

public List<TeacherThreadSlaEventKind> DueSlaStages(DateTimeOffset now)
{
    if (Status != TeacherThreadStatus.Open) { return []; }
    var schedule = SlaSchedule();
    return Enum.GetValues<TeacherThreadSlaEventKind>()
        .Where(x => now >= schedule.DueAt(x))
        .ToList();
}

// Recomputes the stored stage times of the open window when the calendar, reply time or reminder hours changed; the window start never moves.
public bool RescheduleSla(TeacherThreadSlaPolicy slaPolicy, DateTimeOffset rescheduledAt)
{
    if (Status != TeacherThreadStatus.Open || SlaScheduleFingerprint == slaPolicy.Fingerprint) { return false; }
    ApplySlaSchedule(slaPolicy.ScheduleFrom(SlaWindowStartedAt));
    UpdationDate = ToMicroseconds(rescheduledAt);
    return true;
}

private void ApplySlaSchedule(TeacherThreadSlaSchedule schedule)
{
    SlaWindowStartedAt = schedule.WindowStartedAt;
    FirstReminderDueAt = schedule.FirstReminderDueAt;
    SecondReminderDueAt = schedule.SecondReminderDueAt;
    SlaDueAt = schedule.SlaDueAt;
    SlaScheduleFingerprint = schedule.Fingerprint;
}
```
(Braces on every `if` in the real file, one statement per line.) `Submit` and `FollowUp` stamp `UpdationDate` as today; `RescheduleSla` does not touch `UpdatedBy` (system change). `IsOverdueAt` unchanged.

## Worker behaviour (`TeacherThreadSlaWorker`)
- `SweepAsync(int batchSize, CancellationToken stoppingToken)`: after `using var run = jobMetrics.StartRun(JobName);` call `await RescheduleAsync(batchSize, stoppingToken).ConfigureAwait(false);` then the existing listing/processing unchanged.
- New `private async Task RescheduleAsync(int batchSize, CancellationToken stoppingToken)`: `try { int rescheduled; do { await using var scope = scopeFactory.CreateAsyncScope(); rescheduled = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RescheduleTeacherThreadSlasCommand(batchSize), stoppingToken).ConfigureAwait(false); } while (rescheduled == batchSize); } catch (Exception exception) when (!stoppingToken.IsCancellationRequested) { logger.LogError(exception, "Rescheduling Ask a Teacher SLA deadlines failed."); }` — a scope per batch; a failure does not stop the due listing.

## API surface
| Method | Route | Policy | Request | Response | Name |
|--------|-------|--------|---------|----------|------|
| GET | `/api/configuration/exam-periods` | `DefaultCodes.ConfigurationManage` | — | `List<ExamPeriodResult>` | `GetExamPeriods` |
| POST | `/api/configuration/exam-periods` | `ConfigurationManage` | `[FromBody] CreateExamPeriodCommand` | `ExamPeriodResult` (200) | `CreateExamPeriod` |
| PUT | `/api/configuration/exam-periods/{examPeriodId:guid}` | `ConfigurationManage` | `[FromRoute] Guid examPeriodId`, `[FromBody] UpdateExamPeriodRequest` → `UpdateExamPeriodCommand` | `ExamPeriodResult` | `UpdateExamPeriod` |
| DELETE | `/api/configuration/exam-periods/{examPeriodId:guid}` | `ConfigurationManage` | route id → `DeleteExamPeriodCommand` | `Ok()` | `DeleteExamPeriod` |
| GET | `/api/teacher-threads/reply-deadline` | `DefaultCodes.AskTeacherSubmit` | — | `TeacherReplyDeadlineResult` | `GetTeacherReplyDeadline` |
Every action: `[ProducesResponseType<T>(StatusCodes.Status200OK)]` as in the neighbours; `CancellationToken` passed to `Send`. Existing settings endpoints serve the three new keys unchanged.

## Web copy
`features/configuration/i18n/en.json` (ar in brackets):
- `groups.SlaCalendar`: `{ "title": "Reply calendar" [«تقويم مهلة الرد»], "description": "How the Ask a Teacher reply time counts days. During an exam period every day counts." [«كيف تُحتسب أيام مهلة رد المعلّم. خلال فترات الامتحانات تُحتسب كل الأيام.»] }`
- `choices`: `Sunday` Sunday [الأحد], `Monday` [الاثنين], `Tuesday` [الثلاثاء], `Wednesday` [الأربعاء], `Thursday` [الخميس], `Friday` [الجمعة], `Saturday` [السبت], `Africa/Cairo` Cairo [القاهرة], `Asia/Riyadh` Riyadh [الرياض], `Asia/Dubai` Dubai [دبي], `Asia/Kuwait` Kuwait [الكويت], `UTC` UTC [التوقيت العالمي UTC].
- `examPeriods`: `title` Exam periods [فترات الامتحانات]; `description` "Every day counts toward the reply time between these dates, weekends included. Dates follow the calendar's time zone; open questions are updated at the next check." [«تُحتسب كل الأيام من مهلة الرد بين هذه التواريخ، بما فيها العطلات. التواريخ بالمنطقة الزمنية للتقويم، وتُحدَّث الأسئلة المفتوحة عند المراجعة التالية.»]; `add` Add exam period [إضافة فترة امتحانات]; `loading` Loading exam periods… [جارٍ تحميل فترات الامتحانات…]; `errorTitle` Could not load the exam periods. [تعذّر تحميل فترات الامتحانات.]; `empty` No exam periods yet. [لا توجد فترات امتحانات بعد.]; `range` "{start} – {end}"; `edit` Edit [تعديل]; `editLabel` Edit {name} [تعديل {name}]; `delete` Delete [حذف]; `deleteLabel` Delete {name} [حذف {name}]; `form.createTitle` Add exam period [إضافة فترة امتحانات]; `form.editTitle` Edit exam period [تعديل فترة الامتحانات]; `form.name` Name [الاسم]; `form.startDate` First day [أول يوم]; `form.endDate` Last day [آخر يوم]; `form.save` Save [حفظ]; `form.cancel` Cancel [إلغاء]; `deleteDialog.title` Delete this exam period? [حذف فترة الامتحانات؟]; `deleteDialog.body` "Weekends will count normally again during {name}. Open questions are updated at the next check." [«ستعود أيام العطلة خلال {name} لا تُحتسب كالمعتاد. تُحدَّث الأسئلة المفتوحة عند المراجعة التالية.»]; `deleteDialog.confirm` Delete [حذف]; `deleteDialog.cancel` Cancel [إلغاء]; `toast.created` Exam period added. [تمت إضافة فترة الامتحانات.]; `toast.updated` Exam period saved. [تم حفظ فترة الامتحانات.]; `toast.deleted` Exam period deleted. [تم حذف فترة الامتحانات.]; `toast.failed` Could not save the exam period. [تعذّر حفظ فترة الامتحانات.]; `validation.nameRequired` Enter a name. [أدخل الاسم.]; `validation.startRequired` Choose the first day. [اختر أول يوم.]; `validation.endRequired` Choose the last day. [اختر آخر يوم.]; `validation.rangeInvalid` The last day must be on or after the first day. [يجب أن يكون آخر يوم في يوم البداية أو بعده.]
- Zod messages are the namespaced keys `configuration:examPeriods.validation.*` (W1), resolved by `TextField`.

`features/askTeacher/i18n`: `form.slaNote` en "Your question goes to the subject's teachers, who reply within {hours} hours: by {date} if you send it now." / ar «سيُرسل السؤال لمعلّمي المادة، ومهلة الرد {hours} ساعة: حتى {date} إذا أرسلته الآن.»; `form.slaCalendarNote` en "Weekends outside exam periods do not count." / ar «لا تُحتسب أيام العطلة خارج فترات الامتحانات.»

## Docs
| File | Change |
|------|--------|
| `docs/PRD.md` §10.6 | v1 settings bullet adds "the Ask a Teacher reply calendar (skip weekends, weekend days, time zone) and its exam periods (§12.3)". |
| `docs/PRD.md` §12.1 step 3 | "SLA clock starts at submission and counts hours over the reply calendar (§12.3). Reminders to the teacher at 12h and 20h by default; the reply time and both reminder hours are runtime settings (§10.6); admin alert on breach. A follow-up starts a new window of the same length." (drop "the clock never pauses"). |
| `docs/PRD.md` new §12.3 "Reply calendar" | Bullets: counted hours (Decision 5) with defaults (skip on, Friday + Saturday, Africa/Cairo); exam periods (name, inclusive first/last day, admin CRUD on the Configuration page, audited) — every day counts inside one; deadlines and reminder times are stored and recomputed for open questions at the next SLA sweep when the calendar, the reply time or the reminder hours change; reminders already sent are not resent and a recorded breach stays; students see the deadline before sending and on the thread, teachers in the inbox countdown; SLA compliance counts replies whose window had no breach. "Decided (#222): (a) the calendar is configurable as above; (b) a breach gives no refund or credit; (c) a breach fires an admin alert only." Landing and plan copy state the reply hours only. |
| `docs/PRD.md` §15 | `TeacherThread(…, submitted_at, sla_window_started_at, first_reminder_due_at, second_reminder_due_at, sla_due_at, sla_schedule_fingerprint, …)`; `TeacherThreadSlaEvent(id, thread_id, kind[…], window_started_at, sla_due_at, teacher_id?, occurred_at) -- one per SLA window (window_started_at) and stage`; new `ExamPeriod(id, name, start_date, end_date, created_by, created_at, updated_by?, updated_at?)  -- SLA calendar (docs/configuration.md)`. |
| `docs/PRD.md` §16 row | "Manage configuration (runtime settings, feature flags, exam periods)". |
| `docs/PRD.md` §17 rule 11 | "Ask a Teacher: 24h SLA by default (admin-configurable, §10.6) counted over the reply calendar (§12.3) from submission (a follow-up opens a new window); …". |
| `docs/configuration.md` | §1 group list adds Reply calendar (`SlaCalendar`) after Ask a Teacher and the Exam periods section under it; API table adds the four exam-period routes; errors list adds the ten codes. §2 table adds the three keys (`slaCalendar.skipWeekends` Boolean default from `SlaCalendar:SkipWeekends`; `slaCalendar.weekendDays` ChoiceList Sunday–Saturday from `SlaCalendar:WeekendDays`; `slaCalendar.timeZone` Choice of `SlaCalendar:AllowedTimeZones` from `SlaCalendar:TimeZone`) and the rule "with skip on, at least one day counts" (`SLA_CALENDAR_WEEKEND_DAYS_INVALID`); notes the `askTeacher.replySlaHours` change applies to open threads. §5: exam periods are the implemented custom section (table `ExamPeriods`, soft delete, xmin, audit actions). §8: replace the reply-hours bullet with "Calendar, reply-time and reminder-hour changes reach open questions at the next SLA sweep (`AskTeacher:SlaSweepIntervalSeconds`, plus up to `RuntimeSettings:CacheSeconds` for a setting); a teacher action racing that update gets `409 TEACHER_THREAD_MODIFIED_CONCURRENTLY`." |
| `docs/ask-teacher.md` | Table rows for the four thread columns and `SlaDueAt` ("stored deadline counted over the reply calendar; recomputed for `Open` threads when the schedule fingerprint changes"); event table adds `WindowStartedAt` (names the window), `SlaDueAt` = deadline when fired; index line `(ThreadId, Kind, WindowStartedAt)`. SLA section: replace "No pause" with calendar counting; stages read stored times; worker first sends `RescheduleTeacherThreadSlasCommand` in batches, then lists due; remove "the worker never writes TeacherThreads" and state Decision 2. API table adds `GET /api/teacher-threads/reply-deadline`. UI line for `/student/ask-new` → "the SLA note with the deadline if sent now (and the weekend note)". Known limits: replace the reply-SLA bullet. |
| `docs/dashboard.md` | `repliedWithinSla`: "Replies whose SLA window has no Breach event (the SLA sweep records breaches over the reply calendar; a reply less than one sweep interval late counts as within)." |
| `docs/audit-log.md` | Rows `CreateExamPeriod` `ExamPeriod.Create` ExamPeriod result; `UpdateExamPeriod` `ExamPeriod.Update` command; `DeleteExamPeriod` `ExamPeriod.Delete` command; add `ExamPeriod` to audited entities. |
| `docs/claude-design-prompt.md` | `#/admin/configuration` entry: group list adds «تقويم مهلة الرد» after «اسأل معلّم» (switch «استبعاد أيام العطلة», checkboxes «أيام العطلة», select «المنطقة الزمنية لحدود الأيام»), then a white card «فترات الامتحانات» (caption, «إضافة فترة امتحانات», rows: name, date range in ASCII digits, «تعديل», «حذف»; dialog with «الاسم», «أول يوم», «آخر يوم», «حفظ»/«إلغاء»; delete confirm; states loading skeleton, error with retry, «لا توجد فترات امتحانات بعد.»). `#/student/ask-new`: the SLA note shows the deadline and «لا تُحتسب أيام العطلة خارج فترات الامتحانات.» when it applies. |
| `docs/prototype.md` | The Configuration bullet adds "including the reply calendar and exam periods". |

## Test plan

### Domain (no doubles). Times: Cairo is UTC+3 in October 2026 until Thu 29 Oct 24:00, UTC+2 after; UTC+2 until Fri 24 Apr 2026 00:00, UTC+3 after.
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 1 | `SlaCalendarTests` | `AddCountedTime_SkipOff_AddsRealHours` | `(2026-10-01T12:00Z, 24h)` → `2026-10-02T12:00Z` |
| 2 | 〃 | `AddCountedTime_ThursdayAfternoonOverWeekend_ResumesSunday` | Cairo weekends, `2026-10-01T12:00Z` + 24h → `2026-10-04T12:00Z` |
| 3 | 〃 | `AddCountedTime_StartsOnFriday_ClockStartsSundayMidnight` | `2026-10-02T09:00Z` + 12h → `2026-10-04T09:00Z` (Sun 00:00 Cairo = Sat 21:00Z, +12h) |
| 4 | 〃 | `AddCountedTime_EndsExactlyAtMidnightBeforeWeekend_ReturnsThatMidnight` | `2026-10-01T20:00Z` (Thu 23:00) + 1h → `2026-10-01T21:00Z` |
| 5 | 〃 | `AddCountedTime_CairoDayBoundaryNotUtc_UsesCalendarTimeZone` | Thu 23:30 Cairo `2026-10-01T20:30Z` + 1h → `2026-10-03T21:30Z` (Sun 00:30) |
| 6 | 〃 | `AddCountedTime_SameInstantInUtcCalendar_UsesUtcDays` | UTC zone, Fri/Sat weekend: `2026-10-01T20:30Z` + 1h → `2026-10-01T21:30Z` |
| 7 | 〃 | `AddCountedTime_ExamPeriodCoversWeekend_CountsEveryDay` | period 2026-10-02..2026-10-03: `2026-10-01T12:00Z` + 24h → `2026-10-02T12:00Z` |
| 8 | 〃 | `AddCountedTime_ExamPeriodOnFridayOnly_SkipsSaturday` | period 2026-10-02..2026-10-02: `2026-10-01T12:00Z` + 40h → Thu 9h + Fri 24h + Sun 7h → `2026-10-04T04:00Z` |
| 9 | 〃 | `AddCountedTime_ExamPeriodStartsSaturday_CountsSaturday` | period 2026-10-03..2026-10-10: `2026-10-01T12:00Z` + 24h → Thu 9h, Fri skipped, Sat from `2026-10-02T21:00Z` + 15h → `2026-10-03T12:00Z` |
| 10 | 〃 | `AddCountedTime_SpringForwardWeekend_CountsRealHours` | `2026-04-23T18:00Z` (Thu 20:00 +2) + 24h → `2026-04-26T17:00Z` |
| 11 | 〃 | `AddCountedTime_FallBackThursday_CountsTwentyFiveHourDay` | `2026-10-29T09:00Z` (Thu 12:00 +3) + 24h → `2026-11-01T09:00Z` |
| 12 | 〃 | `AddCountedTime_SkipOffAcrossDst_AddsExactlyTwentyFourHours` | skip off, Cairo: `2026-10-29T09:00Z` + 24h → `2026-10-30T09:00Z` |
| 13 | 〃 | `AddCountedTime_EmptyWeekend_AddsRealHours` | skip on, weekend `[]`: `2026-10-01T12:00Z` + 24h → `2026-10-02T12:00Z` |
| 14 | 〃 | `AddCountedTime_NegativeDuration_Throws` | `ArgumentOutOfRangeException` |
| 15 | 〃 | `AddCountedTime_NoCountedDay_ThrowsInvalidOperation` | all 7 days weekend, skip on → `InvalidOperationException` |
| 16 | 〃 | `StartOfDay_SpringForwardFriday_ReturnsTransitionInstant` | `2026-04-24` → `2026-04-23T22:00Z` |
| 17 | 〃 | `StartOfDay_AfterFallBack_ReturnsStandardMidnight` | `2026-10-30` → `2026-10-29T22:00Z` |
| 18 | 〃 | `AddCountedTime_ResultIsUtc` | offset `TimeSpan.Zero` for an input with +03:00 offset |
| 19 | `ExamPeriodTests` | `Create_ValidRange_SetsFields` | name, dates, `CreatedBy` |
| 20 | 〃 | `Create_EndBeforeStart_Throws` | `ArgumentOutOfRangeException` |
| 21 | 〃 | `Update_ValidRange_SetsFieldsAndStamps` | fields, `UpdatedBy`, `UpdationDate` not null |
| 22 | 〃 | `Update_EndBeforeStart_ThrowsAndKeepsFields` | exception; fields unchanged |
| 23 | 〃 | `Delete_SoftDeletesAndStamps` | `IsDeleted`, `UpdatedBy`, `UpdationDate` |
| 24 | 〃 | `ToDateRange_ReturnsInclusiveRange` | `Start`, `End` |
| 25 | `TeacherThreadSlaPolicyTests` | `ScheduleFrom_CairoWeekends_ComputesAllStages` | from `2026-10-01T12:00Z`: first `2026-10-04T00:00Z`, second `2026-10-04T08:00Z`, due `2026-10-04T12:00Z`, window start, fingerprint |
| 26 | 〃 | `Fingerprint_SameInputs_IsEqualAndSixtyFourHex` | equal; length 64; lowercase hex |
| 27 | 〃 | `Fingerprint_WeekendOrderAndPeriodOrder_DoNotMatter` | equal for permuted inputs |
| 28 | 〃 | `Fingerprint_ExamPeriodAdded_Changes` | differs |
| 29 | 〃 | `Fingerprint_ReplyHoursChanged_Changes` | differs |
| 30 | 〃 | `Fingerprint_TimeZoneChanged_Changes` | differs |
| 31 | `TeacherThreadSlaTests` | `Submit_CairoWeekendPolicy_StoresCalendarStageTimes` | builder `.WithSlaPolicy(CairoWeekends())`: window start, three times, fingerprint |
| 32 | 〃 | `FollowUp_StartsNewWindowFromFollowUpTime` | window start = follow-up time; due = calendar time from it |
| 33 | 〃 | `DueSlaStages_BeforeFirstReminder_ReturnsEmpty` | `[]` |
| 34 | 〃 | `DueSlaStages_AtSecondReminder_ReturnsFirstAndSecond` | `[First, Second]` |
| 35 | 〃 | `DueSlaStages_AtDeadline_ReturnsAllThree` | three |
| 36 | 〃 | `DueSlaStages_OverWeekend_UsesStoredCalendarTimes` | Cairo thread at `2026-10-02T12:00Z` (24 h wall-clock) → `[]` |
| 37 | 〃 | `DueSlaStages_AnsweredThread_ReturnsEmpty` | `[]` |
| 38 | 〃 | `RescheduleSla_NewFingerprint_RecomputesFromWindowStartAndStamps` | wall-clock thread → Cairo policy: times match Cairo schedule, window start unchanged, `UpdationDate == rescheduledAt`, returns true |
| 39 | 〃 | `RescheduleSla_SameFingerprint_ReturnsFalseAndKeepsTimes` | false; unchanged |
| 40 | 〃 | `RescheduleSla_AnsweredThread_ReturnsFalse` | false; unchanged |
| 41 | 〃 | `SlaSchedule_ReturnsStoredTimes` | record equals stored; `DueAt(kind)` per kind |
| 42 | `TeacherThreadTests` *modify* | every `Submit(…, TimeSpan.FromHours(24))` → `TeacherThreadSlaPolicies.WallClock()`; `Submit_ValidInput_OpensThreadWithSlaAndFirstStudentMessage` also asserts `SlaWindowStartedAt == SubmittedAt`, `FirstReminderDueAt == +12h`, `SecondReminderDueAt == +20h` | |
| 43 | `TeacherThreadFollowUpTests` *modify* | `ReplySla` → `WallClock()` policy; `FollowUp_AnsweredThread_AddsStudentMessageReopensAndResetsSla` also asserts `SlaWindowStartedAt` = truncated follow-up time | |
| 44 | `TeacherThreadSlaEventTests` *modify* | `Record` with window start; asserts `WindowStartedAt` | |

### Application
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 45 | `SlaCalendarOptionsValidatorTests` | `Validate_Defaults_Succeeds` | success |
| 46 | 〃 | `Validate_UnknownDayName_Fails` | `"Fryday"` → failed, message mentions `WeekendDays` |
| 47 | 〃 | `Validate_RepeatedDay_Fails` | failed |
| 48 | 〃 | `Validate_SevenDays_Fails` | failed |
| 49 | 〃 | `Validate_UnknownAllowedTimeZone_Fails` | `"Mars/Base"` → failed |
| 50 | 〃 | `Validate_TimeZoneNotAllowed_Fails` | `TimeZone = "UTC"`, allowed `"Africa/Cairo"` → failed |
| 51 | `SlaCalendarRuntimeSettingsTests` | `Definitions_DefaultOptions_ReadOptionValues` | skip true; weekend `["Friday","Saturday"]`; tz `Africa/Cairo`; allowed tz = option list; weekend allowed = 7 names in `DayOfWeek` order; group `SlaCalendar` |
| 52 | 〃 | `Constraint_SkipOnAllSevenDays_IsBroken` | `EnsureConstraintsHold` throws `BusinessRuleViolationCoreException` `SLA_CALENDAR_WEEKEND_DAYS_INVALID` |
| 53 | 〃 | `Constraint_SkipOffAllSevenDays_Holds` | no throw |
| 54 | 〃 | `Constraint_SkipOnSixDays_Holds` | no throw |
| 55 | `TeacherThreadSlaPolicyLoaderTests` | `LoadAsync_SettingsAndExamPeriods_BuildsPolicy` | `FakeRuntimeSettings(slaCalendar: new() { SkipWeekends = true })` + substitute repo returning one period: calendar flags, Cairo zone, one range, reply/reminder spans |
| 56 | 〃 | `LoadAsync_RepositoryReturnsNull_UsesNoExamPeriods` | `ExamPeriods` empty |
| 57 | 〃 | `LoadAsync_OverriddenWeekendDays_ParsesDayNames` | `.Set(WeekendDays, ["Saturday"])` → `[Saturday]` |
| 58 | `RescheduleTeacherThreadSlasHandlerTests` | `Handle_StaleOpenThreads_ReschedulesAndSavesOnce` | two wall-clock threads returned by `FindPaginatedAsync`; Cairo-skip settings: both match Cairo schedule; returns 2; `SaveChangesAsync` `Received(1)` |
| 59 | 〃 | `Handle_NoStaleThreads_ReturnsZeroWithoutSaving` | 0; `DidNotReceive()` |
| 60 | 〃 | `Handle_PassesBatchSizeAndCurrentFingerprintFilter` | `FindPaginatedAsync(1, 7, …)`; the captured filter compiled: false for a thread whose fingerprint equals the policy's, true for a stale open thread, false for a stale answered thread |
| 61 | `GetTeacherReplyDeadlineHandlerTests` | `Handle_SkipOnThursday_ReturnsSundayDeadlineAndSkipFlag` | now `2026-10-01T12:00Z`, skip on → `(24, 2026-10-04T12:00Z, true)` |
| 62 | 〃 | `Handle_SkipOff_ReturnsWallClockDeadline` | `(24, 2026-10-02T12:00Z, false)` |
| 63 | 〃 | `Handle_ExamPeriodCoversWeekend_ReturnsWallClockDeadline` | period 10-02..10-03 → `2026-10-02T12:00Z`, false |
| 64 | `GetExamPeriodsHandlerTests` | `Handle_Periods_MapsAllFields` | mapped list; asNoTracking passed true |
| 65 | 〃 | `Handle_RepositoryReturnsNull_ReturnsEmpty` | `[]` |
| 66 | `CreateExamPeriodHandlerTests` | `Handle_Valid_AddsTrimmedPeriodAndReturnsResult` | name trimmed; `AddAsync` Received(1); `SaveChangesAsync` Received(1); result fields |
| 67 | 〃 | `Handle_NoCurrentUser_ThrowsUnauthorized` | `UnauthorizedCoreException` `USER_NOT_AUTHENTICATED`; `SaveChangesAsync` DidNotReceive |
| 68 | `CreateExamPeriodValidatorTests` | `Validate_Valid_Passes` | no errors |
| 69 | 〃 | `Validate_BlankName_FailsNameRequired` | code |
| 70 | 〃 | `Validate_NameOverMax_FailsNameTooLong` | 101 chars → code |
| 71 | 〃 | `Validate_MissingStart_FailsStartDateRequired` | code |
| 72 | 〃 | `Validate_MissingEnd_FailsEndDateRequired` | code |
| 73 | 〃 | `Validate_EndBeforeStart_FailsDateRangeInvalid` | code; no `EXAM_PERIOD_TOO_LONG` |
| 74 | 〃 | `Validate_SameDay_Passes` | no errors |
| 75 | 〃 | `Validate_OverMaxDays_FailsTooLong` | 121 days → code |
| 76 | 〃 | `Validate_ExactlyMaxDays_Passes` | 120 days inclusive → no errors |
| 77 | `UpdateExamPeriodHandlerTests` | `Handle_Existing_UpdatesAndReturnsResult` | fields; `UpdatedBy`; `SaveChangesAsync` Received(1) |
| 78 | 〃 | `Handle_Unknown_ThrowsNotFound` | `NotFoundCoreException` `EXAM_PERIOD_NOT_FOUND`; DidNotReceive |
| 79 | 〃 | `Handle_NoCurrentUser_ThrowsUnauthorized` | code; DidNotReceive |
| 80 | `UpdateExamPeriodValidatorTests` | `Validate_Valid_Passes`; `Validate_EmptyId_FailsIdRequired`; `Validate_BlankName_FailsNameRequired`; `Validate_NameOverMax_FailsNameTooLong`; `Validate_MissingStart_FailsStartDateRequired`; `Validate_MissingEnd_FailsEndDateRequired`; `Validate_EndBeforeStart_FailsDateRangeInvalid`; `Validate_OverMaxDays_FailsTooLong` | one code each |
| 81 | `DeleteExamPeriodHandlerTests` | `Handle_Existing_SoftDeletesAndSaves` | `IsDeleted`; Received(1) |
| 82 | 〃 | `Handle_Unknown_ThrowsNotFound` | code; DidNotReceive |
| 83 | 〃 | `Handle_NoCurrentUser_ThrowsUnauthorized` | code; DidNotReceive |
| 84 | `DeleteExamPeriodValidatorTests` | `Validate_Valid_Passes`; `Validate_EmptyId_FailsIdRequired` | |
| 85 | `ProcessTeacherThreadSlaHandlerTests` *modify* | ctor without `FakeRuntimeSettings`; `Record(...)` helper passes `thread.SlaWindowStartedAt`. Add `Handle_StageRecordedForEarlierWindow_RecordsAgainForCurrentWindow` (event for the same kind with a different `WindowStartedAt` → current window's stage recorded, `WindowStartedAt` = thread's) and `Handle_RecordsWindowStartAndDeadline` (added event `WindowStartedAt`, `SlaDueAt` equal the thread's) | |
| 86 | `GetDueSlaThreadIdsHandlerTests` *modify* | `Handle_UsesClockOptionsAndExcludedIds_ReturnsRepositoryIds` with `GetSlaDueIdsAsync(now, excluded, 50, …)` and the new ctor | |
| 87 | `CreateTeacherThreadHandlerTests` *modify* | ctor adds `Substitute.For<IExamPeriodRepository>()`; `Handle_LessonContext_AddsOpenThreadAndReturnsResult` keeps `SlaDueAt == now + 24h`. Add `Handle_CalendarSkipsWeekend_StoresCalendarDeadline` (`FakeRuntimeSettings(slaCalendar: new() { SkipWeekends = true })`, clock `2026-10-01T12:00Z` → result `SlaDueAt == 2026-10-04T12:00Z`) | |
| 88 | `FollowUpTeacherThreadHandlerTests` *modify* | ctor adds the repo substitute; add `Handle_ExamPeriodCoversWeekend_CountsEveryDay` (repo returns a period over the weekend, skip on, follow-up Thursday → due +24h wall clock) | |
| 89 | `GetTeacherInboxRemindersHandlerTests` *modify* | `Record` helper takes a window start; the "followed up" case records the old reminder with `followedUp.SlaWindowStartedAt.AddHours(-3)` | |
| 90 | `GetAskTeacherMetricsHandlerTests` *modify* | ctor without runtime settings; mocks use the 5-arg `GetReplyStatsAsync`. **Delete** `Handle_PassesConfiguredReplySla` (the parameter no longer exists; compliance is now covered by row 101). | |
| 91 | `GetMyTeacherStatsHandlerTests` *modify* | ctor without runtime settings; 5-arg mocks; `Handle_ExplicitRange_QueriesCairoDayBoundariesForCaller` asserts `GetReplyStatsAsync(start, end, null, _callerId, …)` | |
| 92 | `TrainingExportLineGeneratorTests` *modify* | `FollowUp(…, TeacherThreadSlaPolicies.WallClock())` | |
| 93 | `RuntimeSettingRegistryTests` *modify* | rename `Definitions_DefaultOptions_FourteenOrderedByGroup` → `Definitions_DefaultOptions_SeventeenOrderedByGroup`, `HaveCount(17)` | |
| 94 | `GetRuntimeSettingsHandlerTests` *modify* | `Handle_NoOverrides_ReturnsGroupsInEnumOrderWithDefaults` expects `Features, AskTeacher, SlaCalendar, PlanLimits, Grading, Uploads` | |

### Worker (`Api/Workers/TeacherThreadSlaWorkerTests.cs`, add)
| # | Test method | Asserts |
|---|-------------|---------|
| 95 | `Sweep_ReschedulesBeforeListingDueThreads` | `Received.InOrder`: `Send(RescheduleTeacherThreadSlasCommand(50))` then `Send(GetDueSlaThreadIdsQuery)` |
| 96 | `Sweep_FullRescheduleBatch_SendsAgainUntilShort` | returns 50 then 3 → reschedule sent exactly twice |
| 97 | `Sweep_RescheduleFails_LogsErrorAndStillProcessesDueThreads` | reschedule throws → one `LogLevel.Error`; due thread still processed |

### Integration (Docker)
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 98 | `TeacherThreadSlaSweepTests` *modify* | `Process_OverdueThread_RecordsEachStageOnce`: events `OnlyContain(x => x.WindowStartedAt == thread.SlaWindowStartedAt && x.SlaDueAt == thread.SlaDueAt)`; `GetSlaDueIds_ListsDueThreadsAndSkipsRecordedAndAnswered`: new signature, `Record` with window start | |
| 99 | `TeacherInboxRemindersEndpointTests` *modify*, `DashboardTestData` *modify* | `Record(..., thread.SlaWindowStartedAt, thread.SlaDueAt, ...)` | |
| 100 | `AskTeacherMetricsEndpointTests` (add) | `Get_SubjectFilter_ReplyAfterBreach_IsNotWithinSla` | thread, `SeedBreachAsync`, claim + reply via API → `replies` 1, `repliedWithinSla` 0, `slaComplianceRate` 0 |
| 101 | `ExamPeriodsEndpointTests` | `Post_Valid_CreatesPeriodAndWritesAudit` | 200; body fields; DB row; one `ExamPeriod.Create` audit with resource id |
| 102 | 〃 | `Post_EndBeforeStart_Returns422DateRangeInvalid` | 422, `code` |
| 103 | 〃 | `Post_AsTeacher_Returns403` | 403 |
| 104 | 〃 | `Post_Anonymous_Returns401` | 401 |
| 105 | 〃 | `Get_AsAdmin_ListsNewestStartFirst` | order by `startDate` desc |
| 106 | 〃 | `Put_Existing_UpdatesAndWritesAudit` | 200; DB; `ExamPeriod.Update` audit |
| 107 | 〃 | `Put_Unknown_Returns404` | 404 `EXAM_PERIOD_NOT_FOUND` |
| 108 | 〃 | `Delete_Existing_HidesPeriodAndWritesAudit` | 200; GET omits it; `IgnoreQueryFilters` row `IsDeleted`; `ExamPeriod.Delete` audit |
| 109 | 〃 | `Delete_Unknown_Returns404` | 404 |
| 110 | 〃 | `PutSetting_SevenWeekendDaysWithSkipOn_Returns400` | `PUT /api/configuration/settings/slaCalendar.weekendDays` with 7 days after `skipWeekends=true` override → 400 `SLA_CALENDAR_WEEKEND_DAYS_INVALID` |
| 111 | `TeacherThreadSlaCalendarTests` | `Reschedule_SkipWeekendsTurnedOn_MovesOpenThreadPastWeekend` | builder thread submitted `2026-10-01T12:00Z` (wall clock); override `slaCalendar.skipWeekends=true`; send `RescheduleTeacherThreadSlasCommand(500)` until < 500; stored first `2026-10-04T00:00Z`, second `08:00Z`, due `12:00Z`, window start unchanged |
| 112 | 〃 | `Reschedule_ExamPeriodCoversWeekend_CountsEveryDay` | skip on + period 10-02..10-03 seeded via POST → due `2026-10-02T12:00Z` |
| 113 | 〃 | `Reschedule_AnsweredThread_IsUnchanged` | answered thread keeps its times and fingerprint |
| 114 | 〃 | `GetSlaDueIds_RescheduledThread_UsesStoredCalendarTimes` | after row-111 setup: `GetSlaDueIdsAsync(2026-10-03T12:00Z, [], 100000)` excludes it; `(2026-10-04T00:00Z, …)` includes it |
| 115 | 〃 | `Reschedule_ThreadWithRecordedReminder_DoesNotRecordItAgain` | thread submitted `UtcNow.AddDays(-10)` (wall clock), FirstReminder event seeded for its window; skip-on override; reschedule; send `ProcessTeacherThreadSlaCommand` → events are exactly one FirstReminder, one SecondReminder, one Breach, all with the thread's `WindowStartedAt` |
| 116 | `TeacherReplyDeadlineEndpointTests` | `Get_AsStudent_ReturnsDeadline` | 200; `replySlaHours` 24; `slaDueAt - before >= 24h` and `<= after + 24h`; `skipsUncountedDays` false |
| 117 | 〃 | `Get_AsTeacher_Returns403` | 403 |
| 118 | 〃 | `Get_Anonymous_Returns401` | 401 |
| 119 | `ExamPeriodPersistenceTests` | `SaveChanges_ExamPeriod_RoundTripsDateOnlyColumns` | fresh scope reads equal dates |
| 120 | 〃 | `SaveChanges_StaleVersion_ThrowsExamPeriodModifiedConcurrently` | two scopes update the same row → `ConflictCoreException` `EXAM_PERIOD_MODIFIED_CONCURRENTLY` |
| 121 | `AppDbContextTests` (existing) | `Migrate_FreshDatabase_LeavesNoPendingMigrations` passes unchanged | |

### Web
| # | Test file | `it(...)` | Asserts |
|---|-----------|-----------|---------|
| 122 | `examPeriodSchema.test.ts` | `accepts a valid period`; `requires a name`; `requires the first day`; `requires the last day`; `rejects a last day before the first day`; `accepts a one-day period` | success / the exact message key on the exact path |
| 123 | `ExamPeriodsSection.test.tsx` | `shows the periods under the reply calendar card after loading` | heading «Reply calendar» precedes «Exam periods» in document order; row text "Final exams" and the formatted range |
| 124 | 〃 | `shows the empty state` | "No exam periods yet." |
| 125 | 〃 | `shows retry on error and recovers` | alert, Retry, then the row |
| 126 | 〃 | `adds a period and shows a toast` | dialog fields filled; POST body `{ name, startDate, endDate }`; toast "Exam period added."; list refetched shows new row |
| 127 | 〃 | `shows an inline error when the last day is before the first` | message under Last day; no request (MSW handler not hit) |
| 128 | 〃 | `shows the server error under the name field` | 422 `EXAM_PERIOD_NAME_TOO_LONG` → "The exam period name is too long." |
| 129 | 〃 | `edits a period` | dialog prefilled; PUT to `/exam-periods/{id}`; toast "Exam period saved." |
| 130 | 〃 | `deletes a period after confirming` | confirm dialog; DELETE; toast; row gone |
| 131 | 〃 | `renders right to left in Arabic` | `dir="rtl"`; «فترات الامتحانات» |
| 132 | 〃 | `has no axe violations` | axe |
| 133 | `ConfigurationPage.test.tsx` (add) | `saves the weekend days of the reply calendar` | `SlaCalendar` group with `slaCalendar.weekendDays` ChoiceList → checkboxes labelled «Friday»/«Saturday»; PUT body value `["Friday","Saturday"]` order = allowed order |
| 134 | `AskTeacherNewPage.test.tsx` *modify* | rename `shows the reply-time note from the plan catalogue` → `shows the calendar-aware reply deadline`; uses default handler: text matches `/reply within 24 hours: by Sunday, October 4, 2026/` and "Weekends outside exam periods do not count." visible | |
| 135 | 〃 (add) | `hides the weekend note when no day is skipped` | handler with `skipsUncountedDays: false` → note absent (`queryByText`) |

## Definition of done
- [ ] Branch rebased on `origin/main` containing #263 before implementation.
- [ ] `SlaCalendar.AddCountedTime` counts real elapsed time on counted Cairo days; tests 1–18 pass, incl. both 2026 DST transitions.
- [ ] Three runtime settings in group `SlaCalendar` (after `AskTeacher`), defaults from validated `SlaCalendarOptions`; seven-day weekend with skip on is refused with `400 SLA_CALENDAR_WEEKEND_DAYS_INVALID`.
- [ ] `ExamPeriods` table: soft delete, xmin, check constraint, global filter; `ExamPeriod : IAuditedEntity`; create/update/delete audited as `ExamPeriod.Create/Update/Delete`.
- [ ] Four exam-period endpoints under `/api/configuration/exam-periods`, policy `Configuration.Manage`; reply-deadline endpoint under `AskTeacherSubmit`.
- [ ] `TeacherThread` stores window start, both reminder times, deadline and fingerprint; `Submit`/`FollowUp` take a `TeacherThreadSlaPolicy`; `RescheduleSla` touches only `Open` threads with a different fingerprint and never moves the window start.
- [ ] Sweep: reschedule batches first (scope per batch, failure logged at Error and does not stop listing), then the due query on stored columns; no settings read in `GetDueSlaThreadIds` or `ProcessTeacherThreadSla`.
- [ ] SLA events keyed `(ThreadId, Kind, WindowStartedAt)` (unique); inbox reminders and compliance use the window key; recompute never resends a reminder (test 115).
- [ ] `GetReplyStatsAsync` has no `replySla` parameter; compliance = no breach in the window.
- [ ] Migration `AddSlaCalendar` contains the two backfill statements verbatim and no unplanned drop/rename.
- [ ] `#255` touch points unchanged in shape: `missing` list in `ProcessTeacherThreadSlaHandler`, `TeacherThread.SlaSchedule()`.
- [ ] Web: `SlaCalendar` card renders through the generic editors with day/time-zone labels; `ExamPeriodsSection` directly under it with loading/empty/error/create/edit/delete; ask form shows the deadline and the weekend note.
- [ ] Every new error code in `ErrorCodes`, both `.resx` files and both `configurationErrors` json files.
- [ ] Docs updated as in Docs (PRD §10.6, §12.1, §12.3, §15, §16, §17; configuration, ask-teacher, dashboard, audit-log, design prompt, prototype); `appsettings.example.json`, `deploy/api.env.example`, Postman, `api/openapi/v1.json`, generated web client regenerated.
- [ ] Every test row 1–135 exists with that name; only rows marked *modify*/*delete* touch existing tests.
- [ ] `dotnet build` no new warnings; `dotnet test` green; `dotnet format --verify-no-changes`; guard grep from the skill §9 prints nothing.
- [ ] Web: `npx tsc -b --noEmit`, `npx eslint . --max-warnings=0`, `npx prettier --check .`, `npx vitest run --coverage`, `npm run gen:api` no diff.
