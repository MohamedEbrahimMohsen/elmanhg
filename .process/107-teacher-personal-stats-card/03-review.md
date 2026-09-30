VERDICT: APPROVED

# Review — [E11.S4] Teacher personal stats card (#107)

## Blocking
None.

## Non-blocking
- `api/Elmanhg.Tests/Integration/Dashboard/MyTeacherStatsEndpointTests.cs:1-124` — 124 lines, a little over the ~100-line guide (disclosed in 02). Acceptable; splitting the auth rows (I5/I6) out would read better.
- `web/src/features/dashboard/pages/TeacherStatsPage.tsx:5` — `registerTeacherStatsLocales()` is also called by `TeacherStatsCard.tsx:8`, so this call is redundant. Harmless because `addResourceBundle(..., true, true)` is idempotent.

## Verified
- **Teacher id only from the token:** `GetMyTeacherStatsHandler.cs:17` reads `ICurrentUserService.UserId` and fails closed on null or empty (401). `GetMyTeacherStatsQuery.cs:6` carries only `From`/`To`. The controller (`DashboardController.cs:104-111`) binds only `from`/`to`. There is no teacherId or subjectId anywhere on the surface.
- **Single-teacher reuse of the #104 queries:** the handler passes `subjectId: null, teacherId` (`:23-24`). The SQL filters on `d."DecidedBy"` (`QuestionRepository.Metrics.cs:42`) and `m."SenderId"` (`TeacherThreadRepository.cs:60`), so it is correct for one teacher. I1/I2 prove A and B are isolated at the database level.
- **No cross-teacher caching:**
  - Server: `DashboardCacheBehaviour` only caches `IDashboardQuery`, and the query implements only `IDashboardRange`. C1 pins this.
  - Client: `clearSession` calls `queryClient.clear()` (`authSession.ts:33`), so the non-user-keyed TanStack entry cannot survive a sign-out and a switch of user.
- **Policy:** `TeacherStats.ViewOwn` is `RequireRole(Teacher)` (`PermissionMatrixPolicies.cs:33`). The class-level `[Authorize]` has no policy, so the new action is not also gated by `DashboardsView`. I6 covers Student→403 and Admin→403; I5 covers anonymous→401.
- **#104 unchanged by the `IDashboardRange` split:**
  - `IDashboardRangeQuery` still extends `IDashboardQuery`, so the cache still applies to the admin cards.
  - `DashboardFilterRules` changed only its constraint.
  - No existing dashboard test was edited; the only test change is the C1 addition.
- **Teacher chunk:** I walked the manifest's static closure of `index.html` + `teacher/route` + `teacher/index` (and the same for `teacher/stats`) myself. It contains no `dashboard` module keys, and none of "Sign-up funnel", "SLA breaches in period" or `DailyBarChart`. The teacherStats strings (en and ar) are present.
- **RTL and percent:**
  - Latin digits come from `metricFormat`.
  - `dir="rtl"` is asserted in R4 and R7.
  - The single Deviation (the LRM marks Intl inserts around `%` in `ar`) is real, disclosed, and handled with a tight anchored regex (`TeacherStatsPage.test.tsx:30`).
- **Contract fidelity:** every A1–A5, T1–T3 and W1–W8 file exists and matches the plan's signatures, JSON (verbatim) and render order. There are no extra files beyond the generated Orval models. `ValidationQueuePage.tsx:97` renders the card between the header and the filters. `routes/teacher/stats.tsx` no longer uses `PlaceholderPage`.
- **Design tokens:** only tokens are used (`text-h1`, `lg:text-h1-desktop`, `text-caption`, `text-text-muted`, plus the existing `MetricCard` / `KpiFigure` classes). There are no literals.
- **Postman:** "Get my teacher stats" is the last item of `TeacherInbox`. It is GET `{{baseUrl}}/api/dashboard/my-stats` with `from`/`to` disabled, uses the collection bearer (the teacher token at that point in the run), and has the two planned tests.
- **Docs, no divergence:**
  - `docs/dashboard.md` has the intro, the new section, the caching note and the endpoint row.
  - `docs/PRD.md` §8.4 is updated.
  - `docs/claude-design-prompt.md` §4 has the `#/teacher` card text and the `#/teacher/stats` line.
  - `docs/performance.md` has the §3 row 250/265 and the §4 sentence.
  - All of them match the code.
- **Builds re-run by me:**
  - API: `dotnet test api/ -c Release`, with no `appsettings.json` in the worktree (CI parity), passed 3994/3994.
  - Web static checks: `tsc -b --noEmit`, `eslint --max-warnings=0` and `prettier --check` are all clean.
  - Web tests: `vitest run --coverage` passed 223 files and 1274 tests, exit 0.
  - Web build: `npm run build` succeeded, and `perf:budget` is all ok (teacher-home 250/265).
  - Generated artifacts: `gen:api` produces no drift.

## Test quality
- `GetMyTeacherStatsHandlerTests`: constrains the implementation.
  - H2 stubs non-default stats only for `(null, callerId)`, so passing a null or wrong id yields zeros and fails.
  - H4 pins the exact Cairo instants, `callerId` and the 24 h SLA with `Received(1)`.
  - H1 checks the error code and `DidNotReceive` on both repositories.
- `GetMyTeacherStatsValidatorTests`: each rule has a failing case with `ContainSingle` on the error code.
- `DashboardCacheBehaviourTests` C1: it would fail if the query became an `IDashboardQuery` (the implementer reported this mutation red).
- `MyTeacherStatsEndpointTests`: real database, two teachers, cross-checked isolation for both decisions and replies, plus the 401/403/422 rows. Strong.
- `TeacherStatsCard.test.tsx`, `TeacherStatsPage.test.tsx` and `ValidationQueuePage.stats.test.tsx`: they cover loading, success, idle, error with retry, RTL and axe, and the card's presence and failure isolation on `/teacher`. Each asserts rendered text derived from the fixture, so none is vacuous.
