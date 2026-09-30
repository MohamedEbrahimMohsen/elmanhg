VERDICT: APPROVED

# Review r2: [E11.S2] Dashboard UI (#105)

This review covers the working tree against `HEAD` (dce990d) in `D:/Personal/elmanhg-wt/105`, checked against `03-review.md` and `02-implementation-r2.md`.

## Blocking findings from r1

### 1. Admin strings on student pages: FIXED
- `web/src/app/i18n.ts` is identical to `HEAD` (`git diff --stat HEAD -- web/src/app` is empty).
- `web/src/features/dashboard/locales.ts:5-9` defines `registerDashboardLocales()`, which calls `getI18n().addResourceBundle` for `ar` and `en`. `web/src/features/dashboard/pages/DashboardPage.tsx:19` calls it at module scope.
- Build evidence: the dashboard copy ("Platform figures for the chosen period") appears only in `dist/assets/admin-*.js`, and the entry `index-*.js` has 0 occurrences. The entry wires `/admin/` as `component: lazyRouteComponent(() => import('./admin-*.js'))`, and `validateSearch` stays in the entry. `sideEffects: ["**/*.css"]` lets the barrel re-export tree-shake `DashboardPage` out of the entry.
- No flash of raw keys, including on direct navigation to `/admin`. `main.tsx` calls `initI18n()` synchronously (`initAsync: false`) before the router is created or rendered, so `getI18n()` is always set. The lazy component suspends until its chunk has been evaluated, and evaluating the chunk registers the bundle, so the bundle exists before the first `useTranslation('dashboard')` render. `hasLoadedNamespace` returns true because `resources` is set without `partialBundledLanguages`, so no namespace load is attempted. The page tests go through the router's lazy chunk, and the first-render `Loading Students` assertion passes.
- Ruling on the residual +229 / +278 / +309 B on landing / lesson / quiz: **acceptable.** None of it is admin copy.
  - +57 B is Tailwind utilities in the single global stylesheet. Every feature adds these, and there is no per-route CSS.
  - About +172 B is chunk overhead from Rolldown splitting the shared `ContentListSkeleton` chunk because `MetricCard` reuses `ContentErrorState`. Reusing the component is correct: duplicating it to save about 170 B would break the shared-component rule.
  - The plan's DoD tolerance (`01-plan.md:249`) is ±1 KB, and the largest delta is 0.3 KB. `perf:budget` passes with headroom.

### 2. D8 digit assertions for rate, ratio and amount: FIXED
`web/src/features/dashboard/api/metricFormat.test.ts` now asserts in Arabic:
- `formatRate(0.75,'ar')` contains `75` and has no `[٠-٩]`.
- `formatRatio(3.25,'ar')` is exactly `'3.25'`.
- `formatAmount(895500 EGP,'ar')` contains `8,955` and has no `[٠-٩]`.

Each of these assertions fails if that function's `'latin'` is switched to `'arabic-indic'`, so the reported mutants are killed.

### 3. Filter select height: FIXED
`web/src/features/dashboard/components/DashboardFilters.tsx:17` is `h-11` (44 px), which matches the design-system Select height.

## Non-blocking
- `web/src/features/dashboard/components/DailyBarChart.tsx:75-77`: the r1 note on RTL axis labels was addressed with `dir="auto"` on each label.
- The r1 notes on test 48 (`DashboardPage.filters.test.tsx`), the card-list layout under `md`, and the frozen `now` still apply. None of them gates the merge.

## Verified (my runs in `web/`)
- `npx tsc -b`: exit 0.
- `npx eslint . --max-warnings=0`: exit 0.
- `npx prettier --check ... --end-of-line auto`: clean.
- `npm run build`: exit 0, and `routeTree.gen.ts` is unchanged.
- `npm run perf:budget`: entry 201/210, landing 210/220, lesson 231/240, quiz 249/255, admin-dashboard 219/230, all ok. These match the r2 report.
- `npx vitest run --coverage` (full suite): 2 runs on a loaded machine (about 40 node/dotnet processes from parallel worktrees) each had 3 and 5 timeout failures. The failing tests were different each run, and all of them are outside the diff: `UnitLessons`, `NewEssayQuestion`, `LessonItem`, `AvatarPanel`, `ExamPage`. Every one passes in isolation, and so does the whole `src/features/dashboard` folder (11 files, 81 tests, together with the flaky ones). Load flake, not a regression.
- `docs/performance.md` §3 has the refreshed measured values plus the `admin-dashboard` row. §4 has the new "Admin-only strings on demand" bullet, which describes exactly what the code does. No docs divergence.
- r2 scope: only the files listed in `02-implementation-r2.md` changed. `package.json` and `package-lock.json` are unchanged.

## Test quality
- `metricFormat.test.ts` now constrains Latin digits in Arabic for every formatter.
- `DailyBarChart.test.tsx` and `SuccessRateBreakdown.test.tsx` register the namespace in `beforeAll`, so they still assert on real translated text.
- The page tests use the real lazy-registration path.
