# Plan — [E15.S1] Math step input component (#121)

## Goal
A reusable, phone-first **math-with-steps answer component** exists in `web/`. With it, a student writes an ordered list of steps and a final answer in LaTeX. Each field shows a live KaTeX preview. A touch keypad inserts math symbols at the caret, so the phone keyboard is not needed. The student can add, remove and reorder steps with buttons, which works on touch and keyboard. The draft autosaves to the device and is restored after a refresh or when the phone kills the tab. The component emits the answer shape `{steps: string[], finalAnswer: string}`, which #122 (CAS final-answer check) and #123 (LLM step grading) will grade.

Nothing mounts it for students yet. The repo has no `MathSteps` question type, and no story authors or serves one (see Deferred).

## Scope
**In (web only):**
- A new feature folder `web/src/features/mathSteps/`, containing:
  - a LaTeX field with a KaTeX preview;
  - a 36-key touch keypad that keeps caret and selection, wraps templates and has backspace and caret-move keys;
  - an ordered steps list with add, remove, move up and move down, focus management and a live announcement;
  - a final-answer field;
  - a device-local debounced autosave with restore, a TTL purge, per-student isolation and flushes on `pagehide`, `visibilitychange` and unmount;
  - a status line;
  - the payload mapping.
- i18n (ar and en).
- Docs: a new `docs/math-input.md`, plus a Math input component entry in `docs/design-system.md` and `.claude/design-system.md`.

**Out:**
- The API and grading, which belong to #122 and #123.
- Any `api/` or `ai/` change.
- Any change to `web/src/features/questions/**`, `quiz/**` or `exam/**`. The #118 lane is editing those shared question-type files, and this story must not conflict with it.

