# Plan — Indigo calm theme and header/alignment fixes (#276, E19.S1)

## Goal
Every user (student, teacher, admin, anonymous) sees the Indigo calm palette instead of near-black: indigo primary buttons with white labels, soft-indigo pills for the active nav item and active tab, indigo-ink text, tinted borders and shadows, AA-verified status colours and a retuned indigo aurora. On desktop the header is one 56 px row (logo, nav, role, name, sign-out) inside the same `max-w-layout` container as the page; admins reach their less-used pages through a "More" menu in that row. Login, sign-up, accept-invite and onboarding get the same app bar with the logo and a centred auth column. A repeatable DOM audit, plus fixes made at shared-component level, removes the alignment defects (sticky bars hidden under the app bar or tab bar, misaligned icon buttons, mixed control heights, dialogs drawn under the header).

## Scope
**In:** web/ only (no api/, no ai/). Sub-tasks 1–5 of #276:
1. Token set in `.claude/design-system.md` and `docs/design-system.md`, regenerated `tokens.css`, `app.css` mapping, AA contrast table, plus a contrast unit test over the generated tokens.
2. Primary/accent buttons, pill tabs, nav, selected options, checkboxes, badges, FAB and role badge move from near-black to indigo.
3. Single-row desktop app bar with an admin "More" disclosure; app bar on auth, accept-invite, onboarding and landing; mobile tab bar: active pill, centring, safe-area padding.
4. DOM layout audit script plus a matrix run (3 roles + public × 375/768/1280 × ar/en), fixes F1–F12 (known from the code audit below) and any further findings, recorded in `02-layout-audit.md`.
5. Docs sync: `docs/claude-design-prompt.md`, `docs/prototype.md`. `perf:budget` and web tests stay green.

**Out:**
- `prototype/styles.css`: the prototype stays a grey wireframe. `docs/prototype.md` says so.
- Input/option border contrast (`border.strong` ≈ 1.4:1, the same as v1.0). WCAG 1.4.11 for field boundaries is not part of this story; recorded under Decisions D16.
- Renaming the design system: it stays "Glass". "Indigo calm" is its v2.0 palette, so the constitution, README and react-feature skill do not change.
- A language switcher UI (none exists). The audit switches language from the console.
- Raising any `budgets.json` limit.

**Deferred:** none.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| D1 | Story gives success `#16A34A` on `#DFF3E5`, but that pair is 2.84:1, which fails AA for text. | Two tiers. `color.success` = `#16A34A` for non-text only (borders, icons on surface, pass progress fill, diagram key stroke: 3.30:1 on surface, so it passes the 3:1 UI rule). New `color.success.text` = `#166534` for all success text and verdict icons on `success.soft` (6.14:1). | Keeps the dev's hex and meets the story's AA rule. |
| D2 | White on `#16A34A` is 3.3:1, so the filled ok badges fail. | All ok/bad badges become soft: ok = `bg-success-soft text-success-text`, bad = `bg-danger-soft text-danger`. No white text sits on success anywhere. | Matches the chosen theme mock (soft chip with status text). One rule for every badge. |
| D3 | Danger and warning retune. | danger `#C8233A` / soft `#FDECEE`. warning `#B45309` / soft `#FEF3E2`. | The only candidates that clear 4.5:1 as text on surface, bg and their own soft, and (danger) white on fill (5.59). |
| D4 | Accent hover and pressed. | hover `#4338CA`, pressed `#3730A3`. accent.soft `#EEF0FF` (lighter than soft `#E5E7FB`, as the story asks). | Indigo 700/800. White label 7.90 / 9.93. accent on accent.soft is 5.55. |
| D5 | What happens to the `accent` Button variant now that primary is indigo? | Keep the variant name with the same classes as `primary`. Callers do not change. | Avoids touching 6 callers. Docs say "Accent = Primary since 2.0". |
| D6 | Borders, overlay, shadows. | border `rgba(30,27,75,.08)`, border.strong `rgba(30,27,75,.18)`, overlay `rgba(30,27,75,.40)`. Shadow ink is text-tinted, the large blur is indigo-tinted (see token table). | The story says borders come from the text colour and shadows are tinted toward indigo. |
| D7 | Aurora retune. | `linear-gradient(135deg,#4338CA 0%,#6D28D9 55%,#A21CAF 100%)`. Hero and subscribe-header text changes from `text-surface/80` to `text-surface`. The hero CTA becomes `variant="secondary"` (white pill). | White is ≥6.32:1 on every stop. An indigo button on an indigo gradient would be invisible. |
| D8 | Desktop nav overflow for admin (10 destinations). | Fixed per-role split `topBarKeys`. Student shows all 5 and teacher all 4. Admin shows dashboard, content, questions and users inline, and the other 6 in a "More" disclosure button (`TopNavMore`) at the end of the nav. Wrapping is rejected. | Wrapping breaks the single-row requirement. Measuring at runtime (ResizeObserver) cannot be tested in jsdom and flickers. Worst-case width estimate: admin AR at 1200 px ≈ 871/992 px, student EN at 900 px ≈ 740/852 px. A disclosure ("overflow-menu", ui-ux-pro-max §9) keeps every page one click away. |
| D9 | Width at 900–1199 px. | Add `bp.xl ≥1200`. From lg up to xl the display name is hidden (`lg:max-xl:hidden`) and the sign-out label is `sr-only` (icon button, accessible name kept). From xl up, both show. The nav `<ul>` also gets `overflow-x-auto` as a safety net, so a translation that grows can never cause page overflow. | Without this, student EN with a long name does not fit at 900 px. |
| D10 | "More" menu weight vs the quiz budget (253/255 KB). | `TopNavMore` is `React.lazy`-loaded by `TopTabs` only when the role has overflow items. No new dependency: a hand-rolled disclosure (no Radix DropdownMenu). | Student and teacher chunks stay byte-for-byte the same apart from CSS. Radix menu ≈ 10 KB brotli. |
| D11 | Where the role badge goes. | It moves to the end cluster (before the name), restyled `bg-soft text-text`. | The story wants nav links directly after the logo. A soft-indigo badge would look like an active nav pill. |
| D12 | Active-state styling. | Nav item and More button: `bg-accent-soft text-accent font-semibold`, no border. Pill tabs, filters and segmented toggles: shared `pillTabClassName` keyed off `aria-pressed` / `aria-selected` / `aria-[current=page]` → `border-accent bg-accent-soft text-accent`. Secondary Button with `aria-pressed=true` gets the same. | One systematic rule instead of 9 hand-written ternaries. The accent border gives the active pill a ≥3:1 indicator (6.29). |
| D13 | Logo colour. | Wordmark in `text-accent` (it is a link home). | As in the chosen theme mock. Rule 1 says indigo means link. |
| D14 | Auth width. | New token `layout.auth.max` = 440 px → `max-w-auth`. The card column is centred both ways under the app bar. | A sensible single-column form width that also matches the 420 dialog. |
| D15 | Header height and offsets. | New token `layout.bar` = 56. The header is `h-14` including its hairline. Sticky exam header `top-16`. `scroll-padding-top` = bar + 8. Tab-bar offset utility `bottom-above-tab-bar`. | One number drives every offset. |
| D16 | Input border contrast. | Unchanged policy (decorative boundary). Recorded as "not a text pair" in the contrast table. | Changing it touches about 39 control class strings. That is out of this story. |
| D17 | How to audit without screenshots. | A console-injected DOM script (`layout-audit.js`) measures the checks and returns JSON, run per role × width × language. Fixes are made at shared level first. Any fix outside the named files is limited to className edits in `web/src/features/**` or `web/src/shared/**` and is listed with evidence in `02-layout-audit.md`. | The story requires "fix what's found", and those findings cannot all be named in advance. The plan names everything already found by code audit (F1–F12). |
| D18 | Layout tests in jsdom (no layout engine). | Cheap structural guards only. The header is one row holding logo, nav and sign-out. A contrast test runs over the generated tokens. No visual or browser-mode tests (`@vitest/browser-playwright` is not installed). | Cheap, and it fails if the two-row header or a failing token comes back. |
| D19 | Onboarding and landing headers. | Both use the same `BrandBar` as auth (logo → `/`). Landing keeps its sign-in button in the bar's end slot. | "Tidy, aligned header on every screen". It is cheap because the component is shared. |
| D20 | Morabh reuse. | None. Every piece is web/ UI. Morabh has no equivalent (it is a .NET repo). | dotnet-feature §5 applies to api/ only. |

