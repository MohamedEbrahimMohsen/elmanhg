VERDICT: CHANGES_REQUESTED

# Review — [E19.S2] Dashboard, table actions, dates and select polish (#280)

## Blocking

### 1. The design-system docs still give the old answer on row-action size and style
**Where:**
- Code: `web/src/features/exam/components/ExamAttemptsTable.tsx:59`, `web/src/features/exam/components/ExamLessonBreakdown.tsx:48`, `web/src/features/progress/components/UnitProgressTable.tsx:38,53` and `web/src/features/progress/components/SessionHistoryRow.tsx:44`. These are 36 px `sm` secondary row actions on student screens, at every width.
- Docs:
  - `.claude/design-system.md:163` (Accessibility): "Touch targets ≥44px; the only exception is the desktop top nav and app-bar sign-out".
  - `.claude/design-system.md:86` (Button): "36 for `sm` in dense admin tables". The same cell now also says "Row actions in every table are `sm`".
  - `docs/design-system.md:193` (§5.7): the top nav is "the only exception to principle 5". Principle 5 (line 17) now names a second exception.
  - `docs/design-system.md:158` (§5.1) and `docs/claude-design-prompt.md:94` (§2.4): the Ghost row still says "Inline actions in tables".

**Rule:** `.claude/rules/docs-sync.md` (divergence: UI rules; `docs/design-system.md` and `.claude/design-system.md` must agree); plan Docs table (D8).

**Problem:** The change makes `sm` secondary/danger the row-action style in every table, student tables included. The doc lines above were not updated, so they now contradict both the code and the new sentences next to them. The pipeline reads `.claude/design-system.md` as the token contract. A later planner or reviewer that reads line 163 would treat these 36 px student-table buttons as a target-size violation. One that reads the Ghost row would build table actions as ghost buttons.

**Failure:** Ask "what size may a row action in the student exam-attempts table be?" `.claude/design-system.md:163` answers 44 px or more (the only exceptions are top nav and sign-out). The code renders 36 px, and `docs/design-system.md:17` says 36 px is allowed. Ask "what style is an inline table action?" `docs/design-system.md:158` says Ghost, while the new §5.1 sentence in the same file says secondary or danger `sm`.

**Fix:** Make these doc-only edits:
- In `.claude/design-system.md:163`, add the 36 px `sm` table row actions to the exceptions.
- In `.claude/design-system.md:86`, change "in dense admin tables" to "for table row actions".
- In `docs/design-system.md:193`, qualify "the only exception to principle 5".
- In the Ghost "Use" cell, change "Inline actions in tables" to inline text actions that are not table row actions. Do this in both `docs/design-system.md:158` and `docs/claude-design-prompt.md:94`.

## Non-blocking
- `web/src/shared/ui/button.tsx:9`: dropping `disabled:pointer-events-none` lets the `hover:` fills (`hover:bg-accent-hover`, `hover:bg-soft`, `hover:bg-danger-soft`) apply to disabled buttons. A disabled primary button now darkens on hover under a not-allowed cursor. Clicks are still blocked, because every disabled `Button` is a native `<button disabled>`. No `asChild` + `disabled` or `aria-disabled` use exists in `web/src`. Consider `enabled:hover:` later, but only if the quiz CSS budget allows it.
- `web/src/app/i18n.test.ts:19`: the test name still says "formats ICU numbers with Arabic-Indic digits in Arabic", but it now asserts Latin digits. The plan limited the change to line 22, and the implementer flagged this.
- `web/src/features/dashboard/i18n/ar.json` (`tiles.studentsDetail`, `tiles.subscribersDetail`): the leading plus or minus sign before an interpolated number in an RTL paragraph resolves RTL, so it renders on the visual right of the digits. Consider an LRM or `<bdi>` around the signed number.
- `web/src/features/dashboard/pages/DashboardPage.cards.test.tsx:30-71`: the `toHaveTextContent('80')`, `('3')` and `('2')` checks are substring matches, so 180 or 13 would also pass. Use exact strings or anchored regexes.
- `web/src/features/progress/components/UnitProgressTable.tsx:38`: the unit name, the row's identifying cell, is now a secondary pill button, as the plan said. Similar name links in `UserListRow.tsx:41` stay text links. This is a product call, not a defect.
- `docs/backlog.json:28`: an old E1 sub-task reads "Arabic-Indic digit formatting". It is historical work-log text, not a current rule. Optional tidy.
- Quiz headroom is 27 B (261093/261120) and lesson is 254 B. The build is deterministic (see Verified), but the next byte on the quiz path will trip CI.

## Verified
- **Digit policy:**
  - `numberLocale` returns `ar-EG-u-nu-latn` or `en-US` (`web/src/shared/lib/format.ts:1-3`). `formatNumber(value, lng, options?)`.
  - ICU uses `parseLngForICU: numberLocale` (`web/src/app/i18n.ts:98`).
  - Greps over `web/src`: no `ar-EG` without the latn extension, no `toLocale*String` in production code, no `DigitStyle` or `arabic-indic`, no `\u066x` or `\u06Fx` escapes. `Intl.*Format` appears only in `dateTime.ts`, `format.ts`, `money.ts` and `dashboardRange.ts` (Cairo-day math, not displayed).
  - Arabic-Indic literals remain only in tests, in `convertDigits` (kept per D3) and in `mathSteps/api/mathStepsValue.ts:24`. That input normaliser is untouched, so typed digits are still normalised.
  - The landing counter uses `{count, number}` via ICU.
