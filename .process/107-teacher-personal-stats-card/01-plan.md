# Plan — [E11.S4] Teacher personal stats card (#107)

## Goal
A signed-in teacher sees their own numbers for the last 30 Cairo days: approved and rejected counts, median time to decision, and Ask-a-Teacher reply SLA compliance (plus reply count and median reply time). The card sits at the top of the teacher home (`/teacher`, the review queue). The same card fills `/teacher/stats`, which until now showed a placeholder. The numbers come from a new `GET /api/dashboard/my-stats` endpoint. It reuses #104's decision and reply statistics (already parameterised by `teacherId`), the Cairo-day window and the filter rules. The teacher id comes only from the token, so a teacher can never see anyone else's stats (PRD §8.4, §16 "own stats only").

## Scope
**In:**
- API: `GetMyTeacherStatsQuery`, with its handler, validator and result, plus the `DashboardController.GetMyTeacherStats` action (policy `TeacherStats.ViewOwn`, which already exists).
- A small interface split, `IDashboardRange`, so the range rules can be reused without the cross-user cache.
- Web: `TeacherStatsCard` (reusing `MetricCard`, `KpiFigure` and `metricFormat`), a lazy `teacherStats` i18n namespace, the card on `ValidationQueuePage`, and `TeacherStatsPage` on `/teacher/stats`.
- Tests, OpenAPI and Orval regeneration, the Postman request, a `teacher-home` bundle budget, and doc updates.

**Out:**
- Teacher ratings (PRD §12.1: "Ratings are visible to admin"; the story does not ask for them).
- SLA breach events per teacher (`CountBreachesAsync` has no teacher dimension, and the story asks for compliance, not breaches).
- A subject filter or period selector on the teacher card.
- Pay per validation (PRD §19 Q2, still open).

