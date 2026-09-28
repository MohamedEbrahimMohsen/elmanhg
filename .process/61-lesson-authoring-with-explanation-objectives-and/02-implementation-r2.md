# Implementation r2 (rework) — [E2.S2] Lesson authoring (#61)

## Findings addressed
| # | What I changed | File:line |
|---|---|---|
| 1 | Added a side-effect-free locales module and pointed `app/i18n.ts` at it. The barrel re-exports it and no longer imports the JSON itself. | `web/src/features/content/locales.ts:1-4` (new); `web/src/app/i18n.ts:5`; `web/src/features/content/index.ts:4` |
| 1 | Declared `"sideEffects": ["**/*.css"]` in `web/package.json`. Without it, rolldown put the whole barrel (ContentPage, LessonEditorPage, TipTap, KaTeX) into one shared `content-*.js` chunk that both admin routes load. With it, the editor sits only in the `lesson.$lessonId` chunk, as the review's verification step requires. See Deviations. | `web/package.json:84-86` |
| 2 | Added "inserts a block formula": it ticks "Show on its own line", checks `.katex-display` in the preview, and checks the PUT body for `data-type="block-math"` and the LaTeX. | `web/src/features/content/components/RichTextEditor.test.tsx:78` |
| 2 | Added "applies bold from the toolbar": it selects all text in Explanation, clicks Bold, checks `aria-pressed="true"`, and checks `<strong>` in the PUT body. | `web/src/features/content/components/RichTextEditor.test.tsx:97` |
| 2 | Added "moves an objective down", which checks the PUT objective order o2, o1. | `web/src/features/content/pages/LessonEditorPage.test.tsx:132` |
| 2 | Added the PUT 422 `LESSON_EXPLANATION_TOO_LONG` case. The Explanation textbox must have the message as its accessible description. | `web/src/features/content/pages/LessonEditorPage.test.tsx:182` |
| 2 | Added the PUT 422 `LESSON_OBJECTIVES_TOO_MANY` case. The message must appear inside the Objectives group (fieldset). | `web/src/features/content/pages/LessonEditorPage.test.tsx:201` |
| O | **Orchestrator-directed, not a review finding (issue #142):** raised the Testing Library `asyncUtilTimeout` to 3000 ms once, centrally. | `web/src/test/setup.ts:3,10` |

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `web/src/features/content/locales.ts` | 4 | Content locales without component imports, safe to import from `app/i18n.ts` |

## Files modified
| Path | Change |
|---|---|
| `web/src/app/i18n.ts` | Imports `contentLocales` from `@/features/content/locales` |
| `web/src/features/content/index.ts` | Re-exports `contentLocales` from `./locales` |
| `web/package.json` | Adds `"sideEffects": ["**/*.css"]` |
| `web/src/test/setup.ts` | `configure({ asyncUtilTimeout: 3000 })` (orchestrator-directed) |
| `web/src/features/content/components/RichTextEditor.test.tsx` | +2 tests |
| `web/src/features/content/pages/LessonEditorPage.test.tsx` | +3 tests |

## Deviations
| Plan/review said | Reality | What I did |
|---|---|---|
| Finding 1 fix: "expose locales from a side-effect-free module … import that path in `app/i18n.ts`", then confirm TipTap sits in the `lesson.$lessonId` chunk | That alone cleaned the entry chunk (1,217.18 kB to 435.87 kB). But rolldown then emitted a shared `content-BZuDZ-ED.js` (781 kB, with ProseMirror, TipTap, KaTeX and DOMPurify) that both `/admin/content` and `/admin/lesson/$lessonId` import, because both routes import the same barrel. | Added `"sideEffects": ["**/*.css"]` to `web/package.json`, so rolldown can split the barrel's re-exports per route. `package.json` is outside the finding's named files. The only bare side-effect imports in `src` are CSS and two test-setup imports (`src/test/setup.ts`), and vitest does not use this field. I kept the routes' barrel imports (react-feature §17) instead of deep-importing `pages/LessonEditorPage`. |
| New file `features/content/locales.ts` | Not in the original plan's *Files to create* | The review's fix prescribed it |

## Build & test
`npm --prefix web run build` (exit 0), chunk sizes:

| Chunk | Before | After |
|---|---|---|
| `index-*.js` (entry) | 1,217.18 kB (379.94 kB gzip); ProseMirror 51 hits, tiptap 34, katex 52, DOMPurify present | 445.14 kB (138.54 kB gzip); ProseMirror, tiptap, katex and DOMPurify all 0 hits |
| `lesson._lessonId-*.js` | 0.26 kB | 752.05 kB (235.77 kB gzip); ProseMirror 51, tiptap 34, katex 52, DOMPurify 3 |
| `content-*.js` (subjects/units page) | n/a (all in entry) | 24.36 kB (7.46 kB gzip); editor libraries 0 hits |

With the locales fix alone (no `sideEffects`), the entry was 435.87 kB and the shared `content` chunk was 781.11 kB with the editor inside it.

KaTeX CSS also moved out of the entry: `index-*.css` is now 40.17 kB, and `lesson-*.css` is 29.79 kB.

`npm --prefix web run lint`: 0 warnings.

`npm --prefix web test -- --run --coverage`, three runs back to back:
- Run 1: Test Files 42 passed (42); Tests 216 passed (216); All files 94.03 % stmts / 81.02 % branch / 89.11 % funcs / 93.97 % lines
- Run 2: identical, 42/42 files, 216/216 tests, 94.03 / 81.02 / 89.11 / 93.97
- Run 3: identical, 42/42 files, 216/216 tests, 94.03 / 81.02 / 89.11 / 93.97

API untouched in this rework; not re-run.

## Notes for review
- The review asked for one toolbar test, so the Italic, Heading and list toggles in `RichTextToolbar.tsx` still have 0 hits. Add one test per button if the per-branch rule is meant to cover them.
- The bold test selects all text (Ctrl+A) and then clicks Bold, instead of "click Bold, then type". In jsdom, ProseMirror dropped the first typed character after a stored mark (it produced `<strong>ewton</strong>`), so the type-first form was flaky by construction.
- `prettier --check` flags `package.json` and several older files for CRLF line endings from the Windows checkout. This was already the case before this rework; my edit kept CRLF in `package.json`.
- Both test files are now over 100 lines (157 and 237). The ~100-line cap is written for production code.
