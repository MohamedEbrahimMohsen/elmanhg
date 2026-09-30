# Implementation — [E13.S3] Performance targets (#114)

Branch `feature/114-performance-targets` in worktree `D:\Personal\elmanhg-wt\114`. `git merge origin/main` was a fast-forward to `a68cac9` (#92 conversation logging), so there were no conflicts and no merge commit. The story work is uncommitted.

**Lesson budget missed (autopilot override: recorded and deferred, budget kept):** `lesson_content_visible` p75 = **2.96 s** against the 2 s budget (5 cold loads, median 2.94 s, max 2.96 s; FCP median 2.12 s / p95 2.79 s; LCP median 2.78 s / p95 2.97 s). `quiz_transition` p95 = 215 ms, within its 300 ms budget. Every API budget holds. See "Deferred" below and `docs/performance.md` §8.

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/Elmanhg.Api/Hosting/LoadTestSeedCommand.cs` | 42 | `--SeedLoadTestAndExit=true` host command: refuses Production; validates key, count and password; sends the seed command |
| `api/Elmanhg.Application/LoadTesting/SeedLoadTestData/SeedLoadTestDataCommand.cs` | 5 | command record |
| `…/SeedLoadTestData/SeedLoadTestDataResult.cs` | 3 | result record |
| `…/SeedLoadTestData/SeedLoadTestDataHandler.cs` | 58 | idempotent seed: teacher, curriculum, students with a Base yearly subscription; one `SaveChangesAsync` |
| `…/SeedLoadTestData/LoadTestData.cs` | 15 | names and emails |
| `…/SeedLoadTestData/LoadTestUsers.cs` | 45 | teacher and student creation through `UserManager`; `LOAD_TEST_SEED_FAILED` |
| `…/SeedLoadTestData/LoadTestCurriculumSet.cs` | 10 | record |
| `…/SeedLoadTestData/LoadTestCurriculum.cs` | 69 | 3 units × 5 published lessons × 30 approved MCQs, plus unit blueprints |
| `…/SeedLoadTestData/LoadTestLessonContent.cs` | 39 | explanation (math in even lessons), objectives, MCQ content |
| `api/Elmanhg.Infrastructure/Migrations/20260929173706_AddAttemptStudentCreatedAtIndex.cs` (+ `.Designer.cs`) | 27 | only `CreateIndex`/`DropIndex` of `IX_Attempts_StudentId_CreatedAt` |
| `api/Elmanhg.Tests/Integration/Hosting/LoadTestSeedCommandTests.cs` | 201 | Test plan #1–8b |
| `api/Elmanhg.Tests/Application/Features/LoadTesting/SeedLoadTestData/LoadTestCurriculumTests.cs` | 57 | #13–16 |
| `api/Elmanhg.Tests/Integration/Persistence/AttemptQueryPlanTests.cs` | 40 | #10 |
| `web/scripts/perf/bundleBudget.ts` | 63 | manifest closure, budget check, report |
| `web/scripts/perf/budgetCli.ts` | 16 | `npm run perf:budget` |
| `web/scripts/perf/budgets.json` | 25 | entry 210, landing 220, lesson 240, quiz 255 KB br |
| `web/scripts/perf/compress.ts` | 52 | `.br`/`.gz` writer |
| `web/scripts/perf/compressCli.ts` | 5 | runs after `vite build` |
| `web/scripts/perf/bundleBudget.test.ts` | 61 | #19–23 |
| `web/scripts/perf/compress.test.ts` | 66 | #24–27 |
| `web/src/features/content/components/mathRenderer.ts` | 12 | `hasMath`, memoised `loadMathRenderer` |
| `web/src/features/content/components/lazyImages.ts` | 16 | `decoding=async`; `loading=lazy` after the first image |
| `web/src/features/content/api/optimizeImage.ts` | 48 | client-side WebP downscale (1600 px, q 0.8) |
| `web/src/features/content/components/mathRenderer.test.ts` | 14 | #31–32 |
| `web/src/features/content/components/RichTextViewer.test.tsx` | 32 | #17, #18, #33 |
| `web/src/features/content/api/optimizeImage.test.ts` | 89 | #34–39 |
| `web/src/features/avatar/components/AvatarDock.test.tsx` | 31 | #40–41 |
| `web/src/features/browse/pages/LessonPage.prefetch.test.tsx` | 40 | #42 |
| `deploy/load-test.sh` | 87 | load harness (knobs as planned) |
| `deploy/loadtest/docker-compose.loadtest.yml` | 15 | k6 service, `grafana/k6:2.3.0-with-browser@sha256:fd3bc5d2…44ee` (index digest from `docker buildx imagetools inspect`; `k6 version` = v2.3.0) |
| `deploy/loadtest/lib/config.js` | 63 | profiles, scenarios, thresholds (budgets = docs §2) |
| `deploy/loadtest/lib/http.js` | 52 | tagged requests, login, discover |
| `deploy/loadtest/lib/flows.js` | 76 | landing, browse, quiz, exam, avatar |
| `deploy/loadtest/api-load.js` | 40 | API load script |
| `deploy/loadtest/lesson-page.js` | 68 | throttled browser script |
| `deploy/loadtest/explain.sql` | 105 | 11 hot-query EXPLAINs |
| `.github/workflows/load-test.yml` | 32 | manual full or smoke run with an artifact |
| `docs/performance.md` | 199 | new doc (§1–§9, with the measured results) |

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.Api/Program.cs` | `#region LOAD-TEST SEED` after the migration region |
| `api/Elmanhg.Api/FileStorage/LocalFileStorageExtensions.cs` | public media `Cache-Control: public, max-age=31536000, immutable` |
| `api/Elmanhg.Api/Controllers/Questions/QuestionsController.cs` | ctor takes `IOptions<ContentOptions>`; servable-count sets `public, max-age=<ServableCountCacheSeconds>` |
| `api/Elmanhg.Api/appsettings.example.json` | `LoadTestSeed` section |
| `api/Elmanhg.Api/Resources/Messages.{ar,en}.resx` | `LOAD_TEST_SEED_FAILED` |
| `api/Elmanhg.Application/Exceptions/ErrorCodes.cs` | `LoadTestSeedFailed` |
| `api/Elmanhg.Application/Exams/StartMultiUnitExam/MultiUnitExamDraw.cs` | #181: lessons of the units once, one candidate query, split by unit in memory |
| `api/Elmanhg.Domain/Sessions/Exams/ExamBreakdown.cs` | `UnitPosition` is a `for` loop (no `ToList().IndexOf`) |
| `api/Elmanhg.Infrastructure/Data/Context/AppDbContext.cs` | `AttemptStudentCreatedAtIndex` constant and index |
| `api/Elmanhg.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | regenerated |
| `api/Elmanhg.Tests/Integration/Persistence/AppDbContextTests.cs` | appended `thirtieth => …_AddAttemptStudentCreatedAtIndex` (accepted pattern) |
| `api/Elmanhg.Tests/Application/Features/Exams/StartMultiUnitExam/StartMultiUnitExamHandlerTests.cs` | + #9 |
| `api/Elmanhg.Tests/Integration/Content/ServableQuestionCountEndpointTests.cs` | + #11 |
| `api/Elmanhg.Tests/Integration/Content/LessonImagesEndpointTests.cs` | + #12 |
| `web/vite.config.ts` | `build: { manifest: true }` |
| `web/package.json` | build runs `compressCli`; `perf:budget` script |
| `web/Dockerfile` | `npm run build && rm -rf dist/.vite` |
| `web/src/features/content/components/renderMath.ts` | KaTeX CSS import moved here (body unchanged) |
| `web/src/features/content/components/RichTextViewer.tsx` | math-only lazy path, `lazyImages` |
| `web/src/features/content/components/ImageInsertForm.tsx` | uploads `await optimizeImage(values.file)` |
| `web/src/features/avatar/components/AvatarDock.tsx` | lazy `AvatarPanel`, mounted on first open |
| `web/src/routes/student/lesson.$lessonId.tsx` | loader prefetches the lesson on intent preloads |
| `deploy/Caddyfile` | `file_server { precompressed br gzip }` |
| `deploy/lib.sh` | `set_env` moved here verbatim |
| `deploy/smoke-test.sh` | local `set_env` removed; brotli assertion on an asset |
| `deploy/api.env.example` | commented `LoadTestSeed__*` block (lead rule for new config keys) |
| `deploy/observability/prometheus/rules/elmanhg.rules.yml` | group `elmanhg-latency` (recording rule + `ApiHotPathSlow`); header mentions latency |
| `deploy/observability/prometheus/tests/elmanhg.rules.test.yml` | #28–30 |
| `.github/workflows/images.yml` | `deploy-smoke` timeout 55; load smoke step; upload-artifact `@ea165f8d…` (v4.6.2, resolved with `gh api`) |
| `.github/workflows/web-ci.yml` | `Bundle budgets` step after Build |
| `.gitignore` | `/deploy/.loadtest/`, `/deploy/.loadtest-results/` |
| `docs/PRD.md` | §14 pointer to docs/performance.md |
| `docs/deployment.md` | §4 `LoadTestSeed__*` row; §12 load-test paragraph; §13 Performance row |
| `docs/observability.md` | §4 recording-rule row; §8 `ApiHotPathSlow` row |
| `docs/rich-text.md` | Rendering (lazy KaTeX, image attributes); Images (WebP downscale, immutable media) |
| `docs/question-schemas.md` | servable-count `Cache-Control` bullet (docs-sync; not in the plan, see Deviations) |
| `README.md` | docs table row |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| `MathHtml` uses `use(loadMathRenderer())` | Under React 19's act environment (every RTL render), a component that suspends on `use()` of a module-level promise is never retried ("A component suspended inside an act scope…"), so #17 and every existing suite that renders math would hang. Checked with a probe: `React.lazy` over the same promise resumes. | `const MathHtml = lazy(() => loadMathRenderer().then(({ renderMath }) => ({ default: … })))`. `loadMathRenderer` stays memoised; once resolved, later renders are synchronous, so there is no flash on question 2+. |
| #17 asserts `findByText('F=ma')` `toBeInTheDocument()` | The found node is the KaTeX MathML `<math>` element; jest-dom rejects non-HTML elements | Assert `(await findByText('F=ma')).tagName === 'math'` (this also proves KaTeX produced MathML) |
| Loader: `void context.queryClient.prefetchQuery(…)` on every load | `prefetchQuery` is `@deprecated` in the pinned TanStack Query (lint fails). Prefetching on the direct load also made the data beat the lazy route chunk, so the existing `LessonPage.test.tsx` "shows a loading state…" failed. The plan's claim that the skeleton is "unchanged" did not hold, and that test may not be edited. | `if (preload) { context.queryClient.query(options).catch(() => undefined); }`. Hover/focus prefetch (#42) works; the cold-load parallel fetch is dropped and listed as a lever in docs §9. |
| #40: button named "Ask the assistant" (the en `dock.open` value) | en `dock.open` is "Assistant" ("Ask the assistant" is `ask.default`) | Used the key's value, "Assistant" |
| `avatarBody` = `{ entryPoint, lessonId, history: [], message }` | #92 (merged) replaced `history` with `conversationId` (+ `sessionId`, `questionId`) | `{ entryPoint: 'Lesson', lessonId, sessionId: null, questionId: null, conversationId: null, message }` |
| `api.env`: `Subscriptions__BaseDailyAvatarMessages=100000` | `SubscriptionsOptions` validates `[Range(1, 10000)]`; the API refused to start | 10000 |
| cleanup: `down -v --remove-orphans` through `load_compose` | Passing a `--profile` flag replaces `COMPOSE_PROFILES=ai`, so the `ai` container and the network survived teardown (seen in the first run) | `load_compose --profile '*' down -v --remove-orphans` |
| AppDbContextTests: `twentyNinth` entry, "29 entries" | #92 already added the 29th (`_AddAvatarConversations`) | appended `thirtieth`; the list has 30 entries |
| explain.sql `student_id` = student 001 | VU 1 may run the landing flow, so student 001 can have no session, and `\gset` on zero rows aborts the script | the load-test student with the most attempts (ties by email); `session_id` falls back to a zero uuid |
| Landing flow has no think time | Without one, the landing VU loops at full speed | `think(1, 3)` after the three requests |
| `GeneratedRegex` (the plan doesn't say; the skill §8.13 asks for bounded regex) | The source generator does not support `NonBacktracking` | plain `[GeneratedRegex("^[a-z0-9-]{1,20}$")]`: linear pattern, config input, not user input |
| New config keys also go in the test `ApiFactory` (lead rule) | `LoadTestSeed:*` is never read by the running host; the command reads the `IConfiguration` passed in, and the tests pass their own | Not added to `ApiFactory` (not in the plan's touched list). Added to appsettings.example.json, the code defaults, docs/deployment.md §4 and a commented block in deploy/api.env.example. |
| Docs list | the servable-count `Cache-Control` changes behaviour that docs/question-schemas.md describes | added one bullet there (docs-sync rule) |
| DoD: a local `LOAD_PROFILE=full` run recorded in docs §8 | Lead instruction: run the smoke profile only | docs §8 records the smoke run, the §6 EXPLAIN table is from the smoke data (tiny, so all seq scans); the full run is deferred to the manual `load-test` workflow |
| Decision 25: stop with `BLOCKED: lesson budget` | Autopilot override (00-acceptance.md): record, keep the budget, defer | Recorded here and in docs §8; the budget is unchanged at p75 < 2000 ms |

## Deferred (needs follow-up issues)
1. **Lesson page budget missed: p75 2.96 s vs 2 s** (local smoke, plain HTTP; FCP median 2.12 s). Where the time goes: the entry chunk `index-*.js` is 146 KB br, and 175 KB gz over plain HTTP because Chromium offers brotli only on HTTPS, so about 0.9 s at 200 KB/s. About 200 KB of Arabic woff2 fonts (6 weights) compete for the bandwidth. Then come the session refresh and lesson round trips. Levers: split the 584 KB entry chunk; fewer font weights or `font-display: optional`; start the lesson fetch during the session restore on a cold load; measure over HTTPS/HTTP2 as production serves.
2. **CI will be red.** The new `images.yml` step "Load test smoke" fails while the lesson budget is missed. The orchestrator must choose: land the follow-up first, or mark the browser gate advisory until then. I did not weaken the gate.
3. The full 50-student profile has not been run (lead: smoke only). Run the `load-test` workflow and add its numbers and EXPLAIN table to docs §6/§8.
4. #181 partial: the first and second bullets are fixed; the 3 web and doc nits stay open. The PR body should name #181.

## Build & test
- `git fetch origin main` + `git merge origin/main`: fast-forward to `a68cac9`, no conflicts.
- `dotnet build api/Elmanhg.slnx`: Build succeeded, 0 errors. The only warnings are 9 pre-existing ones in `core-libraries`. `api/openapi/v1.json` is unchanged.
- `dotnet ef migrations has-pending-model-changes`: "No changes have been made to the model since the last migration."
- `dotnet test api/ -c Release` with `api/Elmanhg.Api/appsettings.json` moved aside (then restored): **Passed! total 3178, failed 0, succeeded 3178, skipped 0.** 19 new cases: seed 11 (the #5 theory has 3 rows), curriculum 4, query plan 1, #9, #11, #12.
- `dotnet format --verify-no-changes`: the only finding is pre-existing whitespace in `Elmanhg.Tests/Builders/SubscriptionBuilder.cs` (not touched).
- web: `npm run typecheck` clean; `npm run lint` clean; `npx prettier --check . --end-of-line auto` "All matched files use Prettier code style!"; `npm test -- --run` **173 files, 999 tests passed** (23 new). One full run under Docker load had `UnitPanel.test.tsx` time out at 3 s; it passed alone and in the final quiet full run. `npm run build`: "Precompressed 190 files (.br and .gz)"; `npm run perf:budget`: entry 197/210, landing 205/220, lesson 224/240, quiz 242/255 KB ok. `routeTree.gen.ts` and the generated client are unchanged.
- ai: not touched.
- promtool (pinned `prom/prometheus:v3.14.0@sha256:5ce7…`): `test rules` SUCCESS (all cases incl. #28–30); `check rules` SUCCESS, 19 rules.
- k6 smoke (`bash deploy/load-test.sh`, `LOAD_IMAGE_TAG=lt114`, project `elmanhg-load-114`, ports 8198/8553, subnet 172.30.214.0/24): seed "15 lessons, 450 questions, 60 new students". API: all 15 budgets met, 270 requests, 0 failed, checks 100 % (p95 ms: servable_count 5.7, landing_shell 1.6, plans 3.5, browse_subject 23.5, browse_unit 13.5, browse_lesson 28.4, quiz_start 357.3, quiz_answer 28.2, quiz_finish 19.9, exam_start 42.7, exam_get 67.4, exam_save 20.9, exam_submit 94.2, multi_exam_start 340.9, avatar_message 384.2). Browser: `quiz_transition` p95 214.6 ms ✓; `lesson_content_visible` p75 2.96 s ✗. `explain.txt` produced. Exit 1 because of the lesson budget only. A first attempt died at API start-up (the avatar quota range) and was killed and cleaned by hand.
- `bash deploy/smoke-test.sh` (`SMOKE_SKIP_BUILD=1 SMOKE_IMAGE_TAG=lt114 SMOKE_OBSERVABILITY=0`, project `elmanhg-smoke-114`, ports 8188/8543): "Smoke test passed", including the new brotli assertion and the Caddyfile validation.
- Mutation checks (each test failed against the mutation, then the code was restored): migration without the index → #10; old per-unit draw → #9; no Cache-Control lines → #11 and #12; Production guard disabled → #3; loader prefetch disabled → #42; `index >= 0` in lazyImages → #18; no size check in optimizeImage → #39; `<` instead of `<=` → #23; no size filter in compress → #27; `hasMath` inline only → #31; rule `> 5` → promtool #28; rate floor `> 0` → promtool #30 (after rewriting #30 with 1-minute samples, because the first version could not fail).
- Teardown: no `*114*` containers, networks or volumes remain; the `local/elmanhg-*:lt114` images were removed. `deploy/.loadtest-results/` (gitignored) is kept with the run's summaries.

## Notes for review
- `LoadTestUsers` calls `UserManager.CreateAsync`, which saves the shared `AppDbContext` itself. So the curriculum entities added before the first student are flushed by the first `CreateAsync`, not by the handler's single `SaveChangesAsync`. The results are the same and it is idempotent, but a reviewer may flag the pattern (`SeedAdminHandler` has the same shape).
- #41 ("renders no panel before first open") cannot fail if lazy mounting were removed: the closed radix dialog renders nothing either way. #33 cannot fail on a suspending viewer either, because the fallback shows the same text synchronously. Both are written as planned.
- `LoadTestSeedCommandTests.cs` is 201 lines (the plan put 9 tests in one class); other integration test classes in the repo are similar.
- `deploy/load-test.sh` is new and untracked on Windows: stage it with `git add --chmod=+x deploy/load-test.sh`.
- The EXPLAIN table in docs §6 comes from smoke-sized data (all seq scans; expected at 60–450 rows); `AttemptQueryPlanTests` is what proves the new index is used.
- KaTeX is now only in the dynamic `renderMath` chunk and the `_katex` chunk shared with `RichTextEditor`. `AvatarPanel`, react-hook-form (`Form`) and the radix bundle (`dist`) are absent from the `lesson` closure (checked from the manifest).
