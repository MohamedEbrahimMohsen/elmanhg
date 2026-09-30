# Implementation r2 — SLA timers, reminders and follow-up rules (#97)

Round 2 addresses the two non-blocking review notes the orchestrator promoted, commits the story, and merges `origin/main`.

## Promoted findings
| # | Finding (03-review.md, Non-blocking) | What I changed | File:line |
|---|---|---|---|
| 1 | A push that throws after the commit turns a saved reply into an error | `SignalRTeacherThreadNotifier` now sends through `SendBestEffortAsync`. It catches with `when (!cancellationToken.IsCancellationRequested)` and logs a Warning with the event name and thread id only (no user ids and no text). When the request is aborted, the `OperationCanceledException` is not logged and propagates. This covers text replies, voice replies and the sweep's reminder push. Follow-up and rating send no push. | `api/Elmanhg.Api/Realtime/SignalRTeacherThreadNotifier.cs:7-31` |
| 1 | Tests | `NotifyReplyAsync_SendThrows_SwallowsAndLogsWarning`, `NotifyReminderAsync_SendThrows_SwallowsAndLogsWarning` and `NotifyReplyAsync_RequestAborted_RethrowsWithoutLogging`. Handler-level tests `Handle_PushFails_StillReturnsSavedReply` and `Handle_PushFails_StillReturnsSavedVoiceReply` use the real notifier over a throwing hub. They assert the success result, `Answered` and exactly one save. | `api/Elmanhg.Tests/Api/Realtime/SignalRTeacherThreadNotifierTests.cs`, `.../ReplyToTeacherThread/ReplyToTeacherThreadHandlerTests.cs`, `.../SendVoiceReply/SendVoiceReplyHandlerTests.cs` |
| 2 | No ordering check between the reminder hours and the window | New `AskTeacherOptionsValidator : IValidateOptions<AskTeacherOptions>` requires `FirstReminderAfterHours < SecondReminderAfterHours < Subscriptions:AskTeacherReplySlaHours`. It is registered next to the `AskTeacherOptions` binding, which already uses `ValidateOnStart`. | `api/Elmanhg.Application/Shared/Options/AskTeacherOptionsValidator.cs`, `api/Elmanhg.Application/DependencyInjection.cs:40` |
| 2 | Tests | `AskTeacherOptionsValidatorTests` has `Validate_DefaultStages_Succeeds` and `Validate_StagesOutOfOrder_FailsNamingTheRule`, which covers 4 cases: first > second, first = second, second = window and second > window. | `api/Elmanhg.Tests/Application/Shared/Options/AskTeacherOptionsValidatorTests.cs` |

## Merge of origin/main
- Story committed as `f104adc feat(E9.S4): SLA timers, reminders and follow-up rules`.
- `origin/main` brought only `d4cc222 [E14.S1] Essay question authoring (#218)`. #96 was already in this lane's base (`df47428`). #117 is #218. #114 is not on main yet.
- **Conflict:** only `docs/claude-design-prompt.md` §4 Teacher conflicted. I kept both sides: main's essay rubric sentence on the validation-queue line, and this lane's reminders and rating text on the inbox line.
- **OpenAPI and Orval:** regenerated with `dotnet build api/` and `npm --prefix web run gen:api`. The output was identical to the auto-merged files, so nothing was hand-merged.
- **Migrations:** main added none. The `AppDbContextTests` list and the snapshot are unchanged. `20260929182733_AddTeacherThreadSlaAndRatings` is still last.
- **Voice reply and the SLA:** `ReplyWithVoice` goes through the same `Answer` path as text replies. That path sets `Answered`, or `Closed` after a follow-up. `DueSlaStages` and the sweep listing only consider `Open` threads, so a voice reply closes the window. Two domain tests pin this:
  - `TeacherThreadSlaTests.DueSlaStages_VoiceReply_ClosesWindow`
  - `TeacherThreadSlaTests.DueSlaStages_VoiceReplyToFollowUp_ClosesThreadAndWindow`

## Files created
| Path | Purpose |
|---|---|
| `api/Elmanhg.Application/Shared/Options/AskTeacherOptionsValidator.cs` | Reminder ordering check |
| `api/Elmanhg.Tests/Application/Shared/Options/AskTeacherOptionsValidatorTests.cs` | Its tests |

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.Api/Realtime/SignalRTeacherThreadNotifier.cs` | Best-effort send with a logger |
| `api/Elmanhg.Application/DependencyInjection.cs` | Registers the validator |
| `api/Elmanhg.Tests/Api/Realtime/SignalRTeacherThreadNotifierTests.cs` | Logger ctor arg and 3 failure tests |
| `api/Elmanhg.Tests/Application/Features/TeacherInbox/ReplyToTeacherThread/ReplyToTeacherThreadHandlerTests.cs` | Push-fails test |
| `api/Elmanhg.Tests/Application/Features/TeacherInbox/SendVoiceReply/SendVoiceReplyHandlerTests.cs` | Push-fails test |
| `api/Elmanhg.Tests/Domain/TeacherThreads/TeacherThreadSlaTests.cs` | 2 voice-reply SLA tests |
| `docs/ask-teacher.md` | States that a failed push is logged and never fails the saved reply, and states the ordering rule |
| `docs/deployment.md` | Reminder-hours row gains the startup ordering rule |
| `docs/claude-design-prompt.md` | Merge resolution (both sides kept) |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Catch the push failure, with no `try`/`catch` in handlers (non-negotiable) | Catching in the handlers would break the rule | I put the catch in the Api-layer `SignalRTeacherThreadNotifier`, so every caller is covered, including the SLA sweep. The handler-level proof uses the real notifier over a throwing `IHubContext` rather than a throwing `ITeacherThreadNotifier` substitute. |

## Build & test
- `dotnet build api/`: Build succeeded, 0 Warning(s), 0 Error(s).
- `dotnet test api/ -c Release`, with `appsettings.json` moved aside and then restored: Test run summary: Passed! total 3466, failed 0, succeeded 3466, skipped 0.
- `npm --prefix web run typecheck`: clean. `npm --prefix web run lint`: clean, with `--max-warnings=0`.
- `npm --prefix web test -- --run`: Test Files 183 passed (183), Tests 1058 passed (1058).
- `npx prettier --check --end-of-line auto .` (web): All matched files use Prettier code style!
- `promtool test rules tests/elmanhg.rules.test.yml` (docker `prom/prometheus:v3.14.0`): SUCCESS.

## Notes for review
- `SendBestEffortAsync` has one invariant comment, which explains why the catch exists.
- The sweep's reminder push is now best effort as well. If a push fails, the item still counts as succeeded because the SLA rows are saved. The failure is only logged at Warning.
