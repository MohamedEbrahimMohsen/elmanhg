# Implementation — [E19.S2] Dashboard, table actions, dates and select polish (#280)

## Files created
| Path (under `web/src/`) | Lines | Purpose |
|---|---|---|
| `shared/lib/money.ts` | 13 | `minorUnitsPerMajor`, `formatMoney(amountMinor, currency, lng)` (moved out of `format.ts`) |
| `shared/lib/dateTime.ts` | 61 | `DateTimeStyle`, `dayMs`, `toDate`, `formatDateTime`, `isRecent`, `formatRelativeTime` |
| `shared/components/DateTime.tsx` | 23 | `<time>` with absolute or relative (< 24 h, full date in `title`) text |
| `shared/ui/select.tsx` | 23 | Shared `Select`: native select, `appearance-none`, `pe-10`, ChevronDown at inline-end |
| `features/dashboard/api/dailySeries.ts` | 37 | `DailyPoint`, `fillDailySeries`, `niceCeiling`, `axisTickIndices` |
| `features/dashboard/components/MetricList.tsx` | 30 | `dl` label/value rows (`dd` labelled by its `dt`) |
| `features/dashboard/components/ShareBar.tsx` | 17 | `<progress>` with MasteryBar's exact classes |
| `features/dashboard/components/BarList.tsx` | 31 | Label + value over a ShareBar |
| `features/dashboard/components/GrowthTiles.tsx` | 63 | Students / Active subscriptions / Revenue / Servable questions KPI tiles |
| `features/dashboard/components/LearningTiles.tsx` | 65 | Success rate / Solve rate / Pending review / Open teacher questions KPI tiles |
| `features/dashboard/components/QuestionTypesCard.tsx` | 40 | Questions-by-type bar list with share, empty state |
| `features/dashboard/components/DailyBarPlot.tsx` | 62 | SVG plot: gridlines, day ticks, accent bars, hit rects with `<title>` |
| `shared/lib/money.test.ts` | 19 | Tests #6–8 |
| `shared/lib/dateTime.test.ts` | 60 | Tests #9–16 |
| `shared/components/DateTime.test.tsx` | 44 | Tests #17–20 |
| `shared/ui/select.test.tsx` | 45 | Tests #21–23 |
| `features/dashboard/api/dailySeries.test.ts` | 35 | Tests #24–27 |
| `features/progress/pages/ProgressPage.dates.test.tsx` | 54 | Tests #47–48 |
| `features/users/pages/UsersPage.dates.test.tsx` | 28 | Test #49 |

