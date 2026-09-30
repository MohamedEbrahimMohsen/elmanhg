# Implementation r2 — Performance targets (#114)

## Blocking findings addressed
| # | What I changed | File:line |
|---|---|---|
| 1 | Removed `lesson_content_visible` from the k6 `thresholds`. `quiz_transition: ['p(95)<300']` and `checks: ['rate>0.99']` stay. Added `summaryTrendStats` with `p(75)`. Added `handleSummary`: it reads `lesson_content_visible` p75, and when that is >= 2000 ms (or missing) it prints `::warning::lesson_content_visible p75 <n> ms is over the 2000 ms budget (advisory until the #114 follow-up)`. It writes `/results/browser-summary.json` and a per-metric stdout summary, and it does not change the exit code. `load-test.sh` drops `--summary-export` for the browser run because `handleSummary` now writes that file. `api-load.js` and its thresholds are unchanged, so the API gate still blocks. Docs: performance §5 step 6 and the CI paragraph, and deployment §12, now say the lesson budget is advisory until the #114 follow-up. | `deploy/loadtest/lesson-page.js:14,26,71-86`; `deploy/load-test.sh:80`; `docs/performance.md:73,99`; `docs/deployment.md:390` |
| 2 | Added `RunAsync_PasswordRejectedByIdentity_ThrowsLoadTestSeedFailed`. It uses Staging, a unique key, count 1 and password `"short"`, and asserts `InternalServerErrorCoreException` with `ErrorCode == ErrorCodes.LoadTestSeedFailed`. Mutation check: with `EnsureSucceeded` disabled, the test fails with `DbUpdateException`. With the check restored, it passes. | `api/Elmanhg.Tests/Integration/Hosting/LoadTestSeedCommandTests.cs:89-96` |
| 3 | **#33:** `vi.mock('./mathRenderer')` wraps `loadMathRenderer` in `vi.fn(actual)`, and `vi.mock('./renderMath')` wraps `renderMath`. Both mocks are cleared in `beforeEach`. #33 asserts that neither was called, which holds in any test order, even after the lazy component is cached. #17 asserts `loadMathRenderer` was called. **#41:** moved to the new `AvatarDock.lazy.test.tsx`, where `vi.mock('./AvatarPanel')` renders a `data-testid="avatar-panel"` marker. The test waits with `act(() => vi.dynamicImportSettled())` and asserts the marker is absent. It then opens the panel, closes it with the marker's own button, and asserts the marker is still mounted. Mutation checks: with the `hasMath` shortcut disabled, #33 fails. With an unconditional `<Suspense><AvatarPanel/>`, #41 fails. With the panel gated on `state.isOpen`, #41 also fails, which covers stays-mounted. All three restored. | `web/src/features/content/components/RichTextViewer.test.tsx:8-21,29,49-50`; `web/src/features/avatar/components/AvatarDock.lazy.test.tsx:10-41`; `web/src/features/avatar/components/AvatarDock.test.tsx` (#41 removed) |

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `web/src/features/avatar/components/AvatarDock.lazy.test.tsx` | 42 | #41 with a mocked `AvatarPanel`; the review asked for a separate file |

## Files modified
| Path | Change |
|---|---|
| `deploy/loadtest/lesson-page.js` | The lesson budget is advisory through `handleSummary` |
| `deploy/load-test.sh` | The browser run has no `--summary-export`; `handleSummary` writes the file |
| `docs/performance.md`, `docs/deployment.md` | Document the advisory lesson budget and the follow-up |
| `api/Elmanhg.Tests/Integration/Hosting/LoadTestSeedCommandTests.cs` | Finding 2 test |
| `web/src/features/content/components/RichTextViewer.test.tsx` | Finding 3 mocks and assertions |
| `web/src/features/avatar/components/AvatarDock.test.tsx` | #41 moved out |

## Merge of origin/main (#96, #117)
- Story committed as `57fc5f3 feat(E13.S3): performance targets`, with `deploy/load-test.sh` set to +x. Merge commit: `a374824`.
- Conflicts:
  - `AppDbContextTests`: kept main's list and appended `_AddAttemptStudentCreatedAtIndex` as 31st, in timestamp order after `20260929163757_AddAvatarConversations`.
  - `deploy/api.env.example`: kept the LoadTestSeed block and main's explicit "Set by compose" line.
- `LocalFileStorageExtensions.cs` was renamed to `MediaStorageExtensions.cs` on main. The auto-merge kept the immutable `Cache-Control` on the Local static files. main's `PublicMediaMiddleware` sends the same header for S3.
- OpenAPI (`dotnet build api/`) and Orval (`npm run gen:api`) were regenerated with no drift against the merged tree.
- The k6 scripts still match the API:
  - The avatar body matches `SendAvatarMessageCommand`, including `conversationId`.
  - k6 does not call the voice routes.
  - The seed creates only MCQs, so the new `Essay` type never appears in a quiz or exam.
  - The answer request shapes are unchanged.

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| #41 lives in `AvatarDock.test.tsx` | Review finding 3 asked for a separate file, because `vi.mock` is file-scoped and the other AvatarDock test needs the real panel | Moved #41, with the same name, to `AvatarDock.lazy.test.tsx` |
| Lesson budget is a k6 threshold (Decision 25) | The orchestrator made it advisory in CI | It warns through `handleSummary`; the budget value is unchanged at p75 < 2000 ms |

## Build & test
- `dotnet build api/`: 0 warnings, 0 errors. (Plain `dotnet build` from the repo root fails with MSB1003 because the root has no solution file, so I ran it against `api/`.)
- `dotnet test api/ -c Release`, with `appsettings.json` moved aside and then restored: Passed, total 3380, failed 0.
- Web:
  - `typecheck`: clean.
  - `lint`: clean.
  - `test -- --run`: 184 files, 1056 tests passed.
  - `prettier --check --end-of-line auto .`: all files formatted.
  - `build`: "Precompressed 190 files".
  - `perf:budget`: all within budget. entry 199/210, landing 208/220, lesson 227/240, quiz 245/255 KB.
- promtool (`prom/prometheus:v3.14.0@sha256:5ce7…`): `test rules` SUCCESS, `check rules` SUCCESS with 19 rules.
- k6 smoke (project `elmanhg-load-114r2`, tag `loadtest-114r2`): exit 0, "Load test (smoke) passed".
  - `quiz_transition` p95 214 ms.
  - checks 282/282, `http_req_failed` 0%.
  - It printed `::warning::lesson_content_visible p75 2924 ms is over the 2000 ms budget (advisory until the #114 follow-up)`.
  - Stack, volumes, images and results were removed afterwards.

## Notes for review
- The follow-up issue does not exist yet. The warning and the docs refer to "the #114 follow-up", which is the orchestrator's "Follow-ups from #114" issue. The lead should add its number when the issue is created.
- `handleSummary` replaces k6's default end-of-test text. It prints its own one line per metric with no remote `jslib` import, because the runner is pinned and offline-safe.
