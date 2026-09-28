# Audit log

Semantic audit trail for content and validation actions (PRD §15, §17 rule 13): **who did what, to which resource, when, with what outcome, and which fields changed**. Each audited command appends one row to the append-only `AuditLogs` PostgreSQL table. The append is best-effort: if it fails, the error is logged and the command's result is still returned, so that command has no row (see How it works). Admins read it at `#/admin/audit` (`GET /api/audit-logs`, policy `AuditLog.View`). Deeper technical detail is reached through `TraceId` in the request logs.

Adapted from Morabh `AuditLogs.md`. Elmanhg adds field-level diffs and database-enforced append-only storage.

## How it works

- A command opts in by implementing `IAuditableCommand` (`Core.Auditing`) with a fixed `AuditAction`, `AuditResourceType` and `AuditResourceId`.
- `AuditBehaviour<,>` is the outermost MediatR pipeline behaviour (`AddCoreAuditing` is registered before `AddCoreCQRS`). It writes one row on success **and** on failure, including validation (422) failures and authorisation failures raised inside the pipeline (for example `SubjectScopeBehaviour`). ASP.NET `[Authorize]` policy denials happen before `mediator.Send`, so they are not audited. It observes the outcome and re-throws untouched; `CoreExceptionMiddleware` still shapes the response. It is the only audit writer: there is no domain event or event handler for auditing.
- Create-style commands expose the generated id through `IAuditableResult` on the **result** (explicit interface implementation, so it adds nothing to the JSON).
- Diffs: an entity opts in by implementing the `IAuditedEntity` marker (`Core.DDD`). After domain events are published, `CoreDbContext.SaveChangesAsync` reads the Added, Modified and Deleted entries of marked entities from the change tracker. It hands them to the scoped `IAuditChangeCollector` **only when the save succeeds**. `AuditBehaviour` serialises the changes recorded while its own command ran into the `Diff` jsonb column.
- Append-only: `IAuditLogRepository` exposes only `AppendAsync`, `FindPaginatedAsync` and `GetResourceTypesAsync`. `AppendAsync` is one parameterised `INSERT` that never touches the change tracker, so a failed command's uncommitted changes are never flushed by the audit write. A database trigger rejects every `UPDATE`, `DELETE` and `TRUNCATE` on `AuditLogs`.
- The audit write uses an uncancelled token, because the request token may already be cancelled after the handler committed. A failed audit write is logged and never breaks the request it describes.
- Handlers are never modified for auditing.

Toggle: `CoreAuditing:Enabled` in configuration (on in every environment). Paging and filter caps: `AuditLogs:MaxPageSize`, `AuditLogs:FilterMaxLength`.

## Record fields

| PRD §15 field | Column | Notes |
|---|---|---|
| `id` | `Id` | |
| `actor_id` | `ActorUserId` | null for system actions |
| `actor_name` | `ActorUserName` | JWT name claim at the time (email or mobile number); a snapshot, never re-resolved |
| `actor_role` | `ActorRole` | `Student`, `Teacher` or `Admin` |
| `action` | `Action` | `Resource.Verb`, for example `Teacher.AssignSubject` |
| `entity` | `ResourceType` | |
| `entity_id` | `ResourceId` | |
| `outcome` | `Outcome` | `Success` or `Failure` |
| `error_code` | `ErrorCode` | the core exception code, or the exception type name |
| `diff_json` | `Diff` (jsonb) | null when no audited entity changed. On failure it is null unless the handler committed changes before it threw; those committed changes are listed |
| `trace_id` | `TraceId` | |
| `created_at` | `Timestamp` | UTC |

## Diff JSON shape

An array with one element per changed audited entity. Property keys are camelCase. `before` is null for `Created`; `after` is null for a hard `Deleted`.

```json
[{"entityType":"TeacherSubject","entityId":"<guid>","change":"Created","properties":{"teacherId":{"before":null,"after":"<guid>"},"subjectId":{"before":null,"after":"<guid>"}}}]
```

- `change` is `Created` (added), `Deleted` (removed, or a soft delete: `isDeleted` going `false` to `true`) or `Modified`.
- Excluded properties: `Id`, `DeletedAt`, `CreatedBy`, `CreationDate`, `UpdatedBy`, `UpdationDate`, shadow properties and concurrency tokens. The row already carries the actor and the time. `IsDeleted` is left out of `Created` and hard `Deleted` entries.
- `Modified` keeps only properties whose value actually changed. An entry with nothing left is dropped.

## Audited commands

