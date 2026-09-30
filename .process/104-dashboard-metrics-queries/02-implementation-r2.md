# Implementation (rework r2): [E11.S1] Dashboard metrics queries (#104)

## Blocking findings addressed

| # | What I changed | File:line |
|---|---|---|
| 1 | Added the integration test `Get_AnswerWithoutLandingInRange_IsNotACompletedJourney` on its own date, 2021-06-09. Nothing else in the suite uses 2021-06-08 or 2021-06-09. Visitor D has `LandingViewed` on 2021-06-08, outside the range, and `FirstQuizAnswered` on 2021-06-09. Visitor E answers first and lands 60 s later, both on 2021-06-09. The test asserts steps `[1,0,0,0,2]`, `completedJourneys == 0` and a null median. Existing test and its counts are unchanged. **Mutation-confirmed:** the WHERE was changed to `v."Answered" IS NOT NULL`, and the new test failed (found 2). It was then changed to `v."Landed" IS NOT NULL AND v."Answered" IS NOT NULL`, and the new test failed (found 1). The original was restored and verified. | `api/Elmanhg.Tests/Integration/Dashboard/FunnelMetricsEndpointTests.cs:11,40-56` |
| 2 | Added a `### Dashboard (api.env, docs/dashboard.md)` table to §4 before "Object storage". It lists the six keys with their defaults (`Africa/Cairo`, 60, 30, 366, 7, 30) and ranges from `DashboardOptions`. It uses the sibling "None is a secret; validated at startup" line and adds the IANA and Default ≤ Max constraints. Added a commented `Dashboard__*` block to `deploy/api.env.example`. That is the only API env example: `ai.env.example` belongs to the Python service. Values match the code defaults and `appsettings.example.json:27`. | `docs/deployment.md` (Dashboard section before Object storage); `deploy/api.env.example` (block before Object storage) |
| 3 | Added `submittedAt` to the `QuestionDecision` field list: the question's `submittedAt` at decision time, which the dashboard median uses. Added the `AddDashboardMetrics` backfill clause: taken from the decided version's `QuestionRevision.editedAt`, falling back to `decidedAt`. This matches migration lines 20-21. | `docs/question-schemas.md:182` |

## Non-blocking items also fixed (quick and safe)
| Item | Change |
|---|---|
| Cache key has no test | New `DashboardCacheKeyTests` (2 facts): the exact key has card, from, to and subject; missing filters leave empty slots. |
| PRD §10.3 wording contradicted itself | `docs/PRD.md:361` now reads "Every chart except Content is filterable by date range..." The redundant clause is removed. |
| Transaction caveat for `UserActivityBehaviour` | Added one sentence to `docs/dashboard.md` Activity tracking. |

Not done (left as they are, non-blocking):
- The call-order `Returns` in `GetStudentMetricsHandlerTests`.
- The strength of `UserActivityTrackingTests`.
- The `OperationCanceledException` filter. This would change runtime behaviour and needs its own test.
- Controller length of 102 lines.
- LF endings.

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `api/Elmanhg.Tests/Application/Features/Dashboard/Shared/DashboardCacheKeyTests.cs` | 21 | Pins the dashboard cache-key composition |

## Files modified
| Path | Change |
|---|---|
| `api/Elmanhg.Tests/Integration/Dashboard/FunnelMetricsEndpointTests.cs` | New test and `Answered` date; `using System.Text.Json` |
| `docs/deployment.md` | New Dashboard env-var section |
| `deploy/api.env.example` | New commented `Dashboard__*` block |
| `docs/question-schemas.md` | `submittedAt` and backfill on `QuestionDecision` |
| `docs/PRD.md` | §10.3 date-range wording |
| `docs/dashboard.md` | Transaction caveat in Activity tracking |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Rework mode: address only the blocking findings | The orchestrator also asked for quick, safe non-blocking fixes | Added 1 test file (`DashboardCacheKeyTests`) and 2 doc wording edits. No production code changed. |

## Build & test
The runs were done with `api/Elmanhg.Api/appsettings.json` absent (CI parity; the file does not exist in this worktree).
- `dotnet build api/ -c Release`: 0 warnings, 0 errors.
- `dotnet test api/ -c Release --no-build`: **Passed! total 3603, failed 0, succeeded 3603, skipped 0.** That is 3600 before, plus the funnel test, plus 2 cache-key tests.
- `dotnet format api/Elmanhg.slnx --verify-no-changes --include <2 changed test files>`: clean.
- No web or ai changes, and no endpoint or contract change, so OpenAPI, Orval and Postman are unaffected.

## Notes for review
- xUnit v3 on Microsoft.Testing.Platform rejects `--filter`. Targeted runs need `--filter-class "*Name"`.
