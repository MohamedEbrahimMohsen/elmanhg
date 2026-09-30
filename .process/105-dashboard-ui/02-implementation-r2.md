# Implementation r2 (rework): [E11.S2] Dashboard UI (#105)

## Blocking findings addressed
| # | What I changed | file:line |
|---|---|---|
| 1 | I removed the `dashboard` import, both `resources` entries and the `ns` entry from `app/i18n.ts`, so the file is byte-identical to `HEAD` again. `features/dashboard/locales.ts` now exports `registerDashboardLocales()`. It calls `getI18n().addResourceBundle(lng, 'dashboard', …, true, true)` for `ar` and `en`. `DashboardPage.tsx` calls it at module scope, and that module is the auto-split `/admin/` component chunk. The bundle is registered synchronously when the chunk is evaluated, which is before the lazy component's first render, so keys never flash. The route file is unchanged and stays thin. The two standalone component tests register the bundle in `beforeAll`. The page tests go through the router's lazy chunk, so they exercise the real path; for example, the first-render assertion `Loading Students` passes. | `web/src/features/dashboard/locales.ts:5-9`; `web/src/features/dashboard/pages/DashboardPage.tsx:17-19`; `web/src/app/i18n.ts` (reverted); `DailyBarChart.test.tsx:5,26`; `SuccessRateBreakdown.test.tsx:9,15` |
| 2 | I added Arabic assertions inside the existing tests, with no new test names. `formatRate(0.75,'ar')` must contain `75` and match no `[٠-٩]`. `formatRatio(3.25,'ar')` must be exactly `'3.25'`. `formatAmount({amountMinor: 895500, currency:'EGP'},'ar')` must contain `8,955` and match no `[٠-٩]`. | `web/src/features/dashboard/api/metricFormat.test.ts` (rates, ratios and money tests) |
| 3 | `selectClassName` `h-9` → `h-11` | `web/src/features/dashboard/components/DashboardFilters.tsx:17` |
| NB | RTL axis labels: the row stays `dir="ltr"` (oldest on the left), and each of the three spans now has `dir="auto"`, so Arabic labels are laid out RTL. | `web/src/features/dashboard/components/DailyBarChart.tsx:75-77` |

### Mutation check (finding 2)
In `metricFormat.ts`, I switched `'latin'` to `'arabic-indic'` in one function at a time, ran `metricFormat.test.ts`, and then restored the file (a diff showed it identical):
- `formatRate` mutant: **killed**. `× formats rates as percents and null as a dash`, 1 failed / 7 passed.
- `formatRatio` mutant: **killed**. `× formats ratios with up to two decimals`, 1 failed / 7 passed.
- `formatAmount` mutant: **killed**. `× formats money from minor units`, 1 failed / 7 passed.

### perf:budget before / after (finding 1)
Exact brotli-11 bytes, summed with `pageFiles()` from `scripts/perf/bundleBudget.ts`. The base is `HEAD` with all `web/` changes temporarily stashed. r1 is from the `perf:budget` output.

| Page | Base (HEAD) | r1 (eager) | r2 (lazy) | r2 − base |
|---|---|---|---|---|
| entry | 205,642 B (201 KB) | 203 KB | 205,656 B (201 KB) | +14 B |
| landing | 214,070 B (210 KB) | 211 KB | 214,299 B (210 KB) | +229 B |
| lesson | 235,257 B (230 KB) | 232 KB | 235,535 B (231 KB) | +278 B |
| quiz | 254,162 B (249 KB) | 251 KB | 254,471 B (249 KB) | +309 B |
| admin-dashboard | n/a | 219 KB | 224,081 B (219 KB) | n/a |

The locale JSON is gone from every student page: entry is +14 B, which is minifier noise. The remaining +229 to +309 B has nothing to do with i18n. A per-file diff shows where it comes from:
- `index.css`: +57 B. The dashboard's Tailwind utilities go into the single global stylesheet.
- About +172 B: `MetricCard` reuses `ContentErrorState` from `@/features/content`. Rolldown therefore splits the shared chunk `ContentListSkeleton` (736 B) into `ContentErrorState` (583 B) plus `ContentListSkeleton` (325 B), and the extra chunk adds overhead.
- The rest (±30 B) is per-chunk minifier and hash noise.

`lesson` shows 231 KB and not 230 KB only because the base sits 22 B below a KB boundary.

## Files modified in r2
| Path | Change |
|---|---|
| `web/src/app/i18n.ts` | Reverted to `HEAD`, so there is no dashboard namespace in the entry |
| `web/src/features/dashboard/locales.ts` | Replaced the `dashboardLocales` export with `registerDashboardLocales()`, which calls `addResourceBundle` |
| `web/src/features/dashboard/pages/DashboardPage.tsx` | Module-scope `registerDashboardLocales()` |
| `web/src/features/dashboard/components/DashboardFilters.tsx` | `h-9` → `h-11` |
| `web/src/features/dashboard/components/DailyBarChart.tsx` | `dir="auto"` on the three axis labels |
| `web/src/features/dashboard/api/metricFormat.test.ts` | Arabic Latin-digit assertions for rate, ratio and amount |
| `web/src/features/dashboard/components/DailyBarChart.test.tsx`, `SuccessRateBreakdown.test.tsx` | `beforeAll(registerDashboardLocales)` |
| `docs/performance.md` | §3 measured values refreshed (entry 201, landing 210, lesson 231, quiz 249; admin-dashboard stays 219). §4 has a new bullet, "Admin-only strings on demand". |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Review #1: student bundles return to pre-change sizes within ±0.2 KB (205 B) | The i18n bytes are fully removed (entry +14 B). landing, lesson and quiz remain +229, +278 and +309 B above base. The cause is the global CSS (+57 B) plus a Rolldown chunk split caused by reusing `ContentErrorState` (about +172 B). | I left both in place. Copying the error state to avoid the split would duplicate a shared component, and CSS is a single global file. Removing either would need manual chunking, which is outside this rework. All budgets pass. |
| Plan: `dashboardLocales` export in `locales.ts` | Nothing reads it now | I replaced it with `registerDashboardLocales()` in the same file. No new files. |

## Build & test (web/, all run in r2)
- `npx prettier --check "src/**/*.{ts,tsx,json,css}" "scripts/**/*.{ts,json}" --end-of-line auto`: "All matched files use Prettier code style!"
- `npx eslint . --max-warnings=0`: exit 0
- `npx tsc -b`: exit 0
- `npx vitest run --coverage`: 206 files passed, 1195 tests passed. All files: 95.33 / 84.04 / 91.85 / 95.48.
- `npm run build`: exit 0. `routeTree.gen.ts` is unchanged.
- `npm run perf:budget`: entry 201/210, landing 210/220, lesson 231/240, quiz 249/255, admin-dashboard 219/230, all ok.

## Notes for review
- `registerDashboardLocales` uses `getI18n()` from react-i18next, so the feature does not import `@/app`. It relies on `initI18n()` having run before the admin chunk is evaluated. That is true in `main.tsx`, where init happens before the router renders, and in `test/setup.ts`.
- `package.json` `sideEffects` is `["**/*.css"]`, so the module-scope call survives only because `DashboardPage` itself is used. The route's `validateSearch` import through `@/features/dashboard` does not pull the page into the entry, which the +14 B entry delta confirms.
- `PROGRESS.md` and `.process/117-…/06-coderabbit-triage.md` were modified in this worktree at 16:07 by another process during my run. I did not touch them.
- Nothing is committed.