| Command | AuditAction | ResourceType | Id source |
|---|---|---|---|
| AssignTeacherSubject | `Teacher.AssignSubject` | Teacher | command |
| UnassignTeacherSubject | `Teacher.UnassignSubject` | Teacher | command |
| CreateSubject | `Subject.Create` | Subject | result |
| UpdateSubject | `Subject.Update` | Subject | command |
| ReorderSubject | `Subject.Reorder` | Subject | command (the diff lists every sibling whose `Order` changed) |
| DeleteSubject | `Subject.Delete` | Subject | command |
| CreateUnit | `Unit.Create` | Unit | result |
| UpdateUnit | `Unit.Update` | Unit | command |
| ReorderUnit | `Unit.Reorder` | Unit | command (the diff lists every sibling whose `Order` changed) |
| DeleteUnit | `Unit.Delete` | Unit | command |
| CreateLesson | `Lesson.Create` | Lesson | result |
| UpdateLesson | `Lesson.Update` | Lesson | command (the diff lists the Lesson fields and every `LessonObjective` created, modified or deleted) |
| UploadLessonImage | `Lesson.UploadImage` | Lesson | command (no diff: only a file is written) |
| PublishLesson | `Lesson.Publish` | Lesson | command |
| UnpublishLesson | `Lesson.Unpublish` | Lesson | command |
| ArchiveLesson | `Lesson.Archive` | Lesson | command |
| ReorderLesson | `Lesson.Reorder` | Lesson | command (the diff lists every sibling whose `Order` changed) |
| DeleteLesson | `Lesson.Delete` | Lesson | command (the diff lists the lesson and every objective soft-deleted with it) |
| CreateQuestion | `Question.Create` | Question | result |
| UpdateQuestion | `Question.Update` | Question | command (the diff lists the changed Question fields; a content edit shows `version`, and `validationStatus` when it resets) |
| ResubmitQuestion | `Question.Resubmit` | Question | command (the diff shows `validationStatus`, `rejectionReason`, `validatedBy`, `validatedAt`, and the content fields and `version` when the content changed) |
| RetireQuestion | `Question.Retire` | Question | command (the diff shows `retiredAt`) |
| ImportQuestions | `Question.Import` | QuestionImportBatch | command (the diff lists the new QuestionImportBatch and every created Question; a replay writes a row with no diff) |
| ApproveQuestion | `Question.Approve` | Question | command (the diff shows `validationStatus`, `validatedBy`, `validatedAt`, and `difficulty` when the teacher changed it) |
| RejectQuestion | `Question.Reject` | Question | command (the diff shows `validationStatus`, `rejectionReason`, `validatedBy`, `validatedAt`) |
| BulkApproveQuestions | `Question.BulkApprove` | ReviewSession | command (the diff lists every approved Question) |

Audited entities (`IAuditedEntity`): `TeacherSubject`, `Subject`, `CurriculumUnit`, `Lesson`, `LessonObjective`, `Question`, `QuestionImportBatch`.

`QuestionRevision` and `QuestionDecision` rows are an append-only history and are not diffed.

## Not audited (deliberate)

- **Reads** (queries), including the audit log itself.
- **Auth**: login, register, OTP, logout. A separate security-log concern.
- **Noise**: access-token refresh.
- **Evidence uploads** (Ask a Teacher images and voice): audit the decision, not the file. Lesson content images are the exception: they change content (PRD §17 rule 13), so `UploadLessonImage` is audited as `Lesson.UploadImage` with no diff.
- **Review sessions and openings** (`StartReviewSession`, `RecordQuestionOpening`): a reading aid that gates bulk approval, not a content or validation change. `ReviewSession` and `ReviewSessionOpening` are not `IAuditedEntity`; the approvals they enable are audited.
- **Quiz and exam activity** (`StartQuizSession`, `SubmitAnswer`, `FinishSession`): student practice, not a content or validation change. Attempts are their own append-only log (`docs/sessions.md`).
- **PII entities**: `User` is never marked `IAuditedEntity`, so password hashes and phone numbers never reach a diff.

## Add a new audited command

Update or delete (id known up front):

```csharp
public sealed record RejectQuestionCommand(Guid QuestionId, string Reason) : IRequest, IAuditableCommand
{
    public string AuditAction => "Question.Reject";
    public string AuditResourceType => "Question";
    public Guid? AuditResourceId => QuestionId;
}
```

Create (id generated in the handler, exposed through the result):

```csharp
public sealed record CreateLessonCommand(/* ... */) : IRequest<CreateLessonResult>, IAuditableCommand
{
    public string AuditAction => "Lesson.Create";
    public string AuditResourceType => "Lesson";
    public Guid? AuditResourceId => null;
}

public sealed record CreateLessonResult(Guid LessonId) : IAuditableResult
{
    Guid? IAuditableResult.AuditResourceId => LessonId;
}
```

To get field-level diffs, mark every entity the command changes:

```csharp
public class Lesson : AggregateRoot, IAuditedEntity
```

Then add a row to the "Audited commands" table above.

## Known limits

- Only scalar properties are diffed. Owned types (for example `LocalizedText`) and navigation collections are not. jsonb columns mapped as scalar properties are included.
- Bulk operations record one `ResourceId` (the parent). The diff still lists every changed audited entity.
- `ExecuteUpdateAsync` / `ExecuteDeleteAsync` bypass the change tracker, so they produce no diff.
