VERDICT: APPROVED

# Review — [E8.S4] Conversation logging (#92)

## Blocking
None.

## Non-blocking
1. `api/Elmanhg.Application/Avatar/SendAvatarMessage/SendAvatarMessageHandler.Conversation.cs:19`: every send loads the whole conversation with every message, including the jsonb `Context` (up to about 60k characters per reply), only to take the last `MaxHistoryMessages` and read `MessageCount`. This follows the plan, but the cost grows with the length of the conversation. A later story could load only the recent messages (a filtered include ordered by `Position` descending, taking `MaxHistoryMessages`) and project `Text` and `Role`.
2. `web/src/features/avatar/hooks/avatarReducer.ts:64` (the failed action): a 404 `AVATAR_CONVERSATION_NOT_FOUND` or a 400 `AVATAR_CONVERSATION_CONTEXT_MISMATCH` keeps `conversationId`, so every later send in that context would fail the same way. The normal flow cannot reach this state, because the web context key is stricter than `IsFor`. A reset on those two codes would still make the panel self-healing.
3. `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs:498-500`: `Model`, `PromptVersion` and `StopReason` are capped at 100 characters, but the reply is not checked before the save. An over-long identifier from the AI service would fail the save with a 500 after the model call. Nothing is counted, because the save rolls back. The values in use today are short.
4. `api/Elmanhg.Tests/Integration/Persistence/AvatarMessageAppendOnlyTests.cs`: UPDATE and DELETE are tested; TRUNCATE is not. The trigger exists (`20260929163757_AddAvatarConversations.cs`, the last `Sql` block), and the test table in the plan did not list a TRUNCATE test, although the DoD line mentions it.
5. `web/src/features/avatarConversations/components/AvatarLoggedContext.tsx:21`: the separator for the subject list is a hard-coded Arabic comma, also used in the English UI. A locale key would be cleaner.
6. `web/src/features/avatarConversations/components/AvatarConversationSummary.tsx:49`: the tokens line sits under the `dl` instead of being a `dl` row as in plan #55. It is disclosed in "Notes for review", but not in the Deviations table.
7. `SendAvatarMessageHandlerConversationTests.Handle_UnknownOrForeignConversation_ThrowsAvatarConversationNotFound` covers only the foreign case. An unknown id takes the same path (the stub returns null), so the behaviour is covered, but the name promises both.

