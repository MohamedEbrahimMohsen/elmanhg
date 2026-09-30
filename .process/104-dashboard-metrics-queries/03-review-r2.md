VERDICT: APPROVED

# Review r2 — [E11.S1] Dashboard metrics queries (#104)

Re-review of the round-1 blocking findings against `02-implementation-r2.md` in worktree `D:/Personal/elmanhg-wt/104`. Files changed since round 1 (by mtime): `FunnelEventRepository.cs` (mutation run, restored), `FunnelMetricsEndpointTests.cs`, `DashboardCacheKeyTests.cs`, `docs/dashboard.md`, `docs/deployment.md`, `docs/PRD.md`, `docs/question-schemas.md`, `deploy/api.env.example`. No other production code touched.

## Blocking
None.

## Round-1 findings
- **#1 fixed.** `api/Elmanhg.Tests/Integration/Dashboard/FunnelMetricsEndpointTests.cs:38-54` adds `Get_AnswerWithoutLandingInRange_IsNotACompletedJourney` on 2021-06-09. No other test uses 2021-06-08/09 (grep). Visitor D lands out of range and answers in range; visitor E answers before landing. The expected steps `[1,0,0,0,2]`, `completedJourneys == 0` and null median are correct. Under `"Answered" IS NOT NULL` D and E would both count (2), and under `Landed IS NOT NULL AND Answered IS NOT NULL` E would count (1), so the test kills both mutants. The predicate at `FunnelEventRepository.cs:36` is the original `v."Landed" IS NOT NULL AND v."Answered" >= v."Landed"`. The first test is unchanged.
- **#2 fixed.** `docs/deployment.md` §4 has a new "Dashboard" table with the six keys. Defaults and ranges match `DashboardOptions.cs` and `appsettings.example.json:27`. The startup-validation note matches `Application/DependencyInjection.cs:47-50` (IANA check, Default ≤ Max). `deploy/api.env.example` has the commented `Dashboard__*` block with the same values.
- **#3 fixed.** `docs/question-schemas.md:182` now lists `submittedAt` and the `AddDashboardMetrics` backfill (revision `editedAt`, fallback `decidedAt`), consistent with the migration.

## r2 extras checked for divergence
- `docs/PRD.md` §10.3: the new wording matches the query contracts. `GetContentMetricsQuery` takes a subject but no range. Solve rate, Success rate, Validation and Ask a Teacher take both. Students, Subscribers, Payments and Funnel take a range only. No divergence.
- `docs/dashboard.md:111`: the transaction caveat is accurate. It is documentation only.
- `DashboardCacheKeyTests`: pins the exact key format from `DashboardCacheKey.cs:7` (N-format guid, yyyy-MM-dd, empty slots). Dropping any component fails it.

## Non-blocking
- The r1 items left open by the implementer are still valid and still non-blocking: call-order `Returns` in `GetStudentMetricsHandlerTests`, the weak `UserActivityTrackingTests`, the `OperationCanceledException` catch, the 102-line controller and LF endings.

## Verified
- `dotnet test api/ -c Release` re-run by me with `api/Elmanhg.Api/appsettings.json` absent: **Passed, total 3603, failed 0, skipped 0.** This matches the claimed 3600 + 1 + 2.
- No contract change in r2, so Postman, OpenAPI and Orval are unaffected (as round 1 verified).
- The r2 deviation (non-blocking extras) is disclosed and limited to tests and docs.
