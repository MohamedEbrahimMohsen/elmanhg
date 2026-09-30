VERDICT: CHANGES_REQUESTED

# Review: [E13.S3] Performance targets (#114)

Scope: the whole working tree against `a68cac9`, including untracked files. Judged against the plan, `00-acceptance.md` (the autopilot budget-miss override), the orchestrator decision (the lesson budget is advisory in CI, and the API p95 gates keep blocking), `.claude/skills/dotnet-feature` and `react-feature`, the testing conventions, the PROGRESS.md Conventions, and `.claude/rules/docs-sync.md`.

## Blocking

### 1. CI fails every image build on the lesson budget, which the orchestrator ruled advisory
**Where:** `deploy/loadtest/lesson-page.js:26`, `deploy/load-test.sh:80` and `:86`, `.github/workflows/images.yml:37-42`, `.github/workflows/load-test.yml:24-27`
**Rule:** Orchestrator decision: the CI browser gate for the lesson budget prints a warning and exits 0, and the API p95 gates keep blocking.
**Problem:** `lesson_content_visible: ['p(75)<2000']` is a k6 threshold, so k6 exits 99 when the budget is missed. `load-test.sh:80` stores that as `browser_status`, and `:86` calls `fail` whenever `browser_status != 0`. The new `deploy-smoke` step in `images.yml` runs this script, so `deploy-smoke` fails and `publish` (`needs: deploy-smoke`) never runs. The manual `load-test.yml` fails the same way. The implementer says so in 02-implementation.md, Deferred #2 ("CI will be red").
**Failure:** Today's measured p75 is 2.96 s. Every push that triggers `images.yml` gets exit 1 at "Load test smoke", and no image is published.
**Fix:**
- In `lesson-page.js`, remove `lesson_content_visible` from `thresholds`. Keep `quiz_transition: ['p(95)<300']` and `checks`.
- Add `summaryTrendStats` including `p(75)`.
- Add a `handleSummary(data)` that:
  - reads `data.metrics.lesson_content_visible.values['p(75)']`;
  - when it is >= 2000, prints `::warning::lesson_content_visible p75 <n> ms is over the 2000 ms budget (advisory until #<follow-up>)`;
  - still writes `/results/browser-summary.json` and the stdout summary.
- The k6 exit code then ignores the lesson budget. `api-load.js` and its thresholds stay unchanged, so the API gate keeps blocking.
- In the same change, update the docs that currently say the run fails on any budget: `docs/performance.md` §5 step 6 (line 73) and the §5 CI paragraph (line 99), to say the lesson budget is advisory in CI until the follow-up. Also `docs/deployment.md` §12 (line 361, "It fails when a performance budget is exceeded"). Otherwise the fix creates a docs-sync divergence.

### 2. The `LOAD_TEST_SEED_FAILED` throw has no test
**Where:** `api/Elmanhg.Application/LoadTesting/SeedLoadTestData/LoadTestUsers.cs:38-44` (`EnsureSucceeded`)
**Rule:** Reviewer order #6: every `throw` in new code needs a test. dotnet-testing: every throwing path asserts the exception type and error code.
**Problem:** No test reaches the failed-`IdentityResult` path. `LoadTestSeedCommandTests` covers only the guards and the happy paths.
**Failure:** Suppose `EnsureSucceeded` is deleted, or the check is inverted. A password that Identity rejects (for example `"short"`, under `RequiredLength` 8) then makes `CreateStudentAsync` return a user that was never saved. The handler adds a `Subscription` for a non-existent `StudentId`, and the command reports success or fails later on a foreign key. The whole suite stays green.
**Fix:** Add `RunAsync_PasswordRejectedByIdentity_ThrowsLoadTestSeedFailed` to `LoadTestSeedCommandTests`. Use Staging, a unique key, count 1 and password `"short"`. Assert `InternalServerErrorCoreException` with `Code == ErrorCodes.LoadTestSeedFailed`.

