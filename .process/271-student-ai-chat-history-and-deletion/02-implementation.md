# Implementation — Student AI chat history and deletion (#271, E18.S7)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/Elmanhg.Infrastructure/Migrations/20261002204530_AddAvatarConversationErasure.cs` (+ `.Designer.cs`, tool-generated) | 51 | Erasure SQL from the plan, verbatim: `reject_avatar_mutation_unless_erasing()`, both row triggers re-pointed, `erase_avatar_conversation(uuid)`. Down restores `reject_append_only_mutation()` on both row triggers and drops both functions. `*_no_truncate` untouched. Model snapshot unchanged. |
| `api/Elmanhg.Application/Avatar/GetMyAvatarConversations/GetMyAvatarConversationsQuery.cs` | 7 | Paged query |
| `.../GetMyAvatarConversations/GetMyAvatarConversationsValidator.cs` | 18 | Page number and `StudentConversationsMaxPageSize` |
| `.../GetMyAvatarConversations/GetMyAvatarConversationsHandler.cs` | 55 | Auth guard, exam gate, owner filter, names lookup |
| `api/Elmanhg.Application/Avatar/GetMyAvatarConversation/GetMyAvatarConversationQuery.cs` | 6 | Detail query |
| `.../GetMyAvatarConversation/GetMyAvatarConversationValidator.cs` | 13 | `AVATAR_CONVERSATION_ID_REQUIRED` |
| `.../GetMyAvatarConversation/GetMyAvatarConversationHandler.cs` | 34 | Auth, exam gate, id and owner predicate (404 = BOLA), names |
| `api/Elmanhg.Application/Avatar/DeleteMyAvatarConversation/DeleteMyAvatarConversationCommand.cs` | 11 | `IAuditableCommand` `AvatarConversation.Delete` |
| `.../DeleteMyAvatarConversation/DeleteMyAvatarConversationValidator.cs` | 13 | Id required |
| `.../DeleteMyAvatarConversation/DeleteMyAvatarConversationHandler.cs` | 35 | Auth, flag check, then a transaction: load by id and owner, erase, `Delete(now)`, one `SaveChangesAsync` |
| `api/Elmanhg.Application/Avatar/Shared/StudentAvatarConversationResult.cs` | 5 | List item |
| `api/Elmanhg.Application/Avatar/Shared/StudentAvatarConversationDetailResult.cs` | 5 | Detail |
| `api/Elmanhg.Application/Avatar/Shared/StudentAvatarMessageResult.cs` | 5 | Message: text and citations only |
| `api/Elmanhg.Application/Avatar/Shared/StudentAvatarConversationResultGenerator.cs` | 25 | Mapping |
| `api/Elmanhg.Tests/Application/Features/Avatar/GetMyAvatarConversations/GetMyAvatarConversationsHandlerTests.cs` | 119 | Tests 7–11 |
| `.../GetMyAvatarConversations/GetMyAvatarConversationsValidatorTests.cs` | 47 | Tests 12–15 |
| `.../GetMyAvatarConversation/GetMyAvatarConversationHandlerTests.cs` | 119 | Tests 16–20 |
| `.../GetMyAvatarConversation/GetMyAvatarConversationValidatorTests.cs` | 26 | Test 21 |
| `.../DeleteMyAvatarConversation/DeleteMyAvatarConversationHandlerTests.cs` | 120 | Tests 22–27 |
| `.../DeleteMyAvatarConversation/DeleteMyAvatarConversationValidatorTests.cs` | 26 | Test 28 |
| `api/Elmanhg.Tests/Integration/Avatar/MyAvatarConversationsEndpointTests.cs` | 126 | Tests 35–41 |
| `api/Elmanhg.Tests/Integration/Avatar/AvatarConversationDeletionEndpointTests.cs` | 198 | Tests 42–51 (`RuntimeSettingsCollection`, overrides cleared in Initialize and Dispose) |
| `api/Elmanhg.Tests/Integration/Persistence/AvatarConversationErasureTests.cs` | 179 | Tests 29–34, plus one extra test (see Deviations) |
| `web/src/features/avatar/api/avatarHistory.ts` | 30 | `avatarHistoryPageSize`, `conversationTitle`, `toResumed` |
| `web/src/features/avatar/hooks/useAvatarHistory.ts` | 35 | List query, `resume`, `openingId` |
| `web/src/features/avatar/hooks/useDeleteAvatarConversation.ts` | 38 | Delete mutation: dispatch, invalidate, toasts |
| `web/src/features/avatar/components/AvatarPanelHeader.tsx` | 54 | Title, history or back button, close |
| `web/src/features/avatar/components/AvatarHistory.tsx` | 84 | History view: loading, error and retry, empty, list, pagination, dialog |
| `web/src/features/avatar/components/AvatarHistoryItem.tsx` | 54 | One item: open button and delete icon |
| `web/src/features/avatar/components/DeleteAvatarConversationDialog.tsx` | 52 | Confirmation |
| `web/src/features/avatar/api/avatarHistory.test.ts` | 50 | Tests 52–55 |
| `web/src/features/avatar/components/AvatarHistory.test.tsx` | 291 | Tests 64–76 |
| `web/src/shared/api/generated/model/*` (5 new files) | — | Orval output |

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.Domain/Avatar/AvatarConversation.cs` | `IAuditedEntity`; `Delete(DateTimeOffset deletedAt)` as in the plan |
| `api/Elmanhg.Domain/Avatar/IAvatarConversationRepository.cs` | `ExecuteInTransactionAsync`, `EraseMessagesAsync` |
| `api/Elmanhg.Infrastructure/Avatar/AvatarConversationRepository.cs` | Execution strategy plus transaction; parameterised `ExecuteSqlAsync($"SELECT erase_avatar_conversation({conversationId})")` |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs`, `Messages.en.resx`, `Messages.ar.resx` | 2 codes |
| `api/Elmanhg.Application/Shared/Options/AvatarOptions.cs` | `StudentConversationsMaxPageSize` `[Range(1, 100)]` = 50 |
| `api/Elmanhg.Api/appsettings.example.json`, `deploy/api.env.example`, `ApiFactory.cs`, `docs/deployment.md` | New key |
| `api/Elmanhg.Application/Shared/RuntimeSettings/Definitions/FeatureFlagRuntimeSettings.cs` | `features.studentsCanDeleteAvatarChats`, default `true` |
| `api/Elmanhg.Application/Avatar/Shared/AvatarStatusResult.cs`, `GetAvatarStatusHandler.cs` | `ConversationDeletionEnabled` |
| `api/Elmanhg.Api/Controllers/Avatar/AvatarController.cs` | 3 actions, `DefaultCodes.AvatarChat` |
| `api/openapi/v1.json` | Regenerated |
| `api/Elmanhg.Tests/...` | `GetAvatarStatusHandlerTests` (modified test plus new test), `FeatureFlagRuntimeSettingsTests` (+1), `RuntimeSettingValuesTests` (+1 key), `AvatarConversationTests` (+2), integration `AvatarTestData` (helpers), `AppDbContextTests` (+`_AddAvatarConversationErasure`), `RuntimeSettingRegistryTests` (see Deviations) |
| `postman/elmanhg.postman_collection.json` | 3 requests after "Send avatar message (global)"; Delete is last |
| `web/orval.config.ts`, `web/src/shared/api/generated/**` | Override and regeneration |
| `web/src/features/avatar/hooks/avatarReducer.ts` | `view` and 4 actions; `failed` + `conversationGone` resets the id |
| `web/src/features/avatar/api/avatarErrors.ts` | `conversationGone` |
| `web/src/features/avatar/components/AvatarPanel.tsx` | Header extracted; history and chat switch |
| `web/src/features/avatar/i18n/en.json`, `ar.json` | Plan keys |
| `web/src/test/avatarFixtures.ts` | `conversationDeletionEnabled` and the 3 new fixtures |
| `web/.../avatarReducer.test.ts`, `avatarErrors.test.ts`, `AvatarPanel.test.tsx` | Tests 56–63 added; no existing test edited |
| Docs | `avatar.md`, `training-data.md`, `configuration.md`, `audit-log.md`, `PRD.md` (§9.4, §10.6, §13), `claude-design-prompt.md` (§4 panel bullet, §5 rule 14), `prototype.md`, `deployment.md`, `backlog.json`, `implementation-report.md` |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Existing tests modified: only `GetAvatarStatusHandlerTests` and `RuntimeSettingValuesTests` | `RuntimeSettingRegistryTests.Definitions_DefaultOptions_TwentyOneOrderedByGroup` asserts the registry count (21). The new setting makes it 22. E18.S2 handled the same case by renaming the test. | Renamed it to `Definitions_DefaultOptions_TwentyTwoOrderedByGroup` and changed it to `HaveCount(22)`. This is an intentional contract change and follows the #269 precedent. |
| *Existing code touched* does not list `AppDbContextTests.cs` | The orchestrator and PROGRESS conventions require the migration in that list | Added `_AddAvatarConversationErasure` last, in timestamp order |
| Test plan 29–34 only | The orchestrator asked me to prove that the setting does not leak past the transaction | Added `AvatarConversationErasureTests.Delete_MessageAfterFlagTransactionEnds_RejectedByDatabase`. On one connection it sets the flag in a transaction and commits, then runs a plain `DELETE` of that conversation's message: P0001, row kept. |
| #29: the function return is read through `SqlQuery<int>` | That needs a conversation the function erases | Seeded a third conversation with 2 exchanges; `SqlQuery<int>($"SELECT erase_avatar_conversation({id}) AS \"Value\"")` returns `[4]` and its messages are gone |
| #33: after the function, `current_setting` is `''` | Without a check that the erasure really ran, the test would be weaker | Also asserts that the function returned 2. That proves the trigger let the DELETE through with the setting set; `''` then proves it was cleared. |
| #26 `useAvatarHistory` uses `queryClient.fetchQuery` | `fetchQuery` is `@deprecated` in the installed TanStack Query and fails `@typescript-eslint/no-deprecated` | Used `queryClient.query({ ...getGetMyAvatarConversationQueryOptions(id), staleTime: 0 })`. The behaviour is the same, and the repo already uses it in `routes/student/lesson.$lessonId.tsx`. |
| #24 `AvatarPanelHeader` props `{ status?: AvatarStatusResult }` | `exactOptionalPropertyTypes` rejects passing `undefined` explicitly (`status.isSuccess ? status.data : undefined`) | Typed it `status?: AvatarStatusResult \| undefined` |

