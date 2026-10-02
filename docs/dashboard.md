# Admin dashboard metrics

The admin dashboard (PRD §10.3) reads one endpoint per card under `GET /api/dashboard/*`. This document is the contract for every number on it: the filters, the exact definition of each field, how activity is tracked for DAU/MAU, and the cache. #105 draws the cards; #107's teacher stats card reuses the validation and reply statistics for the signed-in teacher (see Teacher personal stats).

## Filters

- **The admin page** (`/admin`, #105) always sends both `from` and `to`: `to` is today in Africa/Cairo computed in the browser, `from` is `to` minus the chosen period (7, 14 or 30 days, default 14) plus one. The subject filter adds `subjectId` only to the five cards that accept it.
- **Days are Cairo days.** `from` and `to` are dates (`yyyy-MM-dd`) in `Dashboard:TimeZone` (Africa/Cairo), both inclusive. The range covers `[start of from, start of the day after to)` as instants. Local midnight is computed without `ConvertTimeToUtc`, so the spring-forward day (midnight does not exist in Cairo) still has a start.
- **Defaults.** A missing `to` is today. A missing `from` is `to` minus `Dashboard:DefaultRangeDays − 1` (30 days including `to`).
- **Validation** runs on the resolved range: `from` after `to` returns 422 `DASHBOARD_DATE_RANGE_INVALID`; more than `Dashboard:MaxRangeDays` (366) days returns 422 `DASHBOARD_DATE_RANGE_TOO_WIDE`.
- **Subject.** `subjectId` is accepted by Content, Solve rate, Success rate, Validation and Ask a Teacher. An unknown id returns 404 `SUBJECT_NOT_FOUND`. Students, Subscribers, Payments and the Sign-up funnel have no subject dimension and take no `subjectId`.
- **Content** is a current snapshot: it takes `subjectId` only, no range.
- Every result carries `generatedAt` (the server time the numbers were computed), so the UI can show freshness under caching.

Rates are 0–1 decimals rounded to 4 places (solve rate: 2 places), midpoint away from zero, and `null` when the denominator is 0. Durations are whole seconds, `null` when there is nothing to measure. Money is `{ amountMinor, currency }` in `Subscriptions:Currency`.

## Cards

### Students — `GET /api/dashboard/students?from&to`

| Field | Definition |
|---|---|
| `total` | Users with role Student (not deleted). |
| `newInRange` | Students whose account was created inside the range. |
| `newThisWeek` | Students created in the last `Dashboard:RecentWeekDays` (7) Cairo days including today. Ignores the range. |
| `activeToday` | DAU: distinct students with an activity row for today. |
| `activeThisMonth` | MAU: distinct students with an activity row in the last `Dashboard:RecentMonthDays` (30) days including today. Ignores the range. |
| `dailyActive` | Active students per day of the range, every day present (0 when none). |

### Subscribers — `GET /api/dashboard/subscribers?from&to`

| Field | Definition |
|---|---|
| `activeByPlan` | Subscriptions with status Active or PastDue (still entitled), per plan; both plans always listed. |
| `activeSubscriptions` | Sum of `activeByPlan`. |
| `churnedInRange` | Subscriptions Cancelled or Expired whose `CancelledAt ?? ExpiredAt` falls in the range. |
| `churnedThisMonth` | The same over the last 30 days up to now. Ignores the range. |
| `monthlyRecurringRevenue` | For every active subscription, its latest Succeeded payment's amount ÷ that payment's period months, summed and rounded to a whole minor unit. A subscription without such a payment (complimentary) adds 0. |

### Content — `GET /api/dashboard/content?subjectId`

| Field | Definition |
|---|---|
| `subjects` | Subject count (1 when `subjectId` is given). |
| `units` | Units in scope. |
| `lessonsDraft` / `lessonsPublished` / `lessonsArchived` | Lessons in scope per state. |
| `questionsPending` / `questionsApproved` / `questionsRejected` | Non-retired questions per validation status. |
| `questionsRetired` | Every retired question (retirement is final and not a validation status). |
| `questionsByType` | All questions per type, every type listed. |
| `servableTotal` | `ServableQuestionSpecification`: Approved, lesson Published, not retired. The marketed number. |

### Solve rate — `GET /api/dashboard/solve-rate?from&to&subjectId`

| Field | Definition |
|---|---|
| `attempts` | Attempts in the range: session not in admin test mode, question not deleted, all session kinds. `subjectId` filters by the question's subject. |
| `activeStudentDays` | Sum over the range of daily active students (activity has no subject, so `subjectId` does not filter it). |
| `attemptsPerActiveStudentPerDay` | `attempts ÷ activeStudentDays`, 2 places. |
| `daily` | Per day: `attempts`, `activeStudents`. |

### Success rate — `GET /api/dashboard/success-rate?from&to&subjectId`

The same attempts as Solve rate. An attempt is correct when its normalised score is at least `Mastery:CorrectThreshold` (0.8).

| Field | Definition |
|---|---|
| `attempts`, `correct`, `rate` | Overall. |
| `bySubject` / `byUnit` / `byLesson` | The same per group, only groups with at least one attempt, in curriculum order (subject, unit, lesson `Order`, then name). `parentId` is the subject for a unit and the unit for a lesson. |

### Validation — `GET /api/dashboard/validation?from&to&subjectId`

| Field | Definition |
|---|---|
| `pendingBacklog` | Pending, non-retired questions right now. |
| `approved` / `rejected` | Decisions in the range. |
| `medianSecondsToDecision` | Median of `DecidedAt − SubmittedAt` over decisions in the range. `SubmittedAt` is copied onto the decision when it is made, so resubmissions do not rewrite history. |
| `byTeacher` | Decisions in the range per deciding teacher, highest total first, then display name. |
| `dailyDecisions` | Decisions per day. |

### Ask a Teacher — `GET /api/dashboard/ask-teacher?from&to&subjectId`

| Field | Definition |
|---|---|
| `openThreads` | Threads not Closed, now. |
| `awaitingReply` | Threads Open (waiting for a teacher), now. |
| `overdueNow` | Open threads whose SLA deadline has passed, now. |
| `slaBreaches` | Breach events recorded by the SLA sweep in the range. |
| `replies` | Teacher messages sent in the range. |
| `repliedWithinSla` | Replies whose SLA window has no Breach event (the SLA sweep records breaches over the reply calendar; a reply less than one sweep interval late counts as within). The window is the latest earlier student message in the thread, so a follow-up starts a new one (`docs/ask-teacher.md`). |
| `slaComplianceRate` | `repliedWithinSla ÷ replies`. |
| `medianReplySeconds` | Median wait over those replies. |

### Payments — `GET /api/dashboard/payments?from&to`

| Field | Definition |
|---|---|
| `succeeded` | Payments completed in the range with status Succeeded or Refunded (a refunded payment still succeeded on the day it was paid). |
| `failed` | Payments completed in the range with status Failed. |
| `revenue` | Sum of the succeeded payments. |
| `refunds` / `refunded` | Count and sum of payments refunded in the range (counted on the refund day). |
| `netRevenue` | `revenue − refunded`. |
| `revenueByDay` | Gross succeeded amount per Cairo day of completion, every day present. |

### Sign-up funnel — `GET /api/dashboard/funnel?from&to`

As `docs/analytics.md`: distinct visitors per step inside the range in step order, `conversionFromPrevious` (`null` for the first step and after a step with 0 visitors), `completedJourneys` (visitors with a landing and a first answer at or after it, both in the range) and the median seconds between them.

### Teacher personal stats — `GET /api/dashboard/my-stats?from&to`

The signed-in teacher's own numbers, shown on the teacher home and «إحصائياتي». The teacher id comes from the token only; there is no `teacherId` or `subjectId` parameter. It counts all of the caller's own decisions and replies in the range, whatever their current subject assignments. The web sends no range, so it gets the default 30 days.

| Field | Definition |
|---|---|
| `approved` / `rejected` | The caller's decisions in the range. |
| `medianSecondsToDecision` | As in Validation, over the caller's decisions. |
| `replies` | Teacher messages the caller sent in the range. |
| `repliedWithinSla` / `slaComplianceRate` / `medianReplySeconds` | As in Ask a Teacher, over the caller's replies. |

## Activity tracking (DAU/MAU)

- Table `UserActivityDays(Id, UserId, Day, FirstSeenAt)`: at most one row per user per Cairo day (unique index `IX_UserActivityDays_UserId_Day`).
- `UserActivityBehaviour` (MediatR pipeline) records a row after any request that succeeds for a signed-in user, and after login, registration and token refresh (the user is read from the `AuthResult`). Anonymous requests and failed requests record nothing.
- The write is `INSERT … ON CONFLICT DO NOTHING`, outside the request's unit of work. Each API instance remembers in memory which users it has recorded today, so repeat requests do not touch the database. A failed write is logged and never fails the request. The insert runs on the request-scoped `AppDbContext`; no handler opens an explicit transaction today, so a failed insert cannot abort one. A future transaction behaviour around handlers must run this write outside it.
- Rows are written for every role; the dashboard counts students only.

## Caching

Every card is cached in memory for `Dashboard:CacheSeconds` (60), keyed by card, range and subject as sent. There is no invalidation: a number can be up to a minute old, and a default-range entry may be served for up to a minute past midnight. `0` disables the cache (the test host does this).

The teacher stats card is not cached: the cache key comes from the request, which carries no caller, so caching would share one teacher's numbers with another.

## Endpoints

| Method | Route | Policy | Query |
|---|---|---|---|
| GET | `/api/dashboard/students` | `Dashboards.View` (Admin) | `from`, `to` |
| GET | `/api/dashboard/subscribers` | same | `from`, `to` |
| GET | `/api/dashboard/content` | same | `subjectId` |
| GET | `/api/dashboard/solve-rate` | same | `from`, `to`, `subjectId` |
| GET | `/api/dashboard/success-rate` | same | `from`, `to`, `subjectId` |
| GET | `/api/dashboard/validation` | same | `from`, `to`, `subjectId` |
| GET | `/api/dashboard/ask-teacher` | same | `from`, `to`, `subjectId` |
| GET | `/api/dashboard/payments` | same | `from`, `to` |
| GET | `/api/dashboard/funnel` | same | `from`, `to` |
| GET | `/api/dashboard/my-stats` | `TeacherStats.ViewOwn` (Teacher) | `from`, `to` |

## Config

| Key | Default | Meaning |
|---|---|---|
| `Dashboard:TimeZone` | `Africa/Cairo` | Day boundaries (IANA id, checked at start-up). |
| `Dashboard:CacheSeconds` | `60` | Card cache lifetime; `0` disables. |
| `Dashboard:DefaultRangeDays` | `30` | Range when `from` is missing (must not exceed `MaxRangeDays`). |
| `Dashboard:MaxRangeDays` | `366` | Longest accepted range. |
| `Dashboard:RecentWeekDays` | `7` | Window for "new this week". |
| `Dashboard:RecentMonthDays` | `30` | Window for MAU and "churned this month". |
