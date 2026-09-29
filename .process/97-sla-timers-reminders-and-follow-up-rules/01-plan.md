# Plan — [E9.S4] SLA timers, reminders and follow-up rules (#97)

## Goal
After this ships, the platform enforces the PRD §12.1 Ask a Teacher SLA and follow-up rules. A background sweep reminds the teacher in-app 12 h and 20 h into each 24 h reply window. A breach is recorded, counted and raised as an admin alert (Prometheus → Alertmanager). A student can send exactly one follow-up on an answered thread; that opens a new 24 h window, and the teacher's next reply closes the thread. The student can rate the answer 1–5, and rating closes the thread if it is still open. Teacher replies reach the student live over SignalR. Reminders reach connected teachers live, and a reminders card on `/teacher/inbox` shows them to teachers who were offline.

## Scope
**In:**
- Sub-task 1 (SLA job): `TeacherThreadSlaWorker` → `GetDueSlaThreadIdsQuery` → `ProcessTeacherThreadSlaCommand` per thread. It records `FirstReminder` (12 h), `SecondReminder` (20 h) and `Breach` (24 h) once per SLA window in the new `TeacherThreadSlaEvents` table, pushes reminders to teachers, and for a breach it logs, counts `elmanhg.ask_teacher.sla_events` and fires the `AskTeacherSlaBreached` alert. `GET /api/teacher-inbox/reminders` and the reminders card.
- Sub-task 2 (follow-up): `POST /api/teacher-threads/{id}/follow-ups`. It is allowed once, only on `Answered`. The thread returns to `Open` with a new `SlaDueAt`, and the teacher's next reply sets `Closed` and `ClosedAt`.
- Sub-task 3 (rating): `POST /api/teacher-threads/{id}/rating` with a rating of 1–5, once. It closes an `Answered` thread and is also allowed on a thread closed by the final reply. The rating is shown to the student, the teacher and the admin (the admin sees it through `GET /api/teacher-inbox/{id}`).
- Sub-task 4 (realtime): the SignalR hub `/api/hubs/notifications`. `teacherReplyReceived` goes to the student after a text or voice reply is committed. `teacherThreadReminder` goes to the teachers.
- The migration `AddTeacherThreadSlaAndRatings`, options, resx, OpenAPI, Orval, Postman, alert rules and promtool tests, and docs.

