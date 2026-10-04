# Plan — Student Progress and Multi-unit exam UI polish (#286, E19.S4)

## Goal
After this ships a student on `/student/multi-exam` picks units and a size from clear option cards in a responsive grid, sees the question count on each unit card, and always sees «ابدأ الامتحان» next to a selection summary, disabled with a stated reason until the exam can start. On `/student/progress` (and the headline on `/student`) the counter reads cleanly with a mastered-share bar and labelled stat chips, each subject card uses the full width, unit names are plain links, and weak lessons and objectives are compact one-line rows capped at 3 with «عرض الكل (N)». No group label (`<legend>`) anywhere in the app sits on its group's border.

## Scope
**In:** `web/` only. A shared legend fix in the global stylesheet, applied to all 21 fieldsets. Legend spacing normalised in the fieldsets that have a visible legend. A shared option-card class recipe, used by the quiz options and by both multi-exam pickers. The multi-exam start bar and its block-reason hook. The headline counter card. The progress subject section, unit table, weak lists and the new `WeakSpotList` / `WeakSpotRow`. ar/en strings. Tests. A DOM layout probe at 375/768/1280. Docs-sync in `docs/claude-design-prompt.md` §2.4/§4/§6, `docs/prototype.md`, `docs/design-system.md` and `.claude/design-system.md`.
**Out:**
- API and generated client (no API change).
- App shell, nav, `routes/**`, `routeTree.gen.ts` and `scripts/perf/budgets.json`, all owned by the parallel lane #288.
- The landing hero.
- The exam-start page (`ExamStartActions`).
- The «٪» → «%» copy change (D10).
- Admin student-progress view (`features/users`).
**Deferred:** none.

Morabh reuse: not applicable. Morabh is a backend (`apis`) repo, and this story is web-only. Every piece below is "new — no Morabh equivalent".

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| D1 | What is the "shared fieldset/legend" fix? | One global rule in `web/src/styles/app.css` `@layer base`: `fieldset { min-inline-size: 0 }`, `legend { float: inline-start; inline-size: 100% }`, `legend + * { clear: both }`. A floated legend is not a "rendered legend" (HTML spec), so the browser no longer draws it in the fieldset border. In the flex fieldsets this repo uses, it becomes the first flex item, so the fieldset `gap` spaces it. | There is no shared Fieldset component. All 21 fieldsets are hand-written `<fieldset>`. A base rule fixes every current and future fieldset with zero JS and no new abstraction (skill §6.7). `sr-only` legends are absolutely positioned, so float has no effect on them. `min-inline-size: 0` stops fieldsets forcing horizontal scroll at 375. |
| D2 | Legend spacing after D1 | Spacing between a legend and its first control becomes the fieldset `gap`. Remove the legend's `mb-*` everywhere except `NormalizationRulesField` (`gap-1`), where `mb-2` → `mb-1` keeps 8 px. Results: 8 px in `gap-2` fieldsets, 12 px in `gap-3` fieldsets. | One rule (spacing = group gap) replaces today's mix of 0, 4, 8 and 12 px. These are intended changes and are listed in the audit (§Layout check). |
| D3 | Every fieldset in `web/` | 21 total. **Card fieldsets (the bug):** `exam/MultiExamUnitPicker`, `exam/MultiExamSizePicker`. **Visible legend, spacing normalised:** `configuration/ChoiceListSettingForm`, `gradeReview/GradeReviewDecisionField`, `mathSteps/MathStepsInput`, `questions/{ChoiceOptionsField, DiagramImageField, DiagramItemsField, DiagramZonesField, FillBlanksField, MathAnswersField, MathSolutionField, ModelAnswersField, NormalizationRulesField, RubricCriteriaField, RubricLevelsField}`. **Visible legend, no class change (gains 8 px from D1):** `content/ObjectivesField`, `onboarding/SubjectInterestsForm`. **`sr-only` legend, unaffected:** `askTeacher/ReplyPanel`, `questions/ChoiceAnswerInputs`, `users/TeacherSubjectsCell`. | From `grep -rn "<fieldset" web/src`. |
| D4 | Option-card style | New `web/src/shared/ui/optionCard.ts` holds the exact QuizOption classes already in `ChoiceAnswerInputs`. `ChoiceAnswerInputs` and both multi-exam pickers use it. | This is the second use, so skill §6.7 allows sharing it. The classes already exist in the global CSS, so no CSS bytes are added. The design system already specifies QuizOption (48 px, r.md, accent.soft + accent selected). |
| D5 | Unit grid | `grid grid-cols-1 gap-3 md:grid-cols-2 lg:grid-cols-3`: 1 column below 700 px, 2 from 700 px, 3 from 900 px. | The same grid as `HomeSubjects`, `PlanCardGrid` and `DashboardPage`. Every class already exists. |
| D6 | Count control | Radio option cards in `grid grid-cols-1 gap-3 md:grid-cols-3`. | The story allows "same option-card style". This avoids a new segmented component. At 375 px, three cards of about 116 px would not fit side by side. |
| D7 | Unit card accessible name | The input gets `aria-labelledby={nameId}` and `aria-describedby={captionId}`. The whole `<label>` stays the click target. | The checkbox's accessible name stays exactly the unit name, so existing tests that use `{ name: 'Mechanics' }` keep working. The count becomes its description. |
| D8 | When is Start blocked? | Reasons, in priority order: `inProgress` → `chooseTwo` (fewer than 2 units) → `shortfall` (preview data not a placeholder and `isAvailable === false`). Start is **not** blocked while the preview loads or after a preview error, because the server is authoritative and the start error still shows as an alert. | Matches today's rules: Start was shown only when ≥2 units, available and nothing in progress. Not blocking on "pending" keeps the existing start, server-error and paywall tests unmodified. |
| D9 | Where is the start bar? | One always-rendered card at the end of the section (after the preview when there is one). It holds the selection summary, the reason caption under it, a start-error alert, and the primary button: full width below 700 px, at the inline-end from 700 px. The shortfall panel moves out of `MultiExamPreview` and becomes the reason. The «اختر وحدتين على الأقل.» paragraph becomes the reason. | "Always show the primary start button, disabled with the reason, next to the selection summary." Each reason text appears once on the page. |
| D10 | Percent sign | Keep «٪» (Arabic percent sign) and Latin digits. Add an `ar` test asserting Latin digits on these screens. | Latin digits already hold app-wide through ICU `ar-EG-u-nu-latn`. «٪» is the documented copy in 21 strings across 8 locale files and in `docs/claude-design-prompt.md` and `docs/browsing.md`. Changing it is an app-wide copy change outside these two screens. |
| D11 | Headline counter type | The sentence stays one i18n string (`headline.remaining`, the PRD copy «متبقّي لك X سؤال من Y», PRD line 239). It renders in `text-h1` below 900 px and `text-display-desktop` from 900 px, with `text-balance`, and rows `gap-3`. | Display at 1.05 line height wraps into cramped lines at 375. h1 has 1.2 line height, and at ≥ lg the sentence fits on one line. Splitting the sentence would break the PRD copy. Docs record the rule (both design-system files). |
| D12 | Headline stats | Replace the `headline.meta` sentence with a `<dl>` of 3 chips (label `dt`, value `dd`): seen, mastered, streak. Add a `MasteryBar` of mastered ÷ servable total (0 when the total is 0). | The story suggests chips and a progress bar. `dl` gives label/value semantics. |
| D13 | Subject section | `ul` becomes `flex flex-col gap-3`: one full-width card per subject. | Each card holds a 4-column table that needs the width. A half-width grid left empty space. |
| D14 | Unit name in the table | A plain link with the `UserListRow` link classes plus `font-semibold`. The row gets `hover:bg-soft` (Table spec). «امتحان الوحدة» stays a `sm` secondary `Button asChild`. | "Plain text or links, consistent with the other tables". |
| D15 | Weak-list cap | `weakListCap = 3`. A ghost button «عرض الكل (N)» / «عرض أقل» with `aria-expanded` and `aria-controls`. Lessons and objectives are capped independently. | The API returns up to 4 lessons and 3 objectives (`ProgressOptions`; admin-tunable), so 3 is where "show more" starts to matter. 3 matches the prototype's objectives list. |
| D16 | Weak-row structure | Shared `WeakSpotRow` and `WeakSpotList` in `progress/components`. `WeakLessonList` and `WeakObjectiveList` become thin mappers, so `WeakSpotsSection` is unchanged. | One row layout for both lists. |
| D17 | Subject select width | The wrapper is `flex flex-col gap-1.5 md:max-w-xs` (320 px from 700 px). | The same pattern as `progress/SessionKindFilter`. |
| D18 | Layout check | A story probe `.process/286-…/layout-probe.js` plus the existing `.process/276-…/layout-audit.js` (overflow and target checks), run in a real browser at 375/768/1280 × ar/en. The record goes in `02-layout-audit.md`. | Screenshots time out. jsdom cannot measure layout. This is the #276 precedent. |
| D19 | Budgets | No budget changes. Run `npm run build && npm run perf:budget` on base `ea2e23e5` and on the branch, and record both. Quiz, lesson, entry and landing may grow by at most 0.5 KB each. | Every new utility class lands in the global CSS. Most classes chosen above already exist. |
| D20 | Parallel lane #288 | Do not touch `features/shell/**`, `routes/**`, `routeTree.gen.ts`, `scripts/perf/budgets.json`, `features/avatar/**`. Possible shared files are listed under "Shared with #288". | Avoids merge conflicts. |

