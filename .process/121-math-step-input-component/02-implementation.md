# Implementation — [E15.S1] Math step input component (#121)

Worktree `D:/Personal/elmanhg-wt/121`, branch `feature/121-math-step-input-component`. Not committed.

## Files created
All under `web/src/features/mathSteps/` unless noted.

| Path | Lines | Purpose |
|---|---|---|
| `locales.ts` | 4 | `mathStepsLocales = { ar, en }` |
| `i18n/en.json` | 72 | `mathSteps` namespace (en), exact plan strings |
| `i18n/ar.json` | 72 | `mathSteps` namespace (ar), same key tree |
| `api/mathStepsValue.ts` | 123 | F4: value and payload types, limits, step add/remove/move, field get/set, `toLatinDigits`, payload mapping, `isMathStepsPayload`, `toMathPreviewHtml` |
| `api/mathKeys.ts` | 159 | F5: the 6×6 `mathKeyRows` and `applyMathKey` (insert, wrap, backspace, caret moves) |
| `api/mathDraftStore.ts` | 83 | F6: storage key, read (TTL and validation, removes bad drafts), write, clear, purge; all storage access in try/catch |
| `hooks/useMathFieldFocus.ts` | 72 | F7: field and move-button registries (React 19 ref cleanup), `focusNow`, `focusAfterRender` (effect without deps) |
| `hooks/useMathStepsEditor.ts` | 110 | F8: active field, announcements, add/remove/move with focus rules, `pressKey` with limit guard |
| `hooks/useMathStepsDraft.ts` | 77 | F9: draft-wins initial state, 800 ms debounce, flush on `pagehide`, hidden `visibilitychange`, unmount; purge on mount |
| `components/MathPreview.tsx` | 29 | F10: preview group, empty hint or `RichTextViewer` block-math |
| `components/MathField.tsx` | 51 | F11: labelled LTR mono 16 px textarea, `inputMode` none/text, preview |
| `components/MathKeypad.tsx` | 42 | F12: `grid-cols-6` LTR keypad, `preventDefault` on `pointerdown`, translated `aria-label` per key |
| `components/MathStepRow.tsx` | 74 | F13: step card with field, keypad slot, move up/down (ghost) and remove (danger) icon buttons |
| `components/MathStepsInput.tsx` | 80 | F14: controlled editor: hint, keypad toggle (`aria-pressed`), fieldset/ol of steps, add + max note, final answer, sr-only status |
| `components/MathDraftStatus.tsx` | 16 | F15: `role="status"` draft line (danger on error) |
| `components/MathStepsDraftEditor.tsx` | 16 | F16: `useMathStepsDraft` + `MathStepsInput` + `MathDraftStatus` |
| `components/MathStepsAnswer.tsx` | 14 | F17: remounts `MathStepsDraftEditor` by `key={mathDraftStorageKey(owner)}` |
| `index.ts` | 11 | F18 barrel, exactly the plan's exports |
| `api/mathStepsValue.test.ts` | 133 | T1–T14 |
| `api/mathKeys.test.ts` | 62 | T15–T24 |
| `api/mathDraftStore.test.ts` | 115 | T25–T32 |
| `components/MathStepsAnswer.steps.test.tsx` | 146 | T33–T44 |
| `components/MathStepsAnswer.keypad.test.tsx` | 111 | T45–T52 |
| `components/MathStepsAnswer.autosave.test.tsx` | 153 | T53–T62 |
| `docs/math-input.md` (repo root) | 104 | F25: the 7 sections (purpose/E15 map, answer shape, input, keypad, steps list, autosave, integration checklist) |

## Files modified
| Path | Change |
|---|---|
| `web/src/app/i18n.ts` | Imports `mathStepsLocales` after `masteryLocales`; adds `mathSteps` to `resources.ar`, `resources.en` and `ns`, each after `mastery` |
| `docs/design-system.md` | New `### 5.12 Math input` after §5.11 (16 px monospace LTR field, preview box, 6×6 keypad of ≥ 44 px keys on a `--soft` `--r-md` panel under the active field, step cards with ≥ 44 px icon buttons) |
| `.claude/design-system.md` | Components table: `MathInput` row after `Skeleton`, the plan's text verbatim. `npm run gen:tokens` produces no diff |

Nothing under `api/`, `ai/`, `postman/`, `web/src/features/{questions,quiz,exam}/`, `web/src/shared/api/generated/`, `package.json` or the lockfile was touched. There is no new dependency. `grep katex web/src/features/mathSteps` returns 0 hits.

