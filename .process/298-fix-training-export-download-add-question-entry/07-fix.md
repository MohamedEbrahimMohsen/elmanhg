# CodeRabbit fixes — PR #299

| # | What I changed | File |
|---|---|---|
| RC1 | Added an optional `onCloseAutoFocus` prop to `AddQuestionDialog` and passed it to `DialogContent`, which spreads it onto Radix Content. `AddQuestionButton` now keeps a `useRef<HTMLButtonElement>` on the opener. Its `onCloseAutoFocus` calls `event.preventDefault()` and then focuses that ref. | `web/src/features/questions/components/AddQuestionDialog.tsx`, `web/src/features/questions/components/AddQuestionButton.tsx` |
| RC1 test | Added `returns focus to the add question button when the dialog closes`. It opens the dialog in Arabic, presses Escape, checks the dialog is gone, and asserts that «إضافة سؤال» has focus. | `web/src/features/questions/components/AddQuestionButton.test.tsx` |

## Why the ref approach rather than `DialogTrigger`
`@/shared/ui/dialog` exports only `Dialog` and `DialogContent`, not `DialogTrigger`. Using a trigger would mean one of two things:
- widening the shared UI module, or
- moving the opener into `AddQuestionDialog`.

Either one changes the component structure that the PR review already approved. The ref approach touches only the two feature files and keeps the opener's existing behaviour: `aria-haspopup`, the click handler, and the Link branch when the list is filtered by a lesson.

## Mutation check
I removed `openerRef.current?.focus();`. The new test then failed (`expect(element).toHaveFocus()`, 1 failed | 7 passed). After I restored the line, it passed (8 passed).

## Checks
- `npm run typecheck`: clean.
- `npx eslint --max-warnings=0 src/features/questions/components`: clean.
- `npx prettier --check --end-of-line auto src/features/questions/components`: all files formatted.
- CI greps (RTL physical-direction classes and hard-coded colours): no matches.
- `npx vitest run src/features/questions`: 45 files, 298 tests passed.