**Out:** these are not asked for by the story, or need a product answer. Each is recorded for the dev as a product question, and none gets code:
- (a) Refund or credit on an SLA breach. The PRD is silent, so there is no money movement.
- (b) Escalation or reassignment on a breach. The PRD defines none, so a breach does not unclaim the thread.
- (c) WhatsApp or email reminders. The story says in-app only; the #171 channels are not used.
- (d) Pausing the SLA clock. The PRD has no pause rule.
- Also out: a realtime push to the teacher when a follow-up arrives (the reminders cover it), and an admin inbox or dashboard screen (#104–#106).

**Deferred:** none. Everything is buildable offline; SignalR runs in-process and needs no provider.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | SLA pause/resume | There is no pause. A follow-up starts a new window: `SlaDueAt = followUpAt + Subscriptions:AskTeacherReplySlaHours`. | The PRD has no pause rule. The prototype (`askFollowup`) and `docs/ask-teacher.md` ("resets `SlaDueAt`") both use a new window. |
| 2 | When reminders fire | At `windowStart + AskTeacher:FirstReminderAfterHours` (12) and `+ SecondReminderAfterHours` (20). `windowStart = SlaDueAt − AskTeacherReplySlaHours`. The breach is at `now >= SlaDueAt`. Only `Open` threads count. | PRD §12.1 step 3. The timings are configuration; there is no new column. |
| 3 | Who a reminder goes to | The claimer if the thread is claimed. Otherwise every teacher assigned to the subject (`TeacherSubject`). If there are no recipients, the event is still recorded and nobody is pushed. | For an unclaimed thread, "the teacher" means the subject's teacher pool. |
| 4 | Reminder channel | In-app only: a SignalR toast, plus the persistent «تذكيرات» card on `/teacher/inbox` that lists Open threads with a reminder in their current window. | The story asks for nothing more. WhatsApp would need an approved template (Out c). |
| 5 | The admin alert on a breach | A `Breach` event row, `ElmanhgMetrics.RecordAskTeacherSlaEvent(Breach)`, a Warning log, and the Prometheus alert `AskTeacherSlaBreached` (warning), delivered through Alertmanager. | The #113 alerting path is the admin's channel. The rows let #104 count "SLA breaches". There is no admin inbox screen. |
| 6 | Idempotency, and races with a teacher's reply | Events go in a separate table with a unique index on `(ThreadId, Kind, SlaDueAt)`. The worker never writes `TeacherThreads`. | Writing the thread would bump its `xmin` and give a 409 to a teacher who is replying. The unique key makes the stages exactly-once per window. |
| 7 | Catching up (worker down, or first deploy) | Record every missing stage in one save, send one teacher push for the highest missing reminder kind, and never push a breach. | This avoids three toasts at once and still marks every stage done. |
| 8 | Final reply | `Answer()`: if `HasFollowUp()` (the student has more than one message), set `Status = Closed` and `ClosedAt = at`; otherwise `Answered`. | PRD §12.1 step 5, and the "closing after the second reply" note in `ask-teacher.md`. |
| 9 | Rating rules | Rating is allowed when `Rating == null` and `Status != Open`. On `Answered` it sets `Closed` and `ClosedAt`, and gives up the follow-up. On `Closed` it keeps the existing `ClosedAt`. Rating `Open` is `409 TEACHER_THREAD_NOT_ANSWERED`; rating twice is `409 TEACHER_THREAD_ALREADY_RATED`; a value outside 1..5 is `422 TEACHER_THREAD_RATING_INVALID`. | The prototype's «قيّم الإجابة وأغلق السؤال» (rate and close) shows only while the thread is not awaiting a reply. |
| 10 | Follow-up rules | Only the owner can follow up (anyone else gets `404 TEACHER_THREAD_NOT_FOUND`). The thread must be `Answered`, otherwise `409 TEACHER_THREAD_FOLLOW_UP_NOT_ALLOWED`; this also covers "already used", because after a follow-up the thread is never `Answered` again. The text reuses `TEACHER_THREAD_TEXT_REQUIRED` / `TOO_LONG` with `QuestionTextMaxLength`. There is no photo, no add-on re-check, and no quota. | PRD §12.1 steps 1 and 5 (a follow-up does not count). The thread was paid for when it was asked. |
| 11 | Rating scale | The domain constants `TeacherThread.MinRating = 1` and `MaxRating = 5`, with a WHY comment. | PRD §12.1 fixes the scale, and the web renders five stars. It is not tunable. |
| 12 | Auditing | Follow-ups and ratings are not audited. | `ask-teacher.md`: threads are activity, and the rows are the record. |
| 13 | Realtime transport | ASP.NET Core SignalR (shared framework) with a `NotificationsHub` at `/api/hubs/notifications`, protected by `RequireAuthorization(DefaultCodes.AuthenticatedUser)`. The default user id is `NameIdentifier`. A JWT is read from `access_token` only on the hub path. There is a JSON enum string converter. Pushes are sent after `SaveChangesAsync`. | PRD §18. `ICurrentUserService` uses `ClaimTypes.NameIdentifier`. `RequestLogScrubber` and Caddy already redact query values. |
| 14 | Realtime delivery guarantee | Best effort. A missed push is covered by the refetch when the page opens and by the reminders card. The hub runs in memory on one API instance. | The same single-instance assumption as the transcription worker; it is a known limit in the docs. |
| 15 | Where the notifier is | `ITeacherThreadNotifier` lives in `Application/Shared/Realtime`, and `SignalRTeacherThreadNotifier` in `Elmanhg.Api/Realtime`. | This keeps SignalR out of Application. It is a port, so unit tests substitute it. |
| 16 | Web connection lifecycle | `RealtimeContext` holds a `RealtimeClientFactory`. The default is a no-op, `main.tsx` passes SignalR, and the tests pass `FakeRealtimeHub`. One connection per shell (student and teacher route components). | Existing tests stay offline (MSW `onUnhandledRequest: 'error'`), and pushes can be tested. |
| 17 | States of the reminders card | Loading, error and empty all render nothing. | It is an alert above the inbox, which keeps its own loading, error and empty states; a second error block would be noise. |
| 18 | Question text in a reminder | The first student message (`TeacherThreadResultGenerator.QuestionText`). | Consistent with the inbox list. |
| 19 | Follow-up detection | Derived as `HasFollowUp() => Messages.Count(student) > 1`. There is no new column. | The data model keeps PRD §15's `closed_at?` and `rating?` only. The reply handlers already load `Messages`. |
| 20 | Metric tag | The new tag `elmanhg.kind` (`FirstReminder`, `SecondReminder`, `Breach`). | The tag values are enum names, never ids (observability rule). |

Morabh reuse: the reminder job follows the Morabh shape "list the due items, then send one command per item" (`Morabh.Jobs/Functions/InstallmentDueReminderFunction.cs` + `SendInstallmentReminderNotificationFunction.cs`). It is expressed through Elmanhg's existing `SubscriptionLapseWorker` pattern (#81/#99). The code is new, because Morabh uses Azure Functions and Firebase. SignalR hub, notifier, follow-up and rating: new, with no Morabh equivalent (a grep for `Hub`/`SignalR` in Morabh finds nothing). `Core.Notifications` (Firebase/FCM) is not used: there are no FCM credentials, and the web app uses no push tokens.

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Domain/TeacherThreads/TeacherThread.cs` | Add `public DateTimeOffset? ClosedAt { get; private set; }` and `public int? Rating { get; private set; }` after `ClaimedAt`. |
| `api/Elmanhg.Domain/TeacherThreads/TeacherThread.Replies.cs` | `Answer(...)`: final-reply closing (Domain behaviour §3). |
| `api/Elmanhg.Domain/TeacherThreads/ITeacherThreadRepository.cs` | Add the two methods (Files #9). |
| `api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs` | Under `// TEACHER THREADS`: `TeacherThreadFollowUpNotAllowed`, `TeacherThreadNotAnswered`, `TeacherThreadAlreadyRated`. |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | Under `// ASK A TEACHER`: `TeacherThreadRatingInvalid = "TEACHER_THREAD_RATING_INVALID"`. |
| `api/Elmanhg.Application/Shared/Options/AskTeacherOptions.cs` | Add `bool SlaSweepEnabled = true`; `[Range(1,3600)] int SlaSweepIntervalSeconds = 60`; `[Range(1,500)] int SlaSweepBatchSize = 50`; `[Range(1,168)] int FirstReminderAfterHours = 12`; `[Range(1,168)] int SecondReminderAfterHours = 20`; `[Range(1,100)] int ReminderListMaxCount = 20`. |
| `api/Elmanhg.Application/Shared/Observability/ElmanhgMetrics.cs` | Add `public const string KindTag = "elmanhg.kind";`, a counter `_askTeacherSlaEvents = meter.CreateCounter<long>("elmanhg.ask_teacher.sla_events", "{event}", "Ask a Teacher SLA reminders and breaches recorded, by kind.")`, and `public void RecordAskTeacherSlaEvent(TeacherThreadSlaEventKind kind) => _askTeacherSlaEvents.Add(1, new KeyValuePair<string, object?>(KindTag, kind.ToString()));`. |
| `api/Elmanhg.Application/TeacherThreads/Shared/TeacherThreadResult.cs` | Append `int? Rating, DateTimeOffset? ClosedAt, bool CanFollowUp, bool CanRate`. |
| `api/Elmanhg.Application/TeacherThreads/Shared/TeacherThreadResultGenerator.cs` | `Generate` passes `thread.Rating, thread.ClosedAt, thread.CanFollowUp(), thread.CanBeRated()`. |
| `api/Elmanhg.Application/TeacherInbox/Shared/TeacherInboxThreadResult.cs` | Append `int? Rating`. |
| `api/Elmanhg.Application/TeacherInbox/Shared/TeacherInboxResultGenerator.cs` | `GenerateThread` passes `thread.Rating`. Add `public static TeacherInboxReminderResult GenerateReminder(TeacherThread thread, TeacherThreadSlaEventKind kind, Guid callerId, DateTimeOffset now)` → `new(thread.Id, context.SubjectName, context.LessonName, TeacherThreadResultGenerator.QuestionText(thread), kind, thread.IsClaimedBy(callerId), thread.IsOverdueAt(now), thread.SlaDueAt)`. |
| `api/Elmanhg.Application/TeacherInbox/ReplyToTeacherThread/ReplyToTeacherThreadHandler.cs` | Add a constructor dependency `ITeacherThreadNotifier teacherThreadNotifier` (last). After `SaveChangesAsync`: `await teacherThreadNotifier.NotifyReplyAsync(thread.StudentId, thread.Id, cancellationToken).ConfigureAwait(false);`. |
| `api/Elmanhg.Application/TeacherInbox/SendVoiceReply/SendVoiceReplyHandler.cs` | The same as the line above. |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | `public DbSet<TeacherThreadSlaEvent> TeacherThreadSlaEvents { get; set; }`. In `ConfigureTeacherThreads`: `builder.HasIndex(x => new { x.Status, x.SlaDueAt });` and `builder.ToTable(x => x.HasCheckConstraint("CK_TeacherThreads_Rating", "\"Rating\" IS NULL OR \"Rating\" BETWEEN 1 AND 5"));`. A new `ConfigureTeacherThreadSlaEvents(modelBuilder)` called after `ConfigureTeacherVoiceDrafts`: `Kind` `HasConversion<string>().HasMaxLength(EnumColumnMaxLength)`; `HasOne<TeacherThread>().WithMany().HasForeignKey(x => x.ThreadId).OnDelete(Restrict)`; `HasOne<User>().WithMany().HasForeignKey(x => x.TeacherId).OnDelete(Restrict)`; `HasIndex(x => new { x.ThreadId, x.Kind, x.SlaDueAt }).IsUnique()`. Add `modelBuilder.Entity<TeacherThreadSlaEvent>().HasQueryFilter(x => !x.IsDeleted);` to the global filter method. |
| `api/Elmanhg.Infrastructure/TeacherThreads/TeacherThreadRepository.cs` | Implement the two new methods (Files #9). |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | `services.AddScoped<ITeacherThreadSlaEventRepository, TeacherThreadSlaEventRepository>();` after the voice-draft line. |
| `api/Elmanhg.Api/Program.cs` | After `AddHostedService<TeacherVoiceTranscriptionWorker>()`: `builder.Services.AddHostedService<TeacherThreadSlaWorker>();` and `builder.Services.AddElmanhgRealtime();`. After `app.MapControllers();`: `app.MapElmanhgRealtime();`. Middleware order unchanged. |
| `api/Elmanhg.Api/Controllers/TeacherThreads/TeacherThreadsController.cs` | Two actions (API surface). |
| `api/Elmanhg.Api/Controllers/TeacherInbox/TeacherInboxController.cs` | One action `GetTeacherInboxReminders` (API surface), placed before `GetInboxThread`. |
| `api/Elmanhg.Api/Resources/Messages.ar.resx`, `Messages.en.resx` | 4 keys (Error codes). |
| `api/Elmanhg.Api/appsettings.example.json` | In the `AskTeacher` object: `"SlaSweepEnabled": true, "SlaSweepIntervalSeconds": 60, "SlaSweepBatchSize": 50, "FirstReminderAfterHours": 12, "SecondReminderAfterHours": 20, "ReminderListMaxCount": 20`. |
| `api/openapi/v1.json` | Regenerated by `dotnet build`. |
| `api/Directory.Packages.props` | `<PackageVersion Include="Microsoft.AspNetCore.SignalR.Client" Version="10.0.5" />` (verify with `dotnet package search Microsoft.AspNetCore.SignalR.Client --exact-match`; if 10.0.5 is missing, use the newest 10.0.x and report it). |
| `api/Elmanhg.Tests/Elmanhg.Tests.csproj` | `<PackageReference Include="Microsoft.AspNetCore.SignalR.Client" />`. |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | `builder.UseSetting("AskTeacher:SlaSweepEnabled", "false");`, with the comment `// The sweep would race tests that record SLA events through the mediator.` |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | Append `thirtyFirst => thirtyFirst.Should().EndWith("_AddTeacherThreadSlaAndRatings")` (accepted pattern). |
| `api/Elmanhg.Tests/Builders/TeacherThreadBuilder.cs` | Add `FollowedUp()`, `FinalReplied()` and `Rated(int rating)` (Test plan, builder). |
| `api/Elmanhg.Tests/Application/Features/TeacherInbox/ReplyToTeacherThread/ReplyToTeacherThreadHandlerTests.cs` | Add a field `ITeacherThreadNotifier _notifier = Substitute.For<ITeacherThreadNotifier>()` and pass it to the constructor; add the tests T37 and T38. |
| `api/Elmanhg.Tests/Application/Features/TeacherInbox/SendVoiceReply/SendVoiceReplyHandlerTests.cs` | The same field and constructor change; add T39. |
| `api/Elmanhg.Tests/Application/Features/TeacherInbox/Shared/TeacherInboxResultGeneratorTests.cs` | Add T40 and T41. |
| `api/Elmanhg.Tests/Application/Features/Shared/Observability/ElmanhgMetricsTests.cs` | Add T42. |
| `web/package.json`, `web/package-lock.json` | Add `"@microsoft/signalr": "10.0.11"` to `dependencies`, the version `npm view @microsoft/signalr version` printed at planning time. |
| `web/vite.config.ts` | Dev proxy: `'/api': { target: proxyTarget, changeOrigin: true, ws: true }`. |
| `web/src/app/providers.tsx` | Add the prop `realtimeClientFactory?: RealtimeClientFactory`, and wrap `children` + `<Toaster />` in `<RealtimeContext value={realtimeClientFactory ?? noopRealtimeClientFactory}>`. |
| `web/src/main.tsx` | Pass `realtimeClientFactory={createSignalRRealtimeClient}`. |
| `web/src/routes/student/route.tsx` | `component: () => (<AvatarProvider><StudentRealtimeListener /><AppShell role="student" assistant={<AvatarDock />} /></AvatarProvider>)`, imported from `@/features/askTeacher`. |
| `web/src/routes/teacher/route.tsx` | `component: () => (<><TeacherRealtimeListener /><AppShell role="teacher" /></>)`. |
| `web/src/features/askTeacher/index.ts` | Export `StudentRealtimeListener` and `TeacherRealtimeListener`. |
| `web/src/features/askTeacher/pages/TeacherThreadPage.tsx` (the student thread view) | After the messages: `{data.rating != null ? <ThreadRating rating={data.rating} /> : null}` then `<StudentThreadActions thread={data} />`. |
| `web/src/features/askTeacher/pages/InboxThreadPage.tsx` (the teacher thread view) | After the messages: `{data.rating != null ? <ThreadRating rating={data.rating} /> : null}` before `<InboxThreadActions>`. |
| `web/src/features/askTeacher/pages/TeacherInboxPage.tsx` | `<InboxReminders />` between the `h1` and `<InboxFilterTabs>`. |
| `web/src/features/askTeacher/i18n/ar.json`, `en.json` | Keys under Web strings. |
| `web/src/shared/i18n/ar.json`, `en.json` | The 4 error codes under `errors` (Error codes). |
| `web/src/shared/api/generated/**` | Regenerated with `npm --prefix web run gen:api`. |
| `web/src/test/askTeacherFixtures.ts` | `teacherThread()` adds `rating: null, closedAt: null, canFollowUp: false, canRate: false`. Add `answeredThread()` = `teacherThread({ status: 'Answered', canFollowUp: true, canRate: true, messages: [...teacherThread().messages, teacherTextReply] })`, where `teacherTextReply` = `{ id: 'f5f5f5f5-f5f5-4f5f-8f5f-f5f5f5f5f5f5', isFromStudent: false, kind: 'Text', text: 'Force equals mass times acceleration.', imageUrl: null, createdAt: '2026-10-01T09:00:00Z', audioUrl: null, audioDurationSeconds: null }`. `voiceAnsweredThread()` adds `canFollowUp: true, canRate: true`. Add `reminder(overrides?: Partial<TeacherInboxReminderResult>)` = `{ threadId, subjectName: 'Physics', lessonName: "Newton's laws", questionText: 'Why is F = ma?', kind: 'FirstReminder', isClaimedByMe: false, isOverdue: false, slaDueAt: '2026-10-02T07:00:00Z', ...overrides }`. |
| `web/src/test/teacherInboxFixtures.ts` | `inboxThread()` adds `rating: null`. |
| `web/src/test/msw/server.ts` | Default handler `getGetTeacherInboxRemindersMockHandler([])`. |
| `web/src/test/renderWithProviders.tsx` | Both helpers create `const realtime = new FakeRealtimeHub();` and pass `realtimeClientFactory={realtime.factory}` to `AppProviders`. `renderApp` returns `{ ...renderResult, router, queryClient, realtime }`. |
| `deploy/observability/prometheus/rules/elmanhg.rules.yml` | A new group `elmanhg-ask-teacher` with `AskTeacherSlaBreached` (Observability). |
| `deploy/observability/prometheus/tests/elmanhg.rules.test.yml` | 2 cases (Test plan T70–T71). |
| `deploy/api.env.example` | Under Ask a Teacher: `# AskTeacher__SlaSweepEnabled=true`, `# AskTeacher__SlaSweepIntervalSeconds=60`, `# AskTeacher__SlaSweepBatchSize=50`, `# AskTeacher__FirstReminderAfterHours=12`, `# AskTeacher__SecondReminderAfterHours=20`, `# AskTeacher__ReminderListMaxCount=20`. |
| `postman/elmanhg.postman_collection.json` | See Postman. |
| `docs/ask-teacher.md`, `docs/PRD.md`, `docs/observability.md`, `docs/deployment.md`, `docs/claude-design-prompt.md`, `docs/prototype.md`, `docs/subscriptions.md` | See Docs. |

## Files to create

### API — Domain (`namespace Elmanhg.Domain.TeacherThreads;`)
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `api/Elmanhg.Domain/TeacherThreads/TeacherThreadSlaEventKind.cs` | enum | `public enum TeacherThreadSlaEventKind { FirstReminder, SecondReminder, Breach }`. The order matters: `Max()` picks the most advanced reminder. |
| 2 | `api/Elmanhg.Domain/TeacherThreads/TeacherThreadSlaEvent.cs` | `public class TeacherThreadSlaEvent : Entity` | Props (all `{ get; private set; }`): `Guid ThreadId`, `TeacherThreadSlaEventKind Kind`, `DateTimeOffset SlaDueAt`, `Guid? TeacherId`, `DateTimeOffset OccurredAt`. `private TeacherThreadSlaEvent(Guid id) : base(id) { }`. `public static TeacherThreadSlaEvent Record(Guid threadId, TeacherThreadSlaEventKind kind, DateTimeOffset slaDueAt, Guid? teacherId, DateTimeOffset occurredAt)` → a new `Guid.NewGuid()` id, `OccurredAt = TeacherThread.ToMicroseconds(occurredAt)`, `SlaDueAt` stored as given (already truncated on the thread). |
| 3 | `api/Elmanhg.Domain/TeacherThreads/ITeacherThreadSlaEventRepository.cs` | interface | `public interface ITeacherThreadSlaEventRepository : IRepository<TeacherThreadSlaEvent> { }` |
| 4 | `api/Elmanhg.Domain/TeacherThreads/TeacherThread.FollowUps.cs` | `public partial class TeacherThread` | Constants, `HasFollowUp`, `CanFollowUp`, `CanBeRated`, `FollowUp`, `Rate` (Domain behaviour §1–§2). |
| 5 | `api/Elmanhg.Domain/TeacherThreads/TeacherThread.Sla.cs` | `public partial class TeacherThread` | `public List<TeacherThreadSlaEventKind> DueSlaStages(DateTimeOffset now, TimeSpan replySla, TimeSpan firstReminderAfter, TimeSpan secondReminderAfter)` (Domain behaviour §4). |

### API — Application
| # | Path | Type | Contract |
|---|------|------|----------|
| 6 | `api/Elmanhg.Application/Shared/Realtime/ITeacherThreadNotifier.cs` | interface, `namespace Elmanhg.Application.Shared.Realtime;` | `Task NotifyReplyAsync(Guid studentId, Guid threadId, CancellationToken cancellationToken);` and `Task NotifyReminderAsync(IReadOnlyCollection<Guid> teacherIds, Guid threadId, TeacherThreadSlaEventKind kind, CancellationToken cancellationToken);` |
| 7 | `api/Elmanhg.Application/TeacherInbox/Shared/TeacherInboxReminderResult.cs` | record (client-facing, no `LocalizedText`) | `public sealed record TeacherInboxReminderResult(Guid ThreadId, string SubjectName, string LessonName, string QuestionText, TeacherThreadSlaEventKind Kind, bool IsClaimedByMe, bool IsOverdue, DateTimeOffset SlaDueAt);` |
| 8a | `api/Elmanhg.Application/TeacherThreads/FollowUpTeacherThread/FollowUpTeacherThreadCommand.cs` | record | `public sealed record FollowUpTeacherThreadCommand(Guid ThreadId, string? Text) : IRequest<TeacherThreadResult>;` |
| 8b | `.../FollowUpTeacherThread/FollowUpTeacherThreadValidator.cs` | validator | The constructor takes `IOptions<AskTeacherOptions> askTeacherOptions`. `RuleFor(x => x.Text).Cascade(CascadeMode.Stop).ValidateRequired(ErrorCodes.TeacherThreadTextRequired).ValidateMaxLength(options.QuestionTextMaxLength, ErrorCodes.TeacherThreadTextTooLong);` |
| 8c | `.../FollowUpTeacherThread/FollowUpTeacherThreadHandler.cs` | handler | `(ITeacherThreadRepository teacherThreadRepository, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider, ICurrentUserService currentUserService)`. Handle: (1) user guard → `UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated)`; (2) `thread = FirstOrDefaultAsync(x => x.Id == request.ThreadId && x.StudentId == userId, ct, include: q => q.Include(x => x.Messages))` ?? `NotFoundCoreException(ErrorCodes.TeacherThreadNotFound)`; (3) `now = timeProvider.GetUtcNow()`; (4) `thread.FollowUp(request.Text ?? string.Empty, now, TimeSpan.FromHours(subscriptionsOptions.Value.AskTeacherReplySlaHours))`; (5) `SaveChangesAsync` once; (6) `return TeacherThreadResultGenerator.Generate(thread, now)`. |
| 9a | `api/Elmanhg.Application/TeacherThreads/RateTeacherThread/RateTeacherThreadCommand.cs` | record | `public sealed record RateTeacherThreadCommand(Guid ThreadId, int Rating) : IRequest<TeacherThreadResult>;` |
| 9b | `.../RateTeacherThread/RateTeacherThreadValidator.cs` | validator | `RuleFor(x => x.Rating).ValidateRange(TeacherThread.MinRating, TeacherThread.MaxRating, ErrorCodes.TeacherThreadRatingInvalid);` |
| 9c | `.../RateTeacherThread/RateTeacherThreadHandler.cs` | handler | `(ITeacherThreadRepository teacherThreadRepository, TimeProvider timeProvider, ICurrentUserService currentUserService)`. Handle: the user guard → the owner-scoped load with Messages (404 as in 8c) → `now` → `thread.Rate(request.Rating, now)` → `SaveChangesAsync` → `TeacherThreadResultGenerator.Generate(thread, now)`. |
| 10a | `api/Elmanhg.Application/TeacherThreads/GetDueSlaThreadIds/GetDueSlaThreadIdsQuery.cs` | record | `public sealed record GetDueSlaThreadIdsQuery(IReadOnlyCollection<Guid> ExcludedIds) : IRequest<List<Guid>>;` |
| 10b | `.../GetDueSlaThreadIds/GetDueSlaThreadIdsHandler.cs` | handler | `(ITeacherThreadRepository teacherThreadRepository, IOptions<AskTeacherOptions> askTeacherOptions, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider)` → `return await teacherThreadRepository.GetSlaDueIdsAsync(timeProvider.GetUtcNow(), TimeSpan.FromHours(subscriptions.AskTeacherReplySlaHours), TimeSpan.FromHours(askTeacher.FirstReminderAfterHours), TimeSpan.FromHours(askTeacher.SecondReminderAfterHours), request.ExcludedIds, askTeacher.SlaSweepBatchSize, cancellationToken)`. |
| 11a | `api/Elmanhg.Application/TeacherThreads/ProcessTeacherThreadSla/ProcessTeacherThreadSlaCommand.cs` | record | `public sealed record ProcessTeacherThreadSlaCommand(Guid ThreadId) : IRequest;` |
| 11b | `.../ProcessTeacherThreadSla/ProcessTeacherThreadSlaHandler.cs` | handler | `(ITeacherThreadRepository teacherThreadRepository, ITeacherThreadSlaEventRepository teacherThreadSlaEventRepository, ITeacherSubjectRepository teacherSubjectRepository, ITeacherThreadNotifier teacherThreadNotifier, ElmanhgMetrics metrics, IOptions<AskTeacherOptions> askTeacherOptions, IOptions<SubscriptionsOptions> subscriptionsOptions, TimeProvider timeProvider, ILogger<ProcessTeacherThreadSlaHandler> logger)`. Handle: (1) `thread = FirstOrDefaultAsync(x => x.Id == request.ThreadId, ct, asNoTracking: true)`; null → return; (2) `now`; `due = thread.DueSlaStages(now, replySla, first, second)`; empty → return; (3) `recorded = (await teacherThreadSlaEventRepository.FindAsync(x => x.ThreadId == thread.Id && x.SlaDueAt == thread.SlaDueAt, ct, asNoTracking: true)).Select(x => x.Kind)`; (4) `missing = due.Except(recorded).ToList()`; empty → return; (5) `foreach kind`: `AddAsync(TeacherThreadSlaEvent.Record(thread.Id, kind, thread.SlaDueAt, thread.TeacherId, now))`; (6) `SaveChangesAsync` once; (7) `foreach kind`: `metrics.RecordAskTeacherSlaEvent(kind)`; (8) if `missing.Contains(Breach)`: `logger.LogWarning("Ask a Teacher thread {ThreadId} passed its reply deadline {SlaDueAt} without a reply.", thread.Id, thread.SlaDueAt)`; (9) `reminders = missing.Where(x => x != Breach).ToList()`; if none → return; (10) `recipients = await TeacherThreadReminderRecipients.LoadAsync(thread, teacherSubjectRepository, ct)`; if `recipients.Count == 0` → return; (11) `await teacherThreadNotifier.NotifyReminderAsync(recipients, thread.Id, reminders.Max(), ct)`. |
| 11c | `.../ProcessTeacherThreadSla/TeacherThreadReminderRecipients.cs` | static class | `public static async Task<List<Guid>> LoadAsync(TeacherThread thread, ITeacherSubjectRepository teacherSubjectRepository, CancellationToken cancellationToken)`: `thread.TeacherId is { } teacherId` → `[teacherId]`; otherwise `(await teacherSubjectRepository.FindAsync(x => x.SubjectId == thread.SubjectId, cancellationToken, asNoTracking: true)).Select(x => x.TeacherId).Distinct().ToList()`. |
| 12a | `api/Elmanhg.Application/TeacherInbox/GetTeacherInboxReminders/GetTeacherInboxRemindersQuery.cs` | record | `public sealed record GetTeacherInboxRemindersQuery : IRequest<List<TeacherInboxReminderResult>>;` |
| 12b | `.../GetTeacherInboxReminders/GetTeacherInboxRemindersHandler.cs` | handler | `(ITeacherThreadRepository teacherThreadRepository, ITeacherThreadSlaEventRepository teacherThreadSlaEventRepository, ITeacherSubjectRepository teacherSubjectRepository, IOptions<AskTeacherOptions> askTeacherOptions, TimeProvider timeProvider, ICurrentUserService currentUserService)`. Handle: (1) user guard 401; (2) `subjectIds` exactly as in `GetTeacherInboxHandler`: `null` for an Admin (`GetClaim(ClaimTypes.Role) == nameof(UserRole.Admin)`), otherwise the caller's `TeacherSubject.SubjectId`s; (3) `threads = GetRemindedOpenThreadsAsync(subjectIds, userId, options.ReminderListMaxCount, ct)`; (4) `ids = threads.Select(x => x.Id).ToList()`; `events = teacherThreadSlaEventRepository.FindAsync(x => ids.Contains(x.ThreadId) && x.Kind != TeacherThreadSlaEventKind.Breach, ct, asNoTracking: true)`; (5) `now`; return `threads.Select(t => TeacherInboxResultGenerator.GenerateReminder(t, events.Where(e => e.ThreadId == t.Id && e.SlaDueAt == t.SlaDueAt).Max(e => e.Kind), userId, now)).ToList()`, in the repository order. |

### API — Infrastructure
| # | Path | Type | Contract |
|---|------|------|----------|
| 13 | `api/Elmanhg.Infrastructure/TeacherThreads/TeacherThreadSlaEventRepository.cs` | repo | `public class TeacherThreadSlaEventRepository(AppDbContext context) : Repository<TeacherThreadSlaEvent>(context), ITeacherThreadSlaEventRepository { }` |
| 14 | `api/Elmanhg.Infrastructure/Migrations/<timestamp>_AddTeacherThreadSlaAndRatings.cs` (+ `.Designer.cs`, snapshot) | EF migration | Generated with `dotnet ef migrations add AddTeacherThreadSlaAndRatings -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`. It must contain only: `AddColumn ClosedAt timestamptz NULL` and `Rating integer NULL` on `TeacherThreads`, the check constraint `CK_TeacherThreads_Rating`, the index `IX_TeacherThreads_Status_SlaDueAt`, `CreateTable TeacherThreadSlaEvents` with FKs, the unique index `(ThreadId, Kind, SlaDueAt)` and the index on `TeacherId`. No Drop or Rename. |

`ITeacherThreadRepository` gets two methods (declared in the existing file, implemented in `TeacherThreadRepository`):
```csharp
Task<List<Guid>> GetSlaDueIdsAsync(DateTimeOffset now, TimeSpan replySla, TimeSpan firstReminderAfter, TimeSpan secondReminderAfter, IReadOnlyCollection<Guid> excludedIds, int limit, CancellationToken cancellationToken);
Task<List<TeacherThread>> GetRemindedOpenThreadsAsync(IReadOnlyCollection<Guid>? subjectIds, Guid callerId, int limit, CancellationToken cancellationToken);
```
- `GetSlaDueIdsAsync`: `var firstCutoff = now + replySla - firstReminderAfter; var secondCutoff = now + replySla - secondReminderAfter; var events = _context.Set<TeacherThreadSlaEvent>();` then `_dbSet.AsNoTracking().Where(x => x.Status == TeacherThreadStatus.Open && !excludedIds.Contains(x.Id) && ((x.SlaDueAt <= firstCutoff && !events.Any(e => e.ThreadId == x.Id && e.SlaDueAt == x.SlaDueAt && e.Kind == TeacherThreadSlaEventKind.FirstReminder)) || (x.SlaDueAt <= secondCutoff && !events.Any(... SecondReminder)) || (x.SlaDueAt <= now && !events.Any(... Breach)))).OrderBy(x => x.SlaDueAt).ThenBy(x => x.Id).Select(x => x.Id).Take(limit).ToListAsync(cancellationToken).ConfigureAwait(false)`. One chain operator per line.
- `GetRemindedOpenThreadsAsync`: `_dbSet.AsNoTracking().Include(x => x.Messages).Where(x => x.Status == TeacherThreadStatus.Open && (x.TeacherId == callerId || (x.TeacherId == null && (subjectIds == null || subjectIds.Contains(x.SubjectId)))) && events.Any(e => e.ThreadId == x.Id && e.SlaDueAt == x.SlaDueAt && e.Kind != TeacherThreadSlaEventKind.Breach)).OrderBy(x => x.SlaDueAt).ThenBy(x => x.Id).Take(limit).ToListAsync(...)`.

### API — Api layer
| # | Path | Type | Contract |
|---|------|------|----------|
| 15 | `api/Elmanhg.Api/Realtime/NotificationsHub.cs` | `public sealed class NotificationsHub : Hub` | `public const string Path = "/api/hubs/notifications";` It has no client-callable methods. |
| 16 | `api/Elmanhg.Api/Realtime/RealtimeEvents.cs` | static class | `public const string TeacherReplyReceived = "teacherReplyReceived";` and `public const string TeacherThreadReminder = "teacherThreadReminder";` |
| 17 | `api/Elmanhg.Api/Realtime/TeacherReplyReceivedMessage.cs` | record | `public sealed record TeacherReplyReceivedMessage(Guid ThreadId);` |
| 18 | `api/Elmanhg.Api/Realtime/TeacherThreadReminderMessage.cs` | record | `public sealed record TeacherThreadReminderMessage(Guid ThreadId, TeacherThreadSlaEventKind Kind);` |
| 19 | `api/Elmanhg.Api/Realtime/SignalRTeacherThreadNotifier.cs` | `public sealed class SignalRTeacherThreadNotifier(IHubContext<NotificationsHub> hubContext) : ITeacherThreadNotifier` | `NotifyReplyAsync` → `hubContext.Clients.User(studentId.ToString()).SendAsync(RealtimeEvents.TeacherReplyReceived, new TeacherReplyReceivedMessage(threadId), cancellationToken)`. `NotifyReminderAsync` → `hubContext.Clients.Users(teacherIds.Select(x => x.ToString()).ToList()).SendAsync(RealtimeEvents.TeacherThreadReminder, new TeacherThreadReminderMessage(threadId, kind), cancellationToken)`. |
| 20 | `api/Elmanhg.Api/Realtime/HubAccessToken.cs` | static class | `public const string QueryKey = "access_token";` and `public static Task OnMessageReceived(MessageReceivedContext context)`: if `context.HttpContext.Request.Path.StartsWithSegments(NotificationsHub.Path)` and `context.Request.Query[QueryKey]` is not null or empty → `context.Token = value.ToString()`. Return `Task.CompletedTask`. Comment (WHY): `// Browsers cannot set headers on WebSocket or EventSource requests, so SignalR sends the bearer token in the query; it is accepted on the hub path only, and request logs redact query values.` |
| 21 | `api/Elmanhg.Api/Realtime/RealtimeExtensions.cs` | static class | `public static IServiceCollection AddElmanhgRealtime(this IServiceCollection services)`: `services.AddSignalR().AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));`, `services.AddSingleton<ITeacherThreadNotifier, SignalRTeacherThreadNotifier>();`, `services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options => { options.Events ??= new JwtBearerEvents(); options.Events.OnMessageReceived = HubAccessToken.OnMessageReceived; });` (Core.Identity sets no events; this was verified). `public static IEndpointRouteBuilder MapElmanhgRealtime(this IEndpointRouteBuilder endpoints)`: `endpoints.MapHub<NotificationsHub>(NotificationsHub.Path).RequireAuthorization(DefaultCodes.AuthenticatedUser); return endpoints;`. |
| 22 | `api/Elmanhg.Api/Workers/TeacherThreadSlaWorker.cs` | `BackgroundService` | `(IServiceScopeFactory scopeFactory, IOptions<AskTeacherOptions> askTeacherOptions, TimeProvider timeProvider, ILogger<TeacherThreadSlaWorker> logger, BackgroundJobMetrics jobMetrics)`, with `private const string JobName = "ask-teacher-sla";`. A line-for-line mirror of `SubscriptionLapseWorker`: `SlaSweepEnabled`, `SlaSweepIntervalSeconds`, `SlaSweepBatchSize`, the `_deferredIds` HashSet, `GetDueSlaThreadIdsQuery([.. _deferredIds])`, and one `ProcessTeacherThreadSlaCommand(id)` per id in its own async scope. Log texts: `"Listing due Ask a Teacher SLA threads failed."` (Error) and `"SLA processing of thread {ThreadId} failed."` (Warning). |
| 23 | `api/Elmanhg.Api/Controllers/TeacherThreads/Requests.cs` | records | `public sealed record FollowUpTeacherThreadRequest(string? Text);` and `public sealed record RateTeacherThreadRequest(int Rating);` |

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `Domain…ErrorCodes.TeacherThreadFollowUpNotAllowed` | `TEACHER_THREAD_FOLLOW_UP_NOT_ALLOWED` | `TeacherThread.FollowUp` | `ConflictCoreException` | 409 |
| `Domain…ErrorCodes.TeacherThreadNotAnswered` | `TEACHER_THREAD_NOT_ANSWERED` | `TeacherThread.Rate` | `ConflictCoreException` | 409 |
| `Domain…ErrorCodes.TeacherThreadAlreadyRated` | `TEACHER_THREAD_ALREADY_RATED` | `TeacherThread.Rate` | `ConflictCoreException` | 409 |
| `Application…ErrorCodes.TeacherThreadRatingInvalid` | `TEACHER_THREAD_RATING_INVALID` | `RateTeacherThreadValidator` | validation pipeline | 422 |
| (reused) `TeacherThreadTextRequired` / `TeacherThreadTextTooLong` | existing | `FollowUpTeacherThreadValidator` | validation pipeline | 422 |
| (reused) `TeacherMessageTextRequired` | existing | `TeacherMessage.CreateText` (whitespace that passes the validator is impossible; defence only) | `BusinessRuleViolationCoreException` | 400 |

The same strings go in the resx files and in web `shared/i18n` `errors` (Arabic without tashkeel in the resx; the web copy is identical):
| Key | ar | en |
|---|---|---|
| `TEACHER_THREAD_FOLLOW_UP_NOT_ALLOWED` | لا يمكن ارسال سؤال متابعة لهذا السؤال الان. | You can't send a follow-up on this question now. |
| `TEACHER_THREAD_NOT_ANSWERED` | لم يرد المعلم على هذا السؤال بعد. | The teacher has not answered this question yet. |
| `TEACHER_THREAD_ALREADY_RATED` | قيمت هذه الاجابة من قبل. | You have already rated this answer. |
| `TEACHER_THREAD_RATING_INVALID` | اختر تقييما من 1 الى 5. | Choose a rating from 1 to 5. |

## Domain behaviour
**§1 `TeacherThread.FollowUps.cs`**
```csharp
// PRD §12.1 fixes the rating scale at 1–5; the web renders five stars.
public const int MinRating = 1;
public const int MaxRating = 5;

public bool HasFollowUp() => Messages.Count(x => x.SenderId == StudentId) > 1;
public bool CanFollowUp() => Status == TeacherThreadStatus.Answered;
public bool CanBeRated() => Rating is null && Status != TeacherThreadStatus.Open;

public TeacherMessage FollowUp(string text, DateTimeOffset askedAt, TimeSpan replySla)
{
    if (!CanFollowUp()) { throw new ConflictCoreException(ErrorCodes.TeacherThreadFollowUpNotAllowed); }
    var at = ToMicroseconds(askedAt);
    var message = TeacherMessage.CreateText(Id, StudentId, text, null, at);
    Messages.Add(message);
    Status = TeacherThreadStatus.Open;
    SlaDueAt = at + replySla;
    UpdatedBy = StudentId;
    UpdationDate = at;
    return message;
}
```
**§2 `Rate(int rating, DateTimeOffset ratedAt)`**, in this order: `ArgumentOutOfRangeException.ThrowIfLessThan(rating, MinRating)`; `ThrowIfGreaterThan(rating, MaxRating)`; `Status == Open` → `Conflict(TeacherThreadNotAnswered)`; `Rating is not null` → `Conflict(TeacherThreadAlreadyRated)`; `at = ToMicroseconds(ratedAt)`; `Rating = rating`; if `Status == Answered` → `Status = Closed; ClosedAt = at;`; `UpdatedBy = StudentId; UpdationDate = at;`. Braces on every `if`.

**§3 `Replies.cs` `Answer`** (the change):
```csharp
var isFinalReply = HasFollowUp();
Messages.Add(message);
Status = isFinalReply ? TeacherThreadStatus.Closed : TeacherThreadStatus.Answered;
if (isFinalReply) { ClosedAt = at; }
UpdatedBy = teacherId;
UpdationDate = at;
```
`HasFollowUp` must run before `Messages.Add`. Both reply handlers already `Include(x => x.Messages)`; add the WHY comment `// The final reply closes the thread; this reads Messages, so callers must load them.` above `Answer`. A reply on a `Closed` thread still fails in `EnsureCanReply` with `TEACHER_THREAD_NOT_AWAITING_REPLY`.

**§4 `TeacherThread.Sla.cs` `DueSlaStages`**: return `[]` when `Status != Open`. Otherwise `windowStart = SlaDueAt - replySla`. Add `FirstReminder` if `now >= windowStart + firstReminderAfter`, `SecondReminder` if `now >= windowStart + secondReminderAfter`, and `Breach` if `now >= SlaDueAt`, in that order. It is pure and does not stamp `UpdationDate`, because it does not mutate.

**§5 `TeacherThreadSlaEvent.Record`**: an append-only row; there are no mutators.

The state machine after this story: `Open` →(reply)→ `Answered` →(follow-up)→ `Open` →(final reply)→ `Closed`. `Answered` →(rate)→ `Closed`. `Closed`-and-unrated →(rate)→ `Closed` with a rating.

## API surface
| Method · route | Policy | Request | Response | Errors |
|---|---|---|---|---|
| POST `/api/teacher-threads/{threadId:guid}/follow-ups` (Name `FollowUpTeacherThread`) | `DefaultCodes.AskTeacherSubmit` | `[FromBody] FollowUpTeacherThreadRequest` → `new FollowUpTeacherThreadCommand(threadId, request.Text)` | `[ProducesResponseType<TeacherThreadResult>(200)]` | 422 text codes; 404 `TEACHER_THREAD_NOT_FOUND`; 409 `TEACHER_THREAD_FOLLOW_UP_NOT_ALLOWED` / `TEACHER_THREAD_MODIFIED_CONCURRENTLY`; 403 non-student; 401 |
| POST `/api/teacher-threads/{threadId:guid}/rating` (Name `RateTeacherThread`) | `AskTeacherSubmit` | `[FromBody] RateTeacherThreadRequest` → `new RateTeacherThreadCommand(threadId, request.Rating)` | `TeacherThreadResult` | 422 `TEACHER_THREAD_RATING_INVALID`; 404; 409 `TEACHER_THREAD_NOT_ANSWERED` / `ALREADY_RATED` / `MODIFIED_CONCURRENTLY` |
| GET `/api/teacher-inbox/reminders` (Name `GetTeacherInboxReminders`) | `DefaultCodes.AskTeacherReply` | — | `[ProducesResponseType<List<TeacherInboxReminderResult>>(200)]` | 401, 403 |
| Hub `/api/hubs/notifications` | `RequireAuthorization(DefaultCodes.AuthenticatedUser)` | JWT in the header or in `?access_token=` | server → client `teacherReplyReceived {threadId}` and `teacherThreadReminder {threadId, kind}` | 401 on negotiate |

`TeacherThreadResult` gains `rating`, `closedAt`, `canFollowUp` and `canRate`; `TeacherInboxThreadResult` gains `rating`.

## Observability
- `elmanhg.rules.yml`, a new group appended after `elmanhg-errors`:
```yaml
  - name: elmanhg-ask-teacher
    rules:
      - alert: AskTeacherSlaBreached
        expr: 'sum ((increase(elmanhg_ask_teacher_sla_events_total{elmanhg_kind="Breach"}[15m]) unless (elmanhg_ask_teacher_sla_events_total{elmanhg_kind="Breach"} unless elmanhg_ask_teacher_sla_events_total{elmanhg_kind="Breach"} offset 15m)) or (elmanhg_ask_teacher_sla_events_total{elmanhg_kind="Breach"} unless elmanhg_ask_teacher_sla_events_total{elmanhg_kind="Breach"} offset 15m)) > 0'
        labels:
          severity: warning
        annotations:
          summary: 'An Ask a Teacher question passed its reply deadline without a reply.'
          runbook: docs/observability.md#alerts
```

## Web
New files (all under `web/src/`):
| # | Path | Contract |
|---|------|----------|
| W1 | `shared/realtime/realtimeClient.ts` | `export const realtimeEventNames = ['teacherReplyReceived', 'teacherThreadReminder'] as const; export type RealtimeEventName = (typeof realtimeEventNames)[number]; export interface RealtimeClient { on(event: RealtimeEventName, handler: (payload: unknown) => void): () => void; start(): Promise<void>; stop(): Promise<void>; } export type RealtimeClientFactory = () => RealtimeClient; export const noopRealtimeClientFactory: RealtimeClientFactory` (a no-op client: `on` returns a no-op unsubscribe; `start`/`stop` resolve). |
| W2 | `shared/realtime/signalRRealtimeClient.ts` | `export function createSignalRRealtimeClient(): RealtimeClient`: `new HubConnectionBuilder().withUrl(resolveApiUrl('/api/hubs/notifications'), { accessTokenFactory: () => getAccessToken() ?? '' }).withAutomaticReconnect().configureLogging(LogLevel.None).build()`. `on` → `connection.on(event, handler)` and returns `() => { connection.off(event, handler); }`. `start` → `connection.start().catch(() => undefined)`, with the comment `// realtime is best effort: pages refetch when opened, so a failed connection only loses the live update`. `stop` → `connection.stop()`. |
| W3 | `shared/realtime/RealtimeContext.ts` | `export const RealtimeContext = createContext<RealtimeClientFactory>(noopRealtimeClientFactory);` |
| W4 | `shared/realtime/useRealtimeEvents.ts` | `export type RealtimeHandlers = Partial<Record<RealtimeEventName, (payload: unknown) => void>>; export function useRealtimeEvents(handlers: RealtimeHandlers): void`: `factory = use(RealtimeContext)`; `dispatch = useEffectEvent((event, payload) => { handlers[event]?.(payload); })`; `useEffect(() => { const client = factory(); const offs = realtimeEventNames.map((event) => client.on(event, (payload) => { dispatch(event, payload); })); void client.start(); return () => { offs.forEach((off) => { off(); }); void client.stop(); }; }, [factory]);` |
| W5 | `shared/realtime/realtimeEvents.ts` | `export const teacherReplyReceivedSchema = z.object({ threadId: z.uuid() }); export const teacherThreadReminderSchema = z.object({ threadId: z.uuid(), kind: z.enum(['FirstReminder', 'SecondReminder']) });` |
| W6 | `test/fakeRealtimeHub.ts` | `export class FakeRealtimeHub { readonly factory: RealtimeClientFactory; emit(event: RealtimeEventName, payload: unknown): void; }`. It keeps a `Map<RealtimeEventName, Set<handler>>`, shared by every client the factory creates; `stop` removes that client's handlers. |
| W7 | `features/askTeacher/hooks/useStudentRealtime.ts` | `useRealtimeEvents({ teacherReplyReceived: (payload) => { const parsed = teacherReplyReceivedSchema.safeParse(payload); if (!parsed.success) { return; } void queryClient.invalidateQueries({ queryKey: getGetMyTeacherThreadsQueryKey() }); void queryClient.invalidateQueries({ queryKey: getGetMyTeacherThreadQueryKey(parsed.data.threadId) }); toast.info(t('realtime.newReply')); } })`. |
| W8 | `features/askTeacher/hooks/useTeacherRealtime.ts` | The same for `teacherThreadReminder` with `teacherThreadReminderSchema`: invalidate `getGetTeacherInboxRemindersQueryKey()` and `getGetTeacherInboxQueryKey()`, then `toast.info(t('realtime.reminder'))`. |
| W9 | `features/askTeacher/components/StudentRealtimeListener.tsx` | `export function StudentRealtimeListener() { useStudentRealtime(); return null; }` |
| W10 | `features/askTeacher/components/TeacherRealtimeListener.tsx` | The same with `useTeacherRealtime`. |
| W11 | `features/askTeacher/schemas/followUpFormSchema.ts` | `z.object({ text: z.string().trim().min(1, { error: 'askTeacher:followUp.required' }) })`; `export interface FollowUpFormValues { text: string }`. |
| W12 | `features/askTeacher/hooks/useFollowUpThread.ts` | `useFollowUpTeacherThread`; `onSuccess(result)`: `setQueryData(getGetMyTeacherThreadQueryKey(threadId), result)`, invalidate `getGetMyTeacherThreadsQueryKey()`, `toast.success(t('followUp.sent'))`. `onError`: on a 409, invalidate the thread key. Returns `{ submit: (values) => mutation.mutateAsync({ threadId, data: { text: values.text } }) }` (mirrors `useReplyToThread`). |
| W13 | `features/askTeacher/hooks/useRateThread.ts` | Mirrors `useClaimThread`: `useRateTeacherThread`. On success: `setQueryData` on the thread key, invalidate the list, `toast.success(t('rating.sent'))`. On error: `toast.error(t([`common:errors.${code}`, 'common:errors.UNHANDLED_EXCEPTION']))`, then invalidate the thread. Returns `{ rate: (rating: number) => { mutation.mutate({ threadId, data: { rating } }); }, isPending }`. |
| W14 | `features/askTeacher/components/FollowUpForm.tsx` | A card (`flex flex-col gap-4 rounded-lg border border-border bg-surface p-4 shadow-1`) with `h2` `t('followUp.title')`, a `Form` with `serverErrorFields={{ TEACHER_THREAD_TEXT_REQUIRED: 'text', TEACHER_THREAD_TEXT_TOO_LONG: 'text' }}`, `TextAreaField name="text" label={t('followUp.label')} description={t('followUp.hint')}`, `FormRootError`, and `SubmitButton` `t('followUp.send')`. |
| W15 | `features/askTeacher/components/RatingPanel.tsx` | Props `{ threadId: string; closes: boolean }`. A card with `h2 id={useId()}` showing `t(closes ? 'rating.titleClose' : 'rating.title')`, then `div role="group" aria-labelledby` with 5 `Button variant="secondary"` (1..5), each `aria-label={t('rating.option', { rating })}`, showing the number and a Lucide `Star` (`size-4`, `aria-hidden`), and `disabled={isPending}`. |
| W16 | `features/askTeacher/components/ThreadRating.tsx` | Props `{ rating: number }`. A card with `<p className="text-caption text-text-muted">{t('rating.label')}</p>` and `<span role="img" aria-label={t('rating.value', { rating })}>` holding 5 Lucide `Star` icons (`size-5`, `aria-hidden`): filled `fill-current text-text` while `index < rating`, otherwise `text-text-muted`. The star keys are the constant positions `[1,2,3,4,5]` (a static list; a value, not an array index). |
| W17 | `features/askTeacher/components/StudentThreadActions.tsx` | Props `{ thread: TeacherThreadResult }`: `{thread.canFollowUp ? <FollowUpForm threadId={thread.id} /> : null}{thread.canRate ? <RatingPanel threadId={thread.id} closes={thread.status === 'Answered'} /> : null}`. |
| W18 | `features/askTeacher/components/InboxReminders.tsx` | `useGetTeacherInboxReminders()`. When `isPending`, `isError` or `data.length === 0` → `null` (Decision 17). Otherwise `<section aria-labelledby>`: a card with a header row (Lucide `BellRing` `size-5 text-warning aria-hidden`) and `h2` `t('reminders.title')`, `p` `t('reminders.intro')`, then a `ul` of `li` → `Link to="/teacher/thread/$threadId"` styled like `InboxListItem` with the question (`line-clamp-2 text-ui font-semibold`), the caption `t('reminders.meta', { subject, lesson, claim: isClaimedByMe ? t('inbox.claimedByMe') : t('inbox.unclaimed'), kind: t(`reminders.kind.${kind}`) })`, and `<ThreadStatusBadge thread={{ status: 'Open', isOverdue, slaDueAt }} />`. Key: `threadId`. |

Web strings (the `askTeacher` namespace; add both files):
| Key | ar | en |
|---|---|---|
| `followUp.title` | سؤال متابعة (مرة واحدة فقط) | Follow-up question (once only) |
| `followUp.label` | سؤال المتابعة | Your follow-up |
| `followUp.hint` | اكتب سؤال المتابعة | Write your follow-up question |
| `followUp.required` | اكتب سؤال المتابعة أولًا | Write your follow-up question first |
| `followUp.send` | إرسال المتابعة | Send follow-up |
| `followUp.sent` | تم إرسال سؤال المتابعة. | Your follow-up was sent. |
| `rating.titleClose` | قيّم الإجابة وأغلق السؤال | Rate the answer and close the question |
| `rating.title` | قيّم الإجابة | Rate the answer |
| `rating.option` | {rating} من 5 | {rating} of 5 |
| `rating.label` | التقييم: | Rating: |
| `rating.value` | {rating} من 5 | {rating} of 5 |
| `rating.sent` | شكرًا على تقييمك. | Thanks for rating the answer. |
| `reminders.title` | تذكيرات | Reminders |
| `reminders.intro` | اقترب موعد الرد على هذه الأسئلة: | These questions are close to their reply deadline: |
| `reminders.meta` | {subject} / {lesson} · {claim} · {kind} | {subject} / {lesson} · {claim} · {kind} |
| `reminders.kind.FirstReminder` | التذكير الأول | First reminder |
| `reminders.kind.SecondReminder` | التذكير الثاني | Second reminder |
| `realtime.newReply` | وصل رد من المعلّم على سؤالك. | A teacher replied to your question. |
| `realtime.reminder` | تذكير: سؤال طالب بانتظار ردك. | Reminder: a student's question is waiting for your reply. |

## Postman
- `AskTeacher` folder: no change.
- `TeacherInbox` folder: insert `Get reminders` (GET `{{baseUrl}}/api/teacher-inbox/reminders`, test `status is 200`, body is an array) right after `Get inbox`.
- A new folder `AskTeacherFollowUp`, placed right after `TeacherInbox`, that runs after the teacher's reply: `Send follow-up` (POST `{{baseUrl}}/api/teacher-threads/{{teacherThreadId}}/follow-ups`, body `{ "text": "Can you show the units?" }`, test `status is 200 or 409`, and if 200 `status === "Open"`); `Reply to follow-up` (POST `{{baseUrl}}/api/teacher-inbox/{{inboxThreadId}}/replies`, test 200 or 409, and if 200 `status === "Closed"`); `Rate thread` (POST `{{baseUrl}}/api/teacher-threads/{{teacherThreadId}}/rating`, body `{ "rating": 5 }`, test 200 or 409, and if 200 `rating === 5`). Use the same auth mechanics as the existing requests in those folders.

## Docs
| Doc | Change |
|---|---|
| `docs/ask-teacher.md` | Intro: replace "Follow-up, rating, closing, reminders and realtime push arrive with #97." with a sentence saying #97 adds them. Model: add `ClosedAt`/`closed_at?` and `Rating`/`rating?` rows (with the `CK_TeacherThreads_Rating` check), the migration `AddTeacherThreadSlaAndRatings`, the index `(Status, SlaDueAt)`, and a new `TeacherThreadSlaEvent` table (fields, the unique `(ThreadId, Kind, SlaDueAt)`, append-only, never written by requests). Status machine: the final state machine (Domain behaviour). A new "Follow-up and rating" section (Decisions 8–12, the codes, "a follow-up does not count"). Rewrite "SLA": no pause; the new window per follow-up; the 12/20 h reminders relative to the window; recipients; the breach record + metric + alert; catch-up (Decision 7); the worker `ask-teacher-sla`. "Student notification": the realtime `teacherReplyReceived` over `/api/hubs/notifications` (the token in the query on the hub path only). API tables: the 3 new routes + the hub; the new result fields. Web: the follow-up card, the rating card, the stars, the reminders card, the toasts. Options: the 6 new keys. Remove the #97 bullet from "For later stories". Known limits: the in-memory hub on one instance; best-effort pushes; the reminder timing reads the current `AskTeacherReplySlaHours`; the SLA worker has no leasing, like the transcription worker. |
| `docs/PRD.md` | §12.1 step 3: append "A follow-up starts a new 24-hour window; the clock never pauses." §15: after `TeacherVoiceDraft`, add `TeacherThreadSlaEvent(id, thread_id, kind[FirstReminder|SecondReminder|Breach], sla_due_at, teacher_id?, occurred_at)  -- one per SLA window and stage; reminders and breach record (docs/ask-teacher.md)`. §17 rule 11: "24h SLA from submission (a follow-up opens a new 24h window)". |
| `docs/observability.md` | §4 metrics table: a row for `elmanhg.ask_teacher.sla_events` · counter · {event} · `elmanhg.kind` (`FirstReminder`, `SecondReminder`, `Breach`) · `ProcessTeacherThreadSlaHandler` · `elmanhg_ask_teacher_sla_events_total`. Add `ask-teacher-sla` to the list of background jobs. §8: a row for `AskTeacherSlaBreached` · any `Breach` in 15 m · warning · "Open the teacher inbox as admin, or contact the subject's teachers; `ask-teacher-sla` Worker logs show the thread id". |
| `docs/deployment.md` | The Ask a Teacher table: rows for the 6 new keys with their ranges. One line: "Caddy proxies the SignalR hub `/api/hubs/notifications` (WebSockets) with the existing `/api/*` rule; the access log already redacts the `access_token` query value." |
| `docs/claude-design-prompt.md` | Line 139: replace "(follow-up and 1 to 5 rating arrive with teacher replies)" with: the answered-thread cards «سؤال متابعة (مرة واحدة فقط)» with «إرسال المتابعة», and «قيّم الإجابة وأغلق السؤال» (1–5 stars; «قيّم الإجابة» after the teacher's final reply closes the thread), the saved rating «التقييم:» with stars, and a live «وصل رد من المعلّم على سؤالك.» toast. Line 146: add the «تذكيرات» card above the tabs (questions with a 12 h or 20 h reminder: «التذكير الأول / الثاني», claim, SLA badge), the live «تذكير: سؤال طالب بانتظار ردك.» toast, and the student's rating on the thread. |
| `docs/prototype.md` | Item 7: append "The product closes the thread after the teacher's reply to the follow-up, reminds teachers in-app at 12 h and 20 h («تذكيرات» on the inbox and a live toast), and pushes new replies to the student live." |
| `docs/subscriptions.md` | Line 173: "A follow-up (#95)" → "A follow-up (#97)". |

## Test plan
.NET (xUnit v3, FluentAssertions as pinned, NSubstitute). Every handler success test asserts `SaveChangesAsync` `Received(1)`; every throwing test asserts the type, the code and `DidNotReceive()`.

**Builder (`TeacherThreadBuilder`, modify):**
- `FollowedUp()`: after the answer (it requires `AnsweredBy`), `thread.FollowUp("Can you show the units?", _submittedAt.AddHours(2), TimeSpan.FromHours(24))`.
- `FinalReplied()`: implies `FollowedUp()`, then `thread.Reply(teacherId, "Newtons.", _submittedAt.AddHours(3))`.
- `Rated(int rating)`: applied last, `thread.Rate(rating, _submittedAt.AddHours(4))`.

| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| T1 | `Domain/TeacherThreads/TeacherThreadFollowUpTests` | `FollowUp_AnsweredThread_AddsStudentMessageReopensAndResetsSla` | Status Open; `SlaDueAt == at + 24h` (microsecond truncation); 3 messages, the last from the student; `UpdationDate == at` |
| T2 | same | `FollowUp_OpenThread_ThrowsFollowUpNotAllowed` | `ConflictCoreException` `TEACHER_THREAD_FOLLOW_UP_NOT_ALLOWED` |
| T3 | same | `FollowUp_ClosedThread_ThrowsFollowUpNotAllowed` | the same code (a `FinalReplied` thread) |
| T4 | same | `FollowUp_AlreadyFollowedUp_ThrowsFollowUpNotAllowed` | the thread after a follow-up (Open) → the code |
| T5 | same | `FollowUp_BlankText_ThrowsTextRequired` | `BusinessRuleViolationCoreException` `TEACHER_MESSAGE_TEXT_REQUIRED`; the message count is unchanged |
| T6 | same | `Reply_AfterFollowUp_ClosesThreadAndSetsClosedAt` | Closed; `ClosedAt == reply time` |
| T7 | same | `ReplyWithVoice_AfterFollowUp_ClosesThread` | Closed; `ClosedAt` set |
| T8 | same | `Reply_FirstReply_LeavesThreadAnsweredWithoutClosedAt` | Answered; `ClosedAt` null |
| T9 | same | `Reply_ClosedThread_ThrowsNotAwaitingReply` | `TEACHER_THREAD_NOT_AWAITING_REPLY` |
| T10 | `Domain/TeacherThreads/TeacherThreadRatingTests` | `Rate_AnsweredThread_StoresRatingAndCloses` | Rating 4; Closed; `ClosedAt == at` |
| T11 | same | `Rate_ClosedAfterFinalReply_StoresRatingAndKeepsClosedAt` | Rating stored; `ClosedAt` unchanged |
| T12 | same | `Rate_OpenThread_ThrowsNotAnswered` | `TEACHER_THREAD_NOT_ANSWERED` |
| T13 | same | `Rate_AlreadyRated_ThrowsAlreadyRated` | `TEACHER_THREAD_ALREADY_RATED`; the rating is unchanged |
| T14 | same | `Rate_OutOfRange_Throws` `[Theory] 0, 6` | `ArgumentOutOfRangeException` |
| T15 | same | `CanFollowUpAndCanBeRated_ByState_MatchRules` `[Theory]` (Open → false/false; Answered → true/true; FinalReplied → false/true; Rated → false/false) | both flags |
| T16 | `Domain/TeacherThreads/TeacherThreadSlaTests` | `DueSlaStages_BeforeFirstReminder_ReturnsNone` | empty at submitted + 11h59m |
| T17 | same | `DueSlaStages_AtFirstReminder_ReturnsFirst` | `[FirstReminder]` at +12h |
| T18 | same | `DueSlaStages_AtSecondReminder_ReturnsFirstAndSecond` | at +20h |
| T19 | same | `DueSlaStages_AtDueTime_ReturnsAllStages` | at +24h, all three in order |
| T20 | same | `DueSlaStages_AnsweredThread_ReturnsNone` | empty at +30h |
| T21 | same | `DueSlaStages_AfterFollowUp_MeasuresFromNewWindow` | follow-up at +2h: `[]` at +13h, `[FirstReminder]` at +14h |
| T22 | `Domain/TeacherThreads/TeacherThreadSlaEventTests` | `Record_SetsFieldsAndTruncatesOccurredAtToMicroseconds` | every field; ticks truncated |
| T23 | `Application/Features/TeacherThreads/FollowUpTeacherThread/FollowUpTeacherThreadHandlerTests` | `Handle_AnsweredThread_ReopensAndReturnsResult` | result Status Open, `CanFollowUp` false, `CanRate` false, `SlaDueAt == now+24h`, 3 messages; save `Received(1)` |
| T24 | same | `Handle_Unauthenticated_ThrowsUnauthorized` | 401 code; no save |
| T25 | same | `Handle_ThreadNotFound_ThrowsNotFound` | `TEACHER_THREAD_NOT_FOUND`; no save |
| T26 | same | `Handle_OpenThread_ThrowsFollowUpNotAllowed` | 409 code; no save |
| T27 | `.../FollowUpTeacherThread/FollowUpTeacherThreadValidatorTests` | `Validate_ValidText_Passes` | valid |
| T28 | same | `Validate_BlankText_FailsTextRequired` | `TEACHER_THREAD_TEXT_REQUIRED` |
| T29 | same | `Validate_TooLong_FailsTextTooLong` | `TEACHER_THREAD_TEXT_TOO_LONG` (max 10 via options) |
| T30 | `.../RateTeacherThread/RateTeacherThreadHandlerTests` | `Handle_AnsweredThread_ClosesAndReturnsRating` | result `Rating` 5, Closed, `ClosedAt`, `CanRate` false; save `Received(1)` |
| T31 | same | `Handle_Unauthenticated_ThrowsUnauthorized` | 401; no save |
| T32 | same | `Handle_ThreadNotFound_ThrowsNotFound` | 404 code; no save |
| T33 | same | `Handle_AlreadyRated_ThrowsAlreadyRated` | 409 code; no save |
| T34 | `.../RateTeacherThread/RateTeacherThreadValidatorTests` | `Validate_InRange_Passes` `[Theory] 1, 5` | valid |
| T35 | same | `Validate_OutOfRange_FailsRatingInvalid` `[Theory] 0, 6` | `TEACHER_THREAD_RATING_INVALID` |
| T36 | `.../TeacherThreads/GetDueSlaThreadIds/GetDueSlaThreadIdsHandlerTests` | `Handle_UsesClockOptionsAndExcludedIds_ReturnsRepositoryIds` | the repository is called with now, 24h, 12h, 20h, the excluded ids and a batch of 50; the ids are returned |
| T37 | `ReplyToTeacherThreadHandlerTests` (modify) | `Handle_Reply_NotifiesStudentAfterSaving` | `NotifyReplyAsync(studentId, threadId)` `Received(1)`, called after `SaveChangesAsync` (`Received.InOrder`) |
| T38 | same | `Handle_NotClaimed_DoesNotNotifyStudent` | the notifier `DidNotReceive` |
| T39 | `SendVoiceReplyHandlerTests` (modify) | `Handle_VoiceReply_NotifiesStudentAfterSaving` | `Received.InOrder` save then notify |
| T40 | `TeacherInboxResultGeneratorTests` (modify) | `GenerateThread_RatedThread_ReturnsRating` | `Rating` 4 |
| T41 | same | `GenerateReminder_UnclaimedOverdueThread_MapsFields` | every field, `IsClaimedByMe` false, `IsOverdue` true |
| T42 | `ElmanhgMetricsTests` (modify) | `RecordAskTeacherSlaEvent_Breach_TagsKind` | `MetricCollector` on `elmanhg.ask_teacher.sla_events` value 1, tag `elmanhg.kind=Breach` |
| T43 | `Application/Features/TeacherThreads/Shared/TeacherThreadResultGeneratorTests` (new) | `Generate_AnsweredThread_OffersFollowUpAndRating` | `CanFollowUp`, `CanRate` true; `Rating`, `ClosedAt` null |
| T44 | same | `Generate_RatedThread_ReturnsRatingWithoutActions` | Rating 3, `ClosedAt` set, both flags false |
| T45 | `.../TeacherThreads/ProcessTeacherThreadSla/ProcessTeacherThreadSlaHandlerTests` | `Handle_FirstReminderDueOnClaimedThread_RecordsEventAndNotifiesClaimer` | one `AddAsync` with Kind First, `SlaDueAt`, TeacherId; save `Received(1)`; `NotifyReminderAsync([teacher], id, FirstReminder)` |
| T46 | same | `Handle_FirstReminderDueOnUnclaimedThread_NotifiesAssignedTeachers` | recipients = the two assigned teacher ids |
| T47 | same | `Handle_UnclaimedThreadWithoutTeachers_RecordsEventWithoutNotifying` | save `Received(1)`; the notifier `DidNotReceive` |
| T48 | same | `Handle_OnlyBreachMissing_RecordsBreachCountsMetricAndDoesNotNotify` | First and Second already recorded; Add Breach; the metric `Breach`=1; the notifier `DidNotReceive` |
| T49 | same | `Handle_AllStagesMissing_RecordsThreeEventsAndNotifiesSecondReminderOnce` | 3 `AddAsync`; one save; `NotifyReminderAsync(..., SecondReminder)` `Received(1)` |
| T50 | same | `Handle_StagesAlreadyRecorded_DoesNothing` | no Add, no save, no notify |
| T51 | same | `Handle_AnsweredThread_DoesNothing` | no save |
| T52 | same | `Handle_ThreadMissing_DoesNothing` | no save |
| T53 | `.../TeacherInbox/GetTeacherInboxReminders/GetTeacherInboxRemindersHandlerTests` | `Handle_Teacher_PassesAssignedSubjectsAndReturnsLatestReminderKind` | the repository gets `[subjectId]`, the caller and 20; the result Kind is `SecondReminder` when both are recorded for the current window, ignoring an older window's event |
| T54 | same | `Handle_Admin_PassesNullSubjects` | the repository gets `subjectIds == null` |
| T55 | same | `Handle_Unauthenticated_ThrowsUnauthorized` | 401 |
| T56 | `Api/Workers/TeacherThreadSlaWorkerTests` | `Sweep_DueThreads_ProcessesEach` | `ProcessTeacherThreadSlaCommand` sent for each id |
| T57 | same | `Sweep_ProcessingFails_LogsWarningAndRecordsPartiallyFailedRun` | a Warning is logged; the `elmanhg.job.runs` outcome is `PartiallyFailed` |
| T58 | same | `Sweep_FailedIds_AreSkippedUntilTheBacklogEnds` | mirrors the SubscriptionLapse test (the `ExcludedIds` sequence) |
| T59 | same | `Execute_Disabled_EndsWithoutSweeping` | the query is never sent |
| T60 | same | `Sweep_ListingFails_RecordsFailedRun` | outcome `Failed` |
| T61 | `Api/Realtime/HubAccessTokenTests` | `OnMessageReceived_HubPathWithAccessToken_SetsToken` | `context.Token == "abc"` |
| T62 | same | `OnMessageReceived_OtherPath_LeavesTokenUnset` | `/api/teacher-threads?access_token=abc` → null |
| T63 | same | `OnMessageReceived_HubPathWithoutToken_LeavesTokenUnset` | null |
| T64 | `Api/Realtime/SignalRTeacherThreadNotifierTests` | `NotifyReplyAsync_SendsReplyEventToStudent` | substitute `IHubContext`; `Clients.User(studentId.ToString())` `SendCoreAsync("teacherReplyReceived", [TeacherReplyReceivedMessage(threadId)])` |
| T65 | same | `NotifyReminderAsync_SendsReminderEventToTeachers` | `Clients.Users([ids])` `SendCoreAsync("teacherThreadReminder", [message with Kind])` |
| T66 | `Integration/TeacherThreads/FollowUpTeacherThreadEndpointTests` | `FollowUp_AnsweredThread_ReopensWithNewDeadlineAndKeepsQuota` | 200, status Open, `canFollowUp` false; DB: 3 messages and `SlaDueAt` later than before; `/api/subscriptions/usage` `askTeacherQuestionsUsedThisMonth` unchanged |
| T67 | same | `FollowUp_OpenThread_Returns409` | 409 `TEACHER_THREAD_FOLLOW_UP_NOT_ALLOWED`; DB unchanged |
| T68 | same | `FollowUp_OtherStudentsThread_Returns404` | 404 `TEACHER_THREAD_NOT_FOUND` |
| T69 | same | `FollowUp_BlankText_Returns422` | 422 `TEACHER_THREAD_TEXT_REQUIRED` |
| T69b | same | `FollowUp_Teacher_Returns403` | 403 |
| T69c | same | `FollowUp_Anonymous_Returns401` | 401 |
| T70 | promtool `elmanhg.rules.test.yml` | `first ask-teacher breach of a new series fires` | series `elmanhg_ask_teacher_sla_events_total{elmanhg_kind="Breach"}` values `'_x2 1 1 1'`, eval 4m → `AskTeacherSlaBreached` warning with the summary and runbook |
| T71 | promtool | `ask-teacher reminders alone stay quiet` | a `FirstReminder` series `'_x2 1 2 3'`, eval 4m → `exp_alerts: []` |
| T72 | `Integration/TeacherThreads/RateTeacherThreadEndpointTests` | `Rate_AnsweredThread_ClosesAndStoresRating` | 200 `rating` 5, status Closed; DB `Rating` 5, `ClosedAt` set; the teacher's `GET /api/teacher-inbox/{id}` shows `rating` 5 |
| T73 | same | `Rate_AlreadyRated_Returns409` | `TEACHER_THREAD_ALREADY_RATED` |
| T74 | same | `Rate_OutOfRange_Returns422` | `TEACHER_THREAD_RATING_INVALID`; DB `Rating` null |
| T75 | same | `Rate_OtherStudentsThread_Returns404` | 404 |
| T76 | same | `Rate_Teacher_Returns403` | 403 |
| T77 | `Integration/TeacherInbox/FinalReplyEndpointTests` | `Reply_AfterFollowUp_ClosesThreadAndOffersOnlyRating` | the reply gives 200 `status` Closed; DB `ClosedAt` set; the student's GET shows `canFollowUp` false, `canRate` true |
| T78 | same | `Reply_ToClosedThread_Returns409NotAwaitingReply` | 409 |
| T79 | `Integration/TeacherInbox/TeacherInboxRemindersEndpointTests` | `Reminders_RecordedReminder_ListsThreadForSubjectTeacher` | events seeded through `AppDbContext` (`TeacherThreadSlaEvent.Record`); 200 with one item, `kind` `FirstReminder`, `threadId` |
| T80 | same | `Reminders_OtherSubjectTeacher_ReturnsEmpty` | 200 `[]` |
| T81 | same | `Reminders_AnsweredThread_IsNotListed` | 200 `[]` |
| T82 | same | `Reminders_Student_Returns403` | 403 |
| T83 | same | `Reminders_Anonymous_Returns401` | 401 |
| T84 | `Integration/TeacherThreads/TeacherThreadSlaSweepTests` | `Process_OverdueThread_RecordsEachStageOnce` | a thread seeded with `SubmittedAt = UtcNow − 25h` (arrange); `ProcessTeacherThreadSlaCommand` sent twice through the mediator → exactly 3 event rows (First, Second, Breach) |
| T85 | same | `GetSlaDueIds_ListsDueThreadsAndSkipsRecordedAndAnswered` | the repository is called through a scope with an explicit `now = seededSubmittedAt + 13h` and a limit of 100000: it contains the due thread, not the one whose First is already recorded, and not the answered one |
| T86 | `Integration/Realtime/NotificationsHubTests` | `Hub_StudentConnected_ReceivesTeacherReply` | a `HubConnection` (LongPolling, `HttpMessageHandlerFactory = _ => factory.Server.CreateHandler()`, `AccessTokenProvider` = the student's token); the teacher POSTs a reply; `teacherReplyReceived.threadId == thread.Id` within 10 s (`WaitAsync`) |
| T87 | same | `Hub_Anonymous_IsRejected` | `StartAsync` throws `HttpRequestException` with `StatusCode` 401 |
| T88 | `AppDbContextTests` (modify) | `Migrate_FreshDatabase_LeavesNoPendingMigrations` | the list includes `_AddTeacherThreadSlaAndRatings` |

Web (Vitest + RTL + MSW; `userEvent.setup({ advanceTimers })` where fake timers are used, as in the existing TeacherThreadPage tests):
| # | File | `it(...)` | Asserts |
|---|------|-----------|---------|
| W-T1 | `features/askTeacher/schemas/followUpFormSchema.test.ts` | `accepts a follow-up with text` | success |
| W-T2 | same | `rejects a blank follow-up` | the error `askTeacher:followUp.required` |
| W-T3 | `features/askTeacher/pages/TeacherThreadPage.followUp.test.tsx` | `shows the follow-up form and the rating for an answered thread` | heading "Follow-up question (once only)", heading "Rate the answer and close the question", 5 buttons "1 of 5" … "5 of 5" |
| W-T4 | same | `sends a follow-up and shows the thread awaiting a reply` | MSW follow-up handler → Open thread with 3 messages; toast "Your follow-up was sent."; the follow-up text is visible; the form and the rating are gone; the badge reads "Awaiting reply" |
| W-T5 | same | `shows the required error without sending when the follow-up is blank` | "Write your follow-up question first"; no request (MSW `onUnhandledRequest` would fail) |
| W-T6 | same | `shows the server error inline when the follow-up is too long` | 422 `TEACHER_THREAD_TEXT_TOO_LONG` → "The question is too long." on the field |
| W-T7 | same | `rates the answer and shows the closed question with its rating` | click "4 of 5" → toast "Thanks for rating the answer."; badge "Closed"; `img` "4 of 5" after "Rating:" |
| W-T8 | same | `offers only the rating after the final reply` | Closed with `canRate`: heading "Rate the answer"; no follow-up heading |
| W-T9 | same | `shows a toast when rating fails with a conflict` | 409 `TEACHER_THREAD_ALREADY_RATED` → toast "You have already rated this answer." |
| W-T10 | same | `hides the follow-up and rating while awaiting a reply` | Open thread → neither heading |
| W-T11 | same | `renders the follow-up card in Arabic` | lng `ar`: «سؤال متابعة (مرة واحدة فقط)» and `dir="rtl"` on `document.documentElement` |
| W-T12 | `features/askTeacher/pages/InboxThreadPage.rating.test.tsx` | `shows the student's rating on a closed thread` | `inboxThread({ status: 'Closed', rating: 5 })` → "Rating:" and `img` "5 of 5"; the note "This question is closed." |
| W-T13 | same | `shows no rating before the student rates` | no "Rating:" text |
| W-T14 | `features/askTeacher/pages/TeacherInboxPage.reminders.test.tsx` | `lists reminded questions above the inbox with their deadline badge` | heading "Reminders", the intro, a link with the question text, "Physics / Newton's laws · Unclaimed · First reminder", the badge "Awaiting reply · …" |
| W-T15 | same | `links a reminder to its thread` | link `href` `/teacher/thread/{threadId}` |
| W-T16 | same | `shows no reminders card when there are none` | the inbox renders; no heading "Reminders" |
| W-T17 | same | `keeps the inbox usable when reminders fail to load` | reminders 500 → no "Reminders" heading, no retry button from it; the inbox items are visible |
| W-T18 | same | `renders the reminders card in Arabic` | «تذكيرات», «التذكير الثاني» |
| W-T19 | same | `has no axe violations with reminders` | `axe(container)` has no violations |
| W-T20 | `features/askTeacher/components/StudentRealtimeListener.test.tsx` | `refreshes the list and shows a toast when a teacher reply arrives` | `renderApp('/student/ask')`; the list first has no unread; then swap the MSW list to unread; `act(() => realtime.emit('teacherReplyReceived', { threadId }))` → "A teacher replied to your question." and "New reply" visible |
| W-T21 | same | `ignores a malformed reply event` | emit `{}` → no toast text |
| W-T22 | `features/askTeacher/components/TeacherRealtimeListener.test.tsx` | `refreshes the reminders and shows a toast when a reminder arrives` | `renderApp('/teacher/inbox')`, reminders `[]` then one → after emit: toast "Reminder: a student's question is waiting for your reply." and the heading "Reminders" |
| W-T23 | same | `ignores a reminder with an unknown kind` | emit `{ threadId, kind: 'Breach' }` → no toast |
| W-T24 | `shared/realtime/useRealtimeEvents.test.tsx` | `delivers events to the mounted component` | a probe component renders the count of received events; emit twice → "2" |
| W-T25 | same | `stops delivering after unmount` | unmount, emit, re-render a fresh probe → "0" (the old handler is detached; assert through the new probe text) |

## Definition of done
- [ ] All 4 story sub-tasks are delivered: the SLA worker with 12 h and 20 h reminders and the breach alert; the one follow-up and one reply, then closing; the 1–5 rating; SignalR reply push.
- [ ] `TeacherThread` has `ClosedAt` and `Rating` (with the check constraint), `FollowUp`, `Rate`, `DueSlaStages` and final-reply closing, exactly as in Domain behaviour §1–§4.
- [ ] `TeacherThreadSlaEvents` exists with a unique `(ThreadId, Kind, SlaDueAt)`, is in the soft-delete filter, and is written only by `ProcessTeacherThreadSlaHandler`.
- [ ] The migration `AddTeacherThreadSlaAndRatings` contains no Drop or Rename; `AppDbContextTests` lists it.
- [ ] `TeacherThreadSlaWorker` (job `ask-teacher-sla`) mirrors `SubscriptionLapseWorker`, is registered in `Program.cs`, and is disabled in `ApiFactory`.
- [ ] The 3 endpoints have the policies in API surface; the hub requires `AuthenticatedUser`; `EndpointAuthorizationTests` is green.
- [ ] The reply and voice-reply handlers notify the student only after `SaveChangesAsync`.
- [ ] 4 new error codes are in `ErrorCodes`, both resx files and web `shared/i18n`, with the table's text.
- [ ] 6 new `AskTeacherOptions` keys with ranges are in `appsettings.example.json`, `deploy/api.env.example` and `docs/deployment.md`.
- [ ] The metric `elmanhg.ask_teacher.sla_events` (tag `elmanhg.kind`), the alert `AskTeacherSlaBreached` and 2 promtool cases.
- [ ] `api/openapi/v1.json` and the Orval output are regenerated with no drift; the Postman collection has the 4 new requests in state order.
- [ ] Web: follow-up, rating and read-only rating on the student page; rating on the teacher page; the reminders card; the student and teacher live toasts; no connection outside the shells; tests use `FakeRealtimeHub`.
- [ ] Every string is in `ar` and `en`; no literal colours or sizes; logical properties only; no red on the rating or reminders (warning icon only, design rule 1).
- [ ] Every test T1–T88 (with T69b and T69c) and W-T1–W-T25 exists and passes; the existing tests are changed only as listed (builder, two handler tests, generator, metrics, `AppDbContextTests`, web fixtures, MSW server, `renderWithProviders`).
- [ ] `dotnet test api/ -c Release` is green with `appsettings.json` moved aside; `dotnet format` is clean on changed files.
- [ ] Web: `tsc -b`, `eslint --max-warnings=0`, `prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto`, `vitest run --coverage` (features ≥ 80/70) and `gen:api` with no diff.
- [ ] Docs updated as in Docs: ask-teacher, PRD §12.1, §15 and rule 11, observability, deployment, design-prompt lines 139 and 146, prototype item 7, subscriptions line 173.
- [ ] Product questions recorded for the dev (Out a–d); no money movement or reassignment code exists.