### Shared with #288 (possible two-lane edits)
| File | This lane edits | #288 likely edits | Rule |
|---|---|---|---|
| `docs/claude-design-prompt.md` | §2.4 Quiz option / Inputs / Progress paragraphs, §4 bullets for `#/student`, `#/student/multi-exam`, `#/student/progress`, §6 Forms and List bullets | §2.4 Assistant panel, §4 assistant bullet and a new assistant route bullet, maybe §6 | Edit only the named paragraphs. Do not reflow neighbouring lines. |
| `docs/prototype.md` | walkthrough item 6 only | walkthrough item 3 (assistant) | Same. |
| `docs/design-system.md`, `.claude/design-system.md` | version/header, §3.1 display row, §5.3, §5.6, §5.8, new §5.15; components table rows QuizOption, Progress, Input / Select / Textarea, new CompactList; change log row 2.2 | possibly a chat-page component row, version bump | If both bump the version, the later merge takes the next number (2.3). |
| `web/src/styles/app.css` | `@layer base` legend/fieldset rule | unlikely | Add the rule as its own block after the `:disabled` block. |
| Global CSS output (budgets) | small growth | new route classes | Whichever lane merges second re-runs `perf:budget` after rebase. |

## Existing code touched
| File | Change |
|------|--------|
| `web/src/styles/app.css` | In `@layer base`, after the `:disabled { … }` block, add exactly:<br>`fieldset { min-inline-size: 0; }`<br>`legend { float: inline-start; inline-size: 100%; }`<br>`legend + * { clear: both; }` (one declaration block per selector, formatted like the neighbours). |
| `web/src/features/questions/components/ChoiceAnswerInputs.tsx` | Import `optionCardClassName, optionCardIdleClassName` from `@/shared/ui/optionCard`. `stateClasses.none = optionCardIdleClassName`. The label's `className={cn(optionCardClassName, stateClasses[state])}`. The rendered class strings stay identical. Nothing else changes. |
| `web/src/features/configuration/components/ChoiceListSettingForm.tsx` | legend `mb-1 text-caption text-text-muted` → `text-caption text-text-muted` |
| `web/src/features/gradeReview/components/GradeReviewDecisionField.tsx` | legend: remove `mb-1` |
| `web/src/features/mathSteps/components/MathStepsInput.tsx` | legend: remove `mb-3` |
| `web/src/features/questions/components/ChoiceOptionsField.tsx` | legend: remove `mb-2` |
| `web/src/features/questions/components/DiagramImageField.tsx` | legend: remove `mb-2` |
| `web/src/features/questions/components/DiagramItemsField.tsx` | legend: remove `mb-2` |
| `web/src/features/questions/components/DiagramZonesField.tsx` | legend: remove `mb-2` |
| `web/src/features/questions/components/FillBlanksField.tsx` | legend: remove `mb-2` |
| `web/src/features/questions/components/MathAnswersField.tsx` | legend: remove `mb-2` |
| `web/src/features/questions/components/MathSolutionField.tsx` | legend: remove `mb-2` |
| `web/src/features/questions/components/ModelAnswersField.tsx` | legend: remove `mb-2` |
| `web/src/features/questions/components/NormalizationRulesField.tsx` | legend `mb-2` → `mb-1` |
| `web/src/features/questions/components/RubricCriteriaField.tsx` | legend: remove `mb-2` |
| `web/src/features/questions/components/RubricLevelsField.tsx` | legend: remove `mb-2` |
| `web/src/features/exam/components/MultiExamUnitPicker.tsx` | Rewrite per "Component contracts" C1. |
| `web/src/features/exam/components/MultiExamSizePicker.tsx` | Rewrite per C2. |
| `web/src/features/exam/components/MultiExamSubjectSelect.tsx` | Wrapper `flex flex-col gap-1.5` → `flex flex-col gap-1.5 md:max-w-xs`. |
| `web/src/features/exam/components/MultiExamSubjectSection.tsx` | Per C3. |
| `web/src/features/exam/components/MultiExamPreview.tsx` | Per C4. |
| `web/src/features/exam/i18n/en.json`, `ar.json` | See "Strings". |
| `web/src/features/mastery/components/HeadlineCounterCard.tsx` | Per C7. |
| `web/src/features/mastery/i18n/en.json`, `ar.json` | See "Strings". |
| `web/src/features/progress/components/SubjectProgressSection.tsx` | `<ul className="grid grid-cols-1 gap-3 lg:grid-cols-2">` → `<ul className="flex flex-col gap-3">`. |
| `web/src/features/progress/components/UnitProgressTable.tsx` | Per C8. |
| `web/src/features/progress/components/WeakLessonList.tsx` | Per C11. |
| `web/src/features/progress/components/WeakObjectiveList.tsx` | Per C12. |
| `web/src/features/progress/i18n/en.json`, `ar.json` | See "Strings". |
| `web/src/features/exam/pages/MultiExamBuilderPage.test.tsx` | Modify 3 tests and add 3 (Test plan). |
| `web/src/features/mastery/pages/StudentHomePage.test.tsx` | Modify 1 test and add 2. |
| `web/src/features/progress/pages/ProgressPage.test.tsx` | Modify 1 test and add 1. |
| `docs/claude-design-prompt.md`, `docs/prototype.md`, `docs/design-system.md`, `.claude/design-system.md` | See "Docs-sync". |

