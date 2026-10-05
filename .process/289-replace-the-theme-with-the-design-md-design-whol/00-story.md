# [E19.S5] Replace the theme with the DESIGN.md design (whole app)

Issue: #289

Epic: #275

As every user, I want the whole app to use the new design from DESIGN.md: a vivid purple-to-blue gradient hero, mint pill primary buttons, Signal Blue brand accents, circles as the signature shape, and white cards on a pale blue "mist" canvas.

**Dev decision (2026-10-05):** "it seems really perfect, go ahead and do replace the entire theme with this new design." This replaces Indigo calm (#276) across the whole app: student, teacher, admin and public pages. The approved visual reference is the sample `.process/design-sample-busuu.html` (student home), built from DESIGN.md.

**Source:** DESIGN.md, copied into the repo as `docs/design-source.md`, the reference the design-system docs are rewritten from.

**Tokens:**
- canvas `#f2f7fd`, paper `#ffffff`
- charcoal text `#252b2f`, slate caption `#666e7e`
- steel border `#d6dee6`
- Signal Blue `#116eee`, Spring Mint `#11ee92`
- hero gradient `linear-gradient(90deg, #3b1e90 0%, #5a3cc4 30%, #3a6ef0 100%)`
- radii: cards 16, inputs 5, buttons 45 (pill), circles 9999
- one shadow: `rgba(0,0,0,.1) 0 1px 2px 0`
- type scale: 10/12/14/16/18/24/36/40; weights 400/700/800
- page max width 1200, section gap 64, card padding 20

**Resolved contradictions in DESIGN.md:**
- **Primary button:** mint fill with charcoal text, weight 700, pill. It is the only filled chromatic action, one per screen. The component spec, the Do's and the signature choices win over the two lines that say otherwise.
- **Secondary button:** Signal Blue outline pill.
- **Tertiary button:** ghost (charcoal text).
- **Destructive actions:** DESIGN.md has no danger colour. Keep a danger outline or ghost with red text for destructive actions only, as an accessibility and safety exception. Keep the status colours for correct, wrong and partial feedback in quizzes, and for state badges, as documented exceptions with AA contrast.
- **Gradient typo:** `#3b1e9` in DESIGN.md means `#3b1e90`.

**Font:** Nista is not available. Use Poppins, the substitute DESIGN.md names, for Latin. Poppins has no Arabic glyphs, so pair it with a geometric Arabic font that matches its character. Self-host both, with weights 400/700/800. Justify the Arabic choice in the plan; the old ban on Cairo and Tajawal is replaced by this decision.

**Signature shapes:**
- circles for subject, unit and icon containers and avatars;
- pill buttons and tabs;
- 16 px cards;
- a flat look with only the single subtle shadow.

**Gradient hero:**
- used on the landing hero and on the student home headline banner;
- allowed sparingly for other "energetic moment" banners, such as subscribe, and exam results if appropriate.

**Data-heavy pages:** admin and teacher tables, and the dashboard, keep tables and charts. Apply the tokens (cards, borders, type, pills), and document "admin data views" as an allowed exception to the "no dense tables" rule.

**Kept from earlier decisions:**
- Latin digits;
- the RTL and ar/en rules;
- the shared components (`Button`, `Select`, the table actions, the option cards from #286, the assistant page from #288);
- the bundle budgets: the quiz, lesson, entry and landing budgets may not be raised. The fonts are self-hosted and loaded with `font-display: swap`.

**Docs-sync:**
- rewrite `.claude/design-system.md` and `docs/design-system.md` (they must agree);
- update `docs/claude-design-prompt.md` §2–6 and `docs/prototype.md`;
- add `docs/design-source.md`.

### Sub-tasks
- [ ] Copy DESIGN.md into `docs/design-source.md`; rewrite the design-system docs from it, with the resolved contradictions and documented exceptions
- [ ] Tokens, fonts (Poppins and the Arabic pairing) and the Tailwind theme; one shadow; the radii
- [ ] Components: buttons (mint, outline, ghost, danger), inputs and selects (5 px radius, steel border), cards, tabs and pills, nav bar (sticky white, blue logo), badges, option cards, dialogs
- [ ] Gradient hero on landing and on the student home headline; circle tiles for subjects and units
- [ ] Re-audit the layout at 375/768/1280 in ar and en for all roles; AA contrast table; tests updated; budgets green

