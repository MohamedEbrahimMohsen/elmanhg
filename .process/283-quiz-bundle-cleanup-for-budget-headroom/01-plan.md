# Plan — Quiz bundle cleanup for budget headroom (#283, E19.S3)

## Goal
Student-facing changes stop failing `perf:budget` on a few bytes. The quiz page goes from 53 B spare to about 19 KB spare, and lesson, entry and landing gain about 19 to 20 KB each, with no budget raised and nothing visible changed. The disabled-button hover fix deferred from #280 (#282) ships. When a budget does fail, `perf:budget` now prints the per-chunk breakdown of the failing page, so the cause is visible in the CI log.

## Scope
**In:**
- C1: English strings load on demand. The UI is Arabic-only (PRD §14 "Language: Arabic UI, RTL throughout"), and nothing in production ever switches to `en`. Arabic stays in the entry chunk. English becomes 17 lazy JSON chunks loaded by `loadLanguage('en')`.
- C2: the five admin-only namespaces (`audit`, `blueprints`, `payments`, `trainingExport`, `avatarConversations`) leave `app/i18n.ts`. Each is registered by its page module, using the existing `registerUsersLocales` pattern.
- C3: the admin and teacher half of the `questions` namespace (`list`, `editor`, `import`, `validation`, `statuses`, `difficulties`) moves into `questions/i18n/admin.{ar,en}.json`. It is registered by the six question pages and `GradingKeyView`. Student keys (`types`, `view`, `preview`, `essayGrade`, `mathStepGrade`) stay eager.
- C4: the #282 hover fix in `shared/ui/button.tsx`, plus the matching design-system doc line.
- C5: budget diagnostics (`scripts/perf/budgetDiagnostics.ts`), wired into `budgetCli.ts`, plus `docs/performance.md`.

**Out (investigated, no change; the evidence is in the analysis below):**
- Moving more code behind `lazy()`. AvatarPanel, KaTeX (`renderMath`), the paywall and radix dialogs, MathSteps, essay, drag-drop, TeacherReviewNote and TopNavMore are already dynamic. None of them is on the quiz static closure, and nothing preloads them into it.
- Tailwind trimming. Excluding generated API files, JSON, `src/test` and `routeTree.gen.ts` from the scan drops only `.static .table .lowercase` (measured ±0 B br). Content globs are sound.
- Dead code. Rolldown tree-shakes unused exports, so the exported-but-unused constants found by a scan cost 0 B. No student-path duplicate helper exists: the `formatCount` wrappers are admin-only and tested.
- Vendor split. Splitting `react-dom`, the router and i18next out of `index-*.js` changes caching, not page totals (it adds a little).
- Sonner on demand (8.2 KB), FeedbackPanel on check (4.4 KB), fontsource subset pruning (≤1.7 KB) and splitting `common:errors` by role (≈5 KB). Each changes timing, rendering or error resolution, so each is listed as a lever in `docs/performance.md` §9.

**Deferred:** none.

## Measurement

### Method
`npm --prefix web ci && npm --prefix web run build && npm --prefix web run perf:budget` on 75f44a8a. The per-chunk table walks `dist/.vite/manifest.json` with `perf:budget`'s own closure (`pageFiles`) and brotli settings (quality 11, `BROTLI_MODE_TEXT`). Module attribution comes from a `--sourcemap` build of the same commit, compressing each module's bytes on their own. "After" numbers come from a scratch copy of `web/` with C1 to C4 applied exactly as specified below and built (`vite build`). The C1 figure includes the 17 loader stubs. In that prototype, `tsc -b` was clean and the full existing suite passed unchanged (`vitest run`: 289 files, 1696 tests) with only the `setup.ts` change.

### Before / after (bytes brotli; budget = maxKb × 1024)
| Page | Budget | Before | Spare before | After (est.) | Spare after | Δ |
|---|---|---|---|---|---|---|
| entry | 215040 | 213830 | 1210 | 194183 | 20857 | −19647 |
| landing | 225280 | 223058 | 2222 | 203423 | 21857 | −19635 |
| lesson | 245760 | 245462 | 298 | 225843 | 19917 | −19619 |
| **quiz** | 261120 | **261067** | **53** | **241393** | **19727** | **−19674** |
| admin-dashboard | 238592 | 237687 | 905 | 218093 | 20499 | −19594 |
| admin-users | 276480 | 270172 | 6308 | 250543 | 25937 | −19629 |
| teacher-home | 271360 | 264063 | 7297 | 250945 | 20415 | −13118 (C3 adds the question admin strings to the teacher validation queue chunk) |

