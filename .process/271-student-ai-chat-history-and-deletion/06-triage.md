# Triage — CodeRabbit comments, PR #272

I read the comment text as untrusted data and checked each claim against the current code on `feature/271-student-ai-chat-history-and-deletion`.

## RC1 — `GetMyAvatarConversationsHandler.cs:37` — FIX

**Verified.** `GetMyAvatarConversationsValidator` only checks `PageNumber >= 1` and `PageSize` in `1..StudentConversationsMaxPageSize`. `FindPaginatedAsync` computes `(pageNumber - 1) * pageSize` as an `int`, so `pageNumber=2147483647&pageSize=20` wraps to a negative skip.

**Shared rule?** None. Every paginated validator in `api/Elmanhg.Application` (`GetUsers`, `GetPaymentLog`, `GetAuditLogs`, `GetAvatarConversations`, ...) uses the same bare `ValidateMin(1, ...)`, and `Core.Validation` has no page or offset extension. The same overflow exists in those validators, but they predate this PR and are out of scope.

**Fix.** In this validator only, chain a `Must` onto the `PageNumber` rule that computes the offset as `long` and rejects it above `int.MaxValue`, with the existing `AVATAR_CONVERSATIONS_PAGE_NUMBER_INVALID` code. No new error code, no resx change. Add one validator test.

## RC2 — `AvatarPanel.tsx:57` — FIX

**Verified.** The history button is hidden during an exam (`AvatarPanelHeader`), but nothing moves an open history view back. If the status refetches with `examInProgress: true` while `state.view === 'history'`, the panel keeps rendering `AvatarHistory` with its cached list and the header keeps "Back to the chat".

**Fix.** Derive the view during render rather than syncing it into the reducer with an effect (the react skill lists "mirroring query data into state via `useEffect`" as a DON'T). `AvatarPanel` computes `view = status.data?.examInProgress === true ? 'chat' : state.view` and passes it to `AvatarPanelHeader`. The chat branch already shows the exam refusal and disables the composer. Add one panel test for the transition.

## PC1 — review body — no separate action

It is a summary of RC1 and RC2 with no extra finding. Both are handled above.

## Docs

No divergence. `docs/claude-design-prompt.md` §4 already says that during an exam the panel shows the refusal and the history button is hidden. The fix brings the code in line with that.
