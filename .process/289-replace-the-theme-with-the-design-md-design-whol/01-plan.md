# Plan — Replace the theme with the DESIGN.md design, whole app (#289, E19.S5)

## Goal
Every user (student, teacher, admin, anonymous) sees the DESIGN.md look instead of Indigo calm. Pages sit on a pale-blue mist canvas (`#F2F7FD`) with white 16 px cards and one subtle shadow. The single primary action per screen is a Spring Mint pill with a charcoal label. Secondary actions are Signal Blue outline pills, tertiary actions are charcoal ghost buttons, and destructive actions are red outline pills. The app bar is white and sticky, with a Signal Blue logo. Subjects, units and icons sit in circles. Type is Poppins for Latin and Almarai for Arabic, at weights 400/700/800. A purple-to-blue gradient banner carries the landing hero, the student home headline and the subscribe header. Admin and teacher data views keep their tables and charts under the new tokens, as a documented exception. AA contrast is verified by a unit test, the layout is re-audited for every role at 375/768/1280 px in ar and en, and no bundle budget is raised.

## Scope
**In:** web/ and docs only (no api/, no ai/). All five sub-tasks of #289:
1. `docs/design-source.md`: add it to git with a header note. Rewrite `.claude/design-system.md` and `docs/design-system.md` (v3.0 "Mist"), including the resolved contradictions and the documented exceptions.
2. Tokens, `tokens.css`, the `app.css` theme, fonts (Poppins, plus Almarai for Arabic, both self-hosted at 400/700/800), one shadow, and the new radii.
3. Components: Button (mint, outline, ghost, danger), inputs and Select, cards, pill tabs, nav bar, badges, option cards (#286), dialogs, the assistant FAB, panel and page (#288), charts and tables.
4. A gradient hero on the landing page, the student home headline and the subscribe header. Circle tiles for subjects, units and value icons.
5. Layout re-audit for every role × 375/768/1280 × ar/en, the AA table, updated tests, and green budgets. Docs-sync: `docs/claude-design-prompt.md` §0–2, §4, §6 and §8, `docs/prototype.md`, `docs/performance.md`, `docs/security.md`, `docs/constitution.md`, README, and the react-feature skill line.

**Out:**
- `prototype/styles.css`: the prototype stays a grey wireframe.
- DESIGN.md parts with nothing to apply them to: character illustrations and speech bubbles (no art assets exist; a decorative halo circle stands in), the Trustpilot strip (the product has no ratings), the subject carousel with arrows (subjects stay a responsive grid), and a footer locale select (no language switcher exists, as in #276).
- Full-bleed heroes. Banners stay inside the page container (D18).
- Raising any `budgets.json` limit.

**Deferred:** none.

## Decisions
| # | Question | Decision | Why |
|---|----------|----------|-----|
| D1 | System name | "Mist" v3.0 (after Canvas Mist). Every "Glass" mention in README, the constitution, the react-feature skill and the design prompt becomes "Mist". | Nothing glassy remains, and a stale name is docs divergence. |
| D2 | Which blue carries text | `color.accent` = Signal Blue `#116EEE` for the logo, borders (secondary, selected, active), focus ring, progress fill, chart bars, checkbox `accent-color`, and white-text badge/count-pill fills. New `color.accent.text` = `#0E64DA` for all accent-coloured text below large size: links, secondary labels, active nav and tab text, chips. | Signal Blue is 4.68 on white but **4.34 on the canvas** and 4.15 on accent.soft, so it fails AA as text there. `#0E64DA` is 5.45/5.06/4.84/4.83 (surface/bg/soft/accent.soft) and is visually the same brand blue. This is the same two-tier pattern as success/success.text. |
| D3 | Primary fill | New `color.action` `#11EE92` (Spring Mint), `action.hover` `#0FD683`, `action.pressed` `#0DBF75`. The label is `color.text` (charcoal), 9.33/7.48/5.96. | Story rule: mint is the only filled chromatic action. Darker mint on hover and press keeps the charcoal label ≥ 5.96. |
| D4 | `accent` Button variant | Delete it. The 5 callers become `variant="secondary"`, except `FakeCheckoutCard` "succeed", which becomes `variant="primary"` (it is the only action on that screen). | Subscribe buttons repeat per plan card. Mint on each would break "one primary per screen". |
| D5 | Secondary / ghost / danger | Secondary: `border-2 border-accent bg-transparent text-accent-text`, hover `bg-soft`, toggled (`aria-pressed`) `bg-accent-soft`. Ghost: `text-text`, hover `bg-soft`. Danger: `border-2 border-danger bg-transparent text-danger`, hover `bg-danger-soft`. | Story: blue outline, charcoal ghost, red outline (exception). `border-2` is the "1.5–2 px" in DESIGN.md. The variant structure is kept, so `tailwindCascade` precedence behaves as today. |
| D6 | App-bar sign-out | `variant="ghost"` (was secondary). | DESIGN.md nav: the outlined button is the entry action and "Log in" is ghost. A blue outline sign-out would compete with the page's secondary actions. |
| D7 | Assistant FAB | White pill: `border-2 border-accent bg-surface text-accent-text shadow-1 hover:bg-accent-soft`, same size and position. | A mint FAB would be a second mint action on every student screen. DESIGN.md forbids filled blue actions. |
| D8 | Canvas, soft, border tints (not in DESIGN.md) | soft `#ECF2FA`, accent.soft `#EAF2FE`, border (hairline) `#E6ECF2`, border.strong = steel `#D6DEE6`, overlay `rgba(37,43,47,.40)`. | soft and accent.soft are the lightest mist and blue tints that keep slate caption ≥ 4.5 (4.55). Cards keep a hairline because surface vs canvas is only 1.08:1. Steel is for inputs and outlines, as DESIGN.md says. |
| D9 | Status colours | Kept as a documented exception: success `#16A34A` (non-text) / success.text `#166534`, danger `#C8233A`, warning `#B45309`, v2 `#6A3FB5`. Soft fills retuned so slate caption passes: success.soft `#DFF3E5`→`#E6F6EB`, danger.soft `#FDECEE`→`#FEF1F2`, warning.soft `#FEF3E2` kept. | With `#666E7E` as text.muted, the old softs gave 4.42 and 4.49 (fail). The new ones give 4.58 and 4.66. |
| D10 | Hero gradient AA | Token exactly `linear-gradient(90deg,#3B1E90 0%,#5A3CC4 30%,#3A6EF0 100%)` (typo `#3b1e9` read as `#3b1e90`). An RTL twin at `270deg` puts deep indigo behind the inline-start text. White on the stops is 11.84 / 7.30 / **4.498**. Rules: banners keep `p-5` (20 px) or more inline-end padding (white text at 94 %+ of the width is ≥ 4.70, computed). Body copy on the hero is `max-w-120`. Only large text may reach the end stop (3:1). | Keeps the dev's exact colours. The last stop misses 4.5 by 0.002, and no normal-size text sits there. |
| D11 | Focus on the gradient | No change. The 2 px white ring offset gives 4.50–11.84 against the gradient, and the Signal Blue ring is 4.68 on that white. | Visible without a hero-only variant. |
| D12 | Mint boundary | Mint vs surface is 1.54 and vs the gradient end is 2.93. Accepted: the charcoal label (9.33) identifies the control (WCAG 1.4.11 does not require fill contrast when text identifies it). Recorded in the AA table as "not a required pair". | It is the brand CTA. Every alternative changes the design. |
| D13 | Input / option boundary | Steel `#D6DEE6` is 1.36 on surface. It is kept as the decorative-boundary exception, as in #276 D16 (the label and placeholder identify the field). | It is DESIGN.md's token. The #276 policy is unchanged. |
| D14 | Arabic font | **Almarai** (Boutros Fonts / Mourad Boutros, Google Fonts, OFL), `@fontsource/almarai@5.3.0`, arabic subset only. | It is geometric, low-contrast and monoline, with round bowls and open counters that echo Poppins' circle-based Latin. It ships exactly 300/400/700/800, so the 400/700/800 scale maps 1:1 with no synthetic bold. Its large, clear forms stay readable at 14 px. It is popular (rank 61 in the ui-ux-pro-max google-fonts data). Its arabic woff2 is 31–33 KB per weight (Noto is 49–53). Rejected: Readex Pro (no 800, wide); Tajawal (thinner strokes, reads smaller at 14 px); Cairo (more contrast, less geometric); Alexandria (display-wide, too wide for tables); Noto Kufi Arabic (square Kufi forms clash with round Poppins); IBM Plex Sans Arabic (humanist, no 800). The old Cairo/Tajawal ban is replaced by "Poppins + Almarai only". |
| D15 | Font stack and loading | `--font-sans` = `--font-display` = `'Poppins', 'Almarai', Tahoma, sans-serif`. Poppins comes first, so Latin letters, digits and punctuation inside Arabic text render in Poppins and Arabic glyphs fall through to Almarai. Imports: `@fontsource/poppins/latin-{400,700,800}.css` and `@fontsource/almarai/arabic-{400,700,800}.css` (each has `font-display: swap`). Remove `@fontsource/readex-pro` and `@fontsource/noto-sans-arabic`. | One family, as in DESIGN.md. 6 `@font-face` rules replace about 27 (multi-subset), so the entry CSS shrinks. |
| D16 | Preload | No page preloads a font. | There is one `index.html`. The fontsource CSS sits in the entry stylesheet in `<head>`, so faces are discovered at first style resolve. Preloading 64 KB of Arabic woff2 would race the entry JS on the 1.6 Mbps profile behind the 2 s lesson gate (performance.md). `swap` shows fallback text at once. |
| D17 | Weights | Only 400/700/800. Add `--font-weight-*: initial; --font-weight-normal: 400; --font-weight-bold: 700; --font-weight-extrabold: 800;` to `@theme`. Script: `font-semibold`→`font-bold`, `font-medium`→`font-normal`. Display, stat and banner text is `font-extrabold`. The logo is 700. | `font-semibold` would otherwise silently match the 700 face. The reset makes stray weights compile to nothing, and the static grep catches them. 800 then loads only on pages with display or stat text. |
| D18 | Hero placement | A contained banner (`rounded-lg`, 16 px, `bg-hero`, no shadow) inside the page container, on the landing hero, the student home headline (`HeadlineCounterCard`) and the subscribe header. Not on quiz or exam results. | Keeps the #276 rule that the logo edge equals the content edge, with no negative-margin hacks in `AppShell`. Results carry status badges that need their AA pairs on white. |
| D19 | Student home banner content | `HeadlineCounterCard` (as of #286) becomes the banner. Sentence in white extrabold. `MasteryBar tone="onHero"` (white fill on a 30 % white track). Stat chips become `bg-surface` with charcoal and slate text. The greeting h1 stays above on the canvas. No CTA is added: `NextLessonCard`'s button stays the screen's one mint. | Keeps #286's structure and tests. The sample's hero CTA would be a second primary. |
| D20 | Type scale mapping (DESIGN 10/12/14/16/18/24/36/40) | display 36/41·40/46 800; h1 24/31·36/41 700; h2 18/27·24/31 700; h3 16/24 700; body 16/27 400; ui 16/24 400; **label 14/21 700 (new)**; caption 14/21 400; micro 12/18 700; stat 24/31 800; mono 12/18 400. Tracking normal (the two `--letter-spacing` mappings are removed). 10 px is unused. | Uses only DESIGN sizes and line heights (1.14/1.3/1.5/1.7). Inputs at 16 px avoid iOS zoom. Buttons, nav and tabs at 14/700 match DESIGN.md's components. The new minimum for Arabic running text is 14 px (caption); lesson text and inputs are 16. |
| D21 | Radii | radius.sm 5 (inputs), md 16, lg 16, pill 45, circle 9999 (`rounded-full`). | DESIGN.md: "cards 16 px or rounder". Options and list items are card-like. Keeping md/lg/pill class names avoids touching about 230 call sites. |
| D22 | Shadows | One shadow: `shadow.1` = `rgba(0,0,0,.1) 0 1px 2px 0`. `shadow.2` is deleted. Script `shadow-2`→`shadow-1` (dialog, assistant panel, More menu, toaster, drag ghost, skip link, FAB). The app bar uses `shadow-1` in place of its bottom hairline. | Story: one shadow. Dialogs separate through the overlay. |
| D23 | Layout | layout.max 1040→1200. Gutter (16/24), card padding (16 mobile, 20 desktop), bar 56 and breakpoints are unchanged. The landing page main gap is `gap-10 lg:gap-16` (64 px section gap on desktop). | 1200 is DESIGN.md's page width. 16 px card padding on 375 px phones keeps content width. 56 px keeps every sticky offset from #276 (`top-16`, scroll padding). |
| D24 | Circle tiles | New `shared/ui/circle.ts`: `circleTileClassName` (56 px, mist fill, Signal Blue text, h2 800) for the subject initial (`SubjectMasteryCard`) and landing value icons (`ValueProps`); `circleBadgeClassName` (44 px, label 700) for the unit ordinal (`UnitListItem`). Both `aria-hidden`. Initial = `subjectInitial(name)`: strip a leading «ال», first grapheme, uppercased. | DESIGN.md's signature shape. A class constant, not a component (3 call sites, the same pattern as `pillTab.ts`). The text inside is decorative; the name stays the accessible label. |
| D25 | Nav item style | Inactive charcoal `text-text`, label 14/700, hover `bg-soft`. Active keeps the `bg-accent-soft` pill with `text-accent-text`. | DESIGN.md's nav is charcoal with a blue active state. The pill keeps a non-text-colour current indicator. |
| D26 | Admin data views | Tables, KPI tiles and charts in admin, teacher and the student's history and progress tables stay dense. This is documented as an allowed exception to DESIGN.md's "no dense data tables". Restyled only through tokens (16 px card, hairline rows, label/caption type, `sm` outline row actions). | Story rule. These are working tools, not marketing. |
| D27 | Changing ~300 class strings | Five scripted rewrites (S1–S5 below) run after rebasing on main with #286 and #288, then explicit final strings in the shared files. Verified by static greps. | Mechanical, reviewable, and it covers the lane files. |
| D28 | Layout audit | Reuse #276's `layout-audit.js`, plus `primary` (more than one visible mint button), `hero` (direction and inline-end padding) and `fonts()`. Record in `02-layout-audit.md`. | Story: "existing layout-audit.js approach". The new checks cover this story's own rules. |
| D29 | Morabh reuse | None: UI-only story. | dotnet-feature §5 applies to api/ only. |

## Preconditions and order
1. `git fetch origin` and rebase this branch on `origin/main`, which must contain #286 and #288 (`web/src/shared/ui/optionCard.ts`, `features/exam/components/MultiExamStartBar.tsx`, `features/progress/components/WeakSpotRow.tsx`, `features/avatar/pages/AssistantPage.tsx` must exist). If either lane has not merged, stop with `BLOCKED: lane #286/#288 not on main`.
2. Docs `.claude/design-system.md` → `npm --prefix web run gen:tokens` → `app.css` → fonts → scripts S1–S5 → explicit component edits → tests → build/budget → layout audit → remaining docs.

## Existing code touched
| File | Change |
|------|--------|
| `.claude/design-system.md` | Full rewrite: see Docs §A (token tables verbatim). |
| `docs/design-system.md` | Full rewrite: Docs §B. |
| `docs/design-source.md` | `git add`. Prepend Docs §F's 4-line note. Body verbatim (the typo is resolved in design-system.md, not edited here). |
| `docs/claude-design-prompt.md` | Docs §C. |
| `docs/prototype.md` | Line 12 → Docs §D. |
| `docs/performance.md` | §3 Measured column ← this run's `perf:budget` numbers (budgets unchanged). §4 add a font bullet; line 194 add a note (Docs §E). |
| `docs/security.md` | Line 107 "fontsource Noto Sans Arabic and Readex Pro" → "fontsource Poppins and Almarai". |
| `docs/constitution.md` | Line 154 "(Glass, light only)" → "(Mist, light only)". |
| `README.md` | Line 11 → `"Mist" design system (from DESIGN.md): colours, type, spacing, components. Light only.` Add row `\| [docs/design-source.md](docs/design-source.md) \| DESIGN.md, the verbatim style reference the design system is derived from. \|` after it. Line 31 "Glass tokens" → "Mist tokens". |
| `.claude/skills/react-feature/SKILL.md` | Line 10 "(the Glass system," → "(the Mist system,". Nothing else. |
| `web/package.json`, `web/package-lock.json` | dependencies: remove `@fontsource/noto-sans-arabic`, `@fontsource/readex-pro`; add `"@fontsource/almarai": "5.3.0"`, `"@fontsource/poppins": "5.3.0"` (exact pins, alphabetical). `npm install` updates the lockfile. Both versions verified with `npm view` (repo `fontsource/font-files`, OFL-1.1). |
| `web/src/main.tsx` | Replace the 6 fontsource imports with, in this order: `@fontsource/poppins/latin-400.css`, `latin-700.css`, `latin-800.css`, `@fontsource/almarai/arabic-400.css`, `arabic-700.css`, `arabic-800.css`. |
| `web/src/styles/tokens.css` | Regenerated only (`gen:tokens`). |
| `web/src/styles/app.css` | `@theme`: append `--font-weight-*: initial; --font-weight-normal: 400; --font-weight-bold: 700; --font-weight-extrabold: 800;`. `@theme inline`: delete `--color-accent-hover`, `--color-accent-pressed`, `--shadow-2`, `--text-display--letter-spacing`, `--text-stat--letter-spacing`. Add `--color-accent-text: var(--ds-color-accent-text); --color-action: var(--ds-color-action); --color-action-hover: var(--ds-color-action-hover); --color-action-pressed: var(--ds-color-action-pressed); --text-label: var(--ds-type-label-size); --text-label--line-height: var(--ds-type-label-line);`. `--font-display` and `--font-sans` → `'Poppins', 'Almarai', Tahoma, sans-serif`. `:root`: `--primary: var(--ds-color-action); --primary-foreground: var(--ds-color-text);`. Replace `@utility bg-aurora {…}` with `@utility bg-hero { background-image: var(--ds-gradient-hero); &:where([dir='rtl'], [dir='rtl'] *) { background-image: var(--ds-gradient-hero-rtl); } }`. `.rich-text a` colour → `var(--ds-color-accent-text)`. Keep #286's legend/fieldset base rules and #288's `h-assistant*` utilities. |
| `web/src/shared/ui/button.tsx` | Final contract below. |
| `web/src/shared/ui/pillTab.ts` | `pillTabClassName = 'inline-flex min-h-11 items-center justify-center gap-2 rounded-pill border border-border-strong bg-surface px-4 text-label font-bold text-text transition-colors hover:bg-soft focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden disabled:opacity-45 aria-pressed:border-accent aria-pressed:bg-accent-soft aria-pressed:text-accent-text aria-selected:border-accent aria-selected:bg-accent-soft aria-selected:text-accent-text data-[status=active]:border-accent data-[status=active]:bg-accent-soft data-[status=active]:text-accent-text'` |
| `web/src/shared/ui/layout.ts` | `layoutContainerClassName` unchanged. `appBarClassName = 'sticky top-0 z-20 h-14 bg-surface shadow-1'`. `logoClassName = 'inline-flex min-h-11 shrink-0 items-center rounded-sm font-display text-h2 font-bold text-accent focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden'` (the **only** `text-accent` text use left; logo exempt, 4.68 on white). |
| `web/src/shared/ui/dialog.tsx`, `toaster.tsx` | Only through S1/S4 (title `font-bold`, `shadow-1`). Overlay restyles through the `color.overlay` token. |
| `web/src/shared/ui/input.tsx`, `select.tsx`, `optionCard.ts` (#286) | No edit: restyled through tokens (radius.sm 5 / md 16, steel border.strong, 16 px ui, Signal Blue border + accent.soft fill when checked). Listed so the reviewer checks the result. |
| `web/src/features/shell/navStyles.ts` | `topNavItemClassName = 'inline-flex min-h-9 shrink-0 items-center gap-1.5 rounded-pill px-3 text-label font-bold whitespace-nowrap text-text transition-colors hover:bg-soft focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden data-[status=active]:bg-accent-soft data-[status=active]:text-accent-text'` |
| `web/src/features/shell/components/AppBar.tsx` | Sign-out `<Button variant="ghost" size="sm" …>` (classes unchanged). Role badge via S1. |
| `web/src/features/shell/components/TabBar.tsx`, `TopNavMore.tsx` | Only S1/S3/S4. |
| `web/src/features/avatar/components/AvatarDock.tsx` | `dockClassName = 'fixed start-4 bottom-24 z-10 inline-flex h-12 items-center gap-2 rounded-pill border-2 border-accent bg-surface px-5 text-label font-bold text-accent-text shadow-1 hover:bg-accent-soft focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden lg:bottom-6'` |
| `web/src/features/avatar/components/AvatarPanel.tsx`, `AvatarMessageBubble.tsx`, and #288's `pages/AssistantPage.tsx`, `components/AssistantChat.tsx`, `AssistantConversationItem.tsx`, `AssistantConversationList.tsx` | Only S1/S3/S4 (panel `shadow-1`, `rounded-e-lg` = 16; bubbles soft/white; current item `border-accent bg-accent-soft`). |
| `web/src/features/landing/components/LandingHero.tsx` | Contract below. |
| `web/src/features/landing/components/ValueProps.tsx` | Icon → `<span aria-hidden="true" className={circleTileClassName}><Icon aria-hidden strokeWidth={1.8} className="size-6" /></span>`. |
| `web/src/features/landing/pages/LandingPage.tsx` | main `cn(layoutContainerClassName, 'flex flex-col gap-10 pt-6 pb-10 lg:gap-16')`. |
| `web/src/features/subscription/components/SubscribeHeader.tsx` | Contract below. |
| `web/src/features/mastery/components/HeadlineCounterCard.tsx` (#286 version) | Contract below. |
| `web/src/features/mastery/components/MasteryBar.tsx` | Contract below. |
| `web/src/features/mastery/components/SubjectMasteryCard.tsx` | Contract below. |
| `web/src/features/browse/components/UnitListItem.tsx` | Props `ordinal: number` added. Contract below. |
| `web/src/features/browse/pages/SubjectPage.tsx` | `data.units.map((unit, index) => <UnitListItem key={unit.id} unit={unit} ordinal={index + 1} />)`. |
| `web/src/features/dashboard/components/KpiFigure.tsx` | `text-stat font-bold` → `text-stat font-extrabold`. |
| `web/src/features/exam/components/ExamResultSummary.tsx`, `quiz/components/QuizResultSummary.tsx` | `text-display font-bold` → `text-display font-extrabold`. |
| `askTeacher/components/AskTeacherLink.tsx`, `AskTeacherUpsell.tsx`, `subscription/components/AskTeacherCheckoutAction.tsx`, `BaseCheckoutActions.tsx` | `variant="accent"` → `variant="secondary"`. |
| `subscription/components/FakeCheckoutCard.tsx` | `variant="accent"` → `variant="primary"`. |
| `web/scripts/tokens/contrast.test.ts`, `web/src/shared/ui/button.test.tsx` | **modify** per Test plan. |
| `web/src/features/mastery/pages/StudentHomePage.test.tsx`, `web/src/features/browse/pages/SubjectPage.test.tsx` | **add** one test each. |
| Every `web/src/**/*.{ts,tsx}` matched by S1–S5 (not tests, not `src/shared/api/generated`) | Scripted rewrite only. |

Paths without a root are under `web/src/features/`. All were verified to exist on main, or in the #286/#288 worktrees for the lane files.

### Scripted rewrites (run from `web/`, after the rebase, before the explicit edits above)
```bash
FILES=$(grep -rlE --include=*.ts --include=*.tsx 'font-semibold|font-medium|text-accent|shadow-2' src | grep -vE '\.test\.tsx?$|src/shared/api/generated/')
perl -pi -e 's/(?<![-\w])font-semibold(?![-\w])/font-bold/g'          $FILES   # S1
perl -pi -e 's/(?<![-\w])font-medium(?![-\w])/font-normal/g'          $FILES   # S2
perl -pi -e 's/(?<![-\w])text-accent(?![-\w])/text-accent-text/g'     $FILES   # S3 (then restore logoClassName to text-accent)
perl -pi -e 's/(?<![-\w])shadow-2(?![-\w])/shadow-1/g'                $FILES   # S4
perl -pi -e 's/text-display font-bold/text-display font-extrabold/g; s/text-h1 font-bold text-balance/text-h1 font-extrabold text-balance/g' $FILES  # S5
```
Then `npx prettier --write` on the changed files (class re-sort).

## Files to create
| # | Path | Type | Contract |
|---|------|------|----------|
| 1 | `web/src/shared/ui/hero.ts` | class constants (new, no Morabh equivalent) | `export const heroClassName = 'relative isolate flex flex-col gap-3 overflow-hidden rounded-lg bg-hero p-5 text-surface lg:p-8';` · `export const heroHaloClassName = 'pointer-events-none absolute -end-12 -bottom-12 -z-10 hidden size-48 rounded-full bg-surface/10 lg:block';` (bottom-end, lg only, so the halo never sits under text) |
| 2 | `web/src/shared/ui/circle.ts` | class constants | `export const circleTileClassName = 'inline-flex size-14 shrink-0 items-center justify-center rounded-full bg-bg font-display text-h2 font-extrabold text-accent-text';` · `export const circleBadgeClassName = 'inline-flex size-11 shrink-0 items-center justify-center rounded-full bg-bg text-label font-bold text-accent-text';` |
| 3 | `web/src/features/mastery/api/subjectInitial.ts` | pure function | `export function subjectInitial(name: string): string` → `const trimmed = name.trim(); const word = trimmed.length > 2 && trimmed.startsWith('ال') ? trimmed.slice(2) : trimmed; return (Array.from(word)[0] ?? '').toLocaleUpperCase();` |
| 4 | `web/src/features/mastery/api/subjectInitial.test.ts` | tests | T14–T17 |
| 5 | `.process/289-replace-the-theme-with-the-design-md-design-whol/layout-audit.js` | dev script (not shipped) | Copy of `.process/276-…/layout-audit.js` with the header comment's `(#276)` → `(#289)`, plus the additions in "Layout audit" §2. |
| 6 | `.process/289-replace-the-theme-with-the-design-md-design-whol/02-layout-audit.md` | audit record (implementer) | Format in "Layout audit" §4. |

### Component contracts
**button.tsx**
```ts
const actionFillClassName = 'bg-action text-text hover:not-disabled:not-active:bg-action-hover active:bg-action-pressed';
export const buttonVariants = cva(
  'inline-flex shrink-0 items-center justify-center gap-2 rounded-pill font-sans text-label font-bold whitespace-nowrap transition-colors focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 focus-visible:outline-hidden disabled:opacity-45',
  { variants: {
      variant: {
        primary: actionFillClassName,
        secondary: 'border-2 border-accent bg-transparent text-accent-text hover:not-disabled:not-aria-pressed:bg-soft active:bg-soft aria-pressed:bg-accent-soft',
        danger: 'border-2 border-danger bg-transparent text-danger hover:not-disabled:bg-danger-soft',
        ghost: 'bg-transparent text-text hover:not-disabled:bg-soft',
      },
      size: { default: 'min-h-11 px-5', sm: 'min-h-9 px-3', icon: 'size-11' },
    },
    defaultVariants: { variant: 'primary', size: 'default' } });
```
`Button` function unchanged.

**MasteryBar.tsx**: `export interface MasteryBarProps { percent: number; label: string; tone?: 'default' | 'onHero' }`. `const toneClassName = { default: 'bg-soft [&::-moz-progress-bar]:bg-accent [&::-webkit-progress-bar]:bg-soft [&::-webkit-progress-value]:bg-accent', onHero: 'bg-surface/30 [&::-moz-progress-bar]:bg-surface [&::-webkit-progress-bar]:bg-surface/30 [&::-webkit-progress-value]:bg-surface' } as const;` and `className={cn('h-1.5 w-full appearance-none overflow-hidden rounded-full', toneClassName[tone])}` (default `tone = 'default'`).

**HeadlineCounterCard.tsx** (props, data and i18n unchanged from #286):
```tsx
<section aria-label={t('headline.label')} className={heroClassName}>
  <span aria-hidden="true" className={heroHaloClassName} />
  <p className="font-display text-h1 font-extrabold text-balance lg:text-display-desktop">{…remaining…}</p>
  <MasteryBar tone="onHero" percent={masteredPercent} label={t('headline.barLabel')} />
  <dl className="flex flex-wrap gap-2">
    {stats.map((stat) => (
      <div key={stat.key} className="flex items-baseline gap-1.5 rounded-pill bg-surface px-3 py-1">
        <dt className="text-caption text-text-muted">{stat.label}</dt>
        <dd className="text-caption font-bold text-text">{stat.value}</dd>
      </div>))}
  </dl>
</section>
```

**LandingHero.tsx**
```tsx
<section aria-labelledby={headingId} className={cn(heroClassName, 'items-start')}>
  <span aria-hidden="true" className={heroHaloClassName} />
  <p className="text-caption font-bold">{t('hero.eyebrow')}</p>
  <h1 id={headingId} className="max-w-180 font-display text-display font-extrabold lg:text-display-desktop">{…unchanged…}</h1>
  <p className="max-w-120 text-ui text-surface">{t('hero.goal', { goal: marketedQuestionGoal })}</p>
  <Button asChild><Link to="/signup">{t('hero.start')}</Link></Button>
</section>
```
**SubscribeHeader.tsx**: `<header className={heroClassName}><span aria-hidden="true" className={heroHaloClassName} /><h1 className="font-display text-display font-extrabold lg:text-display-desktop">…</h1><p className="max-w-120 text-ui text-surface">…</p></header>`.

**SubjectMasteryCard.tsx**
```tsx
<article aria-labelledby={id} className="flex items-start gap-3 rounded-lg border border-border bg-surface p-4 shadow-1 lg:p-5">
  <span aria-hidden="true" className={circleTileClassName}>{subjectInitial(subject.name)}</span>
  <div className="flex min-w-0 flex-1 flex-col gap-2">{/* existing h3, MasteryBar, p unchanged */}</div>
</article>
```
**UnitListItem.tsx**: `export interface UnitListItemProps { unit: StudentUnitSummaryResult; ordinal: number }`.
```tsx
<li className="flex items-start gap-3 rounded-md border border-border bg-surface px-3.5 py-3 shadow-1">
  <span aria-hidden="true" className={circleBadgeClassName}>{ordinal}</span>
  <div className="flex min-w-0 flex-1 flex-col gap-2">{/* existing Link, MasteryBar, 2× p, Button unchanged; Button keeps self-start */}</div>
</li>
```

## Error codes
None. No api/ change.

## Domain behaviour
None. UI-only story.

## API surface
None. `npm run gen:api` must produce no diff.

## Token mapping (old → new; `.claude/design-system.md` → `tokens.css` → `app.css`)
| Token (CSS var) | Old | New | Tailwind / shadcn | Note |
|---|---|---|---|---|
| color.bg | #F4F5FB | **#F2F7FD** | `bg-bg`, `--background` | Canvas Mist |
| color.surface | #FFFFFF | #FFFFFF | `bg-surface`, `--card` | Paper White |
| color.soft | #E5E7FB | **#ECF2FA** | `bg-soft`, `--muted` | derived (D8) |
| color.text | #1E1B4B | **#252B2F** | `text-text`, `--foreground` | Charcoal |
| color.text.muted | #5F5D7A | **#666E7E** | `text-text-muted`, `--muted-foreground` | Slate Caption |
| color.border | rgba(30,27,75,.08) | **#E6ECF2** | `border-border` | hairline, derived |
| color.border.strong | rgba(30,27,75,.18) | **#D6DEE6** | `border-border-strong`, `--border`, `--input` | Steel |
| color.accent | #4F46E5 | **#116EEE** | `border-accent`, `bg-accent`, `fill-accent`, `accent-accent`, `--ring` | Signal Blue, non-text + logo |
| color.accent.text | — | **#0E64DA** (new) | `text-accent-text` | D2 |
| color.accent.hover / .pressed | #4338CA / #3730A3 | **deleted** | — | |
| color.accent.soft | #EEF0FF | **#EAF2FE** | `bg-accent-soft` | derived |
| color.action | — | **#11EE92** (new) | `bg-action`, `--primary` | Spring Mint |
| color.action.hover / .pressed | — | **#0FD683 / #0DBF75** (new) | `bg-action-hover/-pressed` | |
| color.success / .text | #16A34A / #166534 | unchanged | | exception |
| color.success.soft | #DFF3E5 | **#E6F6EB** | | D9 |
| color.danger | #C8233A | unchanged | `--destructive` | exception |
| color.danger.soft | #FDECEE | **#FEF1F2** | | D9 |
| color.warning / .soft | #B45309 / #FEF3E2 | unchanged | | exception |
| color.v2 | #6A3FB5 | unchanged | | |
| color.overlay | rgba(30,27,75,.40) | **rgba(37,43,47,.40)** | `bg-overlay` | charcoal-tinted |
| gradient.aurora | 135deg #4338CA→#A21CAF | **deleted** | `bg-aurora` deleted | |
| gradient.hero / .rtl | — | **linear-gradient(90deg,#3B1E90 0%,#5A3CC4 30%,#3A6EF0 100%)** / same at **270deg** | `bg-hero` | D10 |
| type.* | Readex/Noto, see old | D20 values | `text-*`, new `text-label` | |
| font | Readex Pro + Noto Sans Arabic | **Poppins + Almarai** | `font-sans`, `font-display` | |
| weights | 400/500/600/700 | **400/700/800** | `font-normal/bold/extrabold` | D17 |
| radius.sm / md / lg / pill | 10 / 14 / 18 / 999 | **5 / 16 / 16 / 45**; radius.circle 9999 (new, `rounded-full`) | `rounded-sm/md/lg/pill`, `--radius` = md | |
| shadow.1 | two-layer indigo | **rgba(0,0,0,.1) 0 1px 2px 0** | `shadow-1` | the only shadow |
| shadow.2 | two-layer | **deleted** | — | |
| layout.max | 1040 | **1200** | `max-w-layout` | |
| space.16 | — | **64** (new) | `gap-16` | section gap |
| everything else (space, gutter, card padding, bar 56, auth 440, motion, breakpoints) | | unchanged | | |

### AA contrast (WCAG 2.x, computed; T4–T8 re-verify)
| Foreground | Background | Ratio | Need | Use |
|---|---|---|---|---|
| text #252B2F | surface / bg / soft / accent.soft | 14.33 / 13.31 / 12.73 / 12.71 | 4.5 | body |
| text | success.soft / danger.soft / warning.soft | 12.79 / 13.02 / 13.06 | 4.5 | feedback panels |
| text.muted #666E7E | surface / bg / soft / accent.soft | 5.13 / 4.76 / 4.55 / 4.55 | 4.5 | captions, inactive tab bar |
| text.muted | success.soft / danger.soft / warning.soft | 4.58 / 4.66 / 4.67 | 4.5 | captions in panels |
| text (charcoal) | action / action.hover / action.pressed (mint) | 9.33 / 7.48 / 5.96 | 4.5 | primary button; **mint on charcoal = 9.33** |
| accent.text #0E64DA | surface / bg / soft / accent.soft | 5.45 / 5.06 / 4.84 / 4.83 | 4.5 | links, secondary label, active nav/tab, chips, circle tiles |
| accent #116EEE (logo only as text) | surface | 4.68 | 4.5 | logo on the white app bar |
| white | accent | 4.68 | 4.5 | "new reply" badge, diagram count pill |
| white | danger | 5.59 | 4.5 | — |
| success.text #166534 | surface / bg / success.soft | 7.13 / 6.62 / 6.37 | 4.5 | ok text and badge |
| danger #C8233A | surface / bg / danger.soft | 5.59 / 5.19 / 5.08 | 4.5 | error text, bad badge, danger button |
| warning #B45309 | surface / bg / warning.soft | 5.02 / 4.66 / 4.58 | 4.5 | partial, pending |
| v2 #6A3FB5 | surface | 7.02 | 4.5 | v2 badge |
| **white** | **hero stops #3B1E90 / #5A3CC4 / #3A6EF0** | **11.84 / 7.30 / 4.498** | 4.5 (3 large) | banners; body text never reaches the end stop (≥ 4.70 at 94 % width with 20 px padding) (D10) |
| accent (focus ring, secondary/selected/active borders) | surface / bg / accent.soft / soft | 4.68 / 4.34 / 4.15 / 4.15 | 3 | UI |
| danger (danger outline) | surface / bg | 5.59 / 5.19 | 3 | UI |
| success #16A34A (non-text) | surface / bg | 3.30 / 3.06 | 3 | borders, icons, pass fill |
| success | success.soft | 2.94 | — | **never** text or the sole indicator |
| mint | surface / bg / hero end | 1.54 / 1.43 / 2.93 | — | not a required pair: the label identifies the control (D12) |
| border.strong (steel) | surface / bg | 1.36 / 1.26 | — | decorative field boundary (D13) |
| border (hairline) | surface | 1.19 | — | decorative |

## Fonts: byte cost (woff2, fontsource 5.3.0, measured from the tarballs)
| Face | Bytes | Loads when |
|---|---|---|
| Almarai arabic 400 / 700 / 800 | 31,672 / 32,912 / 33,328 | Arabic text at that weight (800 only on landing, student home, subscribe, results, admin KPIs) |
| Poppins latin 400 / 700 / 800 | 7,884 / 7,816 / 7,824 | Latin letters, digits and punctuation (every page) |
| **Worst case per page** | **121,436 (118.6 KiB)** | typical admin page without 800: 80,284 |
| Old (Noto Sans Arabic arabic 400/500/600 + Readex Pro arabic+latin 500/600/700) | 231,472 (+ Noto latin) | |

`font-display: swap` (in every imported fontsource file). No preload (D16). **`perf:budget` does not count fonts**: `pageFiles` sums `chunk.file` + `chunk.css` only, and `.woff2`/`.woff` are manifest `assets`. The `@font-face` rules are counted, as part of the entry CSS, and drop from about 27 to 6. The implementer pastes `ls -l dist/assets/*.woff2` into the report.

## Bundle budgets (none may be raised)
Headroom before this story (performance.md §3, plus #288's assistant): entry 190/210, landing 199/220, lesson 221/240, **quiz 236/255 (19 KB)**, admin-dashboard 213/233, admin-users 245/270, teacher-home 246/265, assistant 245/260. The single Tailwind CSS file is on every page, so CSS growth hits every row. Expected net: about 0 or negative (fewer `@font-face` rules; `font-semibold`, `shadow-2`, `bg-aurora` and the accent hover/pressed utilities disappear; new utilities are `bg-action*`, `text-accent-text` variants, `border-2`, `text-label`, `font-extrabold`, `bg-hero`, `bg-surface/10|30` and circle/hero sizes). No JS is added. If any page goes over: `npm run perf:budget -- --breakdown=<page>`, paste it, and stop with `BLOCKED: perf budget <page> <n>/<max>`.

## Layout audit
### 1. Route matrix
Widths 375, 768, 1280, each in `ar` and `en`, for public, student, teacher and admin. The routes are those of #276 `01-plan.md` "Alignment audit §1", plus `/student/assistant` and one discovered `/student/assistant/:id` (#288), and `/student/multi-exam` with two units chosen (#286 option cards). Run against the demo as in #276.

### 2. Additions to `layout-audit.js` (inside `checks`, and in the exported object)
```js
    primary() {
      const mint = [...document.querySelectorAll('[data-slot="button"]')].filter((el) => visible(el) && css(el).backgroundColor === 'rgb(17, 238, 146)');
      return mint.length > 1 ? mint.map((el) => finding('primary-count', el, `${mint.length} mint buttons on the page`)) : [];
    },
    hero() {
      const out = [];
      for (const el of document.querySelectorAll('.bg-hero')) {
        if (!visible(el)) continue;
        const s = css(el), padEnd = parseFloat(rtl() ? s.paddingLeft : s.paddingRight);
        if (!s.backgroundImage.startsWith(`linear-gradient(${rtl() ? 270 : 90}deg`)) out.push(finding('hero-direction', el, s.backgroundImage.slice(0, 40)));
        if (padEnd < 20 - TOL) out.push(finding('hero-padding', el, `inline-end padding ${padEnd}px < 20`));
      }
      return out;
    },
```
And `const fonts = () => [...document.fonts].filter((f) => f.status === 'loaded').map((f) => `${f.family} ${f.weight}`);`, exported as `window.__layoutAudit = { run, audit, go, setLang, links, checks, fonts };`.

### 3. Static checks (from `web/`, output pasted into `02-layout-audit.md`)
| Check | Command | Pass |
|---|---|---|
| no stray weights | `grep -rnE '\bfont-(semibold\|medium\|light\|black\|thin)\b' src --include=*.ts --include=*.tsx` | empty |
| accent text only on the logo | `grep -rnP '(?<![-\w])text-accent(?![-\w])' src --include=*.ts --include=*.tsx` | only `shared/ui/layout.ts` |
| old theme gone | `grep -rnE 'shadow-2\|accent-hover\|accent-pressed\|aurora\|Readex\|Noto Sans\|variant="accent"' src scripts` | empty |
| gradient in 3 places | `grep -rln 'heroClassName' src --include=*.tsx` | `HeadlineCounterCard.tsx`, `LandingHero.tsx`, `SubscribeHeader.tsx` |
| one family | `grep -n fontsource src/main.tsx` | exactly the 6 imports of D15 |

### 4. Record (`02-layout-audit.md`)
Same format as #276 §4: (1) environment (commit, demo URL, accounts, `__layoutAudit.fonts()` on `/student` ar and `/admin` en, which must list `Almarai 400`, `Almarai 700`, `Poppins 400`, `Poppins 700`); (2) matrix `role · width · lang · routes · findings before · after`; (3) findings `# · route · width · dir · check · target · detail · fix (file:line) · status`; (4) static checks; (5) **visual check against `.process/design-sample-busuu.html`** for `/student` at 1280 ar (banner, mint CTA, circles, cards, nav). Columns: `element · sample · app · intended?`. Allowed fixes follow #276 §4's table, plus: `primary-count` → demote the non-main action to `variant="secondary"` (or mark intended with the reason, for example the same action repeated per row); `hero-*` → fix in `hero.ts`/`app.css` only. Every extra file touched is listed in (3).

## Docs content
**§A `.claude/design-system.md`** (rewrite; keep the section headings `### Colour`, `### Typography`, `### Spacing`, `### Radius · Elevation · Motion`, `## Breakpoints & layout`; the generator parses them).
- Title `# Design system — Elmanhg (المنهج) · "Mist" (v3.0)`. Source line: `docs/design-system.md` v3.0 (2026-10-05), derived from `docs/design-source.md` (DESIGN.md). Light only.
- `### Colour` rows (`Token | CSS var | Value | Use`) exactly: color.bg `#F2F7FD` "canvas mist: page ground"; color.surface `#FFFFFF` "cards, sheets, inputs, app bar, tab bar"; color.soft `#ECF2FA` "muted chips, pending and role badges, progress track, skeletons, secondary and ghost hover, user bubbles"; color.text `#252B2F` "charcoal: primary text; never a fill"; color.text.muted `#666E7E` "slate: captions, labels, meta, inactive tab bar"; color.border `#E6ECF2` "hairline on cards, rows, dividers, tab bar"; color.border.strong `#D6DEE6` "steel: inputs, selects, option outlines, inactive pill tabs"; color.accent `#116EEE` "Signal Blue: logo, secondary/selected/active borders, focus ring, progress fill, chart bars, checkbox accent-color, white-text badges; never a filled action, never body-size text except the logo"; color.accent.text `#0E64DA` "Signal Blue text shade: links, secondary and active labels, chips, circle tiles"; color.accent.soft `#EAF2FE` "active nav and pill tab fill, selected option fill, toggled secondary, accent chips"; color.action `#11EE92` "Spring Mint: primary button fill only, one per screen"; color.action.hover `#0FD683`; color.action.pressed `#0DBF75`; success `#16A34A`, success.text `#166534`, success.soft `#E6F6EB`, danger `#C8233A`, danger.soft `#FEF1F2`, warning `#B45309`, warning.soft `#FEF3E2` (Use texts as v2.2, each suffixed "(status exception)"); v2 `#6A3FB5`; overlay `rgba(37,43,47,.40)`; gradient.hero `--ds-gradient-hero` `linear-gradient(90deg,#3B1E90 0%,#5A3CC4 30%,#3A6EF0 100%)` "landing hero, student home banner, subscribe header ONLY"; gradient.hero.rtl `--ds-gradient-hero-rtl` `linear-gradient(270deg,#3B1E90 0%,#5A3CC4 30%,#3A6EF0 100%)` "the same, mirrored for RTL (bg-hero picks it)".
- `### Typography` rows: the D20 table, Family `Poppins / Almarai` (mono: `system monospace`), Size column `36/41 · 40/46` etc. without tracking. Fonts line: "`@fontsource/poppins` latin 400/700/800 and `@fontsource/almarai` arabic 400/700/800, self-hosted, `font-display: swap`, no preload. Stack `'Poppins', 'Almarai', Tahoma, sans-serif`. Only these two families. Arabic running text never below 14 px; lesson text and inputs 16 px. Weights 400/700/800 only."
- `### Spacing`: as v2.2 with `layout.max | 1200` and a new row `space.16 | 64 (section gap, landing page)`.
- `### Radius · Elevation · Motion`: radius.sm 5 (inputs, selects, textareas, text-link focus outline); radius.md 16 (options, list items, feedback panel, menus, bubbles); radius.lg 16 (cards, sheets, dialogs, hero banners); radius.pill 45 (buttons, badges, pill tabs, nav pills, chips); radius.circle 9999 (subject, unit and icon tiles: `rounded-full`); `shadow.1 | rgba(0,0,0,.1) 0 1px 2px 0 — the only shadow: cards, tiles, app bar, menus, dialogs, sheets, toasts, assistant button`; motion rows unchanged.
- Components table: v2.2 rows (including #286's QuizOption/option card, CompactList and legend rule, and #288's AssistantPage), rewritten with: Button (primary mint + charcoal label 700 14 px, hover action.hover, pressed action.pressed; secondary 2 px Signal Blue outline + accent.text, hover soft, aria-pressed accent.soft; ghost charcoal, hover soft; danger 2 px danger outline + danger text, hover danger.soft (exception); no `accent` variant; padding-inline 20 (12 sm); one mint per screen); Card (surface, hairline, shadow.1, radius.lg 16); Badge (pill, micro 12/700); AppBar (sticky white, shadow.1, no bottom border, logo Signal Blue h2 700); TopNav (inactive charcoal label 700, hover soft, active accent.soft + accent.text); TabBar (active accent.text 700 on an accent.soft pill); PillTab (label 700, active accent.soft + accent border + accent.text); AssistantFab (white pill, 2 px accent outline, accent.text, shadow.1); AssistantSheet and Dialog (shadow.1, radius.lg 16); **HeroBanner** (new row: `bg-hero`, radius.lg, padding 20 / 32 lg, white text, display 800 headline, body max 480, optional mint CTA, decorative surface/10 halo circle at bottom-end from lg; `shared/ui/hero.ts`); **CircleTile** (new row: 56 px mist circle with accent.text initial or icon; 44 px badge for ordinals; `shared/ui/circle.ts`, aria-hidden); Progress `onHero` tone (white fill on a white/30 track); KpiTile stat 24/800; Table (label/caption type, hairline rows, `sm` outline row actions; **admin data views exception**).
- Rules (replace 1–11): 1 "Mint = the one primary action per screen. Signal Blue = brand, link, current place, selection, focus, progress, charts. Green/red/amber = status (documented exception). Nothing else is coloured." 2 status-text rule (unchanged wording). 3 "The hero gradient appears only on the landing hero, the student home banner and the subscribe header; never on lesson, quiz, exam, teacher or admin screens. White text keeps ≥ 20 px from the gradient's end edge." 4–10 as v2.2. 11 "Charcoal is never a fill. Signal Blue is never a filled action. Mint is only the primary button fill." 12 "Circles for subject, unit and icon tiles; pills for buttons, tabs, badges and chips; 16 px cards; one shadow." 13 "Admin data views (tables, KPI tiles, charts; also the student's history and progress tables) are an allowed exception to DESIGN.md's 'no dense data tables'."
- `## Breakpoints & layout`: unchanged rows, `layout.max | 1200`.
- Accessibility: replace the table with the AA table above. Touch-target text unchanged.
- RTL rules: unchanged, plus "the hero gradient mirrors in RTL (deep indigo at inline-start)".
- Token → code mapping: app.css snippet with the new `@theme inline` lines and the `@theme` weight reset; shadcn `--primary` → color.action, `--primary-foreground` → color.text, `--radius` → radius.md; "`bg-hero` is the only gradient utility, used through `heroClassName` by exactly three components".
- Change log: keep rows 1.0–2.2, add `| 3.0 | 2026-10-05 | #289: Mist design from DESIGN.md (docs/design-source.md): mint primary, Signal Blue accent and accent.text, mist canvas, steel borders, one shadow, radii 5/16/45/circle, Poppins + Almarai 400/700/800, type scale 12–40, hero gradient banners, circle tiles, layout.max 1200; status colours and danger kept as exceptions; admin data views exception |`.

**§B `docs/design-system.md`** (gives the same answers as §A). Header table: Version 3.0, Date 2026-10-05, Decision "Mist design from DESIGN.md for the whole app (#289), replacing Glass / Indigo calm", Mode light only, Source `docs/design-source.md`. Drop the old Reference canvas row. New **§0 Source and resolved contradictions**, listing: primary = mint fill, charcoal 700 label, pill, one per screen (component spec, Do's and signature choices win over the two contrary lines); secondary = Signal Blue outline; tertiary = charcoal ghost; destructive = red outline (no danger colour in DESIGN.md: accessibility and safety exception); status colours kept for correct/wrong/partial and state badges, with AA; `#3b1e9` = `#3b1e90`; Nista → Poppins + Almarai (D14 rationale in one paragraph); accent.text and derived tints (D2, D8); admin data views exception. §1 principles rewritten (mist canvas and white cards; one mint action; Signal Blue for brand and place; circles, pills, 16 px cards, one shadow; touch first, unchanged). §2.1–2.2 the token values under the doc's names (`--bg`, `--surface`, `--soft`, `--text`, `--text-2`, `--border`, `--border-strong`, `--accent`, `--accent-text` (new), `--accent-soft`, `--action`/`--action-hover`/`--action-pressed` (new), `--ok`/`--ok-text`/`--ok-soft`, `--bad`/`--bad-soft`, `--warn`/`--warn-soft`, `--v2`). §2.3 becomes "Hero gradient" (`--hero` + RTL twin, allowed places, the 20 px rule). §2.4 the AA table above. §3 fonts table (Poppins Latin / Almarai Arabic, 400/700/800, fallback Tahoma) and §3.1 scale (D20). §3.2 numerals and dates unchanged. §4 spacing/radius/elevation values (max width 1200, radii 5/16/16/45/circle, one shadow). §5.1 buttons table (Primary mint / charcoal; Secondary transparent, `--accent-text`, 2 px `--accent`; Danger transparent, `--bad`, 2 px `--bad`; Ghost none, `--text`; no Accent row). §5.2 cards 16 px with hairline and the one shadow. §5.3 quiz/option card (radius 16, steel, selected `--accent-soft` + `--accent`). §5.5 badges (micro 12/700). §5.6 progress (+ onHero tone; the headline counter is now the hero banner). §5.7 navigation (white sticky bar with the subtle shadow, Signal Blue logo, charcoal 700 nav items, active `--accent-soft` + `--accent-text`; sign-out ghost). §5.8 inputs (radius 5, steel, 16 px). §5.9 tables + "Admin data views exception". §5.10 assistant (FAB white outline pill; panel and page cards with the one shadow). §5.11 dialogs (radius 16, overlay `rgba(37,43,47,.40)`, shadow-1). §5.12–5.15 unchanged except radius and shadow names. New §5.16 hero banner and §5.17 circle tiles. §6 add `layout.max 1200` and the RTL gradient mirror. §9 do/don't rewritten (one mint; gradient only in the three places; no dense tables outside admin data views; no second chromatic action; weights 400/700/800). §10 implementation notes: tokens are generated, `--primary` = action, fonts from fontsource.

**§C `docs/claude-design-prompt.md`.** §0 table "Glass" → "Mist". §1 fonts bullet → "Fonts self-hosted or via Google Fonts `<link>`: **Poppins** (400, 700, 800) for Latin and **Almarai** (400, 700, 800) for Arabic, stack `'Poppins', 'Almarai', Tahoma, sans-serif`." §2.1 CSS block → the new variables (`--bg`, `--surface`, `--soft`, `--text`, `--text-2`, `--border`, `--border-strong`, `--accent`, `--accent-text`, `--accent-soft`, `--action`, `--action-hover`, `--action-pressed`, the status set, `--v2`, `--hero` with its comment) with one-line comments from §A. Colour rules → §A rules 1–3 and 11. §2.2 table → D20 sizes with Poppins/Almarai and weights 400/700/800; "Arabic running text never below 14 px". §2.3 → base 4, gutter 16/24, max 1200, card padding 16/20, radii 5/16/16/45/circle, the one shadow, cards white with hairline. §2.4: Buttons (mint primary with charcoal 700 14 px label; table: Primary `--action`/`--text`; Secondary transparent/`--accent-text`/2 px `--accent`; Danger transparent/`--bad`/2 px `--bad`; Ghost none/`--text`; delete the Accent row; padding 20 / 12 sm); Quiz option radius 16 with steel border; Badges micro 12/700; Progress + banner; Navigation (white sticky bar with the subtle shadow, Signal Blue logo, charcoal 700 items, active `--accent-soft` fill with `--accent-text`, max 1200); Inputs (radius 5, steel, 16 px); Tables (+ admin data views exception); Assistant panel (white outline FAB, one shadow); Dialogs (radius 16, overlay `rgba(37,43,47,.40)`); new **Hero banner** and **Circle tiles** paragraphs (from §A). §4 Student `#/student` line: "headline counter card" → "headline banner on the hero gradient"; subscription line "Aurora gradient header" → "hero gradient header"; landing line "Aurora gradient hero" → "hero gradient banner with a mint «ابدأ الآن»-style primary"; subject page "units in order" → "units in order, each with its number in a circle"; home subject cards "with the subject's initial in a circle". §6: list screens "title 15 px 600" → "title 16 px 700"; quiz screen "stem 17 px 600 with 1.6" → "stem 16 px 700 with 1.7"; dashboard "Readex Pro 26 px value" → "24 px 800 value". §8: fonts bullet → "Use only Poppins and Almarai."; gradient bullet → "No gradients, glass blur or decorative illustrations except the hero banners (landing, student home, subscribe)." The #288 chat-page bullet in §6 stays (radius and shadow words follow §A).

**§D `docs/prototype.md` line 12** → "The prototype keeps this grey wireframe look and its two-row top bar with the persona switchers. The product's look (the Mist design from DESIGN.md: mint primary button, Signal Blue accents, hero gradient banners, circle tiles, single-row app bar) comes from `docs/design-system.md`."

**§E `docs/performance.md`.** §3 Measured column ← this run (budgets unchanged), and the paragraph under the table adds "#289 (theme) did not raise any budget." §4 add the bullet "**Two font families, few faces.** Poppins (latin subset) and Almarai (arabic subset) at 400/700/800: ≤ 119 KB of woff2 per page (old fonts about 226 KB), `font-display: swap`, not preloaded. The woff2 files are assets, so `perf:budget` does not count them; their `@font-face` rules (6) are in the entry CSS." Line 194: append "(replaced by Poppins and Almarai in #289, see §4)".

**§F `docs/design-source.md` header** (prepend):
```
> Verbatim style reference (DESIGN.md) supplied by the product owner on 2026-10-05. It is the source for
> `docs/design-system.md` v3.0, which records how its contradictions were resolved (§0) and which
> exceptions apply. Do not edit the reference below; change the design system instead.
```

## Test plan
| # | Test class | Test method | Asserts |
|---|-----------|-------------|---------|
| T1 | `contrast` (contrast.test.ts) | `returns 21 for black on white` | unchanged |
| T2 | `contrast` | `is symmetric and 1 for identical colours` | unchanged |
| T3 | `contrast` / `readToken` | `throws on a colour that is not #RRGGBB`, `throws when a token is missing` | unchanged |
| T4 | `design tokens` (**modify** `textPairs`) | `it.each(textPairs)('%s on %s meets 4.5:1')` | text and text-muted on surface, bg, soft, accent-soft, success-soft, danger-soft, warning-soft; text on action, action-hover, action-pressed; accent-text on surface, bg, soft, accent-soft; surface on accent, danger; success-text on surface, bg, success-soft; danger on surface, bg, danger-soft; warning on surface, bg, warning-soft; v2 on surface. Remove the accent-hover/pressed and old accent-text-on rows |
| T5 | `design tokens` (**modify** `uiPairs`) | `it.each(uiPairs)('%s on %s meets 3:1')` | success on surface, bg; accent on surface, bg, soft, accent-soft; danger on surface, bg |
| T6 | `design tokens` (**modify**, replaces the aurora test) | `keeps white text readable on the hero gradient stops` | `gradientStops(readToken(css,'gradient-hero'))` = `['#3B1E90','#5A3CC4','#3A6EF0']`; stops 0 and 1 vs `#FFFFFF` ≥ 4.5; stop 2 ≥ 3 (comment: large text only, D10) |
| T7 | `design tokens` (new) | `mirrors the hero gradient for right-to-left on the same stops` | `readToken(css,'gradient-hero-rtl')` starts with `linear-gradient(270deg` and its stops equal the T6 stops |
| T8 | `design tokens` (new) | `keeps Signal Blue itself below 4.5 on the canvas so text uses accent-text` | `contrastRatio(colour('accent'), colour('bg'))` < 4.5 and `contrastRatio(colour('accent-text'), colour('bg'))` ≥ 4.5 (guards D2) |
| T9 | `Button` (button.test.tsx, **modify**) | `defaults to type button…`, `renders its child as the element when asChild is set`, `counts pseudo-classes inside :not() towards specificity` | unchanged |
| T10 | `Button` (**modify** the `it.each` rows; `variants` = `['primary','secondary','danger','ghost']`) | `resolves the %s fill from the compiled CSS` | primary at rest → `fill('action')`; primary hovered → `action-hover`; primary pressed → `action-pressed`; disabled primary hovered → `action`; secondary at rest → `'transparent'`; secondary hovered → `soft`; toggled secondary hovered → `accent-soft`; toggled secondary pressed → `accent-soft`; disabled secondary hovered → `'transparent'`; disabled toggled secondary → `accent-soft`; danger at rest → `'transparent'`; danger hovered → `danger-soft`; disabled danger hovered → `'transparent'`; ghost hovered → `soft`; disabled ghost hovered → `'transparent'`. The "accent pressed" row is deleted |
| T11 | `Button` (**modify**) | `keeps the hover fill on an asChild link` | expects `fill('action-hover')` |
| T12 | `StudentHomePage` (**add**) | `shows each subject's initial in a circle beside its name` | after load, `within(screen.getByRole('article', { name: 'Physics' })).getByText('P')` and `…'Chemistry'… getByText('C')` are in the document |
| T13 | `SubjectPage` (**add**) | `numbers the units in order` | after load, `screen.getAllByText(/^[12]$/).map((el) => el.textContent)` equals `['1','2']` |
| T14 | `subjectInitial` (new file) | `drops the Arabic definite article` | `subjectInitial('الرياضيات')` = `'ر'` |
| T15 | `subjectInitial` | `uppercases the first Latin letter` | `subjectInitial('physics')` = `'P'` |
| T16 | `subjectInitial` | `ignores surrounding spaces and keeps a bare article` | `subjectInitial('  ال  ')` = `'ا'`; `subjectInitial(' Chemistry')` = `'C'` |
| T17 | `subjectInitial` | `returns an empty string for an empty name` | `subjectInitial('')` = `''` |

Existing tests with class or colour assertions keep passing unchanged: `BlueprintEditor` (`bg-warning-soft`), `ExamPage` (`text-danger`), `FeedbackPanel` (`border-warning`), `TopNavMore` (`group-has-[[data-status=active]]:bg-accent-soft`: S1/S3 only touch `font-semibold`/`text-accent` in that string). `generateTokensCss.test.ts` uses its own fixtures and is unchanged. Every existing axe test must still pass (the circles are `aria-hidden`). No other test is edited, skipped or deleted.

## Risks
| Risk | Mitigation |
|---|---|
| Wider text (Poppins EN, 14/700 labels) overflows the single-row nav at 900 px | The nav `ul` keeps `overflow-x-auto` (#276). The audit's header and overflow checks at 768/1280, plus a manual check at 900, are in the record. |
| More than one mint button on some screens | The audit's `primary` check, with the allowed demotion to secondary. |
| `@utility` with a nested `&:where(...)` | Tailwind 4.3 supports nested selectors in `@utility`. The audit's `hero-direction` check proves it renders in both directions. |
| Lane merge leaves strings the scripts miss | The scripts run after the rebase; the static greps are the gate. |
| CI literal grep | No hex in features. All values live in tokens or `app.css` theme mapping (weights are numeric theme keys, as before). |

## Definition of done
- [ ] Rebased on main containing #286 and #288. Scripts S1–S5 ran, then prettier.
- [ ] `.claude/design-system.md` and `docs/design-system.md` (v3.0 "Mist") give the same value for every token in the mapping table. Both hold the AA table, the resolved contradictions, the status/danger exceptions and the admin data views exception. Change log 3.0.
- [ ] `npm run gen:tokens` produces no further diff. `tokens.css` has `--ds-color-bg: #F2F7FD`, `--ds-color-accent: #116EEE`, `--ds-color-accent-text: #0E64DA`, `--ds-color-action: #11EE92`, `--ds-gradient-hero`, `--ds-gradient-hero-rtl`, `--ds-radius-pill: 45px`, `--ds-layout-max: 1200px`, `--ds-type-label-size: 14px`, and no `aurora`, `accent-hover` or `shadow-2`.
- [ ] `app.css`: weight reset (400/700/800), new colour and label mappings, font stack Poppins/Almarai, `--primary` = action, `bg-hero` with the RTL twin, `bg-aurora` gone.
- [ ] `package.json`: `@fontsource/poppins` and `@fontsource/almarai` 5.3.0. Readex/Noto removed. Lockfile updated. `main.tsx` has exactly the 6 subset imports.
- [ ] Button: mint primary (charcoal label), blue outline secondary, charcoal ghost, red outline danger, no `accent` variant. The 5 former callers are updated as listed.
- [ ] Hero banner on the landing hero, the student home headline and the subscribe header (`heroClassName`). Circle tiles on subject cards, units and landing value icons.
- [ ] App bar is white with the one shadow, Signal Blue logo and ghost sign-out. Nav items charcoal 700, active accent.soft. FAB is a white outline pill.
- [ ] Every static check in "Layout audit" §3 passes as specified.
- [ ] `02-layout-audit.md` covers every role × 375/768/1280 × ar/en, with zero unexplained findings, the fonts line, and the visual check against the sample.
- [ ] Tests T1–T17 exist with those names and pass. Only `contrast.test.ts` and `button.test.tsx` are modified, and only the two page tests gain a test.
- [ ] `npm run typecheck`, `lint`, `format:check`, `test -- --run --coverage`, `build` and `perf:budget` all exit 0. The budget lines are pasted, with no `budgets.json` change (quiz ≤ 255, lesson ≤ 240, entry ≤ 210, landing ≤ 220, every other page within its budget). `ls -l dist/assets/*.woff2` is pasted. `gen:api` produces no diff.
- [ ] Docs §C–§F applied. README, constitution, react-feature skill and security.md say Mist / Poppins / Almarai. No doc outside `docs/design-source.md` still names Readex Pro, Noto Sans Arabic, aurora, Indigo calm or `#4F46E5` as current.