Quiz saving per cut (each measured with the earlier cuts applied): C1 −14591 (stubs add +≈250 back), C2 −2280, C3 −3027, C4 ±0 (+19 B CSS).

### Quiz path before (261067 B), per chunk
| Bytes br | File | Manifest source | Top contents (isolated br) |
|---|---|---|---|
| 151318 | `index-*.js` | `index.html` | react-dom 54.3 K · **i18n resources ≈42 K (EN ≈14.6 K; `questions` 7.4 K incl. ≈3.0 K AR admin; `common` ≈10.6 K)** · router-core 14.5 K · i18next 12.2 K · sonner 8.2 K · @formatjs + intl-messageformat ≈9 K · query-core 1.9 K · routeTree 1.2 K |
| 24614 | `http-*.js` | `_http` | zod v4 core+classic+locale ≈21.5 K · query-core 2.4 K · `http.ts` 0.7 K |
| 10531 | `RichTextViewer-*.js` | `_RichTextViewer` | dompurify 9.8 K |
| 8497 | `index-*.css` | (global css) | utilities + theme + 27 `@font-face` (1.7 K) |
| 7858 | `utils-*.js` | `_utils` | tailwind-merge 7.7 K |
| 5018 | `compiler-runtime-*.js` | `_compiler-runtime` | react 2.6 K · react-i18next 2.1 K |
| 4977 | `quiz._sessionId-*.js` | quiz route | QuizQuestionCard, QuizRunner, hooks |
| 4738 | `useQuery-*.js` | `_useQuery` | query-core 4.0 K |
| 4719 | `QuestionView-*.js` | `_QuestionView` | answer inputs |
| 4397 | `MathStepGradeStatus-*.js` | `_MathStepGradeStatus` | FeedbackPanel, MathStepGrade*, CorrectAnswer |
| 4227 | `link-*.js` | `_link` | react-router + router-core |
| 2664 · 2339 · 1978 | AppShell · student route · navConfig | | |
| ≈11.5 K | 45 chunks under 1.7 K each | | mathStepsValue 1509, lucide icons, sessions, subscriptions, quizItem… |

Dynamic only, so neither counted nor preloaded on quiz: AvatarPanel 7146, DragDropAnswerInput 6118, MathStepsAnswerInput 5950, QuizEssayCard 2598, TopNavMore 1182, PaywallDialog 879, DragDropCorrectAnswer 793, TeacherReviewNoteContent 655, renderMath 219 (→ katex 78 K gz). `dateTime`/`DateTime` and `dialog` are not in the quiz closure.

### Entry, landing and lesson before
- entry (213830) = `index` 151318 + `http` 24614 + css 8497 + `utils` 7858 + `compiler-runtime` 5018 + `useQuery` 4738 + `link` 4227 + 13 small chunks.
- landing (223058) = entry + `routes/index` 2127 + PlanCard 1609 + lucide 1376 + `questions` api 1157 + ContentErrorState 560 + 7 small chunks.
- lesson (245462) = entry + RichTextViewer 10531 + AppShell 2664 + student route 2339 + lesson route 2325 + navConfig 1978 + 30 small chunks.

The entry chunk is shared by every page, so C1 to C3 (all inside `index-*.js`) lower every page by the same amount.

