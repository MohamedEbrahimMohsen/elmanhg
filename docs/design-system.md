# Elmanhg Design System — "Mist"

| | |
|---|---|
| Version | 3.0 |
| Date | 2026-10-05 |
| Decision | Mist design from DESIGN.md for the whole app (#289), replacing Glass / Indigo calm |
| Mode | Light only. No dark theme. |
| Source | [`docs/design-source.md`](design-source.md) (DESIGN.md, verbatim) |

## 0. Source and resolved contradictions

DESIGN.md (`docs/design-source.md`) is the style reference. Where it contradicts itself or does not fit an exam-prep product, this document decides:

- **Primary action = mint fill.** Spring Mint `#11EE92` pill with a charcoal 700 label, one per screen. The component spec, the Do's and the signature choices all say this; they win over the two contrary lines (the colour table's "do not promote it to the primary CTA" and the quick reference's "no distinct CTA colour").
- **Secondary = Signal Blue outline.** Transparent, 2 px `#116EEE` border, blue label.
- **Tertiary = charcoal ghost.** No fill, no border, charcoal label.
- **Destructive = red outline.** DESIGN.md has no danger colour. Red is kept as an accessibility and safety exception.
- **Status colours kept.** Green, red and amber stay for correct / wrong / partial and state badges, with AA pairs (documented exception to "never introduce a second chromatic colour").
- **`#3b1e9` = `#3b1e90`.** The truncated hex in the gradient is a typo; the quick reference and the CSS block give `#3b1e90`.
- **Nista → Poppins + Almarai.** Nista is not available; DESIGN.md names Poppins as its substitute for Latin. Arabic needs its own face: **Almarai** (Boutros Fonts, OFL) is geometric, low-contrast and monoline, with round bowls and open counters that echo Poppins' circle-based Latin. It ships exactly 300/400/700/800, so the 400/700/800 scale maps 1:1 with no synthetic bold, it stays clear at 14 px, and its Arabic subset is about 32 KB per weight. Readex Pro (no 800), Tajawal (thin, reads small), Cairo (more contrast), Alexandria (too wide for tables), Noto Kufi Arabic (square forms clash with round Poppins) and IBM Plex Sans Arabic (humanist, no 800) were rejected.
- **Blue text shade.** Signal Blue is 4.68:1 on white but 4.34:1 on the mist canvas, so it fails AA as text there. Text uses `--accent-text` `#0E64DA` (5.06:1 on the canvas); `#116EEE` stays for the logo, borders, focus ring, progress fill, chart bars and icons. Same two-tier pattern as `--ok` / `--ok-text`.
- **Derived tints.** DESIGN.md gives no soft fills or hairline. `--soft` `#ECF2FA`, `--accent-soft` `#EAF2FE` and `--border` `#E6ECF2` are derived; the softs are the lightest tints that keep slate captions ≥ 4.5:1.
- **Admin data views exception.** DESIGN.md says "no dense data tables". Admin and teacher tables, KPI tiles and charts (and the student's history and progress tables) stay dense; they are working tools, restyled only through tokens.
- **Not applied** (nothing to apply them to): character illustrations and speech bubbles (a decorative halo circle stands in), the Trustpilot strip, the subject carousel with arrows (subjects stay a responsive grid), the footer locale select, full-bleed heroes (banners stay inside the page container).

## 1. Principles

1. **Mist canvas, white cards.** The page ground is the pale-blue mist `#F2F7FD`; cards float on it in white with a hairline and one subtle shadow.
2. **One mint action.** Each screen has at most one filled chromatic button: the mint primary. Everything else is an outline or ghost.
3. **Signal Blue for brand and place.** Logo, links, where you are, selection, focus, progress and charts. Green, red and amber are status only.
4. **Circles, pills, 16 px cards, one shadow.** Subject, unit and icon tiles are circles; buttons, tabs, badges and chips are pills; cards are 16 px; there is one shadow.
5. **Touch first.** Every control is at least 44 px tall, except 36 px `sm` row actions inside tables. Phone width (375 px) is the primary layout.

## 2. Colour tokens

### 2.1 Core

| Token | Value | Use |
|---|---|---|
| `--bg` | `#F2F7FD` | Canvas mist: page background |
| `--surface` | `#FFFFFF` | Cards, sheets, inputs, app bar, tab bar |
| `--soft` | `#ECF2FA` | Muted chips, pending and role badges, progress track, skeletons, secondary and ghost hover, user bubbles |
| `--text` | `#252B2F` | Charcoal: primary text. Never a fill |
| `--text-2` | `#666E7E` | Slate: captions, labels, meta, inactive tab bar |
| `--border` | `#E6ECF2` | Hairline on cards, rows, dividers, tab bar |
| `--border-strong` | `#D6DEE6` | Steel: inputs, selects, option outlines, inactive pill tabs |

### 2.2 Semantic

| Token | Value | Use |
|---|---|---|
| `--accent` | `#116EEE` | Signal Blue: logo, secondary/selected/active borders, focus ring, progress fill, chart bars, checkbox colour, white-text badges. Never a filled action, never body-size text except the logo |
| `--accent-text` | `#0E64DA` | Blue text shade (new): links, secondary and active labels, chips, circle tiles |
| `--accent-soft` | `#EAF2FE` | Active nav and pill tab fill, selected option fill, toggled secondary, accent chips |
| `--action` | `#11EE92` | Spring Mint (new): primary button fill only, one per screen |
| `--action-hover` | `#0FD683` | Primary button hover |
| `--action-pressed` | `#0DBF75` | Primary button pressed |
| `--ok` | `#16A34A` | Non-text only: borders, icons on white, pass progress fill, diagram key stroke (status exception) |
| `--ok-text` | `#166534` | Success text, ok badge text, verdict icon on `--ok-soft` (status exception) |
| `--ok-soft` | `#E6F6EB` | Correct feedback background, ok badge fill (status exception) |
| `--bad` | `#C8233A` | Wrong, rejected, SLA breached, destructive: text, border, fill (status exception) |
| `--bad-soft` | `#FEF1F2` | Wrong feedback background, bad badge fill, danger button hover (status exception) |
| `--warn` | `#B45309` | Partial credit, pending review (status exception) |
| `--warn-soft` | `#FEF3E2` | Partial feedback background (status exception) |
| `--v2` | `#6A3FB5` | "v2" badge only (internal) |
| overlay | `rgba(37,43,47,.40)` | Dialog backdrop |

### 2.3 Hero gradient

| Token | Value |
|---|---|
| `--hero` | `linear-gradient(90deg, #3B1E90 0%, #5A3CC4 30%, #3A6EF0 100%)` |
| `--hero-rtl` | the same at `270deg` (deep indigo at inline-start in Arabic) |

Allowed on exactly four surfaces: the landing hero, the student home headline banner, the student progress summary (`/student/progress` reuses the same headline banner, for consistency with Home) and the subscribe header. Never inside lesson, quiz, exam, teacher or admin screens, and not on quiz or exam results. Banners are contained (16 px radius, inside the page container), not full-bleed. Text on the gradient is white. White is 11.84 / 7.30 / 4.498:1 on the three stops, so white text keeps at least 20 px from the gradient's end edge (≥ 4.70:1 there); body copy is at most 480 px wide, and only large text may reach the end stop (3:1). The banner's primary button, when it has one, is the mint pill.

### 2.4 Contrast (verified)

Computed with the WCAG 2.x formula. `web/scripts/tokens/contrast.test.ts` re-checks these over the generated tokens.

| Foreground | Background | Ratio | Need | Use |
|---|---|---|---|---|
| `--text` | `--surface` / `--bg` / `--soft` / `--accent-soft` | 14.33 / 13.31 / 12.73 / 12.71 | 4.5 | Body |
| `--text` | `--ok-soft` / `--bad-soft` / `--warn-soft` | 12.79 / 13.02 / 13.06 | 4.5 | Feedback panels |
| `--text-2` | `--surface` / `--bg` / `--soft` / `--accent-soft` | 5.13 / 4.76 / 4.55 / 4.55 | 4.5 | Captions, inactive tab bar |
| `--text-2` | `--ok-soft` / `--bad-soft` / `--warn-soft` | 4.58 / 4.66 / 4.67 | 4.5 | Captions in panels |
| `--text` (charcoal) | `--action` / `--action-hover` / `--action-pressed` | 9.33 / 7.48 / 5.96 | 4.5 | Primary button |
| `--accent-text` | `--surface` / `--bg` / `--soft` / `--accent-soft` | 5.45 / 5.06 / 4.84 / 4.83 | 4.5 | Links, secondary label, active nav/tab, chips, circle tiles |
| `--accent` (logo only as text) | `--surface` | 4.68 | 4.5 | Logo on the white app bar |
| White | `--accent` | 4.68 | 4.5 | "New reply" badge, diagram count pill |
| White | `--bad` | 5.59 | 4.5 | — |
| `--ok-text` | `--surface` / `--bg` / `--ok-soft` | 7.13 / 6.62 / 6.37 | 4.5 | Ok text and badge |
| `--bad` | `--surface` / `--bg` / `--bad-soft` | 5.59 / 5.19 / 5.08 | 4.5 | Error text, bad badge, danger button |
| `--warn` | `--surface` / `--bg` / `--warn-soft` | 5.02 / 4.66 / 4.58 | 4.5 | Partial, pending |
| `--v2` | `--surface` | 7.02 | 4.5 | v2 badge |
| White | hero stops `#3B1E90` / `#5A3CC4` / `#3A6EF0` | 11.84 / 7.30 / 4.498 | 4.5 (3 large) | Banners; body text never reaches the end stop |
| `--accent` (focus ring, secondary/selected/active borders) | `--surface` / `--bg` / `--accent-soft` / `--soft` | 4.68 / 4.34 / 4.15 / 4.15 | 3 | UI |
| `--bad` (danger outline) | `--surface` / `--bg` | 5.59 / 5.19 | 3 | UI |
| `--ok` (non-text) | `--surface` / `--bg` | 3.30 / 3.06 | 3 | Borders, icons, pass fill |
| `--ok` | `--ok-soft` | 2.94 | — | **Never** text or the sole indicator |
| `--action` (mint) | `--surface` / `--bg` / hero end | 1.54 / 1.43 / 2.93 | — | Not a required pair: the charcoal label identifies the control (WCAG 1.4.11) |
| `--border-strong` (steel) | `--surface` / `--bg` | 1.36 / 1.26 | — | Decorative field boundary; label and placeholder identify the field |
| `--border` (hairline) | `--surface` | 1.19 | — | Decorative |

Rule: status text uses `--ok-text`, `--bad` or `--warn` on white or its soft background. `--ok` itself is never text. Badges are soft fills; no white text on green.

## 3. Typography

| Role | Font | Weights | Fallback |
|---|---|---|---|
| Latin letters, digits, punctuation | Poppins | 400, 700, 800 | Tahoma, sans-serif |
| Arabic | Almarai | 400, 700, 800 | Tahoma, sans-serif |
| Code / LaTeX source | system monospace, `direction: ltr` | 400 | |

One stack for everything: `'Poppins', 'Almarai', Tahoma, sans-serif`. Poppins comes first, so Latin text and digits inside Arabic render in Poppins and Arabic glyphs fall through to Almarai. Self-hosted from `@fontsource/poppins` (latin subset) and `@fontsource/almarai` (arabic subset), `font-display: swap`, no preload. Only these two families. Weights 400/700/800 only; tracking normal.

### 3.1 Scale (mobile / desktop)

| Token | Size | Line height | Weight | Use |
|---|---|---|---|---|
| `display` | 36 / 40 px | 41 / 46 px | 800 | Hero banners, headline banner (from 900 px; `h1` below), result scores |
| `h1` | 24 / 36 px | 31 / 41 px | 700 | Page title |
| `h2` | 18 / 24 px | 27 / 31 px | 700 | Section, card title, logo |
| `h3` | 16 px | 24 px | 700 | Sub-section |
| `body` | 16 px | 27 px | 400 (stem 700) | Lesson text, question stem |
| `ui` | 16 px | 24 px | 400 | Inputs, options, list titles |
| `label` | 14 px | 21 px | 700 | Buttons, nav items, pill tabs, field labels |
| `caption` | 14 px | 21 px | 400 | Meta, helper text |
| `micro` | 12 px | 18 px | 700 | Badges, tab bar labels |
| `stat` | 24 px | 31 px | 800 | KPI numbers |
| `mono` | 12 px | 18 px | 400 | LaTeX source, code, ids |

Arabic running text never goes below 14 px; lesson text and inputs are 16 px (inputs at 16 px also avoid iOS zoom).

### 3.2 Numerals

Latin digits (0123) everywhere, in both languages: numbers, dates, times, percentages, money and counters. Arabic formats with the `ar-EG-u-nu-latn` locale (`numberLocale` in `shared/lib/format.ts` is the single place this policy lives). Arabic-Indic digits typed by a user are accepted and normalised, never displayed.

#### Dates

One shared formatter (`formatDateTime` in `shared/lib/dateTime.ts`, the `<DateTime>` component) with five styles. Date and time are joined with «، » in Arabic and «, » in English. A `YYYY-MM-DD` value is a calendar date and is formatted in UTC; everything else in local time.

| Style | Arabic | English | Use |
|---|---|---|---|
| `date` | 3 أكتوبر 2026 | Oct 3, 2026 | Record dates, periods, renewals |
| `dateTime` (default) | 3 أكتوبر 2026، 2:37 م | Oct 3, 2026, 2:37 PM | Activity, messages, logs |
| `time` | 2:37 م | 2:37 PM | Chat messages, autosave |
| `day` | 30 سبتمبر | Sep 30 | Chart axes, dashboard range |
| `fullDateTime` | السبت، 3 أكتوبر 2026، 2:37 م | Saturday, October 3, 2026, 2:37 PM | Reply deadlines |

Activity tables (session history, admin student history, AI conversations, audit log) show relative time («قبل ساعتين») when the item is under 24 hours old, with the full date in the `title`. Deadlines and record dates stay absolute.

## 4. Spacing, radius, elevation

| Token | Value |
|---|---|
| Base unit | 4 px |
| Spacing scale | 4, 8, 12, 16, 20, 24, 32, 40, 48, 64 (section gap on the landing page) |
| Page gutter | 16 px mobile, 24 px desktop |
| Card padding | 16 px mobile, 20 px desktop |
| Content max width | 1200 px |
| App bar / tab bar height | 56 px |
| Auth column max | 440 px |
| `--r-sm` | 5 px (inputs, selects, textareas, text-link focus outline) |
| `--r-md` | 16 px (options, list items, feedback panel, menus, bubbles) |
| `--r-lg` | 16 px (cards, sheets, dialogs, hero banners) |
| `--r-pill` | 45 px (buttons, badges, pill tabs, nav pills, chips) |
| `--r-circle` | 9999 px (subject, unit and icon tiles) |
| `--shadow-1` | `rgba(0,0,0,.1) 0 1px 2px 0`: the only shadow (cards, tiles, app bar, menus, dialogs, sheets, toasts, assistant button) |

## 5. Components

### 5.1 Buttons

| Variant | Fill | Text | Border | Use |
|---|---|---|---|---|
| Primary | `--action` (hover `--action-hover`, pressed `--action-pressed`) | `--text` | none | The one mint action per screen: start quiz, submit, check, sign up |
| Secondary | transparent (hover `--soft`; toggled on `--accent-soft`) | `--accent-text` | 2 px `--accent` | Everything else, including per-card actions (subscribe, ask a teacher) |
| Danger | transparent (hover `--bad-soft`) | `--bad` | 2 px `--bad` | Reject, delete, cancel subscription (status exception) |
| Ghost | none (hover `--soft`) | `--text` | none | Tertiary inline actions, sign-out, "show all" |

All pill-shaped (45 px radius), 44 px min height (36 px for `sm` table row actions in every table, student and admin: a documented exception to the 44 px rule, because rows are dense; adjacent actions keep adequate spacing per WCAG 2.5.8 Target Size (Minimum)), `label` 14 px 700, padding-inline 20 px (12 px `sm`). Size `icon` is 44 × 44 for icon-only buttons beside fields (offset under the field label so it centres on the field). Disabled: 45 percent opacity, and no hover fill. Focus: 2 px `--accent` ring offset 2 px. Cursor: pointer on enabled buttons, selects and `[role=button]`, not-allowed on disabled controls (a global base rule). Row actions in every table are `sm` (36 px): secondary for neutral actions, danger for destructive ones; a row link is a secondary `sm` button (`Button asChild`). There is no Accent variant. A button repeated per card or row (subscribe on each plan card) is secondary, so a screen never shows two mint buttons.

### 5.2 Cards

White, `--r-lg` (16 px), hairline `--border`, `--shadow-1`. No coloured side borders. A card title is `h2`, meta is `caption` in `--text-2`.

### 5.3 Quiz option

Full-width label, `--r-md` (16 px), 48 px min height, `ui` 16 px text, steel `--border-strong`. The radio or checkbox uses `accent-color: var(--accent)`.

| State | Fill | Border |
|---|---|---|
| Default | `--surface` | `--border-strong` |
| Selected | `--accent-soft` | `--accent` |
| Correct (after check) | `--ok-soft` | `--ok` |
| Wrong (after check) | `--bad-soft` | `--bad` |

The same option card is used for choice groups outside the quiz (multi-unit exam units and sizes); a disabled option has 45 percent opacity and no hover fill, and a caption line may sit under the label.

### 5.4 Feedback panel

Appears under the question after checking. Icon circle 26 px in `--ok-text` or `--bad`, bold verdict, explanation in `--text-2`. Background uses the matching soft colour, border the matching strong colour.

### 5.5 Badges

Pill, `micro` 12 px 700. `ok` = `--ok-soft` fill, `--ok-text` text. `bad` = `--bad-soft` fill, `--bad` text. `pending`/neutral = `--soft` fill, `--text-2` text. `role` = `--soft` fill, `--text` text. `v2` = `--v2` outline.

### 5.6 Progress

Track `--soft`, fill `--accent` (mastery) or `--ok` (exam pass). Height 6 px, fully rounded. The `onHero` tone (white fill on a 30 % white track) is used on the hero gradient. The headline counter is now the student home hero banner (5.16), reused at the top of `/student/progress`; 12 px between rows: the counter sentence in white 800 at `h1` size below 900 px and `display` from 900 px, a 6 px `onHero` mastered-share bar, then three stat chips (seen, mastered, day streak): white pills with a caption label in `--text-2` and a 700 value in `--text`. It carries no button; the next-lesson card's button stays the screen's one mint action. The admin dashboard uses the same 6 px bar in its bar lists (share of a total, funnel steps, success-rate rows).

### 5.7 Navigation

App bar: one row, 56 px tall, sticky, white with the one subtle shadow (no bottom border), inside the same 1200 px container as the page, so the logo lines up with the page title. Order: logo (Signal Blue `--accent`, `h2` 700, links home) · nav · role badge · display name · sign-out (ghost). Below 900 px the nav moves to the tab bar. From 900 px the nav sits inline; between 900 and 1199 px the display name is hidden and sign-out shows its icon only (it keeps its accessible name); from 1200 px both show. Below 900 px sign-out is 44 px tall like every touch control; from 900 px it is the 36 px `sm` size.

Top nav (desktop ≥ 900 px): pills 36 px tall (with the 36 px sign-out, one of the two exceptions to principle 5, the other being 36 px `sm` table row actions: both nav controls exist at this size only in the pointer layout, and below 900 px navigation is the tab bar, whose items are taller than 44 px); inactive charcoal `--text` `label` 700, hover `--soft` fill, active `--accent-soft` fill with `--accent-text`. Each role has a fixed top-bar list: student all 6, teacher all 4, admin dashboard, content, questions and users. The admin's other destinations sit in an «المزيد» disclosure menu at the end of the nav: white, hairline border, `--shadow-1`, `--r-md`, items at least 44 px, closes on Esc, outside click, keyboard focus leaving the menu, or choosing an item.

Mobile tab bar: at most 4 items, icons 22 px stroke 1.8, active `--accent-text` 700 with the icon on an `--accent-soft` pill, inactive `--text-2`. A role with more than 3 destinations shows its 3 primary destinations plus a fourth item "المزيد" that opens a list of the rest. Bottom padding follows the device safe area (iOS home indicator).

Brand bar: the same app bar with only the logo (and an optional end slot, such as the landing page's sign-in button) on the landing page, login, sign-up, accept-invite and onboarding. The auth forms sit in a centred column at most 440 px wide under it.

Pill tabs (filters, sub-tabs, segmented toggles): `label` 700; inactive white with steel `--border-strong`, hover `--soft`, active `--accent-soft` fill with `--accent` border and `--accent-text`.

### 5.8 Inputs

White, `--r-sm` (5 px), steel `--border-strong`, 44 px height, `ui` 16 px. Focus ring as buttons. Labels 14 px `--text-2` above the field. Select: native appearance removed, a 16 px Lucide chevron-down in `--text-2` sits in a 40 px box at inline-end, padding-inline-end 40 px, not mirrored (`shared/ui/select.tsx`). A group of choices is a fieldset whose legend is an ordinary label inside the group (`ui` 700 in a card, 14 px `--text-2` in a form), never drawn on the group's border.

### 5.9 Tables (admin, teacher)

White card, no vertical rules, row separator hairline, header `caption` weight 700 `--text-2`, `caption` cells. Sticky header on desktop, horizontal scroll inside the card on mobile. Row actions are `sm` outline buttons (see 5.1). Activity dates are relative under 24 hours with the full date on hover.

**Admin data views exception.** DESIGN.md says "no dense data tables". Tables, KPI tiles and charts in admin and teacher screens, and the student's history and progress tables, stay dense: they are working tools, not marketing. They follow the tokens (16 px card, hairline rows, label and caption type, `sm` outline row actions) and nothing else changes.

### 5.10 Avatar panel

Slide-in sheet from the start edge, `--surface`, `--shadow-1`, `--r-lg` on the outer corners. Student bubbles `--soft`; assistant bubbles white with hairline border. A small sparkle icon in `--accent-text` marks the assistant. The floating assistant button is a white pill with a 2 px `--accent` outline, an `--accent-text` 700 label and `--shadow-1` (hover `--accent-soft`): a mint button there would be a second mint action on every student screen. Student pages where the button shows keep room below their content for it (its offset, its height and a gap, plus the safe-area inset), so the last control scrolls clear of the button; the full-page assistant and the exam page, which hide it, keep no such room. Full page (`/student/assistant`): the list and the chat are white cards (`--r-lg`, `--shadow-1`) in a 1 : 2 grid from 900 px; the current chat item is `--accent-soft` with an `--accent` border; the bubbles are the panel's.

### 5.11 Dialogs

Centered, max 420 px, `--r-lg` (16 px), `--shadow-1`, overlay `rgba(37,43,47,.40)`. The overlay and dialog stack above the app bar and tab bar.

### 5.12 Math input

The math-with-steps answer ([math-input.md](math-input.md)). Each LaTeX field is white, `--r-sm`, `--border-strong`, in the system monospace at 16 px (not the 12 px mono size: smaller text makes iOS zoom on focus), `direction: ltr`. A preview box sits under each field: white, hairline border, `--r-sm`, at least 44 px tall, with horizontal scroll inside. The touch keypad is a 6×6 grid of keys, each white, `--r-sm`, `--border-strong` and at least 44 px tall, on a `--soft` panel with `--r-md`, laid out left to right. It opens under the active field. Step rows are white `--r-md` hairline cards with icon buttons for move up, move down and remove (Danger), each at least 44 px.

### 5.13 Diagram canvas

The drag-and-drop diagram ([question-schemas.md](question-schemas.md)). The image scales to the card width with its aspect ratio reserved. Zones are rectangles with a 2 px non-scaling stroke, and each zone has a number badge (white circle, text colour) at its top-left. Edit mode uses an Accent stroke with a 15 % Accent fill; student mode uses a muted dashed stroke on a 60 % white fill; answer-key mode uses a Success stroke with a 20 % Success fill. A zone being drawn is dashed Accent. The canvas is never mirrored in RTL: image coordinates are physical. Items are pill chips: white, `--border-strong`.

Student answer mode: zones are buttons. An empty zone has a muted dashed stroke on a 60 % white fill; a zone holding items has a solid text-colour stroke; the drop target (dragged over, or focused while an item is chosen) has an Accent stroke with a 15 % Accent fill. Each zone shows its number badge and, when it holds items, a `count/capacity` pill (`--accent` fill, white text). A zone list under the image (white cards, `--r-md`, hairline border) holds each zone's chips, with ghost icon buttons of at least 44 px (earlier, later, return to the bank) and «ضعه هنا» (secondary, small) while an item is chosen. Chips are pills of at least 44 px, white with `--border-strong`; a chosen chip has the `--accent-soft` fill and an `--accent` border; the chip being dragged follows the pointer with `--shadow-1`. After checking, a correct item has the Success soft fill, a Success border and a check icon; a wrong or unplaced item has the Danger soft fill, a Danger border and an x icon, each with its text for screen readers. The correct placements reuse answer-key mode.

### 5.14 Dashboard

Admin dashboard (`/admin`): a row of eight KPI tiles (2 columns, 4 from 700 px): a caption label, one `stat` number (24 px 800), one caption detail (the in-period delta when the API has one, never coloured) and an optional note. Below, eight panels (1 / 2 / 3 columns) of metric rows (caption label in `--text-2` at inline-start, 700-weight value at inline-end, hairline between rows; an overdue value is `--bad`) and bar lists (label and value over a 6 px progress bar, scaled to the total or the first funnel step). Daily charts: `--accent` bars, `--border` gridlines at 0, ½ and the rounded maximum with compact labels, a baseline tick for every day of the period, at most 4 date labels anchored on the latest day, a total and peak line, left-to-right in both languages, an sr-only data table, and the empty message centred in a 160 px box.

### 5.15 Compact list

One white card, hairline between rows, 12 px vertical row padding. Each row: title (`ui` 700) and caption meta at inline-start; a caption value over a 6 px bar (112 px wide from 700 px, full remaining width below); a 36 px `sm` secondary action at inline-end. Below 700 px the value, bar and action form a second line. The first 3 rows show, then a ghost «عرض الكل (N)» / «عرض أقل» with `aria-expanded`.

### 5.16 Hero banner

A contained banner on the hero gradient (2.3): `--r-lg` 16 px, padding 20 px (32 px from 900 px), white text, a `display` 800 headline, body copy at most 480 px wide, an optional mint primary button, and a decorative white-10 % halo circle at the bottom-end corner from 900 px (it never sits under text). No shadow. Used by the landing hero, the student home headline (also the student progress summary, which reuses it) and the subscribe header only (`shared/ui/hero.ts`, `heroClassName`).

### 5.17 Circle tiles

DESIGN.md's signature shape. A 56 px mist (`--bg`) circle holding the subject's initial (`h2` 800 in `--accent-text`; a leading «ال» is dropped) on home subject cards, or a Lucide icon on the landing value cards; a 44 px circle badge (`label` 700) holds each unit's number on the subject page. They are decorative (`aria-hidden`): the name stays the accessible label (`shared/ui/circle.ts`).

## 6. Layout and RTL

- `dir="rtl"` on `html`. Logical properties only (`margin-inline-start`, `padding-inline`), never left/right.
- Icons that imply direction (back, next) are mirrored in RTL.
- The hero gradient mirrors in RTL (deep indigo at inline-start).
- LaTeX, code, URLs and phone numbers are wrapped in `direction: ltr; unicode-bidi: isolate`.
- Content max width 1200 px, centred.
- Breakpoints: 700 px (tablet grid), 900 px (admin two-column editor, nav moves into the app bar), 1200 px (app bar shows the display name and sign-out label).

## 7. Motion

- Durations: 150 ms for hover and focus, 220 ms for panels and dialogs, 400 ms for progress bar fill.
- Easing `cubic-bezier(.2,.8,.2,1)`. Respect `prefers-reduced-motion`.
- Correct answer: feedback panel fades in and the option border transitions. No confetti.

## 8. Iconography

Stroke icons, 1.8 px, round caps, 22 px in navigation and 16 px inline. Lucide set. No emoji anywhere in product UI.

## 9. Do and don't

- Do keep one mint primary button per screen, in every tab and every filtered or empty state (a modal dialog counts as its own screen); a second action is an outline or ghost. Filter-form «تطبيق», actions repeated per card or row, inline add/rename forms and the Home plan line's «اشترك» are always outline; a no-results «مسح الفلاتر» is outline when the screen already has its mint (e.g. «دعوة» on the users page).
- Do use white cards on the mist ground, never tinted cards on white.
- Do use circles for subject, unit and icon tiles, pills for controls, 16 px cards and the one shadow.
- Don't use the hero gradient outside the landing hero, the student home banner, the student progress summary and the subscribe header.
- Don't add a second chromatic action colour; Signal Blue is never a filled action and charcoal is never a fill.
- Don't use dense data tables outside the admin data views exception.
- Don't use weights other than 400, 700 and 800.
- Don't tint lesson content areas.
- Don't colour text for emphasis. Use weight.
- Don't add a dark mode. Light only is a product decision.

## 10. Implementation notes

- Tailwind CSS v4, CSS-first: `web/src/styles/tokens.css` is generated from `.claude/design-system.md` by `npm run gen:tokens` (never hand-edited) and mapped to utilities with `@theme inline` in `web/src/styles/app.css`. There is no `tailwind.config.*`. A `@theme` weight reset leaves only `font-normal` (400), `font-bold` (700) and `font-extrabold` (800).
- shadcn/ui: `--radius` maps to `--r-md`; `--primary` maps to the action (`--action`) with `--text` as its foreground.
- Fonts come from fontsource (`@fontsource/poppins` latin and `@fontsource/almarai` arabic, 400/700/800), imported in `web/src/main.tsx`.
- `bg-hero` is the only gradient utility; it switches to the RTL twin under `dir="rtl"`.
- The prototype in `prototype/` remains the reference for screen content and flow.