## Files to create
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `web/src/shared/ui/optionCard.ts` | class recipe | `export const optionCardClassName = 'flex min-h-12 cursor-pointer items-center gap-2.5 rounded-md border px-3.5 py-3 text-ui';`<br>`export const optionCardIdleClassName = 'border-border-strong bg-surface hover:bg-soft has-checked:border-accent has-checked:bg-accent-soft';`<br>`export const optionCardDisabledClassName = 'cursor-not-allowed border-border-strong bg-surface opacity-45';` |
| 2 | `web/src/features/exam/hooks/useMultiExamBlockReason.ts` | hook | See C5. |
| 3 | `web/src/features/exam/components/MultiExamStartBar.tsx` | component | See C6. |
| 4 | `web/src/features/progress/components/WeakSpotRow.tsx` | component | See C9. |
| 5 | `web/src/features/progress/components/WeakSpotList.tsx` | component | See C10. |
| 6 | `web/src/features/progress/components/WeakSpotList.test.tsx` | test | Test plan rows T13–T17. |
| 7 | `.process/286-student-progress-and-multi-unit-exam-ui-polish/layout-probe.js` | dev script (not shipped, not linted) | Verbatim from §Layout check. |
| 8 | `.process/286-student-progress-and-multi-unit-exam-ui-polish/02-layout-audit.md` | audit record (written by the implementer) | Format in §Layout check. |

### Component contracts
Use `cn` from `@/shared/lib/utils`. Use `Button` from `@/shared/ui/button`. Use `Link` from `@tanstack/react-router`. Named exports only. No `useMemo` / `useCallback`.

**C1 `MultiExamUnitPicker`.** Props unchanged (`units`, `selected`, `onToggle`).
```tsx
<fieldset className="flex min-w-0 flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">
  <legend className="text-ui font-semibold text-text">{t('multi.units')}</legend>
  <div className="grid grid-cols-1 gap-3 md:grid-cols-2 lg:grid-cols-3">
    {units.map((unit) => {                       // nameId = `${baseId}-${unit.unitId}-name`, captionId = `${baseId}-${unit.unitId}-caption`
      <label key={unit.unitId} className={cn(optionCardClassName, unit.hasBlueprint ? optionCardIdleClassName : optionCardDisabledClassName)}>
        <input type="checkbox" aria-labelledby={nameId} aria-describedby={captionId}
          className="size-4.5 shrink-0 accent-accent focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden"
          checked={selected.includes(unit.unitId)} disabled={!unit.hasBlueprint}
          onChange={(event) => { onToggle(unit.unitId, event.target.checked); }} />
        <span className="flex min-w-0 flex-col">
          <span id={nameId} className="font-semibold text-text">{unit.name}</span>
          <span id={captionId} className="text-caption text-text-muted">
            {unit.hasBlueprint ? t('multi.available', { count: Number(unit.servableCount) }) : t('multi.noBlueprint')}
          </span>
        </span>
      </label>
    })}
  </div>
</fieldset>
```
**C2 `MultiExamSizePicker`.** Props unchanged. The fieldset and legend are the same as C1 (legend `t('multi.size')`). The options container is `grid grid-cols-1 gap-3 md:grid-cols-3`. Each option is `<label key={size} className={cn(optionCardClassName, optionCardIdleClassName)}>` with `<input type="radio" name={name} className="size-4.5 shrink-0 accent-accent focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden" checked={size === value} onChange={() => { onChange(size); }} />` followed by `{t('multi.sizeOption', { size })}`. The name stays the label text.

**C3 `MultiExamSubjectSection`.**
- Delete the local `minimumUnits` constant and import it from `../hooks/useMultiExamBlockReason`.
- Delete the `{selectedIds.length < minimumUnits ? <p …chooseTwo/> : <MultiExamPreview … canStart/>}` block and replace it with:
```tsx
{selectedIds.length >= minimumUnits ? <MultiExamPreview subjectId={subjectId} unitIds={selectedIds} size={size} /> : null}
<MultiExamStartBar subjectId={subjectId} unitIds={selectedIds} size={size} inProgress={inProgress != null} />
```
- Everything else stays: the in-progress banner, both pickers, loading, error and empty states.

**C4 `MultiExamPreview`.**
- Props become `{ subjectId: string; unitIds: string[]; size: number }` (remove `canStart`).
- Remove `useStartMultiExam`, `PaywallDialog`, `paywallReason`, `Button`, the shortfall panel and the start block.
- Keep the loading skeleton, the error alert (same `errorTextKey`), the `h2`, `ExamBlueprintSummary` and `MultiExamUnitShares`.
- Keep the query options exactly `{ query: { placeholderData: keepPreviousData } }`.

