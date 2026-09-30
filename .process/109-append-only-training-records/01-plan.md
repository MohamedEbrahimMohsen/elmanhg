# Plan — [E12.S1] Append-only training records (#109)

## Goal
From now on the platform keeps a text-only training copy of three sources: every student quiz or exam attempt, every Avatar exchange, and every Ask a Teacher thread when it closes (and again if it is rated after closing). Each copy is keyed by a keyed hash of the student id, carries subject, unit, lesson and question references, and goes into its own append-only table. PostgreSQL triggers reject UPDATE, DELETE and TRUNCATE on those tables. A copy is written in the same transaction as its source row. `docs/training-data.md` holds the retention and privacy review checklist. #110 (JSONL export) reads these tables. No endpoint or UI changes.

## Scope
**In:**
- 3 tables: `AttemptTrainingRecords`, `AvatarTrainingRecords`, `TeacherThreadTrainingRecords`. Each has an HMAC `StudentHash` and append-only triggers.
- 4 new domain events: `AttemptsRecorded`, `AvatarExchangeRecorded`, `TeacherThreadClosed` and `TeacherThreadRatedAfterClose`. They are raised from the existing aggregate methods.
- 3 MediatR notification handlers that write the records.
- `IStudentIdHasher` + `HmacStudentIdHasher` + `TrainingData` options, with a startup validator.
- 1 vendored Core fix: `CoreDbContext` materialises entities before publishing, so handlers can add entities during `SaveChangesAsync`.
- `IQuestionRepository.GetPlacementsAsync`.
- Docs: new `docs/training-data.md` (including the checklist); PRD §13 and §15; `docs/avatar.md`, `docs/sessions.md`, `docs/ask-teacher.md`, `docs/deployment.md`; `deploy/api.env.example`; `appsettings.example.json`.

