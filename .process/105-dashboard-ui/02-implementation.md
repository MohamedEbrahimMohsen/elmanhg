# Implementation — [E11.S2] Dashboard UI (#105)

Worktree `D:/Personal/elmanhg-wt/105`, branch `feature/105-dashboard-ui`. Nothing committed.

## Files created
All under `web/src/`.

| Path | Lines | Purpose |
|---|---|---|
| `features/dashboard/index.ts` | 2 | Barrel (`DashboardPage`, `dashboardSearchSchema`, `DashboardSearch`) |
| `features/dashboard/locales.ts` | 4 | `dashboardLocales = { ar, en }` |
| `features/dashboard/i18n/en.json` | 119 | English strings (plan text, reflowed by prettier) |
| `features/dashboard/i18n/ar.json` | 119 | Arabic strings, same keys |
| `features/dashboard/api/dashboardRange.ts` | 32 | Cairo "today" and the 7/14/30-day `from`/`to` range (D3) |
| `features/dashboard/api/metricFormat.ts` | 51 | Formatters for count, rate, ratio, elapsed time, amount and day, all with `'latin'` digits (D8, D15) |
| `features/dashboard/schemas/dashboardSearchSchema.ts` | 8 | URL search schema `days` / `subjectId` (D4) |
| `features/dashboard/hooks/useDashboardFilters.ts` | 49 | Reads and writes the URL filters; derives range and params |
| `features/dashboard/components/MetricCard.tsx` | 44 | Card shell: its own skeleton, error with retry, and data render-prop |
| `features/dashboard/components/KpiFigure.tsx` | 19 | Headline value, caption, note and detail list |
| `features/dashboard/components/DashboardFilters.tsx` | 78 | Subject and period selects, plus the resolved range line |
| `features/dashboard/components/StudentsCard.tsx` | 34 | KPI card |
| `features/dashboard/components/SubscribersCard.tsx` | 41 | KPI card |
| `features/dashboard/components/ContentCard.tsx` | 52 | KPI card with the questions-by-type list |
| `features/dashboard/components/SolveRateCard.tsx` | 27 | KPI card |
| `features/dashboard/components/SuccessRateCard.tsx` | 31 | KPI card |
| `features/dashboard/components/ValidationCard.tsx` | 37 | KPI card |
| `features/dashboard/components/AskTeacherCard.tsx` | 38 | KPI card |
| `features/dashboard/components/PaymentsCard.tsx` | 39 | KPI card |
| `features/dashboard/components/FunnelCard.tsx` | 41 | KPI card |
| `features/dashboard/components/DailyBarChart.tsx` | 98 | Plain SVG bar chart with an sr-only table and an empty state (D1, D9–D12) |
| `features/dashboard/components/DashboardCharts.tsx` | 61 | The three chart panels (they reuse the KPI query keys) |
| `features/dashboard/components/SuccessRateBreakdown.tsx` | 90 | Subjects / Units / Lessons toggle and table (D14) |
| `features/dashboard/pages/DashboardPage.tsx` | 56 | The page |
| `test/dashboardFixtures.ts` | 198 | Fixture factories, `dashboardSubjects`, `dashboardHandlers()` |
| `features/dashboard/api/dashboardRange.test.ts` | 24 | Tests 1–5 |
| `features/dashboard/api/metricFormat.test.ts` | 53 | Tests 6–13 |
| `features/dashboard/schemas/dashboardSearchSchema.test.ts` | 25 | Tests 14–17 |
| `features/dashboard/components/DailyBarChart.test.tsx` | 69 | Tests 18–23 |
| `features/dashboard/components/SuccessRateBreakdown.test.tsx` | 69 | Tests 24–29 |
| `features/dashboard/pages/DashboardPage.test.tsx` | 119 | Tests 30–38 |
| `features/dashboard/pages/DashboardPage.cards.test.tsx` | 66 | Tests 39–42 |
| `features/dashboard/pages/DashboardPage.filters.test.tsx` | 133 | Tests 43–48 |

That is 33 files, exactly the plan's list. No new dependency, and `package.json` / `package-lock.json` are unchanged.

