# Design system — Elmanhg (المنهج) · "Mist" (v3.0)

Owner: product (Mohamed). Engineers do not edit values here; they reference tokens. Human-readable rationale lives in `docs/design-system.md`; this file is the token contract the pipeline reads. Both must agree (docs-sync rule).

Source of truth: `docs/design-system.md` v3.0 (2026-10-05), derived from `docs/design-source.md` (DESIGN.md). No Figma. Screen content, flow and states come from `prototype/`; the look comes only from this file.
Mode: **light only**. There is no dark theme and no `.dark` override block.

## Tokens

### Colour
| Token | CSS var | Value | Use |
|-------|---------|-------|-----|
| color.bg | --ds-color-bg | #F2F7FD | canvas mist: page ground |
| color.surface | --ds-color-surface | #FFFFFF | cards, sheets, inputs, app bar, tab bar |
| color.soft | --ds-color-soft | #ECF2FA | muted chips, pending and role badges, progress track, skeletons, secondary and ghost hover, user bubbles |
| color.text | --ds-color-text | #252B2F | charcoal: primary text; never a fill |
| color.text.muted | --ds-color-text-muted | #666E7E | slate: captions, labels, meta, inactive tab bar |
| color.border | --ds-color-border | #E6ECF2 | hairline on cards, rows, dividers, tab bar |
| color.border.strong | --ds-color-border-strong | #D6DEE6 | steel: inputs, selects, option outlines, inactive pill tabs |
| color.accent | --ds-color-accent | #116EEE | Signal Blue: logo, secondary/selected/active borders, focus ring, progress fill, chart bars, checkbox accent-color, white-text badges; never a filled action, never body-size text except the logo |
| color.accent.text | --ds-color-accent-text | #0E64DA | Signal Blue text shade: links, secondary and active labels, chips, circle tiles |
| color.accent.soft | --ds-color-accent-soft | #EAF2FE | active nav and pill tab fill, selected option fill, toggled secondary, accent chips |
| color.action | --ds-color-action | #11EE92 | Spring Mint: primary button fill only, one per screen |
| color.action.hover | --ds-color-action-hover | #0FD683 | hover on the primary button |
| color.action.pressed | --ds-color-action-pressed | #0DBF75 | pressed primary button |
| color.success | --ds-color-success | #16A34A | non-text only: borders, icons on surface, pass progress fill, diagram key stroke (status exception) |
| color.success.text | --ds-color-success-text | #166534 | success text, ok badge text, verdict icon on success.soft (status exception) |
| color.success.soft | --ds-color-success-soft | #E6F6EB | correct feedback background, ok badge fill (status exception) |
| color.danger | --ds-color-danger | #C8233A | wrong, rejected, overdue, destructive: text, border, fill (status exception) |
| color.danger.soft | --ds-color-danger-soft | #FEF1F2 | wrong feedback background, bad badge fill, danger button hover (status exception) |
| color.warning | --ds-color-warning | #B45309 | partial credit, pending review (status exception) |
| color.warning.soft | --ds-color-warning-soft | #FEF3E2 | partial feedback background (status exception) |
| color.v2 | --ds-color-v2 | #6A3FB5 | v2 badge only |
| color.overlay | --ds-color-overlay | rgba(37,43,47,.40) | dialog backdrop |
| gradient.hero | --ds-gradient-hero | linear-gradient(90deg,#3B1E90 0%,#5A3CC4 30%,#3A6EF0 100%) | landing hero, student home banner, student progress summary, subscribe header ONLY |
| gradient.hero.rtl | --ds-gradient-hero-rtl | linear-gradient(270deg,#3B1E90 0%,#5A3CC4 30%,#3A6EF0 100%) | the same, mirrored for RTL (bg-hero picks it) |

### Typography
| Token | Family | Size / line (mobile · desktop) | Weight | Use |
|-------|--------|-------------------------------|--------|-----|
| type.display | Poppins / Almarai | 36/41 · 40/46 | 800 | hero banners, headline banner (from lg; type.h1 below lg), result scores |
| type.h1 | Poppins / Almarai | 24/31 · 36/41 | 700 | page title |
| type.h2 | Poppins / Almarai | 18/27 · 24/31 | 700 | section, card title, logo |
| type.h3 | Poppins / Almarai | 16/24 | 700 | sub-section |
| type.body | Poppins / Almarai | 16/27 | 400 (question stem 700) | lesson text, question stem |
| type.ui | Poppins / Almarai | 16/24 | 400 | inputs, options, list titles |
| type.label | Poppins / Almarai | 14/21 | 700 | buttons, nav items, pill tabs, field labels |
| type.caption | Poppins / Almarai | 14/21 | 400 | meta, helper text |
| type.micro | Poppins / Almarai | 12/18 | 700 | badges, tab bar labels |
| type.stat | Poppins / Almarai | 24/31 | 800 | KPI numbers |
| type.mono | system monospace | 12/18 | 400 | LaTeX source, code, ids (always `dir="ltr"`) |

Fonts: `@fontsource/poppins` latin 400/700/800 and `@fontsource/almarai` arabic 400/700/800, self-hosted, `font-display: swap`, no preload. Stack `'Poppins', 'Almarai', Tahoma, sans-serif`. Only these two families. Arabic running text never below 14 px; lesson text and inputs 16 px. Weights 400/700/800 only. Tracking normal everywhere.

### Spacing (4-pt grid)
| Token | px |
|-------|----|
| space.1 | 4 |
| space.2 | 8 |
| space.3 | 12 |
| space.4 | 16 |
| space.5 | 20 |
| space.6 | 24 |
| space.8 | 32 |
| space.10 | 40 |
| space.12 | 48 |
| space.16 | 64 (section gap, landing page) |
| layout.gutter | 16 mobile · 24 desktop |
| layout.card.padding | 16 mobile · 20 desktop |
| layout.stack.gap | 12 (between stacked cards and grid cells) |
| layout.max | 1200 |
| layout.bar | 56 (app bar and mobile tab bar height) |
| layout.auth.max | 440 (auth column) |

### Radius · Elevation · Motion
| Token | Value |
|-------|-------|
| radius.sm | 5 (inputs, selects, textareas, text-link focus outline) |
| radius.md | 16 (options, list items, feedback panel, menus, bubbles) |
| radius.lg | 16 (cards, sheets, dialogs, hero banners) |
| radius.pill | 45 (buttons, badges, pill tabs, nav pills, chips) |
| radius.circle | 9999 (subject, unit and icon tiles: `rounded-full`) |
| shadow.1 | rgba(0,0,0,.1) 0 1px 2px 0 — the only shadow: cards, tiles, app bar, menus, dialogs, sheets, toasts, assistant button |
| motion.fast | 150ms cubic-bezier(.2,.8,.2,1) — hover, focus |
| motion.base | 220ms cubic-bezier(.2,.8,.2,1) — panels, dialogs |
| motion.slow | 400ms cubic-bezier(.2,.8,.2,1) — progress fill |

## Components
| Component | Variants | States | Notes |
|-----------|----------|--------|-------|
| Button | primary (mint action fill, charcoal label; hover action.hover, pressed action.pressed), secondary (transparent, 2 px accent outline, accent.text label; hover soft; aria-pressed accent.soft), ghost (transparent, charcoal label; hover soft), danger (transparent, 2 px danger outline, danger label; hover danger.soft; status exception). No `accent` variant | default, hover, focus, pressed, disabled (45% opacity, no hover fill), loading | pill (radius.pill), type.label 14/700, min height 44 (36 for `sm` table row actions, a documented exception to the 44 rule), padding-inline 20 (12 sm); size `icon` 44×44 for icon-only buttons beside fields (offset `mt-6.5` under a label). **One mint per screen.** Cursor pointer on enabled buttons/selects/[role=button], not-allowed on disabled (global base rule). Row actions in every table are `sm` (36): secondary, destructive `danger`; row links use `Button asChild`. |
| Card | default | default, hover (border.strong) for clickable cards | surface, hairline border, shadow.1, radius.lg 16. No coloured side borders ever. |
| QuizOption | radio, checkbox | default, hover (soft), selected (accent.soft fill + accent border), correct (success.soft + success), wrong (danger.soft + danger), disabled | full-width label, min height 48, radius.md 16, steel border, padding 12×14, gap 10, control 18px with accent-color accent; also the option card for choice groups (multi-exam units and sizes), disabled 45% opacity no hover, `shared/ui/optionCard.ts` |
| FeedbackPanel | correct, wrong, partial | enter (fade, motion.base) | soft bg + strong border of the verdict colour, 26px filled circle icon, bold verdict, explanation in text.muted |
| Badge | ok, bad, pending, role, v2, neutral | — | pill, type.micro 12/700, padding 2×10. ok = success.soft + success.text; bad = danger.soft + danger; pending/neutral = soft + text.muted; role = soft + text; v2 = outline |
| Progress | mastery (accent), pass (success), onHero (white fill on a white/30 track) | — | height 6, track soft, radius.pill, fill animates motion.slow; the headline counter is the student home HeroBanner (and the same banner tops `/student/progress`): sentence h1 below lg / display from lg with balanced wrap, mastered-share bar (onHero), 3 stat chips (surface pill, caption label text.muted, 700 value text), rows gap 12 |
| Input / Select / Textarea | default | default, focus (2px accent ring, offset 2), error (danger border + caption), disabled | surface, steel border.strong, radius.sm 5, height 44, padding 9×12, type.ui 16; label type.caption above with gap 6. Select: appearance none, Lucide ChevronDown 16 px text.muted in a 40 px inline-end box, padding-inline-end 40, not mirrored (`shared/ui/select.tsx`); fieldset legend is a block label inside the group (global base rule floats it), never on the border |
| TabBar (mobile) | — | active (accent.text 700 on an accent.soft icon pill), inactive (text.muted) | at most 4 items; a role with more destinations shows 3 + "المزيد" (list of the rest); Lucide icons 22px stroke 1.8, label type.micro, surface + top hairline; bottom padding env(safe-area-inset-bottom) |
| AppBar | — | — | single row, height layout.bar, sticky white surface with shadow.1 (no bottom border), same container as main: logo (Signal Blue accent, type.h2 700) · nav · role badge · name (≥ xl) · sign-out (ghost; label ≥ xl; min height 44 below lg, 36 `sm` from lg) |
| TopNav (desktop) | — | inactive (charcoal text, label 700), hover (soft), active (accent.soft + accent.text) | pills min-h 36; destinations beyond the role's top-bar list sit in an «المزيد» disclosure menu: surface, border, shadow.1, radius.md, items ≥ 44 px, Esc/outside click/focus leaving closes |
| BrandBar | — | — | app bar with logo only (+ end slot) on landing, login, sign-up, accept-invite, onboarding |
| PillTab | — | inactive (surface + steel border.strong), hover (soft), active (accent.soft + accent border + accent.text) | filters, sub-tabs, segmented toggles; pill, type.label 700, min height 44 |
| Table | — | row hover (soft) | inside a Card, no vertical rules, row hairline, header type.caption 700 text.muted, cell type.caption, padding 9×10, sticky header on desktop, horizontal scroll inside the card on mobile; row actions `sm` outline; activity dates relative under 24 h with the full date in `title`; **admin data views exception** |
| AssistantSheet | — | open, closed | slides from inline-start, max 380, shadow.1, outer corners radius.lg 16; user bubble soft; assistant bubble surface + border; sparkle icon in accent |
| AssistantFab | — | default, hover (accent.soft) | white pill: surface fill, 2 px accent outline, accent.text label 700, shadow.1, min height 48, fixed bottom inline-start |
| AssistantPage | — | default, list (mobile) | two Cards 1:2 from lg, height = viewport − app bar − main padding (`h-assistant`), log scrolls inside the chat card, current item accent.soft + accent border; under lg one column with a «محادثاتي السابقة» toggle |
| Dialog | default, confirm, destructive | open | centred, max 420, radius.lg 16, padding 20, shadow.1, backdrop color.overlay |
| HeroBanner | — | — | `bg-hero` (gradient.hero; gradient.hero.rtl in RTL), radius.lg 16, padding 20 / 32 from lg, white text, display 800 headline, body max 480, optional mint CTA, decorative surface/10 halo circle at bottom-end from lg; `shared/ui/hero.ts`; only landing hero, student home banner (the same headline banner on the student progress summary), subscribe header |
| CircleTile | tile, badge | — | 56 px mist (bg) circle with an accent.text initial (h2 800) or icon; 44 px badge (label 700) for unit ordinals; `shared/ui/circle.ts`, `aria-hidden` (the name stays the accessible label) |
| ExamTimer | normal, urgent (last 2 min → danger) | — | sticky Card under app bar, radius.md, type.stat 18px |
| KpiTile | — | loading, error | Card, type.caption label, type.stat 24/800 value, one type.caption detail (period delta when the API has one, never coloured), optional note; grid 2 columns, 4 from md |
| MetricList | — | — | caption rows, label text.muted at inline-start, value 700 text at inline-end, hairline between rows; overdue value danger |
| BarList | — | — | label and value over a 6 px Progress (accent on soft), scaled to the total or first step |
| CompactList | — | — | one Card, hairline rows (py 12), title ui 700 + caption meta at inline-start, caption value over a 6 px Progress (w 112 from md) and an `sm` secondary action at inline-end; second line below md; first 3 rows then ghost «عرض الكل (N)» / «عرض أقل» (aria-expanded) |
| DailyBarChart | — | empty | accent bars, border gridlines at 0/½/max with compact labels, a baseline tick for every day of the period, ≤ 4 date labels anchored on the latest day, total/peak line, LTR in both languages, sr-only data table, empty message in a 160 px box |
| EmptyState | no-data, no-results | — | icon, one line, primary CTA (secondary when the screen already shows its one mint, e.g. Invite on `/admin/users`); no-results offers "مسح الفلاتر" |
| Skeleton | — | loading | soft blocks with radius of the element they replace |
| MathInput | step, final | default, focus, disabled, keypad open (inputmode none) | textarea like Input but type.mono family at type.body size (16 px, avoids iOS zoom), dir="ltr"; preview box surface + border, radius.sm, min height 44; keypad 6×6 keys (surface, border.strong, radius.sm, ≥44px) on soft panel radius.md, dir="ltr", under the active field; step row surface + border radius.md with move up/down (ghost) and remove (danger) icon buttons ≥44px |
| DiagramCanvas | edit, student, key, answer | default, drawing, drop target, marked | image full width with aspect ratio; zones: 2px non-scaling stroke — edit accent + accent/15 fill, student text.muted dashed + surface/60, key success + success/20; number badge surface circle top-left; draft dashed accent; never mirrored in RTL; item chips pill, surface, border.strong; answer mode: zones are buttons — empty text.muted dashed + surface/60, holding items solid text stroke, drop target (dragged over, or focused with an item chosen) accent + accent/15, number badge + count/capacity pill (accent fill, white text); zone list below (surface cards, r.md, hairline) with ≥44px ghost icon buttons (earlier, later, return to bank) and «ضعه هنا» (secondary, sm) while an item is chosen; chips ≥44px, chosen: accent.soft fill + accent border, dragging ghost shadow.1; after check correct success.soft + success border + check icon, wrong or not placed danger.soft + danger border + x icon; correct placements reuse key mode |

## Rules
1. Mint = the one primary action per screen (every tab, filtered or empty state; a modal dialog is its own screen). Filter-form «تطبيق», actions repeated per card or row, inline add/rename forms and the Home plan line's «اشترك» are secondary. Signal Blue = brand, link, current place, selection, focus, progress, charts. Green/red/amber = status (documented exception). Nothing else is coloured.
2. Status text uses success.text, danger or warning on surface or its soft background. `success` itself is never text. Badges are soft fills; no white text on success.
3. The hero gradient appears only on the landing hero, the student home banner, the student progress summary (`/student/progress`, the same headline banner as Home) and the subscribe header; never on lesson, quiz, exam, teacher or admin screens. White text keeps ≥ 20 px from the gradient's end edge.
4. Lesson content areas are pure surface white with no tint.
5. White cards on the mist ground, never tinted cards on white.
6. Emphasis by weight, never by colour.
7. Every visual value in code is a token. A literal `#hex`, `px` size, radius or shadow outside `tokens.css` is a blocking finding.
8. No emoji in UI. Icons are Lucide, stroke 1.8, round caps.
9. Light only. No dark mode, no `prefers-color-scheme: dark` styles.
10. Honour `prefers-reduced-motion`: all transitions off.
11. Charcoal is never a fill. Signal Blue is never a filled action. Mint is only the primary button fill.
12. Circles for subject, unit and icon tiles; pills for buttons, tabs, badges and chips; 16 px cards; one shadow.
13. Admin data views (tables, KPI tiles, charts; also the student's history and progress tables) are an allowed exception to DESIGN.md's "no dense data tables".

## Breakpoints & layout
| Token | Value | Behaviour |
|-------|-------|-----------|
| bp.base | 0–699 | single column, bottom TabBar, gutter 16 |
| bp.md | ≥700 | card grids `repeat(auto-fill, minmax(280px, 1fr))`, gap 12 |
| bp.lg | ≥900 | two-column editors (1.2fr / 1fr), TopNav in the app bar instead of TabBar |
| bp.xl | ≥1200 | app bar shows the display name and the sign-out label |
| layout.max | 1200 | content centred |
| layout.bar | 56 | app bar and mobile tab bar height; sticky offsets and scroll-padding derive from it |
| layout.auth.max | 440 | auth column (login, sign-up, accept-invite) |

Verified widths: 375, 390, 768, 1280. No horizontal page scroll at any width.

## Accessibility requirements (WCAG 2.2 AA)
Computed with the WCAG 2.x formula; `web/scripts/tokens/contrast.test.ts` re-verifies them over `tokens.css`.

| Foreground | Background | Ratio | Need | Use |
|---|---|---|---|---|
| text #252B2F | surface / bg / soft / accent.soft | 14.33 / 13.31 / 12.73 / 12.71 | 4.5 | body |
| text | success.soft / danger.soft / warning.soft | 12.79 / 13.02 / 13.06 | 4.5 | feedback panels |
| text.muted #666E7E | surface / bg / soft / accent.soft | 5.13 / 4.76 / 4.55 / 4.55 | 4.5 | captions, inactive tab bar |
| text.muted | success.soft / danger.soft / warning.soft | 4.58 / 4.66 / 4.67 | 4.5 | captions in panels |
| text (charcoal) | action / action.hover / action.pressed (mint) | 9.33 / 7.48 / 5.96 | 4.5 | primary button |
| accent.text #0E64DA | surface / bg / soft / accent.soft | 5.45 / 5.06 / 4.84 / 4.83 | 4.5 | links, secondary label, active nav/tab, chips, circle tiles |
| accent #116EEE (logo only as text) | surface | 4.68 | 4.5 | logo on the white app bar |
| white | accent | 4.68 | 4.5 | "new reply" badge, diagram count pill |
| white | danger | 5.59 | 4.5 | — |
| success.text #166534 | surface / bg / success.soft | 7.13 / 6.62 / 6.37 | 4.5 | ok text and badge |
| danger #C8233A | surface / bg / danger.soft | 5.59 / 5.19 / 5.08 | 4.5 | error text, bad badge, danger button |
| warning #B45309 | surface / bg / warning.soft | 5.02 / 4.66 / 4.58 | 4.5 | partial, pending |
| v2 #6A3FB5 | surface | 7.02 | 4.5 | v2 badge |
| white | hero stops #3B1E90 / #5A3CC4 / #3A6EF0 | 11.84 / 7.30 / 4.498 | 4.5 (3 large) | banners; body text never reaches the end stop (≥ 4.70 at 94 % width with 20 px padding) |
| accent (focus ring, secondary/selected/active borders) | surface / bg / accent.soft / soft | 4.68 / 4.34 / 4.15 / 4.15 | 3 | UI |
| danger (danger outline) | surface / bg | 5.59 / 5.19 | 3 | UI |
| success #16A34A (non-text) | surface / bg | 3.30 / 3.06 | 3 | borders, icons, pass fill |
| success | success.soft | 2.94 | — | **never** text or the sole indicator |
| mint | surface / bg / hero end | 1.54 / 1.43 / 2.93 | — | not a required pair: the charcoal label identifies the control |
| border.strong (steel) | surface / bg | 1.36 / 1.26 | — | decorative field boundary |
| border (hairline) | surface | 1.19 | — | decorative |

Touch targets ≥44px; the only exceptions are (1) the desktop top nav and app-bar sign-out (36 px, shown only ≥ lg, the pointer layout; below lg the tab bar and a 44 px sign-out take over) and (2) table row actions in every table, student and admin (36 px `sm`, because rows are dense; adjacent actions keep adequate spacing per WCAG 2.5.8 Target Size (Minimum)). Focus visible on every interactive element (2px accent ring, offset 2). Real `<button>`, `<a href>`, `<label>` + `<input>`; never click handlers on divs. Icon-only buttons carry `aria-label`.

## RTL rules (Arabic)
- `<html lang="ar" dir="rtl">`. Logical properties only (`margin-inline-start`, `padding-inline`, `inset-inline-start`); `left`/`right` are findings.
- Direction-implying icons (back, next, chevrons) mirror in RTL.
- The hero gradient mirrors in RTL (deep indigo at inline-start).
- LaTeX, code, URLs, phone numbers, emails: `dir="ltr"` + `unicode-bidi: isolate`.
- Digits: Latin (0123) everywhere, in both languages: numbers, dates, times, percentages, money, counters (`ar-EG-u-nu-latn`, `numberLocale` in `shared/lib/format.ts`). Arabic-Indic digits are accepted in input and normalised, never displayed.
- Dates (`formatDateTime` in `shared/lib/dateTime.ts`, `<DateTime>` component): `date` «3 أكتوبر 2026» / Oct 3, 2026; `dateTime` (default) «3 أكتوبر 2026، 2:37 م» / Oct 3, 2026, 2:37 PM; `time` «2:37 م»; `day` «30 سبتمبر» / Sep 30; `fullDateTime` «السبت، 3 أكتوبر 2026، 2:37 م». Date and time are joined with «، » (ar) or «, » (en); a `YYYY-MM-DD` value is a UTC calendar date. Activity tables (session history, student history, AI conversations, audit log) show relative time («قبل ساعتين») under 24 h with the full date in `title`; deadlines and record dates stay absolute.

## Token → code mapping
- `src/styles/tokens.css` is generated from the tables above by `npm --prefix web run gen:tokens`: every token becomes `--ds-<group>-<name>` on `:root` (dots → dashes). Composite values split: typography → `-size`, `-line`, `-size-desktop`, `-line-desktop`, `-weight`; motion → `-duration`, `-easing`; `N mobile · M desktop` → base + `-desktop`. `bp.*` → `--breakpoint-*` inside `@theme`. No `.dark` block.
- `src/styles/app.css`:
  ```css
  @import "tailwindcss";
  @import "./tokens.css";
  @theme {
    --font-weight-*: initial;
    --font-weight-normal: 400; --font-weight-bold: 700; --font-weight-extrabold: 800;
  }
  @theme inline {
    --color-bg: var(--ds-color-bg);            --color-surface: var(--ds-color-surface);
    --color-soft: var(--ds-color-soft);        --color-text: var(--ds-color-text);
    --color-text-muted: var(--ds-color-text-muted);
    --color-border: var(--ds-color-border);    --color-border-strong: var(--ds-color-border-strong);
    --color-accent: var(--ds-color-accent);    --color-accent-soft: var(--ds-color-accent-soft);
    --color-accent-text: var(--ds-color-accent-text);
    --color-action: var(--ds-color-action);
    --color-action-hover: var(--ds-color-action-hover);
    --color-action-pressed: var(--ds-color-action-pressed);
    --color-success: var(--ds-color-success);  --color-success-soft: var(--ds-color-success-soft);
    --color-success-text: var(--ds-color-success-text);
    --color-danger: var(--ds-color-danger);    --color-danger-soft: var(--ds-color-danger-soft);
    --color-warning: var(--ds-color-warning);  --color-warning-soft: var(--ds-color-warning-soft);
    --color-v2: var(--ds-color-v2);
    --font-display: 'Poppins', 'Almarai', Tahoma, sans-serif;
    --font-sans: 'Poppins', 'Almarai', Tahoma, sans-serif;
    --radius-sm: var(--ds-radius-sm); --radius-md: var(--ds-radius-md);
    --radius-lg: var(--ds-radius-lg); --radius-pill: var(--ds-radius-pill);
    --shadow-1: var(--ds-shadow-1);
    --text-label: var(--ds-type-label-size); --text-label--line-height: var(--ds-type-label-line);
    --container-layout: var(--ds-layout-max); --container-auth: var(--ds-layout-auth-max);
  }
  ```
- shadcn/ui: `--radius` → radius.md; `--primary` → color.action, `--primary-foreground` → color.text, `--ring` → color.accent, `--destructive` → color.danger, `--background` → color.bg, `--card` → color.surface, `--muted` → color.soft, `--muted-foreground` → color.text.muted, `--border` → color.border.strong.
- `bg-hero` is the only gradient utility (defined in `app.css`, switching to gradient.hero.rtl under `dir="rtl"`), used through `heroClassName` by exactly three components (LandingHero, HeadlineCounterCard, SubscribeHeader); HeadlineCounterCard renders on `/student` and `/student/progress`, so the banner appears on four routes.

## Change log
| Version | Date | Change |
|---------|------|--------|
| 1.0 | 2026-09-26 | Glass adopted (light only, aurora on landing + subscribe) |
| 2.0 | 2026-10-03 | Indigo calm palette (#276): indigo primary and active states, ink-tinted borders and shadows, AA-tuned status colours (success.text added), aurora retuned; single-row app bar with More menu, BrandBar on auth and onboarding, layout.bar, layout.auth.max, bp.xl |
| 2.1 | 2026-10-03 | #280: Latin digits app-wide, shared date formats, Select chevron, global pointer cursor, row-action style and neutral-border danger, dashboard KPI tile / metric list / bar list / daily chart |
| 2.2 | 2026-10-04 | #286: legend-in-group rule, option cards for choice groups, headline counter chips and bar, CompactList |
| 3.0 | 2026-10-05 | #289: Mist design from DESIGN.md (docs/design-source.md): mint primary, Signal Blue accent and accent.text, mist canvas, steel borders, one shadow, radii 5/16/45/circle, Poppins + Almarai 400/700/800, type scale 12–40, hero gradient banners, circle tiles, layout.max 1200; status colours and danger kept as exceptions; admin data views exception |
