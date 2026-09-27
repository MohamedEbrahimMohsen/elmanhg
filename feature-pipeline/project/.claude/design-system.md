# Design system — <product name>

Owner: UI/UX team. Engineers do not edit values here; they reference tokens. If a Figma frame uses a value that is not in this file, that is a question for the UI/UX team, not a new literal in code.

Source of truth in Figma: <link to the Figma library file>
Last synced: <date>

## Tokens

### Colour
| Token | Value | Use |
|-------|-------|-----|
| color.primary | #4B2A8A | primary actions, active states |
| color.accent | #B5E31C | highlights, success emphasis |
| color.surface | #FFFFFF | page and card background |
| color.surface.alt | #F7F5FB | secondary background |
| color.text | #1D1730 | body text |
| color.text.muted | #6B6480 | captions, placeholders |
| color.border | #C9C2DA | dividers, input borders |
| color.danger | #C0392B | errors, destructive |
| color.warning | #C98A1C | warnings |
| color.success | #2E8B57 | success |

### Typography
| Token | Family | Size / line | Weight | Use |
|-------|--------|-------------|--------|-----|
| type.display | <Latin family> / <Arabic family> | 32 / 40 | 600 | page titles |
| type.h1 | | 24 / 32 | 600 | section titles |
| type.h2 | | 20 / 28 | 600 | card titles |
| type.body | | 16 / 24 | 400 | body |
| type.body.sm | | 14 / 20 | 400 | secondary |
| type.caption | | 12 / 16 | 500 | labels, uppercase with 0.06em tracking |

### Spacing (4-pt grid)
| Token | px |
|-------|----|
| space.xs | 4 |
| space.sm | 8 |
| space.md | 16 |
| space.lg | 24 |
| space.xl | 32 |
| space.2xl | 48 |

### Radius · Elevation · Motion
| Token | Value |
|-------|-------|
| radius.sm | 4 |
| radius.md | 8 |
| radius.lg | 16 |
| radius.pill | 999 |
| shadow.card | 0 1px 3px rgba(29,23,48,.12) |
| motion.fast | 120ms ease-out |
| motion.base | 200ms ease-out |

## Components
One row per component in the Figma library. The plan maps every element in a frame to one of these; anything that does not map is a question for the UI/UX team.

| Component | Variants | States | Notes |
|-----------|----------|--------|-------|
| Button | primary, secondary, ghost, danger | default, hover, pressed, disabled, loading | min height 44 |
| Input | text, number, password, search | default, focus, error, disabled | label above, helper/error below |
| Select | single, multi | | |
| Card | default, interactive | | |
| Table | | loading, empty, error | |
| Empty state | | | illustration + one line + one action |
| Toast | success, error, info | | |
| Modal | sm, md, lg | | |

## Rules
- RTL first: Arabic and English are both first-class. Logical properties only.
- Every list/detail screen has loading, empty, and error states. Figma has a frame for each; code has a branch for each.
- Minimum tap target 44×44 (web) / 48×48 (mobile).
  → 2026-09 update: WCAG 2.2 floor is 24×24 CSS px for every target (2.5.8); team default stays 44×44 for primary web actions, 44×44 pt iOS, 48×48 dp Android.
- Contrast ≥ 4.5:1 for text.
  → 2026-09 update: also ≥ 3:1 for large text (≥ 24 px, or ≥ 18.66 px bold) and for UI component boundaries, focus rings and meaningful icons (WCAG 1.4.11). Checked for light and dark sets.
- Where the tokens live in code: web `web/src/styles/tokens.css` + `tailwind.config.ts` · flutter `mobile/lib/core/theme/app_tokens.dart` · kmp `shared/ui/theme/AppTheme.kt`. Those files are generated from this document; do not hand-edit them.
  → 2026-09 update: web has no `tailwind.config.ts` (Tailwind v4 is CSS-first). Tokens live in `web/src/styles/tokens.css` (`--ds-*` variables) and are mapped to utilities by `@theme inline` in the Tailwind entry CSS. See "Token → code mapping" below.

---

# Extended template (fill every `<…>`; leave a row blank only if the product does not use it)

