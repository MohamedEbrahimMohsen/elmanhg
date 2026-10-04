VERDICT: CHANGES_REQUESTED

# Review — Student Progress and Multi-unit exam UI polish (#286, E19.S4)

## Blocking

### 1. `docs/exams.md` still describes the old multi-exam builder: no start button on a shortfall or an open exam, and the count in parentheses
**Where:** code `web/src/features/exam/components/MultiExamStartBar.tsx:27-52` (the Start button is always rendered and disabled with a reason), `web/src/features/exam/components/MultiExamSubjectSection.tsx:62-65`, `web/src/features/exam/i18n/ar.json:87` («{count, number} سؤال متاح», no parentheses). Doc `docs/exams.md:187`, § "Student screens (web)", bullet **`/student/multi-exam`**.
**Rule:** `.claude/rules/docs-sync.md`, divergence (product behaviour or state change, and the doc describing that screen still says the old thing). Review order #8.
**Problem:** The doc says: units are checkboxes with «({count} سؤال متاح)»; with fewer than two units the page *says* «اختر وحدتين على الأقل.»; with two or more it shows the preview "then «ابدأ الامتحان», or the shortfall warning with no button"; and with an open exam it shows the warning "with a link and no start button". The code now always renders «ابدأ الامتحان», next to a selection summary. It is disabled with the reason (chooseTwo / inProgress / shortfall) as its caption and `aria-describedby`. The shortfall panel is gone from the preview, units are option cards, and the caption has no parentheses. `docs/claude-design-prompt.md` §4 and `docs/prototype.md` were updated, but `docs/exams.md` was not, so the two docs now contradict each other.
**Failure:** A reader asks "is there a Start button when the selected units are short of questions, or when an exam is open?" `docs/exams.md:187` says no. The app and `docs/claude-design-prompt.md` §4 say yes, disabled with its reason.
**Fix:** Rewrite the `/student/multi-exam` bullet in `docs/exams.md:187` to match the code. Describe the unit and size option cards (count «N سؤال متاح» on each unit card, a no-blueprint unit disabled), the preview with two or more units, and the always-visible start card: summary, plus «ابدأ الامتحان» disabled with «اختر وحدتين على الأقل.» / «أنهِ الامتحان الجاري أولًا.» / the shortfall text. Keep the in-progress warning with its link.

## Non-blocking
- `web/src/styles/app.css:146-149`: `float: inline-start` needs Chrome 118+ / Safari 15+ / Firefox 55+. Lightning CSS emits it as written. On Chromium 111-117 the float is ignored and the legend goes back to sitting on the border, the old look. This is cosmetic only: the accessible name is unaffected.
- `web/src/features/exam/hooks/useMultiExamBlockReason.ts:16-39`: the hook has branching logic but no test file of its own (react skill §5, "one test file per component/hook with logic"). All three reasons and the null case are covered through the page tests T2, T3, T4 and T6. A small hook test would also pin the "placeholder data does not block" branch (`:35`), which no test exercises.
- `web/src/features/progress/components/WeakSpotList.test.tsx:62-83`: `aria-controls` pointing to the list id is not asserted. Only `aria-expanded` is.
- `web/src/features/exam/pages/MultiExamBuilderPage.test.tsx`: 348 lines, against the 200-line rule. It was already 288 before this story, and the plan placed T4-T6 here. Split by state later.
- `docs/claude-design-prompt.md` §2.4 Inputs and `docs/design-system.md` §5.8: «13 px `--text-2` in a form». Two form legends keep their own type: `GradeReviewDecisionField.tsx:20` (ui 600) and `MathStepsInput.tsx:43` (h3). Both styles predate this story. Consider wording the rule as "typically".
- D8 window: after a selection change, while the new preview is still loading (`isPlaceholderData`), Start is enabled even if the previous selection was short. The server stays authoritative: `EXAM_SHORTFALL` shows as the start-bar alert. This matches the plan.

## Verified
- **Legend rule** (`app.css:142-153`): exactly the three base declarations from D1, placed after `:disabled`, and present in the built CSS (`legend{float:inline-start;inline-size:100%}`, `legend+*{clear:both}`). All 21 fieldsets are `flex` containers, so the floated legend becomes the first flex item and the fieldset gap spaces it.
  - The three `sr-only` legends (`ReplyPanel:23`, `ChoiceAnswerInputs:60`, `TeacherSubjectsCell:22`) are absolutely positioned, so float computes to none and the utility-layer `width:1px` beats the base `inline-size`. Unaffected.
  - The `flex-wrap` `ChoiceListSettingForm` legend takes its own 100% row.
  - Every legend is still the first `<legend>` child of its fieldset, so the group accessible name (HTML-AAM) is unchanged. Floating does not affect the name computation.
  - The legend `mb-*` edits are exactly as in D3, including `NormalizationRulesField` `mb-1`.
