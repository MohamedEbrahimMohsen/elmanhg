# Plan — Student AI chat history and deletion (#271, E18.S7)

## Goal
After this ships, a student can open «محادثاتي السابقة» inside the assistant panel. They see their own past assistant chats, newest first, each with its lesson or subject, date and first question. They can reopen one and keep chatting in it, and delete one after confirming. A deleted chat is gone from the student, admin and training views. Its messages and training copies are erased through a narrow, migration-owned database path, and the deletion is audited without any text. Deleting a chat never gives back daily messages. Admins can turn deletion off on the Configuration page (`features.studentsCanDeleteAvatarChats`, on by default).

## Scope
**In:**
- API: `GET /api/avatar/my-conversations`, `GET /api/avatar/my-conversations/{id}` and `DELETE /api/avatar/my-conversations/{id}`. All three are scoped to the caller and use the `Avatar.Chat` policy (Student only).
- Erasure: an append-only exemption that allows only `DELETE`, only for one conversation id, only inside `erase_avatar_conversation(uuid)`. It applies to `AvatarMessages` and `AvatarTrainingRecords`.
- Conversation soft delete, with `MessageCount` → 0 and an `AvatarConversation.Delete` audit row.
- `conversationDeletionEnabled` on `GET /api/avatar/status`.
- Runtime setting `features.studentsCanDeleteAvatarChats`.
- Web: a history view inside the lazy `AvatarPanel` (list, reopen, delete with a confirmation dialog, pagination, loading, empty and error states) in ar/en with RTL. A deleted or unknown conversation id resets the panel's conversation (the #215 nit).
- A TRUNCATE test on `AvatarMessages` (the #215 nit).
- Docs-sync and Postman.

**Out:**
- Erasing a whole account, or erasing attempts, teacher threads or essay grades.
- An automatic retention purge. The dev decision on #215 is to keep chats until the student deletes them.
- Recalling JSONL files that were already exported or downloaded. This is documented, not solved.
- These #215 nits, which have nothing to do with this story: loading only the last N turns per send, validating the 100-character identifier columns, and moving the Arabic comma separator to i18n. The orchestrator moves them to a new issue when it closes #215.

**Deferred:**
- Telling students, or asking their consent, that their interactions are used for training. This is a dev or legal decision only a human can make. It stays an open line in the `docs/training-data.md` checklist, and the orchestrator opens an issue for it when it closes #215.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | How does erasure get past the append-only trigger? | Add a new trigger function, `reject_avatar_mutation_unless_erasing()`, on `AvatarMessages` and `AvatarTrainingRecords` only. It lets a row through only when `TG_OP = 'DELETE'` and `OLD."ConversationId"::text = current_setting('elmanhg.erase_avatar_conversation', true)`. Everything else raises `'% is append-only'` (P0001). The only code that sets that transaction-local setting is the migration-owned `erase_avatar_conversation(uuid)`. It sets the setting, deletes the training rows, deletes the messages and clears the setting again. TRUNCATE triggers stay on `reject_append_only_mutation()`. | UPDATE stays rejected even during erasure. DELETE outside the function, or for another conversation, stays rejected. Every other append-only table keeps the shared function. A `SECURITY DEFINER` function alone would not help, because the app role owns the tables and the triggers fire for every role. The setting carries the exact id, so a stray `DELETE` in the same transaction cannot widen the erasure. It guards against application bugs, not against a database owner, who could already drop triggers. Documented. |
| 2 | How are the training copies found? | By `AvatarTrainingRecords.ConversationId`, which already exists and is indexed (`(ConversationId, StudentMessagePosition)`). No schema change. | The link exists ([training-data.md](../../docs/training-data.md) Tables), so the hashed student id is not needed. |
| 3 | Hard or soft delete of `AvatarConversations`? | Soft delete. `AvatarConversation.Delete(at)` sets `IsDeleted`, `DeletedAt`, `MessageCount = 0`, `UpdatedBy`, `UpdationDate`. Messages and training rows are hard-deleted, because they hold the text. | This mirrors Morabh `DeleteShippingAddressHandler` (owner-scoped soft delete) and Elmanhg `ExamPeriod.Delete`. The row holds no text. The global query filter hides it from every reader (student list and detail, admin list and detail, send). `MessageCount → 0` is true after erasure and puts the count in the audit diff. |
| 4 | How is the deletion made atomic? | `IAvatarConversationRepository.ExecuteInTransactionAsync(Func<CancellationToken, Task>, CancellationToken)` runs execution strategy + `BeginTransactionAsync` + commit, mirroring `UserRepository.ExecuteInAdminRosterLockAsync`. `EraseMessagesAsync(Guid, CancellationToken)` runs the SQL function. The handler loads, erases, soft-deletes and calls `SaveChangesAsync` inside the callback. | `SaveChangesAsync` stays in the handler. The `xmin` token on the conversation makes a racing send or a racing delete fail with `409 AVATAR_CONVERSATION_MODIFIED_CONCURRENTLY`, which is already mapped in `AppDbContext`. The whole erasure then rolls back. |
| 5 | Usage counters | The delete path never reads or writes `AvatarMessageUsages`. | Usage rows hold no text and count toward the daily limit, so deleting a chat must not reset it. An integration test proves the limit is unchanged after a delete. |
| 6 | Audit content | `DeleteMyAvatarConversationCommand : IAuditableCommand` (`AvatarConversation.Delete`, resource type `AvatarConversation`, id = the command's id). `AvatarConversation` becomes `IAuditedEntity`. | The actor is the student (`ActorUserId`) and the resource is the conversation id. The diff is `isDeleted false→true` and `messageCount N→0`. The entity has no text property, so no text can reach `AuditLogs`. |
| 7 | Already-exported JSONL | Not recalled. Later exports no longer contain the conversation. A completed export file still on the server is deleted when its retention runs out (`TrainingExports:RetentionDays`, 7 by default). Files an admin already downloaded are outside the platform. An export that is running during the delete may include the rows it already read. Documented in `training-data.md` and `avatar.md`. | Story: "document it; you don't need to solve it". |
| 8 | Teacher and admin views | Teachers have no avatar view. Admin list, search and detail read through `IAvatarConversationRepository`, so the global filter hides a deleted conversation and the admin detail returns `404 AVATAR_CONVERSATION_NOT_FOUND`. No dashboard or analytics reads the avatar tables (verified by grep). No admin code changes; integration tests prove both. | Verified readers: only `GetAvatarConversations*`, `SendAvatarMessage`, the training export and the new student slices. |
| 9 | Disabled-flag response | `400 AVATAR_CONVERSATION_DELETION_DISABLED` (`BusinessRuleViolationCoreException`), checked before any load. | Mirrors `RefundPaymentHandler` with `PAYMENT_REFUNDS_DISABLED`. |
| 10 | How the UI knows the flag | New last field `ConversationDeletionEnabled` on `AvatarStatusResult`, read in `GetAvatarStatusHandler`. | The panel already loads the status when it opens. `PageData<T>` cannot carry extra fields. |
| 11 | Setting key, group and default | `features.studentsCanDeleteAvatarChats`, group `Features`, Boolean, constant default `true` (no Options key). | Mirrors `features.refundsEnabled` (constant default). Story: default on. |
| 12 | History during an exam in progress | List and detail return `403 AVATAR_EXAM_IN_PROGRESS` through `AvatarGate.EnsureNoExamInProgressAsync`. The panel hides the history button while `examInProgress`. Delete is not gated, because it reveals nothing. | PRD §17 rule 10: past replies can explain questions that also appear in the running exam. |
| 13 | Where the history UI lives | Inside `AvatarPanel`, which is already lazy (`AvatarDock` → `lazy(import('./AvatarPanel'))`). No new route and no nav entry. `view: 'chat' \| 'history'` is in the avatar reducer. The page number is local state in `AvatarHistory`. | The story says to continue the chat from the avatar panel. `perf:budget` ignores dynamic imports (`bundleBudget.ts`), so page budgets are unchanged. URL search params do not suit a global overlay that sits on top of every route. |
| 14 | Route names | `api/avatar/my-conversations` on `AvatarController`. | `/api/avatar/conversations` is the admin route. `my-` follows `my-stats` (`DashboardController`). The tag stays `Avatar`, so the hooks go into `generated/avatar/avatar.ts`. |
| 15 | Page size | Query `pageNumber` (default 1) and `pageSize` (default 20), at most the new `Avatar:StudentConversationsMaxPageSize` (default 50, range 1–100). Error codes `AVATAR_CONVERSATIONS_PAGE_NUMBER_INVALID` and `AVATAR_CONVERSATIONS_PAGE_SIZE_INVALID` are reused. | A student can collect many chats. Mirrors `GetMyTeacherThreadsValidator`. The cap is an Options value, not a constant (skill §8.1). |
| 16 | Student result content | Context ids and names, dates, message count, the first question, and messages with text and citations only. No model, tokens, cost, context or stop reason. | Those are admin-only fields (docs/avatar.md, Admin view). |
| 17 | Reopen behaviour | A fresh fetch of the detail (`staleTime: 0`), then dispatch `resumed`. The context is `{entryPoint, lessonId?, sessionId?, questionId?, title: lessonName ?? subjectName}`, with the keys left out when they are null (`exactOptionalPropertyTypes`). The `SendAvatarMessage` checks (`IsFor`, lesson lock, exam) are unchanged. | Continuing reuses the existing send path with the stored conversation id. |
| 18 | A deleted chat that is still open in the panel | Delete success dispatches `conversationDeleted(id)`, which clears messages and the conversation id when the id matches. A send that gets `AVATAR_CONVERSATION_NOT_FOUND` shows the notice `conversationGone` and resets `conversationId` to null. | Fixes the #215 nit "keeps the conversation id after a 404". |
| 19 | Response of delete | Empty `200` (`Ok()`). | Mirrors `DeleteExamPeriod`. |

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Domain/Avatar/AvatarConversation.cs` | Implement `IAuditedEntity` (`using Core.DDD.Entities` already present); add `Delete(DateTimeOffset deletedAt)` (see Domain behaviour). |
| `api/Elmanhg.Domain/Avatar/IAvatarConversationRepository.cs` | Add `Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken);` and `Task EraseMessagesAsync(Guid conversationId, CancellationToken cancellationToken);` |
| `api/Elmanhg.Infrastructure/Avatar/AvatarConversationRepository.cs` | Implement both (see Files to create #6 for bodies). |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | Under the avatar group, after `AvatarConversationsDateRangeInvalid`: `AvatarConversationIdRequired = "AVATAR_CONVERSATION_ID_REQUIRED"`, `AvatarConversationDeletionDisabled = "AVATAR_CONVERSATION_DELETION_DISABLED"`. |
| `api/Elmanhg.Api/Resources/Messages.en.resx`, `Messages.ar.resx` | Two keys (see Error codes). |
| `api/Elmanhg.Application/Shared/Options/AvatarOptions.cs` | `[Range(1, 100)] public int StudentConversationsMaxPageSize { get; set; } = 50;` |
| `api/Elmanhg.Api/appsettings.example.json` | `"StudentConversationsMaxPageSize": 50` in `Avatar`. |
| `deploy/api.env.example` | `# Avatar__StudentConversationsMaxPageSize=50` after `Avatar__ConversationSearchMaxLength`. |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | `["Avatar:StudentConversationsMaxPageSize"] = "50"` next to the other Avatar keys. |
| `api/Elmanhg.Application/Shared/RuntimeSettings/Definitions/FeatureFlagRuntimeSettings.cs` | `public static readonly RuntimeSettingKey<bool> StudentsCanDeleteAvatarChats = new("features.studentsCanDeleteAvatarChats");`. Third entry: `RuntimeSettingDefinition.ForBoolean(StudentsCanDeleteAvatarChats, RuntimeSettingGroup.Features, true, new LocalizedText("السماح للطلاب بحذف محادثات المساعد", "Students can delete assistant chats"), new LocalizedText("عند الإيقاف، يرى الطالب محادثاته السابقة ويكملها لكن لا يستطيع حذفها. المحادثات المحذوفة سابقًا لا تعود.", "When off, students can still see and continue their past chats but cannot delete them. Chats already deleted do not come back."))` |
| `api/Elmanhg.Application/Avatar/Shared/AvatarStatusResult.cs` | Append `bool ConversationDeletionEnabled` as the last positional parameter. |
| `api/Elmanhg.Application/Avatar/GetAvatarStatus/GetAvatarStatusHandler.cs` | After `used`: `var deletionEnabled = await runtimeSettings.GetAsync(FeatureFlagRuntimeSettings.StudentsCanDeleteAvatarChats, cancellationToken).ConfigureAwait(false);` and pass it last to `AvatarStatusResult`. |
| `api/Elmanhg.Api/Controllers/Avatar/AvatarController.cs` | Three actions (see API surface). |
| `postman/elmanhg.postman_collection.json` | In folder "Avatar", after "Send avatar message (global)", add "List my avatar conversations" (GET `{{baseUrl}}/api/avatar/my-conversations?pageNumber=1&pageSize=20`, test 200 + `items` array), "Get my avatar conversation" (GET `.../my-conversations/{{avatarConversationId}}`, test 200 + `messages` array) and "Delete my avatar conversation" (DELETE `.../my-conversations/{{avatarConversationId}}`, test 200). Delete is last. |
| `api/openapi/v1.json` | Regenerated by `dotnet build` (never hand-edited). |
| `web/orval.config.ts` | Add `GetMyAvatarConversations: { zod: { generate: { query: false } } },` to `apiZod.output.override.operations`. |
| `web/src/shared/api/generated/**` | Regenerated by `npm run gen:api`. |
| `web/src/features/avatar/hooks/avatarReducer.ts` | `view` state and new actions (see Files to create, web). |
| `web/src/features/avatar/api/avatarErrors.ts` | `AvatarNoticeKind` gains `'conversationGone'`; `noticeByCode.AVATAR_CONVERSATION_NOT_FOUND = 'conversationGone'`. |
| `web/src/features/avatar/components/AvatarPanel.tsx` | Replace the inline header with `<AvatarPanelHeader status={status.data} />`, rendered as `status.isSuccess ? status.data : undefined`. Show the context line only when `state.view === 'chat'`. In the success branch, render `state.view === 'history' ? <AvatarHistory status={status.data} /> : <>quota + AvatarConversation + AvatarComposer</>`. |
| `web/src/features/avatar/i18n/en.json`, `ar.json` | Keys under i18n below. |
| `web/src/test/avatarFixtures.ts` | `avatarStatus()` gains `conversationDeletionEnabled: true`; add the fixtures listed under web files. |
| `api/Elmanhg.Tests/Application/Features/Avatar/GetAvatarStatus/GetAvatarStatusHandlerTests.cs` | **modify** `Handle_FreeStudentWithTwoMessages_ReturnsCountsAndLimits`: expected `new AvatarStatusResult(false, PlanTier.Free, 5, 2, 3, 2000, 10, true)` (intentional contract change). Add the test listed in the Test plan. |
| `api/Elmanhg.Tests/Application/Shared/RuntimeSettings/RuntimeSettingValuesTests.cs` | **modify** `Get_EveryRegisteredKey_DeserialisesToItsType`: add `values.Get(FeatureFlagRuntimeSettings.StudentsCanDeleteAvatarChats),` after `RefundsEnabled` (the test lists every key). |
| `api/Elmanhg.Tests/Application/Shared/RuntimeSettings/FeatureFlagRuntimeSettingsTests.cs` | Add one test. |
| `api/Elmanhg.Tests/Domain/Avatar/AvatarConversationTests.cs` | Add two tests. |
| `api/Elmanhg.Tests/Integration/Avatar/AvatarTestData.cs` | Add the helpers listed in Files to create #20. |
| `web/src/features/avatar/hooks/avatarReducer.test.ts`, `api/avatarErrors.test.ts`, `components/AvatarPanel.test.tsx` | Add tests (Test plan); no existing test edited. |
| Docs (see Docs-sync) | `docs/avatar.md`, `docs/training-data.md`, `docs/configuration.md`, `docs/audit-log.md`, `docs/PRD.md`, `docs/claude-design-prompt.md`, `docs/prototype.md`, `docs/deployment.md`, `docs/backlog.json`, `docs/implementation-report.md`. |

## Files to create
### API
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `api/Elmanhg.Infrastructure/Migrations/<timestamp>_AddAvatarConversationErasure.cs` (+ `.Designer.cs` from `dotnet ef migrations add AddAvatarConversationErasure -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`) | Migration, SQL only | The model has no change, so the generated Up/Down are empty. `Up`: one `migrationBuilder.Sql("""…""")` with the SQL in "Erasure SQL" below. `Down`: restore both row triggers to `reject_append_only_mutation()`, then `DROP FUNCTION erase_avatar_conversation(uuid); DROP FUNCTION reject_avatar_mutation_unless_erasing();`. The snapshot is touched only by the tool. |
| 2 | `api/Elmanhg.Application/Avatar/GetMyAvatarConversations/GetMyAvatarConversationsQuery.cs` | `sealed record` | `namespace Elmanhg.Application.Avatar.GetMyAvatarConversations; public sealed record GetMyAvatarConversationsQuery(int PageNumber = 1, int PageSize = 20) : IRequest<PageData<StudentAvatarConversationResult>>;` |
| 3 | `.../GetMyAvatarConversations/GetMyAvatarConversationsValidator.cs` | `sealed class : AbstractValidator<GetMyAvatarConversationsQuery>` | ctor `(IOptions<AvatarOptions> avatarOptions)`. `RuleFor(x => x.PageNumber).ValidateMin(1, ErrorCodes.AvatarConversationsPageNumberInvalid);` `RuleFor(x => x.PageSize).ValidateRange(1, options.StudentConversationsMaxPageSize, ErrorCodes.AvatarConversationsPageSizeInvalid);` |
| 4 | `.../GetMyAvatarConversations/GetMyAvatarConversationsHandler.cs` | `sealed class : IRequestHandler<GetMyAvatarConversationsQuery, PageData<StudentAvatarConversationResult>>` | ctor `(IAvatarConversationRepository avatarConversationRepository, ISubjectRepository subjectRepository, ILessonRepository lessonRepository, ISessionRepository sessionRepository, IOptions<ExamsOptions> examsOptions, TimeProvider timeProvider, ICurrentUserService currentUserService)`. Handle: (1) UserId null/default → `UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated)`; (2) `await AvatarGate.EnsureNoExamInProgressAsync(userId, sessionRepository, examsOptions.Value, timeProvider.GetUtcNow(), cancellationToken)`; (3) `FindPaginatedAsync(request.PageNumber, request.PageSize, cancellationToken, filter: x => x.StudentId == userId, include: q => q.Include(x => x.Messages.Where(m => m.Position == 0)), orderBy: q => q.OrderByDescending(x => x.LastMessageAt).ThenByDescending(x => x.Id), asNoTracking: true)`; (4) distinct subject and lesson ids → one `FindAsync(x => ids.Contains(x.Id), …, asNoTracking: true)` each → dictionaries (mirror `GetAvatarConversationsHandler`); (5) return `PageData` with items from `StudentAvatarConversationResultGenerator.Generate(conversation, subjectName, lessonName)` and the page fields copied. |
| 5 | `api/Elmanhg.Application/Avatar/GetMyAvatarConversation/GetMyAvatarConversationQuery.cs`, `…Validator.cs`, `…Handler.cs` | query / validator / handler | Query: `public sealed record GetMyAvatarConversationQuery(Guid ConversationId) : IRequest<StudentAvatarConversationDetailResult>;`. Validator: `RuleFor(x => x.ConversationId).ValidateRequired(ErrorCodes.AvatarConversationIdRequired);`. Handler ctor: same 7 dependencies as #4. Handle: (1) auth guard; (2) exam gate as #4; (3) `FirstOrDefaultAsync(x => x.Id == request.ConversationId && x.StudentId == userId, cancellationToken, include: q => q.Include(x => x.Messages), asNoTracking: true) ?? throw new NotFoundCoreException(ErrorCodes.AvatarConversationNotFound)`; (4) subject and lesson by `GetByIdAsync(id, cancellationToken, asNoTracking: true)` when the id is not null; (5) `StudentAvatarConversationResultGenerator.GenerateDetail(conversation, subject?.Name, lesson?.Name)`. |
| 6 | `api/Elmanhg.Infrastructure/Avatar/AvatarConversationRepository.cs` (existing, body) | repository | `ExecuteInTransactionAsync`: a copy of `UserRepository.ExecuteInAdminRosterLockAsync` without the advisory lock: `var strategy = context.Database.CreateExecutionStrategy(); await strategy.ExecuteAsync(async () => { await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false); await operation(cancellationToken).ConfigureAwait(false); await transaction.CommitAsync(cancellationToken).ConfigureAwait(false); }).ConfigureAwait(false);`. `EraseMessagesAsync`: `await context.Database.ExecuteSqlAsync($"SELECT erase_avatar_conversation({conversationId})", cancellationToken).ConfigureAwait(false);` (parameterised `FormattableString`). |
| 7 | `api/Elmanhg.Application/Avatar/DeleteMyAvatarConversation/DeleteMyAvatarConversationCommand.cs` | `sealed record` | `public sealed record DeleteMyAvatarConversationCommand(Guid ConversationId) : IRequest, IAuditableCommand { public string AuditAction => "AvatarConversation.Delete"; public string AuditResourceType => "AvatarConversation"; public Guid? AuditResourceId => ConversationId; }` |
| 8 | `.../DeleteMyAvatarConversation/DeleteMyAvatarConversationValidator.cs` | validator | `RuleFor(x => x.ConversationId).ValidateRequired(ErrorCodes.AvatarConversationIdRequired);` |
| 9 | `.../DeleteMyAvatarConversation/DeleteMyAvatarConversationHandler.cs` | `sealed class : IRequestHandler<DeleteMyAvatarConversationCommand>` | ctor `(IAvatarConversationRepository avatarConversationRepository, IRuntimeSettings runtimeSettings, TimeProvider timeProvider, ICurrentUserService currentUserService)`. Handle: (1) auth guard → `UnauthorizedCoreException(UserNotAuthenticated)`; (2) `if (!await runtimeSettings.GetAsync(FeatureFlagRuntimeSettings.StudentsCanDeleteAvatarChats, cancellationToken).ConfigureAwait(false))` → `throw new BusinessRuleViolationCoreException(ErrorCodes.AvatarConversationDeletionDisabled)`; (3) `var studentId = …; var now = timeProvider.GetUtcNow();`; (4) `await avatarConversationRepository.ExecuteInTransactionAsync(async token => { var conversation = await avatarConversationRepository.FirstOrDefaultAsync(x => x.Id == request.ConversationId && x.StudentId == studentId, token).ConfigureAwait(false) ?? throw new NotFoundCoreException(ErrorCodes.AvatarConversationNotFound); await avatarConversationRepository.EraseMessagesAsync(conversation.Id, token).ConfigureAwait(false); conversation.Delete(now); await avatarConversationRepository.SaveChangesAsync(token).ConfigureAwait(false); }, cancellationToken).ConfigureAwait(false);`. Messages are not included in the load. The handler never touches usage rows. |
| 10 | `api/Elmanhg.Application/Avatar/Shared/StudentAvatarConversationResult.cs` | `sealed record`, client-facing | `(Guid Id, AvatarEntryPoint EntryPoint, Guid? SubjectId, string? SubjectName, Guid? LessonId, string? LessonName, Guid? SessionId, Guid? QuestionId, DateTimeOffset StartedAt, DateTimeOffset LastMessageAt, int MessageCount, string FirstQuestion)`. Names are plain `string` (`Subject.Name` and `Lesson.Name` are not `LocalizedText`), so there is no `.Localized()`. |
| 11 | `api/Elmanhg.Application/Avatar/Shared/StudentAvatarConversationDetailResult.cs` | `sealed record`, client-facing | `(Guid Id, AvatarEntryPoint EntryPoint, Guid? SubjectId, string? SubjectName, Guid? LessonId, string? LessonName, Guid? SessionId, Guid? QuestionId, DateTimeOffset StartedAt, DateTimeOffset LastMessageAt, int MessageCount, List<StudentAvatarMessageResult> Messages)` |
| 12 | `api/Elmanhg.Application/Avatar/Shared/StudentAvatarMessageResult.cs` | `sealed record`, client-facing | `(Guid Id, int Position, AvatarMessageRole Role, string Text, DateTimeOffset CreatedAt, List<AvatarCitationResult> Citations)` |
| 13 | `api/Elmanhg.Application/Avatar/Shared/StudentAvatarConversationResultGenerator.cs` | `static class` | `Generate(AvatarConversation conversation, string? subjectName, string? lessonName)`: `FirstQuestion` = text of the lowest-`Position` message, or `string.Empty`. `GenerateDetail(AvatarConversation conversation, string? subjectName, string? lessonName)`: messages ordered by `Position`; `Citations` = `message.Citations is null ? [] : AvatarMessageJson.ReadCitations(message.Citations)`. Mirrors `AdminAvatarConversationResultGenerator`. |

### Erasure SQL (migration #1 `Up`, exact)
```sql
CREATE FUNCTION reject_avatar_mutation_unless_erasing() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP = 'DELETE' AND OLD."ConversationId"::text = current_setting('elmanhg.erase_avatar_conversation', true) THEN
        RETURN OLD;
    END IF;
    RAISE EXCEPTION '% is append-only', TG_TABLE_NAME;
END; $$;
DROP TRIGGER avatar_messages_append_only ON "AvatarMessages";
CREATE TRIGGER avatar_messages_append_only BEFORE UPDATE OR DELETE ON "AvatarMessages" FOR EACH ROW EXECUTE FUNCTION reject_avatar_mutation_unless_erasing();
DROP TRIGGER avatar_training_records_append_only ON "AvatarTrainingRecords";
CREATE TRIGGER avatar_training_records_append_only BEFORE UPDATE OR DELETE ON "AvatarTrainingRecords" FOR EACH ROW EXECUTE FUNCTION reject_avatar_mutation_unless_erasing();
CREATE FUNCTION erase_avatar_conversation(p_conversation_id uuid) RETURNS integer LANGUAGE plpgsql AS $$
DECLARE erased integer;
BEGIN
    PERFORM set_config('elmanhg.erase_avatar_conversation', p_conversation_id::text, true);
    DELETE FROM "AvatarTrainingRecords" WHERE "ConversationId" = p_conversation_id;
    DELETE FROM "AvatarMessages" WHERE "ConversationId" = p_conversation_id;
    GET DIAGNOSTICS erased = ROW_COUNT;
    PERFORM set_config('elmanhg.erase_avatar_conversation', '', true);
    RETURN erased;
END; $$;
```
The `*_no_truncate` triggers are not touched.

### API tests (new files)
| # | Path |
|---|------|
| 14 | `api/Elmanhg.Tests/Application/Features/Avatar/GetMyAvatarConversations/GetMyAvatarConversationsHandlerTests.cs` |
| 15 | `api/Elmanhg.Tests/Application/Features/Avatar/GetMyAvatarConversations/GetMyAvatarConversationsValidatorTests.cs` |
| 16 | `api/Elmanhg.Tests/Application/Features/Avatar/GetMyAvatarConversation/GetMyAvatarConversationHandlerTests.cs` |
| 17 | `api/Elmanhg.Tests/Application/Features/Avatar/GetMyAvatarConversation/GetMyAvatarConversationValidatorTests.cs` |
| 18 | `api/Elmanhg.Tests/Application/Features/Avatar/DeleteMyAvatarConversation/DeleteMyAvatarConversationHandlerTests.cs` (stub `ExecuteInTransactionAsync` to invoke its callback, as in `SuspendUserHandlerTests`; use `AvatarTestData.StubConversations`) |
| 19 | `api/Elmanhg.Tests/Application/Features/Avatar/DeleteMyAvatarConversation/DeleteMyAvatarConversationValidatorTests.cs` |
| 20 | `api/Elmanhg.Tests/Integration/Avatar/AvatarTestData.cs` (existing; add) `public const string MyConversationsRoute = $"{Route}/my-conversations";`, `GetMyConversationsAsync(HttpClient client, string query)`, `GetMyConversationAsync(HttpClient client, Guid conversationId)`, `DeleteMyConversationAsync(HttpClient client, Guid conversationId)`, `ReadConversationIncludingDeletedAsync(ApiFactory factory, Guid conversationId)` (`IgnoreQueryFilters()` with a WHY comment: the test asserts the tombstone), `ReadTrainingRecordsAsync(ApiFactory factory, Guid conversationId)` |
| 21 | `api/Elmanhg.Tests/Integration/Avatar/MyAvatarConversationsEndpointTests.cs` |
| 22 | `api/Elmanhg.Tests/Integration/Avatar/AvatarConversationDeletionEndpointTests.cs`: `[Collection(RuntimeSettingsCollection.Name)]`, `IAsyncLifetime` whose `InitializeAsync` calls `ConfigurationTestData.ClearOverridesAsync(factory)` |
| 23 | `api/Elmanhg.Tests/Integration/Persistence/AvatarConversationErasureTests.cs` |

### Web
| # | Path | Type | Contract |
|---|------|------|----------|
| 24 | `web/src/features/avatar/hooks/avatarReducer.ts` (existing) | reducer | `AvatarState` adds `view: 'chat' \| 'history'` (initial `'chat'`). New `AvatarAction` members: `{ type: 'showHistory' }`, `{ type: 'showChat' }`, `{ type: 'resumed'; context: AvatarContextInput; conversationId: string; messages: AvatarMessage[] }`, `{ type: 'conversationDeleted'; conversationId: string }`. `open` also sets `view: 'chat'`. `showHistory` sets `view: 'history'`; `showChat` sets `view: 'chat'`. `resumed` returns `{ ...state, isOpen: true, view: 'chat', context, conversationId, messages }`. `conversationDeleted` returns `{ ...state, messages: [], conversationId: null }` when `state.conversationId === action.conversationId`, and otherwise `state`. `failed` keeps its append and also sets `conversationId: null` when `action.notice === 'conversationGone'`. |
| 25 | `web/src/features/avatar/api/avatarHistory.ts` | pure functions | `export const avatarHistoryPageSize = 20;` · `export function conversationTitle(item: { lessonName?: string \| null; subjectName?: string \| null }): string \| null` → `lessonName ?? subjectName ?? null` · `export function toResumed(detail: StudentAvatarConversationDetailResult): { context: AvatarContextInput; conversationId: string; messages: AvatarMessage[] }`. The context is `{ entryPoint }` plus `lessonId`, `sessionId`, `questionId` and `title` (`conversationTitle(detail)`), each added only when it is not null. Messages: `Student` → `{ id, kind: 'student', text }`; `Assistant` → `{ id, kind: 'assistant', text, citations }`, in the order given. |
| 26 | `web/src/features/avatar/hooks/useAvatarHistory.ts` | hook | `useAvatarHistory(page: number)` returns `{ list, resume: (conversationId: string) => Promise<void>, openingId: string \| null }`. `list = useGetMyAvatarConversations({ pageNumber: page, pageSize: avatarHistoryPageSize })`. `resume` sets `openingId`, runs `queryClient.fetchQuery({ ...getGetMyAvatarConversationQueryOptions(conversationId), staleTime: 0 })`, then `dispatch({ type: 'resumed', ...toResumed(detail) })`. On error it calls `toast.error(t('history.openFailed'))`. It clears `openingId` in `finally`. |
| 27 | `web/src/features/avatar/hooks/useDeleteAvatarConversation.ts` | hook | Returns `{ remove: (conversationId: string) => Promise<void>, isPending: boolean }` over `useDeleteMyAvatarConversation`. `onSuccess(_, { conversationId })`: `dispatch({ type: 'conversationDeleted', conversationId })`, `void queryClient.invalidateQueries({ queryKey: getGetMyAvatarConversationsQueryKey() })`, `toast.success(t('toast.deleted'))`. `onError(error)`: `toast.error(t(error instanceof ApiError && error.code === 'AVATAR_CONVERSATION_DELETION_DISABLED' ? 'toast.deleteDisabled' : 'toast.deleteFailed'))`. |
| 28 | `web/src/features/avatar/components/AvatarPanelHeader.tsx` | component | Props `{ status?: AvatarStatusResult }`. It contains the existing title row (Sparkles + `panel.title` in `Dialog.Title`, close button). Before the close button: when `state.view === 'chat'` and `status` is defined and `!status.examInProgress`, a ghost icon button (`History` icon, `aria-label={t('panel.history')}`, `min-w-11 px-0`) → `dispatch({ type: 'showHistory' })`. When `state.view === 'history'`, a ghost button with `ArrowLeft` (`rtl:rotate-180`) and the text `t('panel.backToChat')` → `dispatch({ type: 'showChat' })`. |
| 29 | `web/src/features/avatar/components/AvatarHistory.tsx` | component | Props `{ status: AvatarStatusResult }`. `const [page, setPage] = useState(1)`; `const [pendingDelete, setPendingDelete] = useState<StudentAvatarConversationResult \| null>(null)`. `<section aria-labelledby>` with `<h3>` `t('history.title')` (`font-display text-h3 font-semibold`). States: pending → `<div role="status" aria-busy="true">{t('history.loading')}</div>`; error → `<p role="alert" className="text-ui text-danger">{t('history.error')}</p>` plus a secondary Button `t('panel.retry')` → `refetch()`; when there are no items and `page > 1` → `setPage(page - 1)` (guarded render-phase update); no items → `<p className="text-ui text-text-muted">{t('history.empty')}</p>`; otherwise a `<ul className="flex flex-1 flex-col gap-2 overflow-y-auto">` of `AvatarHistoryItem`, plus `<Pagination>` when `totalPages > 1`. `canDelete = status.conversationDeletionEnabled`. Renders `DeleteAvatarConversationDialog`. |
| 30 | `web/src/features/avatar/components/AvatarHistoryItem.tsx` | component | Props `{ conversation: StudentAvatarConversationResult; canDelete: boolean; isOpening: boolean; onOpen: (id: string) => void; onDelete: (conversation: StudentAvatarConversationResult) => void }`. `<li className="flex items-start gap-2 rounded-md border border-border bg-surface p-3">`. Main `<button type="button" className="flex min-h-11 flex-1 flex-col items-start gap-1 text-start …focus ring…" aria-busy={isOpening} onClick={() => onOpen(id)}>`: line 1 `text-ui font-semibold` = `conversationTitle(c) ?? t('history.general')`; line 2 `text-caption text-text-muted` = `t('history.meta', { entryPoint: t(\`history.entryPoint.${c.entryPoint}\`), date })`, where date = `formatDate(new Date(c.lastMessageAt), i18n.language, 'arabic-indic', { dateStyle: 'medium', timeStyle: 'short' })`; line 3 `text-caption line-clamp-2` = `c.firstQuestion`. When `canDelete`, a danger-tinted ghost icon button (`Trash2`, `className="min-h-11 min-w-11 px-0 text-danger"`, `aria-label={t('history.delete', { title })}`) → `onDelete(c)`. |
| 31 | `web/src/features/avatar/components/DeleteAvatarConversationDialog.tsx` | component | Mirrors `DeleteExamPeriodDialog`. Props `{ conversation: StudentAvatarConversationResult \| null; onOpenChange: (open: boolean) => void; onConfirm: () => Promise<void>; isPending: boolean }`. `<Dialog open={conversation !== null}>` + `<DialogContent title={t('deleteDialog.title')}>`, body `t('deleteDialog.body')`, buttons secondary `deleteDialog.cancel` and danger `deleteDialog.confirm` (`disabled`/`aria-busy` = `isPending`; `void onConfirm().then(() => onOpenChange(false)).catch(() => undefined)`). |
| 32 | `web/src/test/avatarFixtures.ts` (existing; add) | fixtures | `myAvatarConversationId = '7d1c2b3a-0000-4000-8000-0000000c0de2'`; `myAvatarConversation(overrides?)` (Lesson entry point, `browseLessonId`, lessonName 'قانون أوم', subjectName 'الفيزياء', lastMessageAt `'2026-10-01T12:05:00Z'`, firstQuestion 'ما هو قانون أوم؟', messageCount 2); `myAvatarConversationsPage(items, overrides?)` (pageNumber 1, pageSize 20, totalItems = items.length, totalPages 1); `myAvatarConversationDetail(overrides?)` (the same context, two messages: Student 'ما هو قانون أوم؟' and Assistant `avatarReply().reply` with `avatarReply().citations`). |
| 33 | `web/src/features/avatar/api/avatarHistory.test.ts`, `web/src/features/avatar/components/AvatarHistory.test.tsx` | tests | See Test plan. |

### i18n (both files; values given en / ar)
| Key | en | ar |
|---|---|---|
| `panel.history` | Past chats | محادثاتي السابقة |
| `panel.backToChat` | Back to the chat | العودة إلى المحادثة |
| `history.title` | Past chats | محادثاتي السابقة |
| `history.loading` | Loading your chats… | جارٍ تحميل محادثاتك… |
| `history.error` | Your chats could not load. | تعذّر تحميل محادثاتك. |
| `history.empty` | You have no past chats yet. | لا توجد محادثات سابقة بعد. |
| `history.general` | General | عام |
| `history.meta` | {entryPoint} · {date} | {entryPoint} · {date} |
| `history.entryPoint.Lesson` / `.QuizQuestion` / `.ExamReview` / `.Global` | Lesson / Practice question / Exam review / General | درس / سؤال تدريب / مراجعة امتحان / عام |
| `history.delete` | Delete chat: {title} | حذف المحادثة: {title} |
| `history.openFailed` | The chat could not open. Try again. | تعذّر فتح المحادثة. حاول مرة أخرى. |
| `deleteDialog.title` | Delete this chat? | حذف هذه المحادثة؟ |
| `deleteDialog.body` | The chat and all its messages will be deleted for good. Today's message count does not change. | ستُحذف المحادثة وكل رسائلها نهائيًا. لن يتغيّر عدد رسائل اليوم. |
| `deleteDialog.cancel` / `deleteDialog.confirm` | Cancel / Delete | إلغاء / حذف |
| `toast.deleted` | The chat was deleted. | حُذفت المحادثة. |
| `toast.deleteFailed` | The chat could not be deleted. Try again. | تعذّر حذف المحادثة. حاول مرة أخرى. |
| `toast.deleteDisabled` | Deleting chats is not available right now. | حذف المحادثات غير متاح الآن. |
| `notice.conversationGone` | This chat no longer exists. Your next message starts a new chat. | هذه المحادثة لم تعد موجودة. ستبدأ رسالتك التالية محادثة جديدة. |

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `AvatarConversationIdRequired` | `AVATAR_CONVERSATION_ID_REQUIRED` | `GetMyAvatarConversationValidator`, `DeleteMyAvatarConversationValidator` | validation pipeline | 422 |
| `AvatarConversationDeletionDisabled` | `AVATAR_CONVERSATION_DELETION_DISABLED` | `DeleteMyAvatarConversationHandler` | `BusinessRuleViolationCoreException` | 400 |
| `AvatarConversationNotFound` (existing) | `AVATAR_CONVERSATION_NOT_FOUND` | detail and delete handlers (unknown, another student's, or deleted) | `NotFoundCoreException` | 404 |
| `AvatarExamInProgress` (existing) | `AVATAR_EXAM_IN_PROGRESS` | list and detail handlers via `AvatarGate` | `ForbiddenCoreException` | 403 |
| `AvatarConversationsPageNumberInvalid` / `PageSizeInvalid` (existing) | … | `GetMyAvatarConversationsValidator` | validation | 422 |
| `AvatarConversationModifiedConcurrently` (existing) | … | `AppDbContext.SaveChangesAsync` (xmin race during delete) | `ConflictCoreException` | 409 |
| `UserNotAuthenticated` (existing) | … | all three handlers | `UnauthorizedCoreException` | 401 |

Resx: `AVATAR_CONVERSATION_ID_REQUIRED` — en "The conversation id is required." / ar «معرّف المحادثة مطلوب.»; `AVATAR_CONVERSATION_DELETION_DISABLED` — en "Deleting chats is turned off." / ar «حذف المحادثات غير متاح حاليًا.»

## Domain behaviour
`AvatarConversation : AuditEntity, IAuditedEntity`
```csharp
public void Delete(DateTimeOffset deletedAt)
{
    var at = ToMicroseconds(deletedAt);
    SoftDelete();
    DeletedAt = at;
    MessageCount = 0;
    UpdatedBy = StudentId;
    UpdationDate = at;
}
```
- No guard: ownership is enforced by the handler's predicate (404, BOLA). Being idempotent keeps an execution-strategy retry safe.
- It does not touch `Messages` (they are not loaded; clearing the collection would make EF treat them as orphans). `StudentId`, `EntryPoint`, context ids, `StartedAt` and `LastMessageAt` are unchanged.
- No domain event is raised.

## API surface
| Method | Route | Policy | Request | Response |
|---|---|---|---|---|
| GET | `/api/avatar/my-conversations` (`Name = "GetMyAvatarConversations"`) | `DefaultCodes.AvatarChat` | `[FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20` → `GetMyAvatarConversationsQuery` | `PageData<StudentAvatarConversationResult>` |
| GET | `/api/avatar/my-conversations/{conversationId:guid}` (`Name = "GetMyAvatarConversation"`) | `DefaultCodes.AvatarChat` | `[FromRoute] Guid conversationId` → `GetMyAvatarConversationQuery` | `StudentAvatarConversationDetailResult` |
| DELETE | `/api/avatar/my-conversations/{conversationId:guid}` (`Name = "DeleteMyAvatarConversation"`) | `DefaultCodes.AvatarChat` | `[FromRoute] Guid conversationId` → `DeleteMyAvatarConversationCommand` | empty `200` (`Ok()`), `[ProducesResponseType(StatusCodes.Status200OK)]` |
| GET | `/api/avatar/status` (existing) | `AvatarChat` | — | `AvatarStatusResult` + `conversationDeletionEnabled` |

No new policy. No rate-limit attribute: these routes are not in `ConcurrencyCappedPolicies`.

## Test plan
### API unit
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 1 | `AvatarConversationTests` | `Delete_Conversation_SoftDeletesAndZeroesMessageCount` | `IsDeleted` true, `MessageCount` 0, `DeletedAt == UpdationDate ==` the truncated time, `UpdatedBy == StudentId` |
| 2 | `AvatarConversationTests` | `Delete_Conversation_KeepsContextAndTimes` | `StudentId`, `EntryPoint`, `LessonId`, `StartedAt`, `LastMessageAt` unchanged |
| 3 | `FeatureFlagRuntimeSettingsTests` | `Definitions_StudentsCanDeleteAvatarChats_IsBooleanFeatureFlagDefaultingOn` | `(Features, Boolean, true)` for key `features.studentsCanDeleteAvatarChats` |
| 4 | `RuntimeSettingValuesTests` (modify) | `Get_EveryRegisteredKey_DeserialisesToItsType` | also reads the new key |
| 5 | `GetAvatarStatusHandlerTests` (modify) | `Handle_FreeStudentWithTwoMessages_ReturnsCountsAndLimits` | expected record ends in `true` |
| 6 | `GetAvatarStatusHandlerTests` | `Handle_DeletionTurnedOff_ReportsConversationDeletionDisabled` | `new FakeRuntimeSettings().Set(StudentsCanDeleteAvatarChats, false)` → `ConversationDeletionEnabled` false |
| 7 | `GetMyAvatarConversationsHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | type + code; `FindPaginatedAsync` not received |
| 8 | ″ | `Handle_ExamInProgress_ThrowsAvatarExamInProgress` | `ForbiddenCoreException` + `AvatarExamInProgress` (open exam via `ExamSessionBuilder` + `AvatarTestData.StubSessions`) |
| 9 | ″ | `Handle_Student_ReturnsPageWithNamesAndFirstQuestion` | items mapped: subject and lesson names, `FirstQuestion`, `MessageCount`, page fields copied |
| 10 | ″ | `Handle_Student_FiltersByCaller` | the captured `filter` compiled: true for the caller's conversation, false for another student's |
| 11 | ″ | `Handle_GlobalConversation_ReturnsNullNames` | `SubjectName` and `LessonName` null, `FirstQuestion` set |
| 12 | `GetMyAvatarConversationsValidatorTests` | `Validate_Defaults_Passes` | no errors |
| 13 | ″ | `Validate_PageNumberZero_FailsWithPageNumberInvalid` | code |
| 14 | ″ | `Validate_PageSizeZero_FailsWithPageSizeInvalid` | code |
| 15 | ″ | `Validate_PageSizeOverStudentMax_FailsWithPageSizeInvalid` | `StudentConversationsMaxPageSize = 50`, size 51 → code |
| 16 | `GetMyAvatarConversationHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | type + code |
| 17 | ″ | `Handle_ExamInProgress_ThrowsAvatarExamInProgress` | type + code |
| 18 | ″ | `Handle_Own_ReturnsMessagesInOrderWithCitationsAndNames` | messages ordered by position, roles, texts, the citation parsed, names |
| 19 | ″ | `Handle_OtherStudentsConversation_ThrowsNotFound` | `StubConversations` with a conversation of another student → `NotFoundCoreException` + `AvatarConversationNotFound` |
| 20 | ″ | `Handle_Unknown_ThrowsNotFound` | same |
| 21 | `GetMyAvatarConversationValidatorTests` | `Validate_Id_Passes` / `Validate_EmptyId_FailsWithIdRequired` | code `AvatarConversationIdRequired` |
| 22 | `DeleteMyAvatarConversationHandlerTests` | `Handle_Own_ErasesSoftDeletesAndSaves` | `EraseMessagesAsync(conversation.Id)` received once; `IsDeleted` true; `MessageCount` 0; `SaveChangesAsync` `Received(1)`; `ExecuteInTransactionAsync` `Received(1)` |
| 23 | ″ | `Handle_Own_ErasesBeforeSaving` | `Received.InOrder(EraseMessagesAsync, SaveChangesAsync)` |
| 24 | ″ | `Handle_NoCurrentUser_ThrowsUnauthorized` | type + code; no transaction, erase or save |
| 25 | ″ | `Handle_DeletionDisabled_ThrowsDeletionDisabled` | `BusinessRuleViolationCoreException` + `AvatarConversationDeletionDisabled`; no transaction, erase or save; conversation not deleted |
| 26 | ″ | `Handle_OtherStudentsConversation_ThrowsNotFound` | `NotFoundCoreException` + code; no erase or save; their conversation not deleted |
| 27 | ″ | `Handle_Unknown_ThrowsNotFound` | same |
| 28 | `DeleteMyAvatarConversationValidatorTests` | `Validate_Id_Passes` / `Validate_EmptyId_FailsWithIdRequired` | code |

### API integration (Testcontainers)
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| 29 | `AvatarConversationErasureTests` | `Erase_Conversation_DeletesItsMessagesAndTrainingRecordsOnly` | two seeded conversations (the builder writes the training rows); `EraseMessagesAsync(a)` inside `ExecuteInTransactionAsync` via the repository → no messages or training rows for `a`; `b` intact; the function returns the message count (raw `SELECT erase_avatar_conversation(...)` read through `SqlQuery<int>`) |
| 30 | ″ | `Delete_MessageWithFlagForAnotherConversation_RejectedByDatabase` | in one transaction `set_config('elmanhg.erase_avatar_conversation', a, true)` then `DELETE` a message of `b` → `PostgresException` P0001; row kept |
| 31 | ″ | `Update_MessageWithFlagForSameConversation_RejectedByDatabase` | flag = `a`, `UPDATE` a message of `a` → P0001 |
| 32 | ″ | `Delete_TrainingRecordWithoutFlag_RejectedByDatabase` | P0001; row kept |
| 33 | ″ | `Erase_Conversation_ClearsTheFlagBeforeReturning` | inside one transaction, after the function, `current_setting('elmanhg.erase_avatar_conversation', true)` is `''` |
| 34 | ″ | `Truncate_AvatarMessages_RejectedByDatabase` | P0001 (#215 nit) |
| 35 | `MyAvatarConversationsEndpointTests` | `GetMine_Student_ReturnsOwnConversationsNewestFirst` | two own and one foreign conversation → only own ids, newest first, `lessonName` and `subjectName` set for a lesson conversation, `firstQuestion` |
| 36 | ″ | `GetMine_PageSizeOverMax_Returns422` | `code` `AVATAR_CONVERSATIONS_PAGE_SIZE_INVALID` |
| 37 | ″ | `GetMine_ExamInProgress_Returns403` | `AVATAR_EXAM_IN_PROGRESS` (`ExamTestData.SeedExamUnitAsync` + `StartAsync`) |
| 38 | ″ | `GetMine_Anonymous_Returns401` | status |
| 39 | ″ | `GetMine_Teacher_Returns403` | status |
| 40 | ″ | `GetMineDetail_Own_ReturnsMessagesWithoutReplyMetadata` | messages in order with `citations`; no `model`, `costUsd` or `context` property on any message |
| 41 | ″ | `GetMineDetail_OtherStudents_Returns404` | `AVATAR_CONVERSATION_NOT_FOUND` |
| 42 | `AvatarConversationDeletionEndpointTests` | `Delete_Own_ErasesMessagesAndTrainingRecords` | 200; tombstone `IsDeleted` true, `MessageCount` 0; no message rows; no training rows for the id |
| 43 | ″ | `Delete_Own_HiddenFromStudentAndAdminViews` | student list lacks it; student detail 404; admin list (`?search=<token>`) lacks it; admin detail 404 |
| 44 | ″ | `Delete_AfterSending_KeepsDailyUsage` | free student POSTs one Global message (fake AI), deletes the returned conversation → `GET /api/avatar/status` `messagesUsedToday` still 1; usage rows for the student still 1 |
| 45 | ″ | `Delete_Own_AuditedWithMessageCountAndNoText` | `ConfigurationTestData.ReadAuditsAsync(factory, "AvatarConversation.Delete", id)` single: Success, `ActorUserId` = student, `ResourceType` `AvatarConversation`; the diff has `messageCount` before 2, after 0; the diff string contains no question or reply text |
| 46 | ″ | `Delete_OtherStudents_Returns404AndKeepsMessages` | code; messages still there |
| 47 | ″ | `Delete_WhenSettingOff_Returns400AndKeepsMessages` | admin `PUT /api/configuration/settings/features.studentsCanDeleteAvatarChats` `{value:false}`; delete → 400 `AVATAR_CONVERSATION_DELETION_DISABLED`; messages kept; status `conversationDeletionEnabled` false |
| 48 | ″ | `SendMessage_DeletedConversationId_Returns404` | after the delete, POST with that `conversationId` → 404 `AVATAR_CONVERSATION_NOT_FOUND` |
| 49 | ″ | `Delete_Anonymous_Returns401` / `Delete_Admin_Returns403` | statuses |
| 50 | ″ | `GetConfigurationSettings_ChatDeletionFlag_ListedUnderFeaturesAndOn` | `Features` group holds the key with value true, default true, not overridden |
| 51 | ″ | `GetStatus_Default_ReportsConversationDeletionEnabled` | `conversationDeletionEnabled` true |

### Web (Vitest + RTL + MSW)
| # | File | `it(...)` | Asserts |
|---|-----------|-------------|---------|
| 52 | `avatarHistory.test.ts` | `maps a lesson conversation to its context, title and messages` | context `{entryPoint:'Lesson', lessonId, title:'قانون أوم'}`, student and assistant messages with citations, `conversationId` |
| 53 | ″ | `maps a global conversation without ids or title` | `context` toEqual `{ entryPoint: 'Global' }` |
| 54 | ″ | `keeps session and question ids for a quiz conversation` | `sessionId` and `questionId` present |
| 55 | ″ | `titles a conversation by lesson, then subject, then nothing` | three cases |
| 56 | `avatarReducer.test.ts` | `shows the history and returns to the chat` | `view` toggles; messages kept |
| 57 | ″ | `returns to the chat view when opened from a button` | `open` after `showHistory` → `'chat'` |
| 58 | ″ | `resumes a past conversation with its context and messages` | state fields after `resumed` |
| 59 | ″ | `clears the open conversation when it is deleted` | messages `[]`, id null |
| 60 | ″ | `keeps the open conversation when another one is deleted` | unchanged |
| 61 | ″ | `forgets the conversation id when the conversation is gone` | `failed` + `conversationGone` → id null, notice appended |
| 62 | `avatarErrors.test.ts` | `maps a missing conversation to the conversation-gone notice` | `AVATAR_CONVERSATION_NOT_FOUND` → `'conversationGone'` |
| 63 | `AvatarPanel.test.tsx` | `starts a new conversation after the open one no longer exists` | the first send ok, the second returns 404 `AVATAR_CONVERSATION_NOT_FOUND` → notice text shown; the third send body has `conversationId: null` |
| 64 | `AvatarHistory.test.tsx` | `lists past chats newest first with title, date and first question` | loading text, then two items in order (first-line text, «Lesson ·» meta) |
| 65 | ″ | `shows the empty state when there are no past chats` | `You have no past chats yet.` |
| 66 | ″ | `shows an error with retry and recovers` | the first list call 500 → alert; Retry → items |
| 67 | ″ | `reopens a chat in the panel and continues it` | click item → chat view shows the stored student and assistant texts and `Context: قانون أوم`; send → body has `conversationId: myAvatarConversationId`, `entryPoint: 'Lesson'`, `lessonId` |
| 68 | ″ | `deletes a chat after confirmation` | click Delete → dialog «Delete this chat?» → Delete → DELETE request seen, toast `The chat was deleted.`, list refetched without the item |
| 69 | ″ | `keeps the chat when the deletion is cancelled` | Cancel → no DELETE request (MSW spy count 0), item still listed |
| 70 | ″ | `hides delete when deleting chats is turned off` | status `conversationDeletionEnabled: false` → no `Delete chat:` button |
| 71 | ″ | `clears the open chat when that chat is deleted` | reopen, go back to history, delete it, back to chat → stored messages gone, greeting only |
| 72 | ″ | `shows an error toast when deleting fails` | DELETE 400 `AVATAR_CONVERSATION_DELETION_DISABLED` → toast `Deleting chats is not available right now.` |
| 73 | ″ | `moves to the next page` | totalPages 2 → Next page → request with `pageNumber=2` |
| 74 | ″ | `hides the history button during an exam` | status `examInProgress: true` → no `Past chats` button |
| 75 | ″ | `renders right to left in Arabic` | `lng 'ar'`, heading «محادثاتي السابقة», `dir="rtl"` |
| 76 | ″ | `has no axe violations` | `axe(panel)` with the list rendered |

All web tests use `renderApp('/student', { session: testSessions.student })`, open the panel through the «Assistant» button and press «Past chats», like `AvatarPanel.test.tsx`.

## Docs-sync (same change)
| Doc | Edit |
|---|---|
| `docs/avatar.md` | **HTTP**: rows for the three routes plus `conversationDeletionEnabled` in the status JSON; error rows for the two new codes. **Configuration**: the `Avatar:StudentConversationsMaxPageSize` row. **Conversation log, Append-only**: the erasure exemption (function, setting, DELETE only, one conversation, UPDATE always rejected, TRUNCATE unchanged, not a guard against the DB owner). **Privacy**: the student history view; delete; usage rows kept; audit `AvatarConversation.Delete` without text. **Retention**: kept until the student deletes (dev decision #215, 2026-10-02); no purge job; already-exported files (Decision 7). New section **Student history** (history view, reopen, delete, exam gate, runtime flag). **UI**: the history view, its states and the confirmation. **Not in this story**: remove the history line; keep "a retention purge". |
| `docs/training-data.md` | Append-only: `AvatarTrainingRecords` uses `reject_avatar_mutation_unless_erasing()` and the erasure path. Checklist: retention period `[x]` decided (kept until the student deletes the chat); erasure path `[x]` for avatar conversations (#271), with other sources and whole-account erasure not offered; consent line stays `[ ]` (follow-up issue). Export: a note on already-exported files (Decision 7). |
| `docs/configuration.md` | §2 row `features.studentsCanDeleteAvatarChats \| Features \| Boolean \| – \| true (constant, no Options key)`. |
| `docs/audit-log.md` | Audited commands row: `DeleteMyAvatarConversation \| AvatarConversation.Delete \| AvatarConversation \| command (student actor; the diff shows isDeleted and messageCount → 0; no message text)`. Add `AvatarConversation` to the audited entities list. |
| `docs/PRD.md` | §9 new **9.4 History and deletion** (own chats newest first, reopen and continue, delete with a confirmation, erases messages and training copies, the daily limit is unchanged, audited, the admin flag, refused during an exam). §10.6 v1 settings: add "students can delete assistant chats (feature flag, on by default)". §13: a sentence on student-initiated erasure of Avatar records. |
| `docs/claude-design-prompt.md` | §4 Assistant panel bullet (line ~141): «محادثاتي السابقة» button, the list contents, reopen, delete with «حذف هذه المحادثة؟», states. §5: rule "a deleted chat is gone everywhere; deleting never restores today's messages". |
| `docs/prototype.md` | One line like the admin-conversations note: the product has a past-chats view in the panel; the prototype does not simulate it. |
| `docs/deployment.md` | Row `Avatar__StudentConversationsMaxPageSize \| 50 \| 1 to 100`. |
| `docs/backlog.json` | E18: append the story "Student AI chat history and deletion" with the five sub-tasks from `00-story.md`. |
| `docs/implementation-report.md` | Row #215 in "Awaiting a dev decision": decided (kept until the student deletes; per-chat erasure #271). Known limits row "#235, #215": replace "Avatar conversations are kept indefinitely" with "Avatar chats are kept until the student deletes them; exported files are not recalled". |

## Definition of done
- [ ] Migration `AddAvatarConversationErasure` applies, and its Down restores `reject_append_only_mutation()` on both tables. `*_no_truncate` triggers are unchanged.
- [ ] `UPDATE` on `AvatarMessages`/`AvatarTrainingRecords` is rejected even with the erasure setting set; `DELETE` is rejected without it or for another conversation (tests 30–32, 34).
- [ ] `DELETE /api/avatar/my-conversations/{id}` erases messages and training rows, soft-deletes the conversation with `MessageCount 0`, all in one transaction.
- [ ] `AvatarMessageUsages` are untouched; `messagesUsedToday` is unchanged after a delete (test 44).
- [ ] The audit row `AvatarConversation.Delete` has the student actor, the conversation id and `messageCount` N→0, and no message text.
- [ ] Every student endpoint filters by `ICurrentUserService.UserId`; another student's id → 404; Teacher/Admin → 403; anonymous → 401.
- [ ] List and detail return 403 `AVATAR_EXAM_IN_PROGRESS` during an exam.
- [ ] A deleted conversation is absent from the admin list and its detail returns 404; sending to it returns 404.
- [ ] `features.studentsCanDeleteAvatarChats` appears under Features on the Configuration page, default on; off → 400 `AVATAR_CONVERSATION_DELETION_DISABLED` and the UI hides Delete.
- [ ] `AvatarStatusResult.ConversationDeletionEnabled` is exposed; the web fixture is updated.
- [ ] Both new error codes are in `ErrorCodes.cs`, `Messages.en.resx` and `Messages.ar.resx`.
- [ ] `Avatar:StudentConversationsMaxPageSize` is in `AvatarOptions` (validated range), `appsettings.example.json`, `api.env.example`, `ApiFactory` and `deployment.md`.
- [ ] Postman has the three requests; Delete is last.
- [ ] `api/openapi/v1.json` and `web/src/shared/api/generated` are regenerated; `npm run gen:api` produces no diff.
- [ ] The history UI is reachable only through the lazy `AvatarPanel`; `npm run build && npm run perf:budget` exits 0.
- [ ] Every new string is in `avatar/i18n/en.json` and `ar.json`; logical properties only; tokens only; icon buttons have `aria-label`; the axe test passes; the RTL test passes.
- [ ] Loading, empty, error+retry and paginated states exist in the history view.
- [ ] Every test in the Test plan exists with that name; only the two listed existing tests are modified; none skipped.
- [ ] `dotnet build` has no new warnings; `dotnet test` is green; `dotnet format --verify-no-changes` passes; `npx tsc -b`, `eslint --max-warnings=0`, `prettier --check` and `vitest run` pass.
- [ ] Every doc in Docs-sync is updated; the follow-up issues (the #215 leftover nits and the consent decision) are listed for the orchestrator.