Token naming: `<category>.<role>[.<variant>][.<state>]`, lowercase, dot-separated. Code names are derived mechanically: CSS `--ds-color-primary-hover`, Dart `tokens.color.primaryHover`, Kotlin `tokens.color.primaryHover`.
Values: colours in hex or OKLCH; sizes in px (web) = logical px (Flutter) = dp (Compose); font sizes in px = sp.

## Colour roles (light / dark)

| Token | Light | Dark | Use | Contrast pair (≥ ratio) |
|-------|-------|------|-----|------------------------|
| color.bg | <…> | <…> | page background | — |
| color.surface | <…> | <…> | cards, sheets | — |
| color.surface.raised | <…> | <…> | menus, dialogs | — |
| color.fg | <…> | <…> | body text | on color.bg ≥ 4.5 |
| color.fg.muted | <…> | <…> | secondary text | on color.bg ≥ 4.5 |
| color.fg.disabled | <…> | <…> | disabled text | exempt (disabled) |
| color.primary | <…> | <…> | primary action fill | — |
| color.primary.fg | <…> | <…> | text/icon on primary | on color.primary ≥ 4.5 |
| color.primary.hover | <…> | <…> | hover fill | — |
| color.primary.pressed | <…> | <…> | pressed fill | — |
| color.secondary / .fg | <…> | <…> | secondary action | ≥ 4.5 |
| color.border | <…> | <…> | dividers, input borders | on bg ≥ 3 (inputs) |
| color.focus.ring | <…> | <…> | focus indicator | on adjacent bg ≥ 3 |
| color.danger / .fg / .subtle | <…> | <…> | errors, destructive | ≥ 4.5 text |
| color.warning / .fg / .subtle | <…> | <…> | warnings | ≥ 4.5 text |
| color.success / .fg / .subtle | <…> | <…> | success | ≥ 4.5 text |
| color.info / .fg / .subtle | <…> | <…> | info | ≥ 4.5 text |
| color.overlay | <…> | <…> | modal scrim | — |
| color.skeleton | <…> | <…> | loading placeholders | — |

Rules: every `fg` token lists its background pair and the measured ratio. Dark mode is a full second set, not an automatic inversion. Status is never conveyed by colour alone (icon or text too).

## Typography scale

| Token | Latin family | Arabic family | Size / line-height | Weight | Letter-spacing | Use |
|-------|--------------|---------------|--------------------|--------|----------------|-----|
| type.display | <…> | <…> | <…> | <…> | <…> | hero / page title |
| type.h1 | | | | | | page heading (one per page) |
| type.h2 | | | | | | section |
| type.h3 | | | | | | card / group title |
| type.body.lg | | | | | | lead text |
| type.body | | | | | | default |
| type.body.sm | | | | | | secondary |
| type.label | | | | | | form labels, buttons |
| type.caption | | | | | | helper text, meta |
| type.mono | | — | | | | code, IDs |

Rules: Arabic line-height ≥ Latin line-height for the same token (Arabic needs more vertical room). No letter-spacing on Arabic text. Minimum body size 14 (web px / sp). Sizes scale with user text settings (rem on web, sp on Compose, `TextScaler` on Flutter).
Digits: <Latin | Arabic-Indic> in Arabic UI (web locale `ar-<region>` or `ar-<region>-u-nu-latn`).

## Spacing scale (4-pt grid)

| Token | Value | Typical use |
|-------|-------|-------------|
| space.0 | 0 | — |
| space.1 | 4 | icon–text gap |
| space.2 | 8 | tight stacks |
| space.3 | 12 | input padding |
| space.4 | 16 | card padding, default gap |
| space.6 | 24 | section gap |
| space.8 | 32 | page gutter (desktop) |
| space.12 | 48 | large section |
| space.16 | 64 | hero |

(Map the existing `space.xs…2xl` names above to these; keep one set in code.)

## Radius

| Token | Value | Use |
|-------|-------|-----|
| radius.none | 0 | tables, full-bleed |
| radius.sm | <…> | chips, inputs |
| radius.md | <…> | buttons, cards |
| radius.lg | <…> | sheets, dialogs |
| radius.full | 9999 | avatars, pills |

## Elevation

| Token | Web shadow | Flutter elevation | Compose `Dp` | Use |
|-------|-----------|-------------------|-------------|-----|
| elevation.0 | none | 0 | 0 | flat |
| elevation.1 | <…> | 1 | 1 | cards |
| elevation.2 | <…> | 3 | 3 | menus, popovers |
| elevation.3 | <…> | 6 | 6 | dialogs, sheets |