## Existing code touched
| File | Change |
|------|--------|
| `.claude/design-system.md` | Token, component, rule, breakpoint, AA and mapping updates (see "Docs content" §A). Change log 2.0. |
| `docs/design-system.md` | v2.0, mirrors §A (see §B). |
| `docs/claude-design-prompt.md` | §2.1 colour block and rules, §2.3 shadows, §2.4 Buttons, Quiz option, Badges, Navigation, Assistant panel, Dialogs (see §C). |
| `docs/prototype.md` | One sentence under "Files" (see §D). |
| `web/index.html` | viewport `content="width=device-width, initial-scale=1, viewport-fit=cover"`. |
| `web/src/styles/tokens.css` | Regenerated by `npm run gen:tokens` only. Never hand-edited. |
| `web/src/styles/app.css` | In `@theme inline` add `--color-accent-hover: var(--ds-color-accent-hover); --color-accent-pressed: var(--ds-color-accent-pressed); --color-success-text: var(--ds-color-success-text); --container-auth: var(--ds-layout-auth-max);`. In `:root` set `--primary: var(--ds-color-accent);`. Add `@utility pb-safe-area { padding-block-end: env(safe-area-inset-bottom); }` and `@utility bottom-above-tab-bar { inset-block-end: calc(var(--ds-layout-bar) + var(--ds-space-2) + env(safe-area-inset-bottom)); }`. In `@layer base`, `html { scroll-padding-top: calc(var(--ds-layout-bar) + var(--ds-space-2)); }` (replaces `calc(var(--ds-space-12) * 2)`) and a new rule `th { text-align: start; }`. |
| `web/src/shared/ui/button.tsx` | primary and accent: `bg-accent text-surface hover:bg-accent-hover active:bg-accent-pressed`. secondary: add `active:bg-soft aria-pressed:border-accent aria-pressed:bg-accent-soft aria-pressed:text-accent`. New size `icon: 'size-11'`. Remove `hover:opacity-90`. |
| `web/src/shared/ui/dialog.tsx` | Overlay `fixed inset-0 z-40 bg-overlay`. Content gains `z-50`. |
| `web/src/features/shell/navConfig.ts` | `RoleNav.topBarKeys: readonly string[]`. Per role: student `['home','progress','multiExam','ask','subscription']`, teacher `['queue','gradeReviews','inbox','stats']`, admin `['dashboard','content','questions','users']`. New `topBarItems`, `topBarOverflowItems` (contracts below). |
| `web/src/features/shell/components/AppBar.tsx` | Single row (contract below). |
| `web/src/features/shell/components/TopTabs.tsx` | Inline nav inside the AppBar row plus a lazy `TopNavMore` (contract below). |
| `web/src/features/shell/components/AppShell.tsx` | main `className={cn(layoutContainerClassName, 'pt-6 pb-24 lg:pb-8')}`. Skip link `focus:z-50` (was `focus:z-20`). |
| `web/src/features/shell/components/TabBar.tsx` | nav `fixed inset-x-0 bottom-0 z-20 border-t border-border bg-surface pb-safe-area lg:hidden`. Link `group flex min-h-14 flex-col items-center justify-center gap-0.5 text-micro text-text-muted focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-hidden data-[status=active]:font-semibold data-[status=active]:text-accent`. The icon is wrapped in `<span aria-hidden className="inline-flex h-7 w-14 items-center justify-center rounded-pill group-data-[status=active]:bg-accent-soft">`. The label `<span>` gets `leading-none`. |
| `web/src/features/shell/pages/MorePage.tsx`, `PlaceholderPage.tsx` | Page root `gap-3` → `gap-4` (F6). |
| `web/src/features/shell/navConfig.test.ts`, `components/AppShell.test.tsx` | Add tests (Test plan). No existing test edited. |
| `web/src/features/session/components/AuthLayout.tsx` | BrandBar plus centred column (contract below). |
| `web/src/features/session/components/MethodSwitch.tsx` | `<Button variant=…>` → `<button type="button" aria-pressed={active} className={pillTabClassName}>`. Drop the `Button` import. |
| `web/src/features/session/pages/LoginPage.test.tsx`, `SignUpPage.test.tsx` | Add one test each. |
| `web/src/features/onboarding/pages/OnboardingPage.tsx` | `<><BrandBar /><main id="main" className={cn(layoutContainerClassName, 'flex flex-col gap-4 py-6')}>…</main></>` (drop `min-h-dvh`). |
| `web/src/features/onboarding/pages/OnboardingPage.test.tsx` | Add one test. |
| `web/src/features/landing/components/LandingTopBar.tsx` | `return <BrandBar><Button asChild variant="secondary" size="sm"><Link to="/login">{t('topBar.signIn')}</Link></Button></BrandBar>;` |
| `web/src/features/landing/components/LandingHero.tsx` | CTA `variant="secondary"`. `text-surface/80` → `text-surface`. |
| `web/src/features/landing/pages/LandingPage.tsx` | main `className={cn(layoutContainerClassName, 'flex flex-col gap-6 pt-6 pb-10')}`. |
| `web/src/features/subscription/components/SubscribeHeader.tsx` | `text-surface/80` → `text-surface`. |
| Pill tabs: `askTeacher/components/InboxFilterTabs.tsx`, `gradeReview/components/GradeReviewKindTabs.tsx`, `gradeReview/components/GradeReviewSubjectPicker.tsx`, `payments/components/PaymentLogTabs.tsx`, `users/components/StudentHistorySection.tsx`, `users/components/UserRoleTabs.tsx` | Delete the local `tabClassName`/`pillClassName` constant and the active/inactive ternary. Use `className={pillTabClassName}` and keep the existing `aria-pressed`/`aria-selected`. Drop `cn` if it becomes unused. |
| `browse/components/LessonTabs.tsx` | Remove `activeProps`/`inactiveProps`. `className={pillTabClassName}` (active comes from `aria-current="page"`, which TanStack Link sets with `activeOptions.exact`). |
| `dashboard/components/SuccessRateBreakdown.tsx` | Level buttons `className={cn(pillTabClassName, 'min-h-9 px-3.5')}`. Remove the ternary. |
| Badges: `askTeacher/components/ThreadStatusBadge.tsx`, `audit/components/OutcomeBadge.tsx`, `configuration/components/ConfigurationBadge.tsx`, `content/components/LessonStateBadge.tsx`, `payments/components/PaymentStatusBadge.tsx`, `questions/components/QuestionStatusBadge.tsx`, `subscription/components/PaymentHistoryTable.tsx`, `subscription/components/PlanCard.tsx`, `trainingExport/components/TrainingExportStatusBadge.tsx`, `users/components/UserStatusBadge.tsx` | `bg-success text-surface` → `bg-success-soft text-success-text`. `bg-danger text-surface` → `bg-danger-soft text-danger` (PlanCard: `bg-success … text-surface` → `bg-success-soft … text-success-text`). |
| `exam/components/ExamResultSummary.tsx` | `badgeClassName` drops `text-surface`. Passed: `bg-success-soft text-success-text`. Failed: `bg-danger-soft text-danger`. |
| Verdict icons: `questions/components/GradeResultPanel.tsx`, `quiz/components/EssayGradeOutcome.tsx`, `quiz/components/FeedbackPanel.tsx`, `quiz/components/MathStepGradeOutcome.tsx` | Correct verdict `color: 'text-success'` → `'text-success-text'`. |
| Checkbox/radio colour (15 files): `askTeacher/components/ReplyPanel.tsx`, `blueprints/components/DifficultyMixFields.tsx`, `configuration/components/BooleanSettingForm.tsx`, `configuration/components/ChoiceListSettingForm.tsx`, `content/components/FormulaInsertForm.tsx`, `exam/components/MultiExamSizePicker.tsx`, `exam/components/MultiExamUnitPicker.tsx`, `gradeReview/components/GradeReviewDecisionField.tsx`, `onboarding/components/SubjectInterestsForm.tsx`, `questions/components/CheckboxField.tsx`, `questions/components/ChoiceAnswerInputs.tsx`, `questions/components/ChoiceOptionRow.tsx` (2×), `questions/components/QuestionPreviewPanel.tsx`, `questions/components/ValidationQueueItem.tsx`, `users/components/TeacherSubjectsCell.tsx` | `accent-text` → `accent-accent` (16 occurrences). |
| Selected option: `questions/components/ChoiceAnswerInputs.tsx`, `onboarding/components/SubjectInterestsForm.tsx` | `has-checked:border-text has-checked:bg-soft` → `has-checked:border-accent has-checked:bg-accent-soft`. |
| `questions/components/DiagramItemChip.tsx` | selected `border-text bg-soft` → `border-accent bg-accent-soft`. |
| `questions/components/DiagramAnswerCanvas.tsx` | count pill `bg-text` → `bg-accent` (the `border-text` stroke stays). |
| `avatar/components/AvatarDock.tsx` | `bg-text` → `bg-accent hover:bg-accent-hover active:bg-accent-pressed`. |
| `avatar/components/AvatarPanel.tsx` | Overlay `z-40`, content `z-50` (F12). |
| `exam/components/ExamHeader.tsx` | `sticky top-0 z-10` → `sticky top-16 z-10` (F1). |
| `exam/components/ExamRunner.tsx` | `sticky bottom-0` → `sticky bottom-above-tab-bar` (keep `lg:static`) (F2). |
| Icon buttons beside fields: `questions/components/DiagramItemRow.tsx`, `FillBlanksField.tsx`, `MathAnswersField.tsx`, `MathSolutionField.tsx`, `ModelAnswersField.tsx`, `RubricLevelsField.tsx` | `size="sm" … className="mt-6"` → `size="icon" … className="mt-6.5"` (F4). |
| `configuration/components/NumberSettingForm.tsx`, `ChoiceSettingForm.tsx`, `BooleanSettingForm.tsx`, `ChoiceListSettingForm.tsx` | Save button: remove `size="sm"` (F5). |
| `blueprints/pages/ExamBlueprintsPage.tsx` | Page root `gap-3` → `gap-4` (F6). |