**C5 `useMultiExamBlockReason.ts`**
```ts
import { keepPreviousData } from '@tanstack/react-query';
import { usePreviewMultiUnitExam } from '@/shared/api/generated/exams/exams';

// PRD §7.5: a multi-unit exam covers two or more units.
export const minimumUnits = 2;
export type MultiExamBlockReason = 'inProgress' | 'chooseTwo' | 'shortfall';

export function useMultiExamBlockReason(subjectId: string, unitIds: string[], size: number, inProgress: boolean): MultiExamBlockReason | null
```
Body, in order:
1. `const enoughUnits = unitIds.length >= minimumUnits;`
2. `const { data, isPlaceholderData } = usePreviewMultiUnitExam(subjectId, { unitIds, size }, { query: { enabled: enoughUnits, placeholderData: keepPreviousData } });`. This is the same key as `MultiExamPreview`, so the request is deduplicated.
3. `if (inProgress) return 'inProgress';`
4. `if (!enoughUnits) return 'chooseTwo';`
5. `if (data && !isPlaceholderData && !data.isAvailable) return 'shortfall';`
6. `return null;`

**C6 `MultiExamStartBar.tsx`.** `export interface MultiExamStartBarProps { subjectId: string; unitIds: string[]; size: number; inProgress: boolean }`.
```tsx
const reasonKeys = { inProgress: 'multi.reasonInProgress', chooseTwo: 'multi.chooseTwo', shortfall: 'start.shortfall' } as const satisfies Record<MultiExamBlockReason, string>;
// body
const { t } = useTranslation('exam');
const reasonId = useId();
const reason = useMultiExamBlockReason(subjectId, unitIds, size, inProgress);
const starter = useStartMultiExam();
return (
  <div className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1 md:flex-row md:items-center md:justify-between lg:p-5">
    <div className="flex min-w-0 flex-col gap-1">
      <p className="text-ui font-semibold text-text">{t('multi.summary', { units: unitIds.length, size })}</p>
      <p id={reasonId} aria-live="polite" className="text-caption text-text-muted">{reason ? t(reasonKeys[reason]) : null}</p>
      {starter.errorCode && paywallReason(starter.errorCode) === null ? (
        <p role="alert" className="text-caption text-danger">{t([`common:errors.${starter.errorCode}`, 'common:errors.UNHANDLED_EXCEPTION'])}</p>
      ) : null}
    </div>
    <Button variant="primary" className="w-full md:w-auto" disabled={reason !== null || starter.isPending}
      aria-describedby={reason ? reasonId : undefined}
      onClick={() => { starter.start(subjectId, unitIds, size); }}>
      {t('start.start')}
    </Button>
    <PaywallDialog reason={paywallReason(starter.errorCode)} onClose={starter.reset} />
  </div>
);
```
`PaywallDialog` and `paywallReason` come from `@/features/subscription`. (In C1 the name span inherits `text-ui` from `optionCardClassName`.)

**C7 `HeadlineCounterCard`.** Props unchanged. Import `MasteryBar` from `./MasteryBar`.
```tsx
const total = Number(headline.servableTotal);
const mastered = Number(headline.masteredCount);
const masteredPercent = total > 0 ? Math.round((mastered * 100) / total) : 0;
const stats = [
  { key: 'seen', label: t('headline.seen'), value: t('headline.count', { value: Number(headline.seenCount) }) },
  { key: 'mastered', label: t('headline.mastered'), value: t('headline.count', { value: mastered }) },
  { key: 'streak', label: t('headline.streak'), value: t('headline.streakValue', { streak: streakDays }) },
];
<section aria-label={t('headline.label')} className="flex flex-col gap-3 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">
  <p className="font-display text-h1 font-bold text-balance lg:text-display-desktop">
    {t('headline.remaining', { remaining: Number(headline.remainingCount), total })}
  </p>
  <MasteryBar percent={masteredPercent} label={t('headline.barLabel')} />
  <dl className="flex flex-wrap gap-2">
    {stats.map((stat) => (
      <div key={stat.key} className="flex items-baseline gap-1.5 rounded-pill bg-soft px-3 py-1">
        <dt className="text-caption text-text-muted">{stat.label}</dt>
        <dd className="text-caption font-semibold text-text">{stat.value}</dd>
      </div>
    ))}
  </dl>
</section>
```
**C8 `UnitProgressTable`.**
- Each body row's `className` becomes `"border-t border-border hover:bg-soft"`.
- Replace the first cell's `<Button asChild variant="secondary" size="sm"><Link …>{unit.name}</Link></Button>` with:
```tsx
<Link to="/student/unit/$unitId" params={{ unitId: unit.unitId }}
  className="rounded-sm font-semibold text-accent hover:underline focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden">{unit.name}</Link>
```
- The exam cell is unchanged (`sm` secondary `Button asChild`).

**C9 `WeakSpotRow.tsx`.** `export interface WeakSpotRowProps { title: string; meta: string; percent: number; barLabel: string; lessonId: string }`.
```tsx
<li className="flex flex-col gap-2 py-3 md:flex-row md:items-center md:gap-4">
  <div className="flex min-w-0 flex-1 flex-col">
    <p className="text-ui font-semibold text-text">{title}</p>
    <p className="text-caption text-text-muted">{meta}</p>
  </div>
  <div className="flex items-center gap-3">
    <div className="flex flex-1 flex-col gap-1 md:w-28 md:flex-none">
      <p className="text-caption text-text-muted">{t('weakSpots.mastery', { percent })}</p>
      <MasteryBar percent={percent} label={barLabel} />
    </div>
    <Button asChild size="sm" variant="secondary">
      <Link to="/student/lesson/$lessonId/practice" params={{ lessonId }}>{t('weakSpots.train')}</Link>
    </Button>
  </div>
</li>
```
`MasteryBar` comes from `@/features/mastery`. Translations use the `progress` namespace.

**C10 `WeakSpotList.tsx`.**
- Types: `export interface WeakSpotItem extends WeakSpotRowProps { id: string }` and `export interface WeakSpotListProps { items: WeakSpotItem[] }`.
- `const weakListCap = 3;`
- Body:
  - `useTranslation('progress')`
  - `const listId = useId();`
  - `const [expanded, setExpanded] = useState(false);`
  - `const shown = expanded ? items : items.slice(0, weakListCap);`
