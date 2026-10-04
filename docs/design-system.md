# Elmanhg Design System — "Glass"

| | |
|---|---|
| Version | 2.2 |
| Date | 2026-10-04 |
| Decision | Glass direction for the whole app. Aurora gradient allowed only on the landing hero and the subscribe screen. Indigo calm palette (#276). #280 polish: Latin digits, shared date formats, Select chevron, pointer cursor, row actions, dashboard. #286: legends inside their group, option cards for choice groups, headline stat chips, compact ranked lists. |
| Mode | Light only. No dark theme. |
| Reference canvas | https://claude.ai/artifact/VHfyt2bTbtQJnA9Fg6Snpp (board 4, Glass) |

## 1. Principles

1. **Low glare.** The page ground is light grey, never pure white. White is reserved for cards, so contrast lands where reading happens.
2. **One colour per job.** Indigo means action, link or where you are. Green means "correct or mastered". Red means "wrong or overdue". Amber means partial or pending. Nothing else is coloured. Near-black is text, never a fill.
3. **Content is the hero.** Lesson text, equations and diagrams sit on white with no tint. Chrome stays grey.
4. **Calm surfaces.** Depth comes from soft shadows and hairline borders, not from colour blocks.
5. **Touch first.** Every control is at least 44 px tall, except 36 px `sm` row actions inside tables. Phone width (375 px) is the primary layout.

## 2. Colour tokens

### 2.1 Core

| Token | Value | Use |
|---|---|---|
| `--bg` | `#F4F5FB` | Page background |
| `--surface` | `#FFFFFF` | Cards, sheets, inputs, app bar, tab bar |
| `--soft` | `#E5E7FB` | Muted chips, pending badge, progress track, skeletons, secondary hover, user bubbles |
| `--text` | `#1E1B4B` | Primary text. Never a button, tab or badge fill |
| `--text-2` | `#5F5D7A` | Secondary text, labels, captions, inactive nav |
| `--border` | `rgba(30,27,75,.08)` | Hairline borders on cards, app bar, tab bar, table rows |
| `--border-strong` | `rgba(30,27,75,.18)` | Inputs, option outlines, secondary buttons, inactive pill tabs |

### 2.2 Semantic

| Token | Value | Use |
|---|---|---|
| `--accent` | `#4F46E5` | Primary button fill, links, focus ring, progress fill, active nav/tab text, selected option border, checkbox colour, logo |
| `--accent-hover` | `#4338CA` | Hover on accent fills |
| `--accent-pressed` | `#3730A3` | Pressed on accent fills |
| `--accent-soft` | `#EEF0FF` | Active nav item and pill tab fill, selected option fill, accent chips, toggled secondary button |
| `--ok` | `#16A34A` | Non-text only: borders, icons on white, pass progress fill, diagram key stroke |
| `--ok-text` | `#166534` | Success text, ok badge text, verdict icon on `--ok-soft` |
| `--ok-soft` | `#DFF3E5` | Correct feedback background, ok badge fill |
| `--bad` | `#C8233A` | Wrong, rejected, SLA breached, destructive: text, border, fill |
| `--bad-soft` | `#FDECEE` | Wrong feedback background, bad badge fill |
| `--warn` | `#B45309` | Partial credit, pending review |
| `--warn-soft` | `#FEF3E2` | Partial feedback background |
| `--v2` | `#6A3FB5` | "v2" badge only (internal) |

### 2.3 Landing gradient (marketing surfaces only)

| Token | Value |
|---|---|
| `--aurora` | `linear-gradient(135deg, #4338CA 0%, #6D28D9 55%, #A21CAF 100%)` |

Allowed on: landing page hero background, subscribe screen header, marketing emails. Never inside lesson, quiz, exam, teacher or admin screens. Text on the aurora is plain white (at least 6.32:1 on every stop); the hero button is the white Secondary pill.

### 2.4 Contrast (verified)

Computed with the WCAG 2.x formula. `web/scripts/tokens/contrast.test.ts` re-checks these over the generated tokens.

| Foreground | Background | Ratio | Need | Use |
|---|---|---|---|---|
| `--text` | `--surface` | 15.99 | 4.5 | Body |
| `--text` | `--bg` | 14.69 | 4.5 | Page text |
| `--text` | `--soft` | 13.06 | 4.5 | Pending badge, bubbles |
| `--text` | `--accent-soft` | 14.12 | 4.5 | — |
| `--text` | `--ok-soft` / `--bad-soft` / `--warn-soft` | 13.77 / 14.01 / 14.57 | 4.5 | Feedback panels |
| `--text-2` | `--surface` / `--bg` / `--soft` | 6.30 / 5.79 / 5.15 | 4.5 | Captions, inactive nav |
| `--text-2` | `--accent-soft` / `--ok-soft` / `--bad-soft` / `--warn-soft` | 5.56 / 5.43 / 5.52 / 5.74 | 4.5 | Captions in panels |
| White | `--accent` / `--accent-hover` / `--accent-pressed` | 6.29 / 7.90 / 9.93 | 4.5 | Primary button states |
| White | `--bad` | 5.59 | 4.5 | — |
| `--accent` | `--surface` / `--bg` / `--soft` / `--accent-soft` | 6.29 / 5.78 / 5.14 / 5.55 | 4.5 | Links, active nav pill, chips, ghost |
| `--ok-text` | `--surface` / `--ok-soft` | 7.13 / 6.14 | 4.5 | Ok text, ok badge, verdict icon |
| `--bad` | `--surface` / `--bg` / `--bad-soft` | 5.59 / 5.14 / 4.90 | 4.5 | Error text, bad badge |
| `--warn` | `--surface` / `--bg` / `--warn-soft` | 5.02 / 4.62 / 4.58 | 4.5 | Partial, pending |
| `--v2` | `--surface` | 7.02 | 4.5 | v2 badge |
| White | aurora stops `#4338CA` / `#6D28D9` / `#A21CAF` | 7.90 / 7.10 / 6.32 | 4.5 | Hero and subscribe header |
| `--accent` (focus ring, active pill border) | `--surface` / `--bg` | 6.29 / 5.78 | 3 | UI |
| `--ok` (non-text) | `--surface` / `--bg` | 3.30 / 3.03 | 3 | Borders, icons, pass fill |
| `--ok` | `--ok-soft` | 2.84 | — | **Never** text or the sole indicator |
| `--border-strong` | `--surface` | ≈1.44 | — | Decorative field boundary, not a text pair |

Rule: status text uses `--ok-text`, `--bad` or `--warn` on white or its soft background. `--ok` itself is never text. Badges are soft fills; no white text on green.

## 3. Typography

| Role | Font | Fallback |
|---|---|---|
| Display and headings | Readex Pro 600/700 | Tahoma, sans-serif |
| Body and UI | Noto Sans Arabic 400/500/600 | Tahoma, sans-serif |
| Numbers in stats | Readex Pro 700, `letter-spacing: -0.02em` | |
| Code / LaTeX source | system monospace, `direction: ltr` | |

Both fonts are on Google Fonts and cover Arabic and Latin. Load with `display=swap`. Self-host in production.

### 3.1 Scale (mobile / desktop)

| Token | Size | Line height | Use |
|---|---|---|---|
| `display` | 36 / 44 px | 1.05 | Headline counter (from 900 px; `h1` below), landing hero |
| `h1` | 26 / 30 px | 1.2 | Page title |
| `h2` | 20 / 22 px | 1.3 | Section, card title |
| `h3` | 16 px | 1.4 | Sub-section |
| `body` | 16 px | 1.7 | Lesson text, question stem |
| `ui` | 15 px | 1.5 | Buttons, inputs, options |
| `caption` | 13 px | 1.5 | Meta, helper text |
| `micro` | 12 px | 1.4 | Badges, tab labels |

Arabic body text never goes below 15 px. Lesson explanation uses `body` at 1.8 line height.

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
| Spacing scale | 4, 8, 12, 16, 20, 24, 32, 40, 48 |
| Page gutter | 16 px mobile, 24 px desktop |
| Card padding | 16 px mobile, 20 px desktop |
| Content max width | 1040 px |
| App bar / tab bar height | 56 px |
| Auth column max | 440 px |
| `--r-sm` | 10 px (inputs, chips) |
| `--r-md` | 14 px (options, list items) |
| `--r-lg` | 18 px (cards) |
| `--r-pill` | 999 px (buttons, badges) |
| `--shadow-1` | `0 1px 2px rgba(30,27,75,.05), 0 8px 24px rgba(79,70,229,.08)` cards |
| `--shadow-2` | `0 4px 12px rgba(30,27,75,.08), 0 24px 48px rgba(79,70,229,.14)` sheets, dialogs, nav menu |

## 5. Components

### 5.1 Buttons

| Variant | Fill | Text | Border | Use |
|---|---|---|---|---|
| Primary | `--accent` (hover `--accent-hover`, pressed `--accent-pressed`) | white | none | One per screen: start quiz, submit, check |
| Accent | same as Primary since 2.0 | white | none | Subscribe, Ask a teacher |
| Secondary | `--surface` (hover and pressed `--soft`; toggled on `--accent-soft`) | `--text` (toggled `--accent`) | `--border-strong` (toggled `--accent`) | Everything else |
| Danger | `--surface` (hover `--bad-soft`) | `--bad` | `--border-strong` | Reject, delete, cancel subscription |
| Ghost | none | `--accent` | none | Inline text actions outside table rows (table row actions are secondary or danger `sm`) |

All pill-shaped, 44 px min height (36 px for `sm` table row actions in every table, student and admin: a documented exception to the 44 px rule, because rows are dense; adjacent actions keep adequate spacing per WCAG 2.5.8 Target Size (Minimum)), 15 px 600 weight. Size `icon` is 44 × 44 for icon-only buttons beside fields (offset under the field label so it centres on the field). Disabled: 45 percent opacity, and no hover fill. Focus: 2 px `--accent` ring offset 2 px. Cursor: pointer on enabled buttons, selects and `[role=button]`, not-allowed on disabled controls (a global base rule). Row actions in every table are `sm` (36 px): secondary for neutral actions, danger for destructive ones (same outline, only the text is red); a row link is a secondary `sm` button (`Button asChild`).

### 5.2 Cards

White, `--r-lg`, hairline border, `--shadow-1`. No coloured left borders. A card title is `h2`, meta is `caption` in `--text-2`.

### 5.3 Quiz option

Full-width label, `--r-md`, 48 px min height, 15 px text. The radio or checkbox uses `accent-color: var(--accent)`.

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

Pill, 12 px 600. `ok` = `--ok-soft` fill, `--ok-text` text. `bad` = `--bad-soft` fill, `--bad` text. `pending`/neutral = `--soft` fill, `--text-2` text. `role` = `--soft` fill, `--text` text. `v2` = `--v2` outline.

### 5.6 Progress

Track `--soft`, fill `--accent` (mastery) or `--ok` (exam pass). Height 6 px, fully rounded. The headline counter is a white card with 12 px between rows: the counter sentence in H1 size below 900 px and Display from 900 px, a 6 px mastered-share bar, then three stat chips (seen, mastered, day streak): `--soft` pills with a caption label in `--text-2` and a 600 value. The admin dashboard uses the same 6 px bar in its bar lists (share of a total, funnel steps, success-rate rows).

### 5.7 Navigation

App bar: one row, 56 px tall, sticky, white with a bottom hairline, inside the same 1040 px container as the page, so the logo lines up with the page title. Order: logo (`--accent`, links home) · nav · role badge · display name · sign-out. Below 900 px the nav moves to the tab bar. From 900 px the nav sits inline; between 900 and 1199 px the display name is hidden and sign-out shows its icon only (it keeps its accessible name); from 1200 px both show. Below 900 px sign-out is 44 px tall like every touch control; from 900 px it is the 36 px `sm` size.

Top nav (desktop ≥ 900 px): pills 36 px tall (with the 36 px sign-out, one of the two exceptions to principle 5, the other being 36 px `sm` table row actions: both nav controls exist at this size only in the pointer layout, and below 900 px navigation is the tab bar, whose items are taller than 44 px); inactive `--text-2`, hover `--bg` fill with `--text`, active `--accent-soft` fill with `--accent` 600. Each role has a fixed top-bar list: student all 6, teacher all 4, admin dashboard, content, questions and users. The admin's other destinations sit in an «المزيد» disclosure menu at the end of the nav: white, hairline border, `--shadow-2`, `--r-md`, items at least 44 px, closes on Esc, outside click, keyboard focus leaving the menu, or choosing an item.

Mobile tab bar: at most 4 items, icons 22 px stroke 1.8, active `--accent` 600 with the icon on an `--accent-soft` pill, inactive `--text-2`. A role with more than 3 destinations shows its 3 primary destinations plus a fourth item "المزيد" that opens a list of the rest. Bottom padding follows the device safe area (iOS home indicator).

Brand bar: the same app bar with only the logo (and an optional end slot, such as the landing page's sign-in button) on the landing page, login, sign-up, accept-invite and onboarding. The auth forms sit in a centred column at most 440 px wide under it.

Pill tabs (filters, sub-tabs, segmented toggles): inactive white with `--border-strong`, hover `--soft`, active `--accent-soft` fill with `--accent` border and text.

### 5.8 Inputs

White, `--r-sm`, `--border-strong`, 44 px height, 15 px. Focus ring as buttons. Labels 13 px `--text-2` above the field. Select: native appearance removed, a 16 px Lucide chevron-down in `--text-2` sits in a 40 px box at inline-end, padding-inline-end 40 px, not mirrored (`shared/ui/select.tsx`). A group of choices is a fieldset whose legend is an ordinary label inside the group (15 px 600 in a card, 13 px `--text-2` in a form), never drawn on the group's border.

### 5.9 Tables (admin, teacher)

White card, no vertical rules, row separator hairline, header `caption` weight 600 `--text-2`, 13.5 px cells. Sticky header on desktop, horizontal scroll inside the card on mobile. Row actions are `sm` buttons (see 5.1). Activity dates are relative under 24 hours with the full date on hover.

### 5.10 Avatar panel

Slide-in sheet from the start edge, `--surface`, `--shadow-2`, `--r-lg` on the outer corners. Student bubbles `--soft`; assistant bubbles white with hairline border. A small sparkle icon in `--accent` marks the assistant. The floating assistant button is an `--accent` pill with a white label. Full page (`/student/assistant`): the list and the chat are white cards (`--r-lg`, `--shadow-1`) in a 1 : 2 grid from 900 px; the current chat item is `--accent-soft` with an `--accent` border; the bubbles are the panel's.

### 5.11 Dialogs

Centered, max 420 px, `--r-lg`, `--shadow-2`, overlay `rgba(30,27,75,.40)`. The overlay and dialog stack above the app bar and tab bar.

### 5.12 Math input

The math-with-steps answer ([math-input.md](math-input.md)). Each LaTeX field is white, `--r-sm`, `--border-strong`, in the system monospace at 16 px (not the 12 px mono size: smaller text makes iOS zoom on focus), `direction: ltr`. A preview box sits under each field: white, hairline border, `--r-sm`, at least 44 px tall, with horizontal scroll inside. The touch keypad is a 6×6 grid of keys, each white, `--r-sm`, `--border-strong` and at least 44 px tall, on a `--soft` panel with `--r-md`, laid out left to right. It opens under the active field. Step rows are white `--r-md` hairline cards with icon buttons for move up, move down and remove (Danger), each at least 44 px.

### 5.13 Diagram canvas

The drag-and-drop diagram ([question-schemas.md](question-schemas.md)). The image scales to the card width with its aspect ratio reserved. Zones are rectangles with a 2 px non-scaling stroke, and each zone has a number badge (white circle, text colour) at its top-left. Edit mode uses an Accent stroke with a 15 % Accent fill; student mode uses a muted dashed stroke on a 60 % white fill; answer-key mode uses a Success stroke with a 20 % Success fill. A zone being drawn is dashed Accent. The canvas is never mirrored in RTL: image coordinates are physical. Items are pill chips: white, `--border-strong`.

Student answer mode: zones are buttons. An empty zone has a muted dashed stroke on a 60 % white fill; a zone holding items has a solid text-colour stroke; the drop target (dragged over, or focused while an item is chosen) has an Accent stroke with a 15 % Accent fill. Each zone shows its number badge and, when it holds items, a `count/capacity` pill (`--accent` fill). A zone list under the image (white cards, `--r-md`, hairline border) holds each zone's chips, with ghost icon buttons of at least 44 px (earlier, later, return to the bank) and «ضعه هنا» (secondary, small) while an item is chosen. Chips are pills of at least 44 px, white with `--border-strong`; a chosen chip has the `--accent-soft` fill and an `--accent` border; the chip being dragged follows the pointer with `--shadow-2`. After checking, a correct item has the Success soft fill, a Success border and a check icon; a wrong or unplaced item has the Danger soft fill, a Danger border and an x icon, each with its text for screen readers. The correct placements reuse answer-key mode.

### 5.14 Dashboard

Admin dashboard (`/admin`): a row of eight KPI tiles (2 columns, 4 from 700 px): a caption label, one `stat` number, one caption detail (the in-period delta when the API has one, never coloured) and an optional note. Below, eight panels (1 / 2 / 3 columns) of metric rows (caption label in `--text-2` at inline-start, 600-weight value at inline-end, hairline between rows; an overdue value is `--bad`) and bar lists (label and value over a 6 px progress bar, scaled to the total or the first funnel step). Daily charts: `--accent` bars, `--border` gridlines at 0, ½ and the rounded maximum with compact labels, a baseline tick for every day of the period, at most 4 date labels anchored on the latest day, a total and peak line, left-to-right in both languages, an sr-only data table, and the empty message centred in a 160 px box.

### 5.15 Compact list

One white card, hairline between rows, 12 px vertical row padding. Each row: title (`ui` 600) and caption meta at inline-start; a caption value over a 6 px bar (112 px wide from 700 px, full remaining width below); a 36 px `sm` secondary action at inline-end. Below 700 px the value, bar and action form a second line. The first 3 rows show, then a ghost «عرض الكل (N)» / «عرض أقل» with `aria-expanded`.

## 6. Layout and RTL

- `dir="rtl"` on `html`. Logical properties only (`margin-inline-start`, `padding-inline`), never left/right.
- Icons that imply direction (back, next) are mirrored in RTL.
- LaTeX, code, URLs and phone numbers are wrapped in `direction: ltr; unicode-bidi: isolate`.
- Breakpoints: 700 px (tablet grid), 900 px (admin two-column editor, nav moves into the app bar), 1200 px (app bar shows the display name and sign-out label).

## 7. Motion

- Durations: 150 ms for hover and focus, 220 ms for panels and dialogs, 400 ms for progress bar fill.
- Easing `cubic-bezier(.2,.8,.2,1)`. Respect `prefers-reduced-motion`.
- Correct answer: feedback panel fades in and the option border transitions. No confetti.

## 8. Iconography

Stroke icons, 1.8 px, round caps, 22 px in navigation and 16 px inline. Lucide set. No emoji anywhere in product UI.

## 9. Do and don't

- Do keep one primary button per screen.
- Do use white cards on the grey ground, never grey cards on white.
- Don't tint lesson content areas.
- Don't use the gradient outside landing and subscribe.
- Don't colour text for emphasis. Use weight.
- Don't add a dark mode. Light only is a product decision.

## 10. Implementation notes

- Tailwind CSS v4, CSS-first: `web/src/styles/tokens.css` is generated from `.claude/design-system.md` by `npm run gen:tokens` (never hand-edited) and mapped to utilities with `@theme inline` in `web/src/styles/app.css`. There is no `tailwind.config.*`.
- shadcn/ui: override `--radius` to 14 px; `--primary` maps to the accent (`--accent`). Replace default Inter with the two fonts.
- The prototype in `prototype/` remains the reference for screen content and flow.
