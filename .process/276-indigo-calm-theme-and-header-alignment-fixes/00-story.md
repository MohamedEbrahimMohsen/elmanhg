# [E19.S1] Indigo calm theme and header/alignment fixes

Issue: #276

Epic: #275

As a student, teacher or admin I see a calm, colourful interface: indigo for actions and the active place, soft tinted backgrounds instead of near-black, and a tidy, aligned header and layout on every screen size.

Dev decisions (2026-10-03):
- "it's 100% black which really bad, plus there are many alignment issues such as in the nav bar."
- The dev picked theme **2. Indigo calm** from the 10 options in `.process/theme-options.html`.

**Theme tokens (Indigo calm)**
- **Base colours:** page bg `#F4F5FB` · surface `#FFFFFF` · soft `#E5E7FB` · text `#1E1B4B` · muted text `#5F5D7A`.
- **Accent and primary:** accent/primary `#4F46E5`, with a hover/pressed shade derived from it. The accent soft tint is lighter than soft.
- **Status colours:** success `#16A34A` on `#DFF3E5`. Tune danger, warning and their soft tints to sit with indigo.
- **Borders and shadows:** border tints come from the text colour, not pure black. Card shadows are tinted toward indigo.
- **Primary buttons:** filled with the accent (indigo) and white text, not near-black. The active tab and nav item use the soft indigo pill.
- **Contrast:** every text/background pair meets WCAG AA (4.5:1 body, 3:1 large and UI). Include the contrast table in `docs/design-system.md`.
- **Aurora gradient:** the landing hero and subscribe header keep a gradient, retuned to the indigo family.

**Header and alignment**
- **Desktop header, today:** two stacked rows (logo and user row, then a separate nav row), 101 px tall.
- **Desktop header, target:** one aligned row. Logo at the start (right in RTL), nav links next to it, user name and sign-out at the end. All items share one baseline and vertical centre, inside the same `max-w-layout` container as the page content.
- **Mobile:** the bottom tab bar (3 items plus "More") keeps its behaviour; check its icon and label alignment and safe-area padding.
- **Alignment audit:** check every main page at 375, 768 and 1280 px, in Arabic (RTL) and English (LTR), for each role: student, teacher and admin.
  - Look for mismatched start edges between header, page title and cards.
  - Look for inconsistent gaps, off-centre icons, and buttons and inputs of different heights in one row.
  - Look for misaligned table columns and overflow.
  - Fix what's found, and list each fix in the implementation report.
- **Login and signup:** add the app bar with the logo, and centre the auth card at a sensible max width.

### Sub-tasks
- [ ] New colour tokens in `.claude/design-system.md` and `docs/design-system.md` (they must agree), plus the Tailwind/CSS variables, with an AA contrast table
- [ ] Primary button and active-state components switched from near-black to indigo
- [ ] Single-row desktop header and the login/signup app bar; checks of the mobile tab bar
- [ ] Alignment audit and fixes across the student, teacher and admin pages at 375, 768 and 1280, in RTL and LTR
- [ ] Update `docs/claude-design-prompt.md` and `docs/prototype.md` where they describe colours or the header; keep `perf:budget` and the web tests green