**Out:**
- The JSONL export, PII stripping of free text, and the admin export page (#110).
- AI-grading or teacher-override training records. E17 "Review queue and override" owns "Training record for every override". Attempt records already carry `GradedBy` and `Grade`, so AI-graded attempts (#118) are captured automatically.
- Backfilling records for rows that existed before this migration.
- Any endpoint, web or ai change.

**Deferred:**
- **Retention period and student erasure for operational and training data.** This is a pending dev decision (#215): "kept indefinitely in v1". The checklist records it as open. The orchestrator adds a comment to #215 linking `docs/training-data.md` §Checklist, and opens no new issue. No purge job and no erasure path are built, because both need the human decision.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | How is the student id anonymised? | `StudentHash` = lower-case hex HMAC-SHA256, keyed by `TrainingData:StudentIdHashKey`, over `studentId.ToString("D")` (64 chars) | Keyed pseudonymisation: without the server secret, a leaked export cannot be joined to user ids. It also reuses Morabh's `HmacOtpHasher` shape. A plain SHA-256 of a known GUID can be re-linked by anyone who holds the Users table. |
| 2 | What if the key is missing? | Startup validator: an empty or short key fails in **Production and Staging**. A non-empty key shorter than 32 characters fails in every environment. The development placeholder key fails in Production and Staging. In Development and Testing, an empty key falls back to `TrainingDataOptions.DevelopmentStudentIdHashKey`. | Follows the #58 lesson: capture is always on and a misconfigured production host fails loudly at start. CI and local dev run with no secrets. |
| 3 | Can the key be rotated? | No. The docs say "never rotate": rotation splits every student's history into two pseudonyms. | Longitudinal consistency is needed for calibration. |
| 4 | What triggers the writes? | Domain events raised by the aggregates. Handlers in `Application/Events/TrainingRecords` add the rows. `CoreDbContext.SaveChangesAsync` publishes events **before** `base.SaveChangesAsync`, so the records commit in the same transaction as the source. | This is what the story asks for ("event handlers"). It is atomic, needs no outbox, and a failed source save writes no record. |
| 5 | Should `CoreDbContext` change? | Yes. Materialise `ChangeTracker.Entries<Entity>()` with `.ToList()` before publishing, and pass `cancellationToken` to `Publish`. | Today the change tracker is enumerated lazily while handlers run. A handler that adds an entity mutates the collection being enumerated. Morabh's `Core/Core.EntityFrameworkCore/Context/CoreDbContext.cs` has the same code. |
| 6 | Which attempts are captured? | Every attempt created through `Attempt.Create`, which covers quiz `RecordAttempt` and exam `SubmitExam`, including the auto-submit worker. Test-mode sessions (Admin) are skipped in the handler. | `docs/sessions.md` §Test mode: "training-data export (E12) exclude[s] test-mode sessions". Admin actions are not student data. |
| 7 | How do attempt records get subject, unit and lesson? | New `IQuestionRepository.GetPlacementsAsync(ids)` does one no-tracking join of Questions and Lessons per event, with `IgnoreQueryFilters` on both. | PRD §13 requires the references. `Attempt` holds only `QuestionId`. Questions never change lesson and lessons never change unit, so a copy taken at write time is exact. |
| 8 | Avatar granularity | One record per exchange: the student message, the assistant reply, the model, the prompt version and the context JSON the model read. Rows carry `ConversationId` and `StudentMessagePosition`, so #110 can rebuild full conversations. | Matches the sub-task ("on avatar message"). `RecordExchange` is the single write point. |
| 9 | When is a teacher-thread record written, given that the rating can arrive after close? | A full snapshot is written when the thread becomes `Closed`, through either the final reply or rating an Answered thread (trigger `Closed`). A second full snapshot is written when a Closed thread is later rated (trigger `RatedAfterClose`). There is a unique index on `(ThreadId, Trigger)`. #110 takes the latest snapshot per thread. | PRD §12.2 wants the rating in the record, and append-only forbids updating the first row. |
| 10 | Is the thread record text-only? | `Messages` is a jsonb array of `{author, kind, text, hasImage, sentAt}`. A voice reply contributes its final transcript (`Text`). No audio URL, image URL, sender id or teacher id is stored. `Context` is the thread's context JSON copied as is: curriculum ids and names, the question stem and `attemptId`. | PRD §12.2 "training record is text only". The teacher is staff, so the conservative default drops the teacher's identity. |
| 11 | Should training tables have foreign keys? | None: not to Users, content or source rows. Only plain indexed Guid columns. | Avoids re-identification joins that the schema itself enforces. A future erasure of operational rows (#215) is never blocked by training tables. |
| 12 | Which source ids are stored? | `AttemptId`, `ConversationId`, `StudentMessageId`, `AssistantMessageId` and `ThreadId`. There is no `SessionId`. | They serve as idempotency and unique keys and let #110 group conversations. The checklist records that #110 must not export raw source ids. |
| 13 | What about PII in free text (student question or message)? | It is stored verbatim, as it already is in the operational tables. Stripping happens at export (#110, PRD §13 "Exports … with PII stripped"). This is recorded in the checklist. | Capture and export have separate responsibilities. The export story owns stripping. |
| 14 | How is uniqueness enforced? | Unique `AttemptTrainingRecords.AttemptId`; unique `AvatarTrainingRecords.StudentMessageId`; unique `TeacherThreadTrainingRecords(ThreadId, Trigger)`. | Each source event writes exactly once, and the database enforces it. |
| 15 | Which time columns are used? | `OccurredAt` is the source time: attempt `CreatedAt`, reply `CreatedAt`, `ClosedAt` or the rating time. `RecordedAt` = `TimeProvider.GetUtcNow()`. Each table is indexed on `OccurredAt` and on `StudentHash`. | #110 exports by date range on `OccurredAt`. The `StudentHash` index supports per-pseudonym lookups (#215 erasure). |
| 16 | Do training records use a file split for `AppDbContext`? | Yes. Make `AppDbContext` `partial` and add `AppDbContext.TrainingData.cs`. | The main file is already 582 lines, and the skill's long-file rule applies. |
| 17 | Error codes | None are added. Violated invariants throw `InvalidOperationException`. | These are programmer errors, not client-facing. |
| 18 | Privacy stance while #215 is open | The conservative default: hashed student id, no names, phone, email or teacher id, text only, no retention purge. This is written in `docs/training-data.md` as "dev decision pending (#215)". | Instruction: do not invent a retention policy. |

## Morabh reuse
| Piece | Source |
|---|---|
| Notification-handler shape (`Application/Events/<Name>/…Handler : INotificationHandler<T>`) | Morabh `Morabh.Application/Events/OrderWorkflowNotificationEvent/OrderWorkflowNotificationEventHandler.cs` |
| HMAC hasher (`HMACSHA256` keyed by an options secret) | Morabh `Core/Core.OTP/OtpHasher/HmacOtpHasher.cs` (vendored at `api/core-libraries/Core.OTP/OtpHasher/HmacOtpHasher.cs`) |
| Domain-event dispatch in `SaveChangesAsync` (the fix applies to it) | Morabh `Core/Core.EntityFrameworkCore/Context/CoreDbContext.cs` |
| `Repository<T>` base for the three repositories | Morabh `Core/Core.EntityFrameworkCore/Repositories/Repository.cs` (vendored) |
| Append-only trigger function `reject_append_only_mutation()` | Elmanhg `20260928114118_AddSessionsAndAttempts.cs` (no Morabh equivalent) |
| Training record entities, hasher options/validator, placements query | new, with no Morabh equivalent |

## Existing code touched
| File | Change |
|------|--------|
| `api/core-libraries/Core.EntityFrameworkCore/Context/CoreDbContext.cs` | `SaveChangesAsync`: `var entities = ChangeTracker.Entries<Entity>().Select(e => e.Entity).ToList(); var domainEvents = entities.SelectMany(e => e.GetDomainEvents()).ToList(); foreach (var domainEvent in domainEvents) { await mediator.Publish(domainEvent, cancellationToken).ConfigureAwait(false); } foreach (var entity in entities) { entity.ClearDomainEvents(); }`. The audit read and base save that follow are unchanged. Add one WHY comment: "Materialised: notification handlers may add entities while events are published." |
| `api/Elmanhg.Domain/Sessions/Session.Answering.cs` | In `RecordAttempt`, after `Touch(now);`: `RaiseDomainEvent(new AttemptsRecorded(this, [attempt]));`. The idempotent early-return path does not raise. |
| `api/Elmanhg.Domain/Sessions/Session.ExamSubmission.cs` | In `SubmitExam`, after `Touch(at);`: `if (attempts.Count > 0) { RaiseDomainEvent(new AttemptsRecorded(this, attempts)); }` |
| `api/Elmanhg.Domain/Avatar/AvatarConversation.cs` | `RecordExchange`: build `studentMessage` and `assistantMessage` into locals, add both, and keep the counters as they are. The last statement is `RaiseDomainEvent(new AvatarExchangeRecorded(this, studentMessage, assistantMessage));` |
| `api/Elmanhg.Domain/TeacherThreads/TeacherThread.Replies.cs` | `Answer`: after `UpdationDate = at;`, `if (isFinalReply) { RaiseDomainEvent(new TeacherThreadClosed(this)); }` |
| `api/Elmanhg.Domain/TeacherThreads/TeacherThread.FollowUps.cs` | `Rate`: see Domain behaviour |
| `api/Elmanhg.Domain/Questions/IQuestionRepository.cs` | Add `Task<Dictionary<Guid, QuestionPlacement>> GetPlacementsAsync(IReadOnlyCollection<Guid> questionIds, CancellationToken cancellationToken);` |
| `api/Elmanhg.Infrastructure/Questions/QuestionRepository.cs` | Implement: `_dbSet.IgnoreQueryFilters().Where(x => questionIds.Contains(x.Id)).Join(_context.Set<Lesson>().IgnoreQueryFilters(), question => question.LessonId, lesson => lesson.Id, (question, lesson) => new QuestionPlacement(question.Id, question.SubjectId, lesson.UnitId, question.LessonId)).ToDictionaryAsync(x => x.QuestionId, cancellationToken).ConfigureAwait(false)`, with one operator per line |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | `public class` → `public partial class`. In `OnModelCreating`, call `ConfigureTrainingData(modelBuilder);` before `ApplyGlobalFilter…`. In `ApplyGlobalFilterToIgnoreSoftDeletionInAllQueries`, add `HasQueryFilter(x => !x.IsDeleted)` for the 3 new entities |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | `services.AddTrainingData();` after `AddFileStorage()`. Add 3 scoped repository registrations: `IAttemptTrainingRecordRepository`, `IAvatarTrainingRecordRepository`, `ITeacherThreadTrainingRecordRepository` |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated by `dotnet ef migrations add AddTrainingRecords` |
| `api/Elmanhg.Api/appsettings.example.json` | Add `"TrainingData": { "StudentIdHashKey": "" },` after `"Avatar"` |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | `public const string TestStudentIdHashKey = "elmanhg-tests-training-student-id-key-0123456789";` with the WHY comment "Keys only the student hashes written inside this in-memory host." Add `builder.UseSetting("TrainingData:StudentIdHashKey", TestStudentIdHashKey);` next to the other `UseSetting` calls |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | Append `thirtyThird => thirtyThird.Should().EndWith("_AddTrainingRecords")` to the migration list (the accepted pattern) |
| `deploy/api.env.example` | After the Identity block: `# Training data (docs/training-data.md). openssl rand -hex 32; set once, never rotate.` then `TrainingData__StudentIdHashKey=change-me-openssl-rand-hex-32` |
| `docs/PRD.md` | §13: after the table, add: "The anonymised id is an HMAC-SHA256 of the student id under a server secret. Admin test-mode sessions are not recorded. AI-grading and override records arrive with E17. Tables, triggers and the privacy checklist: `docs/training-data.md`." §15: add the 3 entity lines listed under Domain behaviour → PRD §15 lines |
| `docs/avatar.md` | Privacy bullet: replace "(operational data; #109 derives hashed-id copies)" with "(operational data; each exchange is also copied to `AvatarTrainingRecords` under a hashed id, see [training-data.md](training-data.md))". Retention bullet: replace "belong to #109's retention and privacy review" with "are open in the retention and privacy checklist of [training-data.md](training-data.md) (dev decision #215)". Consumers bullet: replace it with "#110 (JSONL export) reads `AvatarTrainingRecords` ([training-data.md](training-data.md))". Line 184 "(#109 review)" → "(#215)" |
| `docs/sessions.md` | Line 129: replace "Anonymisation happens at export time (E12), not by rewriting attempts." with "Each new attempt is also copied, under a hashed student id, to the append-only `AttemptTrainingRecords` ([training-data.md](training-data.md)); attempts themselves are never rewritten." Line 150: "training-data export (E12) exclude" → "training records (#109) exclude" |
| `docs/ask-teacher.md` | Line 90: "which #109 turns into training records" → "which are copied to `TeacherThreadTrainingRecords` when the thread closes and when a closed thread is rated ([training-data.md](training-data.md))". Line 141: "#109 exports `Text` for both kinds" → "the training record keeps `Text` for both kinds" |
| `docs/deployment.md` | §3 "Generating secrets" table: add row `TrainingData__StudentIdHashKey` · `openssl rand -hex 32` · "set once; never rotate". §3 Rotation table: add row "Training hash key · never rotate · a new key gives every student a new pseudonym and splits their training history". §4 "API identity and seed" table: add row `TrainingData__StudentIdHashKey` · yes (Staging, Production) · empty · "secret; HMAC key of student ids in training records; the API refuses to start without it outside Development and Testing" |

## Files to create
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `api/Elmanhg.Domain/Sessions/AttemptsRecorded.cs` | event | `namespace Elmanhg.Domain.Sessions; public sealed record AttemptsRecorded(Session Session, IReadOnlyList<Attempt> Attempts) : DomainEvent;` |
| 2 | `api/Elmanhg.Domain/Avatar/AvatarExchangeRecorded.cs` | event | `public sealed record AvatarExchangeRecorded(AvatarConversation Conversation, AvatarMessage StudentMessage, AvatarMessage AssistantMessage) : DomainEvent;` |
| 3 | `api/Elmanhg.Domain/TeacherThreads/TeacherThreadClosed.cs` | event | `public sealed record TeacherThreadClosed(TeacherThread Thread) : DomainEvent;` |
| 4 | `api/Elmanhg.Domain/TeacherThreads/TeacherThreadRatedAfterClose.cs` | event | `public sealed record TeacherThreadRatedAfterClose(TeacherThread Thread, DateTimeOffset RatedAt) : DomainEvent;` |
| 5 | `api/Elmanhg.Domain/Questions/QuestionPlacement.cs` | record | `public sealed record QuestionPlacement(Guid QuestionId, Guid SubjectId, Guid UnitId, Guid LessonId);` |
| 6 | `api/Elmanhg.Domain/TrainingData/AttemptTrainingRecord.cs` | entity | `namespace Elmanhg.Domain.TrainingData; public class AttemptTrainingRecord : Entity`. Private-set props: `string StudentHash`, `Guid AttemptId`, `Guid QuestionId`, `int QuestionVersion`, `Guid SubjectId`, `Guid UnitId`, `Guid LessonId`, `SessionKind SessionKind`, `string Answer`, `decimal Score`, `decimal NormalisedScore`, `AttemptGrader GradedBy`, `string? Grade`, `int TimeTakenMilliseconds`, `DateTimeOffset OccurredAt`, `DateTimeOffset RecordedAt`. `private AttemptTrainingRecord(Guid id) : base(id) { }`. `public static AttemptTrainingRecord From(Attempt attempt, SessionKind sessionKind, QuestionPlacement placement, string studentHash, DateTimeOffset recordedAt)` (see Domain behaviour) |
| 7 | `api/Elmanhg.Domain/TrainingData/AvatarTrainingRecord.cs` | entity | `public class AvatarTrainingRecord : Entity`. Props: `string StudentHash`, `Guid ConversationId`, `Guid StudentMessageId`, `Guid AssistantMessageId`, `int StudentMessagePosition`, `AvatarEntryPoint EntryPoint`, `Guid? SubjectId`, `Guid? UnitId`, `Guid? LessonId`, `Guid? QuestionId`, `string StudentText`, `string AssistantText`, `string Model`, `string PromptVersion`, `string Context`, `DateTimeOffset AskedAt`, `DateTimeOffset OccurredAt`, `DateTimeOffset RecordedAt`. `public static AvatarTrainingRecord From(AvatarConversation conversation, AvatarMessage studentMessage, AvatarMessage assistantMessage, string studentHash, DateTimeOffset recordedAt)` |
| 8 | `api/Elmanhg.Domain/TrainingData/TeacherThreadTrainingRecord.cs` | entity | `public class TeacherThreadTrainingRecord : Entity`. Props: `string StudentHash`, `Guid ThreadId`, `TeacherThreadTrainingTrigger Trigger`, `Guid SubjectId`, `Guid UnitId`, `Guid LessonId`, `Guid? QuestionId`, `int? QuestionVersion`, `Guid? AttemptId`, `string Context`, `string Messages`, `int? Rating`, `DateTimeOffset SubmittedAt`, `DateTimeOffset OccurredAt`, `DateTimeOffset RecordedAt`. `public static TeacherThreadTrainingRecord From(TeacherThread thread, TeacherThreadTrainingTrigger trigger, string studentHash, DateTimeOffset occurredAt, DateTimeOffset recordedAt)`. `public IReadOnlyList<TeacherThreadTrainingMessage> ReadMessages() => JsonSerializer.Deserialize<List<TeacherThreadTrainingMessage>>(Messages, QuestionJson.SerializerOptions) ?? [];` |
| 9 | `api/Elmanhg.Domain/TrainingData/TeacherThreadTrainingTrigger.cs` | enum | `public enum TeacherThreadTrainingTrigger { Closed, RatedAfterClose }` |
| 10 | `api/Elmanhg.Domain/TrainingData/TeacherThreadTrainingAuthor.cs` | enum | `public enum TeacherThreadTrainingAuthor { Student, Teacher }` |
| 11 | `api/Elmanhg.Domain/TrainingData/TeacherThreadTrainingMessage.cs` | record | `public sealed record TeacherThreadTrainingMessage(TeacherThreadTrainingAuthor Author, TeacherMessageKind Kind, string Text, bool HasImage, DateTimeOffset SentAt);` |
| 12 | `api/Elmanhg.Domain/TrainingData/IAttemptTrainingRecordRepository.cs` | port | `public interface IAttemptTrainingRecordRepository : IRepository<AttemptTrainingRecord> { }` |
| 13 | `api/Elmanhg.Domain/TrainingData/IAvatarTrainingRecordRepository.cs` | port | `public interface IAvatarTrainingRecordRepository : IRepository<AvatarTrainingRecord> { }` |
| 14 | `api/Elmanhg.Domain/TrainingData/ITeacherThreadTrainingRecordRepository.cs` | port | `public interface ITeacherThreadTrainingRecordRepository : IRepository<TeacherThreadTrainingRecord> { }` |
| 15 | `api/Elmanhg.Application/Shared/TrainingData/IStudentIdHasher.cs` | port | `namespace Elmanhg.Application.Shared.TrainingData; public interface IStudentIdHasher { string Hash(Guid studentId); }` |
| 16 | `api/Elmanhg.Application/Events/TrainingRecords/AttemptTrainingRecordHandler.cs` | handler | `namespace Elmanhg.Application.Events.TrainingRecords; public sealed class AttemptTrainingRecordHandler(IAttemptTrainingRecordRepository attemptTrainingRecordRepository, IQuestionRepository questionRepository, IStudentIdHasher studentIdHasher, TimeProvider timeProvider) : INotificationHandler<AttemptsRecorded>`. `Handle` steps: 1. `if (notification.Session.IsTestMode) return;` 2. `var questionIds = notification.Attempts.Select(x => x.QuestionId).Distinct().ToList();` 3. `var placements = await questionRepository.GetPlacementsAsync(questionIds, cancellationToken).ConfigureAwait(false);` 4. `var studentHash = studentIdHasher.Hash(notification.Session.StudentId); var now = timeProvider.GetUtcNow();` 5. `records = notification.Attempts.Select(x => AttemptTrainingRecord.From(x, notification.Session.Kind, placements.GetValueOrDefault(x.QuestionId) ?? throw new InvalidOperationException($"Question {x.QuestionId} has no placement."), studentHash, now)).ToList();` 6. `await attemptTrainingRecordRepository.AddRangeAsync(records, cancellationToken).ConfigureAwait(false);`. Never calls `SaveChangesAsync`, because the enclosing save persists the rows |
| 17 | `api/Elmanhg.Application/Events/TrainingRecords/AvatarTrainingRecordHandler.cs` | handler | `public sealed class AvatarTrainingRecordHandler(IAvatarTrainingRecordRepository avatarTrainingRecordRepository, IStudentIdHasher studentIdHasher, TimeProvider timeProvider) : INotificationHandler<AvatarExchangeRecorded>`. `Handle`: `var record = AvatarTrainingRecord.From(notification.Conversation, notification.StudentMessage, notification.AssistantMessage, studentIdHasher.Hash(notification.Conversation.StudentId), timeProvider.GetUtcNow()); await avatarTrainingRecordRepository.AddAsync(record, cancellationToken).ConfigureAwait(false);`. No save |
| 18 | `api/Elmanhg.Application/Events/TrainingRecords/TeacherThreadTrainingRecordHandler.cs` | handler | `public sealed class TeacherThreadTrainingRecordHandler(ITeacherThreadTrainingRecordRepository teacherThreadTrainingRecordRepository, IStudentIdHasher studentIdHasher, TimeProvider timeProvider) : INotificationHandler<TeacherThreadClosed>, INotificationHandler<TeacherThreadRatedAfterClose>`. `Handle(TeacherThreadClosed)` → `AppendAsync(thread, TeacherThreadTrainingTrigger.Closed, thread.ClosedAt ?? throw new InvalidOperationException("A closed thread has no ClosedAt."), ct)`. `Handle(TeacherThreadRatedAfterClose)` → `AppendAsync(thread, TeacherThreadTrainingTrigger.RatedAfterClose, notification.RatedAt, ct)`. `private async Task AppendAsync(TeacherThread thread, TeacherThreadTrainingTrigger trigger, DateTimeOffset occurredAt, CancellationToken cancellationToken)` → `From(thread, trigger, studentIdHasher.Hash(thread.StudentId), occurredAt, timeProvider.GetUtcNow())` then `AddAsync`. No save |
| 19 | `api/Elmanhg.Infrastructure/TrainingData/TrainingDataOptions.cs` | options | `namespace Elmanhg.Infrastructure.TrainingData; public sealed class TrainingDataOptions { public const string SectionName = "TrainingData"; public const int MinStudentIdHashKeyLength = 32; public const string DevelopmentStudentIdHashKey = "elmanhg-development-training-student-id-key-not-a-secret";` (WHY comment: "Only Development and Testing may fall back to this public key; the validator refuses it elsewhere.") `public string StudentIdHashKey { get; set; } = string.Empty; }` |
| 20 | `api/Elmanhg.Infrastructure/TrainingData/TrainingDataOptionsValidator.cs` | validator | `public sealed class TrainingDataOptionsValidator(IHostEnvironment hostEnvironment) : IValidateOptions<TrainingDataOptions>`. `Validate(string? name, TrainingDataOptions options)`: `var key = options.StudentIdHashKey ?? string.Empty; var requiresKey = hostEnvironment.IsProduction() \|\| hostEnvironment.IsStaging();`. Failures: (a) `key.Length > 0 && key.Length < MinStudentIdHashKeyLength` → `"TrainingData:StudentIdHashKey must be at least 32 characters."`; (b) `requiresKey && key.Length == 0` → `"TrainingData:StudentIdHashKey is required in Staging and Production."`; (c) `requiresKey && key == DevelopmentStudentIdHashKey` → `"TrainingData:StudentIdHashKey must not be the development key in Staging and Production."`. Returns `Success` or `Fail(failures)` (the `AiServiceOptionsValidator` shape) |
| 21 | `api/Elmanhg.Infrastructure/TrainingData/HmacStudentIdHasher.cs` | adapter | `public sealed class HmacStudentIdHasher(IOptions<TrainingDataOptions> options) : IStudentIdHasher`. `private readonly byte[] _key = Encoding.UTF8.GetBytes(string.IsNullOrEmpty(options.Value.StudentIdHashKey) ? TrainingDataOptions.DevelopmentStudentIdHashKey : options.Value.StudentIdHashKey);` `public string Hash(Guid studentId) => Convert.ToHexStringLower(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(studentId.ToString("D"))));` |
| 22 | `api/Elmanhg.Infrastructure/TrainingData/TrainingDataServiceCollectionExtensions.cs` | DI | `public static IServiceCollection AddTrainingData(this IServiceCollection services)`: `services.AddOptions<TrainingDataOptions>().BindConfiguration(TrainingDataOptions.SectionName).ValidateOnStart(); services.AddSingleton<IValidateOptions<TrainingDataOptions>, TrainingDataOptionsValidator>(); services.AddSingleton<IStudentIdHasher, HmacStudentIdHasher>(); return services;` |
| 23 | `api/Elmanhg.Infrastructure/TrainingData/AttemptTrainingRecordRepository.cs` | repo | `public class AttemptTrainingRecordRepository(AppDbContext context) : Repository<AttemptTrainingRecord>(context), IAttemptTrainingRecordRepository { }` |
| 24 | `api/Elmanhg.Infrastructure/TrainingData/AvatarTrainingRecordRepository.cs` | repo | same shape for `AvatarTrainingRecord` |
| 25 | `api/Elmanhg.Infrastructure/TrainingData/TeacherThreadTrainingRecordRepository.cs` | repo | same shape for `TeacherThreadTrainingRecord` |
| 26 | `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.TrainingData.cs` | EF config | `public partial class AppDbContext`. Table-name consts: `public const string AttemptTrainingRecordsTable = "AttemptTrainingRecords"`, `AvatarTrainingRecordsTable = "AvatarTrainingRecords"`, `TeacherThreadTrainingRecordsTable = "TeacherThreadTrainingRecords"`. DbSets: `AttemptTrainingRecords`, `AvatarTrainingRecords`, `TeacherThreadTrainingRecords`. `private static void ConfigureTrainingData(ModelBuilder modelBuilder)`, with the config for each entity listed under EF mapping below |
| 27 | `api/Elmanhg.Infrastructure/Migrations/<timestamp>_AddTrainingRecords.cs` (+ `.Designer.cs`) | migration | Generated with `dotnet ef migrations add AddTrainingRecords --project api/Elmanhg.Infrastructure --startup-project api/Elmanhg.Api`. At the end of `Up`, one `migrationBuilder.Sql("""…""")` with 6 statements: for each table T in (`AttemptTrainingRecords` → prefix `attempt_training_records`, `AvatarTrainingRecords` → `avatar_training_records`, `TeacherThreadTrainingRecords` → `teacher_thread_training_records`): `CREATE TRIGGER <prefix>_append_only BEFORE UPDATE OR DELETE ON "T" FOR EACH ROW EXECUTE FUNCTION reject_append_only_mutation();` and `CREATE TRIGGER <prefix>_no_truncate BEFORE TRUNCATE ON "T" FOR EACH STATEMENT EXECUTE FUNCTION reject_append_only_mutation();`. `Down` first drops the 6 triggers (`DROP TRIGGER <name> ON "T";`), then the tables. It does not create or drop the function, which is owned by `AddSessionsAndAttempts` |
| 28 | `docs/training-data.md` | doc | Sections are listed under Docs content below |
| 29–44 | tests | — | See the Test plan. Files: `api/Elmanhg.Tests/Domain/TrainingData/AttemptTrainingRecordTests.cs`, `…/Domain/TrainingData/AvatarTrainingRecordTests.cs`, `…/Domain/TrainingData/TeacherThreadTrainingRecordTests.cs`, `…/Domain/Sessions/AttemptsRecordedEventTests.cs`, `…/Domain/Avatar/AvatarExchangeRecordedEventTests.cs`, `…/Domain/TeacherThreads/TeacherThreadTrainingEventsTests.cs`, `…/Application/Features/Events/AttemptTrainingRecordHandlerTests.cs`, `…/Application/Features/Events/AvatarTrainingRecordHandlerTests.cs`, `…/Application/Features/Events/TeacherThreadTrainingRecordHandlerTests.cs`, `…/Infrastructure/TrainingData/HmacStudentIdHasherTests.cs`, `…/Infrastructure/TrainingData/TrainingDataOptionsValidatorTests.cs`, `…/Integration/TrainingData/TrainingDataTestData.cs`, `…/Integration/TrainingData/AttemptTrainingRecordTests.cs`, `…/Integration/TrainingData/AvatarTrainingRecordTests.cs`, `…/Integration/TrainingData/TeacherThreadTrainingRecordTests.cs`, `…/Integration/Persistence/TrainingRecordAppendOnlyTests.cs` |

### EF mapping (`ConfigureTrainingData`)
Use the existing private consts from `AppDbContext.cs`: `Sha256HexLength`, `EnumColumnMaxLength`, `AiIdentifierMaxLength`.
| Entity | Config |
|---|---|
| `AttemptTrainingRecord` | `ToTable(AttemptTrainingRecordsTable)`. `Id` `ValueGeneratedNever()`. `StudentHash` required, `HasMaxLength(Sha256HexLength)`. `SessionKind` and `GradedBy` use `HasConversion<string>().HasMaxLength(EnumColumnMaxLength)`. `Answer` required jsonb. `Grade` jsonb. `Score` `HasPrecision(9, 2)`. `NormalisedScore` `HasPrecision(5, 4)`. Indexes: `AttemptId` unique; `OccurredAt`; `StudentHash`. **No `HasOne`** |
| `AvatarTrainingRecord` | `ToTable(AvatarTrainingRecordsTable)`. `Id` `ValueGeneratedNever()`. `StudentHash` as above. `EntryPoint` string enum. `StudentText` and `AssistantText` required. `Model` and `PromptVersion` required, `HasMaxLength(AiIdentifierMaxLength)`. `Context` required jsonb. Indexes: `StudentMessageId` unique; `(ConversationId, StudentMessagePosition)`; `OccurredAt`; `StudentHash`. No `HasOne` |
| `TeacherThreadTrainingRecord` | `ToTable(TeacherThreadTrainingRecordsTable)`. `Id` `ValueGeneratedNever()`. `StudentHash` as above. `Trigger` string enum. `Context` and `Messages` required jsonb. Indexes: `(ThreadId, Trigger)` unique; `OccurredAt`; `StudentHash`. No `HasOne` |

## Error codes
None added. `ErrorCodes`, resources and problem mappings are unchanged. Invariant breaks throw `InvalidOperationException` or `ArgumentException`, and they would surface as a 500 on the source command, which is atomic, so nothing is half-written.

## Domain behaviour
**`TeacherThread.Rate`** (full body after the guards, which stay unchanged):
```csharp
var at = ToMicroseconds(ratedAt);
var wasClosed = Status == TeacherThreadStatus.Closed;
Rating = rating;
if (Status == TeacherThreadStatus.Answered)
{
    Status = TeacherThreadStatus.Closed;
    ClosedAt = at;
}

UpdatedBy = StudentId;
UpdationDate = at;
RaiseDomainEvent(wasClosed ? new TeacherThreadRatedAfterClose(this, at) : new TeacherThreadClosed(this));
```
**`TeacherThread.Answer`**: unchanged, plus `if (isFinalReply) { RaiseDomainEvent(new TeacherThreadClosed(this)); }` after `UpdationDate = at;`. `UpdationDate` is already set on both paths.

**`AttemptTrainingRecord.From`**:
1. `ArgumentException.ThrowIfNullOrWhiteSpace(studentHash);`
2. `if (placement.QuestionId != attempt.QuestionId) throw new InvalidOperationException("Placement does not belong to the attempt's question.");`
3. Return a new record (`Guid.NewGuid()`) that copies `AttemptId = attempt.Id`, `QuestionId`, `QuestionVersion`, `SubjectId`/`UnitId`/`LessonId` from the placement, `SessionKind = sessionKind`, `Answer`, `Score`, `NormalisedScore`, `GradedBy`, `Grade`, `TimeTakenMilliseconds`, `OccurredAt = attempt.CreatedAt`, `RecordedAt = recordedAt` and `StudentHash`.

**`AvatarTrainingRecord.From`**:
1. `ArgumentException.ThrowIfNullOrWhiteSpace(studentHash);`
2. If `studentMessage.Role != AvatarMessageRole.Student || assistantMessage.Role != AvatarMessageRole.Assistant`, throw `InvalidOperationException("An exchange is a student message followed by an assistant reply.")`.
3. If either message's `ConversationId != conversation.Id`, throw `InvalidOperationException("Messages do not belong to the conversation.")`.
4. Copy `ConversationId = conversation.Id`, `StudentMessageId`, `AssistantMessageId`, `StudentMessagePosition = studentMessage.Position`, `EntryPoint` and `SubjectId`/`UnitId`/`LessonId`/`QuestionId` from the conversation, `StudentText = studentMessage.Text`, `AssistantText = assistantMessage.Text`. `Model`, `PromptVersion` and `Context` come from the assistant message, each followed by `?? throw new InvalidOperationException("An assistant reply has no model, prompt version or context.")`. Then `AskedAt = studentMessage.CreatedAt`, `OccurredAt = assistantMessage.CreatedAt` and `RecordedAt`. `SessionId`, tokens, cost and citations are **not** copied.

**`TeacherThreadTrainingRecord.From`**:
1. `ArgumentException.ThrowIfNullOrWhiteSpace(studentHash);`
2. If `thread.Status != TeacherThreadStatus.Closed`, throw `InvalidOperationException("Only a closed thread becomes a training record.")`.
3. If `thread.Messages.Count == 0`, throw `InvalidOperationException("Thread messages must be loaded.")`.
4. `var context = thread.ReadContext();`
5. Build `messages = thread.Messages.OrderBy(x => x.CreatedAt).Select(x => new TeacherThreadTrainingMessage(x.SenderId == thread.StudentId ? TeacherThreadTrainingAuthor.Student : TeacherThreadTrainingAuthor.Teacher, x.Kind, x.Text, x.ImageUrl is not null, x.CreatedAt)).ToList()`.
6. Return a record with `ThreadId = thread.Id`, `Trigger`, `SubjectId = context.SubjectId`, `UnitId`, `LessonId`, `QuestionId`, `QuestionVersion`, `AttemptId`, `Context = thread.Context`, `Messages = JsonSerializer.Serialize(messages, QuestionJson.SerializerOptions)`, `Rating = thread.Rating`, `SubmittedAt = thread.SubmittedAt`, `OccurredAt = occurredAt`, `RecordedAt` and `StudentHash`.

Training records have no mutating method, and no `UpdationDate` (they derive from `Entity`, not `AuditEntity`).

**PRD §15 lines** (add after `AvatarMessageUsage`):
```
AttemptTrainingRecord(id, student_hash, attempt_id, question_id, question_version, subject_id, unit_id, lesson_id, session_kind, answer_json, score, normalised_score, graded_by, grade_json?, time_taken_ms, occurred_at, recorded_at)  -- append-only; docs/training-data.md
AvatarTrainingRecord(id, student_hash, conversation_id, student_message_id, assistant_message_id, student_message_position, entry_point, subject_id?, unit_id?, lesson_id?, question_id?, student_text, assistant_text, model, prompt_version, context_json, asked_at, occurred_at, recorded_at)  -- append-only
TeacherThreadTrainingRecord(id, student_hash, thread_id, trigger[Closed|RatedAfterClose], subject_id, unit_id, lesson_id, question_id?, question_version?, attempt_id?, context_json, messages_json, rating?, submitted_at, occurred_at, recorded_at)  -- append-only; one per trigger
```

### Docs content: `docs/training-data.md`
Sections, in this order:
1. **Purpose.** PRD §13. Capture only; #110 exports.
2. **Student hash.** HMAC-SHA256, the key config, the validator rules (Decision 2), and never rotate.
3. **Tables.** One sub-section per table: the columns (as in PRD §15), the unique and plain indexes, and "no foreign keys" with the reason.
4. **When rows are written.** A table of source event → trigger → handler: quiz answer or exam submission including auto-submit → `AttemptsRecorded`; Avatar reply → `AvatarExchangeRecorded`; final reply or rating an answered thread → `TeacherThreadClosed`; rating a closed thread → `TeacherThreadRatedAfterClose`. State that rows are written in the same transaction as the source, that test-mode sessions are skipped, and that no backfill is done.
5. **Append-only.** 6 triggers using `reject_append_only_mutation()` (as in `docs/sessions.md`).
6. **What is not stored.** Student id, names, phone, email, teacher id, audio or image URLs, session id, Avatar tokens, cost and citations.
7. **Retention and privacy review checklist.** Exactly these items:
   - `[x]` Text only: voice replies keep the teacher's final transcript; no audio, image or file references.
   - `[x]` No direct identifiers: `StudentHash` replaces the student id; no teacher id.
   - `[x]` The hash key is a secret in `api.env`, required in Staging and Production, and never rotated.
   - `[x]` Admin test-mode sessions are excluded.
   - `[x]` Append-only is enforced by database triggers (UPDATE, DELETE, TRUNCATE).
   - `[x]` Written atomically with the source row: a failed save writes neither.
   - `[x]` No foreign keys from training tables, so operational erasure is never blocked by them.
   - `[ ]` Free text may contain PII the student typed (names, phone numbers). Stripping is #110's job at export.
   - `[ ]` Source ids (`AttemptId`, `ConversationId`, message ids, `ThreadId`, `attemptId` in context) can be joined to operational tables by anyone with database access. #110 must not export them raw.
   - `[ ]` **Retention period**: dev decision pending (#215). Until then, operational and training data are kept indefinitely with no purge job.
   - `[ ]` **Student erasure path**: dev decision pending (#215). The triggers block DELETE, so erasure needs a migration-owned privileged procedure. Anyone holding the key can re-link hashes to ids.
   - `[ ]` **Notice or consent** that interactions are used for training: dev or legal decision (#215).
   - `[x]` No API reads these tables. The only reader will be the Admin-only export (#110, PRD §16 "Export training data").
8. **Consumers.** #110.

## API surface
None. No endpoint, policy, OpenAPI, Orval or Postman change. `dotnet build` must regenerate `api/openapi/v1.json` with no diff.

## Test plan
The suite uses FluentAssertions (already pinned), NSubstitute and xUnit v3. Pass `TestContext.Current.CancellationToken` everywhere.

| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 1 | `Domain/TrainingData/AttemptTrainingRecordTests` | `From_Attempt_CopiesAnswerScoreGradeAndPlacement` | Every copied field equals the attempt's or placement's value; `OccurredAt == attempt.CreatedAt`; `StudentHash` is the given hash |
| 2 | 〃 | `From_PlacementOfOtherQuestion_ThrowsInvalidOperationException` | exception type |
| 3 | 〃 | `From_BlankStudentHash_ThrowsArgumentException` | exception type |
| 4 | `Domain/TrainingData/AvatarTrainingRecordTests` | `From_Exchange_CopiesTextsModelPromptContextAndReferences` | Texts, model `claude-sonnet-5`, prompt `v2`, context equals the assistant `Context`, entry point and lesson ids, `AskedAt`/`OccurredAt`, `StudentMessagePosition == 0` |
| 5 | 〃 | `From_MessagesInSwappedRoles_ThrowsInvalidOperationException` | exception type |
| 6 | 〃 | `From_MessageOfOtherConversation_ThrowsInvalidOperationException` | exception type |
| 7 | `Domain/TrainingData/TeacherThreadTrainingRecordTests` | `From_FinalRepliedThread_StoresAllMessagesInOrderAsText` | `ReadMessages()` has 4 entries with authors Student, Teacher, Student, Teacher and the texts of the thread messages |
| 8 | 〃 | `From_VoiceAnsweredThread_StoresTranscriptWithoutAudioUrl` | Builder `.AnsweredByVoice(t, url)`, then `Rate(4, …)`. The teacher entry has `Kind == Voice` and `Text == "Voice transcript."`. The `Messages` JSON does not contain `url` |
| 9 | 〃 | `From_ThreadWithImage_FlagsImageWithoutStoringUrl` | The first entry has `HasImage == true`. `Messages` does not contain the image url |
| 10 | 〃 | `From_ClosedThread_CopiesContextIdsRatingAndOmitsTeacherId` | Subject, unit and lesson ids come from the context; `Rating`; `Context == thread.Context`; `Messages` does not contain the teacher id string |
| 11 | 〃 | `From_OpenThread_ThrowsInvalidOperationException` | exception type |
| 12 | `Domain/Sessions/AttemptsRecordedEventTests` | `RecordAttempt_NewAttempt_RaisesAttemptsRecordedWithAttempt` | `GetDomainEvents()` contains exactly one `AttemptsRecorded` whose `Attempts` is `[attempt]` and whose `Session` is the session |
| 13 | 〃 | `RecordAttempt_SameAnswerAgain_RaisesNoSecondEvent` | Still exactly one event after the repeat |
| 14 | 〃 | `SubmitExam_AnsweredItems_RaisesOneEventWithAllAttempts` | `ExamSessionBuilder`, `SaveExamAnswer` on 2 items, `SubmitExam`: one event with 2 attempts |
| 15 | 〃 | `SubmitExam_NoAnsweredItems_RaisesNoEvent` | No `AttemptsRecorded` |
| 16 | `Domain/Avatar/AvatarExchangeRecordedEventTests` | `RecordExchange_Always_RaisesEventWithBothMessages` | The event's `StudentMessage` and `AssistantMessage` are `Messages[0]` and `Messages[1]` |
| 17 | `Domain/TeacherThreads/TeacherThreadTrainingEventsTests` | `Reply_FirstReply_RaisesNoClosedEvent` | No `TeacherThreadClosed` |
| 18 | 〃 | `Reply_FinalReply_RaisesTeacherThreadClosed` | Exactly one `TeacherThreadClosed(thread)` after `ClearDomainEvents()` and the final reply |
| 19 | 〃 | `Rate_AnsweredThread_RaisesTeacherThreadClosed` | One `TeacherThreadClosed`; no `RatedAfterClose` |
| 20 | 〃 | `Rate_ClosedThread_RaisesRatedAfterCloseWithRatingTime` | One `TeacherThreadRatedAfterClose` whose `RatedAt` equals the truncated rating time; no `TeacherThreadClosed` |
| 21 | `Application/Features/Events/AttemptTrainingRecordHandlerTests` | `Handle_StudentQuizAttempt_AddsRecordWithHashAndPlacement` | Captured `AddRangeAsync` list: 1 record with the hasher's value and the placement ids; `SaveChangesAsync` `DidNotReceive()` |
| 22 | 〃 | `Handle_TestModeSession_AddsNothing` | `GetPlacementsAsync` and `AddRangeAsync` `DidNotReceive()` |
| 23 | 〃 | `Handle_ExamAttempts_LoadsPlacementsOnceAndAddsOneRecordPerAttempt` | `GetPlacementsAsync` `Received(1)`; 2 records with `SessionKind.UnitExam` |
| 24 | 〃 | `Handle_MissingPlacement_ThrowsInvalidOperationException` | exception; `AddRangeAsync` `DidNotReceive()` |
| 25 | `Application/Features/Events/AvatarTrainingRecordHandlerTests` | `Handle_Exchange_AddsRecordWithHashedStudent` | Captured `AddAsync` record has `StudentHash` from `Hash(conversation.StudentId)` and `StudentMessageId`; no save |
| 26 | `Application/Features/Events/TeacherThreadTrainingRecordHandlerTests` | `Handle_TeacherThreadClosed_AddsClosedRecordAtClosedAt` | `Trigger == Closed`; `OccurredAt == thread.ClosedAt` |
| 27 | 〃 | `Handle_RatedAfterClose_AddsRatedRecordAtRatedAt` | `Trigger == RatedAfterClose`; `OccurredAt == RatedAt`; `Rating` set |
| 28 | `Infrastructure/TrainingData/HmacStudentIdHasherTests` | `Hash_SameIdTwice_ReturnsSameValue` | Equal |
| 29 | 〃 | `Hash_Always_Returns64LowerCaseHexCharacters` | Matches `^[0-9a-f]{64}$` |
| 30 | 〃 | `Hash_DifferentKeys_ReturnDifferentValues` | Not equal |
| 31 | 〃 | `Hash_Always_DiffersFromUnkeyedSha256OfId` | Not equal to `Convert.ToHexStringLower(SHA256.HashData(utf8(id)))` |
| 32 | 〃 | `Hash_EmptyKey_UsesDevelopmentKey` | Equal to a hasher built with `DevelopmentStudentIdHashKey` |
| 33 | `Infrastructure/TrainingData/TrainingDataOptionsValidatorTests` | `Validate_EmptyKeyInRequiredEnvironment_Fails` | `[Theory]` over `Production` and `Staging`; `Failed` and the message contains `required` |
| 34 | 〃 | `Validate_DevelopmentKeyInProduction_Fails` | `Failed` |
| 35 | 〃 | `Validate_ShortKey_FailsInAnyEnvironment` | `[Theory]` over `Development` and `Testing` with a 31-character key; `Failed` |
| 36 | 〃 | `Validate_EmptyKeyInDevelopmentOrTesting_Succeeds` | `[Theory]` over `Development` and `Testing` |
| 37 | 〃 | `Validate_32CharacterKeyInProduction_Succeeds` | `Succeeded` |
| 38 | `Integration/TrainingData/AttemptTrainingRecordTests` | `SubmitAnswer_StudentQuiz_WritesRecordWithHashedStudentAndPlacement` | HTTP 200. One row with `AttemptId` equal to the stored attempt id. `StudentHash == TrainingDataTestData.ExpectedHash(student.Id)`, which does not contain the student id. Subject, unit and lesson equal the seeded lesson's. `OccurredAt == attempt.CreatedAt`. Answer JSON equals the attempt's |
| 39 | 〃 | `SubmitAnswer_SameAnswerTwice_WritesOneRecord` | Two 200 responses; 1 row |
| 40 | 〃 | `SubmitAnswer_AdminTestMode_WritesNoRecord` | Admin client, test-mode session: an attempt exists and there are 0 rows for it |
| 41 | 〃 | `SaveChanges_ConcurrentFirstAnswers_KeepsOneRecord` | Mirrors `SessionPersistenceTests.SaveChanges_ConcurrentFirstAnswers_…`. The second save throws `SESSION_QUESTION_ALREADY_ANSWERED`, and exactly 1 training row exists for the session's question (atomicity) |
| 42 | `Integration/TrainingData/AvatarTrainingRecordTests` | `PostMessage_Reply_WritesRecordWithTextsModelAndContext` | Mirrors the `AvatarConversationLogEndpointTests` setup. One row: `StudentText` is the sent message, `AssistantText` equals the reply, model and prompt version match the stored assistant message, `StudentHash` is the expected hash, `EntryPoint == Lesson` and `LessonId` is set |
| 43 | 〃 | `PostMessage_SecondMessage_WritesSecondRecordAtPositionTwo` | 2 rows with the same `ConversationId`; positions `[0, 2]` |
| 44 | 〃 | `PostMessage_BlankMessage_Returns422AndWritesNoRecord` | 422 and 0 rows for the student hash |
| 45 | `Integration/TrainingData/TeacherThreadTrainingRecordTests` | `Reply_FinalReplyAfterFollowUp_WritesClosedRecord` | Seed via `TeacherThreadBuilder…AnsweredBy(t).FollowedUp()` and POST the reply. One row with `Trigger == Closed`, `Rating == null`, 4 messages, the last one's text equal to the posted reply. The `Messages` JSON does not contain the teacher id |
| 46 | 〃 | `Rate_AnsweredThread_WritesClosedRecordWithRating` | 200; one row with `Closed` and `Rating == 5` |
| 47 | 〃 | `Rate_ClosedThread_AppendsRatedAfterCloseRecord` | Seed `FinalReplied()` (its seed save writes the `Closed` row), then rate 4. Rows: `Closed` with rating null, and `RatedAfterClose` with rating 4 |
| 48 | 〃 | `Rate_OpenThread_Returns409AndWritesNoRecord` | 409 `TEACHER_THREAD_NOT_ANSWERED`; 0 rows for the thread |
| 49 | `Integration/Persistence/TrainingRecordAppendOnlyTests` | `Update_TrainingRecordRow_RejectedByDatabase` | `[Theory]` over the 3 table consts: `UPDATE "T" SET "RecordedAt" = now() WHERE "Id" = …` throws `PostgresException` with `SqlState == "P0001"`, and the row's `RecordedAt` is unchanged |
| 50 | 〃 | `Delete_TrainingRecordRow_RejectedByDatabase` | `[Theory]` over the 3 tables; P0001; the row still exists |
| 51 | 〃 | `Truncate_TrainingRecordTable_RejectedByDatabase` | `[Theory]` over the 3 tables. Inside `BeginTransactionAsync` followed by a rollback: `TRUNCATE "T"` throws P0001 |
| 52 | `Integration/Persistence/AppDbContextTests` | `Migrate_FreshDatabase_LeavesNoPendingMigrations` (existing) | The list gains `_AddTrainingRecords` |

`Integration/TrainingData/TrainingDataTestData.cs` (static helper): `ExpectedHash(Guid studentId)` computes `Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(ApiFactory.TestStudentIdHashKey), Encoding.UTF8.GetBytes(studentId.ToString("D"))))`. It also has read helpers `ReadAttemptRecordsAsync(factory, Guid attemptId)`, `ReadAvatarRecordsAsync(factory, string studentHash)`, `ReadThreadRecordsAsync(factory, Guid threadId)` and `SeedRecordIdAsync(factory, string table)`, which seeds one row for the append-only theory: an Avatar conversation through `AvatarConversationBuilder` + `AvatarTestData.SeedConversationAsync`, a `FinalReplied` thread through `TeacherInboxTestData.SeedThreadAsync`, or a quiz attempt through `SessionTestData.SeedServableLessonAsync` + a `StartQuiz`/`RecordAttempt` save. It returns the new record id.

## Definition of done
- [ ] Three tables exist with the exact columns, the unique indexes (`AttemptId`, `StudentMessageId`, `(ThreadId, Trigger)`), `OccurredAt` and `StudentHash` indexes, and no foreign keys.
- [ ] The `AddTrainingRecords` migration creates 6 triggers on `reject_append_only_mutation()`. `Down` drops them before the tables.
- [ ] No training table has a student id, teacher id, audio or image URL, or session id column.
- [ ] `StudentHash` is HMAC-SHA256 hex under `TrainingData:StudentIdHashKey`. The validator refuses an empty or development key in Staging and Production, and a key under 32 characters anywhere.
- [ ] `CoreDbContext.SaveChangesAsync` materialises entities and events before publishing and passes `cancellationToken`.
- [ ] `Session.RecordAttempt` (new attempt only) and `SubmitExam` (≥1 attempt) raise `AttemptsRecorded`. `RecordExchange` raises `AvatarExchangeRecorded`. Final reply and rating an answered thread raise `TeacherThreadClosed`. Rating a closed thread raises `TeacherThreadRatedAfterClose`.
- [ ] Handlers add rows without calling `SaveChangesAsync`. Test-mode sessions write nothing.
- [ ] A failed source save persists no training row (test 41).
- [ ] `appsettings.example.json`, `ApiFactory` (`UseSetting`), `deploy/api.env.example` and `docs/deployment.md` carry `TrainingData:StudentIdHashKey`.
- [ ] `docs/training-data.md` exists with the checklist exactly as specified. PRD §13 and §15, `avatar.md`, `sessions.md` and `ask-teacher.md` are updated as listed, and no doc still says "#109 will…".
- [ ] All 52 test rows are present and pass. `dotnet test api/ -c Release` passes with `api/Elmanhg.Api/appsettings.json` moved aside.
- [ ] `dotnet build` leaves `api/openapi/v1.json` unchanged. There are no web, ai or Postman changes.
- [ ] Every new file listed above exists and no other file is added. File-scoped namespaces, `ConfigureAwait(false)`, no comments except the WHY comments named here.