## Files modified
| Path | Change |
|---|---|
| `web/src/shared/lib/format.ts`, `format.test.ts` | Reduced to `numberLocale(lng)` + `formatNumber(value, lng, options?)`; test rewritten (#1–5) |
| `web/src/app/i18n.ts`, `i18n.test.ts` | `parseLngForICU: numberLocale`; line 22 expects `'3 أسئلة'` |
| `web/src/styles/app.css` | Two cursor rules in `@layer base` after `th` |
| `web/src/shared/ui/button.tsx` | Dropped `disabled:pointer-events-none`; `danger` = `border border-border-strong bg-surface text-danger hover:bg-danger-soft` |
| `features/questions/api/pendingAge.ts`, `pendingAge.test.ts` | Deleted; `questions/index.ts` export removed |
| `ValidationQueueItem.tsx`, `GradeReviewListItem.tsx` | `formatRelativeTime` from `@/shared/lib/dateTime` |
| All 27 files in the plan's digit-argument list | Digit argument removed (`tsc` clean) |
| 6 formatMoney callers | Import from `@/shared/lib/money`, digit arg removed |
| 22 formatDate callers | `formatDateTime(value, lng, style)` with the plan's styles; `ExamPeriodRow` passes the `YYYY-MM-DD` string |
| `AuditLogRow`, `AvatarConversationRow`, `SessionHistoryRow`, `StudentHistoryTable` | `<DateTime value relative />` |
| `AvatarConversationRow`, `ExamAttemptsTable`, `ExamLessonBreakdown`, `SessionHistoryRow`, `UnitProgressTable` (2 links) | `<Button asChild variant="secondary" size="sm"><Link…/></Button>` |
| `users/components/UserListRow.tsx` | Actions wrapper `items-center` |
| 14 select files (16 selects) | `<Select>`; className and `selectClassName` consts removed; dashboard keeps `className="md:w-60"` |
| Locale JSON (blueprints, questions, quiz, shared ar) | Arabic-Indic digits → Latin (`convertDigits` kept) |
| 7 digit test assertions | Exactly the listed lines |
| `dashboard/api/metricFormat.ts`, `.test.ts` | money import, `formatDay` → `formatDateTime(…,'day')`, `formatCompact`; test #28 added |
| `dashboard/components/{Students,Subscribers,Payments,AskTeacher,Validation,Content,Funnel}Card.tsx` | Panels with `MetricList`/`BarList` |
| `dashboard/components/SolveRateCard.tsx`, `SuccessRateCard.tsx` | Deleted |
| `DailyBarChart.tsx`, `DashboardCharts.tsx`, `SuccessRateBreakdown.tsx`, `DashboardFilters.tsx`, `DashboardPage.tsx` | As per contracts |
| `dashboard/i18n/en.json`, `ar.json` | Replaced with the plan's key set |
| `DailyBarChart.test.tsx`, `DashboardPage.test.tsx`, `DashboardPage.cards.test.tsx`, `DashboardPage.filters.test.tsx` | Tests #29–46 |
| `web/scripts/perf/budgets.json` | admin-dashboard `maxKb` 230 → 233 (procedure step 5) |
| `docs/performance.md` §3 | admin-dashboard row 233/233 + the #280 sentence |
| `.claude/design-system.md` | v2.1: Button danger/cursor/row actions, Select, Table, KpiTile/MetricList/BarList/DailyBarChart, Digits + Dates bullets, change log 2.1 |
| `docs/design-system.md` | v2.1: principle 5, §3.2 numerals + Dates table, §5.1 danger/cursor/row actions, §5.6, §5.8, §5.9, new §5.14 Dashboard |
| `docs/claude-design-prompt.md` | §2.2 digits/dates, §2.4 danger, cursor, row actions, Select, Tables, Cards grid, §4 progress + admin dashboard, §6 Dashboard |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Only the listed digit assertions change; "no other existing test is edited" | `features/askTeacher/api/formatDuration.test.ts` ("uses Arabic-Indic digits in Arabic", `toBe('\\u0660:\\u0664\\u0662')`) asserts the old policy. The plan's grep missed it because it uses `\\u` escapes. It now fails (`'0:42'`). | Left it untouched per test-integrity rules. **BLOCKED: `formatDuration.test.ts > uses Arabic-Indic digits in Arabic` — asserts the policy D1 removes; it needs a planner-approved change to expect `'0:42'`.** |
| `app/i18n.test.ts`: change line 22 only | The test name still reads "formats ICU numbers with Arabic-Indic digits in Arabic" | Changed only line 22 as instructed; the name is now misleading. |
| Plan step (a) frees entry bytes for (b)/(c) | Moving `formatDate`/`formatMoney` out of `format.ts` creates new shared chunks (`dateTime`, `money`, `DateTime`, `select`), whose names enter `__vite__mapDeps` lists. `dateTime` joins AvatarPanel's preload list in the student route (+15 B br on quiz). An intermediate build was 11 B over on quiz. | No change to the plan's code. In the final build quiz passes at 261093/261120 B (27 B headroom). The margin depends on brotli noise; see the notes. |

## Build & test
All commands run in `web/` on Windows.
- `npm ci` → ok.
- Baseline (before edits) `npm run build && npm run perf:budget`: entry 209/210, landing 218/220, lesson 240/240, quiz 255/255, admin-dashboard 229/230, admin-users 263/270, teacher-home 257/265. Exact bytes matched the plan's D14.
- `npm run typecheck` → exit 0.
- `npm run lint` → exit 0.
- `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto` → "All matched files use Prettier code style!", exit 0.
- `npx vitest run --coverage` → `Test Files 3 failed | 286 passed (289)`, `Tests 3 failed | 1693 passed (1696)`:
  - `formatDuration.test.ts > uses Arabic-Indic digits in Arabic`: `expected '0:42' to be '٠:٤٢'` (the deviation above, BLOCKED).
  - `AvatarPanel.test.tsx > opens from the floating button…` and `NewEssayQuestion.test.tsx > creates an essay question…` were timeouts in untouched suites. Rerun alone: `Test Files 2 passed (2)`, `Tests 20 passed (20)`.
- `npm run build` → exit 0.
- `npm run perf:budget` → exit 0: entry 209/210, landing 218/220, lesson 240/240, quiz 255/255, admin-dashboard 233/233, admin-users 264/270, teacher-home 258/265. Exact brotli bytes: entry 213847, landing 223095, lesson 245506, **quiz 261093/261120**, admin-dashboard 237720.
- CI "No literal tokens or physical directions" greps → both exit 1 (no match).
- `npm run gen:tokens` → no diff in `tokens.css`. `gen:api` was not run; no API change.
- Built CSS vs baseline: the only new class is `.pe-10` (and `.disabled\:pointer-events-none:disabled` is gone). `.pe-10` comes after `.px-3`.
- Visual check: `vite preview` ran with the API proxied to :8080. The served CSS contains `button:enabled,select:enabled,[role=button]{cursor:pointer}:disabled{cursor:not-allowed}` and `.pe-10`. The live `/api/dashboard/*` payloads (admin demo login) carry `from`/`to` and the shapes the panels read. **A browser DOM measurement could not be run**: no Playwright package is installed, and the sandbox denies running the cached Chromium binary ("Permission denied"). I did not bypass the sandbox.

## Notes for review
- Quiz headroom is 27 B. An earlier intermediate build of the same code measured 11 B over, because index/route chunk renaming changes brotli output. The structural cost on the quiz path is: CSS +63 B (cursor rules and `pe-10`), and the `dateTime` entry in AvatarPanel's `__vite__mapDeps` in the student route, +15 B.
- `dateTime.ts` picks the style through an object of thunks (`{ date: () => …}[style]()`), not `switch`.
- `ExamPeriodRow` date-only values are now formatted in UTC (before, local midnight). The visible date is the same.
- `UnitProgressTable`'s unit-name link is also an `sm` secondary button ("both links", as the plan says).
- `fillDailySeries` derives every UTC day from `from`..`to`, so the attempts chart shows 14 days for the fixture (test #38).
- Prettier expanded the inline `periods`/`plans`/`levels` objects in `dashboard/i18n/*.json` when I regenerated them. The content is unchanged.
- `git rm` was used for the four deleted files, so they are staged. Nothing is committed.
- Demo credentials were used only on the command line and are not in any file.
