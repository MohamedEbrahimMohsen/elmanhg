# Layout audit — Student Progress and Multi-unit exam UI polish (#286)

## 1. Environment

- **Code:** `feature/286-student-progress-and-multi-unit-exam-ui-polish` = base `ea2e23e5` + the uncommitted #286 working tree. **Baseline ("before"):** the same worktree built at `ea2e23e5` before any edit.
- **Server:** each tree built with `npm run build` (tsc + `vite build`) and served with `vite preview` (before on :5277, after on :5286), `API_PROXY_TARGET=http://localhost:8080` (the running demo stack, every provider fake).
- **Driver:** headless Chrome 154 over CDP from a Node script (scratchpad, not committed). Per run: `Emulation.setDeviceMetricsOverride` width 375 / 768 / 1280, height 900, `mobile` below 900; full page load per route; wait for `<main>` and no `[aria-busy="true"]`; inject `.process/276-…/layout-audit.js` (unchanged), then `layout-probe.js` (verbatim from the plan); call `__probe286.audit()` (probe checks + `__layoutAudit` overflow / targets / tables). Language: `ar` is the default; for `en` the driver took the app's i18next instance from the `I18nextProvider` props in the React tree, added the `en` bundles from that tree's own `src/**/i18n/en.json` (the production build lazy-loads them only through `initI18n`), then `changeLanguage('en')`. `ar` → `rtl` and `en` → `ltr` held on every finding.
- **Accounts:** the demo stack's seeded admin and first load-test student (from the seed / `scripts/demo.sh`). Credentials are not recorded here.
- **Discovered ids:** the first subject with two units that have a blueprint (3 units, 2 with blueprints) from `GET /api/exams/subjects/{id}/multi-unit`; the lesson id from the student's `nextLesson`, reused for `/admin/question/new/<lessonId>`.
- **Question editor:** on `/admin/question/new/<lessonId>` the driver also switched the type select through every type (Mcq, Multi, TrueFalse, Fill, Short, Essay, MathSteps, DragDrop) and audited each, which renders 13 of the 14 question/mathSteps visible-legend fieldsets.
- **Code-audited only (no reachable screen in the demo):** `gradeReview/GradeReviewDecisionField` (empty grade-review queue, teacher role), `content/ObjectivesField` (admin lesson editor, not in the plan's route list), `askTeacher/ReplyPanel`, `questions/ChoiceAnswerInputs` and `users/TeacherSubjectsCell` (all three `sr-only` legends; `sr-only` is absolutely positioned, so the float rule has no effect).

## 2. Matrix

Routes: student = `/student`, `/student/progress`, `/student/multi-exam`, `/onboarding`, `/student/multi-exam?subjectId=<id>&unitIds=<two ids>` (5); admin = `/admin/configuration`, `/admin/question/new/<lessonId>` + 8 type variants (10).

| role | width | lang | routes | findings before (ea2e23e5) | findings after |
|---|---|---|---|---|---|
| student | 375 | ar | 5 | 35 | 0 |
| student | 375 | en | 5 | 35 | 0 |
| student | 768 | ar | 5 | 32 | 0 |
| student | 768 | en | 5 | 32 | 0 |
| student | 1280 | ar | 5 | 33 | 0 |
| student | 1280 | en | 5 | 33 | 0 |
| admin | 375 | ar | 10 | 0 | 0 |
| admin | 375 | en | 10 | 0 | 0 |
| admin | 768 | ar | 10 | 0 | 0 |
| admin | 768 | en | 10 | 0 | 0 |
| admin | 1280 | ar | 10 | 0 | 0 |
| admin | 1280 | en | 10 | 0 | 0 |

## 3. Findings

Counts are per language (identical in ar and en).

| # | route | width | dir | check | target | detail (before) | fix (file:line) | status |
|---|---|---|---|---|---|---|---|---|
| F1 | `/student/multi-exam` (both states) | all | both | legend-on-border | «اختر وحدتين أو أكثر:», «عدد الأسئلة» | legend top 21 px above the fieldset content box (drawn on the border) | `web/src/styles/app.css:142-153` base rule; `exam/components/MultiExamUnitPicker.tsx:19`, `MultiExamSizePicker.tsx:18` | fixed (0 px) |
| F2 | `/student/multi-exam` | all | both | option-height | every unit and size label | 44 px plain labels | `shared/ui/optionCard.ts:1`; pickers use it (48 px cards) | fixed |
| F3 | `/student/multi-exam` | all | both | option-caption | each unit | count caption outside the label | `MultiExamUnitPicker.tsx:43-50` caption inside the card, `aria-describedby` | fixed |
| F4 | `/student/multi-exam` | 768, 1280 | both | option-grid | unit list | 1 column, expected 2 / 3 | `MultiExamUnitPicker.tsx:20` `grid-cols-1 md:grid-cols-2 lg:grid-cols-3` | fixed |
| F5 | `/student/multi-exam` (no selection) | all | both | start-visible | «ابدأ الامتحان» | button not rendered | `exam/components/MultiExamStartBar.tsx`, `MultiExamSubjectSection.tsx:65` | fixed (always rendered; disabled with reason, beside the summary from 700 px) |
| F6 | `/student/multi-exam` | 768, 1280 | both | select-width | subject select | full main width | `exam/components/MultiExamSubjectSelect.tsx:18` `md:max-w-xs` | fixed (≤ 320) |
| F7 | `/student`, `/student/progress` | all | both | headline | headline card | no stat chips / bar | `mastery/components/HeadlineCounterCard.tsx` | fixed |
| F8 | `/student/progress` | all | both | unit-link-pill | unit names | `sm` pill button (border 1 px, inline-flex) | `progress/components/UnitProgressTable.tsx:38-44` plain link | fixed |
| F9 | `/student/progress` | 1280 | both | subject-width | Chemistry card | half-width grid cell | `progress/components/SubjectProgressSection.tsx:31` `flex flex-col` | fixed |
| F10 | `/student/progress` | 375 | both | weak-row-height | weak lesson / objective rows | rows of 7 stacked lines over 120 px | `progress/components/WeakSpotRow.tsx` | fixed |

### D2 legend spacing (intended)

Measured legend-bottom → first-control gap, before → after, at 1280 (same at 375 / 768, both languages):

| fieldset | route | before | after | status |
|---|---|---|---|---|
| MultiExamUnitPicker | `/student/multi-exam` | 20 (legend on border) | 12 | intended (gap-3) |
| MultiExamSizePicker | `/student/multi-exam` | 20 (legend on border) | 12 | intended (gap-3) |
| SubjectInterestsForm | `/onboarding` | 0 | 8 | intended (gap-2, D3 "gains 8 px") |
| ChoiceListSettingForm | `/admin/configuration` | 4 | 12 | intended (flex-wrap gap-3) |
| ChoiceOptionsField | question new (Mcq, Multi) | 8 | 12 | intended (gap-3) |
| FillBlanksField | question new (Fill) | 8 | 12 | intended (gap-3) |
| NormalizationRulesField | question new (Fill) | 8 | 8 | intended (`mb-1` + gap-1) |
| RubricCriteriaField | question new (Essay) | 8 | 12 | intended |
| RubricLevelsField | question new (Essay) | 8 | 12 | intended |
| ModelAnswersField | question new (Essay) | 8 | 12 | intended |
| MathAnswersField | question new (MathSteps) | 8 | 12 | intended |
| MathSolutionField | question new (MathSteps) | 8 | 12 | intended |
| MathStepsInput | question new (MathSteps) | 12 | 12 | intended (unchanged) |
| DiagramImageField | question new (DragDrop) | 8 | 12 | intended |
| DiagramZonesField | question new (DragDrop) | 8 | 12 | intended |
| DiagramItemsField | question new (DragDrop) | 8 | 12 | intended |

The plan predicted 8 px for `gap-2` fieldsets; the question-editor fieldsets above are `gap-3`, so they measure 12 px, which is the D2 rule (spacing = group gap).

## 4. perf:budget (brotli bytes; budgets.json unchanged)

| page | base ea2e23e5 | branch | Δ | max |
|---|---|---|---|---|
| entry | 194282 (190 KB) | 194581 (191 KB) | +299 | 215040 (210 KB) |
| landing | 203512 (199 KB) | 203833 (200 KB) | +321 | 225280 (220 KB) |
| lesson | 225943 (221 KB) | 226223 (221 KB) | +280 | 245760 (240 KB) |
| quiz | 241517 (236 KB) | 241924 (237 KB) | +407 | 261120 (255 KB) |
| admin-dashboard | 218143 (214 KB) | 218466 (214 KB) | +323 | 238592 (233 KB) |
| admin-users | 250603 (245 KB) | 250938 (246 KB) | +335 | 276480 (270 KB) |
| teacher-home | 250989 (246 KB) | 251360 (246 KB) | +371 | 271360 (265 KB) |

Quiz, lesson, entry and landing each grow by less than 0.5 KB (512 B).