All paths above are under `web/src/features/` unless written in full, and all were verified to exist.

## Files to create
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `web/src/shared/ui/layout.ts` | class constants (new, no Morabh equivalent) | `export const layoutContainerClassName = 'mx-auto w-full max-w-layout px-4 lg:px-6';` · `export const appBarClassName = 'sticky top-0 z-20 h-14 border-b border-border bg-surface';` (h-14 = `layout.bar`, includes the hairline) · `export const appBarRowClassName = \`${layoutContainerClassName} flex h-full items-center gap-3\`;` · `export const logoClassName = 'shrink-0 rounded-sm font-display text-h3 font-bold text-accent focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden';` |
| 2 | `web/src/shared/ui/pillTab.ts` | class constant | `export const pillTabClassName = 'inline-flex min-h-11 items-center justify-center gap-2 rounded-pill border border-border-strong bg-surface px-4 text-ui font-semibold text-text transition-colors hover:bg-soft focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden disabled:opacity-45 aria-pressed:border-accent aria-pressed:bg-accent-soft aria-pressed:text-accent aria-selected:border-accent aria-selected:bg-accent-soft aria-selected:text-accent aria-[current=page]:border-accent aria-[current=page]:bg-accent-soft aria-[current=page]:text-accent';` |
| 3 | `web/src/shared/components/BrandBar.tsx` | component | `export interface BrandBarProps { children?: ReactNode }` · `export function BrandBar({ children }: BrandBarProps)`: `useTranslation()` and render `<header className={appBarClassName}><div className={appBarRowClassName}><Link to="/" className={logoClassName}>{t('common:app.name')}</Link>{children ? <div className="ms-auto flex items-center gap-2">{children}</div> : null}</div></header>`. |
| 4 | `web/src/features/shell/navStyles.ts` | class constant | `export const topNavItemClassName = 'inline-flex min-h-9 shrink-0 items-center gap-1.5 rounded-pill px-3 text-ui font-medium whitespace-nowrap text-text-muted transition-colors hover:bg-bg hover:text-text focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden data-[status=active]:bg-accent-soft data-[status=active]:font-semibold data-[status=active]:text-accent';` |
| 5 | `web/src/features/shell/components/TopNavMore.tsx` | component (named export, lazy-loaded) | `export interface TopNavMoreProps { items: readonly NavItem[] }` · `export function TopNavMore({ items }: TopNavMoreProps)`. State `open` (useState false). Refs `rootRef: HTMLDivElement`, `buttonRef: HTMLButtonElement`. `panelId = useId()`. `pathname = useLocation({ select: (l) => l.pathname })`. `active = items.some((i) => pathname === i.to \|\| pathname.startsWith(\`${i.to}/\`))`. `useEffect([open])`: when open, add `document` `pointerdown` (close if the target is not inside `rootRef`) and `keydown` (`Escape` → close, then `buttonRef.current?.focus()`). Remove both on cleanup. Render: `<div ref={rootRef} className="relative shrink-0"><button ref={buttonRef} type="button" aria-expanded={open} aria-controls={open ? panelId : undefined} data-status={active ? 'active' : undefined} className={topNavItemClassName} onClick={() => { setOpen(!open); }}>{t('nav.more')}<ChevronDown aria-hidden className="size-4" strokeWidth={navIconStrokeWidth} /></button>{open ? <ul id={panelId} className="absolute end-0 top-full z-30 mt-2 flex min-w-56 flex-col gap-0.5 rounded-md border border-border bg-surface p-1.5 shadow-2">{items.map(({ key, to, labelKey, icon: Icon }) => <li key={key}><Link to={to} onClick={() => { setOpen(false); }} className="flex min-h-11 items-center gap-3 rounded-sm px-3 text-ui text-text hover:bg-bg focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-hidden data-[status=active]:bg-accent-soft data-[status=active]:font-semibold data-[status=active]:text-accent"><Icon aria-hidden className="size-5 shrink-0" strokeWidth={navIconStrokeWidth} /><span>{t(labelKey)}</span></Link></li>)}</ul> : null}</div>`. Namespace `shell`. No new strings. |
| 6 | `web/src/features/shell/components/TopNavMore.test.tsx` | tests | Rows T13–T18. |
| 7 | `web/scripts/tokens/contrast.ts` | pure functions (node) | `export function relativeLuminance(hex: string): number` (accepts `#RRGGBB`, case-insensitive; WCAG 2.x sRGB formula, 0.03928 threshold) · `export function contrastRatio(a: string, b: string): number` = `(Lmax+0.05)/(Lmin+0.05)` · `export function readToken(css: string, name: string): string`: returns the value of `--ds-${name}: <value>;` from tokens.css, and throws `Error(\`token --ds-${name} not found\`)` when missing · `export function gradientStops(value: string): string[]`: every `#RRGGBB` in order. |
| 8 | `web/scripts/tokens/contrast.test.ts` | tests (`// @vitest-environment node`) | Rows T1–T6. Reads `resolve(import.meta.dirname, '../../src/styles/tokens.css')`. |
| 9 | `.process/276-indigo-calm-theme-and-header-alignment-fixes/layout-audit.js` | dev script (not shipped, not linted) | Exactly the code in "Alignment audit" §2. |
| 10 | `.process/276-indigo-calm-theme-and-header-alignment-fixes/02-layout-audit.md` | audit record (written by the implementer) | The format is in "Alignment audit" §4. |

### Component contracts (modified files)
**AppBar.tsx**
```tsx
<header className={appBarClassName}>
  <div className={appBarRowClassName}>
    <Link to={roleHome[role]} className={logoClassName}>{t('common:app.name')}</Link>
    <TopTabs role={role} />
    <span className="ms-auto shrink-0 rounded-pill bg-soft px-2.5 py-0.5 text-micro font-semibold text-text lg:ms-0">{t(`role.${role}`)}</span>
    <span className="max-w-28 min-w-0 truncate text-caption text-text-muted lg:max-xl:hidden xl:max-w-36">{session?.displayName}</span>
    <Button variant="secondary" size="sm" onClick={signOut} className="shrink-0">
      <LogOut aria-hidden className="size-4" strokeWidth={navIconStrokeWidth} />
      <span className="lg:max-xl:sr-only">{t('signOut')}</span>
    </Button>
  </div>
</header>
```
The `header` has exactly one child. Below lg, `TopTabs` is `hidden`, so the badge's `ms-auto` pushes the end cluster. From lg, the nav is `flex-1` and fills the gap.

