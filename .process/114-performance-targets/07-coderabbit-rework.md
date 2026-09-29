# CodeRabbit rework — PR #219 (#114 performance targets)

Applied the 4 FIX items from `06-coderabbit-triage.md` (RC1, RC4, RC5, RC7). Nothing is committed.

| # | What I changed | File:line |
|---|---|---|
| RC1 | Linked the follow-up issue [#220](https://github.com/MohamedEbrahimMohsen/elmanhg/issues/220) (open, "Follow-ups from #114: lesson page p75 2.96s over 2s budget, CDN, full load run"). "A follow-up issue tracks it." is now "[#220](…) tracks it.". The deployment status cell now records the miss: "the lesson p75 budget is missed (2.96 s locally, advisory in CI) and tracked in [#220](…)". I also did the optional step 4 and replaced every "the #114 follow-up" with #220. | `docs/performance.md:181`, `:73`, `:99`; `docs/deployment.md:402`, `:390`; `deploy/loadtest/lesson-page.js:76`, `:82` |
| RC4 | Replaced the `BuildTargetModel` body (from `#pragma warning disable 612, 618` through `restore`) with the `BuildModel` body from `AppDbContextModelSnapshot.cs`. The attributes, class name and usings are unchanged. The file is still UTF-8 with BOM and CRLF. The migration `.cs` and the snapshot are untouched. The two pragma-to-pragma bodies now `diff` as identical (+112 lines: the `TeacherMessage` audio fields and their index, and `TeacherVoiceDraft` with its relationship). | `api/Elmanhg.Infrastructure/Migrations/20260929173706_AddAttemptStudentCreatedAtIndex.Designer.cs:23-2715` |
| RC5 | Added `[constants.BROTLI_PARAM_MODE]: constants.BROTLI_MODE_TEXT` next to `BROTLI_PARAM_QUALITY`, so `perf:budget` now measures the same brotli mode the build ships (`compress.ts`). The object is split across lines to fit prettier's width. | `web/scripts/perf/budgetCli.ts:11-17` |
| RC5 (doc) | Updated "Bundle sizes after this change" to the current `perf:budget` figures (199/208/227/245 KB). | `docs/performance.md:189` |
| RC7 | Moved the dock `className` into the module constant `dockClassName`. The `Suspense` fallback is now a non-interactive copy of the dock: `<button type="button" disabled aria-busy="true" className={dockClassName}>` with the same `Sparkles` icon and `t('dock.open')` label. No new tokens or i18n keys. | `web/src/features/avatar/components/AvatarDock.tsx:8-9`, `:34-41` |
| RC7 (test) | Added `shows a busy dock while the panel chunk loads`. A `vi.hoisted` gate holds the mocked `AvatarPanel` import. After the dock click, the test asserts that the "Assistant" button has `aria-busy="true"` and is disabled. It then releases the gate and asserts that the panel is shown and the dock is gone. This case runs first because `lazy()` caches the resolved module for the rest of the file. | `web/src/features/avatar/components/AvatarDock.lazy.test.tsx:10-19`, `:42-56` |

## Deviations

| Triage said | Reality | What I did |
|---|---|---|
| RC5: update `docs/performance.md:189` only if a page's KB figure changes | Switching to TEXT mode changes no figure. I checked by running the same script in GENERIC mode on the fresh build: both give 199/208/227/245. The doc said 197/205/224/242, so it was already stale against the current build, probably since the merge of main (a374824). | Updated the line to the figures `perf:budget` reports now. The lesson headroom is 13 KB (227/240), not the 16 KB the triage quotes. |
| RC7: add a test case | `Promise.withResolvers` is not in the web tsconfig `lib`, which failed typecheck on the first attempt | Used a hand-rolled deferred inside `vi.hoisted` instead (test-only code). |

## Build & test

- `dotnet build api/`: 0 errors, 9 warnings. The warnings were already there (for example, CS8618 in `Core.Notifications`).
- `dotnet ef migrations has-pending-model-changes` (with the CI connection string env): "No changes have been made to the model since the last migration."
- `dotnet test api/ -c Release`, run with `api/Elmanhg.Api/appsettings.json` moved to the scratchpad and restored afterwards: "Test run summary: Passed!". This includes `AppDbContextTests` and `AttemptQueryPlanTests`. I only captured the summary line, not the per-project counts.
- `npm run typecheck`: clean.
- `npm run lint`: clean (`--max-warnings=0`).
- `npm test -- --run`: Test Files 184 passed (184), Tests 1057 passed (1057).
- `npx prettier --check --end-of-line auto .`: "All matched files use Prettier code style!"
- `npm run build`: succeeded, "Precompressed 190 files (.br and .gz)."
- `npm run perf:budget`: entry 199/210, landing 208/220, lesson 227/240, quiz 245/255 KB, all ok.
- Mutation check for RC7: with `fallback={null}` put back, the new test fails ("Unable to find an accessible element with the role "button" and name "Assistant""). The fix was then restored.

## Notes for review

- The first full `npm test` run (before the typecheck fix, test code only) had 1 failure out of 1057. I did not capture which test it was. The rerun after the fix passed 1057/1057, so it looks like a load-related flake. It was not in the avatar tests, which passed 3/3 in isolation on both versions.
- `has-pending-model-changes` compares against the snapshot, not the Designer, so it was clean before RC4 too. The real RC4 check is the empty body diff, which holds.
- `docs/performance.md:56` (lazy avatar panel) is still accurate, so I left it as is. It does not describe the loading state.
- `.process/114-performance-targets/04-metrics.md` was already modified before this rework. I did not touch it.

## Main merge 2

Merged `origin/main`, which brought in #97 (SLA timers, reminders and follow-up rules, migration `20260929182733_AddTeacherThreadSlaAndRatings`). Merge commit `3dd67d9`, then fix commit `c00cb5f`. Nothing is pushed.

| Conflict | Resolution |
|---|---|
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | Kept every migration in timestamp order: `..._AddAttemptStudentCreatedAtIndex` (20260929173706, #114) is thirty-first and `..._AddTeacherThreadSlaAndRatings` (20260929182733, #97) is thirty-second. |
| `deploy/observability/prometheus/rules/elmanhg.rules.yml` | Kept both groups: `elmanhg-latency` (`ApiHotPathSlow` and its recording rule), then `elmanhg-ask-teacher` (`AskTeacherSlaBreached`). |
| `deploy/observability/prometheus/tests/elmanhg.rules.test.yml` | Kept the three `ApiHotPathSlow` cases and the two `AskTeacherSlaBreached` cases. |
| `docs/observability.md` | Kept both alert rows (`ApiHotPathSlow`, then `AskTeacherSlaBreached`). |

### Deviations

| Asked | Reality | What I did |
|---|---|---|
| Make the #114 migration's Designer include #97's model changes | #114's migration (20260929173706) runs **before** #97's (20260929182733). A Designer holds the model *after its own* migration, so #114's Designer must not contain the #97 tables. The gap was the other way round: #97's Designer lacked the #114 `IX_Attempts_StudentId_CreatedAt` index. The snapshot (auto-merged) already had both. | Replaced the pragma-to-pragma `BuildTargetModel` body of `20260929182733_AddTeacherThreadSlaAndRatings.Designer.cs` with the snapshot's `BuildModel` body (+2 lines, the index). BOM and CRLF kept. The #114 Designer is unchanged. The last migration's Designer and the snapshot now `diff` as identical. |
| Verify `perf:budget` | After the merge it failed: entry 212/210, lesson 241/240, quiz 259/255 KB. #97's `main.tsx` imports `createSignalRRealtimeClient`, which statically imported `@microsoft/signalr` into the entry chunk. | Commit `c00cb5f`: `signalRRealtimeClient.ts` now loads `@microsoft/signalr` with a dynamic import on `start()`. Handlers live in a local registry, and one hub listener per event name fans out to them, so `on()` still works before the chunk loads. `start()` and `stop()` still swallow failures. Added `signalRRealtimeClient.test.ts` (2 cases: no connection built before `start`, events routed and unsubscribed, a failed start resolves). `docs/performance.md` gets a "Realtime client on connect" bullet and the new sizes: 201/209/230/248 KB. |

### Build & test

- `dotnet build api/`: 0 errors, 9 warnings (pre-existing, for example CS8618 in `Core.Notifications`). The OpenAPI file regenerated with no drift from the merged `api/openapi/v1.json`.
- `npm --prefix web run gen:api`: no drift from the merged generated client.
- `dotnet ef migrations has-pending-model-changes` (with the CI connection string env): "No changes have been made to the model since the last migration."
- `dotnet test api/ -c Release`, with `api/Elmanhg.Api/appsettings.json` moved to the temp folder and restored afterwards: "Test run summary: Passed!", total 3486, failed 0, succeeded 3486.
- `npm ci` was needed first: the worktree's `node_modules` lacked #97's `@microsoft/signalr`, so typecheck and lint failed until then.
- `npm run typecheck`: clean. `npm run lint`: clean. `npx prettier --check --end-of-line auto .`: "All matched files use Prettier code style!"
- `npm test -- --run`: Test Files 192 passed (192), Tests 1084 passed (1084). This run was after the SignalR change. Before the change it was 191/1082.
- `npm run build`, then `npm run perf:budget`: entry 201/210, landing 209/220, lesson 230/240, quiz 248/255 KB, all ok.
- `promtool` (Docker `prom/prometheus:v3.14.0`, the pinned image): `check rules`: "SUCCESS: 20 rules found". `check config`: rules SUCCESS. `test rules`: "SUCCESS".

### Notes for review

- The SignalR change edits a #97 file on this branch. It keeps the `RealtimeClient` contract, so `useRealtimeEvents` and the fake hub are unchanged. It was not exercised against a live hub here. `NotificationsHubTests` covers the server side only.
- `docs/performance.md` was already not prettier-clean before this change (checked against `HEAD~` in the temp folder). The web prettier check does not cover `docs/`.