**Deferred** (the orchestrator files one issue):
1. **The math-with-steps question type end to end: backlog gap.** This covers:
   - `QuestionType.MathSteps`;
   - the body and grading-spec schema;
   - admin authoring (model solution steps and final answers);
   - the servable rule;
   - `QuestionAnswerRules` for `{steps, finalAnswer}`;
   - mounting `MathStepsAnswer` in `QuestionView` for quiz and exam, with the server-side exam autosave through the existing `PUT /api/exams/{id}/answers/{questionId}`;
   - clearing the draft on submit (`clearMathDraft`).

   **Why it is deferred:** epic E15 (#120) has no authoring or serving story, unlike E14, where #117 was authoring and #119 was serving. The per-question tolerance and form spec is #122's. Building the type here would touch `QuestionType.cs`, `QuestionSchemaRules`, `QuestionAnswerRules`, `studentQuestion.ts`, `QuestionView.tsx` and the editor schema. #118 is modifying those files concurrently, and the orchestrator instructed a minimal footprint on them.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| 1 | Math input library (MathLive etc.)? | **No new dependency.** The field is a plain LTR `<textarea>` holding LaTeX, with a custom keypad. The preview reuses the existing lazy KaTeX path through `RichTextViewer` (content barrel). KaTeX 0.18.9 is MIT and already installed. | MathLive (MIT) is about 200 KB gzipped, uses a custom element with shadow DOM that jsdom and Testing Library cannot drive, brings its own virtual keyboard and RTL behaviour that clash with Glass, and would threaten the `quiz` bundle budget (255 KB br) when mounted. KaTeX is already split into a lazy chunk (`docs/performance.md` §4). The admin formula input (`FormulaInsertForm`) is also plain LTR LaTeX, which gives consistency. |
| 2 | Preview renderer | `MathPreview` builds `<div data-type="block-math" data-latex="…escaped…"></div>` with `toMathPreviewHtml` and renders it with `RichTextViewer` from `@/features/content`. It adds no new KaTeX import and no new loader. | This is one KaTeX path, sanitised by `SafeHtml`, and it follows the `docs/rich-text.md` math node contract. Invalid LaTeX shows the KaTeX error inline (`throwOnError: false`). |
| 3 | What is a step? | One LaTeX string. Arabic words go inside `\text{…}` (the keypad «نص» key). The field keeps what the student typed. Arabic-Indic digits (U+0660–0669, U+06F0–06F9) and `٫` (U+066B) are mapped to Latin digits and `.` **only** in the preview and the payload (`toLatinDigits`). | PRD §6 says "steps (LaTeX/text)". The design system says to use Latin digits in anything copied into formulas. Rewriting the field while the student types would move the caret. |
| 4 | Keypad layout and targets | 6 columns × 6 rows = 36 keys (table below). Each key is ≥ 44 px tall. At 375 px each key is about 45 px wide (311 px content − `p-2` − 5×`gap-1`). The keypad is `dir="ltr"`, like a calculator. | 7 columns would give keys under 44 px at 375 px (design system: touch targets ≥ 44). |
| 5 | Keypad vs native keyboard | The keypad is **open by default**. While it is open, math fields get `inputMode="none"`, so the phone keyboard stays hidden, while physical keyboards still type. A toggle button «لوحة الرموز» (`aria-pressed`) closes it, which gives `inputMode="text"` for letters and Arabic. | This makes "on my phone" work out of the box without media queries. A desktop user loses nothing. |
| 6 | Where the keypad renders | Directly under the **active** field: the last math field that got focus, a step or the final answer. It renders only when open, when a field is active and when the component is not disabled. Key buttons call `preventDefault` on `pointerdown`, so the textarea keeps focus and selection. After each key the hook refocuses the field and sets the caret. | The keypad stays next to the caret, and the caret survives taps. Keyboard users (Tab to a key, then Enter) also land back in the field. |
| 7 | Reorder mechanism | «انقل لأعلى» and «انقل لأسفل» icon buttons on each step. There is **no drag**. | This satisfies WCAG 2.5.7 without a DnD dependency. Drag conflicts with scrolling on phones. |
| 8 | Focus after list edits | **Add:** focus the new step's field. **Remove:** focus the previous step's field, or the new first step when the first was removed. **Move:** focus the moved step's same-direction button, or its other button when that one is now disabled. Each edit is announced in an `sr-only` `role="status"` region. | React moves keyed DOM nodes, and a moved focused node loses focus in browsers, so focus is restored explicitly after render. |
| 9 | Limits | `mathStepsMaxCount = 20`, `mathStepMaxLength = 500`, `mathFinalAnswerMaxLength = 200` (constants in `mathStepsValue.ts`). Textareas carry `maxLength`. A keypad key that would exceed the limit does nothing. There is always at least 1 step row: remove is disabled when only 1 is left. Blank steps are dropped from the payload. | There is no server contract yet (it arrives with the deferred type), so these are client-side and named. The limits are generous for Thanaweya Amma problems. |
| 10 | Autosave target | **Device-local** `localStorage`, under the key `elmanhg.mathDraft.<studentId>.<sessionId>.<questionId>` (the prefix style of `elmanhg.funnel.`). The value is `{"savedAt": <ms>, "answer": {"steps": string[], "finalAnswer": string}}`, stored untrimmed so the layout restores exactly. There is a debounce of 800 ms (`mathDraftSaveDelayMilliseconds`). Pending writes flush on `pagehide`, on `visibilitychange`→hidden and on unmount. | No server answer shape exists for this type. Quiz answers are only sent on «تحقّق». Phones kill background tabs. The exam's server autosave is layered on by the integration story through `onChange`. |
| 11 | Draft privacy and lifetime | The owner prop **requires** `studentId`, `sessionId` and `questionId`, so another account on a shared phone never sees the draft. Drafts expire after 7 days (`mathDraftTtlMilliseconds = 604_800_000`). An expired or unparseable draft is removed when read, and `purgeExpiredMathDrafts` sweeps all math drafts on mount. The store does not hook into logout. | Answers are not credentials (the skill forbids tokens only). Hooking logout would touch `features/session`, which is outside this story's footprint. The TTL bounds leftovers. |
| 12 | Draft vs `initialValue` | A valid stored draft **wins** over `initialValue`. The status shows «استعدنا مسودتك المحفوظة.». | The draft is at least as new as anything this device typed. The integration story can clear it on submit (`clearMathDraft`). |
| 13 | Storage failure | `localStorage` access is wrapped in `try/catch`. If a write fails (quota, or Safari private mode), the status shows «تعذّر حفظ المسودة على هذا الجهاز.» in `text-danger`, and editing continues. If a read fails, the result is treated as no draft. | The component never breaks the answer flow. |
| 14 | Changing `owner` | `MathStepsAnswer` renders `MathStepsDraftEditor` with `key={mathDraftStorageKey(owner)}`, so a new question or session remounts, flushes the old draft and loads the new one. | This avoids key-change logic inside the hook. |
| 15 | Field typography | `font-mono` (type.mono family), but size `text-body` (16 px) instead of `text-mono` (12 px). | 12 px student input is unreadable on phones, and iOS zooms on focus below 16 px (PRD §14 mobile-first). The size is still a token. This is recorded in both design-system files (Math input row). |
| 16 | Public surface | The barrel exports `MathStepsAnswer` (autosaving), `MathStepsInput` (controlled, no autosave, for future admin model-solution authoring), `clearMathDraft`, `emptyMathStepsValue`, `fromMathStepsPayload`, `toMathStepsPayload` and the types. | This is what #122, #123 and the deferred integration need, and nothing more. |
| 17 | Bundle | Not mounted on any route, so no page budget moves. `web/src/app/i18n.ts` statically imports the `mathSteps` locales, which adds about 1 KB br to `entry` (197 KB of 210 KB). The implementer runs `npm run build && npm run perf:budget`. | Every feature namespace is registered this way today. |
| 18 | Morabh | Morabh (`D:\Personal\Projects\Projects\Morabh\repos\apis`) is a .NET API only. A grep for latex, keypad and autosave returns 0 hits. Every piece here is **new, with no Morabh equivalent**. The in-repo patterns reused are `RichTextViewer` (KaTeX lazy), the `EssayAnswerInput` textarea classes, the `useExamAnswers` debounce and flush, and the `funnelTracker` localStorage key style. | Reuse-first check done. |

### Keypad (row by row, `dir="ltr"`)
| Row | Keys (`MathKeyId` → glyph → action) |
|-----|-----|
| 1 | `d7` 7 · `d8` 8 · `d9` 9 · `divide` ÷ → `\div ` · `openParen` ( → `(` · `closeParen` ) → `)` |
| 2 | `d4` 4 · `d5` 5 · `d6` 6 · `times` × → `\times ` · `power` xⁿ → `^{}` caret 2 wraps · `sqrt` √ → `\sqrt{}` caret 6 wraps |
| 3 | `d1` 1 · `d2` 2 · `d3` 3 · `minus` − → `-` · `fraction` a/b → `\frac{}{}` caret 6 wraps · `pi` π → `\pi ` |
| 4 | `d0` 0 · `point` . → `.` · `equals` = → `=` · `plus` + → `+` · `x` x → `x` · `y` y → `y` |
| 5 | `le` ≤ → `\le ` · `ge` ≥ → `\ge ` · `ne` ≠ → `\ne ` · `pm` ± → `\pm ` · `sin` sin → `\sin ` · `cos` cos → `\cos ` |
| 6 | `tan` tan → `\tan ` · `log` log → `\log ` · `text` (glyph `t('keypad.textGlyph')`) → `\text{}` caret 6 wraps · `left` icon `ArrowLeft` · `right` icon `ArrowRight` · `backspace` icon `Delete` |

Digit keys insert the digit. Every key has a translated `aria-label` from `keys.<id>`.

## Existing code touched
| File | Change |
|------|--------|
| `web/src/app/i18n.ts` | Add `import { mathStepsLocales } from '@/features/mathSteps/locales';` after the `masteryLocales` import. Add `mathSteps: mathStepsLocales.ar,` in `resources.ar` and `mathSteps: mathStepsLocales.en,` in `resources.en`, both after `mastery`. Add `'mathSteps',` to `ns` after `'mastery'`. |
| `docs/design-system.md` | After §5.11, add `### 5.12 Math input`. Content: LaTeX field (white, `--r-sm`, `--border-strong`, system monospace at 16 px, `direction: ltr`); preview box below (white, hairline, `--r-sm`, min 44 px, horizontal scroll inside); touch keypad (6×6 keys, each white `--r-sm` `--border-strong` and at least 44 px, on a `--soft` panel with `--r-md`, laid out left to right) under the active field; step rows are white `--r-md` hairline cards with icon buttons for move up, move down and remove (Danger), each at least 44 px. |
| `.claude/design-system.md` | Components table, after `Skeleton`: `\| MathInput \| step, final \| default, focus, disabled, keypad open (inputmode none) \| textarea like Input but type.mono family at type.body size (16 px, avoids iOS zoom), dir="ltr"; preview box surface + border, radius.sm, min height 44; keypad 6×6 keys (surface, border.strong, radius.sm, ≥44px) on soft panel radius.md, dir="ltr", under the active field; step row surface + border radius.md with move up/down (ghost) and remove (danger) icon buttons ≥44px \|` |

## Files to create
All paths are under `web/src/features/mathSteps/` unless the path is absolute from the repo root. TypeScript is strict, `exactOptionalPropertyTypes` is on, and exports are named only.

| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `locales.ts` | module | `import ar from './i18n/ar.json'; import en from './i18n/en.json'; export const mathStepsLocales = { ar, en };` |
| 2 | `i18n/en.json`, 3 `i18n/ar.json` | i18n | The exact keys and strings are in the **i18n** table below. |
| 4 | `api/mathStepsValue.ts` | pure module | See **F4**. |
| 5 | `api/mathKeys.ts` | pure module | See **F5**. |
| 6 | `api/mathDraftStore.ts` | module (localStorage) | See **F6**. |
| 7 | `hooks/useMathFieldFocus.ts` | hook | See **F7**. |
| 8 | `hooks/useMathStepsEditor.ts` | hook | See **F8**. |
| 9 | `hooks/useMathStepsDraft.ts` | hook | See **F9**. |
| 10 | `components/MathPreview.tsx` | component | See **F10**. |
| 11 | `components/MathField.tsx` | component | See **F11**. |
| 12 | `components/MathKeypad.tsx` | component | See **F12**. |
| 13 | `components/MathStepRow.tsx` | component | See **F13**. |
| 14 | `components/MathStepsInput.tsx` | component | See **F14**. |
| 15 | `components/MathDraftStatus.tsx` | component | See **F15**. |
| 16 | `components/MathStepsDraftEditor.tsx` | component | See **F16**. |
| 17 | `components/MathStepsAnswer.tsx` | component | See **F17**. |
| 18 | `index.ts` | barrel | `export { MathStepsAnswer, type MathStepsAnswerProps } from './components/MathStepsAnswer'; export { MathStepsInput, type MathStepsInputProps } from './components/MathStepsInput'; export { clearMathDraft, type MathDraftOwner } from './api/mathDraftStore'; export { emptyMathStepsValue, fromMathStepsPayload, toMathStepsPayload, type MathStep, type MathStepsPayload, type MathStepsValue } from './api/mathStepsValue';` |
| 19 | `api/mathStepsValue.test.ts` | test | Test plan T1–T14 |
| 20 | `api/mathKeys.test.ts` | test | T15–T24 |
| 21 | `api/mathDraftStore.test.ts` | test | T25–T32 |
| 22 | `components/MathStepsAnswer.steps.test.tsx` | test | T33–T44 |
| 23 | `components/MathStepsAnswer.keypad.test.tsx` | test | T45–T52 |
| 24 | `components/MathStepsAnswer.autosave.test.tsx` | test | T53–T62 |
| 25 | `docs/math-input.md` (repo root) | doc | See **F25**. |

### F4 `api/mathStepsValue.ts`
```ts
export interface MathStep { id: string; latex: string }
export interface MathStepsValue { steps: MathStep[]; finalAnswer: string }
export interface MathStepsPayload { steps: string[]; finalAnswer: string }
export type MoveDirection = -1 | 1;
export type MathFieldId = 'final' | `step:${string}`;
export const mathStepsMaxCount = 20;
export const mathStepMaxLength = 500;
export const mathFinalAnswerMaxLength = 200;
export function stepFieldId(stepId: string): MathFieldId            // `step:${stepId}`
export function newMathStep(latex = ''): MathStep                   // { id: crypto.randomUUID(), latex }
export function emptyMathStepsValue(): MathStepsValue               // { steps: [newMathStep()], finalAnswer: '' }
export function addMathStep(value: MathStepsValue): MathStepsValue  // steps.length >= max → same object; else append newMathStep()
export function removeMathStep(value: MathStepsValue, stepId: string): MathStepsValue   // steps.length <= 1 or id unknown → same object
export function moveMathStep(value: MathStepsValue, stepId: string, direction: MoveDirection): MathStepsValue // swap with index+direction; out of range/unknown → same object
export function fieldValue(value: MathStepsValue, field: MathFieldId): string      // 'final' → finalAnswer; else matching step latex ('' if missing)
export function setFieldValue(value: MathStepsValue, field: MathFieldId, latex: string): MathStepsValue // new object; only the target changes
export function fieldMaxLength(field: MathFieldId): number           // final → 200, step → 500
export function toLatinDigits(text: string): string                  // U+0660–0669 and U+06F0–06F9 → '0'–'9'; U+066B → '.'
export function toMathStepsPayload(value: MathStepsValue): MathStepsPayload // steps: toLatinDigits(latex).trim(), drop ''; finalAnswer: toLatinDigits(finalAnswer).trim()
export function fromMathStepsPayload(payload: MathStepsPayload): MathStepsValue // steps.map(newMathStep); zero steps → [newMathStep()]
export function isMathStepsPayload(candidate: unknown): candidate is MathStepsPayload
  // object with steps: array (length ≤ mathStepsMaxCount) of strings each ≤ mathStepMaxLength, and finalAnswer: string ≤ mathFinalAnswerMaxLength
export function toMathPreviewHtml(latex: string): string
  // `<div data-type="block-math" data-latex="${escape(toLatinDigits(latex))}"></div>`; escape & → &amp;, < → &lt;, > → &gt;, " → &quot;, ' → &#39; (& first)
```

### F5 `api/mathKeys.ts`
```ts
export type MathKeyId = 'd0'|'d1'|'d2'|'d3'|'d4'|'d5'|'d6'|'d7'|'d8'|'d9'|'point'|'plus'|'minus'|'times'|'divide'|'equals'
  |'openParen'|'closeParen'|'power'|'sqrt'|'fraction'|'pi'|'x'|'y'|'le'|'ge'|'ne'|'pm'|'sin'|'cos'|'tan'|'log'|'text'
  |'left'|'right'|'backspace';
export type MathKeyAction =
  | { kind: 'insert'; text: string; caret?: number; wraps?: boolean }
  | { kind: 'backspace' } | { kind: 'left' } | { kind: 'right' };
export interface MathKey { id: MathKeyId; glyph: string | null; action: MathKeyAction }
export const mathKeyRows: readonly (readonly MathKey[])[];   // exactly the 6×6 Keypad table; glyph null for text/left/right/backspace
export interface MathEditState { value: string; selectionStart: number; selectionEnd: number }
export interface MathEditResult { value: string; caret: number }
export function applyMathKey(state: MathEditState, keyId: MathKeyId): MathEditResult
```
`applyMathKey` rules, where `start = min(selectionStart, selectionEnd)`, `end = max(...)` and `selected = value.slice(start, end)`:
- **insert:** `caret = action.caret ?? text.length`. With `wraps` and non-empty `selected`, the result is `before + text.slice(0, caret) + selected + text.slice(caret) + after`, with the caret at `start + caret + selected.length`. Otherwise the result is `before + text + after` (the selection is replaced), with the caret at `start + caret`.
- **backspace:** with a selection, remove `[start, end)` and put the caret at `start`. Otherwise, when `start > 0`, remove the char at `start - 1` and put the caret at `start - 1`. Otherwise the value is unchanged and the caret stays at `0`.
- **left:** the value is unchanged. The caret is `start` when there is a selection, otherwise `max(0, start - 1)`.
- **right:** the value is unchanged. The caret is `end` when there is a selection, otherwise `min(value.length, end + 1)`.
- An unknown id cannot occur: the key is looked up in a `Map` built from `mathKeyRows`, and the `get` result is checked (`if (!key) return { value, caret: end }`).

### F6 `api/mathDraftStore.ts`
```ts
import { fromMathStepsPayload, isMathStepsPayload, type MathStepsValue } from './mathStepsValue';
export interface MathDraftOwner { studentId: string; sessionId: string; questionId: string }
export const mathDraftKeyPrefix = 'elmanhg.mathDraft.';
export const mathDraftTtlMilliseconds = 604_800_000;   // 7 days
export const mathDraftSaveDelayMilliseconds = 800;
export function mathDraftStorageKey(owner: MathDraftOwner): string   // `${prefix}${studentId}.${sessionId}.${questionId}`
export function readMathDraft(key: string, now: number): MathStepsValue | null
export function writeMathDraft(key: string, value: MathStepsValue, now: number): boolean
export function clearMathDraft(owner: MathDraftOwner): void
export function purgeExpiredMathDrafts(now: number): void
```
- `readMathDraft`:
  1. `try` `localStorage.getItem(key)`. On a throw, return `null`.
  2. `null` → `null`.
  3. `JSON.parse` inside `try`. The parsed value must be an object with a finite number `savedAt` and an `answer` for which `isMathStepsPayload` holds. Otherwise remove the key and return `null`.
  4. When `now - savedAt > mathDraftTtlMilliseconds`, remove the key and return `null`.
  5. Return `fromMathStepsPayload(answer)`.
- `writeMathDraft`: `try` `localStorage.setItem(key, JSON.stringify({ savedAt: now, answer: { steps: value.steps.map((s) => s.latex), finalAnswer: value.finalAnswer } }))` and return `true`. `catch` returns `false`.
- `clearMathDraft`: `try` `removeItem(mathDraftStorageKey(owner))`. `catch` ignores the error.
- `purgeExpiredMathDrafts`:
  1. `try`: collect every `localStorage.key(i)` starting with the prefix.
  2. Run `readMathDraft(key, now)` on each (it removes an expired or invalid draft as a side effect).
  3. `catch` ignores the error.

### F7 `hooks/useMathFieldFocus.ts`
```ts
export type MathFocusTarget =
  | { kind: 'field'; field: MathFieldId; caret?: number }
  | { kind: 'move'; stepId: string; direction: MoveDirection };
export interface MathFieldFocus {
  registerField: (field: MathFieldId) => RefCallback<HTMLTextAreaElement>;
  registerMoveButton: (stepId: string, direction: MoveDirection) => RefCallback<HTMLButtonElement>;
  fieldElement: (field: MathFieldId) => HTMLTextAreaElement | undefined;
  focusNow: (target: MathFocusTarget) => void;
  focusAfterRender: (target: MathFocusTarget) => void;
}
export function useMathFieldFocus(): MathFieldFocus
```
- State: `fields = useRef(new Map<MathFieldId, HTMLTextAreaElement>())`, `buttons = useRef(new Map<string, HTMLButtonElement>())` with the key `${stepId}:${direction}`, and `pending = useRef<MathFocusTarget | null>(null)`.
- The register callbacks set the element and return a cleanup that deletes it (React 19 ref cleanup).
- `focusNow`:
  - **field:** `el.focus()`. If `caret !== undefined`, also `el.setSelectionRange(caret, caret)`.
  - **move:** take the button for the given direction. If it is missing or disabled, take the button for `-direction`. Call `.focus()` on it.
- `focusAfterRender` sets `pending.current`.
- `useEffect(() => { const target = pending.current; if (target === null) return; pending.current = null; focusNow(target); });` has **no dependency array**, so it runs after every commit.

### F8 `hooks/useMathStepsEditor.ts`
```ts
export interface MathStepsEditorOptions { value: MathStepsValue; onChange: (value: MathStepsValue) => void }
export interface MathStepsEditor {
  activeField: MathFieldId | null;
  announcement: string;
  focus: MathFieldFocus;                 // from useMathFieldFocus
  activate: (field: MathFieldId) => void;              // onFocus
  changeField: (field: MathFieldId, latex: string) => void;
  add: () => void;
  remove: (stepId: string) => void;
  move: (stepId: string, direction: MoveDirection) => void;
  pressKey: (keyId: MathKeyId) => void;
}
export function useMathStepsEditor({ value, onChange }: MathStepsEditorOptions): MathStepsEditor
```
The hook uses `useTranslation('mathSteps')`, `const [activeField, setActiveField] = useState<MathFieldId | null>(null)` and `const [announcement, setAnnouncement] = useState('')`.
- `changeField(field, latex)` calls `onChange(setFieldValue(value, field, latex))`.
- `add`:
  1. `next = addMathStep(value)`. If `next === value`, return.
  2. `created = next.steps.at(-1)`. If it is undefined, return.
  3. `focus.focusAfterRender({ kind: 'field', field: stepFieldId(created.id) })`, `setActiveField(stepFieldId(created.id))` and `setAnnouncement(t('steps.added', { number: next.steps.length }))`.
  4. `onChange(next)`.
- `remove(stepId)`:
  1. `index = value.steps.findIndex(...)`. `next = removeMathStep(value, stepId)`. If `next === value`, return.
  2. The target is `value.steps[index - 1] ?? next.steps[0]`. If it is undefined, return.
  3. `focusAfterRender` the target's field and `setActiveField` to it.
  4. Announce `steps.removed { number: index + 1 }`.
  5. `onChange(next)`.
- `move(stepId, direction)`:
  1. `index = ...`. `next = moveMathStep(...)`. If `next === value`, return.
  2. `focusAfterRender({ kind: 'move', stepId, direction })`.
  3. Announce `steps.moved { number: index + direction + 1 }`.
  4. `onChange(next)`.
- `pressKey(keyId)`:
  1. If `activeField === null`, return. `el = focus.fieldElement(activeField)`. If it is undefined, return.
  2. `current = fieldValue(value, activeField)`. `result = applyMathKey({ value: current, selectionStart: el.selectionStart, selectionEnd: el.selectionEnd }, keyId)`.
  3. When `result.value.length > fieldMaxLength(activeField)`: `focus.focusNow({ kind: 'field', field: activeField })` and return.
  4. When `result.value === current`: `focus.focusNow({ kind: 'field', field: activeField, caret: result.caret })` and return.
  5. Otherwise `focus.focusAfterRender({ kind: 'field', field: activeField, caret: result.caret })`, then `onChange(setFieldValue(value, activeField, result.value))`.

### F9 `hooks/useMathStepsDraft.ts`
```ts
export type MathDraftStatusValue = 'idle' | 'restored' | 'saved' | 'error';
export interface MathStepsDraftOptions { owner: MathDraftOwner; initialValue?: MathStepsValue | undefined; onChange?: ((value: MathStepsValue) => void) | undefined }
export interface MathStepsDraft { value: MathStepsValue; change: (value: MathStepsValue) => void; status: MathDraftStatusValue }
export function useMathStepsDraft({ owner, initialValue, onChange }: MathStepsDraftOptions): MathStepsDraft
```
1. `const storageKey = mathDraftStorageKey(owner);`
2. `const [initial] = useState(() => { const draft = readMathDraft(storageKey, Date.now()); return { value: draft ?? initialValue ?? emptyMathStepsValue(), status: draft ? 'restored' : 'idle' } as const; });`
3. `const [value, setValue] = useState(initial.value)` and `const [status, setStatus] = useState<MathDraftStatusValue>(initial.status)`. Also `latest = useRef(initial.value)` and `timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)`.
4. `change(next)`:
   1. `setValue(next)`, `latest.current = next`, `onChange?.(next)`.
   2. If a timer is set, `clearTimeout` it.
   3. `timer.current = setTimeout(() => { timer.current = undefined; setStatus(writeMathDraft(storageKey, latest.current, Date.now()) ? 'saved' : 'error'); }, mathDraftSaveDelayMilliseconds)`.
5. `useEffect(() => { ... }, [storageKey])`:
   1. `purgeExpiredMathDrafts(Date.now())`.
   2. `const flush = () => { if (timer.current === undefined) return; clearTimeout(timer.current); timer.current = undefined; writeMathDraft(storageKey, latest.current, Date.now()); }`
   3. `const onVisibility = () => { if (document.visibilityState === 'hidden') flush(); }`
   4. `document.addEventListener('visibilitychange', onVisibility)` and `window.addEventListener('pagehide', flush)`.
   5. The cleanup removes both listeners, then calls `flush()`.

### F10–F17 components
| # | Component | Props | Renders |
|---|-----------|-------|---------|
| F10 | `MathPreview` | `{ latex: string; label: string }` | `<div role="group" aria-label={label} className="min-h-11 overflow-x-auto rounded-sm border border-border bg-surface px-3 py-2">`. When `latex.trim() === ''`, it holds `<p className="text-caption text-text-muted">{t('preview.empty')}</p>`. Otherwise it holds `<div dir="ltr"><RichTextViewer html={toMathPreviewHtml(latex)} /></div>`. `RichTextViewer` is imported from `@/features/content`. |
| F11 | `MathField` | `{ field: MathFieldId; label: string; value: string; disabled: boolean; keypadOpen: boolean; editor: MathStepsEditor; rows: number }` | Uses `id = useId()`. `<div className="flex flex-col gap-1.5">` holds `<Label htmlFor={id}>{label}</Label>`, then a `<textarea>` with: `id`, `ref={editor.focus.registerField(field)}`, `dir="ltr"`, `rows`, `value`, `maxLength={fieldMaxLength(field)}`, `disabled`, `spellCheck={false}`, `autoCapitalize="off"`, `autoCorrect="off"`, `inputMode={keypadOpen ? 'none' : 'text'}`, `onFocus={() => editor.activate(field)}`, `onChange={(e) => editor.changeField(field, e.target.value)}` and `className="min-h-11 w-full rounded-sm border border-border-strong bg-surface px-3 py-2.25 font-mono text-body text-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden disabled:opacity-45"`. Then `<MathPreview latex={value} label={t('preview.label', { name: label })} />`. |
| F12 | `MathKeypad` | `{ onKey: (keyId: MathKeyId) => void }` | `<div role="group" aria-label={t('keypad.label')} dir="ltr" className="grid grid-cols-6 gap-1 rounded-md border border-border bg-soft p-2">`. It maps `mathKeyRows.flat()` to `<button type="button" key={key.id} aria-label={t(`keys.${key.id}`)} onPointerDown={(e) => e.preventDefault()} onClick={() => onKey(key.id)} className="flex min-h-11 items-center justify-center rounded-sm border border-border-strong bg-surface font-sans text-ui font-semibold text-text hover:bg-soft focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden">`. Each button contains the icon for left (`ArrowLeft`), right (`ArrowRight`) or backspace (`Delete`), each with `aria-hidden` and `className="size-5"`. Otherwise it contains `key.glyph ?? t('keypad.textGlyph')`. |
| F13 | `MathStepRow` | `{ step: MathStep; index: number; count: number; disabled: boolean; keypadOpen: boolean; editor: MathStepsEditor; keypad: ReactNode }` | `number = index + 1`. `<li className="flex flex-col gap-3 rounded-md border border-border bg-surface p-3">` holds: `<MathField field={stepFieldId(step.id)} label={t('steps.label', { number })} value={step.latex} rows={2} …/>`, then `{keypad}`, then `<div className="flex justify-end gap-2">` with three `Button`s. Up: `variant="ghost"`, `className="min-w-11 px-0"`, `aria-label={t('steps.moveUp', { number })}`, `ref={editor.focus.registerMoveButton(step.id, -1)}`, `disabled={disabled \|\| index === 0}`, `onClick={() => editor.move(step.id, -1)}`, icon `ArrowUp`. Down: the same with `+1`, `ArrowDown` and `index === count - 1`. Remove: `variant="danger"`, `aria-label={t('steps.remove', { number })}`, `disabled={disabled \|\| count === 1}`, `onClick={() => editor.remove(step.id)}`, icon `Trash2`. Icons carry `aria-hidden` and `className="size-5"`. |
| F14 | `MathStepsInput` (exported) | `{ value: MathStepsValue; onChange: (value: MathStepsValue) => void; disabled?: boolean \| undefined }` | Holds `editor = useMathStepsEditor({ value, onChange })` and `const [keypadOpen, setKeypadOpen] = useState(true)`. `keypadFor = (field) => keypadOpen && !disabled && editor.activeField === field ? <MathKeypad onKey={editor.pressKey} /> : null`. Structure: `<div className="flex flex-col gap-4">`, containing, in order:<br>1. A row `flex items-start justify-between gap-3` with `<p className="text-caption text-text-muted">{t('input.hint')}</p>` and `<Button variant="secondary" size="sm" aria-pressed={keypadOpen} disabled={disabled} onClick={() => setKeypadOpen(!keypadOpen)}><Keyboard aria-hidden className="size-5" />{t('keypad.toggle')}</Button>`.<br>2. `<fieldset className="flex flex-col gap-3">` with `<legend className="mb-3 font-display text-h3 font-semibold text-text">{t('steps.title')}</legend>` and `<ol className="flex flex-col gap-3">`. The list maps the steps to `<MathStepRow key={step.id} … keypad={keypadFor(stepFieldId(step.id))} />`.<br>3. `<Button variant="secondary" disabled={disabled \|\| atMax} onClick={editor.add}><Plus aria-hidden className="size-5" />{t('steps.add')}</Button>`, followed when `atMax` by `<p className="text-caption text-text-muted">{t('steps.max', { max: mathStepsMaxCount })}</p>`.<br>4. `<MathField field="final" label={t('final.label')} value={value.finalAnswer} rows={1} …/>` then `{keypadFor('final')}`.<br>5. `<p role="status" className="sr-only">{editor.announcement}</p>`.<br>`disabled` defaults to `false`, and each child gets `disabled={disabled}`. |
| F15 | `MathDraftStatus` | `{ status: MathDraftStatusValue }` | Always renders `<p role="status" className={status === 'error' ? 'text-caption text-danger' : 'text-caption text-text-muted'}>`. The content is empty for `idle`, `t('draft.restored')`, `t('draft.saved')` or `t('draft.error')`. |
| F16 | `MathStepsDraftEditor` | `MathStepsAnswerProps` | `const draft = useMathStepsDraft({ owner, initialValue, onChange })`, then `<div className="flex flex-col gap-3"><MathStepsInput value={draft.value} onChange={draft.change} disabled={disabled} /><MathDraftStatus status={draft.status} /></div>`. |
| F17 | `MathStepsAnswer` (exported) | `export interface MathStepsAnswerProps { owner: MathDraftOwner; initialValue?: MathStepsValue \| undefined; onChange?: ((value: MathStepsValue) => void) \| undefined; disabled?: boolean \| undefined }` | `return <MathStepsDraftEditor key={mathDraftStorageKey(props.owner)} {...props} />;` |

Every component takes `useTranslation('mathSteps')`. No file may exceed 200 lines and no component 120. The strings for `t()` are below.

### i18n (`mathSteps` namespace, identical key tree in both files)
| Key | en | ar |
|-----|----|----|
| `input.hint` | Write each step in LaTeX. The preview under it shows how it reads. | اكتب كل خطوة بصيغة LaTeX، وتظهر المعاينة تحتها. |
| `steps.title` | Steps | الخطوات |
| `steps.label` | Step {number, number} | الخطوة {number, number} |
| `steps.add` | Add a step | أضف خطوة |
| `steps.max` | You can add up to {max, number} steps. | يمكنك إضافة {max, number} خطوة كحد أقصى. |
| `steps.moveUp` | Move step {number, number} up | انقل الخطوة {number, number} لأعلى |
| `steps.moveDown` | Move step {number, number} down | انقل الخطوة {number, number} لأسفل |
| `steps.remove` | Remove step {number, number} | احذف الخطوة {number, number} |
| `steps.added` | Step {number, number} added. | أُضيفت الخطوة {number, number}. |
| `steps.removed` | Step {number, number} removed. | حُذفت الخطوة {number, number}. |
| `steps.moved` | Step moved to position {number, number}. | نُقلت الخطوة إلى الموضع {number, number}. |
| `final.label` | Final answer | الإجابة النهائية |
| `preview.label` | Preview of {name} | معاينة {name} |
| `preview.empty` | The preview appears here. | ستظهر المعاينة هنا. |
| `keypad.toggle` | Keypad | لوحة الرموز |
| `keypad.label` | Math keypad | لوحة الرموز الرياضية |
| `keypad.textGlyph` | Text | نص |
| `keys.d0`…`keys.d9` | 0 … 9 | 0 … 9 |
| `keys.point` | Decimal point | فاصلة عشرية |
| `keys.plus` / `minus` / `times` / `divide` / `equals` | Plus / Minus / Times / Divided by / Equals | زائد / ناقص / ضرب / قسمة / يساوي |
| `keys.openParen` / `closeParen` | Opening bracket / Closing bracket | قوس فتح / قوس إغلاق |
| `keys.power` / `sqrt` / `fraction` / `pi` | Power / Square root / Fraction / Pi | أُس / جذر تربيعي / كسر / باي |
| `keys.x` / `keys.y` | x / y | x / y |
| `keys.le` / `ge` / `ne` / `pm` | Less than or equal to / Greater than or equal to / Not equal to / Plus or minus | أصغر من أو يساوي / أكبر من أو يساوي / لا يساوي / زائد أو ناقص |
| `keys.sin` / `cos` / `tan` / `log` | Sine / Cosine / Tangent / Logarithm | جا / جتا / ظا / لوغاريتم |
| `keys.text` | Text | نص |
| `keys.left` / `keys.right` / `keys.backspace` | Move cursor left / Move cursor right / Delete | حرّك المؤشر يسارًا / حرّك المؤشر يمينًا / احذف |
| `draft.restored` | Your saved draft was restored. | استعدنا مسودتك المحفوظة. |
| `draft.saved` | Draft saved on this device. | حُفظت المسودة على هذا الجهاز. |
| `draft.error` | Could not save the draft on this device. | تعذّر حفظ المسودة على هذا الجهاز. |

### F25 `docs/math-input.md`
Sections:
1. **Purpose**, with the E15 map: #121 input, #122 CAS, #123 steps. It states that the component is not mounted until the question type exists (link the deferred issue).
2. **Answer shape**: `{"steps": string[], "finalAnswer": string}`. Values are trimmed, blank steps are dropped, and Arabic-Indic digits and `٫` become Latin in the payload. Limits: 20 steps, 500 characters per step, 200 for the final answer.
3. **Input**: a LaTeX textarea (LTR, monospace at 16 px) with a KaTeX preview through `RichTextViewer`. Arabic words go in `\text{…}`.
4. **Keypad**: the 6×6 table from this plan, the default-open rule, `inputmode="none"`, and the toggle.
5. **Steps list**: add, remove and move buttons, the focus rules and the announcements.
6. **Autosave**: the storage key, the stored JSON, the 800 ms debounce, the flush triggers, the 7-day TTL and purge, the rule that the draft wins over `initialValue`, the failure status, and `clearMathDraft` on submit (the integrator's job). Drafts stay on the device and are never sent to the server.
7. **Integration checklist** for the deferred story: pass `owner`, send `toMathStepsPayload` on check and save, call `clearMathDraft` after submit, and re-check `npm run perf:budget` for `quiz`.

## Error codes
None. There is no API change.

## Domain behaviour
None. The story is web only, and the pure state transitions are in F4 and F5.

## API surface
None. No endpoint is added, so there is no OpenAPI, Orval or Postman change.

## Test plan
All test files live under `web/src/features/mathSteps/`. The component tests render `renderWithProviders(<MathStepsAnswer owner={owner} …/>)` with `const owner = { studentId: 's1', sessionId: 'sess1', questionId: 'q1' }`. Each test creates `const user = userEvent.setup()` first (the autosave file uses `userEvent.setup({ advanceTimers: vi.advanceTimersByTime })` with `vi.useFakeTimers({ shouldAdvanceTime: true })` in `beforeEach` and `vi.useRealTimers()` in `afterEach`). Queries use accessible names only. In `user.type`, a literal `{` must be written `{{`. Use `user.paste` for LaTeX with braces.

| # | Test class (`describe`) | Test method (`it`) | Asserts |
|---|-----------|-------------|---------|
| T1 | `mathStepsValue` | starts with one empty step and an empty final answer | `emptyMathStepsValue()` has 1 step with `latex ''` and a non-empty id, and `finalAnswer ''` |
| T2 | 〃 | appends an empty step with a new id | After `addMathStep`, the length is +1, the last latex is `''`, and the ids are unique |
| T3 | 〃 | refuses a step beyond the limit | With 20 steps, `addMathStep` returns the same object |
| T4 | 〃 | removes a step by id | Steps a, b, c; removing b leaves latex `['a','c']` |
| T5 | 〃 | keeps the last remaining step | With 1 step, `removeMathStep` returns the same object |
| T6 | 〃 | moves a step in either direction | a, b, c: moving b by −1 gives b, a, c, and moving b by +1 gives a, c, b |
| T7 | 〃 | does not move past either end | Moving the first step by −1, the last by +1, or an unknown id returns the same object |
| T8 | 〃 | changes only the targeted field | `setFieldValue` on `step:<id>` changes that step only; on `'final'` it changes `finalAnswer` only; `fieldValue` reads both |
| T9 | 〃 | builds a trimmed payload without blank steps | `[' x=1 ', '  ', 'y']` with final `' 2 '` gives `{steps:['x=1','y'], finalAnswer:'2'}` |
| T10 | 〃 | converts Arabic-Indic digits in the payload | Step `'٣x=٦'` with final `'٢٫٥'` gives `['3x=6']` and `'2.5'` |
| T11 | 〃 | restores steps from a payload and keeps one step when none | `fromMathStepsPayload({steps:['a','b'],finalAnswer:'c'})` gives latex `['a','b']` and final `'c'`; `{steps:[]}` gives 1 empty step |
| T12 | 〃 | accepts only valid payloads | `isMathStepsPayload` is true for a valid payload, and false for `null`, a non-string step, 21 steps, a 501-character step, and a 201-character final |
| T13 | 〃 | escapes LaTeX into a block-math node | `toMathPreviewHtml('a<b & "c"')` equals `<div data-type="block-math" data-latex="a&lt;b &amp; &quot;c&quot;"></div>` |
| T14 | 〃 | uses Latin digits in the preview | `toMathPreviewHtml('٣')` contains `data-latex="3"` |
| T15 | `applyMathKey` | inserts a digit at the caret | `('ab', 1, 1)` with `d7` gives `{value:'a7b', caret:2}` |
| T16 | 〃 | replaces the selection with a symbol | `('ab', 0, 2)` with `times` gives `{value:'\\times ', caret:7}` |
| T17 | 〃 | puts the caret inside the first braces of a fraction | `('', 0, 0)` with `fraction` gives `{value:'\\frac{}{}', caret:6}` |
| T18 | 〃 | wraps the selection in a square root | `('ab', 0, 2)` with `sqrt` gives `{value:'\\sqrt{ab}', caret:8}` |
| T19 | 〃 | deletes the character before the caret | `('abc', 2, 2)` with `backspace` gives `{value:'ac', caret:1}` |
| T20 | 〃 | deletes the selection | `('abc', 0, 2)` with `backspace` gives `{value:'c', caret:0}` |
| T21 | 〃 | keeps the value when deleting at the start | `('abc', 0, 0)` with `backspace` gives `{value:'abc', caret:0}` |
| T22 | 〃 | moves the caret within the bounds | `left` at 0 gives 0; `left` at 2 gives 1; `right` at 3 on `'abc'` gives 3; `right` at 1 gives 2; the value is unchanged |
| T23 | 〃 | collapses a selection on caret moves | `('abc', 1, 3)` gives caret 1 for `left` and caret 3 for `right` |
| T24 | `mathKeyRows` | lays out 36 unique keys in six rows of six | There are 6 rows, each of length 6, with 36 unique ids |
| T25 | `mathDraftStore` | reads back a written draft | `writeMathDraft` returns true; `readMathDraft` gives the same latex and final |
| T26 | 〃 | keys drafts by student, session and question | `mathDraftStorageKey(owner)` equals `'elmanhg.mathDraft.s1.sess1.q1'`; another studentId reads `null` |
| T27 | 〃 | drops an expired draft | A draft written at `t` and read at `t + TTL + 1` gives `null`, and the key is removed |
| T28 | 〃 | drops an unparseable or invalid draft | Stored `'{'` and `{"savedAt":1,"answer":{"steps":[1]}}` both give `null`, and the keys are removed |
| T29 | 〃 | clears a draft | After `clearMathDraft(owner)`, `getItem` is `null` |
| T30 | 〃 | purges only expired math drafts | After `purgeExpiredMathDrafts`, the expired math key is removed, while the fresh math key and `elmanhg.anonymousId` remain |
| T31 | 〃 | reports a failed write | When `vi.spyOn(Storage.prototype, 'setItem')` throws, `writeMathDraft` returns `false` (the spy is restored) |
| T32 | 〃 | treats unreadable storage as no draft | When the `getItem` spy throws, `readMathDraft` returns `null` |
| T33 | `MathStepsAnswer steps` | starts with one step and a final answer | Textboxes 'Step 1' and 'Final answer' exist; 'Move step 1 up', 'Move step 1 down' and 'Remove step 1' are disabled |
| T34 | 〃 | adds a step and focuses it | After clicking 'Add a step', textbox 'Step 2' has focus and the text 'Step 2 added.' is present |
| T35 | 〃 | removes a step and focuses the previous one | Type a, b, c in 3 steps; after clicking 'Remove step 2', 'Step 2' has value `c`, no 'Step 3' exists, and 'Step 1' has focus |
| T36 | 〃 | moves a step down and keeps focus on its move buttons | Steps a, b; after 'Move step 1 down', Step 1 is `b` and Step 2 is `a`; button 'Move step 2 up' has focus; the text is 'Step moved to position 2.' |
| T37 | 〃 | moves a step up | Steps a, b, c; after 'Move step 3 up' the values are a, c, b and 'Move step 2 up' has focus |
| T38 | 〃 | stops adding at 20 steps | After 19 clicks on 'Add a step', the button is disabled and 'You can add up to 20 steps.' is shown |
| T39 | 〃 | previews a step with KaTeX | Type `x^2` in Step 1; `(await within(getByRole('group',{name:'Preview of Step 1'})).findByText('x^2')).tagName` is `'math'` |
| T40 | 〃 | shows the preview hint for a blank step | The group 'Preview of Step 1' contains 'The preview appears here.' |
| T41 | 〃 | reports every edit through onChange | With `onChange = vi.fn()`, typing `42` in 'Final answer' makes the last call's argument have `finalAnswer: '42'` |
| T42 | 〃 | disables every control when disabled | With `disabled`, both textboxes, 'Add a step' and 'Keypad' are disabled |
| T43 | 〃 | keeps LaTeX left to right in Arabic | With `lng: 'ar'`, textbox 'الخطوة ١' has `dir="ltr"` and textbox 'الإجابة النهائية' exists |
| T44 | 〃 | has no axe violations | `(await axe(container)).violations` equals `[]` (`@/test/axe`) |
| T45 | `MathStepsAnswer keypad` | opens the keypad under the focused field | Before focus there is no group 'Math keypad'. After clicking 'Step 1' the group is visible, the textbox has `inputmode="none"`, and 'Keypad' has `aria-pressed="true"` |
| T46 | 〃 | inserts a symbol at the caret and keeps focus | Type `ab` in Step 1, then `user.keyboard('{ArrowLeft}')`, then click key 'Times'; the value is `a\times b` and Step 1 has focus |
| T47 | 〃 | types inside a fraction | Click 'Step 1', 'Fraction', then key '1'; the value is `\frac{1}{}` |
| T48 | 〃 | wraps the selected text in a square root | Type `ab`, `user.tripleClick(step1)`, then click 'Square root'; the value is `\sqrt{ab}` |
| T49 | 〃 | deletes with the backspace key | Type `abc`, then click 'Delete'; the value is `ab` |
| T50 | 〃 | inserts into the final answer when it is active | Click 'Final answer', then key '7'; the final is `7`, Step 1 is `''`, and the keypad group is inside the page once |
| T51 | 〃 | hides the keypad and restores the phone keyboard | Click 'Step 1', then 'Keypad'; group 'Math keypad' is absent, `aria-pressed="false"`, and the textbox has `inputmode="text"` |
| T52 | 〃 | ignores a key that would exceed the limit | Click 'Final answer', `user.paste('1'.repeat(200))`, then click key '7'; the value length is still 200 |
| T53 | `MathStepsAnswer autosave` | saves the draft after the delay | Type `x` in Step 1 and advance 800 ms; 'Draft saved on this device.' is shown, and `JSON.parse(localStorage.getItem('elmanhg.mathDraft.s1.sess1.q1')).answer` equals `{steps:['x'], finalAnswer:''}` |
| T54 | 〃 | saves only the latest value after quick edits | Type `x`, advance 400 ms, type `y`; before 800 ms have passed since `y` the key is `null`; after 800 ms the stored step is `'xy'` |
| T55 | 〃 | restores a saved draft and says so | Seed `writeMathDraft(key, {steps:[a,b],final:'5'}, Date.now())` and render; Step 1 is `a`, Step 2 is `b`, the final is `5`, and 'Your saved draft was restored.' is shown |
| T56 | 〃 | prefers the saved draft over the initial value | Seed with `a`, render with `initialValue` of step `z`; Step 1 is `a` |
| T57 | 〃 | starts from the initial value without a draft | Render with `initialValue` steps `['z']` and final `'1'`; Step 1 is `z` and the final is `1` |
| T58 | 〃 | ignores an expired draft | Seed at `Date.now() - mathDraftTtlMilliseconds - 1`; Step 1 is `''` and the key is removed |
| T59 | 〃 | keeps another student's draft private | Seed for studentId `s2` and render as `s1`; Step 1 is `''` |
| T60 | 〃 | writes a pending draft on unmount | Type `x` then `unmount()` before 800 ms; the stored step is `'x'` |
| T61 | 〃 | writes a pending draft when the page hides | Type `x`, `window.dispatchEvent(new Event('pagehide'))`, and the stored step is `'x'`. Then type `y`, `Object.defineProperty(document,'visibilityState',{value:'hidden',configurable:true})` and `document.dispatchEvent(new Event('visibilitychange'))`; the stored step is `'xy'` (restore the property in `finally`) |
| T62 | 〃 | shows an error when the device cannot save | With `vi.spyOn(Storage.prototype,'setItem')` throwing, type `x` and advance 800 ms; 'Could not save the draft on this device.' is shown |

No existing test is modified or deleted.

## Definition of done
- [ ] The files are exactly the 25 created above plus the 3 modified. There is no change under `api/`, `ai/`, `web/src/features/{questions,quiz,exam}/`, `web/src/shared/api/generated/`, `postman/` or `package.json`/lockfile.
- [ ] No new npm dependency. KaTeX is reached only through `RichTextViewer` from `@/features/content` (grep: no `katex` import in `features/mathSteps`).
- [ ] All 62 tests (T1–T62) exist with those names and pass. `npx vitest run --coverage` exits 0. `features/mathSteps` has ≥ 80 % lines and ≥ 70 % branches.
- [ ] `npm --prefix web run typecheck`, `npm --prefix web run lint` and `npx prettier --check .` (in `web/`) exit 0.
- [ ] `npm --prefix web run build && npm --prefix web run perf:budget` exits 0, and every page is within budget.
- [ ] No physical-direction utilities, raw colours or arbitrary `[..px]` values in `features/mathSteps` (the skill §14/§16 greps are clean).
- [ ] Every textarea, keypad container and keypad key has `dir="ltr"`. Every icon-only button has a translated `aria-label`. Every key is ≥ 44 px tall (`min-h-11`), with `grid-cols-6`.
- [ ] The keypad is open by default, renders only under the active field, and sets `inputMode="none"` while open. The toggle has `aria-pressed`.
- [ ] Focus after add, remove and move follows Decision 8. The announcements are in a `role="status"` region.
- [ ] The draft key has the form `elmanhg.mathDraft.<studentId>.<sessionId>.<questionId>`, with an 800 ms debounce, flushes on unmount, `pagehide` and hidden `visibilitychange`, a 7-day TTL with purge, and `try/catch` around all storage access.
- [ ] The payload drops blank steps, trims values and converts Arabic-Indic digits and `٫`.
- [ ] Every i18n key exists in both `en.json` and `ar.json`, and `mathSteps` is registered in `resources.ar`, `resources.en` and `ns` in `web/src/app/i18n.ts`.
- [ ] `docs/math-input.md` exists with the 7 sections. `docs/design-system.md` §5.12 and the `.claude/design-system.md` `MathInput` row agree (16 px monospace, 6×6 keypad, ≥ 44 px).
- [ ] The implementation report lists the deferred question-type and integration issue for the orchestrator to file.