- **Dates:**
  - One formatter, `web/src/shared/lib/dateTime.ts`, with all five styles and the locale joiner.
  - A date-only value is formatted in UTC. `ExamPeriodsSection` still renders "Jun 1, 2026 – Jul 15, 2026".
  - `<DateTime relative>` shows relative text when 0 <= age < 24 h, with the full date in `title`. It is used in AuditLogRow, AvatarConversationRow, SessionHistoryRow and StudentHistoryTable.
  - `pendingAge.ts` and its test are deleted (staged). ValidationQueueItem and GradeReviewListItem use `formatRelativeTime`.
  - Unit tests build local-time dates. Page tests use UTC fixtures at 09:00-10:00Z, which are stable for any TZ from UTC-9 to UTC+14. CI sets `TZ: UTC`.
- **Dashboard:**
  - Every tile and panel row binds the field the API record defines (`StudentMetricsResult`, `SubscriberMetricsResult`, `PaymentMetricsResult`, `ContentMetricsResult`, `AskTeacherMetricsResult`, `SuccessRateMetricsResult`, `FunnelMetricsResult`, checked against `api/Elmanhg.Application/Dashboard`). Every metric the PRD §10.3 table names is still on the page.
  - `from`/`to` are `DateOnly`, so `fillDailySeries` gets `YYYY-MM-DD`.
  - Charts:
    - Bars are `fill-accent`, with gridlines at max and half and a baseline at 0.
    - The scale comes from `niceCeiling`, with at most 4 date labels anchored on the latest day and a tick for every day.
    - There is a total/peak line, `dir="ltr"`, and `aria-hidden` on the visual layer.
    - Text alternatives: the sr-only table is kept and every day has a `<title>`. The empty state is `h-40`.
  - No new dependency.
  - Not done: a live comparison of tile values with the running demo API. It needs admin credentials, which I did not have.
- **Select:**
  - `<select` outside tests appears only in `web/src/shared/ui/select.tsx`. All 16 selects are migrated, and their props (`ref`, `name`, `onBlur`, `disabled`, `aria-*`, the RHF `register` spread) are unchanged.
  - Placement: `pe-10` with the chevron in an `end-0` box, so it sits at inline-end in both directions, not mirrored. Built CSS has `.pe-10` after `.px-3`, and `.pe-10` is the only new utility.
- **Cursor:** the built CSS contains `button:enabled,select:enabled,[role=button]{cursor:pointer}:disabled{cursor:not-allowed}`. `danger` is `border border-border-strong bg-surface text-danger hover:bg-danger-soft`.
- **Row actions:** all five converted links are `Button asChild variant="secondary" size="sm"`. The existing row actions (PaymentLogRow, QuestionRow, TrainingExportRow, UserActionButtons) were already `sm` secondary or danger. The `UserListRow` actions wrapper uses `items-center`.
- **Budgets:**
  - I ran `npm run build` + `npm run perf:budget` twice. Both exited 0: entry 209/210, landing 218/220, lesson 240/240, quiz 255/255, admin-dashboard 233/233, admin-users 264/270, teacher-home 258/265.
  - Exact brotli bytes match the implementer: quiz 261093/261120, lesson 245506/245760, entry 213847/215040, admin-dashboard 237720/238592.
  - The two builds are byte-identical across all 578 `dist/assets` files (md5), and no built asset contains a CR. The local CRLF/LF mix therefore does not reach the output, and the same source gives the same bytes. The earlier "11 B over" came from a different intermediate source state, not from noise. CI on Linux should reproduce these bytes, but the margin is 27 B.
  - Only `admin-dashboard` `maxKb` changed (230 to 233, within 235). The `docs/performance.md` §3 row and the reason sentence were added.
- **Commands** (all in `web/`):
  - `npm run typecheck` exit 0.
  - `npm run lint` clean.
  - `npx prettier --check . --end-of-line auto` clean.
  - `npx vitest run --coverage`: 1694/1696 passed. The 2 failures were `findBy` timeouts in `ExamPeriodsSection.test.tsx` and `ConfigurationPage.test.tsx`. Rerun alone: 27/27 passed. No coverage threshold error.
  - Both CI literal-token and physical-direction greps found no match.
  - `gen:tokens`, `routeTree.gen.ts` and `generated/` show no diff.
- **Deviations:**
  - The `formatDuration.test.ts` change (now `'0:42'`) follows D1. The orchestrator authorised it.
  - The extra files in `git status` (AskTeacherListPage, AskTeacherNewPage, ExamBlueprintSummary, EssayGradeOutcome, FeedbackPanel, MathStepGradeOutcome) differ only in line endings and have no content diff against origin/main.
- **Postman:** no API change.

## Test quality
- `dateTime.test.ts`, `DateTime.test.tsx`, `money.test.ts` and `format.test.ts` constrain the implementation. They use exact strings in both languages, check the UTC date-only path, the 23 h / 25 h / future boundaries and title presence or absence, and include a negative Arabic-Indic check.
- `dailySeries.test.ts` constrains the implementation: month boundary, from after to, nice steps, and tick anchoring for 7/14/30/1/0.
- `DailyBarChart.test.tsx` constrains the implementation: sr-only rows, tick labels counted (2 vs 1), scale labels, the empty state removing the table, Arabic Latin days, and axe.
- `select.test.tsx` covers labelling, value change, disabled and aria-invalid pass-through, and axe in Arabic. Chevron placement is CSS-only and cannot be tested in jsdom.
- `DashboardPage*.test.tsx` mostly constrains the implementation: regions are named by tile and panel title, and definitions are found by their `dt` names. Weak spot: the substring `toHaveTextContent` checks in `DashboardPage.cards.test.tsx`.
- `ProgressPage.dates.test.tsx` and `UsersPage.dates.test.tsx` constrain the `<DateTime relative>` wiring and the `date` style end to end.
