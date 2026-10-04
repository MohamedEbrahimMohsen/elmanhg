# Design system — Elmanhg (المنهج) · "Glass" — palette Indigo calm (v2.2)

Owner: product (Mohamed). Engineers do not edit values here; they reference tokens. Human-readable rationale lives in `docs/design-system.md`; this file is the token contract the pipeline reads. Both must agree (docs-sync rule).

Source of truth: `docs/design-system.md` v2.2 (2026-10-04). No Figma. Screen content, flow and states come from `prototype/`; the look comes only from this file.
Mode: **light only**. There is no dark theme and no `.dark` override block.

## Tokens

### Colour
| Token | CSS var | Value | Use |
|-------|---------|-------|-----|
| color.bg | --ds-color-bg | #F4F5FB | page ground. Never pure white for the page |
| color.surface | --ds-color-surface | #FFFFFF | cards, sheets, inputs, app bar, tab bar |
| color.soft | --ds-color-soft | #E5E7FB | muted chips, pending badge, progress track, skeletons, secondary hover, user bubbles |
| color.text | --ds-color-text | #1E1B4B | primary text. Never a button, tab or badge fill |
| color.text.muted | --ds-color-text-muted | #5F5D7A | secondary text, labels, captions, inactive nav |
| color.border | --ds-color-border | rgba(30,27,75,.08) | hairline on cards, app bar, tab bar, table rows |
| color.border.strong | --ds-color-border-strong | rgba(30,27,75,.18) | inputs, option outlines, secondary buttons, inactive pill tabs |
| color.accent | --ds-color-accent | #4F46E5 | primary/accent button fill, links, focus ring, progress fill, active nav/tab text, selected option border, checkbox accent-color, logo |
| color.accent.hover | --ds-color-accent-hover | #4338CA | hover on accent fills |
| color.accent.pressed | --ds-color-accent-pressed | #3730A3 | pressed on accent fills |
| color.accent.soft | --ds-color-accent-soft | #EEF0FF | active nav item and pill tab fill, selected option fill, accent chips, toggled secondary button |
| color.success | --ds-color-success | #16A34A | non-text only: borders, icons on surface, pass progress fill, diagram key stroke |
| color.success.text | --ds-color-success-text | #166534 | success text, ok badge text, verdict icon on success.soft |
| color.success.soft | --ds-color-success-soft | #DFF3E5 | correct feedback background, ok badge fill |
| color.danger | --ds-color-danger | #C8233A | wrong, rejected, overdue, destructive: text, border, fill |
| color.danger.soft | --ds-color-danger-soft | #FDECEE | wrong feedback background, bad badge fill |
| color.warning | --ds-color-warning | #B45309 | partial credit, pending review |
| color.warning.soft | --ds-color-warning-soft | #FEF3E2 | partial feedback background |
| color.v2 | --ds-color-v2 | #6A3FB5 | v2 badge only |
| color.overlay | --ds-color-overlay | rgba(30,27,75,.40) | dialog backdrop |
| gradient.aurora | --ds-gradient-aurora | linear-gradient(135deg,#4338CA 0%,#6D28D9 55%,#A21CAF 100%) | landing hero + subscribe header ONLY |

### Typography
| Token | Family | Size / line (mobile · desktop) | Weight | Use |
|-------|--------|-------------------------------|--------|-----|
| type.display | Readex Pro | 36/38 · 44/46, tracking -0.02em | 700 | headline counter (from lg; type.h1 below lg), landing hero |
| type.h1 | Readex Pro | 26/31 · 30/36 | 700 | page title |
| type.h2 | Readex Pro | 20/26 · 22/29 | 700 | section, card title |
| type.h3 | Readex Pro | 16/22 | 600 | sub-section |
| type.body | Noto Sans Arabic | 16/27 (lesson text 16/29) | 400 (question stem 600) | lesson text, question stem |
| type.ui | Noto Sans Arabic | 15/22 | 500–600 | buttons, inputs, options |
| type.caption | Noto Sans Arabic | 13/20 | 400 | meta, helper text |
| type.micro | Noto Sans Arabic | 12/17 | 600 | badges, tab labels |
| type.stat | Readex Pro | 26/30, tracking -0.02em | 700 | KPI numbers |
| type.mono | system monospace | 12/18 | 400 | LaTeX source, code, ids (always `dir="ltr"`) |

Arabic body text never below 15 px. Fonts: Google Fonts `Readex Pro:wght@500;600;700` and `Noto Sans Arabic:wght@400;500;600`, `display=swap`, self-hosted in production. Fallback `Tahoma, sans-serif`. Inter, Roboto, Arial, Cairo, Tajawal are prohibited.

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
| layout.gutter | 16 mobile · 24 desktop |
| layout.card.padding | 16 mobile · 20 desktop |
| layout.stack.gap | 12 (between stacked cards and grid cells) |
| layout.max | 1040 |
| layout.bar | 56 (app bar and mobile tab bar height) |
| layout.auth.max | 440 (auth column) |

### Radius · Elevation · Motion
| Token | Value |
|-------|-------|
| radius.sm | 10 (inputs, chips) |
| radius.md | 14 (quiz options, list items, feedback panel) |
| radius.lg | 18 (cards, sheets, dialogs) |
| radius.pill | 999 (buttons, badges, sub-tabs) |
| shadow.1 | 0 1px 2px rgba(30,27,75,.05), 0 8px 24px rgba(79,70,229,.08) — cards |
| shadow.2 | 0 4px 12px rgba(30,27,75,.08), 0 24px 48px rgba(79,70,229,.14) — sheets, dialogs, floating assistant button, nav menu |
| motion.fast | 150ms cubic-bezier(.2,.8,.2,1) — hover, focus |
| motion.base | 220ms cubic-bezier(.2,.8,.2,1) — panels, dialogs |
| motion.slow | 400ms cubic-bezier(.2,.8,.2,1) — progress fill |

## Components
| Component | Variants | States | Notes |
|-----------|----------|--------|-------|
| Button | primary (accent fill, white label; hover accent.hover, pressed accent.pressed), accent (same as primary since 2.0), secondary (surface + border.strong; aria-pressed → accent.soft + accent border/text), danger (surface + border.strong, danger text; hover danger.soft), ghost (accent text) | default, hover, focus, pressed, disabled (45% opacity, no hover fill), loading | pill, min height 44 (36 for `sm` table row actions, a documented exception to the 44 rule), padding-inline 18 (12 sm), type.ui 600; size `icon` 44×44 for icon-only buttons beside fields (offset `mt-6.5` under a label). One primary per screen. Cursor pointer on enabled buttons/selects/[role=button], not-allowed on disabled (global base rule). Row actions in every table are `sm` (36): secondary, destructive `danger`; row links use `Button asChild`. |
| Card | default | default, hover (border.strong) for clickable cards | surface, border, shadow.1, radius.lg. No coloured side borders ever. |
| QuizOption | radio, checkbox | default, hover (soft), selected (accent.soft fill + accent border), correct (success.soft + success), wrong (danger.soft + danger), disabled | full-width label, min height 48, radius.md, padding 12×14, gap 10, control 18px with accent-color accent; also the option card for choice groups (multi-exam units and sizes), disabled 45% opacity no hover, `shared/ui/optionCard.ts` |
| FeedbackPanel | correct, wrong, partial | enter (fade, motion.base) | soft bg + strong border of the verdict colour, 26px filled circle icon, bold verdict, explanation in text.muted |
| Badge | ok, bad, pending, role, v2, neutral | — | pill, type.micro, padding 2×10. ok = success.soft + success.text; bad = danger.soft + danger; pending/neutral = soft + text.muted; role = soft + text; v2 = outline |
| Progress | mastery (accent), pass (success) | — | height 6, track soft, radius.pill, fill animates motion.slow; headline counter card: sentence h1 below lg / display from lg with balanced wrap, mastered-share bar, 3 stat chips (soft pill, caption label text.muted, 600 value), rows gap 12 |
| Input / Select / Textarea | default | default, focus (2px accent ring, offset 2), error (danger border + caption), disabled | surface, border.strong, radius.sm, height 44, padding 9×12, label type.caption above with gap 6. Select: appearance none, Lucide ChevronDown 16 px text.muted in a 40 px inline-end box, padding-inline-end 40, not mirrored (`shared/ui/select.tsx`); fieldset legend is a block label inside the group (global base rule floats it), never on the border |
| TabBar (mobile) | — | active (accent text 600 on an accent.soft icon pill), inactive (text.muted) | at most 4 items; a role with more destinations shows 3 + "المزيد" (list of the rest); Lucide icons 22px stroke 1.8, label type.micro, surface + top hairline; bottom padding env(safe-area-inset-bottom) |
| AppBar | — | — | single row, height layout.bar, sticky, surface + bottom hairline, same container as main: logo (accent) · nav · role badge · name (≥ xl) · sign-out (label ≥ xl; min height 44 below lg, 36 `sm` from lg) |
| TopNav (desktop) | — | inactive (text.muted), hover (bg + text), active (accent.soft + accent 600) | pills min-h 36; destinations beyond the role's top-bar list sit in an «المزيد» disclosure menu: surface, border, shadow.2, radius.md, items ≥ 44 px, Esc/outside click/focus leaving closes |
| BrandBar | — | — | app bar with logo only (+ end slot) on landing, login, sign-up, accept-invite, onboarding |
| PillTab | — | inactive (surface + border.strong), hover (soft), active (accent.soft + accent border + accent text) | filters, sub-tabs, segmented toggles; pill, min height 44 |
| Table | — | row hover (soft) | inside a Card, no vertical rules, row hairline, header type.caption 600 text.muted, cell 13.5px, padding 9×10, sticky header on desktop, horizontal scroll inside the card on mobile; row actions `sm`; activity dates relative under 24 h with the full date in `title` |
| AssistantSheet | — | open, closed | slides from inline-start, max 380, shadow.2, outer corners radius.lg; user bubble soft; assistant bubble surface + border; sparkle icon in accent |
| AssistantFab | — | default, hover | pill, accent fill (hover, pressed as primary), white label, shadow.2, min height 48, fixed bottom inline-start |
| Dialog | default, confirm, destructive | open | centred, max 420, radius.lg, padding 20, shadow.2, backdrop color.overlay |
| ExamTimer | normal, urgent (last 2 min → danger) | — | sticky Card under app bar, radius.md, type.stat 18px |
| KpiTile | — | loading, error | Card, type.caption label, type.stat value, one type.caption detail (period delta when the API has one, never coloured), optional note; grid 2 columns, 4 from md |
| MetricList | — | — | caption rows, label text.muted at inline-start, value 600 text at inline-end, hairline between rows; overdue value danger |
| BarList | — | — | label and value over a 6 px Progress (accent on soft), scaled to the total or first step |
| CompactList | — | — | one Card, hairline rows (py 12), title ui 600 + caption meta at inline-start, caption value over a 6 px Progress (w 112 from md) and an `sm` secondary action at inline-end; second line below md; first 3 rows then ghost «عرض الكل (N)» / «عرض أقل» (aria-expanded) |
| DailyBarChart | — | empty | accent bars, border gridlines at 0/½/max with compact labels, a baseline tick for every day of the period, ≤ 4 date labels anchored on the latest day, total/peak line, LTR in both languages, sr-only data table, empty message in a 160 px box |
| EmptyState | no-data, no-results | — | icon, one line, primary CTA; no-results offers "مسح الفلاتر" |
| Skeleton | — | loading | soft blocks with radius of the element they replace |
| MathInput | step, final | default, focus, disabled, keypad open (inputmode none) | textarea like Input but type.mono family at type.body size (16 px, avoids iOS zoom), dir="ltr"; preview box surface + border, radius.sm, min height 44; keypad 6×6 keys (surface, border.strong, radius.sm, ≥44px) on soft panel radius.md, dir="ltr", under the active field; step row surface + border radius.md with move up/down (ghost) and remove (danger) icon buttons ≥44px |
| DiagramCanvas | edit, student, key, answer | default, drawing, drop target, marked | image full width with aspect ratio; zones: 2px non-scaling stroke — edit accent + accent/15 fill, student text.muted dashed + surface/60, key success + success/20; number badge surface circle top-left; draft dashed accent; never mirrored in RTL; item chips pill, surface, border.strong; answer mode: zones are buttons — empty text.muted dashed + surface/60, holding items solid text stroke, drop target (dragged over, or focused with an item chosen) accent + accent/15, number badge + count/capacity pill (accent fill); zone list below (surface cards, r.md, hairline) with ≥44px ghost icon buttons (earlier, later, return to bank) and «ضعه هنا» (secondary, sm) while an item is chosen; chips ≥44px, chosen: accent.soft fill + accent border, dragging ghost shadow.2; after check correct success.soft + success border + check icon, wrong or not placed danger.soft + danger border + x icon; correct placements reuse key mode |

## Rules
1. Indigo = action, link or current place. Green = correct. Red = wrong. Amber = partial/pending. Nothing else is coloured.
2. Status text uses success.text, danger or warning on surface or its soft background. `success` itself is never text. Badges are soft fills; no white text on success.
3. The aurora gradient appears only on the landing hero and the subscribe screen header. Never on lesson, quiz, exam, teacher or admin screens.
4. Lesson content areas are pure surface white with no tint.
5. White cards on the grey ground, never grey cards on white.
6. Emphasis by weight, never by colour.
7. Every visual value in code is a token. A literal `#hex`, `px` size, radius or shadow outside `tokens.css` is a blocking finding.
8. No emoji in UI. Icons are Lucide, stroke 1.8, round caps.
9. Light only. No dark mode, no `prefers-color-scheme: dark` styles.
10. Honour `prefers-reduced-motion`: all transitions off.
11. Near-black (`color.text`) is never a fill.

## Breakpoints & layout
| Token | Value | Behaviour |
|-------|-------|-----------|
| bp.base | 0–699 | single column, bottom TabBar, gutter 16 |
| bp.md | ≥700 | card grids `repeat(auto-fill, minmax(280px, 1fr))`, gap 12 |
| bp.lg | ≥900 | two-column editors (1.2fr / 1fr), TopNav in the app bar instead of TabBar |
| bp.xl | ≥1200 | app bar shows the display name and the sign-out label |
| layout.max | 1040 | content centred |
| layout.bar | 56 | app bar and mobile tab bar height; sticky offsets and scroll-padding derive from it |
| layout.auth.max | 440 | auth column (login, sign-up, accept-invite) |

Verified widths: 375, 390, 768, 1280. No horizontal page scroll at any width.

## Accessibility requirements (WCAG 2.2 AA)
Computed with the WCAG 2.x formula; `web/scripts/tokens/contrast.test.ts` re-verifies them over `tokens.css`.

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
| success #16A34A | success.soft | 2.84 | — | **never** text or the sole indicator |
| border.strong | surface | ≈1.44 | — | decorative field boundary, not a text pair |

Touch targets ≥44px; the only exceptions are (1) the desktop top nav and app-bar sign-out (36 px, shown only ≥ lg, the pointer layout; below lg the tab bar and a 44 px sign-out take over) and (2) table row actions in every table, student and admin (36 px `sm`, because rows are dense; adjacent actions keep adequate spacing per WCAG 2.5.8 Target Size (Minimum)). Focus visible on every interactive element (2px accent ring, offset 2). Real `<button>`, `<a href>`, `<label>` + `<input>`; never click handlers on divs. Icon-only buttons carry `aria-label`.

## RTL rules (Arabic)
- `<html lang="ar" dir="rtl">`. Logical properties only (`margin-inline-start`, `padding-inline`, `inset-inline-start`); `left`/`right` are findings.
- Direction-implying icons (back, next, chevrons) mirror in RTL.
- LaTeX, code, URLs, phone numbers, emails: `dir="ltr"` + `unicode-bidi: isolate`.
- Digits: Latin (0123) everywhere, in both languages: numbers, dates, times, percentages, money, counters (`ar-EG-u-nu-latn`, `numberLocale` in `shared/lib/format.ts`). Arabic-Indic digits are accepted in input and normalised, never displayed.
- Dates (`formatDateTime` in `shared/lib/dateTime.ts`, `<DateTime>` component): `date` «3 أكتوبر 2026» / Oct 3, 2026; `dateTime` (default) «3 أكتوبر 2026، 2:37 م» / Oct 3, 2026, 2:37 PM; `time` «2:37 م»; `day` «30 سبتمبر» / Sep 30; `fullDateTime` «السبت، 3 أكتوبر 2026، 2:37 م». Date and time are joined with «، » (ar) or «, » (en); a `YYYY-MM-DD` value is a UTC calendar date. Activity tables (session history, student history, AI conversations, audit log) show relative time («قبل ساعتين») under 24 h with the full date in `title`; deadlines and record dates stay absolute.

## Token → code mapping
- `src/styles/tokens.css` is generated from the tables above by `npm --prefix web run gen:tokens`: every token becomes `--ds-<group>-<name>` on `:root` (dots → dashes). Composite values split: typography → `-size`, `-line`, `-size-desktop`, `-line-desktop`, `-weight`, `-tracking`; motion → `-duration`, `-easing`; `N mobile · M desktop` → base + `-desktop`. `bp.*` → `--breakpoint-*` inside `@theme`. No `.dark` block.
- `src/styles/app.css`:
  ```css
  @import "tailwindcss";
  @import "./tokens.css";
  @theme inline {
    --color-bg: var(--ds-color-bg);            --color-surface: var(--ds-color-surface);
    --color-soft: var(--ds-color-soft);        --color-text: var(--ds-color-text);
    --color-text-muted: var(--ds-color-text-muted);
    --color-border: var(--ds-color-border);    --color-border-strong: var(--ds-color-border-strong);
    --color-accent: var(--ds-color-accent);    --color-accent-soft: var(--ds-color-accent-soft);
    --color-accent-hover: var(--ds-color-accent-hover);
    --color-accent-pressed: var(--ds-color-accent-pressed);
    --color-success: var(--ds-color-success);  --color-success-soft: var(--ds-color-success-soft);
    --color-success-text: var(--ds-color-success-text);
    --color-danger: var(--ds-color-danger);    --color-danger-soft: var(--ds-color-danger-soft);
    --color-warning: var(--ds-color-warning);  --color-warning-soft: var(--ds-color-warning-soft);
    --color-v2: var(--ds-color-v2);
    --font-display: "Readex Pro", Tahoma, sans-serif;
    --font-sans: "Noto Sans Arabic", Tahoma, sans-serif;
    --radius-sm: 10px; --radius-md: 14px; --radius-lg: 18px;
    --shadow-1: var(--ds-shadow-1); --shadow-2: var(--ds-shadow-2);
    --container-auth: var(--ds-layout-auth-max);
  }
  ```
- shadcn/ui: `--radius: 14px`; `--primary` → color.accent, `--ring` → color.accent, `--destructive` → color.danger, `--background` → color.bg, `--card` → color.surface, `--muted` → color.soft, `--muted-foreground` → color.text.muted, `--border` → color.border.strong.
- Aurora is only available as the `.bg-aurora` utility defined in `app.css`, used by exactly two components (LandingHero, SubscribeHeader).

## Change log
| Version | Date | Change |
|---------|------|--------|
| 1.0 | 2026-09-26 | Glass adopted (light only, aurora on landing + subscribe) |
| 2.0 | 2026-10-03 | Indigo calm palette (#276): indigo primary and active states, ink-tinted borders and shadows, AA-tuned status colours (success.text added), aurora retuned; single-row app bar with More menu, BrandBar on auth and onboarding, layout.bar, layout.auth.max, bp.xl |
| 2.1 | 2026-10-03 | #280: Latin digits app-wide, shared date formats, Select chevron, global pointer cursor, row-action style and neutral-border danger, dashboard KPI tile / metric list / bar list / daily chart |
| 2.2 | 2026-10-04 | #286: legend-in-group rule, option cards for choice groups, headline counter chips and bar, CompactList |
