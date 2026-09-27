# Elmanhg Design System — "Glass"

| | |
|---|---|
| Version | 1.0 |
| Date | 2026-09-26 |
| Decision | Glass direction for the whole app. Aurora gradient allowed only on the landing hero and the subscribe screen. |
| Mode | Light only. No dark theme. |
| Reference canvas | https://claude.ai/artifact/VHfyt2bTbtQJnA9Fg6Snpp (board 4, Glass) |

## 1. Principles

1. **Low glare.** The page ground is light grey, never pure white. White is reserved for cards, so contrast lands where reading happens.
2. **One colour per job.** Blue means "action or link". Green means "correct or mastered". Red means "wrong or overdue". Nothing else is coloured.
3. **Content is the hero.** Lesson text, equations and diagrams sit on white with no tint. Chrome stays grey.
4. **Calm surfaces.** Depth comes from soft shadows and hairline borders, not from colour blocks.
5. **Touch first.** Every control is at least 44 px tall. Phone width (375 px) is the primary layout.

## 2. Colour tokens

### 2.1 Core

| Token | Value | Use |
|---|---|---|
| `--bg` | `#F5F5F7` | Page background |
| `--surface` | `#FFFFFF` | Cards, sheets, inputs, app bar |
| `--soft` | `#EDEDF0` | Selected option fill, muted chips, progress track |
| `--text` | `#1D1D1F` | Primary text, primary buttons |
| `--text-2` | `#6E6E73` | Secondary text, labels, captions |
| `--border` | `rgba(0,0,0,.06)` | Hairline borders on cards |
| `--border-strong` | `rgba(0,0,0,.14)` | Inputs, selected option outline |

### 2.2 Semantic

| Token | Value | Use |
|---|---|---|
| `--accent` | `#0071E3` | Links, focus ring, progress fill, "Ask a teacher", tab active |
| `--accent-soft` | `#E8F1FC` | Accent chip background |
| `--ok` | `#34A853` | Correct, mastered, approved, SLA met |
| `--ok-soft` | `#E9F6EC` | Correct feedback background |
| `--bad` | `#E5484D` | Wrong, rejected, SLA breached, destructive |
| `--bad-soft` | `#FDECEC` | Wrong feedback background |
| `--warn` | `#B7791F` | Partial credit, pending review |
| `--warn-soft` | `#FFF6E5` | Partial feedback background |
| `--v2` | `#6A3FB5` | "v2" badge only (internal) |

### 2.3 Landing gradient (marketing surfaces only)

| Token | Value |
|---|---|
| `--aurora` | `linear-gradient(135deg, #7C5CFF 0%, #C86DD7 50%, #FFB07A 100%)` |

Allowed on: landing page hero background, subscribe screen header, marketing emails. Never inside lesson, quiz, exam, teacher or admin screens.

### 2.4 Contrast (verified)

| Pair | Ratio | Passes |
|---|---|---|
| `--text` on `--surface` | 16.1:1 | AAA |
| `--text` on `--bg` | 14.6:1 | AAA |
| `--text-2` on `--surface` | 5.2:1 | AA |
| `--text-2` on `--bg` | 4.7:1 | AA |
| White on `--accent` | 4.6:1 | AA |
| White on `--ok` | 3.3:1 | AA large only. Use `--ok` as text on `--ok-soft` for small text (5.1:1). |
| White on `--bad` | 3.9:1 | AA large only. Same rule as `--ok`. |

Rule: green and red are used as text on their soft backgrounds for body sizes, and as fills only for badges 12 px bold or larger.

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
| `display` | 36 / 44 px | 1.05 | Headline counter, landing hero |
| `h1` | 26 / 30 px | 1.2 | Page title |
| `h2` | 20 / 22 px | 1.3 | Section, card title |
| `h3` | 16 px | 1.4 | Sub-section |
| `body` | 16 px | 1.7 | Lesson text, question stem |
| `ui` | 15 px | 1.5 | Buttons, inputs, options |
| `caption` | 13 px | 1.5 | Meta, helper text |
| `micro` | 12 px | 1.4 | Badges, tab labels |

Arabic body text never goes below 15 px. Lesson explanation uses `body` at 1.8 line height.

### 3.2 Numerals

Arabic-Indic digits (٠١٢٣) in student-facing UI. ASCII digits in admin tables, exports and anything copied into formulas. Never mix within one string.

## 4. Spacing, radius, elevation

