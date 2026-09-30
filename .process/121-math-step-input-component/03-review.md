VERDICT: APPROVED

# Review — [E15.S1] Math step input component (#121)

## Blocking
None.

## Non-blocking
- `web/src/features/mathSteps/hooks/useMathStepsDraft.ts:28-33`: a restored draft is never emitted through `onChange`. So a parent that mirrors the value (the future exam server autosave) holds the stale server value until the student's first edit. Decision 12 accepts this. #122 should either call `onChange` once on restore or read `MathStepsAnswer`'s value on submit.
- `web/src/features/mathSteps/hooks/useMathStepsDraft.ts:39-46`: `flush` (on `pagehide`, `visibilitychange` or unmount) writes but never updates `status`. The line also keeps saying «حُفظت المسودة…» while a newer edit is still pending. This is cosmetic.
- `web/src/features/mathSteps/components/MathStepsInput.tsx:29-31`: the keypad toggle uses `size="sm"` (`min-h-9`, 36 px). The plan specifies this, and it is above the 24 px WCAG minimum, but it is the only control under 44 px in a phone-first component.
- `web/src/features/mathSteps/components/MathKeypad.tsx:28-30`: removing `preventDefault` on `pointerdown` goes undetected in jsdom, as the report admits. It needs a browser-mode or Playwright check once the component is mounted (#122).
- `web/src/features/mathSteps/components/MathStepsAnswer.autosave.test.tsx:50-62` (T54): it relies on `shouldAdvanceTime` with about 400 ms of margin, so it could flake on a loaded CI runner.
- There is no test for the remount on an `owner` change (Decision 14, `MathStepsAnswer.tsx:13`). The report says so.
- Several line counts in `02-implementation.md` (the "Files created" table) are wrong. For example, `mathDraftStore.test.ts` is 101 lines, not 115; `mathDraftStore.ts` is 81, not 83; `MathField.tsx` is 48, not 51; `docs/math-input.md` is 97, not 104. This does not affect the review.

## Verified
- **Commands, re-run in `D:/Personal/elmanhg-wt/121/web`:**
  - `npm run typecheck`: exit 0.
  - `npm run lint`: exit 0.
  - `npx prettier --check --end-of-line auto .`: all files clean.
  - `npx vitest run --coverage`: 198 files, 1146 tests passed, EXIT=0, thresholds met.
    - mathSteps `api`: 97.4 % lines, 95 % branches.
    - mathSteps `hooks`: 95.1 % lines, 84.3 % branches.
    - mathSteps `components`: 100 %.
  - `npm run build`: OK.
  - `npm run perf:budget`: entry 201/210, landing 210/220, lesson 230/240, quiz 249/255. All pass, and no budget was raised.
- **Footprint:**
  - `git status`/`git diff` show exactly the 3 modified files (`web/src/app/i18n.ts`, `docs/design-system.md`, `.claude/design-system.md`) plus the 25 planned new files.
  - Nothing changed under `api/`, `ai/`, `postman/`, `questions`/`quiz`/`exam`, generated code, `package.json` or the lockfile.
  - No `katex` import in the feature.
- **Signatures and decisions:**
  - F4–F17 match the plan's signatures and behaviour: limits, `toLatinDigits` ranges, `&`-first escaping, the wrap/backspace/caret rules, the `Map` lookup guard, TTL `>`, remove-on-invalid, the purge prefix, and `try/catch` on all storage access.
  - Focus rules match Decision 8. Draft-wins, the 800 ms debounce, and the flushes on `pagehide`, hidden `visibilitychange` and unmount are in place. The keypad renders only under the active field, only when open and not disabled. The fields get `inputMode` none/text. The toggle has `aria-pressed`.
- **Barrel:** `index.ts` exports exactly the plan's surface.
- **i18n:**
  - The `en` and `ar` key trees are identical.
  - The strings match the plan's table.
  - `mathSteps` is registered in `resources.ar`, `resources.en` and `ns`.
- **Skill §3/§6:**
  - All classes are tokens that exist in `src/styles/app.css` (`py-2.25` and `disabled:opacity-45` are existing repo patterns).
  - The physical-direction, hex and arbitrary-value greps are clean.
  - No `forwardRef`, memo, `any`, `!`, default exports or index keys.
  - Icon-only buttons have translated `aria-label`s, and `dir="ltr"` is on the fields and keypad.
  - Every file is ≤ 200 lines and every component ≤ 120.
- **Deviations:** all three are declared and acceptable.
  - Extra assertions went inside the planned tests; no test was renamed or dropped.
  - The perf figure turned out as measured, not as the plan estimated.
  - The ref callback has a `null` guard.
- **Postman:** no API change, so none is needed.
- **Docs sync:**
  - `docs/math-input.md` has the 7 sections and agrees with the code (key, JSON shape, debounce, TTL, limits, keypad table).
  - `docs/design-system.md` §5.12 and the `.claude/design-system.md` `MathInput` row agree with each other and with `MathField`, `MathKeypad` and `MathStepRow`.
  - PRD §6 (steps LaTeX/text) and the "math input with LaTeX preview" line are consistent.
  - The deferred question type (#122) is incompleteness, not divergence.

## Test quality
- `mathStepsValue` (T1–T14), `applyMathKey`/`mathKeyRows` (T15–T24) and `mathDraftStore` (T25–T32) test pure functions with exact expected outputs, identity (`toBe(value)`) for the refusal paths, and boundaries (TTL exactly vs +1, 20/21 steps, 500/501, 200/201). They constrain the implementation.
- `MathStepsAnswer steps` (T33–T44) asserts real DOM effects: focus targets after add, remove and move; the announcements; the reorder values; the disabled states; the KaTeX `<math>` output; the `ar` label and `dir`; axe with the keypad open.
- `MathStepsAnswer keypad` (T45–T52) drives the real caret and selection path (ArrowLeft, triple-click, the caret-move key followed by Delete, the length guard past `maxLength`). The one gap is the `preventDefault` mutant above.
- `MathStepsAnswer autosave` (T53–T62) checks the actual `localStorage` contents, not a mock's return. The storage-failure tests spy on `Storage.prototype` and assert the visible outcome.
- No vacuous tests were found. The implementer's mutation run (15 of 16 mutants killed) is consistent with what I read.
