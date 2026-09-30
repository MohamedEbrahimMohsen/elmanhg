VERDICT: APPROVED

# Review — [E9.S4] SLA timers, reminders and follow-up rules (#97)

## Blocking
None.

## Non-blocking
- `web/src/shared/realtime/signalRRealtimeClient.ts:7-11` and `api/Elmanhg.Api/Realtime/RealtimeExtensions.cs:24`: the server checks the JWT only when the connection starts. An open WebSocket outlives the token’s expiry, and a logout in another tab does not close it. The shell unmount closes it on logout in the same tab. Consider `MapHub(..., o => o.CloseOnAuthenticationExpiration = true)` later.
- `api/Elmanhg.Application/Shared/Options/AskTeacherOptions.cs:54-58`: nothing checks that `FirstReminderAfterHours < SecondReminderAfterHours < AskTeacherReplySlaHours`. A misconfiguration gives stages out of order. They are still exactly-once, because of the unique key.
- `api/Elmanhg.Application/TeacherInbox/ReplyToTeacherThread/ReplyToTeacherThreadHandler.cs:32` and `api/Elmanhg.Application/TeacherInbox/SendVoiceReply/SendVoiceReplyHandler.cs:34`: the push runs after the commit. If a send throws, or the request is cancelled at that point, a reply that is already committed returns an error. The in-memory lifetime manager does not throw for users with no connection. The plan accepts this as best effort (Decision 14). Revisit if a backplane is added.
- `api/Elmanhg.Tests/Integration/Realtime/NotificationsHubTests.cs:50-59`: the end-to-end hub test authenticates through the `Authorization` header (LongPolling). The `?access_token=` path is covered only by `HubAccessTokenTests`. That is acceptable, but a WebSocket or SSE case with the query token would close the gap.
- `api/Elmanhg.Infrastructure/TeacherThreads/TeacherThreadRepository.cs:17,32`: the `Where` predicates are single very long lines. Consider extracting an `Expression` per stage.
- The lane base is `df47428`. `origin/main` (`d4cc222`) has since changed the generated OpenAPI, Orval and zod output. Merge main and regenerate before merging, per the PROGRESS.md parallel-lane convention.

## Verified
- **CI parity:** I moved `appsettings.json` aside and ran `dotnet test api/ -c Release`: 3409 total, 3409 passed, 0 failed, 0 skipped. I restored the file afterwards. `dotnet format --verify-no-changes` on the changed and new `.cs` files (migrations excluded): exit 0. The build left no drift in `api/openapi/v1.json`.
- **Web:** `tsc -b` clean; `eslint . --max-warnings=0` clean; `prettier --check` clean; `vitest run --coverage`: 180 files and 1031 tests passed, all files 95.21 / 83.24 / 90.86 / 95.38, no threshold failure.
- **promtool:** `promtool test rules` (prom/prometheus v3.14.0): SUCCESS, including the two new cases.
- **HubAccessToken security (checked by reading):**
  - `HubAccessToken.cs:12-16` sets `context.Token` only when the path `StartsWithSegments("/api/hubs/notifications")` and the query value is non-empty.
  - `RealtimeExtensions.cs:14-18` attaches it through `PostConfigure<JwtBearerOptions>("Bearer")`. Core.Identity registers `AddJwtBearer()` under that default scheme and sets no events, so nothing is overwritten.
  - The hub requires `AuthenticatedUser` (`RealtimeExtensions.cs:24`). T87 proves anonymous gets 401.
  - The token value is never logged:
    - `RequestLoggingMiddleware.cs:47` logs `RequestLogScrubber.Query`, which keeps keys only.
    - Production uses `appsettings.example.json` (`api/Dockerfile:18`), which sets `Microsoft.AspNetCore: Warning`, so the hosting "Request starting" URL line is suppressed.
    - Both Caddy loggers rewrite the query to `?redacted` (`deploy/Caddyfile:13,40`).
    - OTel AspNetCore 1.19 redacts query values by default.
    - No new code logs the token.
- **Hub routing:**
  - `SignalRTeacherThreadNotifier.cs:11` uses `Clients.User(studentId)`, and `:19` uses `Clients.Users(teacherIds)`.
  - The user id is `NameIdentifier`, which `UserClaimsExtensions.cs:14` writes as `Id.ToString("D")`, the same format as `Guid.ToString()`.
  - The hub has no client methods and no group joins.
  - Recipients are the claimer, otherwise the teachers of the subject (`TeacherThreadReminderRecipients.cs:10-19`).
  - T64 and T65 fail if another target were used. T86 proves delivery end to end.
