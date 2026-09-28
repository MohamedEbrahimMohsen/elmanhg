# Plan — Attempt log and session model (#74, E5.S1)

## Goal
A Student (or an Admin in test mode) can start a quiz session on a Published lesson. The server serves up to N servable questions, recording the exact version of each, so every answer is graded against what the student saw. The student answers one question at a time. Each answer is graded by `QuestionGrader.Grade` and stored immediately as an append-only `Attempt` holding the answer JSON, score, normalised score, feedback, question version and time taken. A database trigger enforces "stored forever". The student can then finish the session and get a score out of 100, the total time and a per-question review. A refresh loses nothing: `GET /api/sessions/{id}` returns the saved progress and the current position, and starting a quiz on the same lesson resumes the open session instead of creating a second one. Double-submitting the same answer is idempotent. Indexes back per-student per-question attempt lookups, which #75 (selection) and #77 (mastery) need.

## Scope
**In:**
- Domain `Elmanhg.Domain.Sessions`:
  - `Session` aggregate (partial: core, answering, submission), with `SessionItem` (the served question and version) and `Attempt` (append-only) children.
  - Enums `SessionKind` and `AttemptGrader`, and the value object `QuizScope`.
  - `ISessionRepository`.
- Domain `Questions`:
  - `QuestionRevision.ReadSnapshot()` and `QuestionRevision.Grade(JsonElement)`, so grading runs against the served version.
  - `QuestionGrade.ToOutcome(decimal)`, extracted.
  - Two `IQuestionRepository` methods.
- Application `Sessions`:
  - Commands `StartQuizSession`, `SubmitAnswer` and `FinishSession`, and the query `GetSession`.
  - Shared results and generator, and `SessionsOptions`.
  - `QuestionAnswerRules` moves to `Questions/Shared` and gains `Canonicalize`.
- Infrastructure:
  - `SessionRepository` and the EF mappings and indexes.
  - Unique-violation mapping in `AppDbContext`.
  - Migration `AddSessionsAndAttempts`, which includes the append-only trigger on `Attempts`.
- API: `SessionsController` (`api/sessions`, policy `AssessmentsTake`), `Requests.cs` and resx strings. Plus the regenerated `api/openapi/v1.json`, Orval (`web/src/shared/api/generated/**`, generated files only) and Postman.
- Docs: new `docs/sessions.md`, `docs/PRD.md` §15 and `docs/audit-log.md`.

