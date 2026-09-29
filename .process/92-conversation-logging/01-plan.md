# Plan — [E8.S4] Conversation logging (#92)

## Goal
Every successful avatar exchange is stored on the server as an `AvatarConversation` with append-only `AvatarMessage` rows. Each assistant row carries the context bundle and search results that were sent, the model id, prompt version, input/output tokens, cost (USD), stop reason, citations and how many earlier messages were sent as history. The API, not the browser, now owns the history the model sees. Admins get a new "محادثات المساعد" screen: recent conversations with search and filters, and a detail view of every message with that metadata. #109 (training records) and #110 (JSONL export) can read these rows by date, and they carry subject/unit/lesson/question references.

## Scope
**In:**
- api: entities, migration with an append-only trigger, conversation-aware `SendAvatarMessage` with server-side history, `conversationId` in the reply, admin list and detail queries with a new policy, error codes, Postman, OpenAPI.
- ai: the chat reply returns `costUsd`, which the service already computes for its logs.
- web: the avatar panel sends `conversationId` instead of `history`; new admin feature `avatarConversations` (list and detail pages, nav item).
- docs: avatar.md, ai-service.md, PRD, design prompt, prototype.md, deployment.md, env examples.

**Out:**
- A student-facing history or resume view (the story does not ask for one). Reloading the page starts a new conversation.
- Hashed-id training records (#109), JSONL export (#110), metrics and dashboards (#113), a retention purge or erasure job (see D10), a per-student concurrency cap (#115).

**Deferred:** none. Nothing in this story needs credentials or an online service: the fake AI client returns cost 0 and the real adapter already exists.

## Decisions
| # | Question | Decision | Why |
|---|---|---|---|
| D1 | Who owns the history sent to the model? | **Server.** `SendAvatarMessageCommand` loses `History` and gains `Guid? ConversationId`. The handler sends the last `Avatar:MaxHistoryMessages` stored messages, each cut to `Avatar:HistoryTurnMaxLength`. | The logged conversation must be exactly what the model saw. Client-sent history could diverge or be forged, which would poison eval and training data. The web client ships in the same PR, and old clients that still send `history` are ignored (STJ ignores unknown members), so nothing breaks. |
| D2 | When is a conversation created? | On the first **successful** reply. `ConversationId = null` starts one; the reply returns `conversationId`. Refusals, validation errors and AI failures write nothing, the same rule as the usage row. | The conversation, messages and usage row go in one `SaveChangesAsync`, so they are atomic. |
| D3 | Continuing someone else's conversation, or a different context | Load by `Id AND StudentId`; a miss is `404 AVATAR_CONVERSATION_NOT_FOUND` (BOLA-safe). If the entry point, lesson (Lesson entry) or session and question (QuizQuestion/ExamReview) differ from the conversation, the result is `400 AVATAR_CONVERSATION_CONTEXT_MISMATCH`. Both checks run before the model call and use nothing. | The web keys conversations by context already. The server enforces the same rule. |
| D4 | Where do model, prompt version, tokens, cost and context live? | On the **assistant** `AvatarMessage` row. Student rows have them all null. | The context and retrieved sources change per turn. PRD §15's conversation-level `model`/`prompt_version`/`context_json` becomes per reply, and the PRD is updated. |
| D5 | Context JSON shape | jsonb `{ "bundle": AiContextBundle, "sources": [AiChatSource] }`, serialised with `QuestionJson.SerializerOptions` (camelCase, camelCase enums, nulls omitted), the same way `TeacherThreadContext` does it. Source `content` is stored. | Eval and training need exactly what the model read. |
| D6 | Cost source | The AI service returns `costUsd` (its existing `estimate_cost_usd`, priced by `ELMANHG_AI_MODEL_*_USD_PER_MILLION_TOKENS`). .NET stores it as `decimal(12,6)`. | One pricing config, next to the model id setting. No duplicate price table in the API. |
| D7 | Concurrency (two sends on one conversation) | `AvatarConversation.Version` is xmin (`IsRowVersion`), and there is a unique index `(ConversationId, Position)`. Either collision becomes `409 AVATAR_CONVERSATION_MODIFIED_CONCURRENTLY` in `AppDbContext.SaveChangesAsync`, and the usage row rolls back with it. | Same pattern as `TeacherThread` and `Session`. |
| D8 | Append-only | Yes for `AvatarMessages`: `BEFORE UPDATE OR DELETE` and `BEFORE TRUNCATE` triggers reuse the existing `reject_append_only_mutation()` function from `AddSessionsAndAttempts`. `AvatarConversations` stays mutable (`LastMessageAt`, `MessageCount`). | PRD §13 requires append-only capture. This reuses the Attempts pattern. |
| D9 | Privacy | Rows are keyed by the real `StudentId` (operational data; #109 derives hashed-id copies). The send command stays non-auditable, so no text reaches `AuditLogs`. Nothing logs message text. Admin results show the student's **display name only** (no phone or email). Teachers have no access. The only student access is continuing their own conversation id. | PRD §14 privacy. PRD §8.4: teachers see display names only. |
| D10 | Retention | Kept indefinitely in v1. There is no purge job, and no endpoint deletes a conversation or message. The retention period and erasure path are part of #109's "retention and privacy review checklist". Documented in avatar.md. | **Assumed product answer**: the eval and training value needs the history, and the PRD has no account-deletion flow. |
| D11 | Admin access policy | New `DefaultCodes.AvatarConversationsView = "AvatarConversations.View"`, Admin only. New PRD §16 row. Web capability `avatarConversationsView`. | PRD §16 has no row that fits. `TrainingData.Export` means exports, not viewing. |
| D12 | Admin search semantics | `search` is trimmed and lower-cased. It matches a substring of **any message text** in the conversation (`ToLower().Contains`, the AuditLogs/Questions filter style) **or** a student whose `DisplayName` contains it. Filters: `entryPoint`, `from` (inclusive) and `to` (exclusive), both on `LastMessageAt`. Order: `LastMessageAt desc, Id desc`. Paged with `PageData`. | "Recent conversations with search." Filtering and ordering on the same column is predictable. |
| D13 | Admin list preview | Returns the first student message (`Position 0`) as `FirstQuestion`, loaded through a filtered include. The UI clamps it to two lines. | No denormalised preview column. |
| D14 | `Avatar:HistoryTurnMaxLength` | Kept, now meaning "cut each stored turn to this length when sending it back as history". `AVATAR_HISTORY_TOO_LONG` and `AVATAR_HISTORY_INVALID` are removed, along with their resx entries, `AvatarTurn`, `AvatarTurnRole` and the validator rules. `AvatarStatusResult.MaxHistoryMessages` stays (informational; the web no longer reads it). | Replies can be up to about 1024 tokens, and the AI service rejects history turns over 4000 characters. |
| D15 | Domain guards on `RecordExchange` | None. The validator already rejects a blank question, and `HttpAiServiceClient` already rejects a blank reply as 503. `Position` comes from `MessageCount`, so it is always contiguous. | No unreachable guards, and no new domain error code. |
| D16 | Where the conversation is saved | `avatarMessageUsageRepository.SaveChangesAsync` stays the single save. Both repositories share the scoped `AppDbContext`. | The existing handler tests assert that call. One transaction. |
| D17 | Admin UI (no prototype screen exists) | A list page in the audit and payments style, plus a detail page at `/admin/avatar-conversation/$conversationId`. Content is defined below and added to `docs/claude-design-prompt.md` §4. `docs/prototype.md` notes that the prototype does not simulate it, as it does for payments. | Docs-sync rule: the design prompt must describe every UI page. |
| D18 | Morabh reuse | Paging: Core.DDD `IRepository.FindPaginatedAsync` / `PageData` (vendored from Morabh `Core/Core.DDD/Repositories/IRepository.cs`). Repository base: Morabh `Core/Core.EntityFrameworkCore/Repositories/Repository.cs` (vendored). Morabh has no conversation, chat or LLM-usage logging (searched `Morabh.Application`, `Morabh.Domain`, `Core`), so the entities, handlers and UI are **new — no Morabh equivalent**. They are modelled on the in-repo `TeacherThread`/`TeacherMessage`, `GetPaymentLog` and the audit-log UI. | Reuse-first rule. |

## Existing code touched
| File | Change |
|---|---|
| `api/Elmanhg.Domain/SharedKernel/DefaultCodes.cs` | add `public const string AvatarConversationsView = "AvatarConversations.View";` after `AvatarChat` |
| `api/Elmanhg.Api/Authorization/PermissionMatrixPolicies.cs` | `.AddPolicy(DefaultCodes.AvatarConversationsView, policy => policy.RequireRole(Admin))` |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | AVATAR group: remove `AvatarHistoryTooLong`, `AvatarHistoryInvalid`; add the 8 constants in "Error codes" |
| `api/Elmanhg.Api/Resources/Messages.ar.resx`, `Messages.en.resx` | remove `AVATAR_HISTORY_TOO_LONG`, `AVATAR_HISTORY_INVALID`; add the 8 keys below |
| `api/Elmanhg.Application/Avatar/SendAvatarMessage/SendAvatarMessageCommand.cs` | `public sealed record SendAvatarMessageCommand(AvatarEntryPoint EntryPoint, Guid? LessonId, Guid? SessionId, Guid? QuestionId, Guid? ConversationId, string Message) : IRequest<AvatarReplyResult>;` (drop the `Application.Avatar.Shared` using) |
| `api/Elmanhg.Application/Avatar/SendAvatarMessage/SendAvatarMessageValidator.cs` | delete the `History` rules, `RuleForEach(History)` and `Alternates`; keep every other rule unchanged |
| `api/Elmanhg.Application/Avatar/SendAvatarMessage/SendAvatarMessageHandler.cs` | add ctor dependency `IAvatarConversationRepository avatarConversationRepository`, inserted right after `IAvatarMessageUsageRepository avatarMessageUsageRepository`; new `Handle` (see Files to create #6 for the steps) |
| `api/Elmanhg.Application/Avatar/Shared/AvatarReplyResult.cs` | `public sealed record AvatarReplyResult(Guid ConversationId, string Reply, List<AvatarCitationResult> Citations, int DailyMessageLimit, int MessagesUsedToday, int MessagesRemainingToday, string Model, string PromptVersion);` |
| `api/Elmanhg.Application/Avatar/Shared/AvatarTurn.cs`, `AvatarTurnRole.cs` | **delete** |
| `api/Elmanhg.Application/Shared/AiService/AiChatReply.cs` | `public sealed record AiChatReply(string Reply, string Model, string PromptVersion, int InputTokens, int OutputTokens, string? StopReason, IReadOnlyList<string> Citations, decimal CostUsd);` |
| `api/Elmanhg.Application/Shared/Options/AvatarOptions.cs` | add `[Range(1, 200)] public int AdminConversationsMaxPageSize { get; set; } = 100;` and `[Range(1, 500)] public int ConversationSearchMaxLength { get; set; } = 200;`; change the `HistoryTurnMaxLength` comment to "Each stored turn is cut to this when sent back as history; the AI service rejects turns over 4000." |
| `api/Elmanhg.Infrastructure/AiService/FakeAiServiceClient.cs` | `new AiChatReply(FakeReply, FakeModel, FakePromptVersion, 0, 0, "end_turn", citations, 0m)` |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | DbSets `AvatarConversations`, `AvatarMessages`; consts; mapping; soft-delete filters; two catch clauses (see Domain behaviour, EF) |
| `api/Elmanhg.Infrastructure/DependencyInjection.cs` | `services.AddScoped<IAvatarConversationRepository, AvatarConversationRepository>();` next to the usage repository |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | regenerated by `dotnet ef migrations add AddAvatarConversations -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api` |
| `api/Elmanhg.Api/appsettings.example.json` | Avatar section: add `"AdminConversationsMaxPageSize": 100, "ConversationSearchMaxLength": 200` |
| `api/openapi/v1.json` | regenerated by `dotnet build` |
| `api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs` | add `["Avatar:AdminConversationsMaxPageSize"] = "100"`, `["Avatar:ConversationSearchMaxLength"] = "200"` |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | append `twentyNinth => twentyNinth.Should().EndWith("_AddAvatarConversations")` |
| `api/Elmanhg.Tests/Integration/Authorization/PermissionMatrixPolicyTests.cs` | rows `{ "AvatarConversations.View", "Student", false }, { …, "Teacher", false }, { …, "Admin", true }` |
| `api/Elmanhg.Tests/Application/Features/Avatar/AvatarTestData.cs` | harness: `IAvatarConversationRepository Conversations` substitute, `AvatarConversation? AddedConversation` captured through `Arg.Do` on `AddAsync`, handler ctor updated, `AiChatReply` gains `0.0021m`, `LessonCommand` and `QuestionCommand` pass `null` for `ConversationId`; new `public static void StubConversations(IAvatarConversationRepository repository, params AvatarConversation[] conversations)` that mirrors `StubSessions` for `FirstOrDefaultAsync` |
| `api/Elmanhg.Tests/Application/Features/Avatar/SendAvatarMessage/SendAvatarMessageHandlerTests.cs` | modify: the `AiChatReply` constructions get a trailing `CostUsd`; `Handle_Success_RecordsUsageAndReturnsCountsAndCitations` also asserts `result.ConversationId == _harness.AddedConversation!.Id`; **delete** `Handle_HistoryTurns_MapsRolesToAiChatMessages` (replaced by the conversation tests) |
| `api/Elmanhg.Tests/Application/Features/Avatar/SendAvatarMessage/SendAvatarMessageHandlerWithheldSourcesTests.cs` | modify: `AiChatReply` ctor gets `, 0m` |
| `api/Elmanhg.Tests/Application/Features/Avatar/SendAvatarMessage/SendAvatarMessageValidatorTests.cs` | modify: command ctor calls pass `null` instead of `[]`; `Validate_LessonEntryWithLessonId_Passes` becomes `Lesson() with { ConversationId = Guid.NewGuid() }`; **delete** `Validate_HistoryOverMax_FailsWithAvatarHistoryTooLong`, `Validate_HistoryStartingWithAssistant_FailsWithAvatarHistoryInvalid`, `Validate_OddHistory_FailsWithAvatarHistoryInvalid`, `Validate_BlankTurnContent_FailsWithAvatarHistoryInvalid`, `Validate_TurnContentOverMax_FailsWithAvatarHistoryInvalid` and the `Turn` helper |
| `api/Elmanhg.Tests/Infrastructure/AiService/HttpAiServiceClientTests.cs` | modify: `ReplyBody` gains `,\"costUsd\":0.000105`; `ChatAsync_Success_ReturnsReply` also asserts `reply.CostUsd.Should().Be(0.000105m)` |
| `api/Elmanhg.Tests/Integration/Avatar/AvatarTestData.cs` | add helpers: `SeedConversationAsync(ApiFactory, AvatarConversation)`, `ReadConversationAsync(ApiFactory, Guid)` (`Include(Messages)`, no tracking), `GetConversationsAsync(HttpClient, string query)`, `GetConversationAsync(HttpClient, Guid)` |
| `api/Elmanhg.Tests/Integration/Avatar/AvatarMessageEndpointTests.cs` | **unchanged**. Its bodies still send `history`, which the API now ignores; this proves old clients keep working |
| `ai/src/elmanhg_ai/pipelines/chat.py` | `ChatResult` gains `cost_usd: Decimal` after `stop_reason` (before `citations`); compute `cost = estimate_cost_usd(...)` once and use it for the log field and the result |
| `ai/src/elmanhg_ai/api/chat/schemas.py` | `ChatOut` gains `cost_usd: float` after `stop_reason` (camelCase alias `costUsd`) |
| `ai/src/elmanhg_ai/api/chat/router.py` | `cost_usd=float(result.cost_usd)` |
| `ai/openapi/v1.json` | regenerated: `cd ai && uv run python -m elmanhg_ai.openapi_export` |
| `ai/tests/unit/test_chat_pipeline.py` | modify `test_chat_run_returns_reply_with_model_and_prompt_version` (expected `ChatResult` gains `cost_usd=Decimal("0.000000")`); add one test (Test plan) |
| `ai/tests/integration/test_chat_endpoint.py` | add one test (Test plan) |
| `web/src/features/avatar/api/avatarContext.ts` | delete `historyFor`; `toSendRequest(context: AvatarContextInput, conversationId: string \| null, message: string): SendAvatarMessageCommand` returns `{ entryPoint, lessonId ?? null, sessionId ?? null, questionId ?? null, conversationId, message: message.trim() }` |
| `web/src/features/avatar/hooks/avatarReducer.ts` | `AvatarState.turns` becomes `conversationId: string \| null` (initial `null`); `'replied'` action is `{ type: 'replied'; reply: AvatarReplyResult }` and sets `conversationId: action.reply.conversationId`; `'open'` with another context resets `messages: []` and `conversationId: null`; drop the `AvatarTurn` import |
| `web/src/features/avatar/hooks/useAvatarChat.ts` | `send(text: string)`; body `toSendRequest(state.context, state.conversationId, question)`; `dispatch({ type: 'replied', reply })` |
| `web/src/features/avatar/components/AvatarComposer.tsx` | `await chat.send(values.message);` |
| `web/src/test/avatarFixtures.ts` | `export const avatarConversationId = '7d1c2b3a-0000-4000-8000-00000000c0de';`; `avatarReply` gains `conversationId: avatarConversationId` |
| `web/src/features/avatar/api/avatarContext.test.ts`, `hooks/avatarReducer.test.ts`, `components/AvatarPanel.test.tsx` | modify and delete per Test plan |
| `web/src/features/session/permissions.ts` | add `'avatarConversationsView'` to `capabilities` and to `admin` |
| `web/src/features/session/permissions.test.ts` | add one test |
| `web/src/features/shell/navConfig.ts` | admin item `{ key: 'avatarConversations', to: '/admin/avatar-conversations', labelKey: 'nav.admin.avatarConversations', icon: MessagesSquare, capability: 'avatarConversationsView' }` between `audit` and `export` (import `MessagesSquare` from `lucide-react`) |
| `web/src/features/shell/i18n/ar.json`, `en.json` | `nav.admin.avatarConversations`: «محادثات المساعد» / "Assistant conversations" |
| `web/src/features/shell/pages/MorePage.test.tsx` | expected list: `'Exam blueprints','Users','Payments','Audit log','Assistant conversations','Data export'` |
| `web/src/app/i18n.ts` | import `avatarConversationsLocales` from `@/features/avatarConversations/locales`; register `avatarConversations` in both resource maps and `ns` |
| `web/src/shared/i18n/ar.json`, `en.json` | `errors.AVATAR_CONVERSATION_NOT_FOUND`: «المحادثة غير موجودة.» / "The conversation was not found." |
| `web/src/routeTree.gen.ts` | regenerated by `npm --prefix web run build` |
| `web/src/shared/api/generated/**` | regenerated by `npm --prefix web run gen:api` (the `avatarTurn*` models disappear; `avatar-conversations/*` appear) |
| `postman/elmanhg.postman_collection.json` | see API surface → Postman |
| `deploy/api.env.example` | under the Avatar block: `# Avatar__AdminConversationsMaxPageSize=100`, `# Avatar__ConversationSearchMaxLength=200` |
| `docs/avatar.md`, `docs/ai-service.md`, `docs/PRD.md`, `docs/claude-design-prompt.md`, `docs/prototype.md`, `docs/deployment.md` | see "Docs" |

## Files to create
Namespaces are file-scoped and mirror folders. No comments except WHY invariants. Every `await` has `.ConfigureAwait(false)` outside controllers.

### api — Domain (`namespace Elmanhg.Domain.Avatar;`)
| # | Path | Type | Contract |
|---|---|---|---|
| 1 | `api/Elmanhg.Domain/Avatar/AvatarConversation.cs` | `public class AvatarConversation : AuditEntity` | Properties (private set): `Guid StudentId`, `AvatarEntryPoint EntryPoint`, `Guid? SubjectId`, `Guid? UnitId`, `Guid? LessonId`, `Guid? SessionId`, `Guid? QuestionId`, `DateTimeOffset StartedAt`, `DateTimeOffset LastMessageAt`, `int MessageCount`, `uint Version`, `List<AvatarMessage> Messages = []`. Ctor `private AvatarConversation(Guid id, Guid? createdBy) : base(id, createdBy) { }`. Methods: `static AvatarConversation Start(Guid studentId, AvatarEntryPoint entryPoint, Guid? subjectId, Guid? unitId, Guid? lessonId, Guid? sessionId, Guid? questionId, DateTimeOffset startedAt)`; `bool IsFor(AvatarEntryPoint entryPoint, Guid? lessonId, Guid? sessionId, Guid? questionId)`; `IReadOnlyList<AvatarMessage> RecentMessages(int count)`; `void RecordExchange(string question, AvatarAssistantReply reply, DateTimeOffset askedAt, DateTimeOffset repliedAt)`; `private static DateTimeOffset ToMicroseconds(DateTimeOffset value)` (with the same WHY comment as `TeacherThread`). Bodies in Domain behaviour. |
| 2 | `api/Elmanhg.Domain/Avatar/AvatarMessage.cs` | `public class AvatarMessage : Entity` | Properties (private set): `Guid ConversationId`, `int Position`, `AvatarMessageRole Role`, `string Text = string.Empty`, `DateTimeOffset CreatedAt`, `string? Model`, `string? PromptVersion`, `int? InputTokens`, `int? OutputTokens`, `decimal? CostUsd`, `string? StopReason`, `int? HistoryMessageCount`, `string? Context`, `string? Citations`. Ctor `private AvatarMessage(Guid id) : base(id) { }`. `internal static AvatarMessage FromStudent(Guid conversationId, int position, string text, DateTimeOffset createdAt)`; `internal static AvatarMessage FromAssistant(Guid conversationId, int position, AvatarAssistantReply reply, DateTimeOffset createdAt)` copies every reply field (`Text = reply.Text`). |
| 3 | `api/Elmanhg.Domain/Avatar/AvatarMessageRole.cs` | enum | `public enum AvatarMessageRole { Student, Assistant }` |
| 4 | `api/Elmanhg.Domain/Avatar/AvatarAssistantReply.cs` | value object | `public sealed record AvatarAssistantReply(string Text, string Model, string PromptVersion, int InputTokens, int OutputTokens, decimal CostUsd, string? StopReason, int HistoryMessageCount, string Context, string Citations);` |
| 5 | `api/Elmanhg.Domain/Avatar/IAvatarConversationRepository.cs` | repo interface | `public interface IAvatarConversationRepository : IRepository<AvatarConversation> { }` (the base covers every query) |

### api — Application
| # | Path | Type | Contract |
|---|---|---|---|
| 6 | `api/Elmanhg.Application/Avatar/SendAvatarMessage/SendAvatarMessageHandler.Conversation.cs` | `public sealed partial class SendAvatarMessageHandler` | `private async Task<AvatarConversation?> LoadConversationAsync(SendAvatarMessageCommand request, Guid studentId, CancellationToken cancellationToken)`: if `request.ConversationId is not { } conversationId`, return `null`. Otherwise `avatarConversationRepository.FirstOrDefaultAsync(x => x.Id == conversationId && x.StudentId == studentId, cancellationToken, include: query => query.Include(x => x.Messages))` (tracked) `?? throw new NotFoundCoreException(ErrorCodes.AvatarConversationNotFound)`; if `!conversation.IsFor(request.EntryPoint, request.LessonId, request.SessionId, request.QuestionId)`, throw `new BadRequestCoreException(ErrorCodes.AvatarConversationContextMismatch)`; return it. `private List<AiChatMessage> HistoryOf(AvatarConversation? conversation)`: `[]` when null, else `conversation.RecentMessages(avatarOptions.Value.MaxHistoryMessages).Select(x => new AiChatMessage(x.Role == AvatarMessageRole.Student ? AiChatRole.User : AiChatRole.Assistant, AvatarText.Truncate(x.Text, avatarOptions.Value.HistoryTurnMaxLength))).ToList()`. `private static AvatarConversation StartConversation(SendAvatarMessageCommand request, Guid studentId, AiContextBundle bundle, DateTimeOffset startedAt)` returns `AvatarConversation.Start(studentId, request.EntryPoint, bundle.Subject?.Id, bundle.Unit?.Id, bundle.Lesson?.Id, request.EntryPoint is AvatarEntryPoint.QuizQuestion or AvatarEntryPoint.ExamReview ? request.SessionId : null, bundle.Question?.Id, startedAt)`. |
| — | `SendAvatarMessageHandler.cs` (modified) `Handle` order | | (1) current-user guard → 401. (2) `now`; exam gate. (3) entitlement; `used = EnsureMessageAvailableAsync`. (4) `var conversation = await LoadConversationAsync(...)`. (5) `context = LoadContextAsync(...)`. (6) `history = HistoryOf(conversation)`. (7) `sources` as today. (8) `reply = aiServiceClient.ChatAsync(new AiChatRequest(context.Bundle, history, request.Message.Trim(), sources), ...)`. (9) `citations = AvatarCitationMapper.Map(reply.Citations, context.Matches, context.LessonId)`. (10) `repliedAt = timeProvider.GetUtcNow()`. (11) `var isNew = conversation is null; conversation ??= StartConversation(request, userId, context.Bundle, now);`. (12) `conversation.RecordExchange(request.Message, new AvatarAssistantReply(reply.Reply, reply.Model, reply.PromptVersion, reply.InputTokens, reply.OutputTokens, reply.CostUsd, reply.StopReason, history.Count, AvatarMessageJson.WriteContext(context.Bundle, sources), AvatarMessageJson.WriteCitations(citations)), now, repliedAt);`. (13) `if (isNew) await avatarConversationRepository.AddAsync(conversation, …)`. (14) usage `AddAsync` then `avatarMessageUsageRepository.SaveChangesAsync` (the single save). (15) `return new AvatarReplyResult(conversation.Id, reply.Reply, citations, limit, used + 1, Math.Max(0, limit - used - 1), reply.Model, reply.PromptVersion);` |
| 7 | `api/Elmanhg.Application/Avatar/Shared/AvatarMessageContext.cs` | record | `public sealed record AvatarMessageContext(AiContextBundle Bundle, IReadOnlyList<AiChatSource> Sources);` (admin-facing; no localisation) |
| 8 | `api/Elmanhg.Application/Avatar/Shared/AvatarMessageJson.cs` | `public static class AvatarMessageJson` | `static string WriteContext(AiContextBundle bundle, IReadOnlyList<AiChatSource> sources)` → `JsonSerializer.Serialize(new AvatarMessageContext(bundle, sources), QuestionJson.SerializerOptions)`; `static AvatarMessageContext ReadContext(string json)` (`?? throw new InvalidOperationException("Avatar message context is empty.")`); `static string WriteCitations(IReadOnlyList<AvatarCitationResult> citations)`; `static List<AvatarCitationResult> ReadCitations(string json)` (`?? []`). |
| 9 | `api/Elmanhg.Application/Avatar/Shared/AdminAvatarConversationResult.cs` | admin result | `public sealed record AdminAvatarConversationResult(Guid Id, Guid StudentId, string StudentName, AvatarEntryPoint EntryPoint, Guid? SubjectId, string? SubjectName, Guid? LessonId, string? LessonName, Guid? QuestionId, DateTimeOffset StartedAt, DateTimeOffset LastMessageAt, int MessageCount, string FirstQuestion);` |
| 10 | `api/Elmanhg.Application/Avatar/Shared/AdminAvatarConversationDetailResult.cs` | admin result | `public sealed record AdminAvatarConversationDetailResult(Guid Id, Guid StudentId, string StudentName, AvatarEntryPoint EntryPoint, Guid? SubjectId, string? SubjectName, Guid? UnitId, string? UnitName, Guid? LessonId, string? LessonName, Guid? SessionId, Guid? QuestionId, DateTimeOffset StartedAt, DateTimeOffset LastMessageAt, int MessageCount, int TotalInputTokens, int TotalOutputTokens, decimal? TotalCostUsd, List<AdminAvatarMessageResult> Messages);` |
| 11 | `api/Elmanhg.Application/Avatar/Shared/AdminAvatarMessageResult.cs` | admin result | `public sealed record AdminAvatarMessageResult(Guid Id, int Position, AvatarMessageRole Role, string Text, DateTimeOffset CreatedAt, string? Model, string? PromptVersion, int? InputTokens, int? OutputTokens, decimal? CostUsd, string? StopReason, int? HistoryMessageCount, List<AvatarCitationResult> Citations, AvatarMessageContext? Context);` |
| 12 | `api/Elmanhg.Application/Avatar/Shared/AdminAvatarConversationResultGenerator.cs` | static generator | `static AdminAvatarConversationResult Generate(AvatarConversation conversation, string studentName, string? subjectName, string? lessonName)`: `FirstQuestion = conversation.Messages.OrderBy(x => x.Position).FirstOrDefault()?.Text ?? string.Empty`. `static AdminAvatarConversationDetailResult GenerateDetail(AvatarConversation conversation, string studentName, string? subjectName, string? unitName, string? lessonName)`: messages ordered by `Position` and mapped with `Message`; `TotalInputTokens = Sum(InputTokens ?? 0)`; `TotalOutputTokens` likewise; `TotalCostUsd = Messages.Any(x => x.CostUsd != null) ? Messages.Sum(x => x.CostUsd ?? 0m) : null`. `private static AdminAvatarMessageResult Message(AvatarMessage message)`: `Citations = message.Citations is null ? [] : AvatarMessageJson.ReadCitations(message.Citations)`, `Context = message.Context is null ? null : AvatarMessageJson.ReadContext(message.Context)`. |
| 13 | `api/Elmanhg.Application/Avatar/GetAvatarConversations/GetAvatarConversationsQuery.cs` | query | `public sealed record GetAvatarConversationsQuery(string? Search, AvatarEntryPoint? EntryPoint, DateTimeOffset? From, DateTimeOffset? To, int PageNumber = 1, int PageSize = 20) : IRequest<PageData<AdminAvatarConversationResult>>;` |
| 14 | `…/GetAvatarConversations/GetAvatarConversationsValidator.cs` | validator (ctor `IOptions<AvatarOptions> avatarOptions`) | `PageNumber` `.ValidateMin(1, ErrorCodes.AvatarConversationsPageNumberInvalid)`; `PageSize` `.ValidateRange(1, options.AdminConversationsMaxPageSize, ErrorCodes.AvatarConversationsPageSizeInvalid)`; `Search` `.ValidateMaxLength(options.ConversationSearchMaxLength, ErrorCodes.AvatarConversationsSearchTooLong)`; `EntryPoint` `.IsInEnum().WithErrorCode(ErrorCodes.AvatarConversationsEntryPointInvalid)`; `RuleFor(x => x).Must(x => x.From is null \|\| x.To is null \|\| x.From < x.To).WithErrorCode(ErrorCodes.AvatarConversationsDateRangeInvalid)` |
| 15 | `…/GetAvatarConversations/GetAvatarConversationsFilter.cs` | `public static class GetAvatarConversationsFilter` | `static string? Term(GetAvatarConversationsQuery query)` returns the trimmed, `ToLowerInvariant` search, or `null` when blank. `static Expression<Func<AvatarConversation, bool>> Build(GetAvatarConversationsQuery query, IReadOnlyCollection<Guid> matchingStudentIds)` returns `x => (entryPoint == null \|\| x.EntryPoint == entryPoint) && (from == null \|\| x.LastMessageAt >= from) && (to == null \|\| x.LastMessageAt < to) && (term == null \|\| matchingStudentIds.Contains(x.StudentId) \|\| x.Messages.Any(m => m.Text.ToLower().Contains(term)))`, with `from`/`to` as `ToUniversalTime()` locals (as in `GetPaymentLogFilter`). |
| 16 | `…/GetAvatarConversations/GetAvatarConversationsHandler.cs` | `sealed class GetAvatarConversationsHandler(IAvatarConversationRepository avatarConversationRepository, IUserRepository userRepository, ILessonRepository lessonRepository, ISubjectRepository subjectRepository) : IRequestHandler<GetAvatarConversationsQuery, PageData<AdminAvatarConversationResult>>` | (1) `term = Term(request)`. (2) `matching = term is null ? [] : (await userRepository.FindAsync(x => x.DisplayName.ToLower().Contains(term), ct, asNoTracking: true)).Select(x => x.Id).ToList()`. (3) `page = FindPaginatedAsync(request.PageNumber, request.PageSize, ct, filter: Build(request, matching), include: q => q.Include(x => x.Messages.Where(m => m.Position == 0)), orderBy: q => q.OrderByDescending(x => x.LastMessageAt).ThenByDescending(x => x.Id), asNoTracking: true)`. (4) distinct student, lesson and subject ids from `page.Items`; one `FindAsync(x => ids.Contains(x.Id), asNoTracking: true)` each on users, lessons and subjects, turned into dictionaries. (5) Return a `PageData` shaped like `GetPaymentLogHandler`: items `Generate(x, students.GetValueOrDefault(x.StudentId)?.DisplayName ?? string.Empty, subjectName, lessonName)`. |
| 17 | `api/Elmanhg.Application/Avatar/GetAvatarConversation/GetAvatarConversationQuery.cs` | query | `public sealed record GetAvatarConversationQuery(Guid ConversationId) : IRequest<AdminAvatarConversationDetailResult>;` |
| 18 | `…/GetAvatarConversation/GetAvatarConversationHandler.cs` | `sealed class GetAvatarConversationHandler(IAvatarConversationRepository avatarConversationRepository, IUserRepository userRepository, ISubjectRepository subjectRepository, ICurriculumUnitRepository unitRepository, ILessonRepository lessonRepository) : IRequestHandler<GetAvatarConversationQuery, AdminAvatarConversationDetailResult>` | (1) `FirstOrDefaultAsync(x => x.Id == request.ConversationId, ct, include: q => q.Include(x => x.Messages), asNoTracking: true) ?? throw new NotFoundCoreException(ErrorCodes.AvatarConversationNotFound)`. (2) `student = userRepository.GetByIdAsync(conversation.StudentId, ct, asNoTracking: true)`. (3) Subject, unit and lesson each through `GetByIdAsync(id, asNoTracking: true)` only when the id has a value (sequential awaits). (4) `GenerateDetail(conversation, student?.DisplayName ?? string.Empty, subject?.Name, unit?.Name, lesson?.Name)`. |

No validator for #17: a route `{conversationId:guid}` cannot be empty-invalid, and an unknown id is a 404.

### api — Infrastructure and Api
| # | Path | Type | Contract |
|---|---|---|---|
| 19 | `api/Elmanhg.Infrastructure/Avatar/AvatarConversationRepository.cs` | repo | `public class AvatarConversationRepository(AppDbContext context) : Repository<AvatarConversation>(context), IAvatarConversationRepository { }` |
| 20 | `api/Elmanhg.Infrastructure/Migrations/<ts>_AddAvatarConversations.cs` (+ generated `.Designer.cs`) | migration | Generated tables `AvatarConversations` and `AvatarMessages` with FKs and indexes. At the end of `Up`, `migrationBuilder.Sql("""CREATE TRIGGER avatar_messages_append_only BEFORE UPDATE OR DELETE ON "AvatarMessages" FOR EACH ROW EXECUTE FUNCTION reject_append_only_mutation(); CREATE TRIGGER avatar_messages_no_truncate BEFORE TRUNCATE ON "AvatarMessages" FOR EACH STATEMENT EXECUTE FUNCTION reject_append_only_mutation();""")`. At the start of `Down`, drop both triggers (do **not** drop the function; `AddSessionsAndAttempts` owns it), then drop the tables. Review the generated file: no drop, rename or alter of existing tables. |
| 21 | `api/Elmanhg.Api/Controllers/Avatar/AvatarConversationsController.cs` | controller | `[ApiController] [Route("api/avatar/conversations")] [Authorize] public class AvatarConversationsController(IMediator mediator) : ControllerBase`. Actions in API surface. |

### api — Tests
| # | Path | Contract |
|---|---|---|
| 22 | `api/Elmanhg.Tests/Builders/AvatarConversationBuilder.cs` | `public sealed class AvatarConversationBuilder`: `ForStudent(Guid)`, `WithEntryPoint(AvatarEntryPoint)`, `WithLesson(Guid? subjectId, Guid? unitId, Guid? lessonId)`, `WithQuestion(Guid sessionId, Guid questionId)`, `StartedAt(DateTimeOffset)`, `WithExchange(string question, string reply)` (repeatable; each exchange is 1 minute after the previous one), `Build()` (goes through `Start` and `RecordExchange` with `Reply(reply)`); `public static AvatarAssistantReply Reply(string text = "رد", decimal cost = 0.0021m)` with model `claude-sonnet-5`, prompt `v2`, tokens 100/20, stop `end_turn`, history 0, `Context` = `AvatarMessageJson.WriteContext(<global bundle>, [])`, `Citations` = `"[]"`. |
| 23–33 | tests | See Test plan. |

### web — new feature `web/src/features/avatarConversations/`
| # | Path | Contract |
|---|---|---|
| 34 | `index.ts` | `export { AvatarConversationsPage } …; export { AvatarConversationPage } …; export { avatarConversationSearchSchema, type AvatarConversationSearch } …;` |
| 35 | `locales.ts` | `import ar …; import en …; export const avatarConversationsLocales = { ar, en };` |
| 36–37 | `i18n/ar.json`, `i18n/en.json` | keys and strings in the "Web copy" table |
| 38 | `schemas/avatarConversationSearchSchema.ts` | `// mirrors Avatar:ConversationSearchMaxLength` `export const avatarConversationSearchMaxLength = 200;` `export const avatarEntryPoints = ['Lesson','QuizQuestion','ExamReview','Global'] as const;` `avatarConversationSearchSchema = z.object({ page: z.coerce.number().int().min(1).optional().catch(undefined), search: z.string().trim().min(1).max(avatarConversationSearchMaxLength).optional().catch(undefined), entryPoint: z.enum(avatarEntryPoints).optional().catch(undefined), from: z.iso.date().optional().catch(undefined), to: z.iso.date().optional().catch(undefined) })`; `type AvatarConversationSearch` |
| 40 | `schemas/avatarConversationFiltersSchema.ts` | `z.object({ search: z.string().trim().max(200, { error: 'avatarConversations:filters.errors.searchTooLong' }), entryPoint: z.union([z.literal(''), z.enum(avatarEntryPoints)]), from: dateField, to: dateField }).refine(to === '' \|\| from === '' \|\| to >= from, { path: ['to'], error: 'avatarConversations:filters.errors.dateRange' })`; `dateField` as in audit with error `avatarConversations:filters.errors.date`; `type AvatarConversationFiltersValues` |
| 42 | `api/avatarConversationParams.ts` | `avatarConversationPageSize = 20`; `toAvatarConversationParams(search): GetAvatarConversationsParams` (as `toAuditLogParams`: `pageNumber: page ?? 1`, `pageSize`, optional `search`, `entryPoint`, `from` = local midnight, `to` = local midnight of the next day); `hasActiveFilters(search)` over `search, entryPoint, from, to`; `interface AvatarConversationPage { items; pageNumber; totalPages; totalItems }`; `toAvatarConversationPage(data: PageDataOfAdminAvatarConversationResult)` |
| 44 | `api/avatarConversationFormat.ts` | `formatCostUsd(value: number, lng: string): string` = `formatNumber(value, lng, 'latin', { style: 'currency', currency: 'USD', minimumFractionDigits: 2, maximumFractionDigits: 6 })`; `formatCount(value: number, lng: string)` = `formatNumber(value, lng, 'latin')` |
| 46 | `hooks/useAvatarConversationSearch.ts` | as `useAuditLogSearch`, with `getRouteApi('/admin/avatar-conversations')`; `applyFilters(values)` navigates `{ page: 1, …non-empty fields }`; `setPage`; `clearFilters` |
| 47 | `hooks/useAvatarConversations.ts` | `useGetAvatarConversations(toAvatarConversationParams(search), { query: { placeholderData: keepPreviousData, select: toAvatarConversationPage } })` |
| 48 | `hooks/useAvatarConversation.ts` | `useGetAvatarConversation(conversationId)` |
| 49 | `components/AvatarConversationFilters.tsx` | RHF + `zodResolver(avatarConversationFiltersSchema)`; grid `md:grid-cols-4`: `TextField search` (label `filters.search`, description `filters.searchHint`), `EntryPointField`, `TextField from type=date`, `TextField to type=date`; `SubmitButton filters.apply`, ghost `Button filters.clear` |
| 50 | `components/EntryPointField.tsx` | labelled native `select` bound with `useController` (the `PaymentLogSelectField` markup and classes): option `''` → `filters.allEntryPoints`, then the 4 entry points → `entryPoint.<value>` |
| 51 | `components/AvatarConversationTable.tsx` | the `AuditLogTable` structure, caption `table.caption`, headers `lastMessage, student, context, firstQuestion, messages, actions` |
| 52 | `components/AvatarConversationRow.tsx` | cells: date (`formatDate(…,'latin',{dateStyle:'medium',timeStyle:'short'})`), `studentName`, context (`entryPoint.<x>` then a second line `subjectName › lessonName` when present), `firstQuestion` (`line-clamp-2`), `formatCount(messageCount)`, and a `Link` to `/admin/avatar-conversation/$conversationId` with text `table.open` and `aria-label` `table.openLabel` `{ student }` |
| 53 | `components/AvatarConversationEmptyState.tsx` | as `AuditLogEmptyState` (icon `MessagesSquare`), `variant: 'no-data' \| 'no-results'` |
| 54 | `components/AvatarConversationSkeleton.tsx` | `role="status" aria-busy="true" aria-label={label}` prop `label: string`, 5 `h-11 rounded-md bg-soft` rows (as the audit skeleton) |
| 55 | `components/AvatarConversationSummary.tsx` | white card (`rounded-lg border border-border bg-surface p-4 shadow-1`), `dl` of: `detail.student`, `detail.context` (entry point label + `subject › unit › lesson`), `detail.startedAt`, `detail.lastMessageAt`, `detail.messages`, `detail.tokens {input, output}`, `detail.cost` (`formatCostUsd` or `detail.notPriced` when null) |
| 56 | `components/AvatarLoggedMessage.tsx` | `article`. Student: soft bubble (`bg-soft rounded-md p-3`), label `message.student` and time. Assistant: white bubble with hairline border and accent `Sparkles` icon (`aria-hidden`), label `message.assistant` and time, text `whitespace-pre-wrap`, then a caption `dl`: `message.model`, `message.promptVersion`, `message.tokens` («{in} / {out}»), `message.cost`, `message.stopReason`, `message.history`; citations under `message.sources` as non-link chips `section.<Section>` + « — title»; then `<AvatarLoggedContext>` when `context` is present. Technical values in `<bdi dir="ltr" className="font-mono text-mono">`. |
| 57 | `components/AvatarLoggedContext.tsx` | `<details>` with `<summary>` `context.summary`; inside, each present field labelled: subject, unit, lesson, objectives (`ul`), explanation, summary, question stem, student answer, correct answer, question explanation, subjects (Global); then `context.sources` as a list of `title` + `content` (`whitespace-pre-wrap`), or `context.none` when empty |
| 58 | `pages/AvatarConversationsPage.tsx` | the `AuditLogPage` structure: H1 `page.title`, intro `p` `page.intro`, filters (keyed by the search JSON), skeleton (`page.loading`) / error card (`error.title` + `common:errors.<code>` + retry) / empty (`no-data` vs `no-results` via `hasActiveFilters`) / table + `Pagination` when `totalPages > 1` |
| 60 | `pages/AvatarConversationPage.tsx` | props `{ conversationId: string }`. A back `Link` to `/admin/avatar-conversations` (text `detail.back`, chevron `rtl:rotate-180`), H1 `detail.title`; states: skeleton (`detail.loading`) / error card (`detail.error` + `common:errors.<code>` + retry) / `AvatarConversationSummary` then an `ol` of `AvatarLoggedMessage` in `position` order |
| 62 | `web/src/routes/admin/avatar-conversations.tsx` | `createFileRoute('/admin/avatar-conversations')({ validateSearch: avatarConversationSearchSchema, component: AvatarConversationsPage })` |
| 63 | `web/src/routes/admin/avatar-conversation.$conversationId.tsx` | `createFileRoute('/admin/avatar-conversation/$conversationId')`; `AvatarConversationRoute` reads `conversationId` from params and renders `<AvatarConversationPage conversationId={conversationId} />` |
| 64 | `web/src/test/avatarConversationFixtures.ts` | `conversationItem(overrides?)`: `AdminAvatarConversationResult` (`id 'c1'`, student `'Sara Ahmed'`, `Lesson`, `Physics` / `Ohm's law`, `firstQuestion 'What is resistance?'`, `messageCount 4`); `conversationPage(items, pageNumber=1, totalPages=1)`; `conversationDetail(overrides?)`: 2 messages (student `What is resistance?`; assistant `R = V / I` with model `claude-sonnet-5`, prompt `v2`, tokens 100/20, cost 0.0021, stop `end_turn`, history 0, one citation `Explanation`/`قانون أوم`, context with lesson `Ohm's law` and one source `{reference:'explanation-1', title:'الشرح — قانون أوم', content:'V = I R'}`), totals 100/20/0.0021 |

## Error codes
| Constant | Value | Thrown by | Exception type | HTTP |
|---|---|---|---|---|
| `AvatarConversationNotFound` | `AVATAR_CONVERSATION_NOT_FOUND` | `SendAvatarMessageHandler.LoadConversationAsync`, `GetAvatarConversationHandler` | `NotFoundCoreException` | 404 |
| `AvatarConversationContextMismatch` | `AVATAR_CONVERSATION_CONTEXT_MISMATCH` | `SendAvatarMessageHandler.LoadConversationAsync` | `BadRequestCoreException` | 400 |
| `AvatarConversationModifiedConcurrently` | `AVATAR_CONVERSATION_MODIFIED_CONCURRENTLY` | `AppDbContext.SaveChangesAsync` | `ConflictCoreException` | 409 |
| `AvatarConversationsPageNumberInvalid` | `AVATAR_CONVERSATIONS_PAGE_NUMBER_INVALID` | `GetAvatarConversationsValidator` | validation | 422 |
| `AvatarConversationsPageSizeInvalid` | `AVATAR_CONVERSATIONS_PAGE_SIZE_INVALID` | same | validation | 422 |
| `AvatarConversationsSearchTooLong` | `AVATAR_CONVERSATIONS_SEARCH_TOO_LONG` | same | validation | 422 |
| `AvatarConversationsEntryPointInvalid` | `AVATAR_CONVERSATIONS_ENTRY_POINT_INVALID` | same | validation | 422 |
| `AvatarConversationsDateRangeInvalid` | `AVATAR_CONVERSATIONS_DATE_RANGE_INVALID` | same | validation | 422 |
| ~~`AvatarHistoryTooLong`~~, ~~`AvatarHistoryInvalid`~~ | removed | — | — | — |

| Key | ar | en |
|---|---|---|
| AVATAR_CONVERSATION_NOT_FOUND | المحادثة غير موجودة. | The conversation was not found. |
| AVATAR_CONVERSATION_CONTEXT_MISMATCH | هذه المحادثة تخص سياقا اخر. ابدأ محادثة جديدة. | This conversation belongs to another context. Start a new conversation. |
| AVATAR_CONVERSATION_MODIFIED_CONCURRENTLY | ارسلت رسالة اخرى في هذه المحادثة في نفس الوقت. حاول مرة اخرى. | Another message was sent in this conversation at the same time. Try again. |
| AVATAR_CONVERSATIONS_PAGE_NUMBER_INVALID | رقم الصفحة يجب ان يكون 1 او اكثر. | The page number must be 1 or more. |
| AVATAR_CONVERSATIONS_PAGE_SIZE_INVALID | حجم الصفحة غير صالح. | The page size is not valid. |
| AVATAR_CONVERSATIONS_SEARCH_TOO_LONG | نص البحث طويل جدا. | The search text is too long. |
| AVATAR_CONVERSATIONS_ENTRY_POINT_INVALID | نقطة الدخول غير معروفة. | The entry point is not known. |
| AVATAR_CONVERSATIONS_DATE_RANGE_INVALID | يجب ان تكون البداية قبل النهاية. | The start must be before the end. |

Write the Arabic literally. No `\u` escapes (PROGRESS gotcha).

## Domain behaviour
```csharp
public static AvatarConversation Start(Guid studentId, AvatarEntryPoint entryPoint, Guid? subjectId, Guid? unitId, Guid? lessonId, Guid? sessionId, Guid? questionId, DateTimeOffset startedAt)
{
    var at = ToMicroseconds(startedAt);
    return new AvatarConversation(Guid.NewGuid(), studentId)
    {
        StudentId = studentId, EntryPoint = entryPoint, SubjectId = subjectId, UnitId = unitId, LessonId = lessonId,
        SessionId = sessionId, QuestionId = questionId, StartedAt = at, LastMessageAt = at,
    };
}

public bool IsFor(AvatarEntryPoint entryPoint, Guid? lessonId, Guid? sessionId, Guid? questionId) => EntryPoint == entryPoint && entryPoint switch
{
    AvatarEntryPoint.Lesson => LessonId == lessonId,
    AvatarEntryPoint.QuizQuestion or AvatarEntryPoint.ExamReview => SessionId == sessionId && QuestionId == questionId,
    _ => true,
};

public IReadOnlyList<AvatarMessage> RecentMessages(int count) => count <= 0 ? [] : Messages
    .OrderBy(x => x.Position)
    .TakeLast(count)
    .ToList();

public void RecordExchange(string question, AvatarAssistantReply reply, DateTimeOffset askedAt, DateTimeOffset repliedAt)
{
    var asked = ToMicroseconds(askedAt);
    var replied = ToMicroseconds(repliedAt);
    Messages.Add(AvatarMessage.FromStudent(Id, MessageCount, question.Trim(), asked));
    Messages.Add(AvatarMessage.FromAssistant(Id, MessageCount + 1, reply, replied));
    MessageCount += 2;
    LastMessageAt = replied;
    UpdatedBy = StudentId;
    UpdationDate = replied;
}
```
- Positions come from the persisted `MessageCount`, so they stay contiguous even if `Messages` were not fully loaded. `MaxHistoryMessages` is even (startup validation), so `TakeLast` always starts on a student turn.
- `AvatarMessage` has no mutating method. The DB trigger rejects UPDATE, DELETE and TRUNCATE.

**EF (`AppDbContext`)**
- Consts: `// Model ids, prompt versions and stop reasons are short identifiers set by the AI service; a schema invariant.` `private const int AiIdentifierMaxLength = 100;` and `public const string AvatarMessagePositionIndex = "IX_AvatarMessages_ConversationId_Position";`
- `DbSet<AvatarConversation> AvatarConversations`, `DbSet<AvatarMessage> AvatarMessages`.
- In `ConfigureAvatar`, `AvatarConversation`: `EntryPoint` `HasConversion<string>().HasMaxLength(EnumColumnMaxLength)`; `Version` `IsRowVersion()`; `HasOne<User>()` → `StudentId`; `HasOne<Subject>()` → `SubjectId`; `HasOne<CurriculumUnit>()` → `UnitId`; `HasOne<Lesson>()` → `LessonId`; `HasOne<Session>()` → `SessionId`; `HasOne<Question>()` → `QuestionId`, all `OnDelete(DeleteBehavior.Restrict)`; `HasMany(x => x.Messages).WithOne().HasForeignKey(x => x.ConversationId).OnDelete(Restrict)`; indexes `(StudentId, LastMessageAt)` and `(LastMessageAt)`.
- `AvatarMessage`: `Id` `ValueGeneratedNever()`; `Role` string conversion with `EnumColumnMaxLength`; `Text` `IsRequired()`; `Model`, `PromptVersion` and `StopReason` `HasMaxLength(AiIdentifierMaxLength)`; `CostUsd` `HasPrecision(12, 6)`; `Context` and `Citations` `HasColumnType("jsonb")`; unique `(ConversationId, Position)` named `AvatarMessagePositionIndex`; index `(CreatedAt)` for the date-ranged reads in #109 and #110.
- Global filters: `AvatarConversation` and `AvatarMessage` `HasQueryFilter(x => !x.IsDeleted)`.
- `SaveChangesAsync`: add `catch (DbUpdateConcurrencyException exception) when (exception.Entries.Any(x => x.Entity is AvatarConversation))` → `ConflictCoreException(ErrorCodes.AvatarConversationModifiedConcurrently, innerException: exception)` after the TeacherThread catch; add `catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: AvatarMessagePositionIndex })` → same code, as the last catch.

## API surface
| Method | Route | Policy | Request | Response |
|---|---|---|---|---|
| POST | `/api/avatar/messages` (existing, changed) | `DefaultCodes.AvatarChat` | `SendAvatarMessageCommand { entryPoint, lessonId?, sessionId?, questionId?, conversationId?, message }` | `AvatarReplyResult` (+ `conversationId`) |
| GET | `/api/avatar/conversations` `Name = "GetAvatarConversations"` | `DefaultCodes.AvatarConversationsView` | `[FromQuery] string? search, AvatarEntryPoint? entryPoint, DateTimeOffset? from, DateTimeOffset? to, int pageNumber = 1, int pageSize = 20` → `GetAvatarConversationsQuery` | `[ProducesResponseType<PageData<AdminAvatarConversationResult>>(200)]` |
| GET | `/api/avatar/conversations/{conversationId:guid}` `Name = "GetAvatarConversation"` | `DefaultCodes.AvatarConversationsView` | `[FromRoute] Guid conversationId` → `GetAvatarConversationQuery` | `[ProducesResponseType<AdminAvatarConversationDetailResult>(200)]` |

**AI service** `POST /v1/chat` response gains `"costUsd": <number>` (6-decimal USD, from `estimate_cost_usd`). The request is unchanged.

**Postman** (`postman/elmanhg.postman_collection.json`)
- Collection variables: `avatarConversationId`, `adminAvatarConversationId`.
- Avatar folder, in order:
  1. Get avatar status (unchanged).
  2. Send avatar message (lesson): body has no `history`, has `"conversationId": null`; test script `pm.collectionVariables.set("avatarConversationId", pm.response.json().conversationId)`.
  3. **new** Send avatar message (lesson, follow-up): same lesson, `"conversationId": "{{avatarConversationId}}"`, message «وما وحدة قياس المقاومة؟»; tests status 200 and `conversationId` equals the variable.
  4. Send avatar message (global): body without `history`.
- **New folder "AvatarConversations"** right after Avatar. Description: "Admin only (AvatarConversations.View). Sign in as an admin first so accessToken is set. search matches message text or the student's display name; from is inclusive and to exclusive, on the last message time, ISO 8601 with offset."
  1. List avatar conversations: `GET {{baseUrl}}/api/avatar/conversations?pageNumber=1&pageSize=20`, with disabled query params `search`, `entryPoint`, `from`, `to`; test 200, and `pm.collectionVariables.set("adminAvatarConversationId", …items[0].id)` when the list is non-empty.
  2. Get avatar conversation: `GET {{baseUrl}}/api/avatar/conversations/{{adminAvatarConversationId}}`; test 200 and a `messages` array.

## Web copy (`avatarConversations` namespace; ar / en)
| Key | ar | en |
|---|---|---|
| page.title | محادثات المساعد | Assistant conversations |
| page.intro | محادثات الطلاب مع المساعد الذكي، الأحدث أولًا. لكل رد النموذج ونسخة التعليمات والرموز والتكلفة والسياق المرسل. | Students' conversations with the AI assistant, most recent first. Each reply shows its model, prompt version, tokens, cost and the context sent. |
| page.loading | جارٍ تحميل المحادثات | Loading conversations |
| filters.search / searchHint | بحث / نص في المحادثة أو اسم الطالب | Search / Text in the conversation or the student's name |
| filters.entryPoint / allEntryPoints | نقطة الدخول / الكل | Entry point / All |
| filters.from / to / apply / clear | من / إلى / تطبيق / مسح الفلاتر | From / To / Apply / Clear filters |
| filters.errors.searchTooLong / date / dateRange | نص البحث طويل جدًا. / أدخل تاريخًا صحيحًا. / يجب أن يكون تاريخ النهاية في يوم البداية أو بعده. | The search text is too long. / Enter a valid date. / The end date must be on or after the start date. |
| entryPoint.Lesson / QuizQuestion / ExamReview / Global | الدرس / سؤال تدريب / مراجعة امتحان / عام | Lesson / Practice question / Exam review / General |
| table.caption / lastMessage / student / context / firstQuestion / messages / actions / open | محادثات المساعد / آخر رسالة / الطالب / السياق / أول سؤال / الرسائل / إجراءات / عرض | Assistant conversations / Last message / Student / Context / First question / Messages / Actions / View |
| table.openLabel | عرض محادثة {{student}} | View {{student}}'s conversation |
| empty.noData / noResults | لا توجد محادثات بعد. / لا توجد محادثات تطابق الفلاتر. | No conversations yet. / No conversations match these filters. |
| error.title | تعذّر تحميل المحادثات | Could not load conversations |
| detail.back / title / loading / error | محادثات المساعد / محادثة المساعد / جارٍ تحميل المحادثة / تعذّر تحميل المحادثة | Assistant conversations / Assistant conversation / Loading the conversation / Could not load the conversation |
| detail.student / context / startedAt / lastMessageAt / messages | الطالب / السياق / بدأت / آخر رسالة / الرسائل | Student / Context / Started / Last message / Messages |
| detail.tokens | الرموز: دخل {{input}} · خرج {{output}} | Tokens: in {{input}} · out {{output}} |
| detail.cost / notPriced | التكلفة / غير محسوبة | Cost / Not priced |
| message.student / assistant | الطالب / المساعد | Student / Assistant |
| message.model / promptVersion / tokens / cost / stopReason / history / sources | النموذج / نسخة التعليمات / الرموز / التكلفة / سبب التوقف / رسائل سابقة مرسلة / المصادر | Model / Prompt version / Tokens / Cost / Stop reason / Earlier messages sent / Sources |
| section.Explanation / Objectives / Summary / QuestionExplanation | الشرح / الأهداف / الملخص / شرح سؤال | Explanation / Objectives / Summary / Question explanation |
| context.summary / subject / unit / lesson / objectives / explanation / summaryText | السياق المرسل / المادة / الوحدة / الدرس / الأهداف / الشرح / الملخص | Context sent / Subject / Unit / Lesson / Objectives / Explanation / Summary |
| context.question / studentAnswer / correctAnswer / questionExplanation / subjects / sources / none | السؤال / إجابة الطالب / الإجابة الصحيحة / شرح السؤال / المواد / نتائج البحث المرسلة / لا شيء | Question / Student's answer / Correct answer / Question explanation / Subjects / Search results sent / None |

## Docs
| File | Change |
|---|---|
| `docs/avatar.md` | Role: replace "Conversations live in the browser's memory; persisting them is #92." with "The API stores every conversation (see Conversation log)." History bullet: the browser sends `conversationId` (null for the first message, returned in each reply, reset when the context changes). The API sends the last `Avatar:MaxHistoryMessages` stored messages, each cut to `Avatar:HistoryTurnMaxLength`. A foreign or unknown id is 404, another context is 400. Storage bullet in Daily quota: "#92 owns conversations" becomes a link to Conversation log. HTTP: request example without `history` and with `conversationId`; response adds `conversationId`; "returned for #92" becomes "stored per reply". Error table: remove the two history codes and add `AVATAR_CONVERSATION_NOT_FOUND` 404, `AVATAR_CONVERSATION_CONTEXT_MISMATCH` 400, `AVATAR_CONVERSATION_MODIFIED_CONCURRENTLY` 409. Configuration: new keys, and the new meaning of `HistoryTurnMaxLength`. **New section "Conversation log"**: when rows are written (successful replies only, with the usage row in one transaction); the `AvatarConversations` and `AvatarMessages` columns; the context JSON shape `{bundle, sources}` (camelCase); citations JSON; cost from the AI service; append-only trigger; indexes (`CreatedAt` for #109/#110); privacy (D9); retention (D10); consumers #109/#110. **New section "Admin view"**: routes, policy `AvatarConversations.View`, search semantics (D12), the two HTTP rows, the 5 validation codes. UI: the panel keeps `conversationId`; the admin pages. "Not in this story": replace the #92 line with "A student history view; retention purge (#109 review)". |
| `docs/ai-service.md` | Chat response example gains `"costUsd": 0.0`, plus a sentence: "`costUsd` is `estimate_cost_usd` of the reply's tokens at `ELMANHG_AI_MODEL_*_USD_PER_MILLION_TOKENS`; the API stores it per reply." In the settings table, the two model price rows change "cost logging only" to "cost logging and the `costUsd` returned per reply". |
| `docs/PRD.md` | §9.3: "…recorded on every reply…". §10: new "### 10.5 Assistant conversations" with one paragraph (recent list with search by message text or student name, entry-point and date filters; detail with every message and, per reply, model, prompt version, tokens, cost, citations and the context sent; `docs/avatar.md`). §15: replace the two Avatar lines with `AvatarConversation(id, student_id, entry_point, subject_id?, unit_id?, lesson_id?, session_id?, question_id?, started_at, last_message_at, message_count)  -- docs/avatar.md` and `AvatarMessage(id, conversation_id, position, role[Student\|Assistant], text, created_at, model?, prompt_version?, input_tokens?, output_tokens?, cost_usd?, stop_reason?, history_message_count?, context_json?, citations_json?)  -- append-only; replies carry the context bundle, model and prompt version`. §16: row `\| View Avatar conversations \| – \| – \| ✓ \|` after "View audit log". |
| `docs/claude-design-prompt.md` §4 | After the `#/admin/payments` bullet, add a bullet for `#/admin/avatar-conversations` (nav «محادثات المساعد»; not simulated by the prototype): H1, intro, filter form (بحث with hint, نقطة الدخول, من, إلى, تطبيق, مسح الفلاتر; kept in the URL), table (آخر رسالة, الطالب, السياق, أول سؤال, الرسائل, عرض), states (skeleton, error with retry, «لا توجد محادثات بعد.», «لا توجد محادثات تطابق الفلاتر.» with «مسح الفلاتر»), pagination. Then `#/admin/avatar-conversation/:id`: back link, «محادثة المساعد», the summary card (الطالب, السياق, بدأت, آخر رسالة, الرسائل, tokens, التكلفة or «غير محسوبة»), and messages (student soft bubbles; assistant white bubbles with sparkle, a meta line with النموذج, نسخة التعليمات, الرموز, التكلفة, سبب التوقف, رسائل سابقة مرسلة, «المصادر» chips, and a collapsible «السياق المرسل» with the context fields and «نتائج البحث المرسلة»); skeleton, and error with retry. |
| `docs/prototype.md` | After line 45: "- The product also has an assistant conversations page (list with search, and each conversation with the model, prompt version, tokens, cost and context of every reply); the prototype does not simulate it." |
| `docs/deployment.md` | AI Avatar table: rows `Avatar__AdminConversationsMaxPageSize` `100` (1 to 200) and `Avatar__ConversationSearchMaxLength` `200` (1 to 500); the `HistoryTurnMaxLength` note becomes "1 to 4000; each stored turn is cut to this when sent back as history". AI table price rows: "cost logging and the `costUsd` stored per reply". |

## Test plan
### api (.NET, xUnit v3 + FluentAssertions + NSubstitute, as the repo)
| # | Test class | Test method | Asserts |
|---|---|---|---|
| 1 | `Domain/Avatar/AvatarConversationTests` | `Start_ValidInput_SetsScopeAndEmptyLog` | every scope id, `EntryPoint`, `StudentId`, `CreatedBy == studentId`, `StartedAt == LastMessageAt`, `MessageCount 0`, `Messages` empty |
| 2 | 〃 | `Start_SubMicrosecondTimestamp_TruncatesToMicroseconds` | `StartedAt.Ticks % 10 == 0` |
| 3 | 〃 | `RecordExchange_First_AppendsStudentThenAssistantAtPositions0And1` | roles, positions, trimmed question text, student `CreatedAt == askedAt`, assistant `CreatedAt == repliedAt`, student metadata all null, assistant `Model/PromptVersion/InputTokens/OutputTokens/CostUsd/StopReason/HistoryMessageCount/Context/Citations` equal the reply |
| 4 | 〃 | `RecordExchange_Second_ContinuesPositionsAndStampsConversation` | positions 2,3; `MessageCount 4`; `LastMessageAt == UpdationDate == repliedAt`; `UpdatedBy == StudentId` |
| 5 | 〃 | `RecentMessages_MoreThanCount_ReturnsLastInPositionOrder` | 6 messages, count 4 → positions 2,3,4,5 |
| 6 | 〃 | `RecentMessages_ZeroCount_ReturnsEmpty` | empty |
| 7 | 〃 | `IsFor_ScopeCases_MatchesOnlyTheSameContext` (Theory) | rows: Lesson same lesson → true; Lesson other lesson → false; QuizQuestion same session and question → true; QuizQuestion other question → false; ExamReview with any lessonId but same session and question → true; Global → true; different entry point → false |
| 8 | `Application/Features/Avatar/SendAvatarMessage/SendAvatarMessageHandlerConversationTests` | `Handle_NoConversationId_StartsConversationWithExchangeAndReturnsItsId` | `Conversations.Received(1).AddAsync`; `AddedConversation.Messages` has 2; `result.ConversationId == AddedConversation.Id`; `Usage.Received(1).SaveChangesAsync` |
| 9 | 〃 | `Handle_NewLessonConversation_RecordsSubjectUnitLessonIds` | `SubjectId`, `UnitId`, `LessonId` from the harness lesson; `SessionId`/`QuestionId` null |
| 10 | 〃 | `Handle_NewQuizQuestionConversation_RecordsSessionAndQuestionIds` | `SessionId == session.Id`, `QuestionId == questionId`, `LessonId == lesson.Id`, `EntryPoint QuizQuestion` |
| 11 | 〃 | `Handle_Reply_StoresModelPromptTokensCostAndStopReason` | assistant row `claude-sonnet-5`, `v2`, 10, 5, `0.0021m`, `end_turn`, `HistoryMessageCount 0` |
| 12 | 〃 | `Handle_Reply_StoresContextBundleAndSourcesJson` | `AvatarMessageJson.ReadContext(assistant.Context!)`: `Bundle.Lesson!.Id == lesson.Id`, `Sources` equal the sent sources (stub a match) |
| 13 | 〃 | `Handle_Reply_StoresMappedCitationsJson` | `ReadCitations(assistant.Citations!)` equals `result.Citations` |
| 14 | 〃 | `Handle_ExistingConversation_SendsRecentStoredMessagesAsHistory` | conversation with 3 exchanges; `AvatarOptions.MaxHistoryMessages = 4`; `LastChat.History` equals the 4 last texts with roles User, Assistant, User, Assistant; `HistoryMessageCount 4` on the new reply |
| 15 | 〃 | `Handle_ExistingConversation_AppendsAtNextPositionsWithoutAdding` | new messages at positions 6,7; `Conversations.DidNotReceive().AddAsync`; `result.ConversationId == conversation.Id` |
| 16 | 〃 | `Handle_StoredTurnOverHistoryTurnMax_IsTruncated` | `HistoryTurnMaxLength = 5` → history contents are 5 characters |
| 17 | 〃 | `Handle_UnknownOrForeignConversation_ThrowsAvatarConversationNotFound` | the stub has a conversation of another student → `NotFoundCoreException` with the code; `Ai.DidNotReceive().ChatAsync`; `Usage.DidNotReceive().SaveChangesAsync` |
| 18 | 〃 | `Handle_ConversationOfAnotherContext_ThrowsAvatarConversationContextMismatch` | Global conversation plus Lesson command → `BadRequestCoreException` with the code; no AI call; no save |
| 19 | 〃 | `Handle_AiServiceUnavailable_StartsNoConversation` | `Conversations.DidNotReceive().AddAsync`; no save |
| 20 | `SendAvatarMessageHandlerTests` (modified) | `Handle_Success_RecordsUsageAndReturnsCountsAndCitations` | existing assertions + `ConversationId` |
| 21 | `SendAvatarMessageValidatorTests` (modified) | `Validate_LessonEntryWithLessonId_Passes` | passes with a `ConversationId` set |
| 22 | `Application/Features/Avatar/Shared/AvatarMessageJsonTests` | `WriteContext_ThenReadContext_RoundTripsBundleAndSources` | equivalent `Bundle` (`BeEquivalentTo`) and `Sources` |
| 23 | 〃 | `WriteContext_EntryPoint_IsCamelCase` | JSON contains `"entryPoint":"quizQuestion"` and `"sources"` |
| 24 | 〃 | `WriteCitations_ThenReadCitations_RoundTrips` | equal list |
| 25 | `Application/Features/Avatar/GetAvatarConversations/GetAvatarConversationsValidatorTests` | `Validate_Defaults_Passes` | valid |
| 26 | 〃 | `Validate_PageNumberZero_FailsWithPageNumberInvalid` | code |
| 27 | 〃 | `Validate_PageSizeOverMax_FailsWithPageSizeInvalid` | 101 → code |
| 28 | 〃 | `Validate_PageSizeZero_FailsWithPageSizeInvalid` | code |
| 29 | 〃 | `Validate_SearchOverMax_FailsWithSearchTooLong` | 201 characters → code |
| 30 | 〃 | `Validate_UndefinedEntryPoint_FailsWithEntryPointInvalid` | `(AvatarEntryPoint)99` → code |
| 31 | 〃 | `Validate_FromNotBeforeTo_FailsWithDateRangeInvalid` | from == to → code |
| 32 | `…/GetAvatarConversations/GetAvatarConversationsFilterTests` | `Build_NoFilters_MatchesAll` | compiled predicate true for 2 conversations |
| 33 | 〃 | `Build_EntryPoint_MatchesOnlyThatEntryPoint` | |
| 34 | 〃 | `Build_DateRange_FromInclusiveToExclusiveOnLastMessageAt` | boundaries |
| 35 | 〃 | `Build_Search_MatchesMessageTextCaseInsensitively` | "OHM" matches "ohm's law" in the assistant text; another conversation does not |
| 36 | 〃 | `Build_Search_MatchesListedStudentIds` | a conversation of a listed student with no text match → true |
| 37 | 〃 | `Term_BlankSearch_ReturnsNull` | `"  "` → null; `" Ohm "` → `"ohm"` |
| 38 | `…/GetAvatarConversations/GetAvatarConversationsHandlerTests` | `Handle_Page_MapsStudentSubjectLessonNamesAndFirstQuestion` | substitute repos (stub `FindPaginatedAsync` returns a page of one built conversation; `FindAsync` for users, lessons, subjects) → item fields including `FirstQuestion` and paging numbers |
| 39 | 〃 | `Handle_MissingLookups_ReturnsEmptyStudentNameAndNullNames` | `StudentName ""`, `SubjectName`/`LessonName` null |
| 40 | 〃 | `Handle_SearchMatchesStudentName_ReturnsThatStudentsConversation` | `FindPaginatedAsync` stub compiles the passed `filter` over an in-memory list of 2 conversations; user `FindAsync` stub compiles its predicate over 2 users ("Sara Ahmed", "Omar"); `Search = "sara"` → only Sara's conversation returned (no message text matches) |
| 41 | `…/GetAvatarConversation/GetAvatarConversationHandlerTests` | `Handle_Unknown_ThrowsAvatarConversationNotFound` | `NotFoundCoreException` with the code |
| 42 | 〃 | `Handle_Found_ReturnsMessagesInOrderWithReplyMetadataContextAndCitations` | 4 messages in positions; assistant `Model`, `CostUsd`, `Context.Bundle`, `Citations`; student `Context` null, `Citations` empty; names from repositories |
| 43 | 〃 | `Handle_Found_SumsTokensAndCost` | totals over 2 replies |
| 44 | `Integration/Avatar/AvatarConversationLogEndpointTests` (`[Collection(ContentRetrievalCollection.Name)]`) | `PostMessage_FirstLessonMessage_ReturnsConversationIdAndPersistsExchange` | 200; DB (fresh scope) conversation `StudentId`, `EntryPoint Lesson`, `LessonId`, `SubjectId`, `UnitId`, 2 messages; assistant `Model "fake"`, `PromptVersion "fake"`, `CostUsd 0m`, `ReadContext(...).Bundle.Lesson.Id == lessonId` |
| 45 | 〃 | `PostMessage_WithConversationId_AppendsToSameConversation` | Global twice; same `conversationId`; 4 messages at positions 0–3; `MessageCount 4` |
| 46 | 〃 | `PostMessage_OtherStudentsConversation_Returns404AndRecordsNothing` | problem `code` `AVATAR_CONVERSATION_NOT_FOUND`; student B usage count 0 |
| 47 | 〃 | `PostMessage_ConversationOfAnotherContext_Returns400ContextMismatch` | Global conversation, then a Lesson post (random lessonId) → 400 with the code |
| 48 | `Integration/Avatar/AvatarConversationsEndpointTests` | `GetConversations_Admin_ReturnsMatchesNewestFirstWithNames` | two seeded conversations share a unique token in their text; `search=<token>` returns 2, the newest `LastMessageAt` first, with `studentName` and `firstQuestion` |
| 49 | 〃 | `GetConversations_SearchStudentName_ReturnsTheirConversations` | a student with a unique display name; search by that name returns their conversation only |
| 50 | 〃 | `GetConversations_EntryPointFilter_ExcludesOtherEntryPoints` | token plus `entryPoint=Global` returns only the Global one |
| 51 | 〃 | `GetConversations_PageSizeOverMax_Returns422` | code `AVATAR_CONVERSATIONS_PAGE_SIZE_INVALID` |
| 52 | 〃 | `GetConversations_Student_Returns403` | 403 |
| 53 | 〃 | `GetConversations_Anonymous_Returns401` | 401 |
| 54 | 〃 | `GetConversation_Admin_ReturnsMessagesWithReplyMetadata` | `messages[1].model`, `costUsd`, `context.bundle.entryPoint`, `messages[0].role "Student"` |
| 55 | 〃 | `GetConversation_Unknown_Returns404` | code `AVATAR_CONVERSATION_NOT_FOUND` |
| 56 | 〃 | `GetConversation_Teacher_Returns403` | 403 |
| 57 | `Integration/Persistence/AvatarMessageAppendOnlyTests` | `Update_AvatarMessageRow_RejectedByDatabase` | `PostgresException` `P0001`; text unchanged when read back |
| 58 | 〃 | `Delete_AvatarMessageRow_RejectedByDatabase` | `P0001`; the row still exists |
| 59 | `Integration/Persistence/AvatarConversationPersistenceTests` | `SaveChanges_ConcurrentExchanges_ThrowsAvatarConversationModifiedConcurrently` | two scopes load it (with messages), both `RecordExchange`; the first save succeeds; the second throws `ConflictCoreException` with the code; the DB has 4 messages |
| 60 | `Integration/Persistence/AppDbContextTests` (modified) | `Migrate_FreshDatabase_LeavesNoPendingMigrations` | + `_AddAvatarConversations` |
| 61 | `Integration/Authorization/PermissionMatrixPolicyTests` (modified) | matrix | 3 rows |
| 62 | `Infrastructure/AiService/HttpAiServiceClientTests` (modified) | `ChatAsync_Success_ReturnsReply` | `CostUsd 0.000105m` |

Deleted (intentional behaviour change): `SendAvatarMessageHandlerTests.Handle_HistoryTurns_MapsRolesToAiChatMessages`; the 5 history validator tests listed above.

### ai (pytest)
| # | File | Test | Asserts |
|---|---|---|---|
| 63 | `tests/unit/test_chat_pipeline.py` | `test_chat_run_returns_cost_usd_from_token_usage` | a stub `ModelClient` returns `ModelReply(input_tokens=1000, output_tokens=200, …)`; settings copy with prices 3/15 → `result.cost_usd == Decimal("0.006000")` |
| 64 | 〃 (modified) | `test_chat_run_returns_reply_with_model_and_prompt_version` | expected `ChatResult(..., cost_usd=Decimal("0.000000"))` |
| 65 | `tests/integration/test_chat_endpoint.py` | `test_chat_valid_request_returns_cost_usd` | 200; `body["costUsd"] == 0.0` (fake model, 0 tokens); key present |

The OpenAPI drift test (`test_openapi_document.py`) covers the regenerated `ai/openapi/v1.json`. The prompt is unchanged, so the eval dataset and threshold are unchanged (no `-m eval` re-run needed).

### web (Vitest + Testing Library + MSW)
| # | File | Test | Asserts |
|---|---|---|---|
| 66 | `features/avatar/api/avatarContext.test.ts` (modified) | `sends missing ids as null and trims the message` | `{ entryPoint:'Lesson', lessonId:'l1', sessionId:null, questionId:null, conversationId:null, message:'why?' }` |
| 67 | 〃 (new) | `sends the conversation id when continuing` | `conversationId: 'c1'` |
| — | 〃 (**delete**) | `keeps only the last completed turns` | — |
| 68 | `features/avatar/hooks/avatarReducer.test.ts` (modified) | `starts a new conversation when opened with another context` | `messages []`, `conversationId null` |
| 69 | 〃 (modified) | `keeps the conversation when reopened with the same context` | 2 messages, `conversationId === avatarConversationId` |
| 70 | 〃 (modified, renamed) | `keeps the conversation id when the assistant replies` | messages m0/m1 as before; `conversationId === avatarConversationId` |
| 71 | 〃 (modified) | `adds a notice without changing the conversation when sending fails` | notice m3; `conversationId` unchanged |
| 72 | `features/avatar/components/AvatarPanel.test.tsx` (modified) | `sends a question and shows the reply with its sources` | body `{ entryPoint:'Global', lessonId:null, sessionId:null, questionId:null, conversationId:null, message }` |
| 73 | 〃 (replaces `sends the previous turns as history on the next question`) | `continues the same conversation on the next question` | `bodies[1].conversationId === avatarConversationId`; no `history` key |
| 74 | `features/session/permissions.test.ts` | `grants an admin the assistant conversations view and denies other roles` | admin true, teacher and student false |
| 75 | `features/shell/pages/MorePage.test.tsx` (modified) | `lists the admin destinations that are not in the tab bar` | the 6-item list |
| 76 | `features/avatarConversations/schemas/avatarConversationSearchSchema.test.ts` | `parses valid search params` / `drops an invalid page, entry point, date or overlong search` | parsed object / `undefined` fields |
| 77 | `…/schemas/avatarConversationFiltersSchema.test.ts` | `accepts blank filters`; `rejects a search over the limit`; `rejects an invalid date`; `rejects an end date before the start`; `accepts the same start and end day` | success, or the issue message is the error key |
| 78 | `…/api/avatarConversationParams.test.ts` | `maps the search to request params with local-midnight dates`; `omits empty filters and defaults the page`; `reports active filters`; `defaults missing page fields` | exact objects and booleans |
| 79 | `…/api/avatarConversationFormat.test.ts` | `formats cost in US dollars with up to six decimals` (en `$0.0021`, `$0.00` for 0); `uses latin digits in Arabic` (contains `0.0021`) | strings |
| 80 | `…/pages/AvatarConversationsPage.test.tsx` | `shows conversations after loading` | status "Loading conversations", then a row with `Sara Ahmed`, `Lesson`, `Physics › Ohm's law`, `What is resistance?` |
| 81 | 〃 | `shows the empty state when there are no conversations` | "No conversations yet."; only one "Clear filters" button |
| 82 | 〃 | `offers clear filters when filters match nothing` | `?search=zzz` → "No conversations match these filters."; the empty-state clear shows rows and removes `search` from the URL |
| 83 | 〃 | `sends the search, entry point and dates from the filters` | type a search, select "Exam review", set From/To, Apply → the captured request has `search`, `entryPoint=ExamReview`, `from`, `to`; the URL has `search` |
| 84 | 〃 | `shows an inline error when the search is too long` | 201 characters → "The search text is too long." |
| 85 | 〃 | `shows the error with retry and recovers` | 500 → alert "Could not load conversations"; Retry → row |
| 86 | 〃 | `opens a conversation from its row` | click the "View Sara Ahmed's conversation" link → detail heading "Assistant conversation" |
| 87 | 〃 | `renders right to left in Arabic` | `document.documentElement.dir === 'rtl'`; heading «محادثات المساعد» |
| 88 | 〃 | `has no axe violations` | `axe` clean |
| 89 | `…/pages/AvatarConversationPage.test.tsx` | `shows each reply with its model, prompt version, tokens and cost` | `claude-sonnet-5`, `v2`, `100 / 20`, `$0.0021`, `end_turn` |
| 90 | 〃 | `shows the conversation summary with totals` | "Tokens: in 100 · out 20", student name, "Lesson" |
| 91 | 〃 | `shows the citations of a reply` | chip "Explanation — قانون أوم" (not a link) |
| 92 | 〃 | `reveals the context and search results sent with a reply` | click "Context sent" → "Ohm's law" and source content `V = I R` visible |
| 93 | 〃 | `shows not found with retry when the conversation does not exist` | 404 `AVATAR_CONVERSATION_NOT_FOUND` → "The conversation was not found." and Retry |
| 94 | 〃 | `renders right to left in Arabic` | rtl + «محادثة المساعد» |
| 95 | 〃 | `has no axe violations` | clean |

## Definition of done
- [ ] `AvatarConversation` / `AvatarMessage` / `AvatarMessageRole` / `AvatarAssistantReply` / `IAvatarConversationRepository` exist exactly as specified; `RecordExchange` sets `UpdationDate` and `UpdatedBy`; timestamps are truncated to microseconds.
- [ ] Migration `AddAvatarConversations` creates both tables, the FKs, the indexes (`IX_AvatarMessages_ConversationId_Position` unique, `CreatedAt`, `LastMessageAt`, `(StudentId, LastMessageAt)`) and the two triggers on `AvatarMessages` using the existing `reject_append_only_mutation()`; `Down` does not drop the function.
- [ ] UPDATE, DELETE and TRUNCATE on `AvatarMessages` fail in the DB (tests 57–58).
- [ ] `SendAvatarMessageCommand` has `ConversationId` and no `History`; the model's history comes only from stored messages (tests 14–16); `AvatarTurn`, `AvatarTurnRole` and the two history error codes and resx keys are gone.
- [ ] Only successful replies write conversation rows, in the same save as the usage row (tests 17–19, 46).
- [ ] Each assistant row stores model, prompt version, tokens, cost, stop reason, history count, context `{bundle, sources}` and citations (tests 11–13, 44).
- [ ] 404 for a foreign or unknown conversation, 400 for a context mismatch, 409 for concurrent appends (tests 17, 18, 46, 47, 59).
- [ ] `AiChatReply.CostUsd` is mapped from the AI service's `costUsd`; the fake returns 0; `ChatOut.cost_usd` and `ai/openapi/v1.json` are updated; ai tests 63–65 pass; ruff, mypy and pytest are clean as in `ai-ci.yml`.
- [ ] `GET /api/avatar/conversations` and `/{id}` exist behind `AvatarConversations.View` (Admin only), return `PageData<AdminAvatarConversationResult>` / `AdminAvatarConversationDetailResult`, and show the student display name only (no phone or email).
- [ ] Every new `ErrorCodes` constant has ar and en resx entries; the removed ones are gone from both.
- [ ] `AvatarOptions` has the 2 new keys (validated at startup), and they are in `appsettings.example.json`, `ApiFactory`, `deploy/api.env.example` and `docs/deployment.md`.
- [ ] `api/openapi/v1.json`, `web/src/shared/api/generated/**` and `web/src/routeTree.gen.ts` are regenerated and committed (no drift).
- [ ] Postman: the lesson send stores `avatarConversationId`, a follow-up request uses it, `history` is removed from bodies, and a new AvatarConversations folder has 2 requests in order.
- [ ] The web avatar panel sends `conversationId` (null first, then the reply's id; reset on a context change) and never sends `history`.
- [ ] The admin list page has loading, error with retry, no-data, no-results with clear, pagination and filters in the URL; the detail page has loading, error with retry and the full message metadata. All strings are in ar and en; only logical properties and tokens are used; the axe tests pass.
- [ ] The nav item «محادثات المساعد» is visible to admins only (capability `avatarConversationsView`).
- [ ] Docs updated per the Docs table: avatar.md (Conversation log, Admin view, privacy, retention), ai-service.md, PRD §9.3/§10.5/§15/§16, design prompt §4, prototype.md, deployment.md.
- [ ] Every test in the Test plan exists with that name; deleted tests are exactly the ones listed; no other test is edited.
- [ ] `dotnet build` has no new warnings; `dotnet test api/ -c Release` is green with `appsettings.json` moved aside; `dotnet format --verify-no-changes` is clean (outside core-libraries).
- [ ] `npm --prefix web run typecheck`, `lint`, `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` and `npx vitest run --coverage` are all green.
- [ ] The guard grep (`DateTime.Now|UtcNow`, `.Result`, `.Wait()`, `new HttpClient(`, `FromSqlRaw`, `async void`) is empty on the diff.
