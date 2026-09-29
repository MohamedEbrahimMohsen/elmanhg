# Implementation — [E8.S4] Conversation logging (#92)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| api/Elmanhg.Domain/Avatar/AvatarConversation.cs | 65 | aggregate: `Start`, `IsFor`, `RecentMessages`, `RecordExchange`, microsecond truncation |
| api/Elmanhg.Domain/Avatar/AvatarMessage.cs | 56 | append-only child: `FromStudent`, `FromAssistant` |
| api/Elmanhg.Domain/Avatar/AvatarMessageRole.cs | 3 | `Student`, `Assistant` |
| api/Elmanhg.Domain/Avatar/AvatarAssistantReply.cs | 3 | reply value object |
| api/Elmanhg.Domain/Avatar/IAvatarConversationRepository.cs | 5 | repository interface |
| api/Elmanhg.Application/Avatar/SendAvatarMessage/SendAvatarMessageHandler.Conversation.cs | 41 | `LoadConversationAsync`, `HistoryOf`, `StartConversation` |
| api/Elmanhg.Application/Avatar/Shared/AvatarMessageContext.cs | 5 | `{bundle, sources}` record |
| api/Elmanhg.Application/Avatar/Shared/AvatarMessageJson.cs | 16 | context and citations JSON, `QuestionJson.SerializerOptions` |
| api/Elmanhg.Application/Avatar/Shared/AdminAvatarConversationResult.cs | 5 | list item |
| api/Elmanhg.Application/Avatar/Shared/AdminAvatarConversationDetailResult.cs | 5 | detail |
| api/Elmanhg.Application/Avatar/Shared/AdminAvatarMessageResult.cs | 5 | message |
| api/Elmanhg.Application/Avatar/Shared/AdminAvatarConversationResultGenerator.cs | 59 | list and detail mapping, totals |
| api/Elmanhg.Application/Avatar/GetAvatarConversations/GetAvatarConversationsQuery.cs | 8 | query |
| api/Elmanhg.Application/Avatar/GetAvatarConversations/GetAvatarConversationsValidator.cs | 23 | 5 validation codes |
| api/Elmanhg.Application/Avatar/GetAvatarConversations/GetAvatarConversationsFilter.cs | 22 | `Term`, `Build` |
| api/Elmanhg.Application/Avatar/GetAvatarConversations/GetAvatarConversationsHandler.cs | 52 | paged list, name lookups |
| api/Elmanhg.Application/Avatar/GetAvatarConversation/GetAvatarConversationQuery.cs | 6 | query |
| api/Elmanhg.Application/Avatar/GetAvatarConversation/GetAvatarConversationHandler.cs | 26 | detail |
| api/Elmanhg.Infrastructure/Avatar/AvatarConversationRepository.cs | 7 | repository |
| api/Elmanhg.Infrastructure/Migrations/20260929163757_AddAvatarConversations.cs (+ .Designer.cs) | 178 | tables, FKs, indexes, 2 append-only triggers; `Down` drops the triggers only, not the function |
| api/Elmanhg.Api/Controllers/Avatar/AvatarConversationsController.cs | 35 | 2 GET actions, `AvatarConversations.View` |
| api/Elmanhg.Tests/Builders/AvatarConversationBuilder.cs | 71 | builder + `Reply(...)` |
| api/Elmanhg.Tests/Domain/Avatar/AvatarConversationTests.cs | 106 | tests 1–7 |
| api/Elmanhg.Tests/Application/Features/Avatar/SendAvatarMessage/SendAvatarMessageHandlerConversationTests.cs | 176 | tests 8–19 |
| api/Elmanhg.Tests/Application/Features/Avatar/Shared/AvatarMessageJsonTests.cs | 46 | tests 22–24 |
| api/Elmanhg.Tests/Application/Features/Avatar/GetAvatarConversations/GetAvatarConversationsValidatorTests.cs | 75 | tests 25–31 |
| api/Elmanhg.Tests/Application/Features/Avatar/GetAvatarConversations/GetAvatarConversationsFilterTests.cs | 83 | tests 32–37 |
| api/Elmanhg.Tests/Application/Features/Avatar/GetAvatarConversations/GetAvatarConversationsHandlerTests.cs | 102 | tests 38–40 |
| api/Elmanhg.Tests/Application/Features/Avatar/GetAvatarConversation/GetAvatarConversationHandlerTests.cs | 102 | tests 41–43 |
| api/Elmanhg.Tests/Integration/Avatar/AvatarConversationLogEndpointTests.cs | 95 | tests 44–47 |
| api/Elmanhg.Tests/Integration/Avatar/AvatarConversationsEndpointTests.cs | 155 | tests 48–56 |
| api/Elmanhg.Tests/Integration/Persistence/AvatarMessageAppendOnlyTests.cs | 48 | tests 57–58 |
| api/Elmanhg.Tests/Integration/Persistence/AvatarConversationPersistenceTests.cs | 40 | test 59 |
| web/src/features/avatarConversations/index.ts, locales.ts | 6, 4 | exports, locales |
| web/src/features/avatarConversations/i18n/ar.json, en.json | 93, 93 | copy per the plan's table |
| web/src/features/avatarConversations/schemas/avatarConversationSearchSchema.ts (+ .test.ts) | 16 (28) | URL search schema; test 76 |
| web/src/features/avatarConversations/schemas/avatarConversationFiltersSchema.ts (+ .test.ts) | 21 (39) | form schema; test 77 |
| web/src/features/avatarConversations/api/avatarConversationParams.ts (+ .test.ts) | 44 (50) | params, page mapping; test 78 |
| web/src/features/avatarConversations/api/avatarConversationFormat.ts (+ .test.ts) | 14 (14) | USD and count formatting; test 79 |
| web/src/features/avatarConversations/hooks/useAvatarConversationSearch.ts, useAvatarConversations.ts, useAvatarConversation.ts | 38, 10, 5 | URL state and queries |
| web/src/features/avatarConversations/components/AvatarConversationFilters.tsx, EntryPointField.tsx, AvatarConversationTable.tsx, AvatarConversationRow.tsx, AvatarConversationEmptyState.tsx, AvatarConversationSkeleton.tsx, AvatarConversationSummary.tsx, AvatarLoggedMessage.tsx, AvatarLoggedContext.tsx | 53, 36, 39, 45, 24, 20, 57, 76, 67 | list and detail UI |
| web/src/features/avatarConversations/pages/AvatarConversationsPage.tsx (+ .test.tsx) | 75 (155) | list page; tests 80–88 |
| web/src/features/avatarConversations/pages/AvatarConversationPage.tsx (+ .test.tsx) | 73 (99) | detail page; tests 89–95 |
| web/src/routes/admin/avatar-conversations.tsx, avatar-conversation.$conversationId.tsx | 7, 11 | routes |
| web/src/test/avatarConversationFixtures.ts | 110 | fixtures |