## Build & test
CI parity: `api/Elmanhg.Api/appsettings.json` does not exist in this worktree.
- `dotnet test -c Release` (in `api/`): `Test run summary: Passed! total: 5091 failed: 0 succeeded: 5091 skipped: 0`. Run twice; the second run came after the mutation checks were reverted.
- `dotnet ef migrations has-pending-model-changes -p api/Elmanhg.Infrastructure -s api/Elmanhg.Api`: `No changes have been made to the model since the last migration.`
- `dotnet format --verify-no-changes`: the only hit outside `core-libraries` is `Elmanhg.Tests/Builders/SubscriptionBuilder.cs(57,27) WHITESPACE`. That is an untouched file, so it is local CRLF noise (PROGRESS gotcha). Nothing in a file I changed.
- Guard grep (`DateTime.Now/UtcNow`, `.Result`, `.Wait()`, `FromSqlRaw`/`ExecuteSqlRaw`, `async void`, `new HttpClient(`) over the added `.cs` lines: no hits.
- `npm --prefix web run gen:api`: rerun after the final build, the generated set is identical (5 modified and 5 new files).
- `npx tsc -b`: exit 0. `npx eslint . --max-warnings=0`: clean. `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto`: `All matched files use Prettier code style!`
- `npx vitest run` (full): `Test Files 281 passed (281)  Tests 1614 passed (1614)`. No timeouts.
- `npm run build`: ok. `npm run perf:budget`: exit 0, every page budget ok. The history UI is inside the lazy `AvatarPanel` chunk.
- Mutation checks:
  - API, 6 mutations together, each caught. Dropping `MessageCount = 0` fails the Delete domain test and the handler test. A `filter: x => true` fails `Handle_Student_FiltersByCaller`. The admin cap in the validator fails `Validate_PageSizeOverStudentMax`. Erasing after the save fails `Handle_Own_ErasesBeforeSaving`. A constant `true` in the status fails `Handle_DeletionTurnedOff…`. Dropping the owner predicate in the detail fails `Handle_OtherStudentsConversation_ThrowsNotFound`.
  - Web, 6 mutations, 7 tests failed as expected: the reducer not resetting on `conversationGone`, delete always shown, history shown during an exam, no `conversationDeleted` dispatch, the error map entry dropped, and a null `lessonId` kept.
  - All mutations were reverted by restoring copies of the files, and the restore was verified by grep.
