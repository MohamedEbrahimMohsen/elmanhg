# Implementation — Student Progress and Multi-unit exam UI polish (#286, E19.S4)

## Files created
| Path | Lines | Purpose |
|---|---|---|
| `web/src/shared/ui/optionCard.ts` | 7 | Option-card class recipe (base, idle, disabled), shared by quiz options and both multi-exam pickers (D4) |
| `web/src/features/exam/hooks/useMultiExamBlockReason.ts` | 32 | `minimumUnits`, `MultiExamBlockReason`, block-reason hook (C5) |
| `web/src/features/exam/components/MultiExamStartBar.tsx` | 54 | Always-rendered start card: summary, reason caption, start error, primary button, paywall (C6) |
| `web/src/features/progress/components/WeakSpotRow.tsx` | 36 | Compact weak-spot row (C9) |
| `web/src/features/progress/components/WeakSpotList.tsx` | 47 | One card list capped at 3 with Show all / Show less (C10) |
| `web/src/features/progress/components/WeakSpotList.test.tsx` | 117 | T13–T17 |
| `.process/286-…/layout-probe.js` | 105 | Probe, verbatim from the plan |
| `.process/286-…/02-layout-audit.md` | 86 | Layout audit record and before/after budgets |

## Files modified
| Path | Change |
|---|---|
| `web/src/styles/app.css` | `@layer base`: `fieldset { min-inline-size: 0 }`, `legend { float: inline-start; inline-size: 100% }`, `legend + * { clear: both }` after `:disabled` |
| `web/src/features/questions/components/ChoiceAnswerInputs.tsx` | Uses `optionCardClassName` / `optionCardIdleClassName`; class strings unchanged |
| `configuration/ChoiceListSettingForm`, `gradeReview/GradeReviewDecisionField`, `mathSteps/MathStepsInput`, `questions/{ChoiceOptions,DiagramImage,DiagramItems,DiagramZones,FillBlanks,MathAnswers,MathSolution,ModelAnswers,RubricCriteria,RubricLevels}Field` | Legend `mb-*` removed |
| `questions/components/NormalizationRulesField.tsx` | Legend `mb-2` → `mb-1` |
| `exam/components/MultiExamUnitPicker.tsx`, `MultiExamSizePicker.tsx` | Rewritten per C1 / C2 |
| `exam/components/MultiExamSubjectSelect.tsx` | `md:max-w-xs` |
| `exam/components/MultiExamSubjectSection.tsx` | C3: `minimumUnits` imported, preview only with ≥ 2 units, `MultiExamStartBar` always |
| `exam/components/MultiExamPreview.tsx` | C4: `canStart`, start block, shortfall panel, paywall removed |
| `mastery/components/HeadlineCounterCard.tsx` | C7: h1 / display-desktop, `gap-3`, `MasteryBar`, `dl` of 3 chips |
| `progress/components/SubjectProgressSection.tsx` | `ul` → `flex flex-col gap-3` |
| `progress/components/UnitProgressTable.tsx` | C8: plain unit link, row `hover:bg-soft` |
| `progress/components/WeakLessonList.tsx`, `WeakObjectiveList.tsx` | C11 / C12: thin mappers over `WeakSpotList` |
| `exam/i18n/{en,ar}.json`, `mastery/i18n/{en,ar}.json`, `progress/i18n/{en,ar}.json` | Strings table: `multi.available` changed, `multi.summary`, `multi.reasonInProgress`, `headline.{barLabel,seen,mastered,streak,count,streakValue}` added, `headline.meta` deleted, `weakSpots.{showAll,showLess}` added |
| `exam/pages/MultiExamBuilderPage.test.tsx` | T1, T2 (renamed), T3 modified; T4, T5, T6 added |
| `mastery/pages/StudentHomePage.test.tsx` | T7 modified; T8, T9 added |
| `progress/pages/ProgressPage.test.tsx` | T10 modified; T11 added |
| `docs/claude-design-prompt.md` | §2.4 Quiz option / Progress / Inputs, §4 `#/student`, `#/student/multi-exam`, `#/student/progress`, §6 Forms and List screens, worded as the Docs-sync table |
| `docs/prototype.md` | Walkthrough item 6 appended |
| `docs/design-system.md` | v2.2 / 2026-10-04, decision, §3.1 display, §5.3, §5.6, §5.8, new §5.15 Compact list |
| `.claude/design-system.md` | v2.2 title and source line, `type.display`, QuizOption / Progress / Input rows, new CompactList row, change-log 2.2. `gen:tokens` leaves `tokens.css` unchanged |

