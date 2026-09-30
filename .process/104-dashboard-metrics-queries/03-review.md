VERDICT: CHANGES_REQUESTED

# Review — [E11.S1] Dashboard metrics queries (#104)

Reviewed in worktree `D:/Personal/elmanhg-wt/104` against `01-plan.md`, the gate conditions in `00-acceptance.md` and `02-implementation.md`. Every changed or new production file, the migration and the listed test files were read.

## Blocking

### 1. The funnel "completed journey" rule has no test; the implementer's surviving mutant is real and cheap to kill
**Where:** `api/Elmanhg.Infrastructure/Analytics/FunnelEventRepository.cs:36` (`WHERE v."Landed" IS NOT NULL AND v."Answered" >= v."Landed"`); `api/Elmanhg.Tests/Integration/Dashboard/FunnelMetricsEndpointTests.cs` (only test)
**Rule:** plan Decision 21 ("both in range, answered >= landed"); `docs/analytics.md:52` and `docs/dashboard.md:105`, both written in this change; testing convention (every rule needs a test that fails when the rule is wrong)
**Problem:** The only guard on `completedJourneys` and the median is this predicate, and no test covers it. `02-implementation.md` reports that the mutant `"Answered" IS NOT NULL` survives. The reason given for leaving it (closing it "would change the plan's expected step counts [3,2,1,1,2]") does not hold: a separate test on its own unique date (the plan's rule for global cards) leaves row 83 untouched.
**Failure:** A regression to `"Answered" IS NOT NULL` passes the whole suite. With it, a visitor who answers inside the range after landing before it (the exact case `docs/analytics.md:52` says "has no timing") is counted in `completedJourneys`. The median is also skewed (the null diff is ignored, but the count is not).
**Fix:** Add one integration test on a new date (e.g. 2021-06-07), for example `Get_AnswerWithoutLandingInRange_IsNotACompletedJourney`. Seed visitor D with `FirstQuizAnswered` only inside the range, plus a `LandingViewed` the day before, and visitor E who answers before landing inside the range. Assert `completedJourneys == 0` and `medianLandingToFirstAnswerSeconds` is null.

### 2. `Dashboard:*` keys are missing from the deployment config reference
**Where:** `api/Elmanhg.Api/appsettings.example.json:27` adds the section. `docs/deployment.md` §4 "Environment variable reference" (no Dashboard subsection; compare the non-secret sections "Content retrieval", "AI Avatar" and "Ask a Teacher" at `docs/deployment.md:156-211`). `deploy/api.env.example` has no `Dashboard__*` block (compare lines 49-89).
**Rule:** Orchestrator convention for this run: new config keys go into `appsettings.example.json`, `ApiFactory`, code defaults, **`docs/deployment.md` and `deploy/*.env.example`**. This matches the precedent set by every recent non-secret options section.
**Problem:** The implementer flagged this and chose not to do it. The keys are safe by default, but `Dashboard:TimeZone` is checked by `ValidateOnStart` (`api/Elmanhg.Application/DependencyInjection.cs:47-50`), so a bad value stops the API. The operator reference does not list the key.
**Failure:** An operator who sets `Dashboard__TimeZone=Cairo`, or `Dashboard__DefaultRangeDays` above `MaxRangeDays`, gets an API that will not start, and `docs/deployment.md` has no entry for the key or its constraints. An operator looking to tune `CacheSeconds` finds nothing.
**Fix:** Add a `### Dashboard (api.env, docs/dashboard.md)` table to `docs/deployment.md` §4, with the six keys, their defaults and the "None is a secret; validated at startup" line used by the sibling sections. Add a commented `# Dashboard (docs/dashboard.md) ...` block with the six `Dashboard__*` keys to `deploy/api.env.example`.

### 3. Docs divergence: `QuestionDecision` field list does not include the new `submittedAt`
**Where:** Code: `api/Elmanhg.Domain/Questions/QuestionDecision.cs:14,30`, `api/Elmanhg.Domain/Questions/Question.Approval.cs:26,45` and migration `20260930054113_AddDashboardMetrics.cs` (new non-null column plus backfill). Doc: `docs/question-schemas.md:182`, § "Validation status".
**Rule:** `.claude/rules/docs-sync.md` (data-model change; the owning doc must agree)
**Problem:** The doc lists exactly what every approve or reject records on a `QuestionDecision`: `version`, `outcome`, `reason`, `difficulty`, `difficultyChangedFrom`, `decidedBy`, `decidedAt`. The code now also records `submittedAt`, a copy of the question's `SubmittedAt` at decision time. Existing rows are backfilled from the decided version's `QuestionRevisions.EditedAt`, falling back to `DecidedAt`. `docs/dashboard.md` relies on this field, but the doc that owns the entity says something different.
**Failure:** "What does a `QuestionDecision` record?" gets two answers: 7 fields in the doc, 8 in the code and schema. #107 (per-teacher decision time) would be planned from the stale list.
**Fix:** Add `submittedAt` (when the decided version was submitted; the median time-to-decision in `docs/dashboard.md` is measured from it) to the list at `docs/question-schemas.md:182`. Add one clause on the `AddDashboardMetrics` backfill next to the existing "The migration backfilled..." sentence.

## Non-blocking
- `api/Elmanhg.Application/Dashboard/Shared/DashboardCacheKey.cs:7`: no test checks that `from`, `to` and `subjectId` are in the key. `DashboardCacheBehaviourTests` uses probe keys, and the integration host runs with `CacheSeconds=0`. The code is correct: every one of the 9 queries passes all of its filters, and null formats as empty. A regression that dropped `subjectId` would serve the unfiltered card to a filtered request for 60 s without any test failing. A 3-line `DashboardCacheKeyTests` would close this.
- `api/Elmanhg.Tests/Application/Features/Dashboard/GetStudentMetrics/GetStudentMetricsHandlerTests.cs:34`: `CountAsync(...).Returns(120, 9, 4)` is keyed on call order, so the `NewThisWeek` predicate (`GetStudentMetricsHandler.cs:346`) is not constrained. Integration covers `newInRange` only.
- `api/Elmanhg.Tests/Integration/Dashboard/UserActivityTrackingTests.cs:34` (`AuthenticatedRequests_RecordOneRowPerDay`): the sign-in row alone satisfies it. The `ICurrentUserService` path (a non-auth request records a row) is covered only by the unit tests.
- `api/Elmanhg.Application/Shared/Analytics/UserActivityBehaviour.cs:44`: the catch-all also catches `OperationCanceledException` when a client aborts, which logs a warning for a normal disconnect. Consider an exception filter for cancellation. The failure isolation is otherwise correct: the seen-key is set only after a successful insert, and the response is always returned.
- `UserActivityBehaviour` runs `ExecuteSqlAsync` on the request-scoped `AppDbContext`. That is safe today (no handler or behaviour opens an explicit transaction). If a transaction behaviour is ever added around handlers, a failed insert would abort it. Worth a line in `docs/dashboard.md` (Activity tracking).
- `api/Elmanhg.Api/Controllers/Dashboard/DashboardController.cs` is 102 lines, marginally over the ~100 guide. Acceptable for 9 one-line actions.
- `docs/PRD.md:361` reads "All charts filterable by date range..." and then "Content ... no date range". The meaning is clear, but the wording contradicts itself.
- New files are LF while the checkout is CRLF (git warnings on `FunnelEventRepository.cs`, `PaymentRepository.cs`, etc.). It is harmless, and `dotnet format` passes.

## Verified
- **CI parity:** `dotnet test api/ -c Release` re-run by me with no `api/Elmanhg.Api/appsettings.json` present: **Passed, total 3600, failed 0**. This includes `PermissionMatrixPolicyTests`, `AppDbContextTests` (migration list) and the Testcontainers integration suite.
- **No Orval drift:** `npm --prefix web run gen:api` re-run, and the generated diff hash is identical before and after. **OpenAPI:** `api/openapi/v1.json` was rebuilt by the build, has the 9 `/api/dashboard/*` paths and is consistent with the committed Orval output.
- **Postman:** a `Dashboard` folder sits directly after `AdminPayments`. It has 9 GETs whose URLs and methods match the controller, auth is inherited from the collection bearer token (the admin token at that point), and the from/to/subjectId params are disabled and set only on the cards that accept them. Each request tests `status is 200` plus a field check. No stale or orphaned requests.
- **Admin-only:** the controller has class-level `[Authorize]`, and every one of the 9 actions has `[Authorize(Policy = DefaultCodes.DashboardsView)]`. `PermissionMatrixPolicies.cs:32` maps that policy to `RequireRole(Admin)`. The integration tests cover 401 on all 9 routes, 403 for Student and Teacher, and 200 for admin on all 9.
- **Raw SQL safety:** every raw statement goes through `Database.SqlQuery<T>` or `ExecuteSqlAsync` with an interpolated (parameterised) raw string: the time zone, the enum names built with `nameof`, the ids, the bounds and the SLA seconds are all parameters. There is no `*Raw` anywhere in the diff. Optional filters use `(param::uuid IS NULL OR col = param)`, and integration tests cover both the null and set cases. Every raw query adds `"IsDeleted" = false` for each table it reads.
- **Metric SQL vs PRD §10.3 and plan Decisions 12-21:**
  - Solve rate is sum(attempts) / sum(student-days), excludes test mode and deleted questions, and the subject filter applies to the numerator only.
  - Success rate uses `NormalisedScore >= Mastery:CorrectThreshold`.
  - Validation: the backlog is non-retired Pending questions; the median is `percentile_cont` over `DecidedAt - SubmittedAt`.
  - Ask a Teacher: the reply wait is measured from the latest earlier student message (LATERAL), and the SLA comes from `Subscriptions:AskTeacherReplySlaHours`.
  - Payments: Succeeded or Refunded count on `CompletedAt`, and refunds count on `RefundedAt`.
  - MRR: the latest Succeeded payment / `PeriodMonths` over Active and PastDue subscriptions.
  - Churn: Cancelled or Expired, dated by `CancelledAt ?? ExpiredAt`.
  - Funnel: distinct `AnonymousId` per step.
  - Status buckets split retired questions out. Servable counts go through `WhereServable` only.
- **Migration backfill:** `QuestionDecisions` has no append-only trigger (only Attempts, AuditLogs and AvatarMessages do), so the UPDATE is allowed. A `QuestionRevision` is created at question creation (`Question.cs:54`) as well as on edit, so the (QuestionId, Version) join finds a row for version 1 too. The `DecidedAt` fallback only covers orphaned rows. The column goes nullable, then backfill, then NOT NULL; `Down` reverses everything, and there is no unplanned drop or rename.
- **Cache:** `DashboardCacheBehaviour` applies only to `IDashboardQuery`, and `CacheSeconds == 0` bypasses it. The default is 60 in code and in `appsettings.example.json`, and `ApiFactory` sets 0. Keys contain the card, the raw from/to and subjectId for all 9 queries (Content: subjectId only).
- **UserActivityBehaviour** (gate condition a):
  - Records nothing for anonymous callers or when the handler throws.
  - Takes the user from the current user, falling back to `AuthResult`.
  - Dedups in memory per user per Cairo day, so repeat requests never touch the DB.
  - Sets the seen-key only after a successful insert.
  - Logs and swallows write failures, which is tested by `Handle_RecordFails_ReturnsResponseAndRetriesOnNextRequest`.
  - Per-request cost is one `IMemoryCache` lookup plus a string format.
- **Gate condition b:** the subject-filter deviation is disclosed as Deviation 1, and PRD §10.3 and `docs/dashboard.md` are updated.
- **Deviations 2-6** checked against the code: the logger parameter, `static readonly TimeSpan`, the partial split into `*.Metrics.cs`, the extra `DashboardTestData` helper and the widened day assertion in test 70. All are as described. Deviation 4 adds 2 files beyond *Files to create*, which is justified by the file-length rule.
- All 83 planned test names exist (plus the one extra disclosed test). `dotnet format --verify-no-changes` on the changed .cs files is clean. There is no `DateTime.Now/UtcNow`, `.Result` or block-scoped namespace, and every await has `.ConfigureAwait(false)`.
- Docs: `docs/dashboard.md` exists with the planned sections; `docs/PRD.md` §10.3 and §15 and `docs/analytics.md` are updated as planned.

## Test quality
- `UserActivityBehaviourTests`: strong. It uses a real `MemoryCache`, records the inputs through `Arg.Do` and asserts the day, the user and the dedup counts. Removing the seen-key `Set`, or narrowing the catch, fails it.
- `DashboardCacheBehaviourTests`: constrains the behaviour but not the key composition (see non-blocking).
- `DashboardWindowTests` / `DashboardFilterRulesTests` / `DashboardRatesTests`: constrain the boundaries, the DST gap, the 366/367 edge and rounding. The theory covers all 8 range validators.
- Handler tests (9 classes): mostly constraining, with exact values from generator arithmetic (7.14, 0.4, 0.95, 7200, 210, zero-fill). The exception is the call-order Returns in `GetStudentMetricsHandlerTests`, which leaves the NewThisWeek predicate free.
- Integration metric tests: seed real rows on unique past dates and assert exact numbers. They kill the SQL mutants reported (test mode, refunded, student-only join, sender lateral, order). **`FunnelMetricsEndpointTests` does not constrain the completed-journey filter** (Blocking #1).
- `UserActivityTrackingTests.AuthenticatedRequests_RecordOneRowPerDay` passes on the login row alone, so it does not show that a non-auth authenticated request records a row.
