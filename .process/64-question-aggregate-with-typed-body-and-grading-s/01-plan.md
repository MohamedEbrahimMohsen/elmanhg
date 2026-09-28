# Plan — [E3.S1] Question aggregate with typed body and grading spec (#64)

## Goal
An admin can create a question of any v1 type (mcq, multi, trueFalse, fill, short) on a lesson through `POST /api/questions`, read it back through `GET /api/questions/{id}`, and edit it through `PUT /api/questions/{id}`. Each type has a documented JSON body (what the student sees) and grading spec (the answer key), stored as `jsonb` and validated per type. Every question starts as Pending at version 1. A content edit bumps the version and writes a `QuestionRevision` snapshot, and a content edit on an Approved question sends it back to Pending. Difficulty, objective and tag edits keep the status and the version. The only way a question can become Approved is `Question.Approve(TeacherSubject)`, so an admin cannot approve. Every mutation is audited. A lesson that has questions can no longer be deleted, and each lesson row in the admin content tree shows its question count.

## Scope
**In:**
- Domain: `Question` aggregate (typed enums, jsonb `Body` and `GradingSpec`, content/metadata split, `Version`, `ValidationStatus`, `ValidatedBy`, `ValidatedAt`, `Tags text[]`) and `QuestionRevision` child. Per-type schema records. `Question.Approve(TeacherSubject)` (domain only).
- Application: per-type schema rules (validate and normalise), a shared `QuestionFieldsValidator`, audited `CreateQuestion` and `UpdateQuestion`, and `GetQuestion`.
- #146 pickups: `Lesson.Delete` "has questions" guard (`LESSON_HAS_QUESTIONS`), and `LessonResult.QuestionCount` shown in the web lesson row.
- EF mapping, migration, options, resx, Postman, and regenerated OpenAPI and Orval output.
- Docs: new `docs/question-schemas.md`, plus `docs/PRD.md`, `docs/audit-log.md`, `docs/rich-text.md` and `docs/claude-design-prompt.md`.