No file under `features/shell`, `features/avatar`, `routes/**`, `routeTree.gen.ts` or `scripts/perf/budgets.json` was touched. No Postman change (no API change).

## Deviations
None in production code or tests. Notes on the audit, which do not change any plan contract:
| Plan said | Reality | What I did |
|---|---|---|
| D2/D3: question-editor legends get "8 px in `gap-2` fieldsets" | Those fieldsets are `gap-3`, so they measure 12 px. `ChoiceListSettingForm` is `flex-wrap gap-3` and went from 4 to 12 px | Recorded the measured values as intended in `02-layout-audit.md` §3 (rule: spacing = group gap) |
| Layout check: switch language "as in #276" | In the production build the `en` bundles load only through `initI18n` | The driver took the app's i18next instance from the React tree, added the tree's own `en.json` bundles, then `changeLanguage('en')` (§1 of the audit) |

## Build & test
All run in `web/` on the branch (exit 0 unless stated):
- `npx tsc -b` (typecheck): exit 0.
- `npm run lint` (`eslint . --max-warnings=0`): exit 0.
- `npx prettier --check "src/**/*.{ts,tsx,json,css}" --end-of-line auto`: "All matched files use Prettier code style!" (after `prettier --write` on 4 of my files: line wrapping only).
- `npm test -- --run --coverage`: `Test Files 298 passed (298)`, `Tests 1749 passed (1749)`, All files 95.39 % statements / 84.21 % branches. There were no timeouts, so no reruns were needed.
- `npm run build`: exit 0 ("Precompressed 292 files").
- `npm run perf:budget`: all ok. Base → branch, brotli bytes: entry 194282 → 194581 (+299), landing 203512 → 203833 (+321), lesson 225943 → 226223 (+280), quiz 241517 → 241924 (+407), admin-dashboard +323, admin-users +335, teacher-home +371. Every change is under 512 B, and `budgets.json` is unchanged.
- CI literal-token greps from `web-ci.yml` (physical-direction and hex/px/palette): no matches (grep exit 1 for both, so the CI step passes).
- Route-tree check: `git status --porcelain src/routeTree.gen.ts` is empty after the build. `gen:tokens` leaves `tokens.css` with no diff.
- Layout check (production `vite build` + `vite preview` proxied to the demo on :8080, headless Chrome over CDP, DOM measurement): before 200 findings (student 35/35/32/32/33/33, admin 0), after **0 findings** across student 5 routes and admin 10 route/type variants × 375/768/1280 × ar/en. Details are in `02-layout-audit.md`.

## Notes for review
- `MultiExamBuilderPage.test.tsx` is now 348 lines (it was 288, already over the 200-line rule before this story). The plan places T4–T6 in it.
- `useMultiExamBlockReason` passes `enabled: enoughUnits`. While Start is enabled, a pending preview or a preview error does not block it (D8). The start error alert lives in the start bar, so `shows the server error when the start is refused` still finds a single `alert`.
- T5 asserts `20 questions` is checked first, then clicks `60 questions` and checks the summary and both radios. These are the plan's assertions, with the summary checked before the radios.
- Demo logins came from `scripts/demo.sh` at `9780b428` and were used only in a scratchpad file, which is now deleted. Nothing committed contains them. Nothing is committed.