```tsx
<div className="flex flex-col gap-2">
  <ul id={listId} className="flex flex-col divide-y divide-border rounded-lg border border-border bg-surface px-4 shadow-1 lg:px-5">
    {shown.map(({ id, ...row }) => <WeakSpotRow key={id} {...row} />)}
  </ul>
  {items.length > weakListCap ? (
    <Button variant="ghost" className="self-start" aria-expanded={expanded} aria-controls={listId}
      onClick={() => { setExpanded(!expanded); }}>
      {expanded ? t('weakSpots.showLess') : t('weakSpots.showAll', { count: items.length })}
    </Button>
  ) : null}
</div>
```
**C11 `WeakLessonList`.** Props unchanged. It returns `<WeakSpotList items={lessons.map((lesson) => ({ id: lesson.lessonId, title: lesson.lessonName, meta: lesson.subjectName, percent: Number(lesson.masteryPercent), barLabel: t('weakSpots.barLabel', { name: lesson.lessonName }), lessonId: lesson.lessonId }))} />`. Drop the `MasteryBar`, `Button` and `Link` imports.

**C12 `WeakObjectiveList`.** Props unchanged. It returns `<WeakSpotList items={objectives.map((objective) => ({ id: objective.objectiveId, title: objective.text, meta: t('weakSpots.objectiveLesson', { lesson: objective.lessonName, subject: objective.subjectName }), percent: Number(objective.masteryPercent), barLabel: t('weakSpots.barLabel', { name: objective.text }), lessonId: objective.lessonId }))} />`.

### Strings (both files always; ICU)
| File | Key | en | ar |
|---|---|---|---|
| exam | `multi.available` (change) | `{count, plural, one {# question available} other {# questions available}}` | `{count, number} سؤال متاح` |
| exam | `multi.summary` (new) | `{units, plural, =0 {No units chosen} one {# unit} other {# units}} · {size, number} questions` | `{units, plural, zero {لم تختر وحدات بعد} one {وحدة واحدة} two {وحدتان} few {# وحدات} many {# وحدة} other {# وحدة}} · {size, number} سؤال` |
| exam | `multi.reasonInProgress` (new) | `Finish the exam in progress first.` | `أنهِ الامتحان الجاري أولًا.` |
| mastery | `headline.meta` | delete | delete |
| mastery | `headline.barLabel` (new) | `Questions mastered` | `الأسئلة التي أتقنتها` |
| mastery | `headline.seen` (new) | `Seen` | `شاهدت` |
| mastery | `headline.mastered` (new) | `Mastered` | `أتقنت` |
| mastery | `headline.streak` (new) | `Streak` | `سلسلة الأيام` |
| mastery | `headline.count` (new) | `{value, number}` | `{value, number}` |
| mastery | `headline.streakValue` (new) | `{streak, plural, one {# day} other {# days}}` | `{streak, plural, zero {# يوم} one {يوم واحد} two {يومان} few {# أيام} many {# يومًا} other {# يوم}}` |
| progress | `weakSpots.showAll` (new) | `Show all ({count, number})` | `عرض الكل ({count, number})` |
| progress | `weakSpots.showLess` (new) | `Show less` | `عرض أقل` |

## Error codes
None. No API change. Existing `common:errors.*` are reused for the start error.

## Domain behaviour
None (web-only).

## API surface
None changed. Existing endpoints used: `GET /api/exams/subjects/{subjectId}/multi-unit`, `GET …/multi-unit/preview` (now also observed by `useMultiExamBlockReason`, same query key), `POST …/multi-unit`, `GET /api/mastery/overview`, `GET /api/progress/subjects`, `GET /api/progress/weak-spots`.

## Test plan
Conventions:
- Use `renderApp` with MSW and `userEvent.setup()`.
- Use role queries.
- Rows marked **modify** may change only the lines stated. No other existing test is touched.
- The legend and layout claims are verified by the layout probe, because jsdom has no layout.

| # | Test class (file) | Test method | Asserts |
|---|-----------|-------------|---------|
| T1 | `MultiExamBuilderPage` (`exam/pages/MultiExamBuilderPage.test.tsx`) | **modify** `shows the units of the first subject after loading` | Line 63 becomes `expect(screen.getByRole('checkbox', { name: 'Mechanics' })).toHaveAccessibleDescription('12 questions available')`. |
| T2 | same | **modify** `shows the shortfall and hides Start when questions are short` → rename `shows the shortfall as the reason Start is disabled` | After `tickBoth`: `await waitFor(() => expect(start).toBeDisabled())` where `start = screen.getByRole('button', { name: 'Start exam' })`; `expect(start).toHaveAccessibleDescription('The exam cannot be created: not enough questions are available.')`. |
| T3 | same | **modify** `warns about an exam in progress and links to it` | Replace the last line (`queryByRole … toBeNull`) with: Start is disabled and `toHaveAccessibleDescription('Finish the exam in progress first.')`. |
| T4 | same | **add** `keeps Start visible and disabled with its reason until two units are chosen` | On load: Start is disabled with description `Choose at least two units.`, and the text `No units chosen · 20 questions`. After clicking Mechanics: `1 unit · 20 questions` and still disabled. After Waves: `2 units · 20 questions`, `await waitFor` enabled, and `not.toHaveAccessibleDescription()`. |
| T5 | same | **add** `checks the chosen size card and updates the summary` | Uses the `previewForSize` handler. Radio `20 questions` is checked by default. Click `60 questions` → it is checked, `20 questions` is not, and the text `No units chosen · 60 questions` shows. |
| T6 | same | **add** `shows the Arabic summary and reason with Latin digits` | `openBuilder('', 'ar')`. Start `ابدأ الامتحان` is disabled with description `اختر وحدتين على الأقل.`. The text `لم تختر وحدات بعد · 20 سؤال` shows. The unit checkbox `Mechanics` has description `12 سؤال متاح`. |
| T7 | `StudentHomePage` (`mastery/pages/StudentHomePage.test.tsx`) | **modify** `shows a loading state then the headline counter with seen, mastered and streak` | Line 24 becomes: `const counter = screen.getByRole('region', { name: 'Your question counter' })`. The `getAllByRole('term')` texts in it are `['Seen','Mastered','Streak']`. The `getAllByRole('definition')` texts are `['30','20','3 days']`. |
| T8 | same | **add** `shows the share of mastered questions as a bar` | `progressbar` named `Questions mastered` has `value` `33`. |
| T9 | same | **add** `shows an empty mastered bar when no question is servable yet` | With `headline: { servableTotal: 0, masteredCount: 0, remainingCount: 0, seenCount: 0 }`, the progressbar `value` is `0`. |
| T10 | `ProgressPage` (`progress/pages/ProgressPage.test.tsx`) | **modify** `shows loading then the headline summary` | Line 59 becomes the same region, term and definition assertions as T7. |
| T11 | same | **add** `shows the headline stats with Latin digits in Arabic` | `openProgress(undefined, 'ar')`. The region `عدّاد أسئلتك` `toHaveTextContent('متبقّي لك 40 سؤال من 60')`. The definitions are `['30','20','3 أيام']`. The text `إتقان 20٪` shows in the weak lesson row. |
| T12 | `UnitProgressTable` (`progress/components/UnitProgressTable.test.tsx`) | none (existing `links each unit name to its unit page` covers the link) | — |
| T13 | `WeakSpotList` (`progress/components/WeakSpotList.test.tsx`, new; handlers as in `UnitProgressTable.test.tsx`) | `shows the first three weak lessons and a Show all button when there are more` | 5 lessons (ids `aaaaaaaa-aaaa-4aaa-8aaa-00000000000{1..5}`, names `Lesson 1..5`) and `objectives: []`. There are 3 `link`s named `Train now`. `Lesson 4` is absent. The button `Show all (5)` has `aria-expanded="false"`. |
| T14 | same | `shows every weak lesson after Show all and the first three again after Show less` | Click `Show all (5)` → 5 `Train now` links and button `Show less` with `aria-expanded="true"`. Click it → 3 links. |
| T15 | same | `shows no Show all button when there are three or fewer` | With the default `weakSpots()`, `queryByRole('button', { name: /Show all/ })` is null. |
| T16 | same | `caps weak objectives at three as well` | 4 objectives and `lessons: []` → 3 `Train now` links and the button `Show all (4)`. |
| T17 | same | `puts the name, meta, mastery and Train now in one row` | Default fixture. In the `listitem` containing `Ohm's law` (lesson): text `Physics`, text `Mastery 20%`, a `progressbar` named `Ohm's law mastery` with value `20`, and link `Train now` → `/student/lesson/{weakLessonId}/practice`. |
| T18 | quiz/question suites (existing) | unchanged | The `ChoiceAnswerInputs` refactor is covered by existing quiz tests staying green. |

