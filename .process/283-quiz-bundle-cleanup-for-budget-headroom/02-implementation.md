# Implementation — Quiz bundle cleanup for budget headroom (#283, E19.S3)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `web/src/features/questions/i18n/admin.ar.json` | 345 | F1: `statuses`, `difficulties`, `list`, `editor`, `import`, `validation` moved verbatim from `ar.json` |
| `web/src/features/questions/i18n/admin.en.json` | 345 | F2: same split for `en.json` |
| `web/src/features/questions/questionsAdminLocales.ts` | 9 | F3: `registerQuestionsAdminLocales()` (deep-merges into `questions`) |
| `web/src/features/audit/locales.ts` | 9 | F4: `registerAuditLocales()` |
| `web/scripts/perf/budgetDiagnostics.ts` | 80 | F5: `sourceOf`, `pageBreakdown`, `formatBreakdown`, `breakdownPageNames`, `breakdownFlag` |
| `web/scripts/perf/budgetDiagnostics.test.ts` | 88 | F6: T10 to T17 |
| `web/src/features/audit/locales.test.ts` | 16 | F7: T5 |
| `web/src/features/blueprints/locales.test.ts` | 16 | F8: T6 |
| `web/src/features/payments/locales.test.ts` | 16 | F9: T7 |
| `web/src/features/trainingExport/locales.test.ts` | 16 | F10: T8 |
| `web/src/features/avatarConversations/locales.test.ts` | 16 | F11: T9 |
| `web/src/features/questions/questionsAdminLocales.test.ts` | 18 | F12: T18, T19 |

## Files modified
| Path | Change |
|---|---|
| `web/src/app/i18n.ts` | Arabic-only boot resources (`arabicResources`), `lazyLanguageBundles.en` (16 loaders), exported `loadLanguage(lng, loaders?)` (all-or-nothing), `ns: Object.keys(arabicResources)`, `resources: { ar: arabicResources }`; the five admin namespaces and `commonEn` are gone. The re-init branch now loads before it switches (see Deviations). |
| `web/src/test/setup.ts` | Imports `loadLanguage`; `await loadLanguage('en');` after `initI18n('en');` |
| `web/src/features/{askTeacher,avatar,browse,content,exam,landing,mastery,mathSteps,onboarding,progress,questions,quiz,subscription}/locales.ts` | `{ ar, en: () => import('./i18n/en.json') }`; static `en` import removed |
| `web/src/features/shell/index.ts`, `web/src/features/session/index.ts` | Same loader pattern for `shellLocales` / `sessionLocales` |
| `web/src/features/audit/index.ts` | `import ar`, `import en`, `auditLocales` removed |
| `web/src/features/{blueprints,payments,trainingExport,avatarConversations}/locales.ts` | Body replaced by `register<X>Locales()` (users pattern) |
| `AuditLogPage`, `ExamBlueprintsPage`, `PaymentLogPage`, `TrainingExportPage`, `AvatarConversationsPage`, `AvatarConversationPage` | Module-scope `register<X>Locales();` after the last import |
| `questions/pages/{QuestionList,QuestionEditor,NewQuestion,QuestionImport,ValidationQueue,ValidationQuestion}Page.tsx`, `questions/components/GradingKeyView.tsx` | Module-scope `registerQuestionsAdminLocales();` |
| `web/src/features/questions/i18n/{ar,en}.json` | Keep only `types`, `preview`, `essayGrade`, `view`, `mathStepGrade` (pure line deletions; deep merge with `admin.*` equals `75f44a8a` for both, checked with `util.isDeepStrictEqual`) |
| `web/src/shared/ui/button.tsx` | Four hover fills → `hover:not-disabled:*`; nothing else |
| `web/src/shared/ui/button.test.tsx` | Appended T20 (`it.each` over 4 variants) and T21; existing 2 tests byte-identical |
| `web/src/app/i18n.test.ts` | Import line extended (`initI18n, loadLanguage`); appended T1 to T4 plus one extra test (see Deviations); existing 4 tests byte-identical |
| `web/scripts/perf/budgetCli.ts` | Memoised `sizeOf`; breakdown loop after the report; exit-code logic unchanged |
| `docs/performance.md` | §3 measured column (real run) + "Diagnosing a failure"; §4 new "Arabic at boot, English on demand" bullet + admin-only strings extended; §8 "Bundle cleanup (#283), 2026-10-03" with measured before/after and quiz chunk table; §9 English row removed, five new lever rows |
| `docs/design-system.md` | §5.1: "Disabled: 45 percent opacity, and no hover fill." |
| `.claude/design-system.md` | Button row: "disabled (45% opacity, no hover fill)" |

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| `initI18n`: "same body" apart from `resources`/`ns`; re-init branch stays `void i18n.changeLanguage(lng)` | Orchestrator condition (00-acceptance): any language switch must load the English bundle and re-render. The only switch API in production code is `initI18n(lng)` on an initialised instance; unchanged, it would switch to `en` with no English resources (Arabic fallback forever). | Re-init branch is now `void loadLanguage(lng).then(() => i18n.changeLanguage(lng));`. `changeLanguage` fires `languageChanged`, which react-i18next binds for re-render. |
| Test plan for `i18n.test.ts`: T1 to T4 only | The switch change above needs a test | Added `it('loads English before an initialised instance switches to it')`: removes the `en/quiz` bundle, sets `ar`, calls `initI18n('en')`, and asserts that when `languageChanged` fires, `t('quiz:practice.choose')` is already `'How many questions?'` and `<html lang="en">`. |
| F1: keys "in that source order" | Ambiguous: the list order (`list, editor, …`) differs from the source order (`statuses, difficulties, list, editor, import, validation`) | Used the source order, so `{ar,en}.json` diffs are pure deletions and `admin.*.json` reads as the moved block. |

