# Math input (math-with-steps answer)

## 1. Purpose

Students answer math problems as an ordered list of steps plus a final answer, written in LaTeX on their phone. Epic E15 splits the work:

| Story | Part |
|---|---|
| #121 | The input component in `web/src/features/mathSteps/` (this document) |
| #122 | CAS check of the final answer |
| #123 | LLM grading of the steps |

The component is **not mounted anywhere yet**. No `MathSteps` question type exists, so nothing authors, serves or grades one. The question type end to end (type, schemas, authoring, serving, mounting in quiz and exam) is a deferred backlog item, planned into #122.

The barrel `@/features/mathSteps` exports:

- `MathStepsAnswer`: the autosaving component for students.
- `MathStepsInput`: the controlled component without autosave, for future admin model-solution authoring.
- `clearMathDraft`, `emptyMathStepsValue`, `fromMathStepsPayload`, `toMathStepsPayload` and the types.

## 2. Answer shape

```json
{ "steps": ["2x + 3 = 7", "2x = 4"], "finalAnswer": "x = 2" }
```

`toMathStepsPayload` builds it from the editor value:

- Every value is trimmed, and blank steps are dropped.
- Arabic-Indic digits (U+0660–0669, U+06F0–06F9) become Latin digits, and `٫` (U+066B) becomes `.`. The field itself keeps what the student typed, so the caret never jumps.

| Limit | Value |
|---|---|
| Steps | 20 (`mathStepsMaxCount`) |
| Characters per step | 500 (`mathStepMaxLength`) |
| Characters in the final answer | 200 (`mathFinalAnswerMaxLength`) |

The limits are client-side until the question type brings a server contract. Textareas carry `maxLength`, and a keypad key that would pass the limit does nothing.

## 3. Input

Each step and the final answer is a plain `<textarea>` holding LaTeX. It is `dir="ltr"`, in the system monospace at 16 px (smaller text makes iOS zoom on focus; see [design-system.md](design-system.md) §5.12).

Under each field a preview renders the LaTeX with KaTeX through `RichTextViewer`, as a `block-math` node ([rich-text.md](rich-text.md)). So there is one KaTeX path, sanitised by `SafeHtml` and loaded lazily. Invalid LaTeX shows the KaTeX error inline. A blank field shows «ستظهر المعاينة هنا.».

Arabic words go inside `\text{…}` (the «نص» key).

## 4. Keypad

The keypad is laid out left to right, like a calculator: 6 columns × 6 rows, every key at least 44 px tall.

| Row | Keys |
|---|---|
| 1 | 7 · 8 · 9 · ÷ (`\div `) · ( · ) |
| 2 | 4 · 5 · 6 · × (`\times `) · xⁿ (`^{}`) · √ (`\sqrt{}`) |
| 3 | 1 · 2 · 3 · − (`-`) · a/b (`\frac{}{}`) · π (`\pi `) |
| 4 | 0 · . · = · + · x · y |
| 5 | ≤ (`\le `) · ≥ (`\ge `) · ≠ (`\ne `) · ± (`\pm `) · sin · cos |
| 6 | tan · log · نص (`\text{}`) · caret left · caret right · delete |

- A key inserts at the caret and replaces a selection. Template keys (xⁿ, √, a/b, نص) put the caret inside the first braces, and wrap the selection when there is one.
- Delete removes the selection or the character before the caret. The arrow keys move the caret, or collapse a selection.
- Keys keep the field's focus and selection (`preventDefault` on `pointerdown`). After a key the field gets focus back with the caret in place.
- The keypad is **open by default** and renders under the **active** field only (the last math field that got focus). While it is open, the fields have `inputmode="none"`, so the phone keyboard stays hidden. A physical keyboard still types.
- The «لوحة الرموز» toggle (`aria-pressed`) closes it. The fields then get `inputmode="text"`, for letters and Arabic from the phone keyboard.

## 5. Steps list

- **Add** («أضف خطوة») appends an empty step and focuses it. It is disabled at 20 steps, with a note.
- **Remove** («احذف الخطوة n», Danger) focuses the previous step, or the new first step. It is disabled when only one step is left.
- **Move up / move down** swap a step with its neighbour. Focus stays on the moved step's button in the same direction, or on its other button when that one is now disabled. There is no drag, so reordering works with touch and keyboard (WCAG 2.5.7).
- Every add, remove and move is announced in an `sr-only` `role="status"` region, e.g. «نُقلت الخطوة إلى الموضع ٢.».

## 6. Autosave

`MathStepsAnswer` keeps a draft **on the device only**. Drafts are never sent to the server.

| Rule | Value |
|---|---|
| Storage | `localStorage` |
| Key | `elmanhg.mathDraft.<studentId>.<sessionId>.<questionId>` (the `owner` prop; all three are required, so another account on a shared phone never sees the draft) |
| Stored JSON | `{"savedAt": <ms>, "answer": {"steps": string[], "finalAnswer": string}}`, untrimmed so the layout restores exactly |
| Debounce | 800 ms after the last edit (`mathDraftSaveDelayMilliseconds`) |
| Flush | A pending write happens at once on `pagehide`, on `visibilitychange` to hidden and on unmount |
| Lifetime | 7 days (`mathDraftTtlMilliseconds`). An expired or unreadable draft is removed when read, and every mount sweeps all expired math drafts |

- A valid stored draft **wins** over `initialValue`, and the status line says «استعدنا مسودتك المحفوظة.». After a save it says «حُفظت المسودة على هذا الجهاز.».
- Storage access never breaks the answer. A failed write (quota, private mode) shows «تعذّر حفظ المسودة على هذا الجهاز.» in Danger, and editing continues. A failed read counts as no draft.
- A new `owner` remounts the editor, which flushes the old draft and loads the new one.
- Clearing the draft after submit is the integrator's job: `clearMathDraft(owner)`.

## 7. Integration checklist (for the question-type story)

1. Mount `MathStepsAnswer` with `owner = { studentId, sessionId, questionId }`.
2. Send `toMathStepsPayload(value)` on «تحقّق» (quiz) and on each exam autosave (`onChange` feeds the existing `PUT /api/exams/{id}/answers/{questionId}` debounce).
3. Call `clearMathDraft(owner)` after a successful submit.
4. Re-run `npm run build && npm run perf:budget` and check the `quiz` page budget.
