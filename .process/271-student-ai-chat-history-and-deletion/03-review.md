VERDICT: APPROVED

# Review — Student AI chat history and deletion (#271, E18.S7)

## Blocking
None.

## Non-blocking
- `api/Elmanhg.Tests/Integration/Avatar/AvatarConversationDeletionEndpointTests.cs:63` — the admin-list assertion `BeEmpty()` has no positive control: if the admin search stopped matching display names, the test would still pass. The admin detail 404 on line 64 carries the real proof. You could assert that the list contains the id before the delete.
- `api/Elmanhg.Tests/Integration/Persistence/AvatarConversationErasureTests.cs` — there is no `UPDATE` test on `AvatarTrainingRecords` with the setting set. The same trigger function covers it and `TrainingRecordAppendOnlyTests` covers a plain UPDATE, so this would only be a belt-and-braces test.
- `web/src/features/avatar/components/AvatarPanelHeader.tsx:22` — the history button hides during an exam, but a panel already in the `history` view stays there if an exam starts in another tab. The server refuses with 403 and the view shows the generic history error rather than the exam notice. Server-side gating is correct, so this is UX polish only.

## Verified
- **Migration** (`20261002204530_AddAvatarConversationErasure.cs:13-48`): the SQL matches the plan section "Erasure SQL" exactly.
  - The functions are not SECURITY DEFINER, so search_path hijack does not apply.
  - The uuid is cast to text inside the function. The app calls it through a parameterised `FormattableString` (`AvatarConversationRepository.cs:23`), so there is no injection surface.
  - UPDATE always raises. A DELETE passes only when the row ConversationId equals the transaction-local setting.
  - The no_truncate triggers are untouched.
  - Down restores `reject_append_only_mutation()` on both triggers, matching `AddAvatarConversations.cs:158` and `AddTrainingRecords.cs:158`.
  - Grep finds no other code (api, ai, web) that sets `elmanhg.erase_avatar_conversation`. The test helpers set it only to prove it is rejected.
- **Ownership is server-side.** All three handlers take the student id from `ICurrentUserService.UserId` and put it in the predicate:
  - delete: `DeleteMyAvatarConversationHandler.cs:30`
  - detail: `GetMyAvatarConversationHandler.cs:28`
  - list: `GetMyAvatarConversationsHandler.cs:30`

  Another student id returns 404. Predicate-compiling unit tests and these integration tests prove it: `GetMineDetail_OtherStudents_Returns404`, `Delete_OtherStudents_Returns404AndKeepsMessages`, and `GetMine_Student_ReturnsOwnConversationsNewestFirst` with a foreign row.
- **Exam gate:** list and detail call `AvatarGate.EnsureNoExamInProgressAsync`. `GetMine_ExamInProgress_Returns403` covers it.
- **Runtime flag:** checked server-side before any load (`DeleteMyAvatarConversationHandler.cs:20`). `Delete_WhenSettingOff_Returns400AndKeepsMessages` covers it, and the UI hides delete via `conversationDeletionEnabled`.
- **Data:**
  - Usage rows are kept and messagesUsedToday is unchanged (`Delete_AfterSending_KeepsDailyUsage`).
  - Training rows are erased (`Delete_Own_ErasesMessagesAndTrainingRecords`).
  - The audit diff has messageCount 2 to 0 and no question or reply text (`Delete_Own_AuditedWithMessageCountAndNoText`).
  - The admin list and detail hide the deleted chat, and a send to it returns 404.
  - Teachers and admins get 403 from the Avatar.Chat policy.
- **Atomicity:** erase, soft delete and save run in one `ExecuteInTransactionAsync`. The xmin conflict rolls the erasure back. SaveChangesAsync is in the handler, with Received(1) on success and DidNotReceive() on every throwing path.
- **Tests I ran myself:**
  - `dotnet test -c Release` in api/ with no appsettings.json: 5091 passed, 0 failed.
  - Re-run filtered to AvatarConversationErasureTests, AvatarMessageAppendOnlyTests and TrainingRecordAppendOnlyTests: 21 of 21 passed. That includes the extra no-leak-after-commit test and TRUNCATE on AvatarMessages.
- **Formatting:** `dotnet format --verify-no-changes`, scoped to the changed folders, exits 0.
- **Web checks:**
  - `npx tsc -b`: OK.
  - `eslint --max-warnings=0`: OK.
  - prettier on the avatar feature: OK.
  - `npx vitest run`: 281 files, 1614 tests passed, no timeouts.
  - `npm run build` and `perf:budget`: exit 0. The history UI is imported only from the lazy AvatarPanel.
- **Generated client:** I re-ran `npm run gen:api` and it produced byte-identical output to the working tree, so the generated client is in sync with api/openapi/v1.json.
- **i18n:** ar and en have identical key sets (56 each).
- **Design tokens:** only tokens are used, with no literal colour, size or shadow. Layout uses logical properties (text-start, rtl:rotate-180). Icon buttons have aria-label. The axe and RTL tests pass.
- **Postman:** the three requests are in the Avatar folder after "Send avatar message (global)", and Delete is last.
  - They use collection auth, the correct methods and URLs, and avatarConversationId.
  - The later admin folder uses its own adminAvatarConversationId, so the delete does not break the collection run.
- **Docs-sync:** the code and these docs agree:
  - avatar.md: HTTP, errors, configuration, erasure exemption, privacy, retention, Student history and UI.
  - training-data.md: append-only, already-exported files and the checklist. The consent line stays open.
  - configuration.md
  - PRD.md 9.4, 10.6 and 13
  - audit-log.md
  - claude-design-prompt.md section 4 and section 5 rule 14
  - prototype.md
  - deployment.md
  - backlog.json (valid JSON)
  - implementation-report.md

  No stale "kept indefinitely" or "no student history view" text remains.
- **Deviations in 02-implementation.md** are real and justified:
  - The registry-count test was renamed to 22, following the #269 precedent.
  - AppDbContextTests has the migration appended.
  - There is one extra erasure test.
  - `queryClient.query` replaces the deprecated fetchQuery.
  - The optional `status` prop is typed `| undefined`, which `exactOptionalPropertyTypes` requires.

## Test quality
- DeleteMyAvatarConversationHandlerTests: constrains the implementation. The stub compiles the real predicate, so dropping the owner clause fails the other-student test. Received.InOrder pins erase-before-save, and the flag test asserts no transaction starts.
- GetMyAvatarConversationsHandlerTests: constrains it. The captured filter is compiled and checked against both own and foreign conversations.
- GetMyAvatarConversationHandlerTests: constrains it, covering order, roles, parsed citations and the owner predicate.
- AvatarConversationErasureTests: strong. It asserts SQLSTATE P0001 and the surviving row state for:
  - a DELETE with the setting pointing at another conversation
  - an UPDATE with the setting pointing at the same conversation
  - a training-record DELETE without the setting
  - a DELETE after the setting transaction committed
  - TRUNCATE

  It also checks the function return count and that the setting is cleared.
- Endpoint integration tests: end-to-end against Postgres. The admin-list check is weak (see Non-blocking), but the other assertions are concrete.
- Web tests: they assert request bodies, MSW call counts on cancel, rendered text, toasts and the reset after a deletion. None of them is vacuous.