## Files modified
| Path | Change |
|---|---|
| api/Elmanhg.Domain/SharedKernel/DefaultCodes.cs | `AvatarConversationsView` |
| api/Elmanhg.Api/Authorization/PermissionMatrixPolicies.cs | Admin-only policy |
| api/Elmanhg.Application/Exceptions/ErrorCodes.cs | 2 history codes removed, 8 added |
| api/Elmanhg.Api/Resources/Messages.ar.resx, Messages.en.resx | 2 keys removed, 8 added |
| api/Elmanhg.Application/Avatar/SendAvatarMessage/SendAvatarMessageCommand.cs | `History` replaced by `Guid? ConversationId` |
| api/Elmanhg.Application/Avatar/SendAvatarMessage/SendAvatarMessageValidator.cs | history rules and `Alternates` removed |
| api/Elmanhg.Application/Avatar/SendAvatarMessage/SendAvatarMessageHandler.cs | new ctor dependency, new `Handle` order (plan steps 1–15), one save |
| api/Elmanhg.Application/Avatar/Shared/AvatarReplyResult.cs | `ConversationId` first |
| api/Elmanhg.Application/Avatar/Shared/AvatarTurn.cs, AvatarTurnRole.cs | deleted |
| api/Elmanhg.Application/Shared/AiService/AiChatReply.cs | `decimal CostUsd` |
| api/Elmanhg.Application/Shared/Options/AvatarOptions.cs | 2 keys, `HistoryTurnMaxLength` comment |
| api/Elmanhg.Infrastructure/AiService/FakeAiServiceClient.cs | cost `0m` |
| api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs | DbSets, consts, mapping, filters, 2 catch clauses |
| api/Elmanhg.Infrastructure/DependencyInjection.cs | repository registration |
| api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs | regenerated (199 additions only) |
| api/Elmanhg.Api/appsettings.example.json, deploy/api.env.example | 2 Avatar keys |
| api/openapi/v1.json | regenerated by `dotnet build` |
| api/Elmanhg.Tests/Integration/Infrastructure/ApiFactory.cs | 2 `UseSetting` keys |
| api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs | `twentyNinth` `_AddAvatarConversations` |
| api/Elmanhg.Tests/Integration/Authorization/PermissionMatrixPolicyTests.cs | 3 rows |
| api/Elmanhg.Tests/Application/Features/Avatar/AvatarTestData.cs | `Conversations`, `AddedConversation`, `StubConversations`, cost `0.0021m`, `null` conversation ids |
| api/Elmanhg.Tests/Application/Features/Avatar/SendAvatarMessage/SendAvatarMessageHandlerTests.cs | cost args, `ConversationId` assertion, `Handle_HistoryTurns_MapsRolesToAiChatMessages` deleted |
| api/Elmanhg.Tests/Application/Features/Avatar/SendAvatarMessage/SendAvatarMessageHandlerWithheldSourcesTests.cs | `, 0m` |
| api/Elmanhg.Tests/Application/Features/Avatar/SendAvatarMessage/SendAvatarMessageValidatorTests.cs | `null` ids, `ConversationId` pass case, 5 history tests + `Turn` deleted |
| api/Elmanhg.Tests/Application/Features/Avatar/SendAvatarMessage/SendAvatarMessageHandlerContextTests.cs | 2 command ctor calls `[]` → `null` (see Deviations) |
| api/Elmanhg.Tests/Infrastructure/AiService/HttpAiServiceClientTests.cs | `costUsd` in body and assertion |
| api/Elmanhg.Tests/Integration/Avatar/AvatarTestData.cs | 4 helpers |
| ai/src/elmanhg_ai/pipelines/chat.py, api/chat/schemas.py, api/chat/router.py | `cost_usd` computed once, returned as `costUsd` |
| ai/openapi/v1.json | regenerated |
| ai/tests/unit/test_chat_pipeline.py | expected result gains cost; `test_chat_run_returns_cost_usd_from_token_usage` |
| ai/tests/integration/test_chat_endpoint.py | `test_chat_valid_request_returns_cost_usd`; key set gains `costUsd` (see Deviations) |
| ai/tests/unit/test_avatar_chat_eval.py | `_result` helper gains `cost_usd` (see Deviations) |
| web/src/features/avatar/api/avatarContext.ts, hooks/avatarReducer.ts, hooks/useAvatarChat.ts, components/AvatarComposer.tsx | `conversationId` replaces history |
| web/src/features/avatar/api/avatarContext.test.ts, hooks/avatarReducer.test.ts, components/AvatarPanel.test.tsx | tests 66–73 |
| web/src/test/avatarFixtures.ts | `avatarConversationId`, reply `conversationId` |
| web/src/features/session/permissions.ts (+ .test.ts) | `avatarConversationsView`; test 74 |
| web/src/features/shell/navConfig.ts, i18n/ar.json, i18n/en.json, pages/MorePage.test.tsx | nav item; test 75 |
| web/src/app/i18n.ts | `avatarConversations` namespace |
| web/src/shared/i18n/ar.json, en.json | `errors.AVATAR_CONVERSATION_NOT_FOUND` |
| web/orval.config.ts | `GetAvatarConversations` zod query generation off (see Deviations) |
| web/src/routeTree.gen.ts, web/src/shared/api/generated/** | regenerated (25 generated paths; `avatarTurn*` gone) |
| postman/elmanhg.postman_collection.json | 2 variables; lesson send stores the id; follow-up request; `history` removed; AvatarConversations folder (2 requests) after Avatar |
| docs/avatar.md, ai-service.md, PRD.md (§9.3, §10.5, §15, §16), claude-design-prompt.md §4, prototype.md, deployment.md | per the Docs table |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| `SendAvatarMessageCommand.cs`: drop the `Application.Avatar.Shared` using | `AvatarReplyResult` lives in that namespace; the build failed without it | kept the using |
| Modify only the listed files | `SendAvatarMessageHandlerContextTests.cs` builds `SendAvatarMessageCommand` with `[]` for history in 2 places and no longer compiled | changed those 2 arguments to `null`; no assertion touched |
| Modify only the listed ai tests | `test_chat_endpoint.py::test_chat_valid_request_returns_reply` asserts the exact response key set, and `test_avatar_chat_eval.py::_result` builds `ChatResult` without the new required `cost_usd` | added `"costUsd"` to the key set; added `cost_usd=Decimal("0")` to the helper |
| Orval regenerates cleanly | the generated zod for `GetAvatarConversations` query params (int defaults on a string format) does not type-check; the repo disables query zod for every paged list (`GetPaymentLog`, `GetAuditLogs`, …) in `web/orval.config.ts` | added `GetAvatarConversations: { zod: { generate: { query: false } } }` there and regenerated |
| Web copy `table.openLabel` `{{student}}`, `detail.tokens` `{{input}}`/`{{output}}` | the app uses i18next-icu, whose placeholders are `{name}` | used `{student}`, `{input}`, `{output}` |
| Error-code Arabic written as in the plan (e.g. «سياقا اخر», «ارسلت») | every existing resx entry uses hamza and tanwin | wrote the same words with standard spelling («سياقًا آخر», «أُرسلت», «أن», «أكثر») |

## Build & test
- `dotnet build api/`: Build succeeded, no warnings outside core-libraries; `api/openapi/v1.json` regenerated.
- `dotnet test api/ -c Release` with `api/Elmanhg.Api/appsettings.json` moved aside (restored afterwards): `total: 3079, failed: 0, succeeded: 3079`.
- `dotnet format api/Elmanhg.slnx --verify-no-changes --exclude api/core-libraries`: one pre-existing whitespace finding in `Elmanhg.Tests/Builders/SubscriptionBuilder.cs` (not touched here); nothing in changed files.
- ai, as `ai-ci.yml`: `python -m uv run ruff format --check .` (60 files already formatted), `ruff check .` (All checks passed), `mypy src` (no issues, 35 files), `pytest -m "not eval"`: `137 passed, 1 deselected`.
- web: `npm run typecheck` clean, `npm run lint` clean, `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` clean, `npx vitest run`: `Test Files 165 passed, Tests 966 passed`.
- Mutation checks (each failed as expected, then restored): `IsFor` Lesson → `true` fails the Theory row; dropping `matchingStudentIds.Contains` fails `Build_Search_MatchesListedStudentIds`; dropping `AvatarText.Truncate` fails `Handle_StoredTurnOverHistoryTurnMax_IsTruncated`; a fixed `cost_usd` fails `test_chat_run_returns_cost_usd_from_token_usage`; sending `null` instead of `state.conversationId` fails `continues the same conversation on the next question`. The BOLA filter (`x.StudentId == studentId`) was verified by reading; tests 17 and 46 assert it.

## Notes for review
- Test counts: api +66 new test cases (7 domain incl. a 7-row Theory, 12 handler conversation, 3 JSON, 7 validator, 6 filter, 3 list handler, 3 detail handler, 4 log endpoint, 9 admin endpoint, 2 append-only, 1 concurrency); 6 deleted as planned. web +25 new/replaced cases; 1 deleted as planned. ai +2.
- The #89 sanitising, the #91 exam guard and the #91 withheld-question filter are untouched: the exam gate and entitlement still run first, `LoadContextAsync`/`SearchAsync` are unchanged, and the stored sources are the filtered list actually sent.
- `LoadConversationAsync` runs before `LoadContextAsync` (plan step 4), so a context mismatch with a random lesson id is 400, not 404 (test 47 relies on it).
- The concurrency test may hit either catch (row version or position unique index); both map to the same code.
- The detail page's back chevron is `ChevronLeft` with `rtl:rotate-180`; the summary shows the token line under the `dl` rather than as a `dl` row.
- Guard grep: `timeProvider.GetUtcNow()` (plan) and `DateTimeOffset.UtcNow` in `AvatarConversationPersistenceTests` (test code, as in `TeacherThreadPersistenceTests`).
- `AvatarMessageEndpointTests` is unchanged and still posts `history`; it passes, which shows old clients keep working.
- The local `appsettings.json` needs no change: both new keys have code defaults.