| Token | Value |
|---|---|
| Base unit | 4 px |
| Spacing scale | 4, 8, 12, 16, 20, 24, 32, 40, 48 |
| Page gutter | 16 px mobile, 24 px desktop |
| Card padding | 16 px mobile, 20 px desktop |
| Content max width | 1040 px |
| `--r-sm` | 10 px (inputs, chips) |
| `--r-md` | 14 px (options, list items) |
| `--r-lg` | 18 px (cards) |
| `--r-pill` | 999 px (buttons, badges) |
| `--shadow-1` | `0 1px 2px rgba(0,0,0,.04), 0 8px 24px rgba(0,0,0,.06)` cards |
| `--shadow-2` | `0 4px 12px rgba(0,0,0,.08), 0 24px 48px rgba(0,0,0,.10)` sheets, dialogs |

## 5. Components

### 5.1 Buttons

| Variant | Fill | Text | Border | Use |
|---|---|---|---|---|
| Primary | `--text` | white | none | One per screen: start quiz, submit, check |
| Accent | `--accent` | white | none | Subscribe, Ask a teacher |
| Secondary | `--surface` | `--text` | `--border-strong` | Everything else |
| Danger | `--surface` | `--bad` | `--bad` | Reject, delete, cancel subscription |
| Ghost | none | `--accent` | none | Inline actions in tables |

All pill-shaped, 44 px min height (36 px for `sm` in dense admin tables), 15 px 600 weight. Disabled: 45 percent opacity. Focus: 2 px `--accent` ring offset 2 px.

### 5.2 Cards

White, `--r-lg`, hairline border, `--shadow-1`. No coloured left borders. A card title is `h2`, meta is `caption` in `--text-2`.

### 5.3 Quiz option

Full-width label, `--r-md`, 48 px min height, 15 px text.

| State | Fill | Border |
|---|---|---|
| Default | `--surface` | `--border-strong` |
| Selected | `--soft` | `--text` |
| Correct (after check) | `--ok-soft` | `--ok` |
| Wrong (after check) | `--bad-soft` | `--bad` |

### 5.4 Feedback panel

Appears under the question after checking. Icon circle 26 px filled with `--ok` or `--bad`, bold verdict, explanation in `--text-2`. Background uses the matching soft colour, border the matching strong colour.

### 5.5 Badges

Pill, 12 px 600. `ok` = `--ok` fill white text. `bad` = `--bad` fill. `pending` = `--soft` fill `--text-2` text. `role` = `--text` fill. `v2` = `--v2` outline.

### 5.6 Progress

Track `--soft`, fill `--accent` (mastery) or `--ok` (exam pass). Height 6 px, fully rounded. Headline counter card is a white card with the number in `display` size.

### 5.7 Navigation

Mobile: bottom tab bar, 4 items, icons 22 px stroke 1.8, active in `--text` 600, inactive `--text-2`. Desktop: top bar with the same items as text tabs, active underlined 2 px `--accent`.

### 5.8 Inputs

White, `--r-sm`, `--border-strong`, 44 px height, 15 px. Focus ring as buttons. Labels 13 px `--text-2` above the field.

### 5.9 Tables (admin, teacher)

White card, no vertical rules, row separator hairline, header `caption` weight 600 `--text-2`, 13.5 px cells. Sticky header on desktop, horizontal scroll inside the card on mobile.

### 5.10 Avatar panel

Slide-in sheet from the start edge, `--surface`, `--shadow-2`, `--r-lg` on the outer corners. Student bubbles `--soft`; assistant bubbles white with hairline border. A small sparkle icon in `--accent` marks the assistant.

### 5.11 Dialogs

Centered, max 420 px, `--r-lg`, `--shadow-2`, overlay `rgba(29,29,31,.35)`.

## 6. Layout and RTL

- `dir="rtl"` on `html`. Logical properties only (`margin-inline-start`, `padding-inline`), never left/right.
- Icons that imply direction (back, next) are mirrored in RTL.
- LaTeX, code, URLs and phone numbers are wrapped in `direction: ltr; unicode-bidi: isolate`.
- Breakpoints: 700 px (tablet grid), 900 px (admin two-column editor).

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

- Tailwind config: map every token above to `theme.extend.colors`, `borderRadius`, `boxShadow`, `fontFamily`.
- shadcn/ui: override `--radius` to 14 px and the primary/accent CSS variables. Replace default Inter with the two fonts.
- The prototype in `prototype/styles.css` is the visual reference until the React app exists.