**TopTabs.tsx**
```tsx
const TopNavMore = lazy(() => import('./TopNavMore').then((m) => ({ default: m.TopNavMore })));
export function TopTabs({ role }: TopTabsProps) {
  const nav = navByRole[role]; const overflow = topBarOverflowItems(role, nav);
  return (
    <nav aria-label={t('nav.main')} className="hidden min-w-0 flex-1 items-center gap-1 lg:flex">
      <ul className="flex min-w-0 items-center gap-1 overflow-x-auto p-1">
        {topBarItems(role, nav).map((item) => (
          <li key={item.key} className="shrink-0">
            <Link to={item.to} activeOptions={{ exact: item.to === roleHome[role] }} className={topNavItemClassName}>{t(item.labelKey)}</Link>
          </li>))}
      </ul>
      {overflow.length > 0 ? <Suspense fallback={null}><TopNavMore items={overflow} /></Suspense> : null}
    </nav>);
}
```
The `p-1` on the scrolling list keeps focus rings from being clipped. The More button sits outside the scroller, so its panel is never clipped.

**navConfig.ts**
- `export function topBarItems(role: Role, nav: RoleNav): readonly NavItem[]` → `visibleNavItems(role, nav).filter((i) => nav.topBarKeys.includes(i.key))` (in `items` order).
- `export function topBarOverflowItems(role: Role, nav: RoleNav): readonly NavItem[]` → `visibleNavItems(role, nav).filter((i) => !nav.topBarKeys.includes(i.key))`.

**AuthLayout.tsx** (props unchanged)
```tsx
<div className="flex min-h-dvh flex-col">
  <BrandBar />
  <main id="main" className="mx-auto flex w-full max-w-auth flex-1 flex-col justify-center gap-6 px-4 py-8">
    <h1 …unchanged…>{title}</h1>
    <section …unchanged card classes…>{children}</section>
    <p className="text-caption text-text-muted">{footer}</p>
  </main>
</div>
```

## Error codes
None. This story makes no api/ change.

## Domain behaviour
None. This is a UI-only story.

## API surface
None. `npm run gen:api` must produce no diff.