### 3. Two planned tests cannot fail on the behaviour they name
**Where:** `web/src/features/avatar/components/AvatarDock.test.tsx:25-30` (#41), `web/src/features/content/components/RichTextViewer.test.tsx:27-31` (#33)
**Rule:** Reviewer order #5: would the test fail if the code were wrong? A vacuous test is BLOCKING. The implementer states this in 02-implementation.md "Notes for review".
**Problem:**
- #41 asserts `queryByRole('dialog')` is null. A closed Radix dialog renders no dialog, whether or not `AvatarPanel` is mounted.
- #33 asserts the text shows synchronously. The Suspense fallback at `RichTextViewer.tsx:24` renders the same text synchronously, whether or not the `hasMath` shortcut at `:20-22` exists.
**Failure:**
- Replace the `mounted` gate in `AvatarDock.tsx` with an unconditional `<Suspense><AvatarPanel/></Suspense>`. The AvatarPanel chunk (react-hook-form, Radix dialog) then downloads on every lesson cold load, and #41 still passes.
- Delete the `!hasMath(html)` branch in `RichTextViewer.tsx:20-22`. Every lesson then loads the KaTeX chunk, and #33 still passes.
- The bundle budget does not catch either one: both are dynamic chunks, which `pageFiles` excludes by design.
**Fix:**
- #33: mock `./mathRenderer` with `importOriginal` and wrap `loadMathRenderer` in `vi.fn(actual.loadMathRenderer)`. Assert it was not called for math-free HTML. #17 can assert it was called.
- #41: in a separate test file, `vi.mock('./AvatarPanel', ...)` with a marker component (`data-testid="avatar-panel"`). Assert the marker is absent before the first open and present after clicking the dock button. This also proves the panel stays mounted after it closes.

## Non-blocking
- `api/Elmanhg.Application/LoadTesting/SeedLoadTestData/SeedLoadTestDataHandler.cs:45-55`: each student's subscription is saved only by the next `CreateAsync` or the final `SaveChangesAsync`. A crash between the last `CreateAsync` and `:55` leaves one student without a subscription, and a rerun skips that student (the email exists). This is idempotent but does not repair. It matters only for a crashed seed on a throwaway stack.
- `api/Elmanhg.Api/Hosting/LoadTestSeedCommand.cs:40`: `[GeneratedRegex]` has no `matchTimeoutMilliseconds` (skill §8.13). The input is config, not user input, and the pattern is linear, so the deviation the implementer disclosed is acceptable. Adding a timeout argument would still be cheap.
- PROGRESS.md "CI parity" says new config keys also go into the test `ApiFactory`. `LoadTestSeed:*` was not added there. The justification holds: the running host never reads these keys, and the tests pass their own `IConfiguration`. The deviation is disclosed.
- `web/src/features/content/components/mathRenderer.ts:9-11` and `RichTextViewer.tsx:10-16`: if the `renderMath` chunk fails to load once (3G blip), both the memoised promise and `React.lazy` cache the rejection. Every math viewer then throws until a reload, and the app has no error boundary. Route chunks already carry the same risk, so this adds no new class of failure. Resetting `pending` on rejection would harden it.
- `RichTextViewer.tsx:10-16` defines a second component inline in the file (react skill §2, "one component per file"). This is minor.
- `api/Elmanhg.Tests/Integration/Hosting/LoadTestSeedCommandTests.cs` is 201 lines, against the ~100-line guideline. It is comparable to other integration classes.
- `web/scripts/perf/compress.ts:119-134` also writes `dist/.vite/manifest.json.br` and `.gz`. This is harmless: the Dockerfile removes `dist/.vite`.

## Verified
- **Build and tests, run by me.** API: with `appsettings.json` moved aside, `dotnet test api/ -c Release` passed 3178 of 3178; the file was restored. Web:
  - `npm run typecheck` is clean.
  - `npm run lint` is clean.
  - `npm test -- --run`: 173 files, 999 tests passed.
  - `npm run build` printed "Precompressed 190 files (.br and .gz)".
  - `npm run perf:budget`: entry 197/210, landing 205/220, lesson 224/240, quiz 242/255 KB, all ok.
  - Each budget equals the measured size x 1.05, rounded up to 5 KB, as Decision 12 requires.
- **promtool** (pinned `prom/prometheus:v3.14.0@sha256:5ce7...`): `test rules` SUCCESS, and `check rules` SUCCESS with 19 rules.
- **Manifest.** KaTeX appears only in the dynamic `renderMath` entry and the shared `_katex` chunk. `AvatarPanel` is a dynamic entry. Neither is in the entry, landing, lesson or quiz closure. `routeTree.gen.ts`, OpenAPI and the Orval client are unchanged.
- **Seed command.**
  - `LoadTestSeedCommand.cs:19-23` refuses Production (`IsProduction()` is case-insensitive) and returns 1 before any scope or DB work. Test #3 would catch a removed guard.
  - Key, count and password are validated before sending.
  - The password is never logged.
  - It is idempotent: the subject is found by name and each email by `FindByEmailAsync`. Test #8 checks the counts after two runs.
  - `load-test.sh` sets `ASPNETCORE_ENVIRONMENT=Staging`.
- **Index.** `IX_Attempts_StudentId_CreatedAt` on `(StudentId, CreatedAt)`. The migration holds only `CreateIndex`/`DropIndex`, and the snapshot is updated. It serves the StudentId-plus-CreatedAt-lower-bound predicates of `SessionRepository.CountQuizAttemptsOnDayAsync:46` and `GetQuizActivityDaysAsync:30`. The existing `(StudentId, QuestionId, CreatedAt)` index cannot bound that range.
- **Query-plan test.** `AttemptQueryPlanTests` runs `SET LOCAL enable_seqscan = off` inside a transaction, mirrors the Attempts part of those predicates, asserts the index name through the shared constant, and rolls back.
- **N+1 fix (#181) keeps the same results.**
  - `GetServableExamCandidatesAsync` (`QuestionRepository.cs:80-91`) joins question to lesson and filters on `lesson.UnitId`, so every candidate's `LessonId` is in `unitLessons`. The in-memory split by `unitIdByLessonId` therefore gives the same per-unit lists as the old per-unit calls, in the same `OrderBy(Id)` order.
  - The removed `DistinctBy` was redundant: a question has one lesson, and a lesson has one unit.
  - `selection.Units` is distinct (`MultiUnitExamPlanner.cs:20-29`), so `ToDictionary` cannot throw.
  - `lessons` comes from the same soft-delete-filtered set as the removed second query.
  - `ExamBreakdown.UnitPosition` returns the same first index or `int.MaxValue`. The existing multi-unit suites pass unchanged, and #9 constrains the single call.
- **KaTeX lazy loading.** `renderMath.ts` still owns the KaTeX import and its CSS. Vite's preload helper resolves the dynamic import only after its CSS loads, so formulas do not render unstyled. The fallback shows the same sanitised HTML. #17 proves KaTeX MathML is produced after the lazy load.
- **Caddy.** `file_server { precompressed br gzip }`, with `encode zstd gzip` kept for proxied API responses. Caddy sets `Content-Encoding` and `Vary: Accept-Encoding` for sidecars and does not re-encode an already encoded response. `smoke-test.sh:97-98` asserts `content-encoding: br` on an `/assets/*.js`. The existing `immutable`/`no-cache` header matchers are unchanged.
- **Bundle budgets.** They are enforced in `web-ci.yml` after `Build` (`npm run perf:budget`, exit code 1 when over). Node 24 (`.nvmrc`, Dockerfile) runs the `.ts` CLIs natively.
- **`ApiHotPathSlow`.** The recording rule is p95 by `(le, http_route)` over 15 m, limited to sessions/exams/browse routes (the regex allows a leading slash). The alert fires at > 1 s, joined `on (http_route)` with a 15 m rate > 0.05 rps, `for: 15m`, severity warning, with summary and runbook. It matches plan Decision 22 and the API-surface YAML. Promtool cases #28-30 cover fire, fast and trickle.
- **Dependencies.** k6 is pinned as `grafana/k6:2.3.0-with-browser@sha256:fd3bc5d2...44ee`, and `actions/upload-artifact` by SHA with a `# v4.6.2` comment. `load-test.yml` reuses the pinned checkout SHA with `persist-credentials: false` and `permissions: contents: read`.
- **Headers.** `servable-count` sends `Cache-Control: public, max-age=<ServableCountCacheSeconds>` (#11). Public media is `public, max-age=31536000, immutable`, set only on the `PublicMediaFileProvider` static files, so private teacher-thread media is unaffected (#12).
- **Postman.** No endpoint was added, changed or removed, only response headers, so no Postman change is needed.
- **Docs sync.** PRD §14 pointer, deployment §4/§12/§13, observability §4/§8, rich-text Rendering/Images, question-schemas servable-count bullet, README row, and the new `docs/performance.md` all agree with the code as it stands. Finding 1's fix must carry the doc edits named there.
- **Disclosed deviations checked against the code.**
  - `React.lazy` replaces `use()`.
  - The preload-only loader uses `queryClient.query`; typecheck passes.
  - #40 uses the "Assistant" label.
  - The avatar body uses `conversationId`.
  - The avatar quota is 10000.
  - Teardown uses the all-profiles `down`.
  - There are 30 migration entries.
  - `explain.sql` picks the student with the most attempts.
  - The smoke run stands in for the full run.
  - The Decision 25 override is recorded, and the budget is unchanged at p75 < 2000 ms.

## Test quality
- **`LoadTestSeedCommandTests`**: constrains the guards (Production, password, count and key rows), the seed shape, idempotency, and end-to-end usability (#8b). It is missing the Identity-failure path (finding 2).
- **`LoadTestCurriculumTests`**: constrains the shape, servability (through `ServableQuestionSpecification`), math placement and blueprints.
- **`AttemptQueryPlanTests`**: constrains the index.
- **`StartMultiUnitExamHandlerTests` #9**: the `Received(1)` with both unit ids constrains the single-query draw.
- **`ServableQuestionCountEndpointTests` #11 and `LessonImagesEndpointTests` #12**: both constrain the headers.
- **`bundleBudget` and `compress` tests**: constrain the closure, dynamic-import exclusion, the unknown-entry throw, the `<=` boundary, round-trips, the size filter, the extension filter, and the not-smaller rule.
- **`optimizeImage` tests**: constrain `fitWithin`, the capability fallback, gif, rename/type/size, the canvas size, and the not-smaller or not-webp fallback.
- **`mathRenderer`**: constrains both node shapes and the negatives.
- **`RichTextViewer`**: #17 and #18 constrain. #33 does not (finding 3).
- **`AvatarDock`**: #40 constrains the lazy panel opening. #41 does not (finding 3).
- **`LessonPage.prefetch`** #42: constrains the intent prefetch. The stalled API after hover means the heading can only come from the cache.
- **promtool #28-30**: constrain the threshold and the rate floor.