Existing tests that must stay green unmodified include `asks for two units when fewer are selected`, `starts the exam and opens the exam screen`, `shows the server error when the start is refused`, `MultiExamBuilderPage free tier`, the axe tests, and every `ProgressPage*` and `UnitProgressTable` test not listed above.

## Layout check (DOM measurement, no screenshots)
**Harness.** The same as #276 (`.process/276-…/02-layout-audit.md` §1):
- `npm run build` then `vite preview`, with `API_PROXY_TARGET` pointing at the demo stack.
- Headless Chrome over CDP, or the browser tool's eval, with `Emulation.setDeviceMetricsOverride` width 375 / 768 / 1280, height 900, and `mobile` below 900.
- Inject `.process/276-indigo-calm-theme-and-header-alignment-fixes/layout-audit.js`, unchanged, then `layout-probe.js`.
- Call `await __probe286.run()`.
- Switch language through the app's i18next instance as in #276.

**Routes:**
- Student: `/student`, `/student/progress`, `/student/multi-exam`, and `/student/multi-exam?subjectId=<id>&unitIds=<two ids>` (taken with `__layoutAudit.links`).
- Admin (legends): `/admin/question/new/<lessonId>`, `/admin/configuration`.
- Onboarding (legend) if the demo student can reach it.
- Record unreachable fieldset screens (teacher grade review, ask-teacher reply, admin teacher subjects) as "code-audited only".