## Tokens: the full Indigo calm set (goes into `.claude/design-system.md` → `tokens.css`)
| Token | CSS var | Value |
|---|---|---|
| color.bg | --ds-color-bg | #F4F5FB |
| color.surface | --ds-color-surface | #FFFFFF |
| color.soft | --ds-color-soft | #E5E7FB |
| color.text | --ds-color-text | #1E1B4B |
| color.text.muted | --ds-color-text-muted | #5F5D7A |
| color.border | --ds-color-border | rgba(30,27,75,.08) |
| color.border.strong | --ds-color-border-strong | rgba(30,27,75,.18) |
| color.accent | --ds-color-accent | #4F46E5 |
| color.accent.hover | --ds-color-accent-hover | #4338CA |
| color.accent.pressed | --ds-color-accent-pressed | #3730A3 |
| color.accent.soft | --ds-color-accent-soft | #EEF0FF |
| color.success | --ds-color-success | #16A34A |
| color.success.text | --ds-color-success-text | #166534 |
| color.success.soft | --ds-color-success-soft | #DFF3E5 |
| color.danger | --ds-color-danger | #C8233A |
| color.danger.soft | --ds-color-danger-soft | #FDECEE |
| color.warning | --ds-color-warning | #B45309 |
| color.warning.soft | --ds-color-warning-soft | #FEF3E2 |
| color.v2 | --ds-color-v2 | #6A3FB5 |
| color.overlay | --ds-color-overlay | rgba(30,27,75,.40) |
| gradient.aurora | --ds-gradient-aurora | linear-gradient(135deg,#4338CA 0%,#6D28D9 55%,#A21CAF 100%) |
| shadow.1 | --ds-shadow-1 | 0 1px 2px rgba(30,27,75,.05), 0 8px 24px rgba(79,70,229,.08) |
| shadow.2 | --ds-shadow-2 | 0 4px 12px rgba(30,27,75,.08), 0 24px 48px rgba(79,70,229,.14) |
| layout.bar | --ds-layout-bar | 56 |
| layout.auth.max | --ds-layout-auth-max | 440 |
| bp.xl | --breakpoint-xl | ≥1200 |

### AA contrast (computed with the WCAG 2.x formula; T4–T6 re-verify these)
| Foreground | Background | Ratio | Need | Use |
|---|---|---|---|---|
| text #1E1B4B | surface | 15.99 | 4.5 | body |
| text | bg | 14.69 | 4.5 | page text |
| text | soft | 13.06 | 4.5 | pending badge, bubbles |
| text | accent.soft | 14.12 | 4.5 | — |
| text | success.soft / danger.soft / warning.soft | 13.77 / 14.01 / 14.57 | 4.5 | feedback panels |
| text.muted #5F5D7A | surface / bg / soft | 6.30 / 5.79 / 5.15 | 4.5 | captions, inactive nav |
| text.muted | accent.soft / success.soft / danger.soft / warning.soft | 5.56 / 5.43 / 5.52 / 5.74 | 4.5 | captions in panels |
| white | accent / accent.hover / accent.pressed | 6.29 / 7.90 / 9.93 | 4.5 | primary button states |
| white | danger | 5.59 | 4.5 | — |
| accent #4F46E5 | surface / bg / soft / accent.soft | 6.29 / 5.78 / 5.14 / 5.55 | 4.5 | links, active nav pill, chips, ghost |
| success.text #166534 | surface / success.soft | 7.13 / 6.14 | 4.5 | ok text, ok badge, verdict icon |
| danger #C8233A | surface / bg / danger.soft | 5.59 / 5.14 / 4.90 | 4.5 | error text, bad badge |
| warning #B45309 | surface / bg / warning.soft | 5.02 / 4.62 / 4.58 | 4.5 | partial, pending |
| v2 #6A3FB5 | surface | 7.02 | 4.5 | v2 badge |
| white | aurora stops #4338CA / #6D28D9 / #A21CAF | 7.90 / 7.10 / 6.32 | 4.5 | hero and subscribe header |
| accent (focus ring, active pill border) | surface / bg | 6.29 / 5.78 | 3 | UI |
| success #16A34A (non-text) | surface / bg | 3.30 / 3.03 | 3 | borders, icons, pass fill |
| success #16A34A | success.soft | 2.84 | — | **never** text or the sole indicator (D1) |
| border.strong | surface | ≈1.44 | — | decorative field boundary (D16) |

## Where colour is applied (component state → token)
| Component · state | Fill | Text/icon | Border |
|---|---|---|---|
| Button primary/accent · default / hover / pressed / disabled | accent / accent.hover / accent.pressed / accent at 45 % opacity | surface | — |
| Button secondary · default / hover / pressed / aria-pressed | surface / soft / soft / accent.soft | text / text / text / accent | border.strong / … / … / accent |
| Button danger · default / hover | surface / danger.soft | danger | danger |
| Button ghost · default / hover | transparent / accent.soft | accent | — |
| Focus ring (all) | — | — | 2 px accent, offset 2 |
| TopNav item · inactive / hover / active | — / bg / accent.soft | text.muted / text / accent 600 | — |
| More menu item · default / hover / active | surface / bg / accent.soft | text / text / accent 600 | panel: border, shadow.2 |
| TabBar · inactive / active | surface / icon pill accent.soft | text.muted / accent 600 | top hairline border |
| Pill tab, filter, segmented · inactive / hover / active | surface / soft / accent.soft | text / text / accent | border.strong / … / accent |
| Chips (accent) | accent.soft | accent | — |
| Badge ok / bad / pending / role / v2 / warning | success.soft / danger.soft / soft / soft / transparent / warning.soft | success.text / danger / text.muted / text / v2 / text | v2 outline; warning border |
| Quiz option · default / hover / selected / correct / wrong | surface / soft / accent.soft / success.soft / danger.soft | text | border.strong / … / accent / success / danger |
| Checkbox/radio | `accent-color: accent` | — | — |
| Progress · mastery / pass | track soft, fill accent / fill success | — | — |
| Input/select/textarea · default / focus / error | surface | text, placeholder text.muted | border.strong / accent ring / danger |
| Logo | — | accent | — |
| AssistantFab | accent (hover, pressed as primary) | surface | — |
| Dialog backdrop | overlay | — | — |
| Cards | surface + shadow.1 | — | border |

## Single-row header: structure and breakpoints
| Width | Row content (inline order; RTL mirrors) | Nav |
|---|---|---|
| < 900 (base, md) | logo · role badge (ms-auto) · name (max-w-28, truncates) · sign-out (icon + label) | bottom TabBar (3 + More) |
| 900–1199 (lg) | logo · nav (flex-1) · role badge · sign-out (icon only, label `sr-only`) | inline pills; admin: 4 + More menu |
| ≥ 1200 (xl) | logo · nav · role badge · name (max-w-36) · sign-out (icon + label) | same |

Every item is centred by the row's `items-center` in the 56 px bar. Every control in the bar is 36 px tall (nav pills `min-h-9`, sign-out `sm`). The row uses `layoutContainerClassName`, the same class as `<main>`, so the logo's start edge equals the page title's start edge at every width.

## Alignment audit
### 1. Route matrix
Widths 375, 768 and 1280, each in `ar` (RTL) and `en` (LTR). Set the width with the browser tool's resize (`preview_resize` / `resize_window`) and confirm `innerWidth` in the output.
| Role | Static routes | Discovered (open the list page, then `__layoutAudit.links(prefix)`, take the first link) |
|---|---|---|
| public | `/`, `/login`, `/signup`, `/accept-invite` | — |
| student | `/student`, `/student/progress`, `/student/multi-exam`, `/student/ask`, `/student/ask-new`, `/student/subscription`, `/student/more`, `/onboarding` | subject, unit, lesson (+`/objectives`, `/summary`, `/practice`), thread, quiz-result, exam-result. Quiz `/student/quiz/:id` and exam `/student/exam/:id` are reached by starting one practice quiz and one unit exam with the buttons, then `run()` without routes. |
| teacher | `/teacher`, `/teacher/grades`, `/teacher/inbox`, `/teacher/stats`, `/teacher/more` | `/teacher/q/:id`, `/teacher/thread/:id`, `/teacher/grade/:subjectId/:kind/:gradeId` |
| admin | `/admin`, `/admin/content`, `/admin/questions`, `/admin/blueprints`, `/admin/users`, `/admin/payments`, `/admin/audit`, `/admin/avatar-conversations`, `/admin/export`, `/admin/configuration`, `/admin/more` | `/admin/lesson/:id`, `/admin/question/:id`, `/admin/question/new/:lessonId`, `/admin/question/import/:lessonId`, `/admin/student/:id`, `/admin/avatar-conversation/:id` |

Run against the running demo (API plus `npm run dev` in this worktree with `API_PROXY_TARGET`). Sign in with the demo's accounts per role. If a role has none, the admin invites a teacher and a student signs up with the fake OTP code from the API console.

### 2. `layout-audit.js` (create verbatim)
```js
/* Layout audit (#276). Paste into the console of the running web app (or pass to preview_eval / javascript_tool).
   await __layoutAudit.run({ lang: 'ar', routes: ['/student', '/student/progress'] })  -> findings ([] = clean)
   __layoutAudit.links('/student/subject/') -> in-app hrefs on the current page. Navigation uses history.pushState,
   which TanStack Router's browser history patches, so the SPA routes without a reload. */
(() => {
  const TOL = 1;
  const css = (el) => getComputedStyle(el);
  const rect = (el) => el.getBoundingClientRect();
  const rtl = () => document.documentElement.dir === 'rtl';
  const start = (r) => (rtl() ? r.right : r.left);
  const contentStart = (el) => {
    const r = rect(el), s = css(el);
    return rtl() ? r.right - parseFloat(s.paddingRight) - parseFloat(s.borderRightWidth) : r.left + parseFloat(s.paddingLeft) + parseFloat(s.borderLeftWidth);
  };
  const visible = (el) => { const r = rect(el), s = css(el); return r.width > 0 && r.height > 0 && s.visibility !== 'hidden' && !el.closest('.sr-only'); };
  const ancestors = (el) => { const list = []; for (let p = el.parentElement; p && p !== document.documentElement; p = p.parentElement) list.push(p); return list; };
  const inFixed = (el) => [el, ...ancestors(el)].some((p) => css(p).position === 'fixed');
  const clipped = (el) => ancestors(el).some((p) => ['auto', 'scroll', 'hidden', 'clip'].includes(css(p).overflowX));
  const isFlexRow = (el) => css(el).display.endsWith('flex') && !css(el).flexDirection.startsWith('column');
  const isFlexCol = (el) => css(el).display.endsWith('flex') && css(el).flexDirection.startsWith('column');
  const name = (el) => `${el.tagName.toLowerCase()}${el.id ? `#${el.id}` : ''} "${(el.getAttribute('aria-label') ?? el.textContent ?? '').trim().replace(/\s+/g, ' ').slice(0, 40)}"`;
  const finding = (check, el, detail) => ({ check, target: el ? name(el) : '', detail });
  const align = (el) => {
    const a = css(el).textAlign, ltr = css(el).direction === 'ltr';
    if (a === 'start' || a === (ltr ? 'left' : 'right')) return 'start';
    if (a === 'end' || a === (ltr ? 'right' : 'left')) return 'end';
    return 'center';
  };
  const CONTROL = 'button, a[data-slot="button"], input:not([type="checkbox"]):not([type="radio"]):not([type="hidden"]), select';

  const checks = {
    overflow() {
      if (document.documentElement.scrollWidth <= innerWidth + TOL) return [];
      const hits = [];
      for (const el of document.body.querySelectorAll('*')) {
        if (!visible(el) || inFixed(el) || clipped(el) || hits.some((h) => h.contains(el))) continue;
        const r = rect(el);
        if (r.right > innerWidth + TOL || r.left < -TOL) hits.push(el);
      }
      return hits.map((el) => finding('overflow', el, `left ${Math.round(rect(el).left)} right ${Math.round(rect(el).right)} viewport ${innerWidth}`));
    },
    edges() {
      const main = document.querySelector('main');
      if (!main) return [finding('edges', null, 'no <main>')];
      const ref = contentStart(main), out = [];
      const row = document.querySelector('body header > div');
      if (row && Math.abs(rect(row).width - rect(main).width) <= TOL) {
        if (Math.abs(contentStart(row) - ref) > TOL) out.push(finding('edges', row, `header row content ${Math.round(contentStart(row))} vs main ${Math.round(ref)}`));
        const logo = row.querySelector('a');
        if (logo && Math.abs(start(rect(logo)) - ref) > TOL) out.push(finding('edges', logo, `logo ${Math.round(start(rect(logo)))} vs main ${Math.round(ref)}`));
      }
      const root = main.firstElementChild;
      const blocks = new Set([main.querySelector('h1'), root, ...(root ? [...root.children] : [])]);
      for (const el of blocks) {
        if (!el || !visible(el) || inFixed(el) || rect(el).width < 24) continue;
        if (Math.abs(start(rect(el)) - ref) > TOL) out.push(finding('edges', el, `starts ${Math.round(start(rect(el)) - ref)}px from the page content edge`));
      }
      return out;
    },
    rows() {
      const out = [];
      for (const el of document.body.querySelectorAll('*')) {
        if (!isFlexRow(el) || !visible(el)) continue;
        const controls = [...el.children]
          .flatMap((c) => (c.matches(CONTROL) ? [c] : isFlexCol(c) ? [...c.children].filter((g) => g.matches(CONTROL)) : []))
          .filter(visible);
        const lines = [];
        for (const c of controls) {
          const r = rect(c), line = lines.find((l) => r.top < l.bottom && r.bottom > l.top);
          if (line) { line.items.push(c); line.top = Math.min(line.top, r.top); line.bottom = Math.max(line.bottom, r.bottom); }
          else lines.push({ top: r.top, bottom: r.bottom, items: [c] });
        }
        for (const { items } of lines) {
          if (items.length < 2) continue;
          const hs = items.map((c) => Math.round(rect(c).height));
          const cs = items.map((c) => rect(c).top + rect(c).height / 2);
          if (Math.max(...hs) - Math.min(...hs) > TOL) out.push(finding('row-heights', el, `control heights ${hs.join('/')}`));
          else if (Math.max(...cs) - Math.min(...cs) > TOL) out.push(finding('row-centres', el, `centres differ by ${(Math.max(...cs) - Math.min(...cs)).toFixed(1)}px`));
        }
      }
      return out;
    },
    icons() {
      const out = [];
      for (const svg of document.querySelectorAll('svg.lucide')) {
        if (!visible(svg) || rect(svg).height > 32) continue;
        const parent = svg.parentElement;
        if (isFlexCol(parent) || (isFlexRow(parent) && css(parent).alignItems === 'center')) continue;
        const sibling = [...parent.children].find((c) => c !== svg && visible(c) && c.textContent.trim());
        const textEl = sibling ?? ([...parent.childNodes].some((n) => n.nodeType === 3 && n.textContent.trim()) ? parent : null);
        if (!textEl) continue;
        const ts = css(textEl), lh = parseFloat(ts.lineHeight) || parseFloat(ts.fontSize) * 1.5;
        const lineCentre = rect(textEl).top + parseFloat(ts.paddingTop) + parseFloat(ts.borderTopWidth) + lh / 2;
        const dy = rect(svg).top + rect(svg).height / 2 - lineCentre;
        if (Math.abs(dy) > 2) out.push(finding('icon-centre', parent, `icon ${dy.toFixed(1)}px from the first text line centre`));
      }
      return out;
    },
    tables() {
      const out = [];
      for (const table of document.querySelectorAll('table')) {
        if (!visible(table)) continue;
        if (!['auto', 'scroll'].includes(css(table.parentElement).overflowX)) out.push(finding('table-scroll', table, 'not inside an overflow-x-auto wrapper'));
        const head = table.querySelector('thead tr'), body = table.querySelector('tbody tr');
        if (!head || !body) continue;
        [...head.children].forEach((th, i) => {
          const td = body.children[i];
          if (!td || td.colSpan > 1) return;
          if (align(th) !== align(td)) out.push(finding('table-align', th, `header ${align(th)} vs cell ${align(td)}`));
          else if (align(th) === 'start' && Math.abs(contentStart(th) - contentStart(td)) > TOL) out.push(finding('table-align', th, `header text ${Math.round(contentStart(th) - contentStart(td))}px from cell text`));
        });
      }
      return out;
    },
    header() {
      const banner = document.querySelector('body header');
      if (!banner || !banner.firstElementChild) return [];
      const out = [], row = banner.firstElementChild, mid = rect(row).top + rect(row).height / 2;
      if (banner.children.length !== 1) out.push(finding('header-rows', banner, `${banner.children.length} rows`));
      if (rect(banner).height > 56 + TOL) out.push(finding('header-height', banner, `${Math.round(rect(banner).height)}px (single row is 56)`));
      for (const el of row.querySelectorAll(':scope > *, nav a, nav > div > button')) {
        if (!visible(el) || css(el).display === 'none') continue;
        const c = rect(el).top + rect(el).height / 2;
        if (Math.abs(c - mid) > TOL) out.push(finding('header-centre', el, `${(c - mid).toFixed(1)}px off the row centre`));
      }
      return out;
    },
    tabBar() {
      const nav = [...document.querySelectorAll('nav')].find((n) => css(n).position === 'fixed' && visible(n));
      if (!nav) return [];
      const out = [], links = [...nav.querySelectorAll('a')], hs = links.map((a) => Math.round(rect(a).height));
      if (Math.max(...hs) - Math.min(...hs) > TOL) out.push(finding('tabbar-heights', nav, hs.join('/')));
      for (const a of links) {
        const mid = rect(a).left + rect(a).width / 2;
        for (const part of a.querySelectorAll(':scope > span')) {
          const c = rect(part).left + rect(part).width / 2;
          if (Math.abs(c - mid) > TOL) out.push(finding('tabbar-centre', a, `${(c - mid).toFixed(1)}px off centre`));
        }
      }
      return out;
    },
    targets() {
      const out = [];
      for (const el of document.querySelectorAll('a, button, input:not([type="hidden"]), select, textarea')) {
        if (!visible(el) || (el.tagName === 'A' && css(el).display === 'inline') || (el.matches('input[type="checkbox"], input[type="radio"]') && el.closest('label'))) continue;
        const r = rect(el);
        if (r.width < 24 || r.height < 24) out.push(finding('target-size', el, `${Math.round(r.width)}x${Math.round(r.height)}`));
      }
      return out;
    },
  };

  const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
  const settle = async () => { await sleep(300); for (let i = 0; i < 40 && document.querySelector('[aria-busy="true"]'); i += 1) await sleep(250); await sleep(300); };
  const go = async (path) => { if (location.pathname + location.search !== path) history.pushState(null, '', path); await settle(); };
  const setLang = async (lng) => { const { i18n } = await import('/src/app/i18n.ts'); await i18n.changeLanguage(lng); await settle(); };
  const audit = () => Object.values(checks).flatMap((check) => check().map((f) => ({ route: location.pathname, width: innerWidth, dir: document.documentElement.dir, ...f })));
  const run = async ({ routes = [location.pathname], lang } = {}) => { if (lang) await setLang(lang); const all = []; for (const path of routes) { await go(path); all.push(...audit()); } return all; };
  const links = (prefix) => [...new Set([...document.querySelectorAll(`a[href^="${prefix}"]`)].map((a) => a.getAttribute('href')))];
  window.__layoutAudit = { run, audit, go, setLang, links, checks };
})();
```
If `go()` does not change the page (the h1 stays the same), fall back to `location.assign(path)`, re-paste the script, and run `__layoutAudit.run()`.

### 3. Static checks (run once, no browser): output pasted in `02-layout-audit.md`
| Check | Command (from `web/`) | Pass |
|---|---|---|
| no near-black fills | `grep -rnE '\bbg-text\b\|\baccent-text\b\|has-checked:border-text' src --include=*.tsx` | empty |
| no white on success/danger fills | `grep -rnE 'bg-(success\|danger) text-surface\|text-surface/80' src --include=*.tsx` | empty |
| no label-offset hacks | `grep -rn 'className="mt-6"' src --include=*.tsx` | empty |
| one page container | `grep -rnE 'max-w-layout' src --include=*.tsx --include=*.ts` | only `shared/ui/layout.ts` |
| sticky/fixed offsets | `grep -rnE '\b(sticky\|fixed)\b' src --include=*.tsx` | each hit reviewed against the z-scale: bars z-20, exam header z-10 `top-16`, More menu z-30, dialog overlay z-40, dialog content z-50 |

### 4. Fix catalogue and record
Known findings from the code audit. These must be fixed:
| # | Finding | Fix (where) |
|---|---|---|
| F1 | Sticky exam timer `top-0` slides under the sticky app bar | `ExamHeader` `top-16` |
| F2 | Mobile exam submit bar `sticky bottom-0` is hidden under the fixed tab bar | `ExamRunner` `bottom-above-tab-bar` |
| F3 | `th` defaults to centre in Chrome; columns drift against start-aligned cells | global `th { text-align: start }` in `app.css` |
| F4 | Remove-icon buttons beside fields: 36 px button with a `mt-6` hack vs a 44 px field (6 px off-centre) | `Button size="icon"` (44) + `mt-6.5` (caption line 20 + gap 6) in 6 files |
| F5 | Configuration save buttons 36 px next to 44 px inputs | default size in 4 forms |
| F6 | Page roots use `gap-3` on 3 pages and `gap-4` everywhere else | `gap-4` |
| F7 | Two-row header (101 px), nav in a second row | single row (AppBar / TopTabs) |
| F8 | Lesson tabs use micro 12 px pills; every other tab group uses 15 px / 44 px | `pillTabClassName` |
| F9 | Skip link `z-20` sits under the header | `focus:z-50` |
| F10 | Tab bar ignores the iOS home indicator | `pb-safe-area` + `viewport-fit=cover` |
| F11 | `scroll-padding-top` sized for the 101 px header | bar + 8 |
| F12 | Dialog and assistant overlays have no z-index, so the sticky header and tab bar paint above the backdrop | overlay z-40, content z-50 |

For any further DOM finding, pick the shared-level rule first:
| Check | Allowed fix |
|---|---|
| overflow | `min-w-0` on the flex child, `flex-wrap` on the row, `break-words` on text. Wrap a wide table in `overflow-x-auto`. Never `overflow-x-hidden` on `main`/`body`. |
| edges | Remove the extra padding or margin on the page root. Never add a compensating margin. |
| row-heights / row-centres | A row holding a 44 px field uses default or `icon` buttons. `items-end` rows with no inline error. |
| icon-centre | Make the parent `inline-flex items-center gap-*`. For multi-line text use `items-start` with the icon wrapped in `<span className="flex h-lh shrink-0 items-center">`. |
| table-align | Fix the `th`/`td` alignment class to match (start for text, end for numbers on both). |
| header-*, tabbar-* | Fix in `AppBar`/`TopTabs`/`TabBar` only. |
| target-size | `min-h-11 min-w-11` (or `size="icon"`). |

`02-layout-audit.md` format: (1) environment line (commit, demo URL, accounts used); (2) matrix table `role · width · lang · routes audited · findings before · findings after`; (3) findings table `# · route · width · dir · check · target · detail · fix (file:line) · status (fixed / intended + reason)`; (4) static check outputs. Every row of (3) is fixed or justified as intended. The F1–F12 rows are listed even if the DOM run did not reproduce them. Any extra file touched appears in (3).

## Docs content
**§A `.claude/design-system.md`.**
- Title line: `· "Glass" — palette Indigo calm (v2.0)`.
- The Colour table becomes exactly the token rows above (21 colour rows; keep the 4-column `Token | CSS var | Value | Use` format the generator parses). Use texts: bg "page ground. Never pure white for the page"; surface "cards, sheets, inputs, app bar, tab bar"; soft "muted chips, pending badge, progress track, skeletons, secondary hover, user bubbles"; text "primary text. Never a button, tab or badge fill"; text.muted "secondary text, labels, captions, inactive nav"; border "hairline on cards, app bar, tab bar, table rows"; border.strong "inputs, option outlines, secondary buttons, inactive pill tabs"; accent "primary/accent button fill, links, focus ring, progress fill, active nav/tab text, selected option border, checkbox accent-color, logo"; accent.hover "hover on accent fills"; accent.pressed "pressed on accent fills"; accent.soft "active nav item and pill tab fill, selected option fill, accent chips, toggled secondary button"; success "non-text only: borders, icons on surface, pass progress fill, diagram key stroke"; success.text "success text, ok badge text, verdict icon on success.soft"; success.soft "correct feedback background, ok badge fill"; danger "wrong, rejected, overdue, destructive: text, border, fill"; danger.soft "wrong feedback background, bad badge fill"; warning "partial credit, pending review"; warning.soft "partial feedback background"; v2 "v2 badge only"; overlay "dialog backdrop"; aurora "landing hero + subscribe header ONLY".
- Spacing: add rows `| layout.bar | 56 (app bar and mobile tab bar height) |` and `| layout.auth.max | 440 (auth column) |`.
- shadow.1 and shadow.2: the new values, keeping the ` — use` suffix (shadow.2 use adds "nav menu").
- Components: Button "primary (accent fill, white label; hover accent.hover, pressed accent.pressed), accent (same as primary since 2.0), secondary (surface + border.strong; aria-pressed → accent.soft + accent border/text), danger, ghost"; size `icon` 44×44 for icon-only buttons beside fields (offset `mt-6.5` under a label). Badge "ok = success.soft + success.text; bad = danger.soft + danger; pending/neutral = soft + text.muted; role = soft + text; v2 = outline". QuizOption selected "accent.soft fill + accent border", control accent-color accent. TabBar active "accent text 600 on an accent.soft icon pill; bottom padding env(safe-area-inset-bottom)". Replace TopTabs with **AppBar** ("single row, height layout.bar, sticky, surface + bottom hairline, same container as main: logo (accent) · nav · role badge · name (≥ xl) · sign-out (label ≥ xl)"), **TopNav** ("pills min-h 36; inactive text.muted, hover bg + text, active accent.soft + accent 600; destinations beyond the role's top-bar list sit in an «المزيد» disclosure menu: surface, border, shadow.2, radius.md, items ≥ 44 px, Esc/outside click closes") and **BrandBar** ("app bar with logo only (+ end slot) on landing, login, sign-up, accept-invite, onboarding"). SubTabs becomes **PillTab** ("filters, sub-tabs, segmented toggles: inactive surface + border.strong, active accent.soft + accent border + accent text"). AssistantFab "accent fill". DiagramCanvas count pill "accent fill".
- Rules: 1 → "Indigo = action, link or current place. Green = correct. Red = wrong. Amber = partial/pending. Nothing else is coloured." 2 → "Status text uses success.text, danger or warning on surface or its soft background. `success` itself is never text. Badges are soft fills; no white text on success." Add 11 → "Near-black (`color.text`) is never a fill."
- Breakpoints: add row `| bp.xl | ≥1200 | app bar shows the display name and the sign-out label |`. Add `layout.bar` / `layout.auth.max` to the layout notes.
- Accessibility table: replace with the AA table above.
- Token → code mapping: the app.css snippet adds the four new `@theme inline` lines. shadcn `--primary` → color.accent.
- Change log row: `| 2.0 | 2026-10-03 | Indigo calm palette (#276): indigo primary and active states, ink-tinted borders and shadows, AA-tuned status colours (success.text added), aurora retuned; single-row app bar with More menu, BrandBar on auth and onboarding, layout.bar, layout.auth.max, bp.xl |`.

**§B `docs/design-system.md`** (must give the same answers as §A). Header table: Version 2.0, Date 2026-10-03, Decision adds "Indigo calm palette (#276)". §1 principle 2 → "Indigo means action, link or where you are…". §2.1/§2.2: the same values under the doc's names (`--text-2`, `--ok`, `--ok-text` new, `--bad`, `--warn`, `--accent-hover`, `--accent-pressed`). §2.3 the new aurora. §2.4: the AA table above, plus the rule line from §A rule 2. §4: new shadows, "App bar / tab bar height 56 px", "Auth column max 440 px". §5.1 buttons table: Primary fill `--accent` (hover `--accent-hover`, pressed `--accent-pressed`); Accent = Primary. Add size icon 44. §5.3 Selected = `--accent-soft` / `--accent`. §5.5 badges as §A. §5.7 Navigation rewritten per §A AppBar/TopNav/TabBar/BrandBar, with breakpoints 900 and 1200. §5.10 no change. §5.11 overlay `rgba(30,27,75,.40)`. §6 breakpoints add 1200. §10: shadcn `--primary` maps to the accent.

**§C `docs/claude-design-prompt.md`.** §2.1 CSS block: the new values (add `--accent-hover`, `--accent-pressed`, `--ok-text`). Colour rules first bullet → "Indigo = action, link or current place…". Second bullet → status text rule from §A. §2.3 shadows → the new values. §2.4 Buttons: "Hover: secondary shifts to `--soft`; primary to `--accent-hover`, pressed `--accent-pressed`." Table Primary fill `--accent`, Accent "same as Primary". Quiz option selected `--accent-soft` fill with `--accent` border; `accent-color: var(--accent)`. Badges as §A. Navigation → single-row desktop app bar text from §A (desktop ≥ 900 px; name and sign-out label from 1200 px; admin «المزيد» menu), tab bar active `--accent` on an `--accent-soft` pill. Assistant floating button `--accent` fill. Dialog overlay `rgba(30,27,75,.40)`.

**§D `docs/prototype.md`.** After the `styles.css` bullet add: "The prototype keeps this grey wireframe look and its two-row top bar with the persona switchers. The product's look (Indigo calm palette, single-row app bar) comes from `docs/design-system.md`."

## Test plan
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| T1 | `contrast` (scripts/tokens/contrast.test.ts) | `it('returns 21 for black on white')` | `contrastRatio('#000000', '#FFFFFF')` toBeCloseTo 21, 5 |
| T2 | `contrast` | `it('is symmetric and 1 for identical colours')` | `contrastRatio(a,b) === contrastRatio(b,a)` for `#4F46E5`/`#FFFFFF`; `contrastRatio('#4F46E5','#4f46e5')` = 1 |
| T3 | `readToken` | `it('throws when a token is missing')` | `readToken(':root {}', 'color-bg')` throws `/--ds-color-bg not found/`; `readToken('  --ds-color-bg: #F4F5FB;', 'color-bg')` = `#F4F5FB` |
| T4 | `design tokens` | `it.each(textPairs)('%s on %s meets 4.5:1')` | Over the real tokens.css, each of: text on surface, bg, soft, accent-soft, success-soft, danger-soft, warning-soft; text-muted on surface, bg, soft, accent-soft, success-soft, danger-soft, warning-soft; surface on accent, accent-hover, accent-pressed, danger; accent on surface, bg, soft, accent-soft; success-text on surface, success-soft; danger on surface, bg, danger-soft; warning on surface, bg, warning-soft; v2 on surface → ratio ≥ 4.5 |
| T5 | `design tokens` | `it.each(uiPairs)('%s on %s meets 3:1')` | success on surface and bg; accent on surface and bg → ≥ 3 |
| T6 | `design tokens` | `it('keeps white text readable on every aurora stop')` | `gradientStops(readToken(css,'gradient-aurora'))` has 3 stops, each vs `#FFFFFF` ≥ 4.5 |
| T7 | `topBarItems` (navConfig.test.ts) | `it('returns the admin top-bar destinations in nav order')` | keys `['dashboard','content','questions','users']` |
| T8 | `topBarOverflowItems` | `it('puts the remaining admin destinations in More and none for students or teachers')` | admin `['blueprints','payments','audit','avatarConversations','export','configuration']`; student `[]`; teacher `[]` |
| T9 | `topBarOverflowItems` | `it('hides an overflow destination whose capability the role lacks')` | teacher nav with an injected `users` item (as in the existing visibleNavItems test) → `[]` |
| T10 | `AppShell` (AppShell.test.tsx) | `it('places the logo, main navigation and sign-out in one header row')` | `banner = findByRole('banner')`; `banner.children` length 1 (comment: layout guard #276); `within(banner).getByRole('link',{name:'Elmanhg'}).parentElement` is `banner.firstElementChild` and contains the 'Main navigation' nav and the 'Sign out' button |
| T11 | `AppShell` | `it('shows four admin destinations and a More menu in the top bar')` | `/admin` as admin: `linkNames(Main navigation)` = `['Dashboard','Content','Questions','Users']`; `await within(nav).findByRole('button',{name:'More'})` has `aria-expanded="false"` |
| T12 | `AppShell` | `it('does not show a More menu to students')` | `/student`: `within(main nav).queryByRole('button',{name:'More'})` is null |
| T13 | `TopNavMore` (TopNavMore.test.tsx) | `it('lists the remaining admin destinations when More is opened')` | click More → `aria-expanded="true"`; link names in the menu list (`within(within(nav).getAllByRole('list')[1]).getAllByRole('link')`, the second list in the nav) = `['Exam blueprints','Payments','Audit log','Assistant conversations','Data export','Configuration']` |
| T14 | `TopNavMore` | `it('closes on Escape and returns focus to More')` | open, `user.keyboard('{Escape}')` → 'Payments' link gone, More has focus, `aria-expanded="false"` |
| T15 | `TopNavMore` | `it('closes when the user clicks outside the menu')` | open, `user.click(screen.getByRole('main'))` → 'Payments' link gone |
| T16 | `TopNavMore` | `it('navigates and closes when a destination is chosen')` | `server.use(...getAuditLogsMock())`; open, click 'Audit log' → `router.state.location.pathname` = `/admin/audit` (via `waitFor`); 'Payments' link gone |
| T17 | `TopNavMore` | `it('marks the current overflow destination as the current page')` | `server.use(...getAuditLogsMock())`; renderApp `/admin/audit`; open More → 'Audit log' link `aria-current="page"`, 'Payments' not |
| T18 | `TopNavMore` | `it('has no axe violations with the menu open')` | `/admin`, open More, `(await axe(container)).violations` = [] |
| T19 | `LoginPage` | `it('shows the app bar with the logo linking home')` | `within(await findByRole('banner')).getByRole('link',{name:'Elmanhg'})` has `href="/"` |
| T20 | `SignUpPage` | `it('shows the app bar with the logo linking home')` | same, on `/signup` |
| T21 | `OnboardingPage` | `it('shows the app bar with the logo linking home')` | `openOnboarding()`, same assertion |

All new DOM tests use `renderApp`, `userEvent.setup()` and role queries. No existing test is edited, skipped or deleted. The existing AppShell top-tab tests (student 5 and teacher 4 links) must pass unchanged.

## Risks
| Risk | Mitigation |
|---|---|
| Tests asserting old colours | Code audit: the only class assertions are `bg-warning-soft`, `text-danger` and `border-warning` (token names unchanged). There are no `toMatchSnapshot` tests. MethodSwitch and LessonTabs keep their roles and `aria-*`. |
| `perf:budget` (quiz 253/255 KB brotli) | No dependency is added. `TopNavMore` is lazy and lands only in a dynamic chunk, which the budget does not count. Student and teacher never render it. The only growth is CSS (aria/pill variants, a few utilities). If any budget fails, report `BLOCKED: perf budget <page> <n>/<max>`. Do not raise the budget. |
| `gen:tokens` drift check in CI | Run `npm run gen:tokens` after editing `.claude/design-system.md` and commit `tokens.css`. |
| CI literal grep | Keep every new value in tokens. `env()` lives only in `app.css`. No `[NNpx]` and no `bg-indigo-*`. |
| Tailwind variant order (hover vs aria-pressed) | aria-* variants sort after hover in v4, so the active pill keeps its fill on hover. Check this once in the DOM run. |

## Definition of done
- [ ] `.claude/design-system.md` and `docs/design-system.md` give the same value for every token in the token table. Both hold the AA table. Change log 2.0.
- [ ] `npm run gen:tokens` produces no further diff, and `tokens.css` contains `--ds-color-accent: #4F46E5`, `--ds-color-success-text: #166534`, `--ds-layout-bar: 56px`, `--ds-layout-auth-max: 440px` and `--breakpoint-xl: 1200px`.
- [ ] `app.css` maps accent-hover, accent-pressed, success-text and container-auth. `--primary` → accent. It has the `pb-safe-area` and `bottom-above-tab-bar` utilities, the `th` start rule and the new scroll padding.
- [ ] Primary/accent buttons are indigo with hover and pressed shades. Secondary aria-pressed is indigo-soft. `size="icon"` exists.
- [ ] Static check greps (Alignment audit §3) are all empty or as specified.
- [ ] The desktop header is one `h-14` row in `layoutContainerClassName`: logo · nav · role · name (xl) · sign-out. The admin More menu holds the 6 overflow destinations and closes on Esc, outside click and selection.
- [ ] Login, sign-up, accept-invite, onboarding and landing render `BrandBar`. The auth column is `max-w-auth`, centred.
- [ ] The tab bar active state is an indigo pill with `pb-safe-area`. `index.html` has `viewport-fit=cover`.
- [ ] F1–F12 are fixed in the files named.
- [ ] `02-layout-audit.md` covers every role × 375/768/1280 × ar/en with zero unexplained findings, and lists every extra file touched.
- [ ] `docs/claude-design-prompt.md` §2 and `docs/prototype.md` are updated per §C/§D. No doc still says near-black primary, `#0071E3`, or a two-row/underlined desktop nav.
- [ ] Tests T1–T21 exist with those names and pass. No existing test is changed.
- [ ] `npm run typecheck`, `lint`, `format:check`, `test -- --run --coverage`, `build` and `perf:budget` all exit 0, with the budget lines (quiz ≤ 255) pasted in the report. `gen:api` produces no diff.