Dark mode: elevation shown by lighter surface (`color.surface.raised`), not stronger shadow.

## Motion

| Token | Duration | Easing | Use |
|-------|----------|--------|-----|
| motion.instant | 0 | — | reduced-motion replacement |
| motion.fast | <…> | <…> | hover, press |
| motion.base | <…> | <…> | expand, fade |
| motion.slow | <…> | <…> | page, sheet |

Rules: every animation has a reduced-motion variant (`prefers-reduced-motion` / `MediaQuery.disableAnimations` / platform setting) that uses `motion.instant` or a fade. Nothing flashes more than 3 times per second.

## Breakpoints & layout

| Token | Min width | Columns | Gutter | Margin | Verified in E2E |
|-------|-----------|---------|--------|--------|-----------------|
| bp.sm (mobile) | 0 | 4 | <…> | <…> | 390 |
| bp.md (tablet) | 768 | 8 | <…> | <…> | sanity |
| bp.lg (desktop) | 1024 | 12 | <…> | <…> | — |
| bp.xl (wide) | 1280 | 12 | <…> | <…> | 1280 |

Rules: no horizontal scroll at 320 wide (WCAG 1.4.10). Max content width: <…>.

## Iconography

- Set: <icon library / Figma icon page>. Sizes: `icon.sm` 16, `icon.md` 20, `icon.lg` 24 (touch area still ≥ target size).
- Stroke/fill style: <…>. Colour: inherits text colour token; never a raw colour.
- Directional icons (back/forward arrows, chevrons, "next", progress, reply) mirror in RTL. Never mirror: logos, media play/pause, checkmarks, clocks, charts' time axis, slashes in text.
- Icon-only controls have a localized accessible name. Decorative icons are hidden from assistive tech.

## Component inventory (states)

States every interactive component must specify in Figma: default · hover (web/desktop) · focus-visible · pressed · disabled · error · loading. Data components additionally: loading (skeleton) · empty · error · no-results.

| Component | Variants | default | hover | focus | pressed | disabled | error | loading | empty | Figma link | Code name (web / flutter / kmp) |
|-----------|----------|---------|-------|-------|---------|----------|-------|---------|-------|------------|-------------------------------|
| Button | primary, secondary, ghost, danger | ✓ | ✓ | ✓ | ✓ | ✓ | — | ✓ | — | <…> | `Button` / `AppButton` / `AppButton` |
| Icon button | | ✓ | ✓ | ✓ | ✓ | ✓ | — | ✓ | — | <…> | |
| Text input | text, number, password, search | ✓ | ✓ | ✓ | — | ✓ | ✓ | — | — | <…> | |
| Select / combobox | single, multi | ✓ | ✓ | ✓ | — | ✓ | ✓ | ✓ | ✓ | <…> | |
| Checkbox / radio / switch | | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — | — | <…> | |
| Date picker | | ✓ | ✓ | ✓ | — | ✓ | ✓ | — | — | <…> | |
| Card | default, interactive | ✓ | ✓ | ✓ | ✓ | — | — | ✓ | — | <…> | |
| Table / list | | ✓ | row ✓ | row ✓ | — | — | ✓ | ✓ | ✓ | <…> | |
| Tabs | | ✓ | ✓ | ✓ | ✓ | ✓ | — | — | — | <…> | |
| Dialog / sheet | sm, md, lg | ✓ | — | ✓ (trap) | — | — | — | ✓ | — | <…> | |
| Toast / snackbar | success, error, info | ✓ | — | — | — | — | — | — | — | <…> | |
| Empty state | no-data, no-results | ✓ | — | — | — | — | — | — | ✓ | <…> | |
| Error state | inline, page | ✓ | — | ✓ (retry) | — | — | ✓ | — | — | <…> | |
| Skeleton | per data component | — | — | — | — | — | — | ✓ | — | <…> | |
| Navigation (top bar / side nav / bottom tabs) | | ✓ | ✓ | ✓ | ✓ | — | — | — | — | <…> | |

A ✓ means Figma has a frame and code has a branch. A row the product adds follows the same columns.

## Accessibility requirements (WCAG 2.2 AA)

