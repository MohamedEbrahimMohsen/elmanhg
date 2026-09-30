VERDICT: CHANGES_REQUESTED

# Review — [E11.S2] Dashboard UI (#105)

Diff reviewed: working tree vs `HEAD` (dce990d) in `D:/Personal/elmanhg-wt/105`. Web only; no API change, so Postman sync is not applicable.

## Blocking

### 1. Admin-only dashboard strings ship eagerly on every student page (+~2 KB br on entry, landing, lesson and quiz)
**Where:** `web/src/app/i18n.ts:11`, `web/src/app/i18n.ts:58`, `web/src/app/i18n.ts:81`, `web/src/app/i18n.ts:125`
**Rule:** plan Definition of done ("`entry`, `landing`, `lesson` and `quiz` measured sizes are unchanged … (±1 KB)"); skill §18 (route-level splitting); `docs/performance.md` §1/§5 (lesson p75 < 2000 ms not met, #220).
**Problem:** `dashboardLocales` (ar + en, 8,038 B minified JSON, 2,175 B brotli-11 alone) is imported statically into `app/i18n.ts`, so it lands in the entry chunk every student route loads. Implementer measured entry 201→203, landing 210→211, lesson 230→232, quiz 249→251 KB br; my `perf:budget` re-run matches (203 / 211 / 232 / 251, all under budget). The strings are only read on `/admin/` (Admin role). The only justification offered is "every feature does it", which is consistency with a pattern, not a reason to add admin-only bytes to the page that already misses its 2 s target. The growth is small but neither negligible (≈1 % of the lesson critical path, recurring on every cold student load) nor justified, and it breaks the plan's own ±1 KB DoD line.
**Failure:** a student on 3G opening `/student/lesson/:id` cold downloads ~2 KB of admin dashboard copy it can never display; the `lesson` critical path is 232 KB instead of 230 KB.
**Fix:** drop the `dashboard` import, both `resources` entries and the `ns` entry from `app/i18n.ts`. Register the namespace from the admin route chunk before the page renders, e.g. a `loader`/`beforeLoad` on `routes/admin/index.tsx` that awaits `import('@/features/dashboard/locales')` and calls `i18n.addResourceBundle(lng, 'dashboard', …, true, true)` for `ar` and `en` (or a `registerDashboardLocales()` exported by the feature, called at module scope of the page chunk; keep the route file thin, skill §1). Re-run `npm run build && npm run perf:budget`, confirm entry/landing/lesson/quiz return to 201 / 210 / 230 / 249, update the `admin-dashboard` row, and add one §4 line to `docs/performance.md` for the new pattern.

### 2. D8 (ASCII digits in Arabic) is unconstrained for rates, ratios and amounts; the reported mutant survives
**Where:** `web/src/features/dashboard/api/metricFormat.ts:14-18`, `:20-22`, `:45-47`; tests `web/src/features/dashboard/api/metricFormat.test.ts:10-15` (Arabic: counts only) and `web/src/features/dashboard/pages/DashboardPage.test.tsx:543-549` (Arabic: Students count only).
**Rule:** plan D8 and DoD ("Every number, date and amount uses `'latin'` digits"); design-system "Latin (`ar-EG-u-nu-latn`) in admin tables … never mixed within one string"; testing convention: a test must fail when the code is wrong.
**Problem:** the implementer's own mutation run shows `formatRate` switched to `'arabic-indic'` passes all 48 tests. `formatRatio` and `formatAmount` are equally unguarded in Arabic. `formatCount`, `formatElapsed` and `formatDay` are covered.
**Failure:** change `'latin'` to `'arabic-indic'` in `formatRate` → an Arabic admin sees «٧٥٪» beside «1,250», and «الالتزام بالمهلة» mixes digit systems with the rest of the card; the suite stays green.
**Fix:** add Arabic assertions (Latin text plus `not.toMatch(/[٠-٩]/u)`) for `formatRate(0.75,'ar')`, `formatRatio(3.25,'ar')` and `formatAmount({ amountMinor: 895500, currency: 'EGP' },'ar')` in `metricFormat.test.ts` (and/or assert the Success rate and Payments regions in the Arabic page test). Re-run the `formatRate` mutant and report it killed.

### 3. Filter selects are 36 px tall; the design system fixes Select height at 44
**Where:** `web/src/features/dashboard/components/DashboardFilters.tsx:17` (`h-9`)
**Rule:** `.claude/design-system.md` components table, "Input / Select / Textarea … height 44"; reviewer rule: every visual value comes from the design system.
**Problem:** every other `<select>` in `src/features` uses `h-11` (e.g. `askTeacher/components/LessonPicker.tsx:10`, `blueprints/components/SubjectPicker.tsx:25`); only the dashboard deviates. The 36 px exception in the design system is for `sm` buttons in dense admin tables, not selects. The plan's `h-9` does not override the design system; the implementer flagged the mismatch but kept it.
**Failure:** on `/admin` both filter selects render at 36 px, shorter than every other select in the admin area and below the component spec.
**Fix:** `h-9` → `h-11` in `selectClassName`.

## Non-blocking
- `web/src/features/dashboard/components/DailyBarChart.tsx:74-78`: the whole axis row is `dir="ltr"`, so in Arabic the labels that contain Arabic are laid out LTR: «الأعلى: 2,800» reads value-first to an RTL reader, and «29 سبتمبر» shows as «سبتمبر 29». Keep the row LTR (oldest on the left) but isolate each label (`<bdi>` or `dir="auto"` on the three spans).
- `web/src/features/dashboard/pages/DashboardPage.filters.test.tsx:430-438`: test 48 can pass while `/api/subjects` is still pending (the implementer noted this). A handler spy or `waitFor` on the request having been answered would make it constrain D5.
- `web/src/features/dashboard/components/SuccessRateBreakdown.tsx:54-84`: skill §17 says tables become a card list under `md`. Four narrow columns in `overflow-x-auto` are acceptable here; align it when a shared pattern exists.
- `web/src/features/dashboard/hooks/useDashboardFilters.ts:25`: `now` is frozen at mount, so a tab left open past Cairo midnight keeps yesterday's range until reload. Fine for v1.
- `docs/performance.md` §3: the measured column for entry/landing/lesson/quiz (197/205/224/242) was already stale on the base commit. This change did not cause it, but the re-measure for finding #1 is a good time to refresh it.

## Verified
- My re-runs in `web/`: `npx tsc -b` exit 0; `npx eslint . --max-warnings=0` exit 0; `npx prettier --check "src/**/*.{ts,tsx,json,css}" "scripts/**/*.{ts,json}" --end-of-line auto` clean; `npm run build` exit 0 with `routeTree.gen.ts` unchanged; `npm run perf:budget` exit 0 (entry 203/210, landing 211/220, lesson 232/240, quiz 251/255, admin-dashboard 219/230); `npx vitest run --coverage` on an idle machine: 206 files, 1195 tests passed, all-files 95.35 / 84.04 / 91.91 / 95.5. My first full run failed 4 tests (two AvatarDock lazy tests, one StudentHomePage plan test, dashboard test 34) only because I built in parallel: 1,600 s timeouts. All 4 pass in isolation and in the clean re-run.
- All 33 planned files exist and nothing extra was created. Only the listed existing files changed. `package.json` and `package-lock.json` are unchanged.
- The deviations are real and justified. `UseQueryResult<T, unknown>` matches Orval's error type. `dir` was dropped from `<svg>` because it is not in `SVGProps`; SVG user-space coordinates are never mirrored, so the bars still run oldest→newest from left to right. The `delete next.subjectId` fallback is the plan's own. The fixture helpers `scoped` and `group` keep the values identical. Test 22's assertion was tightened without renaming the test. The +2 KB growth was disclosed honestly (it is finding #1).
- The D3 range maths is correct (`dashboardRange.ts:21-31`, with the Cairo DST cases tested). D4: the URL schema drops invalid values. D6: the no-subject note shows on Students, Subscribers, Payments, Funnel and the revenue chart, and Content shows the snapshot note. D7: each card owns its query, and the charts and breakdown reuse the same keys. `subjectId` goes only to content, solve-rate, success-rate, validation and ask-teacher (`DashboardPage.tsx:36-44`). D19: the headline values are as planned.
- Chart accessibility (D11): the SVG is `aria-hidden`, each bar has a `<title>`, and an `sr-only` table carries the data with a caption and `th scope="col"`. Axe passes on the chart, the breakdown and the whole page. The breakdown toggle uses `aria-pressed` buttons with visible focus rings.
- Design tokens: every class resolves to a theme token (`text-stat`, `text-micro`, `shadow-1`, `bg-soft`, `fill-text-muted`, `stroke-border-strong`, and `text-danger` for overdue, which the design system lists). The §14/§16 greps find no hex, arbitrary values, physical-direction utilities or inline styles. The en and ar key sets are identical (92 keys). No `useMemo`/`useCallback`, `any`, `!` or default export.
- Docs: `docs/claude-design-prompt.md` §4, `docs/performance.md` §3/§4 and `docs/dashboard.md` Filters were edited as planned and agree with the code. PRD §10.3 (date filter, subject dimensions, Content snapshot) and `docs/prototype.md` line 40 agree. No divergence.

## Test quality
- `dashboardRange.test.ts` and `dashboardSearchSchema.test.ts` constrain the logic: DST, month and year boundaries, and dropping invalid values.
- `metricFormat.test.ts` constrains English formatting and the Arabic counts, durations and days. It does **not** constrain Arabic rates, ratios or amounts (finding #2).
- `DailyBarChart.test.tsx` and `SuccessRateBreakdown.test.tsx` assert through the sr-only table and `aria-pressed`, so they fail on wrong data, wrong order or a broken gate.
- `DashboardPage*.test.tsx`: the MSW handlers answer differently per `from`, `to` and `subjectId`, so the filter tests really do constrain the params. The retry test uses a `once` 500. Test 48 is weak (see non-blocking).