**Out:**
- Question editor, preview, test grader, question list and edit-and-resubmit UI (#65). Bulk import (#66). Servable spec, retire and `retired_at` (#67). Approve/Reject commands and endpoints, `rejection_reason`, teacher queue and revision history reads (#68). Graders themselves (quiz engine).
- Moving a question to another lesson, and deleting a question.
- `LessonPublished`/`Unpublished`/`Archived` handlers from #146. Servable is derived at query time (#67), so there is nothing to recalculate here.

**Deferred:** none. Each item above maps to an existing story.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| D1 | Aggregate base | `Question : AuditEntity, IAuditedEntity` (partial across 3 files), namespace `Elmanhg.Domain.Questions`. `QuestionRevision : Entity` is not `IAuditedEntity`. | Mirrors `Lesson` (#61 D1). Revisions are an append-only history. The `Question` diff already shows the change, so auditing the snapshot would only duplicate it. |
| D2 | JSON storage | `Body` and `GradingSpec` are `string` properties with `HasColumnType("jsonb")`. The application writes a **canonical** serialisation of typed records (`QuestionJson.SerializerOptions`: Web defaults, camelCase enum strings, nulls omitted). Unknown input properties are dropped. Change detection uses `JsonNode.DeepEquals`, never string equality. | SKILL delta 2 allows "string with a documented shape" (`docs/question-schemas.md`). PostgreSQL re-formats jsonb text on read (key order, spaces), so string comparison would treat every save as a change: noisy audit diffs and false resets to Pending. |
| D3 | Where the schemas live | Schema records live in Domain (`Elmanhg.Domain.Questions.Schemas`), one file per question type. Validation and normalisation live in Application (`Questions/Shared/*QuestionRules`), dispatched by `QuestionSchemaRules`. | The graders (#65, the quiz engine) and import (#66) reuse the records and rules. One file per type is one contract, and it reads like `docs/question-schemas.md`. |
| D4 | Per-type shapes | See `docs/question-schemas.md` (Files #D). mcq/multi body `{options:[{id,text}]}`. mcq spec `{correctOptionId}`. multi spec `{correctOptionIds:[], partialCredit:false}`. trueFalse body `{}`, spec `{correctAnswer}`. fill body `{blanks:[{id}]}`, stem holds `[[id]]` exactly once per blank, spec `{blanks:[{id, acceptedAnswers:[]}], unifyLetterVariants:true}`. short body `{answerKind:"numeric"\|"text"}`. short spec is numeric `{value, tolerance, toleranceMode:"absolute"\|"percent"}` or text `{acceptedAnswers:[], unifyLetterVariants:true}`. Ids match `^[a-z0-9-]{1,20}$`. | PRD §6 table and §6.2 ("configurable per question, default on"). The body never carries an answer, so it can be sent to students as is. `partialCredit` defaults to false because PRD §19 Q3 is open, and false is the conservative choice. |
| D5 | Rich text inside questions | Stem, explanation and each choice option `text` are sanitised with the existing `IRichTextSanitizer` (same format as lessons). Fill and short accepted answers are plain trimmed text. | Physics options need LaTeX. The sanitiser and the `docs/rich-text.md` contract already exist. Answers are compared after normalisation (§6.2), so they must stay plain text. |
| D6 | Content vs metadata | **Content** = stem, body, grading spec, explanation, max score. **Metadata** = difficulty, objective, tags. Type is immutable: a changed type throws domain `QUESTION_TYPE_IMMUTABLE` (400). | PRD §5.3 names stem, options, answer and grading spec. The explanation and max score are also things the teacher validates and that change what a student sees or scores, so an edit to them must not keep the "validated by a teacher" label. The caller said difficulty, tag and objective edits keep the status. Changing the type would break past attempts. |
| D7 | Version and revision | Created at version 1 with revision v1. **Every** content edit, in any status, does `Version += 1` and appends a revision holding the snapshot of the **new** version. Only Approved → Pending (and it clears `ValidatedBy`/`ValidatedAt`). Metadata-only edits change neither the version nor the revisions. A no-op edit changes nothing and does not stamp `UpdationDate`. | PRD §5.4 "Incremented on content edit" and story sub-task "snapshot on every content edit". One revision row per version means an attempt's `question_version` always resolves to an exact snapshot. |
| D8 | Rejected + content edit | Stays Rejected in this story. `Update` resets only Approved. | "Edit and resubmit" is an explicit flow in #65, and Reject arrives in #68. Neither can be tested here. |
| D9 | "Admin cannot approve" structurally | (a) `ValidationStatus` has a private setter, and `Create` always sets Pending. (b) The only transition to Approved is `Question.Approve(TeacherSubject assignment)`. `TeacherSubject.Create` already throws `USER_NOT_TEACHER` for non-teachers. `Approve` also throws `QUESTION_VALIDATOR_NOT_ASSIGNED` when the assignment is soft-deleted or for another subject, and `QUESTION_NOT_PENDING` when the question is not Pending. (c) Commands and requests carry no status field. (d) No endpoint approves. `Questions.Validate` is Teacher-only (existing). | PRD §16 / §17 rule 3. The type system cannot produce an Approved question without a live teacher assignment for that subject. `Approve` is also needed so domain and integration tests can reach the Approved state. #68 adds the command. |
| D10 | Subject id | `SubjectId` is denormalised at create from the lesson's unit (`unit.SubjectId`) and never changes. FKs to `Lessons`, `Subjects` and `LessonObjectives` are all Restrict. `ValidatedBy` has no FK. | PRD §5.4 "subject_id (denormalised)" is needed for teacher scoping (#68). `ValidatedBy` mirrors `CreatedBy`/`UpdatedBy`, which have no FK. |
| D11 | Objective link | Optional. It must be a **live** objective of the question's lesson (loaded through `GetWithObjectivesAsync`, where the soft-delete filter applies). Otherwise domain `QUESTION_OBJECTIVE_NOT_IN_LESSON` (400). This is checked on create and on update. | PRD §5.4 "Links to one of the lesson's objectives". |
| D12 | Max score | `int`, required, 1..`Content:QuestionMaxScoreMax` (100). The "default 1" belongs to the editor (#65). | An optional score on update would silently reset it. Points are whole numbers, and partial scores belong to attempts. |
| D13 | Tags | `List<string>` mapped to PostgreSQL `text[]`. Trimmed, de-duplicated case-insensitively (first occurrence kept), order kept. Caps in options. | PRD §5.4 "tags (optional) free-form". Npgsql 10 maps primitive collections natively. |
| D14 | Command shape | `QuestionFields` record (every editable field) is nested in `CreateQuestionCommand(LessonId, Question)` and `UpdateQuestionCommand(QuestionId, Question)`, and validated once by `QuestionFieldsValidator` through `SetValidator`. The HTTP requests stay flat, and the controller builds `QuestionFields`. | One rule set for create, update and #66 import, with no duplicated validators. |
| D15 | Invalid JSON input | `QuestionSchemaReader.TryRead<T>` returns false for a non-object `JsonElement` or a `JsonException`, and the rule returns `QUESTION_BODY_INVALID` / `QUESTION_GRADING_SPEC_INVALID` (422). This is the only `catch` in the slice, and it catches only `JsonException`. | Malformed client JSON is expected input, so it becomes a validation code rather than a 500. `System.Text.Json` has no non-throwing deserialise. |
| D16 | Enum binding | Requests use `QuestionType?` and `QuestionDifficulty?` (MVC already has `JsonStringEnumConverter`, which reads case-insensitively). Missing → `*_REQUIRED` (422). Undefined number → `*_INVALID` (422). An unknown enum string is rejected by MVC model binding (400, framework shape). | Typed enums keep the OpenAPI and Orval types exact for #65. This mirrors how the rest of the API binds. |
| D17 | Reads | `GET /api/questions/{id}` returns `QuestionDetailResult`, where `Body` and `GradingSpec` are `JsonElement` (raw JSON on the wire). Policy `ContentManage` (Admin). No list endpoint. | Needed to verify create and update (Postman, and #65 edit). The list with filters is a #65 sub-task. Teacher reads are #68. |
| D18 | Routes and policies | Top-level `api/questions`: `POST` (body carries `lessonId`), `GET /{questionId}`, `PUT /{questionId}`. Every action uses `DefaultCodes.ContentManage`. No `ISubjectScopedRequest`. | Mirrors `LessonsController` (#61 D10). PRD §16: create/edit content is Admin only. |
| D19 | Lesson delete guard | `Lesson.Delete(bool hasQuestions, Guid deletedBy)`. The Published check runs first (`LESSON_IS_PUBLISHED`), then `hasQuestions` → `LESSON_HAS_QUESTIONS` (400). `DeleteLessonHandler` asks `IQuestionRepository.AnyInLessonAsync`. | Mirrors `CurriculumUnit.Delete(bool hasLessons, …)` and #146 item 1. |
| D20 | Question counts | `LessonResult` gains `int QuestionCount`, which counts every live question in any status, filled from `IQuestionRepository.CountByLessonAsync`. The web `LessonItem` shows a plural caption under the name, mirroring `UnitItem`'s lesson count. | #146 item 3. `GetLessons` is Admin-only (`ContentManage`), so counting Pending questions leaks nothing. |
| D21 | Concurrency | No row-version token in this story. | `Lesson` has none, and `CoreExceptionMiddleware` has no `DbUpdateConcurrencyException` mapping. #68's Approve must check the version the teacher reviewed (noted in `docs/question-schemas.md` "Versioning"). |
| D22 | Audit | `CreateQuestion` → `Question.Create` (id from the result). `UpdateQuestion` → `Question.Update` (command id). The diff lists the changed `Question` columns (including `version` and `validationStatus` on a content edit). Revisions are not diffed. | The caller said "audit every mutation". PRD §17 rule 13. |
| D23 | Caps | `ContentOptions` adds `QuestionStemMaxLength` 20000, `QuestionExplanationMaxLength` 20000, `QuestionOptionsMaxCount` 10, `QuestionOptionTextMaxLength` 2000, `QuestionBlanksMaxCount` 10, `QuestionAcceptedAnswersMaxCount` 20, `QuestionAnswerMaxLength` 200, `QuestionTagsMaxCount` 10, `QuestionTagMaxLength` 50, `QuestionMaxScoreMax` 100. These are invariants, not options: minimum 2 choice options, the id regex and the `[[id]]` placeholder format (named constants with a WHY comment). | SKILL §8.1 and constitution §0.3. |
| D24 | Morabh reuse | The CRUD slice shape follows Morabh `CRUD_FEATURE_CREATION_GUIDE.md` through the existing Elmanhg Lessons slice. Question aggregate, schemas, revisions and rules are new, with no Morabh equivalent (grep of Morabh for revision/snapshot/jsonb/JsonElement found nothing). | Reuse-first rule. |

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Domain/Lessons/Lesson.cs` | `Delete(Guid deletedBy)` → `Delete(bool hasQuestions, Guid deletedBy)`; see Domain behaviour. |
| `api/Elmanhg.Domain/SharedKernel/Exceptions/ErrorCodes.cs` | Under `// CONTENT` add `LessonHasQuestions = "LESSON_HAS_QUESTIONS"`. New group `// QUESTIONS`: `QuestionObjectiveNotInLesson`, `QuestionTypeImmutable`, `QuestionNotPending`, `QuestionValidatorNotAssigned` (values in Error codes). |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | New group `// QUESTIONS` after `// LESSONS`, with the 32 application constants in Error codes, in table order. |
| `api/Elmanhg.Application/Shared/Options/ContentOptions.cs` | Append the 10 `[Range(1, int.MaxValue)] public int …` properties from D23, in that order. |
| `api/Elmanhg.Application/Lessons/Shared/LessonResult.cs` | `public sealed record LessonResult(Guid Id, Guid UnitId, string Name, int Order, string State, int QuestionCount);` |
| `api/Elmanhg.Application/Lessons/Shared/LessonResultGenerator.cs` | `public static LessonResult Generate(Lesson lesson, int questionCount)` → `new LessonResult(lesson.Id, lesson.UnitId, lesson.Name, lesson.Order, lesson.State.ToString(), questionCount)`. `GenerateDetail` unchanged. |
| `api/Elmanhg.Application/Lessons/GetLessons/GetLessonsHandler.cs` | Constructor `(ICurriculumUnitRepository unitRepository, ILessonRepository lessonRepository, IQuestionRepository questionRepository)`. After `lessons`: `var questionCounts = await questionRepository.CountByLessonAsync(lessons.Select(x => x.Id).ToList(), cancellationToken).ConfigureAwait(false);` then `return lessons.Select(x => LessonResultGenerator.Generate(x, questionCounts.GetValueOrDefault(x.Id))).ToList();` |
| `api/Elmanhg.Application/Lessons/DeleteLesson/DeleteLessonHandler.cs` | Constructor `(ILessonRepository lessonRepository, IQuestionRepository questionRepository, ICurrentUserService currentUserService)`. After the 404 check: `var hasQuestions = await questionRepository.AnyInLessonAsync(lesson.Id, cancellationToken).ConfigureAwait(false);` then `lesson.Delete(hasQuestions, currentUserService.UserId.Value);`. |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | Add `public DbSet<Question> Questions { get; set; }` and `public DbSet<QuestionRevision> QuestionRevisions { get; set; }`. Call `ConfigureQuestions(modelBuilder);` right after `ConfigureLessons`. Add filter lines for `Question` and `QuestionRevision`. Mapping in Files #I1. |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | `services.AddScoped<IQuestionRepository, QuestionRepository>();` after the lesson repository. |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Regenerated. |
| `api/Elmanhg.Api/Resources/Messages.en.resx`, `Messages.ar.resx` | The 37 keys in Error codes. |
| `api/Elmanhg.Api/appsettings.example.json` | Append the 10 D23 keys to the `"Content"` object (same one-line style). Mirror them into the local gitignored `appsettings.json` if it exists (not committed). |
| `api/openapi/v1.json` | Regenerated by `dotnet build api/Elmanhg.Api`. |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | Add the 10 `["Content:Question…"] = "…"` in-memory keys (D23 values) after `Content:LessonImageMaxSizeInMb`. This keeps CI working without `appsettings.json`. |
| `api/Elmanhg.Tests/Domain/Lessons/LessonLifecycleTests.cs` | **modify**: the three `lesson.Delete(_actor)` calls (in `Delete_DraftWithObjectives_SoftDeletesLessonAndObjectives`, `Delete_Archived_SoftDeletes` and `Delete_Published_ThrowsLessonIsPublished`) become `lesson.Delete(false, _actor)`. No other change. Add row L1. |
| `api/Elmanhg.Tests/Application/Features/Lessons/DeleteLesson/DeleteLessonHandlerTests.cs` | **modify**: add `private readonly IQuestionRepository _questionRepository = Substitute.For<IQuestionRepository>();` and construct `new DeleteLessonHandler(_lessonRepository, _questionRepository, _currentUserService)`. Existing tests are otherwise unchanged (`AnyInLessonAsync` returns false by default). Add row L2. |
| `api/Elmanhg.Tests/Application/Features/Lessons/GetLessons/GetLessonsHandlerTests.cs` | **modify**: add an `IQuestionRepository` substitute to the constructor. In `Handle_ExistingUnit_ReturnsMappedLessons`, stub `_questionRepository.CountByLessonAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, int> { [first.Id] = 3 })` and expect `new LessonResult(first.Id, _unit.Id, "Newton's laws", 1, "Draft", 3)` and `new LessonResult(second.Id, _unit.Id, "Momentum", 2, "Draft", 0)`. |
| `api/Elmanhg.Tests/Integration/Content/LessonManagementEndpointTests.cs` | Add row I15. |
| `api/Elmanhg.Tests/Integration/Content/LessonsEndpointTests.cs` | Add row I16. |
| `postman/elmanhg.postman_collection.json` | New collection variable `questionId`. New folder "Questions" after "Lessons", Bearer like the others: (1) "Create lesson for questions": POST `/api/lessons` `{unitId:{{unitId}}, name:"Question bank"}`, which sets `lessonId`. (2) "Create question": POST `/api/questions` with the mcq sample from `docs/question-schemas.md`, test 200, sets `questionId`. (3) "Get question": GET `/api/questions/{{questionId}}`, test `validationStatus === "Pending"` and `version === 1`. (4) "Update question": PUT with a changed stem, test 200. |
| `docs/PRD.md` | §5.2: after "A published lesson cannot be deleted; move it to draft or archive it first." add "A lesson that has questions cannot be deleted." §5.3: replace the bold "Any edit…" bullet with: "**Any edit to an Approved question's content resets it to Pending.** Content is the stem, body (options, blanks), grading spec, explanation and max score. Edits to difficulty, objective link or tags alone change neither the status nor the version. Every content edit, in any status, increments `version` and writes a `QuestionRevision` snapshot of the new version; version 1 is snapshotted at creation. The type of a question never changes." §6: after the table add "Per-type `body` and `grading_spec` JSON shapes: `docs/question-schemas.md`." §15: `Question(…, objective_id?, tags[], body_json, …)`. |
| `docs/audit-log.md` | Add 2 rows (Audit table below). "Audited entities" gets `Question`. Replace "Validation commands (E3) and question commands join this table when they are built." with "Validation commands (E3) join this table when they are built. `QuestionRevision` rows are an append-only history and are not diffed." |
| `docs/rich-text.md` | Line 3: "…for lesson rich text (`Lesson.Explanation` and `Lesson.Summary`) and question rich text (`Question.Stem`, `Question.Explanation` and each choice option `text` inside `Question.Body`)." Storage: append "Question stem and explanation are stored in `text` columns, and choice option text inside the `jsonb` body, all sanitised the same way." |
| `docs/claude-design-prompt.md` | §4 Admin bullet: "`#/admin/content` subject, unit, lesson tree with reorder…" → "`#/admin/content` subject, unit, lesson tree (each lesson shows its question count) with reorder…". Nothing else changes. |
| `web/src/features/content/components/LessonItem.tsx` | After the first `<div>` (link and badge) insert `<p className="text-caption text-text-muted">{t('lessons.questionCount', { count: lesson.questionCount })}</p>`. |
| `web/src/features/content/components/LessonItem.test.tsx` | **modify** fixtures: `l1` `questionCount: 2`, `l2` `questionCount: 0`, `l3` `questionCount: 1`. Add rows W1–W3. |
| `web/src/features/content/components/UnitLessons.test.tsx` | **modify** fixtures only: both `lessons` entries get `questionCount: 0`. No assertion changes. |
| `web/src/features/content/i18n/en.json`, `ar.json` | `lessons.questionCount`: en `{count, plural, one {# question} other {# questions}}`, ar `{count, plural, zero {لا أسئلة} one {سؤال واحد} two {سؤالان} few {# أسئلة} many {# سؤالًا} other {# سؤال}}`. |
| `web/src/shared/i18n/en.json`, `ar.json` | `errors.LESSON_HAS_QUESTIONS` with the resx text. |
| `web/src/shared/api/generated/**` | `npm run gen:api` after the API build. Expect `questionCount` on `LessonResult` and a new `questions/` tag folder. Never hand-edited. |

## Files to create
Conventions (carried over from #60 and #61):
- Every command handler starts with the current-user guard (`UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated)`).
- Exactly one `SaveChangesAsync` per mutation.
- `.ConfigureAwait(false)` on every await, braces on every `if`, block-bodied methods.
- Application `ErrorCodes` = `Elmanhg.Application.Exceptions.ErrorCodes`. Domain `ErrorCodes` = `Elmanhg.Domain.SharedKernel.Exceptions.ErrorCodes`. The domain throws `BusinessRuleViolationCoreException`.
- All results are admin-facing plain strings. No `LocalizedText`.
- Every file stays at 100 lines or fewer.

### Domain (`api/Elmanhg.Domain/Questions/`, namespace `Elmanhg.Domain.Questions`)
| # | Path | Type | Contract |
|---|------|------|----------|
| D-1 | `QuestionType.cs` | enum | `public enum QuestionType { Mcq, Multi, TrueFalse, Fill, Short }` |
| D-2 | `QuestionDifficulty.cs` | enum | `public enum QuestionDifficulty { Easy, Medium, Hard }` |
| D-3 | `QuestionValidationStatus.cs` | enum | `public enum QuestionValidationStatus { Pending, Approved, Rejected }` |
| D-4 | `QuestionContent.cs` | value object | `public sealed record QuestionContent(string Stem, string Body, string GradingSpec, string Explanation, int MaxScore)` + `public bool IsEquivalentTo(QuestionContent other)` → `Stem == other.Stem && Explanation == other.Explanation && MaxScore == other.MaxScore && QuestionJson.AreEquivalent(Body, other.Body) && QuestionJson.AreEquivalent(GradingSpec, other.GradingSpec)` |
| D-5 | `QuestionMetadata.cs` | value object | `public sealed record QuestionMetadata(QuestionDifficulty Difficulty, Guid? ObjectiveId, IReadOnlyList<string> Tags);` |
| D-6 | `Question.cs` | aggregate | `public partial class Question : AuditEntity, IAuditedEntity`. Props (`{ get; private set; }`): `Guid LessonId`, `Guid SubjectId`, `QuestionType Type`, `string Stem = string.Empty`, `string Body = "{}"`, `string GradingSpec = "{}"`, `string Explanation = string.Empty`, `QuestionDifficulty Difficulty`, `Guid? ObjectiveId`, `List<string> Tags = []`, `int MaxScore`, `int Version`, `QuestionValidationStatus ValidationStatus`, `Guid? ValidatedBy`, `DateTimeOffset? ValidatedAt`, `List<QuestionRevision> Revisions = []`. `public QuestionContent CurrentContent => new(Stem, Body, GradingSpec, Explanation, MaxScore);` · `private Question(Guid id, Guid? createdBy) : base(id, createdBy) { }` · `public static Question Create(Lesson lesson, CurriculumUnit unit, QuestionType type, QuestionContent content, QuestionMetadata metadata, Guid createdBy)` · `private void ApplyContent(QuestionContent content)` · `private void ApplyMetadata(QuestionMetadata metadata)` · `private static void EnsureObjectiveInLesson(Lesson lesson, Guid? objectiveId)` |
| D-7 | `Question.Editing.cs` | partial | `public void Update(QuestionType type, QuestionContent content, QuestionMetadata metadata, Lesson lesson, Guid updatedBy)` |
| D-8 | `Question.Approval.cs` | partial | `public void Approve(TeacherSubject assignment)` |
| D-9 | `QuestionRevision.cs` | entity | `public class QuestionRevision : Entity`. Props (`private set`): `Guid QuestionId`, `int Version`, `string Snapshot = "{}"`, `Guid EditedBy`, `DateTimeOffset EditedAt`. `private QuestionRevision(Guid id) : base(id) { }` · `internal static QuestionRevision Create(Question question, Guid editedBy)` → `new QuestionRevision(Guid.NewGuid()) { QuestionId = question.Id, Version = question.Version, Snapshot = JsonSerializer.Serialize(new QuestionRevisionSnapshot(question.Type, question.Stem, JsonNode.Parse(question.Body), JsonNode.Parse(question.GradingSpec), question.Explanation, question.MaxScore), QuestionJson.SerializerOptions), EditedBy = editedBy, EditedAt = DateTimeOffset.UtcNow }` |
| D-10 | `QuestionRevisionSnapshot.cs` | record | `public sealed record QuestionRevisionSnapshot(QuestionType Type, string Stem, JsonNode? Body, JsonNode? GradingSpec, string Explanation, int MaxScore);` |
| D-11 | `IQuestionRepository.cs` | repo interface | `public interface IQuestionRepository : IRepository<Question> { Task<bool> AnyInLessonAsync(Guid lessonId, CancellationToken cancellationToken); Task<Dictionary<Guid, int>> CountByLessonAsync(IReadOnlyCollection<Guid> lessonIds, CancellationToken cancellationToken); }` |

### Domain schemas (`api/Elmanhg.Domain/Questions/Schemas/`, namespace `Elmanhg.Domain.Questions.Schemas`)
| # | Path | Contract |
|---|------|----------|
| D-12 | `QuestionJson.cs` | `public static class QuestionJson` · `public static JsonSerializerOptions SerializerOptions { get; } = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };` · `public static bool AreEquivalent(string left, string right)` → `JsonNode.DeepEquals(JsonNode.Parse(left), JsonNode.Parse(right))` |
| D-13 | `ChoiceSchemas.cs` | `public sealed record ChoiceOption(string? Id, string? Text);` `public sealed record ChoiceBody(List<ChoiceOption>? Options);` `public sealed record McqGradingSpec(string? CorrectOptionId);` `public sealed record MultiGradingSpec(List<string>? CorrectOptionIds, bool PartialCredit = false);` |
| D-14 | `TrueFalseSchemas.cs` | `public sealed record TrueFalseBody;` `public sealed record TrueFalseGradingSpec(bool? CorrectAnswer);` |
| D-15 | `FillSchemas.cs` | `public sealed record FillBlank(string? Id);` `public sealed record FillBody(List<FillBlank>? Blanks);` `public sealed record FillBlankAnswers(string? Id, List<string>? AcceptedAnswers);` `public sealed record FillGradingSpec(List<FillBlankAnswers>? Blanks, bool UnifyLetterVariants = true);` |
| D-16 | `ShortSchemas.cs` | `public enum ShortAnswerKind { Numeric, Text }` `public enum ToleranceMode { Absolute, Percent }` `public sealed record ShortBody(ShortAnswerKind? AnswerKind);` `public sealed record ShortGradingSpec(decimal? Value, decimal? Tolerance, ToleranceMode? ToleranceMode, List<string>? AcceptedAnswers, bool? UnifyLetterVariants);` |

### Application — shared (`api/Elmanhg.Application/Questions/Shared/`, namespace `Elmanhg.Application.Questions.Shared`)
| # | Path | Type | Contract |
|---|------|------|----------|
| A-1 | `QuestionFields.cs` | record | `public sealed record QuestionFields(QuestionType? Type, string? Stem, JsonElement Body, JsonElement GradingSpec, string? Explanation, QuestionDifficulty? Difficulty, Guid? ObjectiveId, IList<string> Tags, int? MaxScore);` |
| A-2 | `QuestionSchemaReader.cs` | static | `// Option and blank ids are referenced from grading specs and [[id]] stem placeholders; a narrow ASCII format keeps them stable and safe.` `private static readonly Regex IdPattern = new("^[a-z0-9-]{1,20}$", RegexOptions.NonBacktracking);` · `public static bool TryRead<T>(JsonElement element, [NotNullWhen(true)] out T? value) where T : class` → `value = null; if (element.ValueKind != JsonValueKind.Object) { return false; } try { value = element.Deserialize<T>(QuestionJson.SerializerOptions); } catch (JsonException) { return false; } return value is not null;` · `public static T Read<T>(JsonElement element) where T : class` → `element.Deserialize<T>(QuestionJson.SerializerOptions) ?? throw new InvalidOperationException("Question schema was not validated.");` · `public static bool IsValidId(string? id)` → `id is not null && IdPattern.IsMatch(id)` · `public static bool AreValidAcceptedAnswers(List<string>? answers, ContentOptions options)` → `answers is { Count: > 0 } && answers.Count <= options.QuestionAcceptedAnswersMaxCount && answers.All(x => !string.IsNullOrWhiteSpace(x) && x.Trim().Length <= options.QuestionAnswerMaxLength)` · `public static List<string> TrimAnswers(List<string>? answers)` → `(answers ?? []).Select(x => x.Trim()).ToList()` · `public static string Serialize<T>(T value)` → `JsonSerializer.Serialize(value, QuestionJson.SerializerOptions)` |
| A-3 | `ChoiceQuestionRules.cs` | static | `// A choice question with fewer than two options offers no choice.` `private const int MinimumOptions = 2;` · `public static List<string> Validate(JsonElement body, JsonElement gradingSpec, bool multiple, ContentOptions options)`: 1. `TryRead<ChoiceBody>` fails → add `QuestionBodyInvalid`. Spec read (`McqGradingSpec` when `!multiple`, else `MultiGradingSpec`) fails → add `QuestionGradingSpecInvalid`. If either failed, return. 2. `options = body.Options ?? []`. Count outside `MinimumOptions..options.QuestionOptionsMaxCount` → `QuestionOptionsCountInvalid`. 3. Any `!IsValidId(o.Id)` → `QuestionOptionIdInvalid`. 4. Ids not distinct (ordinal) → `QuestionOptionIdDuplicate`. 5. Any `IsNullOrWhiteSpace(o.Text)` → `QuestionOptionTextRequired`. 6. Any `o.Text.Length > options.QuestionOptionTextMaxLength` → `QuestionOptionTextTooLong`. 7. mcq: `CorrectOptionId` is not one of the ids → `QuestionCorrectOptionInvalid`. multi: `CorrectOptionIds` is null or empty, or any id is not an option id → `QuestionCorrectOptionInvalid`. Return the list (each code at most once). · `public static (string Body, string GradingSpec) Normalize(JsonElement body, JsonElement gradingSpec, bool multiple, IRichTextSanitizer sanitizer)`: body → `new ChoiceBody(read.Options!.Select(x => new ChoiceOption(x.Id, sanitizer.Sanitize(x.Text))).ToList())`; mcq → `new McqGradingSpec(spec.CorrectOptionId)`; multi → `new MultiGradingSpec(spec.CorrectOptionIds!.Distinct(StringComparer.Ordinal).ToList(), spec.PartialCredit)`; both `Serialize`. |
| A-4 | `TrueFalseQuestionRules.cs` | static | `Validate(JsonElement body, JsonElement gradingSpec)`: body not readable as `TrueFalseBody` → `QuestionBodyInvalid`. Spec not readable → `QuestionGradingSpecInvalid`. Readable with `CorrectAnswer is null` → `QuestionCorrectAnswerRequired`. · `Normalize(JsonElement gradingSpec)` → `(Serialize(new TrueFalseBody()), Serialize(new TrueFalseGradingSpec(Read<TrueFalseGradingSpec>(gradingSpec).CorrectAnswer)))` (body always `{}`). |
| A-5 | `FillQuestionRules.cs` | static | `// Blanks are placed in the stem as [[id]]; the student view replaces each marker with an input.` `public static string Placeholder(string id) => $"[[{id}]]";` · `Validate(string stem, JsonElement body, JsonElement gradingSpec, ContentOptions options)`: 1. Unreadable body/spec → codes and return. 2. `blanks = body.Blanks ?? []`. Count `< 1` or `> options.QuestionBlanksMaxCount` → `QuestionBlanksCountInvalid`. 3. Any invalid id → `QuestionBlankIdInvalid`. 4. Duplicate ids → `QuestionBlankIdDuplicate`. 5. For any valid blank id, `CountOccurrences(stem, Placeholder(id)) != 1` → `QuestionBlankPlaceholderMissing`. 6. `answers = spec.Blanks ?? []`. The set of answer ids is not equal to the set of blank ids, or the answer ids have duplicates → `QuestionBlankAnswersMismatch`. 7. Any answer entry where `!AreValidAcceptedAnswers(entry.AcceptedAnswers, options)` → `QuestionAcceptedAnswersInvalid`. `private static int CountOccurrences(string text, string value)` loops with `IndexOf(value, index, StringComparison.Ordinal)`. · `Normalize(JsonElement body, JsonElement gradingSpec)` → body `new FillBody(blanks.Select(x => new FillBlank(x.Id)).ToList())`; spec `new FillGradingSpec(blanks.Select(b => new FillBlankAnswers(b.Id, TrimAnswers(spec.Blanks!.Single(a => a.Id == b.Id).AcceptedAnswers))).ToList(), spec.UnifyLetterVariants)` (answers follow the body's blank order). |
| A-6 | `ShortQuestionRules.cs` | static | `Validate(JsonElement body, JsonElement gradingSpec, ContentOptions options)`: 1. Unreadable → codes and return (an unknown `answerKind` string is a `JsonException`, so it gives `QuestionBodyInvalid`). 2. `AnswerKind is null` → `QuestionAnswerKindRequired` and return. 3. Numeric: `Value is null` → `QuestionNumericValueRequired`. `Tolerance is null or < 0` or `ToleranceMode is null` → `QuestionToleranceInvalid`. 4. Text: `!AreValidAcceptedAnswers(spec.AcceptedAnswers, options)` → `QuestionAcceptedAnswersInvalid`. · `Normalize(JsonElement body, JsonElement gradingSpec)`: numeric → spec `new ShortGradingSpec(Value, Tolerance, ToleranceMode, null, null)`; text → `new ShortGradingSpec(null, null, null, TrimAnswers(spec.AcceptedAnswers), spec.UnifyLetterVariants ?? true)`; body `new ShortBody(kind)`. |
| A-7 | `QuestionSchemaRules.cs` | static | `public static List<string> Validate(QuestionFields fields, ContentOptions options)` → switch on `fields.Type`: `Mcq` → `ChoiceQuestionRules.Validate(fields.Body, fields.GradingSpec, multiple: false, options)`, `Multi` → `(…, multiple: true, …)`, `TrueFalse` → `TrueFalseQuestionRules.Validate(fields.Body, fields.GradingSpec)`, `Fill` → `FillQuestionRules.Validate(fields.Stem ?? string.Empty, fields.Body, fields.GradingSpec, options)`, `Short` → `ShortQuestionRules.Validate(fields.Body, fields.GradingSpec, options)`, `_` → `[]`. · `public static (string Body, string GradingSpec) Normalize(QuestionFields fields, IRichTextSanitizer sanitizer)` → the same switch over `Normalize`. `_` → `throw new InvalidOperationException("Unsupported question type.")`. |
| A-8 | `QuestionFieldsValidator.cs` | validator | `public sealed class QuestionFieldsValidator : AbstractValidator<QuestionFields>`, ctor `(IOptions<ContentOptions> contentOptions)`, `var options = contentOptions.Value;`. Rules: `Type.ValidateRequired(QuestionTypeRequired).IsInEnum().WithErrorCode(QuestionTypeInvalid)` · `Stem.ValidateRequired(QuestionStemRequired).ValidateMaxLength(options.QuestionStemMaxLength, QuestionStemTooLong)` · `Explanation.ValidateMaxLength(options.QuestionExplanationMaxLength, QuestionExplanationTooLong)` · `Difficulty.ValidateRequired(QuestionDifficultyRequired).IsInEnum().WithErrorCode(QuestionDifficultyInvalid)` · `MaxScore.ValidateRequired(QuestionMaxScoreRequired)` · `RuleFor(x => x.MaxScore.GetValueOrDefault()).ValidateRange(1, options.QuestionMaxScoreMax, QuestionMaxScoreInvalid).When(x => x.MaxScore.HasValue).OverridePropertyName(nameof(QuestionFields.MaxScore))` · `Tags.ValidateListMaxItems(options.QuestionTagsMaxCount, QuestionTagsTooMany)` · `RuleForEach(x => x.Tags).ValidateRequired(QuestionTagRequired).ValidateMaxLength(options.QuestionTagMaxLength, QuestionTagTooLong)` · `RuleFor(x => x).Custom((fields, context) => { foreach (var code in QuestionSchemaRules.Validate(fields, options)) { context.AddFailure(new ValidationFailure(nameof(QuestionFields.Body), code) { ErrorCode = code }); } }).When(x => x.Type.HasValue && Enum.IsDefined(x.Type.Value))` |
| A-9 | `QuestionContentFactory.cs` | static | `public static QuestionContent CreateContent(QuestionFields fields, IRichTextSanitizer sanitizer)` → `var (body, gradingSpec) = QuestionSchemaRules.Normalize(fields, sanitizer); return new QuestionContent(sanitizer.Sanitize(fields.Stem), body, gradingSpec, sanitizer.Sanitize(fields.Explanation), fields.MaxScore.GetValueOrDefault());` · `public static QuestionMetadata CreateMetadata(QuestionFields fields)` → `new QuestionMetadata(fields.Difficulty.GetValueOrDefault(), fields.ObjectiveId, fields.Tags.Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList())` |
| A-10 | `QuestionDetailResult.cs` | result (admin) | `public sealed record QuestionDetailResult(Guid Id, Guid LessonId, Guid SubjectId, string Type, string Stem, JsonElement Body, JsonElement GradingSpec, string Explanation, string Difficulty, Guid? ObjectiveId, List<string> Tags, int MaxScore, int Version, string ValidationStatus);` |
| A-11 | `QuestionResultGenerator.cs` | static | `public static QuestionDetailResult GenerateDetail(Question question)` → enums `.ToString()`, `Tags = question.Tags.ToList()`, `Body = ParseJson(question.Body)`, `GradingSpec = ParseJson(question.GradingSpec)`. `private static JsonElement ParseJson(string json) { using var document = JsonDocument.Parse(json); return document.RootElement.Clone(); }` |

### Application — use cases (`api/Elmanhg.Application/Questions/<UseCase>/`, namespace `Elmanhg.Application.Questions.<UseCase>`)
| # | Path | Type | Contract |
|---|------|------|----------|
| A-12 | `CreateQuestion/CreateQuestionCommand.cs` | command | `public sealed record CreateQuestionCommand(Guid LessonId, QuestionFields Question) : IRequest<CreateQuestionResult>, IAuditableCommand` · `AuditAction => "Question.Create"` · `AuditResourceType => "Question"` · `AuditResourceId => null` |
| A-13 | `CreateQuestion/CreateQuestionResult.cs` | result | `public sealed record CreateQuestionResult(Guid Id) : IAuditableResult { Guid? IAuditableResult.AuditResourceId => Id; }` |
| A-14 | `CreateQuestion/CreateQuestionValidator.cs` | validator | ctor `(IOptions<ContentOptions> contentOptions)`: `LessonId.ValidateRequired(ErrorCodes.LessonIdRequired)` · `RuleFor(x => x.Question).SetValidator(new QuestionFieldsValidator(contentOptions))` |
| A-15 | `CreateQuestion/CreateQuestionHandler.cs` | handler | `(ILessonRepository lessonRepository, ICurriculumUnitRepository unitRepository, IQuestionRepository questionRepository, IRichTextSanitizer richTextSanitizer, ICurrentUserService currentUserService)`. 1. User guard. 2. `lesson = lessonRepository.GetWithObjectivesAsync(request.LessonId, asNoTracking: true, ct)`. If null, throw `NotFoundCoreException(LessonNotFound)`. 3. `unit = unitRepository.GetByIdAsync(lesson.UnitId, ct, asNoTracking: true)`. If null, throw `NotFoundCoreException(UnitNotFound)`. 4. `content = QuestionContentFactory.CreateContent(request.Question, richTextSanitizer)`; `metadata = QuestionContentFactory.CreateMetadata(request.Question)`. 5. `question = Question.Create(lesson, unit, request.Question.Type.GetValueOrDefault(), content, metadata, userId)` (the domain may throw `QUESTION_OBJECTIVE_NOT_IN_LESSON`). 6. `questionRepository.AddAsync`. 7. `SaveChangesAsync`. 8. `return new CreateQuestionResult(question.Id)`. |
| A-16 | `UpdateQuestion/UpdateQuestionCommand.cs` | command | `public sealed record UpdateQuestionCommand(Guid QuestionId, QuestionFields Question) : IRequest, IAuditableCommand` · `"Question.Update"` · `"Question"` · `AuditResourceId => QuestionId` |
| A-17 | `UpdateQuestion/UpdateQuestionValidator.cs` | validator | ctor `(IOptions<ContentOptions> contentOptions)`: `QuestionId.ValidateRequired(ErrorCodes.QuestionIdRequired)` · `RuleFor(x => x.Question).SetValidator(new QuestionFieldsValidator(contentOptions))` |
| A-18 | `UpdateQuestion/UpdateQuestionHandler.cs` | handler | `(IQuestionRepository questionRepository, ILessonRepository lessonRepository, IRichTextSanitizer richTextSanitizer, ICurrentUserService currentUserService)`. 1. User guard. 2. `question = questionRepository.GetByIdAsync(request.QuestionId, ct)` (tracked, no include; new revisions are inserted through the navigation because of `ValueGeneratedNever`). If null, throw `NotFoundCoreException(QuestionNotFound)`. 3. `lesson = lessonRepository.GetWithObjectivesAsync(question.LessonId, asNoTracking: true, ct)`. If null, throw `NotFoundCoreException(LessonNotFound)`. 4. Content and metadata from the factory. 5. `question.Update(request.Question.Type.GetValueOrDefault(), content, metadata, lesson, userId)`. 6. `SaveChangesAsync`. |
| A-19 | `GetQuestion/GetQuestionQuery.cs` · `GetQuestionValidator.cs` · `GetQuestionHandler.cs` | query set | `public sealed record GetQuestionQuery(Guid QuestionId) : IRequest<QuestionDetailResult>;` · validator `QuestionId.ValidateRequired(QuestionIdRequired)` · handler `(IQuestionRepository questionRepository)`, no user guard: `GetByIdAsync(id, ct, asNoTracking: true)`. If null, throw `NotFoundCoreException(QuestionNotFound)`. Return `QuestionResultGenerator.GenerateDetail(question)`. |

**Audit** (goes into `docs/audit-log.md`):
| Command | AuditAction | ResourceType | Id source |
|---|---|---|---|
| CreateQuestion | `Question.Create` | Question | result |
| UpdateQuestion | `Question.Update` | Question | command (the diff lists the changed Question fields; a content edit shows `version`, and `validationStatus` when it resets) |

### Infrastructure
| # | Path | Contract |
|---|------|----------|
| I1 | `AppDbContext.cs` (modify): `ConfigureQuestions` | `modelBuilder.Entity<Question>(builder => { builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(EnumColumnMaxLength); builder.Property(x => x.Difficulty).HasConversion<string>().HasMaxLength(EnumColumnMaxLength); builder.Property(x => x.ValidationStatus).HasConversion<string>().HasMaxLength(EnumColumnMaxLength); builder.Property(x => x.Stem).IsRequired(); builder.Property(x => x.Body).IsRequired().HasColumnType("jsonb"); builder.Property(x => x.GradingSpec).IsRequired().HasColumnType("jsonb"); builder.Property(x => x.Explanation).IsRequired(); builder.Property(x => x.Tags).IsRequired(); builder.HasOne<Lesson>().WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Restrict); builder.HasOne<Subject>().WithMany().HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict); builder.HasOne<LessonObjective>().WithMany().HasForeignKey(x => x.ObjectiveId).OnDelete(DeleteBehavior.Restrict); builder.HasMany(x => x.Revisions).WithOne().HasForeignKey(x => x.QuestionId).OnDelete(DeleteBehavior.Restrict); builder.HasIndex(x => new { x.LessonId, x.ValidationStatus }); builder.HasIndex(x => new { x.SubjectId, x.ValidationStatus }); });` then `modelBuilder.Entity<QuestionRevision>(builder => { builder.Property(x => x.Id).ValueGeneratedNever(); builder.Property(x => x.Snapshot).IsRequired().HasColumnType("jsonb"); builder.HasIndex(x => new { x.QuestionId, x.Version }).IsUnique(); });` |
| I2 | `api/Elmanhg.Infrastructure/Questions/QuestionRepository.cs` | `public class QuestionRepository(AppDbContext context) : Repository<Question>(context), IQuestionRepository`. `AnyInLessonAsync` → `_dbSet.AnyAsync(x => x.LessonId == lessonId, ct)`. `CountByLessonAsync` → `_dbSet.Where(x => lessonIds.Contains(x.LessonId)).GroupBy(x => x.LessonId).Select(x => new { x.Key, Count = x.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct)` (same shape as `LessonRepository.CountByUnitAsync`). |
| I3–I4 | `api/Elmanhg.Infrastructure/Migrations/<timestamp>_AddQuestions.cs` + `.Designer.cs` | `dotnet ef migrations add AddQuestions -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`. Expect `CreateTable("Questions")` (`Body`/`GradingSpec` jsonb, `Tags` text[], 3 enum varchar(50), FKs `Lessons`/`Subjects`/`LessonObjectives` Restrict, 2 indexes) and `CreateTable("QuestionRevisions")` (`Snapshot` jsonb, FK `Questions` Restrict, unique `(QuestionId, Version)`). No Drop, Rename or AlterColumn. |

### API
| # | Path | Contract |
|---|------|----------|
| P1 | `api/Elmanhg.Api/Controllers/Questions/Requests.cs` | `public sealed record CreateQuestionRequest(Guid LessonId, QuestionType? Type, string? Stem, JsonElement Body, JsonElement GradingSpec, string? Explanation, QuestionDifficulty? Difficulty, Guid? ObjectiveId, List<string>? Tags, int? MaxScore);` · `public sealed record UpdateQuestionRequest(QuestionType? Type, string? Stem, JsonElement Body, JsonElement GradingSpec, string? Explanation, QuestionDifficulty? Difficulty, Guid? ObjectiveId, List<string>? Tags, int? MaxScore);` |
| P2 | `api/Elmanhg.Api/Controllers/Questions/QuestionsController.cs` | `[ApiController][Route("api/questions")][Authorize] public class QuestionsController(IMediator mediator) : ControllerBase` with the 3 actions in API surface. Each builds `new QuestionFields(request.Type, request.Stem, request.Body, request.GradingSpec, request.Explanation, request.Difficulty, request.ObjectiveId, request.Tags ?? [], request.MaxScore)` inline. |

### Tests (api)
| # | Path |
|---|------|
| T1 | `api/Elmanhg.Tests/Builders/QuestionBuilder.cs`: `public sealed class QuestionBuilder`. `Subject Subject` = `Subject.Create("Physics", 1, Guid.NewGuid())`. `CurriculumUnit Unit`, `Lesson Lesson` (created in the ctor; the lesson gets one objective through `Lesson.Update(… [new LessonObjectiveContent(null, "State the first law")] …)`). `Guid ObjectiveId => Lesson.Objectives[0].Id`. `WithMetadata(QuestionMetadata)`, `Approved()`, `Build()` → `Question.Create(Lesson, Unit, QuestionType.Mcq, McqContent(), metadata ?? new(QuestionDifficulty.Medium, null, []), Guid.NewGuid())`, then when approved `question.Approve(TeacherSubject.Create(User.CreateTeacher("Teacher", "teacher@example.com"), Subject, Guid.NewGuid()))`. `public static QuestionContent McqContent()` → `new("<p>2 + 2 = ?</p>", """{"options":[{"id":"a","text":"3"},{"id":"b","text":"4"}]}""", """{"correctOptionId":"b"}""", "<p>Add the numbers.</p>", 1)`. `public static JsonElement Json(string json)` → `JsonDocument.Parse(json).RootElement.Clone()` (use a `using`). |
| T2–T3 | `api/Elmanhg.Tests/Domain/Questions/QuestionTests.cs`, `QuestionApprovalTests.cs` |
| T4–T9 | `api/Elmanhg.Tests/Application/Features/Questions/Shared/{ChoiceQuestionRules,TrueFalseQuestionRules,FillQuestionRules,ShortQuestionRules,QuestionFieldsValidator,QuestionContentFactory}Tests.cs` |
| T10–T15 | `api/Elmanhg.Tests/Application/Features/Questions/{CreateQuestion,UpdateQuestion,GetQuestion}/<UseCase>{Handler,Validator}Tests.cs` |
| T16 | `api/Elmanhg.Tests/Integration/Content/QuestionTestData.cs`: `static Task<Guid> SeedQuestionAsync(ApiFactory factory, Guid lessonId, bool approved, CancellationToken cancellationToken)` loads the lesson with objectives, its unit and its subject, then `Question.Create(…, QuestionBuilder.McqContent(), new(QuestionDifficulty.Medium, null, []), creator)`, and when approved also `Approve(TeacherSubject.Create(User.CreateTeacher("Teacher", $"{Guid.NewGuid():N}@example.com"), subject, creator))` (in memory only, never saved). Then `context.Questions.Add`, save, return the id. `static Task<Question> ReadQuestionAsync(ApiFactory, Guid, CancellationToken)` → `context.Questions.Include(x => x.Revisions).AsNoTracking().SingleAsync(…)`. |
| T17 | `api/Elmanhg.Tests/Integration/Content/QuestionsEndpointTests.cs`. Helpers mirror `LessonsEndpointTests`: `AdminClientAsync`, `TeacherClientAsync`, `SeedLessonAsync` (subject, unit and lesson with one objective through `ContentTestData.SeedLessonAsync`), `ReadCodeAsync`, and `static object McqRequest(Guid lessonId, string stem = "<p>2 + 2 = ?</p>")` (anonymous object in the D4 shape, `difficulty = "Medium"`, `maxScore = 1`, `tags = new[] { "arithmetic" }`). |

Validator and rule tests use `Options.Create(new ContentOptions { … })` with every #60/#61 cap plus the D23 values, except where a row states a limit. Handler tests use NSubstitute for the repositories, `IRichTextSanitizer` (`Sanitize(Arg.Any<string?>())` returns `x => $"clean:{x.Arg<string?>()}"`) and `ICurrentUserService`.

### Docs
| # | Path | Contract |
|---|------|----------|
| DOC1 | `docs/question-schemas.md` | Title "Question schemas". **Types** (the 5 wire names `Mcq Multi TrueFalse Fill Short` and PRD §6 grading, one line each). **Body vs grading spec** (the body is student-visible and never holds an answer; the grading spec is server-only). **Per-type shapes**: one canonical JSON example of body and spec for each type, exactly as in D4, including numeric and text short answers and the `[[id]]` fill placeholder. **Rules** (every rule of A-3…A-6 with its error code; the id format; the D23 caps by config key). **Canonical storage** (jsonb; unknown properties dropped; option text sanitised per `docs/rich-text.md`; answers trimmed; equality is semantic JSON equality). **Versioning** (D6, D7, D8 in prose; revision snapshot shape = `QuestionRevisionSnapshot`; D21 note: an approval must name the version the teacher reviewed). **Validation status** (D9: created Pending; only `Approve(TeacherSubject)` approves; admins cannot). **Changing a schema** (records, rules, this doc and `web` editor change together; existing rows need a data migration). |

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `QuestionNotFound` | `QUESTION_NOT_FOUND` | Update/GetQuestion handlers | `NotFoundCoreException` | 404 |
| `QuestionIdRequired` | `QUESTION_ID_REQUIRED` | Update/GetQuestion validators | validation | 422 |
| `QuestionTypeRequired` | `QUESTION_TYPE_REQUIRED` | QuestionFieldsValidator | validation | 422 |
| `QuestionTypeInvalid` | `QUESTION_TYPE_INVALID` | QuestionFieldsValidator | validation | 422 |
| `QuestionStemRequired` | `QUESTION_STEM_REQUIRED` | QuestionFieldsValidator | validation | 422 |
| `QuestionStemTooLong` | `QUESTION_STEM_TOO_LONG` | QuestionFieldsValidator | validation | 422 |
| `QuestionExplanationTooLong` | `QUESTION_EXPLANATION_TOO_LONG` | QuestionFieldsValidator | validation | 422 |
| `QuestionDifficultyRequired` | `QUESTION_DIFFICULTY_REQUIRED` | QuestionFieldsValidator | validation | 422 |
| `QuestionDifficultyInvalid` | `QUESTION_DIFFICULTY_INVALID` | QuestionFieldsValidator | validation | 422 |
| `QuestionMaxScoreRequired` | `QUESTION_MAX_SCORE_REQUIRED` | QuestionFieldsValidator | validation | 422 |
| `QuestionMaxScoreInvalid` | `QUESTION_MAX_SCORE_INVALID` | QuestionFieldsValidator | validation | 422 |
| `QuestionTagsTooMany` | `QUESTION_TAGS_TOO_MANY` | QuestionFieldsValidator | validation | 422 |
| `QuestionTagRequired` | `QUESTION_TAG_REQUIRED` | QuestionFieldsValidator | validation | 422 |
| `QuestionTagTooLong` | `QUESTION_TAG_TOO_LONG` | QuestionFieldsValidator | validation | 422 |
| `QuestionBodyInvalid` | `QUESTION_BODY_INVALID` | all type rules | validation | 422 |
| `QuestionGradingSpecInvalid` | `QUESTION_GRADING_SPEC_INVALID` | all type rules | validation | 422 |
| `QuestionOptionsCountInvalid` | `QUESTION_OPTIONS_COUNT_INVALID` | ChoiceQuestionRules | validation | 422 |
| `QuestionOptionIdInvalid` | `QUESTION_OPTION_ID_INVALID` | ChoiceQuestionRules | validation | 422 |
| `QuestionOptionIdDuplicate` | `QUESTION_OPTION_ID_DUPLICATE` | ChoiceQuestionRules | validation | 422 |
| `QuestionOptionTextRequired` | `QUESTION_OPTION_TEXT_REQUIRED` | ChoiceQuestionRules | validation | 422 |
| `QuestionOptionTextTooLong` | `QUESTION_OPTION_TEXT_TOO_LONG` | ChoiceQuestionRules | validation | 422 |
| `QuestionCorrectOptionInvalid` | `QUESTION_CORRECT_OPTION_INVALID` | ChoiceQuestionRules | validation | 422 |
| `QuestionCorrectAnswerRequired` | `QUESTION_CORRECT_ANSWER_REQUIRED` | TrueFalseQuestionRules | validation | 422 |
| `QuestionBlanksCountInvalid` | `QUESTION_BLANKS_COUNT_INVALID` | FillQuestionRules | validation | 422 |
| `QuestionBlankIdInvalid` | `QUESTION_BLANK_ID_INVALID` | FillQuestionRules | validation | 422 |
| `QuestionBlankIdDuplicate` | `QUESTION_BLANK_ID_DUPLICATE` | FillQuestionRules | validation | 422 |
| `QuestionBlankPlaceholderMissing` | `QUESTION_BLANK_PLACEHOLDER_MISSING` | FillQuestionRules | validation | 422 |
| `QuestionBlankAnswersMismatch` | `QUESTION_BLANK_ANSWERS_MISMATCH` | FillQuestionRules | validation | 422 |
| `QuestionAcceptedAnswersInvalid` | `QUESTION_ACCEPTED_ANSWERS_INVALID` | Fill/ShortQuestionRules | validation | 422 |
| `QuestionAnswerKindRequired` | `QUESTION_ANSWER_KIND_REQUIRED` | ShortQuestionRules | validation | 422 |
| `QuestionNumericValueRequired` | `QUESTION_NUMERIC_VALUE_REQUIRED` | ShortQuestionRules | validation | 422 |
| `QuestionToleranceInvalid` | `QUESTION_TOLERANCE_INVALID` | ShortQuestionRules | validation | 422 |
| `LessonIdRequired` (existing) | `LESSON_ID_REQUIRED` | CreateQuestionValidator | validation | 422 |
| `LessonNotFound` (existing) | `LESSON_NOT_FOUND` | Create/UpdateQuestion handlers | `NotFoundCoreException` | 404 |
| `UnitNotFound` (existing) | `UNIT_NOT_FOUND` | CreateQuestionHandler | `NotFoundCoreException` | 404 |
| Domain `QuestionObjectiveNotInLesson` | `QUESTION_OBJECTIVE_NOT_IN_LESSON` | `Question.Create/Update` | `BusinessRuleViolationCoreException` | 400 |
| Domain `QuestionTypeImmutable` | `QUESTION_TYPE_IMMUTABLE` | `Question.Update` | `BusinessRuleViolationCoreException` | 400 |
| Domain `QuestionNotPending` | `QUESTION_NOT_PENDING` | `Question.Approve` | `BusinessRuleViolationCoreException` | 400 |
| Domain `QuestionValidatorNotAssigned` | `QUESTION_VALIDATOR_NOT_ASSIGNED` | `Question.Approve` | `BusinessRuleViolationCoreException` | 400 |
| Domain `LessonHasQuestions` | `LESSON_HAS_QUESTIONS` | `Lesson.Delete` | `BusinessRuleViolationCoreException` | 400 |

Resx (en | ar, no tashkeel, same spelling style as the existing ar.resx):
- QUESTION_NOT_FOUND: Question not found. | السؤال غير موجود.
- QUESTION_ID_REQUIRED: Choose a question. | اختر السؤال.
- QUESTION_TYPE_REQUIRED: Choose the question type. | اختر نوع السؤال.
- QUESTION_TYPE_INVALID: This question type is not supported. | نوع السؤال غير مدعوم.
- QUESTION_STEM_REQUIRED: Enter the question text. | اكتب نص السؤال.
- QUESTION_STEM_TOO_LONG: The question text is too long. | نص السؤال طويل جدا.
- QUESTION_EXPLANATION_TOO_LONG: The explanation is too long. | الشرح طويل جدا.
- QUESTION_DIFFICULTY_REQUIRED: Choose the difficulty. | اختر مستوى الصعوبة.
- QUESTION_DIFFICULTY_INVALID: Choose easy, medium or hard. | اختر سهل او متوسط او صعب.
- QUESTION_MAX_SCORE_REQUIRED: Enter the question's points. | اكتب درجة السؤال.
- QUESTION_MAX_SCORE_INVALID: The points are outside the allowed range. | الدرجة خارج النطاق المسموح.
- QUESTION_TAGS_TOO_MANY: This question has too many tags. | عدد وسوم السؤال اكبر من المسموح.
- QUESTION_TAG_REQUIRED: A tag cannot be empty. | لا يمكن ان يكون الوسم فارغا.
- QUESTION_TAG_TOO_LONG: A tag is too long. | احد الوسوم طويل جدا.
- QUESTION_BODY_INVALID: The question content does not match its type. | محتوى السؤال لا يطابق نوعه.
- QUESTION_GRADING_SPEC_INVALID: The answer key does not match the question type. | مفتاح الاجابة لا يطابق نوع السؤال.
- QUESTION_OPTIONS_COUNT_INVALID: Add at least two options, within the allowed number. | اضف خيارين على الاقل وبما لا يتجاوز العدد المسموح.
- QUESTION_OPTION_ID_INVALID: An option has an invalid id. | احد الخيارات له معرف غير صالح.
- QUESTION_OPTION_ID_DUPLICATE: Two options share the same id. | يوجد خياران بنفس المعرف.
- QUESTION_OPTION_TEXT_REQUIRED: Every option needs text. | كل خيار يحتاج الى نص.
- QUESTION_OPTION_TEXT_TOO_LONG: An option is too long. | احد الخيارات طويل جدا.
- QUESTION_CORRECT_OPTION_INVALID: Mark the correct answer among the options. | حدد الاجابة الصحيحة من بين الخيارات.
- QUESTION_CORRECT_ANSWER_REQUIRED: Choose whether the statement is true or false. | حدد ما اذا كانت العبارة صحيحة ام خاطئة.
- QUESTION_BLANKS_COUNT_INVALID: Add at least one blank, within the allowed number. | اضف فراغا واحدا على الاقل وبما لا يتجاوز العدد المسموح.
- QUESTION_BLANK_ID_INVALID: A blank has an invalid id. | احد الفراغات له معرف غير صالح.
- QUESTION_BLANK_ID_DUPLICATE: Two blanks share the same id. | يوجد فراغان بنفس المعرف.
- QUESTION_BLANK_PLACEHOLDER_MISSING: Place each blank exactly once in the question text. | ضع كل فراغ مرة واحدة فقط في نص السؤال.
- QUESTION_BLANK_ANSWERS_MISMATCH: Give accepted answers for every blank, and only for those blanks. | اكتب الاجابات المقبولة لكل فراغ ولهذه الفراغات فقط.
- QUESTION_ACCEPTED_ANSWERS_INVALID: Accepted answers are missing, empty, too long or too many. | الاجابات المقبولة ناقصة او فارغة او طويلة جدا او كثيرة جدا.
- QUESTION_ANSWER_KIND_REQUIRED: Choose a numeric or a text answer. | اختر اجابة رقمية او نصية.
- QUESTION_NUMERIC_VALUE_REQUIRED: Enter the correct number. | اكتب الرقم الصحيح.
- QUESTION_TOLERANCE_INVALID: Enter a tolerance of zero or more and choose absolute or percent. | اكتب هامش خطا صفرا او اكثر واختر قيمة مطلقة او نسبة مئوية.
- QUESTION_OBJECTIVE_NOT_IN_LESSON: This objective does not belong to the question's lesson. | هذا الهدف لا ينتمي الى درس السؤال.
- QUESTION_TYPE_IMMUTABLE: A question's type cannot be changed. Create a new question instead. | لا يمكن تغيير نوع السؤال. انشئ سؤالا جديدا بدلا من ذلك.
- QUESTION_NOT_PENDING: Only a pending question can be approved. | لا يمكن اعتماد الا سؤال قيد المراجعة.
- QUESTION_VALIDATOR_NOT_ASSIGNED: Only a teacher assigned to this subject can approve its questions. | لا يعتمد اسئلة هذه المادة الا معلم مكلف بها.
- LESSON_HAS_QUESTIONS: This lesson has questions and cannot be deleted. | لا يمكن حذف الدرس لانه يحتوي على اسئلة.

Web `shared/i18n/{en,ar}.json` `errors.*`: add only `LESSON_HAS_QUESTIONS` (the question codes arrive with the #65 editor).

## Domain behaviour
```csharp
// Question.cs
public static Question Create(Lesson lesson, CurriculumUnit unit, QuestionType type, QuestionContent content, QuestionMetadata metadata, Guid createdBy)
{
    EnsureObjectiveInLesson(lesson, metadata.ObjectiveId);
    var question = new Question(Guid.NewGuid(), createdBy) { LessonId = lesson.Id, SubjectId = unit.SubjectId, Type = type, Version = 1, ValidationStatus = QuestionValidationStatus.Pending };
    question.ApplyContent(content);
    question.ApplyMetadata(metadata);
    question.Revisions.Add(QuestionRevision.Create(question, createdBy));
    return question;
}

private void ApplyContent(QuestionContent content)   // assign only what differs, so EF/audit never see a false change
{
    if (Stem != content.Stem) { Stem = content.Stem; }
    if (!QuestionJson.AreEquivalent(Body, content.Body)) { Body = content.Body; }
    if (!QuestionJson.AreEquivalent(GradingSpec, content.GradingSpec)) { GradingSpec = content.GradingSpec; }
    if (Explanation != content.Explanation) { Explanation = content.Explanation; }
    if (MaxScore != content.MaxScore) { MaxScore = content.MaxScore; }
}

private void ApplyMetadata(QuestionMetadata metadata)
{
    Difficulty = metadata.Difficulty;
    ObjectiveId = metadata.ObjectiveId;
    if (!Tags.SequenceEqual(metadata.Tags)) { Tags = metadata.Tags.ToList(); }
}

private static void EnsureObjectiveInLesson(Lesson lesson, Guid? objectiveId)
{
    if (objectiveId is not null && lesson.Objectives.All(x => x.Id != objectiveId)) { throw new BusinessRuleViolationCoreException(ErrorCodes.QuestionObjectiveNotInLesson); }
}

// Question.Editing.cs
public void Update(QuestionType type, QuestionContent content, QuestionMetadata metadata, Lesson lesson, Guid updatedBy)
{
    if (type != Type) { throw new BusinessRuleViolationCoreException(ErrorCodes.QuestionTypeImmutable); }
    EnsureObjectiveInLesson(lesson, metadata.ObjectiveId);

    var contentChanged = !CurrentContent.IsEquivalentTo(content);
    var metadataChanged = Difficulty != metadata.Difficulty || ObjectiveId != metadata.ObjectiveId || !Tags.SequenceEqual(metadata.Tags);
    if (!contentChanged && !metadataChanged) { return; }

    ApplyMetadata(metadata);
    if (contentChanged)
    {
        ApplyContent(content);
        Version += 1;
        if (ValidationStatus == QuestionValidationStatus.Approved)
        {
            ValidationStatus = QuestionValidationStatus.Pending;
            ValidatedBy = null;
            ValidatedAt = null;
        }

        Revisions.Add(QuestionRevision.Create(this, updatedBy));
    }

    UpdatedBy = updatedBy;
    UpdationDate = DateTimeOffset.UtcNow;
}

// Question.Approval.cs
public void Approve(TeacherSubject assignment)
{
    if (ValidationStatus != QuestionValidationStatus.Pending) { throw new BusinessRuleViolationCoreException(ErrorCodes.QuestionNotPending); }
    if (assignment.IsDeleted || assignment.SubjectId != SubjectId) { throw new BusinessRuleViolationCoreException(ErrorCodes.QuestionValidatorNotAssigned); }
    var now = DateTimeOffset.UtcNow;
    ValidationStatus = QuestionValidationStatus.Approved;
    ValidatedBy = assignment.TeacherId;
    ValidatedAt = now;
    UpdatedBy = assignment.TeacherId;
    UpdationDate = now;
}

// Lesson.cs
public void Delete(bool hasQuestions, Guid deletedBy)
{
    if (State == LessonState.Published) { throw new BusinessRuleViolationCoreException(ErrorCodes.LessonIsPublished); }
    if (hasQuestions) { throw new BusinessRuleViolationCoreException(ErrorCodes.LessonHasQuestions); }
    // unchanged: delete objectives, SoftDelete(), UpdatedBy, UpdationDate
}
```
In real code, braces go on their own lines (the style guide). The code above is compressed for the plan.

## API surface
| Method | Route | Policy | Request | Response |
|---|---|---|---|---|
| POST | `/api/questions` (Name `CreateQuestion`) | `DefaultCodes.ContentManage` | `CreateQuestionRequest` | `200 CreateQuestionResult { id }` |
| GET | `/api/questions/{questionId:guid}` (Name `GetQuestion`) | `DefaultCodes.ContentManage` | — | `200 QuestionDetailResult` |
| PUT | `/api/questions/{questionId:guid}` (Name `UpdateQuestion`) | `DefaultCodes.ContentManage` | `UpdateQuestionRequest` | `200` (no body) |
| GET | `/api/lessons?unitId=` (existing) | unchanged | — | `LessonResult` now has `questionCount` |
| DELETE | `/api/lessons/{lessonId}` (existing) | unchanged | — | new `400 LESSON_HAS_QUESTIONS` |

Each new action has `[ProducesResponseType<…>(StatusCodes.Status200OK)]` like `LessonsController`.

## Test plan
### Domain
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| D1 | QuestionTests | `Create_ValidInput_IsPendingVersionOneWithSubjectFromUnit` | Pending, `Version` 1, `SubjectId == unit.SubjectId`, `LessonId`, `Type`, content and metadata fields copied, `ValidatedBy` null |
| D2 | QuestionTests | `Create_Always_AddsVersionOneRevisionSnapshot` | one revision, `Version` 1, `EditedBy` = creator; the snapshot JSON `stem`, `type` (`"mcq"`) and `body.options[1].text` match |
| D3 | QuestionTests | `Create_ObjectiveOfLesson_LinksObjective` | `ObjectiveId == builder.ObjectiveId` |
| D4 | QuestionTests | `Create_ObjectiveNotInLesson_ThrowsQuestionObjectiveNotInLesson` | exception type and code |
| D5 | QuestionTests | `Update_ContentChangeOnApproved_ResetsToPendingAndClearsValidation` | Pending, `ValidatedBy`/`ValidatedAt` null |
| D6 | QuestionTests | `Update_ContentChangeOnApproved_IncrementsVersionAndAddsRevision` | `Version` 2, 2 revisions, the last one `Version` 2 with the new stem in its snapshot |
| D7 | QuestionTests | `Update_ContentChangeOnPending_IncrementsVersionAndStaysPending` | Pending, `Version` 2, 2 revisions |
| D8 | QuestionTests | `Update_EachContentField_CountsAsContentChange` (`[Theory]` `"stem"`, `"body"`, `"gradingSpec"`, `"explanation"`, `"maxScore"`) | approved question → Pending and `Version` 2 |
| D9 | QuestionTests | `Update_MetadataOnlyOnApproved_KeepsApprovedAndVersion` | difficulty, objective and tags changed; still Approved, `Version` 1, fields updated |
| D10 | QuestionTests | `Update_MetadataOnly_AddsNoRevision` | `Revisions` count 1 |
| D11 | QuestionTests | `Update_EquivalentJsonWithDifferentFormatting_IsNotAContentChange` | body/spec with reordered keys and whitespace → `Version` 1, the `Body` string is unchanged (reference-equal to before), Approved kept |
| D12 | QuestionTests | `Update_NothingChanged_DoesNotStampUpdate` | `UpdationDate` and `UpdatedBy` unchanged |
| D13 | QuestionTests | `Update_TypeChanged_ThrowsQuestionTypeImmutable` | code; `Version` still 1 |
| D14 | QuestionTests | `Update_ObjectiveNotInLesson_ThrowsQuestionObjectiveNotInLesson` | code |
| D15 | QuestionTests | `Update_TwoContentEdits_ReachesVersionThreeWithThreeRevisions` | `Version` 3, revision versions `[1,2,3]` |
| D16 | QuestionTests | `Update_ContentChange_SetsUpdatedByAndUpdationDate` | `UpdatedBy` = editor, `UpdationDate` later than before |
| A1 | QuestionApprovalTests | `Approve_AssignedTeacherOnPending_SetsApprovedAndValidator` | Approved, `ValidatedBy == assignment.TeacherId`, `ValidatedAt` set |
| A2 | QuestionApprovalTests | `Approve_AssignmentForOtherSubject_ThrowsQuestionValidatorNotAssigned` | code; still Pending |
| A3 | QuestionApprovalTests | `Approve_UnassignedAssignment_ThrowsQuestionValidatorNotAssigned` | `assignment.Unassign(…)` first; code |
| A4 | QuestionApprovalTests | `Approve_AlreadyApproved_ThrowsQuestionNotPending` | code |
| A5 | QuestionApprovalTests | `Approve_AssignmentForAdmin_CannotBeCreated` | `TeacherSubject.Create(User.CreateAdmin(…), subject, …)` throws `USER_NOT_TEACHER` (documents D9's structural guard) |
| L1 | LessonLifecycleTests | `Delete_HasQuestions_ThrowsLessonHasQuestions` | code; `IsDeleted` false |

### Application — rules and validators
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| C1 | ChoiceQuestionRulesTests | `Validate_ValidMcq_ReturnsNoErrors` | empty |
| C2 | ChoiceQuestionRulesTests | `Validate_ValidMulti_ReturnsNoErrors` | empty |
| C3 | ChoiceQuestionRulesTests | `Validate_BodyNotAnObject_ReturnsQuestionBodyInvalid` | body `[]` → contains code |
| C4 | ChoiceQuestionRulesTests | `Validate_GradingSpecWrongShape_ReturnsQuestionGradingSpecInvalid` | spec `{"correctOptionId":5}` → code |
| C5 | ChoiceQuestionRulesTests | `Validate_OneOption_ReturnsQuestionOptionsCountInvalid` | code |
| C6 | ChoiceQuestionRulesTests | `Validate_MoreOptionsThanCap_ReturnsQuestionOptionsCountInvalid` | cap 3, 4 options → code |
| C7 | ChoiceQuestionRulesTests | `Validate_OptionIdWithUppercase_ReturnsQuestionOptionIdInvalid` | id `"A"` → code |
| C8 | ChoiceQuestionRulesTests | `Validate_DuplicateOptionIds_ReturnsQuestionOptionIdDuplicate` | code |
| C9 | ChoiceQuestionRulesTests | `Validate_BlankOptionText_ReturnsQuestionOptionTextRequired` | code |
| C10 | ChoiceQuestionRulesTests | `Validate_OptionTextOverCap_ReturnsQuestionOptionTextTooLong` | cap 5 → code |
| C11 | ChoiceQuestionRulesTests | `Validate_McqCorrectOptionUnknown_ReturnsQuestionCorrectOptionInvalid` | code |
| C12 | ChoiceQuestionRulesTests | `Validate_MultiNoCorrectOptions_ReturnsQuestionCorrectOptionInvalid` | `[]` → code |
| C13 | ChoiceQuestionRulesTests | `Validate_MultiCorrectOptionUnknown_ReturnsQuestionCorrectOptionInvalid` | code |
| C14 | ChoiceQuestionRulesTests | `Normalize_Mcq_SanitisesOptionTextAndDropsUnknownProperties` | body JSON has `clean:` texts and no extra key; spec only `correctOptionId` |
| C15 | ChoiceQuestionRulesTests | `Normalize_Multi_DeduplicatesCorrectIdsAndDefaultsPartialCreditFalse` | `{"correctOptionIds":["a"],"partialCredit":false}` |
| T1 | TrueFalseQuestionRulesTests | `Validate_CorrectAnswerFalse_ReturnsNoErrors` | empty |
| T2 | TrueFalseQuestionRulesTests | `Validate_MissingCorrectAnswer_ReturnsQuestionCorrectAnswerRequired` | code |
| T3 | TrueFalseQuestionRulesTests | `Validate_BodyNotAnObject_ReturnsQuestionBodyInvalid` | code |
| T4 | TrueFalseQuestionRulesTests | `Normalize_AnyBody_StoresEmptyBodyAndCorrectAnswer` | `("{}", "{\"correctAnswer\":true}")` |
| F1 | FillQuestionRulesTests | `Validate_ValidFill_ReturnsNoErrors` | stem `"<p>v = [[1]] m/s</p>"` → empty |
| F2 | FillQuestionRulesTests | `Validate_NoBlanks_ReturnsQuestionBlanksCountInvalid` | code |
| F3 | FillQuestionRulesTests | `Validate_MoreBlanksThanCap_ReturnsQuestionBlanksCountInvalid` | cap 1, 2 blanks → code |
| F4 | FillQuestionRulesTests | `Validate_BlankIdWithSpace_ReturnsQuestionBlankIdInvalid` | code |
| F5 | FillQuestionRulesTests | `Validate_DuplicateBlankIds_ReturnsQuestionBlankIdDuplicate` | code |
| F6 | FillQuestionRulesTests | `Validate_PlaceholderMissingFromStem_ReturnsQuestionBlankPlaceholderMissing` | code |
| F7 | FillQuestionRulesTests | `Validate_PlaceholderTwiceInStem_ReturnsQuestionBlankPlaceholderMissing` | code |
| F8 | FillQuestionRulesTests | `Validate_AnswersForUnknownBlank_ReturnsQuestionBlankAnswersMismatch` | code |
| F9 | FillQuestionRulesTests | `Validate_EmptyAcceptedAnswers_ReturnsQuestionAcceptedAnswersInvalid` | code |
| F10 | FillQuestionRulesTests | `Validate_AcceptedAnswerOverCap_ReturnsQuestionAcceptedAnswersInvalid` | cap 3 → code |
| F11 | FillQuestionRulesTests | `Validate_MoreAcceptedAnswersThanCap_ReturnsQuestionAcceptedAnswersInvalid` | cap 1 → code |
| F12 | FillQuestionRulesTests | `Normalize_MissingUnifyFlag_DefaultsTrueTrimsAndFollowsBodyOrder` | spec JSON exact |
| S1 | ShortQuestionRulesTests | `Validate_ValidNumeric_ReturnsNoErrors` | empty |
| S2 | ShortQuestionRulesTests | `Validate_ValidText_ReturnsNoErrors` | empty |
| S3 | ShortQuestionRulesTests | `Validate_MissingAnswerKind_ReturnsQuestionAnswerKindRequired` | code |
| S4 | ShortQuestionRulesTests | `Validate_UnknownAnswerKind_ReturnsQuestionBodyInvalid` | `"answerKind":"essay"` → code |
| S5 | ShortQuestionRulesTests | `Validate_NumericWithoutValue_ReturnsQuestionNumericValueRequired` | code |
| S6 | ShortQuestionRulesTests | `Validate_NegativeTolerance_ReturnsQuestionToleranceInvalid` | code |
| S7 | ShortQuestionRulesTests | `Validate_MissingToleranceMode_ReturnsQuestionToleranceInvalid` | code |
| S8 | ShortQuestionRulesTests | `Validate_TextWithoutAcceptedAnswers_ReturnsQuestionAcceptedAnswersInvalid` | code |
| S9 | ShortQuestionRulesTests | `Normalize_Numeric_KeepsOnlyNumericFields` | `{"value":9.8,"tolerance":0.1,"toleranceMode":"absolute"}` |
| S10 | ShortQuestionRulesTests | `Normalize_Text_TrimsAnswersAndDefaultsUnifyTrue` | `{"acceptedAnswers":["ماء"],"unifyLetterVariants":true}` |
| V1 | QuestionFieldsValidatorTests | `Validate_ValidMcq_HasNoErrors` | valid |
| V2 | QuestionFieldsValidatorTests | `Validate_MissingType_HasQuestionTypeRequired` | code; no `QUESTION_BODY_INVALID` (schema rule skipped) |
| V3 | QuestionFieldsValidatorTests | `Validate_UndefinedType_HasQuestionTypeInvalid` | `(QuestionType)99` → code |
| V4 | QuestionFieldsValidatorTests | `Validate_EmptyStem_HasQuestionStemRequired` | code |
| V5 | QuestionFieldsValidatorTests | `Validate_StemOverCap_HasQuestionStemTooLong` | code |
| V6 | QuestionFieldsValidatorTests | `Validate_ExplanationOverCap_HasQuestionExplanationTooLong` | code |
| V7 | QuestionFieldsValidatorTests | `Validate_MissingDifficulty_HasQuestionDifficultyRequired` | code |
| V8 | QuestionFieldsValidatorTests | `Validate_UndefinedDifficulty_HasQuestionDifficultyInvalid` | code |
| V9 | QuestionFieldsValidatorTests | `Validate_MissingMaxScore_HasQuestionMaxScoreRequired` | code |
| V10 | QuestionFieldsValidatorTests | `Validate_MaxScoreZero_HasQuestionMaxScoreInvalid` | code |
| V11 | QuestionFieldsValidatorTests | `Validate_MaxScoreOverCap_HasQuestionMaxScoreInvalid` | 101 → code |
| V12 | QuestionFieldsValidatorTests | `Validate_TooManyTags_HasQuestionTagsTooMany` | code |
| V13 | QuestionFieldsValidatorTests | `Validate_BlankTag_HasQuestionTagRequired` | code |
| V14 | QuestionFieldsValidatorTests | `Validate_TagOverCap_HasQuestionTagTooLong` | code |
| V15 | QuestionFieldsValidatorTests | `Validate_SchemaViolation_SurfacesTypeRuleCode` | mcq with unknown correct id → `QUESTION_CORRECT_OPTION_INVALID` |
| QF1 | QuestionContentFactoryTests | `CreateContent_Always_SanitisesStemAndExplanation` | `clean:` prefix on both; `MaxScore` copied |
| QF2 | QuestionContentFactoryTests | `CreateMetadata_Tags_TrimsAndDeduplicatesIgnoringCase` | `[" Kinematics ", "kinematics", "SI"]` → `["Kinematics","SI"]` |
| CV1 | CreateQuestionValidatorTests | `Validate_ValidCommand_HasNoErrors` | valid |
| CV2 | CreateQuestionValidatorTests | `Validate_EmptyLessonId_HasLessonIdRequired` | code |
| CV3 | CreateQuestionValidatorTests | `Validate_InvalidFields_SurfacesFieldCode` | empty stem → `QUESTION_STEM_REQUIRED` |
| UV1 | UpdateQuestionValidatorTests | `Validate_ValidCommand_HasNoErrors` | valid |
| UV2 | UpdateQuestionValidatorTests | `Validate_EmptyQuestionId_HasQuestionIdRequired` | code |
| UV3 | UpdateQuestionValidatorTests | `Validate_InvalidFields_SurfacesFieldCode` | missing difficulty → code |
| GV1 | GetQuestionValidatorTests | `Validate_ValidQuery_HasNoErrors` | valid |
| GV2 | GetQuestionValidatorTests | `Validate_EmptyQuestionId_HasQuestionIdRequired` | code |

### Application — handlers
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| H1 | CreateQuestionHandlerTests | `Handle_ValidCommand_AddsPendingQuestionAndSaves` | `AddAsync` received a question with `SubjectId` from the unit, `clean:` stem, Pending, `Version` 1; result `Id` equals it; `SaveChangesAsync` `Received(1)` |
| H2 | CreateQuestionHandlerTests | `Handle_LessonNotFound_ThrowsLessonNotFound` | type, code, `DidNotReceive` save |
| H3 | CreateQuestionHandlerTests | `Handle_UnitNotFound_ThrowsUnitNotFound` | type, code, `DidNotReceive` save |
| H4 | CreateQuestionHandlerTests | `Handle_ObjectiveNotInLesson_ThrowsQuestionObjectiveNotInLesson` | type, code, `AddAsync` and save `DidNotReceive` |
| H5 | CreateQuestionHandlerTests | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | type, code, `DidNotReceive` save |
| U1 | UpdateQuestionHandlerTests | `Handle_ContentEditOnApproved_ResetsToPendingBumpsVersionAndSaves` | Pending, `Version` 2, save `Received(1)` |
| U2 | UpdateQuestionHandlerTests | `Handle_MetadataEditOnApproved_KeepsApprovedAndSaves` | Approved, `Version` 1, `Difficulty` Hard, save `Received(1)` |
| U3 | UpdateQuestionHandlerTests | `Handle_QuestionNotFound_ThrowsQuestionNotFound` | type, code, `DidNotReceive` save |
| U4 | UpdateQuestionHandlerTests | `Handle_LessonNotFound_ThrowsLessonNotFound` | type, code, `DidNotReceive` save |
| U5 | UpdateQuestionHandlerTests | `Handle_TypeChanged_ThrowsQuestionTypeImmutable` | type, code, `DidNotReceive` save |
| U6 | UpdateQuestionHandlerTests | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | type, code, `DidNotReceive` save |
| G1 | GetQuestionHandlerTests | `Handle_ExistingQuestion_ReturnsDetailWithParsedJson` | `Type` "Mcq", `ValidationStatus` "Pending", `Body.GetProperty("options").GetArrayLength()` 2, `GradingSpec.GetProperty("correctOptionId").GetString()` "b" |
| G2 | GetQuestionHandlerTests | `Handle_QuestionNotFound_ThrowsQuestionNotFound` | type, code |
| L2 | DeleteLessonHandlerTests | `Handle_LessonWithQuestions_ThrowsLessonHasQuestions` | `AnyInLessonAsync` → true; `BusinessRuleViolationCoreException` `LESSON_HAS_QUESTIONS`; not deleted; `DidNotReceive` save |

### Integration (`QuestionsEndpointTests` unless stated)
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| I1 | QuestionsEndpointTests | `Post_AdminMcq_CreatesPendingVersionOneWithRevisionAndAudits` | 200 + id; DB: Pending, `Version` 1, `SubjectId`, 1 revision, `Tags` `["arithmetic"]`; `Question.Create` audit Success |
| I2 | QuestionsEndpointTests | `Post_EachV1Type_Returns200` (`[Theory]`, 5 rows: type, stem, body JSON, spec JSON) | 200; DB `Type` matches |
| I3 | QuestionsEndpointTests | `Post_BodyClaimsApproved_StillPending` | extra `validationStatus:"Approved"` in the JSON → DB Pending |
| I4 | QuestionsEndpointTests | `Post_McqCorrectOptionUnknown_Returns422QuestionCorrectOptionInvalid` | 422 + code contains |
| I5 | QuestionsEndpointTests | `Post_UnknownLesson_Returns404LessonNotFound` | 404 + code |
| I6 | QuestionsEndpointTests | `Post_ObjectiveOfOtherLesson_Returns400QuestionObjectiveNotInLesson` | 400 + code; no question row |
| I7 | QuestionsEndpointTests | `Post_Teacher_Returns403` | 403 |
| I8 | QuestionsEndpointTests | `Post_Anonymous_Returns401` | 401 |
| I9 | QuestionsEndpointTests | `Put_ContentEditOnApproved_ResetsPendingBumpsVersionAndAudits` | seed approved; 200; DB Pending, `Version` 2, 2 revisions, `ValidatedBy` null; `Question.Update` audit diff contains `"version"` |
| I10 | QuestionsEndpointTests | `Put_DifficultyOnlyOnApproved_KeepsApprovedAndVersion` | DB Approved, `Version` 1, `Difficulty` Hard |
| I11 | QuestionsEndpointTests | `Put_TypeChanged_Returns400QuestionTypeImmutable` | 400 + code |
| I12 | QuestionsEndpointTests | `Put_UnknownQuestion_Returns404QuestionNotFound` | 404 + code |
| I13 | QuestionsEndpointTests | `Put_Teacher_Returns403` | 403; DB unchanged |
| I14 | QuestionsEndpointTests | `Get_Admin_ReturnsDetailWithRawJson` | 200; `body.options` array, `gradingSpec.correctOptionId` "b", `validationStatus` "Pending", `version` 1 |
| I14b | QuestionsEndpointTests | `Get_UnknownQuestion_Returns404QuestionNotFound` | 404 + code |
| I14c | QuestionsEndpointTests | `Get_Student_Returns403` | 403 |
| I15 | LessonManagementEndpointTests | `Delete_LessonWithQuestions_Returns400LessonHasQuestions` | 400 + code; lesson not deleted |
| I16 | LessonsEndpointTests | `GetList_Admin_ReturnsQuestionCountPerLesson` | 2 lessons, 2 questions seeded on the first → `questionCount` 2 and 0 |

### Web (`LessonItem.test.tsx`)
| # | Test | Asserts |
|---|------|---------|
| W1 | `it('shows the question count of each lesson')` | row texts include "2 questions", "0 questions" and "1 question" |
| W2 | `it('shows the question count in Arabic')` | `openTree(user, 'ar')` → "سؤالان" visible |
| W3 | `it('explains why a lesson with questions cannot be deleted')` | `http.delete('*/api/lessons/:lessonId')` → 400 `{ code: 'LESSON_HAS_QUESTIONS' }`; after confirming delete, "This lesson has questions and cannot be deleted." is visible |

## Definition of done
- [ ] Every file in Files to create exists with the exact namespace, type name and signature. No other new files.
- [ ] `Question.ValidationStatus` has a private setter. `Create` sets Pending. `Approve(TeacherSubject)` is the only code path to Approved. No command, request or endpoint carries a status or approves.
- [ ] A content edit (stem, body, grading spec, explanation, max score) bumps `Version` and appends a revision in every status, and resets Approved → Pending with the validator cleared. A metadata-only edit changes neither. A type change → `QUESTION_TYPE_IMMUTABLE`.
- [ ] JSON equality is semantic (`JsonNode.DeepEquals`). Re-saving identical content (D11) changes nothing and produces no audit diff for `Body`/`GradingSpec`.
- [ ] `Body`, `GradingSpec` and `QuestionRevisions.Snapshot` are `jsonb`, `Tags` is `text[]`, enums are varchar(50) strings. The migration `AddQuestions` has only CreateTable and CreateIndex.
- [ ] Every per-type rule in A-3…A-6 exists with its error code, and every code exists in both resx files.
- [ ] Stem, explanation and choice option text go through `IRichTextSanitizer`. Accepted answers are trimmed plain text.
- [ ] `CreateQuestion` and `UpdateQuestion` implement `IAuditableCommand` (`Question.Create` / `Question.Update`), and `Question` is `IAuditedEntity`.
- [ ] `Lesson.Delete(bool hasQuestions, …)` guards with `LESSON_HAS_QUESTIONS`. `GET /api/lessons` returns `questionCount`, and the web lesson row shows it (en and ar).
- [ ] All 10 D23 caps are in `ContentOptions`, `appsettings.example.json` and `ApiFactory`. `dotnet test` passes with no `api/Elmanhg.Api/appsettings.json` present.
- [ ] Every test row above exists with that exact name and assertion. The only modified tests are the ones listed as **modify**.
- [ ] `dotnet build` with zero new warnings, `dotnet test` green, `dotnet format --verify-no-changes` exits 0.
- [ ] `api/openapi/v1.json` and `web/src/shared/api/generated/**` are regenerated. `npx tsc -b --noEmit`, `npx eslint . --max-warnings=0`, `npx prettier --check .` and `npx vitest run` all exit 0.
- [ ] Postman "Questions" folder added (4 requests plus the `questionId` variable).
- [ ] Docs: `docs/question-schemas.md` created; `PRD.md` §5.2, §5.3, §6 and §15, `audit-log.md`, `rich-text.md` and `claude-design-prompt.md` §4 updated as specified.
- [ ] Guard grep is clean (no `DateTime.Now/UtcNow`, `.Result`, `FromSqlRaw`, `async void`). The only `catch` is `JsonException` in `QuestionSchemaReader.TryRead`.