## Deviations
| Plan said | Reality | What I did |
|---|---|---|
| Test plan "Asserts" column for T7, T13, T18, T27, T28, T35, T44, T49, T52, T61, T62 | These are the plan's minimum assertions. A few branches (the caret keys through the UI, the visible `visibilitychange`, the TTL boundary, the `'` and `>` escapes) had no test | I kept every named test and every planned assertion, and added assertions **inside the same tests**. T7 also checks `removeMathStep` with an unknown id. T13's input adds `'>` (so the expected string ends `&#39;&gt;`). T18 also wraps a reversed selection with `text`. T27 checks that a draft exactly at the TTL is still read. T28 also covers a stored `null`. T35 checks the announcement. T44 opens the keypad before the axe scan. T49 then clicks «Move cursor left» and Delete, which gives `b`. T52 checks focus stays on the field. T61 checks that a *visible* `visibilitychange` does not flush. T62 checks the value survives. No test was added, renamed or dropped: 62 tests, T1–T62. |
| Decision 17: "entry 197 KB of 210" | `docs/performance.md` §"Bundle sizes after this change" already records entry 201, landing 209, quiz 248 on `main`. The 197 figure is the older table row | This is an estimate, not a contract. Measured after the change: entry 201/210, landing 210/220, quiz 249/255, lesson 230/240. All are within budget, and no budget was raised. |
| F7: register callbacks `set` the element | `@types/react` types a `RefCallback<T>` argument as `T \| null`, so tsc rejects the literal code | Each register callback returns early on `null`, then sets the element and returns the cleanup. Behaviour is unchanged under React 19 ref cleanups. |

## Build & test
All commands ran in `D:/Personal/elmanhg-wt/121/web` (`npm ci` first, because the worktree had no `node_modules`).

- `npm run typecheck` (`tsc -b`): exit 0, no output.
- `npm run lint` (`eslint . --max-warnings=0`): exit 0, no findings.
- `npx prettier --check --end-of-line auto .`: `All matched files use Prettier code style!`
- `npx vitest run --coverage` (full suite): `Test Files 198 passed (198)`, `Tests 1146 passed (1146)`, EXIT=0. Coverage thresholds passed.
  - Coverage for `features/mathSteps`, lines / branches:

    | Folder | Lines | Branches |
    |---|---|---|
    | `api` | 97.2 % | 95 % |
    | `hooks` | 93.3 % | 82.4 % |
    | `components`, `useMathStepsDraft`, `index`, `locales` | 100 % | 100 % |

  - The reporter omits fully covered files, which is why those four are not listed separately.
  - The branch figure for `useMathStepsEditor.ts` in that run was 68 %. It now covers caret-key lines 90–91, through the extra T49 assertions added after the run.
- `npx vitest run --project dom src/features/mathSteps` (after the last test edit): `Tests 62 passed (62)`.
- `npm run build`: `✓ built in 12.33s`, `Precompressed 194 files`. Only the usual >500 kB chunk warning appeared, from the existing `index`.
- `npm run perf:budget`: `entry 201/210 KB ok`, `landing 210/220 KB ok`, `lesson 230/240 KB ok`, `quiz 249/255 KB ok`.
- Skill greps: §14 (physical direction utilities) and §16 (raw hex and arbitrary px/rem) are clean in `src/features/mathSteps`.

### Mutation check (16 mutants, scripted, source restored after each)
- **Killed (15):**
  - Latin `.` mapping;
  - blank-step filter;
  - wrap condition;
  - left-caret clamp;
  - TTL `>` → `>=`;
  - purge prefix filter;
  - flush on unmount;
  - hidden-only visibility flush;
  - draft-wins order;
  - move-button fallback;
  - remove focus target;
  - keypad length limit;
  - `inputMode` switch;
  - keypad-under-active-field;
  - remove disabled at one step.
- **Survived (1):** removing `event.preventDefault()` on key `pointerdown`. jsdom keeps a textarea's selection when it blurs, and the hook refocuses the field and sets the caret after every key anyway. So the effect is only observable in a real browser (the phone keyboard flashing up and the selection surviving the tap). It is left as is. It would need a Playwright or browser-mode test.

## Notes for review
- **Deferred, for the orchestrator to file:** the math-with-steps question type end to end. This covers:
  - `QuestionType.MathSteps`;
  - the body and grading-spec schema;
  - admin authoring of model solution steps and final answers;
  - the servable rule;
  - `QuestionAnswerRules` for `{steps, finalAnswer}`;
  - mounting `MathStepsAnswer` in `QuestionView` for quiz and exam, with the exam server autosave through `PUT /api/exams/{id}/answers/{questionId}`;
  - `clearMathDraft` on submit.

  The acceptance notes that it will be planned into #122. `docs/math-input.md` §1 and §7 describe it.
- **No test for an `owner` change.** Nothing tests the `key`-based remount on an `owner` change (Decision 14). The plan has no such test, and I did not add one.
- **Drafts on logout.** They are not cleared on logout (Decision 11). The 7-day TTL and per-student key bound them.
- **T54 timing.** T54 relies on `shouldAdvanceTime`, so real time drifts into the fake clock. There is about 400 ms of margin between typing `y` and the "still null" assertion. It passed in every run here (the machine was heavily loaded by other lanes).
- **`docs/performance.md`.** Its §4 table still says `entry 197` measured, while its later "after this change" line says 201. That is pre-existing, and this story does not change any budget, so I left the doc alone.