## Files modified
| Path | Change |
|---|---|
| `web/src/routes/admin/index.tsx` | Replaced the placeholder with `validateSearch: dashboardSearchSchema, component: DashboardPage` (verbatim from the plan) |
| `web/src/app/i18n.ts` | Imports `dashboardLocales`; `dashboard` added to both `resources` and `ns` (after `avatarConversations`) |
| `web/src/test/msw/server.ts` | Adds the default handlers `...dashboardHandlers()` and `getGetSubjectsMockHandler([])` |
| `web/scripts/perf/budgets.json` | Adds the `admin-dashboard` page (3 entries), `maxKb: 230` (measured 219 × 1.05 = 229.95, rounded up to 230) |
| `docs/claude-design-prompt.md` | §4 `#/admin` bullet replaced with the plan's exact text |
| `docs/performance.md` | §3 row `admin-dashboard` 219 / 230; §4 bullet "Dashboard charts without a library" |
| `docs/dashboard.md` | New first bullet under `## Filters`: "The admin page …" |
| `web/src/routeTree.gen.ts` | `npm run build` left it unchanged, as the plan expected |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| #9 `MetricCard` prop `query: UseQueryResult<T>` | Orval hooks return `UseQueryResult<T, unknown>`. The default `TError = Error` fails `tsc` (TS2322 on every card). | Typed the prop as `UseQueryResult<T, unknown>`. `ContentErrorState` already takes `error: unknown`. |
| #21 `<svg dir="ltr" …>` | `dir` does not exist on `SVGProps<SVGSVGElement>`, so `tsc` fails (TS2322). | Dropped `dir` from the `<svg>`. SVG user-space coordinates are never mirrored by the CSS direction, so the bars still run oldest→newest left→right. The axis row `<div dir="ltr">` keeps `dir` as planned, so D9 still holds. |
| #8 `const { subjectId: _removed, ...rest } = previous` | ESLint `@typescript-eslint/no-unused-vars` rejects `_removed`. | Used the plan's own fallback: `const next = { ...previous }; delete next.subjectId;` |
| #25 fixtures are one object literal per factory | Prettier expanded the literals, and the file hit 214 lines, over the 200-line limit (DoD). | Added two private helpers, `scoped` (`range` + `subjectId: null`) and `group(...)` for success-rate rows. Values are unchanged; the file is now 198 lines. |
| DoD: `entry`/`landing`/`lesson`/`quiz` sizes "unchanged from `docs/performance.md` §3 (±1 KB)" | The docs' §3 numbers were already stale on the base commit. With my change stashed, the measurements are entry 201 / landing 210 / lesson 230 / quiz 249, against docs 197 / 205 / 224 / 242. With the change they are 203 / 211 / 232 / 251. The +1–2 KB comes from the dashboard locale JSON: the plan registers it eagerly in `app/i18n.ts`, which is the pattern every feature uses. | Kept the plan's i18n wiring. All budgets still pass. I did not rewrite the stale measured values in the existing §3 rows, because the plan only adds the new row. Flagged for review. |
| Test 22 "text containing سبتمبر in the axis row" | A regex query also matches each SVG `<title>`, so the count was 6, not 4. | Asserted the exact axis/table text instead: `29 سبتمبر` appears twice (axis and table cell) and also appears inside the table. The test name is unchanged. |

## Build & test
All commands were run in `web/` on Windows. `npm install` ran first because `node_modules` was missing.

- `npx tsc -b`: exit 0, no output.
- `npx eslint . --max-warnings=0`: exit 0.
- `npx prettier --check "src/**/*.{ts,tsx,json,css}" "scripts/**/*.{ts,json}" --end-of-line auto`: "All matched files use Prettier code style!"
- `npx vitest run --coverage`: exit 0. `Test Files 206 passed (206)`, `Tests 1195 passed (1195)`. `All files | 95.39 | 84.04 | 91.97 | 95.54`. `dashboard/api` lines 100 / branches 92.85; `dashboard/components` lines 100 / branches 79.48. The `src/features/**` thresholds (80 lines / 70 branches) are met.
- Dashboard suite alone: `Test Files 8 passed (8)`, `Tests 48 passed (48)`.
- `npm run build`: exit 0 (the chunk-size warning was already there). `routeTree.gen.ts` is unchanged.
- `npm run perf:budget`: exit 0. Output: `entry 203/210 KB ok`, `landing 211/220 KB ok`, `lesson 232/240 KB ok`, `quiz 251/255 KB ok`, `admin-dashboard 219/230 KB ok`.
- `npm run gen:api`: exit 0, no diff in `src/shared/api/generated`.
- DoD greps on `features/dashboard` found nothing: no hex, arbitrary values, physical-direction utilities, `style=`, `useMemo`/`useCallback`, default export, `any`, or `!`. `formatNumber(` / `formatDate(` / `formatMoney(` appear only in `metricFormat.ts`.

Mutation check: 8 mutants were run against the new tests, one at a time, each reverted afterwards. 7 were killed:
- `dashboardRange` `days - 1` → `days`: 3 tests failed.
- `contentParams` without the subject: 2 failed.
- Axis first-day label removed: 2 failed.
- `refetch` removed from Retry: 1 failed.
- Students "not filtered" note removed: 1 failed.
- Lessons "choose a subject" gate removed: 1 failed.
- Period `onChange` guard broken: 1 failed.

1 survived: switching `formatRate` to `'arabic-indic'` digits. No planned test formats a rate in Arabic; test 7 covers counts only, and test 37 checks only the Students count.

## Notes for review
- **Surviving mutant:** Arabic digit style for rates, ratios, amounts and days is only partly covered. Counts and elapsed times are covered; rates are not. The test plan is fixed, so I added no test.
- **Test 48** ("subjects fail to load") has no observable signal that the subjects request has already failed when it asserts one option. The request runs in parallel with the cards, so it typically settles first, but the assertion would also pass while the request is still pending. This follows D5: a failure is deliberately invisible.
- **Test 46** uses the subject-conditional content handler (300 with Physics, 870 without), so switching to "All subjects" is observable. The plan's text only mentions the final 870.
- **Branch-coverage misses:** the `i18n.resolvedLanguage ?? i18n.language` fallback, and the `overdueNow = 0` class branch in `AskTeacherCard`. Neither is required by the plan.
- **Select height:** the selects use `h-9`, as the plan says. Sibling selects (`SubjectPicker`, `PaymentLogSelectField`) use `h-11`.
- **`docs/performance.md` §3:** the measured values for the existing rows (197 / 205 / 224 / 242) are stale against main. That was true before this change; see Deviations.
- **Other docs:** `docs/prototype.md` line 40 ("Dashboard cards are computed live and can be filtered by subject and period") already agrees, so no edit was needed.