## Verified
- **Conversation ownership (by reading):** `SendAvatarMessageHandler.Conversation.cs:19` loads with `x.Id == conversationId && x.StudentId == studentId`, where `studentId` is the current user id (`SendAvatarMessageHandler.cs:30,35`). A miss returns 404. Test 17 would fail without the `StudentId` term: the foreign conversation is a Lesson conversation on the same lesson, so it would pass `IsFor`. Integration test 46 checks the 404 and that student B has no usage. Students have no read endpoint. The admin reads sit behind `AvatarConversations.View` (Admin only) at `AvatarConversationsController.cs:19,28` and `PermissionMatrixPolicies.cs:30`. The matrix test has 3 rows, and endpoint tests 52, 53 and 56 check 403 / 401.
- **Server-owned history:** `SendAvatarMessageCommand.cs:7` has no `History`. `HistoryOf` builds the history only from stored rows, truncated with `AvatarText.Truncate`. `AvatarTurn`/`AvatarTurnRole`, the two error codes and their resx keys are deleted. A `history` from an old client is ignored: `AvatarMessageEndpointTests` still posts one and passes. The web never sends `history` (test 73 asserts that there is no `history` key).
- **Append-only:** the migration creates a row-level `BEFORE UPDATE OR DELETE` trigger and a statement-level `BEFORE TRUNCATE` trigger on `AvatarMessages`, both with the existing `reject_append_only_mutation()`. `Down` drops only the triggers, then the tables. Tests 57 and 58 check `P0001` and that the row is unchanged. `AvatarMessage` has no mutating method.
- **Transactions:** the conversation, messages and usage row are added and then saved by one `avatarMessageUsageRepository.SaveChangesAsync` (`SendAvatarMessageHandler.cs:45-54`), after the AI reply. Not-found, context mismatch and AI failure call no save (tests 17, 18 and 19 use `DidNotReceive`). The xmin row version and the unique `(ConversationId, Position)` index both map to 409 (`AppDbContext.cs:106-109,143-146`), and test 59 checks that path against real Postgres.
- **Admin access and PII:** the results carry `StudentName` = display name only (`GetAvatarConversationsHandler.cs:44`, `GetAvatarConversationHandler.cs:24`). No phone or email is returned. The search runs through LINQ `ToLower().Contains(term)` expressions, so EF sends it as a parameter; there is no raw SQL.
- **#91 guards:** the exam gate and the quota check still run first (`SendAvatarMessageHandler.cs:32-34`). `LoadContextAsync` and the source construction are unchanged. The stored sources are the filtered list actually sent (test 12 compares with `LastChat.Sources`). `SendAvatarMessageHandlerWithheldSourcesTests` and the ContextTests pass unchanged apart from the constructor arguments.
- **Cost:** `chat.py` computes `estimate_cost_usd` once and uses it for both the log and `ChatResult.cost_usd`. `ChatOut.cost_usd` is returned as `costUsd`. `AiChatReply.CostUsd` is deserialised as a `decimal` (`HttpAiServiceClientTests` checks 0.000105m) and stored as `numeric(12,6)`. The fake returns 0m. ai test 63 prices 1000/200 tokens at 3/15 USD, giving 0.006000.
- **Readiness for #109/#110:** an index on `AvatarMessages.CreatedAt`. Subject, unit, lesson, session and question ids are on the conversation. Assistant rows carry the model, prompt version, tokens, cost, stop reason, history count, the context (bundle and sources) and the citations JSON.
- **Contract fidelity:** every file in *Files to create* exists with the planned signatures. The listed deviations are real and justified: the using was kept, two lines changed in ContextTests, the ai key set and eval helper were updated, an Orval query-zod opt-out was added, ICU placeholders are used, and the Arabic spelling was corrected. I found no other unlisted production change.
- **Test plan:** all 62 api rows and the ai and web rows exist with the planned names. The deleted tests are exactly the listed ones.
- **Postman:** the lesson send stores `avatarConversationId`. The follow-up request uses it. `history` is gone from both bodies. The new AvatarConversations folder follows Avatar and contains List (with disabled filter params, storing `adminAvatarConversationId`) and then Get.
- **Docs:** avatar.md (History, HTTP, errors, config, Conversation log, Admin view, Not in this story), PRD 9.3/10.5/15/16, ai-service.md, deployment.md, claude-design-prompt.md section 4 and prototype.md all agree with the code. No divergence.
- **Builds (run by me):**
  - `dotnet test api/ -c Release` with `appsettings.json` moved aside: total 3079, failed 0. The file was restored afterwards.
  - ai: `ruff format --check` (60 files formatted) and `ruff check` both pass, `mypy src` reports no issues, `pytest -m "not eval"` has 137 passed.
  - web: `typecheck` and `lint` are clean, prettier (end-of-line auto) is clean, `vitest run` has 165 files and 966 tests passed.

## Test quality
- `AvatarConversationTests`: constrains positions, trimming, timestamps, stamping and the `IsFor` rows (the Theory covers each branch and the entry-point mismatch).
- `SendAvatarMessageHandlerConversationTests`:
  - It constrains the owner filter, the context check, the history order and roles, the truncation, the positions, the Add/no-Add behaviour and the single save.
  - The context test compares the stored sources with what was actually sent to the AI, not with a stub echo.
- `GetAvatarConversationsFilterTests` and `HandlerTests`:
  - The filter tests compile the real expression, so the boundaries, the case-insensitive text match and the student-id match are all exercised.
  - Handler test 40 feeds the user predicate through to the page filter.
- `GetAvatarConversationHandlerTests`: constrains the ordering, the JSON read-back and the totals.
- The integration tests check the persisted rows through a fresh scope, and 59 and 57/58 exercise real Postgres behaviour.
- Web page tests go through MSW and the real routing.
- No vacuous test found.
