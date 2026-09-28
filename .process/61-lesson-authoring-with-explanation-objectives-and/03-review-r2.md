VERDICT: APPROVED

# Review r2 — [E2.S2] Lesson authoring with explanation, objectives and summary (#61)

## Blocking
None.

## Round-1 findings
### 1. Editor in the entry chunk — RESOLVED
- `web/src/app/i18n.ts:5` now imports `@/features/content/locales`. `web/src/features/content/locales.ts:1-4` imports only the two JSON files. The barrel re-exports it at `web/src/features/content/index.ts:4`.
- `web/package.json` sets `"sideEffects": ["**/*.css"]`. I checked it: the only bare imports in production `src` are CSS (`main.tsx:1-7` and the KaTeX CSS in `RichTextEditor.tsx:1`/`RichTextViewer.tsx:1`). `./nodeFormData` and jest-dom are imported only in `src/test/setup.ts`, which vitest runs. No non-CSS module is dropped as a result.
- I rebuilt it myself (exit 0). `index-*.js` is 445.14 kB (138.54 kB gzip): ProseMirror 0, tiptap 0, katex 0, DOMPurify 0. `lesson._lessonId-*.js` is 752.05 kB (235.77 kB gzip): ProseMirror 51, tiptap 40, katex 52, DOMPurify 3. `content-*.js` is 24.36 kB with 0 editor hits. KaTeX CSS is split into `lesson-*.css`. The Vite >500 kB warning now applies only to the admin-only editor route. It is expected there and does not block.

### 2. Untested UI branches — RESOLVED as the finding worded it
Each mutant the finding named is now killed:
- block vs inline math: `RichTextEditor.test.tsx:78` asserts `.katex-display` and `data-type="block-math"`;
- toggleBold to toggleItalic: `RichTextEditor.test.tsx:97` asserts `aria-pressed` and `<strong>` in the PUT body;
- move down: `LessonEditorPage.test.tsx:132` asserts the order o2, o1;
- RichTextField error: `LessonEditorPage.test.tsx:182` uses `toHaveAccessibleDescription` on the Explanation textbox;
- objectives error: `LessonEditorPage.test.tsx:201` looks inside the Objectives group.

Every test the Fix prescribed exists. `ObjectivesField.tsx` and `RichTextField.tsx:35-39` are now covered.

## Non-blocking
- `web/src/features/content/components/RichTextToolbar.tsx:53-73`: the Italic, Heading, Bullet-list and Ordered-list handlers still have 0 hits, so a mutant such as `toggleItalic` to `toggleBold` survives. Round-1 #2 listed them under Where, but its Fix asked for "one toolbar test", and that test was delivered. Blocking now would move the goalposts. A table-driven test (one row per button, asserting `aria-pressed` and the tag in the PUT body) would close the gap cheaply.
- `web/src/features/content/components/RichTextEditor.tsx:70-71`: the dialog `onOpenChange(false)` close path (Escape or overlay) is uncovered.
- `web/src/test/setup.ts:10`: `asyncUtilTimeout: 3000` was directed by the orchestrator (issue #142). Accepted.
- The round-1 non-blocking items are unchanged and still stand.

## Verified
- `npm --prefix web run build`: exit 0, chunk sizes and contents as above. They match the table in 02-implementation-r2.md exactly, except that tiptap has 40 hits in the lesson chunk rather than the reported 34. My count is case-insensitive, and that does not matter here.
- `npm --prefix web test -- --run --coverage`: 42/42 files, 216/216 tests. Coverage is 94.03 % stmts / 81.02 % branch / 89.11 % funcs / 93.97 % lines, identical to the claim. No first-test timeouts.
- `npm --prefix web run lint`: clean. `tsc -b`: clean.
- The rework touched only the files it lists: `locales.ts` (new), `i18n.ts`, `index.ts`, `package.json`, `setup.ts` and the two test files. The API is untouched. The round-1 API verification (build, 465/465 tests, Postman, docs) still holds.
- Deviations are declared honestly: the `sideEffects` field and the new `locales.ts` were both required by the finding-1 fix and its verification step. The routes still import through the barrel (react-feature §17).

## Test quality
The five new tests all constrain real behaviour: exact PUT bodies, accessible descriptions and DOM classes, with no stubbed-return assertions. The Ctrl+A-then-Bold approach is a sound workaround for jsdom/ProseMirror stored marks. The earlier test-quality assessment carries over. The only remaining gap is the four toolbar buttons (non-blocking, above).