- **Option cards:**
  - Native checkbox/radio inside a `<label>`: keyboard via Tab, Space, and arrows within the radio group (shared `name`).
  - Focus ring: `focus-visible:ring-2 ring-ring ring-offset-2` on the input.
  - Selected state from the native checked state (`has-checked:border-accent bg-accent-soft`).
  - Units: `aria-labelledby` points to the name and `aria-describedby` to the count, so the name stays "Mechanics" (T1, T6). A unit with no blueprint is a disabled input on an `opacity-45` card with no hover.
  - `ChoiceAnswerInputs` renders identical class strings.
- **Start bar:**
  - Always rendered, and the reasons follow the C5 priority order (inProgress, then chooseTwo, then shortfall on non-placeholder data).
  - The reason is a visible caption under the summary, inside an `aria-live="polite"` region, and is the button `aria-describedby` only while it is blocked.
  - The start error alert and the paywall moved into the bar. The preview no longer starts anything.
- **Progress:**
  - Headline: h1 below lg and display-desktop from lg, `text-balance`, `gap-3`. Mastered-share `MasteryBar` (0 when the total is 0), then a `dl` of 3 chips. `headline.meta` is removed from both locales.
  - Subjects: `flex flex-col` full width.
  - Unit name: a plain `Link` with a focus ring; the row has `hover:bg-soft`; the exam action is unchanged.
  - Weak spots: `WeakSpotList` caps at 3 with «عرض الكل (N)» / «عرض أقل» (`aria-expanded`, `aria-controls`). Lessons and objectives are capped independently.
- **Latin digits:** ar strings use ICU number formatting and plurals. T6 («لم تختر وحدات بعد · 20 سؤال», «12 سؤال متاح») and T11 («متبقّي لك 40 سؤال من 60», definitions 30 / 20 / «3 أيام», «إتقان 20٪») pass.
- **Commands I ran myself in `web/`:**
  - `npx tsc -b --noEmit` exit 0. `npx eslint . --max-warnings=0` exit 0. `npx prettier --check . --end-of-line auto` clean.
  - `npx vitest run`: 298 files, 1749 tests passed, no timeouts. The coverage run reports 95.42% statements and 84.21% branches.
  - `npm run build` exit 0.
  - Both CI literal greps return exit 1 (no matches). `routeTree.gen.ts`, `tokens.css` and `scripts/perf/budgets.json` are unchanged. Nothing under `features/shell`, `features/avatar` or `routes` was touched.
- **perf:budget** passed on the branch. Brotli bytes, measured by me against a fresh `ea2e23e5` build:

  | page | change |
  |---|---|
  | entry | +299 |
  | landing | +321 |
  | lesson | +280 |
  | quiz | +407 |
  | admin-dashboard | +323 |
  | admin-users | +335 |
  | teacher-home | +371 |

  All are under 512 B and match `02-implementation.md` / `02-layout-audit.md` §4 exactly. No budget was raised.
- **Plan coverage:** every file to create exists. T1-T17 exist with the names from the plan, and only T1/T2/T3/T7/T10 modify existing tests.
- **Docs-sync:** the planned edits to `docs/claude-design-prompt.md` (§2.4, §4, §6), `docs/prototype.md` item 6, `docs/design-system.md` (v2.2, §3.1, §5.3, §5.6, §5.8, §5.15) and `.claude/design-system.md` were made and agree with each other and with the code. The one exception is `docs/exams.md` (finding 1).
- No Postman change is needed (no API change).

## Test quality
- **MultiExamBuilderPage:**
  - T2/T3/T4/T6 assert the disabled state together with the exact accessible description, so a wrong reason or a missing `aria-describedby` fails them. T4 also checks the description goes away once Start is enabled.
  - T5 constrains both the radio state and the summary.
  - Gap: the placeholder-data branch is not exercised (non-blocking above).
- **StudentHomePage / ProgressPage:**
  - T7/T10 pin the order and values of the term/definition pairs.
  - T8 (33) and T9 (0 when servable is 0) would fail on a wrong formula or on a division by zero (NaN).
  - T11 pins Latin digits.
- **WeakSpotList:**
  - T13/T14/T16 pin the cap, the count in the label, `aria-expanded` and the collapse.
  - T15 is the negative case.
  - T17 pins the row contents and the link target.
- None of these tests is vacuous.