| Requirement | Rule | Check |
|-------------|------|-------|
| Text contrast (1.4.3) | ≥ 4.5:1 normal, ≥ 3:1 large | ratio column in Colour roles, light and dark |
| Non-text contrast (1.4.11) | ≥ 3:1 for borders of inputs, focus ring, icons that carry meaning | ratio listed |
| Focus visible (2.4.7) + not obscured (2.4.11) | `color.focus.ring`, width <2> px, offset <2> px; sticky headers never cover the focused element | E2E keyboard step |
| Target size (2.5.8) | ≥ 24×24 CSS px all targets; team: 44 web primary / 44 pt iOS / 48 dp Android | component spec |
| Dragging (2.5.7) | every drag has a single-pointer/keyboard alternative | component spec |
| Reflow (1.4.10) | no horizontal scroll at 320 wide | E2E at 390 |
| Text spacing / scaling (1.4.4, 1.4.12) | layout holds at 200 % text | widget/UI test |
| Accessible authentication (3.3.8) | paste allowed, password managers supported, no cognitive puzzles | login spec |
| Consistent help (3.2.6) | help entry in the same place on every page | layout spec |
| Motion | reduced-motion variant for every animation | Motion table |
| Labels | every input has a visible label; placeholders are not labels | component spec |

## RTL rules (Arabic)

- Layout mirrors in RTL: navigation, reading order, progress, carousels, back/forward.
- Use start/end, never left/right, in specs and code (Figma annotations say "start/end").
- Numbers, phone numbers, IBANs, codes stay LTR inside RTL text (bidi isolation).
- Mixed Arabic/Latin strings: Arabic font token for Arabic glyphs; test with real Arabic copy, not lorem.
- Figma provides an RTL frame for every screen with directional content, or states "mirror of <frame>".

## Token → code mapping

| Token category | Web (Tailwind v4) | Flutter | Compose Multiplatform |
|----------------|-------------------|---------|----------------------|
| Colours | `tokens.css` `:root { --ds-color-primary: … }` + `.dark { … }`; `@theme inline { --color-primary: var(--ds-color-primary); }` → `bg-primary` | `ColorScheme` (M3 roles) + `ThemeExtension<AppTokens>` for extra roles; light/dark instances | `lightColorScheme(...)`/`darkColorScheme(...)` in `MaterialTheme` + `LocalAppTokens` for extra roles |
| Typography | `@theme { --font-sans: …; --text-body: …; --text-body--line-height: … }` → `text-body` | `TextTheme` from tokens; Arabic font via `fontFamilyFallback` or locale switch | `Typography(...)` with `FontFamily(Font(Res.font.…))` |
| Spacing | `@theme { --spacing: 0.25rem; }` → `p-4` = 16 | `AppTokens.space.*` (`double`) with `EdgeInsetsDirectional` | `AppTokens.space.*` (`Dp`) |
| Radius | `@theme inline { --radius-md: var(--ds-radius-md); }` → `rounded-md` | `BorderRadiusDirectional.circular(tokens.radius.md)` | `Shapes(medium = RoundedCornerShape(tokens.radius.md))` |
| Elevation | `@theme { --shadow-card: …; }` → `shadow-card` | `tokens.elevation.*` → `Material(elevation:)` | `tokens.elevation.*` → `Surface(shadowElevation =)` |
| Motion | `@theme { --ease-standard: …; }` + `--ds-duration-*` vars; `motion-safe:` | `tokens.motion.*` (`Duration`, `Curve`) | `tokens.motion.*` (`Int` ms, `Easing`) |
| Breakpoints | `@theme { --breakpoint-md: 48rem; }` → `md:` | `LayoutBuilder` thresholds from tokens | window size class thresholds from tokens |
| Dark mode | `@custom-variant dark (&:where(.dark, .dark *));` | `ThemeMode.system` + dark token set | `isSystemInDarkTheme()` + dark token set |

Generation: one script/tool (e.g. Style Dictionary or Figma Variables export) turns this file (or the Figma variables) into `tokens.css`, `app_tokens.g.dart`, `Tokens.kt`. Generated files carry a "do not edit" header. CI regenerates and fails on diff.

## Change log

| Date | Change | By |
|------|--------|----|
| <…> | <…> | <…> |