**Out:**
- Adaptive bucket selection (#75). This story serves a uniform-random draw from the lesson's servable questions, which is exactly bucket 1 for a new student. #75 replaces the body of `QuestionRepository.GetRandomServableInLessonAsync` (or adds a selector) without changing the session model.
- Quiz screen UI (#76). Web gets only the regenerated API client; no hand-written web code.
- Mastery and `QuestionMastery` (#77).
- History list and progress (#78).
- Exams, blueprints, time limits and exam draft auto-save (E6).
- Free-tier daily limits (#87).
- Training-data JSONL export and anonymisation (E12).
- Question difficulty on served items: the revision snapshot has no difficulty. #76 adds it if the chip needs it.

**Deferred:** none. Nothing here needs an external provider or credentials.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | Aggregate shape | `Session` is the aggregate root (`AuditEntity`, like `ReviewSession`). It owns `Items` (`SessionItem : Entity`) and `Attempts` (`Attempt : Entity`). Attempts are created only through `Session.RecordAttempt`. `Attempt` lives in `Elmanhg.Domain.Sessions`, and later stories query it through their own repository methods. | The invariants (item belongs to this session, one answer per item, not after submission) need the session. This mirrors `Question.Revisions` and `Question.Decisions`. |
| 2 | Kinds, and scope without a breaking change for E6 | `SessionKind { Quiz, UnitExam, MultiUnitExam }` exists in full now; only `Session.StartQuiz` exists. The scope is stored twice. `Scope` is jsonb (PRD `scope_json`), holding `{"lessonId":"<guid>"}` for a quiz. `ScopeKey` is a canonical string, `lesson:<guid>` for a quiz, used for resume lookup and the uniqueness index. E6 adds `StartUnitExam` (`unit:<guid>`, `{"unitId"}`) and so on, plus nullable columns (`TimeLimitMinutes`, draft answers on `SessionItem`); all of that is additive. | No jsonb path queries are needed. A string equality is indexable. Nothing here would need to be renamed or dropped for exams. |
| 3 | When questions are served | All N questions are chosen and recorded as `SessionItem` rows (`Position`, `QuestionId`, `QuestionVersion`, `MaxScore`) at start. | Resume is deterministic, PRD §7.2's "never the same question twice in one session" becomes a unique index, and exams (generated whole) fit the same shape. |
| 4 | Servable at serve time | The start handler reads through `WhereServable` (the #67 specification). `Session.StartQuiz` re-checks each question with `ServableQuestionSpecification.IsSatisfiedBy(question, lesson)` and throws `SESSION_QUESTION_NOT_SERVABLE`. A lesson that is not Published returns 404 `LESSON_NOT_FOUND` (student-facing reads see Published only). | Uses the single servable definition. |
| 5 | Question becomes non-servable mid-session (edited, retired, lesson unpublished) | The answer is still accepted and graded against the served version. The in-progress session keeps its items. | The student already saw the question. PRD §17 rule 2: historical attempts keep the old version. Retirement only removes a question from future quizzes. |
| 6 | Grading version | Grading loads the `QuestionRevision` at `item.QuestionVersion` and calls `revision.Grade(answer)`, which calls `QuestionGrader.Grade(snapshot.Type, snapshot.GradingSpec, snapshot.MaxScore, answer)`. The live `Question` row is never used to grade. | Every version has a revision (created on create and on each content bump), and #70 migrated the snapshot specs. `docs/question-schemas.md` already promises that `question_version` resolves to an exact snapshot. |
| 7 | Answer validation | The validator checks that the answer is a JSON object and at most `Sessions:AnswerMaxLength` raw characters. The handler checks the shape for the served type with `QuestionAnswerRules.CanRead(snapshot.Type, answer)` and throws `ApplicationValidationCoreException(QUESTION_ANSWER_INVALID)` (422). | The type is only known after loading. The code and status match `grade-draft`. A malformed shape must never reach `QuestionGrader` (it throws `InvalidOperationException`, which would be a 500). |
| 8 | Stored answer form | `Attempt.Answer` is the canonical re-serialisation of the typed answer record (`QuestionAnswerRules.Canonicalize`), so unknown properties are dropped. | Clean training data, and a stable comparison for idempotency. |
| 9 | Idempotency (double submit) | If the item already has an attempt and the new canonical answer is `QuestionJson.AreEquivalent` to it, the existing attempt is returned (200, same body, no new row). A different answer returns 409 `SESSION_QUESTION_ALREADY_ANSWERED`. The race (two concurrent first submits) is stopped by the unique index `IX_Attempts_SessionId_QuestionId`, and `AppDbContext.SaveChangesAsync` maps that violation to the same 409. The replay check runs before the "already submitted" check. | A retry after a lost response succeeds. Re-answering after seeing feedback is refused. |
| 10 | Append-only enforcement | Migration SQL creates `reject_append_only_mutation()` (`RAISE EXCEPTION '% is append-only', TG_TABLE_NAME`), a `BEFORE UPDATE OR DELETE ... FOR EACH ROW` trigger `attempts_append_only`, and a `BEFORE TRUNCATE ... FOR EACH STATEMENT` trigger `attempts_no_truncate` on `"Attempts"`. `Attempt.Id` is `ValueGeneratedNever()`, so EF inserts rather than updates a new child. No code path modifies or soft-deletes an attempt. | This is the #58 audit-log pattern (`docs/audit-log.md`), and PRD §7.3 says attempts are "Stored forever". |
| 11 | Retention and student deletion | Every FK to `Users`, `Questions` and `Sessions` is `Restrict`. Users are only ever soft-deleted (no hard-delete path exists), so attempts survive. Anonymisation happens at export time (PRD §13, E12), not by rewriting attempts. | The PRD has no deletion rule. "Stored forever" plus "keyed by anonymised student id" in exports. |
| 12 | Access (IDOR) | Policy `DefaultCodes.AssessmentsTake` (Student and Admin, PRD §16). Every session lookup has `x.StudentId == currentUserId` in the same predicate. Another user's session id returns 404 `SESSION_NOT_FOUND`, never 403. Teachers get 403 from the policy. | Skill §10 BOLA. |
| 13 | Admin "test mode" | When the caller's role claim is `Admin`, the session gets `IsTestMode = true`. Admin sessions otherwise behave the same and still write attempts (append-only). #77 and E12 exclude test-mode sessions. | PRD §16 "✓ (test mode)". Keeps admin runs out of mastery and training data without a second code path. |
| 14 | Time taken | The client may send `timeTakenMilliseconds`. The server measures `elapsed = now − session.LastActivityAt`, where `LastActivityAt` is set at start, on resume, on each attempt and on submit. The stored value is `clamp(reported, 0, elapsed)`, or `elapsed` when nothing is reported. The session's time is the sum of its attempts' times. | The client is precise about display time, and the server bounds it so it cannot be inflated. Resume resets the clock, so an overnight gap is never counted. |
| 15 | Auto-save and resume | Every answer is committed as it is submitted; there is no draft state for quizzes. `POST /api/sessions/quiz` for a lesson with an open quiz session of this student returns that session (`Resume()`) and ignores `questionCount`. `GET /api/sessions/{id}` returns the items, saved attempts and `currentPosition` (lowest unanswered position; null when all are answered or the session is submitted). A partial unique index `IX_Sessions_InProgressScope` on `(StudentId, Kind, ScopeKey) WHERE "SubmittedAt" IS NULL AND "IsDeleted" = false` allows one open session per scope. A concurrent double start maps to 409 `SESSION_ALREADY_IN_PROGRESS`, and a retry resumes. | PRD §14: a refresh resumes. The prototype route `#/student/quiz/:sessionId` reloads by id. |
| 16 | Finishing | `FinishSession` is allowed at any time ("إنهاء التدريب" mid-quiz). `ScorePercent = round(Σ attempt.Score / Σ item.MaxScore × 100, 2)`, with unanswered items counting 0. It is idempotent: a second finish changes nothing and returns the same result. Nothing auto-finishes. | PRD §7.2 "score out of 100, time, per-question review". The same formula works for exams. |
| 17 | What the result reveals | Each item carries the served `Type`, `Stem`, `Body` and `MaxScore` from the revision snapshot. `CorrectAnswer` (the snapshot grading spec) and `Explanation` are non-null only when the item has an attempt or the session is submitted. | Immediate feedback per question (PRD §7.2) without leaking unanswered keys. |
| 18 | Quiz size | `Sessions:DefaultQuizSize` = 10, `MinQuizSize` = 5 and `MaxQuizSize` = 20. The validator allows any integer in `[Min, Max]` (the UI offers 5/10/20). A lesson with fewer servable questions gives a shorter quiz; zero servable questions gives 400 `SESSION_NO_SERVABLE_QUESTIONS`. | Skill §8.1: caps go in options. PRD §7.2: the quiz is shorter when there are fewer. |
| 19 | Auditing | Session commands are **not** `IAuditableCommand`, and none of the entities is `IAuditedEntity`. `docs/audit-log.md` "Not audited" gets a row. | The audit log covers content and validation (PRD §17 rule 13). Attempts are their own append-only log. |
| 20 | Names | Following the "no abbreviations" rule: `ScorePercent` (PRD `score_pct`), `TimeTakenMilliseconds` (`time_taken_ms`), `AttemptGrader { Auto, AI, Teacher }` (`graded_by`), `Attempt.Grade` jsonb (`grade_json`, a serialised `GradeFeedback`). | Constitution §3. The PRD fields map one to one. |
| 21 | Start route | `POST /api/sessions/quiz`. E6 adds `POST /api/sessions/unit-exam` and so on. The other routes are kind-agnostic. | Each kind has a different start body. |
| 22 | Revisions lookup | `IQuestionRepository.GetRevisionsAsync(questionIds)` loads every revision of those questions (at most `MaxQuizSize` questions × a few versions), and the generator matches on `(QuestionId, Version)` in memory. | EF cannot translate a tuple `Contains`. The set is small and bounded. |

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Domain/Questions/QuestionRevision.cs` | Add `public QuestionRevisionSnapshot ReadSnapshot()` and `public QuestionGrade Grade(JsonElement answer)` (see Domain behaviour). |
| `api/Elmanhg.Domain/Questions/Grading/QuestionGrade.cs` | Extract `public static GradeOutcome ToOutcome(decimal normalisedScore)` (the existing switch). `FromNormalised` calls it. No behaviour change. |
| `api/Elmanhg.Domain/Questions/IQuestionRepository.cs` | Add `Task<List<Question>> GetRandomServableInLessonAsync(Guid lessonId, int count, CancellationToken cancellationToken);` and `Task<List<QuestionRevision>> GetRevisionsAsync(IReadOnlyCollection<Guid> questionIds, CancellationToken cancellationToken);` |
| `api/Elmanhg.Infrastructure/Questions/QuestionRepository.cs` | Implement both. The first is `_dbSet.WhereServable(_context.Set<Lesson>()).Where(x => x.LessonId == lessonId).OrderBy(x => EF.Functions.Random()).Take(count).AsNoTracking().ToListAsync(...)`. The second is `_context.Set<QuestionRevision>().Where(x => questionIds.Contains(x.QuestionId)).AsNoTracking().ToListAsync(...)`. |
| `api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs` | Add a `// SESSIONS` group (domain codes, see Error codes). |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | Add a `// SESSIONS` group (application codes). |
| `api/Elmanhg.Application/Questions/GradeQuestionDraft/QuestionAnswerRules.cs` | **Delete** (moved to `Questions/Shared`). `GradeQuestionDraftValidator` already has `using Elmanhg.Application.Questions.Shared;`, so it compiles unchanged. |
| `api/Elmanhg.Application/DependencyInjection.cs` | `services.AddOptions<SessionsOptions>().BindConfiguration(SessionsOptions.SectionName).ValidateDataAnnotations().ValidateOnStart();` |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | Add DbSets `Sessions`, `SessionItems` and `Attempts`. Add `ConfigureSessions(modelBuilder)`, called from `OnModelCreating` after `ConfigureReviewSessions`. Add three global filter lines. Add two `catch` clauses in `SaveChangesAsync` (see below). Add constants `public const string InProgressSessionIndex = "IX_Sessions_InProgressScope";` and `public const string AttemptPerQuestionIndex = "IX_Attempts_SessionId_QuestionId";` (public so tests can reference them). Import `DomainErrorCodes = Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes`. |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | `services.AddScoped<ISessionRepository, SessionRepository>();` |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated by `dotnet ef migrations add`. |
| `api/Elmanhg.Api/Resources/Messages.en.resx`, `Messages.ar.resx` | 12 new keys (see Error codes). |
| `api/Elmanhg.Api/appsettings.example.json` | `"Sessions": { "DefaultQuizSize": 10, "MinQuizSize": 5, "MaxQuizSize": 20, "AnswerMaxLength": 4000 },` placed after `"QuestionValidation"`. |
| `api/openapi/v1.json` | Regenerated by `dotnet build`. |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | Add `["Sessions:DefaultQuizSize"] = "10"`, `["Sessions:MinQuizSize"] = "5"`, `["Sessions:MaxQuizSize"] = "20"` and `["Sessions:AnswerMaxLength"] = "4000"` to the in-memory collection. |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | Append `fourteenth => fourteenth.Should().EndWith("_AddSessionsAndAttempts")` to `Migrate_FreshDatabase_LeavesNoPendingMigrations`. This is the accepted pattern. |
| `web/src/shared/api/generated/**` | Regenerated only, with `npm --prefix web run gen:api`: new `sessions/sessions.ts`, `sessions/sessions.msw.ts`, `zod/sessions/sessions.zod.ts`, new `model/*` files, and the `index` files. No hand edits. |
| `postman/elmanhg.postman_collection.json` | New folder `Sessions` after `QuestionValidation`, and collection variables `sessionId` and `sessionQuestionId` (see API surface). |
| `docs/PRD.md` §15 | Session line becomes `Session(id, student_id, kind[Quiz\|UnitExam\|MultiUnitExam], scope_json, scope_key, is_test_mode, started_at, last_activity_at, submitted_at?, score_pct?, time_limit_min?)`. Add a line `SessionItem(id, session_id, position, question_id, question_version, max_score)  -- the questions served, fixed at start`. Append `-- append-only` to the Attempt line. |
| `docs/audit-log.md` | Under "Not audited (deliberate)" add: "**Quiz and exam activity** (`StartQuizSession`, `SubmitAnswer`, `FinishSession`): student practice, not a content or validation change. Attempts are their own append-only log (`docs/sessions.md`)." |

`AppDbContext.SaveChangesAsync` gains the following, after the existing clause:
```csharp
catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: InProgressSessionIndex })
{
    throw new ConflictCoreException(ErrorCodes.SessionAlreadyInProgress, innerException: exception);
}
catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: AttemptPerQuestionIndex })
{
    throw new ConflictCoreException(DomainErrorCodes.SessionQuestionAlreadyAnswered, innerException: exception);
}
```

`ConfigureSessions` (every enum is `HasConversion<string>().HasMaxLength(EnumColumnMaxLength)`, and every FK is `OnDelete(DeleteBehavior.Restrict)`):
- `Session`:
  - `Kind` is the enum. `Scope` is `.IsRequired().HasColumnType("jsonb")`. `ScopeKey` is `.IsRequired()`. `ScorePercent` is `.HasPrecision(5, 2)`.
  - `HasOne<User>().WithMany().HasForeignKey(x => x.StudentId)`.
  - `HasMany(x => x.Items).WithOne().HasForeignKey(x => x.SessionId)` and `HasMany(x => x.Attempts).WithOne().HasForeignKey(x => x.SessionId)`.
  - `HasIndex(x => new { x.StudentId, x.StartedAt })`.
  - `HasIndex(x => new { x.StudentId, x.Kind, x.ScopeKey }).IsUnique().HasFilter("\"SubmittedAt\" IS NULL AND \"IsDeleted\" = false").HasDatabaseName(InProgressSessionIndex)`.
- `SessionItem`:
  - `Id` is `ValueGeneratedNever()`.
  - `HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId)`.
  - Unique `(SessionId, Position)` and unique `(SessionId, QuestionId)`.
- `Attempt`:
  - `Id` is `ValueGeneratedNever()`. `Answer` is `.IsRequired().HasColumnType("jsonb")`. `Grade` is `.HasColumnType("jsonb")`. `GradedBy` is the enum. `Score` is `.HasPrecision(9, 2)`. `NormalisedScore` is `.HasPrecision(5, 4)`.
  - `HasOne<User>().WithMany().HasForeignKey(x => x.StudentId)` and `HasOne<Question>().WithMany().HasForeignKey(x => x.QuestionId)`.
  - `HasIndex(x => new { x.StudentId, x.QuestionId, x.CreatedAt })`, which gets the default name `IX_Attempts_StudentId_QuestionId_CreatedAt`.
  - `HasIndex(x => new { x.SessionId, x.QuestionId }).IsUnique().HasDatabaseName(AttemptPerQuestionIndex)`.

## Files to create
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `api/Elmanhg.Domain/Sessions/SessionKind.cs` | enum | `namespace Elmanhg.Domain.Sessions; public enum SessionKind { Quiz, UnitExam, MultiUnitExam }` |
| 2 | `api/Elmanhg.Domain/Sessions/AttemptGrader.cs` | enum | `public enum AttemptGrader { Auto, AI, Teacher }` |
| 3 | `api/Elmanhg.Domain/Sessions/QuizScope.cs` | value object | `public sealed record QuizScope(Guid LessonId) { public string ToKey() => $"lesson:{LessonId:D}"; public string ToJson() => JsonSerializer.Serialize(this, QuestionJson.SerializerOptions); }` These are methods, not properties, so they are never serialised. `ToJson()` yields `{"lessonId":"<guid>"}`. |
| 4 | `api/Elmanhg.Domain/Sessions/Session.cs` | aggregate (partial, `AuditEntity`) | `public partial class Session : AuditEntity`, with props (all `private set`): `Guid StudentId`, `SessionKind Kind`, `string Scope = "{}"`, `string ScopeKey = string.Empty`, `bool IsTestMode`, `DateTimeOffset StartedAt`, `DateTimeOffset LastActivityAt`, `DateTimeOffset? SubmittedAt`, `decimal? ScorePercent`, `List<SessionItem> Items = []`, `List<Attempt> Attempts = []`. Computed: `public bool IsSubmitted => SubmittedAt is not null;`, `public long TotalTimeTakenMilliseconds => Attempts.Sum(x => (long)x.TimeTakenMilliseconds);`, `public int? CurrentPosition => IsSubmitted ? null : Items.OrderBy(x => x.Position).FirstOrDefault(x => FindAttempt(x.QuestionId) is null)?.Position;`. Ctor `private Session(Guid id, Guid? createdBy) : base(id, createdBy) { }`. Methods `public static Session StartQuiz(Guid studentId, Lesson lesson, IReadOnlyList<Question> questions, bool isTestMode)` and `public void Resume()`. |
| 5 | `api/Elmanhg.Domain/Sessions/Session.Answering.cs` | partial | `public SessionItem? GetItem(Guid questionId)`, `public Attempt? FindAttempt(Guid questionId)`, `public Attempt RecordAttempt(SessionItem item, string answer, QuestionGrade grade, int? reportedTimeTakenMilliseconds)`, `private static int MeasureTimeTaken(DateTimeOffset since, DateTimeOffset now, int? reportedMilliseconds)` |
| 6 | `api/Elmanhg.Domain/Sessions/Session.Submission.cs` | partial | `public void Submit()`, plus the constant `private const int ScorePercentDecimals = 2;` with a WHY comment ("scores display to 2 decimals, matching `QuestionGrade`"). |
| 7 | `api/Elmanhg.Domain/Sessions/SessionItem.cs` | child `Entity` | Props (`private set`): `Guid SessionId`, `int Position`, `Guid QuestionId`, `int QuestionVersion`, `int MaxScore`. `private SessionItem(Guid id) : base(id) { }`. `internal static SessionItem Create(Guid sessionId, int position, Question question)` sets `QuestionVersion = question.Version` and `MaxScore = question.MaxScore`. |
| 8 | `api/Elmanhg.Domain/Sessions/Attempt.cs` | child `Entity` (append-only) | Props (`private set`): `Guid SessionId`, `Guid StudentId`, `Guid QuestionId`, `int QuestionVersion`, `string Answer = "{}"`, `decimal Score`, `decimal NormalisedScore`, `AttemptGrader GradedBy`, `string? Grade`, `int TimeTakenMilliseconds`, `DateTimeOffset CreatedAt`. `public GradeOutcome Outcome => QuestionGrade.ToOutcome(NormalisedScore);` `public GradeFeedback? ReadFeedback() => Grade is null ? null : JsonSerializer.Deserialize<GradeFeedback>(Grade, QuestionJson.SerializerOptions);` `private Attempt(Guid id) : base(id) { }` `internal static Attempt Create(Session session, SessionItem item, string answer, QuestionGrade grade, int timeTakenMilliseconds, DateTimeOffset createdAt)` sets `SessionId = session.Id`, `StudentId = session.StudentId`, `QuestionId = item.QuestionId`, `QuestionVersion = item.QuestionVersion`, `Answer = answer`, `Score = grade.Score`, `NormalisedScore = grade.NormalisedScore`, `GradedBy = AttemptGrader.Auto`, `Grade = grade.Feedback is null ? null : JsonSerializer.Serialize(grade.Feedback, QuestionJson.SerializerOptions)`, `TimeTakenMilliseconds` and `CreatedAt`. |
| 9 | `api/Elmanhg.Domain/Sessions/ISessionRepository.cs` | repo interface | `public interface ISessionRepository : IRepository<Session> { }`. The base `FirstOrDefaultAsync(predicate, ct, include, orderBy, asNoTracking)` covers every lookup. |
| 10 | `api/Elmanhg.Infrastructure/Sessions/SessionRepository.cs` | repo | `public class SessionRepository(AppDbContext context) : Repository<Session>(context), ISessionRepository { }` |
| 11 | `api/Elmanhg.Infrastructure/Migrations/<timestamp>_AddSessionsAndAttempts.cs` (+ `.Designer.cs`) | migration | Generated by `dotnet tool run dotnet-ef migrations add AddSessionsAndAttempts -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`. Creates tables `Sessions`, `SessionItems` and `Attempts` with the indexes above. Append to the end of `Up` one `migrationBuilder.Sql` holding `CREATE FUNCTION reject_append_only_mutation() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION '% is append-only', TG_TABLE_NAME; END; $$;`, `CREATE TRIGGER attempts_append_only BEFORE UPDATE OR DELETE ON "Attempts" FOR EACH ROW EXECUTE FUNCTION reject_append_only_mutation();` and `CREATE TRIGGER attempts_no_truncate BEFORE TRUNCATE ON "Attempts" FOR EACH STATEMENT EXECUTE FUNCTION reject_append_only_mutation();`. Put the reverse at the start of `Down`: drop both triggers, then the function. No `DropColumn`/`DropTable` of existing tables. |
| 12 | `api/Elmanhg.Application/Shared/Options/SessionsOptions.cs` | options | `public sealed class SessionsOptions { public const string SectionName = "Sessions"; [Range(1, 100)] public int DefaultQuizSize { get; set; } = 10; [Range(1, 100)] public int MinQuizSize { get; set; } = 5; [Range(1, 100)] public int MaxQuizSize { get; set; } = 20; [Range(1, 100000)] public int AnswerMaxLength { get; set; } = 4000; }` |
| 13 | `api/Elmanhg.Application/Questions/Shared/QuestionAnswerRules.cs` | static (moved) | `namespace Elmanhg.Application.Questions.Shared; public static class QuestionAnswerRules`. `public static bool CanRead(QuestionType type, JsonElement answer)` is the existing body, unchanged. Add `public static string Canonicalize(QuestionType type, JsonElement answer)`: a switch that calls `QuestionSchemaReader.Serialize(QuestionSchemaReader.Read<McqAnswer>(answer))`, and the same for `MultiAnswer`, `TrueFalseAnswer`, `FillAnswer` and `ShortAnswer`. `_ => throw new InvalidOperationException("Unsupported question type.")` |
| 14 | `api/Elmanhg.Application/Sessions/Shared/AttemptResult.cs` | result (client) | `public sealed record AttemptResult(Guid Id, JsonElement Answer, decimal Score, decimal NormalisedScore, string Outcome, string? Feedback, int TimeTakenMilliseconds, DateTimeOffset CreatedAt);` |
| 15 | `api/Elmanhg.Application/Sessions/Shared/SessionItemResult.cs` | result (client) | `public sealed record SessionItemResult(int Position, Guid QuestionId, int QuestionVersion, string Type, string Stem, JsonElement Body, int MaxScore, AttemptResult? Attempt, JsonElement? CorrectAnswer, string? Explanation);` |
| 16 | `api/Elmanhg.Application/Sessions/Shared/SessionResult.cs` | result (client) | `public sealed record SessionResult(Guid Id, string Kind, JsonElement Scope, bool IsTestMode, DateTimeOffset StartedAt, DateTimeOffset? SubmittedAt, decimal? ScorePercent, long TimeTakenMilliseconds, int? CurrentPosition, List<SessionItemResult> Items);` |
| 17 | `api/Elmanhg.Application/Sessions/Shared/SessionResultGenerator.cs` | static | `public static SessionResult Generate(Session session, IReadOnlyCollection<QuestionRevision> revisions, ILocalizer localizer)`: items ordered by `Position`, each mapped through `GenerateItem`. The revision is `revisions.FirstOrDefault(x => x.QuestionId == item.QuestionId && x.Version == item.QuestionVersion) ?? throw new InvalidOperationException("Served question revision is missing.")`, with a WHY comment ("revisions are never deleted and FKs restrict"). `Kind.ToString()`, `Scope` parsed, `TotalTimeTakenMilliseconds`, `CurrentPosition`. `public static SessionItemResult GenerateItem(Session session, SessionItem item, QuestionRevision revision, ILocalizer localizer)`: `snapshot = revision.ReadSnapshot()`; `attempt = session.FindAttempt(item.QuestionId)`; `reveal = attempt is not null \|\| session.IsSubmitted`; `Type = snapshot.Type.ToString()`, `Stem`, `Body = ToElement(snapshot.Body)`, `MaxScore = item.MaxScore`, `Attempt = attempt is null ? null : new AttemptResult(attempt.Id, Parse(attempt.Answer), attempt.Score, attempt.NormalisedScore, attempt.Outcome.ToString(), GradeFeedbackText.Localize(attempt.ReadFeedback(), localizer), attempt.TimeTakenMilliseconds, attempt.CreatedAt)`, `CorrectAnswer = reveal ? ToElement(snapshot.GradingSpec) : null`, `Explanation = reveal ? snapshot.Explanation : null`. Private helpers are `Parse(string json)` (`JsonDocument.Parse(json)` → `RootElement.Clone()`, in a `using`) and `ToElement(JsonNode? node) => Parse(node?.ToJsonString() ?? "{}")`. |
| 18 | `api/Elmanhg.Application/Sessions/StartQuizSession/StartQuizSessionCommand.cs` | command | `public sealed record StartQuizSessionCommand(Guid LessonId, int? QuestionCount) : IRequest<SessionResult>;` |
| 19 | `api/Elmanhg.Application/Sessions/StartQuizSession/StartQuizSessionValidator.cs` | validator | ctor `(IOptions<SessionsOptions> sessionsOptions)`. `RuleFor(x => x.LessonId).ValidateRequired(ErrorCodes.LessonIdRequired);` `RuleFor(x => x.QuestionCount.GetValueOrDefault()).ValidateRange(options.MinQuizSize, options.MaxQuizSize, ErrorCodes.SessionQuestionCountInvalid).When(x => x.QuestionCount.HasValue).OverridePropertyName(nameof(StartQuizSessionCommand.QuestionCount));` |
| 20 | `api/Elmanhg.Application/Sessions/StartQuizSession/StartQuizSessionHandler.cs` | handler | `public sealed class StartQuizSessionHandler(ISessionRepository sessionRepository, ILessonRepository lessonRepository, IQuestionRepository questionRepository, IOptions<SessionsOptions> sessionsOptions, ICurrentUserService currentUserService, ILocalizer localizer) : IRequestHandler<StartQuizSessionCommand, SessionResult>`. Steps: (1) if `UserId` is null or default, throw `UnauthorizedCoreException(UserNotAuthenticated)`. (2) `lesson = lessonRepository.GetByIdAsync(request.LessonId, ct, asNoTracking: true)`; if null or `lesson.State != LessonState.Published`, throw `NotFoundCoreException(LessonNotFound)`. (3) `scopeKey = new QuizScope(lesson.Id).ToKey()`; `session = sessionRepository.FirstOrDefaultAsync(x => x.StudentId == userId && x.Kind == SessionKind.Quiz && x.ScopeKey == scopeKey && x.SubmittedAt == null, ct, include: query => query.Include(x => x.Items).Include(x => x.Attempts).AsSplitQuery())`. (4) If found, call `session.Resume()`. Otherwise `questions = questionRepository.GetRandomServableInLessonAsync(lesson.Id, request.QuestionCount ?? options.DefaultQuizSize, ct)`, then `session = Session.StartQuiz(userId, lesson, questions, currentUserService.GetClaim(ClaimTypes.Role) == nameof(UserRole.Admin))`, then `sessionRepository.AddAsync(session, ct)`. (5) `sessionRepository.SaveChangesAsync(ct)` once. (6) `revisions = questionRepository.GetRevisionsAsync(session.Items.Select(x => x.QuestionId).ToList(), ct)`. (7) Return `SessionResultGenerator.Generate(session, revisions, localizer)`. |
| 21 | `api/Elmanhg.Application/Sessions/SubmitAnswer/SubmitAnswerCommand.cs` | command | `public sealed record SubmitAnswerCommand(Guid SessionId, Guid QuestionId, JsonElement Answer, int? TimeTakenMilliseconds) : IRequest<SessionItemResult>;` |
| 22 | `api/Elmanhg.Application/Sessions/SubmitAnswer/SubmitAnswerValidator.cs` | validator | ctor `(IOptions<SessionsOptions> sessionsOptions)`. Rules: `SessionId.ValidateRequired(ErrorCodes.SessionIdRequired)`; `QuestionId.ValidateRequired(ErrorCodes.QuestionIdRequired)`; `RuleFor(x => x.Answer).Must(x => x.ValueKind == JsonValueKind.Object).WithErrorCode(ErrorCodes.QuestionAnswerInvalid)`; `RuleFor(x => x.Answer).Must(x => x.GetRawText().Length <= options.AnswerMaxLength).WithErrorCode(ErrorCodes.AttemptAnswerTooLong).When(x => x.Answer.ValueKind == JsonValueKind.Object)`; `RuleFor(x => x.TimeTakenMilliseconds.GetValueOrDefault()).ValidateNonNegative(ErrorCodes.AttemptTimeTakenInvalid).When(x => x.TimeTakenMilliseconds.HasValue).OverridePropertyName(nameof(SubmitAnswerCommand.TimeTakenMilliseconds))`. |
| 23 | `api/Elmanhg.Application/Sessions/SubmitAnswer/SubmitAnswerHandler.cs` | handler | `public sealed class SubmitAnswerHandler(ISessionRepository sessionRepository, IQuestionRepository questionRepository, ICurrentUserService currentUserService, ILocalizer localizer) : IRequestHandler<SubmitAnswerCommand, SessionItemResult>`. Steps: (1) Auth guard. (2) `session = sessionRepository.FirstOrDefaultAsync(x => x.Id == request.SessionId && x.StudentId == userId, ct, include: Items + Attempts + AsSplitQuery)` (tracking); if null, throw `NotFoundCoreException(SessionNotFound)`. (3) `item = session.GetItem(request.QuestionId)`; if null, throw `NotFoundCoreException(SessionQuestionNotFound)`. (4) `revision = (await questionRepository.GetRevisionsAsync([item.QuestionId], ct)).FirstOrDefault(x => x.Version == item.QuestionVersion)`; if null, throw `NotFoundCoreException(QuestionNotFound)`. (5) `type = revision.ReadSnapshot().Type`; if `!QuestionAnswerRules.CanRead(type, request.Answer)`, throw `ApplicationValidationCoreException(ErrorCodes.QuestionAnswerInvalid)`. (6) `session.RecordAttempt(item, QuestionAnswerRules.Canonicalize(type, request.Answer), revision.Grade(request.Answer), request.TimeTakenMilliseconds)`. (7) `SaveChangesAsync` once (a no-op on replay). (8) Return `SessionResultGenerator.GenerateItem(session, item, revision, localizer)`. |
| 24 | `api/Elmanhg.Application/Sessions/FinishSession/FinishSessionCommand.cs` | command | `public sealed record FinishSessionCommand(Guid SessionId) : IRequest<SessionResult>;` |
| 25 | `api/Elmanhg.Application/Sessions/FinishSession/FinishSessionValidator.cs` | validator | `SessionId.ValidateRequired(ErrorCodes.SessionIdRequired)` |
| 26 | `api/Elmanhg.Application/Sessions/FinishSession/FinishSessionHandler.cs` | handler | `(ISessionRepository sessionRepository, IQuestionRepository questionRepository, ICurrentUserService currentUserService, ILocalizer localizer) : IRequestHandler<FinishSessionCommand, SessionResult>`. Steps: auth guard; load the owned session (tracking, Items + Attempts, split), throwing 404 `SessionNotFound` if missing; `session.Submit()`; `SaveChangesAsync` once; load revisions for the items; return `Generate`. |
| 27 | `api/Elmanhg.Application/Sessions/GetSession/GetSessionQuery.cs` | query | `public sealed record GetSessionQuery(Guid SessionId) : IRequest<SessionResult>;` |
| 28 | `api/Elmanhg.Application/Sessions/GetSession/GetSessionValidator.cs` | validator | `SessionId.ValidateRequired(ErrorCodes.SessionIdRequired)` |
| 29 | `api/Elmanhg.Application/Sessions/GetSession/GetSessionHandler.cs` | handler | Same dependencies as #26, `: IRequestHandler<GetSessionQuery, SessionResult>`. Steps: auth guard; load the owned session with `asNoTracking: true` (Items + Attempts, split), throwing 404 if missing; load revisions; return `Generate`. No save. |
| 30 | `api/Elmanhg.Api/Controllers/Sessions/Requests.cs` | request | `public sealed record SubmitAnswerRequest(Guid QuestionId, JsonElement Answer, int? TimeTakenMilliseconds);` |
| 31 | `api/Elmanhg.Api/Controllers/Sessions/SessionsController.cs` | controller | `[ApiController] [Route("api/sessions")] [Authorize] public class SessionsController(IMediator mediator) : ControllerBase`. Four actions (see API surface), each with `[Authorize(Policy = DefaultCodes.AssessmentsTake)]`, `[ProducesResponseType<T>(StatusCodes.Status200OK)]` and a named route. |
| 32 | `docs/sessions.md` | doc | Sections: Model (Session, SessionItem, Attempt, with field tables mapped to PRD §15); Lifecycle (start/resume, answer, finish); Grading against the served version; Idempotency; Time taken; Scoring; What is revealed; Append-only enforcement (trigger names, `reject_append_only_mutation`); Indexes; Access and IDOR; Test mode; Options (`Sessions:*`); API table; error-code table. It must agree with Decisions 1–22. |

**Tests to create** (the contracts are in the Test plan):

| # | Path |
|---|------|
| 33 | `api/Elmanhg.Tests/Builders/SessionBuilder.cs` |
| 34 | `api/Elmanhg.Tests/Domain/Sessions/SessionTests.cs` |
| 35 | `api/Elmanhg.Tests/Domain/Sessions/SessionAnsweringTests.cs` |
| 36 | `api/Elmanhg.Tests/Domain/Sessions/SessionSubmissionTests.cs` |
| 37 | `api/Elmanhg.Tests/Domain/Sessions/AttemptTests.cs` |
| 38 | `api/Elmanhg.Tests/Domain/Questions/QuestionRevisionTests.cs` |
| 39 | `api/Elmanhg.Tests/Application/Features/Questions/Shared/QuestionAnswerRulesTests.cs` |
| 40–46 | `api/Elmanhg.Tests/Application/Features/Sessions/` + `StartQuizSession/StartQuizSessionHandlerTests.cs`, `StartQuizSession/StartQuizSessionValidatorTests.cs`, `SubmitAnswer/SubmitAnswerHandlerTests.cs`, `SubmitAnswer/SubmitAnswerValidatorTests.cs`, `FinishSession/FinishSessionHandlerTests.cs`, `FinishSession/FinishSessionValidatorTests.cs`, `GetSession/GetSessionHandlerTests.cs` |
| 47 | `api/Elmanhg.Tests/Application/Features/Sessions/GetSession/GetSessionValidatorTests.cs` |
| 48 | `api/Elmanhg.Tests/Application/Features/Sessions/SessionRepositoryStub.cs` |
| 49 | `api/Elmanhg.Tests/Integration/Sessions/SessionTestData.cs` |
| 50–53 | `api/Elmanhg.Tests/Integration/Sessions/` + `StartQuizSessionEndpointTests.cs`, `SubmitAnswerEndpointTests.cs`, `FinishSessionEndpointTests.cs`, `GetSessionEndpointTests.cs` |
| 54 | `api/Elmanhg.Tests/Integration/Persistence/AttemptAppendOnlyTests.cs` |
| 55 | `api/Elmanhg.Tests/Integration/Persistence/SessionPersistenceTests.cs` |

Test helper contracts:
- **`SessionBuilder`** (namespace `Elmanhg.Tests.Builders`):
  - ctor: `Questions = new QuestionBuilder()` and `Questions.Lesson.Publish(Guid.NewGuid())`.
  - props: `QuestionBuilder Questions`, `Guid StudentId = Guid.NewGuid()`.
  - `List<Question> BuildQuestions(int count)`: each is `Questions.Approved().Build()`; `Approved()` is idempotent.
  - `Session Build(int count = 2, bool isTestMode = false) => Session.StartQuiz(StudentId, Questions.Lesson, BuildQuestions(count), isTestMode);`
  - `static QuestionGrade Grade(decimal normalised, GradeFeedback? feedback = null) => QuestionGrade.FromNormalised(new NormalisedGrade(normalised, feedback), 1);`
  - `const string AnswerB = "{\"optionId\":\"b\"}"` and `const string AnswerA = "{\"optionId\":\"a\"}"`.
- **`SessionRepositoryStub`** (static, unit tests): `static void StubFind(ISessionRepository repository, params Session[] sessions)`. It configures `FirstOrDefaultAsync(Arg.Any<Expression<Func<Session, bool>>>(), Arg.Any<CancellationToken>(), Arg.Any<Func<IQueryable<Session>, IQueryable<Session>>?>(), Arg.Any<Func<IQueryable<Session>, IOrderedQueryable<Session>>?>(), Arg.Any<bool>())` to return `call => sessions.FirstOrDefault(call.Arg<Expression<Func<Session, bool>>>().Compile())`, so the handler's real predicate (ownership, open-only) decides.
- **`SessionTestData`** (namespace `Elmanhg.Tests.Integration.Sessions`):
  - `const string Route = "/api/sessions"`.
  - `Task<(Guid LessonId, List<Guid> QuestionIds)> SeedServableLessonAsync(ApiFactory factory, int approvedCount)`: `ContentTestData` subject, unit and `SeedLessonInStateAsync(..., LessonState.Published)`, then `QuestionTestData.SeedQuestionAsync(approved: true)` × n.
  - `Task<(User Student, HttpClient Client)> SignedInStudentAsync(ApiFactory factory)`: `ScopeTestData.SeedStudentAsync` + `SignedInClientAsync`.
  - `Task<JsonElement> StartQuizAsync(HttpClient client, Guid lessonId, int? questionCount = null)`: asserts 200 and returns the body.
  - `Task<HttpResponseMessage> AnswerAsync(HttpClient client, Guid sessionId, Guid questionId, string optionId, int? timeTakenMilliseconds = 1000)`.
  - `Task EditQuestionContentAsync(ApiFactory factory, Guid questionId, QuestionContent content)`: tracked load of the question and its lesson (with objectives), then `question.Update(QuestionType.Mcq, content, new QuestionMetadata(question.Difficulty, question.ObjectiveId, question.Tags), lesson, Guid.NewGuid())`, then save.
  - `Task<Session> ReadSessionAsync(ApiFactory factory, Guid sessionId)`: `AsNoTracking`, includes Items and Attempts.
  - `Task<List<Attempt>> ReadAttemptsAsync(ApiFactory factory, Guid sessionId)`.

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| App `SessionNotFound` | `SESSION_NOT_FOUND` | Submit, Finish and Get handlers (missing or not owned) | `NotFoundCoreException` | 404 |
| App `SessionIdRequired` | `SESSION_ID_REQUIRED` | Submit, Finish and Get validators | pipeline | 422 |
| App `SessionQuestionCountInvalid` | `SESSION_QUESTION_COUNT_INVALID` | StartQuizSession validator | pipeline | 422 |
| App `SessionQuestionNotFound` | `SESSION_QUESTION_NOT_FOUND` | SubmitAnswer handler | `NotFoundCoreException` | 404 |
| App `SessionAlreadyInProgress` | `SESSION_ALREADY_IN_PROGRESS` | `AppDbContext.SaveChangesAsync` (unique `IX_Sessions_InProgressScope`) | `ConflictCoreException` | 409 |
| App `AttemptAnswerTooLong` | `ATTEMPT_ANSWER_TOO_LONG` | SubmitAnswer validator | pipeline | 422 |
| App `AttemptTimeTakenInvalid` | `ATTEMPT_TIME_TAKEN_INVALID` | SubmitAnswer validator | pipeline | 422 |
| Domain `SessionNoServableQuestions` | `SESSION_NO_SERVABLE_QUESTIONS` | `Session.StartQuiz` | `BusinessRuleViolationCoreException` | 400 |
| Domain `SessionQuestionNotServable` | `SESSION_QUESTION_NOT_SERVABLE` | `Session.StartQuiz` | `BusinessRuleViolationCoreException` | 400 |
| Domain `SessionQuestionDuplicate` | `SESSION_QUESTION_DUPLICATE` | `Session.StartQuiz` | `BusinessRuleViolationCoreException` | 400 |
| Domain `SessionAlreadySubmitted` | `SESSION_ALREADY_SUBMITTED` | `Session.RecordAttempt`, `Session.Resume` | `BusinessRuleViolationCoreException` | 400 |
| Domain `SessionQuestionAlreadyAnswered` | `SESSION_QUESTION_ALREADY_ANSWERED` | `Session.RecordAttempt`; `AppDbContext` (unique `IX_Attempts_SessionId_QuestionId`) | `ConflictCoreException` | 409 |

Reused codes, which already have resx keys: `USER_NOT_AUTHENTICATED` (401), `LESSON_ID_REQUIRED` (422), `LESSON_NOT_FOUND` (404), `QUESTION_ID_REQUIRED` (422), `QUESTION_NOT_FOUND` (404), and `QUESTION_ANSWER_INVALID` (422, from the validator and the handler's `ApplicationValidationCoreException`).

Resx entries (en / ar, Arabic without tashkeel, following the existing no-hamza-on-alef style):
| Key | English | Arabic |
|---|---|---|
| SESSION_NOT_FOUND | Session not found. | الجلسة غير موجودة. |
| SESSION_ID_REQUIRED | A session is required. | الجلسة مطلوبة. |
| SESSION_QUESTION_COUNT_INVALID | The number of questions is out of the allowed range. | عدد الاسئلة خارج النطاق المسموح. |
| SESSION_QUESTION_NOT_FOUND | This question is not part of the session. | هذا السؤال ليس ضمن الجلسة. |
| SESSION_ALREADY_IN_PROGRESS | A session for this lesson is already in progress. Try again to resume it. | توجد جلسة جارية لهذا الدرس. حاول مرة اخرى لاستكمالها. |
| ATTEMPT_ANSWER_TOO_LONG | The answer is too long. | الاجابة طويلة جدا. |
| ATTEMPT_TIME_TAKEN_INVALID | The time taken must be zero or more. | الوقت المستغرق يجب ان يكون صفرا او اكثر. |
| SESSION_NO_SERVABLE_QUESTIONS | This lesson has no questions available yet. | لا توجد اسئلة متاحة لهذا الدرس بعد. |
| SESSION_QUESTION_NOT_SERVABLE | A question cannot be served right now. | لا يمكن عرض احد الاسئلة حاليا. |
| SESSION_QUESTION_DUPLICATE | A question cannot appear twice in one session. | لا يمكن ان يظهر السؤال مرتين في الجلسة نفسها. |
| SESSION_ALREADY_SUBMITTED | This session has already ended. | انتهت هذه الجلسة بالفعل. |
| SESSION_QUESTION_ALREADY_ANSWERED | You have already answered this question. | لقد اجبت عن هذا السؤال بالفعل. |

## Domain behaviour
```csharp
// Session.cs
public static Session StartQuiz(Guid studentId, Lesson lesson, IReadOnlyList<Question> questions, bool isTestMode)
{
    if (questions.Count == 0) { throw new BusinessRuleViolationCoreException(ErrorCodes.SessionNoServableQuestions); }
    if (questions.Any(x => !ServableQuestionSpecification.IsSatisfiedBy(x, lesson))) { throw new BusinessRuleViolationCoreException(ErrorCodes.SessionQuestionNotServable); }
    if (questions.Select(x => x.Id).Distinct().Count() != questions.Count) { throw new BusinessRuleViolationCoreException(ErrorCodes.SessionQuestionDuplicate); }
    var scope = new QuizScope(lesson.Id);
    var now = DateTimeOffset.UtcNow;
    var session = new Session(Guid.NewGuid(), studentId) { StudentId = studentId, Kind = SessionKind.Quiz, Scope = scope.ToJson(), ScopeKey = scope.ToKey(), IsTestMode = isTestMode, StartedAt = now, LastActivityAt = now };
    session.Items.AddRange(questions.Select((question, index) => SessionItem.Create(session.Id, index + 1, question)));
    return session;
}

public void Resume()
{
    EnsureNotSubmitted();                    // SESSION_ALREADY_SUBMITTED
    var now = DateTimeOffset.UtcNow;
    LastActivityAt = now; UpdatedBy = StudentId; UpdationDate = now;
}

private void EnsureNotSubmitted() { if (IsSubmitted) { throw new BusinessRuleViolationCoreException(ErrorCodes.SessionAlreadySubmitted); } }

// Session.Answering.cs
public SessionItem? GetItem(Guid questionId) => Items.FirstOrDefault(x => x.QuestionId == questionId);
public Attempt? FindAttempt(Guid questionId) => Attempts.FirstOrDefault(x => x.QuestionId == questionId);

public Attempt RecordAttempt(SessionItem item, string answer, QuestionGrade grade, int? reportedTimeTakenMilliseconds)
{
    var existing = FindAttempt(item.QuestionId);
    if (existing is not null)
    {
        if (QuestionJson.AreEquivalent(existing.Answer, answer)) { return existing; }
        throw new ConflictCoreException(ErrorCodes.SessionQuestionAlreadyAnswered);
    }
    EnsureNotSubmitted();
    var now = DateTimeOffset.UtcNow;
    var attempt = Attempt.Create(this, item, answer, grade, MeasureTimeTaken(LastActivityAt, now, reportedTimeTakenMilliseconds), now);
    Attempts.Add(attempt);
    LastActivityAt = now; UpdatedBy = StudentId; UpdationDate = now;
    return attempt;
}

private static int MeasureTimeTaken(DateTimeOffset since, DateTimeOffset now, int? reportedMilliseconds)
{
    var elapsed = (int)Math.Clamp((now - since).TotalMilliseconds, 0, int.MaxValue);
    return reportedMilliseconds is null ? elapsed : Math.Clamp(reportedMilliseconds.Value, 0, elapsed);
}

// Session.Submission.cs
public void Submit()
{
    if (IsSubmitted) { return; }            // idempotent finish (Decision 16)
    var now = DateTimeOffset.UtcNow;
    var possible = Items.Sum(x => x.MaxScore);
    ScorePercent = Math.Round(Attempts.Sum(x => x.Score) * 100m / possible, ScorePercentDecimals, MidpointRounding.AwayFromZero);
    SubmittedAt = now; LastActivityAt = now; UpdatedBy = StudentId; UpdationDate = now;
}

// QuestionRevision.cs (added)
public QuestionRevisionSnapshot ReadSnapshot() => JsonSerializer.Deserialize<QuestionRevisionSnapshot>(Snapshot, QuestionJson.SerializerOptions) ?? throw new InvalidOperationException("Question revision snapshot is not readable.");
public QuestionGrade Grade(JsonElement answer)
{
    var snapshot = ReadSnapshot();
    return QuestionGrader.Grade(snapshot.Type, snapshot.GradingSpec?.ToJsonString() ?? "{}", snapshot.MaxScore, answer);
}

// QuestionGrade.cs (extracted; FromNormalised uses it)
public static GradeOutcome ToOutcome(decimal normalisedScore) => normalisedScore switch { >= 1m => GradeOutcome.Correct, > 0m => GradeOutcome.Partial, _ => GradeOutcome.Incorrect };
```
Invariants:
- `possible > 0` always holds: a session has at least one item, and `MaxScore` is at least 1.
- `Attempt` has no mutating method, and nothing calls `SoftDelete` on it.
- `SessionItem` never changes after start.
- Every mutating `Session` method sets `LastActivityAt`, `UpdatedBy` and `UpdationDate`.
- The code braces every `if` (the one-liners above are compressed for the plan only). Each file stays around 80–100 lines.

## API surface
| Method | Route | Policy | Body | Response |
|---|---|---|---|---|
| POST | `/api/sessions/quiz` (Name `StartQuizSession`) | `DefaultCodes.AssessmentsTake` | `[FromBody] StartQuizSessionCommand` `{ lessonId, questionCount? }` | 200 `SessionResult` (new or resumed) |
| GET | `/api/sessions/{sessionId:guid}` (Name `GetSession`) | `DefaultCodes.AssessmentsTake` | — | 200 `SessionResult` |
| POST | `/api/sessions/{sessionId:guid}/answers` (Name `SubmitSessionAnswer`) | `DefaultCodes.AssessmentsTake` | `[FromBody] SubmitAnswerRequest` `{ questionId, answer, timeTakenMilliseconds? }` → `new SubmitAnswerCommand(sessionId, request.QuestionId, request.Answer, request.TimeTakenMilliseconds)` | 200 `SessionItemResult` |
| POST | `/api/sessions/{sessionId:guid}/finish` (Name `FinishSession`) | `DefaultCodes.AssessmentsTake` | — | 200 `SessionResult` |

Postman folder `Sessions`, in this order:
1. **Start quiz session**: POST `{{baseUrl}}/api/sessions/quiz` with body `{"lessonId":"{{lessonId}}","questionCount":5}`. The test script checks status 200 and sets `sessionId = json.id` and `sessionQuestionId = json.items[0].questionId`.
2. **Get session**: GET `.../api/sessions/{{sessionId}}`.
3. **Submit answer**: POST `.../api/sessions/{{sessionId}}/answers` with body `{"questionId":"{{sessionQuestionId}}","answer":{"optionId":"b"},"timeTakenMilliseconds":12000}`.
4. **Finish session**: POST `.../api/sessions/{{sessionId}}/finish`.

The folder `description` reads: "Sign in as a Student (or an Admin for test mode). `lessonId` must be a Published lesson with approved questions."

## Test plan
FluentAssertions 7.2.2, NSubstitute, xUnit v3, and `TestContext.Current.CancellationToken`. Throwing handler paths assert the exception type, `.Which.ErrorCode`, and `SaveChangesAsync` `DidNotReceive()`. Success paths assert the result and `SaveChangesAsync` `Received(1)`. Domain tests use no doubles. Integration tests go through HTTP and read the DB through a fresh scope.

| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 1 | SessionTests | StartQuiz_ServableQuestions_CreatesOrderedItemsAtServedVersion | Kind Quiz; Items positions 1..2 match the question order; each `QuestionVersion` equals `question.Version`; `MaxScore` 1; `SessionId` equals the session id; `StudentId` and `CreatedBy` equal the student; `IsTestMode` false; `SubmittedAt` null; `LastActivityAt == StartedAt` |
| 2 | SessionTests | StartQuiz_Scope_StoresLessonJsonAndKey | `ScopeKey == $"lesson:{lessonId:D}"`; `Scope` parses to `{"lessonId":"<id>"}` (`JsonNode.DeepEquals`) |
| 3 | SessionTests | StartQuiz_TestMode_FlagsSession | `IsTestMode` true |
| 4 | SessionTests | StartQuiz_NoQuestions_ThrowsSessionNoServableQuestions | `BusinessRuleViolationCoreException` code |
| 5 | SessionTests | StartQuiz_PendingQuestion_ThrowsSessionQuestionNotServable | the question is built without `Approved()` |
| 6 | SessionTests | StartQuiz_LessonNotPublished_ThrowsSessionQuestionNotServable | a `QuestionBuilder` lesson that is never published, with approved questions |
| 7 | SessionTests | StartQuiz_RetiredQuestion_ThrowsSessionQuestionNotServable | `Approved().Retired()` |
| 8 | SessionTests | StartQuiz_SameQuestionTwice_ThrowsSessionQuestionDuplicate | list `[q, q]` |
| 9 | SessionTests | Resume_InProgress_MovesLastActivityForward | `LastActivityAt >= before` and `UpdationDate` set |
| 10 | SessionTests | Resume_Submitted_ThrowsSessionAlreadySubmitted | code |
| 11 | SessionTests | CurrentPosition_NoAttempts_IsFirst | 1 |
| 12 | SessionTests | CurrentPosition_FirstAnswered_IsSecond | 2 |
| 13 | SessionTests | CurrentPosition_AllAnswered_IsNull | null |
| 14 | SessionTests | CurrentPosition_Submitted_IsNull | null, with no answers |
| 15 | SessionAnsweringTests | RecordAttempt_FirstAnswer_AddsGradedAttemptAtServedVersion | one attempt with `SessionId`, `StudentId`, `QuestionId`, `QuestionVersion`, `Answer`, `Score` 1, `NormalisedScore` 1, `GradedBy` Auto, `CreatedAt` close to now |
| 16 | SessionAnsweringTests | RecordAttempt_WithFeedback_StoresGradeJson | `ReadFeedback()` equals `GradeFeedback.ChoiceTally(1, 1, 2)` |
| 17 | SessionAnsweringTests | RecordAttempt_ReportedTimeWithinElapsed_KeepsReportedTime | reported 0 → 0 |
| 18 | SessionAnsweringTests | RecordAttempt_ReportedTimeAboveElapsed_ClampsToElapsed | reported `int.MaxValue` → a value between 0 and 60,000 |
| 19 | SessionAnsweringTests | RecordAttempt_NoReportedTime_UsesServerElapsed | between 0 and 60,000 |
| 20 | SessionAnsweringTests | RecordAttempt_Answer_MovesLastActivityToAttemptTime | `LastActivityAt == attempt.CreatedAt` |
| 21 | SessionAnsweringTests | RecordAttempt_SameAnswerTwice_ReturnsExistingAttempt | same instance; `Attempts` count 1 |
| 22 | SessionAnsweringTests | RecordAttempt_EquivalentJsonDifferentFormatting_ReturnsExistingAttempt | `{"optionId":"b"}` vs `{ "optionId" : "b" }` → same instance |
| 23 | SessionAnsweringTests | RecordAttempt_DifferentAnswerForAnsweredItem_ThrowsSessionQuestionAlreadyAnswered | `ConflictCoreException` code; count 1 |
| 24 | SessionAnsweringTests | RecordAttempt_SubmittedSession_ThrowsSessionAlreadySubmitted | `BusinessRuleViolationCoreException` code; no attempt |
| 25 | SessionAnsweringTests | RecordAttempt_SameAnswerAfterSubmit_ReturnsExistingAttempt | answer, `Submit()`, same answer → same instance |
| 26 | SessionAnsweringTests | GetItem_QuestionNotServed_ReturnsNull | null |
| 27 | SessionSubmissionTests | Submit_AllCorrect_Scores100 | `ScorePercent` 100.00; `SubmittedAt` set |
| 28 | SessionSubmissionTests | Submit_HalfAnswered_CountsUnansweredAsZero | 2 items, 1 correct → 50.00 |
| 29 | SessionSubmissionTests | Submit_PartialCredit_RoundsToTwoDecimals | 3 items, grades 1, 0 and 0 (`Grade(1m)` on one) → 33.33 |
| 30 | SessionSubmissionTests | Submit_AlreadySubmitted_KeepsOriginalSubmission | `SubmittedAt` and `ScorePercent` unchanged after a second `Submit()` |
| 31 | SessionSubmissionTests | TotalTimeTakenMilliseconds_SumsAttempts | reported 0 and 0 → 0; a single attempt reported 0 → 0 (deterministic) |
| 32 | AttemptTests | Outcome_ByNormalisedScore_MapsToGradeOutcome | `[Theory]` rows 1→Correct, 0.5→Partial, 0→Incorrect |
| 33 | AttemptTests | ReadFeedback_NoFeedback_ReturnsNull | `Grade` null; `ReadFeedback()` null |
| 34 | QuestionRevisionTests | ReadSnapshot_CreatedQuestion_ReturnsServedContent | Type Mcq, `Stem`, `MaxScore` 1, and `GradingSpec` containing `correctOptionId` "b" |
| 35 | QuestionRevisionTests | Grade_AfterContentEdit_GradesAgainstOldVersion | Approve, then `Update` with the correct option changed to "a" (version 2). `Revisions.Single(v == 1).Grade({"optionId":"b"})` → Correct; the v2 revision grades "b" Incorrect |
| 36 | QuestionAnswerRulesTests | Canonicalize_McqWithUnknownProperty_DropsIt | `{"optionId":"b","x":1}` → `{"optionId":"b"}` |
| 37 | QuestionAnswerRulesTests | Canonicalize_EachType_RoundTripsTypedAnswer | `[Theory]` rows for Multi, TrueFalse, Fill and Short: the output is `AreEquivalent` to the expected canonical JSON |
| 38 | StartQuizSessionHandlerTests | Handle_NoOpenSession_StartsQuizWithRequestedCount | `GetRandomServableInLessonAsync(lessonId, 5, …)` received (this call is the behaviour); `AddAsync` Received(1) with a session of 2 items; result Items count 2, `Kind` "Quiz", item `Type` "Mcq", `CorrectAnswer` null; Save Received(1) |
| 39 | StartQuizSessionHandlerTests | Handle_NoQuestionCount_UsesDefaultQuizSize | `GetRandomServableInLessonAsync(lessonId, 10, …)` received |
| 40 | StartQuizSessionHandlerTests | Handle_OpenSessionForLesson_ResumesIt | `AddAsync` DidNotReceive; result `Id == existing.Id`; `GetRandomServable…` DidNotReceive; Save Received(1) |
| 41 | StartQuizSessionHandlerTests | Handle_OpenSessionOfOtherStudent_StartsNewSession | stubbed via `SessionRepositoryStub` with another student's open session → `AddAsync` Received(1) |
| 42 | StartQuizSessionHandlerTests | Handle_AdminCaller_StartsTestModeSession | `GetClaim(ClaimTypes.Role)` returns "Admin" → `IsTestMode` true on the added session and the result |
| 43 | StartQuizSessionHandlerTests | Handle_NoCurrentUser_ThrowsUserNotAuthenticated | 401 type and code |
| 44 | StartQuizSessionHandlerTests | Handle_LessonMissing_ThrowsLessonNotFound | `NotFoundCoreException` |
| 45 | StartQuizSessionHandlerTests | Handle_LessonDraft_ThrowsLessonNotFound | `NotFoundCoreException` |
| 46 | StartQuizSessionHandlerTests | Handle_NoServableQuestions_ThrowsSessionNoServableQuestions | `BusinessRuleViolationCoreException`; `AddAsync` DidNotReceive |
| 47 | StartQuizSessionValidatorTests | Validate_ValidCommand_Passes | no errors |
| 48 | StartQuizSessionValidatorTests | Validate_NoQuestionCount_Passes | no errors |
| 49 | StartQuizSessionValidatorTests | Validate_EmptyLessonId_FailsLessonIdRequired | code |
| 50 | StartQuizSessionValidatorTests | Validate_QuestionCountOutOfRange_FailsSessionQuestionCountInvalid | `[Theory]` 4, 21 |
| 51 | SubmitAnswerHandlerTests | Handle_FirstAnswer_RecordsGradedAttemptAndSaves | result Attempt Outcome "Correct", Score 1, `CorrectAnswer` not null, `Explanation` set; `session.Attempts` count 1 with canonical answer; Save Received(1) |
| 52 | SubmitAnswerHandlerTests | Handle_QuestionEditedAfterServing_GradesServedVersion | revisions stub returns v1 (correct "b") and v2 (correct "a"); item at v1; answer "b" → Correct and `QuestionVersion` 1 |
| 53 | SubmitAnswerHandlerTests | Handle_SameAnswerResubmitted_ReturnsOriginalAttempt | result `Attempt.Id` equals the first attempt's id; count 1 |
| 54 | SubmitAnswerHandlerTests | Handle_NoCurrentUser_ThrowsUserNotAuthenticated | |
| 55 | SubmitAnswerHandlerTests | Handle_SessionMissing_ThrowsSessionNotFound | |
| 56 | SubmitAnswerHandlerTests | Handle_OtherStudentsSession_ThrowsSessionNotFound | `SessionRepositoryStub` with a session owned by another id |
| 57 | SubmitAnswerHandlerTests | Handle_QuestionNotInSession_ThrowsSessionQuestionNotFound | |
| 58 | SubmitAnswerHandlerTests | Handle_RevisionMissing_ThrowsQuestionNotFound | revisions stub returns `[]` |
| 59 | SubmitAnswerHandlerTests | Handle_AnswerShapeWrongForType_ThrowsQuestionAnswerInvalid | answer `{"optionIds":1}` → `ApplicationValidationCoreException` |
| 60 | SubmitAnswerHandlerTests | Handle_SubmittedSession_ThrowsSessionAlreadySubmitted | `BusinessRuleViolationCoreException` |
| 61 | SubmitAnswerHandlerTests | Handle_DifferentAnswerForAnsweredQuestion_ThrowsSessionQuestionAlreadyAnswered | `ConflictCoreException` |
| 62 | SubmitAnswerValidatorTests | Validate_ValidCommand_Passes | |
| 63 | SubmitAnswerValidatorTests | Validate_NoTimeTaken_Passes | |
| 64 | SubmitAnswerValidatorTests | Validate_EmptySessionId_FailsSessionIdRequired | |
| 65 | SubmitAnswerValidatorTests | Validate_EmptyQuestionId_FailsQuestionIdRequired | |
| 66 | SubmitAnswerValidatorTests | Validate_AnswerNotObject_FailsQuestionAnswerInvalid | `[Theory]` over `"b"`, `[]` and `default(JsonElement)`, built with `QuestionBuilder.Json` |
| 67 | SubmitAnswerValidatorTests | Validate_AnswerOverMaxLength_FailsAttemptAnswerTooLong | options `AnswerMaxLength` 20 |
| 68 | SubmitAnswerValidatorTests | Validate_NegativeTimeTaken_FailsAttemptTimeTakenInvalid | −1 |
| 69 | FinishSessionHandlerTests | Handle_OpenSession_SubmitsScoresAndSaves | result `SubmittedAt` set, `ScorePercent` 50.00 (1 of 2 correct), `CurrentPosition` null, unanswered item `CorrectAnswer` not null; Save Received(1) |
| 70 | FinishSessionHandlerTests | Handle_AlreadySubmitted_ReturnsSameSubmission | `SubmittedAt` equals the first value |
| 71 | FinishSessionHandlerTests | Handle_NoCurrentUser_ThrowsUserNotAuthenticated | |
| 72 | FinishSessionHandlerTests | Handle_OtherStudentsSession_ThrowsSessionNotFound | |
| 73 | FinishSessionValidatorTests | Validate_ValidCommand_Passes | |
| 74 | FinishSessionValidatorTests | Validate_EmptySessionId_FailsSessionIdRequired | |
| 75 | GetSessionHandlerTests | Handle_OwnSessionWithOneAnswer_ReturnsProgress | item 1 has Attempt, `CorrectAnswer` and `Explanation`; item 2 has null Attempt, `CorrectAnswer` and `Explanation`; `CurrentPosition` 2; `Stem` and `Body` from the snapshot |
| 76 | GetSessionHandlerTests | Handle_SubmittedSession_RevealsUnansweredItems | item 2 `CorrectAnswer` not null |
| 77 | GetSessionHandlerTests | Handle_OtherStudentsSession_ThrowsSessionNotFound | |
| 78 | GetSessionHandlerTests | Handle_NoCurrentUser_ThrowsUserNotAuthenticated | |
| 79 | GetSessionValidatorTests | Validate_ValidQuery_Passes | |
| 80 | GetSessionValidatorTests | Validate_EmptySessionId_FailsSessionIdRequired | |
| 81 | StartQuizSessionEndpointTests | Post_PublishedLessonWithServableQuestions_Returns200AndPersistsItems | 200; body `items` count 3 (seeded 3, count 5); `currentPosition` 1; `kind` "Quiz"; DB session with 3 items at version 1, `IsTestMode` false |
| 82 | StartQuizSessionEndpointTests | Post_MixedQuestions_ServesOnlyServable | 1 approved, 1 pending (`SeedQuestionAsync(approved: false)`), 1 retired → `items` is only the approved id |
| 83 | StartQuizSessionEndpointTests | Post_QuestionCountBelowAvailable_ServesThatMany | 6 seeded, count 5 → 5 distinct ids |
| 84 | StartQuizSessionEndpointTests | Post_SecondStartSameLesson_ResumesSameSession | same `id`; DB has 1 open session for the student and the lesson |
| 85 | StartQuizSessionEndpointTests | Post_NoServableQuestions_Returns400SessionNoServableQuestions | status and `code` |
| 86 | StartQuizSessionEndpointTests | Post_DraftLesson_Returns404LessonNotFound | |
| 87 | StartQuizSessionEndpointTests | Post_QuestionCountOutOfRange_Returns422 | `code` contains `SESSION_QUESTION_COUNT_INVALID` |
| 88 | StartQuizSessionEndpointTests | Post_Admin_StartsTestModeSession | 200; `isTestMode` true |
| 89 | StartQuizSessionEndpointTests | Post_Teacher_Returns403 | |
| 90 | StartQuizSessionEndpointTests | Post_Anonymous_Returns401 | `AuthTestClient.Create(factory)` |
| 91 | SubmitAnswerEndpointTests | Post_CorrectAnswer_Returns200AndPersistsAttempt | body `attempt.outcome` "Correct", `correctAnswer` present; DB attempt with `Score` 1, `NormalisedScore` 1, `QuestionVersion` 1, `Answer` `{"optionId":"b"}`, `GradedBy` Auto, `TimeTakenMilliseconds` between 0 and 1000, `StudentId` = student |
| 92 | SubmitAnswerEndpointTests | Post_SameAnswerTwice_Returns200SameAttemptOneRow | same `attempt.id`; DB count 1 |
| 93 | SubmitAnswerEndpointTests | Post_DifferentAnswerAfterAnswering_Returns409 | `code` `SESSION_QUESTION_ALREADY_ANSWERED`; DB count 1 |
| 94 | SubmitAnswerEndpointTests | Post_QuestionEditedAfterServing_GradesServedVersion | start; `EditQuestionContentAsync` sets correct "a"; answer "b" → "Correct"; DB `QuestionVersion` 1 |
| 95 | SubmitAnswerEndpointTests | Post_OtherStudentsSession_Returns404SessionNotFound | second student's client |
| 96 | SubmitAnswerEndpointTests | Post_QuestionNotInSession_Returns404SessionQuestionNotFound | |
| 97 | SubmitAnswerEndpointTests | Post_AnswerWrongShape_Returns422QuestionAnswerInvalid | `{"value":true}` for an Mcq |
| 98 | SubmitAnswerEndpointTests | Post_Anonymous_Returns401 | |
| 99 | FinishSessionEndpointTests | Post_AfterOneOfTwoAnswers_Returns200WithScoreAndPersists | `scorePercent` 50; `submittedAt` set; DB `SubmittedAt` and `ScorePercent` persisted |
| 100 | FinishSessionEndpointTests | Post_Twice_Returns200SameSubmittedAt | |
| 101 | FinishSessionEndpointTests | Post_AnswerAfterFinish_Returns400SessionAlreadySubmitted | |
| 102 | FinishSessionEndpointTests | Post_StartAfterFinish_StartsNewSession | new `id` differs from the finished one |
| 103 | FinishSessionEndpointTests | Post_OtherStudentsSession_Returns404 | |
| 104 | GetSessionEndpointTests | Get_AfterAnsweringFirst_ReturnsSavedProgress | `currentPosition` 2; `items[0].attempt` present; `items[1].attempt` and `items[1].correctAnswer` null |
| 105 | GetSessionEndpointTests | Get_OtherStudentsSession_Returns404 | |
| 106 | GetSessionEndpointTests | Get_Teacher_Returns403 | |
| 107 | GetSessionEndpointTests | Get_Anonymous_Returns401 | |
| 108 | AttemptAppendOnlyTests | Update_AttemptRow_RejectedByDatabase | `ExecuteSqlAsync` UPDATE → `PostgresException` `SqlState` "P0001"; row unchanged |
| 109 | AttemptAppendOnlyTests | Delete_AttemptRow_RejectedByDatabase | DELETE → P0001; row still present. There is no TRUNCATE test: it would wipe shared data if the trigger were missing |
| 110 | SessionPersistenceTests | Migrate_Attempts_HasStudentQuestionIndex | `SELECT indexdef FROM pg_indexes WHERE indexname = 'IX_Attempts_StudentId_QuestionId_CreatedAt'` contains `"StudentId", "QuestionId", "CreatedAt"` |
| 111 | SessionPersistenceTests | SaveChanges_SecondOpenSessionSameScope_ThrowsSessionAlreadyInProgress | two `StartQuiz` sessions for the same student and lesson added in separate scopes → the second save throws `ConflictCoreException` `SESSION_ALREADY_IN_PROGRESS` |
| 112 | SessionPersistenceTests | SaveChanges_ConcurrentFirstAnswers_ThrowsSessionQuestionAlreadyAnswered | the session is loaded in two scopes; each calls `RecordAttempt` on the same item; the first save passes; the second throws `ConflictCoreException` `SESSION_QUESTION_ALREADY_ANSWERED`; DB count 1 |
| 113 | SessionPersistenceTests | SaveChanges_SubmittedSessionSameScope_AllowsNewSession | submit the first, then add a second → saves; 2 rows |
| 114 | AppDbContextTests (existing) | Migrate_FreshDatabase_LeavesNoPendingMigrations | the list gains `_AddSessionsAndAttempts` |

## Definition of done
- [ ] `Session`, `SessionItem` and `Attempt` exist in `Elmanhg.Domain.Sessions` with exactly the fields in Files to create. Every PRD §15 Session and Attempt field is present (except `time_limit_min`, which is left to E6) with the names from Decision 20.
- [ ] The only way to create an attempt is `Session.RecordAttempt`. `Attempt` has no public mutator, and `Attempt.Id` and `SessionItem.Id` are `ValueGeneratedNever()`.
- [ ] Grading goes through `QuestionRevision.Grade` → `QuestionGrader.Grade` at the served `QuestionVersion`. Nothing grades from the live `Question` row (test 35, 52, 94).
- [ ] Serving uses `WhereServable` and `ServableQuestionSpecification.IsSatisfiedBy`. There is no second definition of servable (test 82).
- [ ] Migration `AddSessionsAndAttempts` creates the three tables, the indexes (`IX_Attempts_StudentId_QuestionId_CreatedAt`, `IX_Attempts_SessionId_QuestionId` unique, `IX_Sessions_InProgressScope` partial unique, `IX_Sessions_StudentId_StartedAt`, and the unique `(SessionId, Position)` and `(SessionId, QuestionId)` on `SessionItems`), and the `Attempts` append-only triggers, with a reversible `Down`. There are no destructive operations on existing tables.
- [ ] UPDATE and DELETE on `Attempts` are rejected by the database (tests 108–109).
- [ ] Double submit of the same answer returns the same attempt with one row. A different answer returns 409. A concurrent race maps to 409 through `AppDbContext` (tests 21–23, 92–93, 112).
- [ ] A refresh resumes: GET returns saved attempts and `currentPosition`, and a repeat start on the same lesson returns the open session (tests 84, 104). A concurrent double start maps to 409 (test 111).
- [ ] Every session lookup filters by `StudentId == currentUserId`. Another student's session returns 404, a Teacher gets 403, and anonymous gets 401 on every endpoint (tests 95, 103, 105, 89/106, 90/98/107).
- [ ] Time taken is `clamp(reported, 0, now − LastActivityAt)`, and `Resume` resets the clock (tests 17–19).
- [ ] Unanswered items in an open session expose no `CorrectAnswer` or `Explanation` (test 75, 104).
- [ ] Every new error constant has an en and an ar resx key; the Domain and Application groups are as tabled.
- [ ] `SessionsOptions` is bound with `ValidateOnStart`, has code defaults, and is added to `appsettings.example.json` and `ApiFactory`.
- [ ] All four actions carry `[Authorize(Policy = DefaultCodes.AssessmentsTake)]`, and `EndpointAuthorizationTests` passes.
- [ ] Session commands are not `IAuditableCommand`, and session entities are not `IAuditedEntity`.
- [ ] `QuestionAnswerRules` lives only in `Questions/Shared` (the old file is deleted), and grade-draft behaviour is unchanged (existing tests green).
- [ ] `api/openapi/v1.json` and `web/src/shared/api/generated/**` are regenerated and committed with no hand edits. Web typecheck, lint and tests are green.
- [ ] The Postman `Sessions` folder has 4 ordered requests, plus the `sessionId` and `sessionQuestionId` variables.
- [ ] Docs agree: new `docs/sessions.md`, the `docs/PRD.md` §15 Session, SessionItem and Attempt lines, and the `docs/audit-log.md` "Not audited" row.
- [ ] All 114 tests in the Test plan exist with those names and pass. `dotnet test api/ -c Release` is green with `appsettings.json` moved aside. `dotnet build` shows no new warnings, and `dotnet format --verify-no-changes` is clean outside `core-libraries`.
- [ ] The guard grep (`DateTime.Now/UtcNow`, `.Result`, `.Wait()`, `new HttpClient(`, `FromSqlRaw`, `async void`) prints nothing. Every file is at most about 100 lines, except the existing `AppDbContext` and the test classes.
- [ ] Morabh reuse: none. Morabh (`/home/user/apis`) has no quiz, session or attempt concept; its "attempts" are BNPL payment attempts and are not reusable. Every piece is new, and the append-only trigger follows Elmanhg #58 (`docs/audit-log.md`, adapted from Morabh `AuditLogs.md`).
