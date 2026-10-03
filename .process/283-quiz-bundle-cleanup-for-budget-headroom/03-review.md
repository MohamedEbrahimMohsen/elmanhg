VERDICT: CHANGES_REQUESTED

# Review — Quiz bundle cleanup for budget headroom (#283, E19.S3)

## Blocking

### 1. The hover fix makes hover outrank the pressed and toggled states, so buttons change visibly
**Where:** `web/src/shared/ui/button.tsx:6` (primary/accent) and `web/src/shared/ui/button.tsx:16` (secondary)
**Rule:** story scope ("nothing visible changed", buttons otherwise unchanged); plan D9 ("`active:` fills unchanged"); `.claude/design-system.md` Components › Button ("hover accent.hover, pressed accent.pressed"; secondary "aria-pressed → accent.soft") and `docs/design-system.md` §5.1.
**Problem:** Tailwind v4 compiles `hover:not-disabled:bg-accent-hover` to `.hover\:not-disabled\:bg-accent-hover:hover:not(:disabled)`. That selector has specificity (0,3,0). The rules it competes with are still (0,2,0): `.active\:bg-accent-pressed:active` and `.aria-pressed\:bg-accent-soft[aria-pressed=true]`. Before this change, every rule was (0,2,0) and the `active:`/`aria-pressed:` rules came later in the stylesheet, so they won. Now the hover rule wins whenever the pointer is over the button. The built CSS confirms this: `dist/assets/index-*.css` has the hover rule at offset ≈43018 and `active:bg-accent-pressed` at ≈45252.
**Failure:** (a) Hold the mouse down on any enabled primary or accent button. Before, it showed `accent.pressed` (#3730a3). Now it stays `accent.hover` (#4338ca), so the pressed state is gone on desktop. (b) Hover a toggled secondary button (`aria-pressed="true"`), for example `PaymentLogTabs`, `MethodSwitch`, `GradeReviewKindTabs` or the `RichTextToolbar` buttons. Before, it kept `accent.soft`. Now it turns `bg-soft` while hovered. T20 and T21 only check class names, so they cannot catch either case.
**Fix:** Restore the precedence of the pressed and toggled states over the guarded hover. One way is to give the competing fills the same compound, for example `active:not-disabled:bg-accent-pressed` and `aria-pressed:hover:not-disabled:bg-accent-soft`. Any equivalent works. Then check in the built `index-*.css` that those rules have at least the hover rule's specificity and come after it, and report the check. Keep the hover fill off disabled buttons and on `asChild` links.

## Non-blocking
- `web/src/app/i18n.ts:92`: `void loadLanguage(lng).then(() => i18n.changeLanguage(lng))` has no rejection handler. When a chunk fails, the language correctly stays Arabic, but the rejection is unhandled and reaches the `unhandledrejection` reporter (`shared/lib/clientErrorReporter.ts:67`). Two quick calls (`'en'` then `'ar'`) can also finish in the wrong order. Nothing in production calls this branch (only `main.tsx:19` calls `initI18n()`, on a fresh instance), so this is latent. A `.catch` that keeps the current language, plus a test, would close it.
- `web/src/app/i18n.ts:90-105`: `initI18n('en')` on a fresh instance still loads no English. Only `test/setup.ts` relies on this, and it awaits `loadLanguage('en')` itself. Worth a note on the function, or making the fresh path load too.
- Tests: no admin page is rendered in Arabic (`lng: 'ar'` appears in no admin, question-editor or validation page test). The English preload in `setup.ts` does not hide a first-render bug, because Arabic resources are inline and each `register*Locales` adds `ar` and `en` together. Still, one `renderApp('/admin/audit', { lng: 'ar' })`-style test would pin the Arabic first render of a page-registered namespace.
- `web/scripts/perf/budgetCli.ts:62`: `--breakdown=nope` throws a raw stack trace after the report. That is correct behaviour (D10), but a one-line message would read better in a CI log.
- Browser check with `vite preview` was not done: there is no Playwright or browser tool in this environment, and admin routes need a session. Production i18next runs without `debug`, so missing keys would not log to the console anyway. The route-level `renderApp` suites cover registration before render.

## Verified
- Typecheck (`tsc -b`) and lint (`--max-warnings=0`) exit 0. `prettier --check … --end-of-line auto` reports clean.
- `vitest run`: 296 files and 1726 tests passed. Coverage: statements 95.42 %, branches 84.1 %, lines 95.55 %. No timeouts.
- `npm run build` exits 0 (292 files precompressed). Both CI greps from `web-ci.yml` find nothing (exit 1).
- `perf:budget -- --breakdown=quiz,teacher-home` shows every page ok: entry 190, landing 199, lesson 221, quiz 236/255, admin-dashboard 213, admin-users 245, teacher-home 246. Quiz is 241437 B of 261120 B (19683 B spare), matching the report and `docs/performance.md` §8.
- `budgets.json`, `bundleBudget.ts`, `vite.config.ts`, `app.css`, `src/routes/**` and `routeTree.gen.ts` are unchanged against origin/main. Only `budgetCli.ts` changed under `scripts/perf`.
- DoD greps on `dist/assets/index-DCFxsNdo.js`: "How many questions?" 0, "Exam blueprints" 0, "اختيار من متعدد" 1, "جارٍ تحميل السؤال" 0. The admin string now lives only in `questionsAdminLocales-*.js`.
- Merging the new `questions/i18n/{ar,en}.json` with `admin.{ar,en}.json` deep-equals the origin/main files. Top-level keys do not overlap.
- Registration happens before the first render on every route. Each admin route uses `autoCodeSplitting`, so its component is a lazy chunk. Each page module calls `register*Locales()` at module scope, after its imports, before the component can render. Refresh and deep links take the same path.
- No consumer of the five namespaces exists outside its feature. Each barrel exports only the page plus schemas or helpers that use no `t()`. The zod error keys are rendered only inside those pages.
- Every admin-group `questions` key (`list`, `editor`, `import`, `validation`, `statuses`, `difficulties`) is used only by components reached from the six question pages, or from `GradingKeyView` through `EssayRubricView` and `MathAnswerRulesView`. The student components (`QuestionView`, the answer inputs, `MathStepsReadOnly`, the score lists, the review items, `ExamQuestionCard`, blueprints, `QuestionTypesCard`) use only base groups. `dashboard/ValidationCard` uses the `dashboard` namespace.
- `loadLanguage` is all-or-nothing (`Promise.all` before any `addResourceBundle`), and `fallbackLng: 'ar'` is unchanged.
- Both design-system docs carry the "no hover fill" clause. `docs/performance.md` §3, §4, §8 and §9 are updated and agree with the code. No other doc refers to the removed exports or to English in the boot bundle. No docs-sync divergence, apart from the button pressed state in finding 1.
- Deviations listed in 02-implementation.md (re-init loads before switching, the extra test, source-order JSON) are present as described.

## Test quality
- `budgetDiagnostics.test.ts`: constrains the code. It covers ordering, sources, CSS attribution, the spare and over lines, the flag parsing and the unknown-page throw.
- `*/locales.test.ts` (×5) and `questionsAdminLocales.test.ts`: constrain the code. Each fails if the namespace is eager (first test) or if registration or the deep merge is wrong. These depend on file order inside one worker; that is acceptable because each file is isolated.
- `i18n.test.ts` additions: T1, T2, T4 and the switch test constrain the code (T2 fails without all-or-nothing; the switch test fails without load-before-switch). T3 only exercises i18next's own `fallbackLng`, which is a weak check but harmless.
- `button.test.tsx` T20/T21: these check class strings only, so they cannot detect finding 1. They also pass for any class order or specificity.