**`layout-probe.js` (create verbatim):**
```js
/* Layout probe (#286). Inject after .process/276-indigo-calm-theme-and-header-alignment-fixes/layout-audit.js.
   await __probe286.run() -> findings ([] = clean). Includes __layoutAudit overflow + targets checks. */
(() => {
  const TOL = 1;
  const css = (el) => getComputedStyle(el);
  const rect = (el) => el.getBoundingClientRect();
  const visible = (el) => { const r = rect(el); return r.width > 0 && r.height > 0 && !el.closest('.sr-only'); };
  const on = (suffix) => location.pathname.endsWith(suffix);
  const f = (check, detail) => ({ check, route: location.pathname + location.search, width: innerWidth, dir: document.documentElement.dir, detail });
  const cols = (el) => css(el).gridTemplateColumns.split(' ').filter(Boolean).length;
  const overlapY = (a, b) => rect(a).top < rect(b).bottom && rect(a).bottom > rect(b).top;
  const checks = {
    legends() {
      return [...document.querySelectorAll('fieldset > legend')].filter(visible).flatMap((legend) => {
        const fs = legend.parentElement, s = css(fs);
        const top = rect(fs).top + parseFloat(s.borderTopWidth) + parseFloat(s.paddingTop);
        return rect(legend).top < top - TOL ? [f('legend-on-border', `${legend.textContent.trim()}: ${rect(legend).top.toFixed(1)} < ${top.toFixed(1)}`)] : [];
      });
    },
    optionCards() {
      if (!on('/multi-exam')) return [];
      const boxes = [...document.querySelectorAll('main fieldset input[type="checkbox"]')];
      const grid = boxes[0]?.closest('label')?.parentElement;
      if (!grid) return [f('option-grid', 'no unit option cards')];
      const out = [], want = innerWidth >= 900 ? Math.min(3, boxes.length) : innerWidth >= 700 ? Math.min(2, boxes.length) : 1;
      if (cols(grid) !== want) out.push(f('option-grid', `${cols(grid)} columns, expected ${want}`));
      for (const box of document.querySelectorAll('main fieldset input[type="checkbox"], main fieldset input[type="radio"]')) {
        const card = box.closest('label');
        if (!card || rect(card).height < 48 - TOL) out.push(f('option-height', `${card?.textContent.trim()}: ${Math.round(card ? rect(card).height : 0)}px`));
        const caption = document.getElementById(box.getAttribute('aria-describedby') ?? '');
        if (box.type === 'checkbox' && !(caption && card?.contains(caption) && visible(caption))) out.push(f('option-caption', `${card?.textContent.trim()}: count not on its card`));
      }
      return out;
    },
    startButton() {
      if (!on('/multi-exam')) return [];
      const start = [...document.querySelectorAll('main button')].find((b) => /ابدأ الامتحان|Start exam/.test(b.textContent));
      if (!start || !visible(start)) return [f('start-visible', 'start button missing or hidden')];
      const out = [];
      if (rect(start).height < 44 - TOL) out.push(f('start-height', `${Math.round(rect(start).height)}px`));
      if (start.disabled) {
        const reason = document.getElementById(start.getAttribute('aria-describedby') ?? '');
        if (!reason || !visible(reason) || !reason.textContent.trim()) out.push(f('start-reason', 'disabled without a visible reason'));
        else if (innerWidth >= 700 && !overlapY(start, reason.parentElement)) out.push(f('start-beside-summary', 'button not beside the summary'));
      }
      return out;
    },
    subjectSelect() {
      if (!on('/multi-exam')) return [];
      const select = document.querySelector('main select');
      if (!select) return [];
      const w = rect(select).width;
      return innerWidth >= 700 && w > 320 + TOL ? [f('select-width', `${Math.round(w)}px, max 320 from 700px`)] : [];
    },
    subjectCards() {
      if (!on('/progress')) return [];
      return [...document.querySelectorAll('main article')].flatMap((card) => {
        const list = card.closest('ul');
        return list && Math.abs(rect(card).width - rect(list).width) > TOL ? [f('subject-width', `${Math.round(rect(card).width)} of ${Math.round(rect(list).width)}px`)] : [];
      });
    },
    unitLinks() {
      return [...document.querySelectorAll('main table a[href^="/student/unit/"]')].flatMap((a) =>
        parseFloat(css(a).borderTopWidth) > 0 || css(a).display !== 'inline' ? [f('unit-link-pill', `${a.textContent.trim()}: border ${css(a).borderTopWidth}, display ${css(a).display}`)] : []);
    },
    headline() {
      const card = [...document.querySelectorAll('main section[aria-label]')].find((s) => s.querySelector('dl') && s.querySelector('progress'));
      if (!on('/student') && !on('/progress')) return [];
      if (!card) return [f('headline', 'no headline card with stats and bar')];
      const out = [], p = card.firstElementChild, next = p.nextElementSibling;
      const lines = Math.round(rect(p).height / parseFloat(css(p).lineHeight));
      if (lines > (innerWidth >= 900 ? 1 : 2)) out.push(f('headline-lines', `${lines} lines`));
      if (rect(next).top - rect(p).bottom < 12 - TOL) out.push(f('headline-gap', `${(rect(next).top - rect(p).bottom).toFixed(1)}px`));
      if (card.querySelectorAll('dl dt').length !== 3) out.push(f('headline-stats', 'expected 3 stat chips'));
      return out;
    },
    weakRows() {
      if (!on('/progress')) return [];
      const out = [];
      for (const li of document.querySelectorAll('main li')) {
        const link = li.querySelector('a[href$="/practice"]');
        const title = li.querySelector('p');
        if (!link || !title) continue;
        if (innerWidth >= 700 && !overlapY(link, title.parentElement)) out.push(f('weak-row', `${title.textContent.trim()}: action not on the title row`));
        if (innerWidth < 700 && rect(li).height > 120 + TOL) out.push(f('weak-row-height', `${title.textContent.trim()}: ${Math.round(rect(li).height)}px`));
      }
      for (const ul of document.querySelectorAll('main ul[id]')) {
        const more = document.querySelector(`button[aria-controls="${ul.id}"]`);
        if (more && more.getAttribute('aria-expanded') === 'false' && ul.children.length > 3) out.push(f('weak-cap', `${ul.children.length} rows before Show all`));
      }
      return out;
    },
  };
  const audit = () => [
    ...Object.values(checks).flatMap((c) => c()),
    ...(window.__layoutAudit ? ['overflow', 'targets', 'tables'].flatMap((k) => window.__layoutAudit.checks[k]().map((x) => ({ route: location.pathname, width: innerWidth, dir: document.documentElement.dir, ...x }))) : [f('setup', '__layoutAudit not loaded')]),
  ];
  const run = async ({ routes = [location.pathname + location.search], lang } = {}) => {
    if (lang && window.__layoutAudit) await window.__layoutAudit.setLang(lang);
    const all = [];
    for (const path of routes) { if (window.__layoutAudit) await window.__layoutAudit.go(path); all.push(...audit()); }
    return all;
  };
  window.__probe286 = { run, audit, checks };
})();
```
**`02-layout-audit.md` format** (the same as #276):
1. Environment: commit, how it was served, driver, accounts (no credentials).
2. Matrix: `role · width · lang · routes · findings before (base ea2e23e5) · findings after`.
3. Findings: `# · route · width · dir · check · target · detail · fix (file:line) · status`. Status is fixed, or intended with a reason. D2 spacing changes are listed as intended, with before/after px for every visible-legend fieldset that was reachable.
4. `perf:budget` output for base and branch: every page, KB used / max.

## Docs-sync
| File | Edit |
|---|---|
| `docs/claude-design-prompt.md` §2.4 **Quiz option.** | Append: «The same option card is used for choice groups outside the quiz (multi-unit exam units and sizes); a disabled option has 45 percent opacity and no hover fill, and a caption line may sit under the label.» |
| same §2.4 **Progress.** | Replace «The headline counter is a white card, number in Display size, meta line in caption.» with «The headline counter is a white card with 12 px between rows: the counter sentence in H1 size below 900 px and Display from 900 px, a 6 px mastered-share bar, then three stat chips (seen, mastered, day streak): `--soft` pills with a caption label in `--text-2` and a 600 value.» |
| same §2.4 **Inputs.** | Append: «A group of choices is a fieldset whose legend is an ordinary label inside the group (15 px 600 in a card, 13 px `--text-2` in a form), never drawn on the group's border.» |
| same §4 `#/student` bullet | Replace «("متبقّي لك X سؤال من Y", seen count, streak)» with «("متبقّي لك X سؤال من Y", a mastered-share bar and stat chips «شاهدت», «أتقنت», «سلسلة الأيام»)». |
| same §4 `#/student/multi-exam` bullet | Replace the whole bullet with: «- `#/student/multi-exam` a subject select (at most 320 px wide from 700 px); «اختر وحدتين أو أكثر:» unit option cards in a 1 / 2 / 3-column grid, each with the unit name and «N سؤال متاح» (or «لا يوجد امتحان لهذه الوحدة», disabled); «عدد الأسئلة» size option cards 20 / 40 / 60 (PRD §7.5); with two or more units, the live merged preview (blueprint table, time, pass mark, questions per unit); then an always-visible start card: the selection summary («وحدتان · 20 سؤال») and the primary «ابدأ الامتحان», disabled with its reason under the summary until the exam can start («اختر وحدتين على الأقل.», «أنهِ الامتحان الجاري أولًا.», or «لا يمكن إنشاء الامتحان: عدد الأسئلة المتاحة غير كافٍ.»). An exam in progress also shows «لديك امتحان جارٍ.» with «استكمل الامتحان».» |
| same §4 `#/student/progress` bullet | Replace the text up to «session history» with: «the headline counter card (as on Home); «المواد»: one full-width card per subject (name linking to the subject page, mastery bar, «إتقان X٪») with a unit table (unit name as a plain link, mastery, best unit-exam score, «امتحان الوحدة» as a 36 px row action); «نقاط الضعف»: «أضعف الدروس» and «أضعف الأهداف» as compact rows in one card each (name, subject or «lesson · subject», «إتقان X٪» over a small bar, «درّب الآن»), the first 3 rows then «عرض الكل (N)» / «عرض أقل»;». Keep the rest of the bullet, from «session history» on, unchanged. |
| same §6 | **Forms** bullet append: «Choice groups use option cards in a grid; a primary action that cannot run yet stays visible and disabled, with its reason in a caption beside it.» **List screens** bullet append: «A short ranked list inside a page section (Progress weak spots) is one white card with hairline-separated rows (12 px vertical padding), showing the first 3 with «عرض الكل (N)».» |
| `docs/prototype.md` walkthrough 6 | Append: « (Product: units and sizes are option cards, each unit card shows its available questions, and «ابدأ الامتحان» is always visible next to the selection summary, disabled with its reason until the exam can start.)» |
| `docs/design-system.md` | Header: Version `2.2`, Date `2026-10-04`. Decision: append « #286: legends inside their group, option cards for choice groups, headline stat chips, compact ranked lists.» §3.1 `display` Use: «Headline counter (from 900 px; `h1` below), landing hero». §5.3: append the option-card sentence from the design-prompt row above. §5.6: replace «Headline counter card is a white card with the number in `display` size.» with the headline sentence from the design-prompt row above. §5.8: append the legend sentence. New `### 5.15 Compact list` with: «One white card, hairline between rows, 12 px vertical row padding. Each row: title (`ui` 600) and caption meta at inline-start; a caption value over a 6 px bar (112 px wide from 700 px, full remaining width below); a 36 px `sm` secondary action at inline-end. Below 700 px the value, bar and action form a second line. The first 3 rows show, then a ghost «عرض الكل (N)» / «عرض أقل» with `aria-expanded`.» |
| `.claude/design-system.md` | Title `(v2.2)`. «Source of truth: `docs/design-system.md` v2.2 (2026-10-04)». Typography `type.display` Use: «headline counter (from lg; type.h1 below lg), landing hero». Components: **QuizOption** Notes append «; also the option card for choice groups (multi-exam units and sizes), disabled 45% opacity no hover, `shared/ui/optionCard.ts`». **Progress** Notes append «; headline counter card: sentence h1 below lg / display from lg with balanced wrap, mastered-share bar, 3 stat chips (soft pill, caption label text.muted, 600 value), rows gap 12». **Input / Select / Textarea** Notes append «; fieldset legend is a block label inside the group (global base rule floats it), never on the border». New row `CompactList` · — · — · «one Card, hairline rows (py 12), title ui 600 + caption meta at inline-start, caption value over a 6 px Progress (w 112 from md) and an `sm` secondary action at inline-end; second line below md; first 3 rows then ghost «عرض الكل (N)» / «عرض أقل» (aria-expanded)». Change log row `2.2 · 2026-10-04 · #286: legend-in-group rule, option cards for choice groups, headline counter chips and bar, CompactList`. Token values are unchanged. `npm --prefix web run gen:tokens` must leave `src/styles/tokens.css` unchanged. |

PRD copy («متبقّي لك X سؤال من …», `docs/PRD.md` line 239) is unchanged. `docs/mastery.md` («plus seen, mastered and the day streak») still holds.

## Definition of done
- [ ] All 21 fieldsets are handled per D3. `app.css` has the three base rules. The legend `mb-*` edits are exactly as listed. The probe's `legends` check is clean on every reachable fieldset route at 375/768/1280 × ar/en.
- [ ] `shared/ui/optionCard.ts` exists. `ChoiceAnswerInputs` renders identical class strings, and the quiz tests are green.
- [ ] Multi-exam: unit option cards in a 1/2/3-column grid, each with the count on the card (as its `aria-describedby`). Disabled units have 45% opacity. Size option cards are in `grid-cols-1 md:grid-cols-3`. The subject select is ≤ 320 px from 700 px.
- [ ] «ابدأ الامتحان» is always rendered, next to `multi.summary`. It is disabled for `inProgress` / `chooseTwo` / `shortfall`, with the reason as visible caption and `aria-describedby`. The shortfall panel and the start block are gone from `MultiExamPreview`.
- [ ] Headline card: h1 below lg and display-desktop from lg, `gap-3`, mastered-share bar, 3 `dt`/`dd` chips. `headline.meta` is removed from both locales.
- [ ] Progress: the subject list is one column at full width. Unit names are plain links (no border, inline) and rows hover soft. The exam action is still `sm` secondary.
- [ ] Weak lessons and objectives render as `WeakSpotRow` in one card each, capped at 3, with «عرض الكل (N)» / «عرض أقل» (`aria-expanded`, `aria-controls`). On ≥ 700 px the action is on the title row.
- [ ] Every test T1–T17 exists with the exact names. Only T1, T2, T3, T7 and T10 modify existing tests. No other test is edited, skipped or deleted.
- [ ] `npx tsc -b --noEmit`, `npx eslint . --max-warnings=0`, `npx prettier --check .` and `npx vitest run --coverage` all exit 0. No file is over 200 lines and no component over 120.
- [ ] `npm run build && npm run perf:budget` exits 0. `budgets.json` is unchanged. Quiz, lesson, entry and landing each grow by ≤ 0.5 KB against base `ea2e23e5`. Both runs are recorded.
- [ ] `02-layout-audit.md` has the matrix (student and admin × 375/768/1280 × ar/en), zero unexplained findings, D2 spacing rows marked intended, and the probe file committed under `.process/286-…/`.
- [ ] Docs-sync edits are exactly as in the table. `gen:tokens` produces no diff. No file under `features/shell`, `features/avatar` or `routes` is touched, and none of `routeTree.gen.ts` or `scripts/perf/budgets.json`.
- [ ] No new dependency. No literal colour, px or arbitrary value. No `ml-`/`mr-`/`left`/`right` utilities. Every new string is in en and ar. Digits are Latin in ar (T6, T11).