No other deviations. `fallbackLng` is unchanged (`'ar'`); a missing Arabic key behaves as before (returns the key). The en→ar fallback while English is missing is covered by T3, and failed loads by T2.

## Build & test
All run in `D:/Personal/elmanhg-wt/283/web`, no commits.

- `npm run typecheck` → exit 0
- `npm run lint` → exit 0 (`eslint . --max-warnings=0`)
- `npx prettier --check "src/**/*.{ts,tsx,json,css}" "scripts/**/*.ts" --end-of-line auto` → "All matched files use Prettier code style!" (`budgetDiagnostics.ts` needed one `--write` first)
- `npx vitest run --coverage` → exit 0, `Test Files 296 passed (296)`, `Tests 1726 passed (1726)`; Statements 95.42 %, Branches 84.05 %, Lines 95.55 %. No timeouts, so no reruns were needed.
- `npm run build` → exit 0 ("Precompressed 292 files")
- `npm run perf:budget -- --breakdown=quiz` → exit 0:
  ```
  entry 190/210 KB ok
  landing 199/220 KB ok
  lesson 221/240 KB ok
  quiz 236/255 KB ok
  admin-dashboard 213/233 KB ok
  admin-users 245/270 KB ok
  teacher-home 246/265 KB ok

  quiz: 241437 B of 261120 B brotli (19683 B spare)
    131732 B  54.6 %  assets/index-DCFxsNdo.js  index.html
  ...
  ```
- Forced failure: I set lesson `maxKb` to 200 in my working copy, which printed `lesson 221/200 KB OVER` plus `lesson: 225853 B of 204800 B brotli (21053 B over)` and its chunk table with no flag. I then restored it with `git checkout`. `budgets.json` is byte-identical to `75f44a8a`.
- CI greps from `web-ci.yml` (physical directions, literal tokens) → both find nothing (grep exit 1, so the step passes).
- Route tree: `git status --porcelain src/routeTree.gen.ts` → empty.
- DoD greps on `dist/assets/index-*.js`: "How many questions?" 0, "Exam blueprints" 0, "اختيار من متعدد" 1, "جارٍ تحميل السؤال" 0 (it was 1 before).

### Measured before / after (bytes brotli, budget = maxKb × 1024)
Before = `git stash -u` of this change on `75f44a8a`, `npm run build`, then the same closure and brotli settings as `perf:budget`. After = this change.

| Page | Budget | Before | Spare before | After | Spare after | Δ |
|---|---|---|---|---|---|---|
| entry | 215040 | 213830 | 1210 | 194202 | 20838 | −19628 |
| landing | 225280 | 223058 | 2222 | 203452 | 21828 | −19606 |
| lesson | 245760 | 245462 | 298 | 225853 | 19907 | −19609 |
| quiz | 261120 | 261067 | 53 | 241437 | 19683 | −19630 |
| admin-dashboard | 238592 | 237687 | 905 | 218105 | 20487 | −19582 |
| admin-users | 276480 | 270172 | 6308 | 250585 | 25895 | −19587 |
| teacher-home | 271360 | 264063 | 7297 | 250982 | 20378 | −13081 |

The before numbers match the plan exactly. Quiz spare is 19683 B (≥ 8192 B required), and no budget was changed.

## Notes for review
- `initI18n('en')` on a fresh instance (test setup only) still initialises with Arabic-only resources. Setup then awaits `loadLanguage('en')` before any test renders. Production always boots with `ar`.
- After `loadLanguage` adds bundles there is no `languageChanged` event. That is why every switch must go through `loadLanguage` then `changeLanguage`, as `initI18n` now does.
- `docs/performance.md` §8's 2026-09-29 entry still says "every locale" is in the entry chunk. That is a dated historical record and I left it as is. The new §4 bullet and §8 subsection give the current state.
- The §8 quiz table counts 45 chunks under 1.7 KB at 23155 B in total. The plan's "≈11.5 K" for the same bucket looks like an estimate; mine is measured, and the rows sum to 241437 B.
- `budgetDiagnostics.ts` keeps a private `bytesPerKb` constant, because `bundleBudget.ts` does not export its own and the plan forbids touching that file.
