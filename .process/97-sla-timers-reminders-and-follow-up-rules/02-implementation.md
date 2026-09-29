# Implementation — [E9.S4] SLA timers, reminders and follow-up rules (#97)

Worktree `D:\Personal\elmanhg-wt\97`, branch `feature/97-sla-timers-reminders-and-follow-up-rules`. Nothing committed.

## Files created
| Path | Lines | Purpose |
|---|---|---|
| api/Elmanhg.Domain/TeacherThreads/TeacherThreadSlaEventKind.cs | 3 | `FirstReminder, SecondReminder, Breach` |
| api/Elmanhg.Domain/TeacherThreads/TeacherThreadSlaEvent.cs | 26 | append-only SLA event row, `Record(...)` |
| api/Elmanhg.Domain/TeacherThreads/ITeacherThreadSlaEventRepository.cs | 5 | repo port |
| api/Elmanhg.Domain/TeacherThreads/TeacherThread.FollowUps.cs | 60 | `MinRating/MaxRating`, `HasFollowUp`, `CanFollowUp`, `CanBeRated`, `FollowUp`, `Rate` |
| api/Elmanhg.Domain/TeacherThreads/TeacherThread.Sla.cs | 31 | `DueSlaStages` |
| api/Elmanhg.Application/Shared/Realtime/ITeacherThreadNotifier.cs | 9 | realtime port |
| api/Elmanhg.Application/TeacherInbox/Shared/TeacherInboxReminderResult.cs | 5 | reminder result |
| api/Elmanhg.Application/TeacherThreads/FollowUpTeacherThread/{Command,Validator,Handler}.cs | 6/20/32 | follow-up slice |
| api/Elmanhg.Application/TeacherThreads/RateTeacherThread/{Command,Validator,Handler}.cs | 6/14/30 | rating slice |
| api/Elmanhg.Application/TeacherThreads/GetDueSlaThreadIds/{Query,Handler}.cs | 5/16 | sweep listing |
| api/Elmanhg.Application/TeacherThreads/ProcessTeacherThreadSla/{Command,Handler}.cs, TeacherThreadReminderRecipients.cs | 5/72/21 | per-thread SLA processing |
| api/Elmanhg.Application/TeacherInbox/GetTeacherInboxReminders/{Query,Handler}.cs | 6/50 | reminders card query |
| api/Elmanhg.Infrastructure/TeacherThreads/TeacherThreadSlaEventRepository.cs | 7 | repo |
| api/Elmanhg.Infrastructure/Migrations/20260929182733_AddTeacherThreadSlaAndRatings.cs (+ Designer) | 101 / 2783 | migration: 2 columns, check constraint, `(Status, SlaDueAt)` index, `TeacherThreadSlaEvents` table with unique `(ThreadId, Kind, SlaDueAt)` + `TeacherId` index. No Drop/Rename in `Up`. |
| api/Elmanhg.Api/Realtime/NotificationsHub.cs, RealtimeEvents.cs, TeacherReplyReceivedMessage.cs, TeacherThreadReminderMessage.cs, SignalRTeacherThreadNotifier.cs, HubAccessToken.cs, RealtimeExtensions.cs | 8/7/3/5/21/20/27 | SignalR hub, notifier, `access_token` on hub path only, DI + mapping |
| api/Elmanhg.Api/Workers/TeacherThreadSlaWorker.cs | 89 | `ask-teacher-sla` worker, mirror of `SubscriptionLapseWorker` |
| api/Elmanhg.Api/Controllers/TeacherThreads/Requests.cs | 5 | request records |
| api/Elmanhg.Tests/Domain/TeacherThreads/TeacherThread{FollowUp,Rating,Sla,SlaEvent}Tests.cs | 110/90/65/23 | T1–T22 |
| api/Elmanhg.Tests/Application/Features/TeacherThreads/{FollowUpTeacherThread,RateTeacherThread,GetDueSlaThreadIds,ProcessTeacherThreadSla,Shared}/*Tests.cs | 92+39/88+30/29/175/31 | T23–T36, T43–T52 |
| api/Elmanhg.Tests/Application/Features/TeacherInbox/GetTeacherInboxReminders/GetTeacherInboxRemindersHandlerTests.cs | 92 | T53–T55 |
| api/Elmanhg.Tests/Api/Workers/TeacherThreadSlaWorkerTests.cs | 144 | T56–T60 |
| api/Elmanhg.Tests/Api/Realtime/{HubAccessToken,SignalRTeacherThreadNotifier}Tests.cs | 48/42 | T61–T65 |
| api/Elmanhg.Tests/Integration/TeacherThreads/{FollowUpTeacherThreadEndpoint,RateTeacherThreadEndpoint,TeacherThreadSlaSweep}Tests.cs | 114/109/69 | T66–T69c, T72–T76, T84–T85 |
| api/Elmanhg.Tests/Integration/TeacherInbox/{FinalReplyEndpoint,TeacherInboxRemindersEndpoint}Tests.cs | 55/92 | T77–T83 |
| api/Elmanhg.Tests/Integration/Realtime/NotificationsHubTests.cs | 61 | T86–T87 (real HubConnection, LongPolling over TestServer) |
| web/src/shared/realtime/{realtimeClient,signalRRealtimeClient,RealtimeContext,useRealtimeEvents,realtimeEvents}.ts | 17/24/4/28/8 | W1–W5 |
| web/src/test/fakeRealtimeHub.ts | 39 | W6 |
| web/src/features/askTeacher/hooks/{useStudentRealtime,useTeacherRealtime,useFollowUpThread,useRateThread}.ts | 26/26/34/35 | W7, W8, W12, W13 |
| web/src/features/askTeacher/components/{StudentRealtimeListener,TeacherRealtimeListener,FollowUpForm,RatingPanel,ThreadRating,StudentThreadActions,InboxReminders}.tsx | 6/6/42/42/28/16/56 | W9, W10, W14–W18 |
| web/src/features/askTeacher/schemas/followUpFormSchema.ts | 9 | W11 |
| web tests: followUpFormSchema.test.ts, TeacherThreadPage.followUp.test.tsx, InboxThreadPage.rating.test.tsx, TeacherInboxPage.reminders.test.tsx, StudentRealtimeListener.test.tsx, TeacherRealtimeListener.test.tsx, shared/realtime/useRealtimeEvents.test.tsx | 14/173/55/97/71/70/54 | W-T1–W-T25 |
| web/src/shared/api/generated/** (4 new model files) | — | Orval |

## Files modified
| Path | Change |
|---|---|
| Domain `TeacherThread.cs`, `TeacherThread.Replies.cs`, `ITeacherThreadRepository.cs`, `SharedKernel/Exceptions/ErrorCodes.cs` | `ClosedAt`, `Rating`; final-reply closing in `Answer` (WHY comment); 2 repo methods; 3 codes |
| Application `Exceptions/ErrorCodes.cs`, `AskTeacherOptions.cs`, `ElmanhgMetrics.cs`, `TeacherThreadResult(+Generator)`, `TeacherInboxThreadResult`, `TeacherInboxResultGenerator`, `ReplyToTeacherThreadHandler`, `SendVoiceReplyHandler` | as in plan; notifier called after `SaveChangesAsync` |
| Infrastructure `AppDbContext.cs`, `TeacherThreadRepository.cs`, `DependencyInjection.cs`, `Migrations/AppDbContextModelSnapshot.cs` | as in plan |
| Api `Program.cs`, `TeacherThreadsController.cs`, `TeacherInboxController.cs`, `Messages.{ar,en}.resx`, `appsettings.example.json` | worker + realtime registration and `MapElmanhgRealtime()` after `MapControllers()`; 3 actions; 4 keys; 6 keys |
| `api/openapi/v1.json` | regenerated by `dotnet build` |
| `api/Directory.Packages.props`, `Elmanhg.Tests.csproj` | `Microsoft.AspNetCore.SignalR.Client` 10.0.5 (exists on nuget, verified with `dotnet package search --exact-match`; matches `Mvc.Testing` 10.0.5) |
| Tests: `ApiFactory.cs`, `AppDbContextTests.cs`, `TeacherThreadBuilder.cs`, `ReplyToTeacherThreadHandlerTests.cs`, `SendVoiceReplyHandlerTests.cs`, `TeacherInboxResultGeneratorTests.cs`, `ElmanhgMetricsTests.cs` | `SlaSweepEnabled=false`; 31st migration; `FollowedUp/FinalReplied/Rated`; `_notifier` + T37–T42 |
| `web/package.json`, `package-lock.json` | `@microsoft/signalr` 10.0.11 exact (MIT, verified with `npm view`; `npm audit --omit=dev`: 0 vulnerabilities) |
| `web/vite.config.ts`, `app/providers.tsx`, `main.tsx`, `routes/student/route.tsx`, `routes/teacher/route.tsx`, `features/askTeacher/index.ts`, pages `TeacherThreadPage`, `InboxThreadPage`, `TeacherInboxPage` | as in plan |
| `web/src/features/askTeacher/i18n/{ar,en}.json`, `web/src/shared/i18n/{ar,en}.json` | plan strings; 4 error codes (same text as resx) |
| `web/src/test/{askTeacherFixtures,teacherInboxFixtures,msw/server,renderWithProviders}.ts(x)` | as in plan |
| `web/src/shared/api/generated/**` | regenerated with `npm --prefix web run gen:api` (stable on re-run) |
| `deploy/observability/prometheus/rules/elmanhg.rules.yml`, `tests/elmanhg.rules.test.yml`, `deploy/api.env.example` | `elmanhg-ask-teacher` group with `AskTeacherSlaBreached`; 2 promtool cases; 6 keys |
| `postman/elmanhg.postman_collection.json` | `Get reminders` after `Get inbox`; new folder `AskTeacherFollowUp` after `TeacherInbox` (Send follow-up → Reply to follow-up → Rate thread). Inserted textually, so the hand formatting of the rest of the file is untouched (+175 lines, no deletions). |
| `docs/ask-teacher.md`, `PRD.md`, `observability.md`, `deployment.md`, `claude-design-prompt.md`, `prototype.md`, `subscriptions.md` | as in plan's Docs table |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| `<ThreadRating rating={data.rating} />` | .NET 10 OpenAPI types `int?` as `number \| string \| null` in Orval | `rating={Number(data.rating)}`, the pattern the feature already uses for ints (`Number(data.totalPages)`) |
| `answeredThread()` without parameters | tests need Closed / rated variants | `answeredThread(overrides?)`, default output exactly as specified |
| W-T12 `inboxThread({ status: 'Closed', rating: 5 })` | `inboxThread()` defaults to `canClaim: true`, so the page shows «Claim question», not «This question is closed.» | used `{ ...answeredInboxThread(), status: 'Closed', rating: 5 }` (claimed by the caller, `canReply: false`) |
| W-T21 / W-T23 "no toast text" | sonner keeps toasts in a module-level store, so the toast of the previous test in the same file is still rendered | assert the toast count is unchanged and that no refresh happened («New reply» / «Reminders» absent after the list was switched) — stronger than the plan's assertion |
| W-T17 "no Reminders heading" | asserting absence alone passes while the query is still pending | also wait until the reminders query state is `error` before asserting |
| W-T25 "assert through the new probe text" | a fresh probe shows "0" whether or not the old handler was detached (vacuous) | the unmounted probe also gets an `onEvent` spy that must not be called; the new probe's "0" is kept |
| T53 "result Kind is SecondReminder … ignoring an older window's event" | one thread cannot show both properties | two threads: one with First+Second in its window → `SecondReminder`; one followed-up thread with `SecondReminder` in its old window and `FirstReminder` in the current → `FirstReminder` |
| T28 / T34 / T35 names | — | T28 is a `[Theory]` (null, whitespace) under the planned name; T34/T35 `[Theory]` as planned |
| Caddy WebSocket proxy (acceptance condition) | Caddy's `reverse_proxy` upgrades WebSockets by default; `/api/*` already routes to the API; both Caddy loggers already rewrite `?.*` to `?redacted` | no `Caddyfile` change (not in the plan's file list); documented in `docs/deployment.md` |

## Build & test
- `dotnet build api/Elmanhg.slnx` — Build succeeded (only the pre-existing core-libraries nullable warnings). `api/openapi/v1.json` regenerated (+277/−2).
- `dotnet format api/Elmanhg.slnx --verify-no-changes --include <77 changed .cs files>` — exit 0.
- **CI parity:** `api/Elmanhg.Api/appsettings.json` moved aside, `dotnet test api/ -c Release` → `total: 3409, failed: 0, succeeded: 3409, skipped: 0`, exit 0; file restored afterwards.
- New .NET tests: 81 methods in new files + 6 in modified files (T37–T42) + the `AppDbContextTests` entry (T88). Every T1–T88 (with T69b, T69c) exists under the planned class/method names.
- `npm ci` then `npm install --save-exact @microsoft/signalr@10.0.11`; `npm run gen:api` (re-run: identical diff hash).
- `npx tsc -b` — clean. `npx eslint . --max-warnings=0` — clean. `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` — "All matched files use Prettier code style!" (also `vite.config.ts`).
- `npx vitest run --coverage` — `Test Files 180 passed (180)`, `Tests 1031 passed (1031)`, no threshold failure. 25 new `it(...)` (W-T1–W-T25).
- `npm run build` — exit 0.
- promtool: `docker run prom/prometheus:v3.14.0 promtool test rules …/elmanhg.rules.test.yml` — `SUCCESS`.
- Mutation checks (each broke the code, saw the tests fail, restored):
  - `Answer` always `Answered` → 5 domain tests failed (T3, T6, T7, T11, T15).
  - notify `reminders.Min()` instead of `Max()` → T49 failed.
  - repository query without the recorded-First check → T85 failed.
  - `StudentThreadActions` never shows the rating → 4 web tests failed.
  - promtool: breach filter widened to any kind → "ask-teacher reminders alone stay quiet" failed.
  - `HubAccessToken` path check (security code): verified by reading instead of mutating — T62 sends `?access_token=abc` to `/api/teacher-threads` and asserts `Token` stays null, which fails if the path condition is removed.
- `ai/` untouched, so pytest was not run.

## Notes for review
- `TeacherThreadRepository.GetSlaDueIdsAsync`'s `Where` is one long line (one operator per line, as the plan asked).
- The notifier runs after `SaveChangesAsync` inside the request. The in-memory SignalR lifetime manager does not throw for users with no connections, but a throwing send would turn a committed reply into a 500. The plan accepts best-effort; no try/catch was added (handlers must not catch).
- `NotificationsHubTests` authenticates through the `Authorization` header (the .NET client uses it for LongPolling), so the `access_token` path is covered by the `HubAccessToken` unit tests, not end to end.
- `GetTeacherInboxRemindersHandler` uses `Max` over the current-window events; the repository query guarantees at least one non-breach event for that window, so `Max` never sees an empty set.
- `TeacherInboxController.cs` is now about 110 lines (one more action, as planned).
- New `.cs` files are LF in the working tree, and `TeacherThread.Replies.cs` / `TeacherThreadRepository.cs` became LF after the mutation `sed`; git normalises them (autocrlf), and `dotnet format` is clean.
- Product questions (Out a–d) are not coded: no refund, no reassignment, no WhatsApp/email reminders, no SLA pause.
