# Design system — Elmanhg (المنهج) · "Glass"

Owner: product (Mohamed). Engineers do not edit values here; they reference tokens. Human-readable rationale lives in `docs/design-system.md`; this file is the token contract the pipeline reads. Both must agree (docs-sync rule).

Source of truth: `docs/design-system.md` v1.0 (2026-09-26). No Figma. Screen content, flow and states come from `prototype/`; the look comes only from this file.
Mode: **light only**. There is no dark theme and no `.dark` override block.

## Tokens

### Colour
| Token | CSS var | Value | Use |
|-------|---------|-------|-----|
| color.bg | --ds-color-bg | #F5F5F7 | page ground. Never pure white for the page |
| color.surface | --ds-color-surface | #FFFFFF | cards, sheets, inputs, app bar |
| color.soft | --ds-color-soft | #EDEDF0 | selected option fill, muted chips, progress track |
| color.text | --ds-color-text | #1D1D1F | primary text, primary button fill |
| color.text.muted | --ds-color-text-muted | #6E6E73 | secondary text, labels, captions |
| color.border | --ds-color-border | rgba(0,0,0,.06) | hairline on cards |
| color.border.strong | --ds-color-border-strong | rgba(0,0,0,.14) | inputs, option outlines |
| color.accent | --ds-color-accent | #0071E3 | links, focus ring, progress fill, Ask a Teacher, active tab |
| color.accent.soft | --ds-color-accent-soft | #E8F1FC | accent chip background |
| color.success | --ds-color-success | #34A853 | correct, mastered, approved |
| color.success.soft | --ds-color-success-soft | #E9F6EC | correct feedback background |
| color.danger | --ds-color-danger | #E5484D | wrong, rejected, overdue, destructive |
| color.danger.soft | --ds-color-danger-soft | #FDECEC | wrong feedback background |
| color.warning | --ds-color-warning | #B7791F | partial credit, pending review |
| color.warning.soft | --ds-color-warning-soft | #FFF6E5 | partial feedback background |
| color.v2 | --ds-color-v2 | #6A3FB5 | "v2" badge only |
| color.overlay | --ds-color-overlay | rgba(29,29,31,.35) | dialog backdrop |
| gradient.aurora | --ds-gradient-aurora | linear-gradient(135deg,#7C5CFF 0%,#C86DD7 50%,#FFB07A 100%) | landing hero + subscribe header ONLY |

### Typography
| Token | Family | Size / line (mobile · desktop) | Weight | Use |
|-------|--------|-------------------------------|--------|-----|
| type.display | Readex Pro | 36/38 · 44/46, tracking -0.02em | 700 | headline counter, landing hero |
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

### Radius · Elevation · Motion
| Token | Value |
|-------|-------|
| radius.sm | 10 (inputs, chips) |
| radius.md | 14 (quiz options, list items, feedback panel) |
| radius.lg | 18 (cards, sheets, dialogs) |
| radius.pill | 999 (buttons, badges, sub-tabs) |
| shadow.1 | 0 1px 2px rgba(0,0,0,.04), 0 8px 24px rgba(0,0,0,.06) — cards |
| shadow.2 | 0 4px 12px rgba(0,0,0,.08), 0 24px 48px rgba(0,0,0,.10) — sheets, dialogs, floating assistant button |
| motion.fast | 150ms cubic-bezier(.2,.8,.2,1) — hover, focus |
| motion.base | 220ms cubic-bezier(.2,.8,.2,1) — panels, dialogs |
| motion.slow | 400ms cubic-bezier(.2,.8,.2,1) — progress fill |

## Components
| Component | Variants | States | Notes |
|-----------|----------|--------|-------|
| Button | primary (text fill, white label), accent (accent fill), secondary (surface + border.strong), danger (surface, danger text + border), ghost (accent text) | default, hover, focus, pressed, disabled (45% opacity), loading | pill, min height 44 (36 for `sm` in dense admin tables), padding-inline 18 (12 sm), type.ui 600. One primary per screen. |
| Card | default | default, hover (border.strong) for clickable cards | surface, border, shadow.1, radius.lg. No coloured side borders ever. |
| QuizOption | radio, checkbox | default, hover (soft), selected (soft fill + text border), correct (success.soft + success), wrong (danger.soft + danger), disabled | full-width label, min height 48, radius.md, padding 12×14, gap 10, control 18px with accent-color text |
| FeedbackPanel | correct, wrong, partial | enter (fade, motion.base) | soft bg + strong border of the verdict colour, 26px filled circle icon, bold verdict, explanation in text.muted |
| Badge | ok, bad, pending, role, v2, neutral | — | pill, type.micro, padding 2×10. ok/bad = fill + white text; pending/neutral = soft + text.muted; role = text fill; v2 = outline |
| Progress | mastery (accent), pass (success) | — | height 6, track soft, radius.pill, fill animates motion.slow |
| Input / Select / Textarea | default | default, focus (2px accent ring, offset 2), error (danger border + caption), disabled | surface, border.strong, radius.sm, height 44, padding 9×12, label type.caption above with gap 6 |
| TabBar (mobile) | — | active (text, 600), inactive (text.muted) | 4 items, Lucide icons 22px stroke 1.8, label 11px, surface + top hairline |
| TopTabs (desktop) | — | active (2px accent underline), inactive | text tabs |
| SubTabs | — | active (surface + border.strong), inactive | pills, padding 7×14 |
| Table | — | row hover (soft) | inside a Card, no vertical rules, row hairline, header type.caption 600 text.muted, cell 13.5px, padding 9×10, sticky header on desktop, horizontal scroll inside the card on mobile |
| AssistantSheet | — | open, closed | slides from inline-start, max 380, shadow.2, outer corners radius.lg; user bubble soft; assistant bubble surface + border; sparkle icon in accent |
| AssistantFab | — | default, hover | pill, text fill, white label, shadow.2, min height 48, fixed bottom inline-start |
| Dialog | default, confirm, destructive | open | centred, max 420, radius.lg, padding 20, shadow.2, backdrop color.overlay |
| ExamTimer | normal, urgent (last 2 min → danger) | — | sticky Card under app bar, radius.md, type.stat 18px |
| KpiCard | — | — | Card with type.caption label, type.stat value, type.caption sub-line |
| EmptyState | no-data, no-results | — | icon, one line, primary CTA; no-results offers "مسح الفلاتر" |
| Skeleton | — | loading | soft blocks with radius of the element they replace |

## Rules
1. Blue = action or link. Green = correct. Red = wrong. Amber = partial / pending. Nothing else is coloured.
2. Success and danger are used as text on their soft background for body sizes; as fills with white text only for badges 12px bold or larger.
3. The aurora gradient appears only on the landing hero and the subscribe screen header. Never on lesson, quiz, exam, teacher or admin screens.
4. Lesson content areas are pure surface white with no tint.
5. White cards on the grey ground, never grey cards on white.
6. Emphasis by weight, never by colour.
7. Every visual value in code is a token. A literal `#hex`, `px` size, radius or shadow outside `tokens.css` is a blocking finding.
8. No emoji in UI. Icons are Lucide, stroke 1.8, round caps.
9. Light only. No dark mode, no `prefers-color-scheme: dark` styles.
10. Honour `prefers-reduced-motion`: all transitions off.

## Breakpoints & layout
| Token | Value | Behaviour |
|-------|-------|-----------|
| bp.base | 0–699 | single column, bottom TabBar, gutter 16 |
| bp.md | ≥700 | card grids `repeat(auto-fill, minmax(280px, 1fr))`, gap 12 |
| bp.lg | ≥900 | two-column editors (1.2fr / 1fr), TopTabs instead of TabBar |
| layout.max | 1040 | content centred |

Verified widths: 375, 390, 768, 1280. No horizontal page scroll at any width.

## Accessibility requirements (WCAG 2.2 AA)
| Pair | Ratio |
|------|-------|
| text on surface | 16.1:1 |
| text on bg | 14.6:1 |
| text.muted on surface | 5.2:1 |
| text.muted on bg | 4.7:1 |
| white on accent | 4.6:1 |
| white on success | 3.3:1 → badges ≥12px bold only |
| white on danger | 3.9:1 → badges ≥12px bold only |
| success on success.soft | 5.1:1 |

Touch targets ≥44px. Focus visible on every interactive element (2px accent ring, offset 2). Real `<button>`, `<a href>`, `<label>` + `<input>`; never click handlers on divs. Icon-only buttons carry `aria-label`.

## RTL rules (Arabic)
- `<html lang="ar" dir="rtl">`. Logical properties only (`margin-inline-start`, `padding-inline`, `inset-inline-start`); `left`/`right` are findings.
- Direction-implying icons (back, next, chevrons) mirror in RTL.
- LaTeX, code, URLs, phone numbers, emails: `dir="ltr"` + `unicode-bidi: isolate`.
- Digits: Arabic-Indic (`ar-EG`) in student-facing UI; Latin (`ar-EG-u-nu-latn`) in admin tables, exports and anything copied into formulas. Never mixed within one string.

## Token → code mapping
- `src/styles/tokens.css` is generated from the tables above: every token becomes `--ds-<group>-<name>` on `:root` (dots → dashes). No `.dark` block.
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
    --color-success: var(--ds-color-success);  --color-success-soft: var(--ds-color-success-soft);
    --color-danger: var(--ds-color-danger);    --color-danger-soft: var(--ds-color-danger-soft);
    --color-warning: var(--ds-color-warning);  --color-warning-soft: var(--ds-color-warning-soft);
    --color-v2: var(--ds-color-v2);
    --font-display: "Readex Pro", Tahoma, sans-serif;
    --font-sans: "Noto Sans Arabic", Tahoma, sans-serif;
    --radius-sm: 10px; --radius-md: 14px; --radius-lg: 18px;
    --shadow-1: var(--ds-shadow-1); --shadow-2: var(--ds-shadow-2);
  }
  ```
- shadcn/ui: `--radius: 14px`; `--primary` → color.text, `--ring` → color.accent, `--destructive` → color.danger, `--background` → color.bg, `--card` → color.surface, `--muted` → color.soft, `--muted-foreground` → color.text.muted, `--border` → color.border.strong.
- Aurora is only available as the `.bg-aurora` utility defined in `app.css`, used by exactly two components (LandingHero, SubscribeHeader).

## Change log
| Version | Date | Change |
|---------|------|--------|
| 1.0 | 2026-09-26 | Glass adopted (light only, aurora on landing + subscribe) |