**Deferred:** none.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | Where does the endpoint live? | `GET /api/dashboard/my-stats` on the existing `DashboardController`, `Name = "GetMyTeacherStats"`, `[Authorize(Policy = DefaultCodes.TeacherStatsViewOwn)]`. | Same slice family as #104. The policy and its matrix test already exist (`PermissionMatrixPolicies.cs:33`). `TeachersController` is admin-only and #106's lane is editing admin/users areas. |
| 2 | How is "only their own stats" enforced? | The handler reads `ICurrentUserService.UserId` and passes it as `teacherId` to `GetDecisionStatsAsync(..., subjectId: null, teacherId)` and `GetReplyStatsAsync(..., subjectId: null, teacherId, ...)`. The query takes no teacher or subject id. | The id cannot be spoofed. Both repository methods already filter on `DecidedBy` / `SenderId` (added by #104 for this story). |
| 3 | `ISubjectScopedRequest`? | Not used. | That interface needs a non-null `SubjectId` and guards reads of subject-owned content. This query has no subject parameter: it aggregates only the caller's own decisions and replies, so there is nothing to scope. |
| 4 | Count decisions in subjects the teacher is no longer assigned to? | Yes: all of the caller's own decisions and replies in the window. | This is the teacher's own work history, and the aggregate exposes no other subject's content. It matches the prototype `statsCard`, which counts every audit row by actor. |
| 5 | Cache? | **Not cached.** The query implements the new `IDashboardRange`, not `IDashboardQuery`. | `DashboardCacheBehaviour` keys on `request.CacheKey`, which cannot include the caller. Caching would serve teacher A's numbers to teacher B. Both queries are per-teacher and cheap: indexes exist on `QuestionDecisions.DecidedAt` and `TeacherMessages.SenderId` / `CreatedAt`. A unit test pins this. |
| 6 | Reuse the range validation without the cache interface? | New `IDashboardRange { DateOnly? From; DateOnly? To }`. `IDashboardRangeQuery` becomes `: IDashboardQuery, IDashboardRange` with its own members removed. `AddDashboardFilterRules<T>` is constrained to `IDashboardRange`. | Existing queries compile unchanged. This is the smallest change that reuses the 422 rules (`DASHBOARD_DATE_RANGE_INVALID` / `_TOO_WIDE`). |
| 7 | Date range | `from` and `to` are optional query params, with the same semantics as the other cards (Cairo days, inclusive, default = last `Dashboard:DefaultRangeDays` (30) days, max 366). The web sends **no params** and shows the resolved `from`/`to` from the response. | No cache, so no stale default-range key (the reason the admin page always sends both). The server owns "today in Cairo". |
| 8 | Result fields | `From, To, Approved, Rejected, MedianSecondsToDecision (long?), Replies, RepliedWithinSla, SlaComplianceRate (decimal?), MedianReplySeconds (long?), GeneratedAt`. | PRD §8.4 fields, plus the reply count and median, which cost nothing (same SQL row). Rounding and nulls follow `docs/dashboard.md` via `DashboardRates`. |
| 9 | SLA compliance definition | `repliedWithinSla ÷ replies` over replies sent by the caller in the window. The wait runs from the latest earlier student message, against `Subscriptions:AskTeacherReplySlaHours`. | This is exactly the #104 Ask-a-Teacher card definition, restricted to the sender. |
| 10 | Card placement | The card goes on `/teacher` (between the page header and the filters, as the prototype `vTeacherQueue` places `statsCard` after the `h2`) **and** on `/teacher/stats`, which is `TeacherStatsPage`: H1 «إحصائياتي», an intro line, then the same card. | Story: "Stats card UI on the teacher home". Design prompt §4: "stats strip" on `#/teacher`, and `#/teacher/stats` is the personal stats card. |
| 11 | Where the web code lives | Inside `web/src/features/dashboard/`. The card and page are exported through the dashboard barrel, and `ValidationQueuePage` imports `TeacherStatsCard` from `@/features/dashboard`. | This reuses `MetricCard`, `KpiFigure` and `metricFormat` without moving them. Verified with Vite 8.3.1 and this repo's `sideEffects: ["**/*.css"]`: importing one export from a barrel does **not** pull a sibling page or its module-level `register…Locales()` call into another route chunk, so `DashboardPage` and the `dashboard` JSON stay out of `/teacher`. |
| 12 | i18n budget | New lazy namespace `teacherStats`, in `i18n/teacherStats/{ar,en}.json`. `teacherStatsLocales.ts` exports `registerTeacherStatsLocales()`, which is called at module level in `TeacherStatsCard.tsx` (the #105 pattern). It is **not** added to `app/i18n.ts`. `MetricCard` gets an optional `ns` prop (default `'dashboard'`) so it reads `card.loading` / `card.error` from `teacherStats`. | Keeps the admin copy out of teacher pages and the teacher copy out of student pages. A separate locales file stops `locales.ts`'s dashboard JSON imports from joining the teacher chunk. |
| 13 | Digits | Latin, through the existing `metricFormat` helpers. | Existing teacher screens (`features/questions`) all use `'latin'`. Design system: Arabic-Indic digits are for student-facing UI only. |
| 14 | Headline figure | `KpiFigure` value = `approved + rejected` (decisions in the period). Caption: «قرارات المراجعة من {from} إلى {to}». The list follows. Zero activity adds the note «لا توجد قرارات أو ردود في هذه الفترة.» | A card has no list-empty state. The zero case is its explicit empty state, and `—` is shown for null medians and rates. |
| 15 | Bundle budget | Add a `teacher-home` page to `web/scripts/perf/budgets.json` (entries `index.html`, `src/routes/teacher/route.tsx?tsr-split=component`, `src/routes/teacher/index.tsx?tsr-split=component`). `maxKb` = measured × 1.05, rounded up to the next 5, per `docs/performance.md` §3. Add the row to that table. | The teacher home gains a card and a namespace, and it had no guard. |
| 16 | Morabh reuse | New — no Morabh equivalent. Morabh `Morabh.APIs/Controllers/Dashboard/DashboardController.cs` and `Morabh.Application/Dashboard/GetApprovalMetrics/*` are admin-only, with no per-user card. The slice mirrors Elmanhg's own #104 `GetAskTeacherMetrics` / `GetValidationMetrics`, which follow that Morabh shape. | Reuse-first rule. |
| 17 | Error codes | None new. Uses the existing `USER_NOT_AUTHENTICATED`, `DASHBOARD_DATE_RANGE_INVALID` and `DASHBOARD_DATE_RANGE_TOO_WIDE`. | Every failure branch already has a code and resource strings. |

## Existing code touched
| File | Change |
|------|--------|
| `api/Elmanhg.Application/Dashboard/Shared/IDashboardRangeQuery.cs` | Body becomes `public interface IDashboardRangeQuery : IDashboardQuery, IDashboardRange` with an empty `{ }` body. Remove its `From` / `To` members (now inherited). |
| `api/Elmanhg.Application/Dashboard/Shared/DashboardFilterRules.cs` | Constraint `where T : IDashboardRangeQuery` becomes `where T : IDashboardRange`. Nothing else changes. |
| `api/Elmanhg.Api/Controllers/Dashboard/DashboardController.cs` | Add a `GetMyTeacherStats` action as the last action (see API surface) and `using Elmanhg.Application.Dashboard.GetMyTeacherStats;`. |
| `api/openapi/v1.json` | Regenerated by `dotnet build` (never hand-edited). |
| `api/Elmanhg.Tests/Application/Features/Dashboard/Shared/DashboardCacheBehaviourTests.cs` | **Add** one test (row C1). Existing tests stay untouched. |
| `web/src/features/dashboard/components/MetricCard.tsx` | Add the prop `ns?: 'dashboard' \| 'teacherStats'` to `MetricCardProps<T>`. Destructure it with default `'dashboard'` and call `useTranslation(ns)`. No other change. |
| `web/src/features/dashboard/index.ts` | Append `export { TeacherStatsCard } from './components/TeacherStatsCard';` and `export { TeacherStatsPage } from './pages/TeacherStatsPage';`. |
| `web/src/features/questions/pages/ValidationQueuePage.tsx` | Add `import { TeacherStatsCard } from '@/features/dashboard';`. Render `<TeacherStatsCard />` directly after the header `<div className="flex flex-col gap-1">…</div>` and before `<ValidationQueueFilters …/>`. |
| `web/src/routes/teacher/stats.tsx` | Replace the `PlaceholderPage` import and component with `import { TeacherStatsPage } from '@/features/dashboard';` and `component: TeacherStatsPage`. |
| `web/src/test/dashboardFixtures.ts` | Add an exported `teacherStats(overrides: Partial<MyTeacherStatsResult> = {}): MyTeacherStatsResult` (values below). Add `getGetMyTeacherStatsMockHandler(teacherStats())` to the end of `dashboardHandlers()`, and extend the imports. |
| `web/src/shared/api/generated/**` | Regenerated by `npm --prefix web run gen:api` (new `getMyTeacherStats`, `useGetMyTeacherStats`, `getGetMyTeacherStatsMockHandler`, `MyTeacherStatsResult` and `GetMyTeacherStatsParams`). |
| `web/scripts/perf/budgets.json` | Add the `teacher-home` page (Decision 15) after `admin-dashboard`. |
| `postman/elmanhg.postman_collection.json` | Add the request "Get my teacher stats" as the **last** item of the `TeacherInbox` folder (after "Get inbox (mine)"; the teacher token is set and a reply has been sent). Shape as below. |
| `docs/dashboard.md` | Changes listed under "Docs". |
| `docs/PRD.md` §8.4 | Changes listed under "Docs". |
| `docs/claude-design-prompt.md` §4 Teacher | Changes listed under "Docs". |
| `docs/performance.md` §3 | Changes listed under "Docs". |

`teacherStats()` in `web/src/test/dashboardFixtures.ts` returns:
```ts
{ from: '2026-09-01', to: '2026-09-30', approved: 12, rejected: 3, medianSecondsToDecision: 7200,
  replies: 40, repliedWithinSla: 37, slaComplianceRate: 0.925, medianReplySeconds: 5400,
  generatedAt: '2026-09-30T10:00:00Z', ...overrides }
```

Postman item:
- name `Get my teacher stats`
- GET `{{baseUrl}}/api/dashboard/my-stats`
- query `from=2026-09-01` and `to=2026-09-30`, both `disabled: true`
- test script:
  - `pm.test("status is 200", …)`
  - `pm.test("has own stats", function () { const b = pm.response.json(); pm.expect(b).to.have.property("approved"); pm.expect(b).to.have.property("slaComplianceRate"); pm.expect(b).to.have.property("generatedAt"); });`

## Files to create
| # | Path | Type | Contract |
|---|------|------|----------|
| A1 | `api/Elmanhg.Application/Dashboard/Shared/IDashboardRange.cs` | interface | `namespace Elmanhg.Application.Dashboard.Shared; public interface IDashboardRange { DateOnly? From { get; } DateOnly? To { get; } }` |
| A2 | `api/Elmanhg.Application/Dashboard/GetMyTeacherStats/GetMyTeacherStatsQuery.cs` | query | `namespace Elmanhg.Application.Dashboard.GetMyTeacherStats; public sealed record GetMyTeacherStatsQuery(DateOnly? From, DateOnly? To) : IRequest<MyTeacherStatsResult>, IDashboardRange;` It must **not** implement `IDashboardQuery` / `IDashboardRangeQuery`. |
| A3 | `api/Elmanhg.Application/Dashboard/GetMyTeacherStats/GetMyTeacherStatsValidator.cs` | validator | `public sealed class GetMyTeacherStatsValidator : AbstractValidator<GetMyTeacherStatsQuery>`. Constructor `(IOptions<DashboardOptions> dashboardOptions, TimeProvider timeProvider)`, body `this.AddDashboardFilterRules(dashboardOptions.Value, timeProvider);`. Rules (from `DashboardFilterRules`): resolved `From <= To` → `ErrorCodes.DashboardDateRangeInvalid`; `DayCount <= MaxRangeDays` → `ErrorCodes.DashboardDateRangeTooWide`. |
| A4 | `api/Elmanhg.Application/Dashboard/GetMyTeacherStats/GetMyTeacherStatsHandler.cs` | handler | Contract and steps below the table. |
| A5 | `api/Elmanhg.Application/Dashboard/GetMyTeacherStats/MyTeacherStatsResult.cs` | result | `public sealed record MyTeacherStatsResult(DateOnly From, DateOnly To, int Approved, int Rejected, long? MedianSecondsToDecision, int Replies, int RepliedWithinSla, decimal? SlaComplianceRate, long? MedianReplySeconds, DateTimeOffset GeneratedAt);` It is teacher-facing, has no `LocalizedText` fields, and needs no `.Localized()`. |
| T1 | `api/Elmanhg.Tests/Application/Features/Dashboard/GetMyTeacherStats/GetMyTeacherStatsHandlerTests.cs` | unit tests | Rows H1–H5. NSubstitute for `IQuestionRepository`, `ITeacherThreadRepository`, `ICurrentUserService` and `TimeProvider` (`GetUtcNow()` returns `2026-01-15T10:00Z`), `Options.Create(new DashboardOptions())`, `Options.Create(new SubscriptionsOptions { AskTeacherReplySlaHours = 24 })`, FluentAssertions. Mirrors `GetAskTeacherMetricsHandlerTests`. |
| T2 | `api/Elmanhg.Tests/Application/Features/Dashboard/GetMyTeacherStats/GetMyTeacherStatsValidatorTests.cs` | unit tests | Rows V1–V3. Clock fixed at `2026-01-15T10:00Z`. |
| T3 | `api/Elmanhg.Tests/Integration/Dashboard/MyTeacherStatsEndpointTests.cs` | integration | `public sealed class MyTeacherStatsEndpointTests(ApiFactory factory)` (same fixture style as `ValidationMetricsEndpointTests`). Rows I1–I6. Helpers: `ValidationTestData.SeedSubjectTreeAsync`, `ValidationTestData.SeedAssignedTeacherAsync`, `QuestionTestData.SeedQuestionAsync`, `TeacherInboxTestData.SignedInTeacherForAsync` / `SeedThreadAsync` / `ContextFor`, `TeacherThreadBuilder`, `ScopeTestData.SeedStudentAsync` / `SeedAdminAsync` / `SignedInClientAsync`, `AuthTestClient.Create`, `DashboardTestData.Route` / `GetJsonAsync`. |
| W1 | `web/src/features/dashboard/i18n/teacherStats/en.json` | locale | JSON below. |
| W2 | `web/src/features/dashboard/i18n/teacherStats/ar.json` | locale | JSON below. |
| W3 | `web/src/features/dashboard/teacherStatsLocales.ts` | module | `import { getI18n } from 'react-i18next'; import ar from './i18n/teacherStats/ar.json'; import en from './i18n/teacherStats/en.json'; export function registerTeacherStatsLocales(): void { const i18n = getI18n(); i18n.addResourceBundle('ar', 'teacherStats', ar, true, true); i18n.addResourceBundle('en', 'teacherStats', en, true, true); }` |
| W4 | `web/src/features/dashboard/components/TeacherStatsCard.tsx` | component | Contract below the table. |
| W5 | `web/src/features/dashboard/pages/TeacherStatsPage.tsx` | page | `registerTeacherStatsLocales();` at module level, then `export function TeacherStatsPage()`: `const { t } = useTranslation('teacherStats')`. Returns `<section className="flex flex-col gap-4">` containing `<h1 className="font-display text-h1 font-bold lg:text-h1-desktop">{t('page.title')}</h1>`, `<p className="text-caption text-text-muted">{t('page.intro')}</p>` and `<TeacherStatsCard />`. |
| W6 | `web/src/features/dashboard/components/TeacherStatsCard.test.tsx` | tests | Rows R1–R5. Uses `renderWithProviders(<TeacherStatsCard />, { lng })`. |
| W7 | `web/src/features/dashboard/pages/TeacherStatsPage.test.tsx` | tests | Rows R6–R7. Uses `renderApp('/teacher/stats', { session: testSessions.teacher, lng })` and `await rendered.router.loadRouteChunk(rendered.router.routesById['/teacher/stats'])`. |
| W8 | `web/src/features/questions/pages/ValidationQueuePage.stats.test.tsx` | tests | Rows R8–R9. Set up the review-session, filters and queue MSW handlers exactly as `ValidationQueuePage.test.tsx` `beforeEach` does (copy the minimal fixtures in: one item). Uses `renderApp('/teacher', { session: testSessions.teacher })`. |

### A4 handler contract
- Namespace `Elmanhg.Application.Dashboard.GetMyTeacherStats`.
- Declaration: `public sealed class GetMyTeacherStatsHandler(IQuestionRepository questionRepository, ITeacherThreadRepository teacherThreadRepository, ICurrentUserService currentUserService, TimeProvider timeProvider, IOptions<DashboardOptions> dashboardOptions, IOptions<SubscriptionsOptions> subscriptionsOptions) : IRequestHandler<GetMyTeacherStatsQuery, MyTeacherStatsResult>`

`Handle` does, in order:
1. `if (currentUserService.UserId is not { } teacherId || teacherId == Guid.Empty) throw new UnauthorizedCoreException(ErrorCodes.UserNotAuthenticated);`
2. `var window = DashboardWindow.Resolve(request.From, request.To, timeProvider.GetUtcNow(), dashboardOptions.Value);`
3. `var decisions = await questionRepository.GetDecisionStatsAsync(window.Start, window.End, null, teacherId, cancellationToken).ConfigureAwait(false);`
4. `var replies = await teacherThreadRepository.GetReplyStatsAsync(window.Start, window.End, null, teacherId, TimeSpan.FromHours(subscriptionsOptions.Value.AskTeacherReplySlaHours), cancellationToken).ConfigureAwait(false);`
5. Return:
   ```csharp
   new MyTeacherStatsResult(window.From, window.To, decisions.Approved, decisions.Rejected,
       DashboardRates.Seconds(decisions.MedianSecondsToDecision), replies.Replies, replies.RepliedWithinSla,
       DashboardRates.Ratio(replies.RepliedWithinSla, replies.Replies, DashboardRates.RateDecimals),
       DashboardRates.Seconds(replies.MedianReplySeconds), window.Now)
   ```

Rules: no `SaveChangesAsync`, and no repository or domain changes.

### W4 `TeacherStatsCard` contract
Imports:
- `useTranslation`
- `useGetMyTeacherStats` from `@/shared/api/generated/dashboard/dashboard`
- `formatCount`, `formatDay`, `formatElapsed`, `formatRate` from `../api/metricFormat`
- `registerTeacherStatsLocales` from `../teacherStatsLocales`
- `KpiFigure` and `MetricCard`

Module level: `registerTeacherStatsLocales();`

`export function TeacherStatsCard()` takes no props.
- `const { t, i18n } = useTranslation('teacherStats'); const lng = i18n.resolvedLanguage ?? i18n.language; const query = useGetMyTeacherStats();`
- Render `<MetricCard ns="teacherStats" title={t('card.title')} query={query}>`. Its child `(data) => { … }` computes `const decisions = Number(data.approved) + Number(data.rejected); const idle = decisions === 0 && Number(data.replies) === 0;` and returns a `<KpiFigure>`:
  - `value={formatCount(decisions, lng)}`
  - `caption={t('card.caption', { from: formatDay(data.from, lng), to: formatDay(data.to, lng) })}`
  - `note={idle ? t('card.empty') : undefined}`

  The `KpiFigure` children are, in this order:
  - `<li>{t('card.approved', { count: formatCount(data.approved, lng) })}</li>`
  - `<li>{t('card.rejected', { count: formatCount(data.rejected, lng) })}</li>`
  - `<li>{t('card.medianDecision', { duration: formatElapsed(data.medianSecondsToDecision, lng) })}</li>`
  - `<li>{t('card.compliance', { rate: formatRate(data.slaComplianceRate, lng) })}</li>`
  - `<li>{t('card.replies', { count: formatCount(data.replies, lng), within: formatCount(data.repliedWithinSla, lng) })}</li>`
  - `<li>{t('card.medianReply', { duration: formatElapsed(data.medianReplySeconds, lng) })}</li>`
- No `useMemo`, no literals, tokens only.

### W1 `en.json`
```json
{
  "page": { "title": "My stats", "intro": "Your review decisions and replies to students, in Cairo days." },
  "card": {
    "title": "My reviews and replies",
    "loading": "Loading {title}",
    "error": "Could not load {title}",
    "caption": "Review decisions from {from} to {to}",
    "empty": "No decisions or replies in this period.",
    "approved": "Approved: {count}",
    "rejected": "Rejected: {count}",
    "medianDecision": "Median time to decision: {duration}",
    "compliance": "Reply SLA compliance: {rate}",
    "replies": "Replies: {count} ({within} within the SLA)",
    "medianReply": "Median reply time: {duration}"
  }
}
```

### W2 `ar.json`
```json
{
  "page": { "title": "إحصائياتي", "intro": "قراراتك في المراجعة وردودك على الطلاب، بأيام القاهرة." },
  "card": {
    "title": "مراجعاتي وردودي",
    "loading": "جارٍ تحميل {title}",
    "error": "تعذّر تحميل {title}",
    "caption": "قرارات المراجعة من {from} إلى {to}",
    "empty": "لا توجد قرارات أو ردود في هذه الفترة.",
    "approved": "معتمد: {count}",
    "rejected": "مرفوض: {count}",
    "medianDecision": "وسيط زمن القرار: {duration}",
    "compliance": "الالتزام بمهلة الرد: {rate}",
    "replies": "الردود: {count} ({within} خلال المهلة)",
    "medianReply": "وسيط زمن الرد: {duration}"
  }
}
```

## Error codes
No new codes and no resource changes. Existing codes used:

| Constant | Value | Thrown by | Exception type | HTTP |
|----------|-------|-----------|----------------|------|
| `ErrorCodes.UserNotAuthenticated` | `USER_NOT_AUTHENTICATED` | `GetMyTeacherStatsHandler` step 1 | `UnauthorizedCoreException` | 401 |
| `ErrorCodes.DashboardDateRangeInvalid` | `DASHBOARD_DATE_RANGE_INVALID` | `GetMyTeacherStatsValidator` (shared rule) | validation pipeline | 422 |
| `ErrorCodes.DashboardDateRangeTooWide` | `DASHBOARD_DATE_RANGE_TOO_WIDE` | `GetMyTeacherStatsValidator` (shared rule) | validation pipeline | 422 |

The Arabic and English strings already exist from #104 and earlier stories.

## Domain behaviour
None. No entity, aggregate, repository interface, migration or `AppDbContext` change. Both statistics methods (`IQuestionRepository.GetDecisionStatsAsync`, `ITeacherThreadRepository.GetReplyStatsAsync`) already take `Guid? teacherId`.

## API surface
| Method | Route | Policy | Request | Response |
|---|---|---|---|---|
| GET | `/api/dashboard/my-stats?from&to` | `DefaultCodes.TeacherStatsViewOwn` (`TeacherStats.ViewOwn`, Teacher only) | `[FromQuery] DateOnly? from, [FromQuery] DateOnly? to` | `200 MyTeacherStatsResult`; `401` anonymous; `403` Student or Admin; `422` bad range |

Controller action:
```csharp
[HttpGet("my-stats", Name = "GetMyTeacherStats")]
[Authorize(Policy = DefaultCodes.TeacherStatsViewOwn)]
[ProducesResponseType<MyTeacherStatsResult>(StatusCodes.Status200OK)]
public async Task<ActionResult> GetMyTeacherStats([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
{
    var result = await mediator.Send(new GetMyTeacherStatsQuery(from, to), cancellationToken);
    return Ok(result);
}
```

## Docs
- **`docs/dashboard.md`**
  - Intro sentence: change "#107 reuses the validation and reply statistics per teacher" to "#107's teacher stats card reuses the validation and reply statistics for the signed-in teacher (see Teacher personal stats)".
  - Add a section `### Teacher personal stats — GET /api/dashboard/my-stats?from&to` after "Sign-up funnel":
    - Caller only: the teacher id comes from the token, and there is no `teacherId` or `subjectId` parameter.
    - It counts all of the caller's own decisions and replies, whatever their current subject assignments.
    - Field table:
      - `approved` / `rejected`: the caller's decisions in the range.
      - `medianSecondsToDecision`: as in Validation, over the caller's decisions.
      - `replies`: teacher messages the caller sent in the range.
      - `repliedWithinSla`, `slaComplianceRate`, `medianReplySeconds`: as in Ask a Teacher, over the caller's replies.
    - The web sends no range, so it gets the default 30 days.
  - Caching section: add "The teacher stats card is not cached: the cache key comes from the request, which carries no caller, so caching would share one teacher's numbers with another."
  - Endpoints table: add the row `GET · /api/dashboard/my-stats · TeacherStats.ViewOwn (Teacher) · from, to`.
- **`docs/PRD.md` §8.4:** third bullet becomes "Sees a personal stats card on the teacher home and «إحصائياتي»: approved/rejected counts, median decision time, and reply SLA compliance (with reply count and median reply time) over the last 30 Cairo days (`docs/dashboard.md`)."
- **`docs/claude-design-prompt.md` §4 Teacher**
  - In the `#/teacher` bullet, replace "stats strip" with "the personal stats card «مراجعاتي وردودي» (decisions in the period as the headline, the Cairo date range, approved, rejected, median time to decision, reply SLA compliance, replies with how many were within the SLA, median reply time; «—» when there is nothing to measure; «لا توجد قرارات أو ردود في هذه الفترة.» when idle; its own skeleton and error with retry; ASCII digits)".
  - Replace the `#/teacher/stats personal stats card.` line with "`#/teacher/stats` «إحصائياتي»: H1, an intro line, and the same personal stats card (last 30 Cairo days)."
- **`docs/performance.md` §3**
  - Budget table: add the row `teacher-home` · `+ teacher/route, teacher/index` · measured · budget.
  - §4 "Admin-only strings on demand": add "Likewise the `teacherStats` namespace is registered by `TeacherStatsCard` when a teacher route chunk loads."

## Test plan
### API unit — `GetMyTeacherStatsHandlerTests`
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| H1 | `GetMyTeacherStatsHandlerTests` | `Handle_NoCurrentUser_ThrowsUserNotAuthenticated` | `UserId` returns `null`. Throws `UnauthorizedCoreException` with `ErrorCode == ErrorCodes.UserNotAuthenticated`. `GetDecisionStatsAsync` and `GetReplyStatsAsync` `DidNotReceive()`. |
| H2 | same | `Handle_ReturnsCallerDecisionAndReplyStats` | Decision stats `{Approved 5, Rejected 2, Median 7199.6}` and reply stats `{Replies 20, RepliedWithinSla 19, Median 3600.4}` are stubbed **only for** `teacherId == callerId` and `subjectId == null`. Result: `Approved 5`, `Rejected 2`, `MedianSecondsToDecision 7200`, `Replies 20`, `RepliedWithinSla 19`, `SlaComplianceRate 0.95m`, `MedianReplySeconds 3600`, `GeneratedAt == Now`. |
| H3 | same | `Handle_NoActivity_ReturnsNullRateAndMedians` | Empty stats records. `SlaComplianceRate`, `MedianSecondsToDecision` and `MedianReplySeconds` are null. `Approved`, `Rejected` and `Replies` are 0. |
| H4 | same | `Handle_ExplicitRange_QueriesCairoDayBoundariesForCaller` | Query `(2026-01-05, 2026-01-10)`. `GetDecisionStatsAsync` `Received(1)` with `(2026-01-04T22:00Z, 2026-01-10T22:00Z, null, callerId, _)`. `GetReplyStatsAsync` `Received(1)` with the same instants, `null`, `callerId`, `TimeSpan.FromHours(24)`. Result `From`/`To` equal the inputs. |
| H5 | same | `Handle_NoRange_DefaultsToLastThirtyCairoDays` | Query `(null, null)`. `To == 2026-01-15`, `From == 2025-12-17`. |

### API unit — validator and cache
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| V1 | `GetMyTeacherStatsValidatorTests` | `Validate_NoDates_Passes` | `IsValid` is true. |
| V2 | same | `Validate_FromAfterTo_FailsWithDashboardDateRangeInvalid` | `(2026-01-10, 2026-01-05)`: an error with `ErrorCode == ErrorCodes.DashboardDateRangeInvalid`. |
| V3 | same | `Validate_RangeOverMax_FailsWithDashboardDateRangeTooWide` | `(2025-01-01, 2026-01-10)`: an error with `ErrorCode == ErrorCodes.DashboardDateRangeTooWide`. |
| C1 | `DashboardCacheBehaviourTests` (add) | `Handle_TeacherStatsQuery_IsNeverCached` | `Behaviour<GetMyTeacherStatsQuery>()` called twice with `new GetMyTeacherStatsQuery(null, null)` at the default `CacheSeconds` (60). `_nextCalls == 2` and `_memoryCache.Count == 0`. This pins Decision 5. |

### API integration — `MyTeacherStatsEndpointTests`
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| I1 | `MyTeacherStatsEndpointTests` | `Get_Teacher_CountsOnlyOwnDecisions` | Tree "Zoology". Teachers A and B are assigned. A approves one pending question and rejects another (`/api/validation-queue/questions/{id}/approve|reject` with `version = 1`); B approves a third. A's `GET my-stats`: `approved 1`, `rejected 1`, `medianSecondsToDecision` is a Number. B's: `approved 1`, `rejected 0`. |
| I2 | same | `Get_Teacher_CountsOwnRepliesWithinSla` | Subject "Botany". Teacher A (`SignedInTeacherForAsync`) claims and replies to a thread submitted 1 h ago; teacher B is assigned to the same subject. A: `replies 1`, `repliedWithinSla 1`, `slaComplianceRate 1`, `medianReplySeconds > 0`. B: `replies 0`, `slaComplianceRate` Null. |
| I3 | same | `Get_NoActivity_ReturnsZerosNullsAndThirtyDayRange` | A fresh teacher: `approved 0`, `replies 0`, `medianSecondsToDecision` Null, `slaComplianceRate` Null. `to − from == 29` days (parsed `DateOnly`). `generatedAt` is a String. |
| I4 | same | `Get_FromAfterTo_Returns422DashboardDateRangeInvalid` | A teacher calls `my-stats?from=2026-01-10&to=2026-01-05`: 422, problem `code == "DASHBOARD_DATE_RANGE_INVALID"`. |
| I5 | same | `Get_Anonymous_Returns401` | `AuthTestClient.Create(factory)` returns 401. |
| I6 | same | `Get_NonTeacher_Returns403` (`[Theory]` `UserRole.Student`, `UserRole.Admin`) | 403. |

### Web — `TeacherStatsCard.test.tsx` (`describe('TeacherStatsCard')`)
| # | Test method | Asserts |
|---|-------------|---------|
| R1 | `it('shows my stats after loading')` | `status` "Loading My reviews and replies" appears. Then the region "My reviews and replies" shows `15`, "Review decisions from Sep 1 to Sep 30", "Approved: 12", "Rejected: 3", "Median time to decision: 2 hours", "Reply SLA compliance: 92.5%", "Replies: 40 (37 within the SLA)", "Median reply time: 1.5 hours". |
| R2 | `it('shows dashes and the empty note when there is nothing to measure')` | `server.use(getGetMyTeacherStatsMockHandler(teacherStats({ approved: 0, rejected: 0, replies: 0, repliedWithinSla: 0, medianSecondsToDecision: null, slaComplianceRate: null, medianReplySeconds: null })))`. Shows "No decisions or replies in this period.", "Reply SLA compliance: —" and "Median time to decision: —". |
| R3 | `it('shows an error and retries')` | The first response is 500 (`http.get('*/api/dashboard/my-stats', …)`). "Could not load My reviews and replies" appears with a Retry button. After `server.resetHandlers()` and a click, "Approved: 12" appears. |
| R4 | `it('renders in Arabic right to left')` | `lng: 'ar'`: the region «مراجعاتي وردودي» shows «معتمد: 12». `document.documentElement` has `dir="rtl"`. |
| R5 | `it('has no axe violations')` | After the data is visible, `axe(container)` has no violations. |

### Web — `TeacherStatsPage.test.tsx` (`describe('TeacherStatsPage')`)
| # | Test method | Asserts |
|---|-------------|---------|
| R6 | `it('shows the page heading and my stats card')` | `/teacher/stats` as a teacher: heading level 1 "My stats", and the region "My reviews and replies" contains "Approved: 12". The placeholder text is gone (`queryByText` of the shell `placeholder.body` English string is null). |
| R7 | `it('renders the page in Arabic')` | `lng: 'ar'`: heading «إحصائياتي» and «الالتزام بمهلة الرد: 92.5%». `dir="rtl"`. |

### Web — `ValidationQueuePage.stats.test.tsx` (`describe('ValidationQueuePage stats card')`)
| # | Test method | Asserts |
|---|-------------|---------|
| R8 | `it('shows my stats on the teacher home')` | `/teacher`: heading "Review queue", and the region "My reviews and replies" contains "Approved: 12" and "Reply SLA compliance: 92.5%". |
| R9 | `it('keeps the queue usable when my stats fail')` | `my-stats` returns 500. The region shows "Could not load My reviews and replies", and the queue list "Questions waiting for review" still renders. |

Mutation checks the implementer must run and report (break, see red, restore):
- H2/H4: drop `teacherId` (pass `null`).
- C1: make the query implement `IDashboardRangeQuery`.
- R2: remove the `idle` note.
- R8: remove `<TeacherStatsCard />` from `ValidationQueuePage`.

## Definition of done
- [ ] `GET /api/dashboard/my-stats` exists with `[Authorize(Policy = DefaultCodes.TeacherStatsViewOwn)]`, `Name = "GetMyTeacherStats"` and `ProducesResponseType<MyTeacherStatsResult>`.
- [ ] The handler takes the teacher id only from `ICurrentUserService.UserId` and passes `subjectId: null, teacherId` to both repository calls. The query has no teacher or subject id.
- [ ] `GetMyTeacherStatsQuery` implements `IDashboardRange` and not `IDashboardQuery` (so it is not cached). C1 passes.
- [ ] `IDashboardRange` was added, and `IDashboardRangeQuery` and `DashboardFilterRules` changed exactly as specified. Every existing dashboard test passes unmodified.
- [ ] No new error codes, entities, migrations or repository methods.
- [ ] `api/openapi/v1.json` and `web/src/shared/api/generated` are regenerated with no drift (`dotnet build`, `npm --prefix web run gen:api`).
- [ ] `dotnet test api/ -c Release` passes with `appsettings.json` moved aside (CI parity), including the H1–H5, V1–V3, C1 and I1–I6 rows.
- [ ] `TeacherStatsCard` renders on `/teacher` above the filters and on `/teacher/stats`. `PlaceholderPage` is no longer used by `routes/teacher/stats.tsx`.
- [ ] The card shows loading (skeleton with `aria-busy`), success, idle (the empty note and `—`) and error with retry. Digits are Latin.
- [ ] The `teacherStats` namespace is registered lazily by `TeacherStatsCard.tsx` / `TeacherStatsPage.tsx` and is not in `app/i18n.ts`. Both `ar` and `en` files have identical key sets.
- [ ] `MetricCard` has an `ns` prop defaulting to `'dashboard'`, and the admin dashboard tests pass unmodified.
- [ ] `npm run build` then `npm run perf:budget` passes, with the new `teacher-home` entry set to measured × 1.05 rounded up to 5 KB. In `dist/.vite/manifest.json`, the static closure of `src/routes/teacher/index.tsx?tsr-split=component` does not include `src/features/dashboard/pages/DashboardPage.tsx`; the implementer reports the check.
- [ ] Web: `npx tsc -b --noEmit`, `npx eslint . --max-warnings=0` and `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` are clean. `npx vitest run --coverage` passes, including R1–R9. No existing test was edited except the listed addition to `DashboardCacheBehaviourTests`.
- [ ] Postman has "Get my teacher stats" as the last request in `TeacherInbox`.
- [ ] `docs/dashboard.md`, `docs/PRD.md` §8.4, `docs/claude-design-prompt.md` §4 and `docs/performance.md` §3–§4 are updated as specified.
- [ ] Every file created is in the tables above, and no others.