- Security path: per the brief, I did not mutate the trigger function. The tests assert exact SQLSTATE `P0001` and the row state.
  - Plain `UPDATE`/`DELETE` on both tables is still rejected: the existing `AvatarMessageAppendOnlyTests` (Update/Delete) and `TrainingRecordAppendOnlyTests` (Update/Delete/Truncate on `AvatarTrainingRecords`) pass against the new trigger.
  - Other cases are new tests:
    - #30: DELETE with the setting pointing at another conversation.
    - #31: UPDATE with the setting set to the same conversation.
    - #32: training-row DELETE without the setting.
    - The extra test: no leak after commit.
    - #34: `TRUNCATE "AvatarMessages"`.

## Notes for review
- The plan says delete is not gated by the exam, and the code follows that: during an exam, delete still works.
- `AvatarConversation` is now `IAuditedEntity`. The send command is not `IAuditableCommand`, so sends still produce no audit row. The delete diff holds only conversation columns, and the test asserts that neither the question nor the reply text appears.
- `erase_avatar_conversation` runs in the handler's explicit transaction. The `xmin` save after it is what fails on a concurrent send or delete, and that rolls back the erasure. I did not write an integration test for the race (none was planned).
- `AvatarHistory` resets to the previous page during render when a page becomes empty, guarded by `list.isSuccess && items.length === 0 && page > 1`, as the plan says.
- The design-prompt rule went in as §5 rule 14 so the numbered list stays valid.
- Follow-ups for the orchestrator: the #215 leftover nits (last N turns per send, validating the 100-character identifier columns, the Arabic comma separator to i18n) and the notice and consent decision for training use (still `[ ]` in `docs/training-data.md`).