## Decisions
| # | Question | Decision | Why |
|---|---|---|---|
| D1 | Is dropping English from the boot bundle a behaviour change? | No. `initI18n()` defaults to `ar`. No detector, switcher, query parameter or profile field sets `en` (grep: the only `changeLanguage` calls are in `initI18n` and tests). PRD §14 says Arabic UI. English stays fully supported through `loadLanguage('en')`. | Biggest clean cut (−14.6 KB); listed as a lever in `docs/performance.md` §9. |
| D2 | Where do the English loaders live? | Each feature's existing locale export keeps the `{ ar, en }` shape, with `en` now a loader `() => import('./i18n/en.json')`. `app/i18n.ts` holds the `common` loader. | Keeps feature encapsulation. Property-level tree-shaking does not drop `en` from a static `{ ar, en }` (measured: only `commonEn` left). |
| D3 | Why not one English chunk? | 17 small chunks, one per namespace, all loaded in parallel by `Promise.all`. | One chunk would need deep imports of every feature's `en.json` into `app/`. |
| D4 | Loading and fallback for English | `loadLanguage` adds the bundles only after every loader resolves. If one rejects, nothing is added and the promise rejects. Until English is added, i18next resolves from `fallbackLng: 'ar'`, so Arabic text shows (no raw keys). Callers must `await loadLanguage(lng)` before `i18n.changeLanguage(lng)`, because react-i18next does not re-render on `added`. | All-or-nothing avoids a half-English UI. Arabic fallback is the existing `fallbackLng`. |
| D5 | How do tests keep English synchronously? | `src/test/setup.ts` does `initI18n('en'); await loadLanguage('en');` (top-level await in the setup module). | No test file changes; every existing English assertion still resolves. |
| D6 | Where are the admin namespaces registered? | At module scope of the page modules that render them, exactly like `DashboardPage` and `UsersPage`. They are not registered in test setup. | The existing pattern. Pages are lazy route chunks, so registration runs before the first render. The page tests cover them through `renderApp`. |
| D7 | `questions` split boundary | Admin file: top-level keys `list`, `editor`, `import`, `validation`, `statuses`, `difficulties`. Base file: `types`, `preview`, `essayGrade`, `view`, `mathStepGrade`. Values are moved verbatim. Same namespace (`questions`), deep-merged by `addResourceBundle(lng, 'questions', bundle, true, true)`. | No `t()` key changes anywhere. A consumer-graph scan found that every file using an admin-group key under the `questions` namespace is reached only through the six question pages or `GradingKeyView` (the teacher grade review). Student consumers use only base groups: `types` in QuizQuestionHeading, the review items and ExamQuestionCard; `view` and `essayGrade`/`mathStepGrade` in the answer inputs and score lists. |
| D8 | Register `questions` admin strings in the admin/teacher layout routes instead? | No. Register in the 6 pages and `GradingKeyView`. | Route files hold no logic (skill §1), and `AppShell` is shared with students. This matches the existing page-scope pattern. |
| D9 | The hover fix selector | `hover:not-disabled:` on the four variant hover fills, not `enabled:hover:`. | `:enabled` never matches `<a>`, which would drop hover from `Button asChild` links. This is the variant #280 measured. `active:` fills unchanged: a disabled button never matches `:active`. |
| D10 | Diagnostics trigger | Always print the existing report. Then print a breakdown for every failing page, plus any requested with `--breakdown` (all pages) or `--breakdown=quiz,lesson`. An unknown page name throws. | The story asks for a breakdown on failure and a perf script option. There is no new CI step: web-ci already runs `npm run perf:budget`, so the breakdown lands in the job log. |
| D11 | Diagnostics in a new module or in `bundleBudget.ts`? | New `scripts/perf/budgetDiagnostics.ts` with its own test file. `BudgetResult` is unchanged. | `bundleBudget.test.ts` asserts `BudgetResult` with `toEqual`, and existing tests must not be edited. |
| D12 | Budgets | Unchanged in `budgets.json`. Only the "Measured" column of `docs/performance.md` §3 changes. | The story: no budget raised. Lowering the budgets is a separate decision. |
| D13 | Tailwind `@source` exclusions | Not added. | Measured ±0 B. They drop three classes that tsx never uses. Zero gain, nonzero risk. |
| D14 | Remove the five namespaces from `ns:` in `initI18n` | Yes. `ns` becomes `Object.keys(arabicResources)`. | Lazily registered namespaces (`users`, `dashboard`) are already unlisted. `hasLoadedNamespace` returns true with inline `resources` and no backend, so `useTranslation('payments')` does not suspend. |
| D15 | The `audit` barrel exports `auditLocales` | Removed from `features/audit/index.ts`. A new `features/audit/locales.ts` exports `registerAuditLocales`. | The barrel is in the entry chunk (the route's `validateSearch` imports it); keeping the JSON there defeats C2. |

## Existing code touched
| File | Change |
|---|---|
| `web/src/app/i18n.ts` | Rewrite resources: Arabic-only boot resources, English loaders, `loadLanguage`, five admin namespaces removed (contract below) |
| `web/src/test/setup.ts` | Import `loadLanguage`; after `initI18n('en');` add `await loadLanguage('en');` |
| `web/src/shared/i18n/en.json` | unchanged (now loaded via `import()`) |
| `web/src/features/askTeacher/locales.ts` | `export const askTeacherLocales = { ar, en: () => import('./i18n/en.json') };` and delete `import en …` |
| `web/src/features/avatar/locales.ts` | same pattern (`avatarLocales`) |
| `web/src/features/browse/locales.ts` | same (`browseLocales`) |
| `web/src/features/content/locales.ts` | same (`contentLocales`) |
| `web/src/features/exam/locales.ts` | same (`examLocales`) |
| `web/src/features/landing/locales.ts` | same (`landingLocales`) |
| `web/src/features/mastery/locales.ts` | same (`masteryLocales`) |
| `web/src/features/mathSteps/locales.ts` | same (`mathStepsLocales`) |
| `web/src/features/onboarding/locales.ts` | same (`onboardingLocales`) |
| `web/src/features/progress/locales.ts` | same (`progressLocales`) |
| `web/src/features/questions/locales.ts` | same (`questionsLocales`) |
| `web/src/features/quiz/locales.ts` | same (`quizLocales`) |
| `web/src/features/subscription/locales.ts` | same (`subscriptionLocales`) |
| `web/src/features/shell/index.ts` | `export const shellLocales = { ar, en: () => import('./i18n/en.json') };`, delete `import en …` |
| `web/src/features/session/index.ts` | `export const sessionLocales = { ar, en: () => import('./i18n/en.json') };`, delete `import en …` |
| `web/src/features/audit/index.ts` | Delete `import ar`, `import en`, `export const auditLocales` |
| `web/src/features/blueprints/locales.ts` | Replace body with `registerBlueprintsLocales` (contract below) |
| `web/src/features/payments/locales.ts` | Replace body with `registerPaymentsLocales` |
| `web/src/features/trainingExport/locales.ts` | Replace body with `registerTrainingExportLocales` |
| `web/src/features/avatarConversations/locales.ts` | Replace body with `registerAvatarConversationsLocales` |
| `web/src/features/audit/pages/AuditLogPage.tsx` | After the last import: `import { registerAuditLocales } from '../locales';`, blank line, `registerAuditLocales();` |
| `web/src/features/blueprints/pages/ExamBlueprintsPage.tsx` | Same with `registerBlueprintsLocales` from `'../locales'` |
| `web/src/features/payments/pages/PaymentLogPage.tsx` | Same with `registerPaymentsLocales` |
| `web/src/features/trainingExport/pages/TrainingExportPage.tsx` | Same with `registerTrainingExportLocales` |
| `web/src/features/avatarConversations/pages/AvatarConversationsPage.tsx` | Same with `registerAvatarConversationsLocales` |
| `web/src/features/avatarConversations/pages/AvatarConversationPage.tsx` | Same with `registerAvatarConversationsLocales` |
| `web/src/features/questions/i18n/ar.json` | Keep exactly the top-level keys `types`, `preview`, `essayGrade`, `view`, `mathStepGrade` (current order, values untouched); move `list`, `editor`, `import`, `validation`, `statuses`, `difficulties` verbatim to F1 |
| `web/src/features/questions/i18n/en.json` | Same split; the moved keys go to F2 |
| `web/src/features/questions/pages/QuestionListPage.tsx` | After the last import: `import { registerQuestionsAdminLocales } from '../questionsAdminLocales';`, blank line, `registerQuestionsAdminLocales();` |
| `web/src/features/questions/pages/QuestionEditorPage.tsx` | same |
| `web/src/features/questions/pages/NewQuestionPage.tsx` | same |
| `web/src/features/questions/pages/QuestionImportPage.tsx` | same |
| `web/src/features/questions/pages/ValidationQueuePage.tsx` | same |
| `web/src/features/questions/pages/ValidationQuestionPage.tsx` | same |
| `web/src/features/questions/components/GradingKeyView.tsx` | same (import path `'../questionsAdminLocales'`) |
| `web/src/shared/ui/button.tsx` | `hover:bg-accent-hover`→`hover:not-disabled:bg-accent-hover` (in `accentFillClassName`); secondary `hover:bg-soft`→`hover:not-disabled:bg-soft`; danger `hover:bg-danger-soft`→`hover:not-disabled:bg-danger-soft`; ghost `hover:bg-accent-soft`→`hover:not-disabled:bg-accent-soft`. Nothing else. |
| `web/src/shared/ui/button.test.tsx` | **modify (append only)**: tests T20 and T21; the two existing tests are byte-identical |
| `web/src/app/i18n.test.ts` | **modify (append only)**: tests T1 to T4; the four existing tests are byte-identical |
| `web/scripts/perf/budgetCli.ts` | Memoised `sizeOf`; after the report, print breakdowns (contract below) |
| `docs/performance.md` | §3, §4, §8, §9 (below) |
| `docs/design-system.md` | §5.1 Buttons, line 160: `Disabled: 45 percent opacity.` → `Disabled: 45 percent opacity, and no hover fill.` |
| `.claude/design-system.md` | Button row (line 86): `disabled (45% opacity)` → `disabled (45% opacity, no hover fill)` |

Not touched: `budgets.json`, `bundleBudget.ts`, `bundleBudget.test.ts`, `vite.config.ts`, `app.css`, every route file, every existing test except the two append-only files above.

## Files to create
| # | Path | Type | Contract |
|---|---|---|---|
| F1 | `web/src/features/questions/i18n/admin.ar.json` | JSON | `{ "list", "editor", "import", "validation", "statuses", "difficulties" }` moved verbatim from `ar.json`, in that source order. A deep merge of the new `ar.json` and `admin.ar.json` must equal `git show 75f44a8a:web/src/features/questions/i18n/ar.json`. |
| F2 | `web/src/features/questions/i18n/admin.en.json` | JSON | Same for `en.json`. |
| F3 | `web/src/features/questions/questionsAdminLocales.ts` | module | `import { getI18n } from 'react-i18next'; import ar from './i18n/admin.ar.json'; import en from './i18n/admin.en.json';` `export function registerQuestionsAdminLocales(): void { const i18n = getI18n(); i18n.addResourceBundle('ar', 'questions', ar, true, true); i18n.addResourceBundle('en', 'questions', en, true, true); }` |
| F4 | `web/src/features/audit/locales.ts` | module | `registerAuditLocales(): void`, the F3 body with namespace `'audit'`, `./i18n/ar.json`, `./i18n/en.json` |
| F5 | `web/scripts/perf/budgetDiagnostics.ts` | module | See below |
| F6 | `web/scripts/perf/budgetDiagnostics.test.ts` | test | T10 to T17 |
| F7 | `web/src/features/audit/locales.test.ts` | test | T5 |
| F8 | `web/src/features/blueprints/locales.test.ts` | test | T6 |
| F9 | `web/src/features/payments/locales.test.ts` | test | T7 |
| F10 | `web/src/features/trainingExport/locales.test.ts` | test | T8 |
| F11 | `web/src/features/avatarConversations/locales.test.ts` | test | T9 |
| F12 | `web/src/features/questions/questionsAdminLocales.test.ts` | test | T18, T19 |

### `blueprints|payments|trainingExport|avatarConversations/locales.ts` (rewritten)
Same shape as `features/users/locales.ts`:
```ts
import { getI18n } from 'react-i18next';
import ar from './i18n/ar.json';
import en from './i18n/en.json';

export function registerBlueprintsLocales(): void {   // registerPaymentsLocales / registerTrainingExportLocales / registerAvatarConversationsLocales
  const i18n = getI18n();
  i18n.addResourceBundle('ar', 'blueprints', ar, true, true);   // 'payments' / 'trainingExport' / 'avatarConversations'
  i18n.addResourceBundle('en', 'blueprints', en, true, true);
}
```
The old `blueprintsLocales` / `paymentsLocales` / `trainingExportLocales` / `avatarConversationsLocales` exports are deleted (`app/i18n.ts` was their only importer).

### `web/src/app/i18n.ts` (full contract)
- Imports: i18next, ICU, `initReactI18next`, and the locale objects of `askTeacher, avatar, browse, content, exam, landing, mastery, mathSteps, onboarding, progress, questions, quiz, subscription` (from `@/features/<f>/locales`), `sessionLocales` (`@/features/session`) and `shellLocales` (`@/features/shell`). Also `numberLocale` and `commonAr` (`@/shared/i18n/ar.json`). **No** import of `@/shared/i18n/en.json`, `audit`, `blueprints`, `payments`, `trainingExport` or `avatarConversations`.
- `supportedLanguages`, `Language`, `defaultLanguage` and `i18n` are unchanged.
- `type BundleLoaders = Record<string, () => Promise<{ default: object }>>;` (not exported)
- `const arabicResources = { common: commonAr, session: sessionLocales.ar, shell: shellLocales.ar, content: contentLocales.ar, questions: questionsLocales.ar, quiz: quizLocales.ar, mastery: masteryLocales.ar, mathSteps: mathStepsLocales.ar, progress: progressLocales.ar, exam: examLocales.ar, subscription: subscriptionLocales.ar, browse: browseLocales.ar, landing: landingLocales.ar, onboarding: onboardingLocales.ar, avatar: avatarLocales.ar, askTeacher: askTeacherLocales.ar };`
- `const lazyLanguageBundles: Partial<Record<Language, BundleLoaders>> = { en: { common: () => import('@/shared/i18n/en.json'), session: sessionLocales.en, shell: shellLocales.en, content: contentLocales.en, questions: questionsLocales.en, quiz: quizLocales.en, mastery: masteryLocales.en, mathSteps: mathStepsLocales.en, progress: progressLocales.en, exam: examLocales.en, subscription: subscriptionLocales.en, browse: browseLocales.en, landing: landingLocales.en, onboarding: onboardingLocales.en, avatar: avatarLocales.en, askTeacher: askTeacherLocales.en } };`
- `export async function loadLanguage(lng: Language, loaders: BundleLoaders = lazyLanguageBundles[lng] ?? {}): Promise<void>`:
  1. `const bundles = await Promise.all(Object.entries(loaders).map(async ([ns, load]) => [ns, (await load()).default] as const));`
  2. `for (const [ns, bundle] of bundles) { i18n.addResourceBundle(lng, ns, bundle, true, true); }`
- `applyDocumentLanguage` unchanged.
- `initI18n(lng = defaultLanguage)`: same body, except `resources: { ar: arabicResources }` and `ns: Object.keys(arabicResources)`. Everything else is unchanged (ICU, `fallbackLng`, `supportedLngs`, `defaultNS`, `initAsync: false`, `escapeValue`, the `languageChanged` listener).

### `web/scripts/perf/budgetDiagnostics.ts`
```ts
import { pageFiles, type BudgetResult, type Manifest, type PageBudget } from './bundleBudget.ts';

export interface ChunkSize { file: string; source: string; bytes: number; }
export interface PageBreakdown { name: string; bytes: number; maxBytes: number; chunks: ChunkSize[]; }

export const breakdownFlag = '--breakdown';

export function sourceOf(manifest: Manifest, file: string): string
// 1) the manifest key whose chunk.file === file; 2) else the first key (Object.keys order) whose chunk.css includes file → `${key} (css)`; 3) else file.

export function pageBreakdown(manifest: Manifest, budget: PageBudget, sizeOf: (file: string) => number): PageBreakdown
// chunks = pageFiles(manifest, budget.entries).map(file => ({ file, source: sourceOf(manifest, file), bytes: sizeOf(file) }))
//   sorted by bytes desc, then file asc; bytes = sum; maxBytes = budget.maxKb * 1024.

export function formatBreakdown(breakdown: PageBreakdown): string
// line 1: `${name}: ${bytes} B of ${maxBytes} B brotli (${spare} B spare)`, or `(${-spare} B over)` when bytes > maxBytes (spare = maxBytes - bytes).
// one line per chunk: `${String(bytes).padStart(8)} B ${(bytes / total * 100).toFixed(1).padStart(5)} %  ${file}  ${source}`; lines joined with '\n'.

export function breakdownPageNames(args: string[], results: BudgetResult[]): string[]
// requested: no arg starting with breakdownFlag → []; exactly `--breakdown` → every results name;
//   `--breakdown=a,b` → split on ',', trim, drop empties; any name not in results → throw new Error(`Unknown page: ${name}`).
// returns results.map(r => r.name).filter(n => !okOf(n) || requested.includes(n)), i.e. failing pages ∪ requested, in budgets order.
```
`budgetCli.ts` after `console.log(formatReport(results));`:
```ts
const sizes = new Map<string, number>();   // sizeOf becomes memoised: sizes.get(file) ?? compute+set
for (const budget of pages.filter((page) => breakdownPageNames(process.argv.slice(2), results).includes(page.name))) {
  console.log(`\n${formatBreakdown(pageBreakdown(manifest, budget, sizeOf))}`);
}
```
`process.exitCode` logic is unchanged. Usage: `npm run perf:budget -- --breakdown=quiz`.

## Error codes
None. This story has no API or server error surface.

## Domain behaviour
None (web-only cleanup).

## API surface
None. Developer CLI only: `npm run perf:budget [-- --breakdown[=page,…]]`.

## docs/performance.md updates
- §3: keep the budgets. Update "Measured (KB br)" to the post-change `perf:budget` output; the expected figures are entry 190, landing 199, lesson 221, quiz 236, admin-dashboard 213, admin-users 245, teacher-home 246. Use the real run, not these estimates. Add a paragraph **"Diagnosing a failure"**: when a page is over, `perf:budget` prints that page's chunks (brotli bytes, share, file, manifest source), largest first. `npm run perf:budget -- --breakdown=quiz` (or bare `--breakdown` for every page) prints them on demand. Shared `_name` chunks and `index.html` (the entry) count on every page.
- §4: new bullet **"Arabic at boot, English on demand"**. Only Arabic resources are in the entry chunk. Each feature's `en` is a loader; `loadLanguage('en')` loads all English bundles before a switch, and adds none if one fails (Arabic fallback). Tests call it in `src/test/setup.ts`. Extend "Admin-only strings on demand" with `audit`, `blueprints`, `payments`, `trainingExport` and `avatarConversations` (registered by their pages), and with the `questions` split (`admin.{ar,en}.json`, registered by the six question pages and `GradingKeyView`).
- §8: new subsection **"Bundle cleanup (#283), 2026-10-03"**. Add the before/after table from this plan, filled with the measured after values, and the quiz contributor table.
- §9: delete the "Drop English locales…" row. Add rows: sonner on demand (8.2 KB; first toast would wait for a chunk); FeedbackPanel and MathStepGrade on first check (4.4 KB; adds a chunk to the first check's latency); fontsource subset pruning (≤1.7 KB CSS; changes rendering of rare Latin-ext/Vietnamese glyphs); `common:errors` split by role (≈5 KB; error-code resolution would depend on route); vendor split for cross-deploy caching (no budget gain).

## Test plan
Conventions: `describe` per unit, `it('<does X> when <Y>')`, no `fireEvent`, no `.skip`/`.only`. Script tests use `// @vitest-environment node`.

| # | Test file | Test | Asserts |
|---|---|---|---|
| T1 | `src/app/i18n.test.ts` (append) | `it('loads the English bundles on demand')` | `i18n.removeResourceBundle('en', 'quiz')`; `i18n.exists('quiz:practice.choose', { lng: 'en', fallbackLng: false })` is false; `await loadLanguage('en')`; then `i18n.t('quiz:practice.choose', { lng: 'en' })` = `'How many questions?'`, `i18n.t('app.name', { lng: 'en' })` = `'Elmanhg'`, `i18n.t('questions:types.Mcq', { lng: 'en' })` = `'Multiple choice'` |
| T2 | same | `it('adds no English bundle when one fails to load')` | `await expect(loadLanguage('en', { probeLoaded: () => Promise.resolve({ default: { title: 'Loaded' } }), probeFailed: () => Promise.reject(new Error('offline')) })).rejects.toThrow('offline')`; `i18n.hasResourceBundle('en', 'probeLoaded')` is `false` |
| T3 | same | `it('shows Arabic while an English bundle is missing')` | `i18n.addResourceBundle('ar', 'probeFallback', { title: 'عنوان' })`; `i18n.t('probeFallback:title', { lng: 'en' })` = `'عنوان'` |
| T4 | same | `it('has no lazy bundles for Arabic')` | `await loadLanguage('ar')` resolves, and `i18n.t('quiz:practice.choose', { lng: 'ar' })` = `'كم سؤالًا تريد؟'` (Arabic still from the boot resources) |
| T5 | `features/audit/locales.test.ts` | `it('does not ship the audit namespace until registered')` + `it('resolves audit:page.title after registering')` | `hasResourceBundle('en','audit')` false; then after `registerAuditLocales()`: `i18n.t('audit:page.title', { lng: 'en' })` = `'Audit log'`, `{ lng: 'ar' }` = `'سجل التدقيق'` |
| T6 | `features/blueprints/locales.test.ts` | same two tests, blueprints | `'Exam blueprints'` / `'نماذج الامتحانات'` |
| T7 | `features/payments/locales.test.ts` | same two tests, payments | `'Payments'` / `'المدفوعات'` |
| T8 | `features/trainingExport/locales.test.ts` | same two tests, trainingExport | `'Training data export (JSONL)'` / `'تصدير بيانات التدريب (JSONL)'` |
| T9 | `features/avatarConversations/locales.test.ts` | same two tests, avatarConversations | `'Assistant conversations'` / `'محادثات المساعد'` |
| T10 | `scripts/perf/budgetDiagnostics.test.ts` | `it('lists every file of a page with its manifest source, largest first')` | With the `bundleBudget.test.ts` manifest shape and `sizeOf` from a fixed map: chunks in byte order, with sources `index.html`, `_shared.js`… and `bytes` = sum |
| T11 | same | `it('labels a css file with the chunk that imports it')` | `sourceOf(manifest, 'assets/viewer.css')` = `'_viewer.js (css)'` |
| T12 | same | `it('reports spare bytes when the page is under budget')` | first line of `formatBreakdown` = `'page: 2000 B of 2048 B brotli (48 B spare)'` |
| T13 | same | `it('reports the overage when the page is over budget')` | `'… (1 B over)'` for 2049 of 2048 |
| T14 | same | `it('breaks down only failing pages without the flag')` | `breakdownPageNames([], [ok a, failing b])` = `['b']` |
| T15 | same | `it('breaks down every page with a bare --breakdown')` | `['--breakdown']` → `['a','b']` |
| T16 | same | `it('breaks down the named pages with --breakdown=a')` | `['--breakdown=a']` with both ok → `['a']`; with b failing → `['a','b']` |
| T17 | same | `it('throws for an unknown page name')` | `['--breakdown=x']` throws `'Unknown page: x'` |
| T18 | `features/questions/questionsAdminLocales.test.ts` | `it('keeps the student question strings without the admin strings until registered')` | `i18n.t('questions:types.Mcq', { lng: 'en' })` = `'Multiple choice'`; `i18n.exists('questions:editor.newTitle', { lng: 'en' })` = false |
| T19 | same | `it('adds the editor strings after registering')` | after `registerQuestionsAdminLocales()`: `editor.newTitle` en `'New question'`, ar `'سؤال جديد'`; `questions:view.true` (en) still `'True'` (deep merge kept the base) |
| T20 | `src/shared/ui/button.test.tsx` (append) | `it.each(['primary','secondary','danger','ghost'])('keeps the %s hover fill off a disabled button')` | `renderWithProviders(<Button variant={v} disabled>Go</Button>)`: the button `toHaveClass` the variant's `hover:not-disabled:*` class and `not.toHaveClass` the bare `hover:bg-*` (class precedent: `ExamPage.test`, `TopNavMore.test`; jsdom cannot evaluate `:hover`) |
| T21 | same | `it('keeps the hover fill on an asChild link')` | `<Button asChild><a href="/x">Go</a></Button>`: the link `toHaveClass('hover:not-disabled:bg-accent-hover')` |

Existing suites that must stay green unchanged prove that registration happens in the right modules (they render through `renderApp`): `AuditLogPage`, `ExamBlueprintsPage`, `BlueprintEditor`, `UnitBlueprintSection`, `PaymentLogPage*`, `TrainingExportPage`, `AvatarConversation(s)Page`, `QuestionListPage`, `QuestionEditorPage`, `NewQuestionPage` + `New*Question`, `QuestionImportPage`, `ValidationQueuePage*`, `ValidationQuestionPage` + `Validation*Question`, `GradeReviewDetailPage`, `bundleBudget.test.ts`, `i18n.test.ts` (the original four), `users/locales.test.ts`.

## Definition of done
- [ ] `npm run build && npm run perf:budget` passes with `budgets.json` byte-identical to 75f44a8a.
- [ ] quiz spare ≥ 8192 B (expected ≈19.7 K); lesson, entry and landing totals are each lower than before (213830 / 223058 / 245462 B). No other page grows past its budget.
- [ ] `npm run perf:budget -- --breakdown=quiz` prints the quiz chunk table. Forcing a failure (temporarily lowering a budget locally, not committed) prints that page's table automatically.
- [ ] `dist/assets/index-*.js` contains no English UI string. Check: `grep -c "How many questions?" dist/assets/index-*.js` = 0, and `grep -l "Exam blueprints" dist/assets/index-*.js` finds nothing.
- [ ] The production entry still contains Arabic `questions:types` (`grep -c "اختيار من متعدد" dist/assets/index-*.js` ≥ 1), and no longer contains the admin editor string (`grep -c "جارٍ تحميل السؤال" dist/assets/index-*.js` = 0; it was 1 before).
- [ ] Deep merge of `questions/i18n/{ar,admin.ar}.json` equals the 75f44a8a `ar.json`; same for `en`.
- [ ] `button.tsx` diff touches only the four hover classes. Both design-system docs carry the "no hover fill" clause.
- [ ] `npm run typecheck`, `npm run lint`, `npm run format:check` (on Windows, `npx prettier --check "src/**/*.{ts,tsx,json,css}" "scripts/**/*.ts" --end-of-line auto`) and `npm test -- --run --coverage` all pass. Thresholds hold.
- [ ] No existing test edited except the appended blocks in `i18n.test.ts` and `button.test.tsx`; no `.skip`/`.only`.
- [ ] `docs/performance.md` §3 (measured column and "Diagnosing a failure"), §4, §8 (before/after with real numbers) and §9 are updated. No doc says English ships in the boot bundle.
- [ ] No route file, `vite.config.ts`, `app.css` or `budgets.json` change.