- **Sweep:**
  - `TeacherThreadSlaWorker` is a line-for-line mirror of `SubscriptionLapseWorker`, including the deferred ids and the #81 listing and item failure accounting (checked with a diff).
  - `ProcessTeacherThreadSlaHandler.cs:16` loads the thread `asNoTracking` and saves only through the event repository (`:44`), so the thread `xmin` is never bumped and a replying teacher gets no 409.
  - Idempotency comes from `DueSlaStages.Except(recorded for this SlaDueAt)` plus the unique `(ThreadId, Kind, SlaDueAt)` index. T84 runs processing twice and gets exactly 3 rows.
  - Catch-up records every stage and pushes `reminders.Max()` once, never for a breach (T49, T48).
  - The SQL listing is algebraically the same as `DueSlaStages`.
- **Follow-up rule:**
  - `TeacherThread.FollowUps.cs:14,20-23`: allowed only on `Answered`, and after a follow-up the thread is never `Answered` again.
  - The load is owner-scoped, so anyone else gets 404 (`FollowUpTeacherThreadHandler.cs:23`).
  - `Answer` computes `HasFollowUp()` before `Messages.Add`, and closes the thread with `ClosedAt` (`TeacherThread.Replies.cs:61-69`).
- **Rating rule:**
  - The range check comes first. After that, `Open` gives 409 NOT_ANSWERED, then an existing rating gives 409 ALREADY_RATED.
  - Rating an `Answered` thread closes it. Rating a `Closed` thread keeps its `ClosedAt` (`TeacherThread.FollowUps.cs:35-59`).
  - The validator returns 422 outside 1..5, the load is owner-scoped, and the DB check constraint `CK_TeacherThreads_Rating` backs the rule.
- **Alert rule:** `AskTeacherSlaBreached` uses the documented pattern from docs/observability.md: increase over older series, OR the whole value of series born in the window. It is filtered to `elmanhg_kind="Breach"`. It fires on the first breach of a new series and stays quiet on reminders (T70, T71).
- **Existing tests:** none were weakened. The only changes are the constructor argument added to two handler tests, plus the added T37–T42, the builder extensions, the one-entry migration-list append (the accepted pattern) and `SlaSweepEnabled=false` in `ApiFactory`.
- **Contract fidelity:**
  - Every file in Files to create (1–23, W1–W18) exists with the planned signatures.
  - The migration contains only the planned DDL: no Drop or Rename in `Up`.
  - Options, resx, web errors, `appsettings.example.json`, `api.env.example`, the metric and tag, `Program.cs` registration and mapping, and the 3 endpoints with their planned policies all match the plan.
- **Tests:** every planned T1–T88 (with T69b and T69c) method and every W-T1–W-T25 test exists under the planned name (checked by grep).
- **Deviations:** all 9 listed deviations are real and justified. None is hidden. The Caddy item needs no Caddyfile change: `/api/*` is already reverse-proxied, which upgrades WebSockets by default, and query redaction is present.
- **Postman:**
  - `Get reminders` comes after `Get inbox`.
  - The new `AskTeacherFollowUp` folder has Send follow-up, then Reply to follow-up, then Rate thread, in state order, with the correct URLs, methods and bodies.
  - The JSON parses. No stale or orphaned requests.
- **Docs sync:**
  - `ask-teacher.md`, `PRD.md` (§12.1 step 3, §15, rule 11), `observability.md`, `deployment.md`, `claude-design-prompt.md`, `prototype.md` item 7 and `subscriptions.md` all agree with the code.
  - No divergence found. No design tokens changed.
- **Design system:** the new components use tokens only. They add no literal colours or px values and no red on the rating or reminders. The reminder link reuses the `InboxListItem` classes.

## Test quality
- **Domain tests** (follow-up, rating, SLA, SLA event): these constrain the state machine, codes and boundaries (11h59m vs 12h, the new window after a follow-up). The implementer’s mutation of `Answer` failed 5 of them.
- **`ProcessTeacherThreadSlaHandlerTests`:**
  - These test real behaviour against in-memory lists, not substitute echoes.
  - They assert the added rows, the save count, the exact recipients, the pushed kind (the `Max` vs `Min` mutation is caught) and the metric tag.
- **`GetTeacherInboxRemindersHandlerTests`:** these constrain the current-window filter, the latest-kind choice and the subject scoping.
- **`TeacherThreadSlaWorkerTests`:** these constrain the deferred-id sequence, the log levels and the run outcomes.
- **`HubAccessTokenTests`:** T62 fails if the path guard is removed.
- **`SignalRTeacherThreadNotifierTests`:** these constrain the target, the event name and the payload.
- **Integration tests:** these hit real Postgres and SignalR and check DB state, codes and the quota being unchanged.
- **Web tests:**
  - The listener tests swap the MSW data and assert the refetch and the toast.
  - The malformed-event tests assert that no toast appears and no refresh happens.
  - The `useRealtimeEvents` unmount test uses a spy, so it is not vacuous.
- No vacuous tests found.
