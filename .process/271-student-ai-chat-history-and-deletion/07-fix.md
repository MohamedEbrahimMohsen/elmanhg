# Fix — CodeRabbit triage, PR #272

| Item | What I changed | File:line |
|---|---|---|
| RC1 | The `PageNumber` rule now also has `.Must((query, pageNumber) => ((long)pageNumber - 1) * query.PageSize <= int.MaxValue)` with `.WithErrorCode(ErrorCodes.AvatarConversationsPageNumberInvalid)`. No shared max-page rule exists in the repo, so this is local. There is no new error code. | `api/Elmanhg.Application/Avatar/GetMyAvatarConversations/GetMyAvatarConversationsValidator.cs:15-18` |
| RC1 test | `Validate_PageOffsetOverflowsInt_FailsWithPageNumberInvalid`: `PageNumber: int.MaxValue, PageSize: 20` fails with exactly the page-number code. | `api/Elmanhg.Tests/Application/Features/Avatar/GetMyAvatarConversations/GetMyAvatarConversationsValidatorTests.cs:30-36` |
| RC2 | `AvatarPanel` derives `view` during render: `'chat'` whenever `status.data.examInProgress`, otherwise `state.view`. The context line, body branch and header all use it. | `web/src/features/avatar/components/AvatarPanel.tsx:15,30-31,54` |
| RC2 | `AvatarPanelHeader` takes a `view: AvatarView` prop in place of reading `state.view`, so "Back to the chat" goes away with the history list. | `web/src/features/avatar/components/AvatarPanelHeader.tsx:6,10,14,16,25,37` |
| RC2 test | `leaves the history view and shows the exam notice when an exam starts`: open Past chats and see one item, then switch the status mock to `examInProgress: true` and invalidate the status query. The test expects the refusal, no "Past chats" heading and no "Back to the chat" button. Without the fix it fails, because the history list stays. | `web/src/features/avatar/components/AvatarPanel.test.tsx:118-130` |

PC1 is a summary only, so nothing to do. I changed no docs (see 06-triage.md).

## Deviations
None.

## Build & test
- `dotnet test api/Elmanhg.Tests/Elmanhg.Tests.csproj -- --filter-namespace "*Avatar*"`: Passed. total 190, failed 0. There is no `api/Elmanhg.Api/appsettings.json`, which matches CI.
- `npm --prefix web test -- --run src/features/avatar`: 16 files, 86 tests passed.
- I stashed the RC2 production change and ran the new test alone. It failed: the refusal was not found and the history list was still rendered. With the change restored it passes.
- `npm --prefix web run typecheck`: clean.
- `npm --prefix web run lint` (`--max-warnings=0`): clean.
- `npx prettier --check --end-of-line auto src/features/avatar` (run from `web/`): all files formatted.

## Notes for review
- `state.view` stays `'history'` during the exam. If the exam ends while the panel is still open, the history list comes back. That seems acceptable, and it avoids an effect that copies query data into the reducer.
- The other paginated validators have the same overflow. That code existed before this PR, so I did not change it.
